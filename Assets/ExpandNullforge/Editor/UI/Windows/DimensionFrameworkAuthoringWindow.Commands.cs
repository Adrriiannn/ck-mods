using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The action panel, the preview canvas, and the export and diagnostics sections.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private void DrawExportEditor()
        {
            DrawSectionIntro(
                "Export",
                "Preview the dimension manifest and export when required authoring data is valid.");

            DimensionTemplateCustomizerSessionReport session = viewModel.SessionReport;
            DimensionTemplateManifestExportPreview preview = session.ManifestExportPreview;
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Manifest", preview.ManifestBuilt ? "Built" : "Pending", "Whether a runtime manifest preview could be generated.");
            DrawDashboardMetric("Blockers", preview.BlockingManifestExportCount.ToString(), "Required issues that block manifest export.");
            DrawDashboardMetric("Warnings", preview.WarningCount.ToString(), "Warnings to review before shipping.");
            DrawDashboardMetric("Authored", preview.AuthoredContentCount.ToString(), "Authored content records summarized for export.");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate", GUILayout.Width(120f)))
            {
                ValidateExportPreview(preview);
            }

            using (new EditorGUI.DisabledScope(!preview.ReadyForManifestExport))
            {
                if (GUILayout.Button("Generate Runtime Manifest", GUILayout.Width(210f)))
                {
                    DimensionFrameworkAuthoringAssetActionResult result =
                        DimensionFrameworkAuthoringAssetUtility.CreateRuntimeManifestAsset(selectedTemplate, preview);
                    if (result != null && result.CreatedObject != null)
                    {
                        lastGeneratedManifestAsset = result.CreatedObject;
                    }

                    RunAssetAction(result);
                }
            }

            using (new EditorGUI.DisabledScope(lastGeneratedManifestAsset == null))
            {
                if (GUILayout.Button("Show Generated Assets", GUILayout.Width(170f)))
                {
                    Selection.activeObject = lastGeneratedManifestAsset;
                    EditorGUIUtility.PingObject(lastGeneratedManifestAsset);
                    lastEditorActionMessage = "Selected the last generated runtime manifest asset.";
                    lastEditorActionType = MessageType.Info;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8f);
            DrawContextualPreviewCanvas(viewModel.PreviewCanvas);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Readiness checklist", EditorStyles.boldLabel);
            DrawNamedValue("Manifest built", preview.ManifestBuilt ? "Yes" : "No");
            DrawNamedValue("Ready for export", preview.ReadyForManifestExport ? "Yes" : "No");
            DrawNamedValue("Runtime generation", preview.ReadyForRuntimeGeneration ? "Ready" : "Preparation pending");
            DrawNamedValue("Validation request", preview.ReadyForValidation ? "Ready" : "Blocked");
            DrawNamedValue("Apply request", preview.ReadyForApply ? "Ready" : "Blocked");
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Runtime manifest", EditorStyles.boldLabel);
            DrawNamedValue("Dimensions", preview.DimensionCount.ToString());
            DrawNamedValue("Biomes", preview.BiomeCount.ToString());
            DrawNamedValue("Scenes", preview.SceneCount.ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Authoring content", EditorStyles.boldLabel);
            DrawNamedValue("Scenes", preview.AuthoredSceneContentCount.ToString());
            DrawNamedValue("Resources", preview.AuthoredResourceContentCount.ToString());
            DrawNamedValue("Spawns", preview.AuthoredSpawnableContentCount.ToString());
            DrawNamedValue("Ownership", preview.OwnershipBindingCount.ToString());
            DrawNamedValue("Asset references", preview.AssetReferenceCount.ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawEditorCard(
                "Export gate",
                "Export stays unavailable only for true blockers: missing required identity, layout, biome, terrain, broken references, or out-of-bounds required content.");
            GUILayout.Space(8f);
            DrawDetailRows(viewModel.ActiveSectionDetail);
        }

        private void DrawDiagnosticsEditor()
        {
            DrawSectionIntro(
                "Diagnostics",
                "Review raw authoring warnings, blockers, prepared actions, visual asset checks, and setup-guide details. This page is intentionally technical.");

            DrawSummaryStrip();
            GUILayout.Space(8f);
            DrawActionPanel();
            GUILayout.Space(8f);
            DrawVisualAssetValidation();
            GUILayout.Space(8f);
            DrawSetupGuide();
            GUILayout.Space(8f);
            DrawDetailRows(viewModel.ActiveSectionDetail);
        }

        private void DrawSectionIntro(string title, string description)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            }

            GUILayout.Space(8f);
        }

        private void DrawEditorCard(string title, string body)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(body))
            {
                EditorGUILayout.LabelField(body, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void RunAssetAction(DimensionFrameworkAuthoringAssetActionResult result)
        {
            if (result == null)
            {
                lastEditorActionMessage = "The authoring action returned no result.";
                lastEditorActionType = MessageType.Warning;
            }
            else
            {
                lastEditorActionMessage = result.Message;
                lastEditorActionType = result.Executed ? MessageType.Info : MessageType.Warning;
                if (result.CreatedObject is DimensionRuntimeManifestAsset)
                {
                    lastGeneratedManifestAsset = result.CreatedObject;
                }
            }

            RebuildWorkspace();
            Repaint();
        }

        private void ValidateExportPreview(DimensionTemplateManifestExportPreview preview)
        {
            if (!preview.ManifestBuilt)
            {
                lastEditorActionMessage = "Manifest validation could not run because the manifest preview did not build: " + preview.Message;
                lastEditorActionType = MessageType.Warning;
            }
            else if (!preview.ReadyForManifestExport)
            {
                lastEditorActionMessage =
                    "Manifest validation found " +
                    preview.BlockingManifestExportCount.ToString() +
                    " export blocker(s): " +
                    preview.Message;
                lastEditorActionType = MessageType.Warning;
            }
            else
            {
                lastEditorActionMessage =
                    "Manifest validation passed. Runtime records: " +
                    preview.DimensionCount.ToString() +
                    " dimension(s), " +
                    preview.BiomeCount.ToString() +
                    " biome(s), " +
                    preview.ZoneCount.ToString() +
                    " zone(s).";
                lastEditorActionType = MessageType.Info;
            }

            RebuildWorkspace();
            Repaint();
        }

        private BiomeTemplateAsset GetSelectedBiomeOrFirst()
        {
            BiomeTemplateAsset[] biomes = GetBiomes();
            if (biomes.Length == 0)
            {
                return null;
            }

            if (selectedBiomeIndex < 0 || selectedBiomeIndex >= biomes.Length)
            {
                selectedBiomeIndex = 0;
            }

            return biomes[selectedBiomeIndex];
        }

        private void DrawActionPanel()
        {
            DimensionTemplateCustomizerCommandPlan commandPlan =
                viewModel == null ? null : viewModel.CommandPlan;
            IReadOnlyList<DimensionTemplateCustomizerCommand> commands =
                commandPlan == null ? null : commandPlan.Commands;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Authoring actions", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (commandPlan != null)
            {
                EditorGUILayout.LabelField(
                    "Ready " + commandPlan.ReadyCount +
                    "   Warn " + commandPlan.WarningCount +
                    "   Blocked " + commandPlan.BlockedCount +
                    "   Needs code " + commandPlan.ImplementationCount,
                    EditorStyles.miniLabel,
                    GUILayout.Width(270f));
            }

            EditorGUILayout.EndHorizontal();

            if (commands == null || commands.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No actions are available for this section yet.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            actionScroll = EditorGUILayout.BeginScrollView(actionScroll, GUILayout.MinHeight(92f), GUILayout.MaxHeight(140f));
            for (int i = 0; i < commands.Count; i++)
            {
                DrawCommandRow(commands[i]);
            }

            EditorGUILayout.EndScrollView();
            DrawSelectedCommandControls(commandPlan);
            EditorGUILayout.EndVertical();
        }

        private void DrawCommandRow(DimensionTemplateCustomizerCommand command)
        {
            if (command == null || command.Action.ActionId == string.Empty)
            {
                return;
            }

            bool selected = selectedActionId == command.CommandId;
            Color oldColor = GUI.color;
            GUI.color = selected ? new Color(0.74f, 0.9f, 1f, 1f) : CommandStateColor(command.State);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            string label = command.Action.Label;
            if (string.IsNullOrEmpty(label))
            {
                label = command.CommandId;
            }

            if (command.IsPrimary)
            {
                label = "* " + label;
            }

            if (GUILayout.Button(label, selected ? EditorStyles.toolbarButton : EditorStyles.miniButton, GUILayout.Width(190f)))
            {
                SelectCommand(command);
                PreviewSelectedCommand();
            }

            GUI.color = oldColor;
            EditorGUILayout.LabelField(command.State.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(120f));
            string summary = command.Preview == null ? command.BlockedReason : command.Preview.Summary;
            if (string.IsNullOrEmpty(summary))
            {
                summary = command.BlockedReason;
            }

            EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedCommandControls(DimensionTemplateCustomizerCommandPlan commandPlan)
        {
            DimensionTemplateCustomizerCommand selectedCommand =
                ResolveSelectedCommand(commandPlan);
            if (selectedCommand == null)
            {
                EditorGUILayout.HelpBox(
                    "Select an action above to preview its safety requirements and expected result.",
                    MessageType.Info);
                return;
            }

            DimensionTemplateCustomizerActionDescriptor action = selectedCommand.Action;
            DimensionTemplateCustomizerActionPreview preview = selectedCommand.Preview;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(action.Label) ? selectedCommand.CommandId : action.Label,
                EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(action.Tooltip))
            {
                EditorGUILayout.LabelField(action.Tooltip, EditorStyles.wordWrappedMiniLabel);
            }

            if (preview != null)
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(preview.Summary, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrEmpty(preview.ExpectedResult))
                {
                    EditorGUILayout.LabelField("Expected: " + preview.ExpectedResult, EditorStyles.wordWrappedMiniLabel);
                }

                if (!string.IsNullOrEmpty(preview.SafetyMessage))
                {
                    EditorGUILayout.HelpBox(preview.SafetyMessage, MessageType.Warning);
                }

                if (!string.IsNullOrEmpty(preview.InputHint))
                {
                    EditorGUILayout.LabelField(preview.InputHint, EditorStyles.miniBoldLabel);
                    commandInputValue = EditorGUILayout.TextField(commandInputValue);
                }
            }

            if (action.RequiresConfirmation)
            {
                commandConfirmed = EditorGUILayout.ToggleLeft(
                    "I understand this command's safety notes.",
                    commandConfirmed);
            }

            if (action.MutatesAssets)
            {
                allowAssetMutation = EditorGUILayout.ToggleLeft(
                    "Allow this command to mutate authoring assets when an executor is implemented.",
                    allowAssetMutation);
            }

            if (action.TouchesRuntimeState)
            {
                allowRuntimeMutation = EditorGUILayout.ToggleLeft(
                    "Allow this command to touch runtime state when an executor is implemented.",
                    allowRuntimeMutation);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview", GUILayout.Width(120f)))
            {
                PreviewSelectedCommand();
            }

            if (GUILayout.Button("Prepare", GUILayout.Width(120f)))
            {
                PrepareSelectedCommand();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawPreparedCommand(preparedCommand);
            EditorGUILayout.EndVertical();
        }

        private DimensionTemplateCustomizerCommand ResolveSelectedCommand(
            DimensionTemplateCustomizerCommandPlan commandPlan)
        {
            IReadOnlyList<DimensionTemplateCustomizerCommand> commands =
                commandPlan == null ? null : commandPlan.Commands;
            if (commands == null || commands.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(selectedActionId))
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    if (commands[i].CommandId == selectedActionId)
                    {
                        return commands[i];
                    }
                }
            }

            return commandPlan.PrimaryCommand == null ? commands[0] : commandPlan.PrimaryCommand;
        }

        private void SelectCommand(DimensionTemplateCustomizerCommand command)
        {
            if (command == null)
            {
                selectedActionId = string.Empty;
                preparedCommand = null;
                return;
            }

            selectedActionId = command.CommandId;
            preparedCommand = null;
            commandInputValue = string.Empty;
            commandConfirmed = false;
            allowAssetMutation = false;
            allowRuntimeMutation = false;
        }

        private void PreviewSelectedCommand()
        {
            if (workspace == null)
            {
                preparedCommand = null;
                return;
            }

            DimensionTemplateCustomizerCommandRequest request =
                DimensionTemplateCustomizerCommandRequest.Preview(
                    activeSectionId,
                    selectedActionId,
                    DimensionTemplateCustomizerFocusRequest.ForAction(activeSectionId, selectedActionId));
            preparedCommand =
                DimensionTemplateCustomizerCommandRequestUtility.Prepare(workspace, request);
        }

        private void PrepareSelectedCommand()
        {
            if (workspace == null)
            {
                preparedCommand = null;
                return;
            }

            DimensionTemplateCustomizerCommandRequest request =
                DimensionTemplateCustomizerCommandRequest.Prepare(
                    activeSectionId,
                    selectedActionId,
                    DimensionTemplateCustomizerFocusRequest.ForAction(activeSectionId, selectedActionId),
                    commandConfirmed,
                    allowAssetMutation,
                    allowRuntimeMutation,
                    commandInputValue);
            preparedCommand =
                DimensionTemplateCustomizerCommandRequestUtility.Prepare(workspace, request);
        }

        private void DrawPreparedCommand(DimensionTemplateCustomizerPreparedCommand prepared)
        {
            if (prepared == null)
            {
                return;
            }

            MessageType messageType = prepared.CanRun
                ? MessageType.Info
                : prepared.State == DimensionTemplateCustomizerPreparedCommandState.PreviewReady
                    ? MessageType.Info
                    : MessageType.Warning;
            EditorGUILayout.HelpBox(
                prepared.State + ": " + prepared.Message,
                messageType);

            string flags =
                "Can preview: " + prepared.CanPreview +
                "   Can run: " + prepared.CanRun +
                "   Assets: " + prepared.MutatesAssets +
                "   Runtime: " + prepared.TouchesRuntimeState;
            EditorGUILayout.LabelField(flags, EditorStyles.miniLabel);
        }

        private void DrawContextualPreviewCanvas(DimensionAuthoringCanvasModel canvas)
        {
            if (ShouldDrawPreviewCanvas(canvas))
            {
                DrawPreviewCanvas(canvas);
                GUILayout.Space(8f);
                return;
            }

            if (ShouldExplainHiddenPreview())
            {
                EditorGUILayout.HelpBox(
                    "No authored world preview exists yet. Use Layout to place biomes, then Generation to preview how the dimension will be assembled.",
                    MessageType.Info);
                GUILayout.Space(8f);
            }
        }

        private bool ShouldExplainHiddenPreview()
        {
            return activeSectionId == "layout" ||
                activeSectionId == "generation";
        }

        private static bool ShouldDrawPreviewCanvas(DimensionAuthoringCanvasModel canvas)
        {
            if (canvas.Layers == null)
            {
                return false;
            }

            for (int i = 0; i < canvas.Layers.Count; i++)
            {
                DimensionAuthoringCanvasLayer layer = canvas.Layers[i];
                if (!layer.VisibleByDefault ||
                    layer.Items == null ||
                    layer.Items.Count == 0 ||
                    !ShouldLayerCountAsPreviewContent(canvas.PlayableLocalBounds, layer))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool ShouldLayerCountAsPreviewContent(
            DimensionBounds playableBounds,
            DimensionAuthoringCanvasLayer layer)
        {
            if (layer.LayerKind == DimensionAuthoringPreviewLayerKind.PlayableBounds)
            {
                return false;
            }

            if (layer.LayerKind != DimensionAuthoringPreviewLayerKind.BiomeRegion &&
                layer.LayerKind != DimensionAuthoringPreviewLayerKind.GenerationPassBounds)
            {
                return true;
            }

            if (layer.Items.Count > 1)
            {
                return true;
            }

            DimensionAuthoringCanvasItem item = layer.Items[0];
            return !BoundsEqual(playableBounds, item.LocalBounds);
        }

        private static bool BoundsEqual(DimensionBounds left, DimensionBounds right)
        {
            return left.Min.x == right.Min.x &&
                left.Min.y == right.Min.y &&
                left.MaxExclusive.x == right.MaxExclusive.x &&
                left.MaxExclusive.y == right.MaxExclusive.y;
        }

        private void DrawPreviewCanvas(DimensionAuthoringCanvasModel canvas)
        {
            Rect rect = GUILayoutUtility.GetRect(
                420f,
                CanvasMinHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.MinHeight(CanvasMinHeight));
            EditorGUI.DrawRect(rect, new Color(0.07f, 0.09f, 0.11f, 1f));
            DrawGrid(rect);

            if (canvas.Layers == null || canvas.Layers.Count == 0)
            {
                DrawCenteredLabel(rect, "No preview layers available.");
                return;
            }

            DimensionBounds bounds = canvas.PlayableLocalBounds;
            if (bounds.MaxExclusive.x <= bounds.Min.x || bounds.MaxExclusive.y <= bounds.Min.y)
            {
                DrawCenteredLabel(rect, "Preview bounds are empty.");
                return;
            }

            for (int i = 0; i < canvas.Layers.Count; i++)
            {
                DimensionAuthoringCanvasLayer layer = canvas.Layers[i];
                if (!layer.VisibleByDefault ||
                    layer.Items == null ||
                    layer.LayerKind == DimensionAuthoringPreviewLayerKind.PlayableBounds)
                {
                    continue;
                }

                for (int j = 0; j < layer.Items.Count; j++)
                {
                    DrawCanvasItem(rect, bounds, layer, layer.Items[j]);
                }
            }
        }

        private void DrawGrid(Rect rect)
        {
            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.06f);
            for (int i = 1; i < 4; i++)
            {
                float x = Mathf.Lerp(rect.xMin, rect.xMax, i / 4f);
                float y = Mathf.Lerp(rect.yMin, rect.yMax, i / 4f);
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }

            Handles.color = oldColor;
        }

        private void DrawCanvasItem(
            Rect rect,
            DimensionBounds canvasBounds,
            DimensionAuthoringCanvasLayer layer,
            DimensionAuthoringCanvasItem item)
        {
            DimensionBounds itemBounds = item.LocalBounds;
            Rect itemRect = ToCanvasRect(rect, canvasBounds, itemBounds);
            if (itemRect.width < 1f || itemRect.height < 1f)
            {
                return;
            }

            if (layer.FillByDefault)
            {
                EditorGUI.DrawRect(itemRect, ToColor(item.FillColorRgba));
            }

            if (layer.OutlineByDefault)
            {
                DrawRectOutline(itemRect, ToColor(item.OutlineColorRgba), item.HasConflict ? 2f : 1f);
            }

            if (!string.IsNullOrEmpty(item.DisplayName) && itemRect.width > 48f && itemRect.height > 18f)
            {
                GUI.Label(itemRect, item.DisplayName, EditorStyles.centeredGreyMiniLabel);
            }
        }

        private Rect ToCanvasRect(
            Rect rect,
            DimensionBounds canvasBounds,
            DimensionBounds itemBounds)
        {
            float width = canvasBounds.MaxExclusive.x - canvasBounds.Min.x;
            float height = canvasBounds.MaxExclusive.y - canvasBounds.Min.y;
            if (width <= 0f || height <= 0f)
            {
                return Rect.zero;
            }

            float xMin = rect.xMin + ((itemBounds.Min.x - canvasBounds.Min.x) / width) * rect.width;
            float xMax = rect.xMin + ((itemBounds.MaxExclusive.x - canvasBounds.Min.x) / width) * rect.width;
            float yMin = rect.yMax - ((itemBounds.MaxExclusive.y - canvasBounds.Min.y) / height) * rect.height;
            float yMax = rect.yMax - ((itemBounds.Min.y - canvasBounds.Min.y) / height) * rect.height;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void DrawDetailRows(DimensionTemplateCustomizerSectionDetail detail)
        {
            EditorGUILayout.LabelField("Guidance and diagnostics", EditorStyles.boldLabel);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MinHeight(170f));
            if (detail == null || detail.Rows == null || detail.Rows.Count == 0)
            {
                EditorGUILayout.LabelField("No rows for this section.");
            }
            else
            {
                for (int i = 0; i < detail.Rows.Count; i++)
                {
                    DrawDetailRow(detail.Rows[i]);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDetailRow(DimensionTemplateCustomizerDetailRow row)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUIStyle titleStyle = row.Severity == DimensionAuthoringSeverity.Error
                ? EditorStyles.boldLabel
                : EditorStyles.label;
            EditorGUILayout.LabelField(row.Title, titleStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(row.Kind.ToString(), EditorStyles.miniLabel, GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(row.Message))
            {
                EditorGUILayout.LabelField(row.Message, EditorStyles.wordWrappedMiniLabel);
            }

            if (row.HasLocalBounds)
            {
                EditorGUILayout.LabelField(FormatBounds(row.LocalBounds), EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawVisualAssetValidation()
        {
            DimensionVisualAssetValidationReport report =
                viewModel.SessionReport == null ? null : viewModel.SessionReport.VisualAssetValidation;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Visual asset preflight", EditorStyles.boldLabel);
            if (report == null)
            {
                EditorGUILayout.HelpBox("No visual asset validation report is available.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(
                "Ready: " + report.ReadyCount +
                "   Advisory: " + report.AdvisoryCount +
                "   Blocked: " + report.BlockedCount +
                "   Vanilla refs: " + report.VanillaReferenceCount +
                "   Mod assets: " + report.ModAssetCount +
                "   Packages: " + report.PackageAssetCount);
            EditorGUILayout.HelpBox(report.Message, report.BlockedCount > 0 ? MessageType.Error : MessageType.Info);

            notesScroll = EditorGUILayout.BeginScrollView(notesScroll, GUILayout.MinHeight(120f));
            IReadOnlyList<DimensionVisualAssetValidationEntry> entries = report.Entries;
            if (entries == null || entries.Count == 0)
            {
                EditorGUILayout.LabelField("No visual asset references found in the manifest.");
            }
            else
            {
                int maxRows = Mathf.Min(entries.Count, 16);
                for (int i = 0; i < maxRows; i++)
                {
                    DrawVisualAssetEntry(entries[i]);
                }

                if (entries.Count > maxRows)
                {
                    EditorGUILayout.LabelField("Showing first " + maxRows + " of " + entries.Count + " references.");
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawVisualAssetEntry(DimensionVisualAssetValidationEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DimensionAssetReferenceDefinition reference = entry.Reference;
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(reference.DisplayName) ? reference.AssetId : reference.DisplayName,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                entry.State + " | " + entry.SourceKind + " | " + reference.Kind + " | " + reference.ResourceKey,
                EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(entry.Guidance))
            {
                EditorGUILayout.LabelField(entry.Guidance, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private void DrawCenteredLabel(Rect rect, string text)
        {
            GUI.Label(rect, text, EditorStyles.centeredGreyMiniLabel);
        }

        private static Color CommandStateColor(DimensionTemplateCustomizerCommandState state)
        {
            if (state == DimensionTemplateCustomizerCommandState.Ready)
            {
                return new Color(0.78f, 1f, 0.78f, 1f);
            }

            if (state == DimensionTemplateCustomizerCommandState.Warning ||
                state == DimensionTemplateCustomizerCommandState.NeedsConfirmation)
            {
                return new Color(1f, 0.92f, 0.62f, 1f);
            }

            if (state == DimensionTemplateCustomizerCommandState.NeedsImplementation)
            {
                return new Color(0.78f, 0.86f, 1f, 1f);
            }

            if (state == DimensionTemplateCustomizerCommandState.Blocked ||
                state == DimensionTemplateCustomizerCommandState.Disabled)
            {
                return new Color(1f, 0.65f, 0.65f, 1f);
            }

            return new Color(0.78f, 0.78f, 0.78f, 1f);
        }

        private static Color ToColor(uint rgba)
        {
            float r = ((rgba >> 24) & 0xFF) / 255f;
            float g = ((rgba >> 16) & 0xFF) / 255f;
            float b = ((rgba >> 8) & 0xFF) / 255f;
            float a = (rgba & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }

        private static string FormatBounds(DimensionBounds bounds)
        {
            return "Bounds: (" + bounds.Min.x + ", " + bounds.Min.y + ") to (" +
                bounds.MaxExclusive.x + ", " + bounds.MaxExclusive.y + ")";
        }
    }
}
