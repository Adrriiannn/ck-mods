using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEngine;

namespace ExpandNullforge.Skills
{
    /// <summary>
    /// The pictures a mod put on talent squares, by which square each one belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TALENT'S PICTURE CANNOT TRAVEL THE WAY ITS NUMBERS DO. Core Keeper takes a mod's talents
    /// from a small JSON file, and the shape of that file — <c>PugMods.Talents.Talent</c> — carries
    /// a name, an effect and a value per point, and nothing else. There is no field for a picture,
    /// so a talent written by a mod arrived with an empty square while the game's own talents all
    /// have one. That is what this fills.
    /// </para>
    /// <para>
    /// THE SQUARE IS ADDRESSED BY SKILL AND POSITION, because that is how the game addresses it too:
    /// the talent window hands each square its skill and its index and asks for the rest. The
    /// position is the one <see cref="DimensionTalentsTemplate.Group"/> works out, which is also the
    /// position the generator writes the JSON in — one rule, asked for in both places, so a picture
    /// cannot land on the talent next door.
    /// </para>
    /// </remarks>
    public static class DimensionTalentIconRegistry
    {
        private static readonly Dictionary<int, Sprite> icons = new Dictionary<int, Sprite>();

        /// <summary>Whether anything at all was registered, so callers can skip the work.</summary>
        public static bool HasAny
        {
            get { return icons.Count > 0; }
        }

        /// <summary>How many squares have a picture of a mod's own.</summary>
        public static int Count
        {
            get { return icons.Count; }
        }

        /// <summary>Puts a picture on one square. Registering the same square twice replaces it.</summary>
        public static void Register(SkillID skill, int position, Sprite icon)
        {
            if (icon == null || position < 0)
            {
                return;
            }

            icons[Key(skill, position)] = icon;
        }

        /// <summary>The picture for a square, or false when the mod did not give it one.</summary>
        public static bool TryGet(SkillID skill, int position, out Sprite icon)
        {
            return icons.TryGetValue(Key(skill, position), out icon);
        }

        /// <summary>Forgets everything, for a mod being unloaded or an editor reload.</summary>
        public static void Clear()
        {
            icons.Clear();
        }

        /// <summary>
        /// Reads every talent picture out of the mod's own shipped template.
        /// </summary>
        /// <remarks>
        /// Called from the mod's <c>ModObjectLoaded</c>, the same moment and for the same reason as
        /// the boss map pins: a <c>Sprite</c> is a Unity object that only exists once the bundle is
        /// loaded, so it cannot be baked into a number the way the talent's effect is.
        /// </remarks>
        public static void AttachFrom(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return;
            }

            DimensionGameSetupAsset[] setups = template.GlobalGameSetups;
            for (int s = 0; setups != null && s < setups.Length; s++)
            {
                DimensionGameSetupAsset setup = setups[s];

                // The same gate the generator applies before it writes a talent file: a switched
                // off setup ships no talents, so putting pictures on those squares would decorate
                // the game's own talents with a mod's art.
                if (setup == null || !setup.Enabled || !setup.Talents.ChangesWhatTalentsGive)
                {
                    continue;
                }

                List<DimensionTalentGroup> groups =
                    DimensionTalentsTemplate.Group(setup.Talents.Talents, null);
                for (int g = 0; g < groups.Count; g++)
                {
                    DimensionTalentGroup group = groups[g];
                    for (int t = 0; t < group.Talents.Length; t++)
                    {
                        Register(group.Skill, t, group.Talents[t].Icon);
                    }
                }
            }
        }

        private static int Key(SkillID skill, int position)
        {
            return ((int)skill * 1000) + position;
        }
    }
}
