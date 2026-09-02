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
    /// The assets each test builds to work on, and taking them away afterwards.
    /// </summary>
    internal sealed partial class DimensionPortalPresetPackageEditorTests
    {
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

        /// <summary>Makes the fixture folder exist, the same way production does.</summary>
        private static void EnsureFolder(string folder)
        {
            DimensionAssetFolders.Ensure(folder);
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
