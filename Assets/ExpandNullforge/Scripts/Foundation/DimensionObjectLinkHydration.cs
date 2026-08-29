using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Foundation
{
    /// <summary>What happened when a row was applied to a prefab.</summary>
    public enum DimensionObjectLinkResult
    {
        /// <summary>The field already said what the row wanted. Nothing was touched.</summary>
        AlreadyCorrect = 0,

        /// <summary>The field was written.</summary>
        Written,

        /// <summary>
        /// Nothing on the prefab can carry this. Saying so once and stopping is right: retrying
        /// cannot make a missing component appear.
        /// </summary>
        Impossible
    }

    /// <summary>The result of one row, and the sentence to show a creator when it failed.</summary>
    public readonly struct DimensionObjectLinkOutcome
    {
        public DimensionObjectLinkOutcome(DimensionObjectLinkResult result, string reason)
        {
            Result = result;
            Reason = reason ?? string.Empty;
        }

        public DimensionObjectLinkResult Result { get; }

        /// <summary>Empty unless <see cref="Result"/> is <c>Impossible</c>.</summary>
        public string Reason { get; }

        public static DimensionObjectLinkOutcome Written()
        {
            return new DimensionObjectLinkOutcome(DimensionObjectLinkResult.Written, null);
        }

        public static DimensionObjectLinkOutcome AlreadyCorrect()
        {
            return new DimensionObjectLinkOutcome(DimensionObjectLinkResult.AlreadyCorrect, null);
        }

        public static DimensionObjectLinkOutcome Impossible(string reason)
        {
            return new DimensionObjectLinkOutcome(DimensionObjectLinkResult.Impossible, reason);
        }
    }

    /// <summary>
    /// Writes one deferred reference onto the prefab entity that carries it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SEPARATED FROM THE SYSTEM ON PURPOSE. Everything here takes an <c>EntityManager</c> and an
    /// entity and nothing else, so a test can build one entity with one component and check the
    /// arm, instead of standing up a world with a database bank in it.
    /// </para>
    /// <para>
    /// THREE RULES HOLD IN EVERY ARM.
    /// </para>
    /// <para>
    /// (1) READ, COMPARE, WRITE. Setting a component marks its chunk changed whether or not the
    /// value moved, and a re-entered world re-runs every row; writing only on a difference keeps a
    /// settled session free.
    /// </para>
    /// <para>
    /// (2) A MISSING COMPONENT IS <c>Impossible</c>, NOT A RETRY — with one exception. Vanilla's
    /// <c>JewelryConverter</c> adds <c>JewelryCanBePolishedCD</c> only when the id it read was not
    /// <c>None</c>, and the id we baked WAS <c>None</c>, so on that one arm the component genuinely
    /// has to be added here. Every other converter in this table adds its component unconditionally,
    /// so a missing one means the authoring component was stripped at generation and the honest
    /// answer is to say so rather than to invent a component with default values in every other
    /// field.
    /// </para>
    /// <para>
    /// (3) THE COMPLAINT NAMES THE CONTROL AS IT IS LABELLED. A creator reading the log should be
    /// able to go to the asset, find that exact wording, and generate again. Some of these controls
    /// are tick boxes, some are dropdowns and some are text boxes, so the sentence says which — an
    /// instruction to tick something that is really a dropdown sends a person looking for a box
    /// that is not there.
    /// </para>
    /// </remarks>
    public static class DimensionObjectLinkHydration
    {
        public static DimensionObjectLinkOutcome Apply(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (row == null || prefab == Entity.Null || !entityManager.Exists(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "has no object behind it, so there is nothing to point at anything.");
            }

            switch (row.Link)
            {
                case DimensionObjectLink.FiresProjectile:
                case DimensionObjectLink.RandomProjectile:
                case DimensionObjectLink.SecondProjectile:
                    return ApplyRangeWeapon(entityManager, prefab, row, target);

                case DimensionObjectLink.CreatureShot:
                    return ApplyCreatureShot(entityManager, prefab, target);

                case DimensionObjectLink.BeamProjectile:
                    return ApplyBeamWeapon(entityManager, prefab, target);

                case DimensionObjectLink.SecondaryUseMinion:
                    return ApplySecondaryUse(entityManager, prefab, target);

                case DimensionObjectLink.PolishesInto:
                    return ApplyPolish(entityManager, prefab, target);

                case DimensionObjectLink.ScansFor:
                    return ApplyScanner(entityManager, prefab, target);

                case DimensionObjectLink.ShedsWhenDamaged:
                    return ApplyShed(entityManager, prefab, target);

                case DimensionObjectLink.TitanShrine:
                    return ApplyTitanShrine(entityManager, prefab, target);

                case DimensionObjectLink.ContainerBecomes:
                case DimensionObjectLink.ContainerReactsTo:
                    return ApplyContainerVariation(entityManager, prefab, row, target);

                case DimensionObjectLink.SlotAccepts:
                    return ApplySlotRule(entityManager, prefab, row, target);

                case DimensionObjectLink.MelodyAffects:
                    return ApplyMelody(entityManager, prefab, target);

                case DimensionObjectLink.BossChest:
                case DimensionObjectLink.BossSecondChest:
                    return ApplyBossChest(entityManager, prefab, row, target);

                case DimensionObjectLink.SegmentTail:
                case DimensionObjectLink.NeverHits:
                    return ApplySnake(entityManager, prefab, row, target);

                case DimensionObjectLink.ProjectileShard:
                    return ApplyShard(entityManager, prefab, target);

                case DimensionObjectLink.ProjectileNapalm:
                    return ApplyNapalm(entityManager, prefab, row, target);

                case DimensionObjectLink.FlowerOfPlant:
                    return ApplyFlower(entityManager, prefab, row, target);

                case DimensionObjectLink.SpawnerEnemy:
                    return ApplySpawnerPlatform(entityManager, prefab, target);

                case DimensionObjectLink.TrophyEnemy:
                    return ApplyTrophy(entityManager, prefab, target);

                case DimensionObjectLink.TrailObject:
                    return ApplyTrail(entityManager, prefab, target);

                case DimensionObjectLink.MerchantStock:
                    return ApplyMerchantStock(entityManager, prefab, row, target);
            }

            return DimensionObjectLinkOutcome.Impossible(
                "names a kind of link this version of the framework does not write.");
        }

        // ---- weapons -------------------------------------------------------------------

        private static DimensionObjectLinkOutcome ApplyRangeWeapon(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasComponent<RangeWeaponCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "names a shot to fire but carries no ranged weapon, so it fires nothing. Set " +
                    "'What kind of weapon this is' to Ranged on the item and generate again.");
            }

            RangeWeaponCD weapon = entityManager.GetComponentData<RangeWeaponCD>(prefab);
            switch (row.Link)
            {
                case DimensionObjectLink.FiresProjectile:
                    if (weapon.projectileID == target)
                    {
                        return DimensionObjectLinkOutcome.AlreadyCorrect();
                    }

                    weapon.projectileID = target;
                    break;

                case DimensionObjectLink.SecondProjectile:
                    // The explosion size comes back with the shot, and only here. Vanilla's
                    // RangeWeaponConverter logs a red error naming the creator's prefab whenever a
                    // weapon has a size and no explosive shot behind it, and a deferred shot is
                    // exactly that state while the prefab is being written — so the generator bakes
                    // the size as zero and the authored number travels on the row.
                    if (weapon.windupProjectileID == target &&
                        (row.Number <= 0 || weapon.explosionSize == row.Number))
                    {
                        return DimensionObjectLinkOutcome.AlreadyCorrect();
                    }

                    weapon.windupProjectileID = target;
                    if (row.Number > 0)
                    {
                        weapon.explosionSize = row.Number;
                    }

                    break;

                default:
                {
                    // The list lives INSIDE the component as a FixedList64Bytes, so the slot has to
                    // already be there. The generator writes None into it at the right position for
                    // exactly this reason — dropping the entry instead would shift every shot after
                    // it one place left and this write would land on the wrong one.
                    int index = row.Index;
                    if (index < 0 || index >= weapon.randomProjectiles.Length)
                    {
                        return DimensionObjectLinkOutcome.Impossible(
                            "names one of a weapon's random shots at a position the weapon does " +
                            "not have. Generate again so the list is rebuilt.");
                    }

                    if (weapon.randomProjectiles[index] == target)
                    {
                        return DimensionObjectLinkOutcome.AlreadyCorrect();
                    }

                    weapon.randomProjectiles[index] = target;
                    break;
                }
            }

            entityManager.SetComponentData(prefab, weapon);
            return DimensionObjectLinkOutcome.Written();
        }

        /// <summary>
        /// What a CREATURE shoots — a different component from what a weapon shoots.
        /// </summary>
        /// <remarks>
        /// A bow's shot is <c>RangeWeaponCD.projectileID</c> and lives on an item; a creature's is
        /// <c>RangeAttackStateCD.projectileID</c> and lives on the creature
        /// (<c>ck-db\Pug.ECS.Conversion\RangeAttackStateConverter.cs</c> copies it from
        /// <c>RangeAttackStateAuthoring</c>). One arm writing both would have to guess which
        /// component the prefab meant.
        /// </remarks>
        private static DimensionObjectLinkOutcome ApplyCreatureShot(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<RangeAttackStateCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "shoots something but has no ranged attack on it, so it never shoots. Set the " +
                    "creature's attack kind to Ranged, or Melee And Ranged, and generate again.");
            }

            RangeAttackStateCD ranged = entityManager.GetComponentData<RangeAttackStateCD>(prefab);
            if (ranged.projectileID == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            ranged.projectileID = target;
            entityManager.SetComponentData(prefab, ranged);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyBeamWeapon(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<BeamWeaponCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "names a shot for the end of its beam but carries no beam, so nothing is " +
                    "fired. Fill in the beam settings under 'If it fires a beam' on the item and " +
                    "generate again.");
            }

            BeamWeaponCD beam = entityManager.GetComponentData<BeamWeaponCD>(prefab);
            if (beam.windupProjectileID == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            beam.windupProjectileID = target;
            entityManager.SetComponentData(prefab, beam);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplySecondaryUse(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<SecondaryUseCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "summons something on right-click but has no right-click behaviour on it, so " +
                    "nothing is summoned. Set what right-clicking does and generate again.");
            }

            SecondaryUseCD secondary = entityManager.GetComponentData<SecondaryUseCD>(prefab);
            if (secondary.minionToSpawn == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            secondary.minionToSpawn = target;
            entityManager.SetComponentData(prefab, secondary);
            return DimensionObjectLinkOutcome.Written();
        }

        // ---- items ---------------------------------------------------------------------

        private static DimensionObjectLinkOutcome ApplyPolish(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            // THE ONE STRUCTURAL ADD IN THIS TABLE. JewelryConverter writes the component only when
            // the id it read was already real, and the id we baked was None, so on this prefab the
            // component does not exist at all. Adding it is the whole fix; a Set would throw.
            if (!entityManager.HasComponent<JewelryCanBePolishedCD>(prefab))
            {
                entityManager.AddComponentData(prefab, new JewelryCanBePolishedCD
                {
                    polishedVersion = target
                });
                return DimensionObjectLinkOutcome.Written();
            }

            JewelryCanBePolishedCD polish =
                entityManager.GetComponentData<JewelryCanBePolishedCD>(prefab);
            if (polish.polishedVersion == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            polish.polishedVersion = target;
            entityManager.SetComponentData(prefab, polish);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyScanner(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<ScannerCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "points at something to scan for but is not a scanner, so it points at " +
                    "nothing. Fill in 'Using it scans for this object' on the item and generate " +
                    "again.");
            }

            ScannerCD scanner = entityManager.GetComponentData<ScannerCD>(prefab);
            if (scanner.objectToScan == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            scanner.objectToScan = target;
            entityManager.SetComponentData(prefab, scanner);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyShed(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<DropsLootWhenDamagedCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "sheds something as it is hit but nothing on it drops loot when damaged, so it " +
                    "sheds nothing. Only something with a health pool can be hit — give it " +
                    "health, or clear what it sheds, and generate again.");
            }

            DropsLootWhenDamagedCD shed =
                entityManager.GetComponentData<DropsLootWhenDamagedCD>(prefab);
            if (shed.dropsLoot == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            shed.dropsLoot = target;
            entityManager.SetComponentData(prefab, shed);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyTitanShrine(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<TitanShrineCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "is bound to a titan but is not a shrine, so nothing answers it. Tick 'It is a " +
                    "shrine that a titan is bound to' on the item and generate again.");
            }

            TitanShrineCD shrine = entityManager.GetComponentData<TitanShrineCD>(prefab);
            if (shrine.titanObjectID == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            shrine.titanObjectID = target;
            entityManager.SetComponentData(prefab, shrine);
            return DimensionObjectLinkOutcome.Written();
        }

        // ---- containers ----------------------------------------------------------------

        private static DimensionObjectLinkOutcome ApplyContainerVariation(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasComponent<ChangeVariationWhenContainingObjectCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "reacts to what is put inside it but carries no rule for that, so nothing " +
                    "happens. Fill in what it reacts to and what it becomes, then generate again.");
            }

            ChangeVariationWhenContainingObjectCD change =
                entityManager.GetComponentData<ChangeVariationWhenContainingObjectCD>(prefab);
            if (row.Link == DimensionObjectLink.ContainerReactsTo)
            {
                if (change.objectID == target)
                {
                    return DimensionObjectLinkOutcome.AlreadyCorrect();
                }

                change.objectID = target;
            }
            else
            {
                if (change.reinstantiateToNewObjectId == target)
                {
                    return DimensionObjectLinkOutcome.AlreadyCorrect();
                }

                change.reinstantiateToNewObjectId = target;
            }

            entityManager.SetComponentData(prefab, change);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplySlotRule(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasBuffer<InventorySlotRequirementBuffer>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "has a slot rule naming what it accepts, but no inventory to put the rule on. " +
                    "Give it slots and generate again.");
            }

            DynamicBuffer<InventorySlotRequirementBuffer> rules =
                entityManager.GetBuffer<InventorySlotRequirementBuffer>(prefab);

            // Matched on the rule's own slotIndex rather than on the buffer position: InventoryConverter
            // writes the authored index into the element (ck-db\Pug.ECS.Conversion\InventoryConverter.cs:220),
            // so this still lands on the right rule if anything ever appends to the buffer.
            for (int i = 0; i < rules.Length; i++)
            {
                InventorySlotRequirementBuffer rule = rules[i];
                if (rule.slotIndex != row.Index)
                {
                    continue;
                }

                if (row.EntryIndex < 0 || row.EntryIndex >= rule.acceptsObjectIds.Length)
                {
                    return DimensionObjectLinkOutcome.Impossible(
                        "names an accepted item at a position its slot rule does not have. A slot " +
                        "rule holds seven named items at most, so anything past the seventh is " +
                        "dropped. Use a category instead of listing them one by one.");
                }

                FixedList32Bytes<ObjectID> accepts = rule.acceptsObjectIds;
                if (accepts[row.EntryIndex] == target)
                {
                    return DimensionObjectLinkOutcome.AlreadyCorrect();
                }

                accepts[row.EntryIndex] = target;
                rule.acceptsObjectIds = accepts;
                rules[i] = rule;
                return DimensionObjectLinkOutcome.Written();
            }

            return DimensionObjectLinkOutcome.Impossible(
                "names an accepted item for a slot rule the object no longer has. Generate again.");
        }

        private static DimensionObjectLinkOutcome ApplyMelody(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<AffectObjectWhenMelodyPlayedCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "turns into something when a melody is played but is not listening for one. Name " +
                    "at least one melody under 'Which melodies it reacts to' and generate again.");
            }

            AffectObjectWhenMelodyPlayedCD melody =
                entityManager.GetComponentData<AffectObjectWhenMelodyPlayedCD>(prefab);
            if (melody.newObjectId == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            melody.newObjectId = target;
            entityManager.SetComponentData(prefab, melody);
            return DimensionObjectLinkOutcome.Written();
        }

        // ---- creatures -----------------------------------------------------------------

        private static DimensionObjectLinkOutcome ApplyBossChest(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasComponent<BossCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "leaves a chest but the game does not treat it as a boss, so it leaves nothing. " +
                    "A chest only comes from a creature authored as a boss — generate it from " +
                    "the bosses list and try again.");
            }

            BossCD boss = entityManager.GetComponentData<BossCD>(prefab);
            if (row.Link == DimensionObjectLink.BossChest)
            {
                if (boss.chestToSpawn.objectID == target &&
                    boss.chestToSpawn.variation == row.TargetVariation)
                {
                    return DimensionObjectLinkOutcome.AlreadyCorrect();
                }

                boss.chestToSpawn.objectID = target;
                boss.chestToSpawn.variation = row.TargetVariation;
            }
            else
            {
                if (boss.optionalChestVersion.objectID == target &&
                    boss.optionalChestVersion.variation == row.TargetVariation)
                {
                    return DimensionObjectLinkOutcome.AlreadyCorrect();
                }

                boss.optionalChestVersion.objectID = target;
                boss.optionalChestVersion.variation = row.TargetVariation;
            }

            entityManager.SetComponentData(prefab, boss);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplySnake(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasComponent<SnakeMovementStateCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "has a body made of segments but no segmented movement on it, so it has no " +
                    "body to end. Tick 'It is a segmented creature' and generate again.");
            }

            SnakeMovementStateCD snake =
                entityManager.GetComponentData<SnakeMovementStateCD>(prefab);
            if (row.Link == DimensionObjectLink.SegmentTail)
            {
                if (snake.tailObjectId == target)
                {
                    return DimensionObjectLinkOutcome.AlreadyCorrect();
                }

                snake.tailObjectId = target;
            }
            else
            {
                if (snake.cantHitSpecificObject == target)
                {
                    return DimensionObjectLinkOutcome.AlreadyCorrect();
                }

                snake.cantHitSpecificObject = target;
            }

            entityManager.SetComponentData(prefab, snake);
            return DimensionObjectLinkOutcome.Written();
        }

        // ---- projectiles ---------------------------------------------------------------

        private static DimensionObjectLinkOutcome ApplyShard(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<ShatterOnCollisionProjectileCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "breaks into something on impact but is not set to shatter, so it never " +
                    "breaks. Tick 'It shatters instead of exploding when it hits something' on the " +
                    "shot and generate again.");
            }

            ShatterOnCollisionProjectileCD shatter =
                entityManager.GetComponentData<ShatterOnCollisionProjectileCD>(prefab);
            if (shatter.shardObjectID == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            shatter.shardObjectID = target;
            entityManager.SetComponentData(prefab, shatter);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyNapalm(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasComponent<MortarProjectileDamageEffectCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "leaves something where it lands but does not fall like a shell, so it lands " +
                    "nowhere. Set the shot to arc and generate again.");
            }

            MortarProjectileDamageEffectCD mortar =
                entityManager.GetComponentData<MortarProjectileDamageEffectCD>(prefab);
            if (mortar.spawnNapalmObjectID == target &&
                mortar.spawnNapalmVariation == row.TargetVariation)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            mortar.spawnNapalmObjectID = target;
            mortar.spawnNapalmVariation = row.TargetVariation;
            entityManager.SetComponentData(prefab, mortar);
            return DimensionObjectLinkOutcome.Written();
        }

        // ---- world objects -------------------------------------------------------------

        private static DimensionObjectLinkOutcome ApplyFlower(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasComponent<FlowerCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "is the flower of a plant but is not marked as a flower. Fill in 'It is the " +
                    "flower of this plant' on the object and generate again.");
            }

            FlowerCD flower = entityManager.GetComponentData<FlowerCD>(prefab);
            if (flower.plantID == target && flower.plantVariation == row.TargetVariation)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            flower.plantID = target;
            flower.plantVariation = row.TargetVariation;
            entityManager.SetComponentData(prefab, flower);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplySpawnerPlatform(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<EnemySpawnerPlatformCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "sends out a creature but is not a spawner platform, so nothing arrives. " +
                    "Fill in 'It is a spawner platform producing this enemy' on the object and " +
                    "generate again.");
            }

            EnemySpawnerPlatformCD spawner =
                entityManager.GetComponentData<EnemySpawnerPlatformCD>(prefab);
            if (spawner.enemyToSpawn == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            spawner.enemyToSpawn = target;
            entityManager.SetComponentData(prefab, spawner);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyTrophy(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<TrophyCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "calls a creature onto a spawner platform but is not a trophy, so nothing is " +
                    "called. Set the object's kind to Trophy and generate again.");
            }

            TrophyCD trophy = entityManager.GetComponentData<TrophyCD>(prefab);
            if (trophy.enemyToSpawnFromSpawnerPlatform == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            trophy.enemyToSpawnFromSpawnerPlatform = target;
            entityManager.SetComponentData(prefab, trophy);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyTrail(
            EntityManager entityManager,
            Entity prefab,
            ObjectID target)
        {
            if (!entityManager.HasComponent<LeaveTrailCD>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "leaves a trail behind it but has no trail on it, so nothing is left. Tick " +
                    "'It leaves something behind it as it moves' and generate again.");
            }

            LeaveTrailCD trail = entityManager.GetComponentData<LeaveTrailCD>(prefab);
            if (trail.trailObjectID == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            trail.trailObjectID = target;
            entityManager.SetComponentData(prefab, trail);
            return DimensionObjectLinkOutcome.Written();
        }

        private static DimensionObjectLinkOutcome ApplyMerchantStock(
            EntityManager entityManager,
            Entity prefab,
            DimensionObjectLinkDefinition row,
            ObjectID target)
        {
            if (!entityManager.HasBuffer<MerchantItemInfoBuffer>(prefab))
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "sells something but is not a trader, so there is no shop to put it in. Tick " +
                    "'It buys and sells' on the object and generate again.");
            }

            DynamicBuffer<MerchantItemInfoBuffer> stock =
                entityManager.GetBuffer<MerchantItemInfoBuffer>(prefab);
            if (row.Index < 0 || row.Index >= stock.Length)
            {
                return DimensionObjectLinkOutcome.Impossible(
                    "sells something at a position its shop does not have. Generate again so the " +
                    "shop list is rebuilt.");
            }

            MerchantItemInfoBuffer line = stock[row.Index];
            if (line.objectID == target)
            {
                return DimensionObjectLinkOutcome.AlreadyCorrect();
            }

            line.objectID = target;
            stock[row.Index] = line;
            return DimensionObjectLinkOutcome.Written();
        }
    }
}
