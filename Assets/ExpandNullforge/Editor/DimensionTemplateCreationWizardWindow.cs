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
        private const float SetupColumnWidth = 380f;
        private const float MinPreviewColumnWidth = 420f;
        private const float PreviewWidth = 420f;
        private const float FieldWidth = 264f;

        private static readonly Color WizBlue = new Color(0.30f, 0.62f, 0.92f);

        private DimensionTemplateCreationWizardSessionController controller;
        private Vector2 scroll;
        private Vector2 previewScroll;
        private string lastResultMessage = string.Empty;
        private MessageType lastResultType = MessageType.Info;
        private bool showAdvanced;
        private GUIStyle wizHeaderStyle;
        private GUIStyle wizSubtitleStyle;
        private GUIStyle wizSectionStyle;
        private GUIStyle wizFieldLabelStyle;

        // No menu item. Creating a dimension is the Home screen's own front door, so the
        // dashboard is the single way in rather than two doors that do the same thing.
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

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal();
            DrawRequestPanel(snapshot);
            GUILayout.Space(10f);
            DrawPreviewPanel(snapshot);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            // Action bar pinned at the bottom, below the scroll area.
            DrawActionPanel(snapshot);
        }

        private void EnsureWizStyles()
        {
            if (wizHeaderStyle != null)
            {
                return;
            }

            wizHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            wizSubtitleStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            wizSubtitleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            wizSectionStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            wizSectionStyle.normal.textColor = new Color(WizBlue.r, WizBlue.g, WizBlue.b, 0.9f);
            wizFieldLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
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
            EnsureWizStyles();
            Rect bar = EditorGUILayout.GetControlRect(false, 42f);
            EditorGUI.DrawRect(bar, new Color(WizBlue.r, WizBlue.g, WizBlue.b, 0.15f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), WizBlue);
            GUI.Label(new Rect(bar.x + 14f, bar.y, bar.width - 24f, bar.height), "Create your Dimension", wizHeaderStyle);

            GUILayout.Space(4f);
            GUILayout.Label("Pick a starter, name it, choose a folder — the framework derives the IDs and starter assets.", wizSubtitleStyle);

            if (!string.IsNullOrEmpty(lastResultMessage))
            {
                GUILayout.Space(2f);
                EditorGUILayout.HelpBox(lastResultMessage, lastResultType);
            }

            GUILayout.Space(6f);
        }

        private void DrawRequestPanel(
            DimensionTemplateCreationWizardSessionSnapshot snapshot)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox,
                GUILayout.Width(SetupColumnWidth),
                GUILayout.ExpandHeight(true));
            GUILayout.Label("SETUP", wizSectionStyle);
            GUILayout.Space(6f);

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
                if (!ShouldShowInSetupPanel(fields[i]) || IsAdvancedField(fields[i]))
                {
                    continue;
                }

                DrawWizardField(fields[i]);
                GUILayout.Space(10f);
            }

            GUILayout.Space(2f);
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced — world position & biome name", true);
            if (showAdvanced)
            {
                GUILayout.Space(6f);
                for (int i = 0; i < fields.Count; i++)
                {
                    if (!ShouldShowInSetupPanel(fields[i]) || !IsAdvancedField(fields[i]))
                    {
                        continue;
                    }

                    DrawWizardField(fields[i]);
                    GUILayout.Space(8f);
                }
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

        // Position (auto-allocated by the framework) and the biome name (auto-derived) are tucked
        // into the Advanced foldout so the default form is just template + name + folder.
        private static bool IsAdvancedField(DimensionTemplateCreationWizardField field)
        {
            return field.FieldId == DimensionTemplateCreationWizardUtility.AbsoluteOriginXFieldId ||
                   field.FieldId == DimensionTemplateCreationWizardUtility.AbsoluteOriginYFieldId ||
                   field.FieldId == DimensionTemplateCreationWizardUtility.BiomeDisplayNameFieldId;
        }

        private void DrawWizardField(DimensionTemplateCreationWizardField field)
        {
            GUILayout.Label(
                field.Required ? field.Label + " *" : field.Label,
                wizFieldLabelStyle);

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
                    string nextValue = EditorGUILayout.TextField(field.Value, GUILayout.Width(FieldWidth));
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
                EditorGUI.BeginChangeCheck();
                string nextValue = EditorGUILayout.TextField(field.Value, GUILayout.Width(FieldWidth));
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateTargetFolder(field.FieldId, nextValue, false);
                }

                GUILayout.Space(3f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Use Active Mod", GUILayout.Width(110f)))
                {
                    UseActiveModFolder(field);
                }

                if (GUILayout.Button("Pick", GUILayout.Width(52f)))
                {
                    PickDimensionAssetFolder(field);
                }

                GUILayout.FlexibleSpace();
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
            int nextIndex = EditorGUILayout.Popup(selectedIndex, labels, GUILayout.Width(FieldWidth));
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
            GUILayout.Label("PREVIEW", wizSectionStyle);
            GUILayout.Space(4f);

            DimensionTemplateCreationWizardPreview preview =
                snapshot == null ? null : snapshot.Preview;
            if (preview == null)
            {
                EditorGUILayout.HelpBox("No preview exists yet.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawPresetPreview(preview);
            GUILayout.Space(10f);

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.ExpandHeight(true));
            DrawPreviewLine("Template", ResolveExampleLabel(preview));
            if (preview.StarterRequest != null)
            {
                DrawPreviewLine("World position", preview.StarterRequest.AbsoluteOrigin.x + ", " + preview.StarterRequest.AbsoluteOrigin.y);
                DrawPreviewLine("Size", ResolveGenerationBoundsLabel(preview));
                DrawPreviewLine("Dimension ID", preview.StarterRequest.DimensionId);
            }

            if (preview.SavePlan != null)
            {
                DrawPreviewLine("Folder", preview.SavePlan.RootFolder);
                DrawPreviewLine("Assets", Count(preview.SavePlan.Entries).ToString());
            }

            if (preview.Warnings != null && preview.Warnings.Count > 0)
            {
                GUILayout.Space(8f);
                for (int i = 0; i < preview.Warnings.Count; i++)
                {
                    EditorGUILayout.HelpBox(preview.Warnings[i], MessageType.Warning);
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
            Rect inner = DrawCircle(center, maxRadius * 0.30f, new Color(0.36f, 0.66f, 0.90f, 0.95f));
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
            bool canCreate = snapshot != null &&
                snapshot.Preview != null &&
                snapshot.Preview.CanCreate;

            Rect divider = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(divider, new Color(1f, 1f, 1f, 0.08f));
            GUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();
            if (!canCreate)
            {
                string message = snapshot == null || snapshot.Preview == null
                    ? "Fill in the required fields to continue."
                    : snapshot.Preview.Message;
                GUILayout.Label(message, wizSubtitleStyle, GUILayout.MaxWidth(520f));
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset", GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                RunAction(DimensionTemplateCreationWizardActionPlanUtility.ResetToMinimalActionId);
            }

            GUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = WizBlue;
                if (GUILayout.Button("Create Dimension", GUILayout.Width(180f), GUILayout.Height(30f)))
                {
                    RunSaveAssetsAction();
                }

                GUI.backgroundColor = previous;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
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
            RunSaveAssetsAction(0);
        }

        // The portal visual save resolves the framework's portal SpriteAssets through Scriptable
        // Data, which imports asynchronously and is often mid-import right after a domain reload
        // (a recompile). When that happens the save throws with a "reapply after Scriptable Data
        // finishes importing" hint, leaving the dimension half-created and requiring a second
        // manual click. Instead, catch that specific timing failure and retry automatically once
        // the import settles, so a single click always completes.
        private const int MaxSaveRetryAttempts = 10;

        private void RunSaveAssetsAction(int attempt)
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

            DimensionTemplateAssetEditorSaveResult saveResult;
            try
            {
                saveResult = DimensionTemplateAssetEditorSaveUtility.SaveWorkspace(workspace);
            }
            catch (System.InvalidOperationException exception)
                when (exception.Message.IndexOf(
                          "Scriptable Data finishes importing",
                          System.StringComparison.Ordinal) >= 0)
            {
                if (attempt < MaxSaveRetryAttempts)
                {
                    lastResultMessage =
                        "Waiting for portal art to finish importing, then finishing automatically… (attempt " +
                        (attempt + 1) + " of " + MaxSaveRetryAttempts + ")";
                    lastResultType = MessageType.Info;
                    AssetDatabase.Refresh();
                    EditorApplication.delayCall += () => RunSaveAssetsAction(attempt + 1);
                    Repaint();
                    return;
                }

                lastResultMessage =
                    "Portal art has not finished importing yet. Click Create Dimension Asset again once " +
                    "Scriptable Data settles.";
                lastResultType = MessageType.Warning;
                Repaint();
                return;
            }

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
