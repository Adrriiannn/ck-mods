using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// An object that changes its look based on the ground around it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>AdaptiveEntityBufferAuthoring</c> is on 171 vanilla objects — the third most-used block in
    /// the game — and it is why a bridge meets the shore correctly, why a rail knows it is a corner,
    /// and why a wall-mounted thing sits flush. The object checks the four tiles around it and picks
    /// the look that fits.
    /// </para>
    /// <para>
    /// EACH RULE SAYS "if this many of my four neighbours match, wear this look". Set
    /// <c>matchesNeeded</c> to 4 and all four must match; set it to 1 and any one will do. Rules are
    /// checked in order, so put the specific ones first — a rule needing all four before a rule
    /// needing one, or the loose rule wins every time and the specific look is never seen.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionLooksAtItsSurroundingsTemplate
    {
        [Tooltip("Its look changes depending on the ground around it.")]
        [SerializeField] private bool changesWithItsSurroundings;

        [Tooltip("The rules, checked in order. Put the fussiest first.")]
        [SerializeField] private DimensionSurroundingRule[] rules = new DimensionSurroundingRule[0];

        public bool ChangesWithItsSurroundings { get { return changesWithItsSurroundings; } }

        public DimensionSurroundingRule[] Rules
        {
            get { return rules ?? new DimensionSurroundingRule[0]; }
        }

        /// <summary>Whether it adapts with no rules to adapt by.</summary>
        public bool HasNoRules
        {
            get { return changesWithItsSurroundings && Rules.Length == 0; }
        }

        /// <summary>
        /// Whether a loose rule sits before a fussier one, which would hide the fussier one.
        /// </summary>
        /// <remarks>
        /// Rules are checked in order and the first match wins. A rule needing one matching
        /// neighbour placed above a rule needing four means the four-neighbour look can never
        /// appear, and the object will look subtly wrong in exactly the case someone built it for.
        /// </remarks>
        public bool AFussierRuleIsHiddenByALooserOne
        {
            get
            {
                DimensionSurroundingRule[] list = Rules;
                for (int i = 1; i < list.Length; i++)
                {
                    if (list[i].MatchesNeeded > list[i - 1].MatchesNeeded)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    /// <summary>One "if my neighbours look like this, wear that look" rule.</summary>
    [Serializable]
    public struct DimensionSurroundingRule
    {
        [Tooltip("Which look it wears when this rule matches.")]
        [Min(0)]
        [SerializeField] private int wearsLook;

        [Tooltip("How many of the four neighbours have to match. 4 is the fussiest.")]
        [Range(0, 4)]
        [SerializeField] private int matchesNeeded;

        [Tooltip("Any ground counts as a match, whatever it is made of.")]
        [SerializeField] private bool anyGroundCounts;

        [Tooltip("What it expects to its left. Blank for no expectation.")]
        [SerializeField] private string toItsLeft;

        [Tooltip("What it expects to its right.")]
        [SerializeField] private string toItsRight;

        [Tooltip("What it expects in front.")]
        [SerializeField] private string inFront;

        [Tooltip("What it expects behind.")]
        [SerializeField] private string behind;

        [Tooltip("Which kind of tile those all refer to.")]
        [SerializeField] private PugTilemap.TileType tileKind;

        public int WearsLook { get { return wearsLook < 0 ? 0 : wearsLook; } }

        public int MatchesNeeded
        {
            get
            {
                if (matchesNeeded < 0)
                {
                    return 0;
                }

                return matchesNeeded > 4 ? 4 : matchesNeeded;
            }
        }

        public bool AnyGroundCounts { get { return anyGroundCounts; } }

        public string ToItsLeft { get { return toItsLeft ?? string.Empty; } }

        public string ToItsRight { get { return toItsRight ?? string.Empty; } }

        public string InFront { get { return inFront ?? string.Empty; } }

        public string Behind { get { return behind ?? string.Empty; } }

        public PugTilemap.TileType TileKind { get { return tileKind; } }
    }
}
