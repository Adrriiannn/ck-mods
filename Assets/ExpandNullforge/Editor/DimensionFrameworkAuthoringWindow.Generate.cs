using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Running a build over the authored assets, and everything it warns about.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        /// <summary>
        /// Fills empty portal-item icon slots with the framework default icons once, per template, so a
        /// creator sees them applied on view without dragging. Keeps retrying until the default sprites
        /// have imported (so it self-heals right after the PNGs are dropped in), then stops.
        /// </summary>
        private void TryApplyDefaultPortalIcons()
        {
            if (selectedTemplate == null)
            {
                return;
            }

            int id = selectedTemplate.GetInstanceID();
            if (portalIconsScannedTemplates.Contains(id))
            {
                return;
            }

            DimensionFrameworkAuthoringAssetUtility.ApplyDefaultPortalIcons(selectedTemplate);
            if (DimensionFrameworkAuthoringAssetUtility.DefaultPortalIconsExist())
            {
                portalIconsScannedTemplates.Add(id);
            }
        }

        /// <summary>
        /// Generates the item prefabs into the consumer's own mod folder, then reports exactly
        /// what was written and anything that needs a manual pass.
        /// </summary>
        private void GenerateItemPrefabs(DimensionItemAsset[] items)
        {
            string templatePath = AssetDatabase.GetAssetPath(selectedTemplate);
            string modRoot =
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath);
            if (string.IsNullOrEmpty(modRoot))
            {
                EditorUtility.DisplayDialog(
                    "Generate Items",
                    "Could not resolve the mod folder that owns this template, so there is " +
                    "nowhere to put the generated prefabs. Save the template inside a mod " +
                    "folder first.",
                    "OK");
                return;
            }

            string outputFolder = modRoot + "/Items";

            // Make sure every enabled item portal (V2) has a real, editable item asset before we build,
            // creating a default for any that is missing. Persisting it (rather than synthesizing a
            // throwaway) is what lets the creator open it in the item list and assign an icon/recipe.
            DimensionFrameworkAuthoringAssetActionResult portalItemResult =
                DimensionFrameworkAuthoringAssetUtility.EnsurePortalItems(selectedTemplate);
            DimensionFrameworkAuthoringAssetUtility.ApplyDefaultPortalIcons(selectedTemplate);

            // Same guarantee for tileset blocks: every enabled tileset's toggled-on block kinds get
            // a real item asset (id locked to the tileset's derived block id) before generation.
            DimensionFrameworkAuthoringAssetUtility.EnsureTilesetBlockItems(selectedTemplate);

            // And for food: every dish becomes three items and every golden ingredient a fourth,
            // re-synced from the dish and the ingredient each time so a renamed dish cannot ship
            // under its old name. Must run before the item list is read below.
            DimensionFrameworkAuthoringAssetUtility.EnsureFoodItems(selectedTemplate);
            int portalItemsCreated =
                portalItemResult != null && portalItemResult.CreatedObject != null ? 1 : 0;

            // Re-read after the ensure step so any freshly created portal items are included.
            DimensionItemAsset[] itemsToGenerate = selectedTemplate.GlobalItems;

            // The drop-location inversion, done ONCE for the whole generate. Every generator below
            // is handed the same plan, so a chest and a slime can never disagree about what drops
            // from them, and the walk over every authored item happens once rather than per source.
            DimensionDropPlan drops = DimensionDropPlan.Build(
                itemsToGenerate,
                selectedTemplate.GlobalWorldObjects);

            // The mod's own loot tables, answerable by name for the whole generate — the same
            // law as conditions below. Anything stamping a LootTableID (a creature's drops, a
            // container's becomes-loot) resolves vanilla names first and this mod's tables
            // second, to the exact minted id the runtime registry will carry.
            DimensionEditorLootTables.Seed(selectedTemplate);

            // This mod's own conditions have to be answerable by name for the whole generate, and
            // their numbers depend on the whole set — so they are claimed once, here, and put back
            // afterwards. Without it a creature asking for a custom buff silently gets nothing.
            using (DimensionConditionScope conditions =
                new DimensionConditionScope(selectedTemplate.GlobalConditions))
            {
            if (conditions.Count > 0)
            {
                Debug.Log(
                    "[Dimensions API] " + conditions.Count +
                    " condition(s) of this mod's own are available by name for this generate.");
            }

            // Every name the OTHER generators owe the player, gathered before anything is written.
            // It is built here, inside the condition scope, because a custom stat effect's line is
            // keyed by the number the scope hands it; and it is handed to the item generator
            // because the mod has one localization table and one pass may write it.
            DimensionLocalizationPlan localization = DimensionLocalizationPlan.Build(
                selectedTemplate,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionItemGenerationReport report = DimensionItemGenerator.Generate(
                itemsToGenerate,
                outputFolder,
                EveryRecipeInTheMod(),
                selectedTemplate.Tilesets,
                selectedTemplate.Biomes,
                selectedTemplate.GlobalBosses,
                selectedTemplate.NamedAreas,
                localization,
                OwnedObjectIds(),
                selectedTemplate.GlobalExplosions,
                SwitchedOffObjectIds(),
                // The names the prefabs are actually stamped with, which is not the same list as
                // the ids a creator types. It is what the runtime manifest carries for the
                // world-load check to look at.
                DimensionGeneratedObjectIds.ObjectsMade(selectedTemplate));

            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + report.Errors[i]);
            }

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + report.Warnings[i]);
            }

            // An item that never said what it is has one chosen for it, and that choice decides
            // which slot it lands in and how much use it takes before it breaks. Plain log lines
            // rather than warnings: on a project that predates the question every item is on this
            // list, and a hundred warnings would bury the ones that need doing something about.
            for (int i = 0; i < report.Derived.Count; i++)
            {
                Debug.Log("[Dimensions API] " + report.Derived[i]);
            }

            // Ground fog rides the same action because it is generated FROM the blocks, and a mod
            // whose blocks and whose fog were generated at different moments would ship a fog block
            // describing a tileset id that no longer exists.
            DimensionGroundFogReport fogReport =
                DimensionGroundFogGenerator.Generate(selectedTemplate.Tilesets, modRoot);
            for (int i = 0; i < fogReport.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + fogReport.Warnings[i]);
            }

            string fogSummary = DescribeGroundFog(fogReport);

            DimensionContainerGenerationReport containerReport = DimensionContainerGenerator.Generate(
                selectedTemplate.GlobalContainers,
                outputFolder + "/" + DimensionContainerGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                drops);

            for (int i = 0; i < containerReport.Errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + containerReport.Errors[i]);
            }

            for (int i = 0; i < containerReport.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + containerReport.Warnings[i]);
            }

            string containerSummary =
                containerReport.Created.Count + containerReport.Updated.Count +
                containerReport.Skipped.Count > 0
                    ? "\n\n" + containerReport.Summarize()
                    : string.Empty;

            DimensionCreatureGenerationReport creatureReport = GenerateCreatures(outputFolder, drops);
            for (int i = 0; i < creatureReport.Errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + creatureReport.Errors[i]);
            }

            for (int i = 0; i < creatureReport.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + creatureReport.Warnings[i]);
            }

            string creatureSummary =
                creatureReport.Created.Count + creatureReport.Updated.Count + creatureReport.Skipped.Count > 0
                    ? "\n\n" + creatureReport.Summarize()
                    : string.Empty;

            // Everything else a mod can define. These went unreached for a while — the assets and
            // the generators both existed, and nothing called them — which is exactly the shape of
            // failure that leaves an author staring at a crop they authored and never see in game.
            DimensionWorkbenchGenerationReport workbenchReport = DimensionWorkbenchGenerator.Generate(
                selectedTemplate.GlobalWorkbenches,
                outputFolder + "/" + DimensionWorkbenchGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionPlantGenerationReport plantReport = DimensionPlantGenerator.Generate(
                selectedTemplate.GlobalPlants,
                outputFolder + "/" + DimensionPlantGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionWorldObjectGenerationReport worldObjectReport =
                DimensionWorldObjectGenerator.Generate(
                    selectedTemplate.GlobalWorldObjects,
                    outputFolder + "/" + DimensionWorldObjectGenerator.FolderName,
                    DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                    drops,
                    selectedTemplate.Tilesets);

            DimensionExplosionGenerationReport explosionReport = DimensionExplosionGenerator.Generate(
                selectedTemplate.GlobalExplosions,
                outputFolder + "/" + DimensionExplosionGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionCritterGenerationReport critterReport = DimensionCritterGenerator.Generate(
                selectedTemplate.GlobalCritters,
                outputFolder + "/" + DimensionCritterGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                selectedTemplate.Tilesets);

            // World rules are the one thing here that does not become a prefab. Fishing and talents
            // are written as settings files into the mod's own Conf folder, which the game reads
            // while it starts; upgrade prices and the player's numbers ride the generated bootstrap
            // instead. They go to the MOD ROOT rather than the items folder, because Conf is a
            // folder the game itself looks for and it only looks beside the mod, never inside it.
            DimensionWorldRulesGenerationReport gameSetupReport =
                DimensionWorldRulesGenerator.Generate(selectedTemplate.GlobalGameSetups, modRoot);

            DimensionProjectileGenerationReport projectileReport = DimensionProjectileGenerator.Generate(
                selectedTemplate.GlobalProjectiles,
                outputFolder + "/" + DimensionProjectileGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                selectedTemplate.Tilesets);

            DimensionCreatureGenerationReport vehicleReport =
                DimensionVehicleGenerator.Generate(
                    selectedTemplate.GlobalVehicles,
                    outputFolder + "/" + DimensionVehicleGenerator.FolderName,
                    DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            ReportProblems(workbenchReport.Errors, workbenchReport.Warnings);
            ReportProblems(plantReport.Errors, plantReport.Warnings);
            ReportProblems(worldObjectReport.Errors, worldObjectReport.Warnings);
            ReportProblems(vehicleReport.Errors, vehicleReport.Warnings);
            ReportProblems(projectileReport.Errors, projectileReport.Warnings);
            ReportProblems(critterReport.Errors, critterReport.Warnings);
            ReportProblems(explosionReport.Errors, explosionReport.Warnings);
            ReportProblems(gameSetupReport.Errors, gameSetupReport.Warnings);

            string otherSummary =
                Describe("Crafting stations", workbenchReport.Created.Count, workbenchReport.Updated.Count, workbenchReport.Skipped.Count) +
                Describe("Plants", plantReport.Created.Count, plantReport.Updated.Count, plantReport.Skipped.Count) +
                Describe("Objects", worldObjectReport.Created.Count, worldObjectReport.Updated.Count, worldObjectReport.Skipped.Count) +
                Describe("Vehicles", vehicleReport.Created.Count, vehicleReport.Updated.Count, vehicleReport.Skipped.Count) +
                Describe("Projectiles", projectileReport.Created.Count, projectileReport.Updated.Count, projectileReport.Skipped.Count) +
                Describe("Critters", critterReport.Created.Count, critterReport.Updated.Count, critterReport.Skipped.Count) +
                Describe("Explosions", explosionReport.Created.Count, explosionReport.Updated.Count, explosionReport.Skipped.Count) +
                Describe("World rules", gameSetupReport.Created.Count, gameSetupReport.Updated.Count, gameSetupReport.Skipped.Count);

            // A drop naming a source that nothing generates is completely silent: no error, no
            // creature carrying it, and an item that simply never turns up. This is the only moment
            // the two halves are both known, so it is the only place the typo can be caught.
            WarnAboutDropsFromNowhere(drops);
            WarnAboutIdsThatShadowTheGame();
            WarnAboutPortalCostsFromNowhere(outputFolder, report.Warnings);

            // And the same shape of silence for names: an object built by a generator nobody
            // remembered to give a name to reaches the player showing its own key. Every generator
            // above has closed its asset batch by now, which is what makes the prefabs readable.
            WarnAboutObjectsWithNoName(
                report.LocalizationKeys,
                report,
                containerReport,
                creatureReport,
                workbenchReport,
                plantReport,
                worldObjectReport,
                explosionReport,
                critterReport,
                projectileReport,
                vehicleReport);

            // A mod that ships a pooled-prefab bank pools ONLY what that bank lists, and a body
            // with no pool is a KeyNotFoundException thrown from inside the game's own draw loop
            // the first time that object comes on screen — not a missing picture. Every body these
            // generators just wrote is exposed to it, so the bank is brought back into step here,
            // once, after every generator has closed its asset batch and the prefabs are readable.
            DimensionPooledPrefabBankUtility.Result pooledPrefabs =
                DimensionPooledPrefabBankUtility.EnsureGeneratedPrefabsArePooled(
                    modRoot,
                    delegate(string message) { Debug.LogWarning("[Dimensions API] " + message); });

            string portalItemSummary = portalItemsCreated > 0
                ? "\n\n" + portalItemResult.Message +
                  " Open it under Resources ▸ Items to set its Icon sprite, then generate again."
                : string.Empty;

            EditorUtility.DisplayDialog(
                "Generate Items",
                report.Summarize() + creatureSummary + containerSummary + otherSummary +
                fogSummary + pooledPrefabs.Summarize() + portalItemSummary +
                "\n\nOutput: " + outputFolder +
                (report.HasProblems
                    ? "\n\nDetails were written to the Console."
                    : string.Empty),
                "OK");
            }

            DimensionEditorLootTables.Clear();
        }

        /// <summary>
        /// Turns the dimension's authored mobs, bosses and animals into real prefabs.
        /// </summary>
        /// <remarks>
        /// All three shapes go through one generator because a prefab does not care which authoring
        /// asset it came from — what differs is what an author is offered, not what the game needs.
        /// Bosses are marked as such because Core Keeper treats them differently in several places.
        /// Every one of them is an enemy in the game's sense of the word — the tag is what carries
        /// <c>LastAttackerCD</c> — and what separates a cow from a caveling is the Temperament each
        /// asset carries, which the generator turns into attack tags and a chase distance.
        /// </remarks>
        /// <summary>
        /// Every id this template turns into an object, so a reference to one of them is qualified.
        /// </summary>
        /// <remarks>
        /// Handed to every generator that is not the item generator. Without it those runs owned
        /// nothing, and <c>QualifyReference</c> — which is "qualify it only if it is ours" — was the
        /// identity function: a workbench recipe outputting the mod's own item shipped the bare id
        /// while the item had registered under the qualified one, and the station crafted nothing.
        /// </remarks>
        private List<string> OwnedObjectIds()
        {
            return DimensionGeneratedObjectIds.Collect(selectedTemplate);
        }

        /// <summary>
        /// The ids of this mod's own assets that are unticked, so a reference to one can be told
        /// apart from a misspelling.
        /// </summary>
        /// <remarks>
        /// The bootstrap emitter's binder has always been given this. The generators' were not, so
        /// the same unticked projectile got "one of yours but is switched off" from one half of a
        /// generate and "neither one of this mod's nor one the game has" from the other.
        /// </remarks>
        private List<string> SwitchedOffObjectIds()
        {
            return DimensionGeneratedObjectIds.SwitchedOff(selectedTemplate);
        }

        /// <summary>
        /// Every recipe the mod has, whether it sits in the mod's recipe list or only on a
        /// Workbench.
        /// </summary>
        /// <remarks>
        /// WHAT A CRAFT COSTS IS STORED ON THE ITEM IT MAKES, so the item generator is the only
        /// pass that can write it — and it was only being shown the mod's own recipe list. A
        /// Workbench also carries a list of its own, and the bootstrap already registers from that
        /// one. So a recipe added straight to a Workbench appeared at the bench and cost nothing at
        /// all, with no word said. Both lists are handed over now; the same asset in both is
        /// recognised as one recipe.
        /// </remarks>
        private List<DimensionRecipeAsset> EveryRecipeInTheMod()
        {
            List<DimensionRecipeAsset> all = new List<DimensionRecipeAsset>();
            if (selectedTemplate == null)
            {
                return all;
            }

            DimensionRecipeAsset[] global = selectedTemplate.GlobalRecipes;
            for (int i = 0; global != null && i < global.Length; i++)
            {
                if (global[i] != null && !all.Contains(global[i]))
                {
                    all.Add(global[i]);
                }
            }

            DimensionWorkbenchAsset[] benches = selectedTemplate.GlobalWorkbenches;
            for (int b = 0; benches != null && b < benches.Length; b++)
            {
                if (benches[b] == null || !benches[b].Enabled)
                {
                    continue;
                }

                DimensionRecipeAsset[] benchRecipes = benches[b].Recipes;
                for (int i = 0; benchRecipes != null && i < benchRecipes.Length; i++)
                {
                    if (benchRecipes[i] != null && !all.Contains(benchRecipes[i]))
                    {
                        all.Add(benchRecipes[i]);
                    }
                }
            }

            return all;
        }

        private DimensionCreatureGenerationReport GenerateCreatures(
            string outputFolder,
            DimensionDropPlan drops)
        {
            List<DimensionCreatureGenerator.Request> requests =
                new List<DimensionCreatureGenerator.Request>();

            DimensionMobAsset[] mobs = selectedTemplate.GlobalMobs;
            if (mobs != null)
            {
                for (int i = 0; i < mobs.Length; i++)
                {
                    DimensionMobAsset mob = mobs[i];
                    if (mob == null)
                    {
                        continue;
                    }

                    requests.Add(new DimensionCreatureGenerator.Request
                    {
                        CreatureId = mob.MobId,
                        DisplayName = mob.DisplayName,
                        Stats = mob.CreatureStats,
                        Combat = mob.Combat,
                        SimpleTraits = mob.SimpleTraits,
                        ExtraLoot = mob.ExtraLoot,
                        Pet = mob.Pet,
                        DropsFromItems = drops.For(DimensionDropSourceKind.Creature, mob.MobId),
                        LootTable = mob.LootTable,
                        BehaviourName = mob.BehaviorScriptId,
                        IsEnemy = true,
                        Aggression = mob.Aggression,
                        IsBoss = false,
                        Hatching = mob.Hatching,
                        Visual = mob.Visual,
                        Audio = mob.Audio,
                        Enabled = mob.Enabled
                    });

                    // An elite is a second creature, not a mode of the first. It borrows the same
                    // behaviour and art and differs only in the numbers, which is exactly what the
                    // game can already express without any new machinery.
                    DimensionEliteVariantTemplate elite = mob.EliteVariant;
                    if (elite.Enabled)
                    {
                        // The two halves of the elite do not both work on both kinds of stat
                        // block, and which half is doing nothing is invisible in the prefab. On a
                        // level-scaled creature the game recomputes health, damage reduction and
                        // attack damage from the level every time, so only the level bump lands.
                        if (!mob.CreatureStats.UsesAuthoredNumbers &&
                            elite.ExtraLevels == 0)
                        {
                            Debug.LogWarning(
                                "[Dimensions API] '" + mob.DisplayName + "' takes its numbers from " +
                                "the area level curve, and its elite is set to no extra levels. The " +
                                "health, damage and armour multipliers are recomputed from the " +
                                "level on a creature like this, so the elite will be identical to " +
                                "the ordinary one. Give it at least one extra level, or type the " +
                                "mob's numbers under Stats.");
                        }

                        requests.Add(new DimensionCreatureGenerator.Request
                        {
                            CreatureId = DimensionEliteVariantTemplate.IdFor(mob.MobId),
                            DisplayName = elite.DisplayNameFor(mob.DisplayName),
                            Stats = mob.CreatureStats.ScaledForElite(elite, 1, true, false),

                            // The level bump is what makes a level-scaled elite actually harder;
                            // on a creature with typed numbers the multipliers have already done
                            // the work and the rarity is only the colour of its name.
                            Rarity = (Rarity)elite.ExtraLevels,
                            Combat = mob.Combat,
                            SimpleTraits = mob.SimpleTraits,
                            ExtraLoot = mob.ExtraLoot,
                            Pet = mob.Pet,
                            DropsFromItems = drops.For(
                                DimensionDropSourceKind.Creature,
                                DimensionEliteVariantTemplate.IdFor(mob.MobId)),
                            LootTable = elite.LootTable != null ? elite.LootTable : mob.LootTable,
                            BehaviourName = mob.BehaviorScriptId,
                            IsEnemy = true,
                            Aggression = mob.Aggression,
                            IsBoss = false,

                            // Same art and same voice as the mob it is a harder copy of: an elite
                            // is the same creature with different numbers, and giving it its own
                            // clip list would mean drawing every elite twice.
                            Visual = mob.Visual,
                            Audio = mob.Audio,
                            Enabled = mob.Enabled
                        });
                    }
                }
            }

            DimensionBossAsset[] bosses = selectedTemplate.GlobalBosses;
            if (bosses != null)
            {
                for (int i = 0; i < bosses.Length; i++)
                {
                    DimensionBossAsset boss = bosses[i];
                    if (boss == null)
                    {
                        continue;
                    }

                    requests.Add(new DimensionCreatureGenerator.Request
                    {
                        CreatureId = boss.BossId,
                        DisplayName = boss.DisplayName,
                        Stats = boss.CreatureStats,
                        Combat = boss.Combat,
                        SimpleTraits = boss.SimpleTraits,
                        BorrowedKit = boss.BorrowedKit,
                        MoreBorrowedKits = boss.MoreBorrowedKits,
                        TheRestOfTheKits = boss.TheRestOfTheKits,
                        BossChest = boss.BossChest,
                        DropsFromItems = drops.For(DimensionDropSourceKind.Creature, boss.BossId),
                        LootTable = boss.LootTable,
                        IsEnemy = true,

                        // Bosses were pinned to Hostile here, on the reasoning that a fight
                        // nobody can start is not a boss fight. A boss that ignores you until you
                        // touch it is a real shape and the framework was refusing to build it, so
                        // the boss now answers the same question a mob does. Its own default is
                        // Hostile, which is what almost every boss is.
                        Aggression = boss.Aggression,
                        IsBoss = true,
                        MapPin = boss.MapPin,
                        FightMusic = boss.FightMusic,
                        BodySprite = boss.Visual.BodySprite,
                        Visual = boss.Visual,
                        Audio = boss.Audio,
                        SummoningItemId = boss.SummoningItemId,
                        Enabled = boss.Enabled
                    });
                }
            }

            DimensionAnimalAsset[] animals = selectedTemplate.GlobalAnimals;
            if (animals != null)
            {
                for (int i = 0; i < animals.Length; i++)
                {
                    DimensionAnimalAsset animal = animals[i];
                    if (animal == null)
                    {
                        continue;
                    }

                    requests.Add(new DimensionCreatureGenerator.Request
                    {
                        CreatureId = animal.AnimalId,
                        DisplayName = animal.DisplayName,
                        Stats = animal.CreatureStats,
                        Combat = animal.Combat,
                        SimpleTraits = animal.SimpleTraits,
                        DropsFromItems = drops.For(DimensionDropSourceKind.Creature, animal.AnimalId),
                        LootTable = animal.LootTable,

                        Visual = animal.Visual,
                        Audio = animal.Audio,

                        // The game's own Cow and Roly Poly BOTH carry EnemyAuthoring, and they are
                        // as harmless as animals get — what makes them harmless is their empty
                        // attack tags, which is what Temperament now writes. Withholding the tag
                        // instead cost the animal its LastAttackerCD (so a Defensive animal could
                        // never hit back) and made it invisible to explosions and pushback.
                        IsEnemy = true,
                        Aggression = animal.Aggression,
                        IsBoss = false,
                        Enabled = animal.Enabled
                    });
                }
            }

            if (requests.Count == 0)
            {
                return new DimensionCreatureGenerationReport();
            }

            return DimensionCreatureGenerator.Generate(
                requests,
                outputFolder + "/" + DimensionCreatureGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));
        }

        /// <summary>
        /// Reports anything this run built that a player would meet without a name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each generator is named explicitly rather than collected through a shared interface,
        /// because there is no shared interface and inventing one to make this line shorter would
        /// touch ten files to save four. The cost of forgetting a generator here is one missed
        /// warning; the cost of the check not existing at all was a whole class of content shipping
        /// with its key printed in the tooltip.
        /// </para>
        /// <para>
        /// Reports carry prefab PATHS, so the objects are read back off disk. That is deliberate:
        /// asking the prefabs what they are called is the only question that keeps working when
        /// somebody adds a generator, or a second object inside an existing one.
        /// </para>
        /// </remarks>
        private static void WarnAboutObjectsWithNoName(
            ICollection<string> writtenKeys,
            DimensionItemGenerationReport items,
            DimensionContainerGenerationReport containers,
            DimensionCreatureGenerationReport creatures,
            DimensionWorkbenchGenerationReport workbenches,
            DimensionPlantGenerationReport plants,
            DimensionWorldObjectGenerationReport worldObjects,
            DimensionExplosionGenerationReport explosions,
            DimensionCritterGenerationReport critters,
            DimensionProjectileGenerationReport projectiles,
            DimensionCreatureGenerationReport vehicles)
        {
            List<string> paths = new List<string>();
            AddPaths(paths, items == null ? null : items.Created, items == null ? null : items.Updated);
            AddPaths(paths, containers == null ? null : containers.Created, containers == null ? null : containers.Updated);
            AddPaths(paths, creatures == null ? null : creatures.Created, creatures == null ? null : creatures.Updated);
            AddPaths(paths, workbenches == null ? null : workbenches.Created, workbenches == null ? null : workbenches.Updated);
            AddPaths(paths, plants == null ? null : plants.Created, plants == null ? null : plants.Updated);
            AddPaths(paths, worldObjects == null ? null : worldObjects.Created, worldObjects == null ? null : worldObjects.Updated);
            AddPaths(paths, explosions == null ? null : explosions.Created, explosions == null ? null : explosions.Updated);
            AddPaths(paths, critters == null ? null : critters.Created, critters == null ? null : critters.Updated);
            AddPaths(paths, projectiles == null ? null : projectiles.Created, projectiles == null ? null : projectiles.Updated);
            AddPaths(paths, vehicles == null ? null : vehicles.Created, vehicles == null ? null : vehicles.Updated);

            List<string> warnings = new List<string>();
            DimensionLocalizationCoverage.Check(
                DimensionLocalizationCoverage.ReadGeneratedObjects(paths),
                writtenKeys,
                warnings);

            for (int i = 0; i < warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + warnings[i]);
            }
        }

        private static void AddPaths(List<string> paths, List<string> created, List<string> updated)
        {
            if (created != null)
            {
                paths.AddRange(created);
            }

            if (updated != null)
            {
                paths.AddRange(updated);
            }
        }

        /// <summary>Sends a generator's problems to the Console the same way for every generator.</summary>
        private static void ReportProblems(List<string> errors, List<string> warnings)
        {
            for (int i = 0; errors != null && i < errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + errors[i]);
            }

            for (int i = 0; warnings != null && i < warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + warnings[i]);
            }
        }

        /// <summary>
        /// One summary line, or nothing when a creator has never used that kind of thing.
        /// </summary>
        /// <remarks>
        /// Silent when there is nothing to say, for the same reason ground fog is: somebody who has
        /// never authored a plant should not read "Plants: 0 created" on every generate.
        /// </remarks>
        private static string Describe(string label, int created, int updated, int skipped)
        {
            if (created + updated + skipped == 0)
            {
                return string.Empty;
            }

            return "\n\n" + label + ": " + created + " created, " + updated + " updated, " +
                skipped + " skipped.";
        }

        /// <summary>
        /// Warns about drops that name a source nothing in the mod defines.
        /// </summary>
        /// <remarks>
        /// This failure is completely silent otherwise. An item that says it drops from "gaint_slime"
        /// generates without complaint, no creature carries it, and the author plays their own mod
        /// hunting for something that can never appear. Generation is the only moment both halves —
        /// what drops, and what exists to drop it — are known at once, so it is the only place the
        /// typo can be caught.
        /// </remarks>
        /// <summary>
        /// Warns when one of the mod's own ids is also the name of one of the game's objects.
        /// </summary>
        /// <remarks>
        /// THE GAME'S NAME WINS EVERYWHERE, and that has to be said out loud. Calling one of your
        /// objects Torch is allowed and the object generates perfectly well — but every reference
        /// anywhere in the mod that types "Torch" means the game's torch, because the binder asks
        /// the ObjectID enum first and bakes the number it finds. Without this line the only way to
        /// discover that is to notice a recipe quietly making the wrong thing.
        /// </remarks>
        private void WarnAboutIdsThatShadowTheGame()
        {
            List<string> owned = OwnedObjectIds();
            for (int i = 0; i < owned.Count; i++)
            {
                string local = owned[i];
                if (DimensionObjectBinder.Vanilla(local) == ObjectID.None)
                {
                    continue;
                }

                Debug.LogWarning(
                    "[Dimensions API] One of your objects is called '" + local + "', which is also " +
                    "the name of one of the game's. Your object is still made, but anywhere in this " +
                    "mod that types '" + local + "' — a recipe ingredient, a drop, a shot, a trader's " +
                    "stock — means the GAME'S '" + local + "', not yours. Rename yours if you meant " +
                    "to point at it.");
            }
        }

        /// <summary>
        /// Warns when a portal asks for an item nothing answers to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS ONE SHIPS A DOOR THAT CAN NEVER OPEN. The item a portal asks for is typed into a
        /// text box and was checked nowhere: not while authoring, not at generate, not at run time.
        /// A misspelling generates cleanly, the portal is built, the slot is drawn, and no item in
        /// the game will ever go into it — with nothing said anywhere.
        /// </para>
        /// <para>
        /// Asked through <see cref="DimensionObjectBinder"/>, which is the one predicate in the
        /// framework that can tell the game's own names, this mod's own names and a typo apart, and
        /// which the generators and the bootstrap emitter already decide references with. Asking it
        /// here is what keeps this answer and theirs the same answer.
        /// </para>
        /// <para>
        /// ONLY THE RULES THAT REALLY ASK FOR SOMETHING. It used to walk <c>RequiredItems</c> on
        /// every enabled rule, and the list is kept when a creator switches "When it opens" to a
        /// mode that never reads it — <c>AppendTravelRequirements</c> returns immediately unless
        /// <c>UsesRequiredItems</c>, and the Access page hides the offering card entirely — so
        /// generating after that switch warned about entries the creator could neither see on the
        /// page nor act on. An item portal is skipped for the same reason: the page says in as many
        /// words that a portal torn open by an item has no window, so its offering is never asked
        /// for.
        /// </para>
        /// <para>
        /// The messages go to the generate report as well as the Console, because the report is
        /// what the creator is shown when the run finishes.
        /// </para>
        /// </remarks>
        private void WarnAboutPortalCostsFromNowhere(
            string outputFolder,
            List<string> intoReportWarnings)
        {
            DimensionPortalAccessRuleAsset[] rules = selectedTemplate == null
                ? null
                : selectedTemplate.PortalAccessRules;
            if (rules == null || rules.Length == 0)
            {
                return;
            }

            DimensionObjectBinder binder = new DimensionObjectBinder(
                DimensionNamingContext.ForOutputFolder(
                    outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !rule.Enabled || !rule.UsesRequiredItems || rule.IsItemPortal)
                {
                    continue;
                }

                string which = string.IsNullOrEmpty(rule.DisplayName)
                    ? (string.IsNullOrEmpty(rule.RuleId) ? rule.name : rule.RuleId)
                    : rule.DisplayName;

                DimensionPortalRequiredItemTemplate[] items = rule.RequiredItems;
                for (int n = 0; n < items.Length; n++)
                {
                    string itemId = items[n].ItemId;
                    if (string.IsNullOrEmpty(itemId))
                    {
                        // IT DOES NOT HOLD THE DOOR SHUT, and this line used to say it did.
                        // AppendTravelRequirements skips an entry with no ItemId outright
                        // (DimensionPortalAccessRuleAsset.AppendTravelRequirements), so an empty
                        // slot contributes no requirement at all — the portal opens for whatever
                        // the filled slots ask for. The fault is a row that does nothing, not a
                        // locked door, and saying the wrong one sends the reader looking for a
                        // player who cannot get through.
                        Say(
                            intoReportWarnings,
                            "The portal '" + which + "' has an offering row with no item in it. " +
                            "The row is ignored, so it asks for nothing and holds nothing shut — " +
                            "name the item you meant, or take the row out.");
                        continue;
                    }

                    ObjectID baked;
                    if (binder.TryBind(itemId, out baked))
                    {
                        continue;
                    }

                    string switchedOff = binder.ExplainIfSwitchedOff(
                        "The portal '" + which + "' asks for", itemId);
                    if (switchedOff != null)
                    {
                        Say(intoReportWarnings, switchedOff);
                        continue;
                    }

                    Say(
                        intoReportWarnings,
                        "The portal '" + which + "' asks for '" + itemId +
                        "', and nothing in the game or in this mod is called that. The portal is " +
                        "still built and its slot is still drawn, and no item will ever go into " +
                        "it, so no player can open the door — check the spelling against the " +
                        "item you meant.");
                }
            }
        }

        /// <summary>
        /// One warning, said to the Console and carried into the run's own report.
        /// </summary>
        /// <remarks>
        /// The Console alone is where this went, and a creator who has not opened the Console never
        /// sees it. The report is what the generate summary counts and what the dialog at the end
        /// of the run points at, so a portal that can never open is now part of the answer to
        /// "what did that do" rather than a line somebody has to go looking for.
        /// </remarks>
        private static void Say(List<string> intoReportWarnings, string message)
        {
            Debug.LogWarning("[Dimensions API] " + message);
            if (intoReportWarnings != null)
            {
                intoReportWarnings.Add(message);
            }
        }

        private void WarnAboutDropsFromNowhere(DimensionDropPlan drops)
        {
            if (drops == null || drops.IsEmpty)
            {
                return;
            }

            HashSet<string> defined = new HashSet<string>();
            AddIds(defined, selectedTemplate.GlobalMobs, delegate(DimensionMobAsset a) { return a.MobId; });
            AddIds(defined, selectedTemplate.GlobalMobs, delegate(DimensionMobAsset a)
            {
                return a.EliteVariant.Enabled ? DimensionEliteVariantTemplate.IdFor(a.MobId) : null;
            });
            AddIds(defined, selectedTemplate.GlobalBosses, delegate(DimensionBossAsset a) { return a.BossId; });
            AddIds(defined, selectedTemplate.GlobalAnimals, delegate(DimensionAnimalAsset a) { return a.AnimalId; });
            AddIds(defined, selectedTemplate.GlobalCritters, delegate(DimensionCritterAsset a) { return a.CritterId; });
            AddIds(defined, selectedTemplate.GlobalContainers, delegate(DimensionContainerAsset a) { return a.ContainerId; });
            AddIds(defined, selectedTemplate.GlobalWorldObjects, delegate(DimensionWorldObjectAsset a) { return a.ObjectIdentifier; });
            AddIds(defined, selectedTemplate.GlobalScenes, delegate(SceneTemplateAsset a) { return a.SceneId; });

            List<string> unknown = drops.SourcesNothingDefines(defined);
            for (int i = 0; i < unknown.Count; i++)
            {
                Debug.LogWarning(
                    "[Dimensions API] Something is set to drop from '" + unknown[i] +
                    "', but nothing in this dimension is called that. Nothing will drop, and " +
                    "nothing else will say so — check the spelling against the creature, chest, " +
                    "object or scene you meant.");
            }
        }

        private static void AddIds<T>(HashSet<string> into, T[] assets, System.Func<T, string> idOf)
            where T : UnityEngine.Object
        {
            for (int i = 0; assets != null && i < assets.Length; i++)
            {
                if (assets[i] == null)
                {
                    continue;
                }

                string id = idOf(assets[i]);
                if (!string.IsNullOrEmpty(id))
                {
                    into.Add(id);
                }
            }
        }

        /// <summary>
        /// One line about ground fog, or nothing when no block uses it.
        /// </summary>
        /// <remarks>
        /// Silent when there is nothing to say. A creator who has never touched fog should not have to
        /// read "0 ground fog blocks" every time they generate items.
        /// </remarks>
        private static string DescribeGroundFog(DimensionGroundFogReport fogReport)
        {
            int touched = fogReport.Created.Count + fogReport.Updated.Count + fogReport.Removed.Count;
            if (touched == 0)
            {
                return string.Empty;
            }

            return "\n\nGround fog: " + fogReport.Created.Count + " created, " +
                fogReport.Updated.Count + " updated, " + fogReport.Removed.Count + " removed.";
        }
    }
}
