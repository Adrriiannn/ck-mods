using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What an object or creature leaves standing when it dies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <c>SpawnOnDeathAuthoring</c>, on 59 vanilla prefabs, and it is not loot. Loot is
    /// picked up; this is a whole object left in the world — the cocoon a boss leaves, the stump a
    /// felled tree leaves, the smaller worms a big one breaks into. It sits next to
    /// <see cref="DimensionTileOutcomeTemplate"/>, which is the same idea at the tile layer.
    /// </para>
    /// <para>
    /// THE CROWDING LIMIT IS THE INTERESTING FIELD. <c>maxAmountAllowedWithinRadius</c> is what
    /// stops a creature that spawns two of itself on death from filling the map. Vanilla uses it,
    /// and a mod that leaves it at zero has authored an unbounded chain with no error to show for it.
    /// </para>
    /// <para>
    /// <c>dontSpawnIfKilledByDestroyTimer</c> is the other one worth having: without it, something
    /// that expires on its own timer leaves its spawn behind exactly as if a player had killed it,
    /// so the world slowly fills with the leavings of things nobody touched.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionLeavesBehindTemplate
    {
        [Tooltip("What it leaves standing when it dies. Empty means nothing.")]
        [SerializeField] private string objectId = string.Empty;

        [Tooltip("Which variation of that object.")]
        [Min(0)]
        [SerializeField] private int variation;

        [Tooltip("How often it happens. 1 is always.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 1f;

        [Tooltip("Fewest left behind.")]
        [Min(0)]
        [SerializeField] private int minimumAmount = 1;

        [Tooltip("Most left behind.")]
        [Min(0)]
        [SerializeField] private int maximumAmount = 1;

        [Tooltip("Where it appears, relative to where the thing stood.")]
        [SerializeField] private Vector3 offset = Vector3.zero;

        [Tooltip("Stop leaving more once this many are already nearby. 0 means no limit at all.")]
        [Min(0)]
        [SerializeField] private int crowdLimit;

        [Tooltip("How far to look when counting what is already nearby.")]
        [Min(0f)]
        [SerializeField] private float crowdRadius = 8f;

        [Tooltip("Nothing is left behind when it simply ran out of time rather than being killed.")]
        [SerializeField] private bool onlyWhenActuallyKilled = true;

        public string ObjectId
        {
            get { return objectId ?? string.Empty; }
        }

        public int Variation
        {
            get { return variation < 0 ? 0 : variation; }
        }

        public float Chance
        {
            get
            {
                if (chance < 0f)
                {
                    return 0f;
                }

                return chance > 1f ? 1f : chance;
            }
        }

        public int MinimumAmount
        {
            get { return minimumAmount < 0 ? 0 : minimumAmount; }
        }

        /// <summary>The most left behind, never fewer than the minimum.</summary>
        public int MaximumAmount
        {
            get
            {
                int most = maximumAmount < 0 ? 0 : maximumAmount;
                return most < MinimumAmount ? MinimumAmount : most;
            }
        }

        public Vector3 Offset
        {
            get { return offset; }
        }

        public int CrowdLimit
        {
            get { return crowdLimit < 0 ? 0 : crowdLimit; }
        }

        public float CrowdRadius
        {
            get { return crowdRadius < 0f ? 0f : crowdRadius; }
        }

        public bool OnlyWhenActuallyKilled
        {
            get { return onlyWhenActuallyKilled; }
        }

        /// <summary>Whether anything is left behind at all.</summary>
        public bool LeavesAnything
        {
            get { return !string.IsNullOrEmpty(ObjectId) && Chance > 0f && MaximumAmount > 0; }
        }

        /// <summary>Whether something was named and then set never to appear.</summary>
        public bool NamesSomethingItWillNeverLeave
        {
            get
            {
                return !string.IsNullOrEmpty(ObjectId) && (Chance <= 0f || MaximumAmount <= 0);
            }
        }

        /// <summary>Whether it leaves more of itself with no limit on how many can gather.</summary>
        /// <remarks>
        /// The mistake worth catching before it ships. A creature that leaves two of itself and has
        /// no crowd limit is an unbounded chain: kill one, get two, kill those, get four. Nothing
        /// about it errors, and it is only obvious once a world is full of them.
        /// </remarks>
        public bool CanGrowWithoutLimit
        {
            get { return LeavesAnything && MaximumAmount > 1 && CrowdLimit == 0; }
        }
    }
}
