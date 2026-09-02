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
    /// The small records the studio keeps its work in: layers, phases, cached sheets, frames.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        internal struct DrawResult
        {
            public bool Changed;
            public bool TemplateChanged;
            public bool CanApply;
            public bool RuntimeSyncRequested;
            public DimensionPortalVisualProfileAsset UseProfileRequested;
            public DimensionPortalVisualProfileAsset EditingProfile;
            public string Message;
            public MessageType MessageType;
        }

        internal enum StudioLayer
        {
            Frame,
            ChargeSweep,
            Milestones,
            Center,
            InnerFlecks,
            ReadyBurst,
            GroundLight
        }

        private enum PreviewPhase
        {
            Charging,
            Activated
        }

        private enum StudioLayoutMode
        {
            Compact,
            Standard,
            Expanded
        }

        private enum PendingPresetAction
        {
            None,
            Save,
            Duplicate,
            Create,
            Browse,
            Use,

            /// <summary>
            /// Open a profile for editing AND make it the one the mod builds with, in one motion.
            /// The page's profile picker offers no separate "use" step: choosing a look means
            /// using it, so the two must never come apart.
            /// </summary>
            Switch
        }

        private struct AssetIdentity
        {
            public string Guid;
            public long LocalFileId;
            public int InstanceId;

            public bool HasPersistentIdentity
            {
                get { return !string.IsNullOrEmpty(Guid); }
            }
        }

        private struct ColorRole
        {
            public readonly string PropertyName;
            public readonly string Label;
            public readonly string ShortLabel;
            public readonly bool IsHdr;
            public readonly bool IsPaletteColor;

            public ColorRole(
                string propertyName,
                string label,
                string shortLabel,
                bool isHdr,
                bool isPaletteColor)
            {
                PropertyName = propertyName;
                Label = label;
                ShortLabel = shortLabel;
                IsHdr = isHdr;
                IsPaletteColor = isPaletteColor;
            }
        }

        private struct ColorPickerKey : IEquatable<ColorPickerKey>
        {
            public int TargetInstanceId;
            public string PropertyPath;

            public bool Equals(ColorPickerKey other)
            {
                return TargetInstanceId == other.TargetInstanceId &&
                       string.Equals(PropertyPath, other.PropertyPath, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ColorPickerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return TargetInstanceId * 397 ^
                           StringComparer.Ordinal.GetHashCode(PropertyPath ?? string.Empty);
                }
            }
        }

        private sealed class PreviewSheet
        {
            public Texture2D Texture;
            public Color32[] Pixels;
            public int Width;
            public int Height;
            public int FrameCount;
            public bool OwnsTexture;

            public int FrameWidth
            {
                get { return FrameCount <= 0 ? 0 : Width / FrameCount; }
            }

            public bool TryGetPixel(int frameIndex, int x, int y, out Color32 color)
            {
                color = default(Color32);
                int frameWidth = FrameWidth;
                if (Pixels == null ||
                    frameWidth <= 0 ||
                    x < 0 ||
                    x >= frameWidth ||
                    y < 0 ||
                    y >= Height)
                {
                    return false;
                }

                int safeFrame = Mathf.Clamp(frameIndex, 0, FrameCount - 1);
                int pixelIndex = y * Width + safeFrame * frameWidth + x;
                if (pixelIndex < 0 || pixelIndex >= Pixels.Length)
                {
                    return false;
                }

                color = Pixels[pixelIndex];
                return true;
            }
        }

        private sealed class RecolorCacheEntry
        {
            public PreviewSheet Source;
            public Color32[] SourcePalette;
            public PreviewSheet Recolored;
            public byte[] SourcePaletteIndices;
            public int PaletteHash;
        }

        private sealed class FrameCompositeCacheEntry
        {
            public PreviewSheet Albedo;
            public PreviewSheet Emissive;
            public PreviewSheet Composite;
            public int CompositeHash;
        }

        private struct AnimationCacheKey : IEquatable<AnimationCacheKey>
        {
            public int AssetInstanceId;
            public int AnimationIndex;
            public int FallbackFrames;
            public float FallbackFps;

            public bool Equals(AnimationCacheKey other)
            {
                return AssetInstanceId == other.AssetInstanceId &&
                       AnimationIndex == other.AnimationIndex &&
                       FallbackFrames == other.FallbackFrames &&
                       FallbackFps.Equals(other.FallbackFps);
            }

            public override bool Equals(object obj)
            {
                return obj is AnimationCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = AssetInstanceId;
                    hash = hash * 397 ^ AnimationIndex;
                    hash = hash * 397 ^ FallbackFrames;
                    hash = hash * 397 ^ FallbackFps.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class AnimationCacheEntry
        {
            public FrameAnimation Animation;
            public int Signature;
            public AnimationSheet Sheet;
        }

        private struct TextureSlotCacheKey : IEquatable<TextureSlotCacheKey>
        {
            public int ProfileInstanceId;
            public DimensionPortalArtworkLayer Layer;
            public long AddressLow;
            public long AddressHigh;
            public int ResolvedAssetInstanceId;
            public int ResolvedAssetDirtyCount;

            public bool Equals(TextureSlotCacheKey other)
            {
                return ProfileInstanceId == other.ProfileInstanceId &&
                       Layer == other.Layer &&
                       AddressLow == other.AddressLow &&
                       AddressHigh == other.AddressHigh &&
                       ResolvedAssetInstanceId == other.ResolvedAssetInstanceId &&
                       ResolvedAssetDirtyCount == other.ResolvedAssetDirtyCount;
            }

            public override bool Equals(object obj)
            {
                return obj is TextureSlotCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ProfileInstanceId;
                    hash = hash * 397 ^ (int)Layer;
                    hash = hash * 397 ^ AddressLow.GetHashCode();
                    hash = hash * 397 ^ AddressHigh.GetHashCode();
                    hash = hash * 397 ^ ResolvedAssetInstanceId;
                    hash = hash * 397 ^ ResolvedAssetDirtyCount;
                    return hash;
                }
            }
        }

        private sealed class TextureSlotCacheEntry
        {
            public DimensionPortalArtworkEditorUtility.TextureSlot[] Slots;
            public string Message;
            public bool UsesDirectTextureOverride;
        }

        private sealed class PendingTextureUpdate
        {
            public DimensionTemplateAsset Template;
            public DimensionPortalVisualProfileAsset Profile;
            public DimensionPortalArtworkLayer Layer;
            public Texture2D[] Textures;
            public Texture2D[] EmissiveTextures;
            public Texture2D[] NormalTextures;
            public long ExpectedAddressLow;
            public long ExpectedAddressHigh;
            public string ExpectedAssetGuid;
            public DimensionPortalArtworkReferenceKind ExpectedReferenceKind;
            public AssetIdentity ExpectedTemplateIdentity;
            public AssetIdentity ExpectedProfileIdentity;
        }

        private sealed class PendingSwirlTextureUpdate
        {
            public DimensionTemplateAsset Template;
            public DimensionPortalVisualProfileAsset Profile;
            public Texture2D ColorTexture;
            public Texture2D EmissiveTexture;
            public Texture2D NormalTexture;
            public long ExpectedAddressLow;
            public long ExpectedAddressHigh;
            public AssetIdentity ExpectedTemplateIdentity;
            public AssetIdentity ExpectedProfileIdentity;
        }

        private sealed class SwirlTextureSlotCacheEntry
        {
            public int ProfileInstanceId;
            public long AddressLow;
            public long AddressHigh;
            public DimensionPortalSwirlArtworkEditorUtility.AnimationZeroTextureSlot Slot;
            public string Message;
        }

        private struct VisibleFrame
        {
            public StudioLayer Layer;
            public PreviewSheet DisplaySheet;
            public PreviewSheet HitSheet;
            public Rect CanvasRect;
            public int FrameIndex;
            public Color Tint;
            public Color32[] SourcePalette;
            public bool PalettePickingEnabled;
            public Vector2 Pivot;
            public Vector2 Scale;
            public float RotationDegrees;
            public bool FlipX;
            public bool FlipY;
            public string SourceLabel;
            public bool DirectOverride;
        }

        private struct FleckPreviewState
        {
            public bool Visible;
            public Vector2 Origin;
            public Vector2 Scale;
            public float EmissionMultiplier;
            public float SizeMultiplier;
            public float RadiusMultiplier;
        }

        private struct AnimationSheet
        {
            public PreviewSheet Sheet;
            public int FrameCount;
            public float Fps;
            public bool Loop;
            public Vector2 Pivot;
            public FrameAnimation Animation;
            public int[] RuntimeFrameRemap;
            public string SourceLabel;
        }
    }
}
