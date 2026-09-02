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

            /// <summary>The answer already built for one of the game's own icons.</summary>
            /// <remarks>
            /// <c>SkillUIElement.LateUpdate</c> asks <c>GetIcon</c> once or twice per element per
            /// frame (<c>ck-db\Pug.Other\SkillUIElement.cs:11,24,29</c>), so building a fresh
            /// <c>SkillIcon</c> on every answer was a per-frame allocation per claimed skill for as
            /// long as the skill window was open. The game's answer for one skill is the same
            /// object every time — it comes out of a serialized list on <c>SkillIconsTable</c> —
            /// so the built answer is kept beside the icon it was built from and rebuilt only if
            /// that ever changes.
            /// </remarks>
            public SkillIcon Answer;

            public SkillIcon AnsweredFor;
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

            // Built once per (skill, the game's own icon) rather than once per ask — see the
            // Pair.Answer remarks. ReferenceEquals rather than ==: SkillIcon is a plain class, and
            // the point is whether this is the same object the answer was built from.
            if (pair.Answer != null && ReferenceEquals(pair.AnsweredFor, theirs))
            {
                ours = pair.Answer;
                return true;
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
            pair.Answer = ours;
            pair.AnsweredFor = theirs;
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
                    if (!TheGamesOwnSkill(rows[i].SkillName, out skill))
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

        /// <summary>The twelve skills the game has, by the names it gives them.</summary>
        /// <remarks>
        /// <c>SkillID</c> has thirteen names; the thirteenth is <c>NUM_SKILLS</c>, the count rather
        /// than a skill. The names are matched against this list rather than parsed because
        /// <c>Enum.TryParse</c> takes a number as readily as a name — a row named "12" came back as
        /// <c>NUM_SKILLS</c> and registered a picture that nothing would ever ask for, with the
        /// spelling complaint below never printed.
        /// </remarks>
        private static readonly string[] TheGamesOwnSkillNames =
        {
            "Mining", "Running", "Melee", "Vitality", "Crafting", "Range",
            "Gardening", "Fishing", "Cooking", "Magic", "Summoning", "Explosives"
        };

        /// <summary>Answers a skill name, and only one of the game's own twelve.</summary>
        /// <remarks>
        /// Reachable from the tests rather than private, for the same reason the world-event biome
        /// gate is: it is the whole of the refusal, and a refusal nothing can ask about directly is
        /// a refusal nothing checks.
        /// </remarks>
        internal static bool TheGamesOwnSkill(string skillName, out SkillID skill)
        {
            skill = default(SkillID);
            if (string.IsNullOrEmpty(skillName))
            {
                return false;
            }

            for (int i = 0; i < TheGamesOwnSkillNames.Length; i++)
            {
                if (!string.Equals(TheGamesOwnSkillNames[i], skillName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                SkillID named;
                if (System.Enum.TryParse(TheGamesOwnSkillNames[i], false, out named))
                {
                    skill = named;
                    return true;
                }

                return false;
            }

            return false;
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
    /// COST WHEN UNUSED IS ONE BOOLEAN, and only while the skill window is drawing. When it IS
    /// used the cost is a dictionary lookup and a reference test on the same path, because
    /// <c>SkillUIElement.LateUpdate</c> asks once or twice per element per frame and the answer for
    /// one skill is built once rather than per ask.
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
