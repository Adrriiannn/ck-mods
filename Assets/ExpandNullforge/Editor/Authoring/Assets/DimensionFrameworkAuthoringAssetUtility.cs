using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{

    internal static partial class DimensionFrameworkAuthoringAssetUtility
    {

        private static T CreateAsset<T>(
            DimensionTemplateAsset template,
            string sectionFolder,
            string stem)
            where T : ScriptableObject
        {
            return SaveNewAsset(template, sectionFolder, stem, ScriptableObject.CreateInstance<T>());
        }

        /// <summary>
        /// Puts an object that has already been built onto disk beside its dimension.
        /// </summary>
        /// <remarks>
        /// Split out of <see cref="CreateAsset{T}"/> for the case where the defaults live somewhere
        /// else — a portal access rule is born fully configured by the starter factory, so the
        /// alternative would be a second copy of those defaults here.
        /// </remarks>
        private static T SaveNewAsset<T>(
            DimensionTemplateAsset template,
            string sectionFolder,
            string stem,
            T asset)
            where T : ScriptableObject
        {
            string folder = ResolveSectionFolder(template, sectionFolder);
            DimensionAssetFolders.Ensure(folder);
            string safeStem = NormalizeIdToken(stem, "Asset");
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
            DimensionAssetFolders.Ensure(folder);
            string safeStem = NormalizeIdToken(stem, "Asset");
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
            string safeSection = NormalizeIdToken(sectionFolder, "Asset");
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

        private static void SetSerializedObjectReference(Object owner, string propertyName, Object value)
        {
            SetSerialized(owner, propertyName, property => property.objectReferenceValue = value);
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

        private static void SetRelativeFloat(SerializedProperty element, string propertyName, float value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.floatValue = value;
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
