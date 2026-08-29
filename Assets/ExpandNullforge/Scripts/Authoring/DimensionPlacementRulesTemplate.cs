using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Where a placed object is allowed to go, and how it behaves as the player is putting it down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PlaceableObjectAuthoring</c> is on roughly 1,500 vanilla objects — more than any other
    /// component in the game — and eleven of its twenty-six fields were reachable. It is the single
    /// widest-reaching gap in the framework, because every chest, workbench, decoration, light,
    /// door and machine goes through it.
    /// </para>
    /// <para>
    /// MEASURED ACROSS THOSE 1,500: <c>canBePlacedOnBlockingObjects</c> 274,
    /// <c>blocksHangingWallObjects</c> 126, <c>hasVariationsThatCanBePlacedOnWalls</c> 90,
    /// <c>canPlaceOnSideOfWall</c> 88, <c>dontDestroyObjectIfInvalidPlacement</c> 57,
    /// <c>canBePlacedOnImmuneTiles</c> 28, <c>canBePlacedOnLowColliders</c> 24,
    /// <c>variationToPlace</c> 21, <c>dontBlockRoots</c> 20.
    /// </para>
    /// <para>
    /// THE WALL-SIDE FIELDS ARE A SET, not three independent switches: a torch, a sign or a painting
    /// needs <c>canPlaceOnSideOfWall</c> to be allowed there, <c>hasVariationsThatCanBePlacedOnWalls</c>
    /// so the game knows a wall-facing look exists, and <c>wallSideVariationStartsOnIndex1</c> to say
    /// where in the variation list that look begins. Setting one without the others is the reason a
    /// custom wall decoration snaps to the wall and then draws its floor sprite.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionPlacementRulesTemplate
    {
        [Header("What it can go on")]
        [Tooltip("It can be placed over things that would normally block placement. 274 objects do.")]
        [SerializeField] private bool canGoOverBlockingObjects;

        [Tooltip("It can be placed on tiles that are otherwise protected from being built on.")]
        [SerializeField] private bool canGoOnProtectedTiles;

        [Tooltip("It can be placed on top of low obstacles.")]
        [SerializeField] private bool canGoOnLowObstacles;

        [Tooltip("It can be placed on lava.")]
        [SerializeField] private bool canGoOnLava;

        [Header("Wall-mounted")]
        [Tooltip("It can be put on the side of a wall — a torch, a sign, a painting. 88 objects do.")]
        [SerializeField] private bool canGoOnTheSideOfAWall;

        [Tooltip("It has a separate look for when it is on a wall. Needed for the above to look right.")]
        [SerializeField] private bool hasAWallFacingLook;

        [Tooltip("That wall-facing look starts at variation 1 rather than 0.")]
        [SerializeField] private bool wallLookStartsAtVariationOne;

        [Tooltip("Nothing else can hang on the wall it is on. 126 objects claim their wall this way.")]
        [SerializeField] private bool claimsTheWallItIsOn;

        [Header("As it goes down")]
        [Tooltip("It turns to face the way the player is facing as they place it.")]
        [SerializeField] private bool facesThePlayersDirection;

        [Tooltip("Which of its looks is the one placed. 0 is the first.")]
        [Min(0)]
        [SerializeField] private int variationPlaced;

        [Tooltip("The player can cycle it through other looks that are not rotations.")]
        [SerializeField] private bool canBeCycledThroughOtherLooks;

        [Tooltip("How many looks that cycle has.")]
        [Min(0)]
        [SerializeField] private int howManyOtherLooks;

        [Tooltip("A bad placement leaves the item in hand rather than destroying it. 57 objects do.")]
        [SerializeField] private bool aBadPlacementDoesNotDestroyIt;

        [Header("What it does not get in the way of")]
        [Tooltip("Roots and plants can still grow through where it sits.")]
        [SerializeField] private bool rootsStillGrowThrough;

        [Header("How the placement preview draws")]
        [Tooltip("How the game draws it while the player is lining it up. Default suits most things.")]
        [SerializeField] private DimensionPlacementPreview placementPreview =
            DimensionPlacementPreview.Ordinary;

        public bool CanGoOverBlockingObjects { get { return canGoOverBlockingObjects; } }

        public bool CanGoOnProtectedTiles { get { return canGoOnProtectedTiles; } }

        public bool CanGoOnLowObstacles { get { return canGoOnLowObstacles; } }

        public bool CanGoOnLava { get { return canGoOnLava; } }

        public bool CanGoOnTheSideOfAWall { get { return canGoOnTheSideOfAWall; } }

        public bool HasAWallFacingLook { get { return hasAWallFacingLook; } }

        public bool WallLookStartsAtVariationOne { get { return wallLookStartsAtVariationOne; } }

        public bool ClaimsTheWallItIsOn { get { return claimsTheWallItIsOn; } }

        public bool FacesThePlayersDirection { get { return facesThePlayersDirection; } }

        public int VariationPlaced { get { return variationPlaced < 0 ? 0 : variationPlaced; } }

        public bool CanBeCycledThroughOtherLooks { get { return canBeCycledThroughOtherLooks; } }

        public int HowManyOtherLooks { get { return howManyOtherLooks < 0 ? 0 : howManyOtherLooks; } }

        public bool ABadPlacementDoesNotDestroyIt { get { return aBadPlacementDoesNotDestroyIt; } }

        public bool RootsStillGrowThrough { get { return rootsStillGrowThrough; } }

        public DimensionPlacementPreview PlacementPreview { get { return placementPreview; } }

        /// <summary>
        /// Whether it is allowed on walls without having a look for being on one.
        /// </summary>
        /// <remarks>
        /// This is the wall-decoration bug in one property. The object snaps happily onto the wall
        /// and then draws its floor sprite, because nothing told the game a wall-facing variation
        /// exists.
        /// </remarks>
        public bool GoesOnWallsWithNoWallLook
        {
            get { return canGoOnTheSideOfAWall && !hasAWallFacingLook; }
        }

        /// <summary>Whether it has a wall look it can never be placed on a wall to use.</summary>
        public bool HasAWallLookItCannotUse
        {
            get { return hasAWallFacingLook && !canGoOnTheSideOfAWall; }
        }

        /// <summary>Whether a cycle of looks was described without switching cycling on.</summary>
        public bool CycleWillBeIgnored
        {
            get { return !canBeCycledThroughOtherLooks && howManyOtherLooks > 0; }
        }

        /// <summary>Whether cycling was switched on with nothing to cycle to.</summary>
        public bool CycleHasNothingInIt
        {
            get { return canBeCycledThroughOtherLooks && howManyOtherLooks <= 0; }
        }
    }

    /// <summary>
    /// How the game draws a placement preview. Core Keeper's <c>DisplayPlaceableType</c>.
    /// </summary>
    /// <remarks>
    /// The order matches the game's enum exactly and must stay that way. The circuit entries are how
    /// wiring pieces show which way current will run before the player commits to the tile.
    /// </remarks>
    public enum DimensionPlacementPreview
    {
        /// <summary>The plain preview nearly everything uses. Default.</summary>
        Ordinary = 0,

        /// <summary>Shows the two directions it works along. Bidirectional.</summary>
        ShowsBothDirections = 1,

        /// <summary>A four-way wiring junction. CrossCircuit.</summary>
        WiringCrossJunction = 2,

        /// <summary>A three-way wiring junction. TCircuit.</summary>
        WiringTJunction = 3,

        /// <summary>A wiring corner. LCircuit.</summary>
        WiringCorner = 4,

        /// <summary>A straight run of wiring. ICircuit.</summary>
        WiringStraightRun = 5,

        /// <summary>Both directions, with a hint of what it will connect to. BidirectionalWithHint.</summary>
        ShowsBothDirectionsWithAHint = 6
    }
}
