using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Plants;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What goes on the seed, the plant and the produce it gives back.
    /// </summary>
    internal static partial class DimensionPlantGenerator
    {
        /// <summary>
        /// The plant in the ground, on one variation.
        /// </summary>
        /// <remarks>
        /// Typed <c>NonObtainable</c> because a growing plant is not a thing a player carries — vanilla
        /// types every <c>…PlantEntity</c> that way, and typing it as an item would put a half-grown
        /// crop in the crafting menus.
        /// </remarks>
        private static void ConfigurePlant(
            GameObject root,
            DimensionPlantAsset plant,
            string plantName,
            string seedName,
            DimensionCropVersionTemplate version,
            int variation,
            int currentStage,
            bool hasVersions,
            DimensionNamingContext naming,
            DimensionPlantGenerationReport report,
            GameObject visual)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = plantName;
            obj.objectType = ObjectType.NonObtainable;
            obj.initialAmount = 1;
            obj.variation = variation;

            // Assigned even when null, which is the case that matters: a crop whose art was removed
            // must stop pointing at a body, or it would be handed a pooled instance still wearing
            // whichever crop used it last and grow disguised as that one.
            obj.graphicalPrefab = visual;

            // Vanilla's PlantAuthoring bakes an ObjectID that cannot be resolved while this runs, and
            // its converter hardcodes the harvest count to one. The framework twin carries the name
            // and the count, and the two components must not both be present or which of them wrote
            // the entity's PlantCD would come down to converter registration order.
            RemoveComponentIfPresent<PlantAuthoring>(root);

            DimensionPlantProduceAuthoring produce =
                EnsureComponent<DimensionPlantProduceAuthoring>(root);
            produce.growingSettings = BuildGrowingSettings(plant, currentStage);
            produce.produceName = ResolveReference(
                version != null && !string.IsNullOrEmpty(version.ProduceItemId)
                    ? version.ProduceItemId
                    : plant.ProduceItemId,
                naming);
            produce.numberOfPlantsToDrop = version != null && version.HarvestAmount > 0
                ? version.HarvestAmount
                : plant.HarvestAmount;

            WarnAboutReference(plant, produce.produceName, "gives when picked", report);
            ConfigureGiveBacks(root, plant, seedName, version, naming, report);

            if (hasVersions)
            {
                EnsureComponent<DimensionCropTierPlantAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<DimensionCropTierPlantAuthoring>(root);
            }

            // The state machine: without these a plant never registers being hit and cannot be
            // removed, which is the same trap containers and creatures hit.
            DimensionObjectSpine.ApplyDamageableStates(root);
            EnsureComponent<MineableAuthoring>(root);
            EnsureComponent<PlaceableObjectAuthoring>(root);
            DimensionObjectSpine.ApplyPlacementRules(root, plant.PlacementRules, null);

            // What every vanilla plant carries and ours did not. Health is what makes it removable
            // at all; Diggable is what lets a shovel lift it rather than only a tool that mines.
            DimensionObjectSpine.ApplyUniversal(root, false);

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                plant.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add("'" + plant.DisplayName + "' " + message);
                });
            DimensionObjectSpine.ApplyAreaLevel(root, true);
            EnsureComponent<DiggableAuthoring>(root);
            EnsureComponent<Pug.Automation.AutomatedHarvestablePlantAuthoring>(root);

            // THREE HITS WHILE IT GROWS, ONE WHEN IT IS RIPE. That is the game's own shape and the
            // only shape in which "stays tough when ripe" means anything: the growing crop caps
            // what one blow can take off it, and ripening lifts the cap so a ripe crop harvests in
            // a single swing. The cap lives in DamageReductionCD, and the pass that lifts it does
            // nothing at all when the plant has no such component — so the answer the author ticked
            // was inert, and an unripe crop died to any stray swing on the way past.
            // CarrockPlantEntity is three health and one damage per hit.
            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            health.dontCalculateHealthFromLevel = true;
            health.maxHealth = 3;
            health.startHealth = 3;
            health.maxHealthMultiplier = 1f;

            DamageReductionAuthoring toughness = EnsureComponent<DamageReductionAuthoring>(root);
            toughness.calculateReductionFromLevel = false;
            toughness.reductionMultiplier = 1f;
            toughness.reduction = 0;
            toughness.maxDamagePerHit = 1;
            toughness.minDamagePerHit = 0;

            if (plant.WashedAwayByWater)
            {
                EnsureComponent<CanBeRemovedByWaterAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<CanBeRemovedByWaterAuthoring>(root);
            }

            ApplySpreading(root, plant, report);
        }

        /// <summary>
        /// What picking the plant hands back besides the produce: its seed, and a version's extras.
        /// </summary>
        /// <remarks>
        /// ONE CHANCE COVERS THE WHOLE LIST because Core Keeper stores exactly one
        /// (<c>ChanceToDropLootCD</c>) per object and rolls it separately for each entry. So a version
        /// that always hands over a bonus item also always hands back its seed. That is worth knowing
        /// rather than working around, and the number is authored in one place per version.
        /// </remarks>
        private static void ConfigureGiveBacks(
            GameObject root,
            DimensionPlantAsset plant,
            string seedName,
            DimensionCropVersionTemplate version,
            DimensionNamingContext naming,
            DimensionPlantGenerationReport report)
        {
            List<string> names = new List<string>();
            List<int> amounts = new List<int>();

            if (plant.IsPlanted && !string.IsNullOrEmpty(seedName))
            {
                names.Add(seedName);
                amounts.Add(1);
            }

            if (version != null)
            {
                DimensionCropVersionDropTemplate[] extras = version.ExtraDrops;
                for (int i = 0; i < extras.Length; i++)
                {
                    if (extras[i] == null || extras[i].IsEmpty)
                    {
                        continue;
                    }

                    string extraName = ResolveReference(extras[i].ItemId, naming);
                    WarnAboutReference(plant, extraName, "gives as an extra", report);
                    names.Add(extraName);
                    amounts.Add(extras[i].Amount);
                }
            }

            if (names.Count == 0)
            {
                RemoveComponentIfPresent<DimensionPlantDropsAuthoring>(root);
                return;
            }

            float percent = version != null && version.ChanceToGetThingsBackPercent > 0f
                ? version.ChanceToGetThingsBackPercent
                : plant.ChanceToGetTheSeedBackPercent;

            DimensionPlantDropsAuthoring drops = EnsureComponent<DimensionPlantDropsAuthoring>(root);
            drops.itemNames = names.ToArray();
            drops.amounts = amounts.ToArray();
            drops.chance = Mathf.Clamp01(percent / 100f);
        }

        /// <summary>
        /// The seed a player plants, on one variation.
        /// </summary>
        /// <remarks>
        /// <c>turnsIntoPlantName</c> is the whole point of the seed, and it is the name this generator
        /// gave the plant a moment ago rather than anything the author typed — so the two can never
        /// drift apart, and the id behind it is looked up in the running game where it exists.
        /// </remarks>
        private static void ConfigureSeed(
            GameObject root,
            DimensionPlantAsset plant,
            string plantName,
            string seedName,
            int variation,
            DimensionCropVersionTemplate[] versions,
            DimensionPlantGenerationReport report,
            GameObject visual)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = seedName;
            obj.objectType = ObjectType.PlaceablePrefab;
            obj.initialAmount = 1;
            obj.variation = variation;

            // The seed's body, which is what a player looks at for the whole first stage. Null when
            // nothing was drawn, for the same pooling reason as the plant's.
            obj.graphicalPrefab = visual;

            Rarity rarity;
            if (!string.IsNullOrEmpty(plant.RarityId) && Enum.TryParse(plant.RarityId, false, out rarity))
            {
                obj.rarity = rarity;
            }

            // The seed is the half of a crop a player carries, so it is the half with an icon.
            InventoryItemAuthoring inventory = EnsureComponent<InventoryItemAuthoring>(root);
            inventory.isStackable = true;

            // Never left null: ObjectAuthoring.ObjectInfo walks this list with a foreach the moment
            // anything asks the prefab what object it is, and a null there throws inside conversion
            // rather than reporting a missing recipe.
            if (inventory.requiredObjectsToCraft == null)
            {
                inventory.requiredObjectsToCraft = new List<InventoryItemAuthoring.CraftingObject>();
            }

            if (plant.SeedIcon != null)
            {
                inventory.icon = plant.SeedIcon;
                inventory.smallIcon = plant.SeedIcon;
            }

            RemoveComponentIfPresent<SeedAuthoring>(root);

            GiveItEverythingAVanillaSeedHas(root);

            DimensionSeedAuthoring seed = EnsureComponent<DimensionSeedAuthoring>(root);
            seed.growingSettings = BuildGrowingSettings(plant, 0);
            seed.turnsIntoPlantName = plantName;

            // The game's own golden roll only fires when this is above zero, and it only ever places
            // this one variation. Handing it the first version's slot is what keeps a plain golden
            // crop behaving exactly as it does in vanilla, gardening talents included.
            bool gameRollsFirstVersion =
                versions.Length > 0 && versions[0].UsesTheGamesGoldenChance;
            seed.rareSeedVariation = gameRollsFirstVersion ? DimensionPlantAsset.RareSeedVariation : 0;
            seed.rarePlantVariation = gameRollsFirstVersion ? DimensionPlantAsset.RarePlantVariation : 0;

            if (versions.Length == 0)
            {
                RemoveComponentIfPresent<DimensionCropTierSeedAuthoring>(root);
                return;
            }

            DimensionCropTierSeedAuthoring tiers = EnsureComponent<DimensionCropTierSeedAuthoring>(root);
            tiers.seedVariations = new int[versions.Length];
            tiers.plantVariations = new int[versions.Length];
            tiers.chancePercents = new float[versions.Length];
            tiers.usesTheGamesGoldenRoll = new bool[versions.Length];
            tiers.plainSeedVariation = PlainSeedVariationFor(versions.Length);

            for (int i = 0; i < versions.Length; i++)
            {
                tiers.seedVariations[i] = SeedVariationFor(i);
                tiers.plantVariations[i] = PlantVariationFor(i);
                tiers.chancePercents[i] = versions[i].ChancePercent;

                // Only the first version can be left to the game: it has one golden roll and one
                // golden variation, so a later version claiming the same thing would simply never be
                // rolled by anybody. Reported in WarnAboutPlant; taken over here.
                tiers.usesTheGamesGoldenRoll[i] = i == 0 && versions[i].UsesTheGamesGoldenChance;
            }
        }

        /// <summary>
        /// The tilled soils the game has, which is where a seed may be planted.
        /// </summary>
        /// <remarks>
        /// Ten grounds, each with a dug-up and a watered form, and the game's own crop seeds list
        /// exactly the two belonging to their own tileset. A mod's crop has no tileset of its own
        /// to belong to, so it takes all twenty and is plantable in any soil a player has hoed.
        /// A tilled soil that a MOD invented is not in this list; a seed cannot yet be restricted
        /// to one, or extended to one.
        /// </remarks>
        private static readonly ObjectID[] EverySoilACropCanBePlantedIn =
        {
            ObjectID.DugUpGround, ObjectID.WateredGround,
            ObjectID.DugUpStoneGround, ObjectID.WateredStoneGround,
            ObjectID.DugUpClayGround, ObjectID.WateredClayGround,
            ObjectID.DugUpCrystalGround, ObjectID.WateredCrystalGround,
            ObjectID.DugUpMeadowGround, ObjectID.WateredMeadowGround,
            ObjectID.DugUpMoldGround, ObjectID.WateredMoldGround,
            ObjectID.DugUpNatureGround, ObjectID.WateredNatureGround,
            ObjectID.DugUpOasisGround, ObjectID.WateredOasisGround,
            ObjectID.DugUpSeaGround, ObjectID.WateredSeaGround,
            ObjectID.DugUpTurfGround, ObjectID.WateredTurfGround
        };

        /// <summary>
        /// The five answers every one of the game's own crop seeds carries and ours carried none of.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHERE IT MAY BE PLANTED was the worst of them. With no placement answer at all, the
        /// game falls back to "anywhere a player can walk", so a modded seed went into grass, into
        /// floor, onto a bridge and onto a rug. It then never grew, because the timer that advances
        /// a crop only ticks while the tile underneath is watered ground — so the crop sat at stage
        /// one forever with nothing in the log, and the author concluded the framework was broken.
        /// The game's own seeds cannot be planted anywhere but soil, which is what makes their
        /// growth reliable rather than what makes it restricted.
        /// </para>
        /// <para>
        /// DIGGING IT BACK UP is the second. The game only lets a placed thing drop itself when it
        /// is diggable or mineable, and the whole loot machinery — the start and finished markers,
        /// the two "do not drop" flags — is only attached to something that already is. A seed
        /// with none of that is permanent scenery: a shovel does nothing, a sword does nothing, and
        /// nothing says why. The game's own seeds are diggable and cannot be attacked.
        /// </para>
        /// <para>
        /// AUTOMATION is the third and fourth. A planter looks for the seed category tag and for
        /// the automation component, and refuses anything missing either — while the framework
        /// already wrote the harvest half on the plant, which is what made the omission look like
        /// an oversight rather than a decision.
        /// </para>
        /// </remarks>
        private static void GiveItEverythingAVanillaSeedHas(GameObject root)
        {
            PlaceableObjectAuthoring placement = EnsureComponent<PlaceableObjectAuthoring>(root);
            placement.canBePlacedOnAnyWalkableTile = false;
            placement.prefabTileSize = Vector2Int.one;
            placement.canBePlacedOnObjects = new List<ObjectID>(EverySoilACropCanBePlantedIn);

            EnsureComponent<DiggableAuthoring>(root);
            EnsureComponent<CantBeAttackedAuthoring>(root);

            DimensionObjectSpine.SetCategoryTag(root, ObjectCategoryTag.Seed, true);
            EnsureComponent<Pug.Automation.AutomatedPlantableSeedAuthoring>(root);
        }

        /// <summary>
        /// A plant that spreads itself instead of being planted.
        /// </summary>
        private static void ApplySpreading(
            GameObject root,
            DimensionPlantAsset plant,
            DimensionPlantGenerationReport report)
        {
            if (!plant.SpreadsOnItsOwn)
            {
                RemoveComponentIfPresent<RootPlantAuthoring>(root);
                return;
            }

            RootPlantAuthoring rootPlant = EnsureRootPlant(root);
            rootPlant.minTimeBetweenSpread = plant.MinSpreadSeconds;
            rootPlant.maxTimeBetweenSpread = plant.MaxSpreadSeconds;
            rootPlant.canGrowOnTilesets = new List<Tileset>();

            string[] tilesets = plant.SpreadsOnTilesetIds;
            for (int i = 0; i < tilesets.Length; i++)
            {
                Tileset tileset;
                if (Enum.TryParse(tilesets[i], false, out tileset))
                {
                    rootPlant.canGrowOnTilesets.Add(tileset);
                }
                else
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' spreads onto '" + tilesets[i] +
                        "', which is not a tileset the game has. It will not spread there.");
                }
            }

            // Its own tileset — the tiles it LAYS DOWN as it spreads, as opposed to the list above
            // of tilesets it may spread onto. Defaults to the first one it can grow on, because a
            // root that creeps across stone and leaves dirt behind reads as a bug.
            Tileset becomes;
            if (!string.IsNullOrEmpty(plant.BecomesTilesetId) &&
                Enum.TryParse(plant.BecomesTilesetId, false, out becomes))
            {
                rootPlant.tileset = becomes;
            }
            else if (!string.IsNullOrEmpty(plant.BecomesTilesetId))
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' lays down '" + plant.BecomesTilesetId +
                    "' as it spreads, which is not a tileset the game has.");
            }
            else if (rootPlant.canGrowOnTilesets.Count > 0)
            {
                rootPlant.tileset = rootPlant.canGrowOnTilesets[0];
            }
        }

        /// <summary>
        /// The growing curve, shared by the seed and every plant prefab.
        /// </summary>
        /// <remarks>
        /// Built in one place so the seed and the plant cannot disagree about how long growing takes —
        /// vanilla authors the settings on both, and a mismatch there would show up as a plant that
        /// changes pace the moment it is planted.
        /// </remarks>
        private static GrowingSettings BuildGrowingSettings(DimensionPlantAsset plant, int currentStage)
        {
            return new GrowingSettings
            {
                highestStage = plant.GrowthStages,
                timeBetweenStages = plant.SecondsBetweenStages,
                currentStage = currentStage,
                keepDamageReductionWhenRipe = plant.StaysToughWhenRipe
            };
        }
    }
}
