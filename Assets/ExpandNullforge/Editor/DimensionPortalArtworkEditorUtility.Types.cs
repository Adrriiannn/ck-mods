using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What the utility keeps about one layer, one texture slot and one cached answer.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        private sealed class LayerDescriptor
        {
            public DimensionPortalArtworkLayer Layer;
            public string Key;
            public string DisplayName;
            public string ReferenceProperty;
            public string FrameworkAssetPath;
            public long FrameworkAddressLow;
            public long FrameworkAddressHigh;
            public Color32[] SourcePalette;
            public string[] PaletteProperties;
        }

        /// <summary>
        /// One native texture slot exposed by a portal SpriteAsset. Frame has one static
        /// slot; ChargeSweep and Milestones have one animation slot; Center has its mature
        /// loop and opening slots. Frame count comes from the immutable framework contract;
        /// Center dimensions may use either native 16 x 25 frames or full-canvas 48 x 48
        /// frames, while Texture/EmissiveTexture reflect the selected asset.
        /// </summary>
        internal sealed class TextureSlot
        {
            public string DisplayName;
            public int AnimationIndex;
            public int FrameCount;
            public int RequiredWidth;
            public int RequiredHeight;
            public int NativeWidth;
            public int NativeHeight;
            public int AlternateWidth;
            public int AlternateHeight;
            public Texture2D Texture;
            public Texture2D EmissiveTexture;
            public Texture2D NormalTexture;

            public bool IsStatic => AnimationIndex < 0;
            public bool HasAlternateSize => AlternateWidth > 0 && AlternateHeight > 0;
        }

        private sealed class TextureReplacement
        {
            public Texture2D[] Textures;
            public Texture2D[] EmissiveTextures;
            public Texture2D[] NormalTextures;
        }

        [Serializable]
        private sealed class ManagedArtworkMetadata
        {
            public int schemaVersion;
            public string owner;
            public string profileGuid;
            public string layer;
            public string sourceAssetGuid;
            public long sourceAddressLow;
            public long sourceAddressHigh;
            public string managedAssetGuid;
            public long managedAddressLow;
            public long managedAddressHigh;
            public Color[] palette;
            public bool directTextureOverride;
        }

        private sealed class PendingBake
        {
            public DimensionTemplateAsset Template;
            public DimensionPortalVisualProfileAsset Profile;
            public DimensionPortalArtworkLayer Layer;
            public double DueTime;
            public long ExpectedAddressLow;
            public long ExpectedAddressHigh;
            public string ExpectedAssetGuid;
            public DimensionPortalArtworkReferenceKind ExpectedReferenceKind;
            public int ExpectedPaletteHash;
        }

        private sealed class ReferenceCacheEntry
        {
            public SpriteAsset Asset;
            public DimensionPortalArtworkReferenceKind Kind;
        }

        private readonly struct ReferenceCacheKey : IEquatable<ReferenceCacheKey>
        {
            public readonly int ProfileInstanceId;
            public readonly DimensionPortalArtworkLayer Layer;
            public readonly long AddressLow;
            public readonly long AddressHigh;
            public readonly string ContextDirectory;

            public ReferenceCacheKey(
                int profileInstanceId,
                DimensionPortalArtworkLayer layer,
                long addressLow,
                long addressHigh,
                string contextDirectory)
            {
                ProfileInstanceId = profileInstanceId;
                Layer = layer;
                AddressLow = addressLow;
                AddressHigh = addressHigh;
                ContextDirectory = contextDirectory ?? string.Empty;
            }

            public bool Equals(ReferenceCacheKey other)
            {
                return ProfileInstanceId == other.ProfileInstanceId &&
                       Layer == other.Layer &&
                       AddressLow == other.AddressLow &&
                       AddressHigh == other.AddressHigh &&
                       string.Equals(
                           ContextDirectory,
                           other.ContextDirectory,
                           StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is ReferenceCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ProfileInstanceId;
                    hash = hash * 397 ^ (int)Layer;
                    hash = hash * 397 ^ AddressLow.GetHashCode();
                    hash = hash * 397 ^ AddressHigh.GetHashCode();
                    hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(
                        ContextDirectory);
                    return hash;
                }
            }
        }
    }
}
