using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A route a creature walks when it has nowhere better to be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>RoamingPathAuthoring</c>, on 42 vanilla prefabs — the wandering giants that cross a biome
    /// rather than milling about a spawn point. Wandering is a small circle around home; a patrol is
    /// a real route with a shape, and the two feel completely different to a player who watches one
    /// go past.
    /// </para>
    /// <para>
    /// THE ROUTE IS GENERATED, NOT DRAWN. You give it a shape, a size, how many turning points, and
    /// how much it is allowed to wander off a straight line between them — the game builds the path
    /// from that. Which is why the settings read as a description of a walk rather than a list of
    /// coordinates.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionPatrolPathTemplate
    {
        [Tooltip("It walks a route rather than milling around where it spawned.")]
        [SerializeField] private bool walksARoute;

        [Tooltip("What shape the route takes.")]
        [SerializeField] private DimensionPatrolShape shape = DimensionPatrolShape.AnywhereInACircle;

        [Header("How big")]
        [Tooltip("How far out the route reaches, in tiles. The game's own value is 50.")]
        [Min(0)]
        [SerializeField] private int reachesOut = 50;

        [Tooltip("How many turning points the route has. The game's own value is 10.")]
        [Min(0)]
        [SerializeField] private int turningPoints = 10;

        [Tooltip("How smoothly it walks between them — more segments, smoother corners.")]
        [Min(0)]
        [SerializeField] private int smoothness = 10;

        [Tooltip("Nearest and furthest apart two turning points can be.")]
        [SerializeField] private Vector2 gapBetweenPoints = Vector2.zero;

        [Tooltip("How much the route stretches beyond its natural length.")]
        [Min(0f)]
        [SerializeField] private float routeLengthMultiplier = 1f;

        [Header("How wandering")]
        [Tooltip("How much it weaves side to side along the route. 0 walks straight.")]
        [Min(0f)]
        [SerializeField] private float weave;

        [Tooltip("How much the distance to the next point varies.")]
        [Min(0f)]
        [SerializeField] private float distanceVariation;

        [Tooltip("How rounded the corners are. The game's own value is 0.5.")]
        [Range(0f, 1f)]
        [SerializeField] private float cornerRoundness = 0.5f;

        [Tooltip("How far off course it may turn, in degrees. The game's own value is 50.")]
        [Min(0f)]
        [SerializeField] private float turnVariation = 50f;

        [Tooltip("How many points it walks before changing its mind about direction.")]
        [Min(0)]
        [SerializeField] private int pointsBetweenTurns = 5;

        [Header("Staying in its biome")]
        [Tooltip("It sticks to one biome, by the game's own biome name. Blank to roam anywhere.")]
        [SerializeField] private string staysInBiome = string.Empty;

        [Tooltip("Narrowest angle to the biome's middle it will take. Negative turns the other way.")]
        [SerializeField] private float minAngleToBiomeCentre = -10f;

        [Tooltip("Widest angle to the biome's middle it will take.")]
        [SerializeField] private float maxAngleToBiomeCentre = 10f;

        [Tooltip("It roams near the player instead when the player is standing in a sub-biome.")]
        [SerializeField] private bool followsThePlayerInSubBiomes;

        [Tooltip("Which sub-biome ground that check looks for. Blank for none.")]
        [SerializeField] private string subBiomeTilesetId = string.Empty;

        [Header("Making it")]
        [Tooltip("Draw the route in the editor while building it. Off for a shipped mod.")]
        [SerializeField] private bool showTheRouteWhileBuilding;

        public bool WalksARoute { get { return walksARoute; } }

        public DimensionPatrolShape Shape { get { return shape; } }

        public int ReachesOut { get { return reachesOut < 0 ? 0 : reachesOut; } }

        public int TurningPoints { get { return turningPoints < 0 ? 0 : turningPoints; } }

        public int Smoothness { get { return smoothness < 0 ? 0 : smoothness; } }

        /// <summary>The gap range, never with the far end below the near end.</summary>
        public Vector2 GapBetweenPoints
        {
            get
            {
                float near = gapBetweenPoints.x < 0f ? 0f : gapBetweenPoints.x;
                float far = gapBetweenPoints.y < 0f ? 0f : gapBetweenPoints.y;
                return new Vector2(near, far < near ? near : far);
            }
        }

        public float RouteLengthMultiplier
        {
            get { return routeLengthMultiplier < 0f ? 0f : routeLengthMultiplier; }
        }

        public float Weave { get { return weave < 0f ? 0f : weave; } }

        public float DistanceVariation
        {
            get { return distanceVariation < 0f ? 0f : distanceVariation; }
        }

        public float CornerRoundness
        {
            get
            {
                if (cornerRoundness < 0f)
                {
                    return 0f;
                }

                return cornerRoundness > 1f ? 1f : cornerRoundness;
            }
        }

        public float TurnVariation { get { return turnVariation < 0f ? 0f : turnVariation; } }

        public int PointsBetweenTurns
        {
            get { return pointsBetweenTurns < 0 ? 0 : pointsBetweenTurns; }
        }

        public string StaysInBiome { get { return staysInBiome ?? string.Empty; } }

        public float MinAngleToBiomeCentre { get { return minAngleToBiomeCentre; } }

        /// <summary>The wider angle, never below the narrower one.</summary>
        public float MaxAngleToBiomeCentre
        {
            get
            {
                return maxAngleToBiomeCentre < minAngleToBiomeCentre
                    ? minAngleToBiomeCentre
                    : maxAngleToBiomeCentre;
            }
        }

        public bool FollowsThePlayerInSubBiomes { get { return followsThePlayerInSubBiomes; } }

        public string SubBiomeTilesetId { get { return subBiomeTilesetId ?? string.Empty; } }

        public bool ShowTheRouteWhileBuilding { get { return showTheRouteWhileBuilding; } }

        /// <summary>Whether a route was asked for with no turning points to walk between.</summary>
        public bool RouteHasNoPoints
        {
            get { return walksARoute && turningPoints <= 0; }
        }

        /// <summary>Whether it is told to stay in a biome while its shape ignores biomes.</summary>
        public bool BiomeRuleWillBeIgnored
        {
            get
            {
                return !string.IsNullOrEmpty(StaysInBiome)
                    && shape != DimensionPatrolShape.InsideOneBiomeAtADistanceFromTheCore;
            }
        }
    }

    /// <summary>
    /// The shape of a patrol route. Core Keeper's <c>RoamingPathType</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionPatrolShape
    {
        /// <summary>It wanders anywhere inside a circle. <c>RandomInsideCircle</c>.</summary>
        AnywhereInACircle = 0,

        /// <summary>It keeps to one biome, at a set distance out from the Core.
        /// <c>StayInsideBiomeAtDistanceFromCore</c>.</summary>
        InsideOneBiomeAtADistanceFromTheCore = 1,

        /// <summary>It roams wherever the player happens to be. <c>RandomNearPlayer</c>.</summary>
        WhereverThePlayerIs = 2
    }
}
