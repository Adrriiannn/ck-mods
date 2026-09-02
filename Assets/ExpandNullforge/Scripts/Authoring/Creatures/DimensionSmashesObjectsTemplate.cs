using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A creature that attacks the world itself — walls, buildings, whatever is in its way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DamageObjectStateAuthoring</c> is separate from the melee attack on purpose: a creature
    /// hitting a player and a creature hitting a wall are two different states with two different
    /// damage numbers. It is how the game builds a thing that will chew through a base to reach you.
    /// </para>
    /// <para>
    /// THE GIVE-UP COUNT IS WHAT KEEPS IT SANE. <c>maxAllowedDamagesWithoutGoal</c> is how many
    /// swings it will take at scenery before deciding this is not getting it anywhere. Without a
    /// limit, a creature that cannot path to you stands and hits the same wall forever.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSmashesObjectsTemplate
    {
        [Tooltip("It attacks objects in its way, not just creatures.")]
        [SerializeField] private bool smashesWhatIsInItsWay;

        [Tooltip("How many swings it takes at scenery before giving up on getting through.")]
        [Min(0)]
        [SerializeField] private int givesUpAfterSwings = 5;

        [Header("The swing")]
        [Tooltip("Wind-up before it hits an object.")]
        [Min(0f)]
        [SerializeField] private float windUp = 0.4f;

        [Tooltip("How long that swing lasts.")]
        [Min(0f)]
        [SerializeField] private float swingSeconds = 0.3f;

        [Tooltip("How far in front of it the swing lands.")]
        [Min(0f)]
        [SerializeField] private float reach = 1f;

        [Tooltip("How wide the swing is.")]
        [Min(0f)]
        [SerializeField] private float radius = 1f;

        [Header("Damage")]
        [Tooltip("Flat damage to objects, used when there is no tier to scale from.")]
        [Min(0)]
        [SerializeField] private int flatObjectDamage;

        [Tooltip("How hard it hits objects for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float hitsObjectsThisHardForItsTier = 1f;

        [Tooltip("Flat damage to creatures caught in the same swing.")]
        [Min(0)]
        [SerializeField] private int flatCreatureDamage;

        [Tooltip("How hard it hits creatures for its tier in that swing.")]
        [Min(0f)]
        [SerializeField] private float hitsCreaturesThisHardForItsTier = 1f;

        [Tooltip("It smashes things even while chasing, ignoring rules that would stop it.")]
        [SerializeField] private bool smashesEvenWhileChasing;

        public bool SmashesWhatIsInItsWay { get { return smashesWhatIsInItsWay; } }

        public int GivesUpAfterSwings
        {
            get { return givesUpAfterSwings < 0 ? 0 : givesUpAfterSwings; }
        }

        public float WindUp { get { return windUp < 0f ? 0f : windUp; } }

        public float SwingSeconds { get { return swingSeconds < 0f ? 0f : swingSeconds; } }

        public float Reach { get { return reach < 0f ? 0f : reach; } }

        public float Radius { get { return radius < 0f ? 0f : radius; } }

        public int FlatObjectDamage
        {
            get { return flatObjectDamage < 0 ? 0 : flatObjectDamage; }
        }

        public float HitsObjectsThisHardForItsTier
        {
            get
            {
                return hitsObjectsThisHardForItsTier < 0f ? 0f : hitsObjectsThisHardForItsTier;
            }
        }

        public int FlatCreatureDamage
        {
            get { return flatCreatureDamage < 0 ? 0 : flatCreatureDamage; }
        }

        public float HitsCreaturesThisHardForItsTier
        {
            get
            {
                return hitsCreaturesThisHardForItsTier < 0f ? 0f : hitsCreaturesThisHardForItsTier;
            }
        }

        public bool SmashesEvenWhileChasing { get { return smashesEvenWhileChasing; } }

        /// <summary>
        /// Whether it will keep hitting scenery forever rather than giving up.
        /// </summary>
        /// <remarks>
        /// A creature that cannot reach you and never gives up stands at the nearest wall swinging
        /// at it for the rest of the session. Worth saying because the symptom looks like a pathing
        /// bug rather than a number nobody set.
        /// </remarks>
        public bool NeverGivesUpOnAWall
        {
            get { return smashesWhatIsInItsWay && givesUpAfterSwings <= 0; }
        }

        /// <summary>Whether its swing connects with nothing because it has no reach or width.</summary>
        public bool SwingConnectsWithNothing
        {
            get { return smashesWhatIsInItsWay && reach <= 0f && radius <= 0f; }
        }
    }
}
