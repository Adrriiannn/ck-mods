using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Blasts, their triggers, gravity wells and the light and sound around them.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// A patch of ground where nothing can be hurt.
        /// </summary>
        /// <remarks>
        /// Round by default and rectangular on request — the game keeps both shapes on one
        /// component and reads whichever the flag says, so writing both is harmless and writing
        /// neither leaves a zone of nothing.
        /// </remarks>
        public static void ApplyImmunityZone(
            GameObject root,
            bool hasZone,
            float radius,
            bool rectangular,
            int width,
            int height,
            System.Action<string> report)
        {
            if (!hasZone)
            {
                RemoveComponentIfPresent<ImmunityZoneAuthoring>(root);
                return;
            }

            bool coversNothing = rectangular ? (width <= 0 || height <= 0) : radius <= 0f;
            if (coversNothing && report != null)
            {
                report("keeps things safe over an area of nothing, so it protects no one.");
            }

            ImmunityZoneAuthoring zone = EnsureComponent<ImmunityZoneAuthoring>(root);
            zone.radius = radius < 0f ? 0f : radius;
            zone.useRectangularBounds = rectangular;
            zone.rectangularWidth = width < 0 ? 0 : width;
            zone.rectangularHeight = height < 0 ? 0 : height;
        }

        /// <summary>
        /// How gravity wells bend a wandering creature off course.
        /// </summary>
        /// <remarks>
        /// <c>RandomWalkGravityAuthoring</c> mixes config with runtime state — <c>isAffected</c>,
        /// <c>position</c> and <c>timer</c> are what the system writes while the game runs, and
        /// happen to be public. Only the config half is written here; setting the rest would be
        /// baking a moment of play into a prefab.
        /// </remarks>
        public static void ApplyGravityWells(
            GameObject root,
            float chance,
            float strength,
            float range,
            float maxTurn,
            int attractsLayers)
        {
            if (chance <= 0f)
            {
                RemoveComponentIfPresent<RandomWalkGravityAuthoring>(root);
                return;
            }

            RandomWalkGravityAuthoring gravity = EnsureComponent<RandomWalkGravityAuthoring>(root);
            gravity.chanceToBeAffectedByGravityWell = chance;
            gravity.strength = strength;
            gravity.maxDistanceToBeAffected = range;
            gravity.maxAngleDeviation = maxTurn;
            gravity.attractMask = (uint)(attractsLayers < 0 ? 0 : attractsLayers);
        }

        /// <summary>
        /// Makes something go off, and gives it whatever sets it off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The blast is a separate object the explosive names — it is what the player sees and
        /// hears and what actually reaches out. When the author did not name one, the item
        /// generator writes one beside the bomb and the two are linked by name at load, because
        /// <c>ExplosiveAuthoring.explosionID</c> is an <c>ObjectID</c> and a mod's ids do not exist
        /// while the prefab is being written.
        /// </para>
        /// <para>
        /// The trigger is what makes the difference between a bomb and an ornament. Every trigger
        /// works the same way underneath — something zeroes the bomb's health and the game's
        /// explode job runs on things it has just marked destroyed
        /// (<c>ck-db\Pug.Other\ExplosiveSystem.cs:406-408</c>) — so all three are ordinary
        /// components rather than anything this framework has to simulate.
        /// </para>
        /// </remarks>
        public static void ApplyExplosive(
            GameObject root,
            DimensionExplosiveTemplate explosive,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (explosive == null || !explosive.Explodes)
            {
                // The fuse goes too. An item that used to be a bomb and is not one any more would
                // otherwise keep its countdown and quietly delete itself a few seconds after being
                // placed, which reads as the item being broken rather than as a leftover.
                RemoveComponentIfPresent<ExplosiveAuthoring>(root);
                RemoveComponentIfPresent<DestroyTimerAuthoring>(root);
                RemoveComponentIfPresent<ProximityTriggerAuthoring>(root);
                RemoveComponentIfPresent<Pug.Automation.ElectricityAuthoring>(root);
                return;
            }

            if (explosive.ExplodesHarmlessly && report != null)
            {
                report("explodes and does no damage of any kind when it does.");
            }

            if (explosive.HasAFuseThatNeverBurnsDown && report != null)
            {
                report(
                    "is set off by a countdown of zero seconds, which never runs out. Give it a " +
                    "fuse, or set it off by something coming close instead.");
            }

            if (explosive.WaitsForSomethingThatCannotArrive && report != null)
            {
                report(
                    "goes off when something comes within zero tiles, which nothing ever can. " +
                    "Vanilla's proximity bomb uses 1.");
            }

            if (explosive.DigsLessFarThanItReaches && report != null)
            {
                report(
                    "reaches " + explosive.BlastReach + " tiles but only breaks terrain within " +
                    DimensionExplosiveTemplate.TerrainReachCap + ". The game digs in a fixed block " +
                    "around the blast, so the extra reach catches creatures and nothing else.");
            }

            ObjectID explosion = resolveObject == null
                ? ObjectID.None
                : resolveObject(explosive.ExplosionObjectId);

            // NARROWER THAN "ONE OF OURS", ON PURPOSE. The predicate this asks is
            // "does it name one of the mod's own BLASTS" — the exact set the bootstrap registers
            // (DimensionRuntimeConsumerBootstrapUtility.Explosives.cs). A bomb pointed at one of
            // the mod's own swords is not a working setup waiting for the runtime; it is a mistake,
            // and asking the general ownership question here left it silent. When the field is
            // empty the framework makes the blast itself, so that case says nothing either.
            if (explosion == ObjectID.None &&
                !string.IsNullOrEmpty(explosive.ExplosionObjectId) &&
                !(isDeferred != null && isDeferred(explosive.ExplosionObjectId)) &&
                report != null)
            {
                report(
                    "explodes into '" + explosive.ExplosionObjectId + "', which is neither one of " +
                    "this mod's blasts nor one of the game's own objects. Name a blast you made, " +
                    "or leave the field empty and the framework will make and link one.");
            }

            ExplosiveAuthoring authored = EnsureComponent<ExplosiveAuthoring>(root);
            authored.explosionID = explosion;
            authored.explosionVariation = explosive.ExplosionVariation;

            // The flat numbers and the multipliers are BOTH real and neither is redundant: the
            // converter recomputes damage and terrain damage from the level curve when the object
            // has a tier and uses these two straight otherwise
            // (ck-db\Pug.ECS.Conversion\ExplosiveConverter.cs:11-17).
            authored.damage = explosive.HurtsCreaturesBy;
            authored.miningDamage = explosive.BreaksTerrainBy;
            authored.damageMultiplier = explosive.DamageMultiplier;
            authored.miningDamageMultiplier = explosive.TerrainDamageMultiplier;

            // Only ever consulted when the PLAYER's gear rolled the napalm chance, which is the one
            // path where the game picks the fire itself. Kept in step with the author's choice so
            // the two can never disagree about which patch this bomb leaves.
            authored.useSmallNapalmVariant =
                explosive.LeavesBehind == DimensionBlastLeavesBehind.AShortPatchOfFire;

            authored.explosionPushback =
                (ExplosionPushbackLevel)(int)explosive.Pushback;
            authored.explosionInheritsFaction = explosive.SparesWhoeverSetItOff;
            authored.bombInheritsFaction = explosive.TheBombTakesTheirSideToo;
            authored.ignoreExploding = explosive.OtherBlastsDoNotSetItOff;

            // Both are on every one of vanilla's placed bombs and neither is decoration.
            // DontDropSelf is what stops a bomb dropping itself back on the floor when it goes off
            // — the game turns it OFF again in the two cases where the bomb was broken rather than
            // detonated (ck-db\Pug.Other\ExplosiveSystem.cs:584-611), so leaving it off entirely
            // gives infinite bombs. Mineable is what lets a pickaxe hit the placed bomb at all;
            // without it only weapons can set one off by hand.
            EnsureComponent<DontDropSelfAuthoring>(root);
            EnsureComponent<MineableAuthoring>(root);

            ApplyExplosiveTrigger(root, explosive, report);
        }

        /// <summary>
        /// Gives a bomb the one component that sets it off, and takes away the other two.
        /// </summary>
        /// <remarks>
        /// Removing the unused triggers matters as much as adding the chosen one: a bomb switched
        /// from a fuse to a proximity trigger that kept its <c>DestroyTimerAuthoring</c> would go
        /// off on the countdown regardless of whether anything came near, and the author would have
        /// no way to see why.
        /// </remarks>
        private static void ApplyExplosiveTrigger(
            GameObject root,
            DimensionExplosiveTemplate explosive,
            System.Action<string> report)
        {
            bool countdown = explosive.SetOffBy == DimensionExplosiveTrigger.ACountdown;
            bool proximity = explosive.SetOffBy == DimensionExplosiveTrigger.SomethingComingClose;
            bool power = explosive.SetOffBy == DimensionExplosiveTrigger.GettingPower;

            if (countdown)
            {
                DestroyTimerAuthoring fuse = EnsureComponent<DestroyTimerAuthoring>(root);
                fuse.lifetime = new Pug.UnityExtensions.PlatformDependentValue<float>(
                    explosive.SecondsBeforeItGoesOff);
            }
            else
            {
                RemoveComponentIfPresent<DestroyTimerAuthoring>(root);
            }

            if (proximity)
            {
                ProximityTriggerAuthoring trigger = EnsureComponent<ProximityTriggerAuthoring>(root);
                trigger.radius = explosive.HowCloseSomethingHasToCome;
                trigger.delayTime = explosive.SecondsAfterItIsTriggered;

                if (report != null)
                {
                    // Not a mistake, but it is a rule nobody would guess, and it changes how the
                    // item feels to use: PlaceObjectSlot skips the "chance to not consume" roll
                    // entirely for anything carrying a proximity trigger
                    // (ck-db\Pug.Other\PlaceObjectSlot.cs:196-203).
                    report(
                        "goes off when something comes close, so it is always used up when it is " +
                        "placed. The explosives perk that sometimes gives a bomb back does not " +
                        "apply to this kind.");
                }
            }
            else
            {
                RemoveComponentIfPresent<ProximityTriggerAuthoring>(root);
            }

            if (power)
            {
                // Every field left at its default, exactly as vanilla's dynamite pack and remote
                // explosive author it: the trigger job only asks whether the thing has power
                // (ck-db\Pug.Other\ExplosiveSystem.cs:1183-1190).
                EnsureComponent<Pug.Automation.ElectricityAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<Pug.Automation.ElectricityAuthoring>(root);
            }
        }

        public static void ApplyBeamAndAmbience(
            GameObject root,
            DimensionBeamAndAmbienceTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || world == null)
            {
                return;
            }

            if (world.FiresABeamAttack)
            {
                // THE ONE LINE CORE KEEPER'S OWN CONVERTER IS MISSING. BeamAttackStateSystem's
                // query names BeamBuffer and AttackCooldownTimerCD beside the beam state, and
                // BeamAttackStateConverter produces neither — it ensures StateInfoCD, adds
                // BeamAttackStateCD and stops. No vanilla prefab carries a beam, so the gap has
                // never shown up in the shipped game. Our own marker's converter ensures both, and
                // the system then fills the beam list itself the moment the wind-up ends
                // (`ck-db\Pug.Other\BeamAttackStateSystem.cs:133` and `:141`).
                EnsureComponent<ExpandNullforge.Creatures.DimensionBeamBufferAuthoring>(root);

                BeamAttackStateAuthoring beam = EnsureComponent<BeamAttackStateAuthoring>(root);
                beam.anticipationDuration = world.BeamWindUp;
                beam.attackDuration = world.BeamSeconds;
                beam.endDuration = world.BeamRecovery;
                beam.spawnAtDistanceInfront = world.BeamStartsAt;
                beam.beamReachDistance = world.BeamReach;
                beam.beamWidth = world.BeamWidth;
                beam.timeBetweenDamageTicks = world.BeamDamageEvery;
                beam.amountOfBeams = world.BeamCount;
                beam.angleBetweenBeams = world.BeamFanDegrees;
                beam.minCooldown = world.BeamMinCooldown;
                beam.maxCooldown = world.BeamMaxCooldown;
                beam.damage = world.BeamDamage;
                beam.damageMultiplier = world.BeamMultiplier;
            }
            else
            {
                RemoveComponentIfPresent<BeamAttackStateAuthoring>(root);
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionBeamBufferAuthoring>(
                    root);
            }

            if (world.PlaysAnAlert)
            {
                AlertEmoteStateAuthoring alert = EnsureComponent<AlertEmoteStateAuthoring>(root);
                alert.animations = world.AlertAnimations;
                alert.preAlertMinDuration = world.AlertMinPause;
                alert.preAlertMaxDuration = world.AlertMaxPause;
                alert.duration = world.AlertSeconds;
                alert.minCooldown = world.AlertMinCooldown;
                alert.maxCooldown = world.AlertMaxCooldown;
            }
            else
            {
                RemoveComponentIfPresent<AlertEmoteStateAuthoring>(root);
            }

            if (world.DripsItems && !world.DripsNothing)
            {
                ObjectID dripped = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.DripsObjectId);
                if (dripped == ObjectID.None)
                {
                    RemoveComponentIfPresent<SpawnDroppedItemAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "drips '" + world.DripsObjectId + "', which the game does not have, so " +
                            "nothing comes out of it.");
                    }
                }
                else
                {
                    SpawnDroppedItemAuthoring drip =
                        EnsureComponent<SpawnDroppedItemAuthoring>(root);
                    drip.objectID = dripped;
                    drip.amount = world.DripAmount;
                    drip.repeats = world.DripRepeats;
                    drip.timeBetweenSpawns = world.DripInterval;
                }
            }
            else
            {
                RemoveComponentIfPresent<SpawnDroppedItemAuthoring>(root);
            }

            if (world.CanBeIgnited)
            {
                FireSpreading.Authoring.IgnitableAuthoring ignitable =
                    EnsureComponent<FireSpreading.Authoring.IgnitableAuthoring>(root);
                ignitable.spawnOnIgnitedVariation = world.IgnitesIntoVariation;
                ignitable.spawnOnIgnitedObjectID =
                    string.IsNullOrEmpty(world.IgnitesIntoId) || resolveObject == null
                        ? ObjectID.None
                        : resolveObject(world.IgnitesIntoId);
            }
            else
            {
                RemoveComponentIfPresent<FireSpreading.Authoring.IgnitableAuthoring>(root);
            }

            // Written, because it costs nothing and a future game update may read it — but said,
            // because today it is read off exactly one thing and that thing is the game's own oil
            // slime, never off the object carrying the answer.
            SayWhenTicked(
                world.CanBeIgnited,
                report,
                "is set to catch fire and turn into something else. Core Keeper only ever asks that " +
                "question of its own oil slime, never of the thing that is burning, so what you " +
                "chose here will not appear. Fire in the game spreads across oil and nothing else.");

            if (world.SwimsLikeAnAquariumFish)
            {
                ContainedMiniSim.Authoring.AquariumFishMovementAuthoring swim =
                    EnsureComponent<ContainedMiniSim.Authoring.AquariumFishMovementAuthoring>(root);
                swim.swimSpeedMinMax = world.SwimSpeedRange;
                swim.idleTimeMinMax = world.SwimIdleRange;
                swim.smoothingFactor = world.SwimSmoothing;
            }
            else
            {
                RemoveComponentIfPresent<ContainedMiniSim.Authoring.AquariumFishMovementAuthoring>(root);
            }

            if (world.ScuttlesLikeATerrariumCritter)
            {
                ContainedMiniSim.Authoring.TerrariumCritterMovementAuthoring scuttle =
                    EnsureComponent<ContainedMiniSim.Authoring.TerrariumCritterMovementAuthoring>(root);
                scuttle.speed = world.ScuttleSpeed;
                scuttle.minMaxIdleTime = world.ScuttleIdleRange;
            }
            else
            {
                RemoveComponentIfPresent<ContainedMiniSim.Authoring.TerrariumCritterMovementAuthoring>(root);
            }

            if (world.MimicsNotes)
            {
                MimicPlayerInstrumentNotesAuthoring mimic =
                    EnsureComponent<MimicPlayerInstrumentNotesAuthoring>(root);
                mimic.hearRange = world.MimicHearingRange;
                mimic.sfx = new SFXTableIDField { value = world.MimicSound };
                mimic.keyOffset = world.MimicKeyOffset;
            }
            else
            {
                RemoveComponentIfPresent<MimicPlayerInstrumentNotesAuthoring>(root);
            }

            // REFUSED, because writing it stops the generate dead. Core Keeper's step for corner
            // smoothing reaches into the object for the PLAYER's own component and reads its
            // collision layers without checking that it found one, so on anything that is not the
            // player it throws during conversion and the mod does not build at all. The player is
            // the one thing in the whole game that carries it.
            if (world.UsesItsOwnCornerSmoothing && HasNamed(root, "PlayerAuthoring"))
            {
                CornerSmoothingAuthoring corners =
                    EnsureComponent<CornerSmoothingAuthoring>(root);
                corners.forwardSensorDistVertical = world.FeelsAheadVertically;
                corners.forwardSensorDistHorizontal = world.FeelsAheadHorizontally;
                corners.forwardSensorSize = new Vector3(
                    world.FeelerSize,
                    world.FeelerSize,
                    world.FeelerSize);
                corners.escapeSensorSpreadVertical = world.EscapeSpreadVertically;
                corners.escapeSensorSpreadHorizontal = world.EscapeSpreadHorizontally;
                corners.escapeSensorSizeVertical = world.EscapeSizeVertically;
                corners.escapeSensorSizeHorizontal = world.EscapeSizeHorizontally;
                corners.cornerMovementBlend = world.CornerSlide;
                corners.experimentalWallSmoothingEnabled = world.SmoothsAlongWalls;
                corners.wallMovementBlend = world.WallSlide;
            }
            else
            {
                RemoveComponentIfPresent<CornerSmoothingAuthoring>(root);
            }

            SayWhenTicked(
                world.UsesItsOwnCornerSmoothing && !HasNamed(root, "PlayerAuthoring"),
                report,
                "is set to use its own corner smoothing, and that was not written. Corner " +
                "smoothing is how the player slides around a corner instead of catching on it, " +
                "and Core Keeper only ever does it for the player — writing it onto anything else " +
                "stops the mod building at all. The feeler and slide numbers you set here have " +
                "nowhere to go.");

            if (report == null)
            {
                return;
            }

            if (world.BeamsWouldOverlap)
            {
                report(
                    "fires several beams with no angle between them, so they all leave along the " +
                    "same line and only one is visible.");
            }

            if (world.BeamNeverTicks)
            {
                report(
                    "fires a beam that never ticks damage, so it passes over things harmlessly.");
            }

            if (world.DripsNothing)
            {
                report("drips items on a timer without naming what to drip.");
            }
        }

        /// <summary>
        /// The smallest number that makes a gravity well pull at all.
        /// </summary>
        /// <remarks>
        /// <c>RandomWalkGravitySystem</c> reads <c>attractMask</c> only as
        /// <c>(attractMask &amp; attractMask) != 0</c>; the overlap it runs uses a filter the job
        /// hardcodes. So the field is a yes-or-no and 1 is what <c>UpdateFactionSystem</c> writes
        /// into it. Used when the author leaves it at 0, because 0 pulls nothing forever.
        /// </remarks>
        private const uint AGravityWellThatActuallyPulls = 1u;
    }
}
