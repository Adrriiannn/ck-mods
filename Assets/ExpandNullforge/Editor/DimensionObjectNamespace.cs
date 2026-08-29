using System;
using System.Collections.Generic;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The naming decisions for one generation run: which mod owns the output, and which ids belong
    /// to that mod rather than to vanilla.
    /// </summary>
    /// <remarks>
    /// Carried as one parameter rather than two so the generator's call chain grows by a single
    /// argument per method, and so the two values — which are only meaningful together — cannot
    /// drift apart. It is a struct with no mutable state, so nothing here depends on call order.
    /// </remarks>
    internal readonly struct DimensionNamingContext
    {
        private readonly HashSet<string> ownIds;
        private readonly HashSet<string> switchedOffIds;

        public DimensionNamingContext(string modName, IEnumerable<string> ownItemIds)
            : this(modName, ownItemIds, null)
        {
        }

        /// <summary>
        /// A context that also knows which of the mod's own ids are switched off.
        /// </summary>
        /// <remarks>
        /// IT TRAVELS WITH THE NAMING CONTEXT so every generator gets it for free. The set existed
        /// and only the bootstrap emitter's binder was ever built with it, so one reference to an
        /// unticked projectile produced two contradictory lines in the same report: the emitter's
        /// correct "one of yours but switched off" and the generator's "neither one of this mod's
        /// nor one the game has". The context is what every generator is already handed.
        /// </remarks>
        public DimensionNamingContext(
            string modName,
            IEnumerable<string> ownItemIds,
            IEnumerable<string> switchedOffItemIds)
        {
            ModName = modName ?? string.Empty;
            ownIds = new HashSet<string>(StringComparer.Ordinal);
            switchedOffIds = new HashSet<string>(StringComparer.Ordinal);

            if (ownItemIds != null)
            {
                foreach (string id in ownItemIds)
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        ownIds.Add(DimensionObjectNamespace.LocalIdOf(id));
                    }
                }
            }

            if (switchedOffItemIds == null)
            {
                return;
            }

            foreach (string id in switchedOffItemIds)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    switchedOffIds.Add(DimensionObjectNamespace.LocalIdOf(id));
                }
            }
        }

        /// <summary>Local ids of this mod's own assets that are unticked, so nothing answers to them.</summary>
        public IEnumerable<string> SwitchedOffIds
        {
            get { return switchedOffIds ?? EmptyIds; }
        }

        private static readonly HashSet<string> EmptyIds =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// A context for a run that generates something other than items, told which ids the mod
        /// itself makes so a reference to one of them is qualified.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The ids come from one collector shared with the bootstrap emitter
        /// (<c>DimensionGeneratedObjectIds.Collect</c>), so what a generator treats as "ours" and
        /// what the emitter registers as "ours" cannot drift apart.
        /// </para>
        /// <para>
        /// THERE IS DELIBERATELY NO OVERLOAD THAT OWNS NOTHING. There used to be, and it is what
        /// made a workbench recipe ship <c>moddedObjectID = "EmberBolt"</c> while the item generator
        /// had registered the object as <c>MyMod:EmberBolt</c>: <see cref="QualifyReference"/> is
        /// <c>Owns(id) ? Qualify(...) : id</c>, so with an empty ownership set it is the identity
        /// function and the station's lookup misses. Passing <c>null</c> here is still legal for the
        /// rare run that genuinely cannot know, but it has to be written out on purpose rather than
        /// reached for by accident.
        /// </para>
        /// </remarks>
        public static DimensionNamingContext ForOutputFolder(
            string outputFolder,
            IEnumerable<string> ownIds,
            IEnumerable<string> switchedOffIds = null)
        {
            ModBuilderSettings settings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(outputFolder);
            // metadata.name is the mod's canonical id; displayName is a label that may change without
            // the content changing, so it must not decide object identity.
            string modName = settings == null
                ? string.Empty
                : (settings.metadata.name ?? string.Empty);

            return new DimensionNamingContext(modName, ownIds, switchedOffIds);
        }

        /// <summary>The mod that owns this run's output. Empty when it could not be resolved.</summary>
        public string ModName { get; }

        /// <summary>True once a mod name is known, so callers can warn rather than silently ship unqualified names.</summary>
        public bool CanQualify
        {
            get { return !string.IsNullOrEmpty(ModName); }
        }

        /// <summary>True when <paramref name="id"/> names an item this run generates.</summary>
        /// <remarks>
        /// THE GAME'S OWN NAMES WIN, and that is not a preference either. A creator is free to call
        /// one of their objects <c>Torch</c>; what they are not free to do is make every other
        /// mod's — and their own — reference to the word "Torch" stop meaning the game's torch.
        /// <c>DimensionObjectBinder</c> asks the <c>ObjectID</c> enum first and bakes the game's
        /// number, so without this guard the two would answer differently inside one run: the
        /// binder would bake the vanilla torch while <see cref="QualifyReference"/> rewrote the
        /// same string to <c>MyMod:Torch</c> in recipes, loot and trader stock. An id that shadows
        /// a game object is reported at generate time (see the dashboard's shadowed-name warning);
        /// here it simply is not ours.
        /// </remarks>
        public bool Owns(string id)
        {
            return ownIds != null
                && !string.IsNullOrEmpty(id)
                && DimensionObjectBinder.Vanilla(id) == ObjectID.None
                && ownIds.Contains(DimensionObjectNamespace.LocalIdOf(id));
        }

        /// <summary>
        /// The qualified name for an item this run generates. Always qualified — the item is by
        /// definition ours.
        /// </summary>
        public string QualifyGenerated(string localId)
        {
            return DimensionObjectNamespace.Qualify(ModName, localId);
        }

        /// <summary>
        /// The qualified name for a <b>reference</b> — a recipe ingredient, a loot entry, a
        /// summoning item. Qualified only when the id names one of our own items; anything else is
        /// assumed to be vanilla and returned untouched, because qualifying a vanilla name points
        /// the reference at an item that does not exist.
        /// </summary>
        public string QualifyReference(string id)
        {
            return Owns(id) ? DimensionObjectNamespace.Qualify(ModName, id) : (id ?? string.Empty);
        }
    }

    /// <summary>
    /// Qualifies a generated object name with the mod that owns it, so two dimension mods can be
    /// installed together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Core Keeper keys object properties by name:
    /// <c>DefaultConvertSystem</c> builds a <c>Dictionary&lt;string, int&gt;</c> from every object's
    /// name and calls <c>.Add</c> on it, which throws on a duplicate. The same system early-returns
    /// when the save carries no prior property data — which is exactly the state of a brand-new
    /// world. So two mods shipping an item of the same name produce a world that <b>loads once and
    /// then never loads again</b>: the failure appears on the second load, after the player has
    /// already built in it.
    /// </para>
    /// <para>
    /// SEPARATOR. A colon, matching the wider modding convention of <c>mod:item</c>. It is also the
    /// separator this framework's localization path already anticipates: Core Keeper's patched I2
    /// <c>LocalizationManager</c> replaces every ':' with '_' before searching its sources, and
    /// <see cref="DimensionLocalizationCsv.ToLookupKeyName"/> exists to mirror that. Localization
    /// keys therefore resolve as <c>Items/MyMod_Sword</c> while the object name stays
    /// <c>MyMod:Sword</c>.
    /// </para>
    /// <para>
    /// WHAT MUST NOT BE QUALIFIED. A recipe ingredient may name a <b>vanilla</b> item —
    /// <c>IronBar</c>, <c>Wood</c> — and qualifying one of those would point the recipe at an item
    /// that does not exist. Only ids the mod itself generates may be qualified, which is why the
    /// generator passes an explicit set of its own ids rather than qualifying every string it sees.
    /// </para>
    /// </remarks>
    internal static class DimensionObjectNamespace
    {
        /// <summary>Separates the owning mod from the local id.</summary>
        public const char Separator = ':';

        /// <summary>
        /// True when <paramref name="objectName"/> already carries a mod qualifier. Used to keep
        /// qualification idempotent: re-running generation on already-generated content must not
        /// produce <c>MyMod:MyMod:Sword</c>.
        /// </summary>
        public static bool IsQualified(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) && objectName.IndexOf(Separator) >= 0;
        }

        /// <summary>
        /// <c>MyMod:Sword</c> from ("MyMod", "Sword"). Returns <paramref name="localId"/> unchanged
        /// when the mod name is unknown or the id is already qualified — an unqualified name is a
        /// coexistence risk, but a wrongly-qualified one is a broken reference, so the safe failure
        /// is to leave it alone and let the caller warn.
        /// </summary>
        public static string Qualify(string modName, string localId)
        {
            if (string.IsNullOrEmpty(localId))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(modName) || IsQualified(localId))
            {
                return localId;
            }

            return Sanitize(modName) + Separator + localId;
        }

        /// <summary>The owning mod of a qualified name, or empty when it carries no qualifier.</summary>
        public static string ModOf(string objectName)
        {
            if (!IsQualified(objectName))
            {
                return string.Empty;
            }

            return objectName.Substring(0, objectName.IndexOf(Separator));
        }

        /// <summary>
        /// The id without its qualifier, so an authored reference written before namespacing still
        /// matches the item it meant.
        /// </summary>
        public static string LocalIdOf(string objectName)
        {
            if (!IsQualified(objectName))
            {
                return objectName ?? string.Empty;
            }

            return objectName.Substring(objectName.IndexOf(Separator) + 1);
        }

        /// <summary>
        /// Strips whitespace and separators from a mod name so the qualifier cannot itself contain
        /// a colon (which would make <see cref="ModOf"/> and <see cref="LocalIdOf"/> disagree about
        /// where the boundary is) or a space (which reads badly in a localization key).
        /// </summary>
        public static string Sanitize(string modName)
        {
            if (string.IsNullOrEmpty(modName))
            {
                return string.Empty;
            }

            char[] buffer = new char[modName.Length];
            int length = 0;
            for (int i = 0; i < modName.Length; i++)
            {
                char c = modName[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    buffer[length++] = c;
                }
            }

            return new string(buffer, 0, length);
        }
    }
}
