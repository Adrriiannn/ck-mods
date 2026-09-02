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
    /// The swirl half: its sprite asset, its sheets, and the colour baked into it.
    /// </summary>
    internal sealed partial class DimensionPortalPresetPackageEditorTests
    {
        [Test]
        public void EnsureProfileInitialized_PopulatesFrameworkSwirlAndExposesTint()
        {
            SerializedObject serialized = new SerializedObject(sourceProfile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty("centerSwirlSpriteAsset");
            Assert.That(reference, Is.Not.Null);
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryReadReferenceAddress(
                    reference,
                    out long low,
                    out long high),
                Is.True);
            Assert.That(
                low,
                Is.EqualTo(DimensionPortalArtworkEditorUtility.FrameworkSwirlAddressLow));
            Assert.That(
                high,
                Is.EqualTo(DimensionPortalArtworkEditorUtility.FrameworkSwirlAddressHigh));
            Assert.That(
                serialized.FindProperty("centerParticlesFollowCenterPalette").boolValue,
                Is.False,
                "The visible Swirls tint must not be shadowed by a legacy hidden follow flag.");
            Assert.That(
                serialized.FindProperty("centerSwirlVisible").boolValue,
                Is.True,
                "Initialized profiles must keep the Swirls layer visible.");
            Assert.That(
                serialized.FindProperty("centerSwirlEmissiveColor").colorValue,
                Is.EqualTo(Color.white),
                "Initialized profiles must preserve neutral custom-swirl emission.");
            Assert.That(serialized.FindProperty("centerSwirlFlipX").boolValue, Is.False);
            Assert.That(serialized.FindProperty("centerSwirlFlipY").boolValue, Is.False);
            Assert.That(
                serialized.FindProperty("centerParticleScale").vector2Value,
                Is.EqualTo(Vector2.one),
                "Initialized profiles must preserve the custom swirl at authored scale.");
        }

        [Test]
        public void EnsureProfileInitialized_LegacyVersionFiveWithEmptySwirl_RestoresFrameworkStarter()
        {
            Color authoredTint = new Color(0.18f, 0.72f, 0.41f, 0.83f);
            SerializedObject legacy = new SerializedObject(sourceProfile);
            legacy.Update();
            legacy.FindProperty("portalArtworkDefaultsVersion").intValue = 5;
            legacy.FindProperty("centerParticleTint").colorValue = authoredTint;
            SerializedProperty reference = legacy.FindProperty("centerSwirlSpriteAsset");
            SerializedProperty address = reference.FindPropertyRelative("m_address");
            address.FindPropertyRelative("m_low").longValue = 0L;
            address.FindPropertyRelative("m_high").longValue = 0L;
            legacy.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sourceProfile);
            AssetDatabase.SaveAssets();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();

            Assert.That(
                DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                    template,
                    sourceProfile,
                    out string message),
                Is.True,
                message);

            SerializedObject migrated = new SerializedObject(sourceProfile);
            migrated.Update();
            SerializedProperty migratedReference = migrated.FindProperty(
                "centerSwirlSpriteAsset");
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryReadReferenceAddress(
                    migratedReference,
                    out long low,
                    out long high),
                Is.True);
            Assert.That(
                low,
                Is.EqualTo(DimensionPortalArtworkEditorUtility.FrameworkSwirlAddressLow));
            Assert.That(
                high,
                Is.EqualTo(DimensionPortalArtworkEditorUtility.FrameworkSwirlAddressHigh));
            Assert.That(
                migrated.FindProperty("centerParticleTint").colorValue,
                Is.EqualTo(authoredTint),
                "Repairing a missing starter must not reset authored swirl colors.");
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    sourceProfile,
                    migratedReference,
                    NormalizePath(Path.GetDirectoryName(
                        AssetDatabase.GetAssetPath(sourceProfile))),
                    out SpriteAsset resolved),
                Is.True);
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(resolved)),
                Is.EqualTo(FrameworkSwirlPath));
        }

        [Test]
        public void SaveAs_LocalizesCustomSwirlSpriteAssetAndAllAnimationSheets()
        {
            SpriteAsset external = CreateExternalSwirl(
                out List<Texture2D> sourceColors,
                out List<Texture2D> sourceEmissives,
                out List<Texture2D> sourceNormals);
            Color authoredEmissive = new Color(1.75f, 0.25f, 2.5f, 0.65f);
            Vector2 authoredScale = new Vector2(0.72f, 1.18f);
            SetSwirlReference(sourceProfile, external, true);
            SetSwirlVisualSettings(
                sourceProfile,
                false,
                authoredEmissive,
                true,
                true,
                authoredScale);
            AssetDatabase.SaveAssets();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Custom Swirl Package",
                    false,
                    out DimensionPortalVisualProfileAsset created,
                    out string message),
                Is.True,
                message);
            Assert.That(created, Is.Not.Null);

            string packageRoot = NormalizePath(
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(created)));
            SerializedObject serialized = new SerializedObject(created);
            serialized.Update();
            AssertSwirlVisualSettings(
                serialized,
                false,
                authoredEmissive,
                true,
                true,
                authoredScale);
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    created,
                    serialized.FindProperty("centerSwirlSpriteAsset"),
                    packageRoot,
                    out SpriteAsset packaged),
                Is.True);
            Assert.That(packaged, Is.Not.Null);
            Assert.That(packaged, Is.Not.SameAs(external));
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(packaged)),
                Does.StartWith(packageRoot + "/Artwork/Swirls/"));

            ReadAddress(external, out long sourceLow, out long sourceHigh);
            ReadAddress(packaged, out long packagedLow, out long packagedHigh);
            Assert.That(packagedLow == 0L && packagedHigh == 0L, Is.False);
            Assert.That(
                packagedLow == sourceLow && packagedHigh == sourceHigh,
                Is.False,
                "A localized SpriteAsset must not reuse its source Scriptable Data address.");

            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryValidateAnimationContract(
                    packaged,
                    true,
                    out List<DimensionPortalSwirlArtworkEditorUtility.TextureDependency>
                        packagedDependencies,
                    out string validationMessage),
                Is.True,
                validationMessage);
            Assert.That(
                packagedDependencies,
                Has.Count.EqualTo(
                    sourceColors.Count + sourceEmissives.Count + sourceNormals.Count));
            for (int i = 0; i < packagedDependencies.Count; i++)
            {
                Assert.That(
                    NormalizePath(AssetDatabase.GetAssetPath(
                        packagedDependencies[i].Texture)),
                    Does.StartWith(packageRoot + "/Artwork/Swirls/"));
            }

            Assert.That(
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    created,
                    out DimensionPortalPackageAsset package,
                    out string resolvedRoot),
                Is.True);
            Assert.That(NormalizePath(resolvedRoot), Is.EqualTo(packageRoot));
            Assert.That(package.SchemaVersion, Is.EqualTo(
                DimensionPortalPackageAsset.CurrentSchemaVersion));
            Assert.That(HasArtworkRole(package, "Swirls"), Is.True);
            Assert.That(HasDependencyRole(package, "Swirls.Animation[0].Color"), Is.True);
            Assert.That(manifest.spriteAssets.Contains(packaged), Is.True);

            string packagedPath = NormalizePath(AssetDatabase.GetAssetPath(packaged));
            Assert.That(
                DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    created,
                    out string repeatMessage),
                Is.True,
                repeatMessage);
            serialized.Update();
            AssertSwirlVisualSettings(
                serialized,
                false,
                authoredEmissive,
                true,
                true,
                authoredScale);
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    created,
                    serialized.FindProperty("centerSwirlSpriteAsset"),
                    packageRoot,
                    out SpriteAsset repeated),
                Is.True);
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(repeated)),
                Is.EqualTo(packagedPath),
                "Repeated Save & Update must not manufacture another Swirls asset.");
        }

        [Test]
        public void CustomSwirlColor_BakesIntoProfileOwnedAsset()
        {
            // Customizing the swirl color must create a profile-owned SpriteAsset (like every
            // other layer) whose pixels carry the color. The shipped starter sheet is neutral
            // white, so a red tint bakes to red pixels with the green/blue channels driven to
            // ~0 — the exact behaviour a blue source could never produce.
            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Baked Swirl Package",
                    false,
                    out DimensionPortalVisualProfileAsset created,
                    out string saveMessage),
                Is.True,
                saveMessage);

            SerializedObject authored = new SerializedObject(created);
            authored.Update();
            authored.FindProperty("centerSwirlOverrideVanilla").boolValue = true;
            authored.FindProperty("centerParticleTint").colorValue = new Color(1f, 0f, 0f, 1f);
            authored.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();

            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.FlushSwirlBake(
                    template,
                    created,
                    out string bakeMessage),
                Is.True,
                bakeMessage);

            string packageRoot = NormalizePath(
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(created)));
            SerializedObject serialized = new SerializedObject(created);
            serialized.Update();
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    created,
                    serialized.FindProperty("centerSwirlSpriteAsset"),
                    packageRoot,
                    out SpriteAsset owned),
                Is.True);
            Assert.That(owned, Is.Not.Null);
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.IsFrameworkAsset(owned),
                Is.False,
                "Customizing the swirl color must create a profile-owned asset, not reuse the framework starter.");

            Texture2D colorSheet = owned.GetAnimationAt(0).spriteData.texture;
            Assert.That(colorSheet, Is.Not.Null, "The baked swirl is missing its color sheet.");
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(colorSheet)),
                Does.StartWith(packageRoot + "/Artwork/Swirls/"),
                "The baked swirl color sheet must be package-owned.");

            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(
                    decoded.LoadImage(File.ReadAllBytes(
                        Path.GetFullPath(AssetDatabase.GetAssetPath(colorSheet)))),
                    Is.True,
                    "Could not decode the baked swirl color sheet.");
                Color32[] pixels = decoded.GetPixels32();
                int litRed = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.a <= 40)
                    {
                        continue;
                    }

                    Assert.That(
                        pixel.g,
                        Is.LessThan(48),
                        "Green survived a red bake — the color was not baked from a neutral source.");
                    Assert.That(
                        pixel.b,
                        Is.LessThan(48),
                        "Blue survived a red bake — the color was not baked from a neutral source.");
                    if (pixel.r > 150)
                    {
                        litRed++;
                    }
                }

                Assert.That(litRed, Is.GreaterThan(0), "The baked swirl has no lit red pixels.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
            }
        }

        [Test]
        public void SaveProfile_AlternatingProfilesAndAssetRefresh_PreserveIndependentSwirlReferences()
        {
            SpriteAsset external = CreateExternalSwirl(
                out _,
                out _,
                out _);
            SetSwirlReference(sourceProfile, external, true);
            AssetDatabase.SaveAssets();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Profile Switch Custom Swirl",
                    false,
                    out DimensionPortalVisualProfileAsset customProfile,
                    out string customCreateMessage),
                Is.True,
                customCreateMessage);
            Assert.That(customProfile, Is.Not.Null);

            SpriteAsset framework = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                FrameworkSwirlPath);
            Assert.That(framework, Is.Not.Null, FrameworkSwirlPath);
            SetSwirlReference(sourceProfile, framework, false);
            AssetDatabase.SaveAssets();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Profile Switch Framework Swirl",
                    false,
                    out DimensionPortalVisualProfileAsset frameworkProfile,
                    out string frameworkCreateMessage),
                Is.True,
                frameworkCreateMessage);
            Assert.That(frameworkProfile, Is.Not.Null);

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    customProfile,
                    out string customSaveMessage),
                Is.True,
                customSaveMessage);
            Assert.That(
                DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    frameworkProfile,
                    out string frameworkSaveMessage),
                Is.True,
                frameworkSaveMessage);
            Assert.That(
                DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    customProfile,
                    out string repeatedCustomSaveMessage),
                Is.True,
                repeatedCustomSaveMessage);
            Assert.That(
                template.PortalVisualProfile,
                Is.SameAs(sourceProfile),
                "Browsing and saving another profile must not change the profile in use.");

            string customProfilePath = NormalizePath(
                AssetDatabase.GetAssetPath(customProfile));
            string customPackageRoot = NormalizePath(
                Path.GetDirectoryName(customProfilePath));
            SerializedObject savedCustom = new SerializedObject(customProfile);
            savedCustom.Update();
            SerializedProperty savedCustomReference = savedCustom.FindProperty(
                "centerSwirlSpriteAsset");
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryReadReferenceAddress(
                    savedCustomReference,
                    out long expectedCustomLow,
                    out long expectedCustomHigh),
                Is.True);
            Assert.That(expectedCustomLow == 0L && expectedCustomHigh == 0L, Is.False);
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    customProfile,
                    savedCustomReference,
                    customPackageRoot,
                    out SpriteAsset savedCustomSwirl),
                Is.True);
            Assert.That(savedCustomSwirl, Is.Not.Null);
            string customSwirlPath = NormalizePath(
                AssetDatabase.GetAssetPath(savedCustomSwirl));

            string frameworkProfilePath = NormalizePath(
                AssetDatabase.GetAssetPath(frameworkProfile));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();

            DimensionPortalVisualProfileAsset reloadedCustom =
                AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(
                    customProfilePath);
            DimensionPortalVisualProfileAsset reloadedFramework =
                AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(
                    frameworkProfilePath);
            Assert.That(reloadedCustom, Is.Not.Null);
            Assert.That(reloadedFramework, Is.Not.Null);

            SerializedObject reloadedCustomSerialized = new SerializedObject(
                reloadedCustom);
            reloadedCustomSerialized.Update();
            SerializedProperty reloadedCustomReference = reloadedCustomSerialized.FindProperty(
                "centerSwirlSpriteAsset");
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryReadReferenceAddress(
                    reloadedCustomReference,
                    out long reloadedCustomLow,
                    out long reloadedCustomHigh),
                Is.True);
            Assert.That(reloadedCustomLow, Is.EqualTo(expectedCustomLow));
            Assert.That(reloadedCustomHigh, Is.EqualTo(expectedCustomHigh));
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    reloadedCustom,
                    reloadedCustomReference,
                    customPackageRoot,
                    out SpriteAsset reloadedCustomSwirl),
                Is.True);
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(reloadedCustomSwirl)),
                Is.EqualTo(customSwirlPath));

            SerializedObject reloadedFrameworkSerialized = new SerializedObject(
                reloadedFramework);
            reloadedFrameworkSerialized.Update();
            SerializedProperty reloadedFrameworkReference =
                reloadedFrameworkSerialized.FindProperty("centerSwirlSpriteAsset");
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryReadReferenceAddress(
                    reloadedFrameworkReference,
                    out long reloadedFrameworkLow,
                    out long reloadedFrameworkHigh),
                Is.True);
            Assert.That(
                reloadedFrameworkLow,
                Is.EqualTo(DimensionPortalArtworkEditorUtility.FrameworkSwirlAddressLow));
            Assert.That(
                reloadedFrameworkHigh,
                Is.EqualTo(DimensionPortalArtworkEditorUtility.FrameworkSwirlAddressHigh));
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    reloadedFramework,
                    reloadedFrameworkReference,
                    NormalizePath(Path.GetDirectoryName(frameworkProfilePath)),
                    out SpriteAsset reloadedFrameworkSwirl),
                Is.True);
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(reloadedFrameworkSwirl)),
                Is.EqualTo(FrameworkSwirlPath));
        }
    }
}
#endif
