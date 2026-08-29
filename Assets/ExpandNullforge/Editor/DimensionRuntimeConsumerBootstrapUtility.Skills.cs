using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits what a mod's own creatures are worth killing, in skill experience.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS CANNOT BE BAKED ONTO THE PREFAB. There is no component on a creature that says
    /// "worth twenty-five Melee". Core Keeper decides what a kill is worth inside the compiled job
    /// that handles the kill, so the only way for a mod's creature to be worth anything is for the
    /// framework to award it — and awarding it means knowing, at runtime, which object died. An
    /// object's number does not exist until the mod is loaded, so the name is written here and the
    /// number is looked up in the running world.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>Writes one registration per creature worth experience.</summary>
        internal static void AppendSkillExperienceRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionMobAsset[] mobs = template == null ? null : template.GlobalMobs;
            if (mobs == null)
            {
                return;
            }

            for (int i = 0; i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null || !mob.Enabled || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                DimensionSkillRewardTemplate reward = mob.SkillReward;
                if (reward == null || !reward.GivesExperienceWhenKilled)
                {
                    continue;
                }

                if (reward.GivesNothing)
                {
                    Debug.LogWarning(
                        "[Dimensions API] '" + mob.DisplayName + "' is set to give skill " +
                        "experience when it is killed, but the amount is zero, so it gives none. " +
                        "Type an amount or switch it off.");
                    continue;
                }

                SkillID skill;
                if (!reward.TryResolveSkill(out skill))
                {
                    Debug.LogWarning(
                        "[Dimensions API] '" + mob.DisplayName + "' gives experience in '" +
                        reward.Skill + "', which is not one of the game's twelve skills, so it " +
                        "gives none. Name one of Mining, Running, Melee, Vitality, Crafting, " +
                        "Range, Gardening, Fishing, Cooking, Magic, Summoning or Explosives.");
                    continue;
                }

                string creatureName = DimensionObjectNamespace.Qualify(modName, mob.MobId);
                builder.Append("    ExpandNullforge.Skills.DimensionSkillXpRegistry.Register(");
                builder.Append(ToCSharpString(creatureName));
                builder.Append(", SkillID.").Append(skill).Append(", ");
                builder.Append(reward.HowMuch.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine(");");
            }
        }
    }
}
