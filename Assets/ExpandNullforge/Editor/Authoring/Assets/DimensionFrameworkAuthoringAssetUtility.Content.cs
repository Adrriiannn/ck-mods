using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Making one of the other authored assets, and taking one away.
    /// </summary>
    internal static partial class DimensionFrameworkAuthoringAssetUtility
    {
        public static DimensionFrameworkAuthoringAssetActionResult CreateBiome(
            DimensionTemplateAsset template,
            BiomeTemplateAsset source)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a biome.");
            }

            int index = template.Biomes.Length + 1;
            string biomeId = ResolveScopedId(template, "Biome" + index.ToString());
            BiomeTemplateAsset biome = source == null
                ? CreateAsset<BiomeTemplateAsset>(template, "Biomes", "Biome" + index.ToString())
                : DuplicateAsset(template, "Biomes", "Biome" + index.ToString(), source);
            biome.ConfigureIdentity(
                biomeId,
                "Biome " + index.ToString(),
                ResolveMapColor(index),
                index,
                true);
            biome.ApplyFallbackLocalBounds(
                new Vector2Int(-64 + index * 128, -64),
                new Vector2Int(64 + index * 128, 64));
            // Ground and Walls start EMPTY. Pre-filled with ids of the shape
            // "<mod>:GroundBiome1Block", which no block a modder can make ever produces — a
            // generated block's id is "<mod>:<block name>.ground.block" — a new biome opens
            // with two entries that can never resolve, and the world builds dirt while the page
            // shows a full list. An empty list says "you have not picked yet", which is true.
            biome.ApplySemanticTerrainPreset(
                new string[0],
                new string[0],
                new string[0],
                source == null);
            AppendObjectReference(template, "biomes", biome);
            SaveAndSelect(biome);
            return Success(biome, source == null ? "Created a biome." : "Duplicated the selected biome.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult RemoveBiomeReference(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null || biome == null)
            {
                return Failure("Select a biome reference before removing it.");
            }

            bool removed = RemoveObjectReference(template, "biomes", biome);
            if (!removed)
            {
                return Failure("The selected biome is not referenced by this Dimension Asset.");
            }

            SaveAndSelect(template);
            return Success(template, "Removed the biome reference. The biome asset file was left on disk.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateScene(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a scene.");
            }

            int index = CountScenes(template, biome) + 1;
            string sceneId = ResolveScopedId(template, "Scene" + index.ToString());
            SceneTemplateAsset scene =
                CreateAsset<SceneTemplateAsset>(
                    template,
                    "Scenes",
                    "Scene" + index.ToString());
            scene.ConfigureIdentity(
                sceneId,
                sceneId + ".template",
                "Scene " + index.ToString(),
                "scene",
                string.Empty,
                index,
                true);
            scene.ConfigureSpawnPolicy(1, false, true);
            scene.ApplyAutomaticPlacement(new Vector2Int(16, 16));
            if (biome != null)
            {
                scene.SetAllowedBiomeIds(new[] { biome.BiomeId });
                AppendObjectReference(biome, "scenePool", scene);
            }
            else
            {
                AppendObjectReference(template, "globalScenes", scene);
            }

            SaveAndSelect(scene);
            return Success(scene, biome == null ? "Created a global scene." : "Created a biome scene.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateItem(
            DimensionTemplateAsset template,
            DimensionItemKind kind)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an item.");
            }

            int index = template.GlobalItems.Length + 1;
            DimensionItemAsset item =
                CreateAsset<DimensionItemAsset>(
                    template,
                    "Resources",
                    kind.ToString() + index.ToString());
            SetSerializedString(item, "itemId", ResolveScopedId(template, kind.ToString() + index.ToString()));
            SetSerializedString(item, "displayName", ObjectNames.NicifyVariableName(kind.ToString()) + " " + index.ToString());
            SetSerializedEnum(item, "kind", (int)kind);
            SetSerializedBool(item, "enabled", true);
            AppendObjectReference(template, "globalItems", item);
            SaveAndSelect(item);
            return Success(item, "Created an item definition.");
        }

        private static DimensionItemAsset FindGlobalItem(DimensionTemplateAsset template, string itemId)
        {
            DimensionItemAsset[] items = template.GlobalItems;
            if (items == null)
            {
                return null;
            }

            foreach (DimensionItemAsset item in items)
            {
                if (item != null && string.Equals(item.ItemId, itemId, System.StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static string SanitizeAssetName(string raw)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
            }

            return builder.Length == 0 ? "Block" : builder.ToString();
        }

        /// <summary>
        /// Deletes an item and its whole footprint: the reference, the .asset, and the generated
        /// prefab. Items owned by a block (its wall/ground pair) are refused — deleting the block in
        /// the Tileset Studio removes them properly.
        /// </summary>
        public static DimensionFrameworkAuthoringAssetActionResult DeleteItem(
            DimensionTemplateAsset template,
            DimensionItemAsset item)
        {
            if (template == null || item == null)
            {
                return Failure("Nothing to delete.");
            }

            DimensionTilesetAsset[] tilesets = template.Tilesets;
            if (tilesets != null)
            {
                foreach (DimensionTilesetAsset tileset in tilesets)
                {
                    if (tileset != null &&
                        (string.Equals(item.ItemId, tileset.GroundBlockItemId, System.StringComparison.Ordinal) ||
                         string.Equals(item.ItemId, tileset.WallBlockItemId, System.StringComparison.Ordinal)))
                    {
                        return Failure(
                            "\"" + item.DisplayName + "\" belongs to the block \"" + tileset.BlockName +
                            "\" — delete that block in the Tileset Studio to remove it cleanly.");
                    }
                }
            }

            string label = item.DisplayName;
            DimensionItemGenerator.DeleteGeneratedArtifacts(AssetDatabase.GetAssetPath(template), item.ItemId);
            RemoveObjectReference(template, "globalItems", item);
            string path = AssetDatabase.GetAssetPath(item);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            return Success(null, "Deleted item \"" + label + "\" and its generated prefab.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateRecipe(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a recipe.");
            }

            int index = template.GlobalRecipes.Length + 1;
            DimensionRecipeAsset recipe =
                CreateAsset<DimensionRecipeAsset>(template, "Resources", "Recipe" + index.ToString());
            SetSerializedString(recipe, "recipeId", ResolveScopedId(template, "Recipe" + index.ToString()));
            SetSerializedString(recipe, "displayName", "Recipe " + index.ToString());
            SetSerializedBool(recipe, "enabled", true);
            AppendObjectReference(template, "globalRecipes", recipe);
            SaveAndSelect(recipe);
            return Success(recipe, "Created a recipe definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateWorkbench(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a workbench.");
            }

            int index = template.GlobalWorkbenches.Length + 1;
            DimensionWorkbenchAsset workbench =
                CreateAsset<DimensionWorkbenchAsset>(template, "Resources", "Workbench" + index.ToString());
            SetSerializedString(workbench, "workbenchId", ResolveScopedId(template, "Workbench" + index.ToString()));
            SetSerializedString(workbench, "displayName", "Workbench " + index.ToString());
            SetSerializedBool(workbench, "enabled", true);
            AppendObjectReference(template, "globalWorkbenches", workbench);
            SaveAndSelect(workbench);
            return Success(workbench, "Created a workbench definition.");
        }

        /// <summary>
        /// Creates a chest, stash or display stand.
        /// </summary>
        /// <remarks>
        /// These four creators exist because the assets and the generators both did, and there was
        /// no way to reach either from the window — a creator could only make one by right-clicking
        /// in the Project view and hand-dragging it into the template, which is exactly the
        /// technical detour this framework is for avoiding.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult CreateContainer(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a container.");
            }

            int index = template.GlobalContainers.Length + 1;
            DimensionContainerAsset container =
                CreateAsset<DimensionContainerAsset>(template, "Resources", "Container" + index.ToString());
            SetSerializedString(container, "containerId", ResolveScopedId(template, "Container" + index.ToString()));
            SetSerializedString(container, "displayName", "Container " + index.ToString());
            SetSerializedBool(container, "enabled", true);
            AppendObjectReference(template, "globalContainers", container);
            SaveAndSelect(container);
            return Success(container, "Created a container definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateDungeon(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a dungeon.");
            }

            int index = template.GlobalDungeons.Length + 1;
            DimensionDungeonAsset dungeon =
                CreateAsset<DimensionDungeonAsset>(template, "Scenes", "Dungeon" + index.ToString());
            SetSerializedString(dungeon, "dungeonId", ResolveScopedId(template, "Dungeon" + index.ToString()));
            SetSerializedString(dungeon, "displayName", "Dungeon " + index.ToString());
            SetSerializedBool(dungeon, "enabled", true);
            AppendObjectReference(template, "globalDungeons", dungeon);
            SaveAndSelect(dungeon);
            return Success(dungeon, "Created a dungeon.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateGenerationPass(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a generation pass.");
            }

            int index = template.GlobalGenerationPasses.Length + 1;
            GenerationPassTemplateAsset pass =
                CreateAsset<GenerationPassTemplateAsset>(
                    template, "Generation", "GenerationPass" + index.ToString());
            SetSerializedString(pass, "passId", ResolveScopedId(template, "Pass" + index.ToString()));
            SetSerializedString(pass, "displayName", "Pass " + index.ToString());
            SetSerializedBool(pass, "enabled", true);
            AppendObjectReference(template, "globalGenerationPasses", pass);
            SaveAndSelect(pass);
            return Success(pass, "Created a generation pass.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateNamedArea(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a named area.");
            }

            int index = template.NamedAreas.Length + 1;
            DimensionNamedAreaAsset area =
                CreateAsset<DimensionNamedAreaAsset>(template, "Biomes", "NamedArea" + index.ToString());
            SetSerializedString(area, "areaId", ResolveScopedId(template, "NamedArea" + index.ToString()));
            SetSerializedString(area, "displayName", "Named Area " + index.ToString());
            SetSerializedBool(area, "enabled", true);
            AppendObjectReference(template, "namedAreas", area);
            SaveAndSelect(area);
            return Success(area, "Created a named area.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateRoomFilling(
            DimensionTemplateAsset template,
            DimensionDungeonAsset dungeon)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a room filling.");
            }

            // With a dungeon in hand the filling attaches itself. Without one — which is what the
            // page's create button can offer, since it hands over the dimension rather than the
            // selection — attach it anyway when there is only one dungeon it could belong to. A
            // filling that belongs to nothing is a file the creator has to find and drag before it
            // has any effect, and nothing on screen said so.
            if (dungeon == null)
            {
                DimensionDungeonAsset[] dungeons = template.GlobalDungeons;
                int found = 0;
                for (int i = 0; i < dungeons.Length; i++)
                {
                    if (dungeons[i] != null)
                    {
                        found++;
                        dungeon = dungeons[i];
                    }
                }

                if (found != 1)
                {
                    dungeon = null;
                }
            }

            string stem = dungeon == null ? "RoomFilling" : NormalizeIdToken(dungeon.DungeonId, "Asset") + "Filling";
            int index = (dungeon == null ? 0 : dungeon.RoomFillings.Length) + 1;
            DimensionRoomFillingAsset filling =
                CreateAsset<DimensionRoomFillingAsset>(template, "Scenes", stem + index.ToString());
            SetSerializedString(filling, "fillingId",
                ResolveScopedId(
                    template,
                    (dungeon == null ? "filling" : dungeon.DungeonId + "-filling") + index.ToString()));
            SetSerializedString(filling, "displayName", "Room Filling " + index.ToString());
            SetSerializedBool(filling, "enabled", true);
            if (dungeon != null)
            {
                AppendObjectReference(dungeon, "roomFillings", filling);
            }

            SaveAndSelect(filling);
            return Success(
                filling,
                dungeon == null
                    ? "Created a room filling. Open a dungeon and add it under Room Fillings, then " +
                      "open it there to say what it places."
                    : "Created a room filling on '" + dungeon.DisplayName +
                      "'. Open it under that dungeon's Room Fillings to say what it places.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreatePlant(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a plant.");
            }

            int index = template.GlobalPlants.Length + 1;
            DimensionPlantAsset plant =
                CreateAsset<DimensionPlantAsset>(template, "Resources", "Plant" + index.ToString());
            SetSerializedString(plant, "plantId", ResolveScopedId(template, "Plant" + index.ToString()));
            SetSerializedString(plant, "displayName", "Plant " + index.ToString());
            SetSerializedBool(plant, "enabled", true);
            AppendObjectReference(template, "globalPlants", plant);
            SaveAndSelect(plant);
            return Success(plant, "Created a plant. Generating emits its seed, its plant and its ripe plant.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateWorldObject(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an object.");
            }

            int index = template.GlobalWorldObjects.Length + 1;
            DimensionWorldObjectAsset worldObject =
                CreateAsset<DimensionWorldObjectAsset>(template, "Resources", "Object" + index.ToString());
            SetSerializedString(worldObject, "objectIdentifier", ResolveScopedId(template, "Object" + index.ToString()));
            SetSerializedString(worldObject, "displayName", "Object " + index.ToString());
            SetSerializedBool(worldObject, "enabled", true);
            AppendObjectReference(template, "globalWorldObjects", worldObject);
            SaveAndSelect(worldObject);
            return Success(worldObject, "Created a placed object definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateVehicle(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a vehicle.");
            }

            int index = template.GlobalVehicles.Length + 1;
            DimensionVehicleAsset vehicle =
                CreateAsset<DimensionVehicleAsset>(template, "Resources", "Vehicle" + index.ToString());
            SetSerializedString(vehicle, "vehicleId", ResolveScopedId(template, "Vehicle" + index.ToString()));
            SetSerializedString(vehicle, "displayName", "Vehicle " + index.ToString());
            SetSerializedBool(vehicle, "enabled", true);
            AppendObjectReference(template, "globalVehicles", vehicle);
            SaveAndSelect(vehicle);
            return Success(vehicle, "Created a vehicle definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateProjectile(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a projectile.");
            }

            int index = template.GlobalProjectiles.Length + 1;
            DimensionProjectileAsset projectile =
                CreateAsset<DimensionProjectileAsset>(template, "Resources", "Projectile" + index.ToString());
            SetSerializedString(projectile, "projectileId", ResolveScopedId(template, "Projectile" + index.ToString()));
            SetSerializedString(projectile, "displayName", "Projectile " + index.ToString());
            SetSerializedBool(projectile, "enabled", true);
            AppendObjectReference(template, "globalProjectiles", projectile);
            SaveAndSelect(projectile);
            return Success(projectile, "Created a projectile. Name it from a weapon or a creature to fire it.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateExplosion(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an explosion.");
            }

            int index = template.GlobalExplosions.Length + 1;
            DimensionExplosionAsset explosion =
                CreateAsset<DimensionExplosionAsset>(template, "Resources", "Explosion" + index.ToString());
            SetSerializedString(explosion, "explosionId", ResolveScopedId(template, "Explosion" + index.ToString()));
            SetSerializedString(explosion, "displayName", "Explosion " + index.ToString());
            SetSerializedBool(explosion, "enabled", true);
            AppendObjectReference(template, "globalExplosions", explosion);
            SaveAndSelect(explosion);
            return Success(explosion, "Created an explosion. Name it from a bomb to make that bomb use it.");
        }

        /// <summary>
        /// Creates a stat effect (a condition) and registers it on the dimension.
        /// </summary>
        /// <remarks>
        /// The runtime for these shipped long before this button existed: a registered condition
        /// claims its number by NAME ordering across the whole set, so the only thing that
        /// matters here is that the name is scoped and stable from birth. Effects was the one
        /// tab a creator could browse but never add to — every other kind had a creator, and
        /// the asset-menu route it relied on is not a door this framework counts.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult CreateCondition(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an effect.");
            }

            int index = template.GlobalConditions.Length + 1;
            DimensionConditionAsset condition =
                CreateAsset<DimensionConditionAsset>(template, "Resources", "Effect" + index.ToString());
            SetSerializedString(condition, "conditionName", ResolveScopedId(template, "Effect" + index.ToString()));
            SetSerializedString(condition, "displayName", "Effect " + index.ToString());
            SetSerializedBool(condition, "enabled", true);
            AppendObjectReference(template, "globalConditions", condition);
            SaveAndSelect(condition);
            return Success(condition, "Created an effect. Name it from an item or a food to hand it out.");
        }

        /// <summary>
        /// Adds a rule set — the four things a mod changes about the game rather than adds to.
        /// </summary>
        /// <remarks>
        /// The World Rules stage had no way of making one at all: a creator could see the list and
        /// never add to it, which is how the whole stage sat unreachable while everything under it
        /// was written and tested. One rule set is usually enough for a mod, but the list stays a
        /// list because two mods' rule sets merge and a creator may want theirs split by subject.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult CreateWorldRules(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a rule set.");
            }

            int index = template.GlobalGameSetups.Length + 1;
            DimensionGameSetupAsset rules =
                CreateAsset<DimensionGameSetupAsset>(template, "Resources", "WorldRules" + index.ToString());
            SetSerializedString(rules, "setupIdentifier", ResolveScopedId(template, "Rules" + index.ToString()));
            SetSerializedString(rules, "displayName", "World rules " + index.ToString());
            SetSerializedBool(rules, "enabled", true);
            AppendObjectReference(template, "globalGameSetups", rules);
            SaveAndSelect(rules);
            return Success(
                rules,
                "Created a rule set. Switch on the block you want — upgrading, fishing, talents " +
                "or the player — and nothing else changes.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateLootTable(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a loot table.");
            }

            int index = template.GlobalLootTables.Length + 1;
            DimensionLootTableAsset lootTable =
                CreateAsset<DimensionLootTableAsset>(template, "Resources", "LootTable" + index.ToString());
            SetSerializedString(lootTable, "lootTableId", ResolveScopedId(template, "LootTable" + index.ToString()));
            SetSerializedString(lootTable, "displayName", "Loot Table " + index.ToString());
            SetSerializedBool(lootTable, "enabled", true);
            AppendObjectReference(template, "globalLootTables", lootTable);
            SaveAndSelect(lootTable);
            return Success(lootTable, "Created a loot table definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateSpawnable(
            DimensionTemplateAsset template,
            DimensionSpawnableKind kind)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding spawnable content.");
            }

            if (kind == DimensionSpawnableKind.Animal)
            {
                return CreateSpawnableAsset<DimensionAnimalAsset>(template, "globalAnimals", "animalId", "Animal", template.GlobalAnimals.Length + 1);
            }

            if (kind == DimensionSpawnableKind.Critter)
            {
                return CreateSpawnableAsset<DimensionCritterAsset>(template, "globalCritters", "critterId", "Critter", template.GlobalCritters.Length + 1);
            }

            if (kind == DimensionSpawnableKind.Boss)
            {
                return CreateSpawnableAsset<DimensionBossAsset>(template, "globalBosses", "bossId", "Boss", template.GlobalBosses.Length + 1);
            }

            return CreateSpawnableAsset<DimensionMobAsset>(template, "globalMobs", "mobId", "Mob", template.GlobalMobs.Length + 1);
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateRuntimeManifestAsset(
            DimensionTemplateAsset template,
            DimensionTemplateManifestExportPreview preview)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before generating a runtime manifest.");
            }

            if (!preview.ManifestBuilt || !preview.ReadyForManifestExport)
            {
                return Failure("The manifest still has blockers. Validate the Export section before generating runtime output.");
            }

            DimensionRuntimeConsumerBootstrapResult runtimeResult =
                DimensionRuntimeConsumerBootstrapUtility.EnsureGeneratedRuntime(
                    template,
                    preview);
            if (!runtimeResult.Executed)
            {
                return Failure(runtimeResult.Message);
            }

            SaveAndSelect(runtimeResult.RuntimeManifestAsset);
            return Success(runtimeResult.RuntimeManifestAsset, runtimeResult.Message);
        }

        private static DimensionFrameworkAuthoringAssetActionResult CreateSpawnableAsset<T>(
            DimensionTemplateAsset template,
            string collectionProperty,
            string idProperty,
            string label,
            int index)
            where T : ScriptableObject
        {
            T asset = CreateAsset<T>(template, "Spawns", label + index.ToString());
            SetSerializedString(asset, idProperty, ResolveScopedId(template, label + index.ToString()));
            SetSerializedString(asset, "displayName", label + " " + index.ToString());
            SetSerializedString(asset, "objectId", ResolveScopedId(template, label + index.ToString() + "Object"));
            SetSerializedInt(asset, "spawnWeight", 1);
            SetSerializedBool(asset, "enabled", true);
            AppendObjectReference(template, collectionProperty, asset);
            SaveAndSelect(asset);
            return Success(asset, "Created a " + label.ToLowerInvariant() + " definition.");
        }

        /// <summary>
        /// Removes null and wrong-typed entries from the template's global item list (dead
        /// references left by deleted files or script-binding loss). Keeps healthy entries in
        /// order; saves only when something was actually dropped.
        /// </summary>
        private static void CompactGlobalItems(DimensionTemplateAsset template)
        {
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty items = serialized.FindProperty("globalItems");
            if (items == null || !items.isArray)
            {
                return;
            }

            bool changed = false;
            for (int i = items.arraySize - 1; i >= 0; i--)
            {
                Object reference = items.GetArrayElementAtIndex(i).objectReferenceValue;
                if (reference == null || !(reference is DimensionItemAsset))
                {
                    items.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(template);
            }
        }
    }
}
