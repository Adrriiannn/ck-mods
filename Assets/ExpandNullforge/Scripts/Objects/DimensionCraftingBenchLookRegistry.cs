using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// Which crafting window look each generated station wears, keyed by the object name the
    /// generator gave it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A SIDE TABLE RATHER THAN SOMETHING BAKED ON THE PREFAB. Every generated station shares
    /// one pooled <see cref="DimensionCraftingBenchView"/> instance, because Core Keeper pools
    /// graphical objects by component TYPE — so a value baked on the prefab is one value for every
    /// station in the mod, and whichever station was drawn last would decide what the next one's
    /// window looks like. The view has to re-derive its answer per entity, and an object id is the
    /// only thing it is handed, so the answer has to be reachable from an object id.
    /// </para>
    /// <para>
    /// WHY NAMES AND NOT IDS. A mod's object has no <c>ObjectID</c> until the game hands it one
    /// while the world converts, which is long after the generated bootstrap runs. So rows arrive
    /// carrying the object NAME and the id map is built lazily, and rebuilt whenever the row count
    /// changes — the same shape <c>DimensionPlantPresentationRegistry</c> uses, and for the same
    /// reason: there is no callback that says "mod objects now have numbers".
    /// </para>
    /// </remarks>
    public static class DimensionCraftingBenchLookRegistry
    {
        /// <summary>
        /// The look a station wears when nobody said. Wood is the game's own fallback, hard-coded
        /// in <c>CraftingUIBase.ShowCraftingUI</c> for a window with no building behind it, so an
        /// unregistered station looks exactly as it did before this table existed.
        /// </summary>
        public const int DefaultLook = 0;

        private static readonly List<KeyValuePair<string, int>> Rows =
            new List<KeyValuePair<string, int>>();

        private static readonly Dictionary<int, int> ByObjectId = new Dictionary<int, int>();

        private static int idsResolvedForCount = -1;

        public static int Count
        {
            get { return Rows.Count; }
        }

        /// <summary>
        /// Records the look one station's crafting window wears.
        /// </summary>
        /// <param name="objectName">
        /// The station's qualified object name, as the generator wrote it onto
        /// <c>ObjectAuthoring.objectName</c>.
        /// </param>
        /// <param name="look">
        /// The value of <c>UIManager.CraftingUIThemeType</c> to use. Passed as a plain number so
        /// the generated bootstrap does not have to name a game type.
        /// </param>
        public static void Register(string objectName, int look)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                if (string.Equals(Rows[i].Key, objectName, StringComparison.Ordinal))
                {
                    Rows[i] = new KeyValuePair<string, int>(objectName, look);
                    Invalidate();
                    return;
                }
            }

            Rows.Add(new KeyValuePair<string, int>(objectName, look));
            Invalidate();
        }

        /// <summary>
        /// The look this station's window wears, or <see cref="DefaultLook"/> for anything that
        /// never registered one.
        /// </summary>
        public static int For(ObjectID objectID)
        {
            if (objectID == ObjectID.None || Rows.Count == 0)
            {
                return DefaultLook;
            }

            if (idsResolvedForCount != Rows.Count)
            {
                Rebuild();
            }

            int look;
            return ByObjectId.TryGetValue((int)objectID, out look) ? look : DefaultLook;
        }

        public static void Clear()
        {
            Rows.Clear();
            Invalidate();
        }

        private static void Invalidate()
        {
            ByObjectId.Clear();
            idsResolvedForCount = -1;
        }

        private static void Rebuild()
        {
            ByObjectId.Clear();
            for (int i = 0; i < Rows.Count; i++)
            {
                ObjectID id = API.Authoring.GetObjectID(Rows[i].Key);
                if (id != ObjectID.None)
                {
                    ByObjectId[(int)id] = Rows[i].Value;
                }
            }

            idsResolvedForCount = Rows.Count;
        }
    }
}
