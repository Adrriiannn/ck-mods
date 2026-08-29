using System.Collections.Generic;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which access rule owns which portal, and how many rules are being ignored because of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dimension keeps its access rules in one flat list with no pointer from a portal to its
    /// rule. The association is a convention instead: the generator walks that list and takes the
    /// FIRST enabled rule whose kind and dimension match, and every later rule of the same kind is
    /// silently skipped for the prefab, the offering window and the recipe — while still being
    /// registered as a service record with no world object behind it. That asymmetry is the whole
    /// reason this class exists.
    /// </para>
    /// <para>
    /// The generator and the Portal Studio therefore share this one implementation. If the studio
    /// computed ownership with its own copy of the loop, the two would eventually disagree and the
    /// studio would confidently show settings for a rule the game never reads.
    /// </para>
    /// </remarks>
    internal static class DimensionPortalRuleOwnership
    {
        /// <summary>
        /// Which side of the link a rule of this kind is matched on.
        /// </summary>
        /// <remarks>
        /// A way IN is matched on the dimension it leads TO. The return portal lives inside the
        /// dimension and leads back out, so it is matched on the dimension it comes FROM — the same
        /// id, read off the other end of the rule.
        /// </remarks>
        internal static bool MatchesFromDimension(DimensionPortalAccessKind kind)
        {
            return kind == DimensionPortalAccessKind.GeneratedReturnPortal;
        }

        /// <summary>
        /// The rule the generator will actually use for this kind of portal, or null when the
        /// dimension has none.
        /// </summary>
        internal static DimensionPortalAccessRuleAsset Find(
            IReadOnlyList<DimensionPortalAccessRuleAsset> rules,
            DimensionPortalAccessKind accessKind,
            string dimensionId,
            bool matchFromDimension)
        {
            if (rules == null)
            {
                return null;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !rule.Enabled || rule.AccessKind != accessKind)
                {
                    continue;
                }

                string matchedDimensionId = matchFromDimension
                    ? rule.FromDimensionId
                    : rule.ToDimensionId;
                if (string.IsNullOrEmpty(dimensionId) ||
                    string.Equals(matchedDimensionId, dimensionId, System.StringComparison.Ordinal))
                {
                    return rule;
                }
            }

            return null;
        }

        /// <summary>The rule that owns this kind of portal on a dimension, or null.</summary>
        internal static DimensionPortalAccessRuleAsset FindForTemplate(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind accessKind)
        {
            if (template == null)
            {
                return null;
            }

            return Find(
                template.PortalAccessRules,
                accessKind,
                template.DimensionId,
                MatchesFromDimension(accessKind));
        }

        /// <summary>
        /// Every enabled rule of this kind that the generator would match, owner first.
        /// </summary>
        /// <remarks>
        /// Anything past the first entry is dead weight today: it produces a service record but no
        /// portal in the world. The studio shows that as a warning rather than hiding it, because
        /// an author who added a second rule has every reason to expect a second portal.
        /// </remarks>
        internal static List<DimensionPortalAccessRuleAsset> FindAllForTemplate(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind accessKind)
        {
            List<DimensionPortalAccessRuleAsset> matches =
                new List<DimensionPortalAccessRuleAsset>();
            if (template == null)
            {
                return matches;
            }

            bool matchFromDimension = MatchesFromDimension(accessKind);
            string dimensionId = template.DimensionId;
            DimensionPortalAccessRuleAsset[] rules = template.PortalAccessRules;
            if (rules == null)
            {
                return matches;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !rule.Enabled || rule.AccessKind != accessKind)
                {
                    continue;
                }

                string matchedDimensionId = matchFromDimension
                    ? rule.FromDimensionId
                    : rule.ToDimensionId;
                if (string.IsNullOrEmpty(dimensionId) ||
                    string.Equals(matchedDimensionId, dimensionId, System.StringComparison.Ordinal))
                {
                    matches.Add(rule);
                }
            }

            return matches;
        }

        /// <summary>
        /// Any rule of this kind on the dimension, enabled or not, so a page can edit the one that
        /// exists instead of offering to build a second.
        /// </summary>
        internal static DimensionPortalAccessRuleAsset FindAnyOfKind(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind accessKind)
        {
            DimensionPortalAccessRuleAsset[] rules = template == null
                ? null
                : template.PortalAccessRules;
            if (rules == null)
            {
                return null;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] != null && rules[i].AccessKind == accessKind)
                {
                    return rules[i];
                }
            }

            return null;
        }
    }
}
