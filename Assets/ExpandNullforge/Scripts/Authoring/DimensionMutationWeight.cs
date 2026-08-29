using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// One variation a bred baby can come out as, and how likely it is relative to the others.
    /// </summary>
    /// <remarks>
    /// Core Keeper's <c>BreedStateAuthoring.VariationWithWeight</c>. The weights are relative, not
    /// percentages — three entries weighted 1, 1 and 2 give the last one half the babies.
    /// </remarks>
    [Serializable]
    public struct DimensionMutationWeight
    {
        [Tooltip("Which look the baby comes out as.")]
        [Min(0)]
        [SerializeField] private int variation;

        [Tooltip("How likely this one is relative to the others in the list.")]
        [Min(0f)]
        [SerializeField] private float weight;

        public int Variation { get { return variation < 0 ? 0 : variation; } }

        public float Weight { get { return weight < 0f ? 0f : weight; } }
    }
}
