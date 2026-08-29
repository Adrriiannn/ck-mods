using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// An object that switches on and off with electricity, and changes while it is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ActivatedByElectricityStateAuthoring</c>, on 45 vanilla objects. It is the difference
    /// between a decoration that happens to be wired and a machine: the machine takes time to spool
    /// up, changes its look while it runs, and — the part that is easy to miss — can change what its
    /// collider does, so a powered door is solid when off and walkable when on.
    /// </para>
    /// <para>
    /// The wiring template already asks whether a thing needs power. This asks what happens when it
    /// gets it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionPoweredMachineTemplate
    {
        [Tooltip("It reacts to being powered rather than just consuming power.")]
        [SerializeField] private bool reactsToBeingPowered;

        [Tooltip("How long it takes to spool up once power arrives.")]
        [Min(0f)]
        [SerializeField] private float switchOnSeconds;

        [Tooltip("How long it takes to wind down once power is cut.")]
        [Min(0f)]
        [SerializeField] private float switchOffSeconds;

        [Tooltip("It changes its look while it is running.")]
        [SerializeField] private bool changesLookWhileRunning;

        [Tooltip("Which look it wears while running.")]
        [Min(0)]
        [SerializeField] private int runningVariation;

        [Header("What it blocks")]
        [Tooltip("Its collider stops blocking and becomes a trigger while it runs — a powered door.")]
        [SerializeField] private bool stopsBlockingWhileRunning;

        [Tooltip("Which physics layers that trigger belongs to. 0 leaves the game's own choice.")]
        [Min(0)]
        [SerializeField] private int triggerBelongsToLayers;

        [Tooltip("Which physics layers that trigger notices. 0 leaves the game's own choice.")]
        [Min(0)]
        [SerializeField] private int triggerNoticesLayers;

        public bool ReactsToBeingPowered { get { return reactsToBeingPowered; } }

        public float SwitchOnSeconds { get { return switchOnSeconds < 0f ? 0f : switchOnSeconds; } }

        public float SwitchOffSeconds
        {
            get { return switchOffSeconds < 0f ? 0f : switchOffSeconds; }
        }

        public bool ChangesLookWhileRunning { get { return changesLookWhileRunning; } }

        public int RunningVariation { get { return runningVariation < 0 ? 0 : runningVariation; } }

        public bool StopsBlockingWhileRunning { get { return stopsBlockingWhileRunning; } }

        public int TriggerBelongsToLayers
        {
            get { return triggerBelongsToLayers < 0 ? 0 : triggerBelongsToLayers; }
        }

        public int TriggerNoticesLayers
        {
            get { return triggerNoticesLayers < 0 ? 0 : triggerNoticesLayers; }
        }

        /// <summary>Whether a running look was chosen without switching the look change on.</summary>
        public bool RunningLookWillBeIgnored
        {
            get { return !changesLookWhileRunning && runningVariation > 0; }
        }

        /// <summary>Whether trigger layers were set on something whose collider never changes.</summary>
        public bool TriggerLayersWillBeIgnored
        {
            get
            {
                return !stopsBlockingWhileRunning
                    && (triggerBelongsToLayers > 0 || triggerNoticesLayers > 0);
            }
        }
    }

    /// <summary>
    /// An object that keeps the right ground underneath itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>EnsureSameGroundTileBeneathEntityAuthoring</c>, on 45 vanilla objects. It is how a thing
    /// that must not sit on the wrong floor keeps its own: if the ground beneath it is not one it
    /// supports, the game lays down the fallback.
    /// </para>
    /// <para>
    /// Worth having because the failure without it is subtle — a custom object placed on water or a
    /// pit half-sinks, or renders under the floor, and nothing says why.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionKeepsItsFloorTemplate
    {
        [Tooltip("It makes sure the ground beneath it is ground it can sit on.")]
        [SerializeField] private bool keepsItsOwnFloor;

        [Tooltip("Which kind of tile it needs beneath it.")]
        [SerializeField] private PugTilemap.TileType tileKindBeneath = PugTilemap.TileType.ground;

        [Tooltip("Tilesets it is happy to sit on. Empty means any.")]
        [SerializeField] private string[] happyOnTilesets = new string[0];

        [Tooltip("What gets laid down when the ground is not one of those.")]
        [SerializeField] private string fallbackTilesetId = string.Empty;

        [Tooltip("It keeps checking rather than only checking once as it is placed.")]
        [SerializeField] private bool keepsChecking;

        [Tooltip("It stops checking while it is in a particular state. Blank to always check.")]
        [SerializeField] private string stopsCheckingInState = string.Empty;

        public bool KeepsItsOwnFloor { get { return keepsItsOwnFloor; } }

        public PugTilemap.TileType TileKindBeneath { get { return tileKindBeneath; } }

        public string[] HappyOnTilesets { get { return happyOnTilesets ?? new string[0]; } }

        public string FallbackTilesetId { get { return fallbackTilesetId ?? string.Empty; } }

        public bool KeepsChecking { get { return keepsChecking; } }

        public string StopsCheckingInState
        {
            get { return stopsCheckingInState ?? string.Empty; }
        }

        /// <summary>Whether it keeps its own floor without saying what to lay down.</summary>
        /// <remarks>
        /// The fallback tileset is what actually gets placed. Without it the object insists on
        /// ground it supports and has nothing to put there, so it does nothing at all.
        /// </remarks>
        public bool HasNoFallback
        {
            get { return keepsItsOwnFloor && string.IsNullOrEmpty(FallbackTilesetId); }
        }
    }
}
