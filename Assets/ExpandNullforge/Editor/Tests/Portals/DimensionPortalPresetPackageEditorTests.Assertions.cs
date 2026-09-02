#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The assertions these tests share: pixels, addresses, references, serialized fields.
    /// </summary>
    internal sealed partial class DimensionPortalPresetPackageEditorTests
    {
        private static void AssertTintedPixels(
            Color32[] source,
            Color32[] actual,
            int preservedChannel,
            string label)
        {
            Assert.That(source, Is.Not.Null, label + " source");
            Assert.That(actual, Has.Length.EqualTo(source.Length), label);
            bool foundVisibleColor = false;
            for (int i = 0; i < source.Length; i++)
            {
                Color32 expected = new Color32(0, 0, 0, source[i].a);
                if (preservedChannel == 0)
                {
                    expected.r = source[i].r;
                }
                else if (preservedChannel == 1)
                {
                    expected.g = source[i].g;
                }
                else
                {
                    expected.b = source[i].b;
                }

                Assert.That(actual[i], Is.EqualTo(expected), label + " pixel " + i);
                if (source[i].a > 0 &&
                    (expected.r > 0 || expected.g > 0 || expected.b > 0))
                {
                    foundVisibleColor = true;
                }
            }

            Assert.That(
                foundVisibleColor,
                Is.True,
                label + " did not contain a visible source-colored pixel to verify.");
        }

        private static void AssertMultipliedPixels(
            Color32[] source,
            Color32[] actual,
            Color factor,
            bool multiplyAlpha,
            string label)
        {
            Assert.That(source, Is.Not.Null, label + " source");
            Assert.That(actual, Has.Length.EqualTo(source.Length), label);
            float red = Mathf.Max(0f, factor.r);
            float green = Mathf.Max(0f, factor.g);
            float blue = Mathf.Max(0f, factor.b);
            float alpha = multiplyAlpha ? Mathf.Clamp01(factor.a) : 1f;
            bool foundVisibleColor = false;
            for (int i = 0; i < source.Length; i++)
            {
                Color32 expected = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(source[i].r * red), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(source[i].g * green), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(source[i].b * blue), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(source[i].a * alpha), 0, 255));
                Assert.That(actual[i], Is.EqualTo(expected), label + " pixel " + i);
                if (expected.a > 0 &&
                    (expected.r > 0 || expected.g > 0 || expected.b > 0))
                {
                    foundVisibleColor = true;
                }
            }

            Assert.That(
                foundVisibleColor,
                Is.True,
                label + " did not contain a visible source-colored pixel to verify.");
        }

        private static void AssertColorApproximately(
            Color actual,
            Color expected,
            string label)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), label + " R");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), label + " G");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), label + " B");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), label + " A");
        }

        private static bool HasPristineSwirlSidecar(
            string swirlFolder,
            DimensionPortalSwirlArtworkEditorUtility.AnimationZeroTextureSlot current,
            Color32[] pristinePixels)
        {
            string currentColorPath = NormalizePath(
                AssetDatabase.GetAssetPath(current.ColorTexture));
            string currentEmissivePath = NormalizePath(
                AssetDatabase.GetAssetPath(current.EmissiveTexture));
            string currentNormalPath = NormalizePath(
                AssetDatabase.GetAssetPath(current.NormalTexture));
            string[] textureGuids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { swirlFolder });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = NormalizePath(
                    AssetDatabase.GUIDToAssetPath(textureGuids[i]));
                if (string.Equals(path, currentColorPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, currentEmissivePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, currentNormalPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    continue;
                }

                Color32[] pixels = ReadPixels(texture);
                if (pixels.Length == pristinePixels.Length &&
                    PixelsEqual(pixels, pristinePixels))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PixelsEqual(Color32[] left, Color32[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (!left[i].Equals(right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetSwirlVisualSettings(
            DimensionPortalVisualProfileAsset profile,
            bool visible,
            Color emissiveColor,
            bool flipX,
            bool flipY,
            Vector2 scale)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            serialized.FindProperty("centerSwirlVisible").boolValue = visible;
            serialized.FindProperty("centerSwirlEmissiveColor").colorValue = emissiveColor;
            serialized.FindProperty("centerSwirlFlipX").boolValue = flipX;
            serialized.FindProperty("centerSwirlFlipY").boolValue = flipY;
            serialized.FindProperty("centerParticleScale").vector2Value = scale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void AssertSwirlVisualSettings(
            SerializedObject serialized,
            bool expectedVisible,
            Color expectedEmissiveColor,
            bool expectedFlipX,
            bool expectedFlipY,
            Vector2 expectedScale)
        {
            Assert.That(
                serialized.FindProperty("centerSwirlVisible").boolValue,
                Is.EqualTo(expectedVisible));
            Assert.That(
                serialized.FindProperty("centerSwirlEmissiveColor").colorValue,
                Is.EqualTo(expectedEmissiveColor));
            Assert.That(
                serialized.FindProperty("centerSwirlFlipX").boolValue,
                Is.EqualTo(expectedFlipX));
            Assert.That(
                serialized.FindProperty("centerSwirlFlipY").boolValue,
                Is.EqualTo(expectedFlipY));
            Assert.That(
                serialized.FindProperty("centerParticleScale").vector2Value,
                Is.EqualTo(expectedScale));
        }

        private static void AssertTexturePixelsEqual(
            Texture2D expected,
            Texture2D actual,
            string label)
        {
            Color32[] expectedPixels = ReadPixels(expected);
            Color32[] actualPixels = ReadPixels(actual);
            Assert.That(actualPixels, Has.Length.EqualTo(expectedPixels.Length), label);
            Assert.That(actualPixels, Is.EqualTo(expectedPixels), label);
        }

        private static Color32[] ReadPixels(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            Assert.That(path, Is.Not.Empty);
            Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.That(readable.LoadImage(File.ReadAllBytes(path), false), Is.True);
            Color32[] pixels = readable.GetPixels32();
            UnityEngine.Object.DestroyImmediate(readable);
            return pixels;
        }

        private static void SetStaticTextures(
            SpriteAsset asset,
            Texture2D texture,
            Texture2D emissive,
            Texture2D normal)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty data = serialized.FindProperty("m_staticSpriteData");
            Assert.That(data, Is.Not.Null);
            data.FindPropertyRelative("texture").objectReferenceValue = texture;
            data.FindPropertyRelative("emissiveTexture").objectReferenceValue = emissive;
            data.FindPropertyRelative("normalTexture").objectReferenceValue = normal;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAddress(SpriteAsset asset, long low, long high)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty address = serialized.FindProperty("m_address");
            Assert.That(address, Is.Not.Null);
            address.FindPropertyRelative("m_low").longValue = low;
            address.FindPropertyRelative("m_high").longValue = high;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReadAddress(SpriteAsset asset, out long low, out long high)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty address = serialized.FindProperty("m_address");
            Assert.That(address, Is.Not.Null);
            low = address.FindPropertyRelative("m_low").longValue;
            high = address.FindPropertyRelative("m_high").longValue;
        }

        private static void SetReference(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            SpriteAsset asset)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(
                DimensionPortalPackageEditorUtility.GetReferencePropertyName(layer));
            Assert.That(reference, Is.Not.Null);
            if (asset == null)
            {
                SerializedProperty address = reference.FindPropertyRelative("m_address");
                address.FindPropertyRelative("m_low").longValue = 0L;
                address.FindPropertyRelative("m_high").longValue = 0L;
            }
            else
            {
                ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(reference, asset);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache(profile, layer);
        }

        private static SpriteAsset ResolveReference(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            DimensionPortalArtworkEditorUtility.ClassifyReference(
                serialized.FindProperty(DimensionPortalPackageEditorUtility.GetReferencePropertyName(layer)),
                profile,
                layer,
                out SpriteAsset asset);
            return asset;
        }

        private static void AssertReferenceKind(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkReferenceKind expected,
            out SpriteAsset asset)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            DimensionPortalArtworkReferenceKind actual =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    serialized.FindProperty(DimensionPortalPackageEditorUtility.GetReferencePropertyName(layer)),
                    profile,
                    layer,
                    out asset);
            Assert.That(actual, Is.EqualTo(expected), layer.ToString());
        }

        /// <summary>
        /// Builds a throwaway profile whose serialized state is, by construction, the exact
        /// output of restoring every Studio layer to vanilla. This is the single source of
        /// truth the Restore-vanilla action must reproduce.
        ///
        /// It also pins the canonical inner-fleck default: both the versioned defaults
        /// migration (<see cref="DimensionPortalArtworkEditorUtility.EnsureProfileInitialized"/>,
        /// schema version 5) and <c>RestoreLayerDefaults</c> resolve
        /// <c>centerParticlesFollowCenterPalette</c> to <c>false</c> with a white tint, i.e.
        /// the independent vanilla-gradient fleck path. The raw C# field default of
        /// <c>true</c> is only a migration seed and is always overwritten on initialization,
        /// so "restore to schema default" and "restore to the vanilla appearance" agree here.
        /// </summary>
        private DimensionPortalVisualProfileAsset CreateInitializedVanillaProfile()
        {
            DimensionPortalVisualProfileAsset profile =
                ScriptableObject.CreateInstance<DimensionPortalVisualProfileAsset>();
            profile.name = "VanillaReferenceProfile";
            AssetDatabase.CreateAsset(
                profile,
                TestRoot + "/VanillaReferenceProfile.asset");

            // Mirror the source profile's setup so framework artwork references and the
            // versioned defaults migration run identically before the layer restores.
            DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                template,
                profile,
                out _);

            SerializedObject serialized = new SerializedObject(profile);
            foreach (DimensionPortalAppearanceStudio.StudioLayer layer in
                (DimensionPortalAppearanceStudio.StudioLayer[])Enum.GetValues(
                    typeof(DimensionPortalAppearanceStudio.StudioLayer)))
            {
                serialized.Update();
                Assert.That(
                    DimensionPortalAppearanceStudio.RestoreLayerToVanilla(
                        serialized,
                        layer,
                        out string message),
                    Is.True,
                    message);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return profile;
        }

        /// <summary>
        /// The serialized properties a Studio layer owns and must reset on Restore vanilla.
        /// These mirror <c>RestoreLayerDefaults</c> exactly. Artwork data-block references
        /// (portalFrameSpriteAsset, chargeWaveSpriteAsset, milestoneSpriteAsset,
        /// centerEffectSpriteAsset, centerSwirlSpriteAsset) are intentionally excluded here
        /// because their restoration runs through the framework-reference assignment path and
        /// is covered by the dedicated reference-classification and migration tests.
        /// </summary>
        private static string[] GetRestoreLayerPropertyNames(
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.Frame:
                    return new[]
                    {
                        "frameTint",
                        "frameEmissiveColor",
                        "frameVisible",
                        "frameOffsetPixels",
                        "frameScale",
                        "frameRotationDegrees",
                        "frameFlipX",
                        "frameFlipY"
                    };
                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    return new[]
                    {
                        "chargeWaveDarkColor",
                        "chargeWaveDeepColor",
                        "chargeWaveMidColor",
                        "chargeWaveBrightColor",
                        "chargeWaveCoreColor",
                        "chargeWaveTint",
                        "chargeWaveEmissiveColor",
                        "chargeWaveSpeed",
                        "chargeWaveVisible",
                        "chargeWaveOffsetPixels",
                        "chargeWaveScale",
                        "chargeWaveRotationDegrees",
                        "chargeWaveFlipX",
                        "chargeWaveFlipY"
                    };
                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    return new[]
                    {
                        "milestoneDarkColor",
                        "milestoneDeepColor",
                        "milestoneMidColor",
                        "milestoneBrightColor",
                        "milestoneCoreColor",
                        "milestoneTint",
                        "milestoneEmissiveColor",
                        "firstMilestone",
                        "secondMilestone",
                        "thirdMilestone",
                        "milestonesVisible",
                        "milestoneOffsetPixels",
                        "milestoneScale",
                        "milestoneRotationDegrees",
                        "milestoneFlipX",
                        "milestoneFlipY"
                    };
                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    return new[]
                    {
                        "centerDarkColor",
                        "centerDeepColor",
                        "centerMidColor",
                        "centerBrightColor",
                        "centerCoreColor",
                        "centerHighlightColor",
                        "centerTint",
                        "centerEmissiveColor",
                        "centerVisible",
                        "centerOffsetPixels",
                        "centerScale",
                        "centerRotationDegrees",
                        "centerFlipX",
                        "centerFlipY",
                        "centerGlowIntensity"
                    };
                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    return new[]
                    {
                        "centerParticlesEnabled",
                        "centerSwirlOverrideVanilla",
                        "centerSwirlVisible",
                        "centerParticlesFollowCenterPalette",
                        "centerParticleTint",
                        "centerParticleSprite",
                        "centerParticleTexture",
                        "centerParticleEmissionMultiplier",
                        "centerParticleSizeMultiplier",
                        "centerParticleLifetimeMultiplier",
                        "centerParticleOrbitSpeedMultiplier",
                        "centerParticleRadialSpeedMultiplier",
                        "centerParticleRadiusMultiplier",
                        "centerParticleTrailsEnabled",
                        "centerParticleTrailLifetimeMultiplier",
                        "centerParticleOffsetPixels",
                        "centerParticleScale",
                        "centerParticleRotationDegrees",
                        "centerSwirlFlipX",
                        "centerSwirlFlipY",
                        "centerSwirlPlaybackSpeed",
                        "centerSwirlEmissiveColor"
                    };
                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    return new[]
                    {
                        "playReadyFlash",
                        "readyFlashFollowsCenterPalette",
                        "readyFlashTint",
                        "readyFlashSprites",
                        "readyFlashTexture",
                        "readyFlashEmissionMultiplier",
                        "readyFlashSizeMultiplier",
                        "readyFlashOffsetPixels",
                        "readyFlashScale",
                        "readyFlashRotationDegrees"
                    };
                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    return new[]
                    {
                        "groundLightEnabled",
                        "groundLightColor",

                        // groundLightIntensity is NOT here. The serialized field went when the
                        // flicker was found to overwrite it in the first frame; the reset has
                        // nothing to write and FindProperty answers null, so naming it here fails
                        // the layer's own reset test.
                        "groundLightRange",
                        "groundLightMinimumIntensity",
                        "groundLightMaximumIntensity",
                        "groundLightMovement",
                        "groundLightCastsShadows",
                        "groundLightOffsetPixels",
                        "portalShadowEnabled",
                        "portalShadowSprite",
                        "portalShadowCasterSprite",
                        "portalShadowOffsetPixels",
                        "portalShadowScale",
                        "portalShadowRotationDegrees",
                        "portalShadowFlipX",
                        "portalShadowFlipY"
                    };
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Stages a value on <paramref name="actual"/> that is guaranteed to differ from the
        /// vanilla value in <paramref name="expected"/>, so a later Restore vanilla has a real
        /// change to undo. Object-reference and array properties are populated with the
        /// supplied sentinel assets; <paramref name="nonVanillaArtwork"/> is accepted for
        /// call-site symmetry with the artwork layers but is unused because no owned scalar
        /// property is a SpriteAsset reference.
        /// </summary>
        private static void SetSerializedPropertyToNonVanilla(
            SerializedProperty actual,
            SerializedProperty expected,
            string propertyName,
            SpriteAsset nonVanillaArtwork,
            Sprite sentinelSprite,
            Texture2D sentinelTexture)
        {
            switch (actual.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    actual.boolValue = !expected.boolValue;
                    return;
                case SerializedPropertyType.Float:
                    actual.floatValue = expected.floatValue + 3.5f;
                    return;
                case SerializedPropertyType.Integer:
                    actual.intValue = expected.intValue + 5;
                    return;
                case SerializedPropertyType.Color:
                    actual.colorValue = MakeContrastingColor(expected.colorValue);
                    return;
                case SerializedPropertyType.Vector2:
                    actual.vector2Value =
                        expected.vector2Value + new Vector2(3.25f, 4.75f);
                    return;
                case SerializedPropertyType.ObjectReference:
                    actual.objectReferenceValue =
                        propertyName.IndexOf("Texture", StringComparison.Ordinal) >= 0
                            ? (UnityEngine.Object)sentinelTexture
                            : sentinelSprite;
                    return;
            }

            if (actual.isArray)
            {
                actual.arraySize = 1;
                actual.GetArrayElementAtIndex(0).objectReferenceValue = sentinelSprite;
                return;
            }

            Assert.Fail(
                "Unhandled property type for " + propertyName + ": " + actual.propertyType);
        }

        /// <summary>
        /// Asserts that a restored serialized property matches the vanilla reference value.
        /// </summary>
        private static void AssertSerializedPropertyMatches(
            SerializedProperty actual,
            SerializedProperty expected,
            string label)
        {
            Assert.That(actual, Is.Not.Null, label);
            Assert.That(expected, Is.Not.Null, label);
            switch (expected.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    Assert.That(actual.boolValue, Is.EqualTo(expected.boolValue), label);
                    return;
                case SerializedPropertyType.Float:
                    Assert.That(
                        actual.floatValue,
                        Is.EqualTo(expected.floatValue).Within(0.0001f),
                        label);
                    return;
                case SerializedPropertyType.Integer:
                    Assert.That(actual.intValue, Is.EqualTo(expected.intValue), label);
                    return;
                case SerializedPropertyType.Color:
                    Assert.That(actual.colorValue, Is.EqualTo(expected.colorValue), label);
                    return;
                case SerializedPropertyType.Vector2:
                    Assert.That(actual.vector2Value, Is.EqualTo(expected.vector2Value), label);
                    return;
                case SerializedPropertyType.ObjectReference:
                    Assert.That(
                        actual.objectReferenceValue,
                        Is.EqualTo(expected.objectReferenceValue),
                        label);
                    return;
            }

            if (expected.isArray)
            {
                Assert.That(
                    actual.arraySize,
                    Is.EqualTo(expected.arraySize),
                    label + ".arraySize");
                for (int i = 0; i < expected.arraySize; i++)
                {
                    Assert.That(
                        actual.GetArrayElementAtIndex(i).objectReferenceValue,
                        Is.EqualTo(expected.GetArrayElementAtIndex(i).objectReferenceValue),
                        label + "[" + i + "]");
                }

                return;
            }

            Assert.Fail("Unhandled property type for " + label + ": " + expected.propertyType);
        }

        /// <summary>
        /// Returns a color whose every channel is shifted well away from
        /// <paramref name="source"/>, so it can never accidentally equal the vanilla value.
        /// HDR channels above 1 remain valid because the shift is a fixed offset.
        /// </summary>
        private static Color MakeContrastingColor(Color source)
        {
            return new Color(
                source.r > 0.5f ? source.r - 0.45f : source.r + 0.45f,
                source.g > 0.5f ? source.g - 0.45f : source.g + 0.45f,
                source.b > 0.5f ? source.b - 0.45f : source.b + 0.45f,
                source.a > 0.5f ? source.a - 0.45f : source.a + 0.45f);
        }
    }
}
#endif
