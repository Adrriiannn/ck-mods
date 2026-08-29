using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Everything the framework has to say about this dimension, gathered from every stage into
    /// one list and sorted by how much it matters.
    /// </summary>
    /// <remarks>
    /// The page this replaces was called Diagnostics and was described in its own text as
    /// "intentionally technical". Nothing here is technical: a problem is a sentence about the
    /// dimension, and the stage it belongs to is a chip beside it, so a creator can read the list
    /// and know where to go.
    /// </remarks>
    internal sealed class DimensionIssuesStagePage
    {
        private readonly System.Func<DimensionTemplateAuthoringWorkspace> readWorkspace;
        private readonly System.Func<DimensionTemplateCustomizerNavigationModel> readNavigation;
        private readonly System.Action<string> goToStage;

        private VisualElement root;
        private VisualElement body;
        private DimensionTemplateAsset template;

        internal DimensionIssuesStagePage(
            System.Func<DimensionTemplateAuthoringWorkspace> readWorkspace,
            System.Func<DimensionTemplateCustomizerNavigationModel> readNavigation,
            System.Action<string> goToStage)
        {
            this.readWorkspace = readWorkspace;
            this.readNavigation = readNavigation;
            this.goToStage = goToStage;
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
            DimensionTemplateAuthoringWorkspace workspace = readWorkspace();
            DimensionTemplateCustomizerNavigationModel navigation = readNavigation();
            if (template == null || workspace == null || navigation == null)
            {
                body.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and anything worth knowing about it appears here.",
                    null));
                return;
            }

            List<DimensionTemplateCustomizerDetailRow> problems =
                new List<DimensionTemplateCustomizerDetailRow>();
            List<DimensionTemplateCustomizerDetailRow> worthKnowing =
                new List<DimensionTemplateCustomizerDetailRow>();

            for (int i = 0; i < navigation.Sections.Count; i++)
            {
                DimensionTemplateCustomizerSectionDetail detail =
                    DimensionTemplateCustomizerSectionDetailUtility.Build(
                        workspace,
                        navigation.Sections[i].SectionId);
                if (detail == null || detail.Rows == null)
                {
                    continue;
                }

                for (int r = 0; r < detail.Rows.Count; r++)
                {
                    DimensionTemplateCustomizerDetailRow row = detail.Rows[r];
                    if (row.Kind != DimensionTemplateCustomizerDetailRowKind.Issue)
                    {
                        continue;
                    }

                    // The split is by severity, exactly: the Errors card holds errors and only
                    // errors. An earlier partition put warnings among the errors, which taught
                    // creators that half the red list could be ignored — the one lesson a
                    // diagnostics page must never teach.
                    if (row.Severity == DimensionAuthoringSeverity.Error)
                    {
                        problems.Add(row);
                    }
                    else
                    {
                        worthKnowing.Add(row);
                    }
                }
            }

            // Warnings before plain notices inside the second card.
            worthKnowing.Sort((left, right) => right.Severity.CompareTo(left.Severity));

            if (problems.Count == 0 && worthKnowing.Count == 0)
            {
                body.Add(DimensionsApiControls.EmptyState(
                    "Nothing to report",
                    "The framework has no complaints about this dimension. Anything it finds later will appear here.",
                    null));
                return;
            }

            if (problems.Count > 0)
            {
                body.Add(BuildList("Errors", null, problems));
            }

            if (worthKnowing.Count > 0)
            {
                body.Add(BuildList("Warnings", null, worthKnowing));
            }
        }

        private VisualElement BuildList(
            string title,
            string hint,
            List<DimensionTemplateCustomizerDetailRow> rows)
        {
            VisualElement group = DimensionsApiControls.Group(title, hint);
            VisualElement groupBody = DimensionsApiControls.BodyOf(group);
            for (int i = 0; i < rows.Count; i++)
            {
                groupBody.Add(BuildRow(rows[i]));
            }

            return group;
        }

        private VisualElement BuildRow(DimensionTemplateCustomizerDetailRow row)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-issue-card");

            VisualElement head = new VisualElement();
            head.AddToClassList("dim-check-head");

            VisualElement dot = new VisualElement();
            dot.AddToClassList("dim-check-dot");
            dot.AddToClassList(
                row.Severity == DimensionAuthoringSeverity.Error
                    ? "dim-check-bad"
                    : row.Severity == DimensionAuthoringSeverity.Warning
                        ? "dim-check-warn"
                        : "dim-check-good");
            head.Add(dot);

            Label title = new Label(string.IsNullOrEmpty(row.Title) ? "Something to look at" : row.Title);
            title.AddToClassList("dim-check-name");
            head.Add(title);
            card.Add(head);

            if (!string.IsNullOrEmpty(row.Message))
            {
                Label message = new Label(row.Message);
                message.AddToClassList("dim-check-detail");
                card.Add(message);
            }

            // The stage a problem belongs to is the useful part: it turns a complaint into a place
            // to go and fix it.
            DimensionJourneyStage stage;
            if (DimensionJourney.TryGetStage(row.SectionId, out stage))
            {
                VisualElement chips = DimensionsApiControls.ChipRow();
                Button jump = new Button(() => goToStage(row.SectionId))
                {
                    text = "Go to " + stage.Name
                };
                jump.AddToClassList("dim-chip");
                jump.AddToClassList("dim-chip-link");
                chips.Add(jump);
                card.Add(chips);
            }

            return card;
        }
    }
}
