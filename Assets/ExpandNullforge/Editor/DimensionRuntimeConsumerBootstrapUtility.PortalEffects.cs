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
    /// Particles, gradients, tints and the light a portal casts.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private struct PortalParticleRootSet
        {
            public GameObject Persistent;
            public GameObject ReadyFlash;
        }

        /// <summary>
        /// Creates the same configured persistent particle subtree used by generated portal
        /// prefabs, but leaves ownership and activation to an editor preview host. Keeping the
        /// preview on this construction path prevents Portal Studio from drifting into a
        /// hand-authored approximation of GatherEnergy.
        /// </summary>
        internal static GameObject CreatePortalPersistentParticlePreview(
            Transform parent,
            DimensionPortalVisualProfileAsset visualProfile)
        {
            if (parent == null ||
                (visualProfile != null &&
                 (visualProfile.CenterSwirlOverrideVanilla ||
                  !visualProfile.CenterSwirlVisible)))
            {
                return null;
            }

            GameObject template =
                AssetDatabase.LoadAssetAtPath<GameObject>(PortalVisualTemplatePath);
            Transform source = template == null
                ? null
                : FindDescendantTransform(template.transform, "GatherEnergy");
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "Could not find the vanilla portal GatherEnergy particle subtree at " +
                    PortalVisualTemplatePath + ".");
            }

            GameObject preview = CreatePortalParticleEffectRoot(
                source,
                parent,
                "PortalStudioGatherEnergy",
                visualProfile,
                false);
            if (preview == null)
            {
                return null;
            }

            // Portal Studio positions the native 48 x 48 render around the configured canvas
            // origin. Preserve runtime rotation, scale, particle modules, sprites, gradients,
            // trails, and materials while removing only the generated-prefab world offset.
            preview.transform.localPosition = Vector3.zero;
            preview.SetActive(true);
            return preview;
        }

        /// <summary>
        /// Creates the same configured ready-burst (DeathBlink) subtree used by generated portal
        /// prefabs, for an editor preview host. The authored placement, tint, emission, size and
        /// sprite overrides are all baked by the shared construction path, so the Studio replay
        /// is the runtime burst by construction.
        /// </summary>
        internal static GameObject CreatePortalReadyBurstParticlePreview(
            Transform parent,
            DimensionPortalVisualProfileAsset visualProfile)
        {
            if (parent == null ||
                (visualProfile != null && !visualProfile.PlayReadyFlash))
            {
                return null;
            }

            GameObject template =
                AssetDatabase.LoadAssetAtPath<GameObject>(PortalVisualTemplatePath);
            Transform source = template == null
                ? null
                : FindDescendantTransform(template.transform, "GatherEnergy");
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "Could not find the vanilla portal GatherEnergy particle subtree at " +
                    PortalVisualTemplatePath + ".");
            }

            GameObject preview = CreatePortalParticleEffectRoot(
                source,
                parent,
                "PortalStudioReadyBurst",
                visualProfile,
                true);
            if (preview == null)
            {
                return null;
            }

            // The shared path bakes the authored pixel offset into the generated-prefab world
            // placement. The preview keeps only that offset (the canvas supplies the anchor),
            // in the projection where screen Y is world Y + world Z.
            Vector2 offsetPixels = visualProfile == null
                ? Vector2.zero
                : visualProfile.ReadyFlashOffsetPixels;
            preview.transform.localPosition = new Vector3(
                offsetPixels.x / DimensionPortalVisualContract.PixelsPerUnit,
                offsetPixels.y / DimensionPortalVisualContract.PixelsPerUnit,
                0.0f);
            preview.SetActive(true);
            return preview;
        }

        /// <summary>
        /// The vanilla floor-shadow sprite a generated portal falls back to when the profile
        /// authors none. Exposed so the Studio previews exactly the sprite that ships.
        /// </summary>
        internal static Sprite LoadDefaultPortalShadowSprite()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(PortalShadowSpritePath);
        }

        /// <summary>
        /// Gives Portal Studio the same ParticleAdd/Lightning material selection and backing
        /// texture that the generated runtime portal receives, while keeping the temporary
        /// material instances owned by the preview renderer rather than writing assets.
        /// </summary>
        internal static void ConfigurePortalPersistentParticlePreviewMaterials(
            GameObject effectRoot,
            DimensionPortalVisualProfileAsset visualProfile,
            ICollection<Material> ownedMaterials,
            bool readyBurst = false)
        {
            if (effectRoot == null || ownedMaterials == null)
            {
                return;
            }

            Texture2D textureOverride = ResolvePortalParticleTextureOverride(
                visualProfile,
                readyBurst);
            if (textureOverride == null)
            {
                return;
            }

            Material sourceParticleMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(PortalParticleMaterialPath);
            Material sourceLightningMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(PortalLightningMaterialPath);
            Material particleMaterial = CreatePortalParticlePreviewMaterial(
                sourceParticleMaterial,
                textureOverride,
                ownedMaterials);
            Material lightningMaterial = CreatePortalParticlePreviewMaterial(
                sourceLightningMaterial,
                textureOverride,
                ownedMaterials);

            ParticleSystemRenderer[] renderers =
                effectRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    continue;
                }

                if (materials.Length > 1 && particleMaterial != null)
                {
                    for (int j = 0; j < materials.Length; j++)
                    {
                        materials[j] = particleMaterial;
                    }
                }
                else if (materials.Length == 1 && lightningMaterial != null)
                {
                    materials[0] = lightningMaterial;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material CreatePortalParticlePreviewMaterial(
            Material source,
            Texture2D textureOverride,
            ICollection<Material> ownedMaterials)
        {
            if (source == null)
            {
                return null;
            }

            Material material = new Material(source)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = textureOverride
            };
            ownedMaterials.Add(material);
            return material;
        }

        private static PortalParticleRootSet EnsurePortalCenterParticleEffects(
            Transform parent,
            DimensionPortalVisualProfileAsset visualProfile)
        {
            PortalParticleRootSet result = default(PortalParticleRootSet);
            if (parent == null)
            {
                return result;
            }

            string[] generatedNames = { "PortalCenterParticles", "PortalReadyFlash" };
            for (int i = 0; i < generatedNames.Length; i++)
            {
                Transform existing = parent.Find(generatedNames[i]);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject, true);
                }
            }

            Transform legacyCenter = parent.Find("PortalCenterEffectSO");
            Transform legacyParticles = legacyCenter == null
                ? null
                : legacyCenter.Find("GatherEnergy");
            if (legacyParticles != null)
            {
                Object.DestroyImmediate(legacyParticles.gameObject, true);
            }

            bool persistentEnabled = visualProfile == null ||
                (visualProfile.CenterSwirlVisible &&
                 !visualProfile.CenterSwirlOverrideVanilla);
            bool readyFlashEnabled = visualProfile == null ||
                visualProfile.PlayReadyFlash;
            if (!persistentEnabled && !readyFlashEnabled)
            {
                return result;
            }

            GameObject template =
                AssetDatabase.LoadAssetAtPath<GameObject>(PortalVisualTemplatePath);
            Transform source = template == null
                ? null
                : FindDescendantTransform(template.transform, "GatherEnergy");
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "Could not find the vanilla portal GatherEnergy particle subtree at " +
                    PortalVisualTemplatePath +
                    ".");
            }

            if (persistentEnabled)
            {
                result.Persistent = CreatePortalParticleEffectRoot(
                    source,
                    parent,
                    "PortalCenterParticles",
                    visualProfile,
                    false);
            }

            if (readyFlashEnabled)
            {
                result.ReadyFlash = CreatePortalParticleEffectRoot(
                    source,
                    parent,
                    "PortalReadyFlash",
                    visualProfile,
                    true);
            }

            return result;
        }

        private static GameObject CreatePortalParticleEffectRoot(
            Transform source,
            Transform parent,
            string name,
            DimensionPortalVisualProfileAsset visualProfile,
            bool readyBurst)
        {
            GameObject clone = Object.Instantiate(source.gameObject);
            clone.name = name;
            clone.transform.SetParent(parent, false);
            Vector2 offset = visualProfile != null && readyBurst
                ? visualProfile.ReadyFlashOffsetPixels
                : Vector2.zero;
            clone.transform.localPosition =
                DimensionPortalVisualContract.CenterLocalPosition +
                new Vector3(
                    offset.x / DimensionPortalVisualContract.PixelsPerUnit,
                    0.0625f + offset.y / DimensionPortalVisualContract.PixelsPerUnit,
                    -0.0625f);
            clone.transform.localRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null
                    ? 0.0f
                    : readyBurst
                        ? visualProfile.ReadyFlashRotationDegrees
                        : 0.0f);
            Vector2 scale = visualProfile != null && readyBurst
                ? visualProfile.ReadyFlashScale
                : Vector2.one;
            clone.transform.localScale = new Vector3(scale.x, scale.y, 1.0f);
            SetLayerRecursively(clone, parent.gameObject.layer);

            Color tint = ResolvePortalParticleTint(visualProfile, readyBurst);
            float emissionMultiplier = visualProfile != null && readyBurst
                ? visualProfile.ReadyFlashEmissionMultiplier
                : 1.0f;
            float sizeMultiplier = visualProfile != null && readyBurst
                ? visualProfile.ReadyFlashSizeMultiplier
                : 1.0f;
            ParticleSystem[] particleSystems =
                clone.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                bool isReadyFlash = particleSystem.gameObject.name == "DeathBlink";
                bool keep = readyBurst ? isReadyFlash : !isReadyFlash;
                ParticleSystemRenderer renderer =
                    particleSystem.GetComponent<ParticleSystemRenderer>();
                if (!keep)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ParticleSystem.MainModule excludedMain = particleSystem.main;
                    excludedMain.playOnAwake = false;
                    ParticleSystem.EmissionModule excludedEmission =
                        particleSystem.emission;
                    excludedEmission.enabled = false;
                    // GatherEnergy is the parent of DeathBlink in the vanilla prefab.
                    // Keep that parent object alive in the ready-burst clone while
                    // disabling only its persistent emitter and renderer.
                    particleSystem.gameObject.SetActive(!isReadyFlash);
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                    continue;
                }

                particleSystem.gameObject.SetActive(true);
                if (renderer != null)
                {
                    renderer.enabled = true;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                main.startSizeMultiplier *= sizeMultiplier;
                if (readyBurst)
                {
                    main.loop = false;
                }

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                    particleSystem.colorOverLifetime;
                if (colorOverLifetime.enabled)
                {
                    main.startColor = TintParticleGradient(
                        main.startColor,
                        new Color(1.0f, 1.0f, 1.0f, tint.a));
                    colorOverLifetime.color = RecolorParticleGradient(
                        colorOverLifetime.color,
                        tint);
                }
                else
                {
                    main.startColor = TintParticleGradient(main.startColor, tint);
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.rateOverTimeMultiplier *= emissionMultiplier;
                // The ready burst emits through burst counts, not a rate, so scaling only the
                // rate multiplier would leave the authored value with nothing to act on.
                if (readyBurst && emission.burstCount > 0)
                {
                    ParticleSystem.Burst[] bursts =
                        new ParticleSystem.Burst[emission.burstCount];
                    emission.GetBursts(bursts);
                    for (int burstIndex = 0; burstIndex < bursts.Length; burstIndex++)
                    {
                        ParticleSystem.MinMaxCurve count = bursts[burstIndex].count;
                        count.constantMin *= emissionMultiplier;
                        count.constantMax *= emissionMultiplier;
                        bursts[burstIndex].count = count;
                    }

                    emission.SetBursts(bursts);
                }

                ConfigurePortalParticleSprites(
                    particleSystem,
                    visualProfile,
                    readyBurst);
            }

            clone.SetActive(false);
            return clone;
        }

        private static void ConfigurePortalParticleSprites(
            ParticleSystem particleSystem,
            DimensionPortalVisualProfileAsset visualProfile,
            bool readyBurst)
        {
            if (particleSystem == null || visualProfile == null)
            {
                return;
            }

            ParticleSystem.TextureSheetAnimationModule textureSheet =
                particleSystem.textureSheetAnimation;
            if (readyBurst)
            {
                Sprite[] sprites = visualProfile.ReadyFlashSprites;
                if (sprites == null || sprites.Length == 0)
                {
                    return;
                }

                ResolveSharedPortalParticleTexture(
                    sprites,
                    "ready burst");
                ClearPortalParticleSprites(textureSheet);
                for (int i = 0; i < sprites.Length; i++)
                {
                    textureSheet.AddSprite(sprites[i]);
                }

                return;
            }

            // Persistent artwork is either the exact vanilla GatherEnergy subtree or the
            // independent full-canvas SpriteObject. Legacy Sprite/Texture fields remain
            // serialized for profile compatibility but are deliberately not reinterpreted.
            return;
        }

        private static void ClearPortalParticleSprites(
            ParticleSystem.TextureSheetAnimationModule textureSheet)
        {
            for (int i = textureSheet.spriteCount - 1; i >= 0; i--)
            {
                textureSheet.RemoveSprite(i);
            }
        }

        private static Texture2D ResolveSharedPortalParticleTexture(
            Sprite[] sprites,
            string effectLabel)
        {
            if (sprites == null || sprites.Length == 0)
            {
                return null;
            }

            Texture2D sharedTexture = null;
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null || sprite.texture == null)
                {
                    throw new System.InvalidOperationException(
                        "The custom portal " + effectLabel + " Sprite at index " + i +
                        " is missing or has no backing texture.");
                }

                if (sharedTexture == null)
                {
                    sharedTexture = sprite.texture;
                }
                else if (sprite.texture != sharedTexture)
                {
                    throw new System.InvalidOperationException(
                        "All custom portal " + effectLabel +
                        " Sprites must share one backing texture.");
                }
            }

            return sharedTexture;
        }

        private static ParticleSystem.MinMaxGradient TintParticleGradient(
            ParticleSystem.MinMaxGradient source,
            Color tint)
        {
            if (source.mode == ParticleSystemGradientMode.TwoColors)
            {
                return new ParticleSystem.MinMaxGradient(
                    source.colorMin * tint,
                    source.colorMax * tint);
            }

            if (source.mode == ParticleSystemGradientMode.Gradient)
            {
                return new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, tint));
            }

            if (source.mode == ParticleSystemGradientMode.TwoGradients)
            {
                return new ParticleSystem.MinMaxGradient(
                    TintGradient(source.gradientMin, tint),
                    TintGradient(source.gradientMax, tint));
            }

            if (source.mode == ParticleSystemGradientMode.RandomColor)
            {
                ParticleSystem.MinMaxGradient random =
                    new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, tint));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;
            }

            return new ParticleSystem.MinMaxGradient(source.color * tint);
        }

        private static Gradient TintGradient(Gradient source, Color tint)
        {
            if (source == null)
            {
                Gradient empty = new Gradient();
                empty.SetKeys(
                    new[]
                    {
                        new GradientColorKey(tint, 0.0f),
                        new GradientColorKey(tint, 1.0f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(tint.a, 0.0f),
                        new GradientAlphaKey(tint.a, 1.0f)
                    });
                return empty;
            }

            GradientColorKey[] colorKeys = source.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                Color color = colorKeys[i].color * tint;
                color.a = 1.0f;
                colorKeys[i].color = color;
            }

            GradientAlphaKey[] alphaKeys = source.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha *= tint.a;
            }

            Gradient result = new Gradient();
            result.mode = source.mode;
            result.SetKeys(colorKeys, alphaKeys);
            return result;
        }

        private static ParticleSystem.MinMaxGradient RecolorParticleGradient(
            ParticleSystem.MinMaxGradient source,
            Color color)
        {
            if (IsPreserveVanillaParticleColor(color))
            {
                return source;
            }

            if (source.mode == ParticleSystemGradientMode.TwoColors)
            {
                return new ParticleSystem.MinMaxGradient(
                    RecolorParticleColor(source.colorMin, color),
                    RecolorParticleColor(source.colorMax, color));
            }

            if (source.mode == ParticleSystemGradientMode.Gradient)
            {
                return new ParticleSystem.MinMaxGradient(
                    RecolorGradient(source.gradient, color));
            }

            if (source.mode == ParticleSystemGradientMode.TwoGradients)
            {
                return new ParticleSystem.MinMaxGradient(
                    RecolorGradient(source.gradientMin, color),
                    RecolorGradient(source.gradientMax, color));
            }

            if (source.mode == ParticleSystemGradientMode.RandomColor)
            {
                ParticleSystem.MinMaxGradient random =
                    new ParticleSystem.MinMaxGradient(
                        RecolorGradient(source.gradient, color));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;
            }

            return new ParticleSystem.MinMaxGradient(
                RecolorParticleColor(source.color, color));
        }

        private static Gradient RecolorGradient(Gradient source, Color color)
        {
            if (source == null)
            {
                Gradient empty = new Gradient();
                empty.SetKeys(
                    new[]
                    {
                        new GradientColorKey(color, 0.0f),
                        new GradientColorKey(color, 1.0f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.0f, 0.0f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    });
                return empty;
            }

            GradientColorKey[] colorKeys = source.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                colorKeys[i].color = RecolorParticleColor(
                    colorKeys[i].color,
                    color);
            }

            Gradient result = new Gradient();
            result.mode = source.mode;
            result.SetKeys(colorKeys, source.alphaKeys);
            return result;
        }

        private static Color RecolorParticleColor(Color source, Color color)
        {
            float sourceMinimum = Mathf.Min(source.r, Mathf.Min(source.g, source.b));
            float sourceBrightness = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
            if (sourceBrightness - sourceMinimum < 0.0001f)
            {
                return source;
            }

            Color result = new Color(
                color.r * sourceBrightness,
                color.g * sourceBrightness,
                color.b * sourceBrightness,
                source.a);
            return result;
        }

        private static bool IsPreserveVanillaParticleColor(Color color)
        {
            return Mathf.Abs(color.r - 1.0f) < 0.0001f &&
                Mathf.Abs(color.g - 1.0f) < 0.0001f &&
                Mathf.Abs(color.b - 1.0f) < 0.0001f &&
                Mathf.Abs(color.a - 1.0f) < 0.0001f;
        }

        private static Color ResolvePortalParticleTint(
            DimensionPortalVisualProfileAsset visualProfile,
            bool readyBurst)
        {
            if (visualProfile == null)
            {
                return Color.white;
            }

            if (!readyBurst)
            {
                // The persistent swirl owns an explicit author tint. White is the exact
                // vanilla-gradient sentinel; it is intentionally independent of Center.
                return visualProfile.CenterParticleTint;
            }

            bool followsCenter = visualProfile.ReadyFlashFollowsCenterPalette;
            if (!followsCenter)
            {
                return visualProfile.ReadyFlashTint;
            }

            // White is the generator's explicit "leave the source gradients alone"
            // sentinel. Do not approximate vanilla by recoloring it: GatherEnergy and
            // DeathBlink contain several deliberately different blue/cyan keys.
            if (PortalCenterPaletteMatchesVanilla(visualProfile))
            {
                return Color.white;
            }

            Color core = visualProfile.CenterCoreColor;
            float maximum = Mathf.Max(core.r, Mathf.Max(core.g, core.b));
            if (maximum <= 0.0001f)
            {
                return new Color(0.0f, 0.0f, 0.0f, core.a);
            }

            // Follow the center's hue and saturation without dimming the source
            // particles a second time; their authored gradients retain brightness.
            return new Color(
                Mathf.Clamp01(core.r / maximum),
                Mathf.Clamp01(core.g / maximum),
                Mathf.Clamp01(core.b / maximum),
                core.a);
        }

        private static bool PortalCenterPaletteMatchesVanilla(
            DimensionPortalVisualProfileAsset visualProfile)
        {
            return PortalColorsApproximatelyEqual(
                       visualProfile.CenterDarkColor,
                       DimensionPortalVisualProfileAsset.VanillaPaletteDark) &&
                   PortalColorsApproximatelyEqual(
                       visualProfile.CenterDeepColor,
                       DimensionPortalVisualProfileAsset.VanillaCenterPaletteDeep) &&
                   PortalColorsApproximatelyEqual(
                       visualProfile.CenterMidColor,
                       DimensionPortalVisualProfileAsset.VanillaPaletteDeep) &&
                   PortalColorsApproximatelyEqual(
                       visualProfile.CenterBrightColor,
                       DimensionPortalVisualProfileAsset.VanillaPaletteMid) &&
                   PortalColorsApproximatelyEqual(
                       visualProfile.CenterCoreColor,
                       DimensionPortalVisualProfileAsset.VanillaPaletteCore) &&
                   PortalColorsApproximatelyEqual(
                       visualProfile.CenterHighlightColor,
                       Color.white);
        }

        private static bool PortalColorsApproximatelyEqual(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.0001f &&
                   Mathf.Abs(left.g - right.g) < 0.0001f &&
                   Mathf.Abs(left.b - right.b) < 0.0001f &&
                   Mathf.Abs(left.a - right.a) < 0.0001f;
        }

        private static ManagedLight EnsurePortalManagedLight(
            GameObject root,
            DimensionPortalVisualProfileAsset visualProfile,
            bool itemPortal)
        {
            if (root == null)
            {
                return null;
            }

            Transform xScaler = EnsurePortalXScaler(root);
            Vector2 lightOffset = visualProfile == null
                ? Vector2.zero
                : visualProfile.GroundLightOffsetPixels;
            // The instant portal's visual centers on its single tile (x + 0) instead of the
            // placed portal's middle tile (x + 1); keep the light on the visual center.
            Vector3 footprintShift = itemPortal
                ? new Vector3(-1.0f, 0.0f, 0.0f)
                : Vector3.zero;
            Vector3 localPosition =
                DimensionEmittedLightBuilder.TemplateLightLocalPosition() +
                footprintShift +
                new Vector3(
                    lightOffset.x / DimensionPortalVisualContract.PixelsPerUnit,
                    0.0f,
                    lightOffset.y / DimensionPortalVisualContract.PixelsPerUnit);

            Color lightColor = visualProfile == null
                ? DimensionPortalVisualProfileAsset.VanillaGroundLightColor
                : visualProfile.GroundLightColor;
            float lightIntensity = visualProfile == null
                ? 0.65f
                : visualProfile.GroundLightIntensity;
            float lightRange = visualProfile == null
                ? 5.0f
                : visualProfile.GroundLightRange;
            float minimumLightIntensity = visualProfile == null
                ? 0.3f
                : visualProfile.GroundLightMinimumIntensity;
            float maximumLightIntensity = visualProfile == null
                ? 0.3f
                : visualProfile.GroundLightMaximumIntensity;
            bool movement = visualProfile == null || visualProfile.GroundLightMovement;
            bool castsShadows = visualProfile == null || visualProfile.GroundLightCastsShadows;
            bool lightEnabled = visualProfile == null || visualProfile.GroundLightEnabled;

            // THE BODY OF THIS METHOD MOVED, IT DID NOT CHANGE. Every line that used to stand here
            // — destroying an existing light before cloning, the clone out of the donor prefab, the
            // three ManagedLight references, the colour, range and shadow assignments, the flicker
            // range, and returning null for a light that is switched off — now lives in
            // DimensionEmittedLightBuilder.Ensure, so that a placed object authored with a light
            // gets the same subtree the portal does rather than a second implementation of it. The
            // portal's own numbers are unchanged and are still read from its visual profile here.
            return DimensionEmittedLightBuilder.Ensure(
                xScaler,
                localPosition,
                lightColor,
                lightIntensity,
                lightRange,
                castsShadows,
                minimumLightIntensity,
                maximumLightIntensity,
                movement,
                lightEnabled);
        }

        private static Texture2D ResolvePortalParticleTextureOverride(
            DimensionPortalVisualProfileAsset visualProfile,
            bool readyBurst)
        {
            if (visualProfile == null)
            {
                return null;
            }

            if (!readyBurst)
            {
                return null;
            }

            Sprite[] sprites = visualProfile.ReadyFlashSprites;
            if (sprites != null && sprites.Length > 0)
            {
                return ResolveSharedPortalParticleTexture(sprites, "ready burst");
            }

            return visualProfile.ReadyFlashTexture;
        }

        private static void EnsurePortalParticleMaterials(
            GameObject effectRoot,
            Texture2D textureOverride,
            string portalFolder,
            string materialStem)
        {
            Material sourceParticleMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(PortalParticleMaterialPath);
            Material sourceLightningMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(PortalLightningMaterialPath);
            string normalizedFolder = NormalizeAssetPath(portalFolder);
            string safeStem = string.IsNullOrEmpty(materialStem)
                ? "DimensionPortalParticles"
                : materialStem;
            Material particleMaterial = ResolvePortalParticleMaterial(
                sourceParticleMaterial,
                textureOverride,
                normalizedFolder + "/" + safeStem + "_Add.mat");
            Material lightningMaterial = ResolvePortalParticleMaterial(
                sourceLightningMaterial,
                textureOverride,
                normalizedFolder + "/" + safeStem + "_Lightning.mat");
            if (effectRoot == null)
            {
                return;
            }

            ParticleSystemRenderer[] particleRenderers =
                effectRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < particleRenderers.Length; i++)
            {
                ParticleSystemRenderer renderer = particleRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    continue;
                }

                if (materials.Length > 1 && particleMaterial != null)
                {
                    for (int j = 0; j < materials.Length; j++)
                    {
                        materials[j] = particleMaterial;
                    }
                }
                else if (materials.Length == 1 && lightningMaterial != null)
                {
                    materials[0] = lightningMaterial;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material ResolvePortalParticleMaterial(
            Material source,
            Texture2D textureOverride,
            string outputPath)
        {
            if (source == null)
            {
                return null;
            }

            string normalizedPath = NormalizeAssetPath(outputPath);
            if (textureOverride == null)
            {
                if (!string.IsNullOrEmpty(normalizedPath) &&
                    AssetDatabase.LoadAssetAtPath<Material>(normalizedPath) != null)
                {
                    AssetDatabase.DeleteAsset(normalizedPath);
                }

                return source;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(normalizedPath);
            if (material == null)
            {
                material = new Material(source)
                {
                    name = Path.GetFileNameWithoutExtension(normalizedPath)
                };
                AssetDatabase.CreateAsset(material, normalizedPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, material);
                material.name = Path.GetFileNameWithoutExtension(normalizedPath);
            }

            // Texture-only customization deliberately retains the framework material's
            // shader, blend state, render queue and every non-texture property.
            material.mainTexture = textureOverride;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
