using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Review and Build: the finish line. Every check on one page, then one press.
    /// </summary>
    /// <remarks>
    /// The page this replaces reported the same facts as a wall of yes and no rows. Readiness is
    /// the thing a creator is actually looking for, so it leads: a light per check, worded as a
    /// statement about the dimension rather than as the name of an internal flag. The counts that
    /// used to sit at the top are still here, underneath, where they answer "how big is this"
    /// rather than "can I ship".
    /// </remarks>
    internal sealed class DimensionReviewStagePage
    {
        private readonly System.Func<bool> hasSession;
        private readonly System.Func<DimensionTemplateManifestExportPreview> readPreview;
        private readonly System.Action validate;
        private readonly System.Action build;
        private readonly System.Action showAssets;
        private readonly System.Func<bool> canShowAssets;

        private VisualElement root;
        private VisualElement body;
        private DimensionTemplateAsset template;

        internal DimensionReviewStagePage(
            System.Func<bool> hasSession,
            System.Func<DimensionTemplateManifestExportPreview> readPreview,
            System.Action validate,
            System.Action build,
            System.Action showAssets,
            System.Func<bool> canShowAssets)
        {
            this.hasSession = hasSession;
            this.readPreview = readPreview;
            this.validate = validate;
            this.build = build;
            this.showAssets = showAssets;
            this.canShowAssets = canShowAssets;
        }

        internal VisualElement Build()
        {
            root = new VisualElement();
            root.AddToClassList("dim-stage-page-root");

            ScrollView scroller = new ScrollView(ScrollViewMode.Vertical);
            scroller.AddToClassList("dim-fill");
            body = new VisualElement();
            body.AddToClassList("dim-single-detail");
            body.style.maxWidth = 960;
            scroller.Add(body);
            root.Add(scroller);
            return root;
        }

        internal void Refresh(DimensionTemplateAsset dimensionTemplate)
        {
            template = dimensionTemplate;
            if (body == null)
            {
                return;
            }

            body.Clear();
            if (template == null)
            {
                body.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and its checks appear here.",
                    null));
                return;
            }

            if (!hasSession())
            {
                body.Add(DimensionsApiControls.EmptyState(
                    "Nothing to check yet",
                    "The checks appear once the dimension has been read.",
                    null));
                return;
            }

            DimensionTemplateManifestExportPreview preview = readPreview();

            body.Add(BuildChecks(preview));
            body.Add(BuildBuildBar(preview));
            body.Add(BuildCounts(preview));
        }

        private VisualElement BuildChecks(DimensionTemplateManifestExportPreview preview)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Readiness",
                "everything that has to be true");
            VisualElement groupBody = DimensionsApiControls.BodyOf(group);

            VisualElement grid = new VisualElement();
            grid.AddToClassList("dim-check-grid");

            grid.Add(Check(
                preview.ManifestBuilt,
                "The dimension assembles",
                preview.ManifestBuilt
                    ? "Everything the dimension is made of fits together into one world."
                    : "Something is missing or contradictory, so the world cannot be put together yet."));

            grid.Add(Check(
                preview.BlockingManifestExportCount == 0,
                "Nothing is blocking",
                preview.BlockingManifestExportCount == 0
                    ? "No problem serious enough to stop a build."
                    : preview.BlockingManifestExportCount +
                      (preview.BlockingManifestExportCount == 1 ? " problem must" : " problems must") +
                      " be fixed before this can be built."));

            grid.Add(Check(
                preview.WarningCount == 0,
                "Nothing is silent",
                preview.WarningCount == 0
                    ? "Nothing in this dimension quietly does nothing."
                    : preview.WarningCount +
                      (preview.WarningCount == 1 ? " warning is" : " warnings are") +
                      " worth reading before you ship.",
                preview.WarningCount == 0 ? CheckState.Good : CheckState.Warning));

            grid.Add(Check(
                preview.ReadyForRuntimeGeneration,
                "The world can generate",
                preview.ReadyForRuntimeGeneration
                    ? "The ground, the biomes and the map are ready to be built into a real world."
                    : "The world cannot generate yet. Usually the map or a biome is unfinished."));

            grid.Add(Check(
                preview.ReadyForManifestExport,
                "Ready to ship",
                preview.ReadyForManifestExport
                    ? "This dimension can be built and put in a mod."
                    : "Not yet. Clear the blockers above first."));

            groupBody.Add(grid);
            return group;
        }

        private enum CheckState
        {
            Good,
            Warning,
            Bad
        }

        private static VisualElement Check(
            bool passed,
            string title,
            string detail,
            CheckState? forced = null)
        {
            CheckState state = forced ?? (passed ? CheckState.Good : CheckState.Bad);

            VisualElement card = new VisualElement();
            card.AddToClassList("dim-check-card");

            VisualElement head = new VisualElement();
            head.AddToClassList("dim-check-head");
            VisualElement dot = new VisualElement();
            dot.AddToClassList("dim-check-dot");
            dot.AddToClassList(
                state == CheckState.Good
                    ? "dim-check-good"
                    : state == CheckState.Warning ? "dim-check-warn" : "dim-check-bad");
            head.Add(dot);
            Label name = new Label(title);
            name.AddToClassList("dim-check-name");
            head.Add(name);
            card.Add(head);

            Label body = new Label(detail);
            body.AddToClassList("dim-check-detail");
            card.Add(body);
            return card;
        }

        private VisualElement BuildBuildBar(DimensionTemplateManifestExportPreview preview)
        {
            VisualElement bar = new VisualElement();
            bar.AddToClassList("dim-build-bar");

            Label text = new Label(preview.ReadyForManifestExport
                ? "Everything checks out. Building produces the runtime the game loads."
                : "Fix what is blocking above, then build.");
            text.AddToClassList("dim-build-text");
            bar.Add(text);

            Button checkAgain = DimensionsApiControls.GhostButton("Check again", validate);
            bar.Add(checkAgain);

            Button buildButton = DimensionsApiControls.PrimaryButton("Build the Dimension", build);
            buildButton.SetEnabled(preview.ReadyForManifestExport);
            bar.Add(buildButton);

            if (canShowAssets())
            {
                bar.Add(DimensionsApiControls.GhostButton("Show what was built", showAssets));
            }

            return bar;
        }

        private static VisualElement BuildCounts(DimensionTemplateManifestExportPreview preview)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Contents",
                "how big this dimension is");
            VisualElement groupBody = DimensionsApiControls.BodyOf(group);

            VisualElement row = DimensionsApiControls.ChipRow();
            row.Add(DimensionsApiControls.Chip(Plural(preview.BiomeCount, "biome")));
            row.Add(DimensionsApiControls.Chip(Plural(preview.SceneCount, "place")));
            row.Add(DimensionsApiControls.Chip(Plural(preview.AuthoredSpawnableContentCount, "creature")));
            row.Add(DimensionsApiControls.Chip(Plural(preview.AuthoredContentCount, "authored thing")));
            groupBody.Add(row);
            return group;
        }

        private static string Plural(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? string.Empty : "s");
        }
    }
}
