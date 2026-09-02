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
    /// Writing a serialized field by name, and the component helpers around it.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }

        private static void RemoveComponentByName(
            GameObject root,
            string componentTypeName,
            Component exceptComponent = null)
        {
            if (root == null || string.IsNullOrEmpty(componentTypeName))
            {
                return;
            }

            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null ||
                    component == exceptComponent ||
                    component.GetType().Name != componentTypeName)
                {
                    continue;
                }

                Object.DestroyImmediate(component, true);
                return;
            }
        }

        private static void RemoveMissingMonoBehaviours(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
                }
            }
        }

        private static Transform FindDescendantTransform(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDescendantTransform(root.GetChild(i), childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void AssignSerializedObjectReferenceList(
            Object target,
            string propertyName,
            List<Object> values)
        {
            if (target == null || string.IsNullOrEmpty(propertyName) || values == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSerializedObjectReference(
            Object target,
            string propertyName,
            Object value)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null ||
                property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClearSerializedObjectReference(
            Object target,
            string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null &&
                property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGhostAuthoringComponent(GameObject root, string assetPath)
        {
            if (root == null)
            {
                return;
            }

            Component ghost = FindComponentByName(root, "GhostAuthoringComponent");
            if (ghost == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(ghost);
            serializedObject.Update();
            SetSerializedInt(serializedObject, "DefaultGhostMode", 0);
            SetSerializedInt(serializedObject, "SupportedGhostModes", 3);
            SetSerializedInt(serializedObject, "OptimizationMode", 1);
            SetSerializedString(serializedObject, "prefabId", AssetDatabase.AssetPathToGUID(assetPath));
            SetSerializedBool(serializedObject, "HasOwner", false);
            SetSerializedBool(serializedObject, "SupportAutoCommandTarget", false);
            SetSerializedBool(serializedObject, "TrackInterpolationDelay", false);
            SetSerializedBool(serializedObject, "GhostGroup", false);
            SetSerializedBool(serializedObject, "UsePreSerialization", false);
            SetSerializedBool(serializedObject, "DontUsePredictionBackup", false);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Component FindComponentByName(GameObject root, string componentTypeName)
        {
            if (root == null || string.IsNullOrEmpty(componentTypeName))
            {
                return null;
            }

            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().Name == componentTypeName)
                {
                    return component;
                }
            }

            return null;
        }

        private static void SetSerializedString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetSerializedBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = value;
            }
        }

        private static void SetSerializedInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = value;
            }
            else if (property.propertyType == SerializedPropertyType.Enum)
            {
                // Enum values are not guaranteed to be contiguous popup indices.
                // This is especially important for flag enums such as
                // SupportedGhostModes, where 3 is a valid combined value but not
                // necessarily a valid enumValueIndex.
                property.intValue = value;
            }
        }

        private static void SetSerializedFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
            {
                property.floatValue = value;
            }
        }

        private static void SetSerializedColor(
            SerializedObject serializedObject,
            string propertyName,
            Color value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Color)
            {
                property.colorValue = value;
            }
        }

        private static void SetSerializedLong(
            SerializedObject serializedObject,
            string propertyName,
            long value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Integer)
            {
                property.longValue = value;
            }
        }

        private static void SetSpriteAssetAddress(
            SpriteAsset asset,
            long lowValue,
            long highValue)
        {
            if (asset == null)
            {
                throw new System.ArgumentNullException("asset");
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                throw new System.InvalidOperationException(
                    "The generated SpriteAsset address could not be assigned.");
            }

            low.longValue = lowValue;
            high.longValue = highValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TryAddGhostAuthoringComponent(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (root.GetComponent<GhostAuthoringComponent>() != null)
            {
                return;
            }

            root.AddComponent<GhostAuthoringComponent>();
        }

        private static void SetSerializedArraySize(Object target, string propertyName, int size)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.isArray)
            {
                property.arraySize = Mathf.Max(0, size);
                serialized.ApplyModifiedProperties();
            }
        }
    }
}
