using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The editors for the things that live in a world, and the objects placed in it.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private void DrawAnimalAssetEditors(DimensionAnimalAsset[] animals)
        {
            if (animals == null)
            {
                return;
            }

            for (int i = 0; i < animals.Length; i++)
            {
                DimensionAnimalAsset animal = animals[i];
                if (animal == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    animal,
                    BuildAssetEditorTitle("Animal", animal.DisplayName, animal.AnimalId, i),
                    Field("displayName", "Display name"),
                    NameField("animalId", "Animal ID"),
                    NameField("objectId", "Object ID"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("creatureStats", "Stats (every number, exactly as typed)"),
                    Field("combat", "Combat and behaviour"),
                    Field("aggression", "Temperament"),
                    Field("spawnsInWorld", "Spawns in the world"),
                    Field("spawnChance", "Spawn chance"),
                    Field("spawnAmount", "Spawn amount"),
                    Field("spawnsInGroups", "Spawns in groups"),
                    Field("canSpawnInBlockedArea", "May spawn in blocked areas"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("lootTable", "Loot table"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawPlantAssetEditors(DimensionPlantAsset[] plants)
        {
            if (plants == null)
            {
                return;
            }

            for (int i = 0; i < plants.Length; i++)
            {
                DimensionPlantAsset plant = plants[i];
                if (plant == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    plant,
                    BuildAssetEditorTitle("Plant", plant.DisplayName, plant.PlantId, i),
                    Field("displayName", "Display name"),
                    NameField("plantId", "Plant ID"),
                    Field("description", "Description"),
                    Field("rarityId", "Rarity"),
                    Field("seedIcon", "Seed icon"),
                    Field("art", "What it looks like in the ground", true),
                    Field("growthStages", "Growth stages"),
                    Field("minutesToGrow", "Minutes to grow"),
                    Field("staysToughWhenRipe", "Stays tough when ripe"),
                    Field("washedAwayByWater", "Washed away by water"),
                    Field("produceItemId", "Produce item"),
                    Field("harvestAmount", "How many per harvest"),
                    Field("chanceToGetTheSeedBackPercent", "Chance to get the seed back"),
                    Field("versions", "Better versions", true),
                    Field("ground", "Ground it grows on"),
                    Field("spreadsOnTilesetIds", "Spreads on tilesets"),
                    Field("minSpreadSeconds", "Min spread seconds"),
                    Field("maxSpreadSeconds", "Max spread seconds"),
                    Field("becomesTilesetId", "The ground it turns into"),
                    Field("placementRules", "Where it may be planted", true),
                    Field("simpleTraits", "The small things it simply is", true),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                EditorGUILayout.HelpBox(
                    "Generating this writes the seed, the growing plant and the ripe plant, plus a " +
                    "seed and a plant for every better version. They are all two objects — a seed " +
                    "and a plant — the way Core Keeper builds its own crops.\n\n" +
                    "A better version's place in the list decides which variation it sits on, so " +
                    "reordering the list moves crops already planted in an existing world onto a " +
                    "different version. Add new ones at the end.\n\n" +
                    "It needs " + plant.PicturesNeeded + " pictures to be visible: one for each of " +
                    "its " + plant.GrowthStages + " growth stages and one for the ripe plant. A " +
                    "better version with pictures of its own is what makes a golden crop look " +
                    "golden; one without looks exactly like the ordinary one.",
                    MessageType.Info);
            }
        }

        private void DrawWorldObjectAssetEditors(DimensionWorldObjectAsset[] worldObjects)
        {
            if (worldObjects == null)
            {
                return;
            }

            for (int i = 0; i < worldObjects.Length; i++)
            {
                DimensionWorldObjectAsset worldObject = worldObjects[i];
                if (worldObject == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    worldObject,
                    BuildAssetEditorTitle("Object", worldObject.DisplayName, worldObject.ObjectIdentifier, i),
                    Field("displayName", "Display name"),
                    NameField("objectIdentifier", "Object ID"),
                    Field("description", "Description"),
                    Field("rarityId", "Rarity"),
                    Field("sprite", "Sprite"),
                    Field("icon", "Icon"),
                    Field("kind", "What it is"),
                    Field("summonsEnemyId", "Trophy summons"),
                    Field("tileSize", "Tile size"),
                    Field("facesPlacementDirection", "Faces placement direction"),
                    Field("canBePlacedOnWater", "Can be placed on water"),
                    Field("paintable", "Paintable"),
                    Field("surfacePriority", "Surface priority"),
                    Field("emittedLight", "The light it gives off"),
                    Field("lightsTheRoomWhenPlaced", "Glows when it is set down on a table"),
                    Field("lightsTheRoomWhenHeld", "Lights the room when held"),
                    Field("heldLightColor", "Held light colour"),
                    Field("heldLightRange", "Held light range"),
                    Field("objectItselfGlows", "The object itself glows"),
                    Field("glowColor", "Glow colour"),
                    Field("glowIntensity", "Glow intensity"),
                    Field("hitsToBreak", "Hits to break"),
                    Field("cannotBeAttacked", "Cannot be attacked"),
                    Field("disappearsAfterSeconds", "Disappears after (seconds)"),
                    Field("effects", "What it does for you"),
                    Field("impactFeedback", "Hitting and breaking it"),
                    Field("tileOutcome", "What it leaves on the tile"),
                    Field("leavesBehind", "What it leaves standing"),
                    Field("continuousAttack", "If it hurts things"),
                    Field("keepsThingsSafeNearby", "Keeps things safe nearby"),
                    Field("safeRadius", "How far the safety reaches"),
                    Field("basics", "Where it sits in the world"),
                    Field("conditions", "Conditions"),
                    Field("initialFacing", "Faces this way when placed"),
                    Field("itsColliderTurnsToo", "Its collider turns too"),
                    Field("textItComesWith", "Text it comes with"),
                    Field("untouchableForOneFrameOnly", "Untouchable for one frame only"),
                    Field("isAFenceGate", "Is a fence gate"),
                    Field("flowerOfPlantId", "Flower of plant"),
                    Field("spawnsEnemyId", "Spawner platform produces"),
                    Field("playersCanTravelToIt", "Players can travel to it"),
                    Field("activateWithin", "Activate within"),
                    Field("isTheCoreWaypoint", "Is the core waypoint"),
                    Field("music", "Music near it"),
                    Field("automation", "Automation"),
                    Field("rules", "How the world treats it"),
                    Field("alwaysDropsLoot", "Creative mode still gives its drops"),
                    Field("secondaryUse", "Right-click"),
                    Field("dropsFrom", "Where it drops from"),
                    Field("wiring", "Wiring"),

                    // ---- what a player does with it ----
                    Field("interaction", "When a player uses it", true),
                    Field("roles", "Its small roles in a base", true),
                    Field("simpleTraits", "The small things it simply is", true),

                    // ---- placing and appearance details ----
                    Field("placementRules", "Where it may be placed", true),
                    Field("rotationIconOffset", "Icon nudge per rotation"),
                    Field("flowerVariation", "Which look its flower is"),
                    Field("adaptsToSurroundings", "It changes with its surroundings", true),

                    // ---- the safety zone's shape ----
                    Field("safeAreaIsRectangular", "The safe area is a rectangle"),
                    Field("safeWidth", "Safe area width"),
                    Field("safeHeight", "Safe area height"),

                    // ---- what it gives and holds ----
                    Field("extraLoot", "Loot besides its drops", true),
                    Field("extractable", "What machines can draw from it", true),
                    Field("trader", "If it buys and sells", true),
                    Field("nest", "If it is a nest", true),

                    // ---- how it behaves ----
                    Field("melodyResponse", "It answers a tune", true),
                    Field("reactsToNearby", "It reacts to someone coming close", true),
                    Field("summoningCircle", "If it summons something", true),
                    Field("poweredMachine", "If power drives it", true),
                    Field("keepsItsFloor", "It lays its own floor", true),
                    Field("machineRoles", "Its machine roles", true),
                    Field("terrainEffects", "How it changes the ground", true),
                    Field("chainReaction", "If it sets off its neighbours", true),
                    Field("spawnerAndOrb", "If it spawns things", true),
                    Field("manaAndAura", "Mana and auras", true),
                    Field("hidingAndHatching", "Hiding and hatching", true),
                    Field("beamAndAmbience", "Beams and ambience", true),
                    Field("eventTerminal", "If it runs an event", true),
                    Field("finalTouches", "Final touches", true),

                    // ---- the wider world ----
                    Field("worldRoles", "Its roles in the world", true),
                    Field("nativeWorldPlacement", "Placed once, when a world is made", true),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (worldObject.GlowsButLightsNothing)
                {
                    EditorGUILayout.HelpBox(
                        "This glows but lights nothing. Core Keeper keeps those separate - a torch " +
                        "carries the two lighting components and not the glow - so as authored it " +
                        "will shine in a pitch-black room.",
                        MessageType.Warning);
                }

                if (worldObject.IsATrophyThatSummonsNothing)
                {
                    EditorGUILayout.HelpBox(
                        "This is a trophy with nothing to summon, so using it will do nothing.",
                        MessageType.Warning);
                }

                if (worldObject.CanNeverBeRemoved)
                {
                    EditorGUILayout.HelpBox(
                        "Nothing can attack this and it never disappears, so a player who places " +
                        "one can never take it back.",
                        MessageType.Warning);
                }
            }
        }

        /// <summary>
        /// The projectile editors.
        /// </summary>
        /// <remarks>
        /// The ordinary questions first and the exotica after, because that is what vanilla use
        /// looks like: 41 of the game's 71 projectiles are a plain shot with a 0.2 hit radius and
        /// nothing else ticked. Putting all 22 of the component's fields on one flat list would bury
        /// the one that matters.
        /// </remarks>

        /// <summary>
        /// The explosion editors.
        /// </summary>
        /// <remarks>
        /// Three numbers and the ordinary object spine — that really is all an explosion is. The
        /// terrain damage is worth its label: it is measured against the mining curve rather than
        /// health, so the vanilla values that dig are 165 and 210 rather than anything health-like.
        /// </remarks>
        private void DrawExplosionAssetEditors(DimensionExplosionAsset[] explosions)
        {
            if (explosions == null)
            {
                return;
            }

            for (int i = 0; i < explosions.Length; i++)
            {
                DimensionExplosionAsset explosion = explosions[i];
                if (explosion == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    explosion,
                    BuildAssetEditorTitle("Explosion", explosion.DisplayName, explosion.ExplosionId, i),
                    Field("displayName", "Display name"),
                    NameField("explosionId", "Explosion ID"),
                    Field("sprite", "Sprite"),
                    Field("lifetimeSeconds", "Lasts (seconds)"),
                    Field("radius", "How far it reaches"),
                    Field("leavesBehind", "What it leaves burning"),
                    Field("feedback", "What it sounds and looks like"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (explosion.ReachesNothing)
                {
                    EditorGUILayout.HelpBox(
                        "The reach is zero, so this catches nothing. Vanilla runs from 1 to 4.5, " +
                        "and an ordinary bomb's blast reaches 2.",
                        MessageType.Warning);
                }

                if (explosion.NeverGoesAway)
                {
                    EditorGUILayout.HelpBox(
                        "This has no lifetime, so it stays where it went off, doing its damage, " +
                        "for as long as the world is loaded.",
                        MessageType.Warning);
                }

                EditorGUILayout.HelpBox(
                    "How much a blast hurts and how much terrain it breaks are set on whatever " +
                    "sets it off, not here: the game writes those two numbers over the blast's " +
                    "own every time one goes off.",
                    MessageType.Info);
            }
        }

        private void DrawProjectileAssetEditors(DimensionProjectileAsset[] projectiles)
        {
            if (projectiles == null)
            {
                return;
            }

            for (int i = 0; i < projectiles.Length; i++)
            {
                DimensionProjectileAsset projectile = projectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    projectile,
                    BuildAssetEditorTitle("Projectile", projectile.DisplayName, projectile.ProjectileId, i),
                    Field("displayName", "Display name"),
                    NameField("projectileId", "Projectile ID"),
                    Field("sprite", "Sprite"),
                    Field("speed", "Speed"),
                    Field("lifetimeSeconds", "Lifetime (seconds)"),
                    Field("hitRadius", "Hit radius"),
                    Field("goesThroughEnemies", "Goes through enemies"),
                    Field("explodesOnEnemies", "Bursts on enemies"),
                    Field("damagesTerrain", "Damages terrain"),
                    Field("terrainHitRadius", "Terrain hit radius"),
                    Field("bounces", "Bounces off walls"),
                    Field("flight", "How it travels"),
                    Field("useTheGamesOwnTimings", "Use the game's own arc timings"),
                    Field("goUpSeconds", "Seconds going up"),
                    Field("airSeconds", "Seconds in the air"),
                    Field("goDownSeconds", "Seconds coming down"),
                    Field("explodeSeconds", "Seconds before it goes off"),
                    Field("breaksTerrainWhereItLands", "Breaks terrain where it lands"),
                    Field("isMagic", "Counts as magic"),
                    Field("ignoresTheDamageCap", "Ignores the damage cap"),
                    Field("onlyLandsWhereItCanSee", "Only lands in sight"),
                    Field("scattersTilesOnTheWayDown", "Scatters tiles on the way down"),
                    Field("scatteredTilesetId", "Tileset it scatters"),
                    Field("scatterExtraRadius", "Extra scatter radius"),
                    Field("leavesATileWhereItLands", "Leaves a tile where it lands"),
                    Field("landedTilesetId", "Tileset it leaves"),
                    Field("sounds", "What it sounds like"),
                    Field("survivesCollision", "Survives collision"),
                    Field("canBeShotDown", "Can be shot down"),
                    Field("weaves", "Weaves as it flies"),
                    Field("stopsOnUnwalkableTiles", "Stops on unwalkable tiles"),
                    Field("dodgingDoesNotSaveYou", "A dodge still counts as a hit"),
                    Field("shards", "Breaks into"),
                    Field("shardObjectId", "Breaks into what"),
                    Field("shattersOnCollision", "It shatters when it hits"),

                    // ---- flight details ----
                    Field("outAndBackSeconds", "Seconds out before it comes back"),
                    Field("speedFollowsACurve", "Its speed follows a curve"),
                    Field("speedCurve", "That curve"),
                    Field("secondSpeedCurve", "A second curve, blended in"),
                    Field("mayExplodeOnAPartialWindUp", "May go off on a partial wind-up"),
                    Field("onlyHitsTheSameThingEvery", "Only hits the same thing every (seconds)"),
                    Field("fliesThroughWallTypes", "Wall types it flies through", true),
                    Field("clientPredictsIt", "The player's own game predicts it"),

                    // ---- what it does to the ground ----
                    Field("scatteredTileType", "What kind of tile it scatters"),
                    Field("landedTileType", "What kind of tile it leaves"),
                    Field("canPlaceTilesOnWaterAndPits", "Its tiles may land on water and pits"),
                    Field("removesTilesWhereItLands", "It removes tiles where it lands"),
                    Field("removedTilesetId", "Which tileset it removes"),
                    Field("removedTileType", "Which kind of tile it removes"),
                    Field("raggedEdges", "Its craters have ragged edges"),
                    Field("wallsItBreaksDropNothing", "Walls it breaks drop nothing"),

                    // ---- what it does to whoever it hits ----
                    Field("pushesWhatItHits", "How hard it pushes what it hits"),
                    Field("leavesBehindObjectId", "What it leaves behind"),
                    Field("leavesBehindVariation", "That object's look"),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (projectile.NeverGoesAnywhere)
                {
                    EditorGUILayout.HelpBox(
                        "This has no speed, so it appears where it was fired from and expires " +
                        "without travelling.",
                        MessageType.Warning);
                }

                if (projectile.CannotHitAnything)
                {
                    EditorGUILayout.HelpBox(
                        "The hit radius is zero, so this passes through everything. Most of the " +
                        "game uses " + DimensionProjectileAsset.OrdinaryHitRadius + ".",
                        MessageType.Warning);
                }

                if (projectile.ShattersIntoNothing)
                {
                    EditorGUILayout.HelpBox(
                        "This is set to break into pieces without saying what of.",
                        MessageType.Warning);
                }

                if (projectile.DamagesTerrainOverNoArea)
                {
                    EditorGUILayout.HelpBox(
                        "This damages terrain over an area of zero, so the terrain damage can " +
                        "never land.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawVehicleAssetEditors(DimensionVehicleAsset[] vehicles)
        {
            if (vehicles == null)
            {
                return;
            }

            for (int i = 0; i < vehicles.Length; i++)
            {
                DimensionVehicleAsset vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    vehicle,
                    BuildAssetEditorTitle("Vehicle", vehicle.DisplayName, vehicle.VehicleId, i),
                    Field("displayName", "Display name"),
                    NameField("vehicleId", "Vehicle ID"),
                    Field("description", "Description"),
                    Field("kind", "How it moves"),
                    Field("sprite", "Picture"),
                    Field("icon", "Icon"),
                    Field("speedMultiplier", "Speed"),
                    Field("accelerationMultiplier", "Acceleration"),
                    Field("driftingMultiplier", "Drifting"),
                    Field("minecartMaxSpeed", "Minecart top speed"),
                    Field("howCloseToGetOn", "How close to get on"),
                    Field("hitsToBreak", "Hits to break"),
                    Field("dropsItselfWhenBroken", "Breaking gives it back"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        /// <summary>
        /// Draws the setups that change the game itself, rather than adding to it.
        /// </summary>
        /// <remarks>
        /// The field list is short on purpose. Everything else the asset holds is drawn below it by
        /// the catch-all, so nothing here can quietly become unreachable the way whole features
        /// have before.
        /// </remarks>

        /// <summary>Draws the stat effects a mod invented.</summary>
        private void DrawConditionAssetEditors(DimensionConditionAsset[] conditions)
        {
            if (conditions == null)
            {
                return;
            }

            for (int i = 0; i < conditions.Length; i++)
            {
                DimensionConditionAsset condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    condition,
                    "Condition: " + condition.DisplayName,
                    NameField("conditionName", "Id"),
                    Field("displayName", "Called"),
                    Field("enabled", "Generated"),
                    Field("effect", "What it does"));
            }
        }

        private void DrawGameSetupAssetEditors(DimensionGameSetupAsset[] setups)
        {
            if (setups == null)
            {
                return;
            }

            for (int i = 0; i < setups.Length; i++)
            {
                DimensionGameSetupAsset setup = setups[i];
                if (setup == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    setup,
                    "World rules: " + setup.DisplayName,
                    Field("setupIdentifier", "Id"),
                    Field("displayName", "Called"),
                    Field("enabled", "Applied"),
                    Field("upgrading", "What upgrading costs", true),
                    Field("fishing", "What fishing catches", true),
                    Field("talents", "What talents give", true),
                    Field("player", "Overrides on the player", true));
            }
        }

        private void DrawCritterAssetEditors(DimensionCritterAsset[] critters)
        {
            if (critters == null)
            {
                return;
            }

            for (int i = 0; i < critters.Length; i++)
            {
                DimensionCritterAsset critter = critters[i];
                if (critter == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    critter,
                    BuildAssetEditorTitle("Critter", critter.DisplayName, critter.CritterId, i),
                    Field("displayName", "Display name"),
                    NameField("critterId", "Critter ID"),
                    NameField("objectId", "Object ID"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("isFlying", "Flies"),
                    Field("spawnContinuously", "Keeps appearing"),
                    Field("isPersistent", "Survives being left behind"),
                    Field("allowLargerAmount", "More may gather than usual"),
                    Field("canBeCaught", "Can be caught"),
                    Field("home", "Picks its home by"),
                    Field("tilesetIds", "Grounds it lives on"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        /// <summary>
        /// Draws every authored dungeon.
        /// </summary>
        /// <remarks>
        /// Dungeons and quests were both built, generated and tested, and neither had a panel — the
        /// authoring-surface audit found them with no editor at all. Everything a creator could set
        /// on one was reachable only by selecting the raw asset in the Project window, which is the
        /// exact thing this framework exists to avoid.
        /// </remarks>
        private void DrawDungeonAssetEditors(DimensionDungeonAsset[] dungeons)
        {
            if (dungeons == null)
            {
                return;
            }

            for (int i = 0; i < dungeons.Length; i++)
            {
                DimensionDungeonAsset dungeon = dungeons[i];
                if (dungeon == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    dungeon,
                    BuildAssetEditorTitle("Dungeon", dungeon.DisplayName, dungeon.DungeonId, i),
                    Field("displayName", "Display name"),
                    NameField("dungeonId", "Dungeon ID"),
                    Field("biomeId", "Biome it appears in"),
                    Field("radius", "How far out it can appear"),
                    Field("minDistanceFromCentre", "Never closer to the centre than"),
                    Field("spawnChance", "Chance it appears"),
                    Field("roomGroups", "Rooms it is built from"),
                    Field("roomSize", "Room size"),
                    Field("pathSize", "Corridor width"),
                    Field("blockOtherSpawns", "Nothing else spawns inside it"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawMobAssetEditors(DimensionMobAsset[] mobs)
        {
            if (mobs == null)
            {
                return;
            }

            for (int i = 0; i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    mob,
                    BuildAssetEditorTitle("Mob", mob.DisplayName, mob.MobId, i),
                    Field("displayName", "Display name"),
                    NameField("mobId", "Mob ID"),
                    NameField("objectId", "Object ID"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("creatureStats", "Stats (every number, exactly as typed)"),
                    Field("combat", "Combat and behaviour"),
                    Field("eliteVariant", "Elite variant"),
                    Field("spawnsInWorld", "Spawns in the world"),
                    Field("spawnChance", "Spawn chance"),
                    Field("spawnAmount", "Spawn amount"),
                    Field("spawnsInGroups", "Spawns in groups"),
                    Field("canSpawnInBlockedArea", "May spawn in blocked areas"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("lootTable", "Loot table"),
                    Field("extraLoot", "Loot besides its drops", true),
                    Field("pet", "As a pet", true),
                    Field("simpleTraits", "The small things it simply is", true),
                    Field("aggression", "Aggression"),
                    Field("behaviorScriptId", "Behavior script ID"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawBossAssetEditors(DimensionBossAsset[] bosses)
        {
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    boss,
                    BuildAssetEditorTitle("Boss", boss.DisplayName, boss.BossId, i),
                    Field("displayName", "Display name"),
                    NameField("bossId", "Boss ID"),
                    NameField("objectId", "Object ID"),
                    Field("arenaSceneId", "Arena scene ID"),
                    Field("summoningItemId", "Summoning item ID"),
                    Field("creatureStats", "Stats (every number, exactly as typed)"),
                    Field("combat", "Combat and behaviour"),
                    Field("aggression", "Temperament"),
                    Field("bossChest", "Chest it leaves behind"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("mapPin", "Map pin"),
                    Field("fightMusic", "Fight music"),
                    Field("lootTable", "Loot table"),
                    Field("phases", "Phases"),
                    Field("respawnCooldownMinutes", "Respawn cooldown minutes"),

                    // ---- kits borrowed from the game's own bosses ----
                    Field("borrowedKit", "The Hydra and Slime kits", true),
                    Field("moreBorrowedKits", "The Core, Wall and Scarab kits", true),
                    Field("theRestOfTheKits", "The Bird, Robot, Octopus, Larva, Shaman and Snake kits", true),
                    Field("simpleTraits", "The small things it simply is", true),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }
    }
}
