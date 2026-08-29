using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>One editable value, in the words a player would use for it.</summary>
    internal sealed class DimensionFieldDescriptor
    {
        internal DimensionFieldDescriptor(string path, string label, string tooltip)
        {
            Path = path;
            Label = label;
            Tooltip = tooltip;
        }

        internal string Path { get; }

        internal string Label { get; }

        internal string Tooltip { get; }

        /// <summary>
        /// Builds the control for this row itself, when a text box is the wrong thing to offer.
        /// Null for the ordinary case, and then the control comes from the property's type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// SOME FIELDS ARE A CHOICE OUT OF A LIST THE GAME HOLDS — one of 136 stat effects, one of
        /// 1,413 sounds, one of 426 puffs, one of the twelve skills. A bound text box is a correct
        /// control for none of them: the legal values are not written anywhere a creator can read,
        /// and a wrong one is silent. This is the one hook that lets a row say what it really is.
        /// </para>
        /// <para>
        /// The chooser is handed the whole context rather than only the property because two of
        /// them list this dimension's own contents — the effects it invented, the skills it adds —
        /// and those live on the template, not on the asset being edited.
        /// </para>
        /// </remarks>
        internal System.Func<DimensionFieldChooserContext, UnityEngine.UIElements.VisualElement> Chooser
        {
            get;
            private set;
        }

        /// <summary>Gives this row a chooser, and hands the row back so a catalog can chain it.</summary>
        internal DimensionFieldDescriptor With(
            System.Func<DimensionFieldChooserContext, UnityEngine.UIElements.VisualElement> chooser)
        {
            Chooser = chooser;
            return this;
        }
    }

    /// <summary>What a chooser is given: the value it edits, and the dimension it belongs to.</summary>
    internal sealed class DimensionFieldChooserContext
    {
        internal DimensionFieldChooserContext(
            UnityEditor.SerializedProperty property,
            DimensionTemplateAsset template)
        {
            Property = property;
            Template = template;
        }

        internal UnityEditor.SerializedProperty Property { get; }

        /// <summary>The dimension being edited. May be null when nothing is open.</summary>
        internal DimensionTemplateAsset Template { get; }
    }

    /// <summary>A card of fields that together answer one question about a thing.</summary>
    internal sealed class DimensionGroupDescriptor
    {
        internal DimensionGroupDescriptor(
            string title,
            string hint,
            params DimensionFieldDescriptor[] fields)
        {
            Title = title;
            Hint = hint;
            Fields = fields ?? new DimensionFieldDescriptor[0];
        }

        internal string Title { get; }

        internal string Hint { get; }

        internal IReadOnlyList<DimensionFieldDescriptor> Fields { get; }
    }

    /// <summary>
    /// One kind of thing a stage lets a creator make: where the dimension keeps them, how to make
    /// another, how to describe one in a list, and the cards its editor is built from.
    /// </summary>
    /// <remarks>
    /// Describing a content type rather than hand-writing a page for it is the only way one style
    /// reaches all twenty-odd kinds of thing this framework authors. A page built from a
    /// descriptor cannot drift from its neighbours, because there is only one page.
    /// </remarks>
    internal sealed class DimensionCollectionDescriptor
    {
        internal DimensionCollectionDescriptor(
            string key,
            string tabLabel,
            string singular,
            string emptyHeadline,
            string emptyHelp,
            System.Func<DimensionTemplateAsset, IReadOnlyList<Object>> read,
            System.Func<DimensionTemplateAsset, DimensionFrameworkAuthoringAssetActionResult> create,
            System.Func<Object, string> title,
            System.Func<Object, string> subtitle,
            params DimensionGroupDescriptor[] groups)
        {
            Key = key;
            TabLabel = tabLabel;
            Singular = singular;
            EmptyHeadline = emptyHeadline;
            EmptyHelp = emptyHelp;
            Read = read;
            Create = create;
            Title = title;
            Subtitle = subtitle;
            Groups = groups ?? new DimensionGroupDescriptor[0];
        }

        internal string Key { get; }

        internal string TabLabel { get; }

        internal string Singular { get; }

        internal string EmptyHeadline { get; }

        internal string EmptyHelp { get; }

        internal System.Func<DimensionTemplateAsset, IReadOnlyList<Object>> Read { get; }

        /// <summary>Null when this kind of thing cannot be created from its own page yet.</summary>
        internal System.Func<DimensionTemplateAsset, DimensionFrameworkAuthoringAssetActionResult> Create { get; }

        internal System.Func<Object, string> Title { get; }

        internal System.Func<Object, string> Subtitle { get; }

        internal IReadOnlyList<DimensionGroupDescriptor> Groups { get; }

        /// <summary>
        /// Resolves the cards for one specific thing, when a list mixes kinds. Null for the
        /// common case of one kind per list; then <see cref="Groups"/> serves everything.
        /// </summary>
        /// <remarks>
        /// This is what lets one list hold mobs AND bosses without gluing two pages together:
        /// the list is one list, the selection is one selection, and only the cards on the
        /// right change shape with the role of the thing selected.
        /// </remarks>
        internal System.Func<Object, IReadOnlyList<DimensionGroupDescriptor>> GroupsFor { get; set; }

        /// <summary>
        /// Extra ways to add to this list, each with its own label — "+ New Monster" beside
        /// "+ New Boss". Null for the common single-creator case.
        /// </summary>
        internal IReadOnlyList<DimensionCollectionCreateChoice> CreateChoices { get; set; }

        /// <summary>The cards for one thing: its own kind's cards, or the shared set.</summary>
        internal IReadOnlyList<DimensionGroupDescriptor> ResolveGroups(Object item)
        {
            if (GroupsFor != null && item != null)
            {
                IReadOnlyList<DimensionGroupDescriptor> resolved = GroupsFor(item);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return Groups;
        }
    }

    /// <summary>One labelled way of adding a thing to a mixed list.</summary>
    internal sealed class DimensionCollectionCreateChoice
    {
        internal DimensionCollectionCreateChoice(
            string label,
            System.Func<DimensionTemplateAsset, DimensionFrameworkAuthoringAssetActionResult> create)
        {
            Label = label;
            Create = create;
        }

        internal string Label { get; }

        internal System.Func<DimensionTemplateAsset, DimensionFrameworkAuthoringAssetActionResult> Create { get; }
    }

    /// <summary>A stage of the journey, and the kinds of thing it owns.</summary>
    internal sealed class DimensionStageDescriptor
    {
        internal DimensionStageDescriptor(
            string sectionId,
            params DimensionCollectionDescriptor[] collections)
        {
            SectionId = sectionId;
            Collections = collections ?? new DimensionCollectionDescriptor[0];
        }

        internal string SectionId { get; }

        internal IReadOnlyList<DimensionCollectionDescriptor> Collections { get; }
    }
}
