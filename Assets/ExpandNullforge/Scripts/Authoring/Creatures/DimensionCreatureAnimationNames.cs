using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
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
}
