using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The small rules Core Keeper attaches to a placed object, each one a component of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six components with thirteen fields between them, most of them bare markers with none at
    /// all. They are one template because they are one question — "how does the world treat this
    /// thing" — and because seven separate templates for seven ticks would be worse than the wall
    /// of fields it was trying to avoid.
    /// </para>
    /// <para>
    /// WHY THEY ARE ALL OPT-IN. Several are on a large share of vanilla prefabs —
    /// <c>AlwaysDropVariationZero</c> on 487, <c>OverrideNetworkSyncDistance</c> on 262 — and it is
    /// tempting to write them on everything. But the first changes what an object drops and only
    /// means anything when the object has more than one variation, and the second is a network
    /// trade-off, not a default. Adding either blindly because vanilla uses it a lot would be the
    /// invented-toughness-tiers mistake again: a number that looks measured and was guessed.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionObjectRulesTemplate
    {
        [Tooltip("When it drops itself, it drops variation 0 rather than the variation it was. " +
            "Only means anything for an object with more than one variation.")]
        [SerializeField] private bool alwaysDropsItsFirstVariation;

        [Tooltip("How far away it still updates for other players. 0 leaves the game's own distance.")]
        [Min(0f)]
        [SerializeField] private float networkRange;

        [Tooltip("It is ground cover — grass, a rug, a floor tile. Digging or planting clears it, " +
            "and a player, an animal and anything being placed all pass straight over it. " +
            "Untick it and the thing is solid: a player walks into it, unless you also tick " +
            "the walk-through answer below.")]
        [SerializeField] private bool isGroundCover;

        [Tooltip("A player and a creature walk straight through it, the way they walk through a " +
            "torch or a candle, and it still refuses to let anything else be built on its tile " +
            "and can still be hit with a sword. Ground cover wins if both are ticked.")]
        [SerializeField] private bool playersWalkThroughIt;

        [Tooltip("Its doorway runs along the wall, north to south, rather than across it. " +
            "The game ships the two as separate doors and their hitboxes lie at right angles, " +
            "so a door set the wrong way blocks the wall instead of the doorway. Doors only.")]
        [SerializeField] private bool doorwayRunsAlongTheWall;

        [Tooltip("A scanner finds it.")]
        [SerializeField] private bool aScannerFindsIt;

        [Tooltip("It stays switched on even when the game would otherwise disable it.")]
        [SerializeField] private bool alwaysStaysEnabled = true;

        [Header("Where it may stand")]
        [Tooltip("It is destroyed unless it stands on this object. Empty means no such rule.")]
        [SerializeField] private string mustStandOnObjectId = string.Empty;

        [Tooltip("Any tile a player can walk on will do.")]
        [SerializeField] private bool anyWalkableTileWillDo;

        [Tooltip("It may stand on water.")]
        [SerializeField] private bool mayStandOnWater;

        [Tooltip("It may stand on lava.")]
        [SerializeField] private bool mayStandOnLava;

        [Tooltip("It may stand over a pit.")]
        [SerializeField] private bool mayStandOverAPit;

        [Header("On the map")]
        [Tooltip("How it shows on the map. Empty means it does not.")]
        [SerializeField] private string mapMarker = string.Empty;

        [Tooltip("The marker disappears once the player has found the thing.")]
        [SerializeField] private bool markerGoesOnceFound;

        [Tooltip("Which painted colour it starts as. Unpainted for the plain look.")]
        [SerializeField] private string startingPaintColour = "Unpainted";

        [Tooltip("A scanner reports this object instead of the one it is. Blank for itself.")]
        [SerializeField] private string scannerReportsObjectId = string.Empty;

        [Tooltip("Which look of that object the scanner reports.")]
        [Min(0)]
        [SerializeField] private int scannerReportsVariation;

        [Tooltip("It plays dead at zero health instead of dying: it drops, it stands back up if " +
                 "anything heals it, and it never disappears or drops loot.")]
        [SerializeField] private bool animatesWhenItWouldHaveDied;

        [Tooltip("Creative mode ignores its immunity to loot-skipping.")]
        [SerializeField] private bool creativeIgnoresItsLootImmunity = true;

        [Tooltip("Its environment interaction is switched off.")]
        [SerializeField] private bool environmentInteractionIsOff;

        [Tooltip("It supports a longer animation history, for long or layered animations.")]
        [SerializeField] private bool supportsLongAnimationHistory;

        [Tooltip("Its tier generates level entities even when nothing asked for them.")]
        [SerializeField] private bool forcesLevelEntities;

        [Tooltip("A marker id shared by everything that should share one map pin. Blank for its own.")]
        [SerializeField] private string sharedMapMarkerId = string.Empty;

        [Tooltip("Which player-placed marker kind it shows as, if any.")]
        [SerializeField] private string playerMarkerKind = "None";

        [Tooltip("Nudges where its immunity zone sits, in tiles.")]
        [SerializeField] private Vector2Int immunityZoneTileOffset = Vector2Int.zero;

        [Tooltip("Which way it faces when its look decides its facing.")]
        [SerializeField] private Vector2Int facingFromVariation = Vector2Int.zero;

        [Tooltip("Its hitbox turns with it when its look decides its facing. The one hitbox it " +
            "has is rotated to match, which is how anything that faces a direction is turned.")]
        [SerializeField] private bool colliderTurnsWithIt;

        [Tooltip("It lines up with nearby wiring or affectors as it is placed.")]
        [SerializeField] private bool linesUpWithNeighboursWhenPlaced;

        [Tooltip("Its physics switch off this long after it appears. 0 to keep them.")]
        [Min(0f)]
        [SerializeField] private float physicsStopAfterSeconds;

        [Tooltip("It drops nothing when its lifetime runs out, as opposed to when it is broken.")]
        [SerializeField] private bool dropsNothingWhenItsTimeIsUp;

        [Tooltip("Its lifetime only starts once it reaches this look. -1 to start immediately.")]
        [SerializeField] private int lifetimeStartsAtVariation = -1;

        [Tooltip("A condition it grants while it sits on a table. Blank for none.")]
        [SerializeField] private string tableConditionId = string.Empty;

        [Tooltip("How strong that condition is.")]
        [SerializeField] private int tableConditionStrength;

        public string StartingPaintColour { get { return startingPaintColour ?? "Unpainted"; } }

        public string ScannerReportsObjectId
        {
            get { return scannerReportsObjectId ?? string.Empty; }
        }

        public int ScannerReportsVariation
        {
            get { return scannerReportsVariation < 0 ? 0 : scannerReportsVariation; }
        }

        public bool AnimatesWhenItWouldHaveDied { get { return animatesWhenItWouldHaveDied; } }

        public bool CreativeIgnoresItsLootImmunity
        {
            get { return creativeIgnoresItsLootImmunity; }
        }

        public bool EnvironmentInteractionIsOff { get { return environmentInteractionIsOff; } }

        public bool SupportsLongAnimationHistory { get { return supportsLongAnimationHistory; } }

        public bool ForcesLevelEntities { get { return forcesLevelEntities; } }

        public string SharedMapMarkerId { get { return sharedMapMarkerId ?? string.Empty; } }

        public string PlayerMarkerKind { get { return playerMarkerKind ?? "None"; } }

        public Vector2Int ImmunityZoneTileOffset { get { return immunityZoneTileOffset; } }

        public Vector2Int FacingFromVariation { get { return facingFromVariation; } }

        public bool ColliderTurnsWithIt { get { return colliderTurnsWithIt; } }

        public bool LinesUpWithNeighboursWhenPlaced
        {
            get { return linesUpWithNeighboursWhenPlaced; }
        }

        public float PhysicsStopAfterSeconds
        {
            get { return physicsStopAfterSeconds < 0f ? 0f : physicsStopAfterSeconds; }
        }

        public bool DropsNothingWhenItsTimeIsUp { get { return dropsNothingWhenItsTimeIsUp; } }

        public int LifetimeStartsAtVariation { get { return lifetimeStartsAtVariation; } }

        public string TableConditionId { get { return tableConditionId ?? string.Empty; } }

        public int TableConditionStrength { get { return tableConditionStrength; } }

        public bool AlwaysDropsItsFirstVariation
        {
            get { return alwaysDropsItsFirstVariation; }
        }

        public float NetworkRange
        {
            get { return networkRange < 0f ? 0f : networkRange; }
        }

        /// <summary>Whether a network range was chosen at all.</summary>
        public bool OverridesNetworkRange
        {
            get { return NetworkRange > 0f; }
        }

        public bool IsGroundCover
        {
            get { return isGroundCover; }
        }

        /// <summary>
        /// A player walks through it, and it is still solid to placement and to a sword.
        /// </summary>
        /// <remarks>
        /// 277 of the game's 977 prop shapes are this — <c>TorchEntity</c>,
        /// <c>PaintingBackWallEntity</c>, <c>CandleEntity</c> — and until this tick existed a
        /// modded prop could only be a solid wall or a rug nothing casts at.
        /// </remarks>
        public bool PlayersWalkThroughIt
        {
            get { return playersWalkThroughIt && !isGroundCover; }
        }

        /// <summary>Whether both walk-through answers were ticked, so one had to give way.</summary>
        public bool WalkThroughWasOverruledByGroundCover
        {
            get { return playersWalkThroughIt && isGroundCover; }
        }

        /// <summary>The doorway runs along the wall rather than across it.</summary>
        /// <remarks>
        /// Every vanilla door prefab is one tile by one tile whichever way it faces, so the
        /// footprint cannot answer this and the pass that guessed from it made every modded door
        /// an across door.
        /// </remarks>
        public bool DoorwayRunsAlongTheWall
        {
            get { return doorwayRunsAlongTheWall; }
        }

        public bool AScannerFindsIt
        {
            get { return aScannerFindsIt; }
        }

        public bool AlwaysStaysEnabled
        {
            get { return alwaysStaysEnabled; }
        }

        public string MustStandOnObjectId
        {
            get { return mustStandOnObjectId ?? string.Empty; }
        }

        public bool AnyWalkableTileWillDo
        {
            get { return anyWalkableTileWillDo; }
        }

        public bool MayStandOnWater
        {
            get { return mayStandOnWater; }
        }

        public bool MayStandOnLava
        {
            get { return mayStandOnLava; }
        }

        public bool MayStandOverAPit
        {
            get { return mayStandOverAPit; }
        }

        /// <summary>Whether anything restricts where it may stand.</summary>
        public bool HasAPlacementRule
        {
            get
            {
                return !string.IsNullOrEmpty(MustStandOnObjectId)
                    || anyWalkableTileWillDo
                    || mayStandOnWater
                    || mayStandOnLava
                    || mayStandOverAPit;
            }
        }

        public string MapMarker
        {
            get { return mapMarker ?? string.Empty; }
        }

        public bool ShowsOnTheMap
        {
            get { return !string.IsNullOrEmpty(MapMarker); }
        }

        public bool MarkerGoesOnceFound
        {
            get { return ShowsOnTheMap && markerGoesOnceFound; }
        }

        /// <summary>
        /// Whether it was told where it may stand in a way that lets it stand nowhere.
        /// </summary>
        /// <remarks>
        /// Naming nothing to stand on and ticking none of the surfaces means the rule matches no
        /// tile at all, and the object destroys itself the moment it is placed. It reads as an
        /// object that will not place rather than as a placement rule that is too narrow.
        /// </remarks>
        public bool MayStandNowhere
        {
            get
            {
                return HasAPlacementRule
                    && string.IsNullOrEmpty(MustStandOnObjectId)
                    && !anyWalkableTileWillDo
                    && !mayStandOnWater
                    && !mayStandOnLava
                    && !mayStandOverAPit;
            }
        }
    }
}
