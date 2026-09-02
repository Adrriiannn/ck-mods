using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The counts the summary strip shows, and how each is worded.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private void DrawDashboardMetric(string title, string value, string tooltip)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(tooltip))
            {
                EditorGUILayout.LabelField(tooltip, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNamedValue(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(150f));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static string BuildAssetEditorTitle(
            string prefix,
            string displayName,
            string recordId,
            int index)
        {
            string resolvedPrefix = string.IsNullOrEmpty(prefix) ? "Asset" : prefix;
            string resolvedName = !string.IsNullOrEmpty(displayName)
                ? displayName
                : !string.IsNullOrEmpty(recordId)
                    ? recordId
                    : resolvedPrefix + " " + (index + 1).ToString();
            return resolvedPrefix + ": " + resolvedName;
        }

        private BiomeTemplateAsset DrawBiomeSelectorHeader()
        {
            BiomeTemplateAsset[] biomes = GetBiomes();
            if (biomes.Length == 0)
            {
                return null;
            }

            string[] names = new string[biomes.Length];
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                names[i] = biome == null || string.IsNullOrEmpty(biome.DisplayName)
                    ? "Biome " + (i + 1)
                    : biome.DisplayName;
            }

            selectedBiomeIndex = Mathf.Clamp(selectedBiomeIndex, 0, biomes.Length - 1);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Biome", EditorStyles.boldLabel, GUILayout.Width(80f));
            selectedBiomeIndex = EditorGUILayout.Popup(selectedBiomeIndex, names, GUILayout.MaxWidth(340f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                (selectedBiomeIndex + 1) + " / " + biomes.Length,
                EditorStyles.miniLabel,
                GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            return biomes[selectedBiomeIndex];
        }

        private BiomeTemplateAsset[] GetBiomes()
        {
            return selectedTemplate == null ? new BiomeTemplateAsset[0] : selectedTemplate.Biomes;
        }

        private int CountBiomes()
        {
            return GetBiomes().Length;
        }

        private int CountScenes()
        {
            int count = selectedTemplate == null ? 0 : CountAssets(selectedTemplate.GlobalScenes);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                count += CountBiomeScenes(biomes[i]);
            }

            return count;
        }

        private int CountBiomeScenes(BiomeTemplateAsset biome)
        {
            return biome == null ? 0 : CountAssets(biome.ScenePool);
        }

        private int CountSceneContents()
        {
            int count = selectedTemplate == null ? 0 : CountSceneContents(selectedTemplate.GlobalScenes);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome != null)
                {
                    count += CountSceneContents(biome.ScenePool);
                }
            }

            return count;
        }

        private int CountResources()
        {
            return selectedTemplate == null
                ? 0
                : CountAssets(selectedTemplate.GlobalItems) +
                    CountAssets(selectedTemplate.GlobalRecipes) +
                    CountAssets(selectedTemplate.GlobalWorkbenches) +
                    CountAssets(selectedTemplate.GlobalLootTables);
        }

        private int CountSpawns()
        {
            return selectedTemplate == null
                ? 0
                : CountAssets(selectedTemplate.GlobalAnimals) +
                    CountAssets(selectedTemplate.GlobalCritters) +
                    CountAssets(selectedTemplate.GlobalMobs) +
                    CountAssets(selectedTemplate.GlobalBosses);
        }

        private int CountGenerationPasses()
        {
            int count = selectedTemplate == null ? 0 : CountAssets(selectedTemplate.GlobalGenerationPasses);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                count += CountBiomeGenerationPasses(biomes[i]);
            }

            return count;
        }

        private int CountBiomeGenerationPasses(BiomeTemplateAsset biome)
        {
            return biome == null ? 0 : CountAssets(biome.GenerationPasses);
        }

        private int CountBiomeTerrainIds()
        {
            int count = 0;
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                count += biome.FloorObjectIds.Length;
                count += biome.WallObjectIds.Length;
                count += biome.OreObjectIds.Length;
            }

            return count;
        }

        private static int CountAssets<T>(T[] values)
            where T : UnityEngine.Object
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSceneContents(SceneTemplateAsset[] scenes)
        {
            if (scenes == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene != null)
                {
                    count += CountValues(scene.Triggers);
                }
            }

            return count;
        }

        private static int CountValues<T>(T[] values)
            where T : class
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatInt2(Unity.Mathematics.int2 value)
        {
            return value.x + ", " + value.y;
        }
    }
}
