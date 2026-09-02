using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Helpers for the three portal versions a dimension can offer and the rule that at least one
    /// player-facing entry (V1 user-accessible OR V2 instantaneous item) is always enabled. V3 (the
    /// generated return portal) is always present and is not part of the enable/disable choice.
    /// Shared by the generator and the Portal Studio so both agree on classification and the
    /// invariant.
    /// </summary>
    public static class DimensionPortalVersions
    {
        /// <summary>V1: the placed, user-accessible portal (craftable/generated/droppable object).</summary>
        public static bool IsUserAccessible(DimensionPortalAccessKind kind)
        {
            return kind == DimensionPortalAccessKind.PlacedPortal ||
                   kind == DimensionPortalAccessKind.GeneratedEntrance;
        }

        /// <summary>V2: the instantaneous item portal (an item that spawns a temporary portal).</summary>
        public static bool IsInstantaneousItem(DimensionPortalAccessKind kind)
        {
            return kind == DimensionPortalAccessKind.InventoryItem;
        }

        /// <summary>V3: the unbreakable generated return portal inside the dimension.</summary>
        public static bool IsGeneratedReturn(DimensionPortalAccessKind kind)
        {
            return kind == DimensionPortalAccessKind.GeneratedReturnPortal;
        }

        /// <summary>A player-facing way IN to the dimension (V1 or V2) — the enable/disable choice.</summary>
        public static bool IsEntryVersion(DimensionPortalAccessKind kind)
        {
            return IsUserAccessible(kind) || IsInstantaneousItem(kind);
        }

        /// <summary>True if any enabled entry version (V1 or V2) exists in the set.</summary>
        public static bool HasEnabledEntry(IReadOnlyList<DimensionPortalAccessRuleAsset> rules)
        {
            if (rules == null)
            {
                return false;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule != null && rule.Enabled && IsEntryVersion(rule.AccessKind))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Guarantees the invariant: if the modder disabled every entry version, re-enable the first
        /// user-accessible one (falling back to the first item portal) so the dimension is always
        /// reachable. Returns the rule that was force-enabled, or null if none needed changing.
        /// </summary>
        public static DimensionPortalAccessRuleAsset EnsureAtLeastOneEntryEnabled(
            IReadOnlyList<DimensionPortalAccessRuleAsset> rules)
        {
            if (rules == null || HasEnabledEntry(rules))
            {
                return null;
            }

            DimensionPortalAccessRuleAsset fallback = FindFirst(rules, DimensionPortalAccessKind.PlacedPortal)
                ?? FindFirst(rules, DimensionPortalAccessKind.GeneratedEntrance)
                ?? FindFirst(rules, DimensionPortalAccessKind.InventoryItem);
            if (fallback != null)
            {
                fallback.SetEnabled(true);
            }

            return fallback;
        }

        private static DimensionPortalAccessRuleAsset FindFirst(
            IReadOnlyList<DimensionPortalAccessRuleAsset> rules,
            DimensionPortalAccessKind kind)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].AccessKind == kind)
                {
                    return rules[i];
                }
            }

            return null;
        }
    }
}
