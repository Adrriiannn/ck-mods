using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Portals;
using ExpandNullforge.Scenes;
using Pug.Sprite;
using PugMod;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Removing what earlier versions of the generator left on a creator disk.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static void DeleteLegacyIntegratedPortalChargingAssets(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalChargingBody",
                "DimensionPortalChargingBody");
            string assetPath = portalFolder + "/" + assetName + ".asset";
            SpriteAsset generated = AssetDatabase.LoadAssetAtPath<SpriteAsset>(assetPath);
            if (generated != null)
            {
                RemoveSpriteAssetManifestReference(modRoot, generated);
                AssetDatabase.DeleteAsset(assetPath);
            }

            string bodyPath = portalFolder + "/" + assetName + "Body.png";
            string emissivePath = portalFolder + "/" + assetName + "Emissive.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(bodyPath) != null)
            {
                AssetDatabase.DeleteAsset(bodyPath);
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(emissivePath) != null)
            {
                AssetDatabase.DeleteAsset(emissivePath);
            }
        }

        private static void RemoveLegacyPortalVisualArtifacts(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            RemoveComponentsByTypeName(root, "Sprite" + "Renderer");
            RemoveComponentsByTypeName(root, "Outline" + "Controller");
            DestroyLegacyVisualObjects(root);
        }

        private static void RemoveComponentsByTypeName(GameObject root, string typeName)
        {
            if (root == null || string.IsNullOrEmpty(typeName))
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                {
                    continue;
                }

                Component[] components = transforms[i].GetComponents<Component>();
                for (int j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component != null && component.GetType().Name == typeName)
                    {
                        Object.DestroyImmediate(component, true);
                    }
                }
            }
        }

        private static void DestroyLegacyVisualObjects(GameObject root)
        {
            List<GameObject> legacyObjects = new List<GameObject>();
            CollectLegacyVisualObjects(root.transform, root, legacyObjects);
            for (int i = 0; i < legacyObjects.Count; i++)
            {
                if (legacyObjects[i] != null)
                {
                    Object.DestroyImmediate(legacyObjects[i], true);
                }
            }
        }

        private static void CollectLegacyVisualObjects(
            Transform current,
            GameObject root,
            List<GameObject> legacyObjects)
        {
            if (current == null)
            {
                return;
            }

            if (current.gameObject != root && IsLegacyPortalVisualObjectName(current.name))
            {
                legacyObjects.Add(current.gameObject);
                return;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CollectLegacyVisualObjects(current.GetChild(i), root, legacyObjects);
            }
        }

        private static bool IsLegacyPortalVisualObjectName(string name)
        {
            return name == "SR" ||
                name.StartsWith("SR (", System.StringComparison.Ordinal) ||
                name == "portalEffect" + "SR" ||
                name == "load" + "Points" ||
                name == "PortalBodyRenderer" ||
                name == "PortalBodySpriteObject" ||
                name == "PortalChargeWaveSO" ||
                name == "PortalIndirectLightSO" ||
                name == "PortalShadowSO" ||
                name == "PortalShadowCasterSO" ||
                name == "Shadow";
        }
    }
}
