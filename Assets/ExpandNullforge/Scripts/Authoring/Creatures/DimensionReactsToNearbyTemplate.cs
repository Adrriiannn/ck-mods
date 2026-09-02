using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// An object that changes when something comes near it, and can shove what comes too close.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three vanilla components with one idea between them, so they are asked about together:
    /// <c>ChangeVariationWhenObjectNearbyAuthoring</c> (54 prefabs) reacts to an object being
    /// placed near it, <c>ChangeVariationWhenPlayerHoldObjectNearbyAuthoring</c> (54) reacts to the
    /// player merely holding one, and <c>ToggleInteractionOnVariationAuthoring</c> (54) switches
    /// whether the thing can be interacted with at all depending which look it is wearing.
    /// </para>
    /// <para>
    /// Together they are how the game builds a lock: a door that opens when the right key is nearby,
    /// a shrine that lights when you hold its offering, a mechanism that becomes usable only once
    /// something else has happened to it. None of it was reachable.
    /// </para>
    /// <para>
    /// <c>AddForceToNearbyEntitiesAuthoring</c> (27) rides along here because it is the same
    /// question asked with a push instead of a look: a vent, a fan, a pulsing hazard.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionReactsToNearbyTemplate
    {
        [Header("When an object is placed near it")]
        [Tooltip("It changes look when a particular object is placed nearby.")]
        [SerializeField] private bool reactsToANearbyObject;

        [Tooltip("Which object it watches for.")]
        [SerializeField] private string watchesForObjectId = string.Empty;

        [Tooltip("It only reacts to one particular look of that object.")]
        [SerializeField] private bool onlyOneLookOfIt;

        [Tooltip("Which look of it that is.")]
        [Min(0)]
        [SerializeField] private int itsLook;

        [Tooltip("How close that object has to be, in tiles.")]
        [Min(0f)]
        [SerializeField] private float withinDistance = 1f;

        [Tooltip("Nudges where it looks for that object, relative to itself.")]
        [SerializeField] private Vector3 looksAtOffset = Vector3.zero;

        [Tooltip("Which look it changes to.")]
        [Min(0)]
        [SerializeField] private int changesToLook = 1;

        [Tooltip("It stays changed once it has changed, rather than reverting.")]
        [SerializeField] private bool staysChanged;

        [Tooltip("It plays its activation animation as it changes.")]
        [SerializeField] private bool playsItsActivationAnimation = true;

        [Tooltip("Anyone's object counts, not only the player's own faction.")]
        [SerializeField] private bool anyonesObjectCounts;

        [Header("When the player is holding one")]
        [Tooltip("It also changes when a player merely holds the object near it.")]
        [SerializeField] private bool reactsToAHeldObject;

        [Tooltip("Which object being held it reacts to. Blank reuses the one above.")]
        [SerializeField] private string watchesForHeldObjectId = string.Empty;

        [Tooltip("How close the player has to be while holding it.")]
        [Min(0f)]
        [SerializeField] private float heldWithinDistance = 1f;

        [Tooltip("Nudges where it looks for the held object.")]
        [SerializeField] private Vector3 heldLooksAtOffset = Vector3.zero;

        [Tooltip("Which look it changes to for the held object.")]
        [Min(0)]
        [SerializeField] private int heldChangesToLook = 1;

        [Tooltip("Its collider is cleared away as it changes — how a barrier opens.")]
        [SerializeField] private bool alsoClearsItsCollider;

        [Header("Whether it can be used")]
        [Tooltip("Whether it can be interacted with depends on which look it is wearing.")]
        [SerializeField] private bool usableDependsOnItsLook;

        [Tooltip("How that works — usable only at that look, or usable at everything but that look.")]
        [SerializeField] private DimensionUsableByLook usableRule = DimensionUsableByLook.OnlyAtThisLook;


        [Tooltip("Which look the rule is about.")]
        [Min(0)]
        [SerializeField] private int theLookInQuestion;

        [Header("Shoving what comes close")]
        [Tooltip("It pushes things away from it.")]
        [SerializeField] private bool shovesNearbyThings;

        [Tooltip("How far the shove reaches, in tiles.")]
        [Min(0f)]
        [SerializeField] private float shoveReaches = 3f;

        [Tooltip("How hard it shoves.")]
        [Min(0f)]
        [SerializeField] private float shoveForce = 5f;

        [Tooltip("How hard it shoves during the wind-up, before the full shove.")]
        [Min(0f)]
        [SerializeField] private float shoveForceWhileWindingUp;

        [Tooltip("It only shoves what it can actually see.")]
        [SerializeField] private bool onlyShovesWhatItCanSee;

        [Tooltip("How long it winds up before shoving.")]
        [Min(0f)]
        [SerializeField] private float shoveWindUp;

        [Tooltip("How long the shove lasts. 0 shoves continuously.")]
        [Min(0f)]
        [SerializeField] private float shoveLasts;

        [Tooltip("How the shove strength changes across its active window. Flat by default.")]
        [SerializeField] private AnimationCurve shoveStrengthOverTime = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Tooltip("How long it rests between shoves.")]
        [Min(0f)]
        [SerializeField] private float restsBetweenShoves;

        public bool ReactsToANearbyObject { get { return reactsToANearbyObject; } }

        public string WatchesForObjectId { get { return watchesForObjectId ?? string.Empty; } }

        public bool OnlyOneLookOfIt { get { return onlyOneLookOfIt; } }

        public int ItsLook { get { return itsLook < 0 ? 0 : itsLook; } }

        public float WithinDistance { get { return withinDistance < 0f ? 0f : withinDistance; } }

        public Vector3 LooksAtOffset { get { return looksAtOffset; } }

        public int ChangesToLook { get { return changesToLook < 0 ? 0 : changesToLook; } }

        public bool StaysChanged { get { return staysChanged; } }

        public bool PlaysItsActivationAnimation { get { return playsItsActivationAnimation; } }

        public bool AnyonesObjectCounts { get { return anyonesObjectCounts; } }

        public bool ReactsToAHeldObject { get { return reactsToAHeldObject; } }

        /// <summary>Which object being held it watches for, falling back to the placed one.</summary>
        public string WatchesForHeldObjectId
        {
            get
            {
                return string.IsNullOrEmpty(watchesForHeldObjectId)
                    ? WatchesForObjectId
                    : watchesForHeldObjectId;
            }
        }

        public float HeldWithinDistance
        {
            get { return heldWithinDistance < 0f ? 0f : heldWithinDistance; }
        }

        public Vector3 HeldLooksAtOffset { get { return heldLooksAtOffset; } }

        public int HeldChangesToLook
        {
            get { return heldChangesToLook < 0 ? 0 : heldChangesToLook; }
        }

        public bool AlsoClearsItsCollider { get { return alsoClearsItsCollider; } }

        public bool UsableDependsOnItsLook { get { return usableDependsOnItsLook; } }

        public DimensionUsableByLook UsableRule { get { return usableRule; } }

        public int TheLookInQuestion
        {
            get { return theLookInQuestion < 0 ? 0 : theLookInQuestion; }
        }

        public bool ShovesNearbyThings { get { return shovesNearbyThings; } }

        public float ShoveReaches { get { return shoveReaches < 0f ? 0f : shoveReaches; } }

        public float ShoveForce { get { return shoveForce < 0f ? 0f : shoveForce; } }

        public float ShoveForceWhileWindingUp
        {
            get { return shoveForceWhileWindingUp < 0f ? 0f : shoveForceWhileWindingUp; }
        }

        public bool OnlyShovesWhatItCanSee { get { return onlyShovesWhatItCanSee; } }

        public float ShoveWindUp { get { return shoveWindUp < 0f ? 0f : shoveWindUp; } }

        public float ShoveLasts { get { return shoveLasts < 0f ? 0f : shoveLasts; } }

        public AnimationCurve ShoveStrengthOverTime { get { return shoveStrengthOverTime; } }

        public float RestsBetweenShoves
        {
            get { return restsBetweenShoves < 0f ? 0f : restsBetweenShoves; }
        }

        /// <summary>Whether it watches for a nearby object without naming one.</summary>
        public bool WatchesForNothing
        {
            get { return reactsToANearbyObject && string.IsNullOrEmpty(WatchesForObjectId); }
        }

        /// <summary>Whether it reacts to a held object without either field naming one.</summary>
        public bool WatchesForNothingHeld
        {
            get { return reactsToAHeldObject && string.IsNullOrEmpty(WatchesForHeldObjectId); }
        }

        /// <summary>Whether it shoves with no force behind it.</summary>
        public bool ShovesWithNoForce
        {
            get { return shovesNearbyThings && shoveForce <= 0f && shoveForceWhileWindingUp <= 0f; }
        }
    }

    /// <summary>
    /// How a look decides whether something can be used. Core Keeper's
    /// <c>ToggleInteractionByVariationType</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionUsableByLook
    {
        /// <summary>
        /// Usable at every look except that one. The game calls this <c>DisableIfVariation</c> —
        /// it names the look that SWITCHES IT OFF, which is the opposite of how it reads.
        /// </summary>
        AtEveryLookButThisOne = 0,

        /// <summary>Usable only while it wears that look. <c>EnableIfVariation</c>.</summary>
        OnlyAtThisLook = 1
    }
}
