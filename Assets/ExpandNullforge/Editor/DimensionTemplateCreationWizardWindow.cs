using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    public sealed class DimensionTemplateCreationWizardWindow : EditorWindow
    {
        private const string WindowTitle = "Create your Dimension";
        private const float SetupColumnWidth = 430f;
        private const float MinPreviewColumnWidth = 430f;
        private const float PreviewWidth = 420f;

        private DimensionTemplateCreationWizardSessionController controller;
        private Vector2 scroll;
        private Vector2 previewScroll;
        private string lastResultMessage = string.Empty;
        private MessageType lastResultType = MessageType.Info;

        [MenuItem("Dimensions API/Create your Dimension")]
        public static void Open()
        {
            DimensionTemplateCreationWizardWindow window =
                GetWindow<DimensionTemplateCreationWizardWindow>(WindowTitle);
            window.minSize = new Vector2(1040f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            EnsureController();
        }

        private void OnGUI()
        {
            EnsureController();

            DimensionTemplateCreationWizardSessionSnapshot snapshot =
                controller.GetSnapshot();

            DrawHeader();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginHorizontal();
            DrawRequestPanel(snapshot);
            GUILayout.Space(8f);
            DrawPreviewPanel(snapshot);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8f);
            DrawActionPanel(snapshot);
            EditorGUILayout.EndScrollView();
        }

        private void EnsureController()
        {
            if (controller != null)
            {
                return;
            }

            DimensionTemplateCreationWizardRequest request =
                new DimensionTemplateCreationWizardRequest();
            request.ModIdPrefix =
                DimensionApiModFolderUtility.ResolveActiveModDisplayName();
            request.SuggestedRootFolder =
                DimensionApiModFolderUtility.ResolvePreferredDimensionAssetFolder();
            controller = new DimensionTemplateCreationWizardSessionController(request);
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Create the first Dimension Asset for your mod. Choose a starter template, name the dimension, choose where it lives in world space, and the framework will derive the IDs and starter assets for you.",
                EditorStyles.wordWrappedLabel);

            if (!string.IsNullOrEmpty(lastResultMessage))
            {
                EditorGUILayout.HelpBox(lastResultMessage, lastResultType);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRequestPanel(
            DimensionTemplateCreationWizardSessionSnapshot snapshot)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox,
                GUILayout.Width(SetupColumnWidth),
                GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Dimension setup", EditorStyles.boldLabel);

            DimensionTemplateCreationWizardModel model = snapshot == null ? null : snapshot.Model;
            IReadOnlyList<DimensionTemplateCreationWizardField> fields =
                model == null ? null : model.Fields;
            if (fields == null || fields.Count == 0)
            {
                EditorGUILayout.HelpBox("No wizard fields are available.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < fields.Count; i++)
            {
                if (!ShouldShowInSetupPanel(fields[i]))
                {
                    continue;
                }

                DrawWizardField(fields[i]);
                GUILayout.Space(6f);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private static bool ShouldShowInSetupPanel(
            DimensionTemplateCreationWizardField field)
        {
            if (field.FieldId == DimensionTemplateCreationWizardUtility.DimensionIdFieldId ||
                field.FieldId == DimensionTemplateCreationWizardUtility.HalfSizeTilesFieldId ||
                field.FieldId == DimensionTemplateCreationWizardUtility.ReservedShellPaddingTilesFieldId ||
                field.FieldId == DimensionTemplateCreationWizardUtility.BiomeIdFieldId ||
                field.FieldId == DimensionTemplateCreationWizardUtility.ZoneIdFieldId)
            {
                return false;
            }

            return true;
        }

        private void DrawWizardField(DimensionTemplateCreationWizardField field)
        {
            EditorGUILayout.LabelField(
                field.Required ? field.Label + " *" : field.Label,
                EditorStyles.miniBoldLabel);

            if (field.Kind == DimensionTemplateCreationWizardFieldKind.Preset)
            {
                DrawPresetField(field);
            }
            else if (field.Kind == DimensionTemplateCreationWizardFieldKind.Folder)
            {
                DrawFolderField(field);
            }
            else
            {
                using (new EditorGUI.DisabledScope(!field.Editable))
                {
                    EditorGUI.BeginChangeCheck();
                    string nextValue = EditorGUILayout.TextField(field.Value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        controller.UpdateField(field.FieldId, nextValue);
                    }
                }
            }
        }

        private void DrawFolderField(DimensionTemplateCreationWizardField field)
        {
            using (new EditorGUI.DisabledScope(!field.Editable))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string nextValue = EditorGUILayout.TextField(field.Value);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateTargetFolder(field.FieldId, nextValue, false);
                }

                if (GUILayout.Button("Use Active Mod", GUILayout.Width(110f)))
                {
                    UseActiveModFolder(field);
                }

                if (GUILayout.Button("Pick", GUILayout.Width(52f)))
                {
                    PickDimensionAssetFolder(field);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void UseActiveModFolder(DimensionTemplateCreationWizardField field)
        {
            UpdateTargetFolder(
                field.FieldId,
                DimensionApiModFolderUtility.ResolvePreferredDimensionAssetFolder(),
                true);
        }

        private void PickDimensionAssetFolder(DimensionTemplateCreationWizardField field)
        {
            string selected = EditorUtility.OpenFolderPanel(
                "Choose Dimension Asset Folder",
                Application.dataPath,
                string.Empty);
            string assetFolder =
                DimensionApiModFolderUtility.ConvertAbsoluteFolderToAssetFolder(selected);
            if (string.IsNullOrEmpty(assetFolder))
            {
                lastResultMessage = "Choose a folder inside this Unity project's Assets folder.";
                lastResultType = MessageType.Warning;
            }
            else
            {
                UpdateTargetFolder(field.FieldId, assetFolder, true);
            }
        }

        private void UpdateTargetFolder(
            string fieldId,
            string assetFolder,
            bool normalizeAsDimensionAssetFolder)
        {
            string normalizedFolder = normalizeAsDimensionAssetFolder
                ? DimensionApiModFolderUtility.NormalizeDimensionAssetFolder(assetFolder)
                : DimensionApiModFolderUtility.NormalizeFolder(assetFolder);
            string modPrefix =
                DimensionApiModFolderUtility.ResolveModDisplayNameForAssetFolder(normalizedFolder);
            if (string.IsNullOrEmpty(modPrefix))
            {
                modPrefix = DimensionApiModFolderUtility.ResolveActiveModDisplayName();
            }

            controller.UpdateField(fieldId, normalizedFolder);
            if (!string.IsNullOrEmpty(modPrefix))
            {
                controller.UpdateField(
                    DimensionTemplateCreationWizardUtility.ModIdPrefixFieldId,
                    modPrefix);
            }
        }

        private void DrawPresetField(DimensionTemplateCreationWizardField field)
        {
            IReadOnlyList<DimensionTemplateCreationWizardOption> options = field.Options;
            if (options == null || options.Count == 0)
            {
                EditorGUILayout.HelpBox("No starter templates are registered.", MessageType.Warning);
                return;
            }

            int selectedIndex = 0;
            string[] labels = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                DimensionTemplateCreationWizardOption option = options[i];
                labels[i] = string.IsNullOrEmpty(option.DisplayName)
                    ? option.OptionId
                    : option.DisplayName;
                if (option.Selected)
                {
                    selectedIndex = i;
                }
            }

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(selectedIndex, labels);
            if (EditorGUI.EndChangeCheck() &&
                nextIndex >= 0 &&
                nextIndex < options.Count)
            {
                controller.UpdateField(field.FieldId, options[nextIndex].OptionId);
            }
        }

        private void DrawPreviewPanel(
            DimensionTemplateCreationWizardSessionSnapshot snapshot)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox,
                GUILayout.MinWidth(MinPreviewColumnWidth),
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Starter preview", EditorStyles.boldLabel);

            DimensionTemplateCreationWizardPreview preview =
                snapshot == null ? null : snapshot.Preview;
            if (preview == null)
            {
                EditorGUILayout.HelpBox("No preview exists yet.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawPresetPreview(preview);
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "This is only the starter bootstrap. It creates the root Dimension Asset and first biome so the project has something valid to open. The real world shape belongs in the Dimension Layout step, where the framework can use radial bands, angular sectors, manual regions, painted masks, or hybrid layouts.",
                MessageType.Info);
            GUILayout.Space(4f);

            MessageType previewType = preview.CanCreate
                ? MessageType.Info
                : MessageType.Warning;
            EditorGUILayout.HelpBox(preview.Message, previewType);

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.ExpandHeight(true));
            DrawPreviewLine("Starter template", ResolveExampleLabel(preview));
            DrawPreviewLine("Starter layout", ResolveLayoutLabel(preview));

            if (preview.StarterRequest != null)
            {
                DrawPreviewLine("Origin", preview.StarterRequest.AbsoluteOrigin.x + ", " + preview.StarterRequest.AbsoluteOrigin.y);
                DrawPreviewLine("Starter shape", ResolveStarterShapeLabel(preview));
                DrawPreviewLine("Bounds envelope", ResolveGenerationBoundsLabel(preview));
                DrawPreviewLine("Compiled regions", ResolveCompiledRegionLabel(preview));
                DrawPreviewLine("Terrain passes", ResolveGenerationPassLabel(preview));
                DrawPreviewLine("Shell", preview.StarterRequest.ReservedShellPaddingTiles + " tiles");
                DrawPreviewLine("Dimension ID", preview.StarterRequest.DimensionId);
                DrawPreviewLine("Biome", preview.StarterRequest.BiomeDisplayName);
                DrawPreviewLine("Biome ID", preview.StarterRequest.BiomeId);
            }

            if (preview.SavePlan != null)
            {
                GUILayout.Space(6f);
                EditorGUILayout.LabelField("Save target", EditorStyles.miniBoldLabel);
                DrawPreviewLine("Folder", preview.SavePlan.RootFolder);
                DrawPreviewLine("Assets", Count(preview.SavePlan.Entries).ToString());
            }

            if (preview.Warnings != null && preview.Warnings.Count > 0)
            {
                GUILayout.Space(6f);
                EditorGUILayout.LabelField("Warnings", EditorStyles.miniBoldLabel);
                for (int i = 0; i < preview.Warnings.Count; i++)
                {
                    EditorGUILayout.LabelField("- " + preview.Warnings[i], EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static string ResolveExampleLabel(
            DimensionTemplateCreationWizardPreview preview)
        {
            if (preview == null || string.IsNullOrEmpty(preview.SelectedExample.ExampleId))
            {
                return "-";
            }

            return preview.SelectedExample.DisplayName;
        }

        private static string ResolveLayoutLabel(
            DimensionTemplateCreationWizardPreview preview)
        {
            if (preview == null || string.IsNullOrEmpty(preview.SelectedLayoutPreset.PresetId))
            {
                return "-";
            }

            return preview.SelectedLayoutPreset.DisplayName;
        }

        private static string ResolveStarterShapeLabel(
            DimensionTemplateCreationWizardPreview preview)
        {
            if (preview == null)
            {
                return "-";
            }

            string presetId = preview.SelectedLayoutPreset.PresetId;
            if (presetId == DimensionLayoutTemplatePresetCatalog.CenteredGridPresetId)
            {
                return "3 x 3 grid cells";
            }

            if (presetId == DimensionLayoutTemplatePresetCatalog.RadialRingsPresetId)
            {
                return "3 radial rings";
            }

            return "Single square room";
        }

        private static string ResolveGenerationBoundsLabel(
            DimensionTemplateCreationWizardPreview preview)
        {
            DimensionBounds bounds;
            if (TryGetCompiledPlayableBounds(preview, out bounds))
            {
                return FormatTileSize(bounds.Size);
            }

            int diameter = ResolvePlayableDiameter(preview);
            return diameter + " x " + diameter + " tiles";
        }

        private static string ResolveCompiledRegionLabel(
            DimensionTemplateCreationWizardPreview preview)
        {
            int count = CountPreviewEntries(preview, DimensionAuthoringPreviewLayerKind.BiomeRegion);
            return count <= 0 ? "-" : count.ToString();
        }

        private static string ResolveGenerationPassLabel(
            DimensionTemplateCreationWizardPreview preview)
        {
            int count = CountPreviewEntries(preview, DimensionAuthoringPreviewLayerKind.GenerationPassBounds);
            return count <= 0 ? "-" : count.ToString();
        }

        private static bool TryGetCompiledPlayableBounds(
            DimensionTemplateCreationWizardPreview preview,
            out DimensionBounds bounds)
        {
            bounds = default(DimensionBounds);
            DimensionAuthoringPreviewSummary summary =
                preview == null || preview.Assessment == null
                    ? default(DimensionAuthoringPreviewSummary)
                    : preview.Assessment.Preview;
            if (!summary.Success)
            {
                return false;
            }

            bounds = summary.PlayableLocalBounds;
            return bounds.MaxExclusive.x > bounds.Min.x &&
                   bounds.MaxExclusive.y > bounds.Min.y;
        }

        private static int CountPreviewEntries(
            DimensionTemplateCreationWizardPreview preview,
            DimensionAuthoringPreviewLayerKind layerKind)
        {
            DimensionAuthoringPreviewSummary summary =
                preview == null || preview.Assessment == null
                    ? default(DimensionAuthoringPreviewSummary)
                    : preview.Assessment.Preview;
            if (summary.Entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < summary.Entries.Count; i++)
            {
                if (summary.Entries[i].LayerKind == layerKind)
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatTileSize(int2 size)
        {
            return size.x + " x " + size.y + " tiles";
        }

        private void DrawPresetPreview(
            DimensionTemplateCreationWizardPreview preview)
        {
            Rect rect = GUILayoutUtility.GetRect(
                PreviewWidth - 24f,
                170f,
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.12f, 0.13f, 1f));
            DrawRectOutline(rect, new Color(0.35f, 0.55f, 0.72f, 1f), 1f);

            Rect mapRect = new Rect(
                rect.x + 14f,
                rect.y + 12f,
                rect.width - 28f,
                rect.height - 42f);
            EditorGUI.DrawRect(mapRect, new Color(0.04f, 0.05f, 0.055f, 1f));

            DimensionLayoutKind kind = preview == null
                ? DimensionLayoutKind.ManualRegions
                : preview.SelectedLayoutPreset.LayoutKind;
            if (kind == DimensionLayoutKind.GridRegions)
            {
                DrawGridPreset(mapRect, preview);
            }
            else if (kind == DimensionLayoutKind.RadialRings)
            {
                DrawRadialPreset(mapRect, preview);
            }
            else
            {
                int areaSize = ResolvePlayableDiameter(preview);
                Rect biome = new Rect(
                    mapRect.center.x - 46f,
                    mapRect.center.y - 34f,
                    92f,
                    68f);
                EditorGUI.DrawRect(biome, new Color(0.25f, 0.58f, 0.60f, 0.88f));
                DrawRectOutline(biome, new Color(0.75f, 0.90f, 0.93f, 1f), 2f);
                RegisterPreviewTooltip(
                    biome,
                    BuildBiomeTooltip(
                        preview,
                        "Starter biome",
                        areaSize,
                        areaSize));
            }

            Rect origin = new Rect(mapRect.center.x - 3f, mapRect.center.y - 3f, 6f, 6f);
            EditorGUI.DrawRect(origin, new Color(0.55f, 1f, 0.65f, 1f));
            GUI.Label(
                new Rect(rect.x + 8f, rect.yMax - 25f, rect.width - 16f, 20f),
                "Starter-only preview - hover a region for biome and tile size",
                EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawGridPreset(
            Rect mapRect,
            DimensionTemplateCreationWizardPreview preview)
        {
            float cellWidth = mapRect.width / 3f;
            float cellHeight = mapRect.height / 3f;
            int playableSize = ResolvePlayableDiameter(preview);
            int cellTileWidth = Mathf.Max(1, Mathf.RoundToInt(playableSize / 3f));
            int cellTileHeight = Mathf.Max(1, Mathf.RoundToInt(playableSize / 3f));
            Color[] colors =
            {
                new Color(0.23f, 0.52f, 0.62f, 0.88f),
                new Color(0.30f, 0.45f, 0.57f, 0.88f),
                new Color(0.18f, 0.39f, 0.50f, 0.88f)
            };

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Rect cell = new Rect(
                        mapRect.x + x * cellWidth + 2f,
                        mapRect.y + y * cellHeight + 2f,
                        cellWidth - 4f,
                        cellHeight - 4f);
                    EditorGUI.DrawRect(cell, colors[(x + y) % colors.Length]);
                    RegisterPreviewTooltip(
                        cell,
                        BuildBiomeTooltip(
                            preview,
                            "Grid cell " + (x + 1) + "," + (y + 1),
                            cellTileWidth,
                            cellTileHeight));
                }
            }

            DrawRectOutline(mapRect, new Color(0.75f, 0.90f, 0.93f, 1f), 2f);
        }

        private static void DrawRadialPreset(
            Rect mapRect,
            DimensionTemplateCreationWizardPreview preview)
        {
            Vector2 center = mapRect.center;
            float maxRadius = Mathf.Min(mapRect.width, mapRect.height) * 0.42f;
            int playableSize = ResolvePlayableDiameter(preview);
            Rect outer = DrawCircle(center, maxRadius, new Color(0.16f, 0.35f, 0.46f, 0.88f));
            RegisterPreviewTooltip(
                outer,
                BuildBiomeTooltip(preview, "Outer biome band", playableSize, playableSize));
            Rect middle = DrawCircle(center, maxRadius * 0.64f, new Color(0.23f, 0.52f, 0.62f, 0.90f));
            RegisterPreviewTooltip(
                middle,
                BuildBiomeTooltip(
                    preview,
                    "Middle biome band",
                    Mathf.Max(1, Mathf.RoundToInt(playableSize * 0.64f)),
                    Mathf.Max(1, Mathf.RoundToInt(playableSize * 0.64f))));
            Rect inner = DrawCircle(center, maxRadius * 0.30f, new Color(0.40f, 0.72f, 0.70f, 0.95f));
            RegisterPreviewTooltip(
                inner,
                BuildBiomeTooltip(
                    preview,
                    "Inner biome band",
                    Mathf.Max(1, Mathf.RoundToInt(playableSize * 0.30f)),
                    Mathf.Max(1, Mathf.RoundToInt(playableSize * 0.30f))));
            DrawCircleOutline(center, maxRadius, new Color(0.75f, 0.90f, 0.93f, 1f));
        }

        private static Rect DrawCircle(Vector2 center, float radius, Color color)
        {
            Rect rect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = color;
            Handles.DrawSolidDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, radius);
            Handles.color = oldColor;
            Handles.EndGUI();
            return rect;
        }

        private static void DrawCircleOutline(Vector2 center, float radius, Color color)
        {
            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = color;
            Handles.DrawWireDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, radius);
            Handles.color = oldColor;
            Handles.EndGUI();
        }

        private static int ResolvePlayableDiameter(
            DimensionTemplateCreationWizardPreview preview)
        {
            if (preview == null || preview.StarterRequest == null)
            {
                return 0;
            }

            return Mathf.Max(1, preview.StarterRequest.HalfSizeTiles * 2);
        }

        private static string BuildBiomeTooltip(
            DimensionTemplateCreationWizardPreview preview,
            string regionName,
            int widthTiles,
            int heightTiles)
        {
            string biomeName = preview != null &&
                preview.StarterRequest != null &&
                !string.IsNullOrEmpty(preview.StarterRequest.BiomeDisplayName)
                    ? preview.StarterRequest.BiomeDisplayName
                    : "Starter biome";
            string presetName = preview != null &&
                !string.IsNullOrEmpty(preview.SelectedLayoutPreset.DisplayName)
                    ? preview.SelectedLayoutPreset.DisplayName
                    : "Starter template";
            return regionName + "\n" +
                "Biome: " + biomeName + "\n" +
                "Starter template: " + presetName + "\n" +
                "Approx size: " + widthTiles + " x " + heightTiles + " tiles";
        }

        private static void RegisterPreviewTooltip(Rect rect, string tooltip)
        {
            GUI.Label(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        private static void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawPreviewLine(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(115f));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionPanel(
            DimensionTemplateCreationWizardSessionSnapshot snapshot)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset", GUILayout.Width(110f), GUILayout.Height(30f)))
            {
                RunAction(DimensionTemplateCreationWizardActionPlanUtility.ResetToMinimalActionId);
            }

            bool canCreate = snapshot != null &&
                snapshot.Preview != null &&
                snapshot.Preview.CanCreate;
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Create Dimension Asset", GUILayout.Width(210f), GUILayout.Height(30f)))
                {
                    RunSaveAssetsAction();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!canCreate)
            {
                string message = snapshot == null || snapshot.Preview == null
                    ? "Fill the required fields to preview the Dimension Asset."
                    : snapshot.Preview.Message;
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void RunAction(string actionId)
        {
            if (actionId == DimensionTemplateCreationWizardActionPlanUtility.SaveAssetsActionId)
            {
                RunSaveAssetsAction();
                return;
            }

            DimensionTemplateCreationWizardActionResult result =
                controller.RunSourceAction(actionId);
            lastResultMessage = result == null
                ? "The wizard action returned no result."
                : result.Message;
            lastResultType = result != null && result.Executed
                ? MessageType.Info
                : MessageType.Warning;

            Repaint();
        }

        private void RunSaveAssetsAction()
        {
            DimensionTemplateCreationWizardSessionSnapshot snapshot =
                controller.GetSnapshot();
            DimensionTemplateAuthoringWorkspace workspace =
                snapshot == null ? null : snapshot.LastCreatedWorkspace;
            if (workspace == null)
            {
                DimensionTemplateCreationWizardActionResult createResult =
                    controller.RunSourceAction(
                        DimensionTemplateCreationWizardActionPlanUtility.CreateWorkspaceActionId);
                workspace = createResult == null
                    ? null
                    : createResult.Workspace;
            }

            DimensionTemplateAssetEditorSaveResult saveResult =
                DimensionTemplateAssetEditorSaveUtility.SaveWorkspace(workspace);
            lastResultMessage = saveResult.Message;
            lastResultType = saveResult.Executed
                ? MessageType.Info
                : MessageType.Warning;
            if (saveResult.Executed)
            {
                controller.MarkClean();
            }

            Repaint();
        }

        private static int Count<T>(IReadOnlyList<T> list)
        {
            return list == null ? 0 : list.Count;
        }
    }
}
