using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCreationWizardFieldKind
    {
        Text = 0,
        Preset = 1,
        Folder = 2
    }

    public readonly struct DimensionTemplateCreationWizardOption
    {
        public readonly string OptionId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly bool Selected;
        public readonly bool Available;

        public DimensionTemplateCreationWizardOption(
            string optionId,
            string displayName,
            string description,
            bool selected,
            bool available)
        {
            OptionId = optionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Selected = selected;
            Available = available;
        }
    }

    public readonly struct DimensionTemplateCreationWizardField
    {
        public readonly string FieldId;
        public readonly string Label;
        public readonly string Value;
        public readonly string Placeholder;
        public readonly string HelpText;
        public readonly DimensionTemplateCreationWizardFieldKind Kind;
        public readonly bool Required;
        public readonly bool Editable;
        public readonly IReadOnlyList<DimensionTemplateCreationWizardOption> Options;

        public DimensionTemplateCreationWizardField(
            string fieldId,
            string label,
            string value,
            string placeholder,
            string helpText,
            DimensionTemplateCreationWizardFieldKind kind,
            bool required,
            bool editable,
            IReadOnlyList<DimensionTemplateCreationWizardOption> options)
        {
            FieldId = fieldId ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Placeholder = placeholder ?? string.Empty;
            HelpText = helpText ?? string.Empty;
            Kind = kind;
            Required = required;
            Editable = editable;
            Options = options ?? new List<DimensionTemplateCreationWizardOption>();
        }
    }

    public sealed class DimensionTemplateCreationWizardRequest
    {
        public string ExampleId = DimensionTemplateAuthoringExampleCatalog.MinimalRoomExampleId;
        public string ModIdPrefix = string.Empty;
        public string DimensionId = string.Empty;
        public string DimensionDisplayName = "Dimension";
        public string ContentPackId = string.Empty;
        public string ContentPackDisplayName = string.Empty;
        public string ContentPackAuthor = string.Empty;
        public string BiomeId = string.Empty;
        public string BiomeDisplayName = string.Empty;
        public string ZoneId = string.Empty;
        // A dimension extends from its origin, so the origin must sit PAST the protected ±5000
        // overworld band by the dimension's half-extent — an origin exactly at 5000 makes the
        // playable bounds straddle the band and registration is rejected. 7000 clears it with
        // room for reasonably large dimensions; north-aligned (+Y) matches the slot allocator.
        public int AbsoluteOriginX = 0;
        public int AbsoluteOriginY = 7000;
        public int HalfSizeTiles = 0;
        public int ReservedShellPaddingTiles = -1;
        public string FloorResourceKey = string.Empty;
        public string WallResourceKey = string.Empty;
        public string LiquidResourceKey = string.Empty;
        public string OreResourceKey = string.Empty;
        public string ObjectResourceKey = string.Empty;
        public string TerrainGenerationProviderId = string.Empty;
        public string SuggestedRootFolder = string.Empty;
    }

    public sealed class DimensionTemplateCreationWizardModel
    {
        public DimensionTemplateCreationWizardModel(
            DimensionTemplateCreationWizardRequest request,
            IReadOnlyList<DimensionTemplateCreationWizardField> fields,
            IReadOnlyList<DimensionTemplateAuthoringExampleDescriptor> examples,
            IReadOnlyList<DimensionLayoutTemplatePresetDescriptor> layoutPresets)
        {
            Request = request ?? new DimensionTemplateCreationWizardRequest();
            Fields = fields ?? new List<DimensionTemplateCreationWizardField>();
            Examples = examples ?? new List<DimensionTemplateAuthoringExampleDescriptor>();
            LayoutPresets = layoutPresets ?? new List<DimensionLayoutTemplatePresetDescriptor>();
        }

        public DimensionTemplateCreationWizardRequest Request { get; private set; }

        public IReadOnlyList<DimensionTemplateCreationWizardField> Fields { get; private set; }

        public IReadOnlyList<DimensionTemplateAuthoringExampleDescriptor> Examples { get; private set; }

        public IReadOnlyList<DimensionLayoutTemplatePresetDescriptor> LayoutPresets { get; private set; }
    }

    public sealed class DimensionTemplateCreationWizardPreview
    {
        public DimensionTemplateCreationWizardPreview(
            bool canCreate,
            string code,
            string message,
            DimensionTemplateCreationWizardRequest request,
            DimensionTemplateAuthoringExampleDescriptor selectedExample,
            DimensionLayoutTemplatePresetDescriptor selectedLayoutPreset,
            DimensionTemplateStarterRequest starterRequest,
            DimensionTemplateStarterAssessment assessment,
            DimensionTemplateAssetSavePlan savePlan,
            IReadOnlyList<string> warnings)
        {
            CanCreate = canCreate;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Request = request ?? new DimensionTemplateCreationWizardRequest();
            SelectedExample = selectedExample;
            SelectedLayoutPreset = selectedLayoutPreset;
            StarterRequest = starterRequest;
            Assessment = assessment;
            SavePlan = savePlan;
            Warnings = warnings ?? new List<string>();
        }

        public bool CanCreate { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public DimensionTemplateCreationWizardRequest Request { get; private set; }

        public DimensionTemplateAuthoringExampleDescriptor SelectedExample { get; private set; }

        public DimensionLayoutTemplatePresetDescriptor SelectedLayoutPreset { get; private set; }

        public DimensionTemplateStarterRequest StarterRequest { get; private set; }

        public DimensionTemplateStarterAssessment Assessment { get; private set; }

        public DimensionTemplateAssetSavePlan SavePlan { get; private set; }

        public IReadOnlyList<string> Warnings { get; private set; }
    }

    public static class DimensionTemplateCreationWizardUtility
    {
        public const string ExampleFieldId = "exampleId";
        public const string ModIdPrefixFieldId = "modIdPrefix";
        public const string DimensionIdFieldId = "dimensionId";
        public const string DimensionDisplayNameFieldId = "dimensionDisplayName";
        public const string ContentPackIdFieldId = "contentPackId";
        public const string ContentPackDisplayNameFieldId = "contentPackDisplayName";
        public const string ContentPackAuthorFieldId = "contentPackAuthor";
        public const string BiomeIdFieldId = "biomeId";
        public const string BiomeDisplayNameFieldId = "biomeDisplayName";
        public const string ZoneIdFieldId = "zoneId";
        public const string AbsoluteOriginXFieldId = "absoluteOriginX";
        public const string AbsoluteOriginYFieldId = "absoluteOriginY";
        public const string HalfSizeTilesFieldId = "halfSizeTiles";
        public const string ReservedShellPaddingTilesFieldId = "reservedShellPaddingTiles";
        public const string FloorResourceKeyFieldId = "floorResourceKey";
        public const string WallResourceKeyFieldId = "wallResourceKey";
        public const string LiquidResourceKeyFieldId = "liquidResourceKey";
        public const string OreResourceKeyFieldId = "oreResourceKey";
        public const string ObjectResourceKeyFieldId = "objectResourceKey";
        public const string TerrainGenerationProviderIdFieldId = "terrainGenerationProviderId";
        public const string RootFolderFieldId = "suggestedRootFolder";

        public static DimensionTemplateCreationWizardModel BuildModel(
            DimensionTemplateCreationWizardRequest request)
        {
            DimensionTemplateCreationWizardRequest resolved = NormalizeRequest(request);
            return new DimensionTemplateCreationWizardModel(
                resolved,
                BuildFields(resolved),
                DimensionTemplateAuthoringExampleCatalog.GetExamples(),
                DimensionLayoutTemplatePresetCatalog.GetBuiltInPresets());
        }

        public static DimensionTemplateCreationWizardPreview Preview(
            DimensionTemplateCreationWizardRequest request)
        {
            DimensionTemplateCreationWizardRequest resolved = NormalizeRequest(request);
            List<string> warnings = new List<string>();

            DimensionTemplateAuthoringExampleDescriptor example;
            bool hasExample = DimensionTemplateAuthoringExampleCatalog.TryGetExample(
                resolved.ExampleId,
                out example);
            if (!hasExample)
            {
                DimensionTemplateAuthoringExampleCatalog.TryGetExample(
                    DimensionTemplateAuthoringExampleCatalog.MinimalRoomExampleId,
                    out example);
                warnings.Add("Unknown starter template selected. Starter Room will be used.");
            }

            DimensionLayoutTemplatePresetDescriptor layoutPreset;
            if (!DimensionLayoutTemplatePresetCatalog.TryGetBuiltInPreset(
                example.LayoutPresetId,
                out layoutPreset))
            {
                DimensionLayoutTemplatePresetCatalog.TryGetBuiltInPreset(
                    DimensionLayoutTemplatePresetCatalog.SingleBiomeSquarePresetId,
                    out layoutPreset);
                warnings.Add("The selected starter template points to an unknown layout preset.");
            }

            if (string.IsNullOrEmpty(resolved.DimensionDisplayName))
            {
                return BuildBlockedPreview(
                    resolved,
                    example,
                    layoutPreset,
                    warnings,
                    "dimension-name-missing",
                    "A dimension display name is required.");
            }

            DimensionTemplateStarterRequest starterRequest =
                CreateStarterRequest(example.ExampleId, resolved);
            AddStarterRequestWarnings(starterRequest, warnings);

            DimensionTemplateStarterGraph graph =
                DimensionTemplateAuthoringExampleCatalog.CreateStarterGraph(
                    example.ExampleId,
                    starterRequest);
            if (graph == null)
            {
                return BuildBlockedPreview(
                    resolved,
                    example,
                    layoutPreset,
                    warnings,
                    "starter-graph-missing",
                    "The starter graph could not be created.");
            }

            DimensionTemplateStarterAssessment assessment =
                DimensionTemplateStarterAssessmentUtility.Assess(graph);
            DimensionTemplateAssetSavePlan savePlan =
                DimensionTemplateAssetSavePlanUtility.CreateForStarterGraph(
                    graph,
                    resolved.SuggestedRootFolder);
            IReadOnlyList<string> presetValidationMessages =
                DimensionTemplateStarterPresetValidator.Validate(example, graph);
            bool starterPresetValid =
                presetValidationMessages == null ||
                presetValidationMessages.Count == 0;
            AddPresetValidationWarnings(presetValidationMessages, warnings);

            bool canCreate =
                assessment != null &&
                assessment.HasDimensionTemplate &&
                savePlan != null &&
                starterPresetValid;
            string code = canCreate
                ? "ready-to-create"
                : starterPresetValid ? "blocked" : "starter-preset-invalid";
            string message = canCreate
                ? "Ready to create the starter Dimension Asset. The deeper layout, biome, content, and export checks happen after this asset exists."
                : starterPresetValid
                    ? "Fill in the highlighted starter fields before creating the Dimension Asset."
                    : "The selected starter template failed framework shape validation. Fix the built-in preset before creating the Dimension Asset.";

            return new DimensionTemplateCreationWizardPreview(
                canCreate,
                code,
                message,
                resolved,
                example,
                layoutPreset,
                starterRequest,
                assessment,
                savePlan,
                warnings);
        }

        private static void AddPresetValidationWarnings(
            IReadOnlyList<string> presetValidationMessages,
            List<string> warnings)
        {
            if (presetValidationMessages == null || warnings == null)
            {
                return;
            }

            for (int i = 0; i < presetValidationMessages.Count; i++)
            {
                if (!string.IsNullOrEmpty(presetValidationMessages[i]))
                {
                    warnings.Add("Starter preset validation: " + presetValidationMessages[i]);
                }
            }
        }

        public static DimensionTemplateAuthoringWorkspace CreateWorkspace(
            DimensionTemplateCreationWizardRequest request)
        {
            DimensionTemplateCreationWizardPreview preview = Preview(request);
            if (preview == null || !preview.CanCreate)
            {
                return null;
            }

            DimensionTemplateStarterGraph graph =
                DimensionTemplateAuthoringExampleCatalog.CreateStarterGraph(
                    preview.SelectedExample.ExampleId,
                    preview.StarterRequest);
            return DimensionTemplateAuthoringWorkspaceUtility.BuildWorkspace(
                graph,
                preview.Request.SuggestedRootFolder);
        }

        private static DimensionTemplateStarterRequest CreateStarterRequest(
            string exampleId,
            DimensionTemplateCreationWizardRequest request)
        {
            DimensionTemplateCreationWizardRequest resolved =
                NormalizeRequest(request);
            DimensionTemplateStarterRequest starterRequest =
                DimensionTemplateAuthoringExampleCatalog.CreateStarterRequest(
                    exampleId,
                    resolved.DimensionId,
                    resolved.DimensionDisplayName);
            starterRequest.ContentPackId = string.IsNullOrEmpty(resolved.ContentPackId)
                ? starterRequest.ContentPackId
                : resolved.ContentPackId;
            starterRequest.ContentPackDisplayName = string.IsNullOrEmpty(resolved.ContentPackDisplayName)
                ? starterRequest.ContentPackDisplayName
                : resolved.ContentPackDisplayName;
            starterRequest.ContentPackAuthor = resolved.ContentPackAuthor;
            starterRequest.BiomeId = string.IsNullOrEmpty(resolved.BiomeId)
                ? starterRequest.BiomeId
                : resolved.BiomeId;
            starterRequest.BiomeDisplayName = string.IsNullOrEmpty(resolved.BiomeDisplayName)
                ? starterRequest.BiomeDisplayName
                : resolved.BiomeDisplayName;
            starterRequest.ZoneId = string.IsNullOrEmpty(resolved.ZoneId)
                ? starterRequest.ZoneId
                : resolved.ZoneId;
            starterRequest.AbsoluteOrigin = new Vector2Int(
                resolved.AbsoluteOriginX,
                resolved.AbsoluteOriginY);
            starterRequest.HalfSizeTiles = resolved.HalfSizeTiles;
            starterRequest.ReservedShellPaddingTiles = resolved.ReservedShellPaddingTiles;
            starterRequest.FloorResourceKey = string.IsNullOrEmpty(resolved.FloorResourceKey)
                ? starterRequest.FloorResourceKey
                : resolved.FloorResourceKey;
            starterRequest.WallResourceKey = string.IsNullOrEmpty(resolved.WallResourceKey)
                ? starterRequest.WallResourceKey
                : resolved.WallResourceKey;
            starterRequest.LiquidResourceKey = resolved.LiquidResourceKey ?? string.Empty;
            starterRequest.OreResourceKey = resolved.OreResourceKey ?? string.Empty;
            starterRequest.ObjectResourceKey = resolved.ObjectResourceKey ?? string.Empty;
            starterRequest.TerrainGenerationProviderId = resolved.TerrainGenerationProviderId ?? string.Empty;
            return starterRequest;
        }

        private static void AddStarterRequestWarnings(
            DimensionTemplateStarterRequest starterRequest,
            List<string> warnings)
        {
            if (starterRequest == null || warnings == null)
            {
                return;
            }

            if (starterRequest.HalfSizeTiles < 8)
            {
                warnings.Add("The playable half-size is very small. It is useful for a teleport test room, but not enough for biome progression.");
            }

            if (starterRequest.ReservedShellPaddingTiles >= DimensionTemplateStarterFactory.MaxReservedShellPaddingTiles)
            {
                warnings.Add("The reserved coordinate shell is at the framework maximum of 5000 tiles.");
            }
        }

        private static DimensionTemplateCreationWizardPreview BuildBlockedPreview(
            DimensionTemplateCreationWizardRequest request,
            DimensionTemplateAuthoringExampleDescriptor example,
            DimensionLayoutTemplatePresetDescriptor layoutPreset,
            IReadOnlyList<string> warnings,
            string code,
            string message)
        {
            return new DimensionTemplateCreationWizardPreview(
                false,
                code,
                message,
                request,
                example,
                layoutPreset,
                null,
                null,
                null,
                warnings);
        }

        private static DimensionTemplateCreationWizardRequest NormalizeRequest(
            DimensionTemplateCreationWizardRequest request)
        {
            DimensionTemplateCreationWizardRequest source =
                request ?? new DimensionTemplateCreationWizardRequest();
            DimensionTemplateCreationWizardRequest normalized =
                new DimensionTemplateCreationWizardRequest();
            normalized.ExampleId = string.IsNullOrEmpty(source.ExampleId)
                ? DimensionTemplateAuthoringExampleCatalog.MinimalRoomExampleId
                : source.ExampleId;
            DimensionTemplateAuthoringExampleDescriptor example;
            if (!DimensionTemplateAuthoringExampleCatalog.TryGetExample(
                normalized.ExampleId,
                out example))
            {
                DimensionTemplateAuthoringExampleCatalog.TryGetExample(
                    DimensionTemplateAuthoringExampleCatalog.MinimalRoomExampleId,
                    out example);
            }

            normalized.SuggestedRootFolder = source.SuggestedRootFolder ?? string.Empty;
            normalized.ModIdPrefix = ResolveModIdPrefix(source);
            normalized.DimensionDisplayName = string.IsNullOrEmpty(source.DimensionDisplayName)
                ? "Dimension"
                : source.DimensionDisplayName;
            string dimensionToken = NormalizeIdToken(
                normalized.DimensionDisplayName,
                "Dimension");
            normalized.DimensionId = normalized.ModIdPrefix + ":" + dimensionToken;
            normalized.ContentPackId = normalized.ModIdPrefix + ":" + dimensionToken + "Pack";
            normalized.ContentPackDisplayName = string.IsNullOrEmpty(source.ContentPackDisplayName)
                ? normalized.DimensionDisplayName + " Pack"
                : source.ContentPackDisplayName;
            normalized.ContentPackAuthor = source.ContentPackAuthor ?? string.Empty;
            string biomeName = string.IsNullOrEmpty(source.BiomeDisplayName)
                ? "Starter Biome"
                : source.BiomeDisplayName;
            string biomeToken = NormalizeIdToken(biomeName, "StarterBiome");
            normalized.BiomeId = normalized.ModIdPrefix + ":" + biomeToken;
            normalized.BiomeDisplayName = biomeName;
            normalized.ZoneId = normalized.BiomeId;
            normalized.AbsoluteOriginX = source.AbsoluteOriginX;
            normalized.AbsoluteOriginY = source.AbsoluteOriginY;
            normalized.HalfSizeTiles = source.HalfSizeTiles < 1
                ? example.RecommendedHalfSizeTiles
                : source.HalfSizeTiles;
            normalized.ReservedShellPaddingTiles = Clamp(
                source.ReservedShellPaddingTiles < 0
                    ? example.RecommendedShellPaddingTiles
                    : source.ReservedShellPaddingTiles,
                0,
                DimensionTemplateStarterFactory.MaxReservedShellPaddingTiles);
            normalized.FloorResourceKey = normalized.ModIdPrefix + ":Ground" + biomeToken + "Block";
            normalized.WallResourceKey = normalized.ModIdPrefix + ":Wall" + biomeToken + "Block";
            normalized.LiquidResourceKey = source.LiquidResourceKey ?? string.Empty;
            normalized.OreResourceKey = source.OreResourceKey ?? string.Empty;
            normalized.ObjectResourceKey = source.ObjectResourceKey ?? string.Empty;
            normalized.TerrainGenerationProviderId = string.IsNullOrEmpty(source.TerrainGenerationProviderId)
                ? DimensionGenerationProviderIds.SafePlatform
                : source.TerrainGenerationProviderId;
            return normalized;
        }

        private static string ResolveModIdPrefix(
            DimensionTemplateCreationWizardRequest source)
        {
            string folderPrefix = ResolveModFolderNameFromAssetFolder(
                source == null ? string.Empty : source.SuggestedRootFolder);
            if (!string.IsNullOrEmpty(folderPrefix))
            {
                return NormalizeIdToken(folderPrefix, "ModName");
            }

            string explicitPrefix = source == null ? string.Empty : source.ModIdPrefix;
            return string.IsNullOrEmpty(explicitPrefix)
                ? "ModName"
                : NormalizeIdToken(explicitPrefix, "ModName");
        }

        private static string ResolveModFolderNameFromAssetFolder(string assetFolder)
        {
            string normalized = NormalizeFolder(assetFolder);
            const string assetsPrefix = "Assets/";
            if (!normalized.StartsWith(assetsPrefix))
            {
                return string.Empty;
            }

            string relative = normalized.Substring(assetsPrefix.Length);
            int slash = relative.IndexOf('/');
            string rootFolder = slash < 0 ? relative : relative.Substring(0, slash);
            if (string.IsNullOrEmpty(rootFolder) ||
                rootFolder == "DimensionAssets")
            {
                return string.Empty;
            }

            return rootFolder;
        }

        private static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }

            string normalized = folder.Replace('\\', '/').Trim();
            while (normalized.EndsWith("/"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private static IReadOnlyList<DimensionTemplateCreationWizardField> BuildFields(
            DimensionTemplateCreationWizardRequest request)
        {
            List<DimensionTemplateCreationWizardField> fields =
                new List<DimensionTemplateCreationWizardField>();

            fields.Add(new DimensionTemplateCreationWizardField(
                ExampleFieldId,
                "Starter Template",
                request.ExampleId,
                DimensionTemplateAuthoringExampleCatalog.MinimalRoomExampleId,
                string.Empty,
                DimensionTemplateCreationWizardFieldKind.Preset,
                true,
                true,
                BuildExampleOptions(request.ExampleId)));
            fields.Add(CreateTextField(
                DimensionDisplayNameFieldId,
                "Dimension Name",
                request.DimensionDisplayName,
                "Dimension",
                string.Empty,
                true));
            fields.Add(CreateTextField(
                DimensionIdFieldId,
                "Dimension ID",
                request.DimensionId,
                "ModName:DimensionName",
                "Auto-generated from the active mod and Dimension Name.",
                true,
                false));
            fields.Add(CreateTextField(
                AbsoluteOriginXFieldId,
                "Absolute Origin X",
                request.AbsoluteOriginX.ToString(),
                "5000",
                string.Empty,
                true));
            fields.Add(CreateTextField(
                AbsoluteOriginYFieldId,
                "Absolute Origin Y",
                request.AbsoluteOriginY.ToString(),
                "5000",
                string.Empty,
                true));
            fields.Add(CreateTextField(
                HalfSizeTilesFieldId,
                "Starter Area Size",
                (request.HalfSizeTiles * 2).ToString() + " x " + (request.HalfSizeTiles * 2).ToString() + " tiles",
                "16 x 16 tiles",
                "Chosen by the selected starter template. Later, this is calculated from the actual biome/layout content.",
                true,
                false));
            fields.Add(CreateTextField(
                ReservedShellPaddingTilesFieldId,
                "Coordinate Shell",
                request.ReservedShellPaddingTiles.ToString() + " tiles",
                "Auto",
                "Auto-calculated from playable bounds, with a hard maximum of 5000 tiles.",
                true,
                false));
            fields.Add(CreateTextField(
                BiomeDisplayNameFieldId,
                "Starter Biome Name",
                request.BiomeDisplayName,
                "Starter Biome",
                string.Empty,
                true));
            fields.Add(CreateTextField(
                BiomeIdFieldId,
                "Biome ID",
                request.BiomeId,
                request.ModIdPrefix + ":StarterBiome",
                "Auto-generated from Starter Biome Name.",
                true,
                false));
            fields.Add(CreateTextField(
                ZoneIdFieldId,
                "Zone ID",
                request.ZoneId,
                request.BiomeId,
                "Defaults to the Biome ID so spawn/map/generation systems share one starter zone.",
                true,
                false));
            fields.Add(new DimensionTemplateCreationWizardField(
                RootFolderFieldId,
                "Target Folder",
                request.SuggestedRootFolder,
                "Assets/YourMod/DimensionAssets",
                "Folder where the Dimension Asset and its starter authoring assets will be saved. The editor defaults this to the active PugMod folder.",
                DimensionTemplateCreationWizardFieldKind.Folder,
                false,
                true,
                null));

            return fields;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static DimensionTemplateCreationWizardField CreateTextField(
            string fieldId,
            string label,
            string value,
            string placeholder,
            string helpText,
            bool required)
        {
            return CreateTextField(
                fieldId,
                label,
                value,
                placeholder,
                helpText,
                required,
                true);
        }

        private static DimensionTemplateCreationWizardField CreateTextField(
            string fieldId,
            string label,
            string value,
            string placeholder,
            string helpText,
            bool required,
            bool editable)
        {
            return new DimensionTemplateCreationWizardField(
                fieldId,
                label,
                value,
                placeholder,
                helpText,
                DimensionTemplateCreationWizardFieldKind.Text,
                required,
                editable,
                null);
        }

        private static string NormalizeIdToken(string value, string fallback)
        {
            string source = string.IsNullOrEmpty(value) ? fallback : value;
            string result = string.Empty;
            bool makeUpper = true;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if ((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9'))
                {
                    if (makeUpper && character >= 'a' && character <= 'z')
                    {
                        character = (char)(character - 32);
                    }

                    result += character;
                    makeUpper = false;
                }
                else
                {
                    makeUpper = true;
                }
            }

            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        private static IReadOnlyList<DimensionTemplateCreationWizardOption> BuildExampleOptions(
            string selectedExampleId)
        {
            List<DimensionTemplateCreationWizardOption> options =
                new List<DimensionTemplateCreationWizardOption>();
            IReadOnlyList<DimensionTemplateAuthoringExampleDescriptor> examples =
                DimensionTemplateAuthoringExampleCatalog.GetExamples();
            for (int i = 0; i < examples.Count; i++)
            {
                DimensionTemplateAuthoringExampleDescriptor example = examples[i];
                options.Add(new DimensionTemplateCreationWizardOption(
                    example.ExampleId,
                    example.DisplayName,
                    example.Description,
                    example.ExampleId == selectedExampleId,
                    true));
            }

            return options;
        }
    }
}
