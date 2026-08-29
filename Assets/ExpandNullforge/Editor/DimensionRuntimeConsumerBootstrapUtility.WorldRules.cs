using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the half of a mod's world rules that only exists once the game is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO OF THE FOUR RULE BLOCKS ARE NOT FILES. Fishing and talents reach the game as settings
    /// files, written straight into the mod's Conf folder and read before anything of ours runs.
    /// Upgrade prices and the player's numbers have no file: the game reads both off objects it
    /// holds in memory, once, as a world loads. So they are carried as instructions in the
    /// generated bootstrap and applied at that one moment.
    /// </para>
    /// <para>
    /// PRICES CARRY NAMES, NOT NUMBERS. An item this mod adds has no number until the mod is
    /// loaded, which is after the editor has finished. Emitting the name and answering it at load
    /// is what lets a mod price an upgrade in its own bars — the same law the creature and loot
    /// emitters already follow.
    /// </para>
    /// <para>
    /// THE DRIFT CURVE IS EMITTED AS TWO ROWS OF NUMBERS. Generated code is C# source; a curve
    /// object cannot be written into it, but the times and the amounts it is made of can, and the
    /// runtime rebuilds the curve from them.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>Writes the upgrade prices and player overrides every rule set asks for.</summary>
        internal static void AppendWorldRulesRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {

            DimensionGameSetupAsset[] setups = template == null ? null : template.GlobalGameSetups;
            if (setups == null)
            {
                return;
            }

            for (int i = 0; i < setups.Length; i++)
            {
                DimensionGameSetupAsset setup = setups[i];
                if (setup == null || !setup.Enabled)
                {
                    continue;
                }

                AppendUpgradeCosts(builder, setup, modName);
                AppendFishFights(builder, setup, template, modName);
                AppendPlayerOverrides(builder, setup);
            }
        }

        /// <summary>
        /// Writes the fights belonging to fish this mod adds — the half of fishing that cannot be
        /// a settings file.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE SPLIT IS INVISIBLE TO THE AUTHOR AND DELIBERATE HERE. One list of turns in the
        /// editor becomes two things: a fight for one of Core Keeper's fish is a settings file the
        /// game reads before any code of ours runs, and a fight for one of this mod's own fish is a
        /// registration applied while the world loads. The reason is the settings file's own shape
        /// — it names the fish by object NUMBER, and this mod's objects have no number until the
        /// mod is loaded, which is after every settings file has been read.
        /// </para>
        /// <para>
        /// So the split is decided by exactly the question the file can answer: does Core Keeper's
        /// own object list have this name? The generator writes the ones it does and passes over
        /// the rest; this writes the rest. Both ask through the same
        /// <c>DimensionWorldRulesGenerator.IsOneOfTheGamesOwnFish</c>, so neither a fish written
        /// twice nor a fish written nowhere is possible.
        /// </para>
        /// </remarks>
        private static void AppendFishFights(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionFishingTemplate fishing = setup.Fishing;
            if (!fishing.ChangesWhatFishingCatches)
            {
                return;
            }

            System.Collections.Generic.Dictionary<
                string, System.Collections.Generic.List<DimensionFishFightTurn>> byFish;
            System.Collections.Generic.List<string> order =
                DimensionWorldRulesGenerator.GroupFishFights(fishing.FishFights, out byFish);

            for (int i = 0; i < order.Count; i++)
            {
                string fishName = order[i];
                ObjectID unused;
                if (DimensionWorldRulesGenerator.IsOneOfTheGamesOwnFish(fishName, out unused))
                {
                    continue;
                }

                System.Collections.Generic.List<DimensionFishFightTurn> forFish = byFish[fishName];
                bool anyPull;
                bool anyRest;
                DimensionWorldRulesGenerator.MeasureFishFight(forFish, out anyPull, out anyRest);
                if (!anyPull || !anyRest)
                {
                    Debug.LogWarning(
                        "[Dimensions API] " +
                        DimensionWorldRulesGenerator.DescribeUnresolvableFight(
                            setup.DisplayName, fishName, anyPull));
                    continue;
                }

                if (!TemplateHasAnItemNamed(template, fishName))
                {
                    // Not a refusal: a fish can be brought in by something this check does not
                    // know how to read, and refusing would take away a working fight. The name is
                    // answered again while the world loads, and the runtime says so by name if
                    // nothing answers it there either.
                    Debug.LogWarning(
                        "[Dimensions API] '" + setup.DisplayName + "' gives '" + fishName +
                        "' a fight, but that is neither one of the game's fish nor an item this " +
                        "mod makes. If it is a typo the fight will not reach the game — check the " +
                        "spelling against the item's own id.");
                }

                string qualified = QualifyWorldRuleReference(modName, fishName);

                StringBuilder pulls = new StringBuilder();
                StringBuilder seconds = new StringBuilder();
                for (int t = 0; t < forFish.Count; t++)
                {
                    if (t > 0)
                    {
                        pulls.Append(", ");
                        seconds.Append(", ");
                    }

                    pulls.Append(forFish[t].Pulls ? "true" : "false");
                    seconds.Append(
                        forFish[t].Seconds.ToString("R", CultureInfo.InvariantCulture)).Append('f');
                }

                builder.Append("    ExpandNullforge.WorldRules.DimensionFishFightRegistry.Register(");
                builder.Append(ToCSharpString(qualified));
                builder.Append(", new bool[] { ").Append(pulls);
                builder.Append(" }, new float[] { ").Append(seconds).AppendLine(" });");
            }
        }

        /// <summary>
        /// True when this mod authors an item by that id, so a fight aimed at a name nobody makes
        /// can be pointed out while the author is still in the editor.
        /// </summary>
        /// <remarks>
        /// Items and dishes are looked at because those are what a catchable thing is made as. The
        /// answer is only ever used to warn, never to refuse, so a fish arriving some other way
        /// costs a line in the console and nothing else.
        /// </remarks>
        private static bool TemplateHasAnItemNamed(DimensionTemplateAsset template, string fishName)
        {
            if (template == null || string.IsNullOrEmpty(fishName))
            {
                return false;
            }

            string wanted = DimensionObjectNamespace.LocalIdOf(fishName);

            DimensionItemAsset[] items = template.GlobalItems;
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] != null && items[i].Enabled &&
                        string.Equals(DimensionObjectNamespace.LocalIdOf(items[i].ItemId), wanted,
                            System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            DimensionDishAsset[] dishes = template.GlobalDishes;
            if (dishes != null)
            {
                for (int i = 0; i < dishes.Length; i++)
                {
                    if (dishes[i] == null || !dishes[i].Enabled)
                    {
                        continue;
                    }

                    if (NamesMatch(dishes[i].DishId, wanted) ||
                        NamesMatch(dishes[i].RareItemId, wanted) ||
                        NamesMatch(dishes[i].EpicItemId, wanted))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool NamesMatch(string candidate, string wanted)
        {
            return !string.IsNullOrEmpty(candidate) &&
                string.Equals(DimensionObjectNamespace.LocalIdOf(candidate), wanted,
                    System.StringComparison.Ordinal);
        }

        private static void AppendUpgradeCosts(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            string modName)
        {
            DimensionUpgradeCostTemplate upgrading = setup.Upgrading;
            if (!upgrading.ChangesWhatUpgradingCosts)
            {
                return;
            }

            if (upgrading.NothingWasPriced)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' changes what upgrading costs " +
                    "but prices no level, so every level keeps the game's own price. Add a row or " +
                    "switch the block off.");
                return;
            }

            DimensionUpgradeCost[] costs = upgrading.Costs;
            for (int i = 0; i < costs.Length; i++)
            {
                DimensionUpgradeCost cost = costs[i];
                if (string.IsNullOrEmpty(cost.ItemId))
                {
                    continue;
                }

                if (cost.Amount == 0)
                {
                    Debug.LogWarning(
                        "[Dimensions API] '" + setup.DisplayName + "' prices level " +
                        cost.Level.ToString(CultureInfo.InvariantCulture) + " at zero '" +
                        cost.ItemId + "', which reads as free rather than as unpriced. Set an " +
                        "amount above zero, or take the row out to leave the level alone.");
                }

                // A price may be paid in one of this mod's own items, and those are namespaced the
                // way every other reference to one is. Vanilla names are left untouched, because
                // qualifying one would point the price at an item that does not exist.
                string itemName = QualifyWorldRuleReference(modName, cost.ItemId);

                builder.Append("    ExpandNullforge.WorldRules.DimensionUpgradeCostRegistry.Register(");
                builder.Append(cost.Level.ToString(CultureInfo.InvariantCulture)).Append(", ");
                builder.Append(ToCSharpString(itemName)).Append(", ");
                builder.Append(cost.Amount.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
            }
        }

        private static void AppendPlayerOverrides(
            StringBuilder builder,
            DimensionGameSetupAsset setup)
        {
            DimensionPlayerTemplate player = setup.Player;
            if (!player.OverridesTheGamesPlayer)
            {
                return;
            }

            CultureInfo invariant = CultureInfo.InvariantCulture;

            builder.Append("    ExpandNullforge.WorldRules.DimensionPlayerOverrideRegistry")
                .Append(".RegisterTurningDelay(")
                .Append(player.TurningCatchesUpAfter.ToString("R", invariant))
                .AppendLine("f);");

            AnimationCurve drift = player.VehicleDrift;
            if (player.DriftCurveIsEmpty)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' overrides the player but its " +
                    "vehicle drift curve has no points, which would stop vehicles drifting rather " +
                    "than change how they drift. The game's own curve was kept — add at least two " +
                    "points to the curve.");
            }
            else if (drift != null && drift.length > 0)
            {
                StringBuilder times = new StringBuilder();
                StringBuilder amounts = new StringBuilder();
                for (int k = 0; k < drift.length; k++)
                {
                    if (k > 0)
                    {
                        times.Append(", ");
                        amounts.Append(", ");
                    }

                    times.Append(drift[k].time.ToString("R", invariant)).Append('f');
                    amounts.Append(drift[k].value.ToString("R", invariant)).Append('f');
                }

                builder.Append("    ExpandNullforge.WorldRules.DimensionPlayerOverrideRegistry")
                    .Append(".RegisterVehicleDrift(new float[] { ").Append(times)
                    .Append(" }, new float[] { ").Append(amounts).AppendLine(" });");
            }

            Vector3 aim = player.AimSitsAt;
            builder.Append("    ExpandNullforge.WorldRules.DimensionPlayerOverrideRegistry")
                .Append(".RegisterAimOffset(")
                .Append(aim.x.ToString("R", invariant)).Append("f, ")
                .Append(aim.y.ToString("R", invariant)).Append("f, ")
                .Append(aim.z.ToString("R", invariant)).AppendLine("f);");
        }

        /// <summary>
        /// The qualified name for a reference to one of this mod's items, or the name untouched
        /// when it is one of Core Keeper's.
        /// </summary>
        /// <remarks>
        /// A price is a reference, not a definition, so the same rule applies as everywhere else:
        /// a vanilla name must stay bare, because qualifying it points at an item nothing owns.
        /// </remarks>
        private static string QualifyWorldRuleReference(string modName, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(modName))
            {
                return itemId ?? string.Empty;
            }

            if (DimensionObjectBinder.Vanilla(itemId) != ObjectID.None)
            {
                return itemId;
            }

            return DimensionObjectNamespace.Qualify(modName, itemId);
        }
    }
}
