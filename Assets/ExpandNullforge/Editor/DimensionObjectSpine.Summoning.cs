using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Summoning circles, the item they want, and what a summon sets off.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        public static void ApplyChainReaction(
            GameObject root,
            DimensionChainReactionTemplate chain,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, int> resolveTileset,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || chain == null)
            {
                return;
            }

            if (chain.ExplodesInAChain && !chain.ChainHasNoCharges)
            {
                SequenceExplosiveAuthoring sequence =
                    EnsureComponent<SequenceExplosiveAuthoring>(root);
                sequence.initialDelay = chain.DelayBeforeFirst;
                sequence.animationInitialDelay = chain.AnimationDelay;
                sequence.triggerOnDeath = chain.ChainsOnDeath;
                sequence.useDirection = chain.ChainFollowsItsFacing;
                sequence.useFirstItemSettingForAllCharges = chain.EveryChargeCopiesTheFirst;
                sequence.chargeSettings =
                    new System.Collections.Generic.List<SequenceExplosiveAuthoring.SequenceCharge>();

                ConditionID limit;
                if (!string.IsNullOrEmpty(chain.ChargesLimitedByCondition) &&
                    System.Enum.TryParse(chain.ChargesLimitedByCondition, false, out limit))
                {
                    sequence.consumesConditionForMaxExplosions = limit;
                }

                DimensionChainCharge[] charges = chain.Charges;
                for (int i = 0; i < charges.Length; i++)
                {
                    ObjectID blast = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(charges[i].ExplosionId);
                    if (blast == ObjectID.None)
                    {
                        if (report != null)
                        {
                            report(
                                "chains explosion '" + charges[i].ExplosionId + "', which the game " +
                                "does not have, so that link is left out of the chain.");
                        }

                        continue;
                    }

                    sequence.chargeSettings.Add(new SequenceExplosiveAuthoring.SequenceCharge
                    {
                        explosionID = blast,
                        variation = charges[i].Variation,
                        delayFromPrevious = charges[i].DelayFromPrevious,
                        spreadFromPreviousDistance = charges[i].FurtherOutBy,
                        offsetDegrees = charges[i].TurnedByDegrees,
                        amountToSpawn = charges[i].HowMany,
                        directionType =
                            (SequenceExplosionChargeDirectionType)(int)charges[i].Direction
                    });
                }

                if (sequence.chargeSettings.Count == 0)
                {
                    RemoveComponentIfPresent<SequenceExplosiveAuthoring>(root);
                }
            }
            else
            {
                RemoveComponentIfPresent<SequenceExplosiveAuthoring>(root);
            }

            SayWhenTicked(
                chain.LeavesATrail && !chain.TrailOfNothing,
                report,
                "leaves a trail behind it. Core Keeper lays a trail down from the weapon a player " +
                "is SWINGING — the legendary sword, the shard club and the void club are all of " +
                "it — so a placed object leaves nothing. Put the answer on a weapon.");

            if (chain.LeavesATrail && !chain.TrailOfNothing)
            {
                LeaveTrailAuthoring trail = EnsureComponent<LeaveTrailAuthoring>(root);
                trail.leaveTrail = true;
                trail.trails = chain.TrailPieces;
                trail.trailObjectID = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(chain.TrailObjectId);
                if (trail.trailObjectID == ObjectID.None &&
                    !(isDeferred != null && isDeferred(chain.TrailObjectId)))
                {
                    RemoveComponentIfPresent<LeaveTrailAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "leaves '" + chain.TrailObjectId + "' behind it, which is neither one " +
                            "of this mod's objects nor one the game has, so it leaves nothing.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<LeaveTrailAuthoring>(root);
            }

            if (chain.ChangesLookOverTime)
            {
                ChangeVariationAfterTimeAuthoring timed =
                    EnsureComponent<ChangeVariationAfterTimeAuthoring>(root);
                timed.requiredVariation = chain.FromLook;
                timed.targetVariation = chain.ToLook;
                timed.timeSeconds = chain.AfterSeconds;
            }
            else
            {
                RemoveComponentIfPresent<ChangeVariationAfterTimeAuthoring>(root);
            }

            if (chain.ChangesLookWhenHurt)
            {
                EnsureComponent<ChangeVariationWhenTookDamageAuthoring>(root)
                    .variationToChangeTo = chain.HurtLook;
            }
            else
            {
                RemoveComponentIfPresent<ChangeVariationWhenTookDamageAuthoring>(root);
            }

            if (chain.IsAWaterSource)
            {
                WaterSourceAuthoring water = EnsureComponent<WaterSourceAuthoring>(root);
                water.splashPosition = new Unity.Mathematics.float3(
                    chain.SplashOffset.x,
                    chain.SplashOffset.y,
                    chain.SplashOffset.z);

                int pool = ResolveTilesetName(chain.WaterTilesetId, resolveTileset);
                if (pool >= 0)
                {
                    water.watertileset = (PugTilemap.Tileset)pool;
                }
                else if (!string.IsNullOrEmpty(chain.WaterTilesetId) && report != null)
                {
                    report(
                        "fills the ground with '" + chain.WaterTilesetId + "', which is not a " +
                        "tileset, so it makes the game's own water.");
                }
            }
            else
            {
                RemoveComponentIfPresent<WaterSourceAuthoring>(root);
            }

            if (chain.PetExperience > 0)
            {
                EnsureComponent<PetCandyAuthoring>(root).xp = chain.PetExperience;

                SayWhenTicked(
                    true,
                    report,
                    "gives a pet experience. Core Keeper only hands that over when a pet EATS the " +
                    "thing, so it has to be food a player can feed them. On a placed object " +
                    "nothing will ever be eaten.");
            }
            else
            {
                RemoveComponentIfPresent<PetCandyAuthoring>(root);
            }

            if (chain.SpillsEverythingWhenHit)
            {
                EnsureComponent<DropAllItemsOnHitAuthoring>(root).dropOffset = chain.SpillOffset;

                SayWhenTicked(
                    !HasNamed(root, "InventoryAuthoring"),
                    report,
                    "spills everything when it is hit, and it holds nothing to spill. The game " +
                    "empties the object's own slots, so give it slots — build it as a Container — " +
                    "or nothing will come out.");
            }
            else
            {
                RemoveComponentIfPresent<DropAllItemsOnHitAuthoring>(root);
            }

            if (chain.IsAVendingMachine)
            {
                VendingMachineAuthoring machine = EnsureComponent<VendingMachineAuthoring>(root);
                machine.sizeX = chain.MachineColumns;
                machine.sizeY = chain.MachineRows;
                machine.items = new System.Collections.Generic.List<ObjectData>();

                DimensionMelodyReward[] stock = chain.MachineStock;
                for (int i = 0; i < stock.Length; i++)
                {
                    ObjectID offered = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(stock[i].ObjectId);
                    if (offered == ObjectID.None)
                    {
                        if (report != null)
                        {
                            report(
                                "offers '" + stock[i].ObjectId + "', which the game does not have, " +
                                "so that slot is left empty.");
                        }

                        continue;
                    }

                    machine.items.Add(new ObjectData
                    {
                        objectID = offered,
                        amount = stock[i].Amount,
                        variation = stock[i].Variation
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<VendingMachineAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (chain.ChainHasNoCharges)
            {
                report("explodes in a chain with no charges in it, so nothing goes off.");
            }

            if (chain.TrailOfNothing)
            {
                report("leaves a trail without saying what to leave.");
            }

            if (chain.ChangesLookToTheSameLook)
            {
                report(
                    "changes its look after a while to the look it already wears, so nothing " +
                    "visibly happens when the timer runs out.");
            }

            if (chain.MachineStockDoesNotFit)
            {
                report(
                    "offers more things than its grid has room for, so the last ones are never " +
                    "shown. Make the grid bigger or the list shorter.");
            }
        }

        public static void ApplyMoreCombat(
            GameObject root,
            DimensionMoreCombatTemplate combat,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || combat == null)
            {
                return;
            }

            if (combat.SweepsARay)
            {
                RayAttackState.RayAttackStateAuthoring ray =
                    EnsureComponent<RayAttackState.RayAttackStateAuthoring>(root);
                ray.rayLength = combat.RayLength;
                ray.rayRadius = combat.RayThickness;
                ray.rotateDegreesPerSecond = combat.RaySpinSpeed;
                ray.randomInitialAngle = combat.RayStartsAtARandomAngle;
                ray.isStatic = combat.RayDoesNotTurn;
                ray.offsetFromCenter = combat.RayStartsOutAt;
                ray.expandTime = combat.RayGrowSeconds;
                ray.shrinkTime = combat.RayShrinkSeconds;
                ray.introTimeSeconds = combat.RayWindUpSeconds;
                ray.activeTimeSeconds = combat.RaySweepSeconds;
                ray.endingTimeSeconds = combat.RayRecoverySeconds;
                ray.attackTimeSeconds = combat.RayTotalSeconds;
                ray.damage = combat.RayDamage;
                ray.damageMultiplier = combat.RayMultiplier;
                ray.isRanged = combat.RayIsRanged;
                ray.isMagic = combat.RayIsMagic;
            }
            else
            {
                RemoveComponentIfPresent<RayAttackState.RayAttackStateAuthoring>(root);
            }

            if (combat.HurtsOnTouch)
            {
                TouchAttackAuthoring touch = EnsureComponent<TouchAttackAuthoring>(root);
                touch.hitRadius = combat.TouchRadius;
                touch.pushback = combat.TouchShove;
                touch.cooldownAfterHit = combat.TouchCooldown;
                touch.ignoreDamageReduction = combat.TouchIgnoresArmour;
                touch.triggerAnimationOnHit = combat.TouchAnimation;
            }
            else
            {
                RemoveComponentIfPresent<TouchAttackAuthoring>(root);
            }

            if (combat.HoldsAShield)
            {
                ShieldAuthoring shield = EnsureComponent<ShieldAuthoring>(root);
                shield.shieldWidthDegrees = combat.ShieldWidthDegrees;
                shield.defaultShieldActive = combat.ShieldStartsUp;
            }
            else
            {
                RemoveComponentIfPresent<ShieldAuthoring>(root);
            }

            if (combat.OrbitsItsOwner)
            {
                MinionOrbitAuthoring orbit = EnsureComponent<MinionOrbitAuthoring>(root);
                orbit.radius = combat.OrbitRadius;
                orbit.orbitSpeed = combat.OrbitSpeed;
            }
            else
            {
                RemoveComponentIfPresent<MinionOrbitAuthoring>(root);
            }

            if (combat.PlacesObjects && !combat.PlacesNothing)
            {
                ObjectID placed = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(combat.PlacesObjectId);
                if (placed == ObjectID.None)
                {
                    RemoveComponentIfPresent<PlaceObjectStateAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "places '" + combat.PlacesObjectId + "' as it fights, which the game " +
                            "does not have, so it places nothing.");
                    }
                }
                else
                {
                    PlaceObjectStateAuthoring places =
                        EnsureComponent<PlaceObjectStateAuthoring>(root);
                    places.objectToPlace = placed;
                    places.placeDuration = combat.PlaceSeconds;
                    places.minCooldown = combat.MinPlaceCooldown;
                    places.maxCooldown = combat.MaxPlaceCooldown;
                    places.maxObjectsToPlace = combat.AtMostPlaced;
                    places.onlyPlaceWhenInCombatWithPlayer = combat.PlacesOnlyInCombat;
                    places.placeOnAnyTileset = combat.PlacesOnAnyGround;
                    places.placeOnTileType = combat.PlacesOnTileKind;

                    if (!combat.PlacesOnAnyGround)
                    {
                        int ground = ResolveTilesetName(combat.PlacesOnTilesetId, resolveTileset);
                        if (ground >= 0)
                        {
                            places.placeOnTileset = (PugTilemap.Tileset)ground;
                        }
                        else
                        {
                            places.placeOnAnyTileset = true;
                            if (report != null)
                            {
                                report(
                                    "only places on '" + combat.PlacesOnTilesetId + "', which is " +
                                    "not a tileset, so it will place on any ground instead.");
                            }
                        }
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<PlaceObjectStateAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (combat.RayNeverSweeps)
            {
                report(
                    "fires a ray that never turns, so it is a fixed beam rather than a sweep. Give " +
                    "it a spin speed if it was meant to sweep.");
            }

            if (combat.PlacesNothing)
            {
                report("places objects as it fights without naming what to place.");
            }

            if (combat.FussyAboutGroundWithoutNamingIt)
            {
                report(
                    "will only place on one kind of ground without saying which, so it will never " +
                    "find anywhere to place.");
            }
        }

        /// <summary>
        /// Makes an object a summoning circle: put the right item on it and a boss arrives.
        /// </summary>
        /// <remarks>
        /// A name the resolver cannot turn into an id is NOT necessarily a mistake: the mod's
        /// own creatures have no ids at generation time at all. When <paramref name="resolveOwnName"/>
        /// is supplied, an unresolvable name keeps the circle and bakes the qualified name
        /// beside it for runtime hydration — the fix for the bug where a circle summoning the
        /// mod's own boss was silently stripped with a warning blaming the game.
        /// </remarks>
        public static void ApplySummoningCircle(
            GameObject root,
            DimensionSummoningCircleTemplate circle,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, string> resolveOwnName = null)
        {
            if (root == null || circle == null || !circle.SummonsSomething)
            {
                RemoveComponentIfPresent<SummonAreaAuthoring>(root);
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionSummonAreaByNameAuthoring>(root);
                return;
            }

            ObjectID summoned = resolveObject == null
                ? ObjectID.None
                : resolveObject(circle.SummonsObjectId);
            bool byName = false;
            if (summoned == ObjectID.None)
            {
                if (resolveOwnName == null)
                {
                    RemoveComponentIfPresent<SummonAreaAuthoring>(root);
                    RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionSummonAreaByNameAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "summons '" + circle.SummonsObjectId + "', which the game does not have, so " +
                            "it summons nothing.");
                    }

                    return;
                }

                byName = true;
            }

            if (byName)
            {
                ExpandNullforge.Creatures.DimensionSummonAreaByNameAuthoring link =
                    EnsureComponent<ExpandNullforge.Creatures.DimensionSummonAreaByNameAuthoring>(root);
                link.bossObjectName = resolveOwnName(circle.SummonsObjectId);
                link.optionalBossObjectName = string.IsNullOrEmpty(circle.AlternativeObjectId)
                    ? string.Empty
                    : resolveOwnName(circle.AlternativeObjectId);
            }
            else
            {
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionSummonAreaByNameAuthoring>(root);
            }

            SummonAreaAuthoring area = EnsureComponent<SummonAreaAuthoring>(root);
            area.bossToSummon = summoned;
            area.anticipationTime = circle.WindUpSeconds;
            area.spawnTime = circle.ArrivalSeconds;
            area.distanceToDestroyTilesOnSpawn = circle.ClearsTilesWithin;
            area.dontOffsetSpawnItemLocation = circle.SummoningItemStaysWhereItWasPut;
            area.overrideDistanceToCheckSummoningItem = circle.LooksForItsItemWithin;
            area.overrideDistanceToCheckForExistingBoss = circle.LooksForAnExistingOneWithin;
            area.spawnOffset = new Unity.Mathematics.float3(
                circle.ArrivesAt.x,
                circle.ArrivesAt.y,
                circle.ArrivesAt.z);

            area.optionalBossToSummon = ObjectID.None;
            if (!string.IsNullOrEmpty(circle.AlternativeObjectId))
            {
                area.optionalBossToSummon = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(circle.AlternativeObjectId);
                if (area.optionalBossToSummon == ObjectID.None && report != null)
                {
                    report(
                        "can alternatively summon '" + circle.AlternativeObjectId + "', which the " +
                        "game does not have, so it only ever summons the first.");
                }
            }

            MakeTheCircleNoticeTheItem(root, circle.LooksForItsItemWithin);
        }

        /// <summary>
        /// Gives a summoning circle the two things the game's summoning system will not run without.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS WHY A GENERATED CIRCLE USED TO DO NOTHING AT ALL. Core Keeper's
        /// <c>BossSummoningSystem</c> only looks at objects that have all four of
        /// <c>NearbyEntitiesBufferCD</c>, <c>AnimationBuffer</c>, <c>AnimationBufferPointer</c> and
        /// <c>SummonAreaCD</c>. <c>SummonAreaAuthoring</c> supplies the last one and nothing else,
        /// and it carries no <c>[RequireComponent]</c>, so a circle built from it alone was never in
        /// the query. Everything upstream still succeeded — the boss id was resolved, the item was
        /// wired, the arena check passed — and the idol placed on the circle did nothing, forever,
        /// with nothing said anywhere.
        /// </para>
        /// <para>
        /// The buffer comes from <c>NearbyEntitiesTrackerAuthoring</c> and the two animation pieces
        /// from <c>AnimationAuthoring</c>. The tracker is what puts the dropped offering in reach:
        /// the system walks the nearby list looking for something carrying the boss, so a circle
        /// that notices nothing finds nothing.
        /// </para>
        /// <para>
        /// The numbers are the game's own summoning circles': it detects on physics layer 0 within
        /// 1.7, which is a little over the 2-tile distance the system checks by default. A circle
        /// told to look for its item further away has to be able to see that far, so the radius
        /// grows with it.
        /// </para>
        /// <para>
        /// Switching a circle back off leaves these two behind on purpose. Neither does anything on
        /// its own — the tracker fills a list nothing reads and the animation pieces play nothing —
        /// and <c>NearbyEntitiesTrackerAuthoring</c> is the component a creature's whole perception
        /// hangs off (<c>DimensionCreatureGenerator.ApplyPerception</c>). Reaching across to remove
        /// it here is the shape of bug this framework keeps finding, for no gain a player can see.
        /// </para>
        /// </remarks>
        public static void MakeTheCircleNoticeTheItem(GameObject root, float looksForItsItemWithin)
        {
            if (root == null)
            {
                return;
            }

            EnsureComponent<AnimationAuthoring>(root);

            // IT USED TO SET THE THREE FIELDS OUTRIGHT, and on a world object that was a clobber.
            // The tracker is [DisallowMultipleComponent] — one reach and one layer mask shared by
            // every answer on the object that watches its surroundings — and a circle is usually
            // not the only such answer. When this ran last it silently replaced a shove authored at
            // six tiles with 1.7, and narrowed the mask to one layer. Merging keeps both features:
            // seeing further than one of them needs costs it nothing, because each consumer filters
            // the list again by its own rule.
            DimensionQueryCompanions.SeesNearbyThings(
                root,
                looksForItsItemWithin > VanillaSummonCircleNoticeRadius
                    ? looksForItsItemWithin
                    : VanillaSummonCircleNoticeRadius,
                1u,
                false);
        }

        /// <summary>How far the game's own summoning circles notice things, measured off theirs.</summary>
        private const float VanillaSummonCircleNoticeRadius = 1.7f;
    }
}
