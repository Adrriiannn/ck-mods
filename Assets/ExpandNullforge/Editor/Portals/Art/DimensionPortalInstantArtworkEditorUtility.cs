using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Owns the framework-side artwork for the instant item portal (V2): a three-animation
    /// center SpriteAsset (idle, opening, closing — five 20 x 25 frames each) built from the
    /// PortalVisuals/Texture2D/Instant sheets, plus the defaults applied to a freshly created
    /// instant-portal visual profile. The SpriteAsset is created (and repaired) in editor code
    /// instead of being hand-authored so the animation contract always matches the shipped
    /// sheets; it lives inside the framework folder and ships with ExpandNullforge like every
    /// other PortalVisuals SpriteAsset.
    /// </summary>
    internal static class DimensionPortalInstantArtworkEditorUtility
    {
        internal const string InstantCenterAssetPath =
            "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCenterEffectInstant.asset";
        internal const long InstantCenterAddressLow = 7231989440551620387L;
        internal const long InstantCenterAddressHigh = -5163094287549960941L;
        internal const string InstantIdleTexturePath =
            "Assets/ExpandNullforge/PortalVisuals/Texture2D/Instant/PortalCenterEffect_idle2.png";
        internal const string InstantOpenTexturePath =
            "Assets/ExpandNullforge/PortalVisuals/Texture2D/Instant/PortalCenterEffect_open2.png";
        internal const string InstantCloseTexturePath =
            "Assets/ExpandNullforge/PortalVisuals/Texture2D/Instant/PortalCenterEffect_close2.png";
        internal const int InstantFrameCount = 5;
        internal const int InstantSheetWidth = 100;
        internal const int InstantSheetHeight = 25;
        internal const int InstantClosingAnimationIndex = 2;

        private const string SourceCenterAssetPath =
            "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCenterEffect.asset";
        private const string FrameworkModRoot = "Assets/ExpandNullforge";
        private const float CenterAnimationFps = 10.0f;

        // Stable identity for the closing animation. The idle and opening animations keep the
        // guids cloned from the vanilla center asset so their hashes stay distinct.
        private const string ClosingAnimationGuid = "c3f1a2b4-88d0-5c11-9e42-71fa30c2d9ab";

        /// <summary>
        /// Creates or repairs the framework instant-portal center SpriteAsset. Safe to call
        /// repeatedly; the asset is only written when something actually changed.
        /// </summary>
        public static bool EnsureFrameworkCenterAsset(out string message)
        {
            message = string.Empty;

            SpriteAsset source = AssetDatabase.LoadAssetAtPath<SpriteAsset>(SourceCenterAssetPath);
            if (source == null)
            {
                message = "The framework center SpriteAsset is missing at " +
                          SourceCenterAssetPath + ".";
                return false;
            }

            Texture2D idleTexture = LoadInstantTexture(InstantIdleTexturePath, ref message);
            Texture2D openTexture = LoadInstantTexture(InstantOpenTexturePath, ref message);
            Texture2D closeTexture = LoadInstantTexture(InstantCloseTexturePath, ref message);
            if (idleTexture == null || openTexture == null || closeTexture == null)
            {
                return false;
            }

            SpriteAsset instant = AssetDatabase.LoadAssetAtPath<SpriteAsset>(InstantCenterAssetPath);
            bool created = false;
            if (instant == null)
            {
                instant = UnityEngine.Object.Instantiate(source);
                instant.name = Path.GetFileNameWithoutExtension(InstantCenterAssetPath);
                AssetDatabase.CreateAsset(instant, InstantCenterAssetPath);
                created = true;
            }

            SerializedObject serialized = new SerializedObject(instant);
            serialized.Update();

            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty addressLow = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty addressHigh = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (addressLow == null || addressHigh == null)
            {
                message = "The instant center SpriteAsset does not expose an address contract.";
                return false;
            }

            addressLow.longValue = InstantCenterAddressLow;
            addressHigh.longValue = InstantCenterAddressHigh;

            SerializedProperty animations = serialized.FindProperty("m_animations");
            if (animations == null || !animations.isArray || animations.arraySize < 2)
            {
                message = "The instant center SpriteAsset does not expose the expected animation contract.";
                return false;
            }

            // Growing the array duplicates the last element (the opening), which the closing
            // configuration below then repoints at its own sheet and identity.
            while (animations.arraySize < 3)
            {
                animations.arraySize++;
            }

            while (animations.arraySize > 3)
            {
                animations.DeleteArrayElementAtIndex(animations.arraySize - 1);
            }

            string idleGuid = ReadAnimationGuid(animations, 0);
            if (!ConfigureAnimation(animations, 0, idleTexture, true, string.Empty, null, ref message) ||
                !ConfigureAnimation(animations, 1, openTexture, false, idleGuid, null, ref message) ||
                !ConfigureAnimation(animations, 2, closeTexture, false, string.Empty, ClosingAnimationGuid, ref message))
            {
                return false;
            }

            bool changed = serialized.ApplyModifiedPropertiesWithoutUndo();
            if (changed || created)
            {
                EditorUtility.SetDirty(instant);
                AssetDatabase.SaveAssetIfDirty(instant);
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                DimensionPortalArtworkEditorUtility.InvalidateSpriteAssetAddressIndex();
            }

            EnsureFrameworkManifestContains(InstantCenterAssetPath, ref message);
            if (!string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (created)
            {
                message = "Created the framework instant-portal center SpriteAsset at " +
                          InstantCenterAssetPath + ".";
            }

            return true;
        }

        /// <summary>
        /// Applies the instant-portal starting point to a freshly created profile: the frameless
        /// layer visibility and the three-animation instant center sheets. Runs after
        /// EnsureProfileInitialized so the schema migration cannot turn the layers back on. The
        /// framework instant reference set here is immediately materialized into a package-owned
        /// clone by <see cref="EnsurePackagedInstantCenter"/>, keeping the portal package
        /// self-contained.
        /// </summary>
        public static bool ApplyInstantProfileDefaults(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (profile == null)
            {
                message = "The instant portal profile is required.";
                return false;
            }

            if (!EnsureFrameworkCenterAsset(out message))
            {
                return false;
            }

            SpriteAsset instant = AssetDatabase.LoadAssetAtPath<SpriteAsset>(InstantCenterAssetPath);
            if (instant == null)
            {
                message = "The instant center SpriteAsset could not be loaded after creation.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();

            SerializedProperty centerReference = serialized.FindProperty("centerEffectSpriteAsset");
            if (centerReference == null)
            {
                message = "The portal profile no longer exposes the center artwork reference.";
                return false;
            }

            ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(centerReference, instant);
            SetBool(serialized, "frameVisible", false);
            SetBool(serialized, "chargeWaveVisible", false);
            SetBool(serialized, "milestonesVisible", false);
            SetBool(serialized, "portalShadowEnabled", false);
            // The ready burst is a charge-completion effect; an instant portal spawns fully
            // charged, and its Studio tab does not offer the Effects layer.
            SetBool(serialized, "playReadyFlash", false);

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            return EnsurePackagedInstantCenter(template, profile, out message);
        }

        /// <summary>
        /// Ensures a packaged instant profile's center is a package-owned managed clone of the
        /// three-animation instant contract. A portal package must stay self-contained (its
        /// inventory rejects shared framework references), so the framework instant asset only
        /// ever acts as the clone source. Retires the placed-contract center clone that generic
        /// package creation left behind, so the package holds exactly one center artwork.
        /// No-ops unless the center currently points at the framework instant asset, which also
        /// heals profiles created before this invariant existed.
        /// </summary>
        public static bool EnsurePackagedInstantCenter(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (template == null || profile == null)
            {
                message = "The Dimension Asset and instant portal profile are required.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty("centerEffectSpriteAsset");
            SerializedProperty address = reference == null
                ? null
                : reference.FindPropertyRelative("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                message = "The portal profile no longer exposes the center artwork reference.";
                return false;
            }

            if (low.longValue != InstantCenterAddressLow ||
                high.longValue != InstantCenterAddressHigh)
            {
                return true;
            }

            if (!DimensionPortalArtworkEditorUtility.CreateEditableCopy(
                    template,
                    profile,
                    DimensionPortalArtworkLayer.CenterInstant,
                    out message))
            {
                return false;
            }

            RetireOrphanedPlacedCenterClone(profile);
            return true;
        }

        /// <summary>
        /// Deletes the stale placed-contract center clone from the instant profile's package
        /// Artwork/Center folder. Only assets that carry Dimensions API managed-artwork
        /// metadata and are not the currently referenced center are removed.
        /// </summary>
        private static void RetireOrphanedPlacedCenterClone(
            DimensionPortalVisualProfileAsset profile)
        {
            string artworkFolder = DimensionPortalPackageEditorUtility.GetArtworkFolder(
                profile,
                DimensionPortalArtworkLayer.CenterInstant);
            if (string.IsNullOrEmpty(artworkFolder) ||
                !AssetDatabase.IsValidFolder(artworkFolder))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty("centerEffectSpriteAsset");
            SerializedProperty address = reference == null
                ? null
                : reference.FindPropertyRelative("m_address");
            long referencedLow = address == null
                ? 0L
                : address.FindPropertyRelative("m_low")?.longValue ?? 0L;
            long referencedHigh = address == null
                ? 0L
                : address.FindPropertyRelative("m_high")?.longValue ?? 0L;

            string[] guids = AssetDatabase.FindAssets(
                "t:SpriteAsset",
                new[] { artworkFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                SpriteAsset asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(assetPath);
                if (asset == null)
                {
                    continue;
                }

                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                string userData = importer == null ? string.Empty : importer.userData;
                if (string.IsNullOrEmpty(userData) ||
                    !userData.StartsWith(
                        "ExpandNullforge.PortalArtwork:",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                SerializedObject serializedAsset = new SerializedObject(asset);
                serializedAsset.Update();
                SerializedProperty assetAddress = serializedAsset.FindProperty("m_address");
                long assetLow = assetAddress == null
                    ? 0L
                    : assetAddress.FindPropertyRelative("m_low")?.longValue ?? 0L;
                long assetHigh = assetAddress == null
                    ? 0L
                    : assetAddress.FindPropertyRelative("m_high")?.longValue ?? 0L;
                if (assetLow == referencedLow && assetHigh == referencedHigh)
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(assetPath);
            }

            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            DimensionPortalArtworkEditorUtility.InvalidateSpriteAssetAddressIndex();
        }

        private static Texture2D LoadInstantTexture(string texturePath, ref string message)
        {
            if (!File.Exists(texturePath))
            {
                message = AppendMessage(
                    message,
                    "The instant portal sheet is missing at " + texturePath + ".");
                return null;
            }

            DimensionPortalArtworkEditorUtility.ConfigureSpriteTextureImporter(
                texturePath,
                InstantSheetWidth,
                InstantSheetHeight);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                message = AppendMessage(
                    message,
                    "The instant portal sheet could not be imported at " + texturePath + ".");
            }

            return texture;
        }

        private static string ReadAnimationGuid(SerializedProperty animations, int index)
        {
            if (animations == null || index < 0 || index >= animations.arraySize)
            {
                return string.Empty;
            }

            SerializedProperty guid = animations
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("m_guid");
            return guid == null ? string.Empty : guid.stringValue;
        }

        private static bool ConfigureAnimation(
            SerializedProperty animations,
            int index,
            Texture2D texture,
            bool loop,
            string exitAnimationGuid,
            string animationGuidOverride,
            ref string message)
        {
            SerializedProperty animation = animations.GetArrayElementAtIndex(index);
            SerializedProperty spriteData = animation.FindPropertyRelative("m_spriteData");
            SerializedProperty textureProperty = spriteData == null
                ? null
                : spriteData.FindPropertyRelative("texture");
            SerializedProperty emissiveProperty = spriteData == null
                ? null
                : spriteData.FindPropertyRelative("emissiveTexture");
            SerializedProperty normalProperty = spriteData == null
                ? null
                : spriteData.FindPropertyRelative("normalTexture");
            SerializedProperty frameCount = animation.FindPropertyRelative("srcFrameCount");
            SerializedProperty fps = animation.FindPropertyRelative("fps");
            SerializedProperty loopProperty = animation.FindPropertyRelative("loop");
            SerializedProperty exitGuid = animation.FindPropertyRelative("m_exitAnimationGuid");
            SerializedProperty frameData = animation.FindPropertyRelative("frameData");
            if (textureProperty == null ||
                emissiveProperty == null ||
                normalProperty == null ||
                frameCount == null ||
                fps == null ||
                loopProperty == null ||
                exitGuid == null ||
                frameData == null ||
                !frameData.isArray)
            {
                message = "Animation slot " + (index + 1) +
                          " of the instant center SpriteAsset does not expose the expected contract.";
                return false;
            }

            textureProperty.objectReferenceValue = texture;
            // The instant sheets are self-emissive like the vanilla center sheets.
            emissiveProperty.objectReferenceValue = texture;
            normalProperty.objectReferenceValue = null;
            frameCount.intValue = InstantFrameCount;
            fps.floatValue = CenterAnimationFps;
            loopProperty.boolValue = loop;
            exitGuid.stringValue = exitAnimationGuid ?? string.Empty;

            if (!string.IsNullOrEmpty(animationGuidOverride))
            {
                SerializedProperty guid = animation.FindPropertyRelative("m_guid");
                if (guid != null)
                {
                    guid.stringValue = animationGuidOverride;
                }
            }

            frameData.arraySize = InstantFrameCount;
            for (int i = 0; i < frameData.arraySize; i++)
            {
                SerializedProperty frame = frameData.GetArrayElementAtIndex(i);
                SerializedProperty holdFrames = frame.FindPropertyRelative("holdFrames");
                SerializedProperty eventMask = frame.FindPropertyRelative("eventMask");
                if (holdFrames != null)
                {
                    holdFrames.intValue = 0;
                }

                if (eventMask != null)
                {
                    eventMask.intValue = 0;
                }
            }

            return true;
        }

        private static void EnsureFrameworkManifestContains(
            string spriteAssetPath,
            ref string message)
        {
            SpriteAssetBase spriteAsset =
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(spriteAssetPath);
            if (spriteAsset == null)
            {
                message = AppendMessage(
                    message,
                    "The instant center SpriteAsset could not be loaded for manifest registration.");
                return;
            }

            string manifestPath = FrameworkModRoot + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<SpriteAssetManifest>();
                manifest.name = "SpriteAssetManifest";
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }

            if (manifest.spriteAssets == null)
            {
                manifest.spriteAssets = new List<SpriteAssetBase>();
            }

            if (!manifest.spriteAssets.Contains(spriteAsset))
            {
                manifest.spriteAssets.Add(spriteAsset);
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssetIfDirty(manifest);
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static string AppendMessage(string existing, string addition)
        {
            return string.IsNullOrEmpty(existing) ? addition : existing + " " + addition;
        }
    }
}
