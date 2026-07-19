using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateStarterAssessment
    {
        public DimensionTemplateStarterAssessment(
            DimensionTemplateStarterGraph graph,
            DimensionAuthoringPreviewSummary preview,
            DimensionAuthoringReadinessReport readiness,
            bool hasDimensionTemplate,
            bool hasGenerationPass,
            bool hasGenerationTable,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            string code,
            string message,
            IReadOnlyList<string> advisoryMessages)
        {
            Graph = graph;
            Preview = preview;
            Readiness = readiness;
            HasDimensionTemplate = hasDimensionTemplate;
            HasGenerationPass = hasGenerationPass;
            HasGenerationTable = hasGenerationTable;
            ReadyForManifestExport = readyForManifestExport;
            ReadyForRuntimeGeneration = readyForRuntimeGeneration;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            AdvisoryMessages = advisoryMessages ?? new List<string>();
        }

        public DimensionTemplateStarterGraph Graph { get; private set; }

        public DimensionAuthoringPreviewSummary Preview { get; private set; }

        public DimensionAuthoringReadinessReport Readiness { get; private set; }

        public bool HasDimensionTemplate { get; private set; }

        public bool HasGenerationPass { get; private set; }

        public bool HasGenerationTable { get; private set; }

        public bool ReadyForManifestExport { get; private set; }

        public bool ReadyForRuntimeGeneration { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public IReadOnlyList<string> AdvisoryMessages { get; private set; }
    }

    public static class DimensionTemplateStarterAssessmentUtility
    {
        public static DimensionTemplateStarterAssessment CreateAndAssessSingleBiomeStarter(
            DimensionTemplateStarterRequest request)
        {
            DimensionTemplateStarterGraph graph =
                DimensionTemplateStarterFactory.CreateSingleBiomeStarter(request);
            return Assess(graph);
        }

        public static DimensionTemplateStarterAssessment Assess(
            DimensionTemplateStarterGraph graph)
        {
            DimensionTemplateAsset dimension = graph == null ? null : graph.Dimension;
            DimensionAuthoringPreviewSummary preview =
                DimensionAuthoringPreviewBuilder.Build(dimension);
            DimensionAuthoringReadinessReport readiness =
                DimensionAuthoringReadinessUtility.BuildReport(preview);

            bool hasDimensionTemplate = dimension != null;
            bool hasGenerationPass = HasAny(graph == null ? null : graph.GenerationPasses);
            bool hasGenerationTable = HasAny(graph == null ? null : graph.GenerationTables);
            bool readyForManifestExport =
                hasDimensionTemplate &&
                preview.Success &&
                readiness.BlockedCount == 0;
            bool readyForRuntimeGeneration =
                readyForManifestExport &&
                hasGenerationPass;

            string code;
            string message;
            if (!hasDimensionTemplate)
            {
                code = "starter-graph-missing";
                message = "No starter dimension graph is available.";
            }
            else if (!preview.Success || readiness.BlockedCount > 0)
            {
                code = "authoring-errors";
                message = "The starter dimension has blocking authoring issues.";
            }
            else if (!hasGenerationPass)
            {
                code = "starter-ready";
                message = "The starter dimension is ready for editing. Runtime generation rules can be added later in the Generation section.";
            }
            else
            {
                code = "ready";
                message = "The starter dimension is ready for manifest export and runtime generation.";
            }

            return new DimensionTemplateStarterAssessment(
                graph,
                preview,
                readiness,
                hasDimensionTemplate,
                hasGenerationPass,
                hasGenerationTable,
                readyForManifestExport,
                readyForRuntimeGeneration,
                code,
                message,
                CollectAdvisoryMessages(readiness, hasGenerationPass, hasGenerationTable));
        }

        private static bool HasAny<T>(IReadOnlyList<T> values)
        {
            return values != null && values.Count > 0;
        }

        private static IReadOnlyList<string> CollectAdvisoryMessages(
            DimensionAuthoringReadinessReport readiness,
            bool hasGenerationPass,
            bool hasGenerationTable)
        {
            List<string> messages = new List<string>();

            if (!hasGenerationTable)
            {
                messages.Add("Add at least one semantic object ID or weighted generation table when the biome should generate actual content.");
            }

            IReadOnlyList<DimensionAuthoringReadinessEntry> entries = readiness.Entries;
            if (entries == null)
            {
                return messages;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringReadinessEntry entry = entries[i];
                if (entry.State != DimensionAuthoringReadinessState.Missing &&
                    entry.State != DimensionAuthoringReadinessState.Partial &&
                    entry.State != DimensionAuthoringReadinessState.Blocked)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.Message))
                {
                    messages.Add(entry.Message);
                }
            }

            return messages;
        }
    }

    public static class DimensionTemplateStarterPresetValidator
    {
        private const int ExpectedGridColumns = 3;
        private const int ExpectedGridRows = 3;
        private const int ExpectedRadialRings = 3;
        private const float MinimumRadialFillRatio = 0.65f;
        private const float MaximumRadialFillRatio = 0.90f;

        public static IReadOnlyList<string> Validate(
            DimensionTemplateAuthoringExampleDescriptor example,
            DimensionTemplateStarterGraph graph)
        {
            List<string> messages = new List<string>();
            if (string.IsNullOrEmpty(example.ExampleId))
            {
                messages.Add("Starter preset validation could not resolve the selected example.");
                return messages;
            }

            if (graph == null || graph.Dimension == null || graph.Layout == null)
            {
                messages.Add("Starter preset validation could not create a dimension graph.");
                return messages;
            }

            DimensionCompiledGenerationPlan plan =
                DimensionTemplateCompiler.Compile(graph.Dimension);
            if (!plan.Success)
            {
                messages.Add("Starter preset does not compile: " + FirstError(plan));
                return messages;
            }

            ValidateCommonPlan(plan, messages);

            if (example.LayoutPresetId == DimensionLayoutTemplatePresetCatalog.SingleBiomeSquarePresetId)
            {
                ValidateSingleBiomeStarter(graph, plan, messages);
            }
            else if (example.LayoutPresetId == DimensionLayoutTemplatePresetCatalog.CenteredGridPresetId)
            {
                ValidateGridStarter(graph, plan, messages);
            }
            else if (example.LayoutPresetId == DimensionLayoutTemplatePresetCatalog.RadialRingsPresetId)
            {
                ValidateRadialStarter(graph, plan, messages);
            }
            else
            {
                messages.Add("Starter preset validation has no contract for layout preset '" + example.LayoutPresetId + "'.");
            }

            return messages;
        }

        private static void ValidateCommonPlan(
            DimensionCompiledGenerationPlan plan,
            List<string> messages)
        {
            int regionCount = CountRegions(plan);
            int passCount = CountGenerationPasses(plan);
            if (regionCount <= 0)
            {
                messages.Add("Starter preset did not compile any biome regions.");
            }

            if (passCount <= 0)
            {
                messages.Add("Starter preset did not compile any runtime terrain generation passes.");
            }

            if (passCount > 0 && regionCount > 0 && passCount < regionCount)
            {
                messages.Add("Starter preset compiled fewer terrain generation passes than biome regions.");
            }

            ValidateGenerationPassIds(plan, messages);
        }

        private static void ValidateSingleBiomeStarter(
            DimensionTemplateStarterGraph graph,
            DimensionCompiledGenerationPlan plan,
            List<string> messages)
        {
            if (graph.Layout.LayoutKind != DimensionLayoutKind.ManualRegions)
            {
                messages.Add("Starter Room must compile as a manual single-region layout.");
            }

            if (CountRegions(plan) != 1)
            {
                messages.Add("Starter Room must compile to exactly one biome region.");
                return;
            }

            DimensionCompiledBiomeRegion region = plan.BiomeRegions[0];
            int expectedSide = graph.PlayableLocalBounds.Size.x;
            int2 size = region.LocalBounds.Size;
            if (size.x != expectedSide || size.y != expectedSide)
            {
                messages.Add("Starter Room region size is " + FormatSize(size) + " but expected " + expectedSide + " x " + expectedSide + ".");
            }
        }

        private static void ValidateGridStarter(
            DimensionTemplateStarterGraph graph,
            DimensionCompiledGenerationPlan plan,
            List<string> messages)
        {
            if (graph.Layout.LayoutKind != DimensionLayoutKind.GridRegions)
            {
                messages.Add("Centered grid layout must compile as a grid-region layout.");
            }

            int expectedCells = ExpectedGridColumns * ExpectedGridRows;
            if (graph.Layout.GridCells.Length != expectedCells)
            {
                messages.Add("Centered grid layout must author " + expectedCells + " grid cells.");
            }

            if (CountRegions(plan) != expectedCells)
            {
                messages.Add("Centered grid layout must compile to " + expectedCells + " biome regions.");
                return;
            }

            int2 expectedCellSize = new int2(
                graph.Layout.GridCellSize.x,
                graph.Layout.GridCellSize.y);
            for (int i = 0; i < plan.BiomeRegions.Count; i++)
            {
                int2 size = plan.BiomeRegions[i].LocalBounds.Size;
                if (size.x != expectedCellSize.x || size.y != expectedCellSize.y)
                {
                    messages.Add("Centered grid layout region " + i + " is " + FormatSize(size) + " but expected " + FormatSize(expectedCellSize) + ".");
                    return;
                }
            }
        }

        private static void ValidateRadialStarter(
            DimensionTemplateStarterGraph graph,
            DimensionCompiledGenerationPlan plan,
            List<string> messages)
        {
            if (graph.Layout.LayoutKind != DimensionLayoutKind.RadialRings)
            {
                messages.Add("Radial Starter must compile as a radial-ring layout.");
            }

            int enabledRingCount = CountEnabledRadialRings(graph.Layout);
            if (enabledRingCount != ExpectedRadialRings)
            {
                messages.Add("Radial Starter must author " + ExpectedRadialRings + " enabled rings.");
            }

            int regionCount = CountRegions(plan);
            if (regionCount <= enabledRingCount)
            {
                messages.Add("Radial Starter must compile to row-run biome regions, not whole square regions.");
                return;
            }

            DimensionBounds union;
            long paintedTiles;
            if (!TryMeasureRegions(plan, out union, out paintedTiles))
            {
                messages.Add("Radial Starter could not measure its compiled footprint.");
                return;
            }

            int2 unionSize = union.Size;
            long envelopeTiles = Area(union);
            if (envelopeTiles <= 0)
            {
                messages.Add("Radial Starter compiled an invalid bounds envelope.");
                return;
            }

            float fillRatio = paintedTiles / (float)envelopeTiles;
            if (fillRatio < MinimumRadialFillRatio || fillRatio > MaximumRadialFillRatio)
            {
                messages.Add(
                    "Radial Starter fill ratio is " +
                    fillRatio.ToString("0.00") +
                    " inside a " +
                    FormatSize(unionSize) +
                    " envelope; expected a circular footprint, not a filled square.");
            }

            for (int i = 0; i < plan.BiomeRegions.Count; i++)
            {
                if (plan.BiomeRegions[i].LocalBounds.Size.y != 1)
                {
                    messages.Add("Radial Starter region " + i + " is not a one-row radial span.");
                    return;
                }
            }
        }

        private static void ValidateGenerationPassIds(
            DimensionCompiledGenerationPlan plan,
            List<string> messages)
        {
            if (plan.GenerationPasses == null)
            {
                return;
            }

            Dictionary<string, bool> passIds = new Dictionary<string, bool>();
            for (int i = 0; i < plan.GenerationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition pass = plan.GenerationPasses[i];
                if (string.IsNullOrEmpty(pass.PassId))
                {
                    messages.Add("Starter preset compiled a generation pass with no id.");
                    return;
                }

                if (passIds.ContainsKey(pass.PassId))
                {
                    messages.Add("Starter preset compiled duplicate generation pass id '" + pass.PassId + "'.");
                    return;
                }

                passIds.Add(pass.PassId, true);

                if (!pass.HasLocalBounds)
                {
                    messages.Add("Starter preset generation pass '" + pass.PassId + "' has no local bounds.");
                    return;
                }
            }
        }

        private static int CountEnabledRadialRings(DimensionLayoutTemplateAsset layout)
        {
            if (layout == null || layout.RadialRings == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < layout.RadialRings.Length; i++)
            {
                if (layout.RadialRings[i] != null && layout.RadialRings[i].Enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryMeasureRegions(
            DimensionCompiledGenerationPlan plan,
            out DimensionBounds union,
            out long paintedTiles)
        {
            union = new DimensionBounds(new int2(0, 0), new int2(0, 0));
            paintedTiles = 0L;
            bool found = false;

            if (plan.BiomeRegions == null)
            {
                return false;
            }

            for (int i = 0; i < plan.BiomeRegions.Count; i++)
            {
                DimensionBounds bounds = plan.BiomeRegions[i].LocalBounds;
                if (!IsValid(bounds))
                {
                    continue;
                }

                paintedTiles += Area(bounds);
                union = found ? Union(union, bounds) : bounds;
                found = true;
            }

            return found;
        }

        private static string FirstError(DimensionCompiledGenerationPlan plan)
        {
            if (plan.Issues != null)
            {
                for (int i = 0; i < plan.Issues.Count; i++)
                {
                    DimensionAuthoringIssue issue = plan.Issues[i];
                    if (issue.Severity == DimensionAuthoringSeverity.Error)
                    {
                        return issue.Code + " - " + issue.Message;
                    }
                }
            }

            return string.IsNullOrEmpty(plan.Message) ? plan.Code : plan.Message;
        }

        private static int CountRegions(DimensionCompiledGenerationPlan plan)
        {
            return plan.BiomeRegions == null ? 0 : plan.BiomeRegions.Count;
        }

        private static int CountGenerationPasses(DimensionCompiledGenerationPlan plan)
        {
            return plan.GenerationPasses == null ? 0 : plan.GenerationPasses.Count;
        }

        private static long Area(DimensionBounds bounds)
        {
            int2 size = bounds.Size;
            return size.x <= 0 || size.y <= 0
                ? 0L
                : (long)size.x * size.y;
        }

        private static bool IsValid(DimensionBounds bounds)
        {
            return bounds.MaxExclusive.x > bounds.Min.x &&
                   bounds.MaxExclusive.y > bounds.Min.y;
        }

        private static DimensionBounds Union(DimensionBounds a, DimensionBounds b)
        {
            return new DimensionBounds(
                new int2(math.min(a.Min.x, b.Min.x), math.min(a.Min.y, b.Min.y)),
                new int2(math.max(a.MaxExclusive.x, b.MaxExclusive.x), math.max(a.MaxExclusive.y, b.MaxExclusive.y)));
        }

        private static string FormatSize(int2 size)
        {
            return size.x + " x " + size.y;
        }
    }
}
