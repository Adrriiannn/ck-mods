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
    /// The texture slots a layer draws from, and how they are shown.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private void DrawTexturePanel(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkReferenceKind referenceKind,
            SpriteAsset resolvedAsset,
            long referenceLow,
            long referenceHigh,
            float targetHeight,
            float fixedWidth)
        {
            bool expandHeight = targetHeight > 0f;
            if (expandHeight && fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.Width(fixedWidth),
                    GUILayout.Height(targetHeight));
            }
            else if (expandHeight)
            {
                EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.Height(targetHeight));
            }
            else if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
            }

            TextureSlotCacheEntry cacheEntry = GetTextureSlotCacheEntry(
                profile,
                layer,
                referenceKind,
                resolvedAsset,
                referenceLow,
                referenceHigh);
            DimensionPortalArtworkEditorUtility.TextureSlot[] slots =
                cacheEntry.Slots ??
                Array.Empty<DimensionPortalArtworkEditorUtility.TextureSlot>();
            int expectedSlotCount = GetExpectedTextureSlotCount(layer);
            bool slotsReady = slots.Length == expectedSlotCount;
            for (int i = 0; slotsReady && i < expectedSlotCount; i++)
            {
                slotsReady = slots[i] != null;
            }

            string unavailableMessage = string.IsNullOrEmpty(cacheEntry.Message)
                ? "The selected SpriteAsset does not expose compatible texture slots."
                : cacheEntry.Message;
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);

            Texture2D[] displayedTextures = GetDisplayedTextureArray(
                profile,
                layer,
                slots,
                0,
                expectedSlotCount);
            Texture2D[] displayedEmissive = GetDisplayedTextureArray(
                profile,
                layer,
                slots,
                1,
                expectedSlotCount);
            Texture2D[] displayedNormals = GetDisplayedTextureArray(
                profile,
                layer,
                slots,
                2,
                expectedSlotCount);
            bool changed = false;
            EditorGUI.BeginDisabledGroup(!slotsReady);
            // One field per animation, labeled with the animation's name ("Loop", "Opening",
            // "Closing", or the layer's own name for single-sheet layers). It edits the COLOR
            // sheet — the artwork itself. Emissive (the self-glow sheet, cloned from the same
            // art in managed assets) and normal maps are intentionally not exposed; existing
            // values pass through QueueTextureUpdate untouched.
            for (int i = 0; i < expectedSlotCount; i++)
            {
                DimensionPortalArtworkEditorUtility.TextureSlot slot =
                    i < slots.Length ? slots[i] : null;
                if (slot == null)
                {
                    DrawTextureSlotPlaceholder(layer, i, unavailableMessage);
                    continue;
                }

                Rect textureRect = EditorGUILayout.GetControlRect(
                    false,
                    EditorGUIUtility.singleLineHeight);
                Texture2D texture = EditorGUI.ObjectField(
                    textureRect,
                    new GUIContent(
                        slot.DisplayName ?? "Texture",
                        GetTextureFieldTooltip(slot, "Color")),
                    displayedTextures[i],
                    typeof(Texture2D),
                    false) as Texture2D;
                if (texture != displayedTextures[i])
                {
                    displayedTextures[i] = texture;
                    changed = true;
                }
            }
            EditorGUI.EndDisabledGroup();

            if (changed && slotsReady)
            {
                QueueTextureUpdate(
                    template,
                    profile,
                    layer,
                    displayedTextures,
                    displayedEmissive,
                    displayedNormals);
            }

            if (expandHeight)
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSwirlTexturePanel(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            bool customMode,
            float targetHeight,
            float fixedWidth)
        {
            bool expandHeight = targetHeight > 0f;
            if (expandHeight && fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.Width(fixedWidth),
                    GUILayout.Height(targetHeight));
            }
            else if (expandHeight)
            {
                EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.Height(targetHeight));
            }
            else if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
            }

            SwirlTextureSlotCacheEntry cacheEntry =
                GetSwirlTextureSlotCacheEntry(profile);
            DimensionPortalSwirlArtworkEditorUtility.AnimationZeroTextureSlot slot =
                cacheEntry == null ? null : cacheEntry.Slot;
            string unavailableMessage = cacheEntry == null ||
                                        string.IsNullOrEmpty(cacheEntry.Message)
                ? "The selected Swirls SpriteAsset does not expose animation 0 textures."
                : cacheEntry.Message;

            Texture2D colorTexture = slot == null ? null : slot.ColorTexture;
            Texture2D emissiveTexture = slot == null ? null : slot.EmissiveTexture;
            Texture2D normalTexture = slot == null ? null : slot.NormalTexture;
            if (pendingSwirlTextureUpdate != null &&
                pendingSwirlTextureUpdate.Profile == profile)
            {
                colorTexture = pendingSwirlTextureUpdate.ColorTexture;
                emissiveTexture = pendingSwirlTextureUpdate.EmissiveTexture;
                normalTexture = pendingSwirlTextureUpdate.NormalTexture;
            }

            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
            bool fieldsEnabled = customMode && slot != null;
            string modeTooltip = customMode
                ? unavailableMessage
                : "Enable Override vanilla to use and edit this custom animation.";
            EditorGUI.BeginDisabledGroup(!fieldsEnabled);
            EditorGUI.BeginChangeCheck();
            // Draw the texture slot with an explicit single-line rect so Unity renders the compact
            // one-line object field (small circle picker), matching the other artwork panels —
            // rather than the large checkerboard Texture2D thumbnail the layout overload reserves.
            Rect swirlColorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Texture2D selectedColor = EditorGUI.ObjectField(
                swirlColorRect,
                new GUIContent(
                    "Loop",
                    fieldsEnabled
                        ? "Sheet for the custom Swirls loop animation."
                        : modeTooltip),
                colorTexture,
                typeof(Texture2D),
                false) as Texture2D;
            // Emissive (self-glow) and normal maps are intentionally not exposed; the existing
            // values pass through QueueSwirlTextureUpdate untouched.
            Texture2D selectedEmissive = emissiveTexture;
            Texture2D selectedNormal = normalTexture;
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.EndDisabledGroup();

            if (changed && fieldsEnabled)
            {
                if (selectedColor == null)
                {
                    pendingArtworkMessage =
                        "Custom Swirls require a Color texture for animation 0.";
                    pendingArtworkMessageType = MessageType.Error;
                    repaintRequested = true;
                }
                else
                {
                    QueueSwirlTextureUpdate(
                        template,
                        profile,
                        selectedColor,
                        selectedEmissive,
                        selectedNormal);
                }
            }

            if (expandHeight)
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndVertical();
        }

        private SwirlTextureSlotCacheEntry GetSwirlTextureSlotCacheEntry(
            DimensionPortalVisualProfileAsset profile)
        {
            long low = 0L;
            long high = 0L;
            if (profile != null)
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.Update();
                TryReadReferenceAddress(
                    serialized.FindProperty(
                        DimensionPortalSwirlArtworkEditorUtility.ReferencePropertyName),
                    out low,
                    out high);
            }

            int profileInstanceId = profile == null ? 0 : profile.GetInstanceID();
            if (swirlTextureSlotCache != null &&
                swirlTextureSlotCache.ProfileInstanceId == profileInstanceId &&
                swirlTextureSlotCache.AddressLow == low &&
                swirlTextureSlotCache.AddressHigh == high)
            {
                return swirlTextureSlotCache;
            }

            SwirlTextureSlotCacheEntry entry = new SwirlTextureSlotCacheEntry
            {
                ProfileInstanceId = profileInstanceId,
                AddressLow = low,
                AddressHigh = high,
                Slot = null,
                Message = string.Empty
            };
            if (profile != null &&
                !DimensionPortalSwirlArtworkEditorUtility.TryGetAnimationZeroTextureSlot(
                    profile,
                    out entry.Slot,
                    out entry.Message))
            {
                entry.Slot = null;
            }

            swirlTextureSlotCache = entry;
            return entry;
        }

        private TextureSlotCacheEntry GetTextureSlotCacheEntry(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkReferenceKind referenceKind,
            SpriteAsset resolvedAsset,
            long referenceLow,
            long referenceHigh)
        {
            TextureSlotCacheKey key = new TextureSlotCacheKey
            {
                ProfileInstanceId = profile == null ? 0 : profile.GetInstanceID(),
                Layer = layer,
                AddressLow = referenceLow,
                AddressHigh = referenceHigh,
                ResolvedAssetInstanceId = resolvedAsset == null
                    ? 0
                    : resolvedAsset.GetInstanceID(),
                ResolvedAssetDirtyCount = resolvedAsset == null
                    ? 0
                    : EditorUtility.GetDirtyCount(resolvedAsset)
            };
            if (textureSlotCache.TryGetValue(key, out TextureSlotCacheEntry cached))
            {
                return cached;
            }

            TextureSlotCacheEntry entry = new TextureSlotCacheEntry
            {
                Slots = Array.Empty<DimensionPortalArtworkEditorUtility.TextureSlot>(),
                Message = string.Empty,
                UsesDirectTextureOverride = false
            };
            if (profile != null &&
                !DimensionPortalArtworkEditorUtility.TryGetTextureSlots(
                    profile,
                    layer,
                    out entry.Slots,
                    out entry.Message))
            {
                entry.Slots = Array.Empty<DimensionPortalArtworkEditorUtility.TextureSlot>();
            }

            if (profile != null &&
                referenceKind == DimensionPortalArtworkReferenceKind.Managed)
            {
                entry.UsesDirectTextureOverride =
                    DimensionPortalArtworkEditorUtility.UsesDirectTextureOverride(
                        profile,
                        layer);
            }

            textureSlotCache[key] = entry;
            return entry;
        }

        private Texture2D[] GetDisplayedTextureArray(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkEditorUtility.TextureSlot[] slots,
            int channel,
            int expectedSlotCount)
        {
            if (pendingTextureUpdate != null &&
                pendingTextureUpdate.Profile == profile &&
                pendingTextureUpdate.Layer == layer)
            {
                Texture2D[] pending = channel == 1
                    ? pendingTextureUpdate.EmissiveTextures
                    : channel == 2
                        ? pendingTextureUpdate.NormalTextures
                        : pendingTextureUpdate.Textures;
                if (pending != null && pending.Length == expectedSlotCount)
                {
                    return (Texture2D[])pending.Clone();
                }
            }

            Texture2D[] result = new Texture2D[expectedSlotCount];
            for (int i = 0; i < expectedSlotCount; i++)
            {
                DimensionPortalArtworkEditorUtility.TextureSlot slot =
                    i < slots.Length ? slots[i] : null;
                if (slot != null)
                {
                    result[i] = channel == 1
                        ? slot.EmissiveTexture
                        : channel == 2
                            ? slot.NormalTexture
                            : slot.Texture;
                }
            }

            return result;
        }

        private bool IsDirectTextureReference(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkReferenceKind referenceKind,
            SpriteAsset resolvedAsset,
            SerializedProperty reference)
        {
            if (referenceKind == DimensionPortalArtworkReferenceKind.External)
            {
                return true;
            }

            if (referenceKind != DimensionPortalArtworkReferenceKind.Managed)
            {
                return false;
            }

            TryReadReferenceAddress(reference, out long low, out long high);
            return GetTextureSlotCacheEntry(
                       profile,
                       layer,
                       referenceKind,
                       resolvedAsset,
                       low,
                       high)
                   .UsesDirectTextureOverride;
        }

        private bool TryGetPendingTextureSlot(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            int slotIndex,
            out Texture2D texture,
            out Texture2D emissive)
        {
            texture = null;
            emissive = null;
            if (pendingTextureUpdate == null ||
                pendingTextureUpdate.Profile != profile ||
                pendingTextureUpdate.Layer != layer ||
                pendingTextureUpdate.Textures == null ||
                pendingTextureUpdate.EmissiveTextures == null ||
                slotIndex < 0 ||
                slotIndex >= pendingTextureUpdate.Textures.Length ||
                slotIndex >= pendingTextureUpdate.EmissiveTextures.Length)
            {
                return false;
            }

            texture = pendingTextureUpdate.Textures[slotIndex];
            emissive = pendingTextureUpdate.EmissiveTextures[slotIndex];
            return true;
        }

        private static void DrawTextureSlotPlaceholder(
            DimensionPortalArtworkLayer layer,
            int slotIndex,
            string message)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent(
                    GetExpectedTextureSlotDisplayName(layer, slotIndex),
                    message),
                DimensionsApiImguiTheme.SectionLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                new GUIContent("Waiting for SpriteAsset data", message),
                DimensionsApiImguiTheme.Caption,
                GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        private static string GetTextureFieldTooltip(
            DimensionPortalArtworkEditorUtility.TextureSlot slot,
            string channel)
        {
            return channel + " texture for " +
                   (slot.DisplayName ?? "this animation") +
                   ". Color, emissive, and normal textures in one animation must use matching dimensions. " +
                   "Clear the field to restore the vanilla sheet for this slot.";
        }

        private static float GetTexturePanelMinimumHeight(
            DimensionPortalArtworkLayer layer)
        {
            // One labeled row per animation slot; emissive and normal maps are not exposed.
            return 34f + GetExpectedTextureSlotCount(layer) * 24f;
        }

        private static int GetExpectedTextureSlotCount(
            DimensionPortalArtworkLayer layer)
        {
            return layer == DimensionPortalArtworkLayer.CenterInstant
                ? 3
                : layer == DimensionPortalArtworkLayer.Center
                    ? 2
                    : 1;
        }

        private static string GetExpectedTextureSlotDisplayName(
            DimensionPortalArtworkLayer layer,
            int slotIndex)
        {
            if (layer == DimensionPortalArtworkLayer.CenterInstant)
            {
                return slotIndex == 0
                    ? "Loop"
                    : slotIndex == 1
                        ? "Opening"
                        : "Closing";
            }

            if (layer == DimensionPortalArtworkLayer.Center)
            {
                return slotIndex == 0 ? "Loop" : "Opening";
            }

            return GetArtworkLayerDisplayName(layer);
        }

        private static string GetArtworkLayerDisplayName(
            DimensionPortalArtworkLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalArtworkLayer.Frame:
                    return "Frame";
                case DimensionPortalArtworkLayer.ChargeSweep:
                    return "Charge sweep";
                case DimensionPortalArtworkLayer.Milestones:
                    return "Milestones";
                case DimensionPortalArtworkLayer.Center:
                case DimensionPortalArtworkLayer.CenterInstant:
                    return "Center";
                default:
                    return "portal artwork";
            }
        }

        private static string GetArtworkReferencePropertyName(
            DimensionPortalArtworkLayer layer)
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
                case DimensionPortalArtworkLayer.CenterInstant:
                    return "centerEffectSpriteAsset";
                default:
                    return string.Empty;
            }
        }

        private void InvalidateTextureSlotCache()
        {
            textureSlotCache.Clear();
            swirlTextureSlotCache = null;
        }
    }
}
