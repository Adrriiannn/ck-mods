using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Tracks whether a consumer's generated items actually made it into the game.
    ///
    /// PugMod registers a prefab under its <c>ObjectAuthoring.objectName</c>, which the framework
    /// sets to the authored item id. Resolution is asynchronous — ids are not available until the
    /// game has loaded mod content — so a consumer declares the ids it expects and the framework
    /// reports which ones never arrived. Without this, a mis-generated item is silently absent:
    /// no error, no object, and nothing to tell the creator which of their items failed.
    /// </summary>
    public static class DimensionItemObjectRegistry
    {
        private static readonly Dictionary<string, ObjectID> Resolved =
            new Dictionary<string, ObjectID>(StringComparer.Ordinal);

        /// <summary>Declared item id to the content pack that declared it.</summary>
        private static readonly Dictionary<string, string> Declared =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly HashSet<string> LoggedMissing =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Seam for tests, which cannot register real objects with the game. Null means "ask
        /// PugMod", which is always the case at runtime.
        /// </summary>
        private static Func<string, ObjectID> resolverOverride;

        /// <summary>Item ids that have resolved to a real object.</summary>
        public static int ResolvedCount
        {
            get { return Resolved.Count; }
        }

        /// <summary>How many times <see cref="Declare"/> has been called with something in it.</summary>
        /// <remarks>
        /// The mod entry watches this so its one-shot "did my items register?" report waits until
        /// declarations have stopped arriving. <c>ApplyManifests</c> emits the <c>Declare</c> call
        /// behind a service gate that retries across frames, so a report fired the first frame a
        /// world exists can easily run BEFORE anything has been declared — and then the ledger is
        /// never read at all.
        /// </remarks>
        public static int DeclarationVersion { get; private set; }

        /// <summary>Declared item ids that have not resolved yet.</summary>
        /// <remarks>
        /// Counted, not subtracted. <c>Resolved</c> is filled by every <see cref="TryResolve"/> call
        /// whether or not the id was ever declared, so <c>Declared.Count - Resolved.Count</c> goes
        /// negative the moment anything resolves an undeclared name — and then
        /// <see cref="IsComplete"/> answers true while declared items are genuinely missing.
        /// </remarks>
        public static int PendingCount
        {
            get { return GetPending().Count; }
        }

        /// <summary>True when every declared item has resolved.</summary>
        public static bool IsComplete
        {
            get { return PendingCount <= 0; }
        }

        /// <summary>
        /// Declares the item ids a content pack expects the game to register. Safe to call more
        /// than once; re-declaring an id keeps its existing resolution.
        /// </summary>
        public static void Declare(string contentPackId, IEnumerable<string> itemIds)
        {
            if (itemIds == null)
            {
                return;
            }

            string owner = string.IsNullOrEmpty(contentPackId) ? "<unknown>" : contentPackId;
            bool declaredAnything = false;
            foreach (string itemId in itemIds)
            {
                if (!string.IsNullOrEmpty(itemId))
                {
                    Declared[itemId] = owner;
                    declaredAnything = true;
                }
            }

            if (declaredAnything)
            {
                DeclarationVersion++;
            }
        }

        /// <summary>
        /// Resolves an item id to its in-game object, caching the result. Returns false while the
        /// game has not registered it yet, so callers can retry on a later frame.
        /// </summary>
        public static bool TryResolve(string itemId, out ObjectID objectID)
        {
            objectID = ObjectID.None;
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            if (Resolved.TryGetValue(itemId, out objectID) && objectID != ObjectID.None)
            {
                return true;
            }

            objectID = ResolveFromGame(itemId);
            if (objectID == ObjectID.None)
            {
                return false;
            }

            Resolved[itemId] = objectID;
            LoggedMissing.Remove(itemId);
            return true;
        }

        /// <summary>
        /// Re-checks every declared id that has not resolved yet. Call once content loading has
        /// finished; the count of still-missing ids is the answer to "did my items register?".
        /// </summary>
        public static int RefreshAll()
        {
            List<string> pending = GetPending();
            for (int i = 0; i < pending.Count; i++)
            {
                TryResolve(pending[i], out _);
            }

            return PendingCount;
        }

        /// <summary>Declared ids that still have no object, in declaration order.</summary>
        public static List<string> GetPending()
        {
            List<string> pending = new List<string>();
            foreach (KeyValuePair<string, string> declared in Declared)
            {
                if (!Resolved.ContainsKey(declared.Key))
                {
                    pending.Add(declared.Key);
                }
            }

            return pending;
        }

        /// <summary>
        /// Logs one line per item that never registered, naming the content pack that declared
        /// it, and stays quiet on repeat calls so a per-frame retry cannot spam the console.
        /// </summary>
        public static void ReportMissing()
        {
            List<string> pending = GetPending();
            for (int i = 0; i < pending.Count; i++)
            {
                string itemId = pending[i];
                if (!LoggedMissing.Add(itemId))
                {
                    continue;
                }

                Declared.TryGetValue(itemId, out string owner);
                // Said without a component name in it. This line reaches a person who has never
                // opened Unity, and "check that its ObjectAuthoring name matches the item id" is
                // not something they can act on — the generator writes that name, so if it is
                // wrong the answer is always to generate again.
                DimensionFrameworkLog.Warning(
                    "'" + owner + "' expects an item called '" + itemId +
                    "', and nothing in this world answers to it. Either that item is switched off " +
                    "in the dashboard, or it was renamed after this was built. Switch it back on, " +
                    "or fix the name, and generate again.");
            }
        }

        public static void Clear()
        {
            Resolved.Clear();
            Declared.Clear();
            LoggedMissing.Clear();
            DeclarationVersion = 0;

            // The stand-in lookup goes with everything else. Leaving it behind meant a test that
            // installed one and then called Clear left a fake resolver wired into a static that
            // production code reads.
            resolverOverride = null;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Replaces the game lookup. Tests use this because they cannot register real objects;
        /// passing null restores the real PugMod lookup.
        /// </summary>
        /// <remarks>
        /// Compiled only where tests are, so a shipped build has no way to replace the lookup the
        /// whole framework resolves names through.
        /// </remarks>
        internal static void SetResolverForTesting(Func<string, ObjectID> resolver)
        {
            resolverOverride = resolver;
        }
#endif

        /// <remarks>
        /// Routed through the framework's one resolver so a VANILLA name answers here too. Asking
        /// <c>API.Authoring</c> alone means every one of the game's own names comes back as
        /// <c>None</c> outside a loaded game, and reads as "never registered".
        /// </remarks>
        private static ObjectID ResolveFromGame(string itemId)
        {
            if (resolverOverride != null)
            {
                return resolverOverride(itemId);
            }

            return DimensionObjectNames.Resolve(itemId);
        }
    }
}
