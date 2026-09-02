using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The access rules a portal carries into the manifest.
    /// </summary>
    public static partial class DimensionTemplateManifestBuilder
    {
        private static void AddPortalAccessRules(
            DimensionTemplateAsset template,
            string contentPackId,
            bool hasContentPack,
            List<DimensionPortalDefinition> portals,
            List<DimensionPortalPresentationDefinition> portalPresentations,
            List<DimensionTravelRequirementDefinition> travelRequirements,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (template == null)
            {
                return;
            }

            DimensionPortalAccessRuleAsset[] rules = template.PortalAccessRules;
            if (rules.Length == 0)
            {
                return;
            }

            Dictionary<string, bool> portalIds = new Dictionary<string, bool>();
            Dictionary<string, bool> presentationIds = new Dictionary<string, bool>();
            Dictionary<string, bool> requirementIds = new Dictionary<string, bool>();
            List<DimensionTravelRequirementDefinition> localRequirements =
                new List<DimensionTravelRequirementDefinition>();

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !rule.Enabled)
                {
                    continue;
                }

                DimensionPortalDefinition portal = rule.ToPortalDefinition();
                if (AddUnique(portalIds, portal.PortalId))
                {
                    portals.Add(portal);
                    AddOwnership(
                        hasContentPack,
                        contentPackId,
                        DimensionContentRecordKind.Portal,
                        portal.PortalId,
                        portal.DisplayName,
                        ownershipBindings,
                        ownershipKeys);
                }

                DimensionPortalPresentationDefinition presentation = rule.ToPortalPresentationDefinition();
                if (AddUnique(presentationIds, presentation.PresentationId))
                {
                    portalPresentations.Add(presentation);
                    AddOwnership(
                        hasContentPack,
                        contentPackId,
                        DimensionContentRecordKind.PortalPresentation,
                        presentation.PresentationId,
                        presentation.DisplayName,
                        ownershipBindings,
                        ownershipKeys);
                }

                localRequirements.Clear();
                rule.AppendTravelRequirements(localRequirements);
                for (int requirementIndex = 0; requirementIndex < localRequirements.Count; requirementIndex++)
                {
                    DimensionTravelRequirementDefinition requirement = localRequirements[requirementIndex];
                    if (!AddUnique(requirementIds, requirement.RequirementId))
                    {
                        continue;
                    }

                    travelRequirements.Add(requirement);
                    AddOwnership(
                        hasContentPack,
                        contentPackId,
                        DimensionContentRecordKind.TravelRequirement,
                        requirement.RequirementId,
                        requirement.DisplayName,
                        ownershipBindings,
                        ownershipKeys);
                }
            }
        }
    }
}
