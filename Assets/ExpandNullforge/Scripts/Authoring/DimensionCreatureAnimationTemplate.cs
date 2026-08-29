using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// One thing a creature is seen doing, named the way a player would name it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERY VALUE HERE IS A CORE KEEPER ANIMATION NAME IN DISGUISE. The game's servers do not send
    /// pictures: a state system writes an integer into a replicated ring buffer, and that integer is
    /// <c>Animator.StringToHash</c> of a fixed name — "idle", "move", "attack". A clip only ever
    /// plays if its name hashes to the number the server sent, so the mapping in
    /// <see cref="DimensionCreatureAnimationNames"/> is not decoration; it is the only reason an
    /// authored strip is ever reached.
    /// </para>
    /// <para>
    /// The list is limited to the states this framework can actually make a creature enter. A name
    /// with no state system behind it would be a slot an author could fill and never see.
    /// </para>
    /// </remarks>
    public enum DimensionCreatureClipKind
    {
        /// <summary>Doing nothing. Every creature should have this one.</summary>
        Standing = 0,

        /// <summary>Walking, wandering, chasing.</summary>
        Walking = 1,

        /// <summary>Swinging at something within reach.</summary>
        Attacking = 2,

        /// <summary>Firing something from a distance.</summary>
        Shooting = 3,

        /// <summary>Lying down to sleep.</summary>
        Sleeping = 4,

        /// <summary>Still asleep. Repeats until something wakes it.</summary>
        FastAsleep = 5,

        /// <summary>Getting up again.</summary>
        WakingUp = 6,

        /// <summary>Dying.</summary>
        Dying = 7,

        /// <summary>Arriving in the world.</summary>
        Appearing = 8,

        /// <summary>Leaping at something.</summary>
        Leaping = 9,

        /// <summary>Running at something in a straight line.</summary>
        Charging = 10,

        /// <summary>Losing its temper below a health threshold.</summary>
        GettingAngry = 11,

        /// <summary>Eating.</summary>
        Eating = 12,

        /// <summary>Yawning, when nothing is happening.</summary>
        Yawning = 13,

        /// <summary>Chattering, when nothing is happening.</summary>
        Talking = 14,

        /// <summary>Pointing at something, mid fight.</summary>
        Pointing = 15,

        /// <summary>Taunting, mid fight.</summary>
        Taunting = 16,

        /// <summary>Spotting you and starting after you.</summary>
        NoticingYou = 17,

        /// <summary>Giving up and turning back.</summary>
        GivingUpTheChase = 18,

        /// <summary>Digging in before a run.</summary>
        WindingUpToRun = 19,

        /// <summary>Slowing down at the end of a run.</summary>
        SkiddingToAHalt = 20,

        /// <summary>Running into a wall.</summary>
        HittingAWall = 21,

        /// <summary>About to drop its guard.</summary>
        AboutToBeOpen = 22,

        /// <summary>Guard down. Repeats until it closes up again.</summary>
        OpenToAttack = 23,

        /// <summary>Getting its guard back up.</summary>
        ClosingUpAgain = 24,

        /// <summary>Swelling up before it bursts.</summary>
        AboutToBlowUp = 25,

        /// <summary>Disappearing, on its way somewhere else.</summary>
        Vanishing = 26,

        /// <summary>Turning up again somewhere else.</summary>
        Reappearing = 27,

        /// <summary>Throwing something in an arc.</summary>
        Lobbing = 28,

        /// <summary>Beginning to pour healing into something.</summary>
        StartingToChannel = 29,

        /// <summary>Pouring healing into something. Repeats while it holds.</summary>
        Channelling = 30,

        /// <summary>An egg starting to crack.</summary>
        AboutToHatch = 31,

        /// <summary>An egg breaking open.</summary>
        Hatching = 32,

        /// <summary>An egg that has broken open.</summary>
        Hatched = 33,

        /// <summary>Waiting mid fight, out of reach. Repeats.</summary>
        SizingYouUp = 34,

        /// <summary>Flinching when something lands a hit.</summary>
        BeingHurt = 35,

        /// <summary>Sweeping a beam across.</summary>
        FiringABeam = 36,

        /// <summary>Dropping at no health instead of dying.</summary>
        PlayingDead = 37,

        /// <summary>Standing back up after playing dead.</summary>
        GettingBackUp = 38,

        /// <summary>Making for a bush to hide in.</summary>
        HeadingIntoABush = 39,

        /// <summary>Hidden in a bush. Repeats until it peeks or leaves.</summary>
        HidingInABush = 40,

        /// <summary>Poking its head out of the bush.</summary>
        PeekingOut = 41,

        /// <summary>Coming back out of the bush.</summary>
        LeavingTheBush = 42
    }

    /// <summary>Whether a clip repeats.</summary>
    public enum DimensionClipRepeat
    {
        /// <summary>What the game's own creatures do with this one.</summary>
        SameAsTheGame = 0,

        /// <summary>Keeps going until something else is played.</summary>
        KeepsGoing = 1,

        /// <summary>Plays through once and hands back to standing.</summary>
        PlaysOnce = 2
    }

    /// <summary>The size of the dark blob under a creature's feet.</summary>
    /// <remarks>
    /// These are the game's own shadow sprites, named the way its shared Shadow asset names them.
    /// Picking one of these means a modded creature's shadow is the same art everything else in
    /// the world casts, rather than a lookalike that reads slightly wrong beside it.
    /// </remarks>
    public enum DimensionCreatureShadowSize
    {
        /// <summary>No shadow at all.</summary>
        None = 0,

        /// <summary>About the size of a critter.</summary>
        Tiny = 1,

        /// <summary>About the size of a slime.</summary>
        Small = 2,

        /// <summary>About the size of a caveling. The usual answer.</summary>
        Medium = 3,

        /// <summary>About the size of a brute.</summary>
        Large = 4,

        /// <summary>Boss sized.</summary>
        Huge = 5,

        /// <summary>Soft edged, for something that hovers.</summary>
        Soft = 6,

        /// <summary>Soft edged and wide, for something big that hovers.</summary>
        BigAndSoft = 7
    }

    /// <summary>
    /// Turns the words an author picks into the names Core Keeper's animation system answers to.
    /// </summary>
    /// <remarks>
    /// Kept out of the editor assembly on purpose: the runtime view needs the same hashes to know
    /// which trigger just arrived, and two copies of this table would drift the first time a name
    /// changed.
    /// </remarks>
    public static class DimensionCreatureAnimationNames
    {
        /// <summary>Every kind, in the order an author is offered them.</summary>
        public static readonly DimensionCreatureClipKind[] AllKinds =
        {
            DimensionCreatureClipKind.Standing,
            DimensionCreatureClipKind.Walking,
            DimensionCreatureClipKind.Attacking,
            DimensionCreatureClipKind.Shooting,
            DimensionCreatureClipKind.Sleeping,
            DimensionCreatureClipKind.FastAsleep,
            DimensionCreatureClipKind.WakingUp,
            DimensionCreatureClipKind.Dying,
            DimensionCreatureClipKind.Appearing,
            DimensionCreatureClipKind.Leaping,
            DimensionCreatureClipKind.Charging,
            DimensionCreatureClipKind.GettingAngry,
            DimensionCreatureClipKind.Eating,
            DimensionCreatureClipKind.Yawning,
            DimensionCreatureClipKind.Talking,
            DimensionCreatureClipKind.Pointing,
            DimensionCreatureClipKind.Taunting,
            DimensionCreatureClipKind.NoticingYou,
            DimensionCreatureClipKind.GivingUpTheChase,
            DimensionCreatureClipKind.WindingUpToRun,
            DimensionCreatureClipKind.SkiddingToAHalt,
            DimensionCreatureClipKind.HittingAWall,
            DimensionCreatureClipKind.AboutToBeOpen,
            DimensionCreatureClipKind.OpenToAttack,
            DimensionCreatureClipKind.ClosingUpAgain,
            DimensionCreatureClipKind.AboutToBlowUp,
            DimensionCreatureClipKind.Vanishing,
            DimensionCreatureClipKind.Reappearing,
            DimensionCreatureClipKind.Lobbing,
            DimensionCreatureClipKind.StartingToChannel,
            DimensionCreatureClipKind.Channelling,
            DimensionCreatureClipKind.AboutToHatch,
            DimensionCreatureClipKind.Hatching,
            DimensionCreatureClipKind.Hatched,
            DimensionCreatureClipKind.SizingYouUp,
            DimensionCreatureClipKind.BeingHurt,
            DimensionCreatureClipKind.FiringABeam,
            DimensionCreatureClipKind.PlayingDead,
            DimensionCreatureClipKind.GettingBackUp,
            DimensionCreatureClipKind.HeadingIntoABush,
            DimensionCreatureClipKind.HidingInABush,
            DimensionCreatureClipKind.PeekingOut,
            DimensionCreatureClipKind.LeavingTheBush
        };

        /// <summary>
        /// The Core Keeper animation name a kind stands for.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each of these was read off the state system that fires it, not guessed from the enum:
        /// <c>IdleStateSystem</c> fires "idle", <c>RoamingStateSystem</c> "move",
        /// <c>MeleeAttackStateSystem</c> "attack", <c>SleepStateSystem</c> "sleep" and "wakeUp",
        /// and the emote systems fire the strings a creature authors for them. "sleeping" is the
        /// exception: no system fires it, the asset chains into it from "sleep".
        /// </para>
        /// <para>
        /// THE LIST GREW BECAUSE THE STATE SURFACE GREW, NOT BECAUSE THE RULE CHANGED. The rule is
        /// still the one at the top of the enum — a name only earns a place here when a system in
        /// the game fires its hash AND this framework can put a generated creature into that state.
        /// It was last applied when far fewer states were reachable and nobody re-ran it as the
        /// generator learned to write more of them, so twenty-six names sat unreachable for no
        /// reason. Whoever adds a state to the generator should re-run it rather than assume it is
        /// closed.
        /// </para>
        /// <para>
        /// The second batch, each read off the system that fires it, by grepping the integer in
        /// <c>ck-db\Pug.Base\AnimID.cs</c> through the decompile — the decompiler inlined the
        /// constants, so the NAME appears nowhere and only the number finds the caller:
        /// "preChase" and "endChase" from <c>ChaseStateSystem</c>; "chargeAnticipation",
        /// "chargeEnd" and "collide" from <c>ChargeAttackStateSystem</c>; "prevulnerable",
        /// "vulnerable" and "endVulnerable" from <c>VulnerableStateSystem</c> (and "vulnerable"
        /// again from <c>ChargeAttackStateSystem</c>); "startExplode" from
        /// <c>ExplodeStateSystem</c>; "startTeleport" and "endTeleport" from
        /// <c>TeleportStateSystem</c>; "attackFire" from <c>ShootMortarProjectileStateSystem</c>;
        /// "startChanneling" and "channeling" from <c>HealOtherEntityStateSystem</c>;
        /// "isHatching", "hatch" and "hasHatched" from <c>HatchWhenPlayerNearbyStateSystem</c>;
        /// "idleCombat" from <c>IdleInCombatStateSystem</c>; "takeDamage" from <c>AttackSystem</c>,
        /// which fires it on anything with health; "beamAttack" from <c>BeamAttackStateSystem</c>;
        /// "feignDeath" and "revive" from <c>AnimateDontDestroyOnZeroHealthSystem</c>; and
        /// "goToBush", "bush", "peak" and "leaveBush" from <c>BushStateSystem</c>.
        /// </para>
        /// <para>
        /// DELIBERATELY ABSENT. "phaseTransition1" is fired by <c>EnemyStagesStateSystem</c> and
        /// <c>PhaseTransitionStateStateSystem</c>, and this framework writes neither
        /// <c>EnemyStagesAuthoring</c> nor <c>PhaseTransitionStateAuthoring</c>, so no generated
        /// boss can ever enter that state. "endBeamAttack" and the boss-only names — "sleepyEyes",
        /// "wakeUpEnrage", "screech" and the rest — are fired from <c>Pug.Objects</c> classes gated
        /// on a hardcoded vanilla ObjectID, which a modded creature cannot carry.
        /// </remarks>
        public static string AnimationNameFor(DimensionCreatureClipKind kind)
        {
            switch (kind)
            {
                case DimensionCreatureClipKind.Standing: return "idle";
                case DimensionCreatureClipKind.Walking: return "move";
                case DimensionCreatureClipKind.Attacking: return "attack";
                case DimensionCreatureClipKind.Shooting: return "rangedAttack";
                case DimensionCreatureClipKind.Sleeping: return "sleep";
                case DimensionCreatureClipKind.FastAsleep: return "sleeping";
                case DimensionCreatureClipKind.WakingUp: return "wakeUp";
                case DimensionCreatureClipKind.Dying: return "death";
                case DimensionCreatureClipKind.Appearing: return "spawn";
                case DimensionCreatureClipKind.Leaping: return "jump";
                case DimensionCreatureClipKind.Charging: return "charge";
                case DimensionCreatureClipKind.GettingAngry: return "enrage";
                case DimensionCreatureClipKind.Eating: return "eat";
                case DimensionCreatureClipKind.Yawning: return "yawn";
                case DimensionCreatureClipKind.Talking: return "talking";
                case DimensionCreatureClipKind.Pointing: return "point";
                case DimensionCreatureClipKind.Taunting: return "taunt";
                case DimensionCreatureClipKind.NoticingYou: return "preChase";
                case DimensionCreatureClipKind.GivingUpTheChase: return "endChase";
                case DimensionCreatureClipKind.WindingUpToRun: return "chargeAnticipation";
                case DimensionCreatureClipKind.SkiddingToAHalt: return "chargeEnd";
                case DimensionCreatureClipKind.HittingAWall: return "collide";
                case DimensionCreatureClipKind.AboutToBeOpen: return "prevulnerable";
                case DimensionCreatureClipKind.OpenToAttack: return "vulnerable";
                case DimensionCreatureClipKind.ClosingUpAgain: return "endVulnerable";
                case DimensionCreatureClipKind.AboutToBlowUp: return "startExplode";
                case DimensionCreatureClipKind.Vanishing: return "startTeleport";
                case DimensionCreatureClipKind.Reappearing: return "endTeleport";
                case DimensionCreatureClipKind.Lobbing: return "attackFire";
                case DimensionCreatureClipKind.StartingToChannel: return "startChanneling";
                case DimensionCreatureClipKind.Channelling: return "channeling";
                case DimensionCreatureClipKind.AboutToHatch: return "isHatching";
                case DimensionCreatureClipKind.Hatching: return "hatch";
                case DimensionCreatureClipKind.Hatched: return "hasHatched";
                case DimensionCreatureClipKind.SizingYouUp: return "idleCombat";
                case DimensionCreatureClipKind.BeingHurt: return "takeDamage";
                case DimensionCreatureClipKind.FiringABeam: return "beamAttack";
                case DimensionCreatureClipKind.PlayingDead: return "feignDeath";
                case DimensionCreatureClipKind.GettingBackUp: return "revive";
                case DimensionCreatureClipKind.HeadingIntoABush: return "goToBush";
                case DimensionCreatureClipKind.HidingInABush: return "bush";
                case DimensionCreatureClipKind.PeekingOut: return "peak";
                case DimensionCreatureClipKind.LeavingTheBush: return "leaveBush";
                default: return string.Empty;
            }
        }

        /// <summary>The words shown to an author for a kind.</summary>
        public static string LabelFor(DimensionCreatureClipKind kind)
        {
            switch (kind)
            {
                case DimensionCreatureClipKind.Standing: return "Standing";
                case DimensionCreatureClipKind.Walking: return "Walking";
                case DimensionCreatureClipKind.Attacking: return "Attacking";
                case DimensionCreatureClipKind.Shooting: return "Shooting";
                case DimensionCreatureClipKind.Sleeping: return "Sleeping";
                case DimensionCreatureClipKind.FastAsleep: return "Fast asleep";
                case DimensionCreatureClipKind.WakingUp: return "Waking up";
                case DimensionCreatureClipKind.Dying: return "Dying";
                case DimensionCreatureClipKind.Appearing: return "Appearing";
                case DimensionCreatureClipKind.Leaping: return "Leaping";
                case DimensionCreatureClipKind.Charging: return "Charging";
                case DimensionCreatureClipKind.GettingAngry: return "Getting angry";
                case DimensionCreatureClipKind.Eating: return "Eating";
                case DimensionCreatureClipKind.Yawning: return "Yawning";
                case DimensionCreatureClipKind.Talking: return "Talking";
                case DimensionCreatureClipKind.Pointing: return "Pointing";
                case DimensionCreatureClipKind.Taunting: return "Taunting";
                case DimensionCreatureClipKind.NoticingYou: return "Noticing you";
                case DimensionCreatureClipKind.GivingUpTheChase: return "Giving up the chase";
                case DimensionCreatureClipKind.WindingUpToRun: return "Winding up to run";
                case DimensionCreatureClipKind.SkiddingToAHalt: return "Skidding to a halt";
                case DimensionCreatureClipKind.HittingAWall: return "Hitting a wall";
                case DimensionCreatureClipKind.AboutToBeOpen: return "About to be open to attack";
                case DimensionCreatureClipKind.OpenToAttack: return "Open to attack";
                case DimensionCreatureClipKind.ClosingUpAgain: return "Closing up again";
                case DimensionCreatureClipKind.AboutToBlowUp: return "About to blow up";
                case DimensionCreatureClipKind.Vanishing: return "Vanishing";
                case DimensionCreatureClipKind.Reappearing: return "Reappearing";
                case DimensionCreatureClipKind.Lobbing: return "Lobbing something";
                case DimensionCreatureClipKind.StartingToChannel: return "Starting to channel";
                case DimensionCreatureClipKind.Channelling: return "Channelling";
                case DimensionCreatureClipKind.AboutToHatch: return "About to hatch";
                case DimensionCreatureClipKind.Hatching: return "Hatching";
                case DimensionCreatureClipKind.Hatched: return "Hatched";
                case DimensionCreatureClipKind.SizingYouUp: return "Sizing you up";
                case DimensionCreatureClipKind.BeingHurt: return "Being hurt";
                case DimensionCreatureClipKind.FiringABeam: return "Firing a beam";
                case DimensionCreatureClipKind.PlayingDead: return "Playing dead";
                case DimensionCreatureClipKind.GettingBackUp: return "Getting back up";
                case DimensionCreatureClipKind.HeadingIntoABush: return "Heading into a bush";
                case DimensionCreatureClipKind.HidingInABush: return "Hiding in a bush";
                case DimensionCreatureClipKind.PeekingOut: return "Peeking out";
                case DimensionCreatureClipKind.LeavingTheBush: return "Leaving the bush";
                default: return kind.ToString();
            }
        }

        /// <summary>The number the game sends for this kind.</summary>
        public static int HashFor(DimensionCreatureClipKind kind)
        {
            string name = AnimationNameFor(kind);
            return string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name);
        }

        /// <summary>
        /// Whether the game's own creatures repeat this one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Measured off caveling.asset and slimeBlob_orange.asset: standing, walking, being fast
        /// asleep and chattering repeat; everything else plays once and hands back.
        /// </para>
        /// <para>
        /// Four of the later kinds repeat for a different reason — the state system holds the
        /// creature there for as long as it likes and only triggers the next name when it is done,
        /// so a clip that played through once would leave the creature standing while it is meant
        /// to still be doing the thing. <c>HealOtherEntityStateSystem</c> holds "channeling" for
        /// the whole heal; <c>VulnerableStateSystem</c> holds "vulnerable" until the window shuts;
        /// <c>BushStateSystem</c> re-triggers "bush" after every peek; and
        /// <c>IdleInCombatStateSystem</c> holds "idleCombat" while the creature waits out of reach.
        /// </para>
        /// </remarks>
        public static bool RepeatsByDefault(DimensionCreatureClipKind kind)
        {
            return kind == DimensionCreatureClipKind.Standing ||
                kind == DimensionCreatureClipKind.Walking ||
                kind == DimensionCreatureClipKind.FastAsleep ||
                kind == DimensionCreatureClipKind.Talking ||
                kind == DimensionCreatureClipKind.Channelling ||
                kind == DimensionCreatureClipKind.OpenToAttack ||
                kind == DimensionCreatureClipKind.HidingInABush ||
                kind == DimensionCreatureClipKind.SizingYouUp;
        }

        /// <summary>The name of the game shadow sprite behind a size, or empty for no shadow.</summary>
        /// <remarks>
        /// Medium answers with an empty name deliberately. The game's shared Shadow asset stores
        /// <c>shadow_10x4</c> as its plain, unnamed sprite rather than as a named size, so asking
        /// for it BY name finds nothing; an empty name is what selects it.
        /// </remarks>
        public static string ShadowVariantNameFor(DimensionCreatureShadowSize size)
        {
            switch (size)
            {
                case DimensionCreatureShadowSize.Tiny: return "shadow_4x3";
                case DimensionCreatureShadowSize.Small: return "shadow_6x4";
                case DimensionCreatureShadowSize.Medium: return string.Empty;
                case DimensionCreatureShadowSize.Large: return "shadow_16x6";
                case DimensionCreatureShadowSize.Huge: return "shadow_32x25";
                case DimensionCreatureShadowSize.Soft: return "shadow_blur_14x12";
                case DimensionCreatureShadowSize.BigAndSoft: return "shadow_blur_24x22";
                default: return string.Empty;
            }
        }

        /// <summary>The number the shadow sprite's size is stored as. Zero means the plain one.</summary>
        public static int ShadowVariantHashFor(DimensionCreatureShadowSize size)
        {
            string name = ShadowVariantNameFor(size);
            return string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name);
        }

        /// <summary>The address of the game's shared shadow sprite, low half.</summary>
        /// <remarks>
        /// A shadow is not art a mod ships: every creature in Core Keeper points at one shared
        /// asset and picks a size out of it. These two numbers are that asset's address, read
        /// straight off <c>Data/SpriteAsset/Shadow.asset</c>.
        /// </remarks>
        public const long SharedShadowAddressLow = -5209509996517173420L;

        /// <summary>The address of the game's shared shadow sprite, high half.</summary>
        public const long SharedShadowAddressHigh = 6427741878288745627L;

        /// <summary>The most events one sprite asset can carry.</summary>
        /// <remarks>
        /// Not a policy: a frame's events are a 32-bit mask, so event 33 has no bit to live in.
        /// </remarks>
        public const int MaximumEvents = 32;
    }

    /// <summary>
    /// One strip of pictures and how it plays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A STRIP IS ONE ROW, ALWAYS. Core Keeper slices a clip's texture by width only — frame n is
    /// the nth slice of <c>width / frames</c> pixels. A sheet stacked in rows cannot be read at all,
    /// and there is no setting that makes it work.
    /// </para>
    /// <para>
    /// The two extra strips are the same motion seen from behind and from the side. There is
    /// deliberately no strip for facing the camera: that is what the main strip IS, and the game
    /// asks for it by asking for no variant at all.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureClipTemplate
    {
        [Tooltip("What the creature is doing.")]
        [SerializeField] private DimensionCreatureClipKind kind = DimensionCreatureClipKind.Standing;

        [Tooltip("The pictures, side by side in one row, facing the camera.")]
        [SerializeField] private Texture2D strip;

        [Tooltip("The same motion seen from behind. Optional.")]
        [SerializeField] private Texture2D stripFromBehind;

        [Tooltip("The same motion seen from the side, facing right. Optional.")]
        [SerializeField] private Texture2D stripFromTheSide;

        [Tooltip("How many pictures are in the row. Leave at 0 to work it out from the strip.")]
        [SerializeField] private int frames;

        [Tooltip("Pictures per second.")]
        [SerializeField] private float speed = 10f;

        [Tooltip("Whether it repeats.")]
        [SerializeField] private DimensionClipRepeat repeats = DimensionClipRepeat.SameAsTheGame;

        [Tooltip("Extra ticks to linger on each picture, one number per picture. Leave empty for none.")]
        [SerializeField] private int[] holdEachPictureFor = new int[0];

        [Tooltip("Names for the moments in this clip that make a noise: the frame a foot lands, " +
                 "a claw connects, wings beat.")]
        [SerializeField] private string[] momentNames = new string[0];

        [Tooltip("Which picture each moment happens on, counting from 0.")]
        [SerializeField] private int[] momentPictures = new int[0];

        [Tooltip("The sound each moment plays, by name. Leave one empty for a silent moment.")]
        [SerializeField] private string[] momentSounds = new string[0];

        [SerializeField] private bool enabled = true;

        public DimensionCreatureClipKind Kind
        {
            get { return kind; }
        }

        public Texture2D Strip
        {
            get { return strip; }
        }

        public Texture2D StripFromBehind
        {
            get { return stripFromBehind; }
        }

        public Texture2D StripFromTheSide
        {
            get { return stripFromTheSide; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>Pictures per second, never zero — a zero would divide by nothing at playback.</summary>
        public float Speed
        {
            get { return speed <= 0f ? 10f : speed; }
        }

        /// <summary>The Core Keeper animation name this clip will carry.</summary>
        public string AnimationName
        {
            get { return DimensionCreatureAnimationNames.AnimationNameFor(kind); }
        }

        public bool Repeats
        {
            get
            {
                if (repeats == DimensionClipRepeat.KeepsGoing)
                {
                    return true;
                }

                if (repeats == DimensionClipRepeat.PlaysOnce)
                {
                    return false;
                }

                return DimensionCreatureAnimationNames.RepeatsByDefault(kind);
            }
        }

        /// <summary>
        /// How many pictures the strip holds.
        /// </summary>
        /// <remarks>
        /// An unset count is worked out the way the game itself works one out for a new clip:
        /// width divided by height, rounded up. That is right for the square-ish frames creature
        /// art uses and wrong for nothing an author is likely to draw — but it is a guess, so an
        /// author who draws wide frames sets the number instead.
        /// </remarks>
        public int FrameCount
        {
            get
            {
                if (frames > 0)
                {
                    return frames;
                }

                if (strip == null || strip.height <= 0)
                {
                    return 1;
                }

                return Mathf.Max(1, Mathf.CeilToInt((float)strip.width / strip.height));
            }
        }

        /// <summary>The extra ticks per picture, always exactly one number per picture.</summary>
        public int[] HoldFrames
        {
            get
            {
                int count = FrameCount;
                int[] result = new int[count];
                if (holdEachPictureFor == null)
                {
                    return result;
                }

                for (int i = 0; i < count && i < holdEachPictureFor.Length; i++)
                {
                    result[i] = holdEachPictureFor[i] < 0 ? 0 : holdEachPictureFor[i];
                }

                return result;
            }
        }

        /// <summary>Whether the author gave a hold list that does not match the picture count.</summary>
        public bool HoldListIsTheWrongLength
        {
            get
            {
                return holdEachPictureFor != null &&
                    holdEachPictureFor.Length > 0 &&
                    holdEachPictureFor.Length != FrameCount;
            }
        }

        public string[] MomentNames
        {
            get { return momentNames ?? new string[0]; }
        }

        public int[] MomentPictures
        {
            get { return momentPictures ?? new int[0]; }
        }

        public string[] MomentSounds
        {
            get { return momentSounds ?? new string[0]; }
        }

        /// <summary>Whether a moment names no picture, or names one the strip does not have.</summary>
        public bool HasAMomentOffTheEndOfTheStrip
        {
            get
            {
                string[] names = MomentNames;
                int[] pictures = MomentPictures;
                int count = FrameCount;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.IsNullOrEmpty(names[i]))
                    {
                        continue;
                    }

                    int picture = i < pictures.Length ? pictures[i] : 0;
                    if (picture < 0 || picture >= count)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Whether a direction was drawn but the clip itself was not.</summary>
        public bool HasADirectionWithoutABaseStrip
        {
            get
            {
                return strip == null &&
                    (stripFromBehind != null || stripFromTheSide != null);
            }
        }
    }

    /// <summary>
    /// Everything about how a creature looks while it moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A LIST OF CLIPS AND NOT ONE PICTURE. Core Keeper drives a creature's art by sending the
    /// name of what it is doing — the same name for a caveling and for a boss. A creature with one
    /// still picture receives all of those and can answer none of them, which is why every mob this
    /// framework generated before this template existed stood frozen, and mostly invisible.
    /// </para>
    /// <para>
    /// Nothing here is required. A creature with no clips at all still generates; it simply has no
    /// body, and the generator says so.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureAnimationTemplate
    {
        [Tooltip("What the creature is seen doing, one row of pictures per thing.")]
        [SerializeField] private DimensionCreatureClipTemplate[] clips = new DimensionCreatureClipTemplate[0];

        [Tooltip("The shadow it casts.")]
        [SerializeField] private DimensionCreatureShadowSize shadow = DimensionCreatureShadowSize.Medium;

        [Tooltip("Whether it turns to face the way it is walking. Off for anything that never turns.")]
        [SerializeField] private bool turnsToFaceWhereItGoes = true;

        [Tooltip("Whether it flashes white when hit, the way the game's creatures do.")]
        [SerializeField] private bool flashesWhenHit = true;

        [Tooltip("The material its body draws with. Leave empty for the game's standard lit one.")]
        [SerializeField] private Material bodyMaterial;

        [Tooltip("Notes for whoever edits this next. Never reaches the game.")]
        [SerializeField] private string notes = string.Empty;

        public DimensionCreatureClipTemplate[] Clips
        {
            get { return clips ?? new DimensionCreatureClipTemplate[0]; }
        }

        public DimensionCreatureShadowSize Shadow
        {
            get { return shadow; }
        }

        public bool TurnsToFaceWhereItGoes
        {
            get { return turnsToFaceWhereItGoes; }
        }

        public bool FlashesWhenHit
        {
            get { return flashesWhenHit; }
        }

        public Material BodyMaterial
        {
            get { return bodyMaterial; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        /// <summary>Whether anything here would produce a body at all.</summary>
        public bool HasAnyClip
        {
            get
            {
                DimensionCreatureClipTemplate[] all = Clips;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].Enabled && all[i].Strip != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Whether the same thing is described twice, which would lose one of them.</summary>
        /// <remarks>
        /// A sprite asset keeps the FIRST clip under a name and silently drops later ones, so a
        /// second "Walking" is not an override — it is art that never plays.
        /// </remarks>
        public bool HasDuplicateClips
        {
            get
            {
                DimensionCreatureClipTemplate[] all = Clips;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || !all[i].Enabled)
                    {
                        continue;
                    }

                    for (int j = i + 1; j < all.Length; j++)
                    {
                        if (all[j] != null && all[j].Enabled && all[j].Kind == all[i].Kind)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>Every distinct moment name across every clip, in the order they were written.</summary>
        /// <remarks>
        /// The order IS the storage: a moment's position in this list is the bit it occupies in
        /// each frame's mask, so reordering it would move which frames fire which sound.
        /// </remarks>
        public string[] CollectMomentNames()
        {
            System.Collections.Generic.List<string> ordered =
                new System.Collections.Generic.List<string>();
            DimensionCreatureClipTemplate[] all = Clips;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].Enabled)
                {
                    continue;
                }

                string[] names = all[i].MomentNames;
                for (int n = 0; n < names.Length; n++)
                {
                    if (string.IsNullOrEmpty(names[n]) || ordered.Contains(names[n]))
                    {
                        continue;
                    }

                    ordered.Add(names[n]);
                }
            }

            return ordered.ToArray();
        }

        /// <summary>
        /// The sound each collected moment plays, one per name, in the same order.
        /// </summary>
        /// <remarks>
        /// A moment name reused across clips keeps the FIRST sound anyone gave it. The name is a
        /// single bit shared by every clip that mentions it, so it can only have one sound — and
        /// the alternative, letting the last clip in the list win, would mean reordering the clips
        /// changed what a footstep sounds like.
        /// </remarks>
        public string[] CollectMomentSounds()
        {
            string[] names = CollectMomentNames();
            string[] sounds = new string[names.Length];
            DimensionCreatureClipTemplate[] all = Clips;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].Enabled)
                {
                    continue;
                }

                string[] clipNames = all[i].MomentNames;
                string[] clipSounds = all[i].MomentSounds;
                for (int n = 0; n < clipNames.Length; n++)
                {
                    if (string.IsNullOrEmpty(clipNames[n]) || n >= clipSounds.Length ||
                        string.IsNullOrEmpty(clipSounds[n]))
                    {
                        continue;
                    }

                    int slot = System.Array.IndexOf(names, clipNames[n]);
                    if (slot >= 0 && string.IsNullOrEmpty(sounds[slot]))
                    {
                        sounds[slot] = clipSounds[n];
                    }
                }
            }

            for (int i = 0; i < sounds.Length; i++)
            {
                if (sounds[i] == null)
                {
                    sounds[i] = string.Empty;
                }
            }

            return sounds;
        }

        /// <summary>Whether more moments were named than a frame has bits to record.</summary>
        public bool HasTooManyMoments
        {
            get
            {
                return CollectMomentNames().Length > DimensionCreatureAnimationNames.MaximumEvents;
            }
        }

        /// <summary>The clip for a kind, or null when the author did not draw one.</summary>
        public DimensionCreatureClipTemplate ClipFor(DimensionCreatureClipKind kind)
        {
            DimensionCreatureClipTemplate[] all = Clips;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].Enabled && all[i].Kind == kind && all[i].Strip != null)
                {
                    return all[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Whether a clip repeats, once the rest of the list is taken into account.
        /// </summary>
        /// <remarks>
        /// Falling asleep is the one clip whose answer depends on its neighbours. With a fast
        /// asleep clip beside it, it plays once and hands over; without one, it has to repeat or
        /// the creature would finish lying down and then have nothing to be doing for the whole
        /// minute it is meant to be asleep.
        /// </remarks>
        public bool RepeatsFor(DimensionCreatureClipTemplate clip)
        {
            if (clip == null)
            {
                return false;
            }

            if (clip.Kind == DimensionCreatureClipKind.Sleeping &&
                ClipFor(DimensionCreatureClipKind.FastAsleep) == null)
            {
                return true;
            }

            return clip.Repeats;
        }

        /// <summary>
        /// What a clip hands over to when it finishes, or empty when it holds where it is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE CHAIN IS DATA, NOT CODE. No server system ever asks a creature to go back to
        /// standing after a swing; the sprite asset does it, by naming the clip to play next.
        /// Sleeping is the case that proves it: the server only ever sends "sleep" and "wakeUp",
        /// and the long stretch of lying there comes from sleep handing over to being fast asleep,
        /// exactly as the caveling's own asset does it.
        /// </para>
        /// <para>
        /// Dying deliberately hands over to nothing. A death clip that returned to standing would
        /// leave a corpse breathing for the second before the game hides it.
        /// </para>
        /// </remarks>
        public DimensionCreatureClipKind? ExitFor(DimensionCreatureClipKind kind)
        {
            if (kind == DimensionCreatureClipKind.Dying)
            {
                return null;
            }

            if (kind == DimensionCreatureClipKind.Sleeping)
            {
                return ClipFor(DimensionCreatureClipKind.FastAsleep) != null
                    ? DimensionCreatureClipKind.FastAsleep
                    : (DimensionCreatureClipKind?)null;
            }

            if (kind == DimensionCreatureClipKind.FastAsleep)
            {
                return ClipFor(DimensionCreatureClipKind.WakingUp) != null
                    ? DimensionCreatureClipKind.WakingUp
                    : (DimensionCreatureClipKind?)null;
            }

            // THE SIX GAPS A STATE SYSTEM LEAVES OPEN. Each of these is a short clip the game
            // plays at the START of a stretch it then holds the creature in for a timer's worth of
            // seconds before triggering the next name itself. The generic answer below — hand back
            // to standing — fills that stretch with the creature standing still in the middle of
            // channelling a heal, dropping its guard, or burrowing into a bush. Handing over to the
            // clip that IS the stretch is what the game's own assets do.
            //
            // Only these six. "vulnerable", "bush", "channeling" and "idleCombat" are NOT chained
            // onward, because the system triggers what comes after them on its own schedule and a
            // chain would end the look early; they repeat instead (see RepeatsByDefault). Chained
            // conditionally, exactly like sleeping above: a chain to a clip the author never drew
            // writes a guid that resolves to nothing.
            DimensionCreatureClipKind? staged = StagedExitFor(kind);
            if (staged.HasValue && ClipFor(kind) != null)
            {
                return ClipFor(staged.Value) != null ? staged : (DimensionCreatureClipKind?)null;
            }

            DimensionCreatureClipTemplate clip = ClipFor(kind);
            if (clip == null || clip.Repeats)
            {
                return null;
            }

            return ClipFor(DimensionCreatureClipKind.Standing) != null
                ? DimensionCreatureClipKind.Standing
                : (DimensionCreatureClipKind?)null;
        }

        /// <summary>
        /// The clip that carries on where a staged one leaves off, or nothing when the kind is not
        /// the opening beat of a stage.
        /// </summary>
        /// <remarks>
        /// Read off the systems that drive the stages, not chosen:
        /// <c>VulnerableStateSystem.cs:116</c> fires "prevulnerable" and only fires "vulnerable" at
        /// <c>:126</c> once the anticipation timer elapses;
        /// <c>HealOtherEntityStateSystem.cs:33-34</c> is the same shape for "startChanneling" and
        /// "channeling"; <c>HatchWhenPlayerNearbyStateSystem.cs:51-53</c> steps "isHatching",
        /// "hatch", "hasHatched" on its own timers; and <c>BushStateSystem.cs:166</c> fires
        /// "goToBush", then holds for the go-to-bush duration before firing "bush" at <c>:178</c>.
        /// Peeking is the odd one out and goes BACK: <c>:184</c> fires "peak" and <c>:185</c> sets
        /// the next stage to being in the bush again, so a peek that handed back to standing would
        /// show the creature in the open while the game has it hidden.
        /// </remarks>
        private static DimensionCreatureClipKind? StagedExitFor(DimensionCreatureClipKind kind)
        {
            switch (kind)
            {
                case DimensionCreatureClipKind.AboutToBeOpen:
                    return DimensionCreatureClipKind.OpenToAttack;
                case DimensionCreatureClipKind.StartingToChannel:
                    return DimensionCreatureClipKind.Channelling;
                case DimensionCreatureClipKind.AboutToHatch:
                    return DimensionCreatureClipKind.Hatching;
                case DimensionCreatureClipKind.Hatching:
                    return DimensionCreatureClipKind.Hatched;
                case DimensionCreatureClipKind.HeadingIntoABush:
                    return DimensionCreatureClipKind.HidingInABush;
                case DimensionCreatureClipKind.PeekingOut:
                    return DimensionCreatureClipKind.HidingInABush;
                default:
                    return null;
            }
        }
    }
}
