using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>One burst of particles.</summary>
    /// <remarks>
    /// Maps onto Core Keeper's <c>PuffParams</c>. The <c>puff</c> is a <c>PuffID</c> — the game's own
    /// catalogue of particle bursts — and the offset lets a tall object throw its dust from the top
    /// rather than the floor.
    /// </remarks>
    [Serializable]
    public sealed class DimensionPuffBurst
    {
        [Tooltip("Which burst. A PuffID name from the game's own particle catalogue.")]
        [DimensionPuffName]
        [SerializeField] private string puffId = string.Empty;

        [Tooltip("How many particles.")]
        [Min(0)]
        [SerializeField] private int particleCount = 8;

        [Tooltip("Where it comes from, relative to the object's own position.")]
        [SerializeField] private Vector3 offset = Vector3.zero;

        public string PuffId
        {
            get { return puffId ?? string.Empty; }
        }

        public int ParticleCount
        {
            get { return particleCount < 0 ? 0 : particleCount; }
        }

        public Vector3 Offset
        {
            get { return offset; }
        }

        public bool NamesAPuff
        {
            get { return !string.IsNullOrEmpty(PuffId); }
        }

        /// <summary>Whether it names a burst but asks for no particles.</summary>
        public bool ProducesNothing
        {
            get { return NamesAPuff && ParticleCount == 0; }
        }
    }

    /// <summary>
    /// What hitting and breaking something sounds and looks like.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <c>TileEffectAuthoring</c>, and the name is misleading enough to be worth stating: it
    /// is not a gameplay effect on a tile. It is three fields — a sound when damaged, a sound when
    /// destroyed, and the particles thrown on destruction — and it is on 306 vanilla prefabs.
    /// </para>
    /// <para>
    /// It matters more than it looks. It is most of the difference between a custom block that feels
    /// like Core Keeper and one that shatters in silence with nothing coming off it. Nothing about
    /// that is visible in the inspector, and nothing errors — it is simply flat.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionImpactFeedbackTemplate
    {
        [Tooltip("Sound when it is hit but not destroyed, by its name. Blank for the game's own.")]
        [DimensionSoundName]
        [SerializeField] private string hitSoundId = string.Empty;

        [Tooltip("Sound when it is destroyed, by its name.")]
        [DimensionSoundName]
        [SerializeField] private string breakSoundId = string.Empty;

        [Tooltip("Particles thrown when it breaks.")]
        [SerializeField] private DimensionPuffBurst[] breakParticles = new DimensionPuffBurst[0];

        public string HitSoundIdName { get { return hitSoundId ?? string.Empty; } }

        public int HitSoundId { get { return DimensionSoundNames.Hash(HitSoundIdName); } }

        public string BreakSoundIdName { get { return breakSoundId ?? string.Empty; } }

        public int BreakSoundId { get { return DimensionSoundNames.Hash(BreakSoundIdName); } }

        /// <summary>Break particles, with the entries naming no burst dropped.</summary>
        public DimensionPuffBurst[] BreakParticles
        {
            get
            {
                DimensionPuffBurst[] all = breakParticles ?? new DimensionPuffBurst[0];
                System.Collections.Generic.List<DimensionPuffBurst> kept =
                    new System.Collections.Generic.List<DimensionPuffBurst>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].NamesAPuff)
                    {
                        kept.Add(all[i]);
                    }
                }

                return kept.ToArray();
            }
        }

        /// <summary>Whether anything at all happens when this is hit or broken.</summary>
        public bool HasAnyFeedback
        {
            get { return HitSoundId != 0 || BreakSoundId != 0 || BreakParticles.Length > 0; }
        }

        /// <summary>Whether it breaks in complete silence with nothing coming off it.</summary>
        /// <remarks>
        /// Legal, and occasionally wanted for something ethereal. On an ordinary block it is the
        /// difference between feeling like Core Keeper and feeling unfinished, and it is invisible
        /// until someone swings at it.
        /// </remarks>
        public bool BreaksSilentlyAndInvisibly
        {
            get { return BreakSoundId == 0 && BreakParticles.Length == 0; }
        }
    }
}
