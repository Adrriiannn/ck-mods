using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What sort of placed thing this is.
    /// </summary>
    /// <remarks>
    /// These are the shapes Core Keeper actually distinguishes with a component of their own.
    /// Everything else a player puts down — a chair, a rug, a statue, a pot — is a
    /// <see cref="Decoration"/> as far as the game is concerned, and differs only in its art and its
    /// footprint. Inventing more kinds than the game has would be the invented-toughness-tiers mistake
    /// again.
    /// </remarks>
    public enum DimensionWorldObjectKind
    {
        /// <summary>Something you place that just sits there. Most objects in the game.</summary>
        Decoration = 0,

        /// <summary>A door. It swaps its collider as it opens and closes. (34 vanilla prefabs)</summary>
        Door = 1,

        /// <summary>A bed a player can claim and sleep in.</summary>
        Bed = 2,

        /// <summary>A trophy that summons the enemy it commemorates. (83)</summary>
        Trophy = 3
    }

    /// <summary>
    /// Something a player places in the world that is not a container, a station, a plant or a
    /// creature.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THIS COVERS. The long tail — doors, lights, beds, trophies, and the great mass of ordinary
    /// decoration. They share almost all of their components; what differs is a handful of small
    /// markers on top of the spine every placed object has.
    /// </para>
    /// <para>
    /// LIGHTING, CAREFULLY. Core Keeper has three separate things that all sound like "it glows", and
    /// they are not interchangeable — a torch carries two of them and NOT the third:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Lights the room while held</b> — <c>ActAsLightSourceWhenHeldInHand</c>, a colour and a
    /// range. This is what a carried torch does.</item>
    /// <item><b>Lights the room while placed</b> — <c>TableItemLightSource</c>. This is what a torch
    /// on a wall does.</item>
    /// <item><b>The object itself glows</b> — <c>GlowLight</c>, a colour and an intensity. Measured
    /// across the game, this sits on weapons and armour that shine, and <b>a torch does not carry
    /// it</b>. It tints the object; it does not light anything around it.</item>
    /// </list>
    /// <para>
    /// They are asked separately below for that reason. Rolling them into one "is a light" tick would
    /// produce lamps that glow prettily and leave the room pitch dark.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/World Object")]
    public sealed class DimensionWorldObjectAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string objectIdentifier = "worldobject";

        [Tooltip("What players see it called.")]
        [SerializeField] private string displayName = "Object";

        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;

        [Tooltip("Its rarity colour in the world and in tooltips.")]
        [SerializeField] private string rarityId = string.Empty;

        [Header("Look")]
        [Tooltip("The picture of it standing in the world.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("Its icon in inventories. Falls back to the sprite when empty.")]
        [SerializeField] private Sprite icon;

        [Header("What it is")]
        [SerializeField] private DimensionWorldObjectKind kind = DimensionWorldObjectKind.Decoration;

        [Tooltip("The enemy this trophy summons. Trophies only.")]
        [SerializeField] private string summonsEnemyId = string.Empty;

        [Header("Placement")]
        [Tooltip("How many tiles it occupies.")]
        [SerializeField] private Vector2Int tileSize = Vector2Int.one;

        [Tooltip("Which of those tiles it stands on, counted from the bottom-left one. " +
            "Leave it at zero unless the thing hangs off its own tile, the way a planter box " +
            "reaches one tile to the left of where it was put down.")]
        [SerializeField] private Vector2Int standsOnTile = Vector2Int.zero;

        [Tooltip("Faces the way the player was looking when it was placed.")]
        [SerializeField] private bool facesPlacementDirection = true;

        [Tooltip("Can be placed on water.")]
        [SerializeField] private bool canBePlacedOnWater;

        [Tooltip("Can take a paint bucket.")]
        [SerializeField] private bool paintable = true;

        [Tooltip("Drawn in front of other things sharing its tile. Higher wins.")]
        [SerializeField] private int surfacePriority;

        [Header("Light")]
        [Tooltip("The light it throws on the floor where it stands. This is what a wall torch, a " +
                 "lamp and a campfire do.")]
        [SerializeField] private DimensionEmittedLightTemplate emittedLight =
            new DimensionEmittedLightTemplate();

        // RELABELLED, NOT REWIRED. This tick writes TableItemLightSourceAuthoring, whose only
        // reader in the whole game is Table.UpdateGlowingObject: it lights the TABLE'S own light
        // node for an item lying on the table, and does nothing at all for the object standing in
        // the world. Its old wording, "it lights the area around it once placed. This is what a
        // wall torch does", is the sentence the fold above now actually delivers, and leaving the
        // two side by side would have made one of them a lie. The field, its name and everything
        // it writes are untouched.
        [Tooltip("It glows in its slot when it is set down on a table. It does not light the " +
                 "room. Which colour and how far that glow reaches are answered under how the " +
                 "world treats it.")]
        [SerializeField] private bool lightsTheRoomWhenPlaced;

        [Tooltip("It lights the area around the player while carried. This is what a held torch does.")]
        [SerializeField] private bool lightsTheRoomWhenHeld;

        [Tooltip("Colour of the light it casts while held.")]
        [SerializeField] private Color heldLightColor = new Color(1f, 0.635f, 0f, 1f);

        [Tooltip("How far the light it casts while held reaches.")]
        [Min(0)]
        [SerializeField] private int heldLightRange = 8;

        [Tooltip("The object itself glows. This tints the object and does NOT light the room.")]
        [SerializeField] private bool objectItselfGlows;

        [Tooltip("Colour of that glow.")]
        [SerializeField] private Color glowColor = new Color(1f, 0.635f, 0f, 1f);

        [Tooltip("How strong that glow is.")]
        [Min(0f)]
        [SerializeField] private float glowIntensity = 1f;

        [Header("Breaking it")]
        [Tooltip("How many hits to break it.")]
        [Min(1)]
        [SerializeField] private int hitsToBreak = 2;

        [Tooltip("Nothing can attack it at all — not the player, not a creature.")]
        [SerializeField] private bool cannotBeAttacked;

        [Tooltip("It disappears on its own after this many seconds. 0 means it stays forever.")]
        [Min(0f)]
        [SerializeField] private float disappearsAfterSeconds;

        [Header("What it does for you")]
        [Tooltip("Effects it grants while held, worn or eaten.")]
        [SerializeField] private DimensionItemEffectsTemplate effects = new DimensionItemEffectsTemplate();

        [Header("Hitting and breaking it")]
        [Tooltip("What it sounds like and what comes off it when hit or destroyed.")]
        [SerializeField] private DimensionImpactFeedbackTemplate impactFeedback = new DimensionImpactFeedbackTemplate();

        [Header("What it leaves standing")]
        [Tooltip("A whole object left in the world when it dies. Not loot.")]
        [SerializeField] private DimensionLeavesBehindTemplate leavesBehind = new DimensionLeavesBehindTemplate();

        [Tooltip("Creative mode does not skip its drops.")]
        [SerializeField] private bool alwaysDropsLoot;

        [Header("What it leaves on the tile")]
        [Tooltip("The tile left where it stood, and the cracked version while it is being broken.")]
        [SerializeField] private DimensionTileOutcomeTemplate tileOutcome = new DimensionTileOutcomeTemplate();

        [Header("Right-click")]
        [Tooltip("What the right mouse button does with it.")]
        [SerializeField] private DimensionSecondaryUseTemplate secondaryUse = new DimensionSecondaryUseTemplate();

        [Header("Where it drops from")]
        [Tooltip("Every place this drops. Said from the item's side; the generator inverts it into each source's loot.")]
        [SerializeField] private DimensionDropSource[] dropsFrom = new DimensionDropSource[0];

        [Header("Music near it")]
        [Tooltip("Music that plays when a player comes close. For an arena or a landmark.")]
        [SerializeField] private DimensionMusicAreaTemplate music = new DimensionMusicAreaTemplate();

        [Header("If it is a waypoint")]
        [Tooltip("Players can travel to it. This is what a waypoint statue is.")]
        [SerializeField] private bool playersCanTravelToIt;

        [Header("Other placed kinds")]
        [Tooltip("It is a fence gate — it opens as part of a fence line.")]
        [SerializeField] private bool isAFenceGate;

        [Header("Facing and text")]
        [Tooltip("Which way it faces when first placed.")]
        [SerializeField] private Vector3 initialFacing = new Vector3(0f, 0f, -1f);

        [Tooltip("Its collider turns with it, not just its art.")]
        [SerializeField] private bool itsColliderTurnsToo;

        [Tooltip("Nudges which rotation icon the placement UI shows.")]
        [SerializeField] private int rotationIconOffset;

        [Tooltip("Text it already carries when placed. For a sign or a labelled chest.")]
        [TextArea(1, 3)]
        [SerializeField] private string textItComesWith = string.Empty;

        [Tooltip("It is only untouchable for its first frame, not forever. For something that lands.")]
        [SerializeField] private bool untouchableForOneFrameOnly;

        [Header("Where it sits in the world")]
        [Tooltip("Its world tier and which ways its art faces. The tier drives the stat curves.")]
        [SerializeField] private DimensionObjectBasicsTemplate basics = new DimensionObjectBasicsTemplate();

        [Header("Conditions")]
        [Tooltip("What it starts affected by, and what conditions cannot touch it.")]
        [SerializeField] private DimensionInitialConditionsTemplate conditions = new DimensionInitialConditionsTemplate();

        [Tooltip("It is the flower of this plant. Empty means it is not a flower.")]
        [SerializeField] private string flowerOfPlantId = string.Empty;

        [Min(0)]
        [SerializeField] private int flowerVariation;

        [Tooltip("It is a spawner platform producing this enemy. The simplest dungeon furniture there is.")]
        [SerializeField] private string spawnsEnemyId = string.Empty;

        [Tooltip("How close a player must get to activate it.")]
        [Min(0f)]
        [SerializeField] private float activateWithin = 0.5f;

        [Tooltip("It is the core waypoint — the one a player returns to. Only one should be.")]
        [SerializeField] private bool isTheCoreWaypoint;

        [Header("If it hurts things")]
        [Tooltip("A spike, a drill, a turret. Most of the game's continuous attackers need power.")]
        [SerializeField] private DimensionContinuousAttackTemplate continuousAttack = new DimensionContinuousAttackTemplate();

        [Header("If it keeps things safe")]
        [Tooltip("Nothing can be hurt within this area.")]
        [SerializeField] private bool keepsThingsSafeNearby;

        [Tooltip("How far the safety reaches.")]
        [Min(0f)]
        [SerializeField] private float safeRadius = 6f;

        [Tooltip("The safe area is a rectangle rather than a circle.")]
        [SerializeField] private bool safeAreaIsRectangular;

        [Min(0)]
        [SerializeField] private int safeWidth = 8;

        [Min(0)]
        [SerializeField] private int safeHeight = 8;

        [Header("Automation")]
        [Tooltip("What conveyors, arms, drills and crafters can do with it.")]
        [SerializeField] private DimensionAutomationTemplate automation = new DimensionAutomationTemplate();

        [Header("How the world treats it")]
        [Tooltip("The small rules: automation, ground cover, scanning, where it may stand, the map.")]
        [SerializeField] private DimensionObjectRulesTemplate rules = new DimensionObjectRulesTemplate();

        [Header("Wiring")]
        [SerializeField] private DimensionWiringTemplate wiring = new DimensionWiringTemplate();

        [Tooltip("Where it is allowed to be put down, and how it behaves as the player lines it up.")]
        [SerializeField] private DimensionPlacementRulesTemplate placementRules = new DimensionPlacementRulesTemplate();

        [Tooltip("Loot it drops other than on death: as it is hit, when it is used, or by season.")]
        [SerializeField] private DimensionExtraLootTemplate extraLoot = new DimensionExtraLootTemplate();

        [Tooltip("How it reacts to a melody played near it — the ocarina system.")]
        [SerializeField] private DimensionMelodyResponseTemplate melodyResponse = new DimensionMelodyResponseTemplate();

        [Tooltip("Event terminals, tanks and terrariums, fishing nets, farming machines, recipes.")]
        [SerializeField] private DimensionEventTerminalTemplate eventTerminal = new DimensionEventTerminalTemplate();

        [Tooltip("Seats, reacting to its own wounds, pheromones, boss beams and spawn points.")]
        [SerializeField] private DimensionFinalTouchesTemplate finalTouches = new DimensionFinalTouchesTemplate();

        [Tooltip("Its own beam attack, alerts, dripping items, catching fire, ambient movement.")]
        [SerializeField] private DimensionBeamAndAmbienceTemplate beamAndAmbience = new DimensionBeamAndAmbienceTemplate();

        [Tooltip("Hiding in bushes, eggs that hatch, caveling territories, delayed shots, extra slots.")]
        [SerializeField] private DimensionHidingAndHatchingTemplate hidingAndHatching = new DimensionHidingAndHatchingTemplate();

        [Tooltip("Mana pools and siphons, healing auras, ancient wiring, ownership, boss hooks.")]
        [SerializeField] private DimensionManaAndAuraTemplate manaAndAura = new DimensionManaAndAuraTemplate();

        [Tooltip("Plain spawners, drifting orbs, followers, statues, roamers that chew ground.")]
        [SerializeField] private DimensionSpawnerAndOrbTemplate spawnerAndOrb = new DimensionSpawnerAndOrbTemplate();

        [Tooltip("A chain of explosions, a trail it leaves, timed look changes, water, vending.")]
        [SerializeField] private DimensionChainReactionTemplate chainReaction = new DimensionChainReactionTemplate();

        [Tooltip("Machines that work on their own: drills, automated miners, belt filters, anvils.")]
        [SerializeField] private DimensionMachineRolesTemplate machineRoles = new DimensionMachineRolesTemplate();

        [Tooltip("Going off on contact, taking its ground with it, or standing in for ground.")]
        [SerializeField] private DimensionTerrainEffectTemplate terrainEffects = new DimensionTerrainEffectTemplate();

        [Tooltip("Its look changes to fit the ground around it — bridges, rails, edges.")]
        [SerializeField] private DimensionLooksAtItsSurroundingsTemplate adaptsToSurroundings = new DimensionLooksAtItsSurroundingsTemplate();

        [Tooltip("It keeps producing creatures or objects around itself.")]
        [SerializeField] private DimensionNestTemplate nest = new DimensionNestTemplate();

        [Tooltip("Trading, being money, and belonging to a season.")]
        [SerializeField] private DimensionTraderTemplate trader = new DimensionTraderTemplate();

        [Tooltip("Small roles it plays in a base: sitting, naming, painting, resizing, being found.")]
        [SerializeField] private DimensionObjectRolesTemplate roles = new DimensionObjectRolesTemplate();

        [Tooltip("How it changes when something comes near it, and whether it shoves what comes close.")]
        [SerializeField] private DimensionReactsToNearbyTemplate reactsToNearby = new DimensionReactsToNearbyTemplate();

        [Tooltip("What arrives when the right item is put down on it.")]
        [SerializeField] private DimensionSummoningCircleTemplate summoningCircle = new DimensionSummoningCircleTemplate();

        [Tooltip("What makes it open: a held item, an object placed nearby, or a melody. This " +
                 "is how doors, hidden passages and singing walls work. A key put INSIDE " +
                 "something is a container — author that on a container instead.")]
        [SerializeField] private DimensionGateTemplate gate = new DimensionGateTemplate();

        [Tooltip("What a machine can pull out of it, over and over, without breaking it.")]
        [SerializeField] private DimensionExtractableTemplate extractable = new DimensionExtractableTemplate();

        [Tooltip("How it behaves when it is given electricity.")]
        [SerializeField] private DimensionPoweredMachineTemplate poweredMachine = new DimensionPoweredMachineTemplate();

        [Tooltip("How it keeps ground it can sit on underneath itself.")]
        [SerializeField] private DimensionKeepsItsFloorTemplate keepsItsFloor = new DimensionKeepsItsFloorTemplate();

        [Tooltip("Other roles it holds: shrine, summoning item, firefly, grave, barrier, minion.")]
        [SerializeField] private DimensionWorldRolesTemplate worldRoles = new DimensionWorldRolesTemplate();

        [Tooltip("Whether the game places it somewhere by itself when a world is made.")]
        [SerializeField] private DimensionNativeWorldPlacementTemplate nativeWorldPlacement =
            new DimensionNativeWorldPlacementTemplate();

        [Tooltip("The small things it is, or does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [Tooltip("What happens when a player walks up to it and uses it.")]
        [SerializeField] private DimensionInteractionTemplate interaction =
            new DimensionInteractionTemplate();

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string ObjectIdentifier
        {
            get { return objectIdentifier ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public string RarityId
        {
            get { return rarityId ?? string.Empty; }
        }

        public Sprite Sprite
        {
            get { return sprite; }
        }

        public Sprite Icon
        {
            get { return icon != null ? icon : sprite; }
        }

        public DimensionWorldObjectKind Kind
        {
            get { return kind; }
        }

        /// <summary>The enemy a trophy summons, or empty when it is not a trophy.</summary>
        public string SummonsEnemyId
        {
            get
            {
                return kind == DimensionWorldObjectKind.Trophy
                    ? (summonsEnemyId ?? string.Empty)
                    : string.Empty;
            }
        }

        public Vector2Int TileSize
        {
            get
            {
                return new Vector2Int(
                    tileSize.x < 1 ? 1 : tileSize.x,
                    tileSize.y < 1 ? 1 : tileSize.y);
            }
        }

        /// <summary>
        /// Where the object sits inside its own footprint.
        /// </summary>
        /// <remarks>
        /// It is <c>prefabCornerOffset</c>, and it moves the hitbox as well as the placement:
        /// <c>PlanterBoxEntity</c> is three tiles wide with an offset of <c>-1</c>, and its box
        /// sits at <c>x = 0</c> rather than at <c>x = 1</c> because of it. Nothing in this
        /// framework ever wrote a non-zero one before, so the term the body calculation had added
        /// for it could never be anything but zero.
        /// </remarks>
        public Vector2Int StandsOnTile
        {
            get { return standsOnTile; }
        }

        public bool FacesPlacementDirection
        {
            get { return facesPlacementDirection; }
        }

        public bool CanBePlacedOnWater
        {
            get { return canBePlacedOnWater; }
        }

        public bool Paintable
        {
            get { return paintable; }
        }

        public int SurfacePriority
        {
            get { return surfacePriority; }
        }

        /// <summary>The light it throws on the floor where it stands.</summary>
        /// <remarks>
        /// Never null: the generator asks it questions on every pass, and an asset saved before
        /// this block existed deserialises the field as null rather than as an empty one.
        /// </remarks>
        public DimensionEmittedLightTemplate EmittedLight
        {
            get { return emittedLight ?? (emittedLight = new DimensionEmittedLightTemplate()); }
        }

        /// <summary>Whether it glows in its slot when it is set down on a table.</summary>
        /// <remarks>
        /// The name is the one the field shipped under and is kept so no saved asset loses its
        /// answer. What it writes has only ever been the table glow — see the remark beside the
        /// serialized field.
        /// </remarks>
        public bool LightsTheRoomWhenPlaced
        {
            get { return lightsTheRoomWhenPlaced; }
        }

        /// <summary>Whether it lights the area around the player while carried.</summary>
        public bool LightsTheRoomWhenHeld
        {
            get { return lightsTheRoomWhenHeld; }
        }

        public Color HeldLightColor
        {
            get { return heldLightColor; }
        }

        public int HeldLightRange
        {
            get { return heldLightRange < 0 ? 0 : heldLightRange; }
        }

        /// <summary>Whether the object itself is tinted with a glow.</summary>
        /// <remarks>
        /// Not a light. Measured across the game, <c>GlowLightAuthoring</c> sits on weapons and armour
        /// that shine, and a torch does not carry it.
        /// </remarks>
        public bool ObjectItselfGlows
        {
            get { return objectItselfGlows; }
        }

        public Color GlowColor
        {
            get { return glowColor; }
        }

        public float GlowIntensity
        {
            get { return glowIntensity < 0f ? 0f : glowIntensity; }
        }

        public int HitsToBreak
        {
            get { return hitsToBreak < 1 ? 1 : hitsToBreak; }
        }

        public bool CannotBeAttacked
        {
            get { return cannotBeAttacked; }
        }

        public float DisappearsAfterSeconds
        {
            get { return disappearsAfterSeconds < 0f ? 0f : disappearsAfterSeconds; }
        }

        public bool DisappearsOnItsOwn
        {
            get { return DisappearsAfterSeconds > 0f; }
        }

        /// <summary>Effects it grants while held, worn or eaten.</summary>
        public DimensionItemEffectsTemplate Effects
        {
            get { return effects ?? new DimensionItemEffectsTemplate(); }
        }

        /// <summary>What it sounds like and what comes off it when hit or destroyed.</summary>
        public DimensionImpactFeedbackTemplate ImpactFeedback
        {
            get { return impactFeedback ?? new DimensionImpactFeedbackTemplate(); }
        }

        /// <summary>A whole object left in the world when it dies.</summary>
        public DimensionLeavesBehindTemplate LeavesBehind
        {
            get { return leavesBehind ?? new DimensionLeavesBehindTemplate(); }
        }

        /// <summary>Whether creative mode still gives its drops.</summary>
        public bool AlwaysDropsLoot
        {
            get { return alwaysDropsLoot; }
        }

        /// <summary>What it leaves on the tile when damaged and when destroyed.</summary>
        public DimensionTileOutcomeTemplate TileOutcome
        {
            get { return tileOutcome ?? new DimensionTileOutcomeTemplate(); }
        }

        /// <summary>What the right mouse button does with it.</summary>
        public DimensionSecondaryUseTemplate SecondaryUse
        {
            get { return secondaryUse ?? new DimensionSecondaryUseTemplate(); }
        }

        /// <summary>Every place this drops from, with the blank entries dropped.</summary>
        public DimensionDropSource[] DropsFrom
        {
            get
            {
                DimensionDropSource[] all = dropsFrom ?? new DimensionDropSource[0];
                System.Collections.Generic.List<DimensionDropSource> kept =
                    new System.Collections.Generic.List<DimensionDropSource>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                    {
                        kept.Add(all[i]);
                    }
                }

                return kept.ToArray();
            }
        }

        /// <summary>What it does to things that come near it.</summary>
        public DimensionContinuousAttackTemplate ContinuousAttack
        {
            get { return continuousAttack ?? new DimensionContinuousAttackTemplate(); }
        }

        public bool KeepsThingsSafeNearby { get { return keepsThingsSafeNearby; } }
        public float SafeRadius { get { return safeRadius < 0f ? 0f : safeRadius; } }
        public bool SafeAreaIsRectangular { get { return safeAreaIsRectangular; } }
        public int SafeWidth { get { return safeWidth < 0 ? 0 : safeWidth; } }
        public int SafeHeight { get { return safeHeight < 0 ? 0 : safeHeight; } }

        /// <summary>Whether it both hurts and protects, which cancel each other out.</summary>
        public bool HurtsAndProtectsAtOnce
        {
            get { return ContinuousAttack.HurtsWhatComesNear && keepsThingsSafeNearby; }
        }
        /// <summary>Its world tier and which ways its art faces.</summary>
        public DimensionObjectBasicsTemplate Basics
        {
            get { return basics ?? new DimensionObjectBasicsTemplate(); }
        }

        /// <summary>What it starts affected by, and what cannot touch it.</summary>
        public DimensionInitialConditionsTemplate Conditions
        {
            get { return conditions ?? new DimensionInitialConditionsTemplate(); }
        }

        public Vector3 InitialFacing { get { return initialFacing; } }
        public bool ItsColliderTurnsToo { get { return itsColliderTurnsToo; } }
        public int RotationIconOffset { get { return rotationIconOffset; } }
        public string TextItComesWith { get { return textItComesWith ?? string.Empty; } }
        public bool UntouchableForOneFrameOnly { get { return untouchableForOneFrameOnly; } }

        /// <summary>Whether it was told to be untouchable for one frame while nothing can touch it anyway.</summary>
        /// <remarks>
        /// The one-frame form is a spawn protection for something that lands and should be hittable
        /// straight after. Ticked together with "cannot be attacked" it reads as a contradiction, and
        /// the permanent one wins.
        /// </remarks>
        public bool OneFrameRuleIsPointless
        {
            get { return untouchableForOneFrameOnly && cannotBeAttacked; }
        }

        public bool IsAFenceGate { get { return isAFenceGate; } }
        public string FlowerOfPlantId { get { return flowerOfPlantId ?? string.Empty; } }
        public int FlowerVariation { get { return flowerVariation < 0 ? 0 : flowerVariation; } }
        public string SpawnsEnemyId { get { return spawnsEnemyId ?? string.Empty; } }

        /// <summary>Whether players can travel to it.</summary>
        public bool PlayersCanTravelToIt { get { return playersCanTravelToIt; } }

        public float ActivateWithin { get { return activateWithin < 0f ? 0f : activateWithin; } }

        /// <summary>Whether it is the core waypoint, which only matters when it is a waypoint.</summary>
        public bool IsTheCoreWaypoint { get { return playersCanTravelToIt && isTheCoreWaypoint; } }

        /// <summary>Music that plays when a player comes close.</summary>
        public DimensionMusicAreaTemplate Music
        {
            get { return music ?? new DimensionMusicAreaTemplate(); }
        }

        /// <summary>What automation can do with it.</summary>
        public DimensionAutomationTemplate Automation
        {
            get { return automation ?? new DimensionAutomationTemplate(); }
        }

        /// <summary>The small rules the world applies to it.</summary>
        public DimensionObjectRulesTemplate Rules
        {
            get { return rules ?? new DimensionObjectRulesTemplate(); }
        }

        public DimensionWiringTemplate Wiring
        {
            get { return wiring ?? new DimensionWiringTemplate(); }
        }

        /// <summary>Whether it was made to glow but never to light anything.</summary>
        /// <remarks>
        /// Worth saying out loud because it is the one lighting mistake that looks right in the
        /// inspector: a lamp that glows in the dark and leaves the room black.
        /// <para>
        /// The light it gives off where it stands counts here, and it did not used to, because
        /// until that fold existed there was no way for a placed object to light anything at all.
        /// Without this clause a lamp answered honestly — a real light, and a glow on top — would
        /// be told it lights nothing.
        /// </para>
        /// </remarks>
        public bool GlowsButLightsNothing
        {
            get
            {
                return objectItselfGlows &&
                       !EmittedLight.GivesOffLight &&
                       !lightsTheRoomWhenPlaced &&
                       !lightsTheRoomWhenHeld;
            }
        }

        /// <summary>
        /// Fills in the numbers behind a named light the first time one is chosen.
        /// </summary>
        /// <remarks>
        /// Unity runs this whenever the inspector writes a field, which is what lets picking
        /// "Torch" in the dropdown fill the colour, reach, flicker and height in the same frame and
        /// then leave them alone. Everything it can do is inside the light block; nothing else on
        /// this asset is touched.
        /// </remarks>
        private void OnValidate()
        {
            EmittedLight.CopyThePresetInIfItChanged();
        }

        /// <summary>Whether it is a trophy with nothing to summon.</summary>
        public bool IsATrophyThatSummonsNothing
        {
            get
            {
                return kind == DimensionWorldObjectKind.Trophy && string.IsNullOrEmpty(SummonsEnemyId);
            }
        }

        /// <summary>Whether it can neither be broken nor removed, on purpose or otherwise.</summary>
        /// <remarks>
        /// A player who places one of these can never take it back, which is the same trap the
        /// container asset guards against with its unbreakable tick.
        /// </remarks>
        public bool CanNeverBeRemoved
        {
            get { return cannotBeAttacked && !DisappearsOnItsOwn; }
        }

        public DimensionPlacementRulesTemplate PlacementRules
        {
            get { return placementRules ?? (placementRules = new DimensionPlacementRulesTemplate()); }
        }

        public DimensionExtraLootTemplate ExtraLoot
        {
            get { return extraLoot ?? (extraLoot = new DimensionExtraLootTemplate()); }
        }

        public DimensionMelodyResponseTemplate MelodyResponse
        {
            get { return melodyResponse ?? (melodyResponse = new DimensionMelodyResponseTemplate()); }
        }

        public DimensionEventTerminalTemplate EventTerminal
        {
            get { return eventTerminal ?? (eventTerminal = new DimensionEventTerminalTemplate()); }
        }

        public DimensionFinalTouchesTemplate FinalTouches
        {
            get { return finalTouches ?? (finalTouches = new DimensionFinalTouchesTemplate()); }
        }

        public DimensionBeamAndAmbienceTemplate BeamAndAmbience
        {
            get
            {
                return beamAndAmbience ??
                    (beamAndAmbience = new DimensionBeamAndAmbienceTemplate());
            }
        }

        public DimensionHidingAndHatchingTemplate HidingAndHatching
        {
            get
            {
                return hidingAndHatching ??
                    (hidingAndHatching = new DimensionHidingAndHatchingTemplate());
            }
        }

        public DimensionManaAndAuraTemplate ManaAndAura
        {
            get { return manaAndAura ?? (manaAndAura = new DimensionManaAndAuraTemplate()); }
        }

        public DimensionSpawnerAndOrbTemplate SpawnerAndOrb
        {
            get { return spawnerAndOrb ?? (spawnerAndOrb = new DimensionSpawnerAndOrbTemplate()); }
        }

        public DimensionChainReactionTemplate ChainReaction
        {
            get { return chainReaction ?? (chainReaction = new DimensionChainReactionTemplate()); }
        }

        public DimensionMachineRolesTemplate MachineRoles
        {
            get { return machineRoles ?? (machineRoles = new DimensionMachineRolesTemplate()); }
        }

        public DimensionTerrainEffectTemplate TerrainEffects
        {
            get { return terrainEffects ?? (terrainEffects = new DimensionTerrainEffectTemplate()); }
        }

        public DimensionLooksAtItsSurroundingsTemplate AdaptsToSurroundings
        {
            get
            {
                return adaptsToSurroundings ??
                    (adaptsToSurroundings = new DimensionLooksAtItsSurroundingsTemplate());
            }
        }

        public DimensionNestTemplate Nest
        {
            get { return nest ?? (nest = new DimensionNestTemplate()); }
        }

        public DimensionTraderTemplate Trader
        {
            get { return trader ?? (trader = new DimensionTraderTemplate()); }
        }

        public DimensionObjectRolesTemplate Roles
        {
            get { return roles ?? (roles = new DimensionObjectRolesTemplate()); }
        }

        public DimensionReactsToNearbyTemplate ReactsToNearby
        {
            get { return reactsToNearby ?? (reactsToNearby = new DimensionReactsToNearbyTemplate()); }
        }

        public DimensionSummoningCircleTemplate SummoningCircle
        {
            get { return summoningCircle ?? (summoningCircle = new DimensionSummoningCircleTemplate()); }
        }

        public DimensionGateTemplate Gate
        {
            get { return gate ?? (gate = new DimensionGateTemplate()); }
        }

        public DimensionExtractableTemplate Extractable
        {
            get { return extractable ?? (extractable = new DimensionExtractableTemplate()); }
        }

        public DimensionPoweredMachineTemplate PoweredMachine
        {
            get { return poweredMachine ?? (poweredMachine = new DimensionPoweredMachineTemplate()); }
        }

        public DimensionKeepsItsFloorTemplate KeepsItsFloor
        {
            get { return keepsItsFloor ?? (keepsItsFloor = new DimensionKeepsItsFloorTemplate()); }
        }

        public DimensionWorldRolesTemplate WorldRoles
        {
            get { return worldRoles ?? (worldRoles = new DimensionWorldRolesTemplate()); }
        }

        /// <summary>Whether the game places it by itself when a world is made.</summary>
        public DimensionNativeWorldPlacementTemplate NativeWorldPlacement
        {
            get
            {
                return nativeWorldPlacement ??
                       (nativeWorldPlacement = new DimensionNativeWorldPlacementTemplate());
            }
        }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        /// <summary>What happens when a player uses it.</summary>
        public DimensionInteractionTemplate Interaction
        {
            get { return interaction ?? (interaction = new DimensionInteractionTemplate()); }
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
