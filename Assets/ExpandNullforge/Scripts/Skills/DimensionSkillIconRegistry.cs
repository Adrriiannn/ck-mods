using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.Skills
{
    /// <summary>
    /// The pictures a mod put on the game's own twelve skills.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A PICTURE CANNOT TRAVEL AS A NUMBER, so this is filled at <c>ModObjectLoaded</c> from the
    /// mod's own shipped template, exactly as the talent icons are and for the same reason: a
    /// <c>Sprite</c> only exists once the bundle is loaded.
    /// </para>
    /// <para>
    /// TWELVE, NOT THIRTEEN. Nothing here adds a skill. <c>SkillIconsTable</c> answers by
    /// <c>SkillID</c> and the game has twelve of them; a mod's picture replaces one of those
    /// twelve.
    /// </para>
    /// </remarks>
    public static class DimensionSkillIconRegistry
    {
        private sealed class Pair
        {
            public Sprite Picture;
            public Sprite PictureAtMaxLevel;
        }

        private static readonly Dictionary<int, Pair> icons = new Dictionary<int, Pair>();

        /// <summary>Whether anything at all was registered, so callers can skip the work.</summary>
        public static bool HasAny { get { return icons.Count > 0; } }

        /// <summary>How many skills have a picture of a mod's own.</summary>
        public static int Count { get { return icons.Count; } }

        /// <summary>
        /// Puts pictures on one skill. Registering the same skill twice replaces the earlier claim.
        /// </summary>
        /// <remarks>
        /// Either picture may be null, which means "keep the game's" for that one. A call with both
        /// null claims nothing, so a row left empty in the editor does not shadow the game's icon
        /// with a blank.
        /// </remarks>
        public static void Register(SkillID skill, Sprite picture, Sprite pictureAtMaxLevel)
        {
            if (picture == null && pictureAtMaxLevel == null)
            {
                return;
            }

            icons[(int)skill] = new Pair
            {
                Picture = picture,
                PictureAtMaxLevel = pictureAtMaxLevel
            };
        }

        /// <summary>Forgets everything, for a mod being unloaded or an editor reload.</summary>
        public static void Clear()
        {
            icons.Clear();
        }

        /// <summary>
        /// What one skill should be drawn with, given what the game was going to draw it with.
        /// </summary>
        /// <remarks>
        /// The game's answer is passed in rather than looked up, so a half claim — a picture for
        /// the ordinary state and none for the gold one — keeps the game's other half instead of
        /// blanking it.
        /// </remarks>
        /// <returns>False when this mod says nothing about that skill.</returns>
        public static bool TryAnswer(SkillID skill, SkillIcon theirs, out SkillIcon ours)
        {
            ours = null;

            Pair pair;
            if (!icons.TryGetValue((int)skill, out pair))
            {
                return false;
            }

            ours = new SkillIcon
            {
                skillID = skill,
                icon = pair.Picture != null
                    ? pair.Picture
                    : (theirs != null ? theirs.icon : null),
                goldIcon = pair.PictureAtMaxLevel != null
                    ? pair.PictureAtMaxLevel
                    : (theirs != null ? theirs.goldIcon : null)
            };
            return true;
        }

        /// <summary>
        /// Reads every skill picture out of the mod's own shipped template.
        /// </summary>
        /// <remarks>
        /// Called from the mod's <c>ModObjectLoaded</c>, the same moment and for the same reason as
        /// the talent icons. A skill name the game does not have is skipped: there are twelve, and
        /// a thirteenth name is a spelling mistake rather than a new skill.
        /// </remarks>
        public static void AttachFrom(DimensionTemplateAsset template, System.Action<string> report)
        {
            if (template == null)
            {
                return;
            }

            DimensionGameSetupAsset[] setups = template.GlobalGameSetups;
            for (int s = 0; setups != null && s < setups.Length; s++)
            {
                DimensionGameSetupAsset setup = setups[s];
                if (setup == null || !setup.Enabled || !setup.SkillPictures.ChangesTheSkillPictures)
                {
                    continue;
                }

                DimensionSkillIcon[] rows = setup.SkillPictures.Skills;
                for (int i = 0; i < rows.Length; i++)
                {
                    SkillID skill;
                    if (!System.Enum.TryParse(rows[i].SkillName, false, out skill))
                    {
                        if (report != null)
                        {
                            report(
                                "A picture was set for the skill '" + rows[i].SkillName +
                                "', which is not one of the game's twelve. The names are Mining, " +
                                "Running, Melee, Vitality, Crafting, Range, Gardening, Fishing, " +
                                "Cooking, Magic, Summoning and Explosives.");
                        }

                        continue;
                    }

                    Register(skill, rows[i].Picture, rows[i].PictureAtMaxLevel);
                }
            }
        }
    }

    /// <summary>
    /// Answers what a skill is drawn with, where the game asks the question.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SkillIconsTable</c> is not loaded by name anywhere: it reaches the game as an inspector
    /// reference on <c>SkillUIElement</c>, which is its only reader. So there is nothing to
    /// <c>Resources.Load</c>, and the answer is supplied where it is used — the same law the talent
    /// icon patch follows. The game's own table is never written to, and a skill this mod did not
    /// claim keeps exactly the picture the game gave it.
    /// </para>
    /// <para>
    /// COST WHEN UNUSED IS ONE BOOLEAN, and only while the skill window is drawing.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(SkillIconsTable), "GetIcon")]
    internal static class DimensionSkillIconPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        [HarmonyPostfix]
        private static void After(SkillID conditionID, ref SkillIcon __result)
        {
            Fired++;

            if (!DimensionSkillIconRegistry.HasAny)
            {
                return;
            }

            SkillIcon ours;
            if (DimensionSkillIconRegistry.TryAnswer(conditionID, __result, out ours))
            {
                __result = ours;
            }
        }
    }
}
