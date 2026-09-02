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

            // ---- the last card: its name, and what points at it ----
            // Below everything else because it is where a creator ends up, not where they start:
            // "what uses this" is the question asked once the thing is built, and renaming or
            // deleting it is the rarest thing done on the page. One insertion here covers every
            // collection on every stage, because there is one page.
            VisualElement identity = DimensionIdentityCard.Build(
                template,
                selected,
                NamesTheDisplayName(groups),
                DeferredRefresh);
            if (identity != null)
            {
                detailHost.Add(identity);
            }
        }

        /// <summary>
        /// Whether a card above already offers the name a player reads.
        /// </summary>
        /// <remarks>
        /// Nearly every collection's first card does. The identity card at the foot then says where
        /// that control is instead of drawing a second one for the same value — the same rule
        /// <see cref="BuildEverythingElse"/> follows for a curated block's parent.
        /// </remarks>
        private static bool NamesTheDisplayName(IReadOnlyList<DimensionGroupDescriptor> groups)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                for (int f = 0; f < groups[i].Fields.Count; f++)
                {
                    if (groups[i].Fields[f].Path == "displayName")
                    {
                        return true;
                    }
                }
            }

            return false;
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

            // A card naming "cooking.role" curates a value INSIDE the top-level "cooking" block.
            // The block itself is then not in `named`, so it used to be drawn whole down here as
            // well, and the creator had the same value under two editors on one page with neither
            // aware of the other.
            HashSet<string> curatedParents = DimensionCuratedPaths.ParentsOf(named);

            // AND THE IDS ARE NOT DRAWN HERE EITHER, for the same reason and a worse one. No id is
            // a curated card field, so every one of them landed in this fold as a live-bound
            // PropertyField — one keystroke, one committed rename — directly above the card that
            // exists to stop exactly that. Worse, it is a second editor over the same property that
            // knows nothing about the sweep, so every refusal the rename makes could be walked
            // around by typing in the fold instead. The row stays, saying where the control is.
            HashSet<string> identities = new HashSet<string>();
            List<DimensionIdentityField> renameable =
                DimensionIdentityCatalog.Of(serialized.targetObject);
            for (int i = 0; i < renameable.Count; i++)
            {
                identities.Add(renameable[i].Property);
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

                if (identities.Contains(path))
                {
                    Label sentAway = new Label(
                        iterator.displayName +
                        " — edited at the foot of this page, under \"Its name, and what points " +
                        "at it\", where changing it can say what else it would change.");
                    sentAway.AddToClassList("dim-note");
                    foldout.Add(sentAway);
                    shown++;
                    continue;
                }

                // A block with a curated value inside it is opened rather than skipped. Skipping
                // it would close the double editor and lose the rest of the block with it: the
                // cooking block holds nineteen values and ten of them have cards, so nine — the
                // four colours a dish borrows, the golden dish, and the four cross-links — would
                // have had no editor anywhere. What "Everything else" promises is that nothing on
                // the asset is unreachable.
                if (curatedParents.Contains(path))
                {
                    shown += AddUncuratedChildren(
                        iterator.Copy(),
                        named,
                        curatedParents,
                        foldout,
                        iterator.displayName + ": ");
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

        /// <summary>
        /// The values inside one block that no card upstairs already edits.
        /// </summary>
        /// <remarks>
        /// Recursive because a curated path may go deeper than one dot, and a block that holds
        /// another curated block has to be opened the same way rather than drawn whole. The label
        /// carries the block's name down with it, so a value that reads as "Brightest" on its own
        /// arrives as "Cooking: Brightest" and can be told from the four other fields in this fold
        /// with a colour in them.
        /// </remarks>
        private static int AddUncuratedChildren(
            SerializedProperty parent,
            HashSet<string> named,
            HashSet<string> curatedParents,
            VisualElement into,
            string labelPrefix)
        {
            int shown = 0;
            SerializedProperty child = parent.Copy();
            SerializedProperty end = parent.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                string path = child.propertyPath;
                if (named.Contains(path))
                {
                    continue;
                }

                if (curatedParents.Contains(path))
                {
                    shown += AddUncuratedChildren(
                        child.Copy(),
                        named,
                        curatedParents,
                        into,
                        labelPrefix + child.displayName + ": ");
                    continue;
                }

                PropertyField field =
                    new PropertyField(child.Copy(), labelPrefix + child.displayName);
                field.AddToClassList("dim-raw-field");
                into.Add(field);
                shown++;
            }

            return shown;
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
