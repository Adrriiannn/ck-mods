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
    /// Making one sprite object or renderer, and checking the finished visual is only those.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static SpriteObject FindDescendantSpriteObject(
            Transform root,
            string childName)
        {
            Transform transform = FindDescendantTransform(root, childName);
            return transform == null ? null : transform.GetComponent<SpriteObject>();
        }

        private static void SetLayerRecursively(GameObject root, int layer)
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
                    transforms[i].gameObject.layer = layer;
                }
            }
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            return child;
        }

        private static Material RequirePortalMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual requires material " +
                    path +
                    ".");
            }

            return material;
        }

        private static Sprite RequirePortalSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual requires sprite " +
                    path +
                    ".");
            }

            return sprite;
        }

        private static void TrySetUnityTag(GameObject gameObject, string tag)
        {
            if (gameObject == null || string.IsNullOrEmpty(tag))
            {
                return;
            }

            try
            {
                gameObject.tag = tag;
            }
            catch (UnityException)
            {
                // The SDK project defines this vanilla sorting tag. If a trimmed
                // project removes it, shadow behavior still falls back to layer order.
            }
        }

        private static SpriteObject EnsurePortalSpriteObject(
            Transform parent,
            string name,
            long addressLow,
            long addressHigh,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Color color,
            Color emissiveColor,
            bool active,
            int layer = -1)
        {
            Transform transform = EnsureChild(parent, name);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
            transform.gameObject.layer = layer >= 0 ? layer : parent.gameObject.layer;

            SpriteObject spriteObject = transform.GetComponent<SpriteObject>();
            if (spriteObject == null)
            {
                spriteObject = transform.gameObject.AddComponent<SpriteObject>();
            }

            AssignSpriteAssetAddress(spriteObject, addressLow, addressHigh);
            if (material != null)
            {
                spriteObject.material = material;
            }

            spriteObject.color = color;
            spriteObject.emissiveColor = emissiveColor;
            spriteObject.flashColor = Color.clear;
            spriteObject.outlineColor = Color.clear;
            spriteObject.animationTimescale = 1.0f;
            spriteObject.syncAnimation = false;
            spriteObject.syncVariant = false;
            spriteObject.syncSprite = false;
            spriteObject.ApplyVisualChange();
            transform.gameObject.SetActive(active);
            return spriteObject;
        }

        private static SpriteRenderer EnsurePortalBodyRenderer(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            bool active)
        {
            Transform transform = EnsureChild(parent, name);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
            transform.gameObject.layer = parent.gameObject.layer;

            SpriteObject spriteObject = transform.GetComponent<SpriteObject>();
            if (spriteObject != null)
            {
                Object.DestroyImmediate(spriteObject, true);
            }

            SpriteRenderer renderer = transform.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = transform.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sharedMaterial = material;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.flipX = false;
            renderer.flipY = false;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.spriteSortPoint = SpriteSortPoint.Center;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.renderingLayerMask = 1;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            renderer.sortingOrder = 0;
            transform.gameObject.SetActive(active);
            return renderer;
        }

        private static SpriteRenderer EnsurePortalShadowRenderer(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            bool active,
            bool sliced,
            int layer,
            int sortingOrder)
        {
            Transform transform = EnsureChild(parent, name);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
            transform.gameObject.layer = layer;

            SpriteObject spriteObject = transform.GetComponent<SpriteObject>();
            if (spriteObject != null)
            {
                Object.DestroyImmediate(spriteObject, true);
            }

            SpriteRenderer renderer = transform.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = transform.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sharedMaterial = material;
            renderer.sprite = sprite;
            renderer.color = new Color(0.0f, 0.0f, 0.0f, 0.7019608f);
            renderer.drawMode = sliced ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
            if (sliced)
            {
                renderer.size = new Vector2(3.5f, 0.75f);
            }
            renderer.flipX = false;
            renderer.flipY = false;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.spriteSortPoint = SpriteSortPoint.Center;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderingLayerMask = uint.MaxValue;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
            if (sortingOrder == 0)
            {
                renderer.sortingLayerID = 1861650685;
            }

            transform.gameObject.SetActive(active);
            return renderer;
        }

        private static void AssignSpriteAssetAddress(
            SpriteObject spriteObject,
            long addressLow,
            long addressHigh)
        {
            SerializedObject serializedObject = new SerializedObject(spriteObject);
            serializedObject.Update();
            SetSerializedLong(
                serializedObject,
                "m_assetRef.m_address.m_low",
                addressLow);
            SetSerializedLong(
                serializedObject,
                "m_assetRef.m_address.m_high",
                addressHigh);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateSpriteObjectOnlyPortal(GameObject root, bool itemPortal)
        {
            Vector3 expectedPivot = itemPortal
                ? ItemPortalSpritePivotPosition
                : PortalSpritePivotPosition;
            string forbiddenComponent = FindForbiddenPortalVisualComponent(root);
            if (!string.IsNullOrEmpty(forbiddenComponent))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visible visuals must use the approved portal render paths; " +
                    "only the analytic portal body plus the vanilla Shadow and ShadowCaster renderer children are allowed. " +
                    "Forbidden component remains: " +
                    forbiddenComponent +
                    ".");
            }

            Transform spriteRoot = root == null
                ? null
                : root.transform.Find(
                    "XScaler/AnimPositionRotation/AnimScale/SRPivot/PortalSpriteObjects");
            if (spriteRoot == null)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing the vanilla animator hierarchy " +
                    "XScaler/AnimPositionRotation/AnimScale/SRPivot/PortalSpriteObjects.");
            }

            Transform spritePivot = spriteRoot.parent;
            Transform animScale = spritePivot == null ? null : spritePivot.parent;
            Transform animPositionRotation = animScale == null ? null : animScale.parent;
            if (spritePivot == null ||
                animScale == null ||
                animPositionRotation == null ||
                (spritePivot.localPosition - expectedPivot).sqrMagnitude > 0.000001f ||
                spriteRoot.localPosition.sqrMagnitude > 0.000001f ||
                animScale.localPosition.sqrMagnitude > 0.000001f ||
                animPositionRotation.localPosition.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(spritePivot.localRotation, Quaternion.identity) > 0.001f ||
                Quaternion.Angle(spriteRoot.localRotation, Quaternion.identity) > 0.001f ||
                Quaternion.Angle(animScale.localRotation, Quaternion.identity) > 0.001f ||
                Quaternion.Angle(animPositionRotation.localRotation, Quaternion.identity) > 0.001f ||
                (spritePivot.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                (spriteRoot.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                (animScale.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                (animPositionRotation.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual has invalid vanilla animator wrapper " +
                    "transforms. SRPivot must be anchored at " +
                    expectedPivot +
                    " and all wrapper rotations/scales plus the SpriteObject root transform " +
                    "must remain at their vanilla defaults.");
            }

            string missingLayer = FindMissingPortalSpriteObjectLayer(spriteRoot);
            if (!string.IsNullOrEmpty(missingLayer))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing required SpriteObject layer " +
                    missingLayer +
                    ".");
            }

            string unresolvedLayer = FindUnresolvedPortalSpriteObjectLayer(spriteRoot);
            if (!string.IsNullOrEmpty(unresolvedLayer))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual has an unresolved SpriteAsset reference: " +
                    unresolvedLayer +
                    ". The portal was not saved; reapply after Scriptable Data finishes importing.");
            }

            string missingShadow = FindMissingPortalShadowRendererLayer(spriteRoot);
            if (!string.IsNullOrEmpty(missingShadow))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing required vanilla shadow renderer " +
                    missingShadow +
                    ".");
            }

            string missingLight = FindMissingPortalLightLayer(root);
            if (!string.IsNullOrEmpty(missingLight))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing required vanilla light layer " +
                    missingLight +
                    ".");
            }
        }

        private static string FindMissingPortalSpriteObjectLayer(Transform spriteRoot)
        {
            string[] requiredLayers =
            {
                "PortalBodySO",
                "PortalChargeProgressSO",
                "PortalEmissiveWaveSO",
                "PortalCenterEffectSO",
                "PortalOutlineMaskSO",
                "PortalOutlineSupportMaskSO",
                "PortalOutlineCapSO"
            };

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                Transform layer = spriteRoot.Find(requiredLayers[i]);
                if (layer == null || layer.GetComponent<SpriteObject>() == null)
                {
                    return requiredLayers[i];
                }
            }

            return string.Empty;
        }

        private static string FindUnresolvedPortalSpriteObjectLayer(Transform spriteRoot)
        {
            string[] requiredLayers =
            {
                "PortalBodySO",
                "PortalChargeProgressSO",
                "PortalEmissiveWaveSO",
                "PortalCenterEffectSO",
                "PortalOutlineMaskSO",
                "PortalOutlineSupportMaskSO",
                "PortalOutlineCapSO"
            };

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                Transform layer = spriteRoot.Find(requiredLayers[i]);
                SpriteObject spriteObject = layer == null
                    ? null
                    : layer.GetComponent<SpriteObject>();
                if (spriteObject == null || spriteObject.asset != null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(spriteObject);
                serialized.Update();
                SerializedProperty low = serialized.FindProperty(
                    "m_assetRef.m_address.m_low");
                SerializedProperty high = serialized.FindProperty(
                    "m_assetRef.m_address.m_high");
                return requiredLayers[i] + " (address low " +
                    (low == null ? "?" : low.longValue.ToString(CultureInfo.InvariantCulture)) +
                    ", high " +
                    (high == null ? "?" : high.longValue.ToString(CultureInfo.InvariantCulture)) +
                    ")";
            }

            return string.Empty;
        }

        private static string FindMissingPortalShadowRendererLayer(Transform spriteRoot)
        {
            string[] requiredLayers =
            {
                "PortalShadowGroup/Shadow",
                "PortalShadowGroup/ShadowCaster"
            };

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                Transform layer = spriteRoot.Find(requiredLayers[i]);
                if (layer == null || layer.GetComponent<SpriteRenderer>() == null)
                {
                    return requiredLayers[i];
                }
            }

            return string.Empty;
        }

        private static string FindMissingPortalLightLayer(GameObject root)
        {
            Transform lightRoot = root == null
                ? null
                : root.transform.Find("XScaler/" + PortalLightObjectName);
            if (lightRoot == null)
            {
                return PortalLightObjectName;
            }

            ManagedLight managedLight = lightRoot.GetComponent<ManagedLight>();
            if (managedLight == null)
            {
                return PortalLightObjectName + "/ManagedLight";
            }

            Light pointLight = lightRoot.GetComponentInChildren<Light>(true);
            if (pointLight == null)
            {
                return PortalLightObjectName + "/Point Light";
            }

            SpriteObject fallback = FindDescendantSpriteObject(
                lightRoot,
                "IndirectLightSprite");
            if (fallback == null)
            {
                return PortalLightObjectName + "/IndirectLightSprite";
            }

            return string.Empty;
        }

        private static string FindForbiddenPortalVisualComponent(GameObject root)
        {
            if (root == null)
            {
                return string.Empty;
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
                    if (component == null)
                    {
                        continue;
                    }

                    string typeName = component.GetType().Name;
                    if (typeName == "Sprite" + "Renderer")
                    {
                        string path = GetTransformPath(transforms[i], root.transform);
                        if (!IsAllowedPortalRendererPath(path))
                        {
                            return typeName + " on " + path;
                        }
                    }
                    else if (typeName == "Outline" + "Controller")
                    {
                        return typeName + " on " + GetTransformPath(transforms[i], root.transform);
                    }
                }
            }

            return string.Empty;
        }

        private static bool IsAllowedPortalRendererPath(string path)
        {
            return path.EndsWith(
                    "/XScaler/AnimPositionRotation/AnimScale/SRPivot/" +
                    "PortalSpriteObjects/PortalBodyRenderer",
                    System.StringComparison.Ordinal) ||
                path.EndsWith(
                    "/XScaler/AnimPositionRotation/AnimScale/SRPivot/" +
                    "PortalSpriteObjects/PortalShadowGroup/Shadow",
                    System.StringComparison.Ordinal) ||
                path.EndsWith(
                    "/XScaler/AnimPositionRotation/AnimScale/SRPivot/" +
                    "PortalSpriteObjects/PortalShadowGroup/ShadowCaster",
                    System.StringComparison.Ordinal);
        }

        private static string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            if (transform == root || transform.parent == null)
            {
                return transform.name;
            }

            return GetTransformPath(transform.parent, root) + "/" + transform.name;
        }

        private static void EnsurePortalHitAuthoring(GameObject root, bool destructible)
        {
            MineableAuthoring mineable = EnsureComponent<MineableAuthoring>(root);
            mineable.playFailedEffectOnZeroDamage = true;

            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            health.dontCalculateHealthFromLevel = false;
            health.overrideStartHealth = false;
            health.normalizedOverrideStartHealth = 1.0f;
            health.startHealth = 3;
            health.maxHealth = 3;
            health.maxHealthMultiplier = 1.0f;
            health.hasHealthRegeneration = true;
            health.healInCombatAsWell = false;
            health.healthIncreasePercentPerFiveSeconds = 100;
            health.healDelayAfterLeavingCombat = 5.0f;

            EnsureComponent<DamageableObjectAuthoring>(root);
            if (destructible)
            {
                EnsureComponent<DestructibleObjectAuthoring>(root).requiresDrill = false;
            }
            else
            {
                RemoveComponentIfPresent<DestructibleObjectAuthoring>(root);
            }
            EnsureComponent<DamageEffectAuthoring>(root);
            EnsureComponent<IdleStateAuthoring>(root).playIdleAnimation = true;

            TookDamageStateAuthoring tookDamage = EnsureComponent<TookDamageStateAuthoring>(root);
            tookDamage.duration = 0.0f;
            tookDamage.refreshStateOnNewDamageTaken = false;

            DeathStateAuthoring death = EnsureComponent<DeathStateAuthoring>(root);
            death.overrideTimeBeforeDestroy = false;
            death.timeBeforeDestroy = 0.0f;
            death.timeBeforeLootDrop = 0.0f;
            death.skipDeathAnimation = false;

            DamageReductionAuthoring damageReduction = EnsureComponent<DamageReductionAuthoring>(root);
            damageReduction.calculateReductionFromLevel = false;
            damageReduction.reductionMultiplier = 1.0f;
            damageReduction.reduction = VanillaPortalDamageReduction;
            damageReduction.maxDamagePerHit = 1;
            damageReduction.minDamagePerHit = 0;
            damageReduction.ignoreReductionWhenDamagedByDrill = false;
        }
    }
}
