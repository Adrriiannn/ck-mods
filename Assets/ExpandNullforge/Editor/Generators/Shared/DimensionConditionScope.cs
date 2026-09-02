using System;
using ExpandNullforge.Authoring;
using ExpandNullforge.Conditions;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Makes a mod's own conditions answerable by name for the length of a generate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A CUSTOM CONDITION'S NUMBER DEPENDS ON THE WHOLE SET, because the numbers are handed out in
    /// name order. In a running game the set is whatever loaded; at generate time it is whatever the
    /// mod contains. Those have to agree, or a creature generated with condition 358 would be given
    /// something else entirely once the game loads.
    /// </para>
    /// <para>
    /// They agree because both go through the same registry with the same rule. This claims the
    /// mod's conditions before a generate and puts the registry back afterwards, so an editor
    /// session does not accumulate the conditions of every mod it has ever opened.
    /// </para>
    /// </remarks>
    internal sealed class DimensionConditionScope : IDisposable
    {
        /// <summary>
        /// Claims every condition in the set, so names resolve for as long as this is held.
        /// </summary>
        public DimensionConditionScope(DimensionConditionAsset[] conditions)
        {
            DimensionConditionRegistry.Clear();
            if (conditions == null)
            {
                return;
            }

            for (int i = 0; i < conditions.Length; i++)
            {
                DimensionConditionAsset asset = conditions[i];
                if (asset == null || !asset.Enabled || string.IsNullOrEmpty(asset.ConditionName))
                {
                    continue;
                }

                // The same construction site the running game uses, so a field added to a condition
                // cannot reach one and not the other. See DimensionCustomCondition.From.
                DimensionConditionRegistry.Claim(DimensionCustomCondition.From(asset));
            }
        }

        /// <summary>How many were claimed, for the generation report to mention.</summary>
        public int Count
        {
            get { return DimensionConditionRegistry.Count; }
        }

        public void Dispose()
        {
            DimensionConditionRegistry.Clear();
        }
    }
}
