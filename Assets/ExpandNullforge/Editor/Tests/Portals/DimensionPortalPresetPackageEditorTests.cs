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
    internal sealed partial class DimensionPortalPresetPackageEditorTests
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
    }
}
#endif
