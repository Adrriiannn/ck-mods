using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The things an object simply is, or simply does — each one a single yes or no.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERY SETTING HERE IS A TICKBOX AND NOTHING ELSE. In the game these are components that hold
    /// no values at all: the component being on the object IS the whole setting. Thirty of them,
    /// asked about together, because separately each would be a panel with one tickbox in it.
    /// </para>
    /// <para>
    /// A FEW OF THEM ONLY WORK FOR THE GAME'S OWN OBJECT. Core Keeper has a handful of systems that
    /// check the object's identity before they do anything — the Slime King's shot cycling asks
    /// "is this the Lava Slime Boss?" and skips everything else. Those are marked below and warn
    /// when generated, because a tickbox that quietly does nothing is worse than one that is not
    /// there.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSimpleTraitsTemplate
    {
        [Header("Being hit")]
        [Tooltip("A melee weapon can hit it even though it is something you would normally mine.")]
        [SerializeField] private bool canBeHitWithAWeaponNotJustATool;

        [Tooltip("Hitting it does not count as connecting — no recoil, and the swing carries on.")]
        [SerializeField] private bool hittingItDoesNotCountAsAHit;

        [Tooltip("It can still be hurt while standing on ground that normally makes things immune.")]
        [SerializeField] private bool immuneGroundDoesNotProtectIt;

        [Tooltip("Its map marker disappears the moment it dies.")]
        [SerializeField] private bool itsMapMarkerGoesWhenItDies;

        [Header("How it sits in the world")]
        [Tooltip("A player can dig the ground out from under it.")]
        [SerializeField] private bool doesNotBlockDigging;

        [Tooltip("Nothing pushes it and it pushes nothing — it just sits there.")]
        [SerializeField] private bool hasNoPhysics;

        [Tooltip("Its movement is smoothed between ticks so it does not look like it is stepping.")]
        [SerializeField] private bool movesSmoothly;

        [Tooltip("It has no position at all. For things that are only ever data.")]
        [SerializeField] private bool hasNoPositionOfItsOwn;

        [Tooltip("If its spot stops being a legal place to stand, it disappears.")]
        [SerializeField] private bool disappearsIfItsSpotStopsBeingValid;

        [Tooltip("A lever or switch wired to it flips it to its other look — how wired doors work.")]
        [SerializeField] private bool aTriggerFlipsItsLook;

        [Tooltip("Dropped on the floor, it stacks together with matching drops instead of piling up.")]
        [SerializeField] private bool droppedCopiesMergeTogether;

        [Tooltip("It is a piece of a handmade room, placed as part of that room rather than on its own.")]
        [SerializeField] private bool isPartOfAHandmadeRoom;

        [Header("What it is")]
        [Tooltip("It is a trash can: anything put in it is gone.")]
        [SerializeField] private bool isATrashCan;

        [Tooltip("It waters every tilled tile within two tiles of it, every second or two.")]
        [SerializeField] private bool isASprinkler;

        [Tooltip("It is bait on a pole — the thing fish are drawn to.")]
        [SerializeField] private bool isBaitOnAPole;

        [Tooltip("It is a cherry tree, which is its own small thing the game keeps track of.")]
        [SerializeField] private bool isACherryTree;

        [Tooltip("It is a shoal of fish rather than one fish.")]
        [SerializeField] private bool isAShoalOfFish;

        [Tooltip("It can live inside a tank or terrarium.")]
        [SerializeField] private bool canLiveInATank;

        [Tooltip("It is a plant partway through growing.")]
        [SerializeField] private bool isAGrowingPlant;

        [Tooltip("It is a trail left behind by something else, and remembers who left it.")]
        [SerializeField] private bool isATrailSomethingLeftBehind;

        [Header("Creatures and pets")]
        [Tooltip("Two of them can be bred, and the pairing can be turned off per creature.")]
        [SerializeField] private bool canBeBred;

        [Tooltip("It follows pheromone trails, the way a caveling follows its own kind.")]
        [SerializeField] private bool followsPheromoneTrails;

        [Tooltip("It is a pet's home and remembers whose pet lives there — a pet bed does this.")]
        [SerializeField] private bool isAPetsHome;

        [Tooltip("It carries a pet's chosen look.")]
        [SerializeField] private bool carriesAPetsLook;

        [Tooltip("It is a minion — something summoned that fights for whoever summoned it.")]
        [SerializeField] private bool isAMinion;

        [Header("Player things")]
        [Tooltip("It keeps souls, the way a player's ghost does.")]
        [SerializeField] private bool keepsSouls;

        [Tooltip("It can play a music sheet on an instrument.")]
        [SerializeField] private bool canPlayInstruments;

        [Tooltip("It wears equipment, so it needs equipment slots.")]
        [SerializeField] private bool wearsEquipment;

        [Tooltip("It remembers who it has been fighting.")]
        [SerializeField] private bool remembersWhoItFights;

        [Tooltip("It counts towards achievements.")]
        [SerializeField] private bool countsTowardsAchievements;

        [Tooltip("It can carry affixes — the extra rolled properties on rare gear.")]
        [SerializeField] private bool canCarryAffixes;

        [Tooltip("A legendary in this slot skips the usual slot requirements.")]
        [SerializeField] private bool legendariesIgnoreSlotRulesHere;

        [Tooltip("On a controller with adjustable triggers, its triggers push back as it is used.")]
        [SerializeField] private bool theTriggersPushBackWhenUsed;

        [Header("Being sent over the network")]
        [Tooltip("Each player is told which biomes are near them, rather than all of them.")]
        [SerializeField] private bool eachPlayerGetsItsOwnBiomeSamples;

        [Tooltip("Each player is sent their own slice of the map, rather than the whole thing.")]
        [SerializeField] private bool eachPlayerGetsItsOwnSliceOfTheMap;

        [Tooltip("Its movement is smoothed for onlookers once it has finished spawning.")]
        [SerializeField] private bool smoothsItselfOnceItHasSpawned;

        [Tooltip("It is given a character id of its own the first time it appears.")]
        [SerializeField] private bool getsACharacterIdOfItsOwn;

        [Tooltip("It is given a player id of its own the first time it appears.")]
        [SerializeField] private bool getsAPlayerIdOfItsOwn;

        [Tooltip("It is a floating damage number — the little figure that rises off something hit.")]
        [SerializeField] private bool isAFloatingDamageNumber;

        [Header("Showing it on the debug map")]
        [Tooltip("It is drawn on the world explorer, the map view used for looking at generation.")]
        [SerializeField] private bool showItOnTheDebugMap;

        [Tooltip("Drawn as a circle rather than a box.")]
        [SerializeField] private bool drawnAsACircle = true;

        [Tooltip("What colour it is drawn in.")]
        [SerializeField] private Color debugMapColour = Color.red;

        [Tooltip("How big the mark is, in tiles.")]
        [Min(0)]
        [SerializeField] private int debugMapRadius = 2;

        [Tooltip("Its name is written beside the mark.")]
        [SerializeField] private bool debugMapShowsItsName;

        [Tooltip("It is still drawn while it is switched off, in a second colour.")]
        [SerializeField] private bool debugMapShowsItWhileSwitchedOff;

        [Tooltip("The colour used while it is switched off.")]
        [SerializeField] private Color debugMapSwitchedOffColour = Color.gray;

        public bool EachPlayerGetsItsOwnBiomeSamples
        {
            get { return eachPlayerGetsItsOwnBiomeSamples; }
        }

        public bool EachPlayerGetsItsOwnSliceOfTheMap
        {
            get { return eachPlayerGetsItsOwnSliceOfTheMap; }
        }

        public bool SmoothsItselfOnceItHasSpawned { get { return smoothsItselfOnceItHasSpawned; } }

        public bool GetsACharacterIdOfItsOwn { get { return getsACharacterIdOfItsOwn; } }

        public bool GetsAPlayerIdOfItsOwn { get { return getsAPlayerIdOfItsOwn; } }

        public bool IsAFloatingDamageNumber { get { return isAFloatingDamageNumber; } }

        public bool ShowItOnTheDebugMap { get { return showItOnTheDebugMap; } }

        public bool DrawnAsACircle { get { return drawnAsACircle; } }

        public Color DebugMapColour { get { return debugMapColour; } }

        public int DebugMapRadius { get { return debugMapRadius < 0 ? 0 : debugMapRadius; } }

        public bool DebugMapShowsItsName { get { return debugMapShowsItsName; } }

        public bool DebugMapShowsItWhileSwitchedOff
        {
            get { return debugMapShowsItWhileSwitchedOff; }
        }

        public Color DebugMapSwitchedOffColour { get { return debugMapSwitchedOffColour; } }

        /// <summary>Drawn on the debug map with no size, so the mark would be invisible.</summary>
        public bool DebugMarkIsInvisible
        {
            get { return showItOnTheDebugMap && debugMapRadius <= 0; }
        }

        public bool CanBeHitWithAWeaponNotJustATool { get { return canBeHitWithAWeaponNotJustATool; } }

        public bool HittingItDoesNotCountAsAHit { get { return hittingItDoesNotCountAsAHit; } }

        public bool ImmuneGroundDoesNotProtectIt { get { return immuneGroundDoesNotProtectIt; } }

        public bool ItsMapMarkerGoesWhenItDies { get { return itsMapMarkerGoesWhenItDies; } }

        public bool DoesNotBlockDigging { get { return doesNotBlockDigging; } }

        public bool HasNoPhysics { get { return hasNoPhysics; } }

        public bool MovesSmoothly { get { return movesSmoothly; } }

        public bool HasNoPositionOfItsOwn { get { return hasNoPositionOfItsOwn; } }

        public bool DisappearsIfItsSpotStopsBeingValid
        {
            get { return disappearsIfItsSpotStopsBeingValid; }
        }

        public bool ATriggerFlipsItsLook { get { return aTriggerFlipsItsLook; } }

        public bool DroppedCopiesMergeTogether { get { return droppedCopiesMergeTogether; } }

        public bool IsPartOfAHandmadeRoom { get { return isPartOfAHandmadeRoom; } }

        public bool IsATrashCan { get { return isATrashCan; } }

        public bool IsASprinkler { get { return isASprinkler; } }

        public bool IsBaitOnAPole { get { return isBaitOnAPole; } }

        public bool IsACherryTree { get { return isACherryTree; } }

        public bool IsAShoalOfFish { get { return isAShoalOfFish; } }

        public bool CanLiveInATank { get { return canLiveInATank; } }

        public bool IsAGrowingPlant { get { return isAGrowingPlant; } }

        public bool IsATrailSomethingLeftBehind { get { return isATrailSomethingLeftBehind; } }

        public bool CanBeBred { get { return canBeBred; } }

        public bool FollowsPheromoneTrails { get { return followsPheromoneTrails; } }

        public bool IsAPetsHome { get { return isAPetsHome; } }

        public bool CarriesAPetsLook { get { return carriesAPetsLook; } }

        public bool IsAMinion { get { return isAMinion; } }

        public bool KeepsSouls { get { return keepsSouls; } }

        public bool CanPlayInstruments { get { return canPlayInstruments; } }

        public bool WearsEquipment { get { return wearsEquipment; } }

        public bool RemembersWhoItFights { get { return remembersWhoItFights; } }

        public bool CountsTowardsAchievements { get { return countsTowardsAchievements; } }

        public bool CanCarryAffixes { get { return canCarryAffixes; } }

        public bool LegendariesIgnoreSlotRulesHere { get { return legendariesIgnoreSlotRulesHere; } }

        public bool TheTriggersPushBackWhenUsed { get { return theTriggersPushBackWhenUsed; } }

        /// <summary>
        /// A cherry tree that is part of a handmade room is skipped by the counting, so ticking
        /// both means the tree is there but counts towards nothing.
        /// </summary>
        public bool CherryTreeInAHandmadeRoomIsNotCounted
        {
            get { return isACherryTree && isPartOfAHandmadeRoom; }
        }
    }
}
