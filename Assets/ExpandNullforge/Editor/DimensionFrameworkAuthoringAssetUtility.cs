using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionFrameworkAuthoringAssetActionResult
    {
        public DimensionFrameworkAuthoringAssetActionResult(
            bool executed,
            Object createdObject,
            string message)
        {
            Executed = executed;
            CreatedObject = createdObject;
            Message = message ?? string.Empty;
        }

        public bool Executed { get; private set; }

        public Object CreatedObject { get; private set; }

        public string Message { get; private set; }
    }

    internal static class DimensionFrameworkAuthoringAssetUtility
    {
        public static DimensionFrameworkAuthoringAssetActionResult CreateLayoutTemplate(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before creating a layout.");
            }

            DimensionLayoutTemplateAsset layout =
                CreateAsset<DimensionLayoutTemplateAsset>(
                    template,
                    "Layout",
                    "Layout");
            string biomeId = ResolvePrimaryBiomeId(template);
            layout.ApplySingleBiomeSquarePreset(
                biomeId,
                biomeId,
                64);
            SetObjectReference(template, "layoutTemplate", layout);
            SaveAndSelect(layout);
            return Success(layout, "Created and assigned a layout template.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult AddLayoutRegion(
            DimensionLayoutTemplateAsset layout,
            BiomeTemplateAsset biome)
        {
            if (layout == null)
            {
                return Failure("Assign or create a layout template before adding regions.");
            }

            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty regions = serialized.FindProperty("regions");
            if (regions == null || !regions.isArray)
            {
                return Failure("The layout template does not expose editable manual regions.");
            }

            int index = regions.arraySize;
            regions.InsertArrayElementAtIndex(index);
            SerializedProperty element = regions.GetArrayElementAtIndex(index);
            string biomeId = biome == null ? string.Empty : biome.BiomeId;
            string regionId = "region-" + (index + 1).ToString();
            SetRelativeString(element, "regionId", regionId);
            SetRelativeString(element, "biomeId", biomeId);
            SetRelativeString(element, "zoneId", biomeId);
            SetRelativeString(element, "displayName", string.IsNullOrEmpty(biomeId) ? "Region" : biome.DisplayName + " Region");
            SetRelativeVector2Int(element, "localMin", new Vector2Int(-64 + index * 128, -64));
            SetRelativeVector2Int(element, "localMaxExclusive", new Vector2Int(64 + index * 128, 64));
            SetRelativeInt(element, "priority", index);
            SetRelativeBool(element, "enabled", true);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(layout, "Added a layout region for " + (string.IsNullOrEmpty(biomeId) ? "the selected layout" : biomeId) + ".");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreatePortalAccessRule(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding portal access.");
            }

            int index = template.PortalAccessRules.Length + 1;
            string baseId = ResolveScopedId(template, "Portal" + index.ToString());
            DimensionPortalAccessRuleAsset rule =
                CreateAsset<DimensionPortalAccessRuleAsset>(
                    template,
                    "Portals",
                    "PortalAccessRule" + index.ToString());
            rule.Configure(
                baseId + ".rule",
                baseId,
                baseId + ".presentation",
                template.DisplayName + " Portal",
                DimensionIds.Overworld,
                Vector2.zero,
                template.DimensionId,
                Vector2.zero,
                DimensionPortalAccessKind.PlacedPortal,
                DimensionPortalActivationMode.VanillaCooldown,
                2.0f,
                true,
                true,
                "Enter " + template.DisplayName,
                "The portal is not active yet.",
                string.Empty,
                string.Empty,
                string.Empty,
                index,
                true,
                new DimensionPortalRequiredItemTemplate[0]);
            AppendObjectReference(template, "portalAccessRules", rule);
            SaveAndSelect(rule);
            return Success(rule, "Created a placed-portal access rule.");
        }

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
            biome.ApplySemanticTerrainPreset(
                new[] { ResolveScopedId(template, "GroundBiome" + index.ToString() + "Block") },
                new[] { ResolveScopedId(template, "WallBiome" + index.ToString() + "Block") },
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

        public static DimensionFrameworkAuthoringAssetActionResult CreateResourceNode(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a resource node.");
            }

            int index = CountResourceNodes(template, biome) + 1;
            string nodeId = ResolveScopedId(template, "ResourceNode" + index.ToString());
            ResourceNodeTemplateAsset node =
                CreateAsset<ResourceNodeTemplateAsset>(
                    template,
                    "Resources",
                    "ResourceNode" + index.ToString());
            string zoneId = biome == null ? string.Empty : biome.BiomeId;
            node.ConfigureIdentity(
                nodeId,
                "Resource Node " + index.ToString(),
                zoneId,
                ResolveScopedId(template, "Resource" + index.ToString()),
                DimensionResourceNodeKind.Custom,
                string.Empty,
                string.Empty,
                1,
                index,
                true);
            node.ApplyLocalBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32));
            AppendObjectReference(biome == null ? (Object)template : biome, biome == null ? "globalResourceNodes" : "resourceNodes", node);
            SaveAndSelect(node);
            return Success(node, biome == null ? "Created a global resource node." : "Created a biome resource node.");
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

        public static DimensionFrameworkAuthoringAssetActionResult CreateSpawnRule(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a spawn rule.");
            }

            int index = CountSpawnRules(template, biome) + 1;
            SpawnRuleTemplateAsset rule =
                CreateAsset<SpawnRuleTemplateAsset>(template, "Spawns", "SpawnRule" + index.ToString());
            string zoneId = biome == null ? string.Empty : biome.BiomeId;
            rule.ConfigureIdentity(
                ResolveScopedId(template, "SpawnRule" + index.ToString()),
                "Spawn Rule " + index.ToString(),
                zoneId,
                ResolveScopedId(template, "Mob" + index.ToString()),
                DimensionSpawnSubjectKind.Mob,
                1,
                index,
                true);
            rule.ApplyLocalBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32));
            AppendObjectReference(biome == null ? (Object)template : biome, biome == null ? "globalSpawnRules" : "spawnRules", rule);
            SaveAndSelect(rule);
            return Success(rule, biome == null ? "Created a global spawn rule." : "Created a biome spawn rule.");
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

        private static T CreateAsset<T>(
            DimensionTemplateAsset template,
            string sectionFolder,
            string stem)
            where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            string folder = ResolveSectionFolder(template, sectionFolder);
            EnsureFolder(folder);
            string safeStem = SanitizeFileName(stem);
            asset.name = safeStem;
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeStem + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T DuplicateAsset<T>(
            DimensionTemplateAsset template,
            string sectionFolder,
            string stem,
            T source)
            where T : ScriptableObject
        {
            T asset = Object.Instantiate(source);
            string folder = ResolveSectionFolder(template, sectionFolder);
            EnsureFolder(folder);
            string safeStem = SanitizeFileName(stem);
            asset.name = safeStem;
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeStem + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static string ResolveSectionFolder(
            DimensionTemplateAsset template,
            string sectionFolder)
        {
            string root = ResolveTemplateRootFolder(template);
            string safeSection = SanitizeFileName(sectionFolder);
            return string.IsNullOrEmpty(safeSection)
                ? root
                : root + "/" + safeSection;
        }

        private static string ResolveTemplateRootFolder(DimensionTemplateAsset template)
        {
            string assetPath = template == null ? string.Empty : AssetDatabase.GetAssetPath(template);
            assetPath = DimensionApiModFolderUtility.NormalizeFolder(assetPath);
            if (!string.IsNullOrEmpty(assetPath))
            {
                int slash = assetPath.LastIndexOf('/');
                if (slash > 0)
                {
                    return assetPath.Substring(0, slash);
                }
            }

            return DimensionApiModFolderUtility.ResolvePreferredDimensionAssetFolder();
        }

        private static void AppendObjectReference(Object owner, string propertyName, Object value)
        {
            if (owner == null || value == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
        }

        private static bool RemoveObjectReference(Object owner, string propertyName, Object value)
        {
            if (owner == null || value == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return false;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != value)
                {
                    continue;
                }

                property.DeleteArrayElementAtIndex(i);
                if (i < property.arraySize &&
                    property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    property.DeleteArrayElementAtIndex(i);
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(owner);
                return true;
            }

            return false;
        }

        private static void SetObjectReference(Object owner, string propertyName, Object value)
        {
            if (owner == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
        }

        private static void SetSerializedString(Object owner, string propertyName, string value)
        {
            SetSerialized(owner, propertyName, property => property.stringValue = value ?? string.Empty);
        }

        private static void SetSerializedInt(Object owner, string propertyName, int value)
        {
            SetSerialized(owner, propertyName, property => property.intValue = value);
        }

        private static void SetSerializedBool(Object owner, string propertyName, bool value)
        {
            SetSerialized(owner, propertyName, property => property.boolValue = value);
        }

        private static void SetSerializedEnum(Object owner, string propertyName, int value)
        {
            SetSerialized(owner, propertyName, property => property.enumValueIndex = value);
        }

        private static void SetSerialized(Object owner, string propertyName, System.Action<SerializedProperty> setter)
        {
            if (owner == null || setter == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            setter(property);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
        }

        private static void SetRelativeString(SerializedProperty element, string propertyName, string value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetRelativeInt(SerializedProperty element, string propertyName, int value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetRelativeBool(SerializedProperty element, string propertyName, bool value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetRelativeVector2Int(SerializedProperty element, string propertyName, Vector2Int value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.vector2IntValue = value;
            }
        }

        private static int CountScenes(DimensionTemplateAsset template, BiomeTemplateAsset biome)
        {
            return biome == null ? template.GlobalScenes.Length : biome.ScenePool.Length;
        }

        private static int CountResourceNodes(DimensionTemplateAsset template, BiomeTemplateAsset biome)
        {
            return biome == null ? template.GlobalResourceNodes.Length : biome.ResourceNodes.Length;
        }

        private static int CountSpawnRules(DimensionTemplateAsset template, BiomeTemplateAsset biome)
        {
            return biome == null ? template.GlobalSpawnRules.Length : biome.SpawnRules.Length;
        }

        private static string ResolvePrimaryBiomeId(DimensionTemplateAsset template)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes != null && biomes.Length > 0 && biomes[0] != null)
            {
                return biomes[0].BiomeId;
            }

            return ResolveScopedId(template, "StarterBiome");
        }

        private static string ResolveScopedId(DimensionTemplateAsset template, string suffix)
        {
            string prefix = ResolveModPrefix(template);
            return prefix + ":" + NormalizeIdToken(suffix, "Content");
        }

        private static string ResolveModPrefix(DimensionTemplateAsset template)
        {
            string dimensionId = template == null ? string.Empty : template.DimensionId;
            int colon = dimensionId.IndexOf(':');
            if (colon > 0)
            {
                return NormalizeIdToken(dimensionId.Substring(0, colon), "ModName");
            }

            string contentPackId = template == null ? string.Empty : template.ContentPackId;
            colon = contentPackId.IndexOf(':');
            if (colon > 0)
            {
                return NormalizeIdToken(contentPackId.Substring(0, colon), "ModName");
            }

            return "ModName";
        }

        private static string NormalizeIdToken(string value, string fallback)
        {
            string source = string.IsNullOrEmpty(value) ? fallback : value;
            string result = string.Empty;
            bool makeUpper = true;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                bool valid = (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9');
                if (valid)
                {
                    if (makeUpper && character >= 'a' && character <= 'z')
                    {
                        character = (char)(character - 32);
                    }

                    result += character;
                    makeUpper = false;
                }
                else
                {
                    makeUpper = true;
                }
            }

            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        private static string SanitizeFileName(string value)
        {
            return NormalizeIdToken(value, "Asset");
        }

        private static Color ResolveMapColor(int index)
        {
            Color[] colors =
            {
                new Color(0.25f, 0.45f, 0.55f, 1f),
                new Color(0.38f, 0.52f, 0.30f, 1f),
                new Color(0.53f, 0.42f, 0.28f, 1f),
                new Color(0.44f, 0.34f, 0.58f, 1f),
                new Color(0.55f, 0.36f, 0.42f, 1f)
            };

            return colors[Mathf.Abs(index) % colors.Length];
        }

        private static void EnsureFolder(string folder)
        {
            string normalized = DimensionApiModFolderUtility.NormalizeFolder(folder);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] parts = normalized.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void SaveAndSelect(Object asset)
        {
            if (asset != null)
            {
                EditorUtility.SetDirty(asset);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static DimensionFrameworkAuthoringAssetActionResult Success(Object createdObject, string message)
        {
            return new DimensionFrameworkAuthoringAssetActionResult(true, createdObject, message);
        }

        private static DimensionFrameworkAuthoringAssetActionResult Failure(string message)
        {
            return new DimensionFrameworkAuthoringAssetActionResult(false, null, message);
        }
    }
}
