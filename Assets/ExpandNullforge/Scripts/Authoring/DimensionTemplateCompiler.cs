using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public static partial class DimensionTemplateCompiler
    {
        private const int BoundsAlignmentTiles = 16;
        private const int MinimumShellPaddingTiles = 64;
        private const int MaximumShellPaddingTiles = 5000;

        public static DimensionCompiledGenerationPlan Compile(DimensionTemplateAsset template)
        {
            List<DimensionAuthoringIssue> issues = new List<DimensionAuthoringIssue>();
            List<DimensionCompiledBiomeRegion> biomeRegions = new List<DimensionCompiledBiomeRegion>();
            List<DimensionCompiledScenePlacement> scenePlacements = new List<DimensionCompiledScenePlacement>();
            List<DimensionGenerationPassDefinition> generationPasses = new List<DimensionGenerationPassDefinition>();

            if (template == null)
            {
                DimensionBounds emptyBounds = new DimensionBounds(new int2(0, 0), new int2(0, 0));
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "template-null",
                    "Dimension template is missing.",
                    "DimensionTemplate",
                    string.Empty));

                return new DimensionCompiledGenerationPlan(
                    false,
                    "template-null",
                    "Dimension template is missing.",
                    string.Empty,
                    string.Empty,
                    emptyBounds,
                    emptyBounds,
                    MinimumShellPaddingTiles,
                    biomeRegions,
                    scenePlacements,
                    generationPasses,
                    issues);
            }

            string dimensionId = template.DimensionId;
            DimensionBounds reservedBounds = template.ReservedLocalBounds;

            if (string.IsNullOrEmpty(dimensionId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "dimension-id-empty",
                    "Dimension id is required.",
                    "DimensionTemplate",
                    template.name));
            }

            if (!IsValidBounds(reservedBounds))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "dimension-reserved-bounds-invalid",
                    "Dimension reserved local bounds must have positive width and height.",
                    "DimensionTemplate",
                    dimensionId,
                    true,
                    reservedBounds));
            }

            BuildBiomeRegions(template, dimensionId, biomeRegions, issues);
            ValidateBiomeRegionDiagnostics(biomeRegions, issues);
            BuildScenePlacements(template, dimensionId, biomeRegions, scenePlacements, issues);
            BuildGenerationPasses(template, dimensionId, biomeRegions, generationPasses, issues);
            ValidateBiomeSemanticObjectIds(template, issues);
            ValidateBiomeTerrainMaterials(template, biomeRegions, issues);
            ValidateExactSceneOverlaps(scenePlacements, issues);

            DimensionBounds playableBounds;
            if (!TryResolvePlayableBounds(
                    biomeRegions,
                    scenePlacements,
                    generationPasses,
                    out playableBounds))
            {
                playableBounds = CreateCenteredBounds(BoundsAlignmentTiles);
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "dimension-empty-playable-area",
                    "No enabled biome region or scene contributes a playable area.",
                    "DimensionTemplate",
                    dimensionId));
            }

            playableBounds = AlignToSquareBounds(playableBounds, BoundsAlignmentTiles);
            int shellPadding = ResolveShellPadding(playableBounds);
            DimensionBounds compiledReservedBounds = ExpandBounds(playableBounds, shellPadding);

            if (IsValidBounds(reservedBounds) && !Contains(reservedBounds, playableBounds))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "dimension-playable-exceeds-reserved",
                    "Compiled playable bounds extend outside the dimension reserved bounds. The runtime placement allocator may need a larger reserved range.",
                    "DimensionTemplate",
                    dimensionId,
                    true,
                    playableBounds));
            }

            int errorCount = CountIssues(issues, DimensionAuthoringSeverity.Error);
            bool success = errorCount == 0;
            string code = success ? "ok" : "validation-failed";
            string message = success
                ? "Dimension authoring template compiled successfully."
                : "Dimension authoring template has validation errors.";

            return new DimensionCompiledGenerationPlan(
                success,
                code,
                message,
                dimensionId,
                template.DisplayName,
                compiledReservedBounds,
                playableBounds,
                shellPadding,
                biomeRegions,
                scenePlacements,
                generationPasses,
                issues);
        }

        private static int CountIssues(List<DimensionAuthoringIssue> issues, DimensionAuthoringSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsString(string[] values, string value)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The zone a compiled region belongs to, whether it named one or not.</summary>
        /// <remarks>
        /// ONE COPY, BECAUSE THE ANSWER IS AN ID. The compiler, the manifest builder and the
        /// service all have to name the same region the same way; if two of them ever disagreed, a
        /// compiled zone and its manifest entry would sit at different ids and nothing would say so.
        /// </remarks>
        internal static string ResolveCompiledZoneId(DimensionCompiledBiomeRegion region)
        {
            if (!string.IsNullOrEmpty(region.ZoneId))
            {
                return region.ZoneId;
            }

            if (!string.IsNullOrEmpty(region.SourceTemplateId))
            {
                return region.DimensionId + "." + region.SourceTemplateId;
            }

            return region.DimensionId + "." + region.BiomeId;
        }

        /// <summary>An id qualified by the dimension, and by the zone when there is one.</summary>
        /// <remarks>Same rule, same reason as <see cref="ResolveCompiledZoneId"/>: one copy.</remarks>
        internal static string BuildScopedId(string dimensionId, string zoneId, string id)
        {
            string resolvedId = id ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedId))
            {
                return string.Empty;
            }

            if (resolvedId.IndexOf('.') >= 0 || resolvedId.IndexOf(':') >= 0)
            {
                return resolvedId;
            }

            if (!string.IsNullOrEmpty(zoneId))
            {
                if (zoneId.StartsWith(dimensionId + "."))
                {
                    return zoneId + "." + resolvedId;
                }

                return dimensionId + "." + zoneId + "." + resolvedId;
            }

            return dimensionId + "." + resolvedId;
        }

        private static DimensionAuthoringIssue CreateIssue(
            DimensionAuthoringSeverity severity,
            string code,
            string message,
            string recordKind,
            string recordId)
        {
            return CreateIssue(
                severity,
                code,
                message,
                recordKind,
                recordId,
                false,
                new DimensionBounds(new int2(0, 0), new int2(0, 0)));
        }

        private static DimensionAuthoringIssue CreateIssue(
            DimensionAuthoringSeverity severity,
            string code,
            string message,
            string recordKind,
            string recordId,
            bool hasLocalBounds,
            DimensionBounds localBounds)
        {
            return new DimensionAuthoringIssue(
                severity,
                code,
                message,
                recordKind,
                recordId,
                hasLocalBounds,
                localBounds);
        }
    }
}
