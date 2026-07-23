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
    /// AssetDatabase-level regression coverage for Portal Studio package saves.
    /// The fixture is an isolated throwaway consumer mod so a failed test can never
    /// rewrite a real dimension, preset, SpriteAsset manifest, or authored texture.
    /// </summary>
    internal sealed class DimensionPortalPresetPackageEditorTests
    {
        private const string TestRoot = "Assets/__ExpandNullforgePortalPresetTests";
        private const string DimensionFolder = TestRoot + "/DimensionAssets/TestDimension";
        private const string DataFolder = TestRoot + "/Data";
        private const string FrameworkFramePath =
            "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalBody.asset";
        private const string FrameworkCenterPath =
            "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCenterEffect.asset";
        private const string FrameworkSwirlPath =
            "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCustomSwirl.asset";
        private const string MetadataPrefix = "ExpandNullforge.PortalArtwork:";

        [Serializable]
        private sealed class LegacyManagedArtworkMetadata
        {
            public int schemaVersion = 1;
            public string owner = "Dimensions API";
            public string profileGuid;
            public string layer = "frame";
            public string sourceAssetGuid;
            public long sourceAddressLow;
            public long sourceAddressHigh;
            public string managedAssetGuid;
            public long managedAddressLow;
            public long managedAddressHigh;
            public Color[] palette = Array.Empty<Color>();

            // Intentionally no directTextureOverride field. This reproduces Frame assets
            // created before exact texture override metadata was introduced.
        }

        private ScriptableDataEditorUtility.Context previousContext;
        private DimensionTemplateAsset template;
        private DimensionPortalVisualProfileAsset sourceProfile;
        private SpriteAssetManifest manifest;
        private SpriteAsset legacyFrame;
        private Texture2D legacyFrameTexture;
        private Texture2D legacyFrameEmissive;

        [SetUp]
        public void SetUp()
        {
            previousContext = ScriptableDataEditorUtility.currentContext;
            DeleteFixture();
            EnsureFolder(DimensionFolder);
            EnsureFolder(DataFolder + "/SpriteAsset");

            manifest = ScriptableObject.CreateInstance<SpriteAssetManifest>();
            manifest.spriteAssets = new List<SpriteAssetBase>();
            manifest.gradientMaps = new List<Texture2D>();
            manifest.transformAnimations = new List<TransformAnimation>();
            AssetDatabase.CreateAsset(manifest, TestRoot + "/SpriteAssetManifest.asset");

            sourceProfile = ScriptableObject.CreateInstance<DimensionPortalVisualProfileAsset>();
            sourceProfile.name = "SourcePortalProfile";
            AssetDatabase.CreateAsset(
                sourceProfile,
                DimensionFolder + "/PortalVisualProfile.asset");

            template = ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            template.name = "TestDimension";
            template.ApplyCustomizerMetadata(
                "PortalPresetRegression:TestDimension",
                "Portal preset regression",
                string.Empty,
                "PortalPresetRegression",
                "Portal preset regression",
                "1.0.0",
                "Dimensions API tests",
                1);
            template.SetPortalVisualProfile(sourceProfile);
            AssetDatabase.CreateAsset(template, DimensionFolder + "/TestDimension.asset");

            Assert.That(
                DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out string contextError),
                Is.True,
                contextError);
            DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                template,
                sourceProfile,
                out _);

            CreateLegacyManagedFrame();
            AssetDatabase.SaveAssets();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
            AssertReferenceKind(
                sourceProfile,
                DimensionPortalArtworkLayer.Frame,
                DimensionPortalArtworkReferenceKind.Managed,
                out SpriteAsset resolvedFrame);
            Assert.That(resolvedFrame, Is.SameAs(legacyFrame));
        }

        [TearDown]
        public void TearDown()
        {
            CancelPendingFixtureArtwork();
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
            if (previousContext.isValid)
            {
                ScriptableDataEditorUtility.SetContext(previousContext);
            }

            DeleteFixture();
        }

        [Test]
        public void SaveAs_LegacyManagedFrame_PreservesExactStaticColorAndEmissive()
        {
            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Legacy Frame Copy",
                    false,
                    out DimensionPortalVisualProfileAsset created,
                    out string message),
                Is.True,
                message);
            Assert.That(created, Is.Not.Null);

            Assert.That(
                DimensionPortalArtworkEditorUtility.TryGetTextureSlots(
                    created,
                    DimensionPortalArtworkLayer.Frame,
                    out DimensionPortalArtworkEditorUtility.TextureSlot[] slots,
                    out string slotMessage),
                Is.True,
                slotMessage);
            Assert.That(slots, Has.Length.EqualTo(1));
            Assert.That(slots[0].Texture, Is.Not.Null);
            Assert.That(slots[0].EmissiveTexture, Is.Not.Null);
            Assert.That(slots[0].Texture, Is.Not.SameAs(legacyFrameTexture));
            Assert.That(slots[0].EmissiveTexture, Is.Not.SameAs(legacyFrameEmissive));
            AssertTexturePixelsEqual(legacyFrameTexture, slots[0].Texture, "Frame color");
            AssertTexturePixelsEqual(
                legacyFrameEmissive,
                slots[0].EmissiveTexture,
                "Frame emissive");

            string packageRoot = NormalizePath(
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(created)));
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(slots[0].Texture)),
                Does.StartWith(packageRoot + "/"));
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(slots[0].EmissiveTexture)),
                Does.StartWith(packageRoot + "/"));
            Assert.That(
                AssetDatabase.LoadMainAssetAtPath(packageRoot + "/PortalPackage.asset"),
                Is.Not.Null,
                "A saved portal must have one discoverable package inventory beside its profile.");
            Assert.That(
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    created,
                    out DimensionPortalPackageAsset package,
                    out string resolvedPackageRoot),
                Is.True);
            Assert.That(NormalizePath(resolvedPackageRoot), Is.EqualTo(packageRoot));
            Assert.That(package.Artwork, Has.Count.EqualTo(4));
        }

        [Test]
        public void SaveAs_MaterializesFrameworkEmptyAndExternalLayersIntoOnePackage()
        {
            SetReference(
                sourceProfile,
                DimensionPortalArtworkLayer.Milestones,
                null);
            SpriteAsset externalCenter = CreateExternalCenter();
            SetReference(
                sourceProfile,
                DimensionPortalArtworkLayer.Center,
                externalCenter);
            AssetDatabase.SaveAssets();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Complete Package",
                    false,
                    out DimensionPortalVisualProfileAsset created,
                    out string message),
                Is.True,
                message);
            Assert.That(created, Is.Not.Null);

            string packageRoot = NormalizePath(
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(created)));
            foreach (DimensionPortalArtworkLayer layer in new[]
                     {
                         DimensionPortalArtworkLayer.Frame,
                         DimensionPortalArtworkLayer.ChargeSweep,
                         DimensionPortalArtworkLayer.Milestones,
                         DimensionPortalArtworkLayer.Center
                     })
            {
                AssertReferenceKind(
                    created,
                    layer,
                    DimensionPortalArtworkReferenceKind.Managed,
                    out SpriteAsset packagedAsset);
                Assert.That(packagedAsset, Is.Not.Null, layer.ToString());
                Assert.That(
                    NormalizePath(AssetDatabase.GetAssetPath(packagedAsset)),
                    Does.StartWith(packageRoot + "/"),
                    layer + " was not localized into its portal package.");
            }

            Assert.That(
                AssetDatabase.GetAssetPath(ResolveReference(
                    created,
                    DimensionPortalArtworkLayer.Center)),
                Is.Not.EqualTo(AssetDatabase.GetAssetPath(externalCenter)),
                "External center art must be copied, not shared by the saved package.");
            Assert.That(
                AssetDatabase.LoadMainAssetAtPath(packageRoot + "/PortalPackage.asset"),
                Is.Not.Null);
            Assert.That(
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    created,
                    out DimensionPortalPackageAsset package,
                    out string resolvedPackageRoot),
                Is.True);
            Assert.That(NormalizePath(resolvedPackageRoot), Is.EqualTo(packageRoot));
            Assert.That(package.Artwork, Has.Count.EqualTo(4));
        }

        [Test]
        public void SaveAs_LocalizesParticleSpritesAndTheirBackingTextures()
        {
            string sourceFolder = TestRoot + "/ExternalParticleArtwork";
            EnsureFolder(sourceFolder);
            Texture2D centerTexture = CreateTexture(
                sourceFolder + "/CenterFleck.png",
                new Color32(30, 220, 255, 255),
                new Color32(0, 0, 0, 0));
            Sprite centerSprite = CreateStandaloneSprite(
                sourceFolder + "/CenterFleck.asset",
                centerTexture,
                new Rect(0f, 0f, 48f, 48f),
                "CenterFleck");

            Texture2D readyTexture = CreateTexture(
                sourceFolder + "/ReadyFlash.png",
                new Color32(255, 245, 190, 255),
                new Color32(0, 0, 0, 0));
            Sprite readySprite0 = CreateStandaloneSprite(
                sourceFolder + "/ReadyFlash0.asset",
                readyTexture,
                new Rect(0f, 0f, 24f, 48f),
                "ReadyFlash0");
            Sprite readySprite1 = CreateStandaloneSprite(
                sourceFolder + "/ReadyFlash1.asset",
                readyTexture,
                new Rect(24f, 0f, 24f, 48f),
                "ReadyFlash1");
            SetObjectReference(sourceProfile, "centerParticleSprite", centerSprite);
            SetSpriteArrayReference(
                sourceProfile,
                "readyFlashSprites",
                new[] { readySprite0, readySprite1 });
            AssetDatabase.SaveAssets();

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Particle Sprite Package",
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
            Sprite packagedCenter = serialized
                .FindProperty("centerParticleSprite")
                .objectReferenceValue as Sprite;
            AssertPackagedSprite(
                packagedCenter,
                centerSprite,
                centerTexture,
                packageRoot,
                "Persistent center fleck");

            SerializedProperty packagedReady = serialized.FindProperty(
                "readyFlashSprites");
            Assert.That(packagedReady, Is.Not.Null);
            Assert.That(packagedReady.isArray, Is.True);
            Assert.That(packagedReady.arraySize, Is.EqualTo(2));
            Sprite packagedReady0 = packagedReady
                .GetArrayElementAtIndex(0)
                .objectReferenceValue as Sprite;
            Sprite packagedReady1 = packagedReady
                .GetArrayElementAtIndex(1)
                .objectReferenceValue as Sprite;
            AssertPackagedSprite(
                packagedReady0,
                readySprite0,
                readyTexture,
                packageRoot,
                "Ready flash frame 0");
            AssertPackagedSprite(
                packagedReady1,
                readySprite1,
                readyTexture,
                packageRoot,
                "Ready flash frame 1");
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(packagedReady0.texture)),
                Is.EqualTo(NormalizePath(
                    AssetDatabase.GetAssetPath(packagedReady1.texture))),
                "Frames sharing one source sheet should share one localized backing copy.");

            Assert.That(
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    created,
                    out DimensionPortalPackageAsset package,
                    out string resolvedPackageRoot),
                Is.True);
            Assert.That(NormalizePath(resolvedPackageRoot), Is.EqualTo(packageRoot));
            Assert.That(HasDependencyRole(package, "CenterParticleSprite"), Is.True);
            Assert.That(HasDependencyRole(package, "CenterParticleSprite.Source"), Is.True);
            Assert.That(HasDependencyRole(package, "ReadyFlashSprite[0]"), Is.True);
            Assert.That(HasDependencyRole(package, "ReadyFlashSprite[0].Source"), Is.True);
            Assert.That(HasDependencyRole(package, "ReadyFlashSprite[1]"), Is.True);

            HashSet<string> dependencyPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < package.Dependencies.Count; i++)
            {
                DimensionPortalPackageAsset.DependencyEntry dependency =
                    package.Dependencies[i];
                Assert.That(
                    dependencyPaths.Add(dependency.RelativePath),
                    Is.True,
                    "Package dependency inventory should contain each owned asset once.");
            }
        }

        [Test]
        public void SaveProfile_LocalizesNewParticleSpritesIntoExistingPackageOnce()
        {
            Assert.That(
                DimensionPortalPresetEditorUtility.SaveAs(
                    template,
                    "Editable Particle Package",
                    false,
                    out DimensionPortalVisualProfileAsset created,
                    out string createMessage),
                Is.True,
                createMessage);
            Assert.That(created, Is.Not.Null);

            string sourceFolder = TestRoot + "/ExternalParticleReplacement";
            EnsureFolder(sourceFolder);
            Texture2D centerTexture = CreateTexture(
                sourceFolder + "/CenterFleck.png",
                new Color32(50, 235, 155, 255),
                new Color32(0, 0, 0, 0));
            Sprite centerSprite = CreateStandaloneSprite(
                sourceFolder + "/CenterFleck.asset",
                centerTexture,
                new Rect(0f, 0f, 48f, 48f),
                "CenterFleckReplacement");

            Texture2D readyTexture = CreateTexture(
                sourceFolder + "/ReadyFlash.png",
                new Color32(255, 115, 65, 255),
                new Color32(0, 0, 0, 0));
            Sprite readySprite0 = CreateStandaloneSprite(
                sourceFolder + "/ReadyFlash0.asset",
                readyTexture,
                new Rect(0f, 0f, 24f, 48f),
                "ReadyFlashReplacement0");
            Sprite readySprite1 = CreateStandaloneSprite(
                sourceFolder + "/ReadyFlash1.asset",
                readyTexture,
                new Rect(24f, 0f, 24f, 48f),
                "ReadyFlashReplacement1");
            SetObjectReference(created, "centerParticleSprite", centerSprite);
            SetSpriteArrayReference(
                created,
                "readyFlashSprites",
                new[] { readySprite0, readySprite1 });
            AssetDatabase.SaveAssets();

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    created,
                    out string saveMessage),
                Is.True,
                saveMessage);

            string packageRoot = NormalizePath(
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(created)));
            SerializedObject serialized = new SerializedObject(created);
            serialized.Update();
            Sprite packagedCenter = serialized
                .FindProperty("centerParticleSprite")
                .objectReferenceValue as Sprite;
            SerializedProperty packagedReady = serialized.FindProperty(
                "readyFlashSprites");
            Sprite packagedReady0 = packagedReady
                .GetArrayElementAtIndex(0)
                .objectReferenceValue as Sprite;
            Sprite packagedReady1 = packagedReady
                .GetArrayElementAtIndex(1)
                .objectReferenceValue as Sprite;
            AssertPackagedSprite(
                packagedCenter,
                centerSprite,
                centerTexture,
                packageRoot,
                "Saved center fleck");
            AssertPackagedSprite(
                packagedReady0,
                readySprite0,
                readyTexture,
                packageRoot,
                "Saved ready frame 0");
            AssertPackagedSprite(
                packagedReady1,
                readySprite1,
                readyTexture,
                packageRoot,
                "Saved ready frame 1");

            string centerPath = NormalizePath(AssetDatabase.GetAssetPath(packagedCenter));
            string readyPath0 = NormalizePath(AssetDatabase.GetAssetPath(packagedReady0));
            string readyPath1 = NormalizePath(AssetDatabase.GetAssetPath(packagedReady1));
            Assert.That(
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    created,
                    out DimensionPortalPackageAsset package,
                    out string resolvedPackageRoot),
                Is.True);
            Assert.That(NormalizePath(resolvedPackageRoot), Is.EqualTo(packageRoot));
            Assert.That(HasDependencyRole(package, "CenterParticleSprite"), Is.True);
            Assert.That(HasDependencyRole(package, "CenterParticleSprite.Source"), Is.True);
            Assert.That(HasDependencyRole(package, "ReadyFlashSprite[0]"), Is.True);
            Assert.That(HasDependencyRole(package, "ReadyFlashSprite[0].Source"), Is.True);
            Assert.That(HasDependencyRole(package, "ReadyFlashSprite[1]"), Is.True);

            Assert.That(
                DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    created,
                    out string repeatMessage),
                Is.True,
                repeatMessage);
            serialized.Update();
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(serialized
                    .FindProperty("centerParticleSprite")
                    .objectReferenceValue)),
                Is.EqualTo(centerPath));
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(serialized
                    .FindProperty("readyFlashSprites")
                    .GetArrayElementAtIndex(0)
                    .objectReferenceValue)),
                Is.EqualTo(readyPath0));
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(serialized
                    .FindProperty("readyFlashSprites")
                    .GetArrayElementAtIndex(1)
                    .objectReferenceValue)),
                Is.EqualTo(readyPath1));
        }

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

        [TestCase(DimensionPortalAppearanceStudio.StudioLayer.Frame)]
        [TestCase(DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep)]
        [TestCase(DimensionPortalAppearanceStudio.StudioLayer.Milestones)]
        [TestCase(DimensionPortalAppearanceStudio.StudioLayer.Center)]
        [TestCase(DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks)]
        [TestCase(DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst)]
        [TestCase(DimensionPortalAppearanceStudio.StudioLayer.GroundLight)]
        public void RestoreLayerToVanilla_ResetsEveryOwnedPropertyAndPreservesUnrelatedSettings(
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            DimensionPortalVisualProfileAsset vanilla = CreateInitializedVanillaProfile();
            SerializedObject expected = new SerializedObject(vanilla);
            expected.Update();

            Sprite sentinelSprite = null;
            if (layer == DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks ||
                layer == DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst ||
                layer == DimensionPortalAppearanceStudio.StudioLayer.GroundLight)
            {
                sentinelSprite = CreateStandaloneSprite(
                    TestRoot + "/RestoreVanillaSentinelSprite.asset",
                    legacyFrameTexture,
                    new Rect(0f, 0f, legacyFrameTexture.width, legacyFrameTexture.height),
                    "RestoreVanillaSentinelSprite");
            }

            SpriteAsset nonVanillaArtwork = legacyFrame;
            if (layer == DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks)
            {
                nonVanillaArtwork = CreateExternalSwirl(out _, out _, out _);
            }

            string[] ownedProperties = GetRestoreLayerPropertyNames(layer);
            SerializedObject authored = new SerializedObject(sourceProfile);
            authored.Update();
            for (int i = 0; i < ownedProperties.Length; i++)
            {
                string propertyName = ownedProperties[i];
                SerializedProperty actualProperty = authored.FindProperty(propertyName);
                SerializedProperty expectedProperty = expected.FindProperty(propertyName);
                Assert.That(actualProperty, Is.Not.Null, propertyName);
                Assert.That(expectedProperty, Is.Not.Null, propertyName);
                SetSerializedPropertyToNonVanilla(
                    actualProperty,
                    expectedProperty,
                    propertyName,
                    nonVanillaArtwork,
                    sentinelSprite,
                    legacyFrameTexture);
            }

            string unrelatedPropertyName =
                layer == DimensionPortalAppearanceStudio.StudioLayer.GroundLight
                    ? "frameRotationDegrees"
                    : "groundLightRange";
            const float unrelatedSentinel = 7.625f;
            SerializedProperty unrelated = authored.FindProperty(unrelatedPropertyName);
            Assert.That(unrelated, Is.Not.Null, unrelatedPropertyName);
            unrelated.floatValue = unrelatedSentinel;
            authored.ApplyModifiedPropertiesWithoutUndo();

            // Reuse the same SerializedObject that stages every reset value. This is the
            // exact regression for the former Swirls path, where a second SerializedObject
            // assigned the framework reference and then discarded the pending tint/options.
            authored.Update();
            Assert.That(
                DimensionPortalAppearanceStudio.RestoreLayerToVanilla(
                    authored,
                    layer,
                    out string restoreMessage),
                Is.True,
                restoreMessage);
            authored.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject restored = new SerializedObject(sourceProfile);
            restored.Update();
            expected.Update();
            for (int i = 0; i < ownedProperties.Length; i++)
            {
                string propertyName = ownedProperties[i];
                AssertSerializedPropertyMatches(
                    restored.FindProperty(propertyName),
                    expected.FindProperty(propertyName),
                    layer + "." + propertyName);
            }

            Assert.That(
                restored.FindProperty(unrelatedPropertyName).floatValue,
                Is.EqualTo(unrelatedSentinel).Within(0.0001f),
                "Restoring " + layer + " must not reset another layer.");
        }

        /// <summary>
        /// Pins the canonical inner-fleck defaults so the Restore-vanilla path and the
        /// versioned defaults migration can never silently disagree again. Vanilla flecks use
        /// the independent white/vanilla-gradient path (follow=false, tint=white), while the
        /// one-shot ready burst deliberately follows the center palette (follow=true). Both
        /// values are what a freshly initialized profile resolves to and what restoring the
        /// owning layer must reproduce.
        /// </summary>
        [Test]
        public void InitializedProfile_PinsCanonicalInnerFleckDefaults()
        {
            DimensionPortalVisualProfileAsset vanilla = CreateInitializedVanillaProfile();
            SerializedObject serialized = new SerializedObject(vanilla);
            serialized.Update();

            Assert.That(
                serialized.FindProperty("centerParticlesFollowCenterPalette").boolValue,
                Is.False,
                "Vanilla flecks must use the independent white gradient, not the center palette.");
            Assert.That(
                serialized.FindProperty("centerParticleTint").colorValue,
                Is.EqualTo(DimensionPortalVisualProfileAsset.VanillaSwirlTint),
                "The neutral white swirl sheet is tinted the vanilla cyan-blue by default.");
            Assert.That(
                serialized.FindProperty("readyFlashFollowsCenterPalette").boolValue,
                Is.True,
                "The one-shot ready burst deliberately follows the center palette.");
        }

        [Test]
        public void PortalParityValidator_ReportsAllLayersAndCleanSwirl()
        {
            List<DimensionPortalParityValidator.Finding> findings =
                DimensionPortalParityValidator.Validate(template, sourceProfile, out string report);
            Assert.That(report, Does.Contain("portal parity"), report);
            foreach (string layer in new[] { "Frame", "Charge", "Milestones", "Center", "Swirls" })
            {
                Assert.That(
                    findings.Exists(f => f.Layer == layer),
                    Is.True,
                    "No parity finding for " + layer + ". Report:\n" + report);
            }

            // With override off (the default), the swirl uses vanilla particles — a clean OK
            // with no custom SpriteAsset to resolve.
            DimensionPortalParityValidator.Finding swirl = findings.Find(f => f.Layer == "Swirls");
            Assert.That(
                swirl.Severity,
                Is.EqualTo(DimensionPortalParityValidator.Severity.Ok),
                report);
            Assert.That(swirl.Message, Does.Contain("Vanilla"), report);
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

        private void CreateLegacyManagedFrame()
        {
            string profileGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(sourceProfile));
            string frameFolder =
                TestRoot + "/Data/SpriteAsset/PortalArtwork/" + profileGuid + "/frame";
            EnsureFolder(frameFolder);

            legacyFrameTexture = CreateTexture(
                frameFolder + "/LegacyFrame.png",
                new Color32(187, 32, 211, 255),
                new Color32(15, 9, 31, 0));
            legacyFrameEmissive = CreateTexture(
                frameFolder + "/LegacyFrame_Emissive.png",
                new Color32(255, 137, 24, 255),
                new Color32(0, 0, 0, 0));

            SpriteAsset framework = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                FrameworkFramePath);
            Assert.That(framework, Is.Not.Null);
            legacyFrame = UnityEngine.Object.Instantiate(framework);
            legacyFrame.name = "LegacyManagedFrame";
            SetAddress(legacyFrame, 0x1376137613761376L, 0x2468246824682468L);
            SetStaticTextures(legacyFrame, legacyFrameTexture, legacyFrameEmissive, null);
            string framePath = frameFolder + "/LegacyManagedFrame.asset";
            AssetDatabase.CreateAsset(legacyFrame, framePath);

            ReadAddress(framework, out long frameworkLow, out long frameworkHigh);
            ReadAddress(legacyFrame, out long managedLow, out long managedHigh);
            AssetImporter importer = AssetImporter.GetAtPath(framePath);
            Assert.That(importer, Is.Not.Null);
            importer.userData = MetadataPrefix + JsonUtility.ToJson(
                new LegacyManagedArtworkMetadata
                {
                    profileGuid = profileGuid,
                    sourceAssetGuid = AssetDatabase.AssetPathToGUID(FrameworkFramePath),
                    sourceAddressLow = frameworkLow,
                    sourceAddressHigh = frameworkHigh,
                    managedAssetGuid = AssetDatabase.AssetPathToGUID(framePath),
                    managedAddressLow = managedLow,
                    managedAddressHigh = managedHigh
                });
            importer.SaveAndReimport();

            // NativeFormatImporter reimport can replace the live Unity object. Resolve the
            // durable asset again before placing it in the manifest/DataBlock reference.
            legacyFrame = AssetDatabase.LoadAssetAtPath<SpriteAsset>(framePath);
            Assert.That(legacyFrame, Is.Not.Null);

            AddToManifest(legacyFrame);
            SetReference(
                sourceProfile,
                DimensionPortalArtworkLayer.Frame,
                legacyFrame);
        }

        private SpriteAsset CreateExternalCenter()
        {
            string folder = DataFolder + "/SpriteAsset/External";
            EnsureFolder(folder);
            SpriteAsset framework = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                FrameworkCenterPath);
            Assert.That(framework, Is.Not.Null);
            SpriteAsset external = UnityEngine.Object.Instantiate(framework);
            external.name = "ExternalCenter";
            SetAddress(external, 0x1111222233334444L, 0x5555666677771234L);
            AssetDatabase.CreateAsset(external, folder + "/ExternalCenter.asset");
            AddToManifest(external);
            return external;
        }

        private SpriteAsset CreateExternalSwirl(
            out List<Texture2D> colors,
            out List<Texture2D> emissives,
            out List<Texture2D> normals)
        {
            string folder = DataFolder + "/SpriteAsset/ExternalSwirl";
            EnsureFolder(folder);
            SpriteAsset framework = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                FrameworkSwirlPath);
            Assert.That(framework, Is.Not.Null, FrameworkSwirlPath);
            SpriteAsset external = UnityEngine.Object.Instantiate(framework);
            external.name = "ExternalSwirl";
            SetAddress(external, 0x1736173617361736L, 0x2864286428642864L);
            string assetPath = folder + "/ExternalSwirl.asset";
            AssetDatabase.CreateAsset(external, assetPath);

            colors = new List<Texture2D>();
            emissives = new List<Texture2D>();
            normals = new List<Texture2D>();
            SerializedObject serialized = new SerializedObject(external);
            serialized.Update();
            SerializedProperty animations = serialized.FindProperty("m_animations");
            Assert.That(animations, Is.Not.Null);
            Assert.That(animations.arraySize, Is.GreaterThan(0));
            for (int i = 0; i < animations.arraySize; i++)
            {
                SerializedProperty animation = animations.GetArrayElementAtIndex(i);
                int frameCount = animation.FindPropertyRelative("srcFrameCount").intValue;
                Assert.That(frameCount, Is.GreaterThan(0));
                Texture2D color = CreateSheetTexture(
                    folder + "/ExternalSwirl_Anim" + i + ".png",
                    frameCount,
                    new Color32((byte)(35 + i * 20), 210, 255, 255));
                Texture2D emissive = CreateSheetTexture(
                    folder + "/ExternalSwirl_Anim" + i + "_Emissive.png",
                    frameCount,
                    new Color32(180, (byte)(180 + i * 15), 255, 255));
                Texture2D normal = CreateSheetTexture(
                    folder + "/ExternalSwirl_Anim" + i + "_Normal.png",
                    frameCount,
                    new Color32(128, 128, 255, 255));
                SerializedProperty spriteData =
                    animation.FindPropertyRelative("m_spriteData");
                spriteData.FindPropertyRelative("texture").objectReferenceValue = color;
                spriteData.FindPropertyRelative("emissiveTexture").objectReferenceValue =
                    emissive;
                spriteData.FindPropertyRelative("normalTexture").objectReferenceValue = normal;
                colors.Add(color);
                emissives.Add(emissive);
                normals.Add(normal);
            }

            SerializedProperty staticData = serialized.FindProperty("m_staticSpriteData");
            if (staticData != null)
            {
                staticData.FindPropertyRelative("texture").objectReferenceValue = null;
                staticData.FindPropertyRelative("emissiveTexture").objectReferenceValue = null;
                staticData.FindPropertyRelative("normalTexture").objectReferenceValue = null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(external);
            AssetDatabase.SaveAssetIfDirty(external);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            external = AssetDatabase.LoadAssetAtPath<SpriteAsset>(assetPath);
            Assert.That(external, Is.Not.Null);
            AddToManifest(external);
            return external;
        }

        private void AddToManifest(SpriteAsset asset)
        {
            List<SpriteAssetBase> assets = manifest.spriteAssets ??
                                           new List<SpriteAssetBase>();
            if (!assets.Contains(asset))
            {
                assets.Add(asset);
            }

            manifest.spriteAssets = assets;
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
        }

        private static Texture2D CreateTexture(
            string assetPath,
            Color32 foreground,
            Color32 transparent)
        {
            const int size = 48;
            Texture2D staged = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool mark = x == y || x + y == size - 1 ||
                                (x >= 7 && x <= 10 && y >= 5 && y <= 42);
                    pixels[y * size + x] = mark ? foreground : transparent;
                }
            }

            staged.SetPixels32(pixels);
            staged.Apply(false, false);
            string absolutePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(absolutePath, staged.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staged);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D CreateSheetTexture(
            string assetPath,
            int frameCount,
            Color32 foreground)
        {
            int width = 48 * frameCount;
            const int height = 48;
            Texture2D staged = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                false);
            Color32[] pixels = new Color32[width * height];
            for (int frame = 0; frame < frameCount; frame++)
            {
                int origin = frame * 48;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < 48; x++)
                    {
                        bool mark = x == y || x + y == 47 ||
                                    (x >= 20 && x <= 27 && y >= 20 && y <= 27);
                        pixels[y * width + origin + x] = mark
                            ? foreground
                            : new Color32(0, 0, 0, 0);
                    }
                }
            }

            staged.SetPixels32(pixels);
            staged.Apply(false, false);
            string absolutePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(absolutePath, staged.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staged);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Sprite CreateStandaloneSprite(
            string assetPath,
            Texture2D texture,
            Rect rect,
            string spriteName)
        {
            Assert.That(texture, Is.Not.Null);
            Sprite sprite = Sprite.Create(
                texture,
                rect,
                new Vector2(0.5f, 0.5f),
                16f,
                1u,
                SpriteMeshType.FullRect);
            Assert.That(sprite, Is.Not.Null);
            sprite.name = spriteName;
            AssetDatabase.CreateAsset(sprite, assetPath);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
            Sprite saved = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.texture, Is.SameAs(texture));
            return saved;
        }

        private static void AssertPackagedSprite(
            Sprite actual,
            Sprite sourceSprite,
            Texture2D sourceTexture,
            string packageRoot,
            string label)
        {
            Assert.That(actual, Is.Not.Null, label);
            Assert.That(actual, Is.Not.SameAs(sourceSprite), label);
            Assert.That(actual.texture, Is.Not.Null, label + " backing texture");
            Assert.That(actual.texture, Is.Not.SameAs(sourceTexture), label);
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(actual)),
                Does.StartWith(packageRoot + "/"),
                label + " Sprite was not localized.");
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(actual.texture)),
                Does.StartWith(packageRoot + "/"),
                label + " backing texture was not localized.");
        }

        private static bool HasDependencyRole(
            DimensionPortalPackageAsset package,
            string role)
        {
            if (package == null || package.Dependencies == null)
            {
                return false;
            }

            for (int i = 0; i < package.Dependencies.Count; i++)
            {
                if (string.Equals(
                        package.Dependencies[i].Role,
                        role,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasArtworkRole(
            DimensionPortalPackageAsset package,
            string role)
        {
            if (package == null || package.Artwork == null)
            {
                return false;
            }

            for (int i = 0; i < package.Artwork.Count; i++)
            {
                if (string.Equals(
                        package.Artwork[i].Role,
                        role,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetObjectReference(
            DimensionPortalVisualProfileAsset profile,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void SetSpriteArrayReference(
            DimensionPortalVisualProfileAsset profile,
            string propertyName,
            IReadOnlyList<Sprite> sprites)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.isArray, Is.True, propertyName);
            property.arraySize = sprites == null ? 0 : sprites.Count;
            for (int i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void SetSwirlReference(
            DimensionPortalVisualProfileAsset profile,
            SpriteAsset asset,
            bool overrideVanilla)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty("centerSwirlSpriteAsset");
            SerializedProperty overrideProperty = serialized.FindProperty(
                "centerSwirlOverrideVanilla");
            Assert.That(reference, Is.Not.Null);
            Assert.That(overrideProperty, Is.Not.Null);
            ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(reference, asset);
            overrideProperty.boolValue = overrideVanilla;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void SetSwirlBakeColors(
            DimensionPortalVisualProfileAsset profile,
            Color tint,
            Color emissive,
            float emissionMultiplier)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            serialized.FindProperty("centerSwirlOverrideVanilla").boolValue = true;
            serialized.FindProperty("centerSwirlVisible").boolValue = true;
            serialized.FindProperty("centerParticlesFollowCenterPalette").boolValue = false;
            serialized.FindProperty("centerParticleTint").colorValue = tint;
            serialized.FindProperty("centerSwirlEmissiveColor").colorValue = emissive;
            serialized.FindProperty("centerParticleEmissionMultiplier").floatValue =
                emissionMultiplier;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static SpriteAsset ResolveSwirlReference(
            DimensionPortalVisualProfileAsset profile,
            string packageRoot)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(
                "centerSwirlSpriteAsset");
            Assert.That(reference, Is.Not.Null);
            Assert.That(
                DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    profile,
                    reference,
                    packageRoot,
                    out SpriteAsset resolved),
                Is.True);
            return resolved;
        }

        private static void AssertStableSwirlIdentity(
            SpriteAsset actual,
            string expectedPath,
            long expectedLow,
            long expectedHigh)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(
                NormalizePath(AssetDatabase.GetAssetPath(actual)),
                Is.EqualTo(expectedPath),
                "A color rebake must update the selected Swirls asset in place.");
            ReadAddress(actual, out long actualLow, out long actualHigh);
            Assert.That(actualLow, Is.EqualTo(expectedLow));
            Assert.That(actualHigh, Is.EqualTo(expectedHigh));
        }

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
                GetReferencePropertyName(layer));
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
                serialized.FindProperty(GetReferencePropertyName(layer)),
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
                    serialized.FindProperty(GetReferencePropertyName(layer)),
                    profile,
                    layer,
                    out asset);
            Assert.That(actual, Is.EqualTo(expected), layer.ToString());
        }

        private static string GetReferencePropertyName(DimensionPortalArtworkLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalArtworkLayer.Frame:
                    return "portalFrameSpriteAsset";
                case DimensionPortalArtworkLayer.ChargeSweep:
                    return "chargeWaveSpriteAsset";
                case DimensionPortalArtworkLayer.Milestones:
                    return "milestoneSpriteAsset";
                case DimensionPortalArtworkLayer.Center:
                    return "centerEffectSpriteAsset";
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
            }
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
                        "groundLightIntensity",
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

        private static void EnsureFolder(string folder)
        {
            string normalized = NormalizePath(folder);
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }

        private static void DeleteFixture()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void CancelPendingFixtureArtwork()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                return;
            }

            string[] profileGuids = AssetDatabase.FindAssets(
                "t:DimensionPortalVisualProfileAsset",
                new[] { TestRoot });
            for (int i = 0; i < profileGuids.Length; i++)
            {
                DimensionPortalVisualProfileAsset profile =
                    AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(
                        AssetDatabase.GUIDToAssetPath(profileGuids[i]));
                if (profile != null)
                {
                    DimensionPortalArtworkEditorUtility.CancelPending(profile);
                }
            }
        }
    }
}
#endif
