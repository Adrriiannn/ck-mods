using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The sounds an attack makes, named the way the game names them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A SOUND IS NAMED, NOT NUMBERED. The game stores a sound as a hash of its name, and the
    /// framework does the hashing at generate time — so an author types
    /// <c>hydraBossBiteAnticipation</c> and never sees the number it becomes. The full list of the
    /// game's own names is <c>DimensionSoundNames.All</c>, 1,413 of them.
    /// </para>
    /// <para>
    /// A NAME THE GAME DOES NOT SHIP STILL WORKS, deliberately: a mod that brings its own sound
    /// elements names them whatever it likes, and the same hashing reaches them at runtime. The
    /// generator warns on an unknown name rather than refusing it, because it cannot tell a typo
    /// from a sound the mod is about to ship.
    /// </para>
    /// <para>
    /// BLANK MEANS THE GAME'S OWN. Every one of these only overrides when something is typed, so
    /// an attack with no sounds set keeps whatever its behaviour came with.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionAttackSoundsTemplate
    {
        [Tooltip("Swinging or firing it. A sound name from the game's list, or one of your own. Blank keeps the game's sound.")]
        [DimensionSoundName]
        [SerializeField] private string attackSound = string.Empty;

        [Tooltip("Connecting with something.")]
        [DimensionSoundName]
        [SerializeField] private string impactSound = string.Empty;

        [Tooltip("Winding up, before the attack lands.")]
        [DimensionSoundName]
        [SerializeField] private string windUpSound = string.Empty;

        [Tooltip("A wind-up that was cancelled.")]
        [DimensionSoundName]
        [SerializeField] private string windUpCancelledSound = string.Empty;

        [Tooltip("The heavier, charged version of the attack.")]
        [DimensionSoundName]
        [SerializeField] private string strongAttackSound = string.Empty;

        public string AttackSoundName { get { return attackSound ?? string.Empty; } }

        public string ImpactSoundName { get { return impactSound ?? string.Empty; } }

        public string WindUpSoundName { get { return windUpSound ?? string.Empty; } }

        public string WindUpCancelledSoundName
        {
            get { return windUpCancelledSound ?? string.Empty; }
        }

        public string StrongAttackSoundName { get { return strongAttackSound ?? string.Empty; } }

        /// <summary>The numbers the game stores, hashed from the names.</summary>
        public int AttackSound { get { return DimensionSoundNames.Hash(AttackSoundName); } }

        public int ImpactSound { get { return DimensionSoundNames.Hash(ImpactSoundName); } }

        public int WindUpSound { get { return DimensionSoundNames.Hash(WindUpSoundName); } }

        public int WindUpCancelledSound
        {
            get { return DimensionSoundNames.Hash(WindUpCancelledSoundName); }
        }

        public int StrongAttackSound
        {
            get { return DimensionSoundNames.Hash(StrongAttackSoundName); }
        }

        /// <summary>Whether anything at all was named.</summary>
        public bool HasAnySound
        {
            get
            {
                return AttackSound != 0 || ImpactSound != 0 || WindUpSound != 0 ||
                       WindUpCancelledSound != 0 || StrongAttackSound != 0;
            }
        }

        /// <summary>
        /// Legal, and almost always an oversight: the sound before the attack was chosen and the
        /// attack itself left as the default, so the two do not match.
        /// </summary>
        public bool WindsUpButSwingsWithTheDefault
        {
            get { return WindUpSound != 0 && AttackSound == 0; }
        }

        /// <summary>
        /// The names that are neither the game's own nor blank — a typo, or a sound the mod is
        /// about to ship. The generator says them out loud so a typo does not play as silence.
        /// </summary>
        public System.Collections.Generic.List<string> NamesTheGameDoesNotShip()
        {
            System.Collections.Generic.List<string> unknown =
                new System.Collections.Generic.List<string>();
            AddIfUnknown(unknown, AttackSoundName);
            AddIfUnknown(unknown, ImpactSoundName);
            AddIfUnknown(unknown, WindUpSoundName);
            AddIfUnknown(unknown, WindUpCancelledSoundName);
            AddIfUnknown(unknown, StrongAttackSoundName);
            return unknown;
        }

        private static void AddIfUnknown(
            System.Collections.Generic.List<string> unknown,
            string name)
        {
            if (!string.IsNullOrEmpty(name) &&
                !DimensionSoundNames.IsAGameSound(name) &&
                !unknown.Contains(name))
            {
                unknown.Add(name);
            }
        }
    }
}
