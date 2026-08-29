using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.WorldRules
{
    /// <summary>
    /// Rewrites what upgrading a piece of gear costs, one level at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE ONE RULE TABLE WITH NO FILE OF ITS OWN. Core Keeper reads most of its rule tables back
    /// through <c>Manager.mod</c>, which merges settings files out of every loaded mod's Conf
    /// folder before a world is built — loot, fishing, talents and what spawns where all arrive
    /// that way. Upgrade costs do not: <c>UpgradeCostsTableConverter</c> reads the asset the
    /// authoring object points at, directly, with nothing in between. So the only place to change
    /// them is the moment the converter runs.
    /// </para>
    /// <para>
    /// THE TABLE IS COPIED, NEVER EDITED. The asset the converter is pointed at is Core Keeper's
    /// own, shared by everything in the process and never reloaded. Writing into it would leave a
    /// modded price behind in a vanilla world opened later in the same sitting, and there would be
    /// nothing to put back. So a copy is made, the copy is edited, the converter is pointed at the
    /// copy for the length of its call, and the original is put back afterwards. This is the same
    /// deep-copy-then-edit law the rest of the framework follows around blob assets.
    /// </para>
    /// <para>
    /// NAMES ARE ANSWERED HERE, NOT IN THE EDITOR. An item this mod adds has no number until the
    /// mod is loaded, so the rows carry names and the numbers are looked up at the moment the
    /// table is built. A name nothing answers drops that row and says so, which leaves the level at
    /// the game's own price rather than at a free one.
    /// </para>
    /// </remarks>
    public static class DimensionUpgradeCostRegistry
    {
        /// <summary>One ingredient in one upgrade level's price.</summary>
        public struct UpgradeCostRow
        {
            /// <summary>Which upgrade level this price is for. 0 is the first upgrade.</summary>
            public int Level;

            /// <summary>The item's object name — Core Keeper's own, or one this mod registers.</summary>
            public string ItemName;

            /// <summary>How many of it.</summary>
            public int Amount;
        }

        private static readonly List<UpgradeCostRow> Rows = new List<UpgradeCostRow>();

        /// <summary>Queues one row. Call from the generated bootstrap; names resolve at load.</summary>
        public static void Register(int level, string itemName, int amount)
        {
            if (level < 0 || string.IsNullOrEmpty(itemName) || amount < 0)
            {
                return;
            }

            Rows.Add(new UpgradeCostRow
            {
                Level = level,
                ItemName = itemName,
                Amount = amount
            });
        }

        /// <summary>Clears queued rows (mod reload).</summary>
        public static void Clear()
        {
            Rows.Clear();
        }

        /// <summary>True once at least one level has been priced.</summary>
        public static bool HasAny { get { return Rows.Count > 0; } }

        internal static int PendingCount { get { return Rows.Count; } }

        /// <summary>
        /// A copy of <paramref name="original"/> with every queued level's price written over it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The copy is made the way Core Keeper itself copies its rule tables when it merges mod
        /// settings files: serialize the asset and read it back into a fresh instance. That is a
        /// real deep copy of the lists inside, which a shallow clone would not give — the level
        /// lists would still be the originals, and editing one would edit the game's.
        /// </para>
        /// <para>
        /// A level nobody priced is not touched. A level priced past the end of the game's own
        /// table is added, so a mod that raises the level ceiling can price the new levels too.
        /// </para>
        /// </remarks>
        internal static UpgradeCostsTable BuildEditedCopy(
            UpgradeCostsTable original,
            System.Func<string, ObjectID> resolveItem,
            System.Action<string> report)
        {
            if (original == null || Rows.Count == 0)
            {
                return null;
            }

            UpgradeCostsTable copy = ScriptableObject.CreateInstance<UpgradeCostsTable>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), copy);
            if (copy.upgradeCosts == null)
            {
                copy.upgradeCosts = new List<UpgradeCosts>();
            }

            Dictionary<int, List<UpgradeCost>> byLevel = new Dictionary<int, List<UpgradeCost>>();
            for (int i = 0; i < Rows.Count; i++)
            {
                UpgradeCostRow row = Rows[i];
                ObjectID item = resolveItem == null ? ObjectID.None : resolveItem(row.ItemName);
                if (item == ObjectID.None)
                {
                    if (report != null)
                    {
                        report(
                            "Upgrading to level " + row.Level + " was priced in '" + row.ItemName +
                            "', which is not a thing this game has. That ingredient was left out — " +
                            "check the spelling against the item's own name.");
                    }

                    continue;
                }

                List<UpgradeCost> level;
                if (!byLevel.TryGetValue(row.Level, out level))
                {
                    level = new List<UpgradeCost>();
                    byLevel.Add(row.Level, level);
                }

                level.Add(new UpgradeCost { item = item, amount = row.Amount });
            }

            foreach (KeyValuePair<int, List<UpgradeCost>> priced in byLevel)
            {
                while (copy.upgradeCosts.Count <= priced.Key)
                {
                    copy.upgradeCosts.Add(new UpgradeCosts { upgradeCost = new List<UpgradeCost>() });
                }

                copy.upgradeCosts[priced.Key].upgradeCost = priced.Value;
            }

            return copy;
        }

        /// <summary>Answers an object name, this mod's own included, once the mod is loaded.</summary>
        internal static ObjectID ResolveItemName(string name)
        {
            return ExpandNullforge.Foundation.DimensionObjectNames.Resolve(name);
        }
    }

    /// <summary>
    /// The one moment upgrade costs can be changed: the converter is about to read the table off
    /// the authoring object and bake it, and everything after runs from the baked copy.
    /// </summary>
    /// <remarks>
    /// A prefix that swaps the reference and a postfix that swaps it back, rather than a prefix
    /// that replaces the whole method. The converter's own blob-building code stays the game's, so
    /// a change to it in a future update carries through instead of being silently overwritten by a
    /// copy of the old one.
    /// </remarks>
    [HarmonyPatch(typeof(UpgradeCostsTableConverter), "Convert")]
    internal static class DimensionUpgradeCostsTableConverterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(UpgradeCostsTableAuthoring authoring, out UpgradeCostsTable __state)
        {
            __state = null;
            if (authoring == null ||
                authoring.upgradeCostsTable == null ||
                !DimensionUpgradeCostRegistry.HasAny)
            {
                return;
            }

            try
            {
                UpgradeCostsTable edited = DimensionUpgradeCostRegistry.BuildEditedCopy(
                    authoring.upgradeCostsTable,
                    DimensionUpgradeCostRegistry.ResolveItemName,
                    Foundation.DimensionFrameworkLog.Warning);
                if (edited == null)
                {
                    return;
                }

                __state = authoring.upgradeCostsTable;
                authoring.upgradeCostsTable = edited;
            }
            catch (System.Exception exception)
            {
                // A mod's prices are never worth losing the game's own table over. Leaving the
                // original in place means upgrading costs what it always did.
                __state = null;
                DimensionLog.Fatal(DimensionLogChannels.WorldRule, null, 
                    "Could not apply this mod's upgrade prices, so upgrading " +
                    "costs what the game says it does: " + exception);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(UpgradeCostsTableAuthoring authoring, UpgradeCostsTable __state)
        {
            if (authoring == null || __state == null)
            {
                return;
            }

            UpgradeCostsTable temporary = authoring.upgradeCostsTable;
            authoring.upgradeCostsTable = __state;
            if (temporary != null && temporary != __state)
            {
                Object.Destroy(temporary);
            }
        }
    }
}
