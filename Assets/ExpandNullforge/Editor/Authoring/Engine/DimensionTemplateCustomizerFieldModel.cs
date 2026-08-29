using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldKind
    {
        Text = 0,
        Integer = 1,
        Boolean = 2,
        Bounds = 3,
        Reference = 4,
        Count = 5,
        State = 6,
        Message = 7
    }

    public enum DimensionTemplateCustomizerFieldEditability
    {
        ReadOnly = 0,
        EditableInEditor = 1,
        RequiresEditorImplementation = 2,
        RuntimeOnly = 3
    }

    public sealed class DimensionTemplateCustomizerField
    {
        public DimensionTemplateCustomizerField(
            string fieldId,
            string label,
            string value,
            string hint,
            DimensionTemplateCustomizerFieldKind kind,
            DimensionTemplateCustomizerFieldEditability editability,
            bool required,
            bool hasWarning,
            string warning,
            int priority)
        {
            FieldId = fieldId ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Hint = hint ?? string.Empty;
            Kind = kind;
            Editability = editability;
            Required = required;
            HasWarning = hasWarning;
            Warning = warning ?? string.Empty;
            Priority = priority < 0 ? 0 : priority;
        }

        public string FieldId { get; private set; }

        public string Label { get; private set; }

        public string Value { get; private set; }

        public string Hint { get; private set; }

        public DimensionTemplateCustomizerFieldKind Kind { get; private set; }

        public DimensionTemplateCustomizerFieldEditability Editability { get; private set; }

        public bool Required { get; private set; }

        public bool HasWarning { get; private set; }

        public string Warning { get; private set; }

        public int Priority { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldSet
    {
        public DimensionTemplateCustomizerFieldSet(
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            string displayName,
            int fieldCount,
            int editableCount,
            int warningCount,
            IReadOnlyList<DimensionTemplateCustomizerField> fields)
        {
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FieldCount = fieldCount < 0 ? 0 : fieldCount;
            EditableCount = editableCount < 0 ? 0 : editableCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Fields = fields ?? new List<DimensionTemplateCustomizerField>();
        }

        public string SectionId { get; private set; }

        public string BiomeId { get; private set; }

        public string RecordKind { get; private set; }

        public string RecordId { get; private set; }

        public string DisplayName { get; private set; }

        public int FieldCount { get; private set; }

        public int EditableCount { get; private set; }

        public int WarningCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerField> Fields { get; private set; }
    }

    public static class DimensionTemplateCustomizerFieldModelUtility
    {
        public static DimensionTemplateCustomizerFieldSet Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusState focus)
        {
            if (workspace == null || workspace.Graph == null)
            {
                return CreateMissingFieldSet(focus);
            }

            DimensionTemplateCustomizerFocusState resolvedFocus = focus ??
                workspace.ResolveFocus(DimensionTemplateCustomizerFocusRequest.ForSection("overview"));
            string sectionId = ResolveSectionId(workspace, resolvedFocus);
            List<DimensionTemplateCustomizerField> fields = new List<DimensionTemplateCustomizerField>();

            BiomeTemplateAsset biome;
            if (TryResolveBiome(workspace, resolvedFocus, sectionId, out biome))
            {
                AddBiomeFields(fields, biome);
                return BuildFieldSet(
                    sectionId,
                    biome.BiomeId,
                    "Biome",
                    biome.BiomeId,
                    ResolveDisplayName(biome.DisplayName, biome.BiomeId, "Biome"),
                    fields);
            }

            if (ShouldUseDimensionFields(sectionId, resolvedFocus))
            {
                AddDimensionFields(fields, workspace.Graph.Dimension, workspace.Graph);
                return BuildFieldSet(
                    sectionId,
                    string.Empty,
                    "Dimension",
                    workspace.Graph.Dimension == null ? string.Empty : workspace.Graph.Dimension.DimensionId,
                    workspace.Graph.Dimension == null
                        ? "Dimension"
                        : ResolveDisplayName(
                            workspace.Graph.Dimension.DisplayName,
                            workspace.Graph.Dimension.DimensionId,
                            "Dimension"),
                    fields);
            }

            AddFocusFields(fields, resolvedFocus);
            return BuildFieldSet(
                sectionId,
                resolvedFocus == null ? string.Empty : resolvedFocus.BiomeId,
                resolvedFocus == null ? string.Empty : resolvedFocus.RecordKind,
                resolvedFocus == null ? string.Empty : resolvedFocus.RecordId,
                resolvedFocus == null
                    ? "Selection"
                    : ResolveDisplayName(
                        resolvedFocus.DisplayName,
                        resolvedFocus.RecordId,
                        resolvedFocus.Kind.ToString()),
                fields);
        }

        public static DimensionTemplateCustomizerFieldSet Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusRequest focusRequest)
        {
            DimensionTemplateCustomizerFocusState focus = workspace == null
                ? DimensionTemplateCustomizerFocusUtility.Resolve(null, focusRequest)
                : workspace.ResolveFocus(focusRequest);
            return Build(workspace, focus);
        }

        private static DimensionTemplateCustomizerFieldSet CreateMissingFieldSet(
            DimensionTemplateCustomizerFocusState focus)
        {
            List<DimensionTemplateCustomizerField> fields =
                new List<DimensionTemplateCustomizerField>();
            AddField(
                fields,
                "workspace",
                "Workspace",
                "Missing",
                "Select or create a Dimension Asset before editing fields.",
                DimensionTemplateCustomizerFieldKind.State,
                DimensionTemplateCustomizerFieldEditability.ReadOnly,
                true,
                true,
                "No authoring workspace is available.",
                0);

            return BuildFieldSet(
                focus == null ? string.Empty : focus.SectionId,
                focus == null ? string.Empty : focus.BiomeId,
                focus == null ? string.Empty : focus.RecordKind,
                focus == null ? string.Empty : focus.RecordId,
                "Missing workspace",
                fields);
        }

        private static void AddDimensionFields(
            List<DimensionTemplateCustomizerField> fields,
            DimensionTemplateAsset dimension,
            DimensionTemplateStarterGraph graph)
        {
            if (dimension == null)
            {
                AddField(
                    fields,
                    "dimension",
                    "Dimension",
                    "Missing",
                    "A Dimension Asset is required before any dimension can be generated.",
                    DimensionTemplateCustomizerFieldKind.State,
                    DimensionTemplateCustomizerFieldEditability.ReadOnly,
                    true,
                    true,
                    "Create or assign a Dimension Asset.",
                    0);
                return;
            }

            AddRequiredTextField(fields, "dimension-id", "Dimension ID", dimension.DimensionId, 0);
            AddRequiredTextField(fields, "display-name", "Display Name", dimension.DisplayName, 10);
            AddTextField(fields, "description", "Description", dimension.Description, 20);
            AddRequiredTextField(fields, "content-pack-id", "Content Pack ID", dimension.ContentPackId, 30);
            AddTextField(fields, "content-pack-name", "Content Pack Name", dimension.ContentPackDisplayName, 40);
            AddRequiredTextField(fields, "content-pack-version", "Content Pack Version", dimension.ContentPackVersion, 50);
            AddTextField(fields, "content-pack-author", "Content Pack Author", dimension.ContentPackAuthor, 60);
            AddField(
                fields,
                "minimum-api-version",
                "Minimum API Version",
                dimension.MinimumApiVersion.ToString(),
                "Minimum framework contract version required by this Dimension Asset.",
                DimensionTemplateCustomizerFieldKind.Integer,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                true,
                dimension.MinimumApiVersion < 1,
                "Minimum API version must be at least 1.",
                70);
            AddBoundsField(fields, "reserved-local-bounds", "Reserved Local Bounds", dimension.ReservedLocalBounds, 80);
            if (graph != null)
            {
                AddBoundsField(fields, "playable-local-bounds", "Playable Local Bounds", graph.PlayableLocalBounds, 90);
            }

            AddReferenceField(fields, "layout-template", "Layout Template", dimension.LayoutTemplate != null, 100);
            AddCountField(fields, "biomes", "Biomes", dimension.Biomes.Length, 110);
            AddCountField(fields, "global-scenes", "Global Scenes", dimension.GlobalScenes.Length, 130);
            AddCountField(fields, "global-items", "Global Items", dimension.GlobalItems.Length, 150);
            AddCountField(fields, "global-recipes", "Global Recipes", dimension.GlobalRecipes.Length, 160);
            AddCountField(fields, "global-workbenches", "Global Workbenches", dimension.GlobalWorkbenches.Length, 170);
            AddCountField(fields, "global-loot-tables", "Global Loot Tables", dimension.GlobalLootTables.Length, 180);
            AddCountField(fields, "global-animals", "Global Animals", dimension.GlobalAnimals.Length, 190);
            AddCountField(fields, "global-critters", "Global Critters", dimension.GlobalCritters.Length, 200);
            AddCountField(fields, "global-mobs", "Global Mobs", dimension.GlobalMobs.Length, 210);
            AddCountField(fields, "global-bosses", "Global Bosses", dimension.GlobalBosses.Length, 220);
            AddCountField(fields, "global-generation-passes", "Global Generation Passes", dimension.GlobalGenerationPasses.Length, 240);
            AddCountField(fields, "dependencies", "Dependency Content Packs", dimension.DependencyContentPackIds.Length, 260);
        }

        private static void AddBiomeFields(
            List<DimensionTemplateCustomizerField> fields,
            BiomeTemplateAsset biome)
        {
            if (biome == null)
            {
                return;
            }

            AddRequiredTextField(fields, "biome-id", "Biome ID", biome.BiomeId, 0);
            AddRequiredTextField(fields, "display-name", "Display Name", biome.DisplayName, 10);
            AddField(
                fields,
                "enabled",
                "Enabled",
                biome.Enabled ? "Yes" : "No",
                "Disabled biomes remain in the template but should not be used by runtime generation.",
                DimensionTemplateCustomizerFieldKind.Boolean,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                true,
                !biome.Enabled,
                biome.Enabled ? string.Empty : "This biome is disabled.",
                20);
            AddField(
                fields,
                "priority",
                "Priority",
                biome.Priority.ToString(),
                "Higher-priority biomes can win layout conflicts when the generator has to choose.",
                DimensionTemplateCustomizerFieldKind.Integer,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                false,
                false,
                string.Empty,
                30);
            AddField(
                fields,
                "fallback-bounds-enabled",
                "Fallback Bounds",
                biome.HasFallbackLocalBounds ? "Enabled" : "Not set",
                "Fallback bounds give previews and validation a safe biome area when the layout has not supplied one.",
                DimensionTemplateCustomizerFieldKind.Boolean,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                false,
                !biome.HasFallbackLocalBounds,
                biome.HasFallbackLocalBounds ? string.Empty : "No fallback bounds are set for this biome.",
                40);
            if (biome.HasFallbackLocalBounds)
            {
                AddBoundsField(fields, "fallback-local-bounds", "Fallback Local Bounds", biome.FallbackLocalBounds, 50);
            }

            AddTextField(fields, "environment-profile-id", "Environment Profile", biome.EnvironmentProfileId, 60);
            AddTextField(fields, "palette-id", "Palette", biome.PaletteAssetId, 70);
            AddCountField(fields, "scene-pool", "Scene Pool", biome.ScenePool.Length, 100);
            AddCountField(fields, "generation-passes", "Generation Passes", biome.GenerationPasses.Length, 130);
            AddCountField(fields, "floor-objects", "Semantic Floor Objects", biome.FloorObjectIds.Length, 150);
            AddCountField(fields, "wall-objects", "Semantic Wall Objects", biome.WallObjectIds.Length, 160);
            AddCountField(fields, "ore-objects", "Semantic Ore Objects", biome.OreObjectIds.Length, 170);
            AddTextField(fields, "notes", "Notes", biome.Notes, 190);
        }

        private static void AddFocusFields(
            List<DimensionTemplateCustomizerField> fields,
            DimensionTemplateCustomizerFocusState focus)
        {
            if (focus == null || !focus.HasFocus)
            {
                AddField(
                    fields,
                    "focus",
                    "Selection",
                    "None",
                    "Select a section, biome, record, action, or search result to inspect its fields.",
                    DimensionTemplateCustomizerFieldKind.State,
                    DimensionTemplateCustomizerFieldEditability.ReadOnly,
                    false,
                    false,
                    string.Empty,
                    0);
                return;
            }

            AddField(
                fields,
                "kind",
                "Kind",
                focus.Kind.ToString(),
                "The focused authoring surface.",
                DimensionTemplateCustomizerFieldKind.State,
                DimensionTemplateCustomizerFieldEditability.ReadOnly,
                false,
                false,
                string.Empty,
                0);
            AddTextField(fields, "section", "Section", focus.SectionId, 10);
            AddTextField(fields, "biome", "Biome", focus.BiomeId, 20);
            AddTextField(fields, "record-kind", "Record Kind", focus.RecordKind, 30);
            AddTextField(fields, "record-id", "Record ID", focus.RecordId, 40);
            AddTextField(fields, "action", "Action", focus.ActionId, 50);
            AddField(
                fields,
                "state",
                "State",
                focus.State.ToString(),
                "Readiness state for the focused item.",
                DimensionTemplateCustomizerFieldKind.State,
                DimensionTemplateCustomizerFieldEditability.ReadOnly,
                false,
                focus.Severity != DimensionAuthoringSeverity.Info,
                focus.Message,
                60);
            AddField(
                fields,
                "message",
                "Message",
                focus.Message,
                "Current guidance for this focus.",
                DimensionTemplateCustomizerFieldKind.Message,
                DimensionTemplateCustomizerFieldEditability.ReadOnly,
                false,
                false,
                string.Empty,
                70);
            if (focus.HasLocalBounds)
            {
                AddBoundsField(fields, "local-bounds", "Local Bounds", focus.LocalBounds, 80);
            }
        }

        private static bool TryResolveBiome(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusState focus,
            string sectionId,
            out BiomeTemplateAsset biome)
        {
            biome = null;
            if (workspace == null || workspace.Graph == null)
            {
                return false;
            }

            string requestedBiomeId = focus == null ? string.Empty : focus.BiomeId;
            if (string.IsNullOrEmpty(requestedBiomeId) &&
                focus != null &&
                focus.RecordKind == "Biome" &&
                !string.IsNullOrEmpty(focus.RecordId))
            {
                requestedBiomeId = focus.RecordId;
            }

            if (!string.IsNullOrEmpty(requestedBiomeId))
            {
                if (TryGetBiomeById(workspace.Graph.Dimension, requestedBiomeId, out biome))
                {
                    return true;
                }
            }

            if ((sectionId == "biomes" || sectionId == "terrain") && workspace.Graph.Biome != null)
            {
                biome = workspace.Graph.Biome;
                return true;
            }

            return false;
        }

        private static bool TryGetBiomeById(
            DimensionTemplateAsset dimension,
            string biomeId,
            out BiomeTemplateAsset biome)
        {
            biome = null;
            if (dimension == null || string.IsNullOrEmpty(biomeId))
            {
                return false;
            }

            BiomeTemplateAsset[] biomes = dimension.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset candidate = biomes[i];
                if (candidate != null && candidate.BiomeId == biomeId)
                {
                    biome = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldUseDimensionFields(
            string sectionId,
            DimensionTemplateCustomizerFocusState focus)
        {
            if (focus != null && focus.RecordKind == "Dimension")
            {
                return true;
            }

            return string.IsNullOrEmpty(sectionId) ||
                sectionId == "overview" ||
                sectionId == "dimension" ||
                sectionId == "layout" ||
                sectionId == "export";
        }

        private static string ResolveSectionId(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusState focus)
        {
            if (focus != null && !string.IsNullOrEmpty(focus.SectionId))
            {
                return focus.SectionId;
            }

            if (workspace != null && workspace.Navigation != null)
            {
                return workspace.Navigation.ActiveSectionId;
            }

            return "overview";
        }

        private static void AddRequiredTextField(
            List<DimensionTemplateCustomizerField> fields,
            string fieldId,
            string label,
            string value,
            int priority)
        {
            bool missing = string.IsNullOrEmpty(value);
            AddField(
                fields,
                fieldId,
                label,
                value,
                "Required authoring value.",
                DimensionTemplateCustomizerFieldKind.Text,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                true,
                missing,
                missing ? label + " is required." : string.Empty,
                priority);
        }

        private static void AddTextField(
            List<DimensionTemplateCustomizerField> fields,
            string fieldId,
            string label,
            string value,
            int priority)
        {
            AddField(
                fields,
                fieldId,
                label,
                value,
                string.Empty,
                DimensionTemplateCustomizerFieldKind.Text,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                false,
                false,
                string.Empty,
                priority);
        }

        private static void AddReferenceField(
            List<DimensionTemplateCustomizerField> fields,
            string fieldId,
            string label,
            bool present,
            int priority)
        {
            AddField(
                fields,
                fieldId,
                label,
                present ? "Assigned" : "Missing",
                string.Empty,
                DimensionTemplateCustomizerFieldKind.Reference,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                false,
                !present,
                present ? string.Empty : label + " is not assigned.",
                priority);
        }

        private static void AddCountField(
            List<DimensionTemplateCustomizerField> fields,
            string fieldId,
            string label,
            int count,
            int priority)
        {
            AddField(
                fields,
                fieldId,
                label,
                count < 0 ? "0" : count.ToString(),
                string.Empty,
                DimensionTemplateCustomizerFieldKind.Count,
                DimensionTemplateCustomizerFieldEditability.ReadOnly,
                false,
                false,
                string.Empty,
                priority);
        }

        private static void AddBoundsField(
            List<DimensionTemplateCustomizerField> fields,
            string fieldId,
            string label,
            DimensionBounds bounds,
            int priority)
        {
            AddField(
                fields,
                fieldId,
                label,
                FormatBounds(bounds),
                "Inclusive minimum and exclusive maximum local tile bounds.",
                DimensionTemplateCustomizerFieldKind.Bounds,
                DimensionTemplateCustomizerFieldEditability.EditableInEditor,
                false,
                bounds.Size.x <= 0 || bounds.Size.y <= 0,
                bounds.Size.x <= 0 || bounds.Size.y <= 0
                    ? "Bounds must have a positive size."
                    : string.Empty,
                priority);
        }

        private static void AddField(
            List<DimensionTemplateCustomizerField> fields,
            string fieldId,
            string label,
            string value,
            string hint,
            DimensionTemplateCustomizerFieldKind kind,
            DimensionTemplateCustomizerFieldEditability editability,
            bool required,
            bool hasWarning,
            string warning,
            int priority)
        {
            if (fields == null)
            {
                return;
            }

            fields.Add(new DimensionTemplateCustomizerField(
                fieldId,
                label,
                value,
                hint,
                kind,
                editability,
                required,
                hasWarning,
                warning,
                priority));
        }

        private static DimensionTemplateCustomizerFieldSet BuildFieldSet(
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            string displayName,
            List<DimensionTemplateCustomizerField> fields)
        {
            int editableCount = 0;
            int warningCount = 0;
            if (fields != null)
            {
                fields.Sort(CompareFields);
                for (int i = 0; i < fields.Count; i++)
                {
                    DimensionTemplateCustomizerField field = fields[i];
                    if (field.Editability == DimensionTemplateCustomizerFieldEditability.EditableInEditor)
                    {
                        editableCount++;
                    }

                    if (field.HasWarning)
                    {
                        warningCount++;
                    }
                }
            }

            return new DimensionTemplateCustomizerFieldSet(
                sectionId,
                biomeId,
                recordKind,
                recordId,
                displayName,
                fields == null ? 0 : fields.Count,
                editableCount,
                warningCount,
                fields);
        }

        private static int CompareFields(
            DimensionTemplateCustomizerField left,
            DimensionTemplateCustomizerField right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return string.CompareOrdinal(left.Label, right.Label);
        }

        private static string FormatBounds(DimensionBounds bounds)
        {
            return "(" +
                bounds.Min.x.ToString() +
                ", " +
                bounds.Min.y.ToString() +
                ") -> (" +
                bounds.MaxExclusive.x.ToString() +
                ", " +
                bounds.MaxExclusive.y.ToString() +
                ") " +
                FormatSize(bounds.Size.x, bounds.Size.y);
        }

        private static string FormatSize(int width, int height)
        {
            return "[" + width.ToString() + " x " + height.ToString() + "]";
        }

        private static string ResolveDisplayName(
            string displayName,
            string id,
            string fallback)
        {
            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            return fallback ?? string.Empty;
        }
    }
}
