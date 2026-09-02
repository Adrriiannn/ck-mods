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
    /// Changing a texture reference, and making a variant of the artwork.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private void QueueTextureUpdate(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            Texture2D[] textures,
            Texture2D[] emissiveTextures,
            Texture2D[] normalTextures)
        {
            if (template == null ||
                profile == null ||
                textures == null ||
                emissiveTextures == null ||
                normalTextures == null ||
                textures.Length != emissiveTextures.Length ||
                textures.Length != normalTextures.Length)
            {
                return;
            }

            PendingTextureUpdate update = new PendingTextureUpdate
            {
                Template = template,
                Profile = profile,
                Layer = layer,
                Textures = (Texture2D[])textures.Clone(),
                EmissiveTextures = (Texture2D[])emissiveTextures.Clone(),
                NormalTextures = (Texture2D[])normalTextures.Clone()
            };
            CapturePendingTextureReference(update);
            pendingTextureUpdate = update;
            profileEditSession.MarkChanged();
            previewCompositionDirty = true;
            repaintRequested = true;
            if (textureUpdateQueued)
            {
                return;
            }

            textureUpdateQueued = true;
            EditorApplication.delayCall += ProcessPendingTextureUpdate;
        }

        private void QueueSwirlTextureUpdate(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            Texture2D colorTexture,
            Texture2D emissiveTexture,
            Texture2D normalTexture)
        {
            if (template == null || profile == null || colorTexture == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            TryReadReferenceAddress(
                serialized.FindProperty(
                    DimensionPortalSwirlArtworkEditorUtility.ReferencePropertyName),
                out long low,
                out long high);
            pendingSwirlTextureUpdate = new PendingSwirlTextureUpdate
            {
                Template = template,
                Profile = profile,
                ColorTexture = colorTexture,
                EmissiveTexture = emissiveTexture,
                NormalTexture = normalTexture,
                ExpectedAddressLow = low,
                ExpectedAddressHigh = high,
                ExpectedTemplateIdentity = CaptureAssetIdentity(template),
                ExpectedProfileIdentity = CaptureAssetIdentity(profile)
            };
            profileEditSession.MarkChanged();
            previewCompositionDirty = true;
            repaintRequested = true;
            if (swirlTextureUpdateQueued)
            {
                return;
            }

            swirlTextureUpdateQueued = true;
            EditorApplication.delayCall += ProcessPendingSwirlTextureUpdate;
        }

        public bool FlushPendingFrameTextureUpdate(out string message)
        {
            message = string.Empty;
            if (pendingTextureUpdate != null)
            {
                EditorApplication.delayCall -= ProcessPendingTextureUpdate;
                ProcessPendingTextureUpdate();
                message = pendingArtworkMessage ?? string.Empty;
                if (pendingArtworkMessageType == MessageType.Error)
                {
                    return false;
                }
            }

            if (pendingSwirlTextureUpdate != null)
            {
                EditorApplication.delayCall -= ProcessPendingSwirlTextureUpdate;
                ProcessPendingSwirlTextureUpdate();
                message = pendingArtworkMessage ?? string.Empty;
                if (pendingArtworkMessageType == MessageType.Error)
                {
                    return false;
                }
            }

            return true;
        }

        private void ProcessPendingTextureUpdate()
        {
            textureUpdateQueued = false;
            PendingTextureUpdate update = pendingTextureUpdate;
            pendingTextureUpdate = null;
            if (disposed || update == null)
            {
                return;
            }

            try
            {
                if (!IsActiveEditingContextCurrent(
                        update.Template,
                        update.ExpectedTemplateIdentity,
                        update.Profile,
                        update.ExpectedProfileIdentity))
                {
                    pendingArtworkMessage =
                        "The pending " + GetArtworkLayerDisplayName(update.Layer) +
                        " texture change was cancelled because the active Dimension Asset or portal profile changed.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (!IsTextureReferenceCurrent(update))
                {
                    pendingArtworkMessage =
                        "The pending " + GetArtworkLayerDisplayName(update.Layer) +
                        " texture change was cancelled because the Artwork override changed before it was saved.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (!DimensionPortalArtworkEditorUtility.CreateOrUpdateLayerTextures(
                        update.Template,
                        update.Profile,
                        update.Layer,
                        update.Textures,
                        update.EmissiveTextures,
                        update.NormalTextures,
                        out string message))
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Error;
                }
                else
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Info;
                    serializedProfileCache = null;
                    serializedProfileTarget = null;
                    hasPaletteSnapshot = false;
                    ClearPreviewAssetCaches();
                    ClearPaletteFocus();
                    hasSelectedPixel = false;
                }
            }
            catch (Exception exception)
            {
                pendingArtworkMessage =
                    "Could not update the " + GetArtworkLayerDisplayName(update.Layer) +
                    " textures: " + exception.Message;
                pendingArtworkMessageType = MessageType.Error;
                Debug.LogException(exception);
            }

            repaintRequested = true;
        }

        private void ProcessPendingSwirlTextureUpdate()
        {
            swirlTextureUpdateQueued = false;
            PendingSwirlTextureUpdate update = pendingSwirlTextureUpdate;
            pendingSwirlTextureUpdate = null;
            if (disposed || update == null)
            {
                return;
            }

            try
            {
                if (!IsActiveEditingContextCurrent(
                        update.Template,
                        update.ExpectedTemplateIdentity,
                        update.Profile,
                        update.ExpectedProfileIdentity))
                {
                    pendingArtworkMessage =
                        "The pending Swirls texture change was cancelled because the active Dimension Asset or portal profile changed.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                SerializedObject serialized = new SerializedObject(update.Profile);
                serialized.Update();
                if (!TryReadReferenceAddress(
                        serialized.FindProperty(
                            DimensionPortalSwirlArtworkEditorUtility.ReferencePropertyName),
                        out long low,
                        out long high) ||
                    low != update.ExpectedAddressLow ||
                    high != update.ExpectedAddressHigh)
                {
                    pendingArtworkMessage =
                        "The pending Swirls texture change was cancelled because the Artwork override changed before it was saved.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (!DimensionPortalSwirlArtworkEditorUtility
                        .CreateOrUpdateAnimationZeroTextures(
                            update.Template,
                            update.Profile,
                            update.ColorTexture,
                            update.EmissiveTexture,
                            update.NormalTexture,
                            out string message))
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Error;
                }
                else
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Info;
                    serializedProfileCache = null;
                    serializedProfileTarget = null;
                    InvalidateTextureSlotCache();
                    ClearPreviewAssetCaches();
                    ClearPaletteFocus();
                    hasSelectedPixel = false;
                }
            }
            catch (Exception exception)
            {
                pendingArtworkMessage =
                    "Could not update the Swirls textures: " + exception.Message;
                pendingArtworkMessageType = MessageType.Error;
                Debug.LogException(exception);
            }

            repaintRequested = true;
        }

        private void CapturePendingTextureReference(PendingTextureUpdate update)
        {
            update.ExpectedTemplateIdentity = CaptureAssetIdentity(update.Template);
            update.ExpectedProfileIdentity = CaptureAssetIdentity(update.Profile);
            update.ExpectedAddressLow = 0L;
            update.ExpectedAddressHigh = 0L;
            update.ExpectedAssetGuid = string.Empty;
            update.ExpectedReferenceKind = DimensionPortalArtworkReferenceKind.Empty;
            if (update.Profile == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(update.Profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(
                GetArtworkReferencePropertyName(update.Layer));
            TryReadReferenceAddress(
                reference,
                out update.ExpectedAddressLow,
                out update.ExpectedAddressHigh);
            update.ExpectedReferenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    reference,
                    update.Profile,
                    update.Layer,
                    out SpriteAsset asset);
            update.ExpectedAssetGuid = GetAssetGuid(asset);
        }

        private static bool IsTextureReferenceCurrent(PendingTextureUpdate update)
        {
            if (update == null || update.Profile == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(update.Profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(
                GetArtworkReferencePropertyName(update.Layer));
            if (!TryReadReferenceAddress(reference, out long low, out long high) ||
                low != update.ExpectedAddressLow ||
                high != update.ExpectedAddressHigh)
            {
                return false;
            }

            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
            DimensionPortalArtworkReferenceKind kind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    reference,
                    update.Profile,
                    update.Layer,
                    out SpriteAsset asset);
            return kind == update.ExpectedReferenceKind &&
                   string.Equals(
                       GetAssetGuid(asset),
                       update.ExpectedAssetGuid ?? string.Empty,
                       StringComparison.Ordinal);
        }

        private static bool TryReadReferenceAddress(
            SerializedProperty reference,
            out long low,
            out long high)
        {
            low = 0L;
            high = 0L;
            SerializedProperty address = reference == null
                ? null
                : reference.FindPropertyRelative("m_address");
            SerializedProperty lowProperty = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty highProperty = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (lowProperty == null || highProperty == null)
            {
                return false;
            }

            low = lowProperty.longValue;
            high = highProperty.longValue;
            return true;
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
        }

        private static AssetIdentity CaptureAssetIdentity(UnityEngine.Object asset)
        {
            AssetIdentity identity = new AssetIdentity
            {
                Guid = string.Empty,
                LocalFileId = 0L,
                InstanceId = asset == null ? 0 : asset.GetInstanceID()
            };
            if (asset != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out string guid,
                    out long localFileId))
            {
                identity.Guid = guid ?? string.Empty;
                identity.LocalFileId = localFileId;
            }

            return identity;
        }

        private static bool MatchesAssetIdentity(
            UnityEngine.Object asset,
            AssetIdentity expected)
        {
            if (asset == null)
            {
                return expected.InstanceId == 0 && !expected.HasPersistentIdentity;
            }

            AssetIdentity current = CaptureAssetIdentity(asset);
            if (expected.HasPersistentIdentity)
            {
                return current.HasPersistentIdentity &&
                       string.Equals(
                           current.Guid,
                           expected.Guid,
                           StringComparison.Ordinal) &&
                       current.LocalFileId == expected.LocalFileId;
            }

            return expected.InstanceId != 0 &&
                   current.InstanceId == expected.InstanceId;
        }

        private bool IsActiveEditingContextCurrent(
            DimensionTemplateAsset template,
            AssetIdentity templateIdentity,
            DimensionPortalVisualProfileAsset profile,
            AssetIdentity profileIdentity)
        {
            return template != null &&
                   profile != null &&
                   MatchesAssetIdentity(template, templateIdentity) &&
                   MatchesAssetIdentity(profile, profileIdentity) &&
                   MatchesAssetIdentity(activeTemplate, templateIdentity) &&
                   MatchesAssetIdentity(activeProfile, profileIdentity) &&
                   DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                       template,
                       profile);
        }

        private void CancelPendingTextureUpdate()
        {
            EditorApplication.delayCall -= ProcessPendingTextureUpdate;
            textureUpdateQueued = false;
            pendingTextureUpdate = null;
            EditorApplication.delayCall -= ProcessPendingSwirlTextureUpdate;
            swirlTextureUpdateQueued = false;
            pendingSwirlTextureUpdate = null;
            previewCompositionDirty = true;
            repaintRequested = true;
        }

        private void DrawArtworkOverride(
            DimensionTemplateAsset template,
            SerializedObject serializedProfile,
            StudioLayer layer,
            SerializedProperty property,
            ref DrawResult result)
        {
            if (property == null)
            {
                return;
            }

            GUIContent label = new GUIContent("Artwork override");
            float height = EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            Rect fieldRect = EditorGUI.PrefixLabel(rect, label);
            float createButtonSize = Mathf.Min(
                EditorGUIUtility.singleLineHeight,
                Mathf.Max(1f, fieldRect.height));
            Rect createRect = new Rect(
                fieldRect.x,
                fieldRect.y,
                createButtonSize,
                createButtonSize);
            Event current = Event.current;
            bool createRequested =
                current != null &&
                current.type == EventType.MouseDown &&
                current.button == 0 &&
                createRect.Contains(current.mousePosition);
            bool pointerInteraction =
                current != null &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseUp) &&
                current.button == 0 &&
                fieldRect.Contains(current.mousePosition);
            bool contextReady = true;
            if (pointerInteraction &&
                !DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out string contextError))
            {
                contextReady = false;
                result.Message = contextError;
                result.MessageType = MessageType.Warning;
            }

            if (createRequested)
            {
                current.Use();
                if (contextReady &&
                    (layer == StudioLayer.InnerFlecks ||
                     TryGetArtworkLayer(layer, out _, instantPortalMode)))
                {
                    QueueCreateArtworkVariant(
                        template,
                        serializedProfile.targetObject as DimensionPortalVisualProfileAsset,
                        layer);
                }
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && contextReady;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(fieldRect, property, GUIContent.none, true);
            bool referenceChanged = EditorGUI.EndChangeCheck();
            GUI.Box(
                createRect,
                new GUIContent(
                    "+",
                    artworkVariantCreateQueued
                        ? "An editable artwork variant is already being created."
                        : layer == StudioLayer.InnerFlecks
                            ? "Create and select an editable copy of the current Swirls SpriteAsset in this portal profile."
                            : "Create and select a new vanilla-derived SpriteAsset in this dimension mod."),
                EditorStyles.miniButton);
            GUI.enabled = previousEnabled;
            if (referenceChanged && layer == StudioLayer.InnerFlecks)
            {
                CancelPendingTextureUpdate();
                InvalidateTextureSlotCache();
                ClearPaletteFocus();
                hasSelectedPixel = false;
                result.Changed = true;
            }
            else if (referenceChanged &&
                     TryGetArtworkLayer(layer, out DimensionPortalArtworkLayer artworkLayer, instantPortalMode))
            {
                CancelPendingTextureUpdate();

                DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
                InvalidateTextureSlotCache();
                DimensionPortalArtworkEditorUtility.SynchronizePaletteFromReference(
                    serializedProfile,
                    serializedProfile.targetObject as DimensionPortalVisualProfileAsset,
                    artworkLayer);
                ClearPaletteFocus();
                hasSelectedPixel = false;
                result.Changed = true;
            }
        }

        private void QueueCreateArtworkVariant(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            StudioLayer layer)
        {
            if (artworkVariantCreateQueued || template == null || profile == null)
            {
                return;
            }

            CancelPendingTextureUpdate();

            AssetIdentity templateIdentity = CaptureAssetIdentity(template);
            AssetIdentity profileIdentity = CaptureAssetIdentity(profile);
            int requestVersion = ++artworkVariantRequestVersion;
            artworkVariantCreateQueued = true;
            pendingArtworkVariantLayer = layer;
            EditorApplication.delayCall += () =>
            {
                if (disposed)
                {
                    return;
                }

                if (requestVersion != artworkVariantRequestVersion)
                {
                    return;
                }

                artworkVariantCreateQueued = false;
                try
                {
                    if (!IsActiveEditingContextCurrent(
                            template,
                            templateIdentity,
                            profile,
                            profileIdentity))
                    {
                        pendingArtworkMessage =
                            "The pending artwork copy was cancelled because the active Dimension Asset or portal profile changed.";
                        pendingArtworkMessageType = MessageType.Warning;
                    }
                    else if (!DimensionScriptableDataContextUtility.TryScopeToTemplate(
                            template,
                            out string contextError))
                    {
                        pendingArtworkMessage = contextError;
                        pendingArtworkMessageType = MessageType.Warning;
                    }
                    else
                    {
                        string message;
                        bool created;
                        if (layer == StudioLayer.InnerFlecks)
                        {
                            created =
                                DimensionPortalSwirlArtworkEditorUtility.CreateEditableCopy(
                                    template,
                                    profile,
                                    out message);
                        }
                        else if (TryGetArtworkLayer(
                                     layer,
                                     out DimensionPortalArtworkLayer artworkLayer,
                                     instantPortalMode))
                        {
                            created =
                                DimensionPortalArtworkEditorUtility.CreateEditableCopy(
                                    template,
                                    profile,
                                    artworkLayer,
                                    out message);
                        }
                        else
                        {
                            created = false;
                            message = "This portal layer does not support editable artwork copies.";
                        }

                        if (!created)
                        {
                            pendingArtworkMessage = message;
                            pendingArtworkMessageType = MessageType.Error;
                        }
                        else
                        {
                            pendingArtworkMessage = message;
                            pendingArtworkMessageType = MessageType.Info;
                            profileEditSession.MarkChanged();
                            serializedProfileCache = null;
                            serializedProfileTarget = null;
                            hasPaletteSnapshot = false;
                            InvalidateTextureSlotCache();
                            ClearPaletteFocus();
                            hasSelectedPixel = false;
                        }
                    }
                }
                catch (Exception exception)
                {
                    pendingArtworkMessage =
                        "Could not create editable portal artwork: " + exception.Message;
                    pendingArtworkMessageType = MessageType.Error;
                    Debug.LogException(exception);
                }

                repaintRequested = true;
            };
        }

        private void CancelPendingArtworkVariantCreation(StudioLayer layer)
        {
            if (!artworkVariantCreateQueued || pendingArtworkVariantLayer != layer)
            {
                return;
            }

            ++artworkVariantRequestVersion;
            artworkVariantCreateQueued = false;
        }
    }
}
