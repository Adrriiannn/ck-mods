using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Your own pictures on the game's twelve skills.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWELVE SKILLS, AND THAT IS THE WHOLE OF IT. <c>SkillIconsTable</c> holds one row per
    /// <c>SkillID</c> and the game ships twelve, numbered 0 to 11 — measured on
    /// <c>Resources/SkillIconsTable.asset</c>, which has exactly twelve rows. This changes the
    /// picture on one of those twelve. It does not add a thirteenth skill and cannot: the skill bar
    /// is a fixed twelve-wide buffer elsewhere in the game.
    /// </para>
    /// <para>
    /// TWO PICTURES PER SKILL, BECAUSE THE GAME DRAWS TWO. A skill at its highest level is drawn
    /// with a gold version of its icon and a gold background
    /// (<c>ck-db\Pug.Other\SkillUIElement.cs:22-33</c>). Giving only one of the two leaves the
    /// other as the game's, which is the right answer for a mod that only wants to restyle the
    /// ordinary state.
    /// </para>
    /// <para>
    /// IT IS THE WHOLE GAME'S SKILL WINDOW, not one dimension's. There is one table and it is read
    /// wherever a skill is drawn.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSkillIconTemplate
    {
        [Header("Does this change the pictures on the game's skills?")]
        [Tooltip("Turn on to put your own pictures on the skill window. Off leaves all twelve as the game draws them.")]
        [SerializeField] private bool changesTheSkillPictures;

        [Tooltip("One row per skill you are repainting. A skill nobody names keeps the game's own picture.")]
        [SerializeField] private DimensionSkillIcon[] skills = new DimensionSkillIcon[0];

        public bool ChangesTheSkillPictures { get { return changesTheSkillPictures; } }

        public DimensionSkillIcon[] Skills { get { return skills ?? new DimensionSkillIcon[0]; } }

        /// <summary>Switched on with no rows, so every skill would keep the game's own picture.</summary>
        public bool NothingWasRepainted
        {
            get { return changesTheSkillPictures && Skills.Length == 0; }
        }
    }

    /// <summary>One skill's pictures.</summary>
    [Serializable]
    public struct DimensionSkillIcon
    {
        [Tooltip("Which skill, named as the game names it: Mining, Running, Melee, Vitality, Crafting, Range, Gardening, Fishing, Cooking, Magic, Summoning, Explosives.")]
        [SerializeField] private string skillName;

        [Tooltip("The picture on the skill square. Leave empty to keep the game's own.")]
        [SerializeField] private Sprite picture;

        [Tooltip("The picture once the skill is at its highest level. Leave empty to keep the game's own gold picture.")]
        [SerializeField] private Sprite pictureAtMaxLevel;

        public string SkillName { get { return skillName ?? string.Empty; } }

        public Sprite Picture { get { return picture; } }

        public Sprite PictureAtMaxLevel { get { return pictureAtMaxLevel; } }

        /// <summary>A row that names a skill and gives it neither picture.</summary>
        public bool GivesNoPicture
        {
            get { return picture == null && pictureAtMaxLevel == null; }
        }
    }
}
