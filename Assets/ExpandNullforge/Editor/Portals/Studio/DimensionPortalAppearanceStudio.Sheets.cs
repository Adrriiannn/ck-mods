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
    /// Reading a sprite sheet, recolouring it, and building the animation from it.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private PreviewSheet GetSourceSheet(string assetPath, int frameCount)
        {
            int safeFrameCount = Mathf.Max(1, frameCount);
            string cacheKey = assetPath + "|" + safeFrameCount;
            if (sourceSheetsByPath.TryGetValue(cacheKey, out PreviewSheet cached))
            {
                return cached;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            PreviewSheet sheet = GetSourceSheet(texture, safeFrameCount);
            if (sheet != null)
            {
                sourceSheetsByPath[cacheKey] = sheet;
            }

            return sheet;
        }

        private PreviewSheet GetSourceSheet(Texture2D texture, int frameCount)
        {
            if (texture == null)
            {
                return null;
            }

            int safeFrameCount = Mathf.Max(1, frameCount);
            long key = ((long)texture.GetInstanceID() << 32) ^ (uint)safeFrameCount;
            if (sourceSheets.TryGetValue(key, out PreviewSheet cached))
            {
                return cached;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            PreviewSheet sheet = new PreviewSheet
            {
                Texture = texture,
                Width = texture.width,
                Height = texture.height,
                FrameCount = safeFrameCount,
                OwnsTexture = false
            };
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            if (!string.IsNullOrEmpty(absolutePath) &&
                assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(absolutePath))
            {
                Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = texture.name + " (Portal Studio Preview)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (readable.LoadImage(File.ReadAllBytes(absolutePath)))
                {
                    readable.filterMode = FilterMode.Point;
                    readable.wrapMode = TextureWrapMode.Clamp;
                    sheet.Texture = readable;
                    sheet.Width = readable.width;
                    sheet.Height = readable.height;
                    sheet.Pixels = readable.GetPixels32();
                    sheet.OwnsTexture = true;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                }
            }

            sourceSheets.Add(key, sheet);
            return sheet;
        }

        private PreviewSheet GetFrameCompositeSheet(
            Texture2D albedoTexture,
            Texture2D emissiveTexture,
            Color emissiveColor)
        {
            return GetFrameCompositeSheet(
                albedoTexture,
                emissiveTexture,
                emissiveColor,
                1);
        }

        private PreviewSheet GetFrameCompositeSheet(
            Texture2D albedoTexture,
            Texture2D emissiveTexture,
            Color emissiveColor,
            int frameCount)
        {
            return GetFrameCompositeSheet(
                albedoTexture,
                emissiveTexture,
                Color.white,
                emissiveColor,
                frameCount);
        }

        private PreviewSheet GetFrameCompositeSheet(
            Texture2D albedoTexture,
            Texture2D emissiveTexture,
            Color albedoTint,
            Color emissiveColor,
            int frameCount)
        {
            int safeFrameCount = Mathf.Max(1, frameCount);
            PreviewSheet albedo = GetSourceSheet(albedoTexture, safeFrameCount);
            PreviewSheet emissive = GetSourceSheet(emissiveTexture, safeFrameCount);
            if (albedo == null ||
                emissive == null ||
                albedo.Pixels == null ||
                emissive.Pixels == null ||
                albedo.Width != emissive.Width ||
                albedo.Height != emissive.Height)
            {
                return albedo;
            }

            long key = ((long)albedoTexture.GetInstanceID() << 32) ^
                       (uint)emissiveTexture.GetInstanceID() ^
                       ((long)safeFrameCount << 17);
            if (!frameCompositeSheets.TryGetValue(
                    key,
                    out FrameCompositeCacheEntry cached) ||
                cached == null ||
                cached.Albedo != albedo ||
                cached.Emissive != emissive ||
                cached.Composite == null ||
                cached.Composite.Pixels == null ||
                cached.Composite.Pixels.Length != albedo.Pixels.Length)
            {
                if (cached != null)
                {
                    DestroyOwnedSheet(cached.Composite);
                }

                Texture2D compositeTexture = new Texture2D(
                    albedo.Width,
                    albedo.Height,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = albedoTexture.name + " (Portal Studio Lit Preview)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                cached = new FrameCompositeCacheEntry
                {
                    Albedo = albedo,
                    Emissive = emissive,
                    Composite = new PreviewSheet
                    {
                        Texture = compositeTexture,
                        Pixels = new Color32[albedo.Pixels.Length],
                        Width = albedo.Width,
                        Height = albedo.Height,
                        FrameCount = safeFrameCount,
                        OwnsTexture = true
                    },
                    CompositeHash = int.MinValue
                };
                frameCompositeSheets[key] = cached;
            }

            int compositeHash;
            unchecked
            {
                compositeHash = emissiveColor.GetHashCode();
                compositeHash = compositeHash * 397 ^ albedoTint.GetHashCode();
            }
            if (cached.CompositeHash == compositeHash)
            {
                return cached.Composite;
            }

            Color32[] output = cached.Composite.Pixels;
            for (int i = 0; i < output.Length; i++)
            {
                Color sourceColor = albedo.Pixels[i];
                Color baseColor = new Color(
                    sourceColor.r * Mathf.Max(0f, albedoTint.r),
                    sourceColor.g * Mathf.Max(0f, albedoTint.g),
                    sourceColor.b * Mathf.Max(0f, albedoTint.b),
                    sourceColor.a * Mathf.Clamp01(albedoTint.a));
                Color mask = emissive.Pixels[i];
                Color previewEmission = new Color(
                    mask.r * emissiveColor.r,
                    mask.g * emissiveColor.g,
                    mask.b * emissiveColor.b,
                    1f);
                Color composited = ApplyPreviewEmission(baseColor, previewEmission);
                composited.a = baseColor.a;
                output[i] = composited;
            }

            cached.Composite.Texture.SetPixels32(output);
            cached.Composite.Texture.Apply(false, false);
            cached.CompositeHash = compositeHash;
            return cached.Composite;
        }

        private PreviewSheet GetRecoloredSheet(
            string cacheKey,
            PreviewSheet source,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            return GetRecoloredSheet(
                cacheKey,
                source,
                sourcePalette,
                targetPalette,
                Color.clear);
        }

        private PreviewSheet GetRecoloredSheet(
            string cacheKey,
            PreviewSheet source,
            Color32[] sourcePalette,
            Color[] targetPalette,
            Color emissivePreviewColor)
        {
            if (source == null || source.Pixels == null || sourcePalette == null ||
                targetPalette == null || sourcePalette.Length != targetPalette.Length)
            {
                return source;
            }

            int paletteHash = GetPaletteHash(targetPalette);
            unchecked
            {
                paletteHash = paletteHash * 397 ^ emissivePreviewColor.GetHashCode();
            }
            if (recoloredSheets.TryGetValue(cacheKey, out RecolorCacheEntry cached) &&
                cached != null &&
                cached.Source == source &&
                cached.SourcePalette == sourcePalette &&
                cached.PaletteHash == paletteHash &&
                cached.Recolored != null)
            {
                return cached.Recolored;
            }

            bool canReuse = cached != null &&
                            cached.Source == source &&
                            cached.SourcePalette == sourcePalette &&
                            cached.SourcePaletteIndices != null &&
                            cached.SourcePaletteIndices.Length == source.Pixels.Length &&
                            cached.Recolored != null &&
                            cached.Recolored.Texture != null &&
                            cached.Recolored.Pixels != null &&
                            cached.Recolored.Pixels.Length == source.Pixels.Length;
            if (!canReuse && cached != null)
            {
                DestroyOwnedSheet(cached.Recolored);
            }

            if (!canReuse)
            {
                cached = CreateRecolorCacheEntry(cacheKey, source, sourcePalette);
                recoloredSheets[cacheKey] = cached;
            }

            Color32[] recoloredPixels = cached.Recolored.Pixels;
            for (int i = 0; i < source.Pixels.Length; i++)
            {
                Color32 sourcePixel = source.Pixels[i];
                int paletteIndex = cached.SourcePaletteIndices[i];
                if (paletteIndex == byte.MaxValue)
                {
                    recoloredPixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Color targetColor = ApplyPreviewEmission(
                    targetPalette[paletteIndex],
                    emissivePreviewColor);
                Color32 targetPixel = targetColor;
                targetPixel.a = (byte)Mathf.Clamp(
                    Mathf.RoundToInt(sourcePixel.a * Mathf.Clamp01(targetColor.a)),
                    0,
                    255);
                recoloredPixels[i] = targetPixel;
            }

            cached.Recolored.Texture.SetPixels32(recoloredPixels);
            cached.Recolored.Texture.Apply(false, false);
            cached.PaletteHash = paletteHash;
            return cached.Recolored;
        }

        private static Color ApplyPreviewEmission(Color baseColor, Color emissiveColor)
        {
            // The Studio cannot reproduce Core Keeper's HDR bloom, but it should show
            // the large vanilla distinction between the subtle charging emission and
            // the much stronger persistent load-point emission. This bounded screen
            // response leaves zero-emission pixels untouched and never clips to white.
            return new Color(
                1f - (1f - Mathf.Clamp01(baseColor.r)) *
                Mathf.Exp(-Mathf.Max(0f, emissiveColor.r) * PortalPreviewEmissionExposure),
                1f - (1f - Mathf.Clamp01(baseColor.g)) *
                Mathf.Exp(-Mathf.Max(0f, emissiveColor.g) * PortalPreviewEmissionExposure),
                1f - (1f - Mathf.Clamp01(baseColor.b)) *
                Mathf.Exp(-Mathf.Max(0f, emissiveColor.b) * PortalPreviewEmissionExposure),
                baseColor.a);
        }

        private static RecolorCacheEntry CreateRecolorCacheEntry(
            string cacheKey,
            PreviewSheet source,
            Color32[] sourcePalette)
        {
            int pixelCount = source.Pixels.Length;
            Color32[] recoloredPixels = new Color32[pixelCount];
            byte[] paletteIndices = new byte[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                Color32 sourcePixel = source.Pixels[i];
                paletteIndices[i] = sourcePixel.a == 0
                    ? byte.MaxValue
                    : (byte)FindClosestPaletteIndex(sourcePixel, sourcePalette);
            }

            Texture2D recoloredTexture = new Texture2D(
                source.Width,
                source.Height,
                TextureFormat.RGBA32,
                false)
            {
                name = cacheKey + " (Portal Studio Palette)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            PreviewSheet recolored = new PreviewSheet
            {
                Texture = recoloredTexture,
                Pixels = recoloredPixels,
                Width = source.Width,
                Height = source.Height,
                FrameCount = source.FrameCount,
                OwnsTexture = true
            };
            return new RecolorCacheEntry
            {
                Source = source,
                SourcePalette = sourcePalette,
                Recolored = recolored,
                SourcePaletteIndices = paletteIndices,
                PaletteHash = int.MinValue
            };
        }

        private AnimationSheet GetAnimationSheet(
            SpriteAsset asset,
            int animationIndex,
            int fallbackFrames,
            float fallbackFps)
        {
            if (asset == null || asset.animationCount <= 0)
            {
                return default(AnimationSheet);
            }

            if (animationIndex < 0 || animationIndex >= asset.animationCount)
            {
                AddPreviewError(
                    asset.name + " does not contain animation index " + animationIndex +
                    " (available: 0-" + (asset.animationCount - 1) + ").");
                return default(AnimationSheet);
            }

            FrameAnimation animation = asset.GetAnimationAt(animationIndex);
            if (animation == null || animation.spriteData == null)
            {
                AddPreviewError(asset.name + " animation " + animationIndex + " has no SpriteData.");
                return default(AnimationSheet);
            }

            int frameCount = Mathf.Max(1, animation.srcFrameCount > 0
                ? animation.srcFrameCount
                : fallbackFrames);
            Texture2D sourceTexture = animation.spriteData.GetSrcTexture();
            if (sourceTexture == null)
            {
                AddPreviewError(asset.name + " animation " + animationIndex + " has no source texture.");
                return default(AnimationSheet);
            }

            float fps = animation.fps > 0f ? animation.fps : fallbackFps;
            Vector2 pivot = GetEffectiveAnimationPivot(asset, animationIndex, animation);
            int signature = GetAnimationSignature(
                animation,
                sourceTexture,
                frameCount,
                fps,
                pivot);
            AnimationCacheKey cacheKey = new AnimationCacheKey
            {
                AssetInstanceId = asset.GetInstanceID(),
                AnimationIndex = animationIndex,
                FallbackFrames = fallbackFrames,
                FallbackFps = fallbackFps
            };
            if (animationSheets.TryGetValue(cacheKey, out AnimationCacheEntry cached) &&
                cached != null &&
                ReferenceEquals(cached.Animation, animation) &&
                cached.Signature == signature &&
                cached.Sheet.Sheet != null)
            {
                return cached.Sheet;
            }

            AnimationSheet sheet = new AnimationSheet
            {
                Sheet = GetSourceSheet(sourceTexture, frameCount),
                FrameCount = frameCount,
                Fps = fps,
                Loop = animation.loop,
                Pivot = pivot,
                Animation = animation,
                RuntimeFrameRemap = BuildRuntimeFrameRemap(animation, frameCount),
                SourceLabel = asset.name + " / animation " + animationIndex
            };
            animationSheets[cacheKey] = new AnimationCacheEntry
            {
                Animation = animation,
                Signature = signature,
                Sheet = sheet
            };
            return sheet;
        }

        private static int GetAnimationSignature(
            FrameAnimation animation,
            Texture2D sourceTexture,
            int frameCount,
            float fps,
            Vector2 pivot)
        {
            unchecked
            {
                int hash = sourceTexture == null ? 0 : sourceTexture.GetInstanceID();
                hash = hash * 397 ^ frameCount;
                hash = hash * 397 ^ fps.GetHashCode();
                hash = hash * 397 ^ (animation != null && animation.loop ? 1 : 0);
                hash = hash * 397 ^ pivot.GetHashCode();
                if (animation == null)
                {
                    return hash;
                }

                if (animation.runtimeFrameRemap != null &&
                    animation.runtimeFrameRemap.Length > 0)
                {
                    hash = hash * 397 ^ animation.runtimeFrameRemap.Length;
                    for (int i = 0; i < animation.runtimeFrameRemap.Length; i++)
                    {
                        hash = hash * 397 ^ animation.runtimeFrameRemap[i];
                    }

                    return hash;
                }

                int frameDataCount = animation.frameData == null
                    ? 0
                    : animation.frameData.Count;
                hash = hash * 397 ^ frameDataCount;
                for (int i = 0; i < frameDataCount; i++)
                {
                    hash = hash * 397 ^ animation.frameData[i].holdFrames;
                }

                return hash;
            }
        }

        private static int[] BuildRuntimeFrameRemap(FrameAnimation animation, int frameCount)
        {
            if (animation != null &&
                animation.runtimeFrameRemap != null &&
                animation.runtimeFrameRemap.Length > 0)
            {
                return animation.runtimeFrameRemap;
            }

            int sourceFrameCount = Mathf.Max(1, frameCount);
            bool hasHeldFrames = false;
            int runtimeFrameCount = sourceFrameCount;
            for (int sourceFrame = 0; sourceFrame < sourceFrameCount; sourceFrame++)
            {
                int holdFrames = animation != null &&
                                 animation.frameData != null &&
                                 sourceFrame < animation.frameData.Count
                    ? Mathf.Max(0, animation.frameData[sourceFrame].holdFrames)
                    : 0;
                hasHeldFrames |= holdFrames > 0;
                runtimeFrameCount += holdFrames;
            }

            if (!hasHeldFrames)
            {
                return null;
            }

            List<int> remap = new List<int>(runtimeFrameCount);
            for (int sourceFrame = 0; sourceFrame < sourceFrameCount; sourceFrame++)
            {
                int holdFrames = animation != null &&
                                 animation.frameData != null &&
                                 sourceFrame < animation.frameData.Count
                    ? Mathf.Max(0, animation.frameData[sourceFrame].holdFrames)
                    : 0;
                for (int repeat = 0; repeat <= holdFrames; repeat++)
                {
                    remap.Add(sourceFrame);
                }
            }

            return remap.ToArray();
        }

        private static Vector2 GetEffectiveAnimationPivot(
            SpriteAsset asset,
            int animationIndex,
            FrameAnimation animation)
        {
            if (animation == null || animation.spriteData == null)
            {
                return new Vector2(0.5f, 0.5f);
            }

            SpriteData data = animation.spriteData;
            if (!data.inheritPivot)
            {
                return data.pivot;
            }

            if (asset.staticSpriteData != null && asset.staticSpriteData.hasAnyTexture)
            {
                return asset.staticSpriteData.pivot;
            }

            for (int i = 0; i < Mathf.Min(animationIndex, asset.animationCount); i++)
            {
                FrameAnimation previous = asset.GetAnimationAt(i);
                if (previous != null && previous.hasAnyTexture && previous.spriteData != null)
                {
                    return previous.spriteData.pivot;
                }
            }

            return data.pivot;
        }

        private static void DestroyOwnedSheet(PreviewSheet sheet)
        {
            if (sheet != null && sheet.OwnsTexture && sheet.Texture != null)
            {
                UnityEngine.Object.DestroyImmediate(sheet.Texture);
                sheet.Texture = null;
            }
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
