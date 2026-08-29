using System.Collections.Generic;
using ExpandNullforge.Foundation;
using PugMod;
using UnityEngine;

namespace ExpandNullforge.Skills
{
    /// <summary>
    /// What killing a thing is worth, and to which skill.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CORE KEEPER HANDS OUT EXPERIENCE FROM INSIDE ITS OWN COMPILED JOBS. Every place the game
    /// awards a skill point — a wall mined, a fish landed, a monster killed — is a Burst-compiled
    /// job, which is to say a place a mod cannot reach. What the game does leave open is the other
    /// end: <c>PlayerController.AddSkill</c> is a plain public method that creates one entity
    /// carrying <c>AddSkillValueCD</c>, and <c>AddSkillValueSystem</c> picks that up and does
    /// everything else. The framework had never written one, so a mod's own monster gave nothing
    /// for killing it.
    /// </para>
    /// <para>
    /// REGISTERED BY NAME, READ BY NUMBER. A mod's object has no ObjectID until the game has loaded
    /// the mod and handed one out, so the generated bootstrap can only write the name. The numbers
    /// are looked up once, the first time the system runs in a world that knows them — the same
    /// reason and the same shape as the food ingredient hydration.
    /// </para>
    /// </remarks>
    public static class DimensionSkillXpRegistry
    {
        /// <summary>What one kill is worth.</summary>
        public struct Award
        {
            public Award(SkillID skill, int amount)
            {
                Skill = skill;
                Amount = amount;
            }

            public SkillID Skill { get; }

            public int Amount { get; }
        }

        private static readonly Dictionary<string, Award> byName =
            new Dictionary<string, Award>(System.StringComparer.Ordinal);

        private static readonly Dictionary<int, Award> byObject = new Dictionary<int, Award>();

        private static bool resolved;

        /// <summary>Whether anything at all was registered, so the system can skip its whole tick.</summary>
        public static bool HasAny
        {
            get { return byName.Count > 0; }
        }

        /// <summary>How many kinds of thing were said to be worth something.</summary>
        public static int Count
        {
            get { return byName.Count; }
        }

        /// <summary>How many of those the running game could actually find an object for.</summary>
        public static int ResolvedCount
        {
            get { return byObject.Count; }
        }

        /// <summary>
        /// Says that killing this kind of thing is worth something.
        /// </summary>
        /// <remarks>
        /// Registering the same object twice replaces the earlier amount rather than adding a
        /// second, so a mod reloaded in the editor does not double what its monsters are worth. An
        /// amount of nothing is refused: a reward of zero is a mistake, not a design.
        /// </remarks>
        public static bool Register(string objectName, SkillID skill, int amount)
        {
            if (string.IsNullOrEmpty(objectName) || amount <= 0)
            {
                return false;
            }

            if ((int)skill < 0 || (int)skill >= (int)SkillID.NUM_SKILLS)
            {
                DimensionLog.Problem(DimensionLogChannels.Skill, null, 
                    "'" + objectName + "' was said to give experience in a skill " +
                    "the game does not have, so it gives none. The twelve are Mining, Running, " +
                    "Melee, Vitality, Crafting, Range, Gardening, Fishing, Cooking, Magic, " +
                    "Summoning and Explosives.");
                return false;
            }

            byName[objectName] = new Award(skill, amount);
            resolved = false;
            return true;
        }

        /// <summary>What killing this is worth, or false when it is worth nothing.</summary>
        public static bool TryGet(ObjectID objectID, out Award award)
        {
            return byObject.TryGetValue((int)objectID, out award);
        }

        /// <summary>
        /// Turns the names into the numbers the running game uses, once.
        /// </summary>
        /// <returns>False while nothing could be resolved yet, so the caller can try again later.</returns>
        public static bool EnsureResolved()
        {
            if (resolved)
            {
                return byObject.Count > 0;
            }

            if (byName.Count == 0 || API.Authoring == null)
            {
                return false;
            }

            byObject.Clear();
            List<string> missing = null;
            foreach (KeyValuePair<string, Award> pair in byName)
            {
                ObjectID objectID = API.Authoring.GetObjectID(pair.Key);
                if (objectID == ObjectID.None)
                {
                    if (missing == null)
                    {
                        missing = new List<string>();
                    }

                    missing.Add(pair.Key);
                    continue;
                }

                byObject[(int)objectID] = pair.Value;
            }

            if (byObject.Count == 0)
            {
                // Nothing resolved at all reads as "the world does not know this mod's objects
                // yet", which is a normal state early on. Try again next tick rather than
                // complaining about names that are probably fine.
                return false;
            }

            resolved = true;
            if (missing != null)
            {
                for (int i = 0; i < missing.Count; i++)
                {
                    DimensionLog.Problem(DimensionLogChannels.Skill, null, 
                        "Killing '" + missing[i] + "' was meant to give skill " +
                        "experience, but the game has no object by that name, so it gives none. " +
                        "Check the name against the creature it was written for.");
                }
            }

            DimensionFrameworkLog.Verbose(
                byObject.Count + " of " + byName.Count +
                " things that give skill experience were found in this world.");
            return true;
        }

        /// <summary>Forgets everything, for a mod being unloaded or an editor reload.</summary>
        public static void Clear()
        {
            byName.Clear();
            byObject.Clear();
            resolved = false;
        }
    }
}
