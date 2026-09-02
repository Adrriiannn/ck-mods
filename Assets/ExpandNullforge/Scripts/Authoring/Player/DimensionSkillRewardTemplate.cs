using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What killing this creature teaches a player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CORE KEEPER'S OWN MONSTERS GIVE MELEE AND RANGE EXPERIENCE WHEN THEY DIE, and until now a
    /// creature made here gave nothing at all: every place the game awards experience is inside
    /// compiled code a mod cannot reach. The framework writes the award itself instead, through the
    /// one door the game does leave open, so a monster of your own is worth killing.
    /// </para>
    /// <para>
    /// TWO THINGS THIS DOES NOT DO, both worth knowing before you set a number. It does not count
    /// towards the game's own achievements — those are only reached from the game's own way of
    /// awarding experience. And the number is flat: Core Keeper scales what its monsters are worth
    /// by how deep the area is, and that scaling lives somewhere a mod cannot read, so what you
    /// type is what a player gets wherever they kill it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSkillRewardTemplate
    {
        [Tooltip("Give a player skill experience for killing this.")]
        [SerializeField] private bool givesExperienceWhenKilled;

        [Tooltip("Which skill it teaches: Mining, Running, Melee, Vitality, Crafting, Range, Gardening, Fishing, Cooking, Magic, Summoning or Explosives.")]
        [SerializeField] private string skill = "Melee";

        [Tooltip("How much, per kill. For scale: the game's own early monsters are worth a handful and its bosses hundreds.")]
        [SerializeField] private int howMuch = 10;

        public bool GivesExperienceWhenKilled { get { return givesExperienceWhenKilled; } }

        public string Skill { get { return skill ?? string.Empty; } }

        public int HowMuch { get { return howMuch; } }

        /// <summary>Switched on and worth nothing, which is the one combination that says nothing.</summary>
        public bool GivesNothing
        {
            get { return givesExperienceWhenKilled && howMuch <= 0; }
        }

        /// <summary>The skill it teaches, or false when the name is not one of the game's twelve.</summary>
        public bool TryResolveSkill(out SkillID resolved)
        {
            if (Enum.TryParse(Skill, false, out resolved) &&
                (int)resolved >= 0 && (int)resolved < (int)SkillID.NUM_SKILLS)
            {
                return true;
            }

            resolved = SkillID.Melee;
            return false;
        }
    }
}
