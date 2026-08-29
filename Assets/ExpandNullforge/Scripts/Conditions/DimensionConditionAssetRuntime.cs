using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using UnityEngine;

namespace ExpandNullforge.Conditions
{
    /// <summary>
    /// Turns an authored condition into a claim on one of the game's condition numbers.
    /// </summary>
    /// <remarks>
    /// Called as the mod's assets load, before Core Keeper builds its condition table — which is the
    /// only window that exists. A condition claimed after the table is built has a number nothing
    /// will ever look up, so the claim is made the moment the asset appears rather than the moment
    /// something wants to use it.
    /// </remarks>
    public static class DimensionConditionAssetRuntime
    {
        /// <summary>Claims a number for one authored condition.</summary>
        /// <returns>False when the asset is switched off or unusable.</returns>
        public static bool Register(DimensionConditionAsset asset)
        {
            if (asset == null || !asset.Enabled)
            {
                return false;
            }

            if (string.IsNullOrEmpty(asset.ConditionName))
            {
                DimensionLog.Problem(DimensionLogChannels.Condition, null, 
                    "A condition asset has no name, so there is nothing to " +
                    "address it by. It was skipped.");
                return false;
            }

            if (asset.DoesNothing)
            {
                DimensionLog.Problem(DimensionLogChannels.Condition, null, 
                    "Condition '" + asset.DisplayName + "' applies no effect at " +
                    "all, so it will sit on whoever has it doing nothing.");
            }

            if (asset.NeverShowsInTheBuffRow)
            {
                DimensionFrameworkLog.Verbose(
                    "Condition '" + asset.DisplayName + "' has no picture, so it " +
                    "will work but never appear in the row of buffs. Give it one if it is meant to " +
                    "be seen there.");
            }

            // One construction site. See DimensionCustomCondition.From.
            DimensionCustomCondition condition = DimensionCustomCondition.From(asset);

            if (!DimensionConditionRegistry.Claim(condition))
            {
                return false;
            }

            DimensionFrameworkLog.Verbose(
                "Condition '" + asset.DisplayName + "' claimed number " +
                DimensionConditionRegistry.NumberFor(asset.ConditionName) + ", applying " +
                asset.Effect + ".");
            return true;
        }
    }
}
