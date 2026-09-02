using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The journey screen: its rail, the stage body, and moving between stages.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        // --------------------------------------------------------------- journey ---

        private VisualElement BuildJourneyScreen()
        {
            VisualElement shell = new VisualElement();
            shell.AddToClassList("dim-row");
            shell.AddToClassList("dim-fill");

            VisualElement rail = new VisualElement();
            rail.AddToClassList("dim-rail");
            Label railTitle = new Label("SECTIONS");
            railTitle.AddToClassList("dim-rail-title");
            rail.Add(railTitle);

            railStageHost = new VisualElement();
            rail.Add(railStageHost);

            VisualElement railSpacer = new VisualElement();
            railSpacer.AddToClassList("dim-grow");
            rail.Add(railSpacer);

            VisualElement railFoot = new VisualElement();
            railFoot.AddToClassList("dim-rail-foot");

            // Diagnostics is not a build step, so it does not sit among them. It is pinned at
            // the foot the way a status bar is: always in the same place, out of the way until
            // something needs attention.
            diagnosticsRailRow = BuildDiagnosticsRailRow();
            railFoot.Add(diagnosticsRailRow);

            Button backHome = new Button(() => ShowHome(true)) { text = "← All dimensions" };
            backHome.AddToClassList("dim-button");
            backHome.AddToClassList("dim-button-ghost");
            backHome.AddToClassList("dim-rail-home");
            railFoot.Add(backHome);
            rail.Add(railFoot);
            shell.Add(rail);

            VisualElement content = new VisualElement();
            content.AddToClassList("dim-content");
            AddPageGlow(content);

            VisualElement head = new VisualElement();
            head.AddToClassList("dim-stage-head");
            stageTitleLabel = new Label(string.Empty);
            stageTitleLabel.AddToClassList("dim-h2");
            head.Add(stageTitleLabel);

            // How far this section actually got, read from the capability registry. It sits beside
            // the stage's name because that is the moment it matters — before a creator spends an
            // afternoon on a page whose backend is a preview. The registry was written to be this
            // and until now was drawn only by the panel body nobody can reach.
            stageMaturityChip = new Label(string.Empty);
            stageMaturityChip.AddToClassList("dim-chip");
            stageMaturityChip.style.display = DisplayStyle.None;
            head.Add(stageMaturityChip);

            // Some stages own a specialised editor as well as their fields. Rather than choose
            // one and hide the other, the header offers both.
            originalPanelToggle = new Button(ToggleOriginalPanel);
            originalPanelToggle.AddToClassList("dim-button");
            originalPanelToggle.AddToClassList("dim-button-ghost");
            originalPanelToggle.AddToClassList("dim-header-toggle");
            head.Add(originalPanelToggle);
            content.Add(head);

            content.Add(BuildActionFeedback());

            // Biomes has a page of its own because a biome is not a list of fields, it is a
            // place; everything else is built from its description in the catalog. Whatever the
            // catalog does not describe still shows its original panel, so no capability is lost
            // while the last of them are brought across.
            biomePage = new DimensionBiomeStagePage(
                source => RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.CreateBiome(selectedTemplate, source)),
                RunAssetAction,
                Repaint);
            biomePageRoot = biomePage.Build();
            content.Add(biomePageRoot);

            blockPage = new DimensionBlockStagePage(
                tilesetStudio,
                RunAssetAction,
                FocusBlockItemFromShell,
                Repaint,
                ReportToCreator);
            blockPageRoot = blockPage.Build();
            content.Add(blockPageRoot);

            portalPage = new DimensionPortalStagePage(
                ResolvePortalStudioForShell,
                HandlePortalStudioResultFromShell,
                GetPortalVersionTabForShell,
                SetPortalVersionTabFromShell,
                HasPortalVersionForShell,
                IsPortalVersionEnabledForShell,
                TogglePortalVersionEnabledFromShell,
                ReportToCreator);
            portalPageRoot = portalPage.Build();
            content.Add(portalPageRoot);

            issuesPage = new DimensionIssuesStagePage(
                () => workspace,
                () => viewModel == null ? null : viewModel.Navigation,
                GoToStage);
            issuesPageRoot = issuesPage.Build();
            content.Add(issuesPageRoot);

            mapPage = new DimensionMapStagePage(
                DrawLayoutStudioForShell,
                () => RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.CreateLayoutTemplate(selectedTemplate)));
            mapPageRoot = mapPage.Build();
            content.Add(mapPageRoot);

            reviewPage = new DimensionReviewStagePage(
                () => viewModel != null && viewModel.SessionReport != null,
                () => viewModel.SessionReport.ManifestExportPreview,
                () =>
                {
                    if (viewModel != null && viewModel.SessionReport != null)
                    {
                        ValidateExportPreview(viewModel.SessionReport.ManifestExportPreview);
                    }
                },
                BuildDimensionFromShell,
                ShowGeneratedAssetsFromShell,
                () => lastGeneratedManifestAsset != null);
            reviewPageRoot = reviewPage.Build();
            content.Add(reviewPageRoot);

            identityPage = new DimensionIdentityStagePage();
            identityPageRoot = identityPage.Build();
            content.Add(identityPageRoot);

            catalogPageHost = new VisualElement();
            catalogPageHost.AddToClassList("dim-fill");
            content.Add(catalogPageHost);

            legacyBodyHost = new IMGUIContainer(DrawLegacyStageBody);
            legacyBodyHost.AddToClassList("dim-panel-host");
            legacyBodyHost.style.flexGrow = 1;
            content.Add(legacyBodyHost);

            shell.Add(content);
            return shell;
        }

        /// <summary>The catalog page for a stage, built once and kept.</summary>
        private DimensionCollectionStagePage ResolveCatalogPage(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
            {
                return null;
            }

            DimensionCollectionStagePage page;
            if (catalogPages.TryGetValue(sectionId, out page))
            {
                return page;
            }

            DimensionStageDescriptor descriptor;
            if (!DimensionStageCatalog.TryGetStage(sectionId, out descriptor))
            {
                return null;
            }

            page = new DimensionCollectionStagePage(descriptor, RunAssetAction);
            VisualElement root = page.Build();
            root.style.display = DisplayStyle.None;
            catalogPageHost.Add(root);
            catalogPageRoots[sectionId] = root;
            catalogPages[sectionId] = page;
            return page;
        }

        /// <summary>
        /// Sections that also have an original panel behind them. A synthetic stage has none, so
        /// offering the toggle there would promise something that does not exist.
        /// </summary>
        private static bool HasOriginalPanel(string sectionId)
        {
            // Nothing offers it any more. Every stage that used to fall back to an original
            // panel now covers all of it: Items and Gear, Mobs and Places each carry their
            // content types as tabs, and any value no card names is still reachable under
            // "Everything else" on the page itself. Offering a second, older looking door to
            // the same values is exactly what this rebuild set out to remove.
            return false;
        }

        private void ToggleOriginalPanel()
        {
            if (string.IsNullOrEmpty(activeSectionId))
            {
                return;
            }

            if (!showOriginalPanel.Remove(activeSectionId))
            {
                showOriginalPanel.Add(activeSectionId);
            }

            RefreshStageBody();
            Repaint();
        }

        private void RefreshStageBody()
        {
            if (biomePageRoot == null || legacyBodyHost == null || catalogPageHost == null ||
                identityPageRoot == null || reviewPageRoot == null || mapPageRoot == null ||
                issuesPageRoot == null || blockPageRoot == null || portalPageRoot == null)
            {
                return;
            }

            bool original = showOriginalPanel.Contains(activeSectionId);
            bool isBiomes = !original &&
                string.Equals(activeSectionId, "biomes", System.StringComparison.Ordinal);
            bool isIdentity = !original &&
                string.Equals(activeSectionId, "dimension", System.StringComparison.Ordinal);
            bool isReview = !original &&
                string.Equals(activeSectionId, "export", System.StringComparison.Ordinal);
            bool isMap = !original &&
                string.Equals(activeSectionId, "layout", System.StringComparison.Ordinal);
            bool isIssues = !original &&
                string.Equals(activeSectionId, "diagnostics", System.StringComparison.Ordinal);
            bool isBlocks = !original &&
                string.Equals(activeSectionId, "tilesets", System.StringComparison.Ordinal);
            bool isPortal = !original &&
                string.Equals(activeSectionId, "portals", System.StringComparison.Ordinal);
            DimensionCollectionStagePage catalogPage = original || isBiomes || isIdentity || isReview || isMap || isIssues || isBlocks || isPortal
                ? null
                : ResolveCatalogPage(activeSectionId);

            biomePageRoot.style.display = isBiomes ? DisplayStyle.Flex : DisplayStyle.None;
            identityPageRoot.style.display = isIdentity ? DisplayStyle.Flex : DisplayStyle.None;
            reviewPageRoot.style.display = isReview ? DisplayStyle.Flex : DisplayStyle.None;
            mapPageRoot.style.display = isMap ? DisplayStyle.Flex : DisplayStyle.None;
            issuesPageRoot.style.display = isIssues ? DisplayStyle.Flex : DisplayStyle.None;
            blockPageRoot.style.display = isBlocks ? DisplayStyle.Flex : DisplayStyle.None;
            portalPageRoot.style.display = isPortal ? DisplayStyle.Flex : DisplayStyle.None;
            catalogPageHost.style.display =
                catalogPage != null ? DisplayStyle.Flex : DisplayStyle.None;
            legacyBodyHost.style.display =
                isBiomes || isIdentity || isReview || isMap || isIssues || isBlocks || isPortal ||
                catalogPage != null
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;

            foreach (KeyValuePair<string, VisualElement> pair in catalogPageRoots)
            {
                pair.Value.style.display =
                    catalogPage != null &&
                    string.Equals(pair.Key, activeSectionId, System.StringComparison.Ordinal)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            if (isBiomes)
            {
                biomePage.Refresh(selectedTemplate);
            }
            else if (isIdentity)
            {
                identityPage.Refresh(selectedTemplate);
            }
            else if (isReview)
            {
                reviewPage.Refresh(selectedTemplate);
            }
            else if (isMap)
            {
                mapPage.Refresh(selectedTemplate);
            }
            else if (isIssues)
            {
                issuesPage.Refresh(selectedTemplate);
            }
            else if (isBlocks)
            {
                blockPage.Refresh(selectedTemplate);
            }
            else if (isPortal)
            {
                EnsurePortalProfileReadyForShell();
                portalPage.Refresh(selectedTemplate, ResolvePortalProfileForShell());
            }
            else if (catalogPage != null)
            {
                catalogPage.Refresh(selectedTemplate);
            }

            RefreshOriginalPanelToggle(original);
            RefreshStageMaturityChip();
        }

        /// <summary>
        /// Puts the active section's honest maturity beside its name.
        /// </summary>
        /// <remarks>
        /// Hidden rather than filled with a guess when a section has no capability record of its
        /// own: a badge naming another feature's maturity would be believed, and believed wrongly.
        /// The full note goes in the tooltip, because the one word on the chip is a summary and the
        /// note is where "what is still missing" is actually written down.
        /// </remarks>
        private void RefreshStageMaturityChip()
        {
            if (stageMaturityChip == null)
            {
                return;
            }

            ExpandNullforge.Api.DimensionCapability capability;
            if (!DimensionStageMaturity.TryResolve(activeSectionId, out capability))
            {
                stageMaturityChip.style.display = DisplayStyle.None;
                return;
            }

            stageMaturityChip.RemoveFromClassList("dim-chip-ready");
            stageMaturityChip.RemoveFromClassList("dim-chip-warn");
            stageMaturityChip.RemoveFromClassList("dim-chip-blocked");
            stageMaturityChip.AddToClassList(DimensionStageMaturity.ChipClass(capability.Maturity));
            stageMaturityChip.text = DimensionStageMaturity.Label(capability.Maturity);
            stageMaturityChip.tooltip = capability.Title + " — " + capability.Note;
            stageMaturityChip.style.display = DisplayStyle.Flex;
        }

        private void RefreshOriginalPanelToggle(bool showingOriginal)
        {
            if (originalPanelToggle == null)
            {
                return;
            }

            if (!HasOriginalPanel(activeSectionId))
            {
                originalPanelToggle.style.display = DisplayStyle.None;
                return;
            }

            originalPanelToggle.style.display = DisplayStyle.Flex;
            originalPanelToggle.text = showingOriginal
                ? "Back to the new page"
                : ResolveOriginalPanelLabel(activeSectionId);
        }

        private static string ResolveOriginalPanelLabel(string sectionId)
        {
            switch (sectionId)
            {
                case "tilesets":
                    return "Open the Block Studio";
                case "biomes":
                    return "Open the original panel";
                default:
                    return "Open the original panel";
            }
        }

        private void RefreshRail()
        {
            if (railStageHost == null)
            {
                return;
            }

            railStageHost.Clear();
            IReadOnlyList<DimensionJourneyStage> stages = DimensionJourney.Stages;
            int activeIndex = DimensionJourney.IndexOf(activeSectionId);
            for (int i = 0; i < stages.Count; i++)
            {
                railStageHost.Add(BuildRailStage(stages[i], i, activeIndex));
            }

            if (diagnosticsRailRow != null)
            {
                bool onDiagnostics = string.Equals(
                    activeSectionId,
                    DimensionJourney.DiagnosticsStageId,
                    System.StringComparison.Ordinal);
                diagnosticsRailRow.EnableInClassList("dim-stage-current", onDiagnostics);
            }

            if (diagnosticsAlertChip != null)
            {
                DimensionAuthoringReadinessState diagnosticsState = DimensionJourney.ResolveState(
                    viewModel == null ? null : viewModel.Navigation,
                    DimensionJourney.DiagnosticsStageId);
                diagnosticsAlertChip.style.display =
                    diagnosticsState == DimensionAuthoringReadinessState.Blocked
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            // Nothing may be silently unreachable. Every known section either is a rail item,
            // is pinned, or had its fields moved onto a page that is. A section this warning
            // names has none of those and needs a home before it ships.
            List<string> outside = DimensionJourney.FindSectionsOutsideTheJourney(
                viewModel == null ? null : viewModel.Navigation);
            if (outside.Count > 0)
            {
                Debug.LogWarning(
                    "[Dimensions API] Sections with no way in from the rail: " +
                    string.Join(", ", outside));
            }
        }

        /// <summary>The pinned Diagnostics row at the rail's foot.</summary>
        private VisualElement BuildDiagnosticsRailRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("dim-stage");

            Label bullet = new Label("◦");
            bullet.AddToClassList("dim-stage-num");
            row.Add(bullet);

            VisualElement labels = new VisualElement();
            labels.AddToClassList("dim-stage-labels");
            Label name = new Label("Diagnostics");
            name.AddToClassList("dim-stage-name");
            labels.Add(name);
            row.Add(labels);

            // A badge only when something demands action: errors light it, warnings do not,
            // and a quiet dimension shows a quiet row.
            diagnosticsAlertChip = new Label("!");
            diagnosticsAlertChip.AddToClassList("dim-chip");
            diagnosticsAlertChip.AddToClassList("dim-chip-blocked");
            diagnosticsAlertChip.style.display = DisplayStyle.None;
            row.Add(diagnosticsAlertChip);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                GoToStage(DimensionJourney.DiagnosticsStageId);
                evt.StopPropagation();
            });
            return row;
        }

        private VisualElement BuildRailStage(
            DimensionJourneyStage stage,
            int index,
            int activeIndex)
        {
            DimensionAuthoringReadinessState state = DimensionJourney.ResolveState(
                viewModel == null ? null : viewModel.Navigation,
                stage.SectionId);
            bool done = state == DimensionAuthoringReadinessState.Ready;

            VisualElement row = new VisualElement();
            row.AddToClassList("dim-stage");
            if (index == activeIndex)
            {
                row.AddToClassList("dim-stage-current");
            }
            if (done)
            {
                row.AddToClassList("dim-stage-done");
            }

            Label number = new Label(done ? "✓" : (index + 1).ToString());
            number.AddToClassList("dim-stage-num");
            row.Add(number);

            VisualElement labels = new VisualElement();
            labels.AddToClassList("dim-stage-labels");
            Label name = new Label(stage.Name);
            name.AddToClassList("dim-stage-name");
            labels.Add(name);
            row.Add(labels);

            if (state == DimensionAuthoringReadinessState.Blocked)
            {
                Label chip = new Label("!");
                chip.AddToClassList("dim-chip");
                chip.AddToClassList("dim-chip-blocked");
                row.Add(chip);
            }
            else if (state == DimensionAuthoringReadinessState.Partial)
            {
                Label chip = new Label("·");
                chip.AddToClassList("dim-chip");
                chip.AddToClassList("dim-chip-warn");
                row.Add(chip);
            }

            string sectionId = stage.SectionId;
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                GoToStage(sectionId);
                evt.StopPropagation();
            });
            return row;
        }

        private void GoToStage(string sectionId)
        {
            activeSectionId = sectionId;
            RefreshRail();
            RefreshStageHeader();
            RefreshStageBody();
            Repaint();
        }

        private void RefreshStageHeader()
        {
            if (stageTitleLabel == null)
            {
                return;
            }

            DimensionJourneyStage stage;
            if (!DimensionJourney.TryGetStage(activeSectionId, out stage))
            {
                stageTitleLabel.text = string.Equals(
                    activeSectionId,
                    DimensionJourney.DiagnosticsStageId,
                    System.StringComparison.Ordinal)
                        ? "Diagnostics"
                        : activeSectionId;
                return;
            }

            stageTitleLabel.text = stage.Name;
        }
    }
}
