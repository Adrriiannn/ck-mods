using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldOptionKind
    {
        None = 0,
        Boolean = 1,
        Reference = 2,
        SuggestedValue = 3
    }

    public sealed class DimensionTemplateCustomizerFieldOption
    {
        public DimensionTemplateCustomizerFieldOption(
            string value,
            string label,
            string hint,
            DimensionTemplateCustomizerFieldOptionKind kind,
            bool selected,
            bool available,
            bool hasWarning,
            string warning,
            int priority)
        {
            Value = value ?? string.Empty;
            Label = label ?? string.Empty;
            Hint = hint ?? string.Empty;
            Kind = kind;
            Selected = selected;
            Available = available;
            HasWarning = hasWarning;
            Warning = warning ?? string.Empty;
            Priority = priority < 0 ? 0 : priority;
        }

        public string Value { get; private set; }

        public string Label { get; private set; }

        public string Hint { get; private set; }

        public DimensionTemplateCustomizerFieldOptionKind Kind { get; private set; }

        public bool Selected { get; private set; }

        public bool Available { get; private set; }

        public bool HasWarning { get; private set; }

        public string Warning { get; private set; }

        public int Priority { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldOptionSet
    {
        public DimensionTemplateCustomizerFieldOptionSet(
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            string fieldId,
            string fieldLabel,
            string currentValue,
            DimensionTemplateCustomizerFieldKind fieldKind,
            bool hasFixedOptions,
            bool allowsFreeText,
            bool allowsEmpty,
            int optionCount,
            int availableCount,
            int warningCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldOption> options)
        {
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            FieldId = fieldId ?? string.Empty;
            FieldLabel = fieldLabel ?? string.Empty;
            CurrentValue = currentValue ?? string.Empty;
            FieldKind = fieldKind;
            HasFixedOptions = hasFixedOptions;
            AllowsFreeText = allowsFreeText;
            AllowsEmpty = allowsEmpty;
            OptionCount = optionCount < 0 ? 0 : optionCount;
            AvailableCount = availableCount < 0 ? 0 : availableCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Options = options ?? new List<DimensionTemplateCustomizerFieldOption>();
        }

        public string SectionId { get; private set; }

        public string BiomeId { get; private set; }

        public string RecordKind { get; private set; }

        public string RecordId { get; private set; }

        public string FieldId { get; private set; }

        public string FieldLabel { get; private set; }

        public string CurrentValue { get; private set; }

        public DimensionTemplateCustomizerFieldKind FieldKind { get; private set; }

        public bool HasFixedOptions { get; private set; }

        public bool AllowsFreeText { get; private set; }

        public bool AllowsEmpty { get; private set; }

        public int OptionCount { get; private set; }

        public int AvailableCount { get; private set; }

        public int WarningCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldOption> Options { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldOptionCatalog
    {
        public DimensionTemplateCustomizerFieldOptionCatalog(
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            int fieldCount,
            int optionSetCount,
            int optionCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldOptionSet> optionSets)
        {
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            FieldCount = fieldCount < 0 ? 0 : fieldCount;
            OptionSetCount = optionSetCount < 0 ? 0 : optionSetCount;
            OptionCount = optionCount < 0 ? 0 : optionCount;
            OptionSets = optionSets ?? new List<DimensionTemplateCustomizerFieldOptionSet>();
        }

        public string SectionId { get; private set; }

        public string BiomeId { get; private set; }

        public string RecordKind { get; private set; }

        public string RecordId { get; private set; }

        public int FieldCount { get; private set; }

        public int OptionSetCount { get; private set; }

        public int OptionCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldOptionSet> OptionSets { get; private set; }
    }

    public static class DimensionTemplateCustomizerFieldOptionCatalogUtility
    {
        public static DimensionTemplateCustomizerFieldOptionCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusRequest focusRequest)
        {
            DimensionTemplateCustomizerFieldSet fieldSet =
                workspace == null
                    ? null
                    : workspace.GetFieldSet(focusRequest);
            return Build(workspace, fieldSet);
        }

        public static DimensionTemplateCustomizerFieldOptionCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusState focus)
        {
            DimensionTemplateCustomizerFieldSet fieldSet =
                workspace == null
                    ? null
                    : workspace.GetFieldSet(focus);
            return Build(workspace, fieldSet);
        }

        public static DimensionTemplateCustomizerFieldOptionSet BuildFieldOptions(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            string fieldId)
        {
            DimensionTemplateCustomizerFieldSet fieldSet =
                workspace == null
                    ? null
                    : workspace.GetFieldSet(focusRequest);
            return BuildFieldOptions(workspace, fieldSet, fieldId);
        }

        public static DimensionTemplateCustomizerFieldOptionSet BuildFieldOptions(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusState focus,
            string fieldId)
        {
            DimensionTemplateCustomizerFieldSet fieldSet =
                workspace == null
                    ? null
                    : workspace.GetFieldSet(focus);
            return BuildFieldOptions(workspace, fieldSet, fieldId);
        }

        private static DimensionTemplateCustomizerFieldOptionCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldSet fieldSet)
        {
            List<DimensionTemplateCustomizerFieldOptionSet> optionSets =
                new List<DimensionTemplateCustomizerFieldOptionSet>();
            int optionCount = 0;
            IReadOnlyList<DimensionTemplateCustomizerField> fields =
                fieldSet == null ? null : fieldSet.Fields;
            if (fields != null)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    DimensionTemplateCustomizerField field = fields[i];
                    if (field == null)
                    {
                        continue;
                    }

                    DimensionTemplateCustomizerFieldOptionSet options =
                        BuildOptionsForField(workspace, fieldSet, field);
                    if (options.OptionCount > 0 || options.HasFixedOptions || options.AllowsFreeText)
                    {
                        optionSets.Add(options);
                        optionCount += options.OptionCount;
                    }
                }
            }

            return new DimensionTemplateCustomizerFieldOptionCatalog(
                fieldSet == null ? string.Empty : fieldSet.SectionId,
                fieldSet == null ? string.Empty : fieldSet.BiomeId,
                fieldSet == null ? string.Empty : fieldSet.RecordKind,
                fieldSet == null ? string.Empty : fieldSet.RecordId,
                fields == null ? 0 : fields.Count,
                optionSets.Count,
                optionCount,
                optionSets);
        }

        private static DimensionTemplateCustomizerFieldOptionSet BuildFieldOptions(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldSet fieldSet,
            string fieldId)
        {
            DimensionTemplateCustomizerField field = FindField(fieldSet, fieldId);
            if (field == null)
            {
                return new DimensionTemplateCustomizerFieldOptionSet(
                    fieldSet == null ? string.Empty : fieldSet.SectionId,
                    fieldSet == null ? string.Empty : fieldSet.BiomeId,
                    fieldSet == null ? string.Empty : fieldSet.RecordKind,
                    fieldSet == null ? string.Empty : fieldSet.RecordId,
                    fieldId,
                    string.Empty,
                    string.Empty,
                    DimensionTemplateCustomizerFieldKind.Text,
                    false,
                    false,
                    false,
                    0,
                    0,
                    0,
                    new List<DimensionTemplateCustomizerFieldOption>());
            }

            return BuildOptionsForField(workspace, fieldSet, field);
        }

        private static DimensionTemplateCustomizerFieldOptionSet BuildOptionsForField(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldSet fieldSet,
            DimensionTemplateCustomizerField field)
        {
            List<DimensionTemplateCustomizerFieldOption> options =
                new List<DimensionTemplateCustomizerFieldOption>();
            bool hasFixedOptions = false;
            bool allowsFreeText = IsFreeTextAllowed(field);
            bool allowsEmpty = IsEmptyAllowed(field);

            if (field.Kind == DimensionTemplateCustomizerFieldKind.Boolean)
            {
                hasFixedOptions = true;
                AddBooleanOptions(options, field);
            }
            else if (field.FieldId == "environment-profile-id")
            {
                hasFixedOptions = true;
                AddEmptyOption(
                    options,
                    field,
                    "Auto / inherited",
                    "Let the biome use its assigned template or content preset environment profile.",
                    0);
                AddEnvironmentProfileOptions(options, workspace, field);
                AddCurrentCustomValueOption(options, field, "Custom environment profile ID");
            }
            else if (field.FieldId == "palette-id")
            {
                hasFixedOptions = true;
                AddEmptyOption(
                    options,
                    field,
                    "Auto / inherited",
                    "Let the biome use its assigned template or content preset palette.",
                    0);
                AddPaletteOptions(options, workspace, field);
                AddCurrentCustomValueOption(options, field, "Custom palette ID");
            }
            else if (field.FieldId == "generation-profile")
            {
                hasFixedOptions = true;
                AddGenerationProfileOptions(options, workspace, field);
            }
            else if (field.FieldId == "layout-template")
            {
                hasFixedOptions = true;
                AddLayoutTemplateOptions(options, workspace, field);
            }

            options.Sort(CompareOptions);
            return CreateOptionSet(fieldSet, field, hasFixedOptions, allowsFreeText, allowsEmpty, options);
        }

        private static void AddBooleanOptions(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateCustomizerField field)
        {
            bool current = IsTruthy(field == null ? string.Empty : field.Value);
            AddOption(
                options,
                "enabled",
                "Enabled",
                "Turn this setting on.",
                DimensionTemplateCustomizerFieldOptionKind.Boolean,
                current,
                true,
                false,
                string.Empty,
                0);
            AddOption(
                options,
                "disabled",
                "Disabled",
                "Turn this setting off.",
                DimensionTemplateCustomizerFieldOptionKind.Boolean,
                !current,
                true,
                false,
                string.Empty,
                10);
        }

        private static void AddEnvironmentProfileOptions(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerField field)
        {
            DimensionTemplateStarterGraph graph = workspace == null ? null : workspace.Graph;
            DimensionTemplateAsset dimension = graph == null ? null : graph.Dimension;
            if (dimension != null)
            {
                EnvironmentProfileTemplateAsset[] profiles = dimension.EnvironmentProfiles;
                for (int i = 0; i < profiles.Length; i++)
                {
                    AddEnvironmentProfileOption(options, profiles[i], field, 20 + i);
                }
            }

            AddEnvironmentProfileOption(options, graph == null ? null : graph.EnvironmentProfile, field, 1000);
            AddBiomeEnvironmentProfileOptions(options, dimension, field, 2000);
        }

        private static void AddBiomeEnvironmentProfileOptions(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateAsset dimension,
            DimensionTemplateCustomizerField field,
            int basePriority)
        {
            if (dimension == null)
            {
                return;
            }

            BiomeTemplateAsset[] biomes = dimension.Biomes;
            int priority = basePriority;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                AddEnvironmentProfileOption(options, biome.EnvironmentProfileTemplate, field, priority++);
                EnvironmentProfileTemplateAsset[] profiles = biome.GetEnvironmentProfileTemplatesWithPresets();
                for (int profileIndex = 0; profileIndex < profiles.Length; profileIndex++)
                {
                    AddEnvironmentProfileOption(options, profiles[profileIndex], field, priority++);
                }
            }
        }

        private static void AddEnvironmentProfileOption(
            List<DimensionTemplateCustomizerFieldOption> options,
            EnvironmentProfileTemplateAsset profile,
            DimensionTemplateCustomizerField field,
            int priority)
        {
            if (profile == null)
            {
                return;
            }

            string id = profile.ProfileId;
            if (string.IsNullOrEmpty(id) || ContainsOption(options, id))
            {
                return;
            }

            string label = string.IsNullOrEmpty(profile.DisplayName) ? id : profile.DisplayName;
            AddOption(
                options,
                id,
                label,
                profile.Enabled
                    ? "Environment profile: " + id
                    : "Environment profile is currently disabled: " + id,
                DimensionTemplateCustomizerFieldOptionKind.Reference,
                IsSelected(field, id),
                profile.Enabled,
                !profile.Enabled,
                profile.Enabled ? string.Empty : "This environment profile is disabled.",
                priority);
        }

        private static void AddPaletteOptions(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerField field)
        {
            DimensionTemplateStarterGraph graph = workspace == null ? null : workspace.Graph;
            DimensionTemplateAsset dimension = graph == null ? null : graph.Dimension;
            AddPaletteOption(options, graph == null ? null : graph.Palette, field, 1000);
            if (dimension == null)
            {
                return;
            }

            BiomeTemplateAsset[] biomes = dimension.Biomes;
            int priority = 2000;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                AddPaletteOption(options, biome.PaletteTemplate, field, priority++);
                BiomePaletteTemplateAsset[] palettes = biome.GetPaletteTemplatesWithPresets();
                for (int paletteIndex = 0; paletteIndex < palettes.Length; paletteIndex++)
                {
                    AddPaletteOption(options, palettes[paletteIndex], field, priority++);
                }
            }
        }

        private static void AddPaletteOption(
            List<DimensionTemplateCustomizerFieldOption> options,
            BiomePaletteTemplateAsset palette,
            DimensionTemplateCustomizerField field,
            int priority)
        {
            if (palette == null)
            {
                return;
            }

            string id = palette.PaletteId;
            if (string.IsNullOrEmpty(id) || ContainsOption(options, id))
            {
                return;
            }

            string label = string.IsNullOrEmpty(palette.DisplayName) ? id : palette.DisplayName;
            AddOption(
                options,
                id,
                label,
                palette.Enabled
                    ? "Biome palette: " + id
                    : "Biome palette is currently disabled: " + id,
                DimensionTemplateCustomizerFieldOptionKind.Reference,
                IsSelected(field, id),
                palette.Enabled,
                !palette.Enabled,
                palette.Enabled ? string.Empty : "This biome palette is disabled.",
                priority);
        }

        private static void AddGenerationProfileOptions(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerField field)
        {
            DimensionTemplateStarterGraph graph = workspace == null ? null : workspace.Graph;
            DimensionTemplateAsset dimension = graph == null ? null : graph.Dimension;
            AddGenerationProfileOption(options, graph == null ? null : graph.GenerationProfile, field, 1000);
            if (dimension == null)
            {
                return;
            }

            BiomeTemplateAsset[] biomes = dimension.Biomes;
            int priority = 2000;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                AddGenerationProfileOption(options, biome.GenerationProfile, field, priority++);
                BiomeContentPresetAsset[] presets = biome.ContentPresets;
                for (int presetIndex = 0; presetIndex < presets.Length; presetIndex++)
                {
                    BiomeContentPresetAsset preset = presets[presetIndex];
                    if (preset != null && preset.HasAnyContent)
                    {
                        AddGenerationProfileOption(options, preset.GenerationProfile, field, priority++);
                    }
                }
            }
        }

        private static void AddGenerationProfileOption(
            List<DimensionTemplateCustomizerFieldOption> options,
            BiomeGenerationProfileAsset profile,
            DimensionTemplateCustomizerField field,
            int priority)
        {
            if (profile == null)
            {
                return;
            }

            string id = profile.ProfileId;
            if (string.IsNullOrEmpty(id) || ContainsOption(options, id))
            {
                return;
            }

            string label = string.IsNullOrEmpty(profile.DisplayName) ? id : profile.DisplayName;
            AddOption(
                options,
                id,
                label,
                "Biome generation profile: " + id,
                DimensionTemplateCustomizerFieldOptionKind.Reference,
                IsSelected(field, id),
                true,
                false,
                string.Empty,
                priority);
        }

        private static void AddLayoutTemplateOptions(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerField field)
        {
            DimensionTemplateStarterGraph graph = workspace == null ? null : workspace.Graph;
            AddLayoutTemplateOption(options, graph == null ? null : graph.Layout, field, 1000);
        }

        private static void AddLayoutTemplateOption(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionLayoutTemplateAsset layout,
            DimensionTemplateCustomizerField field,
            int priority)
        {
            if (layout == null)
            {
                return;
            }

            string id = layout.LayoutId;
            if (string.IsNullOrEmpty(id) || ContainsOption(options, id))
            {
                return;
            }

            string label = string.IsNullOrEmpty(layout.DisplayName) ? id : layout.DisplayName;
            AddOption(
                options,
                id,
                label,
                "Layout template: " + id + " (" + layout.LayoutKind.ToString() + ")",
                DimensionTemplateCustomizerFieldOptionKind.Reference,
                IsSelected(field, id),
                true,
                false,
                string.Empty,
                priority);
        }

        private static void AddEmptyOption(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateCustomizerField field,
            string label,
            string hint,
            int priority)
        {
            AddOption(
                options,
                string.Empty,
                label,
                hint,
                DimensionTemplateCustomizerFieldOptionKind.SuggestedValue,
                string.IsNullOrEmpty(field == null ? string.Empty : field.Value),
                true,
                false,
                string.Empty,
                priority);
        }

        private static void AddCurrentCustomValueOption(
            List<DimensionTemplateCustomizerFieldOption> options,
            DimensionTemplateCustomizerField field,
            string labelPrefix)
        {
            string value = field == null ? string.Empty : field.Value;
            if (string.IsNullOrEmpty(value) || ContainsOption(options, value))
            {
                return;
            }

            AddOption(
                options,
                value,
                labelPrefix + ": " + value,
                "This value is currently assigned but was not found among the known template assets.",
                DimensionTemplateCustomizerFieldOptionKind.SuggestedValue,
                true,
                true,
                true,
                "No matching template asset was found for this current value.",
                90000);
        }

        private static DimensionTemplateCustomizerFieldOptionSet CreateOptionSet(
            DimensionTemplateCustomizerFieldSet fieldSet,
            DimensionTemplateCustomizerField field,
            bool hasFixedOptions,
            bool allowsFreeText,
            bool allowsEmpty,
            List<DimensionTemplateCustomizerFieldOption> options)
        {
            int availableCount = 0;
            int warningCount = 0;
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    DimensionTemplateCustomizerFieldOption option = options[i];
                    if (option == null)
                    {
                        continue;
                    }

                    if (option.Available)
                    {
                        availableCount++;
                    }

                    if (option.HasWarning)
                    {
                        warningCount++;
                    }
                }
            }

            return new DimensionTemplateCustomizerFieldOptionSet(
                fieldSet == null ? string.Empty : fieldSet.SectionId,
                fieldSet == null ? string.Empty : fieldSet.BiomeId,
                fieldSet == null ? string.Empty : fieldSet.RecordKind,
                fieldSet == null ? string.Empty : fieldSet.RecordId,
                field == null ? string.Empty : field.FieldId,
                field == null ? string.Empty : field.Label,
                field == null ? string.Empty : field.Value,
                field == null ? DimensionTemplateCustomizerFieldKind.Text : field.Kind,
                hasFixedOptions,
                allowsFreeText,
                allowsEmpty,
                options == null ? 0 : options.Count,
                availableCount,
                warningCount,
                options);
        }

        private static DimensionTemplateCustomizerField FindField(
            DimensionTemplateCustomizerFieldSet fieldSet,
            string fieldId)
        {
            IReadOnlyList<DimensionTemplateCustomizerField> fields =
                fieldSet == null ? null : fieldSet.Fields;
            if (fields == null || string.IsNullOrEmpty(fieldId))
            {
                return null;
            }

            for (int i = 0; i < fields.Count; i++)
            {
                DimensionTemplateCustomizerField field = fields[i];
                if (field != null && field.FieldId == fieldId)
                {
                    return field;
                }
            }

            return null;
        }

        private static void AddOption(
            List<DimensionTemplateCustomizerFieldOption> options,
            string value,
            string label,
            string hint,
            DimensionTemplateCustomizerFieldOptionKind kind,
            bool selected,
            bool available,
            bool hasWarning,
            string warning,
            int priority)
        {
            if (options == null)
            {
                return;
            }

            options.Add(new DimensionTemplateCustomizerFieldOption(
                value,
                string.IsNullOrEmpty(label) ? value : label,
                hint,
                kind,
                selected,
                available,
                hasWarning,
                warning,
                priority));
        }

        private static bool ContainsOption(
            IReadOnlyList<DimensionTemplateCustomizerFieldOption> options,
            string value)
        {
            if (options == null)
            {
                return false;
            }

            string normalized = value ?? string.Empty;
            for (int i = 0; i < options.Count; i++)
            {
                DimensionTemplateCustomizerFieldOption option = options[i];
                if (option != null && option.Value == normalized)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFreeTextAllowed(DimensionTemplateCustomizerField field)
        {
            if (field == null)
            {
                return false;
            }

            return field.Kind == DimensionTemplateCustomizerFieldKind.Text ||
                field.Kind == DimensionTemplateCustomizerFieldKind.Integer ||
                field.Kind == DimensionTemplateCustomizerFieldKind.Bounds ||
                field.FieldId == "environment-profile-id" ||
                field.FieldId == "palette-id";
        }

        private static bool IsEmptyAllowed(DimensionTemplateCustomizerField field)
        {
            if (field == null)
            {
                return false;
            }

            return !field.Required ||
                field.FieldId == "environment-profile-id" ||
                field.FieldId == "palette-id";
        }

        private static bool IsSelected(
            DimensionTemplateCustomizerField field,
            string value)
        {
            return string.Equals(field == null ? string.Empty : field.Value, value ?? string.Empty);
        }

        private static bool IsTruthy(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "yes" ||
                normalized == "true" ||
                normalized == "enabled" ||
                normalized == "1";
        }

        private static int CompareOptions(
            DimensionTemplateCustomizerFieldOption left,
            DimensionTemplateCustomizerFieldOption right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return string.CompareOrdinal(left.Label, right.Label);
        }
    }
}
