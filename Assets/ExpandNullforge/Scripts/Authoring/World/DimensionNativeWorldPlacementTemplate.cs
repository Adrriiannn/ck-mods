using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A marker that tells the game to put something somewhere, once, when a world is made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE GAME'S OWN WAY OF PLACING A ONE-OFF, and it is different from the framework's
    /// scene placement. The framework picks a spot itself and writes the scene in; this hands the
    /// decision to Core Keeper's world generator, which knows about biome bounds, the ring layout
    /// and the distance out from the Core. Use it for the things that should feel like part of the
    /// world's own furniture — a boss arena, a shrine, a wreck — rather than something a mod added.
    /// </para>
    /// <para>
    /// A WORLD IS EITHER CLASSIC OR FULL RELEASE, and the two generate differently, so the game asks
    /// for the biome, the distance and the exact spot twice — once for each kind of world. Creative
    /// worlds use the classic answer.
    /// </para>
    /// <para>
    /// THE MARKER AND THE THING ARE TWO PREFABS. World generation places the marker while it is
    /// still deciding what goes where; the marker then spawns the real thing. Leaving the marker
    /// behind is useful while you are testing, which is why clearing it away is a separate choice.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionNativeWorldPlacementTemplate
    {
        [Header("Placing something once, when a world is made")]
        [Tooltip("It is a world-generation marker: the game places it as it builds a new world.")]
        [SerializeField] private bool placesSomethingWhenAWorldIsMade;

        [Tooltip("What actually gets placed.")]
        [SerializeField] private GameObject theThingToPlace;

        [Tooltip("The stand-in placed first, while generation is still deciding. Blank for none.")]
        [SerializeField] private GameObject theMarkerThatPlacesIt;

        [Tooltip("It appears as soon as the world loads rather than waiting for a player to come near.")]
        [SerializeField] private bool appearsAsSoonAsTheWorldLoads;

        [Tooltip("The stand-in is cleared away once the real thing is there.")]
        [SerializeField] private bool clearsTheMarkerAwayAfterwards = true;

        [Header("Where it goes")]
        [Tooltip("How the spot is chosen.")]
        [SerializeField] private DimensionUniquePlacement howTheSpotIsChosen =
            DimensionUniquePlacement.ACertainDistanceOutInABiome;

        [Tooltip("Which biome it goes in, in a classic world. Blank leaves the game's own default.")]
        [SerializeField] private string biomeInAClassicWorld = string.Empty;

        [Tooltip("And in a full-release world.")]
        [SerializeField] private string biomeInAFullReleaseWorld = string.Empty;

        [Tooltip("How far out from the Core, in tiles, in a classic world.")]
        [Min(0)]
        [SerializeField] private int distanceFromTheCoreClassic;

        [Tooltip("And in a full-release world.")]
        [Min(0)]
        [SerializeField] private int distanceFromTheCoreFullRelease;

        [Tooltip("The exact tile it goes on in a classic world, when the spot is chosen that way.")]
        [SerializeField] private Vector2Int exactSpotClassic;

        [Tooltip("And in a full-release world.")]
        [SerializeField] private Vector2Int exactSpotFullRelease;

        [Tooltip("Where inside the biome the spot may fall. Has no effect in classic worlds.")]
        [SerializeField] private DimensionUniqueSpotSampling whereInsideTheBiome =
            DimensionUniqueSpotSampling.InsideTheFourthRing;

        [Tooltip("It may sit across the border between two spawn cells. No effect in classic worlds.")]
        [SerializeField] private bool mayStraddleSpawnCellBorders;

        [Header("Which version of the game it belongs to")]
        [Tooltip("The content bundle it is part of. Blank for none.")]
        [SerializeField] private ContentBundleDataBlock partOfContentBundle;

        [Tooltip("A newer bundle that replaces it, so the two never both appear. Blank for none.")]
        [SerializeField] private ContentBundleDataBlock replacedByBundle;

        [Header("Ordering")]
        [Tooltip("Its place in the list of things generation considers. Higher goes later.")]
        [Min(0)]
        [SerializeField] private int order;

        public bool PlacesSomethingWhenAWorldIsMade
        {
            get { return placesSomethingWhenAWorldIsMade; }
        }

        public GameObject TheThingToPlace { get { return theThingToPlace; } }

        public GameObject TheMarkerThatPlacesIt { get { return theMarkerThatPlacesIt; } }

        public bool AppearsAsSoonAsTheWorldLoads { get { return appearsAsSoonAsTheWorldLoads; } }

        public bool ClearsTheMarkerAwayAfterwards { get { return clearsTheMarkerAwayAfterwards; } }

        public DimensionUniquePlacement HowTheSpotIsChosen { get { return howTheSpotIsChosen; } }

        public string BiomeInAClassicWorld
        {
            get { return biomeInAClassicWorld ?? string.Empty; }
        }

        public string BiomeInAFullReleaseWorld
        {
            get { return biomeInAFullReleaseWorld ?? string.Empty; }
        }

        public int DistanceFromTheCoreClassic
        {
            get { return distanceFromTheCoreClassic < 0 ? 0 : distanceFromTheCoreClassic; }
        }

        public int DistanceFromTheCoreFullRelease
        {
            get { return distanceFromTheCoreFullRelease < 0 ? 0 : distanceFromTheCoreFullRelease; }
        }

        public Vector2Int ExactSpotClassic { get { return exactSpotClassic; } }

        public Vector2Int ExactSpotFullRelease { get { return exactSpotFullRelease; } }

        public DimensionUniqueSpotSampling WhereInsideTheBiome { get { return whereInsideTheBiome; } }

        public bool MayStraddleSpawnCellBorders { get { return mayStraddleSpawnCellBorders; } }

        public ContentBundleDataBlock PartOfContentBundle { get { return partOfContentBundle; } }

        public ContentBundleDataBlock ReplacedByBundle { get { return replacedByBundle; } }

        public int Order { get { return order < 0 ? 0 : order; } }

        /// <summary>Nothing was named to place, so generation would place nothing.</summary>
        public bool NothingToPlace
        {
            get { return placesSomethingWhenAWorldIsMade && theThingToPlace == null; }
        }

        /// <summary>
        /// An exact spot was asked for and both spots are the origin, which is where the Core sits.
        /// </summary>
        public bool ExactSpotIsOnTopOfTheCore
        {
            get
            {
                return placesSomethingWhenAWorldIsMade &&
                       howTheSpotIsChosen == DimensionUniquePlacement.AnExactSpot &&
                       exactSpotClassic == Vector2Int.zero &&
                       exactSpotFullRelease == Vector2Int.zero;
            }
        }

        /// <summary>The bundle that replaces it is the bundle it is in, so it replaces itself.</summary>
        public bool ReplacedByItsOwnBundle
        {
            get
            {
                return partOfContentBundle != null && partOfContentBundle == replacedByBundle;
            }
        }
    }

    /// <summary>How the game chooses where a one-off goes. Core Keeper's own three ways.</summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionUniquePlacement
    {
        /// <summary>A set distance out from the Core, inside a biome. <c>DistanceFromCoreInBiome</c>.</summary>
        ACertainDistanceOutInABiome = 0,

        /// <summary>Anywhere inside a biome. <c>AnywhereInBiome</c>.</summary>
        AnywhereInThatBiome = 1,

        /// <summary>On one exact tile. <c>ExactPosition</c>.</summary>
        AnExactSpot = 2
    }

    /// <summary>Where inside a biome a one-off is allowed to land.</summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionUniqueSpotSampling
    {
        /// <summary>Inside the fourth ring out from the Core. <c>InsideRing4</c>.</summary>
        InsideTheFourthRing = 0,

        /// <summary>Anywhere the biome reaches. <c>InsideBiomeBounds</c>.</summary>
        AnywhereTheBiomeReaches = 1
    }
}
