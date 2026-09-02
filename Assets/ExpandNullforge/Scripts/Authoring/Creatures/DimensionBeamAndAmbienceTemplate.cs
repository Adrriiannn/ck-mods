using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A creature's own beam attack, alert taunts, and the ambient life that fills a tank or a
    /// terrarium.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A CREATURE'S BEAM IS NOT A WEAPON'S BEAM. The weapon version is held by a player and grows
    /// while held; this one is an attack state with a wind-up, a fixed reach and a tick rate, and it
    /// can fire several beams at once fanned out by an angle. That is how a boss sweeps a room.
    /// </para>
    /// <para>
    /// CORNER SMOOTHING IS WHY VANILLA CREATURES DO NOT SNAG. Nine sensor settings that let a
    /// creature slide around a corner instead of grinding into it. Left alone it uses the game's own
    /// values, which are the right ones — it is offered because a large or oddly-shaped custom
    /// creature is exactly the case where they need adjusting.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionBeamAndAmbienceTemplate
    {
        [Header("Its own beam attack")]
        [Tooltip("It fires a beam of its own, as an attack rather than a held weapon.")]
        [SerializeField] private bool firesABeamAttack;

        [Tooltip("Wind-up before the beam appears.")]
        [Min(0f)]
        [SerializeField] private float beamWindUp = 1f;

        [Tooltip("How long the beam stays out.")]
        [Min(0f)]
        [SerializeField] private float beamSeconds = 2f;

        [Tooltip("Recovery after it.")]
        [Min(0f)]
        [SerializeField] private float beamRecovery = 0.5f;

        [Tooltip("How far in front of it the beam starts.")]
        [Min(0f)]
        [SerializeField] private float beamStartsAt;

        [Tooltip("How far the beam reaches.")]
        [Min(0f)]
        [SerializeField] private float beamReach = 10f;

        [Tooltip("How wide the beam is.")]
        [Min(0f)]
        [SerializeField] private float beamWidth = 1f;

        [Tooltip("How often it hurts what it is passing over.")]
        [Min(0f)]
        [SerializeField] private float beamDamageEvery = 0.25f;

        [Tooltip("How many beams go out at once.")]
        [Min(0)]
        [SerializeField] private int beamCount = 1;

        [Tooltip("How far apart those beams fan, in degrees.")]
        [Min(0f)]
        [SerializeField] private float beamFanDegrees;

        [Tooltip("Shortest wait between beams.")]
        [Min(0f)]
        [SerializeField] private float beamMinCooldown = 4f;

        [Tooltip("Longest wait between beams.")]
        [Min(0f)]
        [SerializeField] private float beamMaxCooldown = 8f;

        [Tooltip("Flat beam damage.")]
        [Min(0)]
        [SerializeField] private int beamDamage;

        [Tooltip("How hard the beam hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float beamMultiplier = 1f;

        [Header("Noticing you")]
        [Tooltip("It plays an alert when it first spots something.")]
        [SerializeField] private bool playsAnAlert;

        [Tooltip("How many different alert animations it has.")]
        [Min(0)]
        [SerializeField] private int alertAnimations = 1;

        [Tooltip("Shortest pause before the alert.")]
        [Min(0f)]
        [SerializeField] private float alertMinPause;

        [Tooltip("Longest pause before it.")]
        [Min(0f)]
        [SerializeField] private float alertMaxPause;

        [Tooltip("How long the alert lasts.")]
        [Min(0f)]
        [SerializeField] private float alertSeconds = 1f;

        [Tooltip("Shortest wait between alerts.")]
        [Min(0f)]
        [SerializeField] private float alertMinCooldown = 4f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float alertMaxCooldown = 6f;

        [Header("Dropping things over time")]
        [Tooltip("It drops items on a timer rather than all at once.")]
        [SerializeField] private bool dripsItems;

        [Tooltip("What it drops.")]
        [SerializeField] private string dripsObjectId = string.Empty;

        [Tooltip("How many each time.")]
        [Min(0)]
        [SerializeField] private int dripAmount = 1;

        [Tooltip("How many times it repeats. 0 for forever.")]
        [Min(0)]
        [SerializeField] private int dripRepeats = 1;

        [Tooltip("How long between drops.")]
        [Min(0f)]
        [SerializeField] private float dripInterval = 1f;

        [Header("Catching fire")]
        [Tooltip("It can be set alight.")]
        [SerializeField] private bool canBeIgnited;

        [Tooltip("What appears when it catches. Blank for the game's own fire.")]
        [SerializeField] private string ignitesIntoId = string.Empty;

        [Tooltip("Which look of that.")]
        [Min(0)]
        [SerializeField] private int ignitesIntoVariation;

        [Header("Ambient life")]
        [Tooltip("It swims about like an aquarium fish.")]
        [SerializeField] private bool swimsLikeAnAquariumFish;

        [Tooltip("Slowest and fastest it swims.")]
        [SerializeField] private Vector2 swimSpeedRange = new Vector2(0.5f, 1.5f);

        [Tooltip("Shortest and longest it rests between swims.")]
        [SerializeField] private Vector2 swimIdleRange = new Vector2(1f, 3f);

        [Tooltip("How smoothly it turns. Higher is smoother.")]
        [Min(0f)]
        [SerializeField] private float swimSmoothing = 2f;

        [Tooltip("It scuttles about like a terrarium critter.")]
        [SerializeField] private bool scuttlesLikeATerrariumCritter;

        [Tooltip("How fast it scuttles.")]
        [Min(0f)]
        [SerializeField] private float scuttleSpeed = 1f;

        [Tooltip("Shortest and longest it rests between scuttles.")]
        [SerializeField] private Vector2 scuttleIdleRange = new Vector2(1f, 3f);

        [Header("Answering an instrument")]
        [Tooltip("It repeats notes played near it, the way a mimicking creature does.")]
        [SerializeField] private bool mimicsNotes;

        [Tooltip("How far away it can hear those notes.")]
        [Min(0f)]
        [SerializeField] private float mimicHearingRange = 10f;

        [Tooltip("The sound it answers with, by its name.")]
        [DimensionSoundName]
        [SerializeField] private string mimicSound = string.Empty;

        [Tooltip("How far its answer sits from the note it heard, in semitones.")]
        [SerializeField] private int mimicKeyOffset;

        [Header("Corner smoothing")]
        [Tooltip("It uses its own corner-sliding numbers instead of the game's. Rarely needed.")]
        [SerializeField] private bool usesItsOwnCornerSmoothing;

        [Tooltip("How far ahead it feels for a wall, vertically.")]
        [Min(0f)]
        [SerializeField] private float feelsAheadVertically = 0.4f;

        [Tooltip("And horizontally.")]
        [Min(0f)]
        [SerializeField] private float feelsAheadHorizontally = 0.4f;

        [Tooltip("How big the forward feeler is.")]
        [Min(0f)]
        [SerializeField] private float feelerSize = 0.25f;

        [Tooltip("How far apart its escape feelers sit, vertically.")]
        [Min(0f)]
        [SerializeField] private float escapeSpreadVertically = 0.6f;

        [Tooltip("And horizontally.")]
        [Min(0f)]
        [SerializeField] private float escapeSpreadHorizontally = 0.6f;

        [Tooltip("How big those feelers are, vertically.")]
        [Min(0f)]
        [SerializeField] private float escapeSizeVertically = 0.25f;

        [Tooltip("And horizontally.")]
        [Min(0f)]
        [SerializeField] private float escapeSizeHorizontally = 0.25f;

        [Tooltip("How strongly it slides round a corner.")]
        [Range(0f, 1f)]
        [SerializeField] private float cornerSlide = 0.185f;

        [Tooltip("It also smooths along flat walls.")]
        [SerializeField] private bool smoothsAlongWalls = true;

        [Tooltip("How strongly it does that.")]
        [Range(0f, 1f)]
        [SerializeField] private float wallSlide = 0.985f;

        public bool FiresABeamAttack { get { return firesABeamAttack; } }

        public float BeamWindUp { get { return Floor(beamWindUp); } }

        public float BeamSeconds { get { return Floor(beamSeconds); } }

        public float BeamRecovery { get { return Floor(beamRecovery); } }

        public float BeamStartsAt { get { return Floor(beamStartsAt); } }

        public float BeamReach { get { return Floor(beamReach); } }

        public float BeamWidth { get { return Floor(beamWidth); } }

        public float BeamDamageEvery { get { return Floor(beamDamageEvery); } }

        public int BeamCount { get { return beamCount < 1 ? 1 : beamCount; } }

        public float BeamFanDegrees { get { return Floor(beamFanDegrees); } }

        public float BeamMinCooldown { get { return Floor(beamMinCooldown); } }

        public float BeamMaxCooldown { get { return AtLeast(beamMaxCooldown, BeamMinCooldown); } }

        public int BeamDamage { get { return beamDamage < 0 ? 0 : beamDamage; } }

        public float BeamMultiplier { get { return Floor(beamMultiplier); } }

        public bool PlaysAnAlert { get { return playsAnAlert; } }

        public int AlertAnimations { get { return alertAnimations < 0 ? 0 : alertAnimations; } }

        public float AlertMinPause { get { return Floor(alertMinPause); } }

        public float AlertMaxPause { get { return AtLeast(alertMaxPause, AlertMinPause); } }

        public float AlertSeconds { get { return Floor(alertSeconds); } }

        public float AlertMinCooldown { get { return Floor(alertMinCooldown); } }

        public float AlertMaxCooldown { get { return AtLeast(alertMaxCooldown, AlertMinCooldown); } }

        public bool DripsItems { get { return dripsItems; } }

        public string DripsObjectId { get { return dripsObjectId ?? string.Empty; } }

        public int DripAmount { get { return dripAmount < 0 ? 0 : dripAmount; } }

        public int DripRepeats { get { return dripRepeats < 0 ? 0 : dripRepeats; } }

        public float DripInterval { get { return Floor(dripInterval); } }

        public bool CanBeIgnited { get { return canBeIgnited; } }

        public string IgnitesIntoId { get { return ignitesIntoId ?? string.Empty; } }

        public int IgnitesIntoVariation
        {
            get { return ignitesIntoVariation < 0 ? 0 : ignitesIntoVariation; }
        }

        public bool SwimsLikeAnAquariumFish { get { return swimsLikeAnAquariumFish; } }

        public Vector2 SwimSpeedRange { get { return Ordered(swimSpeedRange); } }

        public Vector2 SwimIdleRange { get { return Ordered(swimIdleRange); } }

        public float SwimSmoothing { get { return Floor(swimSmoothing); } }

        public bool ScuttlesLikeATerrariumCritter { get { return scuttlesLikeATerrariumCritter; } }

        public float ScuttleSpeed { get { return Floor(scuttleSpeed); } }

        public Vector2 ScuttleIdleRange { get { return Ordered(scuttleIdleRange); } }

        public bool MimicsNotes { get { return mimicsNotes; } }

        public float MimicHearingRange { get { return Floor(mimicHearingRange); } }

        public string MimicSoundName { get { return mimicSound ?? string.Empty; } }

        public int MimicSound { get { return DimensionSoundNames.Hash(MimicSoundName); } }

        public int MimicKeyOffset { get { return mimicKeyOffset; } }

        public bool UsesItsOwnCornerSmoothing { get { return usesItsOwnCornerSmoothing; } }

        public float FeelsAheadVertically { get { return Floor(feelsAheadVertically); } }

        public float FeelsAheadHorizontally { get { return Floor(feelsAheadHorizontally); } }

        public float FeelerSize { get { return Floor(feelerSize); } }

        public float EscapeSpreadVertically { get { return Floor(escapeSpreadVertically); } }

        public float EscapeSpreadHorizontally { get { return Floor(escapeSpreadHorizontally); } }

        public float EscapeSizeVertically { get { return Floor(escapeSizeVertically); } }

        public float EscapeSizeHorizontally { get { return Floor(escapeSizeHorizontally); } }

        public float CornerSlide { get { return Clamp01(cornerSlide); } }

        public bool SmoothsAlongWalls { get { return smoothsAlongWalls; } }

        public float WallSlide { get { return Clamp01(wallSlide); } }

        /// <summary>
        /// Whether several beams go out with no fan between them.
        /// </summary>
        /// <remarks>
        /// They all leave along the same line and only one is visible, which reads as the beam count
        /// being ignored.
        /// </remarks>
        public bool BeamsWouldOverlap
        {
            get { return firesABeamAttack && beamCount > 1 && beamFanDegrees <= 0f; }
        }

        /// <summary>Whether the beam never hurts anything because it never ticks.</summary>
        public bool BeamNeverTicks
        {
            get { return firesABeamAttack && beamDamageEvery <= 0f; }
        }

        /// <summary>Whether it drips nothing.</summary>
        public bool DripsNothing
        {
            get { return dripsItems && string.IsNullOrEmpty(DripsObjectId); }
        }

        private static float Floor(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float AtLeast(float value, float floor)
        {
            float clamped = Floor(value);
            return clamped < floor ? floor : clamped;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        private static Vector2 Ordered(Vector2 range)
        {
            float low = range.x < 0f ? 0f : range.x;
            float high = range.y < 0f ? 0f : range.y;
            return new Vector2(low, high < low ? low : high);
        }
    }
}
