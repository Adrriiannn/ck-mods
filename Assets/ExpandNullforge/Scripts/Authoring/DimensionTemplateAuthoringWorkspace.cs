using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateAuthoringWorkspace
    {
        private DimensionTemplateManifestExportPreview manifestExportPreview;
        private bool manifestExportPreviewBuilt;
        private DimensionTemplateCustomizerSessionReport sessionReport;
        private bool sessionReportBuilt;
        private DimensionTemplateCustomizerNavigationModel navigation;
        private bool navigationBuilt;
        private DimensionTemplateCustomizerSearchIndex searchIndex;
        private bool searchIndexBuilt;
        private DimensionGenerationBudgetSummary generationBudget;
        private bool generationBudgetBuilt;
        private DimensionGenerationBudgetGuidanceCatalog generationBudgetGuidance;
        private bool generationBudgetGuidanceBuilt;
        private DimensionVisualAssetValidationReport visualAssetValidation;
        private bool visualAssetValidationBuilt;
        private DimensionFrameworkSetupGuide setupGuide;
        private bool setupGuideBuilt;
        private DimensionFrameworkExtractionReadinessReport extractionReadiness;
        private bool extractionReadinessBuilt;
        private DimensionFrameworkToolGuide toolGuide;
        private bool toolGuideBuilt;

        public DimensionTemplateAuthoringWorkspace(
            DimensionTemplateStarterGraph graph,
            DimensionTemplateStarterAssessment assessment,
            DimensionTemplateAssetSavePlan savePlan,
            IReadOnlyList<DimensionBiomeAuthoringOverview> biomeOverviews)
            : this(
                graph,
                assessment,
                savePlan,
                biomeOverviews,
                null)
        {
        }

        public DimensionTemplateAuthoringWorkspace(
            DimensionTemplateStarterGraph graph,
            DimensionTemplateStarterAssessment assessment,
            DimensionTemplateAssetSavePlan savePlan,
            IReadOnlyList<DimensionBiomeAuthoringOverview> biomeOverviews,
            DimensionTemplateCustomizerDashboard dashboard)
        {
            Graph = graph;
            Assessment = assessment;
            SavePlan = savePlan;
            BiomeOverviews = biomeOverviews ?? new List<DimensionBiomeAuthoringOverview>();
            BiomeRecipes = DimensionBiomeAuthoringRecipeUtility.BuildRecipes(BiomeOverviews);
            Dashboard = dashboard ?? DimensionTemplateCustomizerDashboardUtility.Build(
                assessment,
                BiomeOverviews);
            PreviewCanvas = assessment == null
                ? default(DimensionAuthoringCanvasModel)
                : DimensionAuthoringCanvasModelUtility.BuildCanvas(assessment.Preview);
            OperationPlan = assessment == null
                ? default(DimensionAuthoringOperationPlan)
                : DimensionAuthoringOperationPlanUtility.BuildPlan(
                    assessment.Preview,
                    assessment.Readiness);
        }

        public DimensionTemplateStarterGraph Graph { get; private set; }

        public DimensionTemplateStarterAssessment Assessment { get; private set; }

        public DimensionTemplateAssetSavePlan SavePlan { get; private set; }

        public IReadOnlyList<DimensionBiomeAuthoringOverview> BiomeOverviews { get; private set; }

        public IReadOnlyList<DimensionBiomeAuthoringRecipe> BiomeRecipes { get; private set; }

        public DimensionTemplateCustomizerDashboard Dashboard { get; private set; }

        public DimensionAuthoringCanvasModel PreviewCanvas { get; private set; }

        public DimensionAuthoringOperationPlan OperationPlan { get; private set; }

        public DimensionTemplateManifestExportPreview ManifestExportPreview
        {
            get
            {
                if (!manifestExportPreviewBuilt)
                {
                    manifestExportPreview = Graph == null
                        ? DimensionTemplateManifestExportPreviewBuilder.Build(null)
                        : DimensionTemplateManifestExportPreviewBuilder.Build(
                            Graph.Dimension,
                            OperationPlan,
                            false,
                            false,
                            "Dimension template authoring workspace manifest export preview.");
                    manifestExportPreviewBuilt = true;
                }

                return manifestExportPreview;
            }
        }

        public DimensionTemplateCustomizerSessionReport SessionReport
        {
            get
            {
                if (!sessionReportBuilt)
                {
                    sessionReport = DimensionTemplateCustomizerSessionReportUtility.Build(this);
                    sessionReportBuilt = true;
                }

                return sessionReport;
            }
        }

        public DimensionTemplateCustomizerNavigationModel Navigation
        {
            get
            {
                if (!navigationBuilt)
                {
                    navigation = DimensionTemplateCustomizerNavigationUtility.Build(this);
                    navigationBuilt = true;
                }

                return navigation;
            }
        }

        public DimensionTemplateCustomizerSearchIndex SearchIndex
        {
            get
            {
                if (!searchIndexBuilt)
                {
                    searchIndex = DimensionTemplateCustomizerSearchIndexUtility.Build(this);
                    searchIndexBuilt = true;
                }

                return searchIndex;
            }
        }

        public DimensionGenerationBudgetSummary GenerationBudget
        {
            get
            {
                if (!generationBudgetBuilt)
                {
                    generationBudget =
                        DimensionGenerationBudgetUtility.BuildSummary(BiomeOverviews);
                    generationBudgetBuilt = true;
                }

                return generationBudget;
            }
        }

        public DimensionGenerationBudgetGuidanceCatalog GenerationBudgetGuidance
        {
            get
            {
                if (!generationBudgetGuidanceBuilt)
                {
                    generationBudgetGuidance =
                        DimensionGenerationBudgetGuidanceUtility.Build(GenerationBudget);
                    generationBudgetGuidanceBuilt = true;
                }

                return generationBudgetGuidance;
            }
        }

        public DimensionVisualAssetValidationReport VisualAssetValidation
        {
            get
            {
                if (!visualAssetValidationBuilt)
                {
                    DimensionContentManifest manifest = ManifestExportPreview.Manifest;
                    IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences =
                        manifest.AssetReferences;
                    visualAssetValidation =
                        DimensionVisualAssetValidationUtility.Build(assetReferences);
                    visualAssetValidationBuilt = true;
                }

                return visualAssetValidation;
            }
        }

        public DimensionFrameworkSetupGuide SetupGuide
        {
            get
            {
                if (!setupGuideBuilt)
                {
                    setupGuide = DimensionFrameworkSetupGuideUtility.Build(this);
                    setupGuideBuilt = true;
                }

                return setupGuide;
            }
        }

        public DimensionFrameworkExtractionReadinessReport ExtractionReadiness
        {
            get
            {
                if (!extractionReadinessBuilt)
                {
                    extractionReadiness =
                        DimensionFrameworkExtractionReadinessUtility.Build(this);
                    extractionReadinessBuilt = true;
                }

                return extractionReadiness;
            }
        }

        public DimensionFrameworkToolGuide ToolGuide
        {
            get
            {
                if (!toolGuideBuilt)
                {
                    toolGuide = DimensionFrameworkToolGuideUtility.Build(this);
                    toolGuideBuilt = true;
                }

                return toolGuide;
            }
        }

        public DimensionTemplateCustomizerSectionDetail GetSectionDetail(string sectionId)
        {
            return DimensionTemplateCustomizerSectionDetailUtility.Build(this, sectionId);
        }

        public DimensionTemplateCustomizerActionCatalog GetActionCatalog(string sectionId)
        {
            return DimensionTemplateCustomizerActionCatalogUtility.Build(this, sectionId);
        }

        public DimensionTemplateCustomizerActionPreviewCatalog GetActionPreviews(string sectionId)
        {
            return DimensionTemplateCustomizerActionPreviewUtility.Build(this, sectionId);
        }

        public DimensionTemplateCustomizerCommandPlan GetCommandPlan(
            string sectionId,
            DimensionTemplateCustomizerFocusRequest focusRequest)
        {
            return DimensionTemplateCustomizerCommandPlanUtility.Build(
                this,
                sectionId,
                focusRequest);
        }

        public DimensionTemplateCustomizerPreparedCommand PrepareCommand(
            DimensionTemplateCustomizerCommandRequest request)
        {
            return DimensionTemplateCustomizerCommandRequestUtility.Prepare(this, request);
        }

        public DimensionTemplateCustomizerCommandPipeline GetCommandPipeline(
            DimensionTemplateCustomizerCommandRequest request)
        {
            return DimensionTemplateCustomizerCommandPipelineUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerInteractionState GetInteractionState(
            DimensionTemplateCustomizerInteractionRequest request)
        {
            return DimensionTemplateCustomizerInteractionStateUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerFieldSet GetFieldSet(
            DimensionTemplateCustomizerFocusRequest request)
        {
            return DimensionTemplateCustomizerFieldModelUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerFieldSet GetFieldSet(
            DimensionTemplateCustomizerFocusState focus)
        {
            return DimensionTemplateCustomizerFieldModelUtility.Build(this, focus);
        }

        public DimensionTemplateCustomizerFieldOptionCatalog GetFieldOptionCatalog(
            DimensionTemplateCustomizerFocusRequest request)
        {
            return DimensionTemplateCustomizerFieldOptionCatalogUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerFieldOptionCatalog GetFieldOptionCatalog(
            DimensionTemplateCustomizerFocusState focus)
        {
            return DimensionTemplateCustomizerFieldOptionCatalogUtility.Build(this, focus);
        }

        public DimensionTemplateCustomizerFieldOptionSet GetFieldOptions(
            DimensionTemplateCustomizerFocusRequest request,
            string fieldId)
        {
            return DimensionTemplateCustomizerFieldOptionCatalogUtility.BuildFieldOptions(
                this,
                request,
                fieldId);
        }

        public DimensionTemplateCustomizerFieldOptionSet GetFieldOptions(
            DimensionTemplateCustomizerFocusState focus,
            string fieldId)
        {
            return DimensionTemplateCustomizerFieldOptionCatalogUtility.BuildFieldOptions(
                this,
                focus,
                fieldId);
        }

        public DimensionTemplateCustomizerIssueResolutionCatalog GetIssueResolutionCatalog()
        {
            return DimensionTemplateCustomizerIssueResolutionUtility.Build(this);
        }

        public DimensionTemplateCustomizerIssueResolutionCatalog GetIssueResolutionCatalog(
            string sectionId)
        {
            return DimensionTemplateCustomizerIssueResolutionUtility.Build(this, sectionId);
        }

        public DimensionTemplateCustomizerIssueRepairCatalog GetIssueRepairCatalog()
        {
            return DimensionTemplateCustomizerIssueRepairPlanUtility.Build(this);
        }

        public DimensionTemplateCustomizerIssueRepairCatalog GetIssueRepairCatalog(
            string sectionId)
        {
            return DimensionTemplateCustomizerIssueRepairPlanUtility.Build(this, sectionId);
        }

        public DimensionTemplateCustomizerFieldEditPreview PreviewFieldEdit(
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            return DimensionTemplateCustomizerFieldEditPreviewUtility.Preview(this, request);
        }

        public DimensionTemplateCustomizerFieldApplyPlan GetFieldApplyPlan(
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            return DimensionTemplateCustomizerFieldApplyPlanUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerFieldEditBatchPreview PreviewFieldEditBatch(
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return DimensionTemplateCustomizerFieldEditBatchUtility.Preview(this, request);
        }

        public DimensionTemplateCustomizerFieldEditBatchPreview PreviewFieldEditBatch(
            IReadOnlyList<DimensionTemplateCustomizerFieldEditRequest> edits)
        {
            return DimensionTemplateCustomizerFieldEditBatchUtility.Preview(this, edits);
        }

        public DimensionTemplateCustomizerFieldEditBatchApplyPlan GetFieldEditBatchApplyPlan(
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return DimensionTemplateCustomizerFieldEditBatchApplyPlanUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerFieldEditBatchApplyPlan GetFieldEditBatchApplyPlan(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            return DimensionTemplateCustomizerFieldEditBatchApplyPlanUtility.Build(preview);
        }

        public DimensionTemplateCustomizerFieldEditChangeSet GetFieldEditChangeSet(
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return DimensionTemplateCustomizerFieldEditChangeSetUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerFieldEditChangeSet GetFieldEditChangeSet(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            return DimensionTemplateCustomizerFieldEditChangeSetUtility.Build(preview);
        }

        public DimensionTemplateCustomizerFieldEditRollbackPlan GetFieldEditRollbackPlan(
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return DimensionTemplateCustomizerFieldEditRollbackPlanUtility.Build(this, request);
        }

        public DimensionTemplateCustomizerFieldEditRollbackPlan GetFieldEditRollbackPlan(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            return DimensionTemplateCustomizerFieldEditRollbackPlanUtility.Build(preview);
        }

        public DimensionTemplateCustomizerFieldEditRollbackPlan GetFieldEditRollbackPlan(
            DimensionTemplateCustomizerFieldEditBatchApplyPlan applyPlan)
        {
            return DimensionTemplateCustomizerFieldEditRollbackPlanUtility.Build(applyPlan);
        }

        public DimensionTemplateCustomizerFieldEditExecutionResult ApplyFieldEdit(
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            return DimensionTemplateCustomizerFieldEditExecutor.Apply(this, request);
        }

        public DimensionTemplateCustomizerFieldEditExecutionResult ApplyFieldEditBatch(
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return DimensionTemplateCustomizerFieldEditExecutor.ApplyBatch(this, request);
        }

        public DimensionTemplateCustomizerFieldEditExecutionResult ApplyFieldEditBatch(
            DimensionTemplateCustomizerFieldEditBatchRequest request,
            string suggestedRootFolder)
        {
            return DimensionTemplateCustomizerFieldEditExecutor.ApplyBatch(
                this,
                request,
                suggestedRootFolder);
        }

        public DimensionTemplateCustomizerViewModel GetCustomizerViewModel(string sectionId)
        {
            return DimensionTemplateCustomizerViewModelUtility.Build(this, sectionId);
        }

        public IReadOnlyList<DimensionTemplateCustomizerSearchEntry> Search(
            string query,
            string sectionId,
            string biomeId,
            int maxResults)
        {
            return DimensionTemplateCustomizerSearchIndexUtility.Search(
                SearchIndex,
                query,
                sectionId,
                biomeId,
                maxResults);
        }

        public DimensionTemplateCustomizerFocusState ResolveFocus(
            DimensionTemplateCustomizerFocusRequest request)
        {
            return DimensionTemplateCustomizerFocusUtility.Resolve(this, request);
        }

        public DimensionAuthoringPreviewSummary Preview
        {
            get
            {
                return Assessment == null
                    ? default(DimensionAuthoringPreviewSummary)
                    : Assessment.Preview;
            }
        }

        public DimensionAuthoringReadinessReport Readiness
        {
            get
            {
                return Assessment == null
                    ? default(DimensionAuthoringReadinessReport)
                    : Assessment.Readiness;
            }
        }

        public bool ReadyForManifestExport
        {
            get
            {
                return Assessment != null && Assessment.ReadyForManifestExport;
            }
        }

        public bool ReadyForRuntimeGeneration
        {
            get
            {
                return Assessment != null && Assessment.ReadyForRuntimeGeneration;
            }
        }
    }

    public static class DimensionTemplateAuthoringWorkspaceUtility
    {
        public static DimensionTemplateAuthoringWorkspace BuildWorkspaceFromTemplate(
            DimensionTemplateAsset template,
            string suggestedRootFolder)
        {
            if (template == null)
            {
                return null;
            }

            DimensionAuthoringPreviewSummary preview =
                DimensionAuthoringPreviewBuilder.Build(template);
            DimensionBounds playableBounds = preview.PlayableLocalBounds;
            DimensionBounds reservedBounds = template.ReservedLocalBounds;
            BiomeTemplateAsset primaryBiome = First(template.Biomes);
            DimensionTemplateStarterGraph graph =
                new DimensionTemplateStarterGraph(
                    template,
                    template.LayoutTemplate,
                    primaryBiome,
                    First(template.EnvironmentProfiles),
                    primaryBiome == null ? null : primaryBiome.PaletteTemplate,
                    primaryBiome == null ? null : primaryBiome.GenerationProfile,
                    CollectGenerationPasses(template, primaryBiome),
                    CollectGenerationTables(template, primaryBiome),
                    playableBounds,
                    reservedBounds);

            return BuildWorkspace(graph, suggestedRootFolder);
        }

        public static DimensionTemplateAuthoringWorkspace CreateSingleBiomeStarterWorkspace(
            DimensionTemplateStarterRequest request,
            string suggestedRootFolder)
        {
            DimensionTemplateStarterGraph graph =
                DimensionTemplateStarterFactory.CreateSingleBiomeStarter(request);
            return BuildWorkspace(graph, suggestedRootFolder);
        }

        public static DimensionTemplateAuthoringWorkspace BuildWorkspace(
            DimensionTemplateStarterGraph graph,
            string suggestedRootFolder)
        {
            DimensionTemplateStarterAssessment assessment =
                DimensionTemplateStarterAssessmentUtility.Assess(graph);
            DimensionTemplateAssetSavePlan savePlan =
                DimensionTemplateAssetSavePlanUtility.CreateForStarterGraph(graph, suggestedRootFolder);

            IReadOnlyList<DimensionBiomeAuthoringOverview> biomeOverviews =
                assessment == null
                    ? new List<DimensionBiomeAuthoringOverview>()
                    : DimensionBiomeAuthoringOverviewUtility.BuildBiomeOverviews(
                        assessment.Preview,
                        assessment.Readiness);

            return new DimensionTemplateAuthoringWorkspace(
                graph,
                assessment,
                savePlan,
                biomeOverviews,
                DimensionTemplateCustomizerDashboardUtility.Build(assessment, biomeOverviews));
        }

        private static T First<T>(IReadOnlyList<T> values)
            where T : class
        {
            return values == null || values.Count == 0 ? null : values[0];
        }

        private static GenerationPassTemplateAsset[] CollectGenerationPasses(
            DimensionTemplateAsset template,
            BiomeTemplateAsset primaryBiome)
        {
            List<GenerationPassTemplateAsset> passes =
                new List<GenerationPassTemplateAsset>();
            AddUnique(template == null ? null : template.GlobalGenerationPasses, passes);
            AddUnique(primaryBiome == null ? null : primaryBiome.GetGenerationPassesWithProfile(), passes);
            return passes.ToArray();
        }

        private static GenerationTableTemplateAsset[] CollectGenerationTables(
            DimensionTemplateAsset template,
            BiomeTemplateAsset primaryBiome)
        {
            List<GenerationTableTemplateAsset> tables =
                new List<GenerationTableTemplateAsset>();
            AddUnique(template == null ? null : template.GlobalGenerationTables, tables);
            AddUnique(primaryBiome == null ? null : primaryBiome.GetGenerationTablesWithProfile(), tables);
            return tables.ToArray();
        }

        private static void AddUnique<T>(IReadOnlyList<T> source, List<T> target)
            where T : class
        {
            if (source == null || target == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (item == null || target.Contains(item))
                {
                    continue;
                }

                target.Add(item);
            }
        }
    }
}
