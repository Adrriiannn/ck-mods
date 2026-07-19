using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Public helper for generation providers that need the common biome/table/seed setup for a pass.
    /// Providers can still do their own terrain work, but this gives them one consistent source of truth.
    /// </summary>
    public static class DimensionGenerationPassWorkPlanUtility
    {
        private const string DefaultPurpose = "generation-pass-work-plan";

        public static DimensionGenerationTableKind GetDefaultTableKind(
            DimensionGenerationPassPhase phase)
        {
            switch (phase)
            {
                case DimensionGenerationPassPhase.Terrain:
                case DimensionGenerationPassPhase.Liquid:
                    return DimensionGenerationTableKind.Terrain;

                case DimensionGenerationPassPhase.Structures:
                case DimensionGenerationPassPhase.Scenes:
                    return DimensionGenerationTableKind.Scene;

                case DimensionGenerationPassPhase.Ore:
                    return DimensionGenerationTableKind.Resource;

                case DimensionGenerationPassPhase.Objects:
                    return DimensionGenerationTableKind.Object;

                case DimensionGenerationPassPhase.Mobs:
                case DimensionGenerationPassPhase.Bosses:
                    return DimensionGenerationTableKind.Spawn;

                case DimensionGenerationPassPhase.Events:
                    return DimensionGenerationTableKind.WorldEvent;

                case DimensionGenerationPassPhase.Polish:
                case DimensionGenerationPassPhase.Custom:
                default:
                    return DimensionGenerationTableKind.Custom;
            }
        }

        public static bool TryBuild(
            IDimensionService service,
            DimensionGenerationPassContext context,
            out DimensionGenerationPassWorkPlan plan)
        {
            return TryBuild(
                service,
                context,
                GetDefaultTableKind(context.Pass.Phase),
                0,
                false,
                string.Empty,
                out plan);
        }

        public static bool TryBuild(
            IDimensionService service,
            DimensionGenerationPassContext context,
            DimensionGenerationTableKind tableKind,
            int picksPerTable,
            bool allowDuplicateEntries,
            string salt,
            out DimensionGenerationPassWorkPlan plan)
        {
            DimensionDefinition dimension = context.GenerationContext.Dimension;
            DimensionArea area = context.GenerationContext.Area;
            DimensionBounds areaBounds = area.LocalBounds;
            DimensionBounds requestedBounds = context.Pass.HasLocalBounds
                ? context.Pass.LocalBounds
                : areaBounds;
            DimensionBounds emptyBounds = new DimensionBounds(new int2(0, 0), new int2(0, 0));
            float2 emptySample = new float2(0f, 0f);
            DimensionGenerationTableResolutionResult emptyResolution = CreateResolutionResult(
                false,
                dimension.Id,
                emptySample,
                tableKind,
                "not-resolved",
                "Generation tables were not resolved.");
            DimensionGenerationTableSelectionResult emptySelection = CreateSelectionResult(
                false,
                emptyResolution,
                "not-selected",
                "Generation table entries were not selected.");

            if (service == null)
            {
                plan = new DimensionGenerationPassWorkPlan(
                    false,
                    "missing-service",
                    "A dimension service is required to build a generation pass work plan.",
                    context,
                    dimension,
                    area,
                    requestedBounds,
                    emptyBounds,
                    emptySample,
                    tableKind,
                    0u,
                    false,
                    default(DimensionBiomeDefinition),
                    emptyResolution,
                    emptySelection);
                return false;
            }

            if (string.IsNullOrEmpty(dimension.Id))
            {
                plan = new DimensionGenerationPassWorkPlan(
                    false,
                    "missing-dimension",
                    "The generation pass context did not contain a dimension id.",
                    context,
                    dimension,
                    area,
                    requestedBounds,
                    emptyBounds,
                    emptySample,
                    tableKind,
                    0u,
                    false,
                    default(DimensionBiomeDefinition),
                    emptyResolution,
                    emptySelection);
                return false;
            }

            if (!context.Pass.Enabled)
            {
                plan = new DimensionGenerationPassWorkPlan(
                    false,
                    "pass-disabled",
                    "The generation pass is disabled.",
                    context,
                    dimension,
                    area,
                    requestedBounds,
                    emptyBounds,
                    emptySample,
                    tableKind,
                    0u,
                    false,
                    default(DimensionBiomeDefinition),
                    emptyResolution,
                    emptySelection);
                return false;
            }

            DimensionBounds effectiveBounds;
            if (!TryIntersect(areaBounds, requestedBounds, out effectiveBounds))
            {
                plan = new DimensionGenerationPassWorkPlan(
                    false,
                    "empty-bounds",
                    "The generation pass local bounds do not overlap the active generation area.",
                    context,
                    dimension,
                    area,
                    requestedBounds,
                    emptyBounds,
                    emptySample,
                    tableKind,
                    0u,
                    false,
                    default(DimensionBiomeDefinition),
                    emptyResolution,
                    emptySelection);
                return false;
            }

            float2 sampleLocalPosition = GetCenter(effectiveBounds);
            uint seed = service.ResolveGenerationSeed(new DimensionGenerationSeedRequest(
                dimension.Id,
                effectiveBounds,
                context.Pass.ProviderId,
                context.Pass.PassId,
                DefaultPurpose,
                salt,
                0u));

            DimensionBiomeResolutionResult biomeResolution = service.ResolveBiomeAtLocal(
                dimension.Id,
                sampleLocalPosition);
            bool hasBiome = biomeResolution.Success && biomeResolution.HasBiome;
            DimensionBiomeDefinition biome = hasBiome
                ? biomeResolution.Biome
                : default(DimensionBiomeDefinition);

            DimensionGenerationTableResolutionRequest resolutionRequest =
                new DimensionGenerationTableResolutionRequest(
                    dimension.Id,
                    sampleLocalPosition,
                    hasBiome ? biome.BiomeId : string.Empty,
                    tableKind,
                    false,
                    !hasBiome);

            DimensionGenerationTableResolutionResult tableResolution =
                service.ResolveGenerationTables(resolutionRequest);

            DimensionGenerationTableSelectionResult tableSelection = picksPerTable > 0
                ? service.SelectGenerationTableEntries(new DimensionGenerationTableSelectionRequest(
                    resolutionRequest,
                    seed,
                    salt,
                    picksPerTable,
                    allowDuplicateEntries))
                : CreateSelectionResult(
                    true,
                    tableResolution,
                    "selection-not-requested",
                    "Generation table entry selection was not requested.");

            plan = new DimensionGenerationPassWorkPlan(
                true,
                "ok",
                string.Empty,
                context,
                dimension,
                area,
                requestedBounds,
                effectiveBounds,
                sampleLocalPosition,
                tableKind,
                seed,
                hasBiome,
                biome,
                tableResolution,
                tableSelection);
            return true;
        }

        private static bool TryIntersect(
            DimensionBounds first,
            DimensionBounds second,
            out DimensionBounds intersection)
        {
            int2 min = new int2(
                math.max(first.Min.x, second.Min.x),
                math.max(first.Min.y, second.Min.y));
            int2 maxExclusive = new int2(
                math.min(first.MaxExclusive.x, second.MaxExclusive.x),
                math.min(first.MaxExclusive.y, second.MaxExclusive.y));

            if (maxExclusive.x <= min.x || maxExclusive.y <= min.y)
            {
                intersection = new DimensionBounds(new int2(0, 0), new int2(0, 0));
                return false;
            }

            intersection = new DimensionBounds(min, maxExclusive);
            return true;
        }

        private static float2 GetCenter(DimensionBounds bounds)
        {
            return new float2(
                (bounds.Min.x + bounds.MaxExclusive.x) * 0.5f,
                (bounds.Min.y + bounds.MaxExclusive.y) * 0.5f);
        }

        private static DimensionGenerationTableResolutionResult CreateResolutionResult(
            bool success,
            string dimensionId,
            float2 localPosition,
            DimensionGenerationTableKind tableKind,
            string code,
            string message)
        {
            return new DimensionGenerationTableResolutionResult(
                success,
                dimensionId,
                localPosition,
                false,
                default(DimensionZoneInfo),
                false,
                default(DimensionBiomeDefinition),
                tableKind,
                new List<DimensionResolvedGenerationTable>(),
                0,
                0,
                0,
                code,
                message);
        }

        private static DimensionGenerationTableSelectionResult CreateSelectionResult(
            bool success,
            DimensionGenerationTableResolutionResult resolution,
            string code,
            string message)
        {
            return new DimensionGenerationTableSelectionResult(
                success,
                resolution,
                new List<DimensionGenerationTableSelection>(),
                0,
                code,
                message);
        }
    }
}
