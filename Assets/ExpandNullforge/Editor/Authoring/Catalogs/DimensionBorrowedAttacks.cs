// GENERATED FILE — do not hand-edit.
//
// Produced by Docs/harvest-borrowed-attacks.sh from the Core Keeper dictionary vault, which
// transcribes every authored value out of the game's own prefabs. Re-run that script to refresh it.
//
// IT SITS BEHIND THE EDITOR BOUNDARY, and that is where the ten thousand lines below belong: a
// preset is POURED INTO the ordinary fields when an author picks one, and the creature keeps no
// reference to it, so nothing at run time can ever ask this table a question. Move it to the
// shipped side and it becomes ten thousand lines compiled into every mod built with this
// framework and read by nothing there.
using System;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Every attack the game itself authored, ready to be borrowed whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE FIRST OF THE TWO DOORS. An author making a creature can build an attack from
    /// nothing, or take one of these and change as much or as little of it as they like. Taking one
    /// and changing nothing is a finished answer: the numbers are the game's own, measured off the
    /// prefab that ships with it, not approximations.
    /// </para>
    /// <para>
    /// A PRESET IS NOT A LOCK. Picking one writes its values into the ordinary fields, where they
    /// are then just values — every one of them stays editable, and the creature does not remember
    /// which preset it came from.
    /// </para>
    /// </remarks>
    public static class DimensionBorrowedAttacks
    {
        /// <summary>One authored value, and the field it belongs in.</summary>
        [Serializable]
        public struct Value
        {
            /// <summary>The field's path inside the combat block, dotted for nested blocks.</summary>
            public string Path;

            /// <summary>The value the game authored, as text.</summary>
            public string Text;

            public Value(string path, string text)
            {
                Path = path;
                Text = text;
            }
        }

        /// <summary>One of the game's own attacks.</summary>
        [Serializable]
        public sealed class Preset
        {
            /// <summary>What it is called in the list an author picks from.</summary>
            public string Name;

            /// <summary>Which creature it came from.</summary>
            public string Creature;

            /// <summary>Which sort of thing it is — Melee, Ranged, Sounds and so on.</summary>
            public string Kind;

            /// <summary>Every value it carries.</summary>
            public Value[] Values;
        }

        /// <summary>All 469 of them, in one list.</summary>
        public static readonly Preset[] All = new Preset[]
        {
            new Preset
            {
                Name = "Electric Pest — the way it chases",
                Creature = "Electric Pest",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "4"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Electric Pest — its leaping attack",
                Creature = "Electric Pest",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.4"),
                    new Value("@ability.duration", "0.7"),
                    new Value("@ability.power", "122"),
                    new Value("@ability.leapHitsThisHardForItsTier", "1"),
                    new Value("@ability.speedMultiplier", "200"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "1"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.range", "6"),
                }
            },
            new Preset
            {
                Name = "Electric Pest — the way it wanders",
                Creature = "Electric Pest",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "AF Pipe Club — the sounds it makes fighting",
                Creature = "AF Pipe Club",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1780527880"),
                    new Value("attackSounds.impactSound", "#sfx:370581956"),
                    new Value("attackSounds.windUpSound", "#sfx:1033959017"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:585077688"),
                }
            },
            new Preset
            {
                Name = "AF Quill Projectile — the sounds it makes fighting",
                Creature = "AF Quill Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:796774512"),
                    new Value("attackSounds.impactSound", "#sfx:-34550171"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "AF Quill Rifle — the sounds it makes fighting",
                Creature = "AF Quill Rifle",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-258703700"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Acid Larva — the way it chases",
                Creature = "Acid Larva",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "1"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "1"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Acid Larva From Hive Egg — the way it chases",
                Creature = "Acid Larva From Hive Egg",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "1"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "1"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "1"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Arcane Beam — its sweeping ray",
                Creature = "Arcane Beam",
                Kind = "Ray",
                Values = new Value[]
                {
                    new Value("moreCombat.rayStartsAtARandomAngle", "1"),
                    new Value("moreCombat.raySpinSpeed", "22.5"),
                    new Value("moreCombat.rayLength", "6"),
                    new Value("moreCombat.rayGrowSeconds", "1"),
                    new Value("moreCombat.rayShrinkSeconds", "1"),
                    new Value("moreCombat.rayStartsOutAt", "0.25"),
                    new Value("moreCombat.rayThickness", "0.2"),
                    new Value("moreCombat.rayDamage", "315"),
                    new Value("moreCombat.rayMultiplier", "1"),
                    new Value("moreCombat.rayDoesNotTurn", "1"),
                    new Value("moreCombat.rayIsRanged", "1"),
                    new Value("moreCombat.rayIsMagic", "1"),
                    new Value("moreCombat.rayTotalSeconds", "0.2"),
                    new Value("moreCombat.rayWindUpSeconds", "0.5"),
                    new Value("moreCombat.raySweepSeconds", "21"),
                    new Value("moreCombat.rayRecoverySeconds", "10"),
                }
            },
            new Preset
            {
                Name = "Aggressive Slime Blob — the way it chases",
                Creature = "Aggressive Slime Blob",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Aggressive Slime Blob — its leaping attack",
                Creature = "Aggressive Slime Blob",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "29"),
                    new Value("@ability.leapHitsThisHardForItsTier", "1"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "0"),
                    new Value("@ability.minCooldown", "0"),
                    new Value("@ability.maxCooldown", "0"),
                    new Value("@ability.range", "3"),
                }
            },
            new Preset
            {
                Name = "Aggressive Slime Blob — the way it wanders",
                Creature = "Aggressive Slime Blob",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Amoeba Bomb — the way it explodes",
                Creature = "Amoeba Bomb",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "2.5"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "1"),
                    new Value("@ability.power", "590"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "0"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "0"),
                    new Value("@ability.onDeath", "1"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Anchor Axe — the sounds it makes fighting",
                Creature = "Anchor Axe",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1507568629"),
                    new Value("attackSounds.impactSound", "#sfx:-636336439"),
                    new Value("attackSounds.windUpSound", "#sfx:82326827"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:560548677"),
                }
            },
            new Preset
            {
                Name = "Ancient Golem — the way it chases",
                Creature = "Ancient Golem",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Ancient Golem — the way it wanders",
                Creature = "Ancient Golem",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Ancient Golem — its ranged shot",
                Creature = "Ancient Golem",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "ShockWaveProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0.3"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.3"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "30"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "2"),
                    new Value("projectilesPerShot", "3"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0.75"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "205"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Ancient Spear — the sounds it makes fighting",
                Creature = "Ancient Spear",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:895019619"),
                    new Value("attackSounds.impactSound", "#sfx:-1232009377"),
                    new Value("attackSounds.windUpSound", "#sfx:2086898086"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-114610782"),
                }
            },
            new Preset
            {
                Name = "Arcane Staff — the sounds it makes fighting",
                Creature = "Arcane Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:968173012"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1895054634"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-613388671"),
                }
            },
            new Preset
            {
                Name = "Arcane Staff Projectile — the sounds it makes fighting",
                Creature = "Arcane Staff Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1912660181"),
                    new Value("attackSounds.impactSound", "#sfx:205299624"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Assassin Dagger Projectile — the sounds it makes fighting",
                Creature = "Assassin Dagger Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1721858547"),
                    new Value("attackSounds.impactSound", "#sfx:-2065720536"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Atlantian Worm Sword — the sounds it makes fighting",
                Creature = "Atlantian Worm Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-635370443"),
                    new Value("attackSounds.impactSound", "#sfx:1508436233"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Basic Staff — the sounds it makes fighting",
                Creature = "Basic Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1282287865"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1895054634"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-613388671"),
                }
            },
            new Preset
            {
                Name = "Basic Staff Projectile — the sounds it makes fighting",
                Creature = "Basic Staff Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1713218607"),
                    new Value("attackSounds.impactSound", "#sfx:1363188206"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Summoned Bat — the way it chases",
                Creature = "Summoned Bat",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "1"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "3"),
                    new Value("pursuit.andAtMostThisFarAway", "8"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Bat — its ranged shot",
                Creature = "Summoned Bat",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "BatMinionProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.25"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.21"),
                    new Value("rangedShape.recoveryAfterShooting", "0.2"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "8"),
                    new Value("rangedMinCooldown", "1"),
                    new Value("rangedMaxCooldown", "1"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.1"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "64"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Bat Minion Projectile — the sounds it makes fighting",
                Creature = "Bat Minion Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:514017545"),
                    new Value("attackSounds.impactSound", "#sfx:-2093636866"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Battle Axe — the sounds it makes fighting",
                Creature = "Battle Axe",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-2101049067"),
                    new Value("attackSounds.impactSound", "#sfx:17654825"),
                    new Value("attackSounds.windUpSound", "#sfx:82326827"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:560548677"),
                }
            },
            new Preset
            {
                Name = "Big Grenade — the sounds it makes fighting",
                Creature = "Big Grenade",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Big Grenade Projectile — the sounds it makes fighting",
                Creature = "Big Grenade Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1732226123"),
                    new Value("attackSounds.impactSound", "#sfx:463347367"),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Big Larva — the way it chases",
                Creature = "Big Larva",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "1"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Big Larva — its close-up swing",
                Creature = "Big Larva",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.5"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.3"),
                    new Value("meleeMaxCooldown", "0.5"),
                    new Value("meleeReach", "1"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "74"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.9"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "10"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Big Larva — the way it wanders",
                Creature = "Big Larva",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Big Larva (from a hive egg) — the way it chases",
                Creature = "Big Larva (from a hive egg)",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "1"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "1"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Big Larva (from a hive egg) — its close-up swing",
                Creature = "Big Larva (from a hive egg)",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.5"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.3"),
                    new Value("meleeMaxCooldown", "0.5"),
                    new Value("meleeReach", "1"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "110"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.9"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "10"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Big Larva (from a hive egg) — the way it wanders",
                Creature = "Big Larva (from a hive egg)",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Blow Dart Projectile — the sounds it makes fighting",
                Creature = "Blow Dart Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1112352286"),
                    new Value("attackSounds.impactSound", "#sfx:-1269921634"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Blowpipe — the sounds it makes fighting",
                Creature = "Blowpipe",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:499127852"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1205425665"),
                }
            },
            new Preset
            {
                Name = "Blue Firefly — the way it wanders",
                Creature = "Blue Firefly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Blue Firefly Persistent — the way it wanders",
                Creature = "Blue Firefly Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Boat Turret Projectile — the sounds it makes fighting",
                Creature = "Boat Turret Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-217796025"),
                    new Value("attackSounds.impactSound", "#sfx:314337682"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bomb Scarab — the way it charges",
                Creature = "Bomb Scarab",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "3"),
                    new Value("@ability.speedMultiplier", "8"),
                    new Value("@ability.chargeShape.endsWithASwing", "0"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "0.5"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "0.4"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0.5"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "3"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "1"),
                    new Value("@ability.chargeShape.passesThroughScenery", "1"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "1"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "226"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "0"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "0"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Bomb Scarab — the way it chases",
                Creature = "Bomb Scarab",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "2.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Bomb Scarab — the way it explodes",
                Creature = "Bomb Scarab",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "1"),
                    new Value("@ability.explosionVariation", "3"),
                    new Value("@ability.healthFraction", "0.5"),
                    new Value("@ability.power", "646"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1.4"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "998"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("@ability.onDeath", "0"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Bomb Scarab — the way it wanders",
                Creature = "Bomb Scarab",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Broken Legendary Sword — the sounds it makes fighting",
                Creature = "Broken Legendary Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1178999969"),
                    new Value("attackSounds.impactSound", "#sfx:-980422243"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bubble Gun — the sounds it makes fighting",
                Creature = "Bubble Gun",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1515350363"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bubble Player Projectile — the sounds it makes fighting",
                Creature = "Bubble Player Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-461267584"),
                    new Value("attackSounds.impactSound", "#sfx:397130670"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bubble Projectile — the sounds it makes fighting",
                Creature = "Bubble Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-295211978"),
                    new Value("attackSounds.impactSound", "#sfx:899894246"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bullet Hell Projectile — the sounds it makes fighting",
                Creature = "Bullet Hell Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", "#sfx:1633841492"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bullet Hell Projectile Miner — the sounds it makes fighting",
                Creature = "Bullet Hell Projectile Miner",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:110716109"),
                    new Value("attackSounds.impactSound", "#sfx:1633841492"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bullet Hell Projectile Two Void — the sounds it makes fighting",
                Creature = "Bullet Hell Projectile Two Void",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", "#sfx:1633841492"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Bunny Poison Dart Projectile — the sounds it makes fighting",
                Creature = "Bunny Poison Dart Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:514017545"),
                    new Value("attackSounds.impactSound", "#sfx:-2093636866"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Burnzooka — the sounds it makes fighting",
                Creature = "Burnzooka",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Burnzooka Projectile — the sounds it makes fighting",
                Creature = "Burnzooka Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:773870543"),
                    new Value("attackSounds.impactSound", "#sfx:-251509151"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Butterfly Base — the way it wanders",
                Creature = "Butterfly Base",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0"),
                    new Value("maxWanderPause", "0.1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Base Persistent — the way it wanders",
                Creature = "Butterfly Base Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Citrus — the way it wanders",
                Creature = "Butterfly Citrus",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0"),
                    new Value("maxWanderPause", "0.1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Citrus Persistent — the way it wanders",
                Creature = "Butterfly Citrus Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Dreamy — the way it wanders",
                Creature = "Butterfly Dreamy",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0"),
                    new Value("maxWanderPause", "0.1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Dreamy Persistent — the way it wanders",
                Creature = "Butterfly Dreamy Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Icy — the way it wanders",
                Creature = "Butterfly Icy",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0"),
                    new Value("maxWanderPause", "0.1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Icy Persistent — the way it wanders",
                Creature = "Butterfly Icy Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Sunset — the way it wanders",
                Creature = "Butterfly Sunset",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0"),
                    new Value("maxWanderPause", "0.1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Butterfly Sunset Persistent — the way it wanders",
                Creature = "Butterfly Sunset Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Camel Baby — the way it chases",
                Creature = "Camel Baby",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Camel Baby — the way it wanders",
                Creature = "Camel Baby",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "5"),
                    new Value("maxWanderPause", "8"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Camel — the way it chases",
                Creature = "Camel",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Camel — the way it wanders",
                Creature = "Camel",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "5"),
                    new Value("maxWanderPause", "8"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Assassin — the way it chases",
                Creature = "Caveling Assassin",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Assassin — its close-up swing",
                Creature = "Caveling Assassin",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.35"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "2.5"),
                    new Value("meleeSwingLandsAhead", "0.2"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "3"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0.2"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.8"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "226"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "25"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Assassin — the way it wanders",
                Creature = "Caveling Assassin",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Assassin — its ranged shot",
                Creature = "Caveling Assassin",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "AssassinDaggerProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.3"),
                    new Value("rangedShape.howLongItKeepsShooting", "1"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "3"),
                    new Value("rangedMaxDistance", "10"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.3"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0.35"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "20"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "1"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "226"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Caveling Brute — the way it chases",
                Creature = "Caveling Brute",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Brute — its close-up swing",
                Creature = "Caveling Brute",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.56"),
                    new Value("meleeSwingDuration", "0.4"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "0.8"),
                    new Value("meleeMaxCooldown", "1.3"),
                    new Value("meleeReach", "2.5"),
                    new Value("meleeSwingLandsAhead", "1"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0.3"),
                    new Value("meleeShape.onlySwingsInFourDirections", "1"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0"),
                    new Value("meleeShape.hitboxHalfWidth", "1.3"),
                    new Value("meleeShape.hitboxHalfLength", "2"),
                    new Value("meleeDamage", "163"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "1"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "2"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "1"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Brute — the way it wanders",
                Creature = "Caveling Brute",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling — the way it chases",
                Creature = "Caveling",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling — its close-up swing",
                Creature = "Caveling",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.35"),
                    new Value("meleeSwingDuration", "0.35"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "102"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling — the way it wanders",
                Creature = "Caveling",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Gardener — the way it chases",
                Creature = "Caveling Gardener",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "2"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Gardener — its close-up swing",
                Creature = "Caveling Gardener",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.35"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "142"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Gardener — the way it wanders",
                Creature = "Caveling Gardener",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Hunter — the way it chases",
                Creature = "Caveling Hunter",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "6"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "6"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Hunter — its close-up swing",
                Creature = "Caveling Hunter",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.35"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "99"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.7"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Hunter — the way it wanders",
                Creature = "Caveling Hunter",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Hunter — its ranged shot",
                Creature = "Caveling Hunter",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "FlintlockMusketProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "1.95"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "2"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "142"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Caveling Merchant — the way it chases",
                Creature = "Caveling Merchant",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.7"),
                    new Value("pursuit.andAtMostThisFarAway", "0.7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0.5"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Merchant — the way it wanders",
                Creature = "Caveling Merchant",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Mummy — the way it chases",
                Creature = "Caveling Mummy",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Mummy — the way it wanders",
                Creature = "Caveling Mummy",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1.5"),
                    new Value("maxWanderDistance", "2.5"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "4"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Scholar — the way it chases",
                Creature = "Caveling Scholar",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "6"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "4"),
                    new Value("pursuit.andAtMostThisFarAway", "6"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Scholar — its close-up swing",
                Creature = "Caveling Scholar",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.6"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "154"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.75"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Scholar — the way it wanders",
                Creature = "Caveling Scholar",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Scholar — its ranged shot",
                Creature = "Caveling Scholar",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "EnergyProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.6"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "2"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "205"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Caveling Shaman — the way it chases",
                Creature = "Caveling Shaman",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "6"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "4"),
                    new Value("pursuit.andAtMostThisFarAway", "6"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Shaman — its close-up swing",
                Creature = "Caveling Shaman",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.6"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "77"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.75"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Shaman — the way it wanders",
                Creature = "Caveling Shaman",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Shaman — its ranged shot",
                Creature = "Caveling Shaman",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "FireballProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.6"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "2"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "102"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Caveling Skirmisher — the way it chases",
                Creature = "Caveling Skirmisher",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "2"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "3"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Skirmisher — its ranged shot",
                Creature = "Caveling Skirmisher",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "SpearProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.3"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.25"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "64"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Caveling Spearman — the way it chases",
                Creature = "Caveling Spearman",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "2"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "3"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Spearman — its close-up swing",
                Creature = "Caveling Spearman",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.3"),
                    new Value("meleeSwingDuration", "0.35"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "2"),
                    new Value("meleeSwingLandsAhead", "0.8"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.7"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "64"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Caveling Spearman — the way it wanders",
                Creature = "Caveling Spearman",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "5"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Chaos Staff — the sounds it makes fighting",
                Creature = "Chaos Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1084922649"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1744047456"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1774219193"),
                }
            },
            new Preset
            {
                Name = "Charm Grenade — the sounds it makes fighting",
                Creature = "Charm Grenade",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Charm Grenade Projectile — the sounds it makes fighting",
                Creature = "Charm Grenade Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1732226123"),
                    new Value("attackSounds.impactSound", "#sfx:463347367"),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Cicada — the way it charges",
                Creature = "Cicada",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "4"),
                    new Value("@ability.speedMultiplier", "3.3"),
                    new Value("@ability.chargeShape.endsWithASwing", "1"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "2.5"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "1"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "0.6"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0"),
                    new Value("@ability.chargeShape.vulnerableFor", "0.1"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0.8"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0.8"),
                    new Value("@ability.minCooldown", "2"),
                    new Value("@ability.maxCooldown", "3"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "5"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "1"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "1"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.6"),
                    new Value("@ability.chargeShape.hitRadius", "2"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "381"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1.3"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "1"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "1"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "3629"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "4"),
                }
            },
            new Preset
            {
                Name = "Nature Cicada — the way it charges",
                Creature = "Nature Cicada",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "6"),
                    new Value("@ability.speedMultiplier", "3.3"),
                    new Value("@ability.chargeShape.endsWithASwing", "1"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "2.5"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "1"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "0.6"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0"),
                    new Value("@ability.chargeShape.vulnerableFor", "0.1"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0.8"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0.8"),
                    new Value("@ability.minCooldown", "3"),
                    new Value("@ability.maxCooldown", "5"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "5"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "1"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "1"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.6"),
                    new Value("@ability.chargeShape.hitRadius", "2"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "226"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "1"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "1"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "2458"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "4"),
                }
            },
            new Preset
            {
                Name = "Cicada Nymph — the way it chases",
                Creature = "Cicada Nymph",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "1"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0.4"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Cicada Nymph — its close-up swing",
                Creature = "Cicada Nymph",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.5"),
                    new Value("meleeSwingDuration", "0.2"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "0.4"),
                    new Value("meleeMaxCooldown", "0.6"),
                    new Value("meleeReach", "2"),
                    new Value("meleeSwingLandsAhead", "0.5"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "439"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1.5"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "1"),
                    new Value("meleeShape.lungeForce", "20"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "1"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Cicada Nymph — the way it wanders",
                Creature = "Cicada Nymph",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Contributor Item Daresiel Sword — the sounds it makes fighting",
                Creature = "Contributor Item Daresiel Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-2078168682"),
                    new Value("attackSounds.impactSound", "#sfx:132662442"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Copper Sledge — the sounds it makes fighting",
                Creature = "Copper Sledge",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:620738550"),
                    new Value("attackSounds.impactSound", "#sfx:-1489610038"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Copper Sword — the sounds it makes fighting",
                Creature = "Copper Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2140410945"),
                    new Value("attackSounds.impactSound", "#sfx:-60950147"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "The Core (boss) — the way it chases",
                Creature = "The Core (boss)",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "1"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "1"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "The Core (boss) — its ranged shot",
                Creature = "The Core (boss)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "1"),
                    new Value("projectileItemId", "CoreBossScarabProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.8"),
                    new Value("rangedShape.howLongItKeepsShooting", "3"),
                    new Value("rangedShape.recoveryAfterShooting", "2"),
                    new Value("rangedMinDistance", "1"),
                    new Value("rangedMaxDistance", "30"),
                    new Value("rangedMinCooldown", "1"),
                    new Value("rangedMaxCooldown", "1"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "1"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "1"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "1"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0.5"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "1"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "338"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "The Core's orb — its ranged shot",
                Creature = "The Core's orb",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "CoreBossElectricProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "0.15"),
                    new Value("rangedWindUp", "1"),
                    new Value("rangedShape.howLongItKeepsShooting", "Infinity"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "0"),
                    new Value("rangedMaxCooldown", "0"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "1"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "-0.15"),
                    new Value("timeBetweenShots", "3.5"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "72"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "3"),
                    new Value("projectilesPerShot", "5"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "270"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Core Boss Scarab Projectile — the sounds it makes fighting",
                Creature = "Core Boss Scarab Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1201953498"),
                    new Value("attackSounds.impactSound", "#sfx:675440459"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Core Boss Whirlwind Projectile — the sounds it makes fighting",
                Creature = "Core Boss Whirlwind Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1149212555"),
                    new Value("attackSounds.impactSound", "#sfx:-635192120"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Cow Baby — the way it chases",
                Creature = "Cow Baby",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Cow Baby — the way it wanders",
                Creature = "Cow Baby",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Cow — the way it chases",
                Creature = "Cow",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Cow — the way it wanders",
                Creature = "Cow",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Crab — the way it chases",
                Creature = "Crab",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0.5"),
                    new Value("pursuit.betweenMidChaseIdles", "1"),
                    new Value("pursuit.startsSideSteppingWithin", "3"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "2"),
                    new Value("pursuit.andAtMostThisFarAway", "3"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Crab — the way it wanders",
                Creature = "Crab",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Crab — its ranged shot",
                Creature = "Crab",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "BubbleProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "1.5"),
                    new Value("rangedShape.recoveryAfterShooting", "0.5"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "3"),
                    new Value("rangedMaxCooldown", "4"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0.4"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "25"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "1"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "165"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.9"),
                }
            },
            new Preset
            {
                Name = "Critter Beetle — the way it wanders",
                Creature = "Critter Beetle",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Beetle Persistent — the way it wanders",
                Creature = "Critter Beetle Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Centipede — the way it wanders",
                Creature = "Critter Centipede",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Centipede Persistent — the way it wanders",
                Creature = "Critter Centipede Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Cockroach — the way it wanders",
                Creature = "Critter Cockroach",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Cockroach Persistent — the way it wanders",
                Creature = "Critter Cockroach Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Crab2 — the way it wanders",
                Creature = "Critter Crab2",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Crab2 Persistent — the way it wanders",
                Creature = "Critter Crab2 Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Crab — the way it wanders",
                Creature = "Critter Crab",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Crab Persistent — the way it wanders",
                Creature = "Critter Crab Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Grasshopper — the way it wanders",
                Creature = "Critter Grasshopper",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Grasshopper Persistent — the way it wanders",
                Creature = "Critter Grasshopper Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Larva — the way it wanders",
                Creature = "Critter Larva",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Larva Persistent — the way it wanders",
                Creature = "Critter Larva Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Larva Void — the way it wanders",
                Creature = "Critter Larva Void",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Larva Void Persistent — the way it wanders",
                Creature = "Critter Larva Void Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Newt — the way it wanders",
                Creature = "Critter Newt",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Newt Persistent — the way it wanders",
                Creature = "Critter Newt Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Passage Fly — the way it wanders",
                Creature = "Critter Passage Fly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "12"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "0.5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Passage Fly Persistent — the way it wanders",
                Creature = "Critter Passage Fly Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "12"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "0.5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Scorpion — the way it wanders",
                Creature = "Critter Scorpion",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Scorpion Persistent — the way it wanders",
                Creature = "Critter Scorpion Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Shrew — the way it wanders",
                Creature = "Critter Shrew",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Shrew Persistent — the way it wanders",
                Creature = "Critter Shrew Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Snoot Fly — the way it wanders",
                Creature = "Critter Snoot Fly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Snoot Fly Persistent — the way it wanders",
                Creature = "Critter Snoot Fly Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Tiny Snail — the way it wanders",
                Creature = "Critter Tiny Snail",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Tiny Snail Persistent — the way it wanders",
                Creature = "Critter Tiny Snail Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Worm — the way it wanders",
                Creature = "Critter Worm",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Critter Worm Persistent — the way it wanders",
                Creature = "Critter Worm Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "2"),
                    new Value("minWanderPause", "0.3"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Crystal Big Snail — the way it wanders",
                Creature = "Crystal Big Snail",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Crystal Merchant — the way it chases",
                Creature = "Crystal Merchant",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.7"),
                    new Value("pursuit.andAtMostThisFarAway", "0.7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0.5"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Crystal Merchant — the way it wanders",
                Creature = "Crystal Merchant",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Cupid Arrow Projectile — the sounds it makes fighting",
                Creature = "Cupid Arrow Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1863248332"),
                    new Value("attackSounds.impactSound", "#sfx:-606880592"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Cupid Bow — the sounds it makes fighting",
                Creature = "Cupid Bow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-258703700"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Deflected Projectile — the sounds it makes fighting",
                Creature = "Deflected Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-987302868"),
                    new Value("attackSounds.impactSound", "#sfx:-924126460"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Desert Brute — the way it charges",
                Creature = "Desert Brute",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "0"),
                    new Value("@ability.speedMultiplier", "8"),
                    new Value("@ability.chargeShape.endsWithASwing", "1"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "5"),
                    new Value("@ability.windUp", "1"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "0"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "1"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "1"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1.5"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "0"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "2"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "211"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1.3"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "0"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "0"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Desert Brute — the way it chases",
                Creature = "Desert Brute",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Desert Brute — its close-up swing",
                Creature = "Desert Brute",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.56"),
                    new Value("meleeSwingDuration", "0.4"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "0.8"),
                    new Value("meleeMaxCooldown", "1.3"),
                    new Value("meleeReach", "2.5"),
                    new Value("meleeSwingLandsAhead", "1"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0.3"),
                    new Value("meleeShape.onlySwingsInFourDirections", "1"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0"),
                    new Value("meleeShape.hitboxHalfWidth", "1.3"),
                    new Value("meleeShape.hitboxHalfLength", "2"),
                    new Value("meleeDamage", "163"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "1"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "2"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "1"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Desert Brute — the way it wanders",
                Creature = "Desert Brute",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Dodo Baby — the way it chases",
                Creature = "Dodo Baby",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Dodo Baby — the way it wanders",
                Creature = "Dodo Baby",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Dodo — the way it chases",
                Creature = "Dodo",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Dodo — the way it wanders",
                Creature = "Dodo",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Electric Projectile — the sounds it makes fighting",
                Creature = "Electric Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-319779117"),
                    new Value("attackSounds.impactSound", "#sfx:-440284429"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Energy Projectile — the sounds it makes fighting",
                Creature = "Energy Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-987302868"),
                    new Value("attackSounds.impactSound", "#sfx:-924126460"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Explosive Barrel — the way it explodes",
                Creature = "Explosive Barrel",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "2.5"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "1"),
                    new Value("@ability.power", "400"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "2000"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("@ability.onDeath", "1"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "1"),
                }
            },
            new Preset
            {
                Name = "Fire Grenade — the sounds it makes fighting",
                Creature = "Fire Grenade",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Fire Grenade Projectile — the sounds it makes fighting",
                Creature = "Fire Grenade Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1732226123"),
                    new Value("attackSounds.impactSound", "#sfx:463347367"),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Fireball Explosive Projectile — the sounds it makes fighting",
                Creature = "Fireball Explosive Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1195602842"),
                    new Value("attackSounds.impactSound", "#sfx:-362119110"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Fireball Projectile — the sounds it makes fighting",
                Creature = "Fireball Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1195602842"),
                    new Value("attackSounds.impactSound", "#sfx:-362119110"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Fireball Staff — the sounds it makes fighting",
                Creature = "Fireball Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-657388364"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1861489081"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1904964714"),
                }
            },
            new Preset
            {
                Name = "Fishing Merchant — the way it chases",
                Creature = "Fishing Merchant",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.7"),
                    new Value("pursuit.andAtMostThisFarAway", "0.7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0.5"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Fishing Merchant — the way it wanders",
                Creature = "Fishing Merchant",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "2.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Flamethrower Turret — its sweeping ray",
                Creature = "Flamethrower Turret",
                Kind = "Ray",
                Values = new Value[]
                {
                    new Value("moreCombat.rayStartsAtARandomAngle", "0"),
                    new Value("moreCombat.raySpinSpeed", "0"),
                    new Value("moreCombat.rayLength", "5"),
                    new Value("moreCombat.rayGrowSeconds", "1"),
                    new Value("moreCombat.rayShrinkSeconds", "0"),
                    new Value("moreCombat.rayStartsOutAt", "0.5"),
                    new Value("moreCombat.rayThickness", "0.2"),
                    new Value("moreCombat.rayDamage", "207"),
                    new Value("moreCombat.rayMultiplier", "1"),
                    new Value("moreCombat.rayDoesNotTurn", "0"),
                    new Value("moreCombat.rayIsRanged", "1"),
                    new Value("moreCombat.rayIsMagic", "0"),
                    new Value("moreCombat.rayTotalSeconds", "0.1"),
                    new Value("moreCombat.rayWindUpSeconds", "0.1"),
                    new Value("moreCombat.raySweepSeconds", "Infinity"),
                    new Value("moreCombat.rayRecoverySeconds", "0.1"),
                }
            },
            new Preset
            {
                Name = "Flintlock Musket — the sounds it makes fighting",
                Creature = "Flintlock Musket",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Flintlock Musket Projectile — the sounds it makes fighting",
                Creature = "Flintlock Musket Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-531596180"),
                    new Value("attackSounds.impactSound", "#sfx:-1106000868"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Galaxite Chakram — the sounds it makes fighting",
                Creature = "Galaxite Chakram",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1515350363"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Galaxite Chakram Projectile — the sounds it makes fighting",
                Creature = "Galaxite Chakram Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1770168999"),
                    new Value("attackSounds.impactSound", "#sfx:-1305735686"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Galaxite Dagger — the sounds it makes fighting",
                Creature = "Galaxite Dagger",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1309146663"),
                    new Value("attackSounds.impactSound", "#sfx:842132709"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Galaxite Energy Projectile — the sounds it makes fighting",
                Creature = "Galaxite Energy Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-100427031"),
                    new Value("attackSounds.impactSound", "#sfx:601612373"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Galaxite Sledge — the sounds it makes fighting",
                Creature = "Galaxite Sledge",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:620738550"),
                    new Value("attackSounds.impactSound", "#sfx:-1489610038"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Galaxite Sword — the sounds it makes fighting",
                Creature = "Galaxite Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1255869780"),
                    new Value("attackSounds.impactSound", "#sfx:921509776"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Galaxite Turret (facing back) — its ranged shot",
                Creature = "Galaxite Turret (facing back)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "GalaxiteEnergyProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.2"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "248"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Galaxite Turret (facing front) — its ranged shot",
                Creature = "Galaxite Turret (facing front)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "GalaxiteEnergyProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.2"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "248"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Galaxite Turret (facing left) — its ranged shot",
                Creature = "Galaxite Turret (facing left)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "GalaxiteEnergyProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.2"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "248"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Galaxite Turret (facing right) — its ranged shot",
                Creature = "Galaxite Turret (facing right)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "GalaxiteEnergyProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.2"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "248"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Ghost Caveling — the way it chases",
                Creature = "Ghost Caveling",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Ghost Caveling — its close-up swing",
                Creature = "Ghost Caveling",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.35"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "184"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Ghost Caveling — the way it wanders",
                Creature = "Ghost Caveling",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Ghost Scholar — the way it chases",
                Creature = "Ghost Scholar",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "6"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "4"),
                    new Value("pursuit.andAtMostThisFarAway", "6"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Ghost Scholar — its close-up swing",
                Creature = "Ghost Scholar",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.6"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "138"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.75"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Ghost Scholar — the way it wanders",
                Creature = "Ghost Scholar",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Ghost Scholar — its ranged shot",
                Creature = "Ghost Scholar",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "EnergyProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.6"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "2"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "184"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Goat Baby — the way it chases",
                Creature = "Goat Baby",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Goat Baby — the way it wanders",
                Creature = "Goat Baby",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Goat — the way it chases",
                Creature = "Goat",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Goat — the way it wanders",
                Creature = "Goat",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Golden Bomb Scarab — the way it charges",
                Creature = "Golden Bomb Scarab",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "3.5"),
                    new Value("@ability.speedMultiplier", "7"),
                    new Value("@ability.chargeShape.endsWithASwing", "0"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "0.5"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "0.8"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0.6"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "3"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "1"),
                    new Value("@ability.chargeShape.passesThroughScenery", "1"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "1"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "1"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "226"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "0"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "0"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Golden Bomb Scarab — the way it chases",
                Creature = "Golden Bomb Scarab",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "2.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Golden Bomb Scarab — the way it explodes",
                Creature = "Golden Bomb Scarab",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "1"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "0.5"),
                    new Value("@ability.power", "646"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1.4"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "998"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("@ability.onDeath", "0"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Golden Bomb Scarab — the way it wanders",
                Creature = "Golden Bomb Scarab",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Gravity Projectile — the sounds it makes fighting",
                Creature = "Gravity Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-987302868"),
                    new Value("attackSounds.impactSound", "#sfx:-924126460"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Green Firefly — the way it wanders",
                Creature = "Green Firefly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Green Firefly Persistent — the way it wanders",
                Creature = "Green Firefly Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Grubzooka — the sounds it makes fighting",
                Creature = "Grubzooka",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Grubzooka Projectile — the sounds it makes fighting",
                Creature = "Grubzooka Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-496499820"),
                    new Value("attackSounds.impactSound", "#sfx:225936973"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Hand Mortar — the sounds it makes fighting",
                Creature = "Hand Mortar",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1707939638"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Hand Mortar Projectile — the sounds it makes fighting",
                Creature = "Hand Mortar Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1705731781"),
                    new Value("attackSounds.impactSound", "#sfx:1264977070"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Hive Big Larva — the way it chases",
                Creature = "Hive Big Larva",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "1"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Big Larva — its close-up swing",
                Creature = "Hive Big Larva",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.5"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.3"),
                    new Value("meleeMaxCooldown", "0.5"),
                    new Value("meleeReach", "1"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "110"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.9"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "10"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Big Larva — the way it wanders",
                Creature = "Hive Big Larva",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Larva — the way it chases",
                Creature = "Hive Larva",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Larva — its close-up swing",
                Creature = "Hive Larva",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.4"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.1"),
                    new Value("meleeMaxCooldown", "2"),
                    new Value("meleeReach", "2"),
                    new Value("meleeSwingLandsAhead", "0"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "61"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.6"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "13"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "0"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Larva — the way it wanders",
                Creature = "Hive Larva",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Larva (from a boss egg) — the way it chases",
                Creature = "Hive Larva (from a boss egg)",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "1"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Larva (from a boss egg) — its close-up swing",
                Creature = "Hive Larva (from a boss egg)",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.4"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.1"),
                    new Value("meleeMaxCooldown", "2"),
                    new Value("meleeReach", "2"),
                    new Value("meleeSwingLandsAhead", "0"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "73"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.6"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "13"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "0"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Hive Larva (from a boss egg) — the way it wanders",
                Creature = "Hive Larva (from a boss egg)",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Hunting Spear — the sounds it makes fighting",
                Creature = "Hunting Spear",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1881833774"),
                    new Value("attackSounds.impactSound", "#sfx:203199470"),
                    new Value("attackSounds.windUpSound", "#sfx:2086898086"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-608077438"),
                }
            },
            new Preset
            {
                Name = "Hydra Bone Sword — the sounds it makes fighting",
                Creature = "Hydra Bone Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:542682359"),
                    new Value("attackSounds.impactSound", "#sfx:-1550761525"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Desert Hydra — its close-up swing",
                Creature = "Desert Hydra",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "1"),
                    new Value("meleeSwingDuration", "2"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "5"),
                    new Value("meleeSwingLandsAhead", "3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "1"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "2"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "293"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "2"),
                    new Value("meleePushForce", "2"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "1"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Hydra's ice shard — its ranged shot",
                Creature = "Hydra's ice shard",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "IceShardProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "1"),
                    new Value("rangedShape.howLongItKeepsShooting", "Infinity"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "0"),
                    new Value("rangedMaxCooldown", "0"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "1"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "-0.15"),
                    new Value("timeBetweenShots", "0.15"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "5"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "4"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "162"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.6"),
                }
            },
            new Preset
            {
                Name = "Nature Hydra — its close-up swing",
                Creature = "Nature Hydra",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "1"),
                    new Value("meleeSwingDuration", "2"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "5"),
                    new Value("meleeSwingLandsAhead", "3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "1"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "2"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "293"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "2"),
                    new Value("meleePushForce", "2"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "1"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Sea Hydra — its close-up swing",
                Creature = "Sea Hydra",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "1"),
                    new Value("meleeSwingDuration", "2"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "5"),
                    new Value("meleeSwingLandsAhead", "3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "1"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "2"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "293"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "2"),
                    new Value("meleePushForce", "2"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "1"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Void Hydra — its close-up swing",
                Creature = "Void Hydra",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "1"),
                    new Value("meleeSwingDuration", "2"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "5"),
                    new Value("meleeSwingLandsAhead", "3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "1"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "2"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "338"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "2"),
                    new Value("meleePushForce", "2"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "1"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Ice Shard Projectile — the sounds it makes fighting",
                Creature = "Ice Shard Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1573302259"),
                    new Value("attackSounds.impactSound", "#sfx:2050097796"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Infected Caveling — the way it chases",
                Creature = "Infected Caveling",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Infected Caveling — its close-up swing",
                Creature = "Infected Caveling",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.5"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "163"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Infected Caveling — the way it wanders",
                Creature = "Infected Caveling",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1.5"),
                    new Value("maxWanderDistance", "2.5"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "4"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Iron Arrow Projectile — the sounds it makes fighting",
                Creature = "Iron Arrow Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2116087123"),
                    new Value("attackSounds.impactSound", "#sfx:149658032"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Iron Bow — the sounds it makes fighting",
                Creature = "Iron Bow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-258703700"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Iron Halberd Spear — the sounds it makes fighting",
                Creature = "Iron Halberd Spear",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1881833774"),
                    new Value("attackSounds.impactSound", "#sfx:203199470"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Iron Sledge — the sounds it makes fighting",
                Creature = "Iron Sledge",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:620738550"),
                    new Value("attackSounds.impactSound", "#sfx:-1489610038"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Iron Sword — the sounds it makes fighting",
                Creature = "Iron Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:374311674"),
                    new Value("attackSounds.impactSound", "#sfx:-1786353722"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Jellyfish Projectile — the sounds it makes fighting",
                Creature = "Jellyfish Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:678586926"),
                    new Value("attackSounds.impactSound", "#sfx:638174902"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Larva — the way it chases",
                Creature = "Larva",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Larva — its close-up swing",
                Creature = "Larva",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.4"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.1"),
                    new Value("meleeMaxCooldown", "2"),
                    new Value("meleeReach", "2"),
                    new Value("meleeSwingLandsAhead", "0"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "38"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.6"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "13"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "0"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Larva — the way it wanders",
                Creature = "Larva",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Larva Spike Club — the sounds it makes fighting",
                Creature = "Larva Spike Club",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1780527880"),
                    new Value("attackSounds.impactSound", "#sfx:370581956"),
                    new Value("attackSounds.windUpSound", "#sfx:1033959017"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:585077688"),
                }
            },
            new Preset
            {
                Name = "Lava Axe — the sounds it makes fighting",
                Creature = "Lava Axe",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1643870925"),
                    new Value("attackSounds.impactSound", "#sfx:500000783"),
                    new Value("attackSounds.windUpSound", "#sfx:-1347443748"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:1134894075"),
                }
            },
            new Preset
            {
                Name = "Lava Butterfly — the way it chases",
                Creature = "Lava Butterfly",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "3"),
                    new Value("pursuit.andAtMostThisFarAway", "6"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Lava Butterfly — the way it wanders",
                Creature = "Lava Butterfly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Lava Butterfly — its ranged shot",
                Creature = "Lava Butterfly",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "SmallFireballProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.16666667"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "2"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.35"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "30"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "248"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Lava Mortar — the sounds it makes fighting",
                Creature = "Lava Mortar",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1707939638"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-889632180"),
                }
            },
            new Preset
            {
                Name = "Lava Slime Blob — the way it chases",
                Creature = "Lava Slime Blob",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Lava Slime Blob — its leaping attack",
                Creature = "Lava Slime Blob",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.7"),
                    new Value("@ability.power", "248"),
                    new Value("@ability.leapHitsThisHardForItsTier", "1"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "0"),
                    new Value("@ability.minCooldown", "0"),
                    new Value("@ability.maxCooldown", "0"),
                    new Value("@ability.range", "3"),
                }
            },
            new Preset
            {
                Name = "Lava Slime Blob — the way it wanders",
                Creature = "Lava Slime Blob",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Lava Slime Boss — its ranged shot",
                Creature = "Lava Slime Boss",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "LavaSlimeBossProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0"),
                    new Value("rangedShape.howLongItKeepsShooting", "1"),
                    new Value("rangedShape.recoveryAfterShooting", "3.5"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "5"),
                    new Value("rangedMaxCooldown", "7"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "1"),
                    new Value("rangedShape.muzzleDistance", "10"),
                    new Value("rangedShape.muzzleDistanceVariation", "2"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "1.1"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "360"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "1"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "1"),
                    new Value("rangedShape.firesAtItself", "1"),
                    new Value("rangedShape.healsItsOwnSideBy", "0.2"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "497"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "2"),
                }
            },
            new Preset
            {
                Name = "Lava Slime Boss Projectile — the sounds it makes fighting",
                Creature = "Lava Slime Boss Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-2010735367"),
                    new Value("attackSounds.impactSound", "#sfx:1042208055"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Legendary Bow — the sounds it makes fighting",
                Creature = "Legendary Bow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-627182789"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1897504916"),
                }
            },
            new Preset
            {
                Name = "Legendary Energy Projectile — the sounds it makes fighting",
                Creature = "Legendary Energy Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-543484442"),
                    new Value("attackSounds.impactSound", "#sfx:-1287559861"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Legendary Mortar — the sounds it makes fighting",
                Creature = "Legendary Mortar",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1707939638"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1713742252"),
                }
            },
            new Preset
            {
                Name = "Legendary Staff Projectile — the sounds it makes fighting",
                Creature = "Legendary Staff Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-604213914"),
                    new Value("attackSounds.impactSound", "#sfx:1593671955"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Legendary Sword — the sounds it makes fighting",
                Creature = "Legendary Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1838783722"),
                    new Value("attackSounds.impactSound", "#sfx:38064714"),
                    new Value("attackSounds.windUpSound", "#sfx:-724843208"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:512091904"),
                }
            },
            new Preset
            {
                Name = "Meteor Staff — the sounds it makes fighting",
                Creature = "Meteor Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-657388364"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1861489081"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1904964714"),
                }
            },
            new Preset
            {
                Name = "Meteor Staff Lesser — the sounds it makes fighting",
                Creature = "Meteor Staff Lesser",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:898160552"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1861489081"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1904964714"),
                }
            },
            new Preset
            {
                Name = "Mimite — the way it charges",
                Creature = "Mimite",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "0"),
                    new Value("@ability.speedMultiplier", "16"),
                    new Value("@ability.chargeShape.endsWithASwing", "0"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "1"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "3"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "1"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1.3"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0.5"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "0"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "1"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "0.5"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "270"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "1"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "1205"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1.5"),
                }
            },
            new Preset
            {
                Name = "Mimite — the way it wanders",
                Creature = "Mimite",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1"),
                    new Value("maxWanderPause", "1.5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Minigun — the sounds it makes fighting",
                Creature = "Minigun",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1294074153"),
                    new Value("attackSounds.windUpCancelledSound", "#sfx:-1149112427"),
                    new Value("attackSounds.strongAttackSound", "#sfx:1649617308"),
                }
            },
            new Preset
            {
                Name = "Minigun Projectile — the sounds it makes fighting",
                Creature = "Minigun Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1070760767"),
                    new Value("attackSounds.impactSound", "#sfx:611093303"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Minion Explosive — the way it chases",
                Creature = "Minion Explosive",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Fire Mite — the way it chases",
                Creature = "Summoned Fire Mite",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Fire Mite — its close-up swing",
                Creature = "Summoned Fire Mite",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.2"),
                    new Value("meleeSwingDuration", "0.53"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "1"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.4"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "1"),
                    new Value("meleeHitRadius", "0.8"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "97"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.8"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "19"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Fighter — the way it chases",
                Creature = "Summoned Fighter",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Fighter — its close-up swing",
                Creature = "Summoned Fighter",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.3"),
                    new Value("meleeSwingDuration", "0.53"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "1"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.4"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "1"),
                    new Value("meleeHitRadius", "0.8"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "83"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Miner — the way it chases",
                Creature = "Summoned Miner",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Miner — its close-up swing",
                Creature = "Summoned Miner",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.1"),
                    new Value("meleeSwingDuration", "0.65"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "1"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.4"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "1"),
                    new Value("meleeHitRadius", "0.8"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "97"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.8"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "19"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Poison Minion — the way it chases",
                Creature = "Summoned Poison Minion",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "1"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "3"),
                    new Value("pursuit.andAtMostThisFarAway", "8"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Summoned Poison Minion — its ranged shot",
                Creature = "Summoned Poison Minion",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "PoisonMinionProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.25"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.21"),
                    new Value("rangedShape.recoveryAfterShooting", "0.2"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "8"),
                    new Value("rangedMinCooldown", "1"),
                    new Value("rangedMaxCooldown", "1"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "20"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "2"),
                    new Value("projectilesPerShot", "3"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "142"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Mold Projectile — the sounds it makes fighting",
                Creature = "Mold Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:143804949"),
                    new Value("attackSounds.impactSound", "#sfx:-1759326286"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Mold Tentacle — its ranged shot",
                Creature = "Mold Tentacle",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "MoldProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.35"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "130"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.8"),
                }
            },
            new Preset
            {
                Name = "Mummy Mortar Projectile — the sounds it makes fighting",
                Creature = "Mummy Mortar Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1289646704"),
                    new Value("attackSounds.impactSound", "#sfx:172690394"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Mushroom Brute — the way it charges",
                Creature = "Mushroom Brute",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "0"),
                    new Value("@ability.speedMultiplier", "12"),
                    new Value("@ability.chargeShape.endsWithASwing", "0"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "0.5"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "3"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0.8"),
                    new Value("@ability.chargeShape.vulnerableFor", "2"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0.8"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0.8"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1.5"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "1"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "0"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "0.75"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "58"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "0.9"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "1"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "166"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1.5"),
                }
            },
            new Preset
            {
                Name = "Mushroom Brute — the way it wanders",
                Creature = "Mushroom Brute",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1"),
                    new Value("maxWanderPause", "1.5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Mushroom — the way it charges",
                Creature = "Mushroom",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "0"),
                    new Value("@ability.speedMultiplier", "8"),
                    new Value("@ability.chargeShape.endsWithASwing", "0"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "1"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "3"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "1"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1.3"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0.5"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "0"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "0.5"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "26"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "0.9"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "0"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "0"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Mushroom — the way it wanders",
                Creature = "Mushroom",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1"),
                    new Value("maxWanderPause", "1.5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Octarine Arrow Projectile — the sounds it makes fighting",
                Creature = "Octarine Arrow Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:217766155"),
                    new Value("attackSounds.impactSound", "#sfx:1831443010"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Octarine Axe — the sounds it makes fighting",
                Creature = "Octarine Axe",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:900786002"),
                    new Value("attackSounds.impactSound", "#sfx:-1233581458"),
                    new Value("attackSounds.windUpSound", "#sfx:82326827"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:560548677"),
                }
            },
            new Preset
            {
                Name = "Octarine Bow — the sounds it makes fighting",
                Creature = "Octarine Bow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-258703700"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Octarine Sledge — the sounds it makes fighting",
                Creature = "Octarine Sledge",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:620738550"),
                    new Value("attackSounds.impactSound", "#sfx:-1489610038"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Octarine Sword — the sounds it makes fighting",
                Creature = "Octarine Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1589121117"),
                    new Value("attackSounds.impactSound", "#sfx:579732127"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Octopus Boss — its ranged shot",
                Creature = "Octopus Boss",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "OctopusBossProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "3"),
                    new Value("rangedShape.recoveryAfterShooting", "0.5"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "3"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "1"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0.25"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "70"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "1"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "205"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Octopus Boss Player Projectile — the sounds it makes fighting",
                Creature = "Octopus Boss Player Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1189947816"),
                    new Value("attackSounds.impactSound", "#sfx:-1610714621"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Octopus Boss Projectile — the sounds it makes fighting",
                Creature = "Octopus Boss Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1149212555"),
                    new Value("attackSounds.impactSound", "#sfx:-635192120"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Octopus tentacle — its close-up swing",
                Creature = "Octopus tentacle",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.6"),
                    new Value("meleeSwingDuration", "0.1"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "2"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "4"),
                    new Value("meleeSwingLandsAhead", "1.5"),
                    new Value("meleeShape.hitboxOffset.x", "0.5"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0.5"),
                    new Value("meleeShape.onlySwingsInFourDirections", "1"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "1"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0"),
                    new Value("meleeShape.hitboxHalfWidth", "1"),
                    new Value("meleeShape.hitboxHalfLength", "1.5"),
                    new Value("meleeDamage", "248"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "1"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "10"),
                    new Value("meleePushForce", "3"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "1"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "1"),
                    new Value("meleeShape.ignoresTheDamageCap", "1"),
                }
            },
            new Preset
            {
                Name = "Oil Grenade — the sounds it makes fighting",
                Creature = "Oil Grenade",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Oil Grenade Projectile — the sounds it makes fighting",
                Creature = "Oil Grenade Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1732226123"),
                    new Value("attackSounds.impactSound", "#sfx:463347367"),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Orbital Turret — the way it chases",
                Creature = "Orbital Turret",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "6"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "4"),
                    new Value("pursuit.andAtMostThisFarAway", "6"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Orbital Turret — the way it wanders",
                Creature = "Orbital Turret",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Orbital Turret — its ranged shot",
                Creature = "Orbital Turret",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "OrbitalTurretProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.3333333"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3999667"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "3"),
                    new Value("rangedMaxDistance", "10"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "5"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "1"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0.3"),
                    new Value("timeBetweenShots", "0.35"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "2"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "4"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "216"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.8"),
                }
            },
            new Preset
            {
                Name = "Orbital Turret Projectile — the sounds it makes fighting",
                Creature = "Orbital Turret Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", "#sfx:-924126460"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Pandorium Axe — the sounds it makes fighting",
                Creature = "Pandorium Axe",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1643870925"),
                    new Value("attackSounds.impactSound", "#sfx:500000783"),
                    new Value("attackSounds.windUpSound", "#sfx:-1347443748"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:1134894075"),
                }
            },
            new Preset
            {
                Name = "Pet Bird — the way it wanders",
                Creature = "Pet Bird",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.5"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Bunny — the way it chases",
                Creature = "Pet Bunny",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "3"),
                    new Value("pursuit.andAtMostThisFarAway", "7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Bunny — the way it wanders",
                Creature = "Pet Bunny",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.5"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Bunny — its ranged shot",
                Creature = "Pet Bunny",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "BunnyPoisonDartProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.42"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.21"),
                    new Value("rangedShape.recoveryAfterShooting", "0.2"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "1"),
                    new Value("rangedMaxCooldown", "1"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.1"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "122"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Pet Cat — the way it chases",
                Creature = "Pet Cat",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "3"),
                    new Value("pursuit.andAtMostThisFarAway", "7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Cat — the way it wanders",
                Creature = "Pet Cat",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "5"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Cat — its ranged shot",
                Creature = "Pet Cat",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "SmallFireballProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.33"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.5"),
                    new Value("rangedShape.recoveryAfterShooting", "0.2"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "1"),
                    new Value("rangedMaxCooldown", "1"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "122"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Pet Dog — the way it chases",
                Creature = "Pet Dog",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Dog — its close-up swing",
                Creature = "Pet Dog",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.3"),
                    new Value("meleeSwingDuration", "0.53"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "1"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.5"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "1"),
                    new Value("meleeHitRadius", "1"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "122"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Dog — the way it wanders",
                Creature = "Pet Dog",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Electric Pet — the way it chases",
                Creature = "Electric Pet",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Electric Pet — its close-up swing",
                Creature = "Electric Pet",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.3"),
                    new Value("meleeSwingDuration", "0.53"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "1"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.5"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "1"),
                    new Value("meleeHitRadius", "1"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "122"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Electric Pet — the way it wanders",
                Creature = "Electric Pet",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.5"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Lava Slime — the way it chases",
                Creature = "Pet Lava Slime",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Lava Slime — its leaping attack",
                Creature = "Pet Lava Slime",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "79"),
                    new Value("@ability.leapHitsThisHardForItsTier", "0.65"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "1"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.range", "2"),
                }
            },
            new Preset
            {
                Name = "Pet Lava Slime — the way it wanders",
                Creature = "Pet Lava Slime",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.25"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Magic — the way it wanders",
                Creature = "Pet Magic",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.5"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Moth — the way it wanders",
                Creature = "Pet Moth",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.5"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Poison Slime — the way it chases",
                Creature = "Pet Poison Slime",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Poison Slime — its leaping attack",
                Creature = "Pet Poison Slime",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "79"),
                    new Value("@ability.leapHitsThisHardForItsTier", "0.65"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "1"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.range", "2"),
                }
            },
            new Preset
            {
                Name = "Pet Poison Slime — the way it wanders",
                Creature = "Pet Poison Slime",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.25"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Prince Slime — the way it chases",
                Creature = "Pet Prince Slime",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Prince Slime — its leaping attack",
                Creature = "Pet Prince Slime",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "79"),
                    new Value("@ability.leapHitsThisHardForItsTier", "0.65"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "1"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.range", "2"),
                }
            },
            new Preset
            {
                Name = "Pet Prince Slime — the way it wanders",
                Creature = "Pet Prince Slime",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.25"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Slime — the way it chases",
                Creature = "Pet Slime",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Slime — its leaping attack",
                Creature = "Pet Slime",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "79"),
                    new Value("@ability.leapHitsThisHardForItsTier", "0.65"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "1"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.range", "2"),
                }
            },
            new Preset
            {
                Name = "Pet Slime — the way it wanders",
                Creature = "Pet Slime",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.25"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Slippery Slime — the way it chases",
                Creature = "Pet Slippery Slime",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Slippery Slime — its leaping attack",
                Creature = "Pet Slippery Slime",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "79"),
                    new Value("@ability.leapHitsThisHardForItsTier", "0.65"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "1"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "1"),
                    new Value("@ability.range", "2"),
                }
            },
            new Preset
            {
                Name = "Pet Slippery Slime — the way it wanders",
                Creature = "Pet Slippery Slime",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.25"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Tardigrade — the way it chases",
                Creature = "Pet Tardigrade",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.25"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "1.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Tardigrade — its close-up swing",
                Creature = "Pet Tardigrade",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.3"),
                    new Value("meleeSwingDuration", "0.53"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "1"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "1"),
                    new Value("meleeHitRadius", "0.8"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "378"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1.2"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "5"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Tardigrade — the way it wanders",
                Creature = "Pet Tardigrade",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.25"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Pet Warlock — the way it wanders",
                Creature = "Pet Warlock",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "2"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "10"),
                    new Value("maxWanderPause", "30"),
                    new Value("wanderSpeedMultiplier", "0.25"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Poison Grenade — the sounds it makes fighting",
                Creature = "Poison Grenade",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Poison Grenade Projectile — the sounds it makes fighting",
                Creature = "Poison Grenade Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1732226123"),
                    new Value("attackSounds.impactSound", "#sfx:463347367"),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Poison Minion Projectile — the sounds it makes fighting",
                Creature = "Poison Minion Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:514017545"),
                    new Value("attackSounds.impactSound", "#sfx:-2093636866"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Poison Slime Blob — its leaping attack",
                Creature = "Poison Slime Blob",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "142"),
                    new Value("@ability.leapHitsThisHardForItsTier", "1"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "0"),
                    new Value("@ability.minCooldown", "0"),
                    new Value("@ability.maxCooldown", "0"),
                    new Value("@ability.range", "3"),
                }
            },
            new Preset
            {
                Name = "Poison Slime Blob — the way it wanders",
                Creature = "Poison Slime Blob",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Poison Slime Blob (drops nothing) — the way it chases",
                Creature = "Poison Slime Blob (drops nothing)",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Poison Slime Blob (drops nothing) — its leaping attack",
                Creature = "Poison Slime Blob (drops nothing)",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "142"),
                    new Value("@ability.leapHitsThisHardForItsTier", "1"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "0"),
                    new Value("@ability.minCooldown", "0"),
                    new Value("@ability.maxCooldown", "0"),
                    new Value("@ability.range", "3"),
                }
            },
            new Preset
            {
                Name = "Poison Slime Blob (drops nothing) — the way it wanders",
                Creature = "Poison Slime Blob (drops nothing)",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Poisonous Sickle — the sounds it makes fighting",
                Creature = "Poisonous Sickle",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2140410945"),
                    new Value("attackSounds.impactSound", "#sfx:-60950147"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Purple Firefly — the way it wanders",
                Creature = "Purple Firefly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Purple Firefly Persistent — the way it wanders",
                Creature = "Purple Firefly Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Razor Flake — the sounds it makes fighting",
                Creature = "Razor Flake",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1789583055"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Razor Flake Projectile — the sounds it makes fighting",
                Creature = "Razor Flake Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1770168999"),
                    new Value("attackSounds.impactSound", "#sfx:-1305735686"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Razor Flake Shard Projectile — the sounds it makes fighting",
                Creature = "Razor Flake Shard Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1770168999"),
                    new Value("attackSounds.impactSound", "#sfx:-1305735686"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Red Firefly — the way it wanders",
                Creature = "Red Firefly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Red Firefly Persistent — the way it wanders",
                Creature = "Red Firefly Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Ritual Dagger — the sounds it makes fighting",
                Creature = "Ritual Dagger",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1179146890"),
                    new Value("attackSounds.impactSound", "#sfx:981354570"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Robot Boss — the way it chases",
                Creature = "Robot Boss",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "1"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "0"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Robot Boss — its ranged shot",
                Creature = "Robot Boss",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "BulletHellProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "0.8"),
                    new Value("rangedShape.animationPerShot", "rangedAttackFire"),
                    new Value("rangedWindUp", "2"),
                    new Value("rangedShape.howLongItKeepsShooting", "1"),
                    new Value("rangedShape.recoveryAfterShooting", "1"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "20"),
                    new Value("rangedMinCooldown", "6"),
                    new Value("rangedMaxCooldown", "10"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "1"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0.1"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "55"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "3"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "1"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0.75"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "315"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Robot Boss Large Projectile — the sounds it makes fighting",
                Creature = "Robot Boss Large Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", "#sfx:1633841492"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Robot Boss Large Projectile One Void — the sounds it makes fighting",
                Creature = "Robot Boss Large Projectile One Void",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", "#sfx:1633841492"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Robot Boss Void Chaser — the way it chases",
                Creature = "Robot Boss Void Chaser",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "0"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Robot Miner — the way it charges",
                Creature = "Robot Miner",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "0"),
                    new Value("@ability.speedMultiplier", "18"),
                    new Value("@ability.chargeShape.endsWithASwing", "0"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "1"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "1"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0.5"),
                    new Value("@ability.minCooldown", "3"),
                    new Value("@ability.maxCooldown", "3"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1.3"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0.5"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "1"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "1"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "1"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "135"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "0.5"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "441"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1.4"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "1"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "1526"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1.5"),
                }
            },
            new Preset
            {
                Name = "Robot Miner — the way it wanders",
                Creature = "Robot Miner",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "10"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Robot Miner — its ranged shot",
                Creature = "Robot Miner",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "BulletHellProjectile"),
                    new Value("rangedShape.projectileVariation", "1"),
                    new Value("rangedShape.projectileSpeedMultiplier", "0.8"),
                    new Value("rangedShape.animationName", "start"),
                    new Value("rangedShape.animationPerShot", "rangedAttack"),
                    new Value("rangedWindUp", "1"),
                    new Value("rangedShape.howLongItKeepsShooting", "1"),
                    new Value("rangedShape.recoveryAfterShooting", "1"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "12"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0.1"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "1"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "30"),
                    new Value("rangedShape.widestSpreadDegrees", "75"),
                    new Value("rangedShape.shotPattern", "5"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "347"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1.1"),
                }
            },
            new Preset
            {
                Name = "Robot Patroller — the way it chases",
                Creature = "Robot Patroller",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "6"),
                    new Value("pursuit.andAtMostThisFarAway", "8"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Robot Patroller — the way it wanders",
                Creature = "Robot Patroller",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "10"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Robot Swarmer — the way it chases",
                Creature = "Robot Swarmer",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Robot Swarmer — its close-up swing",
                Creature = "Robot Swarmer",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.2"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.3"),
                    new Value("meleeMaxCooldown", "0.8"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "189"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.6"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "18"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "1"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "0"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Robot Swarmer — the way it wanders",
                Creature = "Robot Swarmer",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Roly Poly Baby — the way it chases",
                Creature = "Roly Poly Baby",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Roly Poly Baby — the way it wanders",
                Creature = "Roly Poly Baby",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "10"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Roly Poly — the way it chases",
                Creature = "Roly Poly",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Roly Poly — the way it wanders",
                Creature = "Roly Poly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "10"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Royal Slime Blob — the way it chases",
                Creature = "Royal Slime Blob",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Royal Slime Blob — its leaping attack",
                Creature = "Royal Slime Blob",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "102"),
                    new Value("@ability.leapHitsThisHardForItsTier", "1"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "0"),
                    new Value("@ability.minCooldown", "0"),
                    new Value("@ability.maxCooldown", "0"),
                    new Value("@ability.range", "3"),
                }
            },
            new Preset
            {
                Name = "Royal Slime Blob — the way it wanders",
                Creature = "Royal Slime Blob",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Rusty Dagger — the sounds it makes fighting",
                Creature = "Rusty Dagger",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:909757135"),
                    new Value("attackSounds.impactSound", "#sfx:-1242552333"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarab Boss's bomb scarab — the way it charges",
                Creature = "Scarab Boss's bomb scarab",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "3"),
                    new Value("@ability.speedMultiplier", "8"),
                    new Value("@ability.chargeShape.endsWithASwing", "0"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "0"),
                    new Value("@ability.chargeShape.swingsIfWithin", "0"),
                    new Value("@ability.chargeShape.endingSwingLunge", "0"),
                    new Value("@ability.windUp", "0.5"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0"),
                    new Value("@ability.duration", "0.4"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0.5"),
                    new Value("@ability.minCooldown", "1"),
                    new Value("@ability.maxCooldown", "3"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "1"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "1"),
                    new Value("@ability.chargeShape.passesThroughScenery", "1"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "1"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "226"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "0"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "0"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "0"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Scarab Boss's bomb scarab — the way it chases",
                Creature = "Scarab Boss's bomb scarab",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "2.5"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Scarab Boss's bomb scarab — the way it explodes",
                Creature = "Scarab Boss's bomb scarab",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "1"),
                    new Value("@ability.explosionVariation", "3"),
                    new Value("@ability.healthFraction", "0.5"),
                    new Value("@ability.power", "646"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1.4"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "998"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("@ability.onDeath", "0"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Scarab Boss's bomb scarab — the way it wanders",
                Creature = "Scarab Boss's bomb scarab",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Scarab Boss — its ranged shot",
                Creature = "Scarab Boss",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "ScarabBossProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.6"),
                    new Value("rangedShape.howLongItKeepsShooting", "3"),
                    new Value("rangedShape.recoveryAfterShooting", "2"),
                    new Value("rangedMinDistance", "1"),
                    new Value("rangedMaxDistance", "30"),
                    new Value("rangedMinCooldown", "4"),
                    new Value("rangedMaxCooldown", "6"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "1"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "1"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "1"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0.5"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "-2"),
                    new Value("timeBetweenShots", "0.5"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "1"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "248"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Scarab Boss Player Projectile — the sounds it makes fighting",
                Creature = "Scarab Boss Player Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1201953498"),
                    new Value("attackSounds.impactSound", "#sfx:675440459"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarab Boss Projectile — the sounds it makes fighting",
                Creature = "Scarab Boss Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:851265548"),
                    new Value("attackSounds.impactSound", "#sfx:-210548624"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarab Mortar — the sounds it makes fighting",
                Creature = "Scarab Mortar",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1515350363"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarab Mortar Projectile — the sounds it makes fighting",
                Creature = "Scarab Mortar Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1994927904"),
                    new Value("attackSounds.impactSound", "#sfx:1517571259"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarlet Bolt Projectile — the sounds it makes fighting",
                Creature = "Scarlet Bolt Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2116087123"),
                    new Value("attackSounds.impactSound", "#sfx:149658032"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarlet Crossbow — the sounds it makes fighting",
                Creature = "Scarlet Crossbow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Scarlet Dagger — the sounds it makes fighting",
                Creature = "Scarlet Dagger",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1861499588"),
                    new Value("attackSounds.impactSound", "#sfx:-314714120"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarlet Sledge — the sounds it makes fighting",
                Creature = "Scarlet Sledge",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:620738550"),
                    new Value("attackSounds.impactSound", "#sfx:-1489610038"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scarlet Sword — the sounds it makes fighting",
                Creature = "Scarlet Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-638571520"),
                    new Value("attackSounds.impactSound", "#sfx:1513734460"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Scatter Grenade — the sounds it makes fighting",
                Creature = "Scatter Grenade",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Scatter Grenade Projectile — the sounds it makes fighting",
                Creature = "Scatter Grenade Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1732226123"),
                    new Value("attackSounds.impactSound", "#sfx:463347367"),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Scholar Staff — the sounds it makes fighting",
                Creature = "Scholar Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-537375102"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-1895054634"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-613388671"),
                }
            },
            new Preset
            {
                Name = "Seasonal Merchant — the way it chases",
                Creature = "Seasonal Merchant",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.7"),
                    new Value("pursuit.andAtMostThisFarAway", "0.7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Seasonal Merchant — the way it wanders",
                Creature = "Seasonal Merchant",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Shaman Boss — the way it chases",
                Creature = "Shaman Boss",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "1"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Shaman Boss — its close-up swing",
                Creature = "Shaman Boss",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.54"),
                    new Value("meleeSwingDuration", "1"),
                    new Value("meleeShape.damageLandsAfter", "0.2"),
                    new Value("meleeMinCooldown", "2"),
                    new Value("meleeMaxCooldown", "2"),
                    new Value("meleeReach", "4"),
                    new Value("meleeSwingLandsAhead", "1"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0.3"),
                    new Value("meleeShape.onlySwingsInFourDirections", "1"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0"),
                    new Value("meleeShape.hitboxHalfWidth", "1"),
                    new Value("meleeShape.hitboxHalfLength", "2"),
                    new Value("meleeDamage", "92"),
                    new Value("meleeShape.hitsThisHardForItsTier", "0.9"),
                    new Value("meleeBreaksTiles", "1"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "5"),
                    new Value("meleePushForce", "3"),
                    new Value("meleeShape.lungeForce", "25"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "1"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", "FireTrap"),
                    new Value("meleeShape.hitsLowObstacles", "1"),
                    new Value("meleeShape.ignoresTheDamageCap", "1"),
                }
            },
            new Preset
            {
                Name = "Shaman Boss — its ranged shot",
                Creature = "Shaman Boss",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "FireballProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.3"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.6"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0.75"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "92"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.9"),
                }
            },
            new Preset
            {
                Name = "Shard Club — the sounds it makes fighting",
                Creature = "Shard Club",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1780527880"),
                    new Value("attackSounds.impactSound", "#sfx:370581956"),
                    new Value("attackSounds.windUpSound", "#sfx:-1895054634"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:585077688"),
                }
            },
            new Preset
            {
                Name = "Shock Wave Projectile — the sounds it makes fighting",
                Creature = "Shock Wave Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-934054221"),
                    new Value("attackSounds.impactSound", "#sfx:-1470691245"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Slime Blob — its leaping attack",
                Creature = "Slime Blob",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "0.45"),
                    new Value("@ability.power", "19"),
                    new Value("@ability.leapHitsThisHardForItsTier", "0.65"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "0"),
                    new Value("@ability.minCooldown", "0"),
                    new Value("@ability.maxCooldown", "0"),
                    new Value("@ability.range", "3"),
                }
            },
            new Preset
            {
                Name = "Slime Blob — the way it wanders",
                Creature = "Slime Blob",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Slime Merchant — the way it chases",
                Creature = "Slime Merchant",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.7"),
                    new Value("pursuit.andAtMostThisFarAway", "0.7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Slime Merchant — the way it wanders",
                Creature = "Slime Merchant",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Slime Projectile — the sounds it makes fighting",
                Creature = "Slime Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1468545025"),
                    new Value("attackSounds.impactSound", "#sfx:-1918434883"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Slime Staff — the sounds it makes fighting",
                Creature = "Slime Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1664015483"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-568481013"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Slime Sword — the sounds it makes fighting",
                Creature = "Slime Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-800428564"),
                    new Value("attackSounds.impactSound", "#sfx:1401127120"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Slingshot — the sounds it makes fighting",
                Creature = "Slingshot",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-258703700"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Slingshot Projectile — the sounds it makes fighting",
                Creature = "Slingshot Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-287663688"),
                    new Value("attackSounds.impactSound", "#sfx:1067669556"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Slippery Slime Blob — its leaping attack",
                Creature = "Slippery Slime Blob",
                Kind = "Jump",
                Values = new Value[]
                {
                    new Value("@ability.kind", "1"),
                    new Value("@ability.windUp", "0.55"),
                    new Value("@ability.duration", "1"),
                    new Value("@ability.power", "184"),
                    new Value("@ability.leapHitsThisHardForItsTier", "1"),
                    new Value("@ability.speedMultiplier", "100"),
                    new Value("@ability.leapOnlyHitsEnemiesAndPlayers", "0"),
                    new Value("@ability.minCooldown", "0"),
                    new Value("@ability.maxCooldown", "0"),
                    new Value("@ability.range", "3"),
                }
            },
            new Preset
            {
                Name = "Slippery Slime Blob — the way it wanders",
                Creature = "Slippery Slime Blob",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Slippery Slime Boss Bubble Projectile — the sounds it makes fighting",
                Creature = "Slippery Slime Boss Bubble Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1394260493"),
                    new Value("attackSounds.impactSound", "#sfx:-554696720"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Slippery Slime Boss — its ranged shot",
                Creature = "Slippery Slime Boss",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "BubbleProjectile"),
                    new Value("rangedShape.projectileVariation", "1"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "3"),
                    new Value("rangedShape.recoveryAfterShooting", "0.5"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "0"),
                    new Value("rangedMinCooldown", "3"),
                    new Value("rangedMaxCooldown", "6"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0.1"),
                    new Value("rangedShape.keepsAimingWhileShooting", "1"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "180"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "1"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "184"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Slippery Slime Sword — the sounds it makes fighting",
                Creature = "Slippery Slime Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-407387399"),
                    new Value("attackSounds.impactSound", "#sfx:1685991365"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Small Fireball Projectile — the sounds it makes fighting",
                Creature = "Small Fireball Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:898932427"),
                    new Value("attackSounds.impactSound", "#sfx:-160155979"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Small Tentacle — its ranged shot",
                Creature = "Small Tentacle",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "JellyfishProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.35"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "3"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "184"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Snowball Projectile — the sounds it makes fighting",
                Creature = "Snowball Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1912259015"),
                    new Value("attackSounds.impactSound", "#sfx:-7990079"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Solarite Bolt Projectile — the sounds it makes fighting",
                Creature = "Solarite Bolt Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-2027385556"),
                    new Value("attackSounds.impactSound", "#sfx:81879056"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Solarite Crossbow — the sounds it makes fighting",
                Creature = "Solarite Crossbow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Solarite Mining Pick — the sounds it makes fighting",
                Creature = "Solarite Mining Pick",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1990017997"),
                    new Value("attackSounds.impactSound", "#sfx:178999567"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Solarite Sword — the sounds it makes fighting",
                Creature = "Solarite Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:825011163"),
                    new Value("attackSounds.impactSound", "#sfx:-1293596953"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:560548677"),
                }
            },
            new Preset
            {
                Name = "Spear Projectile — the sounds it makes fighting",
                Creature = "Spear Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:750818519"),
                    new Value("attackSounds.impactSound", "#sfx:-496222480"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Stone Mortar — the sounds it makes fighting",
                Creature = "Stone Mortar",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1707939638"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-1713742252"),
                }
            },
            new Preset
            {
                Name = "Stun Grenade — the sounds it makes fighting",
                Creature = "Stun Grenade",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Stun Grenade Projectile — the sounds it makes fighting",
                Creature = "Stun Grenade Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:1732226123"),
                    new Value("attackSounds.impactSound", "#sfx:463347367"),
                    new Value("attackSounds.windUpSound", "#sfx:2050639877"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-366784939"),
                }
            },
            new Preset
            {
                Name = "Sulfur Bud — the way it explodes",
                Creature = "Sulfur Bud",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "2.5"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "1"),
                    new Value("@ability.power", "590"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "1"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "2646"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("@ability.onDeath", "1"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Sun Staff — the sounds it makes fighting",
                Creature = "Sun Staff",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2074594741"),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-2005679996"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:764772183"),
                }
            },
            new Preset
            {
                Name = "Sun Staff Projectile — the sounds it makes fighting",
                Creature = "Sun Staff Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-604213914"),
                    new Value("attackSounds.impactSound", "#sfx:1593671955"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Temple Turret (facing back) — its ranged shot",
                Creature = "Temple Turret (facing back)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "IronArrowProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "136"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.6"),
                }
            },
            new Preset
            {
                Name = "Temple Turret (facing front) — its ranged shot",
                Creature = "Temple Turret (facing front)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "IronArrowProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "136"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.6"),
                }
            },
            new Preset
            {
                Name = "Temple Turret (facing left) — its ranged shot",
                Creature = "Temple Turret (facing left)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "IronArrowProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "136"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.6"),
                }
            },
            new Preset
            {
                Name = "Temple Turret (facing right) — its ranged shot",
                Creature = "Temple Turret (facing right)",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "IronArrowProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.5"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.2"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "0.5"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "15"),
                    new Value("rangedDamage", "136"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "0.6"),
                }
            },
            new Preset
            {
                Name = "Tentacle Whip — the sounds it makes fighting",
                Creature = "Tentacle Whip",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2001890502"),
                    new Value("attackSounds.impactSound", "#sfx:-191135238"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Throwing Daggers Projectile — the sounds it makes fighting",
                Creature = "Throwing Daggers Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Tin Axe — the sounds it makes fighting",
                Creature = "Tin Axe",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:642946839"),
                    new Value("attackSounds.impactSound", "#sfx:-1516537301"),
                    new Value("attackSounds.windUpSound", "#sfx:82326827"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:560548677"),
                }
            },
            new Preset
            {
                Name = "Tin Dagger — the sounds it makes fighting",
                Creature = "Tin Dagger",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:909757135"),
                    new Value("attackSounds.impactSound", "#sfx:-1242552333"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Tin Sledge — the sounds it makes fighting",
                Creature = "Tin Sledge",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:620738550"),
                    new Value("attackSounds.impactSound", "#sfx:-1489610038"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Tin Sword — the sounds it makes fighting",
                Creature = "Tin Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2085369405"),
                    new Value("attackSounds.impactSound", "#sfx:-8004863"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Turtle Baby — the way it chases",
                Creature = "Turtle Baby",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Turtle Baby — the way it wanders",
                Creature = "Turtle Baby",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "10"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Turtle — the way it chases",
                Creature = "Turtle",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.9"),
                    new Value("pursuit.andAtMostThisFarAway", "0.9"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "1"),
                }
            },
            new Preset
            {
                Name = "Turtle — the way it wanders",
                Creature = "Turtle",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "3"),
                    new Value("maxWanderDistance", "10"),
                    new Value("maxWanderDuration", "20"),
                    new Value("minWanderPause", "3"),
                    new Value("maxWanderPause", "10"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Brute — the way it charges",
                Creature = "Void Caveling Brute",
                Kind = "Charge",
                Values = new Value[]
                {
                    new Value("@ability.kind", "0"),
                    new Value("@ability.range", "0"),
                    new Value("@ability.speedMultiplier", "4"),
                    new Value("@ability.chargeShape.endsWithASwing", "1"),
                    new Value("@ability.chargeShape.swingsEvenIfItHitNothing", "1"),
                    new Value("@ability.chargeShape.swingsIfWithin", "2.5"),
                    new Value("@ability.chargeShape.endingSwingLunge", "3"),
                    new Value("@ability.windUp", "1"),
                    new Value("@ability.chargeShape.endingSwingWindUp", "0.56"),
                    new Value("@ability.duration", "1"),
                    new Value("@ability.chargeShape.timeStuckOnImpact", "0"),
                    new Value("@ability.chargeShape.vulnerableFor", "0"),
                    new Value("@ability.chargeShape.endingSwingDuration", "0.4"),
                    new Value("@ability.chargeShape.recoveryAfterwards", "0"),
                    new Value("@ability.minCooldown", "3"),
                    new Value("@ability.maxCooldown", "5"),
                    new Value("@ability.chargeShape.pushesWhatItHits", "2"),
                    new Value("@ability.chargeShape.bouncesBackThisHard", "0"),
                    new Value("@ability.chargeShape.lowObstaclesDoNotStopIt", "0"),
                    new Value("@ability.chargeShape.passesThroughScenery", "0"),
                    new Value("@ability.chargeShape.playsImpactEvenOnAMiss", "0"),
                    new Value("@ability.chargeShape.canSteerMidCharge", "0"),
                    new Value("@ability.chargeShape.stopsSteeringWithin", "1.5"),
                    new Value("@ability.chargeShape.widestSteerDegrees", "180"),
                    new Value("@ability.chargeShape.facingLocksAt", "0.9"),
                    new Value("@ability.chargeShape.hitRadius", "2"),
                    new Value("@ability.chargeShape.hitboxHalfWidth", "0"),
                    new Value("@ability.chargeShape.hitboxHalfLength", "0"),
                    new Value("@ability.chargeShape.hitReach", "0"),
                    new Value("@ability.chargeShape.onlyChargesInFourDirections", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.x", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.y", "0"),
                    new Value("@ability.chargeShape.hitboxOffset.z", "0"),
                    new Value("@ability.power", "884"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "2.3"),
                    new Value("@ability.chargeShape.ploughsThroughTerrain", "0"),
                    new Value("@ability.chargeShape.endingSwingBreaksTerrain", "1"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "1386"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Brute — the way it chases",
                Creature = "Void Caveling Brute",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Brute — its close-up swing",
                Creature = "Void Caveling Brute",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.56"),
                    new Value("meleeSwingDuration", "0.4"),
                    new Value("meleeShape.damageLandsAfter", "0"),
                    new Value("meleeMinCooldown", "0.8"),
                    new Value("meleeMaxCooldown", "1.3"),
                    new Value("meleeReach", "2.5"),
                    new Value("meleeSwingLandsAhead", "1"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0.3"),
                    new Value("meleeShape.onlySwingsInFourDirections", "1"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0"),
                    new Value("meleeShape.hitboxHalfWidth", "1.3"),
                    new Value("meleeShape.hitboxHalfLength", "2"),
                    new Value("meleeDamage", "884"),
                    new Value("meleeShape.hitsThisHardForItsTier", "2.3"),
                    new Value("meleeBreaksTiles", "1"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "2"),
                    new Value("meleeShape.lungeForce", "0"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "1"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Brute — the way it wanders",
                Creature = "Void Caveling Brute",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "5"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Miner — the way it chases",
                Creature = "Void Caveling Miner",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "1.5"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.5"),
                    new Value("pursuit.andAtMostThisFarAway", "2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Miner — its close-up swing",
                Creature = "Void Caveling Miner",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.35"),
                    new Value("meleeSwingDuration", "0.35"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "2"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "631"),
                    new Value("meleeShape.hitsThisHardForItsTier", "2"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Miner — the way it wanders",
                Creature = "Void Caveling Miner",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Shaman — the way it chases",
                Creature = "Void Caveling Shaman",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "1"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "6"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "4"),
                    new Value("pursuit.andAtMostThisFarAway", "6"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "2"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Shaman — its close-up swing",
                Creature = "Void Caveling Shaman",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.6"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "1"),
                    new Value("meleeMaxCooldown", "3"),
                    new Value("meleeReach", "1.5"),
                    new Value("meleeSwingLandsAhead", "0.3"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.6"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "473"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1.5"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "15"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "0"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "1"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Shaman — the way it wanders",
                Creature = "Void Caveling Shaman",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Shaman — its ranged shot",
                Creature = "Void Caveling Shaman",
                Kind = "Ranged",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("projectileItemId", "VoidCavelingShamanProjectile"),
                    new Value("rangedShape.projectileVariation", "0"),
                    new Value("rangedShape.projectileSpeedMultiplier", "1"),
                    new Value("rangedWindUp", "0.6"),
                    new Value("rangedShape.howLongItKeepsShooting", "0.3"),
                    new Value("rangedShape.recoveryAfterShooting", "0"),
                    new Value("rangedMinDistance", "2"),
                    new Value("rangedMaxDistance", "15"),
                    new Value("rangedMinCooldown", "2"),
                    new Value("rangedMaxCooldown", "2"),
                    new Value("rangedShape.shootsAtWhatItCannotSee", "0"),
                    new Value("rangedShape.eachShotPicksItsOwnTarget", "0"),
                    new Value("rangedShape.onlyShootsThingsItWantsToAttack", "0"),
                    new Value("rangedShape.takingAHitInterruptsIt", "0"),
                    new Value("rangedShape.onlyShootsWhenInCombat", "0"),
                    new Value("rangedShape.muzzleDistance", "0.5"),
                    new Value("rangedShape.muzzleDistanceVariation", "0"),
                    new Value("rangedShape.muzzleOffset.x", "0"),
                    new Value("rangedShape.muzzleOffset.y", "0"),
                    new Value("rangedShape.muzzleOffset.z", "0"),
                    new Value("timeBetweenShots", "0"),
                    new Value("rangedShape.keepsAimingWhileShooting", "0"),
                    new Value("rangedShape.locksAimDuringTheWindUp", "0"),
                    new Value("rangedShape.spreadStartsAtDegrees", "0"),
                    new Value("spreadAngle", "0"),
                    new Value("rangedShape.widestSpreadDegrees", "0"),
                    new Value("rangedShape.shotPattern", "0"),
                    new Value("projectilesPerShot", "1"),
                    new Value("rangedShape.shotsFollowTheirTarget", "0"),
                    new Value("rangedShape.firesAtItself", "0"),
                    new Value("rangedShape.healsItsOwnSideBy", "0"),
                    new Value("rangedShape.alsoHurtsThingsTouchingIt", "0"),
                    new Value("rangedShape.speedChangesWithDistance", "0"),
                    new Value("rangedShape.speedFromNearToFar.x", "0"),
                    new Value("rangedShape.speedFromNearToFar.y", "0"),
                    new Value("rangedShape.nearAndFarDistance.x", "0"),
                    new Value("rangedShape.nearAndFarDistance.y", "0"),
                    new Value("rangedShape.startsLeadingTargetsAt", "0"),
                    new Value("rangedShape.stopsLeadingTargetsAt", "0"),
                    new Value("rangedShape.aimConeDegrees", "0"),
                    new Value("rangedDamage", "631"),
                    new Value("rangedShape.shotsHitThisHardForItsTier", "2"),
                }
            },
            new Preset
            {
                Name = "Void Caveling Shaman Fire Ball Projectile — the sounds it makes fighting",
                Creature = "Void Caveling Shaman Fire Ball Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", "#sfx:-362119110"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Void Club — the sounds it makes fighting",
                Creature = "Void Club",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1780527880"),
                    new Value("attackSounds.impactSound", "#sfx:370581956"),
                    new Value("attackSounds.windUpSound", "#sfx:-1895054634"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:585077688"),
                }
            },
            new Preset
            {
                Name = "Void Larva — the way it chases",
                Creature = "Void Larva",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.2"),
                    new Value("pursuit.andAtMostThisFarAway", "0.2"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0"),
                    new Value("pursuit.prefersPathfinding", "0"),
                    new Value("pursuit.neverGivesUp", "0"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Void Larva — its close-up swing",
                Creature = "Void Larva",
                Kind = "Melee",
                Values = new Value[]
                {
                    new Value("meleeWindUp", "0.4"),
                    new Value("meleeSwingDuration", "0.3"),
                    new Value("meleeShape.damageLandsAfter", "0.05"),
                    new Value("meleeMinCooldown", "0.1"),
                    new Value("meleeMaxCooldown", "2"),
                    new Value("meleeReach", "2"),
                    new Value("meleeSwingLandsAhead", "0"),
                    new Value("meleeShape.hitboxOffset.x", "0"),
                    new Value("meleeShape.hitboxOffset.y", "0"),
                    new Value("meleeShape.hitboxOffset.z", "0"),
                    new Value("meleeShape.onlySwingsInFourDirections", "0"),
                    new Value("meleeShape.swingsAtWhatItCannotSee", "0"),
                    new Value("meleeHits", "1"),
                    new Value("meleeShape.givesUpOnAPlayerAfter", "0"),
                    new Value("meleeShape.onlyHitsEnemiesAndPlayers", "0"),
                    new Value("meleeHitRadius", "0.5"),
                    new Value("meleeShape.hitboxHalfWidth", "0"),
                    new Value("meleeShape.hitboxHalfLength", "0"),
                    new Value("meleeDamage", "315"),
                    new Value("meleeShape.hitsThisHardForItsTier", "1"),
                    new Value("meleeBreaksTiles", "0"),
                    new Value("meleeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("meleePushForce", "0"),
                    new Value("meleeShape.lungeForce", "13"),
                    new Value("meleeShape.alwaysLungesAtFullForce", "1"),
                    new Value("meleeShape.cannotTurnDuringTheWindUp", "0"),
                    new Value("meleeShape.cannotTurnDuringTheHit", "0"),
                    new Value("meleeShape.spawnsOnBrokenTilesId", ""),
                    new Value("meleeShape.hitsLowObstacles", "0"),
                    new Value("meleeShape.ignoresTheDamageCap", "0"),
                }
            },
            new Preset
            {
                Name = "Void Larva — the way it wanders",
                Creature = "Void Larva",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "1"),
                    new Value("maxWanderDistance", "3"),
                    new Value("maxWanderDuration", "3"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Void Merchant — the way it chases",
                Creature = "Void Merchant",
                Kind = "Chase",
                Values = new Value[]
                {
                    new Value("pursuit.startsSwitchedOff", "0"),
                    new Value("pursuit.chasesWhatItCannotSee", "0"),
                    new Value("pursuit.lowObstaclesDoNotStopIt", "0"),
                    new Value("pursuit.pauseBeforeChasing", "0"),
                    new Value("pursuit.keepsGoingAfterLosingIt", "0"),
                    new Value("pursuit.idlesMidChaseFor", "0"),
                    new Value("pursuit.betweenMidChaseIdles", "0"),
                    new Value("pursuit.startsSideSteppingWithin", "0"),
                    new Value("pursuit.keepsQuietAboutItsDistance", "0"),
                    new Value("pursuit.keepsAtLeastThisFarAway", "0.7"),
                    new Value("pursuit.andAtMostThisFarAway", "0.7"),
                    new Value("pursuit.looksAheadToAvoidObstacles", "0.5"),
                    new Value("pursuit.prefersPathfinding", "1"),
                    new Value("pursuit.neverGivesUp", "1"),
                    new Value("pursuit.needsAPathToChase", "0"),
                }
            },
            new Preset
            {
                Name = "Void Merchant — the way it wanders",
                Creature = "Void Merchant",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "2"),
                    new Value("maxWanderDistance", "4"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "1.5"),
                    new Value("maxWanderPause", "3"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Void Orb Gun — the sounds it makes fighting",
                Creature = "Void Orb Gun",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Void Orb Gun Projectile — the sounds it makes fighting",
                Creature = "Void Orb Gun Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1942730155"),
                    new Value("attackSounds.impactSound", "#sfx:-512088030"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Desert explosive block — the way it explodes",
                Creature = "Desert explosive block",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "45"),
                    new Value("@ability.duration", "0.7"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "1"),
                    new Value("@ability.power", "369"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "0.8"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "2688"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1.5"),
                    new Value("@ability.onDeath", "1"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Explosive block — the way it explodes",
                Creature = "Explosive block",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "2.5"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "1"),
                    new Value("@ability.power", "101"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "0.7"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "644"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "2"),
                    new Value("@ability.onDeath", "1"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Forest explosive block — the way it explodes",
                Creature = "Forest explosive block",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "2.5"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "1"),
                    new Value("@ability.power", "148"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "0.5"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "910"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "1"),
                    new Value("@ability.onDeath", "1"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Natural explosive block — the way it explodes",
                Creature = "Natural explosive block",
                Kind = "Explode",
                Values = new Value[]
                {
                    new Value("@ability.kind", "3"),
                    new Value("@ability.range", "20"),
                    new Value("@ability.duration", "2.5"),
                    new Value("@ability.explosionVariation", "0"),
                    new Value("@ability.healthFraction", "1"),
                    new Value("@ability.power", "101"),
                    new Value("@ability.chargeShape.hitsThisHardForItsTier", "0.7"),
                    new Value("@ability.chargeShape.flatTerrainDamage", "644"),
                    new Value("@ability.chargeShape.breaksTerrainThisHardForItsTier", "2"),
                    new Value("@ability.onDeath", "1"),
                    new Value("@ability.dropsItsLootWhenItBlowsUp", "0"),
                }
            },
            new Preset
            {
                Name = "Wood Arrow Projectile — the sounds it makes fighting",
                Creature = "Wood Arrow Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:950631078"),
                    new Value("attackSounds.impactSound", "#sfx:-981854540"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Wood Bolt Projectile — the sounds it makes fighting",
                Creature = "Wood Bolt Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:2116087123"),
                    new Value("attackSounds.impactSound", "#sfx:149658032"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Wood Bow — the sounds it makes fighting",
                Creature = "Wood Bow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:-258703700"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-178952987"),
                }
            },
            new Preset
            {
                Name = "Wood Crossbow — the sounds it makes fighting",
                Creature = "Wood Crossbow",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", ""),
                    new Value("attackSounds.impactSound", ""),
                    new Value("attackSounds.windUpSound", "#sfx:1660810992"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", "#sfx:-720043838"),
                }
            },
            new Preset
            {
                Name = "Wood Sword — the sounds it makes fighting",
                Creature = "Wood Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-2078168682"),
                    new Value("attackSounds.impactSound", "#sfx:132662442"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "Yellow Firefly — the way it wanders",
                Creature = "Yellow Firefly",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Yellow Firefly Persistent — the way it wanders",
                Creature = "Yellow Firefly Persistent",
                Kind = "Wander",
                Values = new Value[]
                {
                    new Value("minWanderDistance", "5"),
                    new Value("maxWanderDistance", "8"),
                    new Value("maxWanderDuration", "5"),
                    new Value("minWanderPause", "0.5"),
                    new Value("maxWanderPause", "1"),
                    new Value("wanderSpeedMultiplier", "1"),
                    new Value("ownNumbersBeatTheWalkPattern", "0"),
                }
            },
            new Preset
            {
                Name = "Zealot Sword — the sounds it makes fighting",
                Creature = "Zealot Sword",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-1719887379"),
                    new Value("attackSounds.impactSound", "#sfx:448099537"),
                    new Value("attackSounds.windUpSound", "#sfx:2031665162"),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
            new Preset
            {
                Name = "core Boss_Electric Projectile — the sounds it makes fighting",
                Creature = "core Boss_Electric Projectile",
                Kind = "Sounds",
                Values = new Value[]
                {
                    new Value("attackSounds.attackSound", "#sfx:-319779117"),
                    new Value("attackSounds.impactSound", "#sfx:-440284429"),
                    new Value("attackSounds.windUpSound", ""),
                    new Value("attackSounds.windUpCancelledSound", ""),
                    new Value("attackSounds.strongAttackSound", ""),
                }
            },
        };

        /// <summary>The sorts of thing that can be borrowed, in the order they are offered.</summary>
        public static readonly string[] Kinds = new string[]
        {
            "Melee",
            "Ranged",
            "Chase",
            "Wander",
            "Sounds",
            "Charge",
            "Jump",
            "Explode",
            "Ray",
        };

        /// <summary>The presets of one kind, for the picker that offers them.</summary>
        public static Preset[] OfKind(string kind)
        {
            int count = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind)
                {
                    count++;
                }
            }

            Preset[] matching = new Preset[count];
            int next = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind)
                {
                    matching[next++] = All[i];
                }
            }

            return matching;
        }

        /// <summary>The preset with that name, or null when nothing is called that.</summary>
        public static Preset ByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Name == name)
                {
                    return All[i];
                }
            }

            return null;
        }
    }
}
