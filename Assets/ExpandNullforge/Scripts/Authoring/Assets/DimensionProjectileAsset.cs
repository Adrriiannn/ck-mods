using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>How a projectile gets from the shooter to the target.</summary>
    /// <remarks>
    /// These are two different components, not two settings on one. A straight shot carries
    /// <c>ProjectileAuthoring</c>; an artillery shell carries <c>MortarProjectileAuthoring</c>
    /// INSTEAD — measured on the falling-rock and bomb prefabs, which have no
    /// <c>ProjectileAuthoring</c> at all. So this is a mode, and the generator writes one or the
    /// other rather than both.
    /// </remarks>
    public enum DimensionProjectileFlight
    {
        /// <summary>Straight from the shooter. Arrows, bolts, staff shots.</summary>
        FliesStraight = 0,

        /// <summary>Up, across and down. Falling rocks, boss bombs. (32 vanilla prefabs)</summary>
        ArcsLikeArtillery = 1
    }

    /// <summary>
    /// Something a weapon or a creature fires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A projectile is its own object in Core Keeper, not a setting on the thing that fires it — an
    /// arrow, a staff bolt and a boss's bullet-hell shot are each a separate <c>ObjectID</c> that a
    /// weapon or a creature names. That is why this is an asset rather than another template: the
    /// creature combat template and the ranged weapon both already ask for a projectile <i>by
    /// name</i>, and until now there was no way to make one to answer with.
    /// </para>
    /// <para>
    /// THE FIELDS ARE CHOSEN FROM MEASURED USE, not from the component. <c>ProjectileAuthoring</c>
    /// has 22 public fields; across the 71 vanilla projectiles that carry it, most are almost never
    /// touched. Measured:
    /// </para>
    /// <list type="bullet">
    /// <item><c>damageRadius</c> — 41 use 0.2, 13 use 0.5. This is the real dial.</item>
    /// <item><c>ExplodeOnEnemyCollision</c> 8, <c>piercesEnemies</c> 7, <c>maxBounceCount</c> 7,
    /// <c>damagesTiles</c> 5, <c>mayExplodeWithWindup</c> 4.</item>
    /// <item><c>shatterOnCollision</c> — <b>zero</b>. A field with a system behind it and no user in
    /// the whole game, so it is not offered as though it were a normal choice.</item>
    /// <item><c>zigZag</c>, <c>isDamageable</c>, <c>collideWithNonWalkableTiles</c> — one each.</item>
    /// </list>
    /// <para>
    /// So the ordinary questions are up front and the exotica sit under "rarely used", rather than a
    /// wall of 22 fields where the one that matters is buried.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Projectile")]
    public sealed class DimensionProjectileAsset : ScriptableObject
    {
        /// <summary>The damage radius all but a handful of vanilla projectiles use.</summary>
        public const float OrdinaryHitRadius = 0.2f;

        /// <summary>How long a vanilla projectile lives before it removes itself, in seconds.</summary>
        public const float DefaultLifetimeSeconds = 5f;

        [Header("Identity")]
        [SerializeField] private string projectileId = "projectile";

        [Tooltip("What you call it in your own lists. A shot is never held or hovered, so no player ever reads this.")]
        [SerializeField] private string displayName = "Projectile";

        [Header("Look")]
        [SerializeField] private Sprite sprite;

        [Header("How it flies")]
        [Tooltip("How fast it travels.")]
        [Min(0f)]
        [SerializeField] private float speed = 10f;

        [Tooltip("How long it lives before removing itself, in seconds.")]
        [Min(0f)]
        [SerializeField] private float lifetimeSeconds = DefaultLifetimeSeconds;

        [Header("What it hits")]
        [Tooltip("How wide the hit is. 0.2 is what most of the game uses; 0.5 is a big shot.")]
        [Min(0f)]
        [SerializeField] private float hitRadius = OrdinaryHitRadius;

        [Tooltip("It carries on through the first enemy it hits.")]
        [SerializeField] private bool goesThroughEnemies;

        [Tooltip("It bursts on contact with an enemy.")]
        [SerializeField] private bool explodesOnEnemies;

        [Tooltip("It damages the terrain as well.")]
        [SerializeField] private bool damagesTerrain;

        [Tooltip("How wide the damage to terrain is. Only used when it damages terrain.")]
        [Min(0f)]
        [SerializeField] private float terrainHitRadius;

        [Tooltip("How many times it bounces off walls. 0 means it stops at the first one.")]
        [Min(0)]
        [SerializeField] private int bounces;

        [Header("What it sounds like")]
        [SerializeField] private DimensionAttackSoundsTemplate sounds = new DimensionAttackSoundsTemplate();

        [Header("How it travels")]
        [Tooltip("Straight from the shooter, or arcing up and falling like artillery.")]
        [SerializeField] private DimensionProjectileFlight flight = DimensionProjectileFlight.FliesStraight;

        [Header("Artillery only")]
        [Tooltip("Use the game's own timings for the arc rather than the four below.")]
        [SerializeField] private bool useTheGamesOwnTimings = true;

        [Tooltip("Seconds going up.")]
        [Min(0f)]
        [SerializeField] private float goUpSeconds = 0.3f;

        [Tooltip("Seconds at the top of the arc.")]
        [Min(0f)]
        [SerializeField] private float airSeconds = 0.45f;

        [Tooltip("Seconds coming down.")]
        [Min(0f)]
        [SerializeField] private float goDownSeconds = 0.5f;

        [Tooltip("Seconds between landing and going off.")]
        [Min(0f)]
        [SerializeField] private float explodeSeconds = 0.2f;

        [Tooltip("It damages the terrain where it lands.")]
        [SerializeField] private bool breaksTerrainWhereItLands;

        [Tooltip("It counts as magic rather than a physical hit.")]
        [SerializeField] private bool isMagic;

        [Tooltip("It ignores the per-hit damage cap.")]
        [SerializeField] private bool ignoresTheDamageCap;

        [Tooltip("It only lands where the shooter can see.")]
        [SerializeField] private bool onlyLandsWhereItCanSee;

        [Header("Artillery: the trail it leaves")]
        [Tooltip("It scatters tiles on the way down — the signature falling-rock move.")]
        [SerializeField] private bool scattersTilesOnTheWayDown;

        [Tooltip("The tileset it scatters.")]
        [SerializeField] private string scatteredTilesetId = string.Empty;

        [Tooltip("Which layer of that tileset.")]
        [SerializeField] private PugTilemap.TileType scatteredTileType = PugTilemap.TileType.ground;

        [Tooltip("How much wider than the blast the scatter reaches.")]
        [Min(0f)]
        [SerializeField] private float scatterExtraRadius;

        [Tooltip("It leaves a tile where it lands.")]
        [SerializeField] private bool leavesATileWhereItLands;

        [Tooltip("The tileset it leaves where it lands.")]
        [SerializeField] private string landedTilesetId = string.Empty;

        [Tooltip("Which layer of that tileset.")]
        [SerializeField] private PugTilemap.TileType landedTileType = PugTilemap.TileType.ground;

        [Header("Rarely used")]
        [Tooltip("It survives the collision instead of being destroyed by it.")]
        [SerializeField] private bool survivesCollision;

        [Tooltip("It can be shot down. One vanilla projectile does this.")]
        [SerializeField] private bool canBeShotDown;

        [Tooltip("It weaves as it flies. One vanilla projectile does this.")]
        [SerializeField] private bool weaves;

        [Tooltip("It stops on tiles nothing can walk on. One vanilla projectile does this.")]
        [SerializeField] private bool stopsOnUnwalkableTiles;

        [Tooltip("A dodge counts as a hit against it.")]
        [SerializeField] private bool dodgingDoesNotSaveYou;

        [Tooltip("It breaks into this many of something when it lands.")]
        [Min(0)]
        [SerializeField] private int shards;

        [Tooltip("What it breaks into. One of the game's objects, or one of yours. Only used when it has shards.")]
        [SerializeField] private string shardObjectId = string.Empty;

        [Header("Flight, the finer grain")]
        [Tooltip("It shatters instead of exploding when it hits something.")]
        [SerializeField] private bool shattersOnCollision;

        [Tooltip("It travels out and back again over this long. 0 for an ordinary one-way flight.")]
        [Min(0f)]
        [SerializeField] private float outAndBackSeconds;

        [Tooltip("Its speed follows a curve rather than staying constant.")]
        [SerializeField] private bool speedFollowsACurve;

        [Tooltip("The speed curve it follows.")]
        [SerializeField] private AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Tooltip("A second speed curve, for projectiles that blend between two.")]
        [SerializeField] private AnimationCurve secondSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Tooltip("It can go off early if the shot was released before the wind-up finished.")]
        [SerializeField] private bool mayExplodeOnAPartialWindUp;

        [Tooltip("It only damages the same thing once every this many seconds. 0 for every hit.")]
        [Min(0f)]
        [SerializeField] private float onlyHitsTheSameThingEvery;

        [Tooltip("Which kinds of wall it can fly through. Empty means none.")]
        [SerializeField] private string[] fliesThroughWallTypes = new string[0];

        [Header("Mortar: the ground it changes")]
        [Tooltip("It can put its tiles down on water and pits.")]
        [SerializeField] private bool canPlaceTilesOnWaterAndPits;

        [Tooltip("It takes tiles away where it lands rather than adding them.")]
        [SerializeField] private bool removesTilesWhereItLands;

        [Tooltip("Which tileset it removes. Blank for whatever is there.")]
        [SerializeField] private string removedTilesetId = string.Empty;

        [Tooltip("Which kind of tile it removes.")]
        [SerializeField] private PugTilemap.TileType removedTileType = PugTilemap.TileType.ground;

        [Tooltip("The edge of what it changes is ragged rather than a clean circle.")]
        [SerializeField] private bool raggedEdges;

        [Tooltip("How hard it shoves what it hits.")]
        [Min(0f)]
        [SerializeField] private float pushesWhatItHits;

        [Tooltip("It leaves a fire or pool behind. One of the game's objects, or one of yours. Empty for nothing.")]
        [SerializeField] private string leavesBehindObjectId = string.Empty;

        [Tooltip("Which look that leftover uses.")]
        [Min(0)]
        [SerializeField] private int leavesBehindVariation;

        [Tooltip("Walls and roots it destroys drop nothing.")]
        [SerializeField] private bool wallsItBreaksDropNothing = true;

        [Tooltip("The client predicts its flight rather than waiting for the server.")]
        [SerializeField] private bool clientPredictsIt;

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string ProjectileId
        {
            get { return projectileId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public Sprite Sprite
        {
            get { return sprite; }
        }

        public float Speed
        {
            get { return speed < 0f ? 0f : speed; }
        }

        public float LifetimeSeconds
        {
            get { return lifetimeSeconds < 0f ? 0f : lifetimeSeconds; }
        }

        public float HitRadius
        {
            get { return hitRadius < 0f ? 0f : hitRadius; }
        }

        public bool GoesThroughEnemies
        {
            get { return goesThroughEnemies; }
        }

        public bool ExplodesOnEnemies
        {
            get { return explodesOnEnemies; }
        }

        public bool DamagesTerrain
        {
            get { return damagesTerrain; }
        }

        /// <summary>How wide the terrain damage is, or none when it does not damage terrain.</summary>
        public float TerrainHitRadius
        {
            get
            {
                if (!damagesTerrain)
                {
                    return 0f;
                }

                return terrainHitRadius < 0f ? 0f : terrainHitRadius;
            }
        }

        public int Bounces
        {
            get { return bounces < 0 ? 0 : bounces; }
        }
        /// <summary>How it gets from the shooter to the target.</summary>
        public DimensionProjectileFlight Flight
        {
            get { return flight; }
        }

        /// <summary>Whether it arcs like artillery rather than flying straight.</summary>
        public bool ArcsLikeArtillery
        {
            get { return flight == DimensionProjectileFlight.ArcsLikeArtillery; }
        }

        public bool UseTheGamesOwnTimings
        {
            get { return useTheGamesOwnTimings; }
        }

        public float GoUpSeconds
        {
            get { return goUpSeconds < 0f ? 0f : goUpSeconds; }
        }

        public float AirSeconds
        {
            get { return airSeconds < 0f ? 0f : airSeconds; }
        }

        public float GoDownSeconds
        {
            get { return goDownSeconds < 0f ? 0f : goDownSeconds; }
        }

        public float ExplodeSeconds
        {
            get { return explodeSeconds < 0f ? 0f : explodeSeconds; }
        }

        public bool BreaksTerrainWhereItLands
        {
            get { return breaksTerrainWhereItLands; }
        }

        public bool IsMagic
        {
            get { return isMagic; }
        }

        public bool IgnoresTheDamageCap
        {
            get { return ignoresTheDamageCap; }
        }

        public bool OnlyLandsWhereItCanSee
        {
            get { return onlyLandsWhereItCanSee; }
        }

        /// <summary>Whether it scatters tiles on the way down, and named a tileset to scatter.</summary>
        public bool ScattersTilesOnTheWayDown
        {
            get
            {
                return ArcsLikeArtillery
                    && scattersTilesOnTheWayDown
                    && !string.IsNullOrEmpty(ScatteredTilesetId);
            }
        }

        public string ScatteredTilesetId
        {
            get { return scatteredTilesetId ?? string.Empty; }
        }

        public PugTilemap.TileType ScatteredTileType
        {
            get { return scatteredTileType; }
        }

        public float ScatterExtraRadius
        {
            get { return scatterExtraRadius < 0f ? 0f : scatterExtraRadius; }
        }

        /// <summary>Whether it leaves a tile where it lands, and named one to leave.</summary>
        public bool LeavesATileWhereItLands
        {
            get
            {
                return ArcsLikeArtillery
                    && leavesATileWhereItLands
                    && !string.IsNullOrEmpty(LandedTilesetId);
            }
        }

        public string LandedTilesetId
        {
            get { return landedTilesetId ?? string.Empty; }
        }

        public PugTilemap.TileType LandedTileType
        {
            get { return landedTileType; }
        }

        /// <summary>
        /// Whether it was asked to scatter or leave tiles without naming any.
        /// </summary>
        /// <remarks>
        /// The tick is on and the tileset is blank, so the shell falls and leaves bare ground. It
        /// reads as the scattering being broken rather than as an unfilled field.
        /// </remarks>
        public bool ScattersOrLandsWithoutNamingATileset
        {
            get
            {
                if (!ArcsLikeArtillery)
                {
                    return false;
                }

                return (scattersTilesOnTheWayDown && string.IsNullOrEmpty(ScatteredTilesetId))
                    || (leavesATileWhereItLands && string.IsNullOrEmpty(LandedTilesetId));
            }
        }

        /// <summary>
        /// Whether it arcs with its own timings and every one of them is zero.
        /// </summary>
        /// <remarks>
        /// The game's own timings are ticked off and nothing was put in their place, so the shell
        /// completes its whole arc in no time at all and goes off where it was fired.
        /// </remarks>
        public bool ArcsInstantly
        {
            get
            {
                return ArcsLikeArtillery
                    && !useTheGamesOwnTimings
                    && GoUpSeconds <= 0f
                    && AirSeconds <= 0f
                    && GoDownSeconds <= 0f;
            }
        }


        public DimensionAttackSoundsTemplate Sounds
        {
            get { return sounds ?? new DimensionAttackSoundsTemplate(); }
        }

        public bool SurvivesCollision
        {
            get { return survivesCollision; }
        }

        public bool CanBeShotDown
        {
            get { return canBeShotDown; }
        }

        public bool Weaves
        {
            get { return weaves; }
        }

        public bool StopsOnUnwalkableTiles
        {
            get { return stopsOnUnwalkableTiles; }
        }

        public bool DodgingDoesNotSaveYou
        {
            get { return dodgingDoesNotSaveYou; }
        }

        public int Shards
        {
            get { return shards < 0 ? 0 : shards; }
        }

        /// <summary>What it breaks into, or empty when it has no shards.</summary>
        public string ShardObjectId
        {
            get { return Shards > 0 ? (shardObjectId ?? string.Empty) : string.Empty; }
        }

        /// <summary>Whether it will never reach anything, because it does not move.</summary>
        /// <remarks>
        /// Worth checking rather than assuming: a projectile with no speed is authored the same way
        /// as any other and simply appears at the shooter and expires.
        /// </remarks>
        public bool NeverGoesAnywhere
        {
            get { return Speed <= 0f; }
        }

        /// <summary>Whether it can never hit anything, because its hit is a point of zero width.</summary>
        public bool CannotHitAnything
        {
            get { return HitRadius <= 0f; }
        }

        /// <summary>Whether it was set to break into something, without saying what.</summary>
        public bool ShattersIntoNothing
        {
            get { return Shards > 0 && string.IsNullOrEmpty(ShardObjectId); }
        }

        /// <summary>Whether it damages terrain over an area of nothing.</summary>
        /// <remarks>
        /// Sixty-three of the game's projectiles leave this at zero, so zero is the normal value —
        /// but paired with "damages terrain" it means the terrain damage can never land.
        /// </remarks>
        public bool DamagesTerrainOverNoArea
        {
            get { return damagesTerrain && TerrainHitRadius <= 0f; }
        }

        public bool ShattersOnCollision { get { return shattersOnCollision; } }

        public float OutAndBackSeconds
        {
            get { return outAndBackSeconds < 0f ? 0f : outAndBackSeconds; }
        }

        public bool SpeedFollowsACurve { get { return speedFollowsACurve; } }

        public AnimationCurve SpeedCurve { get { return speedCurve; } }

        public AnimationCurve SecondSpeedCurve { get { return secondSpeedCurve; } }

        public bool MayExplodeOnAPartialWindUp { get { return mayExplodeOnAPartialWindUp; } }

        public float OnlyHitsTheSameThingEvery
        {
            get { return onlyHitsTheSameThingEvery < 0f ? 0f : onlyHitsTheSameThingEvery; }
        }

        public string[] FliesThroughWallTypes
        {
            get { return fliesThroughWallTypes ?? new string[0]; }
        }

        public bool CanPlaceTilesOnWaterAndPits { get { return canPlaceTilesOnWaterAndPits; } }

        public bool RemovesTilesWhereItLands { get { return removesTilesWhereItLands; } }

        public string RemovedTilesetId { get { return removedTilesetId ?? string.Empty; } }

        public PugTilemap.TileType RemovedTileType { get { return removedTileType; } }

        public bool RaggedEdges { get { return raggedEdges; } }

        public float PushesWhatItHits
        {
            get { return pushesWhatItHits < 0f ? 0f : pushesWhatItHits; }
        }

        public string LeavesBehindObjectId
        {
            get { return leavesBehindObjectId ?? string.Empty; }
        }

        public int LeavesBehindVariation
        {
            get { return leavesBehindVariation < 0 ? 0 : leavesBehindVariation; }
        }

        public bool WallsItBreaksDropNothing { get { return wallsItBreaksDropNothing; } }

        public bool ClientPredictsIt { get { return clientPredictsIt; } }

        /// <summary>Whether a speed curve was drawn on a projectile that ignores curves.</summary>
        public bool SpeedCurveWillBeIgnored
        {
            get { return !speedFollowsACurve && speedCurve != null && speedCurve.length > 2; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }
    }
}
