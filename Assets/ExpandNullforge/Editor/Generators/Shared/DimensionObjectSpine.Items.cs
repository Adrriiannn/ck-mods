using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The spine an item carries: what it does when used, worn or held.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
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
                    // A WHOLE SENTENCE, like every other line this method reports. Handing back the
                    // bare name and leaving the caller to build the sentence makes one callback
                    // carry two different things, and the minion complaint below then arrives
                    // wrapped in the caller's charged-attack wording. One contract: everything that
                    // comes out of here is already a sentence.
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
    }
}
