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
}
