using System.Collections.Generic;
using HarmonyLib;
using PugMod;
using UnityEngine;

namespace ExpandNullforge.Portals
{
    /// <summary>
    /// Registers portal items (the V1 placed-portal object or the V2 instantaneous-portal item) as
    /// drops on specific mob/boss loot tables. Drops are injected at load through a Harmony prefix on
    /// <c>LootTableConverter.Convert</c> — the same seam Core Keeper converts its own loot on. That
    /// runs after every mod's objects and loot tables exist, so a boss added by ANOTHER mod can be
    /// targeted safely (the "apply as late as possible" requirement). Modelled on the loot-table
    /// modification pattern proven by CoreLib's LootDropModule, reimplemented here so the framework
    /// stays standalone.
    ///
    /// This is authoring-neutral and reused beyond portals later: register a target object name, an
    /// item name, and a weight/amount, and the drop appears on that enemy.
    /// </summary>
    public static class DimensionPortalDropRegistry
    {
        public struct DropRequest
        {
            public string TargetObjectName;
            public string ItemObjectName;
            public float Weight;
            public float ChancePercent;
            public int MinAmount;
            public int MaxAmount;
        }

        private static readonly List<DropRequest> Requests = new List<DropRequest>();
        private static bool applied;

        /// <summary>Queues a drop. Call from the generated bootstrap; resolution is deferred to load.</summary>
        public static void Register(
            string targetObjectName,
            string itemObjectName,
            float weight,
            float chancePercent,
            int minAmount,
            int maxAmount)
        {
            if (string.IsNullOrEmpty(targetObjectName) || string.IsNullOrEmpty(itemObjectName))
            {
                return;
            }

            int min = Mathf.Max(1, minAmount);
            Requests.Add(new DropRequest
            {
                TargetObjectName = targetObjectName,
                ItemObjectName = itemObjectName,
                Weight = Mathf.Max(1f, weight),
                ChancePercent = Mathf.Clamp(chancePercent, 0f, 100f),
                MinAmount = min,
                MaxAmount = Mathf.Max(min, maxAmount)
            });
        }

        /// <summary>Clears queued drops (mod reload).</summary>
        public static void Clear()
        {
            Requests.Clear();
            applied = false;
        }

        /// <summary>
        /// Applies every queued drop into the game's loot tables. Resolves each target enemy to its
        /// loot-table id (via its DropsLootFromLootTableCD) and appends the item as a weighted entry
        /// — or a guaranteed one when chance is 100. Idempotent per load.
        /// </summary>
        internal static void ApplyToLootTables()
        {
            if (applied || Requests.Count == 0)
            {
                return;
            }

            List<LootTable> lootTables = Manager.mod == null ? null : Manager.mod.LootTable;
            if (lootTables == null)
            {
                return;
            }

            Dictionary<LootTableID, LootTable> byId = new Dictionary<LootTableID, LootTable>();
            for (int i = 0; i < lootTables.Count; i++)
            {
                LootTable table = lootTables[i];
                if (table != null)
                {
                    byId[table.id] = table;
                }
            }

            for (int i = 0; i < Requests.Count; i++)
            {
                DropRequest request = Requests[i];
                ObjectID targetId = API.Authoring.GetObjectID(request.TargetObjectName);
                ObjectID itemId = API.Authoring.GetObjectID(request.ItemObjectName);
                if (targetId == ObjectID.None || itemId == ObjectID.None)
                {
                    Foundation.DimensionFrameworkLog.Warning(
                        "[ExpandNullforge] Portal drop skipped: could not resolve '" +
                        request.TargetObjectName + "' or '" + request.ItemObjectName + "'.");
                    continue;
                }

                if (!TryResolveLootTableId(targetId, out LootTableID tableId) ||
                    !byId.TryGetValue(tableId, out LootTable lootTable))
                {
                    Foundation.DimensionFrameworkLog.Warning(
                        "[ExpandNullforge] Portal drop skipped: '" + request.TargetObjectName +
                        "' has no loot table to add to.");
                    continue;
                }

                bool guaranteed = request.ChancePercent >= 100f;
                List<LootInfo> pool = guaranteed ? lootTable.guaranteedLootInfos : lootTable.lootInfos;
                if (pool == null)
                {
                    continue;
                }

                bool alreadyPresent = false;
                for (int j = 0; j < pool.Count; j++)
                {
                    if (pool[j].objectID == itemId)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (alreadyPresent)
                {
                    continue;
                }

                pool.Add(new LootInfo
                {
                    objectID = itemId,
                    amount = new Pug.UnityExtensions.RangeInt { min = request.MinAmount, max = request.MaxAmount },
                    isPartOfGuaranteedDrop = guaranteed,
                    weight = request.Weight
                });
            }

            applied = true;
        }

        private static bool TryResolveLootTableId(ObjectID mobId, out LootTableID lootTableId)
        {
            lootTableId = LootTableID.Empty;
            ObjectDataCD objectData = new ObjectDataCD { objectID = mobId, variation = 0, amount = 1 };
            if (PugDatabase.TryGetComponent(objectData, out DropsLootFromLootTableCD dropsLoot))
            {
                lootTableId = dropsLoot.lootTableID;
                return lootTableId != LootTableID.Empty;
            }

            return false;
        }
    }

    /// <summary>
    /// Harmony prefix that injects the framework's queued portal drops the moment Core Keeper builds
    /// its loot tables. Attribute-applied by the mod loader like the ModSDK example patches.
    /// </summary>
    [HarmonyPatch(typeof(LootTableConverter), nameof(LootTableConverter.Convert))]
    internal static class DimensionPortalLootTablePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            DimensionPortalDropRegistry.ApplyToLootTables();
        }
    }
}
