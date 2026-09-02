using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What each talent gives, for the twelve skills the game has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWELVE SKILLS, AND THAT NUMBER IS THE GAME'S. Mining, Running, Melee, Vitality, Crafting,
    /// Range, Gardening, Fishing, Cooking, Magic, Summoning and Explosives. A thirteenth cannot be
    /// added here: the game lays out exactly twelve talent trees when a world loads, so a skill name
    /// it does not know is reported and left out rather than written into a table with no room for
    /// it.
    /// </para>
    /// <para>
    /// TALENTS ARE LISTED IN THE ORDER THE TALENT WINDOW SHOWS THEM. The first row for a skill is
    /// that skill's first talent, the second row its second, and so on. So changing the fourth
    /// talent means listing the first three as well — and listing them means typing their names and
    /// numbers as the game has them, because a row with an empty name clears the name the game gave
    /// that talent.
    /// </para>
    /// <para>
    /// THE CONDITION MAY BE ONE OF THIS MOD'S OWN. Talent conditions are answered the same way
    /// everything else in the framework answers them: Core Keeper's own names first, then this
    /// mod's. A talent that grants a stat effect this mod invented works, which is the one thing
    /// here the game could never do on its own.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionTalentsTemplate
    {
        [Header("Does this change what talents give?")]
        [Tooltip("Turn on to write your own talent values. Off leaves every talent as the game has it.")]
        [SerializeField] private bool changesWhatTalentsGive;

        [Tooltip("Each row is one talent. Rows sharing a skill are that skill's talents, in the order shown here.")]
        [SerializeField] private DimensionTalent[] talents = new DimensionTalent[0];

        public bool ChangesWhatTalentsGive { get { return changesWhatTalentsGive; } }

        public DimensionTalent[] Talents
        {
            get { return talents ?? new DimensionTalent[0]; }
        }

        /// <summary>Switched on with no talents, so every talent would keep the game's own values.</summary>
        public bool NothingWasWritten
        {
            get { return changesWhatTalentsGive && Talents.Length == 0; }
        }

        /// <summary>
        /// The rows sorted into one list per skill, in the order the skills were first mentioned.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A TALENT'S POSITION IN ITS SKILL'S LIST IS ITS IDENTITY. The game merges a talent file
        /// into its own tree by position — first row over the first talent, second over the second
        /// — so which square a row lands on is decided entirely by this grouping. Three separate
        /// pieces of the framework need to agree about it: the generator that writes the file, the
        /// wording table that names the squares, and the picture pass that puts an icon on one. If
        /// any two of them count differently, a talent gets another talent's picture and nothing
        /// says so.
        /// </para>
        /// <para>
        /// So the counting is done once, here, and all three ask for it rather than repeating it.
        /// </para>
        /// </remarks>
        /// <param name="unknownSkillNames">
        /// Filled with any skill name the game does not have, once each, so a caller with a report
        /// can say so. Those rows are left out of the result.
        /// </param>
        public static System.Collections.Generic.List<DimensionTalentGroup> Group(
            DimensionTalent[] rows,
            System.Collections.Generic.List<string> unknownSkillNames)
        {
            System.Collections.Generic.List<DimensionTalentGroup> groups =
                new System.Collections.Generic.List<DimensionTalentGroup>();
            if (rows == null)
            {
                return groups;
            }

            System.Collections.Generic.List<string> order =
                new System.Collections.Generic.List<string>();
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<DimensionTalent>> bySkill =
                new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<DimensionTalent>>(
                    System.StringComparer.Ordinal);

            for (int i = 0; i < rows.Length; i++)
            {
                string skill = rows[i].Skill;
                if (string.IsNullOrEmpty(skill))
                {
                    continue;
                }

                System.Collections.Generic.List<DimensionTalent> forSkill;
                if (!bySkill.TryGetValue(skill, out forSkill))
                {
                    forSkill = new System.Collections.Generic.List<DimensionTalent>();
                    bySkill.Add(skill, forSkill);
                    order.Add(skill);
                }

                forSkill.Add(rows[i]);
            }

            for (int i = 0; i < order.Count; i++)
            {
                string skillName = order[i];
                SkillID skill;
                if (!System.Enum.TryParse(skillName, false, out skill) ||
                    (int)skill < 0 || (int)skill >= (int)SkillID.NUM_SKILLS)
                {
                    if (unknownSkillNames != null)
                    {
                        unknownSkillNames.Add(skillName);
                    }

                    continue;
                }

                groups.Add(new DimensionTalentGroup(
                    skill, skillName, bySkill[skillName].ToArray()));
            }

            return groups;
        }
    }

    /// <summary>One skill's talents, in the order the talent window lays its squares out.</summary>
    public struct DimensionTalentGroup
    {
        public DimensionTalentGroup(SkillID skill, string skillName, DimensionTalent[] talents)
        {
            Skill = skill;
            SkillName = skillName ?? string.Empty;
            Talents = talents ?? new DimensionTalent[0];
        }

        public SkillID Skill { get; }

        /// <summary>The name as it was typed, for a message that has to quote it back.</summary>
        public string SkillName { get; }

        public DimensionTalent[] Talents { get; }
    }

    /// <summary>One talent on one skill's tree.</summary>
    [Serializable]
    public struct DimensionTalent
    {
        [Tooltip("The skill this talent belongs to: Mining, Running, Melee, Vitality, Crafting, Range, Gardening, Fishing, Cooking, Magic, Summoning or Explosives.")]
        [SerializeField] private string skill;

        [Tooltip("What the talent is called in the talent window. Type the game's own name if you only mean to change the numbers.")]
        [SerializeField] private string talentName;

        [Tooltip("What a point in it grants, named as the game names it, or as one of this mod's own conditions is named.")]
        [SerializeField] private string gives;

        [Tooltip("How much of that one point is worth. Two points give twice this.")]
        [SerializeField] private int perPoint;

        [Tooltip("What a player reads on the square. Leave empty to show the name above. Ignored when the name above is one of the game's own talents, because then the game's own wording is used in every language.")]
        [SerializeField] private string shownAs;

        [Tooltip("The picture on the square. Leave empty to keep the picture the game gave that talent.")]
        [SerializeField] private Sprite icon;

        public string Skill { get { return skill ?? string.Empty; } }

        public string TalentName { get { return talentName ?? string.Empty; } }

        public string Gives { get { return gives ?? string.Empty; } }

        public int PerPoint { get { return perPoint; } }

        /// <summary>What a player reads, which falls back to the name when nothing was written.</summary>
        public string ShownAs
        {
            get { return string.IsNullOrEmpty(shownAs) ? TalentName : shownAs; }
        }

        public Sprite Icon { get { return icon; } }

        /// <summary>
        /// It reuses one of the game's own talent names, so it means "change only the numbers".
        /// </summary>
        /// <remarks>
        /// The name is the key the talent window looks its wording up under, so writing one of the
        /// game's own keeps that wording in every language it ships — and writing a line of our own
        /// over it would replace twelve translations with one. Anything else is a talent of the
        /// mod's own and needs a line written for it.
        /// </remarks>
        public bool KeepsTheGamesOwnWording
        {
            get { return DimensionVanillaTalentNames.IsAGameTalent(TalentName); }
        }
    }
}
