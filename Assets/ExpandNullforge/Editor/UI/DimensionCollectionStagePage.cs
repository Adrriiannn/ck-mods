using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The page every stage uses: the kinds of thing this stage owns across the top, the things
    /// themselves down the left, and the selected one opened as cards on the right.
    /// </summary>
    /// <remarks>
    /// There is one of these rather than one page per content type, so a stage cannot drift away
    /// from its neighbours in layout, spacing or wording. What varies between stages is the
    /// <see cref="DimensionStageDescriptor"/> handed to it, never the page itself.
    /// </remarks>
    internal sealed class DimensionCollectionStagePage
    {
        private readonly DimensionStageDescriptor descriptor;
        private readonly System.Action<DimensionFrameworkAuthoringAssetActionResult> runAction;

        private VisualElement root;
        private VisualElement tabHost;
        private VisualElement listHost;
        private VisualElement detailHost;
        private DimensionTemplateAsset template;
        private int selectedTab;
        private readonly Dictionary<string, Object> selectionByTab = new Dictionary<string, Object>();

        internal DimensionCollectionStagePage(
            DimensionStageDescriptor descriptor,
            System.Action<DimensionFrameworkAuthoringAssetActionResult> runAction)
        {
            this.descriptor = descriptor;
            this.runAction = runAction;
        }

        internal string SectionId
        {
            get { return descriptor.SectionId; }
        }

        internal VisualElement Build()
        {
            root = new VisualElement();
            root.AddToClassList("dim-stage-page-root");

            tabHost = new VisualElement();
            root.Add(tabHost);

            VisualElement columns = new VisualElement();
            columns.AddToClassList("dim-stage-page");
            root.Add(columns);

            ScrollView listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.AddToClassList("dim-list-column");
            listHost = new VisualElement();
            listScroll.Add(listHost);
            columns.Add(listScroll);

            ScrollView detailScroll = new ScrollView(ScrollViewMode.Vertical);
            detailScroll.AddToClassList("dim-detail-column");
            detailHost = new VisualElement();
            detailHost.AddToClassList("dim-detail");
            detailScroll.Add(detailHost);
            columns.Add(detailScroll);

            return root;
        }

        internal void Refresh(DimensionTemplateAsset dimensionTemplate)
        {
            template = dimensionTemplate;
            if (root == null)
            {
                return;
            }

            if (selectedTab >= descriptor.Collections.Count)
            {
                selectedTab = 0;
            }

            RebuildTabs();
            RebuildList();
            RebuildDetail();
        }

        private DimensionCollectionDescriptor Current
        {
            get
            {
                return descriptor.Collections.Count == 0
                    ? null
                    : descriptor.Collections[Mathf.Clamp(selectedTab, 0, descriptor.Collections.Count - 1)];
            }
        }

        private void RebuildTabs()
        {
            tabHost.Clear();
            if (descriptor.Collections.Count <= 1)
            {
                return;
            }

            List<string> labels = new List<string>();
            for (int i = 0; i < descriptor.Collections.Count; i++)
            {
                DimensionCollectionDescriptor collection = descriptor.Collections[i];
                int count = CountOf(collection);
                labels.Add(count > 0
                    ? collection.TabLabel + "  " + count
                    : collection.TabLabel);
            }

            tabHost.Add(DimensionsApiControls.Tabs(labels, selectedTab, index =>
            {
                selectedTab = index;
                DeferredRefresh();
            }));
        }

        private int CountOf(DimensionCollectionDescriptor collection)
        {
            IReadOnlyList<Object> items = template == null || collection.Read == null
                ? null
                : collection.Read(template);
            return items == null ? 0 : items.Count;
        }

        private void RebuildList()
        {
            listHost.Clear();
            DimensionCollectionDescriptor collection = Current;
            if (collection == null || template == null)
            {
                return;
            }

            IReadOnlyList<Object> items = collection.Read(template);
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    listHost.Add(BuildCard(collection, items[i]));
                }
            }

            if (collection.CreateChoices != null)
            {
                for (int i = 0; i < collection.CreateChoices.Count; i++)
                {
                    DimensionCollectionCreateChoice choice = collection.CreateChoices[i];
                    Button add = new Button(() => runAction(choice.Create(template)))
                    {
                        text = choice.Label
                    };
                    add.AddToClassList("dim-add-card");
                    listHost.Add(add);
                }
            }
            else if (collection.Create != null)
            {
                Button add = new Button(() => runAction(collection.Create(template)))
                {
                    text = "+ New " + collection.Singular
                };
                add.AddToClassList("dim-add-card");
                listHost.Add(add);
            }
        }

        private VisualElement BuildCard(DimensionCollectionDescriptor collection, Object item)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-item-card");
            if (item == Selected(collection))
            {
                card.AddToClassList("dim-item-card-selected");
            }

            Label name = new Label(collection.Title == null || item == null
                ? "Missing"
                : collection.Title(item));
            name.AddToClassList("dim-item-card-name");
            card.Add(name);

            string subtitle = collection.Subtitle == null || item == null
                ? string.Empty
                : collection.Subtitle(item);
            if (!string.IsNullOrEmpty(subtitle))
            {
                Label sub = new Label(subtitle);
                sub.AddToClassList("dim-item-card-sub");
                card.Add(sub);
            }

            if (item != null)
            {
                card.RegisterCallback<MouseDownEvent>(evt =>
                {
                    selectionByTab[collection.Key] = item;
                    evt.StopPropagation();
                    DeferredRefresh();
                });
            }

            return card;
        }

        private Object Selected(DimensionCollectionDescriptor collection)
        {
            Object selected;
            if (!selectionByTab.TryGetValue(collection.Key, out selected) || selected == null)
            {
                IReadOnlyList<Object> items = template == null ? null : collection.Read(template);
                selected = items != null && items.Count > 0 ? items[0] : null;
                selectionByTab[collection.Key] = selected;
                return selected;
            }

            // A thing deleted outside this window must not stay selected inside it.
            IReadOnlyList<Object> current = template == null ? null : collection.Read(template);
            if (current != null && !Contains(current, selected))
            {
                selected = current.Count > 0 ? current[0] : null;
                selectionByTab[collection.Key] = selected;
            }

            return selected;
        }

        private static bool Contains(IReadOnlyList<Object> items, Object candidate)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildDetail()
        {
            detailHost.Clear();
            DimensionCollectionDescriptor collection = Current;
            if (collection == null)
            {
                return;
            }

            if (template == null)
            {
                detailHost.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and its contents appear here.",
                    null));
                return;
            }

            Object selected = Selected(collection);
            if (selected == null)
            {
                Button make = collection.Create == null
                    ? null
                    : DimensionsApiControls.PrimaryButton(
                        "Create your first " + collection.Singular,
                        () => runAction(collection.Create(template)));
                detailHost.Add(DimensionsApiControls.EmptyState(
                    collection.EmptyHeadline,
                    collection.EmptyHelp,
                    make));
                return;
            }

            SerializedObject serialized = new SerializedObject(selected);

            // ---- door one, above the fields it fills ----
            // Every move Core Keeper authored is offered here, on anything with fight settings.
            // It sits above the cards rather than below them because it is the shortcut past
            // them: a creator who takes one arrives at those fields already filled in, and a
            // creator who ignores it fills them in itself, which is the second door.
            VisualElement borrow = DimensionBorrowedAttackCard.Build(selected, DeferredRefresh);
            if (borrow != null)
            {
                detailHost.Add(borrow);
            }

            IReadOnlyList<DimensionGroupDescriptor> groups = collection.ResolveGroups(selected);
            for (int i = 0; i < groups.Count; i++)
            {
                DimensionGroupDescriptor group = groups[i];
                VisualElement card = DimensionsApiControls.Group(group.Title, group.Hint);
                VisualElement body = DimensionsApiControls.BodyOf(card);
                for (int f = 0; f < group.Fields.Count; f++)
                {
                    DimensionFieldDescriptor field = group.Fields[f];
                    System.Func<DimensionFieldChooserContext, VisualElement> chooser =
                        field.Chooser;
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        field.Path,
                        field.Label,
                        field.Tooltip,
                        chooser == null
                            ? (System.Func<SerializedProperty, VisualElement>)null
                            : p => chooser(new DimensionFieldChooserContext(p, template))));
                }

                detailHost.Add(card);
            }

            detailHost.Add(BuildEverythingElse(serialized, collection));
        }

        /// <summary>
        /// Every serialized value the cards above did not name, in one collapsed card.
        /// </summary>
        /// <remarks>
        /// This is a promise, not a convenience: a value that exists on the asset must stay
        /// reachable even when nobody has written a nicely worded row for it yet. Without it, a
        /// tidier page would quietly become a less capable one.
        /// </remarks>
        private VisualElement BuildEverythingElse(
            SerializedObject serialized,
            DimensionCollectionDescriptor collection)
        {
            HashSet<string> named = new HashSet<string>();
            IReadOnlyList<DimensionGroupDescriptor> namedGroups = collection.ResolveGroups(serialized.targetObject);
            for (int i = 0; i < namedGroups.Count; i++)
            {
                for (int f = 0; f < namedGroups[i].Fields.Count; f++)
                {
                    named.Add(namedGroups[i].Fields[f].Path);
                }
            }

            Foldout foldout = new Foldout { text = "Everything else", value = false };
            foldout.AddToClassList("dim-everything-else");

            int shown = 0;
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                string path = iterator.propertyPath;
                if (path == "m_Script" || named.Contains(path) || path.Contains("."))
                {
                    continue;
                }

                PropertyField field =
                    new PropertyField(iterator.Copy());
                field.AddToClassList("dim-raw-field");
                foldout.Add(field);
                shown++;
            }

            foldout.text = shown == 0
                ? "Everything else"
                : "Everything else (" + shown + ")";
            foldout.style.display = shown == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            // PropertyField resolves its property through the bound hierarchy above it.
            foldout.Bind(serialized);
            return foldout;
        }

        private void DeferredRefresh()
        {
            if (root == null)
            {
                return;
            }

            root.schedule.Execute(() => Refresh(template));
        }
    }
}
