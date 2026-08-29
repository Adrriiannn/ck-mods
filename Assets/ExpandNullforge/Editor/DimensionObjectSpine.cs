using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The components Core Keeper puts on essentially every object of a given kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Measured across the game's prefabs, a handful of components sit on almost
    /// everything — <c>AnimationAuthoring</c> on 1,446, <c>AreaLevelAuthoring</c> on 1,184,
    /// <c>IgnoreVertexOffsetsAuthoring</c> on 837 — and the framework was writing none of them. The
    /// result is objects that generate cleanly and then behave subtly unlike their vanilla
    /// neighbours: a chest that cannot be painted or rotated and that conveyors cannot feed, a plant
    /// a shovel will not lift, a creature whose damage cannot scale with where it spawned.
    /// </para>
    /// <para>
    /// It lives in one place rather than being repeated in each generator so the answer to "what does
    /// every object need" has exactly one definition. Each generator still decides which spine
    /// applies to the kind of thing it makes.
    /// </para>
    /// <para>
    /// ORDERING MATTERS for <see cref="ApplyAreaLevel"/>: <c>AreaLevelAuthoring.CalculateLevel</c>
    /// looks for an <c>EntityMonoBehaviourData</c> and falls back to <c>ObjectAuthoring</c>, warning
    /// to the console when it finds neither. Call it after the object's identity is written.
    /// </para>
    /// </remarks>
    internal static class DimensionObjectSpine
    {
        /// <summary>
        /// What every generated object gets, whatever it is.
        /// </summary>
        /// <remarks>
        /// <c>AnimationAuthoring</c> is on more vanilla prefabs than any other component. Without it
        /// nothing we generate can play an animation at all — not a chest lid, not a creature's walk,
        /// not a plant swaying. <c>IgnoreVertexOffsetsAuthoring</c> opts out of Core Keeper's vertex
        /// wobble, which is the same idea the tileset side already exposes as "rigid surface".
        /// </remarks>
        public static void ApplyUniversal(GameObject root, bool rigid)
        {
            EnsureComponent<AnimationAuthoring>(root);

            if (rigid)
            {
                EnsureComponent<IgnoreVertexOffsetsAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<IgnoreVertexOffsetsAuthoring>(root);
            }
        }

        /// <summary>
        /// The state machine anything damageable needs.
        /// </summary>
        /// <remarks>
        /// <c>StateAuthoring</c> is the root the others hang off; without <c>DeathState</c> a thing
        /// with health absorbs hits forever, and without <c>TookDamageState</c> it never visibly
        /// reacts. On 1,267+ vanilla prefabs together, chests and plants included.
        /// </remarks>
        public static void ApplyDamageableStates(GameObject root)
        {
            EnsureComponent<StateAuthoring>(root);
            EnsureComponent<IdleStateAuthoring>(root);
            EnsureComponent<TookDamageStateAuthoring>(root);
            EnsureComponent<DeathStateAuthoring>(root);
        }

        /// <summary>
        /// Ties an object to the difficulty of wherever it ends up.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is how Core Keeper scales health and damage by area — the curves in
        /// <c>HealthAuthoring</c>, <c>MeleeAttackStateAuthoring</c> and friends all read it in
        /// <c>OnValidate</c>.
        /// </para>
        /// <para>
        /// CONDITIONAL, and that is the whole point. <c>HealthAuthoring</c> offers an opt-out
        /// (<c>dontCalculateHealthFromLevel</c>) but the ATTACK components do not: their
        /// <c>OnValidate</c> overwrites damage unconditionally whenever a level is present. So adding
        /// this to a creature whose author asked for exact numbers would make authored attack damage
        /// impossible to keep — the precise failure this framework exists to prevent. It goes on only
        /// when the author chose to scale with the area instead.
        /// </para>
        /// </remarks>
        public static void ApplyAreaLevel(GameObject root, bool scaleWithArea)
        {
            if (scaleWithArea)
            {
                EnsureComponent<AreaLevelAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<AreaLevelAuthoring>(root);
            }
        }

        /// <summary>
        /// What a placed, player-facing object gets: paint, facing, and a description.
        /// </summary>
        /// <remarks>
        /// Vanilla chests carry all three. Paint is the visible one — a custom chest that cannot take
        /// a paint bucket sits oddly beside vanilla ones in the same base.
        /// </remarks>
        public static void ApplyPlacedObject(GameObject root, bool paintable, bool rotatable)
        {
            EnsureComponent<DescriptionAuthoring>(root);

            if (paintable)
            {
                EnsureComponent<PaintableObjectAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<PaintableObjectAuthoring>(root);
            }

            if (rotatable)
            {
                EnsureComponent<RotationAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<RotationAuthoring>(root);
            }
        }

        /// <summary>
        /// What an object does with electricity, if anything.
        /// </summary>
        /// <remarks>
        /// The whole of Core Keeper wiring is one seven-field component and a lot of systems reading
        /// it. Removed rather than left behind when an object stops being wired, or it keeps blocking
        /// current in a circuit nothing in the asset still describes.
        /// </remarks>
        public static void ApplyWiring(GameObject root, DimensionWiringTemplate wiring)
        {
            if (wiring == null || !wiring.IsWired)
            {
                RemoveComponentIfPresent<Pug.Automation.ElectricityAuthoring>(root);
                return;
            }

            Pug.Automation.ElectricityAuthoring electricity =
                EnsureComponent<Pug.Automation.ElectricityAuthoring>(root);
            electricity.sourceEnergy = wiring.PowerProduced;
            electricity.blocksElectricity = wiring.BlocksCurrent;
            electricity.direction = wiring.Direction;
            electricity.isLever = wiring.IsSwitch;
            electricity.isWire = wiring.IsWire;
            electricity.circuitConnectionMode = (CircuitConnectionMode)(int)wiring.Shape;

            if (wiring.Role == DimensionWiringRole.LogicCircuit)
            {
                electricity.circuitType = wiring.DelaysInsteadOfConditions
                    ? CircuitType.Delay
                    : CircuitType.Condition;
            }
            else
            {
                electricity.circuitType = CircuitType.None;
            }
        }

        /// <summary>
        /// What an item grants to whoever has it.
        /// </summary>
        /// <remarks>
        /// Equipped and eaten live on separate Core Keeper components, so they are written separately
        /// and each is removed when its list empties. <c>dontCalculateValuesFromLevel</c> is the same
        /// trap as health and attack damage: left false with an area level present, every authored
        /// number is recomputed from the curve.
        /// <para>
        /// <c>cooldownSeconds</c> is passed in rather than read off the template. The template used
        /// to carry a cooldown of its own, and because this pass runs after everything else it
        /// destroyed the cooldown component whenever that field was blank — throwing away the number
        /// the creator had typed on the item itself. There is one cooldown now; the caller resolves
        /// it and hands it over, and this pass writes it rather than deciding it.
        /// </para>
        /// </remarks>
        /// <param name="report">
        /// Says a whole sentence, unlike <paramref name="reportUnknown"/>, which is handed a bare
        /// condition name for the caller to wrap. The shared-timer check lives here rather than in
        /// the item generator because a world object goes through this same method and never
        /// through that one — so a bad timer on a world object was dropped in silence.
        /// </param>
        public static void ApplyItemEffects(
            GameObject root,
            DimensionItemEffectsTemplate effects,
            float cooldownSeconds,
            System.Action<string> reportUnknown,
            System.Action<string> report = null,
            bool alwaysCarriesEquipmentConditions = false)
        {
            if (effects == null)
            {
                RemoveComponentIfPresent<GivesConditionsWhenEquippedAuthoring>(root);
                RemoveComponentIfPresent<GivesConditionsWhenConsumedAuthoring>(root);
                RemoveComponentIfPresent<CooldownAuthoring>(root);
                return;
            }

            // A PIECE OF ARMOUR CARRIES THIS EVEN WITH NOTHING ON IT. All 100% of the game's own
            // types 100–107 do, and the component is not only about conditions: without it
            // LevelEntitiesBufferConverter builds no LevelEntitiesBuffer, and the first clause of
            // InventoryUtility.CanBeRepaired asks for one — so a helmet with no effects came out
            // impossible to repair no matter how much durability it had. The archetype declares
            // EquipmentConditions for exactly this, and until now nothing read that declaration.
            DimensionItemEffect[] equipped = effects.WhileEquipped;
            if (equipped.Length > 0 || alwaysCarriesEquipmentConditions)
            {
                GivesConditionsWhenEquippedAuthoring gives =
                    EnsureComponent<GivesConditionsWhenEquippedAuthoring>(root);
                gives.isArmor = effects.CountsAsArmor;
                gives.givesConditionsWhenHeldInHand = effects.AlsoWhileJustHeld;
                gives.dontCalculateValuesFromLevel = effects.ValuesAreExactlyAsTyped;
                gives.givesConditionsWhenEquipped = BuildEquipmentConditions(equipped, reportUnknown);
            }
            else
            {
                RemoveComponentIfPresent<GivesConditionsWhenEquippedAuthoring>(root);
            }

            if (effects.WhenEaten.Length > 0)
            {
                // The component was attached and left empty before this, which is the quietest
                // failure there is: the food is edible, the game reads its condition list, and the
                // list has nothing in it.
                GivesConditionsWhenConsumedAuthoring eaten =
                    EnsureComponent<GivesConditionsWhenConsumedAuthoring>(root);
                eaten.Values = BuildConsumedConditions(effects.WhenEaten, reportUnknown);
            }
            else
            {
                RemoveComponentIfPresent<GivesConditionsWhenConsumedAuthoring>(root);
            }

            if (cooldownSeconds > 0f || effects.SharesACooldown)
            {
                CooldownAuthoring cooldown = EnsureComponent<CooldownAuthoring>(root);

                // Only written when there is a number. A shared-cooldown id on its own says which
                // group the item belongs to, not how long it waits, and overwriting the caller's
                // number with a zero is what a slot reads as no delay at all.
                if (cooldownSeconds > 0f)
                {
                    cooldown.cooldown = cooldownSeconds;
                }

                // WRITTEN EVERY TIME, and that is the fix rather than an extra. Generation reloads
                // the existing prefab, so setting this only inside the if meant blanking the field
                // or mistyping it left the previously-baked group in place while the report said
                // the item had been generated without one.
                //
                // The fallback is SlotType because SlotType IS the enum's zero, which is what an
                // untouched CooldownAuthoring already holds — the game has no "its own timer"
                // option at all, and every item ends up sharing with the rest of its slot unless
                // it names one of the potion groups.
                SharedCooldownIdentifier shared = SharedCooldownIdentifier.SlotType;
                string named = effects.SharedCooldownId;
                if (effects.SharesACooldown && !string.IsNullOrEmpty(named))
                {
                    if (string.Equals(named, "ObjectID", System.StringComparison.Ordinal))
                    {
                        // The game keeps this one for the Cupid Bow. CooldownConverter hands
                        // GetCooldownTypeFromObjectID an ObjectID.None for anything built on
                        // ObjectAuthoring, which is every object here, and that method throws on
                        // any id but the bow's — so writing it would take the object down during
                        // conversion in the game rather than saying so here.
                        if (report != null)
                        {
                            report(
                                "shares the ObjectID timer, which only the Cupid Bow can have — " +
                                "the game throws on any other object. It shares with everything " +
                                "in its slot instead. The three a mod can use are HealingPotion, " +
                                "ManaPotion and SlotType.");
                        }
                    }
                    else if (System.Enum.IsDefined(typeof(SharedCooldownIdentifier), named))
                    {
                        // IsDefined, not TryParse. TryParse("7") succeeds and hands back the
                        // number 7, which the game's own switch answers with a thrown exception.
                        shared = (SharedCooldownIdentifier)System.Enum.Parse(
                            typeof(SharedCooldownIdentifier), named, false);
                    }
                    else if (report != null)
                    {
                        report(
                            "asks for shared timer '" + named + "', which the game does not have, " +
                            "so it shares with everything in its slot instead. The three a mod " +
                            "can use are HealingPotion, ManaPotion and SlotType.");
                    }
                }

                cooldown.sharedCooldownIdentifier = shared;
            }
            else
            {
                RemoveComponentIfPresent<CooldownAuthoring>(root);
            }
        }

        /// <summary>
        /// What right-clicking the item does.
        /// </summary>
        /// <remarks>
        /// Removed when it goes back to doing nothing, rather than left behind still charging. The
        /// mechanic is set every time so an item changed from a charged attack into a minion summon
        /// does not keep both behaviours.
        /// </remarks>
        public static void ApplySecondaryUse(
            GameObject root,
            DimensionSecondaryUseTemplate secondary,
            System.Func<string, ObjectID> resolveItem,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (secondary == null || !secondary.HasSecondaryUse)
            {
                RemoveComponentIfPresent<SecondaryUseAuthoring>(root);
                return;
            }

            SecondaryUseAuthoring use = EnsureComponent<SecondaryUseAuthoring>(root);

            switch (secondary.Kind)
            {
                case DimensionSecondaryUseKind.EquipIt:
                    use.mechanic = SecondaryUseMechanic.Equip;
                    break;
                case DimensionSecondaryUseKind.ChargedAttack:
                    use.mechanic = SecondaryUseMechanic.WindUp;
                    break;
                case DimensionSecondaryUseKind.SummonsMinion:
                    use.mechanic = SecondaryUseMechanic.SpawnMinion;
                    break;
                default:
                    use.mechanic = SecondaryUseMechanic.None;
                    break;
            }

            if (secondary.IsChargedAttack)
            {
                use.windupTime = secondary.ChargeTime;
                use.windupTiers = secondary.ChargeSteps;
                use.cancelAttackIfNotAtWindupTier = secondary.MinimumStepToRelease;
                use.windupTimeMultiplier = secondary.WindUpTimeMultiplier;
                use.windupExplosionSequenceStart = secondary.WindUpExplosionStartsAt;
                use.windupExplosionSequenceIncrement = secondary.WindUpExplosionStepsBy;
                use.extraDamageMultiplier = secondary.ExtraDamageMultiplier;
                use.windupAreaSizeMultiplier = secondary.AreaSizeMultiplier;
                use.projectileSpeedMultiplier = secondary.ProjectileSpeedMultiplier;
                use.manaCostMultiplier = secondary.ManaCostMultiplier;
                use.knockback = secondary.Knockback;

                SecondaryUseTerm term;
                if (!string.IsNullOrEmpty(secondary.ChargedAttackName) &&
                    System.Enum.TryParse(secondary.ChargedAttackName, false, out term))
                {
                    use.useTerm = term;
                }
                else if (!string.IsNullOrEmpty(secondary.ChargedAttackName) && report != null)
                {
                    // A WHOLE SENTENCE, like every other line this method reports. It used to hand
                    // back the bare name and leave the caller to build the sentence — so the same
                    // callback carried two different things, and the minion complaint below arrived
                    // wrapped in the caller's charged-attack wording. One contract now: everything
                    // that comes out of here is already a sentence.
                    report(
                        "names charged attack '" + secondary.ChargedAttackName + "', which the " +
                        "game does not have, so it charges with no named effect.");
                }

                WeaponEffectType effect;
                if (!string.IsNullOrEmpty(secondary.WeaponEffect) &&
                    System.Enum.TryParse(secondary.WeaponEffect, false, out effect))
                {
                    use.weaponEffectType = effect;
                }
            }

            if (secondary.SummonsMinion && resolveItem != null)
            {
                ObjectID minion = resolveItem(secondary.MinionObjectId);

                // WRITTEN EVERY TIME, INCLUDING THE None. Generation reloads the existing prefab,
                // so leaving the old value in place when the new one does not resolve means an item
                // whose minion was changed to a typo keeps summoning the previous creature while
                // the report says right-clicking does nothing. For one of the mod's own the None is
                // the placeholder the link hydration overwrites at load.
                use.minionToSpawn = minion;

                if (minion == ObjectID.None &&
                    !(isDeferred != null && isDeferred(secondary.MinionObjectId)) &&
                    !string.IsNullOrEmpty(secondary.MinionObjectId) &&
                    report != null)
                {
                    // This branch said nothing at all before. An item set to summon something on
                    // right-click, naming an object that does not exist, generated clean and then
                    // did nothing when a player right-clicked it.
                    report(
                        "summons '" + secondary.MinionObjectId + "' on right-click, which is " +
                        "neither one of this mod's objects nor one the game has, so right-clicking " +
                        "it does nothing.");
                }
            }
        }

        /// <summary>
        /// The conditions a food gives when it is eaten.
        /// </summary>
        /// <remarks>
        /// Each entry carries TWO condition datas — one for the raw item and one for the cooked
        /// version — because Core Keeper cooks a dish into a better version of the same effect. Both
        /// are written from the one authored effect; the cooked half is what the game reads once the
        /// item has been through a pot, and leaving it blank makes cooking do nothing.
        /// </remarks>
        private static System.Collections.Generic.List<ConditionDataContainer> BuildConsumedConditions(
            DimensionItemEffect[] effects,
            System.Action<string> reportUnknown)
        {
            System.Collections.Generic.List<ConditionDataContainer> built =
                new System.Collections.Generic.List<ConditionDataContainer>();

            for (int i = 0; i < effects.Length; i++)
            {
                ConditionID id;
                if (!TryResolveCondition(effects[i].EffectId, out id))
                {
                    if (reportUnknown != null)
                    {
                        reportUnknown(effects[i].EffectId);
                    }

                    continue;
                }

                ConditionData data = new ConditionData
                {
                    conditionID = id,
                    duration = effects[i].Seconds,
                    value = effects[i].Value,
                    valueMultiplier = effects[i].ValueMultiplier
                };

                built.Add(new ConditionDataContainer
                {
                    conditionData = data,
                    conditionDataWhenCooked = data
                });
            }

            return built;
        }

        private static System.Collections.Generic.List<EquipmentCondition> BuildEquipmentConditions(
            DimensionItemEffect[] effects,
            System.Action<string> reportUnknown)
        {
            System.Collections.Generic.List<EquipmentCondition> built =
                new System.Collections.Generic.List<EquipmentCondition>();

            for (int i = 0; i < effects.Length; i++)
            {
                ConditionID id;
                if (!TryResolveCondition(effects[i].EffectId, out id))
                {
                    if (reportUnknown != null)
                    {
                        reportUnknown(effects[i].EffectId);
                    }

                    continue;
                }

                built.Add(new EquipmentCondition
                {
                    id = id,
                    value = effects[i].Value,
                    valueMultiplier = effects[i].ValueMultiplier
                });
            }

            return built;
        }

        /// <summary>
        /// How a creature arrives, idles, scales with the party, and leaves.
        /// </summary>
        /// <remarks>
        /// Seven components in one call because they are one arc. Every one is added or removed
        /// rather than only added: a boss that stops scaling with the party must actually stop.
        /// </remarks>
        public static void ApplyCreatureLifecycle(
            GameObject root,
            DimensionCreatureLifecycleTemplate lifecycle,
            System.Action<string> report)
        {
            if (lifecycle == null)
            {
                lifecycle = new DimensionCreatureLifecycleTemplate();
            }

            ApplyEntrance(root, lifecycle, report);
            ApplyIdleEmotes(root, lifecycle);

            if (lifecycle.ScalesWithTheParty)
            {
                EnsureComponent<ScaleHealthByPlayerCountAuthoring>(root).scalingFactor =
                    lifecycle.HealthScalesWithPlayers;
            }
            else
            {
                RemoveComponentIfPresent<ScaleHealthByPlayerCountAuthoring>(root);
            }

            if (lifecycle.FollowsALure)
            {
                FollowPheromoneStateAuthoring follow =
                    EnsureComponent<FollowPheromoneStateAuthoring>(root);
                follow.pheromonesToFollow = new System.Collections.Generic.List<PheromoneType>
                {
                    PheromoneType.Player
                };
            }
            else
            {
                RemoveComponentIfPresent<FollowPheromoneStateAuthoring>(root);
            }

            Toggle<CanBeControlledByOtherEntityAuthoring>(
                root,
                lifecycle.SomethingElseCanControlIt);

            if (lifecycle.EverDespawns)
            {
                DestroyWhenNoNearbyPlayerAuthoring despawn =
                    EnsureComponent<DestroyWhenNoNearbyPlayerAuthoring>(root);
                despawn.distance = lifecycle.DespawnsWhenNobodyIsWithin;
                despawn.destroyDelay = lifecycle.DespawnDelaySeconds;
            }
            else
            {
                RemoveComponentIfPresent<DestroyWhenNoNearbyPlayerAuthoring>(root);
            }

            if (lifecycle.DeathClearsThingsNearby)
            {
                if (lifecycle.DeathClearsNothing && report != null)
                {
                    report("clears things when it dies over a radius of nothing.");
                }

                DestroyNearbyOnDeathAuthoring clears =
                    EnsureComponent<DestroyNearbyOnDeathAuthoring>(root);
                clears.radius = lifecycle.DeathClearRadius;
                clears.killAnyTemporaryEnemy = lifecycle.ItsSummonsGoWithIt;
                clears.destroyEntitiesWithDontDestroyOnZeroHealthCD =
                    lifecycle.DeathTakesEvenTheUnkillable;
                if (clears.objectsToDestroy == null)
                {
                    clears.objectsToDestroy = new System.Collections.Generic.List<ObjectID>();
                }
            }
            else
            {
                RemoveComponentIfPresent<DestroyNearbyOnDeathAuthoring>(root);
            }
        }

        /// <summary>
        /// The push a conveyor gives to whatever stands on it.
        /// </summary>
        /// <remarks>
        /// One direction per variation, because a belt with four rotations is one object with four
        /// variations rather than four objects. A belt with no directions runs and moves nothing.
        /// </remarks>
        private static void ApplyConveyorPush(
            GameObject root,
            DimensionAutomationTemplate automation,
            System.Action<string> report)
        {
            if (!automation.PushesWhatStandsOnIt)
            {
                RemoveComponentIfPresent<VelocityAffectorAuthoring>(root);
                return;
            }

            if (automation.PushesNowhere && report != null)
            {
                report(
                    "pushes what stands on it and was never told which way, so it runs and moves " +
                    "nothing.");
            }

            VelocityAffectorAuthoring push = EnsureComponent<VelocityAffectorAuthoring>(root);
            push.priority = automation.PushPriority;
            push.requiresElectricity = automation.PushNeedsPower;
            push.moveForceOptions =
                new System.Collections.Generic.List<VelocityAffectorAuthoring.MoveForceOption>();

            Vector2Int[] directions = automation.PushDirections;
            for (int i = 0; i < directions.Length; i++)
            {
                push.moveForceOptions.Add(new VelocityAffectorAuthoring.MoveForceOption
                {
                    moveForce = new Unity.Mathematics.int2(directions[i].x, directions[i].y)
                });
            }
        }

        private static void ApplyEntrance(
            GameObject root,
            DimensionCreatureLifecycleTemplate lifecycle,
            System.Action<string> report)
        {
            if (!lifecycle.MakesAnEntrance)
            {
                RemoveComponentIfPresent<SpawnStateAuthoring>(root);
                return;
            }

            if (lifecycle.MakesAnEntranceWithNoAnimation && report != null)
            {
                report(
                    "makes an entrance with no animation to make it with, so it stands still for " +
                    lifecycle.EntranceSeconds + " seconds and then acts.");
            }

            SpawnStateAuthoring entrance = EnsureComponent<SpawnStateAuthoring>(root);
            entrance.duration = lifecycle.EntranceSeconds;
            entrance.animId = lifecycle.EntranceAnimation;
            entrance.removeTilesOnSpawn = lifecycle.ClearsTheGroundAsItArrives;
            entrance.radiusToRemoveTilesWithin =
                lifecycle.ClearsTheGroundAsItArrives ? lifecycle.ClearedRadius : 0f;
            entrance.facingDirection = new Unity.Mathematics.float2(
                lifecycle.FacesOnEntrance.x,
                lifecycle.FacesOnEntrance.y);
            entrance.removeTilesOnSpawnOffset = new Unity.Mathematics.float2(
                lifecycle.ClearsTilesOffsetBy.x,
                lifecycle.ClearsTilesOffsetBy.y);
        }

        private static void ApplyIdleEmotes(
            GameObject root,
            DimensionCreatureLifecycleTemplate lifecycle)
        {
            if (!lifecycle.HasIdleEmotes)
            {
                RemoveComponentIfPresent<IdleEmoteStateAuthoring>(root);
                return;
            }

            IdleEmoteStateAuthoring emotes = EnsureComponent<IdleEmoteStateAuthoring>(root);
            emotes.minCooldown = lifecycle.MinimumIdleGap;
            emotes.maxCooldown = lifecycle.MaximumIdleGap;
            emotes.emoteAnimations =
                new System.Collections.Generic.List<IdleEmoteStateAuthoring.EmoteAnimation>();

            DimensionIdleEmote[] authored = lifecycle.IdleEmotes;
            for (int i = 0; i < authored.Length; i++)
            {
                emotes.emoteAnimations.Add(new IdleEmoteStateAuthoring.EmoteAnimation
                {
                    animation = authored[i].Animation,
                    duration = authored[i].Seconds,
                    preIdleMinDuration = authored[i].MinimumWait,
                    preIdleMaxDuration = authored[i].MaximumWait,
                    mustBeOnWalkableGround = authored[i].OnlyOnWalkableGround
                });
            }
        }

        /// <summary>
        /// <summary>
        /// Which way a placed thing faces, what text it comes with, and how long it is untouchable.
        /// </summary>
        /// <remarks>
        /// <c>RotationAuthoring</c>, <c>DescriptionAuthoring</c> and the one-frame form of
        /// <c>CantBeAttackedAuthoring</c> were all attached with nothing set. The description one is
        /// worth naming: <c>initialText</c> is the text a sign or a labelled chest comes with, which
        /// is a different thing from the tooltip the localization path writes.
        /// </remarks>
        public static void ApplyFacingAndText(
            GameObject root,
            Vector3 initialFacing,
            bool colliderTurnsToo,
            int rotationIconOffset,
            string textItComesWith,
            bool untouchableForOneFrameOnly,
            bool alreadyUntouchableForever,
            System.Action<string> report)
        {
            RotationAuthoring rotation = root.GetComponent<RotationAuthoring>();
            if (rotation != null)
            {
                rotation.initialDirection = new Unity.Mathematics.float3(
                    initialFacing.x,
                    initialFacing.y,
                    initialFacing.z);
                rotation.rotatePhysics = colliderTurnsToo;
                rotation.rotationIconOffset = rotationIconOffset;
            }

            DescriptionAuthoring description = EnsureComponent<DescriptionAuthoring>(root);
            description.initialText = textItComesWith ?? string.Empty;

            if (!untouchableForOneFrameOnly)
            {
                CantBeAttackedAuthoring existing = root.GetComponent<CantBeAttackedAuthoring>();
                if (existing != null)
                {
                    existing.removeAfterFirstFrame = false;
                }

                return;
            }

            if (alreadyUntouchableForever)
            {
                if (report != null)
                {
                    report(
                        "is untouchable forever and also marked untouchable for one frame only. " +
                        "The permanent one wins, so the one-frame rule does nothing.");
                }

                return;
            }

            EnsureComponent<CantBeAttackedAuthoring>(root).removeAfterFirstFrame = true;
        }

        /// The world tier a thing belongs to, and which ways its art can face.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both components were already being attached with nothing set, which is the most
        /// misleading state a generator can leave: present, therefore assumed configured.
        /// </para>
        /// <para>
        /// The tier is the one that changes numbers. <c>AreaLevelAuthoring</c> is what the health
        /// and damage curves read, so leaving it at Slime made every generated object compute
        /// starting-area stats no matter which biome it was built for.
        /// </para>
        /// </remarks>
        public static void ApplyBasics(
            GameObject root,
            DimensionObjectBasicsTemplate basics,
            System.Action<string> report,
            bool theTierMayBeAdded = true)
        {
            if (basics == null)
            {
                basics = new DimensionObjectBasicsTemplate();
            }

            // Facing is safe to write on anything: AnimationAuthoring is already universal and
            // orientation support changes nothing but whether the art may turn.
            EnsureComponent<AnimationAuthoring>(root).orientationSupport =
                (AnimationAuthoring.OrientationSupport)(int)basics.Facing;

            // THE TIER IS NOT SAFE TO ADD, and this pass used to add it anyway. AreaLevelAuthoring
            // is the level trap: with it present, MeleeAttackStateAuthoring.OnValidate recomputes
            // attack damage from the curve and offers no way out. A creature whose stats are set to
            // "the numbers you typed are kept exactly" has ApplyAreaLevel take the component off
            // during its state machine — and then this ran, four hundred lines later, and put it
            // straight back on, on the same object, in the same generate. The comment three lines
            // above said it only filled in a tier that was already there; the code below it did not.
            //
            // So a caller that has already decided says so, and the two controls are put in touch
            // instead of fighting.
            if (basics.ScalesWithItsTier)
            {
                if (theTierMayBeAdded)
                {
                    EnsureComponent<AreaLevelAuthoring>(root).areaLevel =
                        (AreaLevel)(int)basics.AreaTier;
                    return;
                }

                AreaLevelAuthoring alreadyThere = root.GetComponent<AreaLevelAuthoring>();
                if (alreadyThere != null)
                {
                    alreadyThere.areaLevel = (AreaLevel)(int)basics.AreaTier;
                    return;
                }

                if (report != null)
                {
                    report(
                        "is set to keep the numbers you typed exactly, and also to scale with its " +
                        "tier. It cannot do both: the game works an attacker's damage back out of " +
                        "the tier and there is no way to tell it not to, so your numbers would be " +
                        "thrown away. It keeps your numbers and the tier does nothing. Set its " +
                        "stats to the area level curve if you want the tier to decide them.");
                }

                return;
            }

            AreaLevelAuthoring level = root.GetComponent<AreaLevelAuthoring>();
            if (level != null)
            {
                // Creatures and plants add it themselves through their own stat source. Filling in
                // the tier there is free; adding it here would not be.
                level.areaLevel = (AreaLevel)(int)basics.AreaTier;
                return;
            }

            if (basics.ChoseATierThatWillBeIgnored && report != null)
            {
                report(
                    "was given the " + basics.AreaTier + " tier without ticking 'scales with its " +
                    "tier', and nothing on it reads a tier, so the choice does nothing.");
            }
        }

        /// <summary>
        /// Adds an inventory without letting its OnValidate take the generate down.
        /// </summary>
        /// <remarks>
        /// <c>InventoryAuthoring.slotRequirements</c> has no initialiser, so it is null the instant
        /// the component is added — and <c>OnValidate</c>, which Unity runs during that very
        /// AddComponent, reads <c>slotRequirements.Count</c>. Anything with a
        /// <c>RequireComponent(typeof(InventoryAuthoring))</c> on it — a trash can, equipment —
        /// drags that crash in behind it. The lists are filled in immediately so every later
        /// OnValidate, on save or on a modder opening the prefab, sees something valid.
        /// </remarks>
        public static InventoryAuthoring EnsureInventory(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            InventoryAuthoring existing = root.GetComponent<InventoryAuthoring>();
            if (existing != null)
            {
                if (existing.slotRequirements == null)
                {
                    existing.slotRequirements = new System.Collections.Generic.List<SlotRequirement>();
                }

                if (existing.itemsInInventory == null)
                {
                    existing.itemsInInventory = new System.Collections.Generic.List<ObjectData>();
                }

                return existing;
            }

            bool logging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            InventoryAuthoring added;
            try
            {
                added = root.AddComponent<InventoryAuthoring>();
            }
            catch (System.Exception)
            {
                added = root.GetComponent<InventoryAuthoring>();
            }
            finally
            {
                Debug.unityLogger.logEnabled = logging;
            }

            if (added != null)
            {
                added.slotRequirements = new System.Collections.Generic.List<SlotRequirement>();
                added.itemsInInventory = new System.Collections.Generic.List<ObjectData>();
            }

            return added;
        }

        /// <summary>
        /// Adds the conditions component without letting its OnValidate take the generate down.
        /// </summary>
        /// <remarks>
        /// <c>SupportsConditionsAuthoring.OnValidate</c> reaches for <c>ConditionsTable.GetTable()</c>
        /// and then walks the condition lists against it. Outside a running game that table is not
        /// there, so a fresh AddComponent throws — and because the throw escapes Configure, every
        /// component the generator would have written AFTER this one silently goes missing. That is
        /// how adding one field broke off-hand items and explosives three calls further down.
        ///
        /// The fix is the one the container generator already uses for InventoryAuthoring: add it
        /// inside a quiet window, and give the lists real values immediately so the next OnValidate
        /// has something valid to walk.
        /// </remarks>
        private static SupportsConditionsAuthoring EnsureSupportsConditions(GameObject root)
        {
            SupportsConditionsAuthoring existing = root.GetComponent<SupportsConditionsAuthoring>();
            if (existing != null)
            {
                return existing;
            }

            bool logging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            SupportsConditionsAuthoring added = null;
            try
            {
                added = root.AddComponent<SupportsConditionsAuthoring>();
            }
            catch (System.Exception)
            {
                // The component is on the object even when OnValidate threw part-way through it.
                added = root.GetComponent<SupportsConditionsAuthoring>();
            }
            finally
            {
                Debug.unityLogger.logEnabled = logging;
            }

            if (added != null)
            {
                added.initialConditions = new System.Collections.Generic.List<ConditionData>();
                added.initialConditionsWithRandomChance =
                    new System.Collections.Generic.List<SupportsConditionsAuthoring.ConditionWithRandomChance>();
            }

            return added;
        }

        /// <summary>
        /// What a thing starts affected by, and what conditions cannot touch it.
        /// </summary>
        /// <remarks>
        /// Certain conditions and chanced ones go into two different lists, because that is how the
        /// game keeps them. A creator says "this happens 40% of the time" once and the sorting
        /// happens here.
        /// </remarks>
        public static void ApplyInitialConditions(
            GameObject root,
            DimensionInitialConditionsTemplate conditions,
            System.Action<string> report)
        {
            SupportsConditionsAuthoring supports = EnsureSupportsConditions(root);

            if (supports == null)
            {
                return;
            }

            if (conditions == null)
            {
                conditions = new DimensionInitialConditionsTemplate();
            }

            supports.cantBeAffectedByAuras = conditions.AurasDoNotReachIt;
            supports.cantBeAffectedByEnvironment = conditions.TheEnvironmentDoesNotAffectIt;
            supports.cantBeAffectedByHealing = conditions.HealingDoesNotTouchIt;
            supports.dontShowStatsTextOnItem = conditions.HideStatsOnTheTooltip;

            supports.initialConditions = new System.Collections.Generic.List<ConditionData>();
            supports.initialConditionsWithRandomChance =
                new System.Collections.Generic.List<SupportsConditionsAuthoring.ConditionWithRandomChance>();

            if (conditions.ImmuneToSomethingItStartsWith && report != null)
            {
                report(
                    "starts with a healing condition and is also immune to healing, so the " +
                    "condition is applied and then does nothing.");
            }

            DimensionStartingCondition[] starting = conditions.StartsWith;
            for (int i = 0; i < starting.Length; i++)
            {
                DimensionStartingCondition wanted = starting[i];

                ConditionID id;
                if (!TryResolveCondition(wanted.ConditionId, out id))
                {
                    if (report != null)
                    {
                        report(
                            "starts with '" + wanted.ConditionId + "', which is not a condition the " +
                            "game has, so it starts with nothing.");
                    }

                    continue;
                }

                if (wanted.CanNeverHappen)
                {
                    if (report != null)
                    {
                        report(
                            "lists '" + wanted.ConditionId + "' as a starting condition with a " +
                            "chance of zero, so it can never happen.");
                    }

                    continue;
                }

                ConditionData data = new ConditionData
                {
                    conditionID = id,
                    duration = wanted.Seconds,
                    value = wanted.Strength,
                    valueMultiplier = wanted.StrengthMultiplier
                };

                if (wanted.IsCertain)
                {
                    supports.initialConditions.Add(data);
                }
                else
                {
                    supports.initialConditionsWithRandomChance.Add(
                        new SupportsConditionsAuthoring.ConditionWithRandomChance
                        {
                            conditionData = data,
                            chance = wanted.Chance
                        });
                }
            }
        }

        /// <summary>
        /// What an item does in the off hand, what it costs, and whether it can be polished.
        /// </summary>
        /// <remarks>
        /// Mana is written whether or not the item is an off-hand one, because a staff's secondary
        /// use costs mana too and the question a creator asked was "what does this cost".
        /// </remarks>
        public static void ApplyOffHandAndCost(
            GameObject root,
            DimensionOffHandTemplate offHand,
            string polishedVersionId,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (offHand == null)
            {
                offHand = new DimensionOffHandTemplate();
            }

            if (offHand.IsAnOffHandItem)
            {
                if (offHand.DoesNothingWhenUsed && report != null)
                {
                    report(
                        "is an off-hand item with no strength, so it equips and plays its animation " +
                        "and has no effect.");
                }

                OffHandAuthoring authored = EnsureComponent<OffHandAuthoring>(root);
                authored.mechanic = (OffHandMechanic)(int)offHand.Kind;
                authored.mechanicValue = offHand.Strength;
                authored.mechanicMultiplier = offHand.StrengthMultiplier;
            }
            else
            {
                RemoveComponentIfPresent<OffHandAuthoring>(root);
            }

            if (offHand.CostsMana)
            {
                ConsumesManaAuthoring mana = EnsureComponent<ConsumesManaAuthoring>(root);
                mana.manaCost = offHand.ManaCost;
                mana.manaCostMultiplier = offHand.ManaCostMultiplier;
            }
            else
            {
                RemoveComponentIfPresent<ConsumesManaAuthoring>(root);
            }

            ApplyJewelry(root, polishedVersionId, resolveObject, report, isDeferred);
        }

        /// <summary>Whether the item polishes into a better version of itself.</summary>
        /// <remarks>
        /// The component is KEPT when the polished version is one of the mod's own, and that is the
        /// only reason the link can work at all. Vanilla's <c>JewelryConverter</c> adds
        /// <c>JewelryCanBePolishedCD</c> only when the id it reads is not <c>None</c>, so a deferred
        /// reference leaves the prefab with no such component — which is why the hydration arm for
        /// this one field ADDS the component rather than writing into it. Strip
        /// <c>JewelryAuthoring</c> here and even that has nothing to hang off.
        /// </remarks>
        private static void ApplyJewelry(
            GameObject root,
            string polishedVersionId,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (string.IsNullOrEmpty(polishedVersionId))
            {
                RemoveComponentIfPresent<JewelryAuthoring>(root);
                return;
            }

            ObjectID polished = resolveObject == null
                ? ObjectID.None
                : resolveObject(polishedVersionId);
            if (polished == ObjectID.None &&
                !(isDeferred != null && isDeferred(polishedVersionId)))
            {
                if (report != null)
                {
                    report(
                        "polishes into '" + polishedVersionId + "', which is not an object the game " +
                        "has, so polishing it will do nothing.");
                }

                RemoveComponentIfPresent<JewelryAuthoring>(root);
                return;
            }

            EnsureComponent<JewelryAuthoring>(root).polishedVersion = polished;
        }

        /// <summary>
        /// The placed-object kinds that are a marker or one reference.
        /// </summary>
        /// <remarks>
        /// A fence gate is a bare marker. A flower names the plant it belongs to. A spawner platform
        /// names the enemy it produces — which makes it the simplest piece of dungeon furniture
        /// there is, and one a modder is very likely to want.
        /// </remarks>
        public static void ApplyPlacedKinds(
            GameObject root,
            bool isAFenceGate,
            string flowerOfPlantId,
            int flowerVariation,
            string spawnsEnemyId,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            Toggle<FenceGateAuthoring>(root, isAFenceGate);

            if (string.IsNullOrEmpty(flowerOfPlantId))
            {
                RemoveComponentIfPresent<FlowerAuthoring>(root);
            }
            else
            {
                ObjectID plant = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(flowerOfPlantId);
                bool plantIsOneOfOurs =
                    plant == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(flowerOfPlantId);
                if (plant == ObjectID.None && !plantIsOneOfOurs)
                {
                    if (report != null)
                    {
                        report(
                            "is the flower of '" + flowerOfPlantId + "', which is neither one of " +
                            "this mod's objects nor one the game has, so it belongs to nothing.");
                    }

                    RemoveComponentIfPresent<FlowerAuthoring>(root);
                }
                else
                {
                    // The component STAYS for one of the mod's own plants. FlowerConverter writes
                    // FlowerCD whatever the id is, and that component is what the link hydration
                    // fills in at load; stripping it would leave nothing to write into.
                    FlowerAuthoring flower = EnsureComponent<FlowerAuthoring>(root);
                    flower.plantID = plant;
                    flower.plantVariation = flowerVariation < 0 ? 0 : flowerVariation;
                }
            }

            if (string.IsNullOrEmpty(spawnsEnemyId))
            {
                RemoveComponentIfPresent<EnemySpawnerPlatformAuthoring>(root);
                return;
            }

            ObjectID enemy = resolveObject == null
                ? ObjectID.None
                : resolveObject(spawnsEnemyId);
            if (enemy == ObjectID.None && !(isDeferred != null && isDeferred(spawnsEnemyId)))
            {
                if (report != null)
                {
                    report(
                        "spawns '" + spawnsEnemyId + "', which is neither one of this mod's " +
                        "creatures nor one the game has, so it will spawn nothing.");
                }

                RemoveComponentIfPresent<EnemySpawnerPlatformAuthoring>(root);
                return;
            }

            EnsureComponent<EnemySpawnerPlatformAuthoring>(root).enemyToSpawn = enemy;
        }

        /// <summary>
        /// A waypoint players can travel to.
        /// </summary>
        /// <remarks>
        /// The core flag is worth guarding rather than trusting: it marks the one waypoint a player
        /// returns to, and a mod that sets it on several is a mod where "go home" is ambiguous.
        /// </remarks>
        public static void ApplyWaypoint(
            GameObject root,
            bool isAWaypoint,
            float activateWithin,
            bool isTheCoreWaypoint)
        {
            if (!isAWaypoint)
            {
                RemoveComponentIfPresent<WayPointAuthoring>(root);
                return;
            }

            WayPointAuthoring waypoint = EnsureComponent<WayPointAuthoring>(root);
            waypoint.distanceToActivate = activateWithin;
            waypoint.isCoreWaypoint = isTheCoreWaypoint;
        }

        /// <summary>
        /// The item kinds that are a marker or a small lookup rather than a system.
        /// </summary>
        /// <remarks>
        /// A potion is a bare marker the game reads for cooldowns and effects. A scanner is three
        /// fields: what it looks for, whether it summons that instead of pointing at it, and whether
        /// it only works in one biome.
        /// </remarks>
        public static void ApplyItemKinds(
            GameObject root,
            bool isAPotion,
            string scansForObjectId,
            bool summonsInstead,
            string onlyInBiome,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            Toggle<PotionAuthoring>(root, isAPotion);

            if (string.IsNullOrEmpty(scansForObjectId))
            {
                RemoveComponentIfPresent<ScannerAuthoring>(root);
                return;
            }

            ObjectID target = resolveObject == null
                ? ObjectID.None
                : resolveObject(scansForObjectId);
            if (target == ObjectID.None &&
                !(isDeferred != null && isDeferred(scansForObjectId)))
            {
                if (report != null)
                {
                    report(
                        "scans for '" + scansForObjectId + "', which is not an object the game has, " +
                        "so using it will find nothing.");
                }

                RemoveComponentIfPresent<ScannerAuthoring>(root);
                return;
            }

            // Kept for one of the mod's own targets. ScannerConverter always writes ScannerCD, and
            // it writes summonInsteadOfScan and onlyInBiome along with it — so the component has to
            // be here for those two to survive as well, not only for the id the runtime fills in.
            ScannerAuthoring scanner = EnsureComponent<ScannerAuthoring>(root);
            scanner.objectToScan = target;
            scanner.summonInsteadOfScan = summonsInstead;

            // Biome's default is a real biome rather than "anywhere", so it is only set when asked.
            Biome biome;
            if (!string.IsNullOrEmpty(onlyInBiome) && System.Enum.TryParse(onlyInBiome, false, out biome))
            {
                scanner.onlyInBiome = biome;
            }
        }

        /// <summary>
        /// What Core Keeper's automation can do with a thing.
        /// </summary>
        /// <remarks>
        /// Five markers and one settings block. The settings only mean anything on the thing doing
        /// the moving, so they are written only there — a chest with mover timings on it would be a
        /// component the game reads and nothing acts on.
        /// </remarks>
        public static void ApplyAutomation(
            GameObject root,
            DimensionAutomationTemplate automation,
            System.Action<string> report)
        {
            if (automation == null)
            {
                automation = new DimensionAutomationTemplate();
            }

            Toggle<Pug.Automation.AffectedByAutomationAuthoring>(
                root,
                automation.AutomationMayActOnIt);
            Toggle<Pug.Automation.AutomatedMineableAuthoring>(root, automation.ADrillCanMineIt);
            Toggle<Pug.Automation.AutomatedPlantableSeedAuthoring>(
                root,
                automation.ASeederCanPlantIt);
            // A CRAFTER NEEDS SLOTS TO CRAFT INTO. Core Keeper counts the machine's crafting slots
            // off its inventory, so a crafter with none is given a timer list of zero length and
            // can never finish anything — and the crafting answer beside it is attached by Unity
            // anyway, because the crafter component requires one, arriving empty and silencing the
            // framework's own "this bench has nothing to craft" warning. The game's own furnace
            // and cooking pot both carry a crafting answer AND an inventory beside the crafter.
            if (automation.AutomationCanCraftAtIt)
            {
                EnsureComponent<Pug.Automation.AutomatedCrafterAuthoring>(root);
                EnsureComponent<CraftingAuthoring>(root);

                bool hadSomewhereToPutThings = HasNamed(root, "InventoryAuthoring");
                EnsureInventory(root);
                SayWhenTicked(
                    !hadSomewhereToPutThings,
                    report,
                    "lets automation craft at it, and a machine crafts into its own slots — with " +
                    "none, the game gives it nothing to work in and it never finishes anything. " +
                    "Slots were filled in. Build it as a Station if you want to choose their size " +
                    "and what goes in them.");
            }
            else
            {
                RemoveComponentIfPresent<Pug.Automation.AutomatedCrafterAuthoring>(root);
            }

            Toggle<Pug.Automation.AutomatedMoverAuthoring>(root, automation.ItMovesThings);

            // The push is its own component and its own question: a belt that shoves the player
            // standing on it is not the same as a belt that carries items, and vanilla has both.
            // Applying it inside the mover path meant a pushing belt that carried nothing never
            // got its push at all.
            ApplyConveyorPush(root, automation, report);

            if (!automation.ItMovesThings)
            {
                RemoveComponentIfPresent<AutomatedMoverSharedAuthoring>(root);
                return;
            }

            if (automation.MovesNothingBecauseAMoveTakesNoTime && report != null)
            {
                report(
                    "moves things with a move time of zero, so no move ever completes. It reads as " +
                    "a conveyor that is switched on and does nothing.");
            }

            // WHICH WAY IT FACES. The game works out a mover's direction from the object's look,
            // and falls back to south for anything that cannot answer — so a belt, an arm and a
            // drill all pointed the same way whatever the player built. Every one of the game's
            // belts, arms and drills carries the answer that turns a variation into a direction.
            EnsureComponent<DirectionBasedOnVariationAuthoring>(root);

            // WHAT IT ACTUALLY CARRIES, which is nothing, and there is no way to say otherwise
            // from here. Core Keeper builds a mover's work from a list of tiles it reaches and
            // where it takes each one — a list the game's own belts and arms have and this
            // framework has no control for. Nothing plausible can be filled in: guessing a reach
            // would be inventing behaviour the author never asked for.
            SayWhenTicked(
                true,
                report,
                "moves things, but which tiles it reaches and where it takes them is not something " +
                "this framework can set yet, and the game builds the whole move out of that list. " +
                "It will run, be powered, face the right way and carry nothing. Use 'things " +
                "standing on it are pushed along' for a working conveyor, and put anything that " +
                "has to be carried into a container beside it.");

            AutomatedMoverSharedAuthoring shared =
                EnsureComponent<AutomatedMoverSharedAuthoring>(root);
            shared.moveTime = automation.MoveSeconds;
            shared.cooldownTime = automation.RestSeconds;
            shared.pickUpDuringMove = automation.PicksUpDuringTheMove;
            shared.allowPickupFromInventories = automation.MayTakeFromInventories;
            shared.splitOnMove = automation.SplitsStacks;
            shared.allowOnlyOneActiveMoverAtATime = automation.OnlyOneAtATime;
            shared.enabledMovers =
                (AutomatedMoverSharedAuthoring.CyclingType)(int)automation.Cycling;
        }

        /// <summary>
        /// Makes a placed thing hurt whatever comes near it.
        /// </summary>
        /// <remarks>
        /// The growing-hit-radius fields are deliberately left at their defaults: three fields with
        /// a whole sub-system behind them and no user anywhere in the game, so writing them would be
        /// shipping a knob nobody has ever turned.
        /// </remarks>
        public static void ApplyContinuousAttack(
            GameObject root,
            DimensionContinuousAttackTemplate attack,
            DimensionWiringTemplate wiring,
            System.Action<string> report)
        {
            if (attack == null || !attack.HurtsWhatComesNear)
            {
                RemoveComponentIfPresent<AttackContinuouslyAuthoring>(root);
                return;
            }

            if (attack.AttacksHarmlessly && report != null)
            {
                report("hurts what comes near it for no damage at all.");
            }

            if (attack.ReachesNothing && report != null)
            {
                report(
                    "hurts what comes near it but reaches nothing, so nothing ever comes near " +
                    "enough. Most of the game uses " +
                    DimensionContinuousAttackTemplate.OrdinaryHitRadius + ".");
            }

            if (attack.NeedsPowerButIsNotWired(wiring) && report != null)
            {
                report(
                    "needs power to attack and is not wired for any, so it will never fire. Give " +
                    "it a wiring role, or untick the power requirement.");
            }

            AttackContinuouslyAuthoring authored = EnsureComponent<AttackContinuouslyAuthoring>(root);
            authored.damage = attack.Damage;
            authored.damageMultiplier = attack.DamageMultiplier;
            authored.hitRadius = attack.Reach;
            authored.attackTime = attack.AttackSeconds;
            authored.cooldownAfterHit = attack.RestSeconds;
            authored.pushback = attack.Pushback;
            authored.requiresElectricity = attack.NeedsPower;
            authored.isStatic = attack.StaysPut;
            authored.cantDamageObjectsHangingOnWalls = attack.SparesThingsOnWalls;
            authored.canHitLowTriggers = attack.ReachesLowThings;
            authored.skipLootDropIfDestroyPlants = attack.PlantsItBreaksDropNothing;
            authored.hitRadiusGrowOverTime = attack.ReachGrowsWhileAttacking;
            authored.hitRadiusAfterGrowth = attack.ReachGrowsTo;
            authored.hitRadiusGrowthRate = attack.ReachGrowthRate;
            authored.canDamageOnlyEnemyAndPlayer = attack.OnlyHitsEnemiesAndPlayers;
            authored.canOnlyHitCertainNonEnemyObjects = attack.OnlyHitsCertainNonEnemies;
            authored.ignoreDamageReduction = attack.IgnoresDamageReduction;
            authored.damageEffectType = (DamageEffectType)(int)attack.DamageFlavour;
            authored.breakAfterSuccessfulHit = attack.BreaksAfterALandedHit;
            authored.breakDelay = attack.BreaksAfterThisLong;
            authored.triggerIdleAnimationOnEnteringState = attack.PlaysIdleOnStarting;
            authored.triggerAnimationOnHit = attack.HitAnimation;
        }

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
        /// The creatures that arrive alongside this one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The component holds real prefab references, not names, so this can only run once the
        /// companions exist. That makes it a second pass over the creature folder rather than part
        /// of configuring each creature — a creature listed before its companion would otherwise
        /// find nothing and generate silently without it.
        /// </para>
        /// <para>
        /// A companion that resolves to nothing is reported rather than dropped, because an empty
        /// companion list looks exactly like a creature that was never meant to have company.
        /// </para>
        /// </remarks>
        public static void ApplyCompanions(
            GameObject root,
            string[] companionIds,
            bool follow,
            Vector3 appearAt,
            System.Func<string, GameObject> findCompanion,
            System.Action<string> report)
        {
            if (companionIds == null || companionIds.Length == 0)
            {
                RemoveComponentIfPresent<SpawnCompanionsAuthoring>(root);
                return;
            }

            System.Collections.Generic.List<GameObject> found =
                new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < companionIds.Length; i++)
            {
                GameObject companion = findCompanion == null
                    ? null
                    : findCompanion(companionIds[i]);
                if (companion == null)
                {
                    if (report != null)
                    {
                        report(
                            "arrives with '" + companionIds[i] + "', which this mod does not " +
                            "generate as a creature, so it will arrive alone.");
                    }

                    continue;
                }

                found.Add(companion);
            }

            if (found.Count == 0)
            {
                RemoveComponentIfPresent<SpawnCompanionsAuthoring>(root);
                return;
            }

            SpawnCompanionsAuthoring companions = EnsureComponent<SpawnCompanionsAuthoring>(root);
            companions.companions = found;
            companions.follow = follow;
            companions.spawnOffset = new Unity.Mathematics.float3(appearAt.x, appearAt.y, appearAt.z);
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

        /// <summary>
        /// The three proximity gates: held item, nearby object, melody. The fourth — a key put
        /// inside — lives on containers, where vanilla keeps it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each gate is one vanilla component doing exactly what its tooltip says; they stack,
        /// which is how the game itself builds "hold the lantern near the singing wall" puzzles.
        /// Every one is added or removed, so unticking in the Studio really closes the gate.
        /// </para>
        /// <para>
        /// IT SHARES TWO OF THOSE COMPONENTS WITH THE REACTS-TO-NEARBY BLOCK, so it is told what
        /// that block claimed. This pass runs second, and its removals used to be unconditional —
        /// so an object that reacts to a nearby lantern, with no gate authored at all, had its
        /// reaction taken straight back off, silently. It now removes only what nothing else
        /// wanted, and says so when the two blocks ask for the same component.
        /// </para>
        /// </remarks>
        public static void ApplyGates(
            GameObject root,
            DimensionGateTemplate gate,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            DimensionReactsToNearbyTemplate reacts = null)
        {
            bool reactsClaimsNearby = reacts != null &&
                reacts.ReactsToANearbyObject && !reacts.WatchesForNothing;
            bool reactsClaimsHeld = reacts != null &&
                reacts.ReactsToAHeldObject && !reacts.WatchesForNothingHeld;

            if (root == null || gate == null || !gate.HasAnyGate)
            {
                if (!reactsClaimsHeld)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                }

                if (!reactsClaimsNearby)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                }

                return;
            }

            // ---- held item ----
            ObjectID held = string.IsNullOrEmpty(gate.OpensWhenHolding) || resolveObject == null
                ? ObjectID.None
                : resolveObject(gate.OpensWhenHolding);
            if (held != ObjectID.None)
            {
                ChangeVariationWhenPlayerHoldObjectNearbyAuthoring holding =
                    EnsureComponent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                holding.objectID = held;
                holding.radius = gate.HoldingReach;
                holding.variationToChangeTo = gate.HoldingOpenLook;

                // ALWAYS FALSE, and the author is told. Nothing in the game reads
                // ChangeVariationWhenPlayerHoldObjectNearbyCD.alsoRemoveCollider — a search over
                // the whole of ck-db finds a reader only on the CONTAINING-object variant, in
                // UpdateColliderWhenChangeVariationWhenContainingObjectSystem. The converter
                // copies the field and no system ever looks at it, so writing the author's answer
                // into it produced the same behaviour whichever way it was set. What DOES happen
                // is ResetColliderAfterVariationChangeSystem restoring the hitbox from the
                // object's own prefab every time the look changes, and with one prefab per object
                // that hitbox is the one it already had.
                holding.alsoRemoveCollider = false;

                SayWhenTicked(
                    gate.HoldingOpensTheWay,
                    report,
                    "is set to let players walk through it while it is held open, and it was " +
                    "generated keeping its hitbox. The game only takes a hitbox away for the " +
                    "gate that opens when something is put INSIDE it; for a gate that opens for " +
                    "a held item it reads nothing, and the hitbox is put straight back from the " +
                    "object's own shape. Its look still changes.");

                if (reactsClaimsHeld && report != null)
                {
                    report(
                        "opens for a player holding '" + gate.OpensWhenHolding + "' and also " +
                        "reacts to a player holding '" + reacts.WatchesForHeldObjectId + "'. An " +
                        "object can only watch for one held thing, so the gate is used. Clear one " +
                        "of the two.");
                }
            }
            else if (!reactsClaimsHeld)
            {
                RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                if (!string.IsNullOrEmpty(gate.OpensWhenHolding) && report != null)
                {
                    report(
                        "opens for a player holding '" + gate.OpensWhenHolding + "', which is " +
                        "not a known object, so that gate never opens.");
                }
            }
            else if (!string.IsNullOrEmpty(gate.OpensWhenHolding) && report != null)
            {
                report(
                    "opens for a player holding '" + gate.OpensWhenHolding + "', which is not a " +
                    "known object, so that gate never opens. What it reacts to when held is used " +
                    "instead.");
            }

            // ---- nearby object ----
            ObjectID nearby = string.IsNullOrEmpty(gate.OpensWhenObjectNearby) || resolveObject == null
                ? ObjectID.None
                : resolveObject(gate.OpensWhenObjectNearby);
            if (nearby != ObjectID.None)
            {
                ChangeVariationWhenObjectNearbyAuthoring nearbyGate =
                    EnsureComponent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                nearbyGate.objectID = nearby;
                nearbyGate.objectNearbySpecificVariation = false;
                nearbyGate.radius = gate.NearbyReach;
                nearbyGate.variationToChangeTo = gate.NearbyOpenLook;
                nearbyGate.dontRevertToOriginalVariation = gate.NearbyStaysOpen;

                if (reactsClaimsNearby && report != null)
                {
                    report(
                        "opens when '" + gate.OpensWhenObjectNearby + "' stands nearby and also " +
                        "reacts to '" + reacts.WatchesForObjectId + "' being placed near it. An " +
                        "object can only watch for one nearby thing, so the gate is used. Clear " +
                        "one of the two.");
                }
            }
            else if (!reactsClaimsNearby)
            {
                RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                if (!string.IsNullOrEmpty(gate.OpensWhenObjectNearby) && report != null)
                {
                    report(
                        "opens when '" + gate.OpensWhenObjectNearby + "' stands nearby, which " +
                        "is not a known object, so that gate never opens.");
                }
            }
            else if (!string.IsNullOrEmpty(gate.OpensWhenObjectNearby) && report != null)
            {
                report(
                    "opens when '" + gate.OpensWhenObjectNearby + "' stands nearby, which is not " +
                    "a known object, so that gate never opens. What it reacts to nearby is used " +
                    "instead.");
            }

            // ---- melody ----
            // THE MELODY GATE IS WRITTEN BY ApplyMelodyResponse, NOT HERE, and it has to be that
            // way round. Both blocks write the one AffectObjectWhenMelodyPlayedAuthoring, and this
            // one ran first: the melody pass then removed the component whenever its own block was
            // empty, so a door authored to open on a tune never opened and nothing said why. There
            // is one writer now, and it is handed both blocks.
        }

        /// <summary>
        /// Music that plays near an object.
        /// </summary>
        /// <remarks>
        /// The distances are the whole mechanism: it starts when a player comes within one and stops
        /// when they pass the other. A start distance smaller than the stop distance means it can
        /// never begin, which is why it is checked rather than assumed.
        /// </remarks>
        public static void ApplyMusicArea(
            GameObject root,
            DimensionMusicAreaTemplate music,
            System.Action<string> report)
        {
            if (music == null || !music.PlaysMusic)
            {
                RemoveComponentIfPresent<MusicAreaAuthoring>(root);
                return;
            }

            MusicRosterType roster;
            if (!System.Enum.TryParse(music.MusicId, false, out roster))
            {
                // Not a vanilla roster. With authored tracks, the name IS the mod's own cue:
                // its id derives from the name, the runtime registry appends the roster, and
                // the play hook feeds the clips. Without tracks it is just a name the game
                // does not have, and saying so beats silence.
                if (music.CustomTrackKeys.Length > 0)
                {
                    roster = (MusicRosterType)ExpandNullforge.Zones.DimensionMusicRosterIds.For(music.MusicId);
                }
                else
                {
                    if (report != null)
                    {
                        report(
                            "plays '" + music.MusicId + "' nearby, which is not music the game has, " +
                            "and it lists no tracks of its own, so nothing will play. Name a game " +
                            "roster like BOSS, or add track clip keys to make it your own music.");
                    }

                    RemoveComponentIfPresent<MusicAreaAuthoring>(root);
                    return;
                }
            }

            if (music.CanNeverStart && report != null)
            {
                report(
                    "starts its music closer than it stops it, so a player walking up to it passes " +
                    "the stop distance first and the music never begins.");
            }

            MusicAreaAuthoring area = EnsureComponent<MusicAreaAuthoring>(root);
            area.musicRosterType = roster;
            area.startAtDistance = music.StartsWithin;
            area.stopAtDistance = music.StopsBeyond;
            area.fadeTime = music.FadeSeconds;
            area.prio = music.Priority;
            area.minCooldownToPlay = music.MinWaitBetweenPlays;
            area.maxCooldownToPlay = music.MaxWaitBetweenPlays;
            area.deactivateWhenEntityIsInState = music.GoesQuietInAState;

            StateID quietState;
            if (music.GoesQuietInAState &&
                !string.IsNullOrEmpty(music.QuietInThisState) &&
                System.Enum.TryParse(music.QuietInThisState, false, out quietState))
            {
                area.stateToDeactivateIn = quietState;
            }
            else if (music.GoesQuietInAState && report != null)
            {
                area.deactivateWhenEntityIsInState = false;
                report(
                    "goes quiet in state '" + music.QuietInThisState + "', which the game does " +
                    "not have, so its music plays throughout instead.");
            }
            area.activeWhenEntityIsInCombat = music.OnlyInCombat;
            area.playOtherMusicWhenInCombat = music.HasCombatMusic;

            if (music.HasCombatMusic)
            {
                MusicRosterType combat;
                if (System.Enum.TryParse(music.CombatMusicId, false, out combat))
                {
                    area.otherMusicRosterType = combat;
                    area.otherFadeTime = music.FadeSeconds;
                }
                else if (report != null)
                {
                    report(
                        "plays '" + music.CombatMusicId + "' in combat, which is not music the game " +
                        "has, so the ordinary music keeps playing instead.");
                }
            }
        }

        /// <summary>
        /// <summary>
        /// The small rules the world applies to a placed object.
        /// </summary>
        /// <remarks>
        /// Seven components, most of them bare markers. Every one is added or removed rather than
        /// only added, so an author who unticks something gets the object they see in the inspector.
        /// </remarks>
        /// <summary>
        /// The last of the placed-object components: paint, scanning, map pins, lifetimes,
        /// variation-driven facing, and the table condition.
        /// </summary>
        /// <remarks>
        /// These are one and two field components rather than systems of their own, so they are
        /// written together here instead of each getting a template. Every one of them was attached
        /// somewhere and left blank, which is the quiet half of the coverage problem: the object
        /// carries the component, the game reads it, and it says nothing.
        /// </remarks>
        private static void ApplyTheRestOfTheObjectRules(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            PaintableObjectAuthoring paintable = root.GetComponent<PaintableObjectAuthoring>();
            if (paintable != null)
            {
                PaintableColor colour;
                if (System.Enum.TryParse(rules.StartingPaintColour, false, out colour))
                {
                    paintable.color = colour;
                }
                else if (report != null)
                {
                    report(
                        "starts painted '" + rules.StartingPaintColour + "', which is not a colour " +
                        "the game paints things, so it starts unpainted.");
                }
            }

            CanBeScannedAuthoring scanned = root.GetComponent<CanBeScannedAuthoring>();
            if (scanned != null && !string.IsNullOrEmpty(rules.ScannerReportsObjectId))
            {
                ObjectID reported = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(rules.ScannerReportsObjectId);
                if (reported != ObjectID.None)
                {
                    scanned.objectData = new ObjectData
                    {
                        objectID = reported,
                        variation = rules.ScannerReportsVariation
                    };
                }
                else if (report != null)
                {
                    report(
                        "reports as '" + rules.ScannerReportsObjectId + "' to a scanner, which the " +
                        "game does not have, so it reports as itself.");
                }
            }

            // THE ANSWER USED TO REACH NOTHING. This was a GetComponent, and nothing anywhere in
            // this framework ever ADDED DontDestroyOnZeroHealthAuthoring — the only other mention
            // of it is a RemoveComponentIfPresent on the framework's own portal — so the tickbox
            // was read, found no component, and did nothing at all, for every object ever
            // generated. `DontDestroyOnZeroHealthConverter` adds `AnimateDontDestroyOnZeroHealthCD`
            // when animate is on, and `AnimateDontDestroyOnZeroHealthSystem` needs only that, the
            // health, the animation buffer and its pointer — which anything with health already
            // carries — so putting the component on is the whole of the fix, and the two clips it
            // fires ("feignDeath" at `:93` and "revive" at `:97`) become drawable.
            //
            // ONLY ON THE WAY ON. Ticking it changes what the object IS — with
            // `DontDestroyOnZeroHealthCD` present and not disabled, `SetEntitiesDestroyedSystem`
            // returns before it ever marks the object destroyed (`:163`), so the object survives
            // zero health and drops nothing. Unticking it therefore only clears the animate flag
            // and leaves a component a borrowed kit may have put there alone, rather than quietly
            // making something mortal that was authored not to be.
            DontDestroyOnZeroHealthAuthoring survives = rules.AnimatesWhenItWouldHaveDied
                ? EnsureComponent<DontDestroyOnZeroHealthAuthoring>(root)
                : root.GetComponent<DontDestroyOnZeroHealthAuthoring>();
            if (survives != null)
            {
                survives.animate = rules.AnimatesWhenItWouldHaveDied;
            }

            SayWhenTicked(
                rules.AnimatesWhenItWouldHaveDied,
                report,
                "plays dead instead of dying. At zero health the game stops short of destroying " +
                "it: it drops, and it stands back up if anything ever heals it. It will not drop " +
                "loot and it will not disappear, because it never actually dies, so something " +
                "else has to take it away. Draw 'Playing dead' and 'Getting back up' for it.");

            ImmuneToSkipLootDropAuthoring lootImmune =
                root.GetComponent<ImmuneToSkipLootDropAuthoring>();
            if (lootImmune != null)
            {
                lootImmune.ignoredInCreativeMode = rules.CreativeIgnoresItsLootImmunity;
            }

            InteractWithEnvironmentAuthoring environment =
                root.GetComponent<InteractWithEnvironmentAuthoring>();
            if (environment != null)
            {
                environment.disableComponent =
                    new Pug.UnityExtensions.PlatformDependentValue<bool>(
                        rules.EnvironmentInteractionIsOff);
            }

            AnimationAuthoring animation = root.GetComponent<AnimationAuthoring>();
            if (animation != null)
            {
                animation.largeAnimationHistorySupport = rules.SupportsLongAnimationHistory;
            }

            AreaLevelAuthoring level = root.GetComponent<AreaLevelAuthoring>();
            if (level != null)
            {
                level.forceGenerateLevelEntities = rules.ForcesLevelEntities;
            }

            ImmunityZoneAuthoring zone = root.GetComponent<ImmunityZoneAuthoring>();
            if (zone != null)
            {
                zone.tileOffset = new Unity.Mathematics.int2(
                    rules.ImmunityZoneTileOffset.x,
                    rules.ImmunityZoneTileOffset.y);
            }

            MapMarkerAuthoring marker = root.GetComponent<MapMarkerAuthoring>();
            if (marker != null)
            {
                // uniqueMarkerId is an ObjectID, not a name: it is the object whose single map pin
                // this one shares, which is how a multi-part structure shows up once.
                marker.uniqueMarkerId = string.IsNullOrEmpty(rules.SharedMapMarkerId)
                    ? ObjectID.None
                    : (resolveObject == null ? ObjectID.None : resolveObject(rules.SharedMapMarkerId));

                if (!string.IsNullOrEmpty(rules.SharedMapMarkerId) &&
                    marker.uniqueMarkerId == ObjectID.None &&
                    report != null)
                {
                    report(
                        "shares its map pin with '" + rules.SharedMapMarkerId + "', which the " +
                        "game does not have, so it gets its own pin.");
                }
                UserMapMarkerType markerKind;
                if (System.Enum.TryParse(rules.PlayerMarkerKind, false, out markerKind))
                {
                    marker.userMapMarkerType = markerKind;
                }
                else if (report != null)
                {
                    report(
                        "shows as map marker kind '" + rules.PlayerMarkerKind + "', which the game " +
                        "does not have, so it shows as an ordinary pin.");
                }
            }

            DirectionBasedOnVariationAuthoring facing =
                root.GetComponent<DirectionBasedOnVariationAuthoring>();
            if (facing != null)
            {
                facing.direction = new Unity.Mathematics.int2(
                    rules.FacingFromVariation.x,
                    rules.FacingFromVariation.y);
                // ALWAYS FALSE, and the author is told why. It is no longer a crash: every
                // generated object now says its look is not part of its identity
                // (DimensionQueryCompanions.ItsLookIsNotPartOfItsIdentity), so
                // ColliderBasedOnDirectionVariationSystem's lookup returns this object's own
                // prefab instead of Entity.Null. What it would then copy is the collider the
                // object already has, because this framework writes one prefab per object and that
                // prefab has one shape — so the switch could only ever pretend to do something.
                //
                // THE AUTHOR'S ANSWER IS NOT LOST. "Its hitbox turns with it" is carried by
                // RotationAuthoring.rotatePhysics instead, which RotationPostConverter turns into
                // four rotated shapes and RotateColliderSystem picks between by direction. That is
                // a real turned hitbox rather than a second look that does not exist.
                facing.alsoUpdateCollider = false;
                facing.alignWithNearbyAffectorsWhenPlaced = rules.LinesUpWithNeighboursWhenPlaced;

                SayWhenTicked(
                    rules.ColliderTurnsWithIt,
                    report,
                    "is set to turn its hitbox with it. Its hitbox is turned by rotating the one " +
                    "shape it has, which is what the game does for anything that faces a " +
                    "direction. The other road — looking the object up under a second look and " +
                    "taking that one's hitbox — is switched off, because this framework builds " +
                    "one look per object and there is no second hitbox to find.");
            }

            DestroyTimerAuthoring timer = root.GetComponent<DestroyTimerAuthoring>();
            if (timer != null)
            {
                timer.disablePhysicsAfterDuration = rules.PhysicsStopAfterSeconds;
                timer.dontDropLootAfterTimerRunsOut = rules.DropsNothingWhenItsTimeIsUp;
                timer.startTimerWhenVariation = rules.LifetimeStartsAtVariation;
            }

            TableItemLightSourceAuthoring table =
                root.GetComponent<TableItemLightSourceAuthoring>();

            // A LAMP WITH NO GLOW LIGHTS NOTHING, and that was the whole of it. Core Keeper decides
            // whether an object standing on a table gives off light by looking at the glow written
            // here: one of the five glow colours, with its strength used as the RANGE. Left blank,
            // the check falls through every branch, the light is switched off and the object sits
            // there dark. The game's own torch ships an orange glow at a range of two.
            if (table != null && string.IsNullOrEmpty(rules.TableConditionId) && report != null)
            {
                report(
                    "lights the room where it is placed but was not given a glow, and the glow is " +
                    "what the light is made of: without one the game leaves the light switched " +
                    "off. Set the table glow to one of the game's glow colours and give it a " +
                    "strength, which is how far the light reaches. The game's own torch uses an " +
                    "orange glow at two.");
            }

            if (table != null && !string.IsNullOrEmpty(rules.TableConditionId))
            {
                ConditionID tableCondition;
                if (System.Enum.TryParse(rules.TableConditionId, false, out tableCondition))
                {
                    table.Condition = new SimpleConditionData
                    {
                        conditionID = tableCondition,
                        value = rules.TableConditionStrength
                    };
                }
                else if (report != null)
                {
                    report(
                        "gives condition '" + rules.TableConditionId + "' from a table, which the " +
                        "game does not have, so it gives nothing.");
                }
            }
        }

        public static void ApplyObjectRules(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null,
            System.Func<string, string> qualify = null)
        {
            if (rules == null)
            {
                rules = new DimensionObjectRulesTemplate();
            }

            Toggle<AlwaysDropVariationZeroAuthoring>(root, rules.AlwaysDropsItsFirstVariation);
            Toggle<GroundDecorationAuthoring>(root, rules.IsGroundCover);

            ApplyTheRestOfTheObjectRules(root, rules, resolveObject, report);

            if (rules.AScannerFindsIt)
            {
                EnsureComponent<CanBeScannedAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<CanBeScannedAuthoring>(root);
            }

            // alwaysEnabled defaults to true on the component, so the tick is only worth writing
            // when it is being turned OFF - otherwise every object carries a component saying what
            // would have happened anyway.
            if (!rules.AlwaysStaysEnabled)
            {
                EnsureComponent<CustomDisableAuthoring>(root).alwaysEnabled = false;
            }
            else
            {
                RemoveComponentIfPresent<CustomDisableAuthoring>(root);
            }

            if (rules.OverridesNetworkRange)
            {
                EnsureComponent<OverrideNetworkSyncDistanceAuthoring>(root).distance =
                    rules.NetworkRange;
            }
            else
            {
                RemoveComponentIfPresent<OverrideNetworkSyncDistanceAuthoring>(root);
            }

            ApplyPlacementRule(root, rules, resolveObject, report, isDeferred, qualify);
            ApplyMapMarker(root, rules, report);
        }

        private static void ApplyPlacementRule(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred,
            System.Func<string, string> qualify)
        {
            if (!rules.HasAPlacementRule)
            {
                RemoveComponentIfPresent<DestroyIfNotOnTileAuthoring>(root);
                ClearPlacedOnTileNames(root);
                return;
            }

            if (rules.MayStandNowhere && report != null)
            {
                report(
                    "has a rule about where it may stand that matches no tile at all, so it will " +
                    "destroy itself the moment it is placed.");
            }

            DestroyIfNotOnTileAuthoring rule = EnsureComponent<DestroyIfNotOnTileAuthoring>(root);
            ObjectID stands = resolveObject == null
                ? ObjectID.None
                : resolveObject(rules.MustStandOnObjectId);

            // ONE OF THE MOD'S OWN TILE OBJECTS RIDES AS A NAME. validTileObject ends up in the
            // sealed property blob (CanBePlaced/allowedObjects), which no system can reach on any
            // tick — so unlike every other reference in the framework this one cannot be filled in
            // at load. DimensionPlacedOnNamesAuthoring carries the name instead and the placement
            // post-converter writes the list once every object has an id. The component existed and
            // its three tile fields were tooltipped, public, and written by nothing.
            bool standsOnOneOfOurs =
                stands == ObjectID.None &&
                isDeferred != null &&
                isDeferred(rules.MustStandOnObjectId);

            if (standsOnOneOfOurs)
            {
                ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring names =
                    EnsureComponent<ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>(root);
                names.allowedTileObjectNames = new string[]
                {
                    qualify == null
                        ? rules.MustStandOnObjectId
                        : qualify(rules.MustStandOnObjectId)
                };
            }
            else
            {
                ClearPlacedOnTileNames(root);
            }

            if (stands == ObjectID.None && !standsOnOneOfOurs &&
                !string.IsNullOrEmpty(rules.MustStandOnObjectId) &&
                report != null)
            {
                report(
                    "must stand on '" + rules.MustStandOnObjectId + "', which is neither one of " +
                    "this mod's objects nor one the game has, so it will destroy itself wherever " +
                    "it is placed.");
            }

            rule.validTileObject = stands;
            rule.canBePlacedOnAnyWalkableTile = rules.AnyWalkableTileWillDo;
            rule.canBePlacedOnWater = rules.MayStandOnWater;
            rule.canBePlacedOnLava = rules.MayStandOnLava;
            rule.canBePlacedOnPit = rules.MayStandOverAPit;
        }

        /// <summary>
        /// Takes back the tile names this pass wrote, without touching the other two lists.
        /// </summary>
        /// <remarks>
        /// The component is shared with the vehicle generator's "also goes on" list, so removing it
        /// outright would delete somebody else's answer. Only the fields this pass owns are cleared,
        /// and the component goes only when nothing is left on it.
        /// </remarks>
        private static void ClearPlacedOnTileNames(GameObject root)
        {
            ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring names =
                root.GetComponent<ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>();
            if (names == null)
            {
                return;
            }

            names.allowedTileObjectNames = new string[0];
            if (names.canBePlacedOnNames == null || names.canBePlacedOnNames.Length == 0)
            {
                RemoveComponentIfPresent<
                    ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>(root);
            }
        }

        private static void ApplyMapMarker(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Action<string> report)
        {
            if (!rules.ShowsOnTheMap)
            {
                RemoveComponentIfPresent<MapMarkerAuthoring>(root);
                return;
            }

            MapMarkerType marker;
            if (!System.Enum.TryParse(rules.MapMarker, false, out marker))
            {
                if (report != null)
                {
                    report(
                        "shows on the map as '" + rules.MapMarker + "', which is not a marker the " +
                        "game has, so it will not show at all.");
                }

                RemoveComponentIfPresent<MapMarkerAuthoring>(root);
                return;
            }

            MapMarkerAuthoring authored = EnsureComponent<MapMarkerAuthoring>(root);
            authored.mapMarkerType = marker;
            authored.hideWhenDiscovered = rules.MarkerGoesOnceFound;
        }

        /// <summary>Adds or removes a component that carries nothing but its own presence.</summary>
        private static void Toggle<T>(GameObject root, bool wanted) where T : Component
        {
            if (wanted)
            {
                EnsureComponent<T>(root);
            }
            else
            {
                RemoveComponentIfPresent<T>(root);
            }
        }

        /// <summary>
        /// Tells the author what a tick they turned on will and will not do.
        /// </summary>
        /// <remarks>
        /// <para>
        /// SILENCE IS THE DEFECT, not the gap. Four censuses against the game's own systems found a
        /// group of answers that write exactly the component they say they write, convert cleanly,
        /// and then do nothing — because Core Keeper reads that component only off a player, only
        /// off an item somebody is holding, or in a few cases reads it nowhere at all. None of that
        /// can be fixed from here; what can be fixed is a control that says nothing while it does
        /// nothing. Every sentence below names what the author ticked, what will happen, and where
        /// to put the answer instead when there is somewhere to put it.
        /// </para>
        /// <para>
        /// It fires on every generate rather than once, because a generation report is read per
        /// run and a warning that appeared only the first time would be invisible to whoever picks
        /// the project up next.
        /// </para>
        /// </remarks>
        private static void SayWhenTicked(bool ticked, System.Action<string> report, string sentence)
        {
            if (ticked && report != null)
            {
                report(sentence);
            }
        }

        /// <summary>
        /// Puts a health pool back on an unbreakable object whose feature is written over one.
        /// </summary>
        /// <remarks>
        /// "Cannot be attacked" takes the pool off, which is right for scenery and wrong for the
        /// handful of features that use health as a dial rather than as damage: both boss beams
        /// and the hive egg name it as something they WRITE, and are skipped entirely without it.
        /// Core Keeper ships all three of those carrying "cannot be attacked" and a pool together.
        /// Nothing else is put back — no mineable, no hurt state — so the object stays untouchable.
        /// </remarks>
        private static void GiveItBackTheHealthAnUnbreakableThingStillNeeds(
            GameObject root,
            System.Action<string> report,
            string sentence)
        {
            if (root == null || !HasNamed(root, "CantBeAttackedAuthoring"))
            {
                return;
            }

            if (root.GetComponent<HealthAuthoring>() != null)
            {
                return;
            }

            HealthAuthoring pool = EnsureComponent<HealthAuthoring>(root);
            pool.dontCalculateHealthFromLevel = true;
            pool.maxHealth = 1;
            pool.startHealth = 1;
            pool.maxHealthMultiplier = 1f;

            SayWhenTicked(true, report, sentence);
        }

        /// <summary>Whether the object's picture carries a seat the game can sit somebody on.</summary>
        /// <remarks>
        /// Looked up by type NAME through the picture's children rather than by type, because the
        /// seat marker lives in a game assembly and this is the only place that asks about it.
        /// </remarks>
        private static bool ThereIsSomewhereToSit(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            ObjectAuthoring identity = root.GetComponent<ObjectAuthoring>();
            if (identity == null || identity.graphicalPrefab == null)
            {
                return false;
            }

            Component[] parts = identity.graphicalPrefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && parts[i].GetType().Name == "SittableObject")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a component with this type name is on the object.</summary>
        /// <remarks>
        /// By name rather than by type, so a check can name a component from an assembly this file
        /// does not reference without widening the assembly graph for one test.
        /// </remarks>
        private static bool HasNamed(GameObject root, string typeName)
        {
            if (root == null)
            {
                return false;
            }

            Component[] present = root.GetComponents<Component>();
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i] != null && present[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What part an item plays in cooking, what it lends the dish, and what eating it gives.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE CATEGORY TAG IS WHAT LETS THE POT TAKE IT AT ALL. The cooking pot's two input slots
        /// accept objects by tag, not by component, so an ingredient with a perfect
        /// <c>CookingIngredientAuthoring</c> and no <c>CookingIngredient</c> tag simply cannot be
        /// dropped in — the slot refuses it and nothing says why. The tag is stamped here rather
        /// than left to the creator for exactly that reason. The two cooked-food tags are the same
        /// story for anything that reads dishes by tier.
        /// </para>
        /// <para>
        /// RAW AND COOKED ARE TWO HALVES OF ONE ENTRY. Core Keeper stores what a food gives as pairs
        /// — the raw payload and the cooked payload side by side — and picks the cooked half for
        /// anything that came out of a pot. Writing one value into both halves, which is what this
        /// did before, threw away the whole "better when cooked" mechanic; the two lists are now
        /// zipped, longest wins, and a missing half repeats its partner.
        /// </para>
        /// <para>
        /// THE ORDER OF THIS CALL MATTERS. It appends to the same consumed-conditions component that
        /// <see cref="ApplyItemEffects"/> writes, and it can only append safely because that method
        /// runs first and always either replaces the whole list or removes the component. Move this
        /// above it and every regenerate would stack another copy of the ingredient's effects on
        /// top of the last.
        /// </para>
        /// <para>
        /// A dish's <c>rareVersion</c>/<c>epicVersion</c> and an ingredient's dish are ObjectIDs. A
        /// vanilla target is a known number and is baked here; one of the mod's own is not knowable
        /// offline and is filled in at runtime by the food hydration system, so it is left at None
        /// and NOT warned about.
        /// </para>
        /// </remarks>
        public static void ApplyCooking(
            GameObject root,
            DimensionCookingTemplate cooking,
            Sprite ownPicture,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, DimensionCookingTemplate> findIngredient,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> reportUnknownCondition,
            System.Action<string> report)
        {
            if (cooking == null || !cooking.TakesPartInCooking)
            {
                RemoveComponentIfPresent<CookingIngredientAuthoring>(root);
                RemoveComponentIfPresent<CookedFoodAuthoring>(root);
                RemoveComponentIfPresent<FishAuthoring>(root);
                SetCategoryTag(root, ObjectCategoryTag.CookingIngredient, false);
                SetCategoryTag(root, ObjectCategoryTag.UncommonOrLowerCookedFood, false);
                SetCategoryTag(root, ObjectCategoryTag.RareOrHigherCookedFood, false);
                StopItBeingEdible(root);
                return;
            }

            if (!cooking.IsAnIngredient)
            {
                RemoveComponentIfPresent<CookingIngredientAuthoring>(root);
                RemoveComponentIfPresent<FishAuthoring>(root);
            }

            if (!cooking.IsACookedDish)
            {
                RemoveComponentIfPresent<CookedFoodAuthoring>(root);
            }

            MakeItEdible(root);

            if (cooking.IsAnIngredient)
            {
                ApplyIngredient(
                    root,
                    cooking,
                    ownPicture,
                    resolveObject,
                    isOneOfOurOwn,
                    reportUnknownCondition,
                    report);
                return;
            }

            ApplyDish(root, cooking, resolveObject, findIngredient, isOneOfOurOwn, report);
        }

        /// <summary>
        /// Marks the object as something a player can eat.
        /// </summary>
        /// <remarks>
        /// EATING IS DECIDED BY THE OBJECT TYPE, NOT BY HAVING FOOD DATA ON IT. The game routes a
        /// held item to a slot behaviour by its <c>ObjectType</c>, and only Eatable reaches the
        /// eating slot — so an ingredient with a full set of consume conditions and any other type
        /// is a thing that grants nothing because it can never be eaten. Every one of the game's 79
        /// ingredients and 45 dishes carries type 1100, including the ones that can also be placed
        /// in the world; placement rides on separate components and does not compete for the type.
        /// Set here rather than left to the creator because "it is food" is already the answer they
        /// gave.
        /// </remarks>
        private static void MakeItEdible(GameObject root)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring != null)
            {
                objectAuthoring.objectType = ObjectType.Eatable;
            }
        }

        /// <summary>
        /// Undoes <see cref="MakeItEdible"/> for something that has stopped being food.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only ever changes a type that says Eatable, and leaves every other value alone.
        /// Re-running generation must not leave an item that is no longer food still sitting in the
        /// eating slot.
        /// </para>
        /// <para>
        /// It used to be true that Eatable could only have come from this pass. It is not any more:
        /// an author can now say "this is food" on the item itself, and this method resets that too.
        /// That is on purpose and the item generator depends on it — the cooking block is the single
        /// source for food, because the game decides eating from the same answer that decides a
        /// slot, and two answers that could disagree would produce a dish nobody can eat. The
        /// generator says so in its report rather than letting the choice vanish.
        /// </para>
        /// </remarks>
        private static void StopItBeingEdible(GameObject root)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring != null && objectAuthoring.objectType == ObjectType.Eatable)
            {
                objectAuthoring.objectType = ObjectType.NonUsable;
            }
        }

        private static void ApplyIngredient(
            GameObject root,
            DimensionCookingTemplate cooking,
            Sprite ownPicture,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> reportUnknownCondition,
            System.Action<string> report)
        {
            CookingIngredientAuthoring ingredient =
                EnsureComponent<CookingIngredientAuthoring>(root);
            ingredient.ingredientType = (IngredientType)(int)cooking.IngredientKind;

            Color[] shades = null;
            if (cooking.ColoursFromItsOwnPicture && ownPicture != null)
            {
                DimensionFoodPalette.TryExtractRamp(ownPicture, out shades);
            }

            if (shades != null)
            {
                ingredient.brightestColor = shades[0];
                ingredient.brightColor = shades[1];
                ingredient.darkColor = shades[2];
                ingredient.darkestColor = shades[3];
            }
            else
            {
                if (cooking.ColoursFromItsOwnPicture && report != null)
                {
                    report(
                        "should take its cooking colours from its own picture, but no picture " +
                        "could be read, so the four colours typed on the item were used instead. " +
                        "Drag the icon into the item's Icon field — an icon found by id is not " +
                        "read for colours — or untick taking the colours from the picture.");
                }

                ingredient.brightestColor = cooking.Brightest;
                ingredient.brightColor = cooking.Bright;
                ingredient.darkColor = cooking.Dark;
                ingredient.darkestColor = cooking.Darkest;
            }

            ingredient.turnsIntoFood = ResolveFoodTarget(
                cooking.MakesDish,
                "makes",
                resolveObject,
                isOneOfOurOwn,
                report);

            SetCategoryTag(root, ObjectCategoryTag.CookingIngredient, true);
            Toggle<FishAuthoring>(root, cooking.CanBeFished);
            SetCategoryTag(root, ObjectCategoryTag.Fish, cooking.CanBeFished);

            // A rare flower in the pot is one of the two things that can push a dish to epic, and
            // the game asks that question of the flower COMPONENT plus the object's rarity. The
            // rarity is the creator's own field on the item; this is the other half.
            if (cooking.CountsAsAFlower)
            {
                EnsureComponent<FlowerAuthoring>(root);
            }

            ApplyRawAndCookedConditions(root, cooking, reportUnknownCondition);

            // THE LEVEL TRAP, in the one place it cannot be switched off. Every other
            // condition-giving component has a "leave my numbers alone" flag;
            // GivesConditionsWhenConsumedAuthoring does not — it recomputes its whole list from
            // the world tier the moment Unity validates the prefab. So an ingredient with a tier
            // ships whatever the curve says, not what its author typed, and there is no field
            // anywhere that changes that.
            if (root.GetComponent<AreaLevelAuthoring>() != null &&
                (cooking.GivesRaw.Length > 0 || cooking.GivesCooked.Length > 0) &&
                report != null)
            {
                report(
                    "has a world tier, and the game recomputes what a food gives from that tier " +
                    "rather than from the numbers typed on it. There is no way to turn that off " +
                    "for food. Clear the world tier if the raw and cooked amounts here are meant " +
                    "to be exactly what a player gets.");
            }
        }

        private static void ApplyDish(
            GameObject root,
            DimensionCookingTemplate cooking,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, DimensionCookingTemplate> findIngredient,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> report)
        {
            // BOTH LISTS ARE DRAWN ON EVERY ITEM AND BOTH ARE DROPPED FOR A DISH. Not an oversight
            // here: the game works out what a meal does to you from the INGREDIENTS it was cooked
            // from — it reads their consumed-conditions and picks each one's cooked half — so a
            // finished dish has nowhere for its own effects to come from. Filling them in on a dish
            // used to grant nothing and say nothing, so a custom stew meant to heal healed nobody.
            if (cooking.RawOrCookedTypedWhereNothingReadsThem && report != null)
            {
                report(
                    "is a dish with 'gives raw' or 'gives cooked' filled in, and neither reaches " +
                    "anybody. What a meal does to whoever eats it comes from the ingredients it " +
                    "was cooked from — put the effects on those instead.");
            }

            CookedFoodAuthoring dish = EnsureComponent<CookedFoodAuthoring>(root);
            dish.rareVersion = ResolveFoodTarget(
                cooking.RareVersion, "upgrades to", resolveObject, isOneOfOurOwn, report);
            dish.epicVersion = ResolveFoodTarget(
                cooking.EpicVersion, "upgrades to", resolveObject, isOneOfOurOwn, report);

            DimensionCookingTemplate first = findIngredient == null
                ? null
                : findIngredient(cooking.MadeFrom);
            DimensionCookingTemplate second = findIngredient == null
                ? null
                : findIngredient(cooking.MadeFromAlso);

            if (first == null && !string.IsNullOrEmpty(cooking.MadeFrom) && report != null)
            {
                report(
                    "is made from '" + cooking.MadeFrom + "', which this mod does not define as an " +
                    "ingredient, so its colours are taken from the dish itself instead.");
            }

            if (second == null && !string.IsNullOrEmpty(cooking.MadeFromAlso) && report != null)
            {
                report(
                    "is made from '" + cooking.MadeFromAlso + "', which this mod does not define " +
                    "as an ingredient, so its colours are taken from the dish itself instead.");
            }

            DimensionCookingTemplate paletteA = first ?? cooking;
            DimensionCookingTemplate paletteB = second ?? cooking;

            dish.ingredient1BrightestColor = paletteA.Brightest;
            dish.ingredient1BrightColor = paletteA.Bright;
            dish.ingredient1DarkColor = paletteA.Dark;
            dish.ingredient1DarkestColor = paletteA.Darkest;
            dish.ingredient2BrightestColor = paletteB.Brightest;
            dish.ingredient2BrightColor = paletteB.Bright;
            dish.ingredient2DarkColor = paletteB.Dark;
            dish.ingredient2DarkestColor = paletteB.Darkest;

            // Which of the two tier tags a dish carries follows its rarity, because that is the
            // question the tags exist to answer: Uncommon and below on one, Rare and above on the
            // other. Read off the object that has already been configured rather than asked for
            // twice, so the tag can never disagree with the colour of the item's name.
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            bool better = objectAuthoring != null && objectAuthoring.rarity >= Rarity.Rare;
            SetCategoryTag(root, ObjectCategoryTag.RareOrHigherCookedFood, better);
            SetCategoryTag(root, ObjectCategoryTag.UncommonOrLowerCookedFood, !better);
        }

        /// <summary>
        /// A dish or ingredient the item points at, as an ObjectID when that is knowable offline.
        /// </summary>
        /// <remarks>
        /// Three outcomes, and only one of them is a mistake. A vanilla name resolves to its number.
        /// One of this mod's own names cannot resolve here at all — the mod's numbers are handed out
        /// while the game loads — so it comes back as None and the runtime fills it in; saying
        /// anything about that would train creators to ignore the warning that matters. A name that
        /// is neither is a typo, and that one is worth saying out loud.
        /// </remarks>
        private static ObjectID ResolveFoodTarget(
            string name,
            string verb,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(name))
            {
                return ObjectID.None;
            }

            ObjectID resolved = resolveObject == null ? ObjectID.None : resolveObject(name);
            if (resolved != ObjectID.None)
            {
                return resolved;
            }

            if (isOneOfOurOwn != null && isOneOfOurOwn(name))
            {
                return ObjectID.None;
            }

            if (report != null)
            {
                report(
                    verb + " '" + name + "', which is neither one of this mod's own foods nor one " +
                    "the game has. Check the spelling, or add a dish with that id.");
            }

            return ObjectID.None;
        }

        /// <summary>
        /// Writes the raw and cooked halves of what an ingredient gives when it is eaten.
        /// </summary>
        /// <remarks>
        /// Appended to whatever the item's own eaten effects already put there, so an ingredient
        /// that is also an ordinary consumable keeps both. See the ordering note on
        /// <see cref="ApplyCooking"/> — appending is only safe because the effects pass has already
        /// rewritten the list from scratch this run.
        /// </remarks>
        private static void ApplyRawAndCookedConditions(
            GameObject root,
            DimensionCookingTemplate cooking,
            System.Action<string> reportUnknownCondition)
        {
            DimensionItemEffect[] raw = cooking.GivesRaw;
            DimensionItemEffect[] cooked = cooking.GivesCooked;
            int count = System.Math.Max(raw.Length, cooked.Length);
            if (count == 0)
            {
                return;
            }

            GivesConditionsWhenConsumedAuthoring eaten =
                EnsureComponent<GivesConditionsWhenConsumedAuthoring>(root);
            if (eaten.Values == null)
            {
                eaten.Values = new System.Collections.Generic.List<ConditionDataContainer>();
            }

            for (int i = 0; i < count; i++)
            {
                // A pair with only one half filled in means "the same either way", which is how the
                // game's own plain ingredients are authored.
                DimensionItemEffect rawHalf = i < raw.Length ? raw[i] : cooked[i];
                DimensionItemEffect cookedHalf = i < cooked.Length ? cooked[i] : raw[i];

                ConditionData rawData;
                ConditionData cookedData;
                if (!TryBuildConditionData(rawHalf, reportUnknownCondition, out rawData) ||
                    !TryBuildConditionData(cookedHalf, reportUnknownCondition, out cookedData))
                {
                    continue;
                }

                eaten.Values.Add(new ConditionDataContainer
                {
                    conditionData = rawData,
                    conditionDataWhenCooked = cookedData
                });
            }
        }

        private static bool TryBuildConditionData(
            DimensionItemEffect effect,
            System.Action<string> reportUnknownCondition,
            out ConditionData data)
        {
            data = default(ConditionData);
            ConditionID id;
            if (effect == null || !TryResolveCondition(effect.EffectId, out id))
            {
                if (effect != null && reportUnknownCondition != null)
                {
                    reportUnknownCondition(effect.EffectId);
                }

                return false;
            }

            data = new ConditionData
            {
                conditionID = id,
                duration = effect.Seconds,
                value = effect.Value,
                valueMultiplier = effect.ValueMultiplier
            };
            return true;
        }

        /// <summary>
        /// Adds or removes one of the game's category tags without disturbing the others.
        /// </summary>
        /// <remarks>
        /// The tag list is what the game's slot rules, filters and creature diets all read, and an
        /// object may legitimately carry several. Rewriting the list would silently drop whatever
        /// another pass had already put there, so this only ever touches the one tag it is asked
        /// about — and it removes as well as adds, so an item that stops being food stops being
        /// accepted by the pot.
        /// </remarks>
        public static void SetCategoryTag(GameObject root, ObjectCategoryTag tag, bool wanted)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring == null)
            {
                return;
            }

            if (objectAuthoring.tags == null)
            {
                objectAuthoring.tags = new System.Collections.Generic.List<ObjectCategoryTag>();
            }

            bool present = objectAuthoring.tags.Contains(tag);
            if (wanted && !present)
            {
                objectAuthoring.tags.Add(tag);
            }
            else if (!wanted && present)
            {
                objectAuthoring.tags.Remove(tag);
            }
        }

        /// <summary>
        /// What it leaves standing when it dies.
        /// </summary>
        /// <remarks>
        /// Not loot — a whole object left in the world. Removed when nothing is named, so a creature
        /// that stops leaving a cocoon does not keep leaving one.
        /// </remarks>
        public static void ApplyLeavesBehind(
            GameObject root,
            DimensionLeavesBehindTemplate leaves,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (leaves == null || !leaves.LeavesAnything)
            {
                RemoveComponentIfPresent<SpawnOnDeathAuthoring>(root);
                return;
            }

            ObjectID spawned = resolveObject == null
                ? ObjectID.None
                : resolveObject(leaves.ObjectId);
            if (spawned == ObjectID.None)
            {
                if (report != null)
                {
                    report(
                        "leaves '" + leaves.ObjectId + "' behind when it dies, but that is not an " +
                        "object the game has, so nothing will be left there.");
                }

                RemoveComponentIfPresent<SpawnOnDeathAuthoring>(root);
                return;
            }

            if (leaves.CanGrowWithoutLimit && report != null)
            {
                report(
                    "leaves up to " + leaves.MaximumAmount + " of '" + leaves.ObjectId +
                    "' behind with no limit on how many may gather nearby. If that object leaves " +
                    "the same thing behind, this is an unbounded chain.");
            }

            SpawnOnDeathAuthoring spawn = EnsureComponent<SpawnOnDeathAuthoring>(root);
            spawn.objectToSpawn = spawned;
            spawn.objectVariation = leaves.Variation;
            spawn.spawnChance = leaves.Chance;
            spawn.offset = new Unity.Mathematics.float3(
                leaves.Offset.x,
                leaves.Offset.y,
                leaves.Offset.z);
            spawn.amount = new Pug.UnityExtensions.RangeInt
            {
                min = leaves.MinimumAmount,
                max = leaves.MaximumAmount
            };
            spawn.maxAmountAllowedWithinRadius = leaves.CrowdLimit;
            spawn.maxAmountCheckRadius = leaves.CrowdRadius;
            spawn.dontSpawnIfKilledByDestroyTimer = leaves.OnlyWhenActuallyKilled;
        }

        /// <summary>
        /// The things something simply shrugs off.
        /// </summary>
        /// <remarks>
        /// Three bare markers with no fields between them, which is why they are one call rather than
        /// three. Each is the absence of an ordinary reaction: nothing pushes it, arrows do not reach
        /// it, and creative mode does not skip its drops.
        /// </remarks>
        public static void ApplyImmunities(
            GameObject root,
            bool immuneToPushBack,
            bool immuneToRangedDamage,
            bool alwaysDropsLoot)
        {
            if (immuneToPushBack)
            {
                EnsureComponent<ImmuneToPushBackAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<ImmuneToPushBackAuthoring>(root);
            }

            if (immuneToRangedDamage)
            {
                EnsureComponent<ImmuneToRangeDamageAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<ImmuneToRangeDamageAuthoring>(root);
            }

            if (alwaysDropsLoot)
            {
                EnsureComponent<ImmuneToSkipLootDropAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<ImmuneToSkipLootDropAuthoring>(root);
            }
        }


        /// <summary>
        /// Makes an item swing, fire or cast.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Core Keeper keeps the three kinds in three separate components and an item carries exactly
        /// one, so the other two are removed rather than left behind. That matters more than it
        /// sounds: an item that was a sword and became a bow would otherwise carry both, and the
        /// game would swing it as well as fire it.
        /// </para>
        /// <para>
        /// The skill multiplier rides along regardless of kind, because it is on every weapon in the
        /// game and is what decides how fast using the thing raises the matching skill.
        /// </para>
        /// </remarks>
        public static void ApplyWeapon(
            GameObject root,
            DimensionWeaponTemplate weapon,
            System.Func<string, ObjectID> resolveProjectile,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (weapon == null || !weapon.IsAWeapon)
            {
                RemoveComponentIfPresent<MeleeWeaponAuthoring>(root);
                RemoveComponentIfPresent<RangeWeaponAuthoring>(root);
                RemoveComponentIfPresent<CastItemAuthoring>(root);
                RemoveComponentIfPresent<WeaponSkillGainedMultiplierAuthoring>(root);
                return;
            }

            if (!weapon.IsMelee)
            {
                RemoveComponentIfPresent<MeleeWeaponAuthoring>(root);
            }

            if (!weapon.IsRanged)
            {
                RemoveComponentIfPresent<RangeWeaponAuthoring>(root);
            }

            if (!weapon.IsCast)
            {
                RemoveComponentIfPresent<CastItemAuthoring>(root);
            }

            EnsureComponent<WeaponSkillGainedMultiplierAuthoring>(root).skillMultiplier =
                weapon.SkillGainMultiplier;

            // A beam is its own weapon class, not a flavour of melee, ranged or cast — so it is
            // applied before the kind branches rather than inside one of them.
            ApplyBeamWeapon(root, weapon.Beam, resolveProjectile, report, isDeferred);

            if (weapon.IsMelee)
            {
                MeleeWeaponAuthoring melee = EnsureComponent<MeleeWeaponAuthoring>(root);
                melee.baseHitColliderSize = weapon.Reach;
                melee.extraHitColliderReachSize = weapon.ExtraReach;
                melee.arcAngle = (ArcAngle)(int)weapon.SwingArc;
                melee.attackFXType = (AttackFXType)(int)weapon.Flourish;
                melee.lungeForce = weapon.Lunge;
                melee.quickHit = weapon.QuickHit;
                melee.isBigSpearWeapon = weapon.ThrustsLikeASpear;
                melee.isBigSwingWeapon = weapon.SwingsLikeATwoHander;
                melee.skipAnticipationAnimation = weapon.SkipsTheWindUpAnimation;
                melee.tileDamageAOE = weapon.BreaksTerrainAcrossTheWholeArc;
                melee.overrideAnimation = weapon.MeleeOverrideAnimation;
                melee.disable = weapon.MeleeDisabled;
                return;
            }

            if (weapon.IsRanged)
            {
                RangeWeaponAuthoring ranged = EnsureComponent<RangeWeaponAuthoring>(root);
                ObjectID projectile = resolveProjectile == null
                    ? ObjectID.None
                    : resolveProjectile(weapon.FiresProjectileId);

                if (projectile == ObjectID.None &&
                    !string.IsNullOrEmpty(weapon.FiresProjectileId) &&
                    !(isDeferred != null && isDeferred(weapon.FiresProjectileId)) &&
                    report != null)
                {
                    report(
                        "fires '" + weapon.FiresProjectileId + "', which is neither a projectile in " +
                        "this mod nor one the game has, so nothing will come out of it.");
                }

                ranged.projectileID = projectile;
                ranged.extraProjectiles = weapon.ExtraShots;
                ranged.spreadAngle = weapon.SpreadAngle;
                ranged.spawnOffsetDistance = weapon.MuzzleDistance;
                ranged.rotateFreely = weapon.AimsFreely;
                ranged.recoilForce = weapon.Recoil;

                ranged.spawnRandomProjectile = weapon.FiresARandomProjectile;
                ranged.randomProjectiles = new System.Collections.Generic.List<ObjectID>();
                string[] randomIds = weapon.RandomProjectileIds;
                for (int i = 0; i < randomIds.Length; i++)
                {
                    ObjectID one = resolveProjectile == null
                        ? ObjectID.None
                        : resolveProjectile(randomIds[i]);
                    if (one != ObjectID.None)
                    {
                        ranged.randomProjectiles.Add(one);
                    }
                    else if (isDeferred != null && isDeferred(randomIds[i]))
                    {
                        // A PLACEHOLDER AT THE RIGHT POSITION, not a dropped entry. The list becomes
                        // RangeWeaponCD.randomProjectiles, a FixedList64Bytes with no room to grow at
                        // load — and dropping this entry would slide every later shot one place left,
                        // so the runtime write would land on the wrong one.
                        ranged.randomProjectiles.Add(ObjectID.None);
                    }
                    else if (report != null)
                    {
                        report(
                            "picks from '" + randomIds[i] + "', which is neither a projectile in " +
                            "this mod nor one the game has, so that one is left out of the list.");
                    }
                }

                ObjectID second =
                    string.IsNullOrEmpty(weapon.SecondProjectileId) || resolveProjectile == null
                        ? ObjectID.None
                        : resolveProjectile(weapon.SecondProjectileId);
                bool secondIsOneOfOurs =
                    second == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(weapon.SecondProjectileId);

                ranged.secondaryProjectileVariationID = second;

                if (second == ObjectID.None && !secondIsOneOfOurs &&
                    !string.IsNullOrEmpty(weapon.SecondProjectileId) && report != null)
                {
                    // This branch said nothing at all before, so a misspelled wound-up shot
                    // generated clean and the full charge fired the ordinary shot instead.
                    report(
                        "fires '" + weapon.SecondProjectileId + "' once its wind-up is full, which " +
                        "is neither a projectile in this mod nor one the game has, so a full " +
                        "wind-up fires its ordinary shot instead.");
                }

                ranged.pierceAtMaxWindup = weapon.PiercesAtFullWindUp;
                ranged.bounceAtMaxWindup = weapon.BouncesAtFullWindUp;
                ranged.overrideAnimation = weapon.OverrideAnimation;

                // THE SIZE WAITS FOR THE SHOT — AND GOES NOWHERE WITHOUT ONE. RangeWeaponConverter
                // logs a red error naming the creator's prefab whenever an explosion size is set
                // and the explosive shot is None (RangeWeaponConverter.cs:14-17). None is exactly
                // what a deferred shot has to bake as, so for one of ours the size is held back
                // here and the bootstrap row carries the authored number, which the link hydration
                // writes back alongside the shot's id on the first ticks.
                //
                // It is also None when the wound-up shot names NOTHING AT ALL, and that case used
                // to ship the size anyway — so a blast size left on a weapon with no wound-up shot
                // put a red error in the player's log about a prefab the author had done nothing
                // wrong to. A size with no shot to carry it does nothing either way; it is dropped
                // and said, rather than kept and logged by the game.
                bool secondNamesNothing = second == ObjectID.None && !secondIsOneOfOurs;
                ranged.explosionSize = secondIsOneOfOurs || secondNamesNothing
                    ? 0
                    : weapon.ExplosionSize;

                // SAID FOR BOTH HALVES OF "names nothing". The condition used to also require the
                // field to be EMPTY, so the misspelling half — a wound-up shot naming something the
                // game does not have — lost its blast number without a word, while the message
                // above talked only about the shot. Both roads end with the size at zero, so both
                // have to say the size went.
                if (secondNamesNothing && weapon.ExplosionSize > 0f && report != null)
                {
                    report(string.IsNullOrEmpty(weapon.SecondProjectileId)
                        ? "has a blast size on its wound-up shot without naming a shot for the " +
                          "wind-up to fire, so there is nothing for the blast to come off. Name " +
                          "the shot, or set the blast size back to zero."
                        : "has a blast size on its wound-up shot, but '" +
                          weapon.SecondProjectileId + "' is not a shot this world has, so the " +
                          "blast size is dropped along with it.");
                }
                ranged.explosionUseWeaponDamage = weapon.ExplosionUsesWeaponDamage;

                ranged.mortarRaycastToTarget = weapon.LobsOntoTheAimPoint;
                ranged.mortarTargetRange = weapon.LobRange;
                ranged.minMaxRandomSpreadDistance = new Unity.Mathematics.float2(
                    weapon.LobScatterNearAndFar.x,
                    weapon.LobScatterNearAndFar.y);
                ranged.secondaryMinMaxRandomSpreadDistance = new Unity.Mathematics.float2(
                    weapon.SecondLobScatterNearAndFar.x,
                    weapon.SecondLobScatterNearAndFar.y);
                ranged.scaleMortarAirTimeWithDistance = weapon.LobHangsLongerWhenFurther;
                ranged.secondaryScaleMortarAirTimeWithDistance =
                    weapon.SecondLobHangsLongerWhenFurther;
                ranged.minMortarAirTimePercentage = weapon.ShortestLobHangShare;
                ranged.secondaryDistanceBetweenHits = weapon.SecondProjectileDistanceBetweenHits;

                if (report != null)
                {
                    if (weapon.RandomProjectilesWillBeIgnored)
                    {
                        report(
                            "lists projectiles to pick from without switching random firing on, so " +
                            "it only ever fires its single projectile.");
                    }

                    if (weapon.RandomProjectilesAreEmpty)
                    {
                        report(
                            "is set to fire a random projectile with nothing in its list to pick " +
                            "from, so nothing will come out of it.");
                    }
                }

                return;
            }

            CastItemAuthoring cast = EnsureComponent<CastItemAuthoring>(root);
            cast.castTime = weapon.CastSeconds;
            cast.useType = (CastItemUseType)(int)weapon.CastPurpose;
            cast.allowHoldToRepeat = weapon.HoldingRepeatsIt;

            AchievementID castAchievement;
            if (!string.IsNullOrEmpty(weapon.CastAchievementId) &&
                System.Enum.TryParse(weapon.CastAchievementId, false, out castAchievement))
            {
                cast.achievement = castAchievement;
            }

            EffectID castEffect;
            if (!string.IsNullOrEmpty(weapon.CastCompleteEffectId) &&
                System.Enum.TryParse(weapon.CastCompleteEffectId, false, out castEffect))
            {
                cast.castCompleteEffect = castEffect;
            }
        }

        /// <summary>
        /// What swinging or firing this sounds like.
        /// </summary>
        /// <remarks>
        /// Removed when nothing is chosen rather than written as five zeroes, so a weapon stripped
        /// of its sounds falls back to the game's own rather than to silence.
        /// </remarks>
        /// <summary>
        /// Writes how a creature closes on its target onto an existing <c>ChaseStateAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT add the component. Chasing is only meaningful on something that
        /// already chases, and the creature generator decides that from the movement questions. A
        /// spine method that added it would give every stationary object a pursuit it never runs.
        /// </remarks>
        public static void ApplyPursuit(GameObject root, DimensionPursuitTemplate pursuit)
        {
            if (root == null || pursuit == null)
            {
                return;
            }

            ChaseStateAuthoring chase = root.GetComponent<ChaseStateAuthoring>();
            if (chase == null)
            {
                return;
            }

            chase.minDistanceToKeep = pursuit.KeepsAtLeastThisFarAway;
            chase.maxDistanceToKeep = pursuit.AndAtMostThisFarAway;
            chase.distanceToStartSideStepping = pursuit.StartsSideSteppingWithin;
            chase.distanceToKeepNoiseDisabled = pursuit.KeepsQuietAboutItsDistance;

            chase.neverStopChasing = pursuit.NeverGivesUp;
            chase.skipVisibilityCheck = pursuit.ChasesWhatItCannotSee;
            chase.ignoreLowColliders = pursuit.LowObstaclesDoNotStopIt;

            chase.needPathToChase = pursuit.NeedsAPathToChase;
            chase.preferPathFind = pursuit.PrefersPathfinding;
            chase.obstacleAvoidDistance = pursuit.LooksAheadToAvoidObstacles;

            chase.preChaseDuration = pursuit.PauseBeforeChasing;
            chase.endChaseDuration = pursuit.KeepsGoingAfterLosingIt;
            chase.idleDuration = pursuit.IdlesMidChaseFor;
            chase.idleCooldown = pursuit.BetweenMidChaseIdles;

            chase.disabled = pursuit.StartsSwitchedOff;

            // belongsToShape is the physics shape the pathfinding entity is built against
            // (PathFindingConversion.CreatePathfindingEntity reads it). There is exactly one
            // sensible value — the creature's own shape — so it is wired rather than asked about.
            // Left null when the creature has no shape, which is what the game already handles.
            chase.belongsToShape = root.GetComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();

            // chaseHeldObjects is left alone on purpose: zero vanilla prefabs set it, so there is
            // nothing to copy and no way to know what the game does with a value nobody authored.
            // belongsToShape is a scene reference the creature does not have, and disabled is what
            // the generator's own "does it chase at all" question already decides.
            if (chase.chaseHeldObjects == null)
            {
                chase.chaseHeldObjects = new System.Collections.Generic.List<ObjectID>();
            }
        }

        /// <summary>
        /// Writes the shape, timing and force of a melee swing onto an existing
        /// <c>MeleeAttackStateAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Like <see cref="ApplyPursuit"/> this never adds the component — the attack kind decides
        /// whether there is a swing at all.
        /// </para>
        /// <para>
        /// THE MULTIPLIERS ARE THE POINT. <c>MeleeAttackStateConverter</c> discards the authored
        /// <c>meleeDamage</c> entirely when the object carries a tier and recomputes it as
        /// <c>LevelToDamage(level, meleeDamageMultiplier)</c>. Before this the framework wrote the
        /// flat number and never the multiplier, so a tiered creature's damage was whatever the
        /// curve said and could not be shifted at all.
        /// </para>
        /// </remarks>
        public static void ApplyMeleeShape(
            GameObject root,
            DimensionMeleeShapeTemplate shape,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || shape == null)
            {
                return;
            }

            MeleeAttackStateAuthoring melee = root.GetComponent<MeleeAttackStateAuthoring>();
            if (melee == null)
            {
                return;
            }

            melee.meleeDamageMultiplier = shape.HitsThisHardForItsTier;
            melee.tileDamageMultiplier = shape.BreaksTerrainThisHardForItsTier;

            melee.durationBeforeDamageDeal = shape.DamageLandsAfter;
            melee.moveForceForward = shape.LungeForce;
            melee.alwaysMoveAtFullForceForward = shape.AlwaysLungesAtFullForce;

            melee.lockOrientationDuringHit = shape.CannotTurnDuringTheHit;
            melee.lockOrientationDuringAnticipation = shape.CannotTurnDuringTheWindUp;
            melee.hitInDiscreteDirections = shape.OnlySwingsInFourDirections;

            melee.skipVisibilityCheck = shape.SwingsAtWhatItCannotSee;
            melee.canOnlyAttackEnemiesAndPlayer = shape.OnlyHitsEnemiesAndPlayers;
            melee.attackPlayerTimeout = shape.GivesUpOnAPlayerAfter;
            melee.canHitLowTriggers = shape.HitsLowObstacles;
            melee.bypassMaxDamagePerHit = shape.IgnoresTheDamageCap;

            melee.hitBoxHalfLength = shape.HitboxHalfLength;
            melee.hitBoxHalfWidth = shape.HitboxHalfWidth;
            melee.hitOffset = new Unity.Mathematics.float3(
                shape.HitboxOffset.x,
                shape.HitboxOffset.y,
                shape.HitboxOffset.z);

            melee.objectToSpawnOnHitTiles = string.IsNullOrEmpty(shape.SpawnsOnBrokenTilesId)
                ? ObjectID.None
                : (resolveObject == null ? ObjectID.None : resolveObject(shape.SpawnsOnBrokenTilesId));

            if (!string.IsNullOrEmpty(shape.SpawnsOnBrokenTilesId) &&
                melee.objectToSpawnOnHitTiles == ObjectID.None &&
                report != null)
            {
                report(
                    "leaves '" + shape.SpawnsOnBrokenTilesId + "' where its swing breaks terrain, " +
                    "which the game does not have. Nothing will be left behind.");
            }

            if (shape.HitboxIsHalfSpecified && report != null)
            {
                report(
                    "overrides only one half of its melee hitbox. The two are read together, so a " +
                    "swing with reach and no width — or width and no reach — connects with nothing. " +
                    "Set both, or leave both at zero and let the game work it out.");
            }
        }

        /// <summary>
        /// Writes the shot pattern onto an existing <c>RangeAttackStateAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// Never adds the component — the attack kind decides whether the creature shoots at all.
        /// </remarks>
        /// <summary>
        /// Writes the detail of a charge onto an existing <c>ChargeAttackStateAuthoring</c>.
        /// </summary>
        public static void ApplyChargeShape(
            GameObject root,
            DimensionChargeShapeTemplate shape,
            System.Action<string> report)
        {
            if (root == null || shape == null)
            {
                return;
            }

            ChargeAttackStateAuthoring charge = root.GetComponent<ChargeAttackStateAuthoring>();
            if (charge == null)
            {
                return;
            }

            charge.damageMultiplier = shape.HitsThisHardForItsTier;
            charge.tileDamageMultiplier = shape.BreaksTerrainThisHardForItsTier;
            charge.hitTiles = shape.PloughsThroughTerrain;

            charge.endChargeWithAttack = shape.EndsWithASwing;
            charge.alwaysEndChargeWithAttack = shape.SwingsEvenIfItHitNothing;
            charge.endChargeDistanceToAttemptAttack = shape.SwingsIfWithin;
            charge.endChargeAttackDuration = shape.EndingSwingDuration;
            charge.endChargeMoveForceForward = shape.EndingSwingLunge;
            charge.chargeAttackAnticipationDuration = shape.EndingSwingWindUp;
            charge.endOfChargeAttackHitTiles = shape.EndingSwingBreaksTerrain;

            charge.collideDuration = shape.TimeStuckOnImpact;
            charge.reversePushback = shape.BouncesBackThisHard;
            charge.dontCollideWithObjects = shape.PassesThroughScenery;
            charge.ignoreLowColliders = shape.LowObstaclesDoNotStopIt;
            charge.triggerAnimationIfNotCollided = shape.PlaysImpactEvenOnAMiss;

            charge.steerTowardsTargetDuringCharge = shape.CanSteerMidCharge;
            charge.steerTowardsTargetMinDistance = shape.StopsSteeringWithin;
            charge.steerTowardsTargetMaxAngleDeg = shape.WidestSteerDegrees;
            charge.lockOrientationAtMultiplier = shape.FacingLocksAt;

            charge.pushback = shape.PushesWhatItHits;
            charge.tileDamage = shape.FlatTerrainDamage;

            charge.steerTowardsTargetChargeAttackRotateToTargetData =
                new ChargeAttackRotateToTargetData
                {
                    chargeAttackRotateToTargetType =
                        (ChargeAttackRotateToTargetType)(int)shape.SteerTurn,
                    degreesPerSecond = shape.SteerDegreesPerSecond
                };

            charge.lockOrientationChargeAttackRotateToTargetData =
                new ChargeAttackRotateToTargetData
                {
                    chargeAttackRotateToTargetType =
                        (ChargeAttackRotateToTargetType)(int)shape.LockTurn,
                    degreesPerSecond = shape.LockDegreesPerSecond
                };

            charge.vulnerabilityDuration = shape.VulnerableFor;
            charge.endDuration = shape.RecoveryAfterwards;

            charge.hitBoxHalfLength = shape.HitboxHalfLength;
            charge.hitBoxHalfWidth = shape.HitboxHalfWidth;
            charge.hitDistanceInfront = shape.HitReach;
            charge.hitRadius = shape.HitRadius;
            charge.hitInDiscreteDirections = shape.OnlyChargesInFourDirections;
            charge.hitOffset = new Unity.Mathematics.float3(
                shape.HitboxOffset.x,
                shape.HitboxOffset.y,
                shape.HitboxOffset.z);

            if (report == null)
            {
                return;
            }

            if (shape.EndingSwingSettingsWillBeIgnored)
            {
                report(
                    "sets up an attack at the end of its charge but never turns that attack on, so " +
                    "it just stops when it arrives and none of those numbers are read.");
            }

            if (shape.SteeringSettingsWillBeIgnored)
            {
                report(
                    "changes how its charge steers without letting it steer at all, so the charge " +
                    "still runs in a straight line.");
            }
        }

        /// <summary>
        /// Writes a mortar barrage onto an existing <c>ShootMortarProjectileStateAuthoring</c>.
        /// </summary>
        /// <summary>
        /// Writes where a placed object may go onto an existing <c>PlaceableObjectAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// Never adds the component. Whether a thing is placeable at all is decided by what kind of
        /// thing it is, upstream of this — a creature is not made placeable by filling in a
        /// placement panel.
        /// </remarks>
        /// <summary>
        /// Writes the three non-death loot channels onto an existing <c>DropLootAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// The "has" flags are derived from whether the author filled each section in, never asked
        /// about separately. The game reads the flag rather than the data, so a section filled in
        /// with the flag off drops nothing and says nothing — a failure worth making unauthorable.
        /// </remarks>
        /// <summary>
        /// Makes an object react to a melody played near it — Core Keeper's ocarina system.
        /// </summary>
        /// <remarks>
        /// Adds the component only when a melody was actually named. An object listening for
        /// nothing is a component the game polls forever to no effect.
        /// </remarks>
        /// <summary>
        /// Makes an object something a machine can pull resources out of indefinitely.
        /// </summary>
        /// <summary>
        /// Makes an object react to what comes near it — the game's whole lock-and-key vocabulary.
        /// </summary>
        /// <summary>
        /// The small roles an object plays in a base — sitting, naming, painting, resizing,
        /// being discovered, and being un-hittable.
        /// </summary>
        /// <remarks>
        /// Several of these components hold no settings at all: their presence IS the setting, so
        /// they are toggled rather than filled in.
        /// </remarks>
        /// <summary>
        /// The habits that make a creature an inhabitant rather than a monster — loitering,
        /// taunting, guarding a nest, being kept, getting full.
        /// </summary>
        /// <summary>
        /// Makes something a trader, money, a soul, or a seasonal object.
        /// </summary>
        /// <summary>
        /// Makes something a nest that keeps producing creatures or objects around itself.
        /// </summary>
        /// <summary>
        /// Makes an object pick its look from the ground around it — bridges meeting shores, rails
        /// knowing they are a corner.
        /// </summary>
        /// <summary>
        /// Machines that work on their own — drills, automated miners, belt filters — and the
        /// workshop roles that go with them.
        /// </summary>
        /// <summary>
        /// Makes a creature a body that follows its head — a worm, a serpent, a caterpillar.
        /// </summary>
        /// <summary>
        /// The remaining world roles — shrines, summoning items, fireflies, graves, spreading fire,
        /// barriers, minions and free-moving weapons.
        /// </summary>
        /// <remarks>
        /// Nine of the components behind this hold no settings at all, so they are toggled. The rest
        /// carry one or two values each.
        /// </remarks>
        /// <summary>
        /// Makes a weapon fire a held beam rather than a projectile.
        /// </summary>
        /// <summary>
        /// Makes a creature attack the world in its way — walls, buildings, scenery.
        /// </summary>
        /// <remarks>
        /// Separate from the melee swing on purpose: hitting a player and hitting a wall are two
        /// states with two damage numbers, which is how a thing that chews through a base to reach
        /// you is built.
        /// </remarks>
        /// <summary>
        /// Gives a custom boss a kit borrowed from one of the game's own.
        /// </summary>
        /// <remarks>
        /// The borrow door. A boss given the Hydra's kit burrows, surfaces and fires the same six
        /// attacks; leave every number alone and it fights exactly like a Hydra with a different
        /// sprite, or change one attack and only that attack changes.
        /// </remarks>
        /// <summary>
        /// Gives a custom boss the Core's, the Wall's or the Scarab's fight.
        /// </summary>
        /// <summary>
        /// Gives a custom boss the Bird's, Robot's, Octopus's, Larva's, Shaman's or Snake's fight.
        /// </summary>
        /// <summary>
        /// More ways a creature fights: a sweeping ray, contact damage, a shield, placing objects,
        /// and orbiting its owner.
        /// </summary>
        /// <summary>
        /// A chain of explosions, a trail, timed look changes, and the odds and ends.
        /// </summary>
        /// <summary>
        /// Plain spawners, drifting orbs, followers, statues, terrain-chewing roamers and gravity
        /// wells.
        /// </summary>
        /// <summary>
        /// Mana pools and siphons, healing auras, ancient wiring, ownership, boss hooks, and blast
        /// ground.
        /// </summary>
        /// <summary>
        /// Bush-hiding, eggs, caveling territories, delayed shots, proximity triggers, animation
        /// speed and the extra inventory slots.
        /// </summary>
        /// <summary>
        /// A creature's own beam attack, alerts, dripping items, catching fire, ambient swimming
        /// and scuttling, note mimicry, and corner smoothing.
        /// </summary>
        /// <summary>
        /// The last small behaviours — seats, reacting to wounds, pheromones, boss beams and spawn
        /// points, hive eggs.
        /// </summary>
        /// <summary>
        /// The game's own way of putting a one-off somewhere when a world is made.
        /// </summary>
        /// <remarks>
        /// Two of the answers are asked twice because a world is either classic or full release and
        /// the two generate differently. Creative worlds read the classic answer.
        /// </remarks>
        public static void ApplyNativeWorldPlacement(
            GameObject root,
            DimensionNativeWorldPlacementTemplate placement,
            System.Action<string> report)
        {
            if (root == null || placement == null)
            {
                return;
            }

            if (!placement.PlacesSomethingWhenAWorldIsMade || placement.NothingToPlace)
            {
                RemoveComponentIfPresent<PugWorldGen.PugWorldGenAuthoring>(root);

                if (report != null && placement.NothingToPlace)
                {
                    report(
                        "is a world-generation marker with nothing named to place, so generation " +
                        "would run it and put nothing in the world.");
                }

                return;
            }

            PugWorldGen.PugWorldGenAuthoring worldGen =
                EnsureComponent<PugWorldGen.PugWorldGenAuthoring>(root);

            worldGen.prefab = placement.TheThingToPlace;
            worldGen.markerPrefab = placement.TheMarkerThatPlacesIt;
            worldGen.spawnImmediatelyOnLoad = placement.AppearsAsSoonAsTheWorldLoads;
            worldGen.destroyMarkerAfterSpawn = placement.ClearsTheMarkerAwayAfterwards;
            worldGen.sortIndex = placement.Order;

            worldGen.placementType = (UniqueScenePlacementType)(int)placement.HowTheSpotIsChosen;
            worldGen.positionSampling =
                (UniqueScenePositionSampling)(int)placement.WhereInsideTheBiome;
            worldGen.allowOverlappingSpawnCellBorders = placement.MayStraddleSpawnCellBorders;

            worldGen.biome = new WorldGenerationTypeDependentValue<Biome>
            {
                classic = ResolveBiomeName(
                    placement.BiomeInAClassicWorld,
                    worldGen.biome.classic,
                    "classic",
                    report),
                fullRelease = ResolveBiomeName(
                    placement.BiomeInAFullReleaseWorld,
                    worldGen.biome.fullRelease,
                    "full-release",
                    report)
            };

            worldGen.targetDistanceFromCore = new WorldGenerationTypeDependentValue<int>
            {
                classic = placement.DistanceFromTheCoreClassic,
                fullRelease = placement.DistanceFromTheCoreFullRelease
            };

            worldGen.spawnPosition = new WorldGenerationTypeDependentValue<Unity.Mathematics.int2>
            {
                classic = new Unity.Mathematics.int2(
                    placement.ExactSpotClassic.x,
                    placement.ExactSpotClassic.y),
                fullRelease = new Unity.Mathematics.int2(
                    placement.ExactSpotFullRelease.x,
                    placement.ExactSpotFullRelease.y)
            };

            worldGen.contentBundle = placement.PartOfContentBundle != null
                ? placement.PartOfContentBundle
                : default(DataBlockRef<ContentBundleDataBlock>);

            worldGen.replacedByBundle = placement.ReplacedByBundle != null
                ? new Pug.UnityExtensions.OptionalValue<DataBlockRef<ContentBundleDataBlock>>(
                    placement.ReplacedByBundle)
                : default(Pug.UnityExtensions.OptionalValue<DataBlockRef<ContentBundleDataBlock>>);

            if (report == null)
            {
                return;
            }

            if (placement.ExactSpotIsOnTopOfTheCore)
            {
                report(
                    "goes on one exact tile and that tile is the origin, which is where the Core " +
                    "sits, so it would be placed on top of the Core.");
            }

            if (placement.ReplacedByItsOwnBundle)
            {
                report(
                    "is replaced by the same content bundle it belongs to, so it replaces itself " +
                    "and never appears.");
            }
        }

        /// <summary>Turns a biome the author typed into the game's own, leaving it alone if blank.</summary>
        private static Biome ResolveBiomeName(
            string name,
            Biome fallback,
            string whichWorld,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(name))
            {
                return fallback;
            }

            Biome biome;
            if (System.Enum.TryParse(name, false, out biome))
            {
                return biome;
            }

            if (report != null)
            {
                report(
                    "is placed in '" + name + "' in a " + whichWorld + " world, which is not a " +
                    "biome the game has, so that world places it wherever it would have anyway.");
            }

            return fallback;
        }

        /// <summary>
        /// Turns a condition an author typed into the number the game will know it by.
        /// </summary>
        /// <remarks>
        /// <para>
        /// TWO KINDS OF ANSWER ARE VALID HERE. One of Core Keeper's own 357 conditions, named as the
        /// game names it, and one of this mod's own, named as its asset names it. An author should
        /// not have to know which kind they are looking at, so both are tried and the game's own
        /// wins a tie — a mod cannot shadow a vanilla condition by naming one after it.
        /// </para>
        /// <para>
        /// The custom half only answers once the mod's condition assets have been claimed, which is
        /// what <c>DimensionConditionScope</c> does around a generate. Outside that, a custom name
        /// resolves to nothing and is reported like any other name the game does not have.
        /// </para>
        /// </remarks>
        public static bool TryResolveCondition(string name, out ConditionID id)
        {
            id = ConditionID.None;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (System.Enum.TryParse(name, false, out id) && id != ConditionID.None)
            {
                return true;
            }

            ConditionID custom = ExpandNullforge.Conditions.DimensionConditionRegistry.IdFor(name);
            if (custom != ConditionID.None)
            {
                id = custom;
                return true;
            }

            id = ConditionID.None;
            return false;
        }


        /// <summary>
        /// The thirty things an object simply is, or simply does. Every one of them is a component
        /// with no fields, so the whole method is on-or-off.
        /// </summary>
        public static void ApplySimpleTraits(
            GameObject root,
            DimensionSimpleTraitsTemplate traits,
            System.Action<string> report)
        {
            if (root == null || traits == null)
            {
                return;
            }

            // ---- being hit ----
            Toggle<AttackableWithMeleeAuthoring>(root, traits.CanBeHitWithAWeaponNotJustATool);
            Toggle<DontCountAsHitForAttackerAuthoring>(root, traits.HittingItDoesNotCountAsAHit);
            Toggle<IgnoreImmuneZoneAuthoring>(root, traits.ImmuneGroundDoesNotProtectIt);
            Toggle<DisableMapMarkerOnDeathAuthoring>(root, traits.ItsMapMarkerGoesWhenItDies);

            // ---- how it sits in the world ----
            Toggle<DontBlockDiggingAuthoring>(root, traits.DoesNotBlockDigging);
            Toggle<DisablePhysicsAuthoring>(root, traits.HasNoPhysics);
            Toggle<MotionSmoothing.Authoring.MotionSmoothingAuthoring>(root, traits.MovesSmoothly);
            Toggle<Pug.Conversion.DontNeedTransformAuthoring>(root, traits.HasNoPositionOfItsOwn);
            SayWhenTicked(
                traits.HasNoPositionOfItsOwn,
                report,
                "has no position of its own, and that takes it out of the world rather than just " +
                "off the grid. Everything the game does to an object — growing, burning, " +
                "watering, dropping, being hit, being drawn — starts from where the object is, so " +
                "none of it will happen. One thing in Core Keeper is built this way and it is a " +
                "bookkeeping entity nobody ever sees. Leave this off for anything a player meets.");
            SayWhenTicked(
                traits.MovesSmoothly,
                report,
                "is set to move smoothly. That needs a moving body with smoothing switched on, " +
                "which nothing this framework builds has, so the movement will look the same as " +
                "it does now. No object in Core Keeper uses it either.");
            Toggle<DestroyEntityIfPlacementNotValidAuthoring>(
                root,
                traits.DisappearsIfItsSpotStopsBeingValid);

            // REFUSED WHEN THE PICTURE HAS NOTHING TO USE, rather than written and crashed on. The
            // trigger answer is what gives an object a "use" — and Core Keeper's step that builds
            // the use reaches into the object's picture for its first usable part and takes it by
            // index, so on a picture with none the generate throws instead of the look failing to
            // flip. The door answer has been guarded this way since the sweep was written; this is
            // the same guard on the other route to the same component.
            bool triggerCanFlipTheLook =
                traits.ATriggerFlipsItsLook &&
                DimensionQueryCompanions.ThereIsSomethingToUseOnIt(root);
            Toggle<ChangeVariationTriggerAuthoring>(root, triggerCanFlipTheLook);
            SayWhenTicked(
                traits.ATriggerFlipsItsLook && !triggerCanFlipTheLook,
                report,
                "is set so that using it flips its look, but there is nothing on it a player can " +
                "walk up to and use. Set what using it does, and give it a picture, and the look " +
                "will flip.");
            Toggle<MergeDroppedItemAuthoring>(root, traits.DroppedCopiesMergeTogether);
            SayWhenTicked(
                traits.DroppedCopiesMergeTogether,
                report,
                "is set so dropped copies merge together. Core Keeper writes that mark down and " +
                "then never looks at it again — no object in the game uses it and no part of the " +
                "game reads it — so dropped copies will lie side by side as they do now.");
            Toggle<CustomScenePrefabAuthoring>(root, traits.IsPartOfAHandmadeRoom);
            SayWhenTicked(
                traits.IsPartOfAHandmadeRoom,
                report,
                "is marked as part of a handmade room. That takes it out of the list the game uses " +
                "to find an object by name, so nothing can spawn it, drop it or give it to a " +
                "player any more. Only tick it on a copy that exists purely to be placed inside a " +
                "handmade room.");

            // ---- what it is ----
            if (traits.IsATrashCan)
            {
                EnsureInventory(root);
            }

            Toggle<TrashCanAuthoring>(root, traits.IsATrashCan);
            Toggle<SprinklerAuthoring>(root, traits.IsASprinkler);
            Toggle<BaitOnAPoleAuthoring>(root, traits.IsBaitOnAPole);
            Toggle<CherryBlossomTreeAuthoring>(root, traits.IsACherryTree);
            Toggle<FishShoalAuthoring>(root, traits.IsAShoalOfFish);
            SayWhenTicked(
                traits.IsAShoalOfFish,
                report,
                "is marked as a shoal of fish. Core Keeper writes that mark and nothing in the " +
                "game ever reads it, so the object will behave exactly as it does now.");
            Toggle<ContainedMiniSim.Authoring.ContainedMiniSimElementAuthoring>(
                root,
                traits.CanLiveInATank);
            Toggle<GrowingPlantAuthoring>(root, traits.IsAGrowingPlant);
            SayWhenTicked(
                traits.IsAGrowingPlant,
                report,
                "is marked as a growing plant. Nothing in Core Keeper reads that mark — growing is " +
                "done by the crop stages on the Plant asset — so on its own it changes nothing.");
            Toggle<TrailAuthoring>(root, traits.IsATrailSomethingLeftBehind);
            SayWhenTicked(
                traits.IsATrailSomethingLeftBehind && !HasNamed(root, "AttackContinuouslyAuthoring"),
                report,
                "is a trail something left behind, but a trail in Core Keeper is only ever a patch " +
                "of ground that keeps hurting whatever stands in it. Without 'it keeps attacking " +
                "whatever is near it' the trail is scenery. The game's own crystal spike trail and " +
                "void club trail both carry it.");

            // ---- creatures and pets ----
            Toggle<BreedToggleAuthoring>(root, traits.CanBeBred);
            Toggle<PheromoneSensorAuthoring>(root, traits.FollowsPheromoneTrails);
            Toggle<PetOwnerAuthoring>(root, traits.IsAPetsHome);
            Toggle<PetDataAuthoring>(root, traits.CarriesAPetsLook);
            SayWhenTicked(
                traits.CarriesAPetsLook,
                report,
                "carries a pet's look. Core Keeper keeps a pet's look and talents on a hidden " +
                "record beside the player's bag, not on the pet, and reads it from there — so on " +
                "an object of your own nothing looks at it.");
            Toggle<MinionDataAuthoring>(root, traits.IsAMinion);
            SayWhenTicked(
                traits.IsAMinion,
                report,
                "is marked as a minion by this tick, and that particular mark has no effect at " +
                "all: Core Keeper never turns it into anything. Use the real minion answer " +
                "instead — 'If it belongs to somebody' on a creature, or the object's roles on a " +
                "placed thing — on something that also has an attack.");

            // ---- player things ----
            Toggle<SoulsAuthoring>(root, traits.KeepsSouls);
            SayWhenTicked(
                traits.KeepsSouls,
                report,
                "is set to keep souls. Souls are read off the player and nowhere else, so on " +
                "anything but a player this collects nothing.");
            Toggle<PlayingInstrumentAuthoring>(root, traits.CanPlayInstruments);
            SayWhenTicked(
                traits.CanPlayInstruments,
                report,
                "is set to play instruments. The music the game plays back is read off players " +
                "only, so this object will hold a tune nobody hears.");
            if (traits.WearsEquipment)
            {
                EnsureInventory(root);
            }

            Toggle<EquipmentAuthoring>(root, traits.WearsEquipment);
            SayWhenTicked(
                traits.WearsEquipment,
                report,
                "is set to wear equipment. Only a player wears equipment in Core Keeper — the " +
                "helmets, hands and presets are all read off the player — so this adds about " +
                "fifty empty slots and four extra bags to the object and changes nothing else.");
            Toggle<CombatantsTrackerAuthoring>(root, traits.RemembersWhoItFights);
            SayWhenTicked(
                traits.RemembersWhoItFights && !HasNamed(root, "NearbyEntitiesTrackerAuthoring"),
                report,
                "is set to remember who it fights, but it cannot see anything near it, and the " +
                "list it would remember is built from what it sees. Give it a notice range.");

            // REFUSED. There is one achievement tracker in a Core Keeper world and the game asks
            // for it by expecting exactly one: a second one makes that ask throw, every frame, for
            // the rest of the session, and achievements stop for everybody in the world — not just
            // for the mod. It is not a gap that can be closed, so the component is not written —
            // and it is actively taken off, because a prefab made before this pass has one.
            Toggle<AchievementTrackerAuthoring>(root, false);
            SayWhenTicked(
                traits.CountsTowardsAchievements,
                report,
                "is set to count towards achievements, and that was not written. Core Keeper keeps " +
                "one achievement record per world and expects to find exactly one: a second stops " +
                "achievements working for everyone in that world, including the game's own. If " +
                "you want killing something to unlock an achievement, use 'triggers an achievement " +
                "when it dies' on a creature instead.");
            if (traits.CanCarryAffixes)
            {
                // Affixes are conditions, and the conditions component crashes on a bare add.
                EnsureSupportsConditions(root);
            }

            Toggle<Affixes.Authoring.SupportAffixesAuthoring>(root, traits.CanCarryAffixes);
            Toggle<OverrideLegendaryForSlotRequirementsAuthoring>(
                root,
                traits.LegendariesIgnoreSlotRulesHere);
            Toggle<TriggerEffectAuthoring>(root, traits.TheTriggersPushBackWhenUsed);
            SayWhenTicked(
                traits.TheTriggersPushBackWhenUsed,
                report,
                "is set to push back through the controller triggers. That part of Core Keeper is " +
                "switched off in this build — the two places that would apply and undo the effect " +
                "are empty and nothing calls them — so no controller will do anything.");


            // ---- being sent over the network ----
            //
            // REFUSED, both of them, for the same reason as the achievement record. Core Keeper
            // keeps one of each of these per player connection and asks for it by expecting
            // exactly one; a second makes that ask throw every frame and the client's biome
            // sampling or its slice of the map stops working for the whole session. They are taken
            // off actively, because a prefab made before this pass has one.
            Toggle<ClientBiomeSamplesAuthoring>(root, false);
            SayWhenTicked(
                traits.EachPlayerGetsItsOwnBiomeSamples,
                report,
                "asks for its own biome samples, and that was not written. Core Keeper keeps one " +
                "set per player and expects to find exactly one: a second stops the map reading " +
                "biomes at all for whoever is playing.");
            Toggle<ClientSubMapAuthoring>(root, false);
            SayWhenTicked(
                traits.EachPlayerGetsItsOwnSliceOfTheMap,
                report,
                "asks for its own slice of the map, and that was not written. Core Keeper picks " +
                "one object in the whole game to hold that, so a second one makes the choice a " +
                "coin toss and the map can end up drawn from the wrong thing.");
            Toggle<ConvertToInterpolatedGhostAfterSpawnAuthoring>(
                root,
                traits.SmoothsItselfOnceItHasSpawned);
            SayWhenTicked(
                traits.SmoothsItselfOnceItHasSpawned,
                report,
                "is set to smooth itself once it has spawned. That only happens to something the " +
                "game predicts on the player's own machine, which nothing this framework builds " +
                "is — everything it makes is already the smoothed kind — so nothing changes.");
            Toggle<CreateCharacterGuidAuthoring>(root, traits.GetsACharacterIdOfItsOwn);
            Toggle<CreatePlayerGuidAuthoring>(root, traits.GetsAPlayerIdOfItsOwn);

            if (traits.IsAFloatingDamageNumber)
            {
                // PugDamageAuthoring requires a ghost, because a damage number only means anything
                // once everyone in the game can see it.
                EnsureComponent<Unity.NetCode.GhostAuthoringComponent>(root);
            }

            Toggle<PugDamageAuthoring>(root, traits.IsAFloatingDamageNumber);
            SayWhenTicked(
                traits.IsAFloatingDamageNumber,
                report,
                "is marked as a floating damage number. Core Keeper has no step that turns that " +
                "mark into anything, and no object in the game carries it, so the object will not " +
                "become a damage number. Damage numbers come from the game's own set.");

            // ---- the debug map ----
            SayWhenTicked(
                traits.ShowItOnTheDebugMap,
                report,
                "is set to show on the debug map. The step in Core Keeper that would read the " +
                "marker, its colour and its size is empty in this build, so nothing will be drawn.");
            if (traits.ShowItOnTheDebugMap)
            {
                WorldExplorerDebugTrackerAuthoring tracker =
                    EnsureComponent<WorldExplorerDebugTrackerAuthoring>(root);
                tracker.markerType = traits.DrawnAsACircle
                    ? WorldExplorerDebugMarkerType.Circle
                    : WorldExplorerDebugMarkerType.Box;
                tracker.color = traits.DebugMapColour;
                tracker.radius = traits.DebugMapRadius;
                tracker.showEntityName = traits.DebugMapShowsItsName;
                tracker.showWhenDisabled = traits.DebugMapShowsItWhileSwitchedOff;
                tracker.colorWhenDisabled = traits.DebugMapSwitchedOffColour;
            }
            else
            {
                RemoveComponentIfPresent<WorldExplorerDebugTrackerAuthoring>(root);
            }
            if (report != null && traits.CherryTreeInAHandmadeRoomIsNotCounted)
            {
                report(
                    "is a cherry tree that is also part of a handmade room, and the game skips " +
                    "handmade-room trees when it counts them, so it counts towards nothing.");
            }

            if (report != null && traits.DebugMarkIsInvisible)
            {
                report(
                    "is drawn on the debug map with no size at all, so the mark it leaves there " +
                    "cannot be seen.");
            }
        }

        /// <summary>
        /// Event terminals, the Cicada's kit, tanks and terrariums, fishing nets, farming machines,
        /// and the last odds and ends.
        /// </summary>
        public static void ApplyEventTerminal(
            GameObject root,
            DimensionEventTerminalTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || world == null)
            {
                return;
            }

            if (world.IsAnEventTerminal && !world.TerminalHasNoSteps)
            {
                EventTerminalAuthoring terminal = EnsureComponent<EventTerminalAuthoring>(root);
                terminal.radius = world.Reaches;
                terminal.duration = world.RunsFor;
                terminal.loopIndex = world.LoopsBackToStep;

                LootTableID reward;
                if (!string.IsNullOrEmpty(world.RewardTableId) &&
                    DimensionEditorLootTables.TryResolve(world.RewardTableId, out reward))
                {
                    terminal.lootTable = reward;
                }
                else if (!string.IsNullOrEmpty(world.RewardTableId) && report != null)
                {
                    report(
                        "rewards from loot table '" + world.RewardTableId + "', which the game does " +
                        "not have, so finishing its event gives nothing.");
                }

                terminal.alwaysActiveConnections =
                    new System.Collections.Generic.List<EventTerminalAuthoring.AlwaysActiveConnection>();
                string[] alwaysOn = world.AlwaysOnConnections;
                for (int i = 0; i < alwaysOn.Length; i++)
                {
                    ConnectionAndDirection wire;
                    if (System.Enum.TryParse(alwaysOn[i], false, out wire))
                    {
                        terminal.alwaysActiveConnections.Add(
                            new EventTerminalAuthoring.AlwaysActiveConnection { connection = wire });
                    }
                    else if (report != null)
                    {
                        report(
                            "keeps connection '" + alwaysOn[i] + "' on for its whole event, which " +
                            "is not a connection the game has.");
                    }
                }

                terminal.eventSequence =
                    new System.Collections.Generic.List<EventTerminalAuthoring.EventTerminalSequence>();
                DimensionTerminalStep[] steps = world.Steps;
                for (int i = 0; i < steps.Length; i++)
                {
                    ConnectionAndDirection target;
                    System.Enum.TryParse(steps[i].Connection, false, out target);
                    terminal.eventSequence.Add(new EventTerminalAuthoring.EventTerminalSequence
                    {
                        action = (EventTerminalAction)(int)steps[i].Action,
                        target = target,
                        duration = steps[i].Seconds
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<EventTerminalAuthoring>(root);
            }

            if (world.FightsLikeTheCicada)
            {
                GiantCicadaBossAuthoring cicada = EnsureComponent<GiantCicadaBossAuthoring>(root);
                cicada.amountOfStages = world.Stages;
                cicada.lowestStageMultiplier = world.WeakestStage;
                cicada.stageTransitionDuration = world.StageChangeSeconds;
                cicada.armSlamDamage = world.ArmSlamDamage;
                cicada.damageMultiplier = world.ArmSlamMultiplier;
                cicada.armSlamAnticipation = world.ArmSlamWindUp;
                cicada.armSlamAnimationDuration = world.ArmSlamSeconds;
                cicada.armSlamCooldown = world.ArmSlamCooldown;
                cicada.spawnDuration = world.NymphSpawnSeconds;
                cicada.spawnNymphsMinCooldown = world.NymphMinCooldown;
                cicada.spawnNymphsMaxCooldown = world.NymphMaxCooldown;
                EnsureComponent<CicadaBossAuthoring>(root);
                cicada.voidSpawn = new GiantCicadaBossAuthoring.VoidSpawnConfiguration
                {
                    disabled = !world.CicadaSummonsVoid,
                    duration = world.CicadaVoidSeconds,
                    durationUntilSpawn = world.CicadaVoidWindUp,
                    durationAfterSpawn = world.CicadaVoidRecovery,
                    minCooldown = world.CicadaVoidMinCooldown,
                    maxCooldown = world.CicadaVoidMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<GiantCicadaBossAuthoring>(root);
                RemoveComponentIfPresent<CicadaBossAuthoring>(root);
            }

            Toggle<CicadaNymphAuthoring>(root, world.IsACicadaNymph);

            if (world.HoldsAMiniWorld)
            {
                ContainedMiniSim.Authoring.ContainedMiniSimAuthoring mini =
                    EnsureComponent<ContainedMiniSim.Authoring.ContainedMiniSimAuthoring>(root);
                mini.maxNumberOfSimulatedElements = world.MiniWorldPopulation;
                mini.simulatedEntity = world.MiniWorldInhabitant;
                mini.simulateAreaMinMaxWidth = world.MiniWorldWidth;
                mini.simulateAreaMinMaxHeight = world.MiniWorldHeight;
                mini.simulateAreaMinMaxLength = world.MiniWorldLength;
            }
            else
            {
                RemoveComponentIfPresent<ContainedMiniSim.Authoring.ContainedMiniSimAuthoring>(root);
            }

            if (world.IsAFishingNet)
            {
                // SOMEWHERE TO PUT THE FISH. The pass that draws a net reads the net's own slots
                // to know what is in it, so a net with none is never drawn catching anything. The
                // game's own fishing net carries slots beside the visual answer.
                bool hadSomewhereToPutFish = HasNamed(root, "InventoryAuthoring");
                EnsureInventory(root);
                SayWhenTicked(
                    !hadSomewhereToPutFish,
                    report,
                    "is a fishing net with nowhere to keep what it catches, and the net is drawn " +
                    "from what is in it. Slots were filled in. Build it as a Container if you " +
                    "want to choose how many.");

                FishingNetVisualAuthoring net = EnsureComponent<FishingNetVisualAuthoring>(root);
                net.minMaxSplashTimerSingleFish = new Unity.Mathematics.float2(
                    world.SplashTimerOneFish.x,
                    world.SplashTimerOneFish.y);
                net.minMaxSplashTimerFullNet = new Unity.Mathematics.float2(
                    world.SplashTimerFull.x,
                    world.SplashTimerFull.y);

                net.visualSlots =
                    new System.Collections.Generic.List<FishingNetVisualAuthoring.Slot>();
                Vector2[] spots = world.FishPositions;
                for (int i = 0; i < spots.Length; i++)
                {
                    net.visualSlots.Add(new FishingNetVisualAuthoring.Slot
                    {
                        visualOffset = spots[i]
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<FishingNetVisualAuthoring>(root);
            }

            if (world.NatureTerritorySize > 0)
            {
                CavelingNatureTerritorySpawnerAuthoring nature =
                    EnsureComponent<CavelingNatureTerritorySpawnerAuthoring>(root);
                nature.size = world.NatureTerritorySize;
                nature.farmerSpawnChance = world.FarmerChance;
                nature.hunterSpawnChance = world.HunterChance;
            }
            else
            {
                RemoveComponentIfPresent<CavelingNatureTerritorySpawnerAuthoring>(root);
            }

            if (world.IsAnAutomatedPlanter && !world.FarmMachineWorksOnNothing)
            {
                AutomatedMoveAndPlanterAuthoring planter =
                    EnsureComponent<AutomatedMoveAndPlanterAuthoring>(root);
                planter.affectedPositions =
                    new System.Collections.Generic.List<
                        AutomatedMoveAndPlanterAuthoring.AffectedPositions>();
                DimensionFarmReach[] reach = world.WorksOn;
                for (int i = 0; i < reach.Length; i++)
                {
                    planter.affectedPositions.Add(
                        new AutomatedMoveAndPlanterAuthoring.AffectedPositions
                        {
                            position = new Unity.Mathematics.int2(reach[i].Tile.x, reach[i].Tile.y),
                            moveVector = new Unity.Mathematics.int2(
                                reach[i].MovesItToward.x,
                                reach[i].MovesItToward.y)
                        });
                }
            }
            else
            {
                RemoveComponentIfPresent<AutomatedMoveAndPlanterAuthoring>(root);
            }

            if (world.IsAnAutomatedHarvester && !world.FarmMachineWorksOnNothing)
            {
                AutomatedHarvestAndMoverAuthoring harvester =
                    EnsureComponent<AutomatedHarvestAndMoverAuthoring>(root);
                harvester.affectedPositions =
                    new System.Collections.Generic.List<
                        AutomatedHarvestAndMoverAuthoring.AffectedPositions>();
                DimensionFarmReach[] reach = world.WorksOn;
                for (int i = 0; i < reach.Length; i++)
                {
                    harvester.affectedPositions.Add(
                        new AutomatedHarvestAndMoverAuthoring.AffectedPositions
                        {
                            position = new Unity.Mathematics.int2(reach[i].Tile.x, reach[i].Tile.y),
                            moveVector = new Unity.Mathematics.int2(
                                reach[i].MovesItToward.x,
                                reach[i].MovesItToward.y)
                        });
                }
            }
            else
            {
                RemoveComponentIfPresent<AutomatedHarvestAndMoverAuthoring>(root);
            }

            // A FARM ARM MOVES THINGS, AND MOVING TAKES TIME. Both farm answers require the shared
            // moving block, so Unity attaches one — at nothing: no move time, no rest, and no
            // picking up. A move time of nothing is not "instant", it is a timer that fires every
            // tick with nothing to count down, and the arm plants and harvests as fast as the game
            // runs. The block is only filled in by the "it moves things" answer, which is a
            // different tick in a different place, and a planter is not obviously a mover to
            // anybody. The game's own farm arm carries real times on all of it.
            if ((world.IsAnAutomatedPlanter || world.IsAnAutomatedHarvester) &&
                !world.FarmMachineWorksOnNothing)
            {
                AutomatedMoverSharedAuthoring howItMoves =
                    EnsureComponent<AutomatedMoverSharedAuthoring>(root);
                if (howItMoves.moveTime <= 0f)
                {
                    howItMoves.moveTime = VanillaFarmArmMoveSeconds;
                    howItMoves.cooldownTime = VanillaFarmArmRestSeconds;
                    howItMoves.pickUpDuringMove = true;
                    howItMoves.allowPickupFromInventories = true;

                    SayWhenTicked(
                        true,
                        report,
                        "plants or harvests on its own, and how long a pass over a tile takes was " +
                        "not set — with nothing there the arm works as fast as the game runs. It " +
                        "was given the timing the game's own farm arm uses. Tick 'it moves things' " +
                        "under Automation if you want to choose the timing yourself.");
                }
            }

            // REFUSED, because writing it deletes the object. Core Keeper's water spreading runs
            // off a tile position stored on the component, and it is an ABSOLUTE position in the
            // world, not an offset from the object. Nothing here can know it, so it arrives as
            // 0,0 — and about two seconds after the object is placed the game looks at world tile
            // 0,0, finds no water and no pit there, and destroys the object. No prefab in Core
            // Keeper carries this: the game only ever makes a bare one-off marker at a tile it
            // already knows, which is not something an object can be.
            Toggle<WaterSpreaderAuthoring>(root, false);
            SayWhenTicked(
                world.SpreadsWater,
                report,
                "is set to spread water, and that was not written. Core Keeper spreads water from " +
                "a tile it is told about rather than from an object, and an object set to do it " +
                "deletes itself a couple of seconds after it is placed. Use watered ground, or a " +
                "sprinkler, instead.");

            if (world.IsAStandaloneRecipe && !world.RecipeMakesNothing)
            {
                ObjectID made = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.RecipeMakesId);
                if (made == ObjectID.None)
                {
                    RemoveComponentIfPresent<RecipeAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "is a recipe for '" + world.RecipeMakesId + "', which the game does not " +
                            "have, so it makes nothing.");
                    }
                }
                else
                {
                    RecipeAuthoring recipe = EnsureComponent<RecipeAuthoring>(root);
                    recipe.objectToCraft = new ObjectData
                    {
                        objectID = made,
                        variation = world.RecipeMakesVariation,
                        amount = world.RecipeMakesAmount
                    };
                    recipe.requiresNearbyObject =
                        string.IsNullOrEmpty(world.RecipeNeedsNearbyId) || resolveObject == null
                            ? ObjectID.None
                            : resolveObject(world.RecipeNeedsNearbyId);
                }
            }
            else
            {
                RemoveComponentIfPresent<RecipeAuthoring>(root);
            }

            if (world.UsesItsOwnPlacementIndicator)
            {
                EnsureComponent<PlacementIndicator.PlacementIndicatorAuthoring>(root).axisToSpeed =
                    world.PlacementIndicatorSpeed;
            }
            else
            {
                RemoveComponentIfPresent<PlacementIndicator.PlacementIndicatorAuthoring>(root);
            }

            SayWhenTicked(
                world.UsesItsOwnPlacementIndicator,
                report,
                "is set to use its own placement indicator. The indicator is the square that " +
                "follows the player's cursor, and Core Keeper only ever reads that off the player, " +
                "so an object of your own will not get one and nothing about placing it changes.");

            if (report == null)
            {
                return;
            }

            if (world.TerminalHasNoSteps)
            {
                report("is an event terminal with no steps, so its event finishes instantly.");
            }

            if (world.LoopsPastTheEnd)
            {
                report(
                    "loops back to a step past the end of its own sequence, so the loop points at " +
                    "nothing.");
            }

            if (world.FarmMachineWorksOnNothing)
            {
                report(
                    "is a farming machine with no tiles listed to work on, so it runs and touches " +
                    "nothing.");
            }

            if (world.RecipeMakesNothing)
            {
                report("is a recipe that makes nothing.");
            }
        }

        public static void ApplyFinalTouches(
            GameObject root,
            DimensionFinalTouchesTemplate world,
            System.Action<string> report)
        {
            if (root == null || world == null)
            {
                return;
            }

            if (world.CanBeOccupied)
            {
                OccupiableAuthoring occupiable = EnsureComponent<OccupiableAuthoring>(root);
                occupiable.occupiableSlots =
                    new System.Collections.Generic.List<OccupiableAuthoring.OccupiableSlot>
                    {
                        new OccupiableAuthoring.OccupiableSlot
                        {
                            offsetForward = ToFloat3(world.OccupantForward),
                            offsetRight = ToFloat3(world.OccupantRight),
                            offsetBack = ToFloat3(world.OccupantBack),
                            offsetLeft = ToFloat3(world.OccupantLeft)
                        }
                    };
            }
            else
            {
                RemoveComponentIfPresent<OccupiableAuthoring>(root);
            }

            if (world.ReactsToItsOwnWounds && !world.ReactsWithNothing)
            {
                ConditionID reaction;
                if (System.Enum.TryParse(world.ReactionConditionId, false, out reaction))
                {
                    ChanceToApplyConditionToSelfWhenDamagedAuthoring reacts =
                        EnsureComponent<ChanceToApplyConditionToSelfWhenDamagedAuthoring>(root);
                    reacts.conditionsByChance =
                        new System.Collections.Generic.List<
                            ChanceToApplyConditionToSelfWhenDamagedAuthoring.ConditionByChance>
                        {
                            new ChanceToApplyConditionToSelfWhenDamagedAuthoring.ConditionByChance
                            {
                                chanceForEachPercentDamageTakenByCurrentHealthPercentage =
                                    world.ReactionChanceByHealth,
                                conditionData = new ConditionData
                                {
                                    conditionID = reaction,
                                    duration = world.ReactionSeconds,
                                    value = world.ReactionStrength,
                                    valueMultiplier = 1f
                                }
                            }
                        };
                }
                else
                {
                    RemoveComponentIfPresent<ChanceToApplyConditionToSelfWhenDamagedAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "reacts to being hurt with '" + world.ReactionConditionId + "', which " +
                            "the game does not have, so nothing happens when it is hurt.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<ChanceToApplyConditionToSelfWhenDamagedAuthoring>(root);
            }

            if (world.DestructibleAboveHealth > 0f)
            {
                EnsureComponent<ActAsDestructibleWhileAboveHealthThresholdAuthoring>(root)
                    .threshold = world.DestructibleAboveHealth;

                // The label says destructible, and the second half of what the game does with it
                // is the half that will surprise somebody. Only one thing in Core Keeper carries
                // this and it is a monster pretending to be a rock, which is exactly the trick,
                // but a decoration set this way turns into a monster the first time it is hit.
                SayWhenTicked(
                    true,
                    report,
                    "counts as destructible only above a share of its health, and below that " +
                    "share Core Keeper turns it into an enemy: it becomes hostile, it stops being " +
                    "mineable, and it loses its damage reduction. That is the trick the game's own " +
                    "crystal snail plays. If you only wanted something breakable, take the share " +
                    "back to zero.");
            }
            else
            {
                RemoveComponentIfPresent<ActAsDestructibleWhileAboveHealthThresholdAuthoring>(root);
            }

            if (world.DiesWithItsOwner)
            {
                EnsureComponent<BeDestroyedAlongWithOwnerAuthoring>(root).owner = world.DiesWith;
            }
            else
            {
                RemoveComponentIfPresent<BeDestroyedAlongWithOwnerAuthoring>(root);
            }

            string[] scents = world.GivesOffPheromones;
            if (scents.Length > 0)
            {
                PheromoneAdderAuthoring scent = EnsureComponent<PheromoneAdderAuthoring>(root);
                scent.pheromones = new System.Collections.Generic.List<PheromoneType>();
                for (int i = 0; i < scents.Length; i++)
                {
                    PheromoneType kind;
                    if (System.Enum.TryParse(scents[i], false, out kind))
                    {
                        scent.pheromones.Add(kind);
                    }
                    else if (report != null)
                    {
                        report(
                            "gives off '" + scents[i] + "', which is not a pheromone the game has, " +
                            "so nothing will smell it.");
                    }
                }

                if (scent.pheromones.Count == 0)
                {
                    RemoveComponentIfPresent<PheromoneAdderAuthoring>(root);
                }
            }
            else
            {
                RemoveComponentIfPresent<PheromoneAdderAuthoring>(root);
            }

            ConditionID barred;
            if (!string.IsNullOrEmpty(world.ShowsConditionAsBar) &&
                System.Enum.TryParse(world.ShowsConditionAsBar, false, out barred))
            {
                EnsureComponent<DisplayConditionAsBarWhenEquippedAuthoring>(root).conditionID =
                    barred;

                SayWhenTicked(
                    true,
                    report,
                    "shows an effect as a bar while it is equipped. The bar is drawn from what a " +
                    "player is holding, so it will show for a weapon or a tool and not for " +
                    "something placed in the world.");
            }
            else
            {
                RemoveComponentIfPresent<DisplayConditionAsBarWhenEquippedAuthoring>(root);
            }

            EnvironmentEventType worldEvent;
            if (!string.IsNullOrEmpty(world.TriggersEventOnDeath) &&
                System.Enum.TryParse(world.TriggersEventOnDeath, false, out worldEvent))
            {
                EnsureComponent<EnvironmentEvents.Authoring.TriggerEnvironmentEventOnDeathAuthoring>(root)
                    .environmentEvent =
                    worldEvent;
            }
            else
            {
                RemoveComponentIfPresent<EnvironmentEvents.Authoring.TriggerEnvironmentEventOnDeathAuthoring>(root);
            }

            if (world.VisualFollowSpeed > 0f)
            {
                EnsureComponent<VisualSmoothFollowAuthoring>(root).speed = world.VisualFollowSpeed;
            }
            else
            {
                RemoveComponentIfPresent<VisualSmoothFollowAuthoring>(root);
            }

            if (world.WarmUpSeconds > 0f)
            {
                EnsureComponent<WarmupAuthoring>(root).warmup = world.WarmUpSeconds;

                SayWhenTicked(
                    true,
                    report,
                    "has a warm-up. Core Keeper only counts a warm-up down on something a player " +
                    "is holding — the minigun is the one thing in the game with one — so on a " +
                    "placed object nothing waits.");
            }
            else
            {
                RemoveComponentIfPresent<WarmupAuthoring>(root);
            }

            if (world.IsABossBeam)
            {
                BirdBossBeamAuthoring beam = EnsureComponent<BirdBossBeamAuthoring>(root);
                beam.startDuration = world.BeamStartSeconds;
                beam.loopDuration = world.BeamHoldSeconds;
                beam.endDuration = world.BeamEndSeconds;
                beam.hiddenEndDuration = world.BeamHiddenSeconds;
                beam.startDamageDelay = world.BeamHarmlessFor;
                beam.moveSpeed = world.BeamTravelSpeed;
                beam.moveSideWays = world.BeamSweepsSideways;
                beam.moveDirection = ToFloat3(world.BeamTravelDirection);

                CoreBossBeamAuthoring core = EnsureComponent<CoreBossBeamAuthoring>(root);
                core.startDuration = world.BeamStartSeconds;
                core.loopDuration = world.BeamHoldSeconds;
                core.endDuration = world.BeamEndSeconds;
                core.hiddenEndDuration = world.BeamHiddenSeconds;

                // A HEALTH POOL, EVEN WHEN NOTHING CAN ATTACK IT. Both beam systems name health as
                // something they WRITE — it is how the beam grows, holds and fades — so a beam
                // with no pool is skipped by both and never appears. "Cannot be attacked" is the
                // natural answer for a beam, and it takes the pool off. Core Keeper's own bird
                // boss beam and core boss beam each carry "cannot be attacked" and a health pool
                // together, which is exactly this pairing.
                GiveItBackTheHealthAnUnbreakableThingStillNeeds(
                    root,
                    report,
                    "is a boss beam that also cannot be attacked, and a beam grows and fades " +
                    "through its own health — with none it never appears at all. It was generated " +
                    "with a small pool, which is what the game's own boss beams carry alongside " +
                    "the same setting. Nothing can hurt it: it is not mineable and it has no " +
                    "hurt state.");

                // AND SOMETHING FOR IT TO HURT. The bird beam's system names the "keeps attacking
                // whatever is near it" component for writing, so a beam without it is skipped even
                // with a pool. Both of the game's beams carry it.
                if (!HasNamed(root, "AttackContinuouslyAuthoring"))
                {
                    EnsureComponent<AttackContinuouslyAuthoring>(root);
                    SayWhenTicked(
                        true,
                        report,
                        "is a boss beam, and a beam in Core Keeper is a thing that keeps hurting " +
                        "whatever stands in it. That answer was filled in, because the beam is " +
                        "skipped without it. Set what it attacks and how hard under the attack " +
                        "block.");
                }
            }
            else
            {
                RemoveComponentIfPresent<BirdBossBeamAuthoring>(root);
                RemoveComponentIfPresent<CoreBossBeamAuthoring>(root);
            }

            if (world.IsABossSpawnPoint)
            {
                CoreBossSpawnAuthoring spawn = EnsureComponent<CoreBossSpawnAuthoring>(root);
                spawn.distanceToPlayerToActivate = world.WakesWithin;
                spawn.distanceToPlayerToSpawn = world.SpawnsWithin;
                spawn.spawnTime = world.SpawnSeconds;
                spawn.destructionTime = world.BreakDownSeconds;
                spawn.spawnZOffset = world.SpawnHeight;

                SayWhenTicked(
                    !HasNamed(root, "SpawnCompanionsAuthoring"),
                    report,
                    "is a boss spawn point but has nothing listed to spawn beside it, and Core " +
                    "Keeper spawns a boss from that list. Nothing will come out of it. The " +
                    "companions list is a creature answer, so build the spawn point as a creature " +
                    "if you want it to bring something with it, the way the game's crystal meteor " +
                    "does.");
            }
            else
            {
                RemoveComponentIfPresent<CoreBossSpawnAuthoring>(root);
            }

            if (world.IsAHiveEgg)
            {
                LarvaHiveEggHatchStateAuthoring egg =
                    EnsureComponent<LarvaHiveEggHatchStateAuthoring>(root);
                egg.stateTransitionDuration = world.HiveEggChangeSeconds;
                egg.hatchDuration = world.HiveEggHatchSeconds;

                // Hatching is written over the egg's health, so an egg that cannot be attacked and
                // therefore has no pool is skipped and never hatches.
                GiveItBackTheHealthAnUnbreakableThingStillNeeds(
                    root,
                    report,
                    "hatches like a hive egg and also cannot be attacked, and the game hatches it " +
                    "through its own health — with none it sits there. It was generated with a " +
                    "small pool. Nothing can hurt it: it is not mineable and it has no hurt state.");
            }
            else
            {
                RemoveComponentIfPresent<LarvaHiveEggHatchStateAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.EverySeatIsTheSameSpot)
            {
                report(
                    "can be occupied but seats its occupant at dead centre whichever way it faces, " +
                    "which for anything wider than one tile puts them inside it.");
            }

            if (world.ReactsWithNothing)
            {
                report("reacts to its own wounds without naming a condition to apply.");
            }

            if (world.WakesCloserThanItSpawns)
            {
                report(
                    "wakes closer than it spawns, so a player is already inside its spawn range " +
                    "before it notices them.");
            }
        }

        /// <summary>A Vector3 as the game's own float3.</summary>
        private static Unity.Mathematics.float3 ToFloat3(Vector3 value)
        {
            return new Unity.Mathematics.float3(value.x, value.y, value.z);
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

        /// <param name="eggIsAnsweredElsewhere">
        /// True when the thing being written already has a better place to say it is an egg. A
        /// creature does: <c>DimensionCreatureGenerator.ApplyHatching</c> owns
        /// <c>HatchWhenPlayerNearbyStateAuthoring</c> on a creature, and it is the only one of the
        /// two that can name one of the MOD'S own creatures to hatch into — it writes a name beside
        /// the id for the hydration system to fill in later. This pass runs after that one, so
        /// without this flag its else-branch would remove the hatching the creature's own answer had
        /// just written, and every generated egg would quietly stop hatching.
        /// </param>
        public static void ApplyHidingAndHatching(
            GameObject root,
            DimensionHidingAndHatchingTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            bool eggIsAnsweredElsewhere = false)
        {
            if (root == null || world == null)
            {
                return;
            }

            Toggle<LarvaHiveBossHatchEggStateAuthoring>(root, world.HatchesEggsLikeTheLarvaHive);

            if (world.HidesInBushes)
            {
                BushStateAuthoring bush = EnsureComponent<BushStateAuthoring>(root);
                bush.goToBushDuration = world.SecondsToReachABush;
                bush.randomlyLeaveStateMinDuration = world.MinSecondsHidden;
                bush.randomlyLeaveStateMaxDuration = world.MaxSecondsHidden;
                bush.peakDuration = world.PeekSeconds;
                bush.leaveDuration = world.SecondsToLeave;
                bush.distanceToTargetToLeaveState = world.ComesOutWithin;
                bush.burrowWhenOutOfCombatDelay = world.BurrowsBackAfter;
            }
            else
            {
                RemoveComponentIfPresent<BushStateAuthoring>(root);
            }

            // NOT TOUCHED AT ALL when something else owns the egg. Not "written anyway and hope
            // they agree": the two answers would fight over the same component, and only the other
            // one can carry a name for one of the mod's own creatures.
            if (eggIsAnsweredElsewhere)
            {
                SayWhenTicked(
                    world.IsAnEgg,
                    report,
                    "is set to be an egg here as well as under its own hatching answers. A creature " +
                    "hatches from Hatching, which is the one that can name a creature of your own " +
                    "to hatch into, so this tick was left alone and changed nothing. Untick it and " +
                    "answer it under Hatching.");
            }
            else if (world.IsAnEgg && !world.HatchesIntoNothing)
            {
                ObjectID hatched = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.HatchesIntoId);
                if (hatched == ObjectID.None)
                {
                    RemoveComponentIfPresent<HatchWhenPlayerNearbyStateAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "hatches into '" + world.HatchesIntoId + "', which the game does not " +
                            "have, so nothing comes out of it.");
                    }
                }
                else
                {
                    HatchWhenPlayerNearbyStateAuthoring egg =
                        EnsureComponent<HatchWhenPlayerNearbyStateAuthoring>(root);
                    egg.timeToHatch = world.SecondsToHatch;
                    egg.objectToSpawn = hatched;
                    egg.minSpawnAmount = world.FewestHatched;
                    egg.maxSpawnAmount = world.MostHatched;
                }
            }
            else
            {
                RemoveComponentIfPresent<HatchWhenPlayerNearbyStateAuthoring>(root);
            }

            if (world.CavelingTerritorySize > 0)
            {
                CavelingTerritorySpawnerAuthoring caveling =
                    EnsureComponent<CavelingTerritorySpawnerAuthoring>(root);
                caveling.size = world.CavelingTerritorySize;
                caveling.cavelingSpawnChance = world.CavelingChance;
                caveling.cavelingShamanSpawnChance = world.CavelingShamanChance;
                caveling.cavelingBruteSpawnChance = world.CavelingBruteChance;
            }
            else
            {
                RemoveComponentIfPresent<CavelingTerritorySpawnerAuthoring>(root);
            }

            if (world.FiresADelayedShot)
            {
                IndirectProjectileAuthoring shot =
                    EnsureComponent<IndirectProjectileAuthoring>(root);
                shot.delayTime = world.ShotDelay;
                shot.speed = world.ShotSpeed;
                shot.seeking = world.ShotSeeks;
            }
            else
            {
                RemoveComponentIfPresent<IndirectProjectileAuthoring>(root);
            }

            SayWhenTicked(
                world.FiresADelayedShot,
                report,
                "is set to fire a delayed shot. Core Keeper only steers a shot like that on " +
                "something that IS a shot — it has to be a projectile, sent to players, with a " +
                "direction, a speed and a notice range — so on a placed object the delay and the " +
                "seeking do nothing. Build it as a projectile and give the weapon that fires it.");

            if (world.TriggersOnApproach)
            {
                ProximityTriggerAuthoring trigger =
                    EnsureComponent<ProximityTriggerAuthoring>(root);
                trigger.radius = world.TriggersWithin;
                trigger.delayTime = world.TriggerDelay;
            }
            else
            {
                RemoveComponentIfPresent<ProximityTriggerAuthoring>(root);
            }

            AnimationSpeedAuthoring animation = root.GetComponent<AnimationSpeedAuthoring>();
            if (!Mathf.Approximately(world.AnimationSpeed, 1f) ||
                !Mathf.Approximately(world.AnimationLeanX, 0f) ||
                !Mathf.Approximately(world.AnimationLeanY, 0f))
            {
                animation = EnsureComponent<AnimationSpeedAuthoring>(root);
                animation.speed = world.AnimationSpeed;
                animation.movementX = world.AnimationLeanX;
                animation.movementY = world.AnimationLeanY;

                SayWhenTicked(
                    true,
                    report,
                    "has its own animation speed and lean. Core Keeper works those out from how " +
                    "the PLAYER is moving and what state they are in, and reads them off nothing " +
                    "else, so the numbers here will not change how this object animates.");
            }
            else if (animation != null)
            {
                RemoveComponentIfPresent<AnimationSpeedAuthoring>(root);
            }

            if (world.HasSellSlots)
            {
                SellSlotsAuthoring sell = EnsureComponent<SellSlotsAuthoring>(root);
                sell.sizeX = world.SellColumns;
                sell.sizeY = world.SellRows;
            }
            else
            {
                RemoveComponentIfPresent<SellSlotsAuthoring>(root);
            }

            Toggle<UpgradeSlotAuthoring>(root, world.HasAnUpgradeSlot);
            Toggle<VanitySlotsAuthoring>(root, world.HasVanitySlots);

            // The three of them are the same finding. Core Keeper builds a sell window, a vanity
            // window and an upgrade window for the player and for nothing else — the code that
            // opens each one is only ever handed the player — so the slots sit on the object and
            // no window is ever built from them.
            SayWhenTicked(
                world.HasSellSlots || world.HasAnUpgradeSlot || world.HasVanitySlots,
                report,
                "has sell, upgrade or vanity slots. Core Keeper only ever builds those windows for " +
                "the player, so the slots will exist on the object and nothing will open them. " +
                "For a shop, use the trader answers; for storage, use a container.");

            if (report == null)
            {
                return;
            }

            // Silent when the egg is somebody else's answer: the sentence above already said the
            // tick did nothing, and following it with two more complaints about how it was filled
            // in reads as three problems instead of one.
            if (world.HatchesIntoNothing && !eggIsAnsweredElsewhere)
            {
                report("is an egg that hatches into nothing.");
            }

            if (world.HatchesNone && !eggIsAnsweredElsewhere)
            {
                report("is an egg that hatches none of what it hatches into.");
            }

            if (world.CavelingTerritoryIsEmpty)
            {
                report(
                    "claims a caveling territory with no chance of a caveling, shaman or brute " +
                    "appearing in it, so the territory stays empty.");
            }

            if (world.DelayedShotNeverMoves)
            {
                report("fires a delayed shot with no speed, so it waits and then sits there.");
            }
        }

        public static void ApplyManaAndAura(
            GameObject root,
            DimensionManaAndAuraTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || world == null)
            {
                return;
            }

            if (world.HoldsMana)
            {
                ManaAuthoring mana = EnsureComponent<ManaAuthoring>(root);
                mana.startMana = world.StartingMana;
                mana.maxMana = world.MaximumMana;
                mana.manaTickRate = world.RefillRate;
                mana.startRegenDelay = world.RefillDelay;
            }
            else
            {
                RemoveComponentIfPresent<ManaAuthoring>(root);
            }

            SayWhenTicked(
                world.HoldsMana,
                report,
                "holds mana. The pool will refill the way you set it, but the barrier the game " +
                "spends mana on only works on something that records when it was last hurt, and " +
                "Core Keeper records that for players only. Mana on anything else is a number that " +
                "goes up.");

            SayWhenTicked(
                world.SiphonsMana && world.OwnedBy == null,
                report,
                "siphons mana, but Core Keeper only siphons on behalf of whoever owns the thing " +
                "doing it, and this has no owner. Set who owns it, under the same block, and the " +
                "siphon will run.");

            if (world.SiphonsMana)
            {
                SiphonMana.Authoring.SiphonManaAuthoring siphon =
                    EnsureComponent<SiphonMana.Authoring.SiphonManaAuthoring>(root);
                siphon.maxManaSiphonedPerSecond = world.SiphonRate;
                siphon.manaSiphonCooldownSeconds = world.SiphonCooldown;
                siphon.maxTransferDistance = world.SiphonReach;
                siphon.siphonRadius = world.SiphonRadius;
            }
            else
            {
                RemoveComponentIfPresent<SiphonMana.Authoring.SiphonManaAuthoring>(root);
            }

            if (world.HealsWhatIsNear)
            {
                HealNearbyEntitiesAuthoring heal =
                    EnsureComponent<HealNearbyEntitiesAuthoring>(root);
                heal.isActive = world.HealingStartsOn;
                heal.healthPerSecond = world.HealthPerSecond;
                heal.radius = world.HealRadius;

                FactionID side;
                if (!string.IsNullOrEmpty(world.HealsFaction) &&
                    System.Enum.TryParse(world.HealsFaction, false, out side))
                {
                    heal.healsTargetsOfFaction = side;
                }
                else if (!string.IsNullOrEmpty(world.HealsFaction) && report != null)
                {
                    report(
                        "heals faction '" + world.HealsFaction + "', which the game does not have, " +
                        "so it heals everyone nearby.");
                }
            }
            else
            {
                RemoveComponentIfPresent<HealNearbyEntitiesAuthoring>(root);
            }

            if (world.IsAncientWiring)
            {
                AncientElectricityConnectionAuthoring ancient =
                    EnsureComponent<AncientElectricityConnectionAuthoring>(root);
                ancient.electricityAmount = world.CarriesPower;
                ancient.sourceEnergy = world.ProducesPower;
                ancient.blocksElectricity = world.BlocksTheFlow;
            }
            else
            {
                RemoveComponentIfPresent<AncientElectricityConnectionAuthoring>(root);
            }

            if (world.IsPartOfSomethingElse)
            {
                EnsureComponent<EntityPartAuthoring>(root).mainEntity = world.BelongsTo;
            }
            else
            {
                RemoveComponentIfPresent<EntityPartAuthoring>(root);
            }

            if (world.OwnedBy != null)
            {
                EnsureComponent<OwnerAuthoring>(root).owner = world.OwnedBy;
            }
            else
            {
                RemoveComponentIfPresent<OwnerAuthoring>(root);
            }

            if (world.IsHydraBait)
            {
                EnsureComponent<HydraBossBaitAuthoring>(root).attractsHydraType =
                    (HydraBossType)(int)world.AttractsHydra;
            }
            else
            {
                RemoveComponentIfPresent<HydraBossBaitAuthoring>(root);
            }

            if (world.MarksABossSpawn && !world.MarksNoBoss)
            {
                ObjectID boss = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.BossThatAppearsId);
                if (boss == ObjectID.None)
                {
                    RemoveComponentIfPresent<BossSpawnLocationAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "marks where '" + world.BossThatAppearsId + "' appears, which the game " +
                            "does not have, so nothing will appear there.");
                    }
                }
                else
                {
                    EnsureComponent<BossSpawnLocationAuthoring>(root).bossID = boss;
                }
            }
            else
            {
                RemoveComponentIfPresent<BossSpawnLocationAuthoring>(root);
            }

            if (world.BlastLaysGround && !world.BlastGroundIsMissing)
            {
                int laid = ResolveTilesetName(world.BlastGroundTilesetId, resolveTileset);
                if (laid < 0)
                {
                    RemoveComponentIfPresent<SpawnTileOnExplosionAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "lays '" + world.BlastGroundTilesetId + "' where it explodes, which is " +
                            "not a tileset, so it leaves the ground as it was.");
                    }
                }
                else
                {
                    SpawnTileOnExplosionAuthoring blast =
                        EnsureComponent<SpawnTileOnExplosionAuthoring>(root);
                    blast.tileset = (PugTilemap.Tileset)laid;
                    blast.tileType = world.BlastGroundKind;
                    blast.duration = world.BlastGroundSeconds;
                    blast.spawnRequiresWalkable = world.BlastNeedsWalkableGround;
                }
            }
            else
            {
                RemoveComponentIfPresent<SpawnTileOnExplosionAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.SiphonsFromNowhere)
            {
                report("siphons mana with no reach and no radius, so it never draws from anything.");
            }

            if (world.HealsNothing)
            {
                report("heals what is near it for nothing per second.");
            }

            if (world.AncientWiringDoesNothing)
            {
                report(
                    "is ancient wiring that carries no power, produces none and blocks nothing, so " +
                    "it does nothing at all in the network.");
            }

            if (world.MarksNoBoss)
            {
                report("marks a boss spawn without naming which boss.");
            }

            if (world.BlastGroundIsMissing)
            {
                report("is told to lay ground where it explodes without naming any.");
            }
        }

        /// <param name="onACreature">
        /// True when the thing being written is a creature rather than something placed. It changes
        /// nothing that is written; it changes what is SAID. Two of the notes below tell a placed
        /// object that wandering and roaming are creature work and to build it as a creature — true
        /// advice on a chest, and a flat lie once the creature generator is the caller.
        /// </param>
        public static void ApplySpawnerAndOrb(
            GameObject root,
            DimensionSpawnerAndOrbTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            bool onACreature = false)
        {
            if (root == null || world == null)
            {
                return;
            }

            // NOTHING IN CORE KEEPER READS THIS MARK, and the object generator's own companion row
            // has said so for a while without the writer saying anything. `SpawnerCD` is declared
            // in `ck-db\Pug.ECS.Components\SpawnerCD.cs` and no system anywhere reads it, so every
            // number under the plain spawner is a number that goes nowhere. Said here as well as in
            // the sweep because an author filling in five spawn distances deserves to hear it from
            // the thing they are filling in.
            SayWhenTicked(
                world.IsAPlainSpawner,
                report,
                "is set up as a plain spawner. Nothing in Core Keeper reads that mark — the " +
                "component it becomes has no reader anywhere in the game — so it will spawn " +
                "nothing however the distances are set. Use a larva or slime territory, a nest, or " +
                "a spawner platform instead.");

            if (world.IsAPlainSpawner)
            {
                SpawnerAuthoring spawner = EnsureComponent<SpawnerAuthoring>(root);
                spawner.minSpawnDistance = world.SpawnsNoCloserThan;
                spawner.maxSpawnDistance = world.SpawnsNoFurtherThan;
                spawner.maxNumberSpawned = world.KeepsTrackOf;
                spawner.forgetWhenThisFarAway = world.ForgetsBeyond;
                spawner.disableSpawnWhenStationary = world.StopsWhenStill;
            }
            else
            {
                RemoveComponentIfPresent<SpawnerAuthoring>(root);
            }

            if (world.LarvaTerritorySize > 0)
            {
                EnsureComponent<LarvaTerritorySpawnerAuthoring>(root).size =
                    world.LarvaTerritorySize;
            }
            else
            {
                RemoveComponentIfPresent<LarvaTerritorySpawnerAuthoring>(root);
            }

            if (world.SlimeTerritorySize > 0)
            {
                SlimeTerritorySpawnerAuthoring slime =
                    EnsureComponent<SlimeTerritorySpawnerAuthoring>(root);
                slime.size = world.SlimeTerritorySize;
                slime.slimeBlobSpawnChance = world.SlimeBlobChance;
            }
            else
            {
                RemoveComponentIfPresent<SlimeTerritorySpawnerAuthoring>(root);
            }

            if (world.IsADriftingOrb && !world.OrbHasNoPattern)
            {
                ElectricOrbAuthoring orb = EnsureComponent<ElectricOrbAuthoring>(root);
                orb.startDuration = world.OrbAppearSeconds;
                orb.loopDuration = world.OrbDriftSeconds;
                orb.endDuration = world.OrbFadeSeconds;
                orb.hiddenEndDuration = world.OrbHiddenSeconds;
                orb.bounceOnWalls = world.OrbBouncesOffWalls;
                orb.movementPatterns =
                    new System.Collections.Generic.List<ElectricOrbAuthoring.MovementPattern>();

                DimensionOrbDrift[] drifts = world.OrbPatterns;
                for (int i = 0; i < drifts.Length; i++)
                {
                    ElectricOrbMovementPattern named;
                    if (!System.Enum.TryParse(drifts[i].Pattern, false, out named))
                    {
                        if (report != null)
                        {
                            report(
                                "drifts using pattern '" + drifts[i].Pattern + "', which the game " +
                                "does not have, so that stretch is left out.");
                        }

                        continue;
                    }

                    orb.movementPatterns.Add(new ElectricOrbAuthoring.MovementPattern
                    {
                        pattern = named,
                        minMaxDurationSeconds = drifts[i].SecondsRange,
                        minMaxSpeed = drifts[i].SpeedRange,
                        sinusoidalPattern = drifts[i].Weaves,
                        sinusoidalMaxTurnAngleDegrees = drifts[i].WeaveAngle,
                        sinusoidalRepeatTimePerSecond = drifts[i].WeaveRate
                    });
                }

                if (orb.movementPatterns.Count == 0)
                {
                    RemoveComponentIfPresent<ElectricOrbAuthoring>(root);
                }
            }
            else
            {
                RemoveComponentIfPresent<ElectricOrbAuthoring>(root);
            }

            SayWhenTicked(
                !onACreature && world.WandersNearSomething && !world.WandersNearNothing,
                report,
                "wanders near something, but wandering in Core Keeper is something a creature " +
                "does: the game needs a walking speed and a body that can be pushed, and a placed " +
                "object has neither. Build it as a creature and it will wander.");

            if (world.WandersNearSomething && !world.WandersNearNothing)
            {
                ObjectID near = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.StaysNearObjectId);
                if (near == ObjectID.None)
                {
                    RemoveComponentIfPresent<RandomFollowStateAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "wanders near '" + world.StaysNearObjectId + "', which the game does " +
                            "not have, so it wanders freely instead.");
                    }
                }
                else
                {
                    RandomFollowStateAuthoring follow =
                        EnsureComponent<RandomFollowStateAuthoring>(root);
                    follow.objectToFollow = near;
                    follow.minDistanceFromObjectToFollow = world.StaysNoCloserThan;
                    follow.maxDistanceFromObjectToFollow = world.StraysNoFurtherThan;
                    follow.maxWalkDuration = world.WalksForAtMost;
                    follow.minIdleDuration = world.RestsAtLeast;
                    follow.maxIdleDuration = world.RestsAtMost;
                }
            }
            else
            {
                RemoveComponentIfPresent<RandomFollowStateAuthoring>(root);
            }

            if (world.IsABossStatue && !world.StatueAcceptsNothing)
            {
                ObjectID crystal = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.AcceptsCrystalId);
                if (crystal == ObjectID.None)
                {
                    RemoveComponentIfPresent<BossStatueAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "is a statue accepting '" + world.AcceptsCrystalId + "', which the game " +
                            "does not have, so nothing will light it.");
                    }
                }
                else
                {
                    BossStatueAuthoring statue = EnsureComponent<BossStatueAuthoring>(root);
                    statue.acceptsCrystalID = crystal;
                    statue.electricityLoadUpTimer = world.StatueChargeSeconds;
                }
            }
            else
            {
                RemoveComponentIfPresent<BossStatueAuthoring>(root);
            }

            SayWhenTicked(
                !onACreature && world.ChewsGroundAsItRoams,
                report,
                "chews the ground as it roams, but roaming in Core Keeper is something a creature " +
                "does: the game needs a walking speed, a body that can be pushed and a patrol " +
                "route to work over, and a placed object has none of the three. Build it as a " +
                "creature and it will roam.");

            // ON A CREATURE THE ADVICE IS THE OTHER HALF OF THE SAME SENTENCE. Roaming really is
            // creature work, and the third of the three things it needs is the route:
            // `RoamingStateSystem.cs:152` names `RoamingPathBuffer` in its query, and the only
            // thing in the game that produces one is `RoamingPathAuthoring`, which this framework
            // writes from the patrol route and nowhere else. The companion sweep fills a route in
            // if the author left it blank; this says so, so a route that appeared from nowhere is
            // not a surprise.
            SayWhenTicked(
                onACreature && world.ChewsGroundAsItRoams,
                report,
                "chews the ground as it roams. Roaming is walked along a route, so it needs one " +
                "under 'Where it walks' — without a route the game never looks at it at all. If " +
                "you left the route blank, a wandering circle was filled in for you, the same " +
                "shape the game's own worm roams in.");

            if (world.ChewsGroundAsItRoams)
            {
                RoamingStateAuthoring roam = EnsureComponent<RoamingStateAuthoring>(root);
                roam.tileDamageRadius = world.ChewRadius;
                roam.distanceInfrontToDamageTiles = world.ChewReach;
                roam.cantHitSpecificObjects = new System.Collections.Generic.List<ObjectID>();

                string[] spared = world.NeverBreaks;
                for (int i = 0; i < spared.Length; i++)
                {
                    ObjectID safe = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(spared[i]);
                    if (safe != ObjectID.None)
                    {
                        roam.cantHitSpecificObjects.Add(safe);
                    }
                    else if (report != null)
                    {
                        report(
                            "spares '" + spared[i] + "' while roaming, which the game does not " +
                            "have, so it will break everything.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<RoamingStateAuthoring>(root);
            }

            if (world.PullsThingsIn)
            {
                RandomWalkGravityWellAuthoring well =
                    EnsureComponent<RandomWalkGravityWellAuthoring>(root);
                well.radius = world.PullReaches;

                // ZERO PULLS NOTHING, AND THE NUMBER MEANS NOTHING ELSE. RandomWalkGravitySystem
                // runs its overlap against a filter it hardcodes and never reads this field as a
                // layer mask at all — the one place it appears is `(attractMask & attractMask) != 0`,
                // so it is a yes-or-no. UpdateFactionSystem writes 1 or 2 into it for a player, and
                // across the 57 vanilla prefabs that carry one the only values are 1 (16 of them)
                // and 2 (41). An earlier pass filled it with Category03|Category15 off a prefab
                // that does not carry those, and told the author it was "pulling on creatures and
                // critters", which was untrue twice over.
                well.attractMask = world.PullsOnLayers != 0
                    ? (uint)world.PullsOnLayers
                    : AGravityWellThatActuallyPulls;

                SayWhenTicked(
                    world.PullsOnLayers == 0,
                    report,
                    "pulls things in and was left at 0, which pulls nothing at all. It was " +
                    "generated at 1, the number the game itself writes. The number has no other " +
                    "meaning — the game only checks it is not 0 — so anything above 0 pulls the " +
                    "same things.");
            }
            else
            {
                RemoveComponentIfPresent<RandomWalkGravityWellAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.OrbHasNoPattern)
            {
                report("drifts as an orb with no movement patterns, so it never moves.");
            }

            if (world.WandersNearNothing)
            {
                report("wanders near something without naming what.");
            }

            if (world.StatueAcceptsNothing)
            {
                report("is a boss statue that accepts no crystal, so nothing can light it.");
            }
        }

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

        public static void ApplyMoreBossKits(
            GameObject root,
            DimensionMoreBossKitsTemplate kit,
            System.Action<string> report)
        {
            if (root == null || kit == null)
            {
                return;
            }

            Toggle<OctopusBossTeleportLocationAuthoring>(root, kit.IsAnOctopusSurfacingSpot);
            Toggle<BossLarvaSpawnStateAuthoring>(root, kit.ArrivesLikeTheBossLarva);

            if (kit.FightsLikeTheBird)
            {
                BirdBossAuthoring bird = EnsureComponent<BirdBossAuthoring>(root);
                bird.landDuration = kit.BirdLandSeconds;
                bird.durationBeforeStartingToSpawnStones = kit.BirdSecondsBeforeStones;
                bird.durationBeforeLeaveStonesSpawnState = kit.BirdSecondsBeforeStopping;
                bird.beamSpawn = new BirdBossAuthoring.SpawnConfiguration
                {
                    durationUntilSpawn = kit.BirdBeamWindUp,
                    durationAfterSpawn = kit.BirdBeamRecovery,
                    minCooldown = kit.BirdBeamMinCooldown,
                    maxCooldown = kit.BirdBeamMaxCooldown
                };
                bird.stoneSpawn = new BirdBossAuthoring.SpawnConfiguration
                {
                    durationUntilSpawn = kit.BirdStoneWindUp,
                    durationAfterSpawn = kit.BirdStoneRecovery,
                    minCooldown = kit.BirdStoneMinCooldown,
                    maxCooldown = kit.BirdStoneMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<BirdBossAuthoring>(root);
            }

            if (kit.FightsLikeTheRobot)
            {
                RobotBossAuthoring robot = EnsureComponent<RobotBossAuthoring>(root);
                robot.legBrokenTime = kit.BrokenLegSeconds;
                robot.legXOffset = kit.LegSideOffset;
                robot.legZOffset = kit.LegDepthOffset;
                robot.maxStepHeight = kit.StepHeight;
                robot.stepHeightProgressMultiplier = kit.StepHeightCurve;
                robot.distanceToTriggerLegMovement = kit.DistanceBeforeALegMoves;
                robot.legMovementSpeed = kit.LegSpeed;
                robot.stepForwardDistance = kit.StepLength;
                robot.startDistance = kit.LegStartDistance;
                robot.legStepCooldownDuration = kit.LegStepCooldown;
                robot.numberOfAttacksInChain = kit.AttacksInAChain;
                robot.chainedDelayBetweenAttacks = kit.DelayBetweenChainedAttacks;
            }
            else
            {
                RemoveComponentIfPresent<RobotBossAuthoring>(root);
            }

            if (kit.FightsLikeTheOctopus)
            {
                OctopusBossAuthoring octopus = EnsureComponent<OctopusBossAuthoring>(root);
                octopus.appearDuration = kit.OctopusAppearSeconds;
                octopus.durationBeforeStartingToSpawnTentacles =
                    kit.OctopusSecondsBeforeTentacles;
                octopus.durationBeforeLeaveTentacleSpawnState =
                    kit.OctopusSecondsBeforeStopping;
                octopus.tentacleSpawn = new OctopusBossAuthoring.SpawnConfiguration
                {
                    durationUntilSpawn = kit.TentacleWindUp,
                    durationAfterSpawn = kit.TentacleRecovery,
                    minCooldown = kit.TentacleMinCooldown,
                    maxCooldown = kit.TentacleMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<OctopusBossAuthoring>(root);
            }

            if (kit.FightsLikeTheLarva)
            {
                BossLarvaAuthoring larva = EnsureComponent<BossLarvaAuthoring>(root);
                larva.damage = kit.LarvaDamage;
                larva.damageMultiplier = kit.LarvaMultiplier;
                larva.segmentPrefabSmall = kit.LarvaSmallSegment;
                larva.segmentPrefabMedium = kit.LarvaMediumSegment;
                larva.segmentPrefabLarge = kit.LarvaLargeSegment;

                // The roam numbers differ between a classic world and a full-release one, so the
                // game stores each as a pair. One authored number is written to both, because
                // "roams 30 tiles" means the same thing to a person whichever world they are in.
                larva.roamDistance = new WorldGenerationTypeDependentValue<int>
                {
                    classic = kit.LarvaRoamDistance,
                    fullRelease = kit.LarvaRoamDistance
                };
                larva.roamDeviation = new WorldGenerationTypeDependentValue<int>
                {
                    classic = kit.LarvaRoamVariation,
                    fullRelease = kit.LarvaRoamVariation
                };
            }
            else
            {
                RemoveComponentIfPresent<BossLarvaAuthoring>(root);
            }

            if (kit.FightsLikeTheShaman)
            {
                ShamanBossAuthoring shaman = EnsureComponent<ShamanBossAuthoring>(root);
                shaman.phase1HealthThreshold = kit.ShamanPhaseAtHealth;
                shaman.phase1TransitionDuration = kit.ShamanPhaseSeconds;
                shaman.invulnerableDuration = kit.ShamanUntouchableSeconds;
            }
            else
            {
                RemoveComponentIfPresent<ShamanBossAuthoring>(root);
            }

            if (kit.FightsLikeTheSnake)
            {
                EnsureComponent<SnakeBossAuthoring>(root).defeatSoundEffectDelay =
                    kit.SnakeDefeatSoundDelay;
            }
            else
            {
                RemoveComponentIfPresent<SnakeBossAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (kit.LarvaHasNoBody)
            {
                report(
                    "fights like the boss Larva with none of its three body segments set, so it " +
                    "will be a head with nothing behind it.");
            }

            if (kit.BirdStopsBeforeItStarts)
            {
                report(
                    "stops dropping stones before it starts dropping them, so it never drops any.");
            }
        }

        public static void ApplyCoreBossKit(
            GameObject root,
            DimensionCoreBossKitTemplate kit,
            System.Action<string> report)
        {
            if (root == null || kit == null)
            {
                return;
            }

            Toggle<CoreBossOrbAuthoring>(root, kit.IsOneOfTheCoresOrbs);
            Toggle<TheCoreAuthoring>(root, kit.IsTheCoreItself);
            Toggle<CoreAttentionMarkerAuthoring>(root, kit.IsTheAttentionMarker);
            Toggle<WallBossHeadAuthoring>(root, kit.IsTheWallsHead);

            if (report != null && kit.IsTheAttentionMarker)
            {
                report(
                    "is the marker that points at the Core, which the game only draws for the Core " +
                    "boss and the crystal meteor, so on anything else nothing acts on it.");
            }

            if (kit.FightsLikeTheCore)
            {
                CoreBossAuthoring core = EnsureComponent<CoreBossAuthoring>(root);
                core.orbCount = kit.OrbCount;
                core.orbRotationSpeed = kit.OrbSpeed;
                core.orbMinDistance = kit.OrbMinDistance;
                core.orbMaxDistance = kit.OrbMaxDistance;
                core.phase1HealthThreshold = kit.PhaseChangesAtHealth;
                core.phase1TransitionDuration = kit.PhaseChangeSeconds;
                core.invulnerableDuration = kit.UntouchableForSeconds;
                core.whirlwindProjectileDamage = kit.WhirlwindDamage;
                core.whirlwindProjectileDamageMultiplier = kit.WhirlwindMultiplier;
                core.homingTriangleProjectileDamage = kit.HomingDamage;
                core.homingTriangleProjectileDamageMultiplier = kit.HomingMultiplier;

                core.voidSpawn = new CoreBossAuthoring.VoidSpawnConfiguration
                {
                    disabled = !kit.SummonsVoid,
                    duration = kit.VoidSummonSeconds,
                    durationUntilSpawn = kit.VoidSummonWindUp,
                    durationAfterSpawn = kit.VoidSummonRecovery,
                    minCooldown = kit.VoidSummonMinCooldown,
                    maxCooldown = kit.VoidSummonMaxCooldown
                };

                core.beamSpawn = new CoreBossAuthoring.SpawnConfiguration
                {
                    disabled = !kit.SummonsBeams,
                    durationUntilSpawn = kit.BeamSummonWindUp,
                    durationAfterSpawn = kit.BeamSummonRecovery,
                    minCooldown = kit.BeamSummonMinCooldown,
                    maxCooldown = kit.BeamSummonMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<CoreBossAuthoring>(root);
            }

            if (kit.FightsLikeTheWall && !kit.WallHasNoSegments)
            {
                WallBossAuthoring wall = EnsureComponent<WallBossAuthoring>(root);
                wall.distanceFromCore = kit.DistanceFromTheCore;
                wall.totalSegments = kit.Segments;
                wall.segmentRadius = kit.SegmentRadius;
                wall.totalWidth = kit.TotalWidth;
                wall.attackDuration = kit.WallAttackSeconds;
                wall.attackCooldown = kit.WallAttackCooldown;
                wall.slitheringFrequencyMultiplier = kit.SlitherSpeed;
                wall.slitheringWavelengthMultiplier = kit.SlitherWavelength;
                wall.slitheringWaveHeightMultiplier = kit.SlitherHeight;
                wall.pauseBeforeBulbsEmergeDuration = kit.PauseBeforeBulbs;
                wall.pauseBeforeHeadEmergesDuration = kit.PauseBeforeHead;
                wall.vulnerableDuration = kit.WallVulnerableSeconds;
                wall.vulnerableOnDamageMaxDuration = kit.WallVulnerableCutShort;
                wall.headOffset = kit.HeadOffset;
                wall.bulbOffset = kit.BulbOffset;

                wall.movement = new System.Collections.Generic.List<MovementParameters>();
                DimensionWallMovement[] moves = kit.MovementByPlayersAlive;
                for (int i = 0; i < moves.Length; i++)
                {
                    wall.movement.Add(new MovementParameters
                    {
                        onTotalAliveTargets = moves[i].WhenThisManyAlive,
                        maxSpeed = moves[i].TopSpeed,
                        accelerationSpeed = moves[i].Acceleration,
                        decelerationSpeed = moves[i].Deceleration,
                        decelerationDurationOnEnter = moves[i].SlowingSecondsOnEntering
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<WallBossAuthoring>(root);
            }

            if (kit.FightsLikeTheScarab)
            {
                ScarabBossAuthoring scarab = EnsureComponent<ScarabBossAuthoring>(root);
                scarab.appearDuration = kit.ScarabAppearSeconds;
                scarab.buryDuration = kit.ScarabBurySeconds;
                scarab.unearthDuration = kit.ScarabSurfaceSeconds;
                scarab.minChargeCooldown = kit.ScarabMinChargeCooldown;
                scarab.maxChargeCooldown = kit.ScarabMaxChargeCooldown;
                scarab.chargeDamage = kit.ScarabChargeDamage;
                scarab.chargeDamageMultiplier = kit.ScarabChargeMultiplier;
                scarab.bombScarabSpawnAnticipationDuration = kit.BombScarabWindUp;
                scarab.bombScarabSpawnDuration = kit.BombScarabSeconds;
                scarab.bombScarabSpawnEndDuration = kit.BombScarabRecovery;
                scarab.bombScarabSpawnMinCooldown = kit.BombScarabMinCooldown;
                scarab.bombScarabSpawnMaxCooldown = kit.BombScarabMaxCooldown;
            }
            else
            {
                RemoveComponentIfPresent<ScarabBossAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (kit.PhaseThresholdMakesNoFight)
            {
                report(
                    "changes phase at full or empty health, so it either changes gear the instant " +
                    "the fight starts or never changes at all. Vanilla uses halfway.");
            }

            if (kit.WallHasNoSegments)
            {
                report("fights like the Wall with no segments, so there is no wall to fight.");
            }
        }

        public static void ApplyBorrowedBossKit(
            GameObject root,
            DimensionBorrowedBossKitTemplate kit,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || kit == null)
            {
                return;
            }

            if (kit.FightsLikeAHydra)
            {
                HydraBossAuthoring hydra = EnsureComponent<HydraBossAuthoring>(root);
                hydra.hydraType = (HydraBossType)(int)kit.HydraKind;
                hydra.vulnerableEntityPrefab = kit.WeakPointPrefab;

                hydra.buryDuration = kit.BuryingSeconds;
                hydra.unearthDuration = kit.SurfacingSeconds;
                hydra.buriedMinCooldown = kit.MinSecondsUnderground;
                hydra.buriedMaxCooldown = kit.MaxSecondsUnderground;

                hydra.buriedAppearDamage = kit.SurfacingSlamDamage;
                hydra.buriedAppearDamageMultiplier = kit.SurfacingSlamMultiplier;
                hydra.beamDamage = kit.BeamDamage;
                hydra.beamDamageMultiplier = kit.BeamMultiplier;
                hydra.stalactiteMortarDamage = kit.StalactiteDamage;
                hydra.stalactiteMortarDamageMultiplier = kit.StalactiteMultiplier;
                hydra.shockwaveDamage = kit.ShockwaveDamage;
                hydra.shockwaveDamageMultiplier = kit.ShockwaveMultiplier;
                hydra.iceShardMortarDamage = kit.IceShardDamage;
                hydra.iceShardMortarDamageMultiplier = kit.IceShardMultiplier;
                hydra.lavaMortarDamage = kit.LavaDamage;
                hydra.lavaMortarDamageMultiplier = kit.LavaMultiplier;
                hydra.nilipedeMortarDamage = kit.NilipedeDamage;
                hydra.nilipedeMortarDamageMultiplier = kit.NilipedeMultiplier;
            }
            else
            {
                RemoveComponentIfPresent<HydraBossAuthoring>(root);
            }

            Toggle<SlimeBossAuthoring>(root, kit.CyclesItsShotsLikeTheSlimeKing);

            if (report != null && kit.CyclesItsShotsLikeTheSlimeKing)
            {
                report(
                    "cycles its shots like the Slime King, which the game only ever does for the " +
                    "Lava Slime Boss itself, so the setting is carried but nothing acts on it.");
            }

            if (kit.SlamsLikeTheSlime)
            {
                SlimeBossJumpStateAuthoring slam =
                    EnsureComponent<SlimeBossJumpStateAuthoring>(root);
                slam.anticipationTime = kit.SlamWindUp;
                slam.maxAirTime = kit.SlamAirTime;
                slam.landTime = kit.SlamLandTime;
                slam.jumpMoveSpeed = kit.SlamSpeed;
                slam.enragedAnticipationTime = kit.EnragedSlamWindUp;
                slam.enragedMaxAirTime = kit.EnragedSlamAirTime;
                slam.enragedJumpMoveSpeed = kit.EnragedSlamSpeed;
                slam.damage = kit.SlamDamage;
                slam.damageMultiplier = kit.SlamMultiplier;

                if (!string.IsNullOrEmpty(kit.SlamLeavesTilesetId))
                {
                    int left = ResolveTilesetName(kit.SlamLeavesTilesetId, resolveTileset);
                    if (left >= 0)
                    {
                        slam.slimeTileset = (PugTilemap.Tileset)left;
                    }
                    else if (report != null)
                    {
                        report(
                            "leaves '" + kit.SlamLeavesTilesetId + "' where it slams, which is not " +
                            "a tileset, so it leaves the ground as it was.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<SlimeBossJumpStateAuthoring>(root);
            }

            if (kit.EnragingMakesItLessDangerous && report != null)
            {
                report(
                    "gets SLOWER when it enrages — its enraged wind-up is longer or its enraged " +
                    "leap slower than its ordinary one. The fight will get easier exactly where a " +
                    "player expects it to get harder.");
            }
        }

        public static void ApplySmashesObjects(
            GameObject root,
            DimensionSmashesObjectsTemplate smash,
            System.Action<string> report)
        {
            if (root == null || smash == null || !smash.SmashesWhatIsInItsWay)
            {
                RemoveComponentIfPresent<DamageObjectStateAuthoring>(root);
                return;
            }

            DamageObjectStateAuthoring smasher =
                EnsureComponent<DamageObjectStateAuthoring>(root);
            smasher.maxAllowedDamagesWithoutGoal = smash.GivesUpAfterSwings;
            smasher.anticipationTime = smash.WindUp;
            smasher.hitDuration = smash.SwingSeconds;
            smasher.hitDistanceInfront = smash.Reach;
            smasher.hitRadius = smash.Radius;
            smasher.damage = smash.FlatObjectDamage;
            smasher.damageMultiplier = smash.HitsObjectsThisHardForItsTier;
            smasher.meleeDamage = smash.FlatCreatureDamage;
            smasher.meleeDamageMultiplier = smash.HitsCreaturesThisHardForItsTier;
            smasher.bypassCantAttackBehaviourWhileChasing = smash.SmashesEvenWhileChasing;

            if (report == null)
            {
                return;
            }

            if (smash.NeverGivesUpOnAWall)
            {
                report(
                    "smashes what is in its way and never gives up, so one it cannot path around " +
                    "will stand at the nearest wall swinging for the rest of the session.");
            }

            if (smash.SwingConnectsWithNothing)
            {
                report(
                    "smashes what is in its way with no reach and no width, so its swing connects " +
                    "with nothing.");
            }
        }

        public static void ApplyBeamWeapon(
            GameObject root,
            DimensionBeamWeaponTemplate beam,
            System.Func<string, ObjectID> resolveProjectile,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || beam == null || !beam.FiresABeam)
            {
                RemoveComponentIfPresent<BeamWeaponAuthoring>(root);
                return;
            }

            BeamWeaponAuthoring weapon = EnsureComponent<BeamWeaponAuthoring>(root);
            weapon.attackDistance = beam.Reaches;
            weapon.expandWhenHeld = beam.GrowsWhileHeld;
            weapon.expandTimeSeconds = beam.GrowsOverSeconds;
            weapon.expandMinDistance = beam.StartsAtReach;
            weapon.isStickyBeam = beam.LatchesOn;
            weapon.onlyDamageAtEndOfBeam = beam.OnlyTheEndHurts;
            weapon.beamVisualFromCenter = beam.DrawnFromTheCentre;
            weapon.extraProjectiles = beam.ExtraBeams;
            weapon.spreadAngle = beam.SpreadDegrees;
            weapon.overrideAnimation = beam.OverrideAnimation;
            weapon.secondaryOverrideAnimation = beam.HeldAnimation;
            weapon.useRangedLoopAnimation = beam.UsesTheLoopingAnimation;
            weapon.spawnOffsetDistance = new Unity.Mathematics.float3(
                beam.StartsAtOffset.x,
                beam.StartsAtOffset.y,
                beam.StartsAtOffset.z);

            weapon.collideFilterPVPOn = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)beam.CollidesWithLayers
            };
            weapon.attackFilterPVPOn = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)beam.AttacksLayers
            };

            ConditionID cost;
            if (!string.IsNullOrEmpty(beam.ManaCostCondition) &&
                System.Enum.TryParse(beam.ManaCostCondition, false, out cost))
            {
                weapon.manaCostCondition = cost;
            }
            else if (!string.IsNullOrEmpty(beam.ManaCostCondition) && report != null)
            {
                report(
                    "drains '" + beam.ManaCostCondition + "' while its beam runs, which the game " +
                    "does not have, so the beam is free.");
            }

            ConditionID boost;
            if (!string.IsNullOrEmpty(beam.DamageBoostCondition) &&
                System.Enum.TryParse(beam.DamageBoostCondition, false, out boost))
            {
                weapon.damageIncreaseCondition = boost;
            }
            else if (!string.IsNullOrEmpty(beam.DamageBoostCondition) && report != null)
            {
                report(
                    "is boosted by '" + beam.DamageBoostCondition + "', which the game does not " +
                    "have, so nothing boosts it.");
            }

            weapon.secondaryProjectileVariationID =
                string.IsNullOrEmpty(beam.SecondProjectileId) || resolveProjectile == null
                    ? ObjectID.None
                    : resolveProjectile(beam.SecondProjectileId);

            if (report == null)
            {
                return;
            }

            // One of the mod's own shots is fine — BeamWeaponAuthoring stays on the item either way,
            // and the runtime fills BeamWeaponCD.windupProjectileID in. A name that is neither used
            // to go through with no word said at all.
            if (!string.IsNullOrEmpty(beam.SecondProjectileId) &&
                weapon.secondaryProjectileVariationID == ObjectID.None &&
                !(isDeferred != null && isDeferred(beam.SecondProjectileId)))
            {
                report(
                    "fires '" + beam.SecondProjectileId + "' at the end of a held beam, which is " +
                    "neither a projectile in this mod nor one the game has, so holding it fires " +
                    "nothing extra.");
            }

            if (beam.GrowsInstantly)
            {
                report(
                    "grows its beam while held but over no time at all, so it snaps to full reach " +
                    "immediately.");
            }

            if (beam.StartsLongerThanItEnds)
            {
                report(
                    "starts its beam longer than it can ever grow to, so holding it makes the beam " +
                    "shorter.");
            }

            if (beam.ExtraBeamsWouldOverlap)
            {
                report(
                    "fires extra beams with no spread between them, so they all leave along the " +
                    "same line and only one is visible.");
            }
        }

        /// <param name="isDeferred">
        /// Answers "this name is one of the mod's own, and the runtime will fill the number in".
        /// Null means the caller has no way to tell, which is the old behaviour: an unresolved name
        /// is treated as a mistake.
        /// </param>
        /// <param name="qualify">
        /// Turns one of the mod's own local ids into the full name the game registers it under. The
        /// summoning list is the one thing here that ships a NAME rather than a number, so without
        /// this a mod boss would be written down as "Emberlord" where the game knows it as
        /// "MyMod:Emberlord" and the idol would find nothing.
        /// </param>
        public static void ApplyWorldRoles(
            GameObject root,
            DimensionWorldRolesTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null,
            System.Func<string, string> qualify = null)
        {
            if (root == null || world == null)
            {
                return;
            }

            Toggle<FireflyAuthoring>(root, world.IsAFirefly);
            Toggle<FireSpreading.Authoring.FireSpreaderAuthoring>(root, world.SpreadsFire);
            Toggle<ConvertToTileAuthoring>(root, world.BecomesGroundWhenItSettles);
            Toggle<RootAuthoring>(root, world.IsARoot);
            Toggle<ManaBarrierAuthoring>(root, world.IsAManaBarrier);
            // BOTH OF THESE GIVE THE OBJECT A "USE", and Core Keeper's step that builds a use
            // reaches into the object's picture for its first usable part and takes it by index —
            // so on a picture with none, the generate throws. Guarded the same way the door is.
            bool canSwitchOffImmunityZones =
                world.SwitchesOffImmunityZones &&
                DimensionQueryCompanions.ThereIsSomethingToUseOnIt(root);
            Toggle<DisableImmuneZoneAuthoring>(root, canSwitchOffImmunityZones);
            if (canSwitchOffImmunityZones)
            {
                // The system that does the switching off names the zone itself, so an object that
                // only carries the switch is never looked at. The game's own zone emitter carries
                // both. Sized to the switch's own reach when the author has not set one.
                ImmunityZoneAuthoring zone = EnsureComponent<ImmunityZoneAuthoring>(root);
                if (zone.radius <= 0f)
                {
                    zone.radius = VanillaSummonCircleNoticeRadius;
                }
            }

            SayWhenTicked(
                world.SwitchesOffImmunityZones && !canSwitchOffImmunityZones,
                report,
                "switches off immunity zones, and that was not written: switching one off is " +
                "something a player does BY USING the object, and there is nothing on it to use. " +
                "Set what using it does, and give it a picture.");

            Toggle<IsHabitableIdolAuthoring>(root, world.CreaturesCanLiveHere);
            SayWhenTicked(
                world.CreaturesCanLiveHere,
                report,
                "says creatures can live here. In Core Keeper that only adds a line to the tooltip " +
                "when you point at it. No creature will move in because of it.");

            bool canBeAGrave =
                world.IsAPlayerGrave && DimensionQueryCompanions.ThereIsSomethingToUseOnIt(root);
            Toggle<PlayerGraveAuthoring>(root, canBeAGrave);
            SayWhenTicked(
                world.IsAPlayerGrave && !canBeAGrave,
                report,
                "is a player's grave, and that was not written: a grave is picked up by using it, " +
                "and there is nothing on this a player can use. Set what using it does, and give " +
                "it a picture.");
            Toggle<Affixes.Authoring.AffixAuthoring>(root, world.CarriesAnAffix);

            if (world.AuraReachOverride > 0f)
            {
                EnsureComponent<AuraDistanceOverrideAuthoring>(root).distance =
                    world.AuraReachOverride;
            }
            else
            {
                RemoveComponentIfPresent<AuraDistanceOverrideAuthoring>(root);
            }

            if (world.LetsThePlayerKeepMoving)
            {
                EnsureComponent<MoveFreelyWeaponAuthoring>(root).moveSpeedMultiplier =
                    world.MovingSpeedMultiplier;
            }
            else
            {
                RemoveComponentIfPresent<MoveFreelyWeaponAuthoring>(root);
            }

            WriteMinion(
                root,
                world.IsAMinion,
                world.MinionHitsThisHardForItsTier,
                world.MinionMinesToo,
                world.MinionMinesThisHardForItsTier,
                report);

            if (world.IsASummoningItem && !world.SummonsNothing)
            {
                SummoningItemAuthoring summoner = EnsureComponent<SummoningItemAuthoring>(root);
                summoner.availableBossesToSummon =
                    new System.Collections.Generic.List<ObjectID>();

                // The mod's own bosses ride here as names. This is NOT the object-link table: a
                // summoning item's list is a buffer that the game's own SummoningItemConverter
                // fills from ObjectIDs, and DimensionSummoningItemConverter already appends names
                // into the same SummoningItemBuffer at load. Reusing that path is what makes a mod
                // boss summonable at all; before this the name was silently dropped and the idol
                // did nothing on a circle, forever, with nothing said at generate.
                System.Collections.Generic.List<string> ourBosses =
                    new System.Collections.Generic.List<string>();

                string[] bosses = world.SummonsAnyOf;
                for (int i = 0; i < bosses.Length; i++)
                {
                    ObjectID boss = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(bosses[i]);
                    if (boss != ObjectID.None)
                    {
                        summoner.availableBossesToSummon.Add(boss);
                    }
                    else if (isDeferred != null && isDeferred(bosses[i]))
                    {
                        ourBosses.Add(qualify == null ? bosses[i] : qualify(bosses[i]));
                    }
                    else if (report != null)
                    {
                        report(
                            "summons '" + bosses[i] + "', which is neither one of this mod's " +
                            "bosses nor one the game has, so that one is left off the list.");
                    }
                }

                // Written whole rather than appended to, so a boss removed from the list actually
                // goes. The item generator's own summoning pass runs AFTER this one and unions its
                // boss-asset names in, which is the one order in which neither pass can erase the
                // other's work.
                if (ourBosses.Count > 0)
                {
                    EnsureComponent<ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root)
                        .bossObjectNames = ourBosses;
                }
                else
                {
                    RemoveComponentIfPresent<
                        ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root);
                }

                if (summoner.availableBossesToSummon.Count == 0 && ourBosses.Count == 0)
                {
                    RemoveComponentIfPresent<SummoningItemAuthoring>(root);
                }
            }
            else
            {
                RemoveComponentIfPresent<SummoningItemAuthoring>(root);
                RemoveComponentIfPresent<
                    ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root);
            }

            if (world.IsATitanShrine && !world.ShrineHasNoTitan)
            {
                ObjectID titan = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.BoundTitanId);
                bool titanIsOurs =
                    titan == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(world.BoundTitanId);
                if (titan == ObjectID.None && !titanIsOurs)
                {
                    RemoveComponentIfPresent<TitanShrineAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "is a shrine bound to '" + world.BoundTitanId + "', which the game does " +
                            "not have, so it is an ordinary object.");
                    }
                }
                else
                {
                    // The component stays even for one of the mod's own titans: TitanShrineConverter
                    // always writes TitanShrineCD, and that component is what the link hydration
                    // fills in at load. Strip it and there is nothing left to write into.
                    EnsureComponent<TitanShrineAuthoring>(root).TitanObjectID = titan;
                }
            }
            else
            {
                RemoveComponentIfPresent<TitanShrineAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.SummonsNothing)
            {
                report(
                    "is a summoning item that names nothing to summon, so putting it on a circle " +
                    "does nothing.");
            }

            if (world.ShrineHasNoTitan)
            {
                report("is a titan shrine with no titan bound to it.");
            }

            if (world.MinionMiningWillBeIgnored)
            {
                report(
                    "sets how hard its minion mines without letting it mine, so that number is " +
                    "never read.");
            }
        }

        public static void ApplySegmentedCreature(
            GameObject root,
            DimensionSegmentedCreatureTemplate segmented,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || segmented == null || !segmented.IsSegmented || segmented.HasNoBody)
            {
                RemoveComponentIfPresent<SnakeMovementStateAuthoring>(root);
                if (segmented != null && segmented.HasNoBody && report != null)
                {
                    report(
                        "is a segmented creature with no segments, so it is just a head. Give it a " +
                        "starting length.");
                }

                return;
            }

            SnakeMovementStateAuthoring snake = EnsureComponent<SnakeMovementStateAuthoring>(root);

            snake.initialLength = segmented.StartingLength;
            snake.spread = segmented.Spacing;
            snake.additionalHorizontalSpread = segmented.ExtraSidewaysSpacing;
            snake.treatSegmentsAsIndividualParts = segmented.SegmentsAreHitSeparately;

            snake.turnDuration = segmented.TurnSeconds;
            snake.wavinessAmplitude = segmented.WeaveWidth;
            snake.wavinessTurnTime = segmented.WeaveSeconds;
            snake.chaoticMovement = segmented.MovesChaotically;
            snake.slowDownForWalls = segmented.SlowsNearWalls;
            snake.descendIntoPits = segmented.GoesIntoPits;
            snake.playMoveAnimation = segmented.PlaysItsMoveAnimation;
            snake.usePhysVelocity = segmented.MovedByPhysics;

            snake.useCaterpillarMovement = segmented.BunchesLikeACaterpillar;
            snake.stretchOutStrength = segmented.StretchOutStrength;
            snake.stretchBackStrength = segmented.PullBackStrength;
            snake.stretchFrequency = segmented.BunchSpeed;
            snake.stretchSpread = segmented.BunchSpread;

            snake.targetingType = (SnakeTargetingType)(int)segmented.PicksTarget;
            snake.distanceToAttackPlayer = segmented.NoticesPlayersWithin;
            snake.distanceToTargetToChangeTarget = segmented.SwitchesTargetWithin;
            snake.playerTargetCooldownMin = segmented.MinTargetCooldown;
            snake.playerTargetCooldownMax = segmented.MaxTargetCooldown;
            snake.distanceAllowedToMoveAwayFromCombatStartPosition =
                segmented.StraysFromTheFightBy;

            snake.disableDamage = segmented.DealsNoDamage;
            snake.damage = segmented.FlatDamage;
            snake.damageMultiplier = segmented.HitsThisHardForItsTier;
            snake.attackRadius = segmented.HitRadius;
            snake.attackOffset = segmented.HitOffset;
            snake.pushbackForce = segmented.ShoveForce;
            snake.tooCloseDistanceForAttack = segmented.TooCloseToAttack;
            snake.dontDropLootFromObjectsBeingDestroyed = segmented.WhatItSmashesDropsNothing;

            snake.tilePlacementType =
                (SnakeMovementTilePlacementType)(int)segmented.LeavesBehind;
            snake.tilePlacementRadiusMultiplier = segmented.TrailWidthMultiplier;

            snake.tailObjectId = string.IsNullOrEmpty(segmented.TailObjectId)
                ? ObjectID.None
                : (resolveObject == null ? ObjectID.None : resolveObject(segmented.TailObjectId));

            snake.cantHitSpecificObject = string.IsNullOrEmpty(segmented.NeverHits)
                ? ObjectID.None
                : (resolveObject == null ? ObjectID.None : resolveObject(segmented.NeverHits));

            if (report == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(segmented.TailObjectId) &&
                snake.tailObjectId == ObjectID.None &&
                !(isDeferred != null && isDeferred(segmented.TailObjectId)))
            {
                report(
                    "ends in '" + segmented.TailObjectId + "', which is neither one of this mod's " +
                    "objects nor one the game has, so its body simply stops instead.");
            }

            if (!string.IsNullOrEmpty(segmented.NeverHits) &&
                snake.cantHitSpecificObject == ObjectID.None &&
                !(isDeferred != null && isDeferred(segmented.NeverHits)))
            {
                report(
                    "is set never to hit '" + segmented.NeverHits + "', which is neither one of " +
                    "this mod's objects nor one the game has, so it will hit everything.");
            }

            if (segmented.BunchingWillBeIgnored)
            {
                report(
                    "has its bunching shaped without caterpillar movement switched on, so its body " +
                    "flows instead and none of those four settings are read.");
            }

            if (segmented.TrailWidthWillBeIgnored)
            {
                report("sets how wide its trail is while leaving no trail.");
            }

            if (segmented.WeaveIsHalfSpecified)
            {
                report(
                    "gives its weave a width but no time, or a time but no width. Both are needed, " +
                    "so it will travel straight.");
            }
        }

        public static void ApplyMachineRoles(
            GameObject root,
            DimensionMachineRolesTemplate machine,
            System.Action<string> report)
        {
            if (root == null || machine == null)
            {
                return;
            }

            Toggle<DrillAuthoring>(root, machine.IsADrill);
            // The filter is read only while Core Keeper is building the machine's MOVING half, and
            // that component declares nothing it needs, so ticking the filter on a machine that
            // does not move things writes it into a branch the game never enters. The game's own
            // robot arm carries the filter beside the moving answer. Supplied, because the moving
            // block is a component this object can carry, and said so the author knows the object
            // now moves things.
            if (machine.FiltersWhatBeltsCarry)
            {
                EnsureComponent<AutomatedApplyFilterForMoversAuthoring>(root);
                if (!HasNamed(root, "AutomatedMoverSharedAuthoring"))
                {
                    EnsureComponent<AutomatedMoverSharedAuthoring>(root);
                    SayWhenTicked(
                        true,
                        report,
                        "filters what belts carry, and the game only reads a filter on something " +
                        "that carries things in the first place. The moving half was filled in. " +
                        "Set the move timing under Automation, or the arm will run as fast as the " +
                        "game does.");
                }
            }
            else
            {
                RemoveComponentIfPresent<AutomatedApplyFilterForMoversAuthoring>(root);
            }
            Toggle<AnvilAuthoring>(root, machine.IsAnAnvil);
            Toggle<PrioritizedRepairMaterialAuthoring>(root, machine.IsPreferredForRepairs);
            Toggle<CommandMinionWeaponAuthoring>(root, machine.CommandsMinions);
            SayWhenTicked(
                machine.CommandsMinions,
                report,
                "commands minions. Core Keeper reads that off the thing a player is swinging — the " +
                "tome of fire and the other summoning weapons — so on a placed object nobody will " +
                "be commanding anything. Put the answer on the weapon instead.");

            if (machine.IsAnAutomatedMiner && !machine.MinesNothing)
            {
                // Which way the drill faces. Without it the game points every drill south and the
                // bite offset never turns with the machine. Every one of the game's drills carries
                // the answer that turns a variation into a direction.
                EnsureComponent<DirectionBasedOnVariationAuthoring>(root);

                Pug.Automation.AutomatedMinerAuthoring miner =
                    EnsureComponent<Pug.Automation.AutomatedMinerAuthoring>(root);
                miner.damage = machine.BitesFor;
                miner.cooldownTime = machine.SecondsBetweenBites;
                miner.damagePositions =
                    new System.Collections.Generic.List<Unity.Mathematics.int2>();

                Vector2Int[] bite = machine.ChewsTilesAt;
                for (int i = 0; i < bite.Length; i++)
                {
                    miner.damagePositions.Add(new Unity.Mathematics.int2(bite[i].x, bite[i].y));
                }

                // ONLY THE FIRST ONE IS EVER USED. Core Keeper reads a single offset off this list
                // and never looks at the rest, so a drill told to chew four tiles chews one. The
                // whole list is still written, because the game may one day read more of it and
                // because throwing the author's answer away would be worse than saying so.
                SayWhenTicked(
                    bite.Length > 1,
                    report,
                    "chews at " + bite.Length + " tiles, and Core Keeper only ever uses the first " +
                    "one. It will chew at " + bite[0].x + "," + bite[0].y + " and nowhere else. " +
                    "Put more drills down if you want more tiles chewed.");
            }
            else
            {
                RemoveComponentIfPresent<Pug.Automation.AutomatedMinerAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (machine.MinesNothing)
            {
                report(
                    "mines on its own with no tiles listed to chew, so it runs and never digs. " +
                    "List at least one offset — (1,0) is the tile to its right.");
            }

            if (machine.BitesForNothing)
            {
                report("mines on its own for no damage, so nothing it chews ever breaks.");
            }
        }

        /// <summary>
        /// Things that go off, take their ground with them, stand in for ground, or bounce.
        /// </summary>
        public static void ApplyTerrainEffects(
            GameObject root,
            DimensionTerrainEffectTemplate terrain,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || terrain == null)
            {
                return;
            }

            // WITHOUT THE FIGHTING TAGS NOTHING LOOKS AT IT. The two systems that set an object off
            // on contact both name the tags that say what it counts as and what it fights — the
            // same block a creature gets. Every one of the game's four objects that explode on
            // contact is a creature or a minion. Supplied here rather than said about, because the
            // tags are a component and this object can carry one.
            if (terrain.ExplodesOnContact)
            {
                bool alreadyKnewItsEnemies = HasNamed(root, "BehaviourTagsAuthoring");
                DimensionQueryCompanions.DecidesWhoIsAnEnemy(root);
                SayWhenTicked(
                    !alreadyKnewItsEnemies,
                    report,
                    "explodes on contact, and nothing was set about what it counts as an enemy — " +
                    "which is the first thing the game checks before it lets anything go off on " +
                    "contact. An empty answer was filled in, which reads as 'nothing here is my " +
                    "enemy'. Set what it attacks if you want it to go off on something.");
            }

            if (terrain.ExplodesOnContact)
            {
                ExplodeOnImpactAuthoring blast = EnsureComponent<ExplodeOnImpactAuthoring>(root);
                blast.distanceToExplode = terrain.ExplodesWithin;
                blast.explodeRadius = terrain.BlastRadius;
                blast.explodeDamage = terrain.BlastDamage;
                blast.explodeDamageMultiplier = terrain.BlastHitsThisHardForItsTier;
                blast.spawnTilesOnExplode = terrain.BlastLaysGround && !terrain.BlastGroundIsMissing;
                if (blast.spawnTilesOnExplode)
                {
                    int laid = ResolveTilesetName(terrain.BlastGroundTilesetId, resolveTileset);
                    if (laid < 0)
                    {
                        blast.spawnTilesOnExplode = false;
                        if (report != null)
                        {
                            report(
                                "lays '" + terrain.BlastGroundTilesetId + "' where it explodes, " +
                                "which is not a tileset, so it leaves the ground as it was.");
                        }
                    }
                    else
                    {
                        blast.tilesetToSpawn = (PugTilemap.Tileset)laid;
                        blast.tileTypeToSpawn = terrain.BlastGroundKind;
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<ExplodeOnImpactAuthoring>(root);
            }

            if (terrain.TakesItsTileWhenItDies)
            {
                RemoveTileOnDeathAuthoring takes =
                    EnsureComponent<RemoveTileOnDeathAuthoring>(root);
                takes.removeChance = terrain.RemovalChance;
                takes.tileType = terrain.RemovedTileKind;

                int removed = ResolveTilesetName(terrain.RemovedTilesetId, resolveTileset);
                if (removed >= 0)
                {
                    takes.tileset = (PugTilemap.Tileset)removed;
                }
                else if (!string.IsNullOrEmpty(terrain.RemovedTilesetId) && report != null)
                {
                    report(
                        "removes '" + terrain.RemovedTilesetId + "' beneath it when it dies, which " +
                        "is not a tileset, so it removes whatever is there instead.");
                }
            }
            else
            {
                RemoveComponentIfPresent<RemoveTileOnDeathAuthoring>(root);
            }

            if (terrain.CountsAsATile && !terrain.StandsInForNothing)
            {
                int stands = ResolveTilesetName(terrain.CountsAsTilesetId, resolveTileset);
                if (stands < 0)
                {
                    RemoveComponentIfPresent<PseudoTileAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "counts as '" + terrain.CountsAsTilesetId + "' underfoot, which is not " +
                            "a tileset, so nothing treats it as ground.");
                    }
                }
                else
                {
                    PseudoTileAuthoring pseudo = EnsureComponent<PseudoTileAuthoring>(root);
                    pseudo.tileset = (PugTilemap.Tileset)stands;
                    pseudo.tileType = terrain.CountsAsTileKind;
                }
            }
            else
            {
                RemoveComponentIfPresent<PseudoTileAuthoring>(root);
                if (terrain.StandsInForNothing && report != null)
                {
                    report(
                        "counts as ground without saying which ground, so nothing treats it as any.");
                }
            }

            if (terrain.BouncesAlongTheGround)
            {
                EnsureComponent<GroundBouncableProjectileAuthoring>(root).verticalCurve =
                    terrain.Arc;

                SayWhenTicked(
                    true,
                    report,
                    "is set to bounce along the ground. Core Keeper only bounces a thrown thing — " +
                    "the seven grenades are all of it — and the bounce needs the effect stream a " +
                    "thrown thing carries, which a placed object does not have. Build it as a " +
                    "projectile and the arc will work.");
            }
            else
            {
                RemoveComponentIfPresent<GroundBouncableProjectileAuthoring>(root);
            }

            if (terrain.BlastGroundIsMissing && report != null)
            {
                report("is told to lay ground where it explodes without naming any.");
            }
        }

        /// <summary>A tileset by our name, then by the game's own, or -1.</summary>
        private static int ResolveTilesetName(
            string tilesetId,
            System.Func<string, int> resolveTileset)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return -1;
            }

            int resolved = resolveTileset == null ? -1 : resolveTileset(tilesetId);
            if (resolved >= 0)
            {
                return resolved;
            }

            PugTilemap.Tileset named;
            return System.Enum.TryParse(tilesetId, false, out named) ? (int)named : -1;
        }

        public static void ApplyLooksAtItsSurroundings(
            GameObject root,
            DimensionLooksAtItsSurroundingsTemplate adapts,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || adapts == null ||
                !adapts.ChangesWithItsSurroundings || adapts.HasNoRules)
            {
                RemoveComponentIfPresent<AdaptiveEntityBufferAuthoring>(root);
                if (adapts != null && adapts.HasNoRules && report != null)
                {
                    report(
                        "changes its look with its surroundings but has no rules saying how, so it " +
                        "always wears its first look.");
                }

                return;
            }

            AdaptiveEntityBufferAuthoring buffer =
                EnsureComponent<AdaptiveEntityBufferAuthoring>(root);
            buffer.adaptiveCondition = new System.Collections.Generic.List<AdaptiveCondition>();

            DimensionSurroundingRule[] rules = adapts.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                buffer.adaptiveCondition.Add(new AdaptiveCondition
                {
                    variation = rules[i].WearsLook,
                    matchesNeeded = rules[i].MatchesNeeded,
                    allowAnyTilesetToMatch = rules[i].AnyGroundCounts,
                    leftTile = BuildTileCondition(rules[i].ToItsLeft, rules[i].TileKind, resolveTileset, report),
                    rightTile = BuildTileCondition(rules[i].ToItsRight, rules[i].TileKind, resolveTileset, report),
                    forwardTile = BuildTileCondition(rules[i].InFront, rules[i].TileKind, resolveTileset, report),
                    backTile = BuildTileCondition(rules[i].Behind, rules[i].TileKind, resolveTileset, report)
                });
            }

            if (adapts.AFussierRuleIsHiddenByALooserOne && report != null)
            {
                report(
                    "lists a rule needing more matching neighbours BELOW one needing fewer. Rules " +
                    "are checked in order and the first match wins, so the fussier look can never " +
                    "appear. Put the fussiest rules first.");
            }
        }

        /// <summary>One neighbour expectation, resolved from a tileset name.</summary>
        /// <remarks>
        /// A blank name is a real answer — "no expectation on this side" — so it resolves to the
        /// game's own zero rather than being reported as a mistake.
        /// </remarks>
        private static TileCondition BuildTileCondition(
            string tilesetId,
            PugTilemap.TileType tileKind,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return default(TileCondition);
            }

            int resolved = resolveTileset == null ? -1 : resolveTileset(tilesetId);
            if (resolved < 0)
            {
                PugTilemap.Tileset named;
                if (System.Enum.TryParse(tilesetId, false, out named))
                {
                    resolved = (int)named;
                }
            }

            if (resolved < 0)
            {
                if (report != null)
                {
                    report(
                        "expects '" + tilesetId + "' beside it, which is neither one of this mod's " +
                        "tilesets nor one of the game's, so that side is treated as no expectation.");
                }

                return default(TileCondition);
            }

            return new TileCondition
            {
                tileset = (PugTilemap.Tileset)resolved,
                tileType = tileKind
            };
        }

        public static void ApplyNest(
            GameObject root,
            DimensionNestTemplate nest,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || nest == null || !nest.IsANest || nest.ProducesNothing)
            {
                RemoveComponentIfPresent<SpawnAroundObjectAuthoring>(root);
                if (nest != null && nest.ProducesNothing && report != null)
                {
                    report("is a nest that produces nothing, so it just sits there.");
                }

                return;
            }

            SpawnAroundObjectAuthoring spawner = EnsureComponent<SpawnAroundObjectAuthoring>(root);
            spawner.spawnEntries =
                new System.Collections.Generic.List<SpawnAroundObjectAuthoring.SpawnEntry>();

            DimensionNestBrood[] broods = nest.Broods;
            for (int i = 0; i < broods.Length; i++)
            {
                ObjectID made = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(broods[i].ObjectId);
                if (made == ObjectID.None)
                {
                    if (report != null)
                    {
                        report(
                            "produces '" + broods[i].ObjectId + "', which the game does not have, " +
                            "so that one is left out of the nest.");
                    }

                    continue;
                }

                SpawnAroundObjectAuthoring.SpawnEntry entry =
                    new SpawnAroundObjectAuthoring.SpawnEntry
                    {
                        objectToSpawn = new ObjectData
                        {
                            objectID = made,
                            variation = broods[i].Variation,
                            amount = 1
                        },
                        limitNumberSpawned = broods[i].AtMostAtOnce,
                        minSpawnCooldown = broods[i].MinWait,
                        maxSpawnCooldown = broods[i].MaxWait,
                        maxSpawnDistance = broods[i].AppearsWithin,
                        minReachedLimitCooldown = broods[i].MinWaitWhenFull,
                        maxReachedLimitCooldown = broods[i].MaxWaitWhenFull,
                        onlySpawnIfInCombat = broods[i].OnlyWhileFighting,
                        spawnCloseToPlayers = broods[i].AppearsNearPlayers,
                        playerNeedsToBeInsideBiome = broods[i].PlayerMustBeInThatBiome,
                        spawnCrittersInsteadOfObject = broods[i].ProducesCritters,
                        critterDespawnDistance = broods[i].CritterDespawnDistance,
                        objectIsPersistent = broods[i].WhatItMakesPersists,
                        spawnsInBiome = new System.Collections.Generic.List<Biome>()
                    };

                string[] biomes = broods[i].OnlyInBiomes;
                for (int b = 0; b < biomes.Length; b++)
                {
                    Biome biome;
                    if (System.Enum.TryParse(biomes[b], false, out biome))
                    {
                        entry.spawnsInBiome.Add(biome);
                    }
                    else if (report != null)
                    {
                        report(
                            "produces in biome '" + biomes[b] + "', which the game does not have, " +
                            "so it will not produce there.");
                    }
                }

                Season season;
                if (!string.IsNullOrEmpty(broods[i].OnlyInSeason) &&
                    System.Enum.TryParse(broods[i].OnlyInSeason, false, out season))
                {
                    entry.onlySpawnsInSeason = season;
                }

                ConditionID needed;
                if (!string.IsNullOrEmpty(broods[i].RequiresCondition) &&
                    System.Enum.TryParse(broods[i].RequiresCondition, false, out needed))
                {
                    entry.requiredCondition = needed;
                }

                if (!string.IsNullOrEmpty(broods[i].KeepsAwayFrom))
                {
                    entry.avoidSpawnCloseToObject = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(broods[i].KeepsAwayFrom);
                }

                spawner.spawnEntries.Add(entry);

                if (broods[i].BiomeRuleIsIncomplete && report != null)
                {
                    report(
                        "requires the player to be standing in its biome without naming any biome, " +
                        "so that rule never lets anything through.");
                }
            }

            if (spawner.spawnEntries.Count == 0)
            {
                RemoveComponentIfPresent<SpawnAroundObjectAuthoring>(root);
            }
        }

        /// <summary>
        /// Makes something an instrument a player can play, or a sheet of music for one.
        /// </summary>
        public static void ApplyInstrument(
            GameObject root,
            DimensionInstrumentTemplate music,
            System.Action<string> report)
        {
            if (root == null || music == null)
            {
                return;
            }

            if (music.IsAnInstrument && !music.PlaysNothing)
            {
                InstrumentAuthoring instrument = EnsureComponent<InstrumentAuthoring>(root);
                instrument.instrumentType = (InstrumentType)(int)music.Kind;
                instrument.noteSound = new SFXTableIDField { value = music.NoteSound };
                instrument.noteSoundOctave = new SFXTableIDField { value = music.NoteSoundOctaveUp };
                instrument.keyOffsetFromC5 = music.KeysFromC5;
            }
            else
            {
                RemoveComponentIfPresent<InstrumentAuthoring>(root);
            }

            if (music.IsAMusicSheet && !music.SheetIsBlank)
            {
                MusicSheetAuthoring sheet = EnsureComponent<MusicSheetAuthoring>(root);
                sheet.harpTrack = new SFXTableIDField { value = music.HarpTrack };
                sheet.fluteTrack = new SFXTableIDField { value = music.FluteTrack };
                sheet.celloTrack = new SFXTableIDField { value = music.CelloTrack };
                sheet.ocarinaTrack = new SFXTableIDField { value = music.OcarinaTrack };
                sheet.drumkitTrack = new SFXTableIDField { value = music.DrumkitTrack };
                sheet.pianoTrack = new SFXTableIDField { value = music.PianoTrack };
            }
            else
            {
                RemoveComponentIfPresent<MusicSheetAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (music.PlaysNothing)
            {
                report("is an instrument with no note sound, so playing it is silent.");
            }

            if (music.HasNoOctave)
            {
                report(
                    "has a note but no octave-up note, so the upper half of its keys will play at " +
                    "the wrong pitch.");
            }

            if (music.SheetIsBlank)
            {
                report("is a music sheet with nothing recorded on it, so it plays silence.");
            }

            if (music.SheetIsIncomplete)
            {
                report(
                    "is a music sheet written for only " + music.TracksWritten + " of the six " +
                    "instruments. A player holding one of the others hears nothing.");
            }
        }

        public static void ApplyTrader(
            GameObject root,
            DimensionTraderTemplate trader,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || trader == null)
            {
                return;
            }

            if (trader.IsATrader && !trader.TradesWithNothingToSell)
            {
                MerchantAuthoring merchant = EnsureComponent<MerchantAuthoring>(root);
                merchant.items = new System.Collections.Generic.List<MerchantItemInfo>();
                DimensionTradeGood[] stock = trader.Stock;
                for (int i = 0; i < stock.Length; i++)
                {
                    ObjectID sold = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(stock[i].ObjectId);
                    if (sold == ObjectID.None &&
                        !(isDeferred != null && isDeferred(stock[i].ObjectId)))
                    {
                        if (report != null)
                        {
                            report(
                                "sells '" + stock[i].ObjectId + "', which is neither one of this " +
                                "mod's objects nor one the game has, so that one is left off the " +
                                "shelf.");
                        }

                        continue;
                    }

                    // A PLACEHOLDER AT THE RIGHT POSITION for one of the mod's own. The shop is a
                    // buffer the link hydration writes into by position, and dropping the line here
                    // would slide every later one along so the write landed on the wrong item.

                    merchant.items.Add(new MerchantItemInfo
                    {
                        objectID = sold,
                        amount = stock[i].Amount,
                        requirementToBeAvailable =
                            (MerchantItemRequirement)(int)stock[i].AppearsAfter
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<MerchantAuthoring>(root);
            }

            Toggle<CantBeSoldAuthoring>(root, trader.CannotBeSold);

            if (trader.CoinValue > 0)
            {
                EnsureComponent<CoinAmountAuthoring>(root).Value = trader.CoinValue;
            }
            else
            {
                RemoveComponentIfPresent<CoinAmountAuthoring>(root);
            }

            SoulID soul;
            if (!string.IsNullOrEmpty(trader.GivesSoul) &&
                System.Enum.TryParse(trader.GivesSoul, false, out soul) && soul != SoulID.None)
            {
                EnsureComponent<SoulOrbAuthoring>(root).givesSoul = soul;
            }
            else
            {
                RemoveComponentIfPresent<SoulOrbAuthoring>(root);
                if (!string.IsNullOrEmpty(trader.GivesSoul) && report != null)
                {
                    report(
                        "grants soul '" + trader.GivesSoul + "', which the game does not have, so " +
                        "collecting it grants nothing.");
                }
            }

            Season season;
            if (!string.IsNullOrEmpty(trader.BelongsToSeason) &&
                System.Enum.TryParse(trader.BelongsToSeason, false, out season))
            {
                SeasonObjectAuthoring seasonal = EnsureComponent<SeasonObjectAuthoring>(root);
                seasonal.belongsToSeason = season;
                seasonal.removeFromWorldWhenOutOfSeason = trader.VanishesOutOfSeason;
            }
            else
            {
                RemoveComponentIfPresent<SeasonObjectAuthoring>(root);
                if (!string.IsNullOrEmpty(trader.BelongsToSeason) && report != null)
                {
                    report(
                        "belongs to season '" + trader.BelongsToSeason + "', which the game does " +
                        "not have, so it is there all year.");
                }
            }

            if (report == null)
            {
                return;
            }

            if (trader.TradesWithNothingToSell)
            {
                report("is a trader with nothing on its shelf, so talking to it offers no trades.");
            }

            if (trader.StockWillBeIgnored)
            {
                report(
                    "lists things to sell without being a trader, so none of them are ever offered.");
            }
        }

        /// <summary>
        /// Gives a creature a route to walk rather than a patch to mill around in.
        /// </summary>
        public static void ApplyPatrolPath(
            GameObject root,
            DimensionPatrolPathTemplate patrol,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || patrol == null || !patrol.WalksARoute || patrol.RouteHasNoPoints)
            {
                RemoveComponentIfPresent<RoamingPathAuthoring>(root);
                if (patrol != null && patrol.RouteHasNoPoints && report != null)
                {
                    report(
                        "walks a route with no turning points on it, so there is no route to walk. " +
                        "Give it at least one.");
                }

                return;
            }

            RoamingPathAuthoring path = EnsureComponent<RoamingPathAuthoring>(root);
            path.pathType = (RoamingPathType)(int)patrol.Shape;
            path.regionRadius = patrol.ReachesOut;
            path.pointCount = patrol.TurningPoints;
            path.segmentation = patrol.Smoothness;
            path.distanceBetweenPoints = patrol.GapBetweenPoints;
            path.pathLengthMultiplier = patrol.RouteLengthMultiplier;
            path.zigzagAmount = patrol.Weave;
            path.distanceDeviation = patrol.DistanceVariation;
            path.curveSmoothness = patrol.CornerRoundness;
            path.angleDeviation = patrol.TurnVariation;
            path.pointsBetweenAngleDeviationChanges = patrol.PointsBetweenTurns;
            path.minAngleToBiomeMidpoint = patrol.MinAngleToBiomeCentre;
            path.maxAngleToBiomeMidpoint = patrol.MaxAngleToBiomeCentre;
            path.roamAroundPlayerIfInSubBiome = patrol.FollowsThePlayerInSubBiomes;
            path.drawDebugLines = patrol.ShowTheRouteWhileBuilding;

            Biome biome;
            if (!string.IsNullOrEmpty(patrol.StaysInBiome) &&
                System.Enum.TryParse(patrol.StaysInBiome, false, out biome))
            {
                path.forceBiome = new Pug.UnityExtensions.OptionalValue<Biome>(biome);
            }
            else
            {
                path.forceBiome = default(Pug.UnityExtensions.OptionalValue<Biome>);
                if (!string.IsNullOrEmpty(patrol.StaysInBiome) && report != null)
                {
                    report(
                        "keeps its route inside biome '" + patrol.StaysInBiome + "', which the game " +
                        "does not have, so it roams anywhere.");
                }
            }

            // A null resolver is normal here — the creature generator has no mod-tileset index,
            // and a sub-biome check is nearly always against one of the game's own grounds. Fall
            // back to the vanilla name so the field still works without one.
            int subBiome = -1;
            if (!string.IsNullOrEmpty(patrol.SubBiomeTilesetId))
            {
                if (resolveTileset != null)
                {
                    subBiome = resolveTileset(patrol.SubBiomeTilesetId);
                }

                PugTilemap.Tileset named;
                if (subBiome < 0 &&
                    System.Enum.TryParse(patrol.SubBiomeTilesetId, false, out named))
                {
                    subBiome = (int)named;
                }
            }

            if (subBiome >= 0)
            {
                path.playerCheckSubBiomeTileset = (PugTilemap.Tileset)subBiome;
            }
            else if (!string.IsNullOrEmpty(patrol.SubBiomeTilesetId) && report != null)
            {
                report(
                    "watches for sub-biome ground '" + patrol.SubBiomeTilesetId + "', which is not " +
                    "a tileset, so it never switches to following the player.");
            }

            if (patrol.BiomeRuleWillBeIgnored && report != null)
            {
                report(
                    "names a biome to keep its route inside, but its route shape does not stay in a " +
                    "biome. Choose the biome-bound shape for that to matter.");
            }
        }

        /// <summary>
        /// Makes a creature something somebody else summoned, and gives it a summoner's numbers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE OTHER "IS A MINION" DOES NOTHING, and that is why this exists. There have been two
        /// controls with that name for a while: the one under simple traits writes
        /// <c>MinionDataAuthoring</c>, which Core Keeper never turns into anything and which the
        /// framework already says so about; and the real one, which writes <c>MinionAuthoring</c>
        /// and lives in the world-object roles the creature generator never calls. Two controls
        /// with one label and different power is worse than one control, so the creature now has
        /// the real one where a creature can reach it.
        /// </para>
        /// <para>
        /// IT CARRIES MORE THAN THE MULTIPLIERS. <c>MinionConverter</c> ensures
        /// <c>OwnerReferenceCD</c>, and that component is what <c>TouchAttackStateSystem</c> and
        /// <c>MinionOrbitStateSystem</c> name in their queries — so a creature that hurts what it
        /// touches, or orbits whoever summoned it, only works at all once it is a minion or a pet.
        /// It also reads <c>IsFlyingAuthoring</c> off the object, which is written by the creature's
        /// habits, so this must run after those.
        /// </para>
        /// </remarks>
        public static void ApplyCreatureMinion(
            GameObject root,
            DimensionCreatureMinionTemplate minion,
            System.Action<string> report)
        {
            if (root == null || minion == null)
            {
                return;
            }

            WriteMinion(
                root,
                minion.IsAMinion,
                minion.HitsThisHardForItsTier,
                minion.MinesToo,
                minion.MinesThisHardForItsTier,
                report,
                true);
        }

        /// <summary>
        /// Puts the minion component on, or takes it off, and says what is missing beside it.
        /// </summary>
        /// <param name="onACreature">
        /// Changes only what is said. On a world object the advice is "build it as a creature,
        /// where the attacks are"; on a creature that advice would be nonsense.
        /// </param>
        private static void WriteMinion(
            GameObject root,
            bool isAMinion,
            float damageMultiplier,
            bool minesToo,
            float miningDamageMultiplier,
            System.Action<string> report,
            bool onACreature = false)
        {
            if (!isAMinion)
            {
                RemoveComponentIfPresent<MinionAuthoring>(root);
                return;
            }

            MinionAuthoring minion = EnsureComponent<MinionAuthoring>(root);
            minion.damageMultiplier = damageMultiplier;

            // ONLY WHEN THERE IS A MELEE ATTACK TO MINE WITH. Core Keeper's own step prints a
            // console error at conversion time when a minion is told to mine and has no melee
            // attack, and a red line in the console with no name attached to it is worse than
            // useless to somebody building a mod.
            minion.hasMiningAttack = minesToo && HasNamed(root, "MeleeAttackStateAuthoring");
            minion.miningDamageMultiplier = miningDamageMultiplier;

            SayWhenTicked(
                minesToo && !minion.hasMiningAttack,
                report,
                onACreature
                    ? "is a minion that also mines, but it has no melee attack to mine with, and " +
                      "Core Keeper refuses that outright. Give it a melee attack."
                    : "is a minion that also mines, but it has no melee attack to mine with, and " +
                      "Core Keeper refuses that outright. Build it as a creature and give it a " +
                      "melee attack.");

            SayWhenTicked(
                !HasNamed(root, "MeleeAttackStateAuthoring") &&
                !HasNamed(root, "RangeAttackStateAuthoring"),
                report,
                onACreature
                    ? "is a minion with no attack. A minion in Core Keeper is fought through: the " +
                      "game hands it a target and then looks for its melee or ranged attack to " +
                      "use. Give it one under its attacks."
                    : "is a minion with no attack. A minion in Core Keeper is fought through: the " +
                      "game hands it a target and then looks for its melee or ranged attack to " +
                      "use. Build it as a creature, where the attacks are.");
        }

        /// <summary>
        /// Lets a creature drop at zero health and get back up, instead of dying.
        /// </summary>
        /// <remarks>
        /// The same component and the same reasoning as the world object's answer under its rules
        /// (see <see cref="ApplyObjectRules"/>), reachable from a creature, which is where the two
        /// clips it fires are actually worth drawing. <c>AnimateDontDestroyOnZeroHealthSystem</c>
        /// asks for health, the animate component, the animation buffer and its pointer, and a
        /// generated creature has all four before this runs.
        /// </remarks>
        public static void ApplyCreatureLastStand(
            GameObject root,
            DimensionCreatureLastStandTemplate lastStand,
            System.Action<string> report)
        {
            if (root == null || lastStand == null)
            {
                return;
            }

            // ON THE WAY ON ONLY, exactly as the world-object answer does it: the component makes
            // the creature survive zero health, so unticking clears the flag rather than quietly
            // making something mortal that a borrowed kit authored not to be.
            DontDestroyOnZeroHealthAuthoring survives = lastStand.PlaysDeadInsteadOfDying
                ? EnsureComponent<DontDestroyOnZeroHealthAuthoring>(root)
                : root.GetComponent<DontDestroyOnZeroHealthAuthoring>();
            if (survives != null)
            {
                survives.animate = lastStand.PlaysDeadInsteadOfDying;
            }

            SayWhenTicked(
                lastStand.PlaysDeadInsteadOfDying,
                report,
                "plays dead instead of dying. At zero health the game stops short of destroying " +
                "it: it drops, and it stands back up if anything ever heals it. It will not drop " +
                "loot and it will not disappear, because it never actually dies, so something " +
                "else has to take it away. Draw 'Playing dead' and 'Getting back up' for it.");
        }

        public static void ApplyCreatureHabits(
            GameObject root,
            DimensionCreatureHabitsTemplate habits,
            System.Action<string> report)
        {
            if (root == null || habits == null)
            {
                return;
            }

            if (habits.NoticesAPlayerWithin > 0f)
            {
                EnsureComponent<IdleWhenNearbyPlayerStateAuthoring>(root).distanceToStartIdle =
                    habits.NoticesAPlayerWithin;
            }
            else
            {
                RemoveComponentIfPresent<IdleWhenNearbyPlayerStateAuthoring>(root);
            }

            if (habits.PausesInCombatBeyond > 0f)
            {
                IdleInCombatStateAuthoring pause =
                    EnsureComponent<IdleInCombatStateAuthoring>(root);

                // The game stores this SQUARED, because it compares against a squared distance to
                // avoid a square root every frame. An author types tiles; this is where that becomes
                // the number the game actually reads.
                pause.sqrDistanceToLeaveCombat =
                    habits.PausesInCombatBeyond * habits.PausesInCombatBeyond;
                pause.checkDistanceToPlayerFromSpawnPointInsteadOfSelf = habits.MeasuresFromItsNest;
            }
            else
            {
                RemoveComponentIfPresent<IdleInCombatStateAuthoring>(root);
            }

            if (habits.TauntsDuringAFight && !habits.TauntsWithNoAnimations)
            {
                CombatEmoteStateAuthoring taunt = EnsureComponent<CombatEmoteStateAuthoring>(root);
                taunt.emoteInstantlyChance = habits.TauntsImmediatelyChance;
                taunt.minCooldown = habits.MinBetweenTaunts;
                taunt.maxCooldown = habits.MaxBetweenTaunts;
                taunt.emoteAnimations =
                    new System.Collections.Generic.List<CombatEmoteStateAuthoring.CombatEmoteAnimation>();

                DimensionTaunt[] list = habits.Taunts;
                for (int i = 0; i < list.Length; i++)
                {
                    taunt.emoteAnimations.Add(new CombatEmoteStateAuthoring.CombatEmoteAnimation
                    {
                        animation = list[i].Animation,
                        duration = list[i].Seconds,
                        preCombatMinDuration = list[i].MinBeforeCombat,
                        preCombatMaxDuration = list[i].MaxBeforeCombat
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<CombatEmoteStateAuthoring>(root);
                if (habits.TauntsWithNoAnimations && report != null)
                {
                    report("taunts during a fight with no taunt animations listed, so it taunts silently.");
                }
            }

            if (habits.StaysAngryFor > 0f)
            {
                EnsureComponent<OverrideLeaveCombatTimeAuthoring>(root).time = habits.StaysAngryFor;
            }
            else
            {
                RemoveComponentIfPresent<OverrideLeaveCombatTimeAuthoring>(root);
            }

            Toggle<HasSpawnPointAuthoring>(root, habits.KeepsANest);
            Toggle<IsFlyingAuthoring>(root, habits.Flies);
            Toggle<CanClaimBedAuthoring>(root, habits.CanClaimABed);
            Toggle<CattleAuthoring>(root, habits.IsLivestock);

            // SOMEWHERE TO KEEP THE NAME THE TENDING WINDOW ASKS FOR. Everything in the game that
            // gives an animal a name uses `ecb.SetComponent<NameCD>` — the deserializer at
            // `ck-db\Pug.Other\DeserializeComponentsSystem.cs:785`, the cage drop and the aux-data
            // copy at `EntityUtility.cs:160` and `:524` — and SetComponent only writes a component
            // that is already there. `NameConverter` is the one thing that puts it there, and it
            // runs off `NameAuthoring`. Until this line, a generated animal opened a tending window
            // offering to name it and had nowhere to keep the name: `Cattle.GetName` answered null
            // for ever and the floating tag stayed hidden. It rides with being livestock rather
            // than being its own tickbox because the window is the only thing that asks.
            //
            // ONE WRITER, deliberately. The other place in this file that writes NameAuthoring is
            // ApplyObjectRoles, which the creature generator never calls, so nothing here can
            // overwrite what the other one decided for a world object.
            Toggle<NameAuthoring>(root, habits.IsLivestock);
            Toggle<MealsEatenAuthoring>(root, habits.RemembersItsMeals);
            Toggle<PutTargetInCombatOnDealingDamageAuthoring>(
                root,
                habits.DraggingWhatItHurtsIntoTheFight);

            if (habits.GuardsItsNest)
            {
                EnsureComponent<ForceInCombatIfPlayerNearbySpawnPointAuthoring>(root)
                    .distanceToStayInCombat = habits.GuardsWithin;

                // A nest to guard. The game stamps a creature with where its nest IS only when the
                // creature says it keeps one, and the guarding pass then looks for that stamp — so
                // "guards its nest" on its own was two boxes that had to be ticked together and
                // nothing said so. Five of the six creatures in the game that guard a nest keep
                // one as well; this makes it six.
                if (!HasNamed(root, "HasSpawnPointAuthoring"))
                {
                    EnsureComponent<HasSpawnPointAuthoring>(root);
                    SayWhenTicked(
                        true,
                        report,
                        "guards its nest without keeping one, and the game only knows where a " +
                        "nest is for a creature that keeps one. It was generated keeping a nest " +
                        "where it spawns, which is what the game's own nest guards do.");
                }
            }
            else
            {
                RemoveComponentIfPresent<ForceInCombatIfPlayerNearbySpawnPointAuthoring>(root);
            }

            if (habits.GetsFullAt > 0)
            {
                EnsureComponent<FullnessAuthoring>(root).maxFullness = habits.GetsFullAt;
            }
            else
            {
                RemoveComponentIfPresent<FullnessAuthoring>(root);
            }

            if (habits.TakesMoveOrders)
            {
                MoveToPositionFromCommandStateAuthoring orders =
                    EnsureComponent<MoveToPositionFromCommandStateAuthoring>(root);
                orders.belongsToShape =
                    root.GetComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();
            }
            else
            {
                RemoveComponentIfPresent<MoveToPositionFromCommandStateAuthoring>(root);
            }

            if (!string.IsNullOrEmpty(habits.UnlocksAchievement))
            {
                AchievementID unlocked;
                if (System.Enum.TryParse(habits.UnlocksAchievement, false, out unlocked))
                {
                    EnsureComponent<TriggerAchievementOnDeathAuthoring>(root).achievement = unlocked;
                }
                else
                {
                    RemoveComponentIfPresent<TriggerAchievementOnDeathAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "unlocks achievement '" + habits.UnlocksAchievement + "' when killed, " +
                            "which the game does not have, so nothing is unlocked.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<TriggerAchievementOnDeathAuthoring>(root);
            }

            if (habits.GuardsWithoutAskingForANest && report != null)
            {
                report(
                    "guards its nest, so it has been given one to guard — guarding is measured from " +
                    "where it spawned.");
            }
        }

        public static void ApplyObjectRoles(
            GameObject root,
            DimensionObjectRolesTemplate roles,
            System.Action<string> report)
        {
            if (root == null || roles == null)
            {
                return;
            }

            // A CHAIR HAS TO HAVE SOMEWHERE TO SIT. Core Keeper's step that makes an object
            // sittable reaches into the object's picture for the marker that says where the
            // sitter's body goes, and takes the first one by index without checking there is one —
            // so on a chair whose picture has no sit position, the generate throws rather than the
            // chair being unsittable. Every one of the game's thirty chairs, stools, thrones,
            // boats and minecarts has that marker on its picture.
            bool thereIsSomewhereToSit = roles.CanBeSatOn && ThereIsSomewhereToSit(root);
            Toggle<SittableAuthoring>(root, thereIsSomewhereToSit);
            SayWhenTicked(
                roles.CanBeSatOn && !thereIsSomewhereToSit,
                report,
                "can be sat on, and that was not written: its picture has no seat on it, so the " +
                "game has nowhere to put the sitter. A seat is a marker inside the picture saying " +
                "where the body goes and which way it faces, the way the game's own chairs and " +
                "boats carry one. Until the picture has one, this object cannot be sat on.");

            Toggle<NameAuthoring>(root, roles.CanBeNamed);
            Toggle<NonHittableAuthoring>(root, roles.NothingCanHitIt);

            if (roles.CanBePainted)
            {
                EnsureComponent<PaintToolAuthoring>(root).paintIndex = roles.StartingPaint;

                // The label says the paint tool works on it. The component underneath makes the
                // object A PAINT TOOL — all fourteen things in the game that carry it are paint
                // brushes — and being paintable is a different answer the framework already writes
                // on every placed object. Said rather than changed, because taking it off would
                // silently drop the starting paint the author chose.
                SayWhenTicked(
                    true,
                    report,
                    "is set so the paint tool works on it. Everything this framework places can " +
                    "already be painted; what this particular answer does is make the object a " +
                    "paint brush of its own, which is almost certainly not what was wanted. Turn " +
                    "it off unless you are building a brush.");
            }
            else
            {
                RemoveComponentIfPresent<PaintToolAuthoring>(root);
            }

            if (roles.SizeCanBeChanged)
            {
                EnsureComponent<ResizableTileSizeAuthoring>(root).StartOnSmallestSize =
                    roles.StartsSmallest;

                SayWhenTicked(
                    true,
                    report,
                    "is set so its footprint can be changed after it is placed. Core Keeper reads " +
                    "that off the TOOL in the player's hand, not off the thing being placed — all " +
                    "thirteen things in the game with it are hoes, shovels, seeders and watering " +
                    "cans — so a placed object gets a resize button that has nothing to read.");
            }
            else
            {
                RemoveComponentIfPresent<ResizableTileSizeAuthoring>(root);
            }

            if (roles.IsADiscovery)
            {
                EnsureComponent<CanBeDiscoveredAuthoring>(root).distanceToDiscover =
                    roles.DiscoveredWithin;
            }
            else
            {
                RemoveComponentIfPresent<CanBeDiscoveredAuthoring>(root);
            }

            if (roles.StartsImmune == DimensionDamageImmunity.LeaveItAlone)
            {
                RemoveComponentIfPresent<ImmuneToDamageAuthoring>(root);
            }
            else
            {
                ImmuneToDamageAuthoring immune = EnsureComponent<ImmuneToDamageAuthoring>(root);
                immune.defaultValue = (ImmuneToDamageState)(int)roles.StartsImmune;

                EffectID turnedAway;
                if (!string.IsNullOrEmpty(roles.TurnedAwayEffectId) &&
                    System.Enum.TryParse(roles.TurnedAwayEffectId, false, out turnedAway))
                {
                    immune.defaultEffectIDOverride = turnedAway;
                }
                else if (!string.IsNullOrEmpty(roles.TurnedAwayEffectId) && report != null)
                {
                    report(
                        "shows effect '" + roles.TurnedAwayEffectId + "' when something is turned " +
                        "away by its immunity, which the game does not have, so it shows the usual one.");
                }
            }

            if (report == null)
            {
                return;
            }

            if (roles.PaintWillBeIgnored)
            {
                report(
                    "starts painted a colour the paint tool cannot touch, so it stays unpainted.");
            }

            if (roles.DiscoveryDistanceWillBeIgnored)
            {
                report(
                    "sets how close a player must get to discover it without being a discovery at " +
                    "all, so walking past does nothing.");
            }
        }

        public static void ApplyReactsToNearby(
            GameObject root,
            DimensionReactsToNearbyTemplate reacts,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || reacts == null)
            {
                return;
            }

            // ---- an object placed nearby ----
            if (reacts.ReactsToANearbyObject && !reacts.WatchesForNothing)
            {
                ObjectID watched = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(reacts.WatchesForObjectId);
                if (watched == ObjectID.None)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "watches for '" + reacts.WatchesForObjectId + "' being placed near it, " +
                            "which the game does not have, so it never reacts.");
                    }
                }
                else
                {
                    ChangeVariationWhenObjectNearbyAuthoring nearby =
                        EnsureComponent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                    nearby.objectID = watched;
                    nearby.objectNearbySpecificVariation = reacts.OnlyOneLookOfIt;
                    nearby.objectNearbyVariation = reacts.ItsLook;
                    nearby.radius = reacts.WithinDistance;
                    nearby.variationToChangeTo = reacts.ChangesToLook;
                    nearby.dontRevertToOriginalVariation = reacts.StaysChanged;
                    nearby.triggerActivateAnimation = reacts.PlaysItsActivationAnimation;
                    nearby.ignorePlayerFaction = reacts.AnyonesObjectCounts;
                    nearby.offset = new Unity.Mathematics.float3(
                        reacts.LooksAtOffset.x,
                        reacts.LooksAtOffset.y,
                        reacts.LooksAtOffset.z);
                }
            }
            else
            {
                RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
            }

            // ---- an object merely held ----
            if (reacts.ReactsToAHeldObject && !reacts.WatchesForNothingHeld)
            {
                ObjectID held = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(reacts.WatchesForHeldObjectId);
                if (held == ObjectID.None)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "reacts to a player holding '" + reacts.WatchesForHeldObjectId +
                            "', which the game does not have, so it never reacts.");
                    }
                }
                else
                {
                    ChangeVariationWhenPlayerHoldObjectNearbyAuthoring holding =
                        EnsureComponent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                    holding.objectID = held;
                    holding.radius = reacts.HeldWithinDistance;
                    holding.variationToChangeTo = reacts.HeldChangesToLook;
                    holding.alsoRemoveCollider = reacts.AlsoClearsItsCollider;
                    holding.offset = new Unity.Mathematics.float3(
                        reacts.HeldLooksAtOffset.x,
                        reacts.HeldLooksAtOffset.y,
                        reacts.HeldLooksAtOffset.z);
                }
            }
            else
            {
                RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
            }

            // ---- whether it can be used at all ----
            if (reacts.UsableDependsOnItsLook)
            {
                ToggleInteractionOnVariationAuthoring toggle =
                    EnsureComponent<ToggleInteractionOnVariationAuthoring>(root);
                toggle.toggleType = (ToggleInteractionByVariationType)(int)reacts.UsableRule;
                toggle.variation = reacts.TheLookInQuestion;
            }
            else
            {
                RemoveComponentIfPresent<ToggleInteractionOnVariationAuthoring>(root);
            }

            // ---- shoving ----
            if (reacts.ShovesNearbyThings && !reacts.ShovesWithNoForce)
            {
                AddForceToNearbyEntitiesAuthoring shove =
                    EnsureComponent<AddForceToNearbyEntitiesAuthoring>(root);
                shove.radius = reacts.ShoveReaches;
                shove.force = reacts.ShoveForce;
                shove.forceDuringActivation = reacts.ShoveForceWhileWindingUp;
                shove.checkLineOfSight = reacts.OnlyShovesWhatItCanSee;
                shove.activationDelay = reacts.ShoveWindUp;
                shove.activeDuration = reacts.ShoveLasts;
                shove.inactiveDuration = reacts.RestsBetweenShoves;
                shove.activeForceMultiplierCurve = reacts.ShoveStrengthOverTime;
            }
            else
            {
                RemoveComponentIfPresent<AddForceToNearbyEntitiesAuthoring>(root);
                if (reacts.ShovesWithNoForce && report != null)
                {
                    report(
                        "is set to shove things away with no force behind it, so nothing moves. " +
                        "Give it a shove force.");
                }
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

        /// <summary>How long one pass of the game's own farm arm takes, and its rest after.</summary>
        /// <remarks>
        /// Used only as a floor when the author has not set a time at all, because nothing is not
        /// "instant" to the job that reads it — it is a timer with nothing to count.
        /// </remarks>
        private const float VanillaFarmArmMoveSeconds = 0.5f;

        /// <summary>The pause between passes.</summary>
        private const float VanillaFarmArmRestSeconds = 0.5f;

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

        public static void ApplyExtractable(
            GameObject root,
            DimensionExtractableTemplate extractable,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || extractable == null || !extractable.YieldsAnything)
            {
                RemoveComponentIfPresent<ExtractableAuthoring>(root);
                if (extractable != null && extractable.ExtractionTimeWillBeIgnored && report != null)
                {
                    report(
                        "sets an extraction time without listing anything a machine can pull out of " +
                        "it, so machines get nothing from it.");
                }

                return;
            }

            ExtractableAuthoring authored = EnsureComponent<ExtractableAuthoring>(root);
            authored.craftingTimeOverride = extractable.ExtractionSecondsOverride;
            authored.extractedObject =
                new System.Collections.Generic.List<ExtractableAuthoring.ExtractableOutput>();

            DimensionExtractableOutput[] yields = extractable.Yields;
            for (int i = 0; i < yields.Length; i++)
            {
                ObjectID pulled = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(yields[i].ObjectId);
                if (pulled == ObjectID.None)
                {
                    if (report != null)
                    {
                        report(
                            "yields '" + yields[i].ObjectId + "' to a machine, which the game does " +
                            "not have, so that one is left out.");
                    }

                    continue;
                }

                authored.extractedObject.Add(new ExtractableAuthoring.ExtractableOutput
                {
                    objectID = pulled,
                    variation = yields[i].Variation,
                    minMaxRandomAmountOverride = yields[i].AmountRange
                });
            }

            if (authored.extractedObject.Count == 0)
            {
                RemoveComponentIfPresent<ExtractableAuthoring>(root);
            }
        }

        /// <summary>
        /// Makes a creature a pet: it follows its owner, fights alongside them, and buffs them.
        /// </summary>
        /// <remarks>
        /// The walk state comes with it. A pet paths to its owner rather than to a target, so the
        /// ordinary chase is the wrong movement — a pet without <c>PetWalkStateAuthoring</c> stands
        /// where it was summoned and never follows anybody.
        /// </remarks>
        public static void ApplyPet(
            GameObject root,
            DimensionPetTemplate pet,
            System.Action<string> report)
        {
            if (root == null || pet == null || !pet.IsAPet)
            {
                RemoveComponentIfPresent<PetAuthoring>(root);
                RemoveComponentIfPresent<PetWalkStateAuthoring>(root);
                if (pet != null && pet.TalentsWillBeIgnored && report != null)
                {
                    report(
                        "lists pet talents without being a pet, so none of them reach a player.");
                }

                return;
            }

            PetAuthoring authored = EnsureComponent<PetAuthoring>(root);
            authored.petType = (PetType)(int)pet.FightsBy;
            authored.isFlying = pet.Flies;
            authored.happyAnimDuration = pet.HappyAnimationSeconds;

            authored.petTalents = new System.Collections.Generic.List<PetTalent>();
            string[] talents = pet.Talents;
            for (int i = 0; i < talents.Length; i++)
            {
                PetTalent talent;
                if (System.Enum.TryParse(talents[i], false, out talent))
                {
                    authored.petTalents.Add(talent);
                }
                else if (report != null)
                {
                    report(
                        "has pet talent '" + talents[i] + "', which the game does not have, so that " +
                        "one gives its owner nothing.");
                }
            }

            PetWalkStateAuthoring walk = EnsureComponent<PetWalkStateAuthoring>(root);
            walk.belongsToShape = root.GetComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();
        }

        /// <summary>
        /// Makes an object switch on and off with electricity, and change while it runs.
        /// </summary>
        public static void ApplyPoweredMachine(
            GameObject root,
            DimensionPoweredMachineTemplate machine,
            System.Action<string> report)
        {
            if (root == null || machine == null || !machine.ReactsToBeingPowered)
            {
                RemoveComponentIfPresent<ActivatedByElectricityStateAuthoring>(root);
                return;
            }

            ActivatedByElectricityStateAuthoring powered =
                EnsureComponent<ActivatedByElectricityStateAuthoring>(root);
            powered.activationTime = machine.SwitchOnSeconds;
            powered.deactivationTime = machine.SwitchOffSeconds;
            powered.changeVariationOnActivate = machine.ChangesLookWhileRunning;
            powered.variationToChangeTo = machine.RunningVariation;
            powered.changeColliderToTriggerWhenActivated = machine.StopsBlockingWhileRunning;
            powered.triggerBelongsTo = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)machine.TriggerBelongsToLayers
            };
            powered.triggerCollidesWith = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)machine.TriggerNoticesLayers
            };

            if (report == null)
            {
                return;
            }

            if (machine.RunningLookWillBeIgnored)
            {
                report(
                    "names a look to wear while running without being told to change look, so it " +
                    "looks the same powered or not.");
            }

            if (machine.TriggerLayersWillBeIgnored)
            {
                report(
                    "sets trigger layers on a machine whose collider never changes, so those " +
                    "layers are never read.");
            }
        }

        /// <summary>
        /// Makes an object keep ground it can sit on underneath itself.
        /// </summary>
        public static void ApplyKeepsItsFloor(
            GameObject root,
            DimensionKeepsItsFloorTemplate floor,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || floor == null || !floor.KeepsItsOwnFloor)
            {
                RemoveComponentIfPresent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
                return;
            }

            if (floor.HasNoFallback)
            {
                RemoveComponentIfPresent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
                if (report != null)
                {
                    report(
                        "keeps its own floor without saying what to lay down, so it would insist on " +
                        "ground it supports with nothing to put there. Give it a fallback tileset.");
                }

                return;
            }

            int fallback = resolveTileset == null ? -1 : resolveTileset(floor.FallbackTilesetId);
            if (fallback < 0)
            {
                RemoveComponentIfPresent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
                if (report != null)
                {
                    report(
                        "lays down '" + floor.FallbackTilesetId + "' beneath itself, which is " +
                        "neither one of this mod's tilesets nor one of the game's, so it keeps no " +
                        "floor at all.");
                }

                return;
            }

            EnsureSameGroundTileBeneathEntityAuthoring keeps =
                EnsureComponent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
            keeps.tileType = floor.TileKindBeneath;
            keeps.fallbackTileset = (PugTilemap.Tileset)fallback;
            keeps.continouslyCheck = floor.KeepsChecking;

            keeps.onlySupportsTilesets = new System.Collections.Generic.List<PugTilemap.Tileset>();
            string[] happy = floor.HappyOnTilesets;
            for (int i = 0; i < happy.Length; i++)
            {
                int supported = resolveTileset == null ? -1 : resolveTileset(happy[i]);
                if (supported >= 0)
                {
                    keeps.onlySupportsTilesets.Add((PugTilemap.Tileset)supported);
                }
                else if (report != null)
                {
                    report(
                        "is happy sitting on '" + happy[i] + "', which is not a tileset, so it will " +
                        "replace that ground instead of leaving it alone.");
                }
            }

            StateID ignoreIn;
            keeps.ignoreCheckingWhileInState =
                !string.IsNullOrEmpty(floor.StopsCheckingInState) &&
                System.Enum.TryParse(floor.StopsCheckingInState, false, out ignoreIn);
            if (keeps.ignoreCheckingWhileInState)
            {
                System.Enum.TryParse(floor.StopsCheckingInState, false, out ignoreIn);
                keeps.stateToIgnore = ignoreIn;
            }
            else if (!string.IsNullOrEmpty(floor.StopsCheckingInState) && report != null)
            {
                report(
                    "stops keeping its floor in state '" + floor.StopsCheckingInState + "', which " +
                    "the game does not have, so it always keeps it.");
            }
        }

        /// <summary>
        /// The one writer of <c>AffectObjectWhenMelodyPlayedAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// TWO AUTHORING BLOCKS, ONE COMPONENT. A world object can say "this opens to a melody" in
        /// its gate block and "this responds to a melody" in its melody block, and Core Keeper
        /// carries both on the same component. They used to be written by two passes in a row:
        /// the gate wrote the listener, then this removed it again whenever the melody block was
        /// empty. A door authored to open on a tune therefore never opened, and the whole gate
        /// melody surface was dead unless an unrelated block happened to be filled in as well.
        /// </para>
        /// <para>
        /// So the gate's tunes arrive here instead. Both lists are heard; where the two blocks
        /// answer the same question, the melody block wins because it is the more detailed one,
        /// and the clash is reported rather than silently resolved.
        /// </para>
        /// </remarks>
        public static void ApplyMelodyResponse(
            GameObject root,
            DimensionMelodyResponseTemplate melody,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null,
            DimensionGateTemplate gate = null)
        {
            System.Collections.Generic.List<MelodyID> gateTunes =
                new System.Collections.Generic.List<MelodyID>();
            if (root != null && gate != null)
            {
                string[] gateNames = gate.OpensToMelodies;
                for (int i = 0; i < gateNames.Length; i++)
                {
                    MelodyID gateTune;
                    if (!string.IsNullOrEmpty(gateNames[i]) &&
                        System.Enum.TryParse(gateNames[i], false, out gateTune) &&
                        gateTune != MelodyID.None)
                    {
                        gateTunes.Add(gateTune);
                    }
                    else if (!string.IsNullOrEmpty(gateNames[i]) && report != null)
                    {
                        report(
                            "opens to melody '" + gateNames[i] + "', which the game does not " +
                            "have, so that tune does nothing.");
                    }
                }
            }

            if (root == null || ((melody == null || !melody.HasAnySetting) && gateTunes.Count == 0))
            {
                RemoveComponentIfPresent<AffectObjectWhenMelodyPlayedAuthoring>(root);
                return;
            }

            if (melody == null || !melody.HasAnySetting)
            {
                ApplyGateMelodyOnly(root, gate, gateTunes, resolveObject, report);
                return;
            }

            AffectObjectWhenMelodyPlayedAuthoring listener =
                EnsureComponent<AffectObjectWhenMelodyPlayedAuthoring>(root);

            listener.melodyIDList = new System.Collections.Generic.List<MelodyID>();
            string[] names = melody.Melodies;
            for (int i = 0; i < names.Length; i++)
            {
                MelodyID tune;
                if (System.Enum.TryParse(names[i], false, out tune) && tune != MelodyID.None)
                {
                    listener.melodyIDList.Add(tune);
                }
                else if (report != null)
                {
                    report(
                        "listens for melody '" + names[i] + "', which the game does not have, so " +
                        "that one will never reach it.");
                }
            }

            // The gate's tunes join the list rather than replacing it or being replaced by it.
            for (int i = 0; i < gateTunes.Count; i++)
            {
                if (!listener.melodyIDList.Contains(gateTunes[i]))
                {
                    listener.melodyIDList.Add(gateTunes[i]);
                }
            }

            if (gateTunes.Count > 0 && report != null &&
                !string.IsNullOrEmpty(gate.MelodyTurnsItInto) &&
                melody.BecomesADifferentObject)
            {
                report(
                    "opens into '" + gate.MelodyTurnsItInto + "' in its gate settings and turns " +
                    "into '" + melody.BecomesObjectId + "' in its melody settings. An object can " +
                    "only become one thing, so the melody settings are used. Clear one of the two.");
            }

            // THE OTHER FIELD BOTH BLOCKS ANSWER, and it went unsaid. The look a gate opens to and
            // the look a melody response changes to are one field on the component
            // (newVariation), and the melody block's answer is written below whatever the gate
            // asked for. Only the "becomes a different object" clash was reported, so an author who
            // set an open look in the gate block and any variation in the melody block watched the
            // door open to the wrong picture with nothing said.
            if (gateTunes.Count > 0 && report != null &&
                gate.MelodyOpenLook != 0 &&
                gate.MelodyOpenLook != melody.BecomesVariation)
            {
                report(
                    "opens to look " + gate.MelodyOpenLook + " in its gate settings and changes to " +
                    "look " + melody.BecomesVariation + " in its melody settings. An object has " +
                    "one look at a time, so the melody settings are used. Clear one of the two.");
            }

            if (listener.melodyIDList.Count == 0)
            {
                RemoveComponentIfPresent<AffectObjectWhenMelodyPlayedAuthoring>(root);
                if (report != null)
                {
                    report(
                        "listens for melodies the game does not have, so it has been left as an " +
                        "ordinary object rather than one that listens for nothing.");
                }

                return;
            }

            listener.hearRange = melody.HearingRange;
            listener.humCooldown = melody.HumCooldown;
            listener.listening = melody.StartsListening;
            listener.weakenWhenAffected = melody.WeakensWhenItHears;
            listener.newVariation = melody.BecomesVariation;
            listener.removeMelodyListener = melody.OnlyRespondsOnce;
            listener.removeOldColliders = melody.ClearsItsOldColliders;

            listener.changeObjectID = melody.BecomesADifferentObject && !melody.BecomesNothing;
            listener.newObjectId = ObjectID.None;
            if (listener.changeObjectID)
            {
                listener.newObjectId = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(melody.BecomesObjectId);
                if (listener.newObjectId == ObjectID.None &&
                    !(isDeferred != null && isDeferred(melody.BecomesObjectId)))
                {
                    listener.changeObjectID = false;
                    if (report != null)
                    {
                        report(
                            "turns into '" + melody.BecomesObjectId + "' when it hears its melody, " +
                            "which is neither one of this mod's objects nor one the game has, so " +
                            "it only changes its look.");
                    }
                }

                // changeObjectID STAYS TRUE for one of the mod's own. The converter writes
                // AffectObjectWhenMelodyPlayedCD either way, and the link hydration fills the id in
                // at load — but the flag is what makes the game read that id at all.
            }

            LootTableID table;
            if (!string.IsNullOrEmpty(melody.LootTableId) &&
                DimensionEditorLootTables.TryResolve(melody.LootTableId, out table))
            {
                listener.tableLoot = table;
            }
            else if (!string.IsNullOrEmpty(melody.LootTableId) && report != null)
            {
                report(
                    "rolls loot table '" + melody.LootTableId + "' when it hears its melody, which " +
                    "is neither the game's nor this mod's, so it gives nothing from a table.");
            }

            listener.customLoot = new System.Collections.Generic.List<ObjectData>();
            DimensionMelodyReward[] contents = melody.Contents;
            for (int i = 0; i < contents.Length; i++)
            {
                ObjectID held = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(contents[i].ObjectId);
                if (held == ObjectID.None)
                {
                    if (report != null)
                    {
                        // ONE OF THE MOD'S OWN IS NOT A MISTAKE, and it used to be told it was.
                        // What a melody leaves behind is a baked ObjectData list on the prefab and
                        // there is no field for a load-time pass to fill, so the entry really is
                        // lost — but the author needs the reason and the way round, not an
                        // accusation about an item they spelled correctly.
                        bool oursWithNoNumberYet =
                            isDeferred != null && isDeferred(contents[i].ObjectId);
                        report(oursWithNoNumberYet
                            ? "holds '" + contents[i].ObjectId + "' after it changes, one of your " +
                              "own items. What a melody leaves behind is fixed when the game " +
                              "builds its objects, before your items have numbers, so that one " +
                              "cannot go in there. Use one of the game's items, or give it a loot " +
                              "table instead."
                            : "holds '" + contents[i].ObjectId + "' after it changes, which is " +
                              "neither one of this mod's items nor one the game has, so that one " +
                              "is left out. Check the spelling.");
                    }

                    continue;
                }

                listener.customLoot.Add(new ObjectData
                {
                    objectID = held,
                    amount = contents[i].Amount,
                    variation = contents[i].Variation
                });
            }

            if (report == null)
            {
                return;
            }

            if (melody.BecomesNothing)
            {
                report(
                    "is set to become a different object when it hears its melody without naming " +
                    "which, so it only changes its look.");
            }

            if (melody.ObjectChangeWillBeIgnored)
            {
                report(
                    "names an object to become when it hears its melody without being told to " +
                    "become one, so it only changes its look.");
            }

            if (melody.LeavesItsOldCollidersBehind)
            {
                report(
                    "turns into a different object without clearing its old colliders, so an " +
                    "invisible wall will be left standing where it was.");
            }
        }

        /// <summary>
        /// The melody listener for an object whose only melody authoring is its gate block.
        /// </summary>
        /// <remarks>
        /// Exactly what the gate pass used to write, moved here so one pass owns the component.
        /// A gate hears its tune, opens to a look, and may become a different object.
        /// </remarks>
        private static void ApplyGateMelodyOnly(
            GameObject root,
            DimensionGateTemplate gate,
            System.Collections.Generic.List<MelodyID> gateTunes,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            AffectObjectWhenMelodyPlayedAuthoring listener =
                EnsureComponent<AffectObjectWhenMelodyPlayedAuthoring>(root);
            listener.melodyIDList = gateTunes;
            listener.listening = true;
            listener.hearRange = gate.HearingRange;
            listener.newVariation = gate.MelodyOpenLook;
            listener.removeOldColliders = true;

            ObjectID becomes = string.IsNullOrEmpty(gate.MelodyTurnsItInto) || resolveObject == null
                ? ObjectID.None
                : resolveObject(gate.MelodyTurnsItInto);
            listener.changeObjectID = becomes != ObjectID.None;
            listener.newObjectId = becomes;
            if (becomes == ObjectID.None && !string.IsNullOrEmpty(gate.MelodyTurnsItInto) &&
                report != null)
            {
                report(
                    "should turn into '" + gate.MelodyTurnsItInto + "' when its melody plays, " +
                    "which is not a known object, so it changes its look but stays itself.");
            }
        }

        public static void ApplyExtraLoot(
            GameObject root,
            DimensionExtraLootTemplate extra,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || extra == null)
            {
                return;
            }

            // Unlike the other shape templates this one MAY add the component. An object can drop
            // nothing on death and still shed ore as you mine it, and refusing to add DropLoot here
            // would make that object unauthorable.
            bool wantsAnyChannel = extra.DropsLootAsItIsHit
                || extra.DropsLootWhenUsed
                || extra.DropsDifferentLootInSeason;
            DropLootAuthoring loot = root.GetComponent<DropLootAuthoring>();
            if (loot == null)
            {
                if (!wantsAnyChannel)
                {
                    return;
                }

                loot = EnsureComponent<DropLootAuthoring>(root);
            }

            // ---- as it is hit ----
            loot.hasLootDropsOnTakingDamage = extra.DropsLootAsItIsHit && !extra.ShedsNothing;
            if (loot.hasLootDropsOnTakingDamage)
            {
                // LootDropsWhenDamaged is a struct, so the field is always there to write into.
                loot.lootDropsWhenDamaged.dropsLoot = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(extra.ShedsObjectId);
                loot.lootDropsWhenDamaged.damageToDealToDropLoot = extra.DamageNeededToShed;
                loot.lootDropsWhenDamaged.healthPercentageDamageToDeal =
                    extra.HealthShareNeededToShed;
                loot.lootDropsWhenDamaged.minHealthToDropLoot = extra.StopsSheddingBelowHealth;
                loot.lootDropsWhenDamaged.minHealthPercentageToDropLoot =
                    extra.StopsSheddingBelowHealthShare;
                loot.lootDropsWhenDamaged.instantiateEntity = extra.ShedsALiveObject;
                loot.lootDropsWhenDamaged.minSpawnOffset = new Unity.Mathematics.float2(
                    extra.ShedLandsFrom.x,
                    extra.ShedLandsFrom.y);
                loot.lootDropsWhenDamaged.maxSpawnOffset = new Unity.Mathematics.float2(
                    extra.ShedLandsTo.x,
                    extra.ShedLandsTo.y);
                loot.lootDropsWhenDamaged.maxLimitToDropInNearbyArea =
                    extra.StopsAfterThisManyNearby;

                bool shedIsOneOfOurs =
                    loot.lootDropsWhenDamaged.dropsLoot == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(extra.ShedsObjectId);

                if (loot.lootDropsWhenDamaged.dropsLoot == ObjectID.None && !shedIsOneOfOurs)
                {
                    loot.hasLootDropsOnTakingDamage = false;
                    if (report != null)
                    {
                        report(
                            "sheds '" + extra.ShedsObjectId + "' as it is hit, which is neither " +
                            "one of this mod's objects nor one the game has, so it sheds nothing.");
                    }
                }
                else if (root.GetComponent<HealthAuthoring>() == null)
                {
                    // NO HEALTH, NO SHED — for a vanilla target as much as for one of ours.
                    // DropLootConverter works out how much damage a shed costs from the health
                    // pool, and with none it logs a red error and returns before writing the shed
                    // (ck-db\Pug.ECS.Conversion\DropLootConverter.cs:98-104). Only the shed itself
                    // is lost — death loot, the loot table, seasonal loot and on-use loot are all
                    // written before that return — but the error names the creator's prefab and
                    // the shed never happens either way, so it is switched off here with a sentence
                    // that says what to do. The check used to fire only for one of our own targets,
                    // which left the common case hitting the game's error with nothing said.
                    loot.hasLootDropsOnTakingDamage = false;
                    if (report != null)
                    {
                        report(
                            "sheds '" + extra.ShedsObjectId + "' as it is hit but has no health " +
                            "pool, and the game works out how much damage a shed costs from that. " +
                            "Give it health, or make it breakable, and generate again.");
                    }
                }

                // The flag STAYS TRUE for one of the mod's own objects. DropLootConverter writes
                // DropsLootWhenDamagedCD only while hasLootDropsOnTakingDamage is set
                // (ck-db\Pug.ECS.Conversion\DropLootConverter.cs:98), so turning it off here leaves
                // no component for the link hydration to fill in and the shed is lost for good.
            }

            // ---- when it is used ----
            loot.hasLootDropsOnUse = extra.DropsLootWhenUsed && !extra.GivesNothingOnUse;
            if (loot.hasLootDropsOnUse)
            {
                // OnUseLootDrops is a struct too.
                // THE TABLE IS WRITTEN EVERY TIME, THE CLEARED ONE INCLUDED. It used to be written
                // only inside the guard, with no else, so an author who set a table, generated,
                // then cleared the table while keeping items in "gives on use" went on handing out
                // the old table forever. A misspelling took the same road and said nothing.
                LootTableID useTable;
                if (string.IsNullOrEmpty(extra.UseLootTableId))
                {
                    loot.onUseLootDrops.lootTableID = default(LootTableID);
                }
                else if (DimensionEditorLootTables.TryResolve(extra.UseLootTableId, out useTable))
                {
                    loot.onUseLootDrops.lootTableID = useTable;
                }
                else
                {
                    loot.onUseLootDrops.lootTableID = default(LootTableID);
                    if (report != null)
                    {
                        report(
                            "gives loot table '" + extra.UseLootTableId + "' when used, which is " +
                            "neither one of this mod's tables nor one the game has, so using it " +
                            "hands out nothing from a table.");
                    }
                }

                EffectID useEffect;
                if (string.IsNullOrEmpty(extra.UseEffectId))
                {
                    loot.onUseLootDrops.spawnEffects = default(EffectID);
                }
                else if (System.Enum.TryParse(extra.UseEffectId, false, out useEffect))
                {
                    loot.onUseLootDrops.spawnEffects = useEffect;
                }
                else
                {
                    loot.onUseLootDrops.spawnEffects = default(EffectID);
                    if (report != null)
                    {
                        report(
                            "shows '" + extra.UseEffectId + "' when used, which is not an effect " +
                            "the game has, so nothing is shown.");
                    }
                }

                loot.onUseLootDrops.lootDrops =
                    new System.Collections.Generic.List<OnUseLootDrop>();
                DimensionLootEntry[] onUse = extra.GivesOnUse;
                for (int i = 0; i < onUse.Length; i++)
                {
                    ObjectID given = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(onUse[i].ObjectId);
                    if (given == ObjectID.None)
                    {
                        if (report != null)
                        {
                            report(
                                "gives '" + onUse[i].ObjectId + "' when used, which the game does " +
                                "not have, so that one is left out.");
                        }

                        continue;
                    }

                    loot.onUseLootDrops.lootDrops.Add(new OnUseLootDrop
                    {
                        lootDropID = given,
                        amount = onUse[i].Amount,
                        chance = onUse[i].Chance
                    });
                }
            }

            // ---- by season ----
            loot.hasSeasonalLoot = extra.DropsDifferentLootInSeason && !extra.SeasonHasNoDrops;
            if (loot.hasSeasonalLoot)
            {
                Season whichSeason;
                if (!System.Enum.TryParse(extra.Season, false, out whichSeason))
                {
                    loot.hasSeasonalLoot = false;
                    if (report != null)
                    {
                        report(
                            "drops seasonal loot during '" + extra.Season + "', which is not a " +
                            "season the game has, so its seasonal drops never happen.");
                    }
                }
                else
                {
                    if (loot.seasonalLootDrops == null)
                    {
                        loot.seasonalLootDrops = new SeasonAndLoots();
                    }

                    SeasonAndLoot forThisSeason = new SeasonAndLoot
                    {
                        season = whichSeason,
                        lootDrops = new System.Collections.Generic.List<SeasonalLootDrop>()
                    };

                    DimensionLootEntry[] seasonal = extra.SeasonalDrops;
                    for (int i = 0; i < seasonal.Length; i++)
                    {
                        ObjectID dropped = resolveObject == null
                            ? ObjectID.None
                            : resolveObject(seasonal[i].ObjectId);
                        if (dropped == ObjectID.None)
                        {
                            if (report != null)
                            {
                                report(
                                    "drops '" + seasonal[i].ObjectId + "' in season, which the " +
                                    "game does not have, so that one is left out.");
                            }

                            continue;
                        }

                        forThisSeason.lootDrops.Add(new SeasonalLootDrop
                        {
                            lootDropID = dropped,
                            amount = seasonal[i].Amount,
                            chance = seasonal[i].Chance,
                            multiplayerAmountAdditionScaling = seasonal[i].ExtraPerPlayer
                        });
                    }

                    loot.seasonalLootDrops.lootDrops =
                        new System.Collections.Generic.List<SeasonAndLoot> { forThisSeason };
                }
            }

            if (report == null)
            {
                return;
            }

            if (extra.ShedsWithNoThreshold)
            {
                report(
                    "sheds loot as it is hit with no damage threshold to shed at, so every point " +
                    "of damage sheds one. Give it a damage amount or a health share.");
            }

            if (extra.GivesNothingOnUse)
            {
                report(
                    "is set to give something when used with neither a loot table nor a list of " +
                    "what to give, so using it does nothing.");
            }

            if (extra.SeasonHasNoDrops)
            {
                report("names a season to drop different loot in with nothing to drop in it.");
            }
        }

        public static void ApplyPlacementRules(
            GameObject root,
            DimensionPlacementRulesTemplate rules,
            System.Action<string> report)
        {
            if (root == null || rules == null)
            {
                return;
            }

            PlaceableObjectAuthoring placeable = root.GetComponent<PlaceableObjectAuthoring>();
            if (placeable == null)
            {
                return;
            }

            placeable.canBePlacedOnBlockingObjects = rules.CanGoOverBlockingObjects;
            placeable.canBePlacedOnImmuneTiles = rules.CanGoOnProtectedTiles;
            placeable.canBePlacedOnLowColliders = rules.CanGoOnLowObstacles;
            placeable.canBePlacedOnLava = rules.CanGoOnLava;

            placeable.canPlaceOnSideOfWall = rules.CanGoOnTheSideOfAWall;
            placeable.hasVariationsThatCanBePlacedOnWalls = rules.HasAWallFacingLook;
            placeable.wallSideVariationStartsOnIndex1 = rules.WallLookStartsAtVariationOne;
            placeable.blocksHangingWallObjects = rules.ClaimsTheWallItIsOn;

            placeable.alignWithPlayerDirection = rules.FacesThePlayersDirection;
            placeable.variationToPlace = rules.VariationPlaced;
            placeable.objectCanBeToggledToNewNonRotationOption = rules.CanBeCycledThroughOtherLooks;
            placeable.toggledToNewNonRotationOptions = rules.HowManyOtherLooks;
            placeable.dontDestroyObjectIfInvalidPlacement = rules.ABadPlacementDoesNotDestroyIt;

            placeable.dontBlockRoots = rules.RootsStillGrowThrough;
            placeable.displayPlaceableType = (DisplayPlaceableType)(int)rules.PlacementPreview;

            if (report == null)
            {
                return;
            }

            if (rules.GoesOnWallsWithNoWallLook)
            {
                report(
                    "can be put on the side of a wall but has no wall-facing look, so it will snap " +
                    "onto the wall and then draw its floor sprite.");
            }

            if (rules.HasAWallLookItCannotUse)
            {
                report(
                    "has a wall-facing look but is not allowed on the side of a wall, so that look " +
                    "can never appear.");
            }

            if (rules.CycleWillBeIgnored)
            {
                report(
                    "lists other looks to cycle through without letting the player cycle it, so " +
                    "only the first is ever seen.");
            }

            if (rules.CycleHasNothingInIt)
            {
                report(
                    "lets the player cycle its look with nothing to cycle to, so the control does " +
                    "nothing when they use it.");
            }
        }

        public static void ApplyMortarBarrage(
            GameObject root,
            DimensionMortarBarrageTemplate barrage,
            System.Action<string> report)
        {
            if (root == null || barrage == null)
            {
                return;
            }

            ShootMortarProjectileStateAuthoring mortar =
                root.GetComponent<ShootMortarProjectileStateAuthoring>();
            if (mortar == null)
            {
                return;
            }

            mortar.minAmountOfProjectiles = barrage.FewestShells;
            mortar.maxAmountOfProjectiles = barrage.MostShells;
            mortar.maxProjectilesShotPerWave = barrage.ShellsPerWave;
            mortar.maxProjectilesShotPerWaveMultiplier = barrage.ShellsPerWaveMultiplier;
            mortar.timeBetweenProjectiles = barrage.TimeBetweenShells;
            mortar.dontAllowOverlappingShots = barrage.NeverOverlapsShots;

            mortar.lineFromShooterToTarget = barrage.LandsInALineTowardsTheTarget;
            mortar.lineBendTowardTarget = barrage.TheLineBendsToFollow;
            mortar.lineLengthMultiplier = barrage.LineLengthMultiplier;
            mortar.minRandomSpreadDistance = barrage.ScatterFrom;
            mortar.maxRandomSpreadDistance = barrage.ScatterTo;
            mortar.shootAtSelf = barrage.ShellsLandOnItself;

            mortar.goUpTime = barrage.RiseTime;
            mortar.airTime = barrage.HangTime;
            mortar.goDownTime = barrage.FallTime;
            mortar.explodeTime = barrage.FuseTime;

            mortar.minDistanceToShoot = barrage.FiresFromAtLeast;
            mortar.maxHealthRatioToShoot = barrage.OnlyFiresBelowHealth;
            mortar.keepShootingUntilTakingDamageXTimes = barrage.KeepsFiringUntilHitThisManyTimes;
            mortar.onlyShootWhenInCombat = barrage.OnlyFiresWhenInCombat;
            mortar.skipVisibilityCheck = barrage.FiresAtWhatItCannotSee;
            mortar.dontInterruptOtherAttackStates = barrage.DoesNotInterruptItsOtherAttacks;

            mortar.damageMultiplier = barrage.HitsThisHardForItsTier;
            mortar.tileDamageMultiplier = barrage.BreaksTerrainThisHardForItsTier;
            mortar.hitTiles = barrage.ShellsBreakTerrain;
            mortar.mortarTileDamage = barrage.FlatTerrainDamage;

            mortar.overrideAnimID = barrage.AnimationName;
            mortar.playAttackFireAnimation = barrage.PlaysTheFiringAnimation;

            if (report == null)
            {
                return;
            }

            if (barrage.LineSettingsWillBeIgnored)
            {
                report(
                    "sets up a line barrage without switching the line on, so its shells scatter " +
                    "at random instead of walking out towards the target.");
            }

            if (barrage.LineHasNoLength)
            {
                report(
                    "fires its barrage in a line with no length, so every shell lands on the same " +
                    "spot. Give the line a length multiplier for it to walk out.");
            }
        }

        public static void ApplyRangedShape(
            GameObject root,
            DimensionRangedShapeTemplate shape,
            System.Action<string> report)
        {
            if (root == null || shape == null)
            {
                return;
            }

            RangeAttackStateAuthoring ranged = root.GetComponent<RangeAttackStateAuthoring>();
            if (ranged == null)
            {
                return;
            }

            ranged.damageMultiplier = shape.ShotsHitThisHardForItsTier;
            ranged.speedMultiplier = shape.ProjectileSpeedMultiplier;

            ranged.spawnAtDistanceInfront = shape.MuzzleDistance;
            ranged.spawnAtDistanceInfrontDeviation = shape.MuzzleDistanceVariation;
            ranged.spawnOffset = new Unity.Mathematics.float3(
                shape.MuzzleOffset.x,
                shape.MuzzleOffset.y,
                shape.MuzzleOffset.z);
            ranged.spawnDirectionType = shape.FiresAtAnyAngle
                ? ProjectileSpawnDirectionType.Free
                : ProjectileSpawnDirectionType.HorizontalAndVertical;

            ranged.aimDegreesMax = shape.AimConeDegrees;
            ranged.allowReAimingWhileShooting = shape.KeepsAimingWhileShooting;
            ranged.dontAllowReAimingDuringAntipation = shape.LocksAimDuringTheWindUp;
            ranged.minExtrapolatedAimDistance = shape.StartsLeadingTargetsAt;
            ranged.maxExtrapolatedAimDistance = shape.StopsLeadingTargetsAt;

            ranged.spreadType = (ProjectileSpreadType)(int)shape.ShotPattern;
            ranged.maxSpreadAngle = shape.WidestSpreadDegrees;
            ranged.shootNewRandomTargetsPerProjectile = shape.EachShotPicksItsOwnTarget;
            ranged.projectileVariation = shape.ProjectileVariation;

            ranged.projectileFollowsTarget = shape.ShotsFollowTheirTarget;
            ranged.projectileTargetsSelf = shape.FiresAtItself;
            ranged.sameFactionHealingPercentage = shape.HealsItsOwnSideBy;
            ranged.meleeDamageRadiusAtEntity = shape.AlsoHurtsThingsTouchingIt;

            ranged.onlyAttackWhenInCombat = shape.OnlyShootsWhenInCombat;
            ranged.interruptOnDamageTaken = shape.TakingAHitInterruptsIt;
            ranged.endDuration = shape.RecoveryAfterShooting;

            ranged.attackDuration = shape.HowLongItKeepsShooting;
            ranged.skipVisibilityCheck = shape.ShootsAtWhatItCannotSee;
            ranged.onlyAttackTargetsWeWantToAttack = shape.OnlyShootsThingsItWantsToAttack;
            ranged.disabled = shape.StartsSwitchedOff;
            ranged.startSpreadAngleOffset = shape.SpreadStartsAtDegrees;

            ranged.modifyBaseSpeedByTargetDistance = shape.SpeedChangesWithDistance;
            ranged.minMaxBaseSpeedMultiplierByTargetDistance = new Unity.Mathematics.float2(
                shape.SpeedFromNearToFar.x,
                shape.SpeedFromNearToFar.y);
            ranged.minMaxDistanceForBaseSpeedMultiplier = new Unity.Mathematics.float2(
                shape.NearAndFarDistance.x,
                shape.NearAndFarDistance.y);

            ranged.animOverride = shape.AnimationName;
            ranged.animPerShot = shape.AnimationPerShot;

            if (report == null)
            {
                return;
            }

            if (shape.SpreadWidthWillBeIgnored)
            {
                report(
                    "sets a widest spread on a shot pattern that never spreads, so the number does " +
                    "nothing. Only the random spread reads it.");
            }

            if (shape.LeadRangeIsHalfSpecified)
            {
                report(
                    "gives only one end of its target-leading range. The two are read together, so " +
                    "it will not lead a moving target at all. Set both, or leave both at zero.");
            }
            if (shape.SpeedByDistanceWillBeIgnored)
            {
                report(
                    "sets a near and far distance for its shot speed without switching that on, so " +
                    "its projectiles all travel at one speed.");
            }
        }

        public static void ApplyAttackSounds(
            GameObject root,
            DimensionAttackSoundsTemplate sounds,
            System.Action<string> report = null)
        {
            if (sounds == null || !sounds.HasAnySound)
            {
                RemoveComponentIfPresent<CustomAttackSoundAuthoring>(root);
                return;
            }

            // A typo in a sound name plays as silence, with nothing anywhere saying why. The name
            // still hashes — a mod shipping its own sounds needs that — so the most that can be
            // done is to say which names the game does not ship.
            if (report != null)
            {
                System.Collections.Generic.List<string> unknown = sounds.NamesTheGameDoesNotShip();
                for (int i = 0; i < unknown.Count; i++)
                {
                    report(
                        "names the sound '" + unknown[i] + "', which the game does not ship. " +
                        "If it is not a sound this mod brings itself, it will play as silence.");
                }
            }

            CustomAttackSoundAuthoring custom = EnsureComponent<CustomAttackSoundAuthoring>(root);
            custom.attackSoundId = new SFXTableIDField { value = sounds.AttackSound };
            custom.impactSoundId = new SFXTableIDField { value = sounds.ImpactSound };
            custom.windupSound = new SFXTableIDField { value = sounds.WindUpSound };
            custom.windupCancelSound = new SFXTableIDField { value = sounds.WindUpCancelledSound };
            custom.strongAttackSound = new SFXTableIDField { value = sounds.StrongAttackSound };
        }

        /// <summary>
        /// What tile an object leaves behind when it is destroyed, and what it cracks into first.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two small components that belong together because they are the same idea at two moments:
        /// <c>CrackableTileAuthoring</c> is the tile it turns into when damaged but not destroyed
        /// (146 and 93 vanilla prefabs respectively), <c>SpawnTileOnDeathAuthoring</c> is what is
        /// left where it stood.
        /// </para>
        /// <para>
        /// Both name a tileset by our own id, not by the vanilla enum, because a custom block is
        /// exactly the thing an author is most likely to want left behind — that is the point of
        /// having a tileset system at all. Resolution goes through the same registry the block
        /// generator uses, so a name that resolves to nothing is reported rather than silently
        /// leaving dirt.
        /// </para>
        /// </remarks>
        public static void ApplyTileOutcomes(
            GameObject root,
            DimensionTileOutcomeTemplate outcome,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (outcome == null || !outcome.LeavesATileBehind)
            {
                RemoveComponentIfPresent<SpawnTileOnDeathAuthoring>(root);
            }
            else
            {
                int tileset = ResolveTilesetOrReport(outcome.LeavesTilesetId, resolveTileset, report);
                if (tileset < 0)
                {
                    RemoveComponentIfPresent<SpawnTileOnDeathAuthoring>(root);
                }
                else
                {
                    SpawnTileOnDeathAuthoring spawn =
                        EnsureComponent<SpawnTileOnDeathAuthoring>(root);
                    spawn.tileType = outcome.LeavesTileType;
                    spawn.tileset = (PugTilemap.Tileset)tileset;
                    spawn.spawnChance = outcome.LeavesChance;
                    spawn.clearOtherTiles = outcome.ClearsWhatWasThere;
                }
            }

            if (outcome == null || !outcome.CracksFirst)
            {
                RemoveComponentIfPresent<CrackableTileAuthoring>(root);
                return;
            }

            int crackTileset = ResolveTilesetOrReport(outcome.CracksIntoTilesetId, resolveTileset, report);
            if (crackTileset < 0)
            {
                RemoveComponentIfPresent<CrackableTileAuthoring>(root);
                return;
            }

            // ONLY ON SOMETHING THAT IS A TILE. Core Keeper's step for cracking looks for the tile
            // answer and the cracking answer on the same object and does nothing at all when
            // either is missing — all ninety-three of the game's cracking objects are tiles. A
            // world object is not a tile, so the cracking answer is written and then skipped in
            // silence. Blocks made in the Tileset studio get this properly; a placed object cannot.
            if (!HasNamed(root, "TileAuthoring"))
            {
                RemoveComponentIfPresent<CrackableTileAuthoring>(root);
                SayWhenTicked(
                    true,
                    report,
                    "is set to crack before it breaks, and that only works on ground and walls. " +
                    "Core Keeper looks for cracking on something it already knows is a tile, and " +
                    "a placed object is not one, so the crack stage was left off. Build it as a " +
                    "block in the Tileset studio if you want it to crack.");
                return;
            }

            CrackableTileAuthoring crack = EnsureComponent<CrackableTileAuthoring>(root);
            crack.crackTileType = outcome.CracksIntoTileType;
            crack.crackTileset = (PugTilemap.Tileset)crackTileset;
        }

        /// <summary>Resolves a tileset name, or -1 with a report when it is not one.</summary>
        private static int ResolveTilesetOrReport(
            string tilesetId,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return -1;
            }

            int resolved = resolveTileset == null ? -1 : resolveTileset(tilesetId);
            if (resolved < 0 && report != null)
            {
                report(
                    "'" + tilesetId + "' is named as a tile to leave behind, but it is neither one " +
                    "of this mod's tilesets nor one of the game's, so nothing will be left there.");
            }

            return resolved;
        }

        /// <summary>
        /// Points an item at the character art it is drawn with.
        /// </summary>
        /// <remarks>
        /// The address is written through a <c>SerializedObject</c> because <c>DataBlockRef</c>
        /// keeps its address private. Removed when there is no skin, so an item that stops being
        /// worn does not keep pointing at art that no longer exists.
        /// </remarks>
        public static void ApplyEquipmentSkin(GameObject root, ScriptableDataBlock skin)
        {
            if (skin == null)
            {
                RemoveComponentIfPresent<EquipmentSkinAuthoring>(root);
                return;
            }

            EquipmentSkinAuthoring authoring = EnsureComponent<EquipmentSkinAuthoring>(root);
            SerializedObject serialized = new SerializedObject(authoring);
            SerializedProperty address = serialized.FindProperty("skinRef.m_address");
            if (address == null)
            {
                return;
            }

            SerializedProperty low = address.FindPropertyRelative("m_low");
            SerializedProperty high = address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                return;
            }

            SerializedObject source = new SerializedObject(skin);
            SerializedProperty sourceAddress = source.FindProperty("m_address");
            low.longValue = sourceAddress.FindPropertyRelative("m_low").longValue;
            high.longValue = sourceAddress.FindPropertyRelative("m_high").longValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// What hitting and breaking it sounds and looks like.
        /// </summary>
        /// <remarks>
        /// Removed when nothing is set, so an object stripped of its feedback does not keep throwing
        /// the dust it used to. An unknown puff name is reported rather than dropped, because a
        /// missing burst is invisible until somebody swings at the thing.
        /// </remarks>
        public static void ApplyImpactFeedback(
            GameObject root,
            DimensionImpactFeedbackTemplate feedback,
            System.Action<string> reportUnknownPuff)
        {
            if (feedback == null || !feedback.HasAnyFeedback)
            {
                RemoveComponentIfPresent<TileEffectAuthoring>(root);
                return;
            }

            TileEffectAuthoring effect = EnsureComponent<TileEffectAuthoring>(root);
            effect.sfxTableDamageId = new SFXTableIDField { value = feedback.HitSoundId };
            effect.sfxTableDestroyId = new SFXTableIDField { value = feedback.BreakSoundId };
            effect.destroyPuffs = new System.Collections.Generic.List<PuffParams>();

            DimensionPuffBurst[] bursts = feedback.BreakParticles;
            for (int i = 0; i < bursts.Length; i++)
            {
                PuffID puff;
                if (!System.Enum.TryParse(bursts[i].PuffId, false, out puff))
                {
                    if (reportUnknownPuff != null)
                    {
                        reportUnknownPuff(bursts[i].PuffId);
                    }

                    continue;
                }

                effect.destroyPuffs.Add(new PuffParams
                {
                    puff = puff,
                    particleCount = bursts[i].ParticleCount,
                    relativePosition = bursts[i].Offset
                });
            }
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        /// <summary>
        /// Removes a component unless another component on the object declares it as required.
        /// </summary>
        /// <returns>
        /// True when the object no longer has the component. False when something requires it, in
        /// which case the caller has to neutralise it — clear its lists, zero its numbers — instead.
        /// </returns>
        /// <remarks>
        /// <para>
        /// GENERATION IS AUTHORITATIVE: a value the author turned off is removed rather than left
        /// behind. Unity's <c>RequireComponent</c> is the one case where that is not allowed —
        /// <c>DestroyImmediate</c> refuses, logs an error, and leaves the component in place with
        /// whatever it had before.
        /// </para>
        /// <para>
        /// Found by the test suite the moment pursuit started adding <c>ChaseStateAuthoring</c>,
        /// which requires <c>BehaviourTagsAuthoring</c> — a creature with no attack tags then tried
        /// to remove a component its own chase depended on. It had been latent all along: the
        /// removal only ever succeeded because nothing that required those components was present.
        /// </para>
        /// </remarks>
        public static bool TryRemoveComponent<T>(GameObject root)
            where T : Component
        {
            if (root == null)
            {
                return true;
            }

            T component = root.GetComponent<T>();
            if (component == null)
            {
                return true;
            }

            if (SomethingRequires(root, typeof(T)))
            {
                // IT USED TO BE SILENT, and the comment on RemoveComponentIfPresent said the
                // opposite. Every caller discards this answer, so a blocked removal left the
                // component on the object carrying the PREVIOUS generate's fields with nothing said
                // anywhere — the same shape as the stale-flag bugs this framework keeps finding,
                // reached by a different road. It is said here because saying it at every one of
                // the several hundred call sites is not something a person would keep up.
                Debug.LogWarning(
                    "[ExpandNullforge] '" + root.name + "' still has " + typeof(T).Name +
                    " after generation asked for it to be taken off: something else on it requires " +
                    "that component, so it keeps whatever the last generate left in it.");
                return false;
            }

            Object.DestroyImmediate(component, true);
            return true;
        }

        private static bool SomethingRequires(GameObject root, System.Type required)
        {
            Component[] present = root.GetComponents<Component>();
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i] == null || required.IsInstanceOfType(present[i]))
                {
                    continue;
                }

                object[] attributes = present[i]
                    .GetType()
                    .GetCustomAttributes(typeof(RequireComponent), true);
                for (int a = 0; a < attributes.Length; a++)
                {
                    RequireComponent rule = (RequireComponent)attributes[a];
                    if (Requires(rule.m_Type0, required) ||
                        Requires(rule.m_Type1, required) ||
                        Requires(rule.m_Type2, required))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Requires(System.Type declared, System.Type required)
        {
            return declared != null && declared.IsAssignableFrom(required);
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            // Routed through the one dependency-aware removal, which says so in the console when a
            // RequireComponent blocks it. The answer is discarded here on purpose: there is nothing
            // this method can do about it, and the one place that knows is the one that says it.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }
    }
}
