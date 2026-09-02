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
    /// The portal prefabs a consumer mod gets: the visual, the entity, and what is wired onto them.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static DimensionRuntimeManifestAsset EnsureRuntimeManifestAsset(
            DimensionTemplateAsset template,
            DimensionTemplateManifestExportPreview preview,
            string generatedFolder)
        {
            string path = generatedFolder + "/RuntimeManifest.asset";
            DimensionRuntimeManifestAsset asset =
                AssetDatabase.LoadAssetAtPath<DimensionRuntimeManifestAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DimensionRuntimeManifestAsset>();
                asset.name = "RuntimeManifest";
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Configure(template, preview, true, false);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject EnsurePortalVisualPrefab(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            bool itemPortal,
            DimensionPortalAccessRuleAsset entryRule)
        {
            string visualStem = itemPortal ? "ItemPortalVisual" : "PortalVisual";
            string path = portalFolder + "/" + portalOutput.AssetStem + visualStem + ".prefab";
            // The item pass wires against the instant visual identity: the item profile drives
            // the look and every generated artwork file/address is seeded by the item portal's
            // own names, isolated from the placed portal's generated assets.
            DimensionRuntimePortalOutput visualOutput = itemPortal
                ? portalOutput.WithInstantVisualIdentity()
                : portalOutput;
            GameObject root = CreateCleanPortalVisualTemplate(itemPortal);
            try
            {
                root.name = portalOutput.AssetStem + visualStem;
                DimensionPortal portal = EnsureComponent<DimensionPortal>(root);
                RemoveComponentByName(root, "Portal", portal);
                RemoveMissingMonoBehaviours(root);
                ConfigurePortalRuntimeFields(portal, portalOutput, itemPortal);
                ApplyPortalOfferingLooks(portal, entryRule, itemPortal);
                WirePortalVisualReferences(root, portal, visualOutput, portalFolder, modRoot, itemPortal);
                RewriteInteractableCallbacks(root, portal);
                if (itemPortal)
                {
                    BakeItemPortalVisualMode(root, !portalOutput.ItemProfileIsDedicated);
                }

                ValidateSpriteObjectOnlyPortal(root, itemPortal);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Bakes the instant item-portal (V2) mode into the dedicated visual prefab: the serialized
        /// item-portal flag that gates the interaction outline, and the closing animation index
        /// (animation 2 of the instant center contract; the runtime guards gracefully when the
        /// resolved center asset has no third animation). The layers an instant portal can never
        /// show — frame, charge wave, milestones and the charge-completion ready burst — are forced
        /// off regardless of profile, matching the Instant Portal Studio tab which does not offer
        /// them. The ground shadow stays profile-driven for a dedicated instant profile and is only
        /// forced off in the shared placed-profile fallback.
        /// </summary>
        private static void BakeItemPortalVisualMode(GameObject root, bool forceFrameless)
        {
            DimensionPortalVisual visual =
                root == null ? null : root.GetComponent<DimensionPortalVisual>();
            if (visual == null)
            {
                return;
            }

            SerializedObject serializedVisual = new SerializedObject(visual);
            serializedVisual.Update();
            SetSerializedBool(serializedVisual, "itemPortalMode", true);
            SetSerializedInt(
                serializedVisual,
                "centerClosingAnimationIndex",
                DimensionPortalInstantArtworkEditorUtility.InstantClosingAnimationIndex);
            SetSerializedBool(serializedVisual, "portalBodyVisible", false);
            SetSerializedBool(serializedVisual, "chargeWaveVisible", false);
            SetSerializedBool(serializedVisual, "milestoneVisible", false);
            SetSerializedBool(serializedVisual, "playReadyFlash", false);
            if (forceFrameless)
            {
                SetSerializedBool(serializedVisual, "projectedShadowVisible", false);
            }

            serializedVisual.ApplyModifiedPropertiesWithoutUndo();
        }

        private enum PortalEntityVariant
        {
            Entry,
            Return,
            Item
        }

        private static GameObject EnsurePortalEntityPrefab(
            DimensionRuntimePortalOutput portalOutput,
            GameObject visualPrefab,
            string portalFolder,
            PortalEntityVariant variant,
            DimensionPortalAccessRuleAsset entryRule)
        {
            string portalKind = variant == PortalEntityVariant.Return
                ? "ReturnPortal"
                : variant == PortalEntityVariant.Item
                    ? "ItemPortal"
                    : "Portal";
            string path = portalFolder + "/" + portalOutput.AssetStem + portalKind + "Entity.prefab";
            bool unloadPrefabContents;
            GameObject root = LoadRequiredPortalEntityTemplate(out unloadPrefabContents);
            try
            {
                root.name = portalOutput.AssetStem + portalKind + "Entity";
                EnsurePortalEntityAuthoring(root, portalOutput, visualPrefab, variant);
                EnsurePortalOffering(root, variant, entryRule);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                ConfigureGhostAuthoringComponent(saved, path);
                PrefabUtility.SavePrefabAsset(saved);
                return saved;
            }
            finally
            {
                if (unloadPrefabContents)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        /// <summary>
        /// Gives the entry portal its offering window: one inventory slot per required item, the
        /// name list the runtime resolves into slot rules, and the look each author chose.
        /// </summary>
        /// <remarks>
        /// Only the placed entry portal asks for an offering. The return portal must always work
        /// (stranding a player inside a dimension is never a feature), and the instant item portal
        /// is its own consumable price.
        /// </remarks>
        /// <summary>
        /// One slot per distinct item, in authored order. The same item named twice is one slot
        /// asking for the summed amount — a window with two half answers to one question would
        /// read as a bug — and the merged slot keeps the first look an author actually chose,
        /// whichever row carried it.
        /// </summary>
        private static List<DimensionPortalRequiredItemTemplate> MergePortalOfferingItems(
            DimensionPortalAccessRuleAsset entryRule)
        {
            List<DimensionPortalRequiredItemTemplate> merged =
                new List<DimensionPortalRequiredItemTemplate>();
            if (entryRule == null || entryRule.RequiredItems == null)
            {
                return merged;
            }

            Dictionary<string, int> indexOf = new Dictionary<string, int>();
            DimensionPortalRequiredItemTemplate[] required = entryRule.RequiredItems;
            for (int i = 0; i < required.Length; i++)
            {
                string itemId = required[i].ItemId;
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                int existing;
                if (!indexOf.TryGetValue(itemId, out existing))
                {
                    indexOf[itemId] = merged.Count;
                    merged.Add(required[i]);
                    continue;
                }

                DimensionPortalRequiredItemTemplate first = merged[existing];
                bool firstLookIsUntouched =
                    first.SlotLook == Portals.DimensionPortalOfferingLook.GhostOfTheItem &&
                    first.SlotSprite == null &&
                    first.SlotDimness <= 0f;
                bool adoptIncomingLook = firstLookIsUntouched;
                merged[existing] = new DimensionPortalRequiredItemTemplate(
                    first.ItemId,
                    first.DisplayName,
                    first.Amount + required[i].Amount,
                    first.ConsumeOnTravel || required[i].ConsumeOnTravel,
                    adoptIncomingLook ? required[i].SlotLook : first.SlotLook,
                    adoptIncomingLook ? required[i].SlotSprite : first.SlotSprite,
                    adoptIncomingLook ? required[i].SlotDimness : first.SlotDimness);
            }

            return merged;
        }

        /// <summary>
        /// Writes the offering look table onto the visual prefab, which is where
        /// <see cref="Portals.DimensionPortal"/> lives and therefore where the slot UI reads it.
        /// Slot order matches <see cref="MergePortalOfferingItems"/> exactly, so a slot index
        /// means the same thing to the entity buffer and to the hint patch.
        /// </summary>
        /// <remarks>
        /// The placed entry and return portals share this prefab, so the table alone must never
        /// imply a window: the entity's offering buffer decides that, and only the entry portal
        /// has one.
        /// </remarks>
        private static void ApplyPortalOfferingLooks(
            Portals.DimensionPortal portal,
            DimensionPortalAccessRuleAsset entryRule,
            bool itemPortal)
        {
            if (portal == null)
            {
                return;
            }

            List<DimensionPortalRequiredItemTemplate> merged = itemPortal
                ? new List<DimensionPortalRequiredItemTemplate>()
                : MergePortalOfferingItems(entryRule);

            SerializedObject serializedPortal = new SerializedObject(portal);
            serializedPortal.Update();
            SerializedProperty slots = serializedPortal.FindProperty("offeringSlots");
            if (slots == null)
            {
                return;
            }

            slots.arraySize = merged.Count;
            for (int i = 0; i < merged.Count; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("itemName").stringValue = merged[i].ItemId;
                slot.FindPropertyRelative("amount").intValue = merged[i].Amount;
                // intValue, not enumValueIndex: the index maps into the enum's name array and
                // only coincides with the numeric value while the enum stays sequential.
                slot.FindPropertyRelative("look").intValue = (int)merged[i].SlotLook;
                slot.FindPropertyRelative("customSprite").objectReferenceValue =
                    merged[i].SlotSprite;
                slot.FindPropertyRelative("dimness").floatValue = merged[i].SlotDimness;
            }

            serializedPortal.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePortalOffering(
            GameObject root,
            PortalEntityVariant variant,
            DimensionPortalAccessRuleAsset entryRule)
        {
            // Merge first, then decide: a rule can nominally use required items while every row
            // is blank, which asks for nothing and must clean up like asking for nothing.
            List<DimensionPortalRequiredItemTemplate> merged =
                variant == PortalEntityVariant.Entry && entryRule != null &&
                entryRule.UsesRequiredItems
                    ? MergePortalOfferingItems(entryRule)
                    : new List<DimensionPortalRequiredItemTemplate>();

            if (merged.Count == 0)
            {
                InventoryAuthoring staleInventory = root.GetComponent<InventoryAuthoring>();
                if (staleInventory != null)
                {
                    Object.DestroyImmediate(staleInventory, true);
                }

                ExpandNullforge.Portals.DimensionPortalOfferingAuthoring staleOffering =
                    root.GetComponent<ExpandNullforge.Portals.DimensionPortalOfferingAuthoring>();
                if (staleOffering != null)
                {
                    Object.DestroyImmediate(staleOffering, true);
                }

                return;
            }


            // InventoryAuthoring's OnValidate walks its lists the moment the component is added,
            // and they ship without initialisers — the same trap the object spine documents. Add
            // inside a quiet window and give the lists real values immediately.
            InventoryAuthoring inventory = root.GetComponent<InventoryAuthoring>();
            if (inventory == null)
            {
                bool logging = Debug.unityLogger.logEnabled;
                Debug.unityLogger.logEnabled = false;
                try
                {
                    inventory = root.AddComponent<InventoryAuthoring>();
                }
                catch (System.Exception)
                {
                    inventory = root.GetComponent<InventoryAuthoring>();
                }
                finally
                {
                    Debug.unityLogger.logEnabled = logging;
                }
            }

            inventory.sizeX = merged.Count;
            inventory.sizeY = 1;
            inventory.maxExtraSize = 0;
            inventory.canOnlyContainOneItemPerSlot = false;
            // Slot rules are written at runtime, once item names can resolve to ids — a mod's own
            // items have no id until the mod loads, so nothing useful can be baked here.
            inventory.slotRequirements = new List<SlotRequirement>();
            inventory.itemsInInventory = new List<ObjectData>();

            ExpandNullforge.Portals.DimensionPortalOfferingAuthoring offering =
                root.GetComponent<ExpandNullforge.Portals.DimensionPortalOfferingAuthoring>();
            if (offering == null)
            {
                offering = root.AddComponent<ExpandNullforge.Portals.DimensionPortalOfferingAuthoring>();
            }

            offering.entries = new List<ExpandNullforge.Portals.DimensionPortalOfferingAuthoringEntry>();
            for (int i = 0; i < merged.Count; i++)
            {
                offering.entries.Add(new ExpandNullforge.Portals.DimensionPortalOfferingAuthoringEntry
                {
                    itemName = merged[i].ItemId,
                    amount = merged[i].Amount,
                    look = merged[i].SlotLook,
                    dimness = merged[i].SlotDimness,
                    consumeOnTravel = merged[i].ConsumeOnTravel,
                });
            }

            // The look table itself belongs to the visual prefab, where DimensionPortal lives;
            // ApplyPortalOfferingLooks writes it there against this same merged order.
        }

        private static void EnsurePortalEntityAuthoring(
            GameObject root,
            DimensionRuntimePortalOutput portalOutput,
            GameObject visualPrefab,
            PortalEntityVariant variant)
        {
            bool returnPortal = variant == PortalEntityVariant.Return;
            bool itemPortal = variant == PortalEntityVariant.Item;

            // The instant item portal (V2) bakes the entry definition as its defaults — every live
            // spawn overwrites DimensionPortalCD from its registered item config in
            // DimensionItemPortalSpawnSystem, so the baked values only cover the pre-replication frame.
            DimensionPortalDefinition portalDefinition =
                returnPortal ? portalOutput.ReturnPortal : portalOutput.EntryPortal;
            DimensionPortalPresentationDefinition presentation =
                returnPortal ? portalOutput.ReturnPresentation : portalOutput.EntryPresentation;
            string objectName = returnPortal
                ? portalOutput.ReturnPortalObjectName
                : itemPortal
                    ? portalOutput.ItemPortalObjectName
                    : portalOutput.PortalObjectName;

            ObjectAuthoring objectAuthoring = EnsureComponent<ObjectAuthoring>(root);
            objectAuthoring.objectName = objectName;
            objectAuthoring.initialAmount = CraftedPortalAmount;
            objectAuthoring.objectType = ObjectType.PlaceablePrefab;
            objectAuthoring.tags = new List<ObjectCategoryTag> { ObjectCategoryTag.LightSource };
            objectAuthoring.rarity = Rarity.Epic;
            objectAuthoring.salvageMultiplier = 0.2f;
            objectAuthoring.graphicalPrefab = visualPrefab;
            objectAuthoring.additionalSprites = new List<Sprite>();

            Sprite inventoryIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PortalInventoryIconPath);
            Sprite smallIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PortalSmallIconPath);
            if (inventoryIcon == null)
            {
                inventoryIcon = smallIcon;
            }

            if (smallIcon == null)
            {
                smallIcon = inventoryIcon;
            }

            InventoryItemAuthoring inventory = EnsureComponent<InventoryItemAuthoring>(root);
            inventory.sellValue = 1;
            inventory.buyValueMultiplier = 1.0f;
            inventory.icon = inventoryIcon;
            inventory.smallIcon = smallIcon;
            inventory.isStackable = true;
            inventory.requiredObjectsToCraft = new List<InventoryItemAuthoring.CraftingObject>();
            inventory.craftingTime = portalOutput.CraftingTimeSeconds;

            LocalizationAuthoring localization = EnsureComponent<LocalizationAuthoring>(root);
            localization.termKey = portalOutput.PortalObjectName;
            SetSerializedArraySize(localization, "languageGenders", 0);

            if (itemPortal)
            {
                // The instant portal is a pass-through effect, not a building: it keeps no
                // placeable footprint at all, so the player walks straight through its tile.
                // It only ever exists as a spawned world entity, never as a placeable object.
                objectAuthoring.objectType = ObjectType.NonObtainable;
                RemoveComponentIfPresent<PlaceableObjectAuthoring>(root);
            }
            else
            {
                PlaceableObjectAuthoring placeable = EnsureComponent<PlaceableObjectAuthoring>(root);
                placeable.prefabTileSize = new Vector2Int(3, 1);
                placeable.prefabCornerOffset = Vector2Int.zero;
                placeable.centerIsAtEntityPosition = false;
                placeable.canBePlacedOnPlayer = false;
                placeable.canBePlacedOnAnyWalkableTile = true;
                placeable.appearInMapUI = false;
                placeable.mapColor = portalOutput.MapColor;
                placeable.canBePlacedOnObjects = new List<ObjectID>();
                placeable.canNotBePlacedOnObjects = new List<ObjectID>();
            }

            InteractWithEnvironmentAuthoring interact = EnsureComponent<InteractWithEnvironmentAuthoring>(root);
            interact.radius = 1.4f;

            RemoveComponentIfPresent<PortalAuthoring>(root);
            if (returnPortal || itemPortal)
            {
                // Return portals are permanent fixtures; instant item portals are transient and
                // despawn on their own timer. Neither may be broken or picked up, which is what
                // the four calls below settle: leave them off and breaking the shared entry object
                // would hand players a free placed portal.
                EnsureComponent<IndestructibleAuthoring>(root);
                EnsureComponent<DontDropSelfAuthoring>(root);
                EnsureComponent<DontDropContainedAuthoring>(root);
                RemoveComponentIfPresent<CanBePickedUpAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<IndestructibleAuthoring>(root);
                RemoveComponentIfPresent<DontDestroyOnZeroHealthAuthoring>(root);
                RemoveComponentIfPresent<DontDropSelfAuthoring>(root);
                RemoveComponentIfPresent<DontDropContainedAuthoring>(root);
                EnsureComponent<CanBePickedUpAuthoring>(root);
            }

            EnsurePortalHitAuthoring(root, variant == PortalEntityVariant.Entry);

            DimensionPortalAuthoring portal = EnsureComponent<DimensionPortalAuthoring>(root);
            portal.PortalId = portalDefinition.PortalId;
            portal.ActivationCooldownSeconds = itemPortal ? 0.0f : presentation.CooldownSeconds;
            portal.ActivationChargeSeconds = returnPortal || itemPortal
                ? 0.0f
                : portalOutput.ActivationChargeSeconds;
            portal.RequireGeneratedArea = presentation.RequireGeneratedAreaOnUse;
            portal.AllowFallbackPosition = presentation.AllowFallbackPositionOnUse;
            portal.ActiveByDefault = true;
            portal.InteractableByDefault = itemPortal || presentation.Interactable;
            portal.IndestructibleByDefault = returnPortal || itemPortal;
            portal.PreviewTargetDimensionId = portalDefinition.ToDimensionId;
            portal.PreviewTargetLocalX = portalDefinition.ToLocalPosition.x;
            portal.PreviewTargetLocalY = portalDefinition.ToLocalPosition.y;

            TryAddGhostAuthoringComponent(root);
            RemoveMissingMonoBehaviours(root);
        }

        private static GameObject CreateCleanPortalVisualTemplate(bool itemPortal)
        {
            GameObject root = new GameObject("DimensionPortalVisualTemplate");
            // The pooled graphical-object system buckets view instances by component type, so
            // the instant visual must carry its own DimensionPortal subclass — otherwise the
            // pool serves placed-portal clones to instant portal entities.
            if (itemPortal)
            {
                root.AddComponent<DimensionInstantPortal>();
            }
            else
            {
                root.AddComponent<DimensionPortal>();
            }

            root.AddComponent<DimensionPortalVisual>();
            root.AddComponent<Animator>();

            Transform xScaler = new GameObject("XScaler").transform;
            xScaler.SetParent(root.transform, false);

            GameObject interactableObject = new GameObject("Interactable");
            interactableObject.transform.SetParent(root.transform, false);
            interactableObject.transform.localPosition = itemPortal
                ? Vector3.zero
                : new Vector3(1.0f, 0.0f, 0.0f);
            InteractableObject interactable =
                interactableObject.AddComponent<InteractableObject>();
            interactable.radius = 1.4f;
            interactable.ignorePlayerDirection = true;
            interactable.useDiscreteOutlineColor = false;

            Transform interactionPoint = new GameObject("InteractionPoint").transform;
            interactionPoint.SetParent(interactableObject.transform, false);
            AssignSerializedObjectReferenceList(
                interactable,
                "interactingPoints",
                new List<Object> { interactionPoint });

            return root;
        }

        private static GameObject LoadRequiredPortalEntityTemplate(out bool unloadPrefabContents)
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(PortalEntityTemplatePath);
            if (template != null)
            {
                unloadPrefabContents = true;
                return PrefabUtility.LoadPrefabContents(PortalEntityTemplatePath);
            }

            unloadPrefabContents = false;
            throw new FileNotFoundException(
                "The required dimension portal entity template is missing. " +
                "Restore the framework prefab before generating portal runtime output: " +
                PortalEntityTemplatePath);
        }

        private static void ConfigurePortalRuntimeFields(
            DimensionPortal portal,
            DimensionRuntimePortalOutput portalOutput,
            bool itemPortal)
        {
            if (portal == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(portal);
            serializedObject.Update();
            SetSerializedString(serializedObject, "fallbackPortalId", portalOutput.EntryPortal.PortalId);
            SetSerializedBool(serializedObject, "requireGeneratedArea", portalOutput.EntryPresentation.RequireGeneratedAreaOnUse);
            SetSerializedBool(serializedObject, "allowFallbackPosition", portalOutput.EntryPresentation.AllowFallbackPositionOnUse);
            SetSerializedString(
                serializedObject,
                "interactionReason",
                itemPortal
                    ? "Enter " + portalOutput.DimensionDisplayName + " through an instant portal."
                    : "Enter " + portalOutput.DimensionDisplayName + " through a placed portal.");

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RewriteInteractableCallbacks(GameObject root, DimensionPortal portal)
        {
            InteractableObject interactable = root == null
                ? null
                : root.GetComponentInChildren<InteractableObject>(true);
            if (interactable == null || portal == null)
            {
                return;
            }

            interactable.radius = 1.4f;
            interactable.ignorePlayerDirection = true;
            interactable.onUseActions = new List<UnityEvent>();
            UnityEvent useEvent = new UnityEvent();
            UnityEventTools.AddPersistentListener(useEvent, portal.Use);
            interactable.onUseActions.Add(useEvent);

            interactable.onTriggerExitActions = new List<UnityEvent>();
            UnityEvent exitEvent = new UnityEvent();
            UnityEventTools.AddPersistentListener(exitEvent, portal.OnLeavePortal);
            interactable.onTriggerExitActions.Add(exitEvent);
        }

        private static void WirePortalVisualReferences(
            GameObject root,
            DimensionPortal portal,
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            bool itemPortal)
        {
            if (root == null || portal == null)
            {
                return;
            }

            RemoveLegacyPortalVisualArtifacts(root);

            portal.XScaler = EnsurePortalXScaler(root);
            portal.animator = root.GetComponent<Animator>();

            DimensionPortalVisualProfileAsset visualProfile = portalOutput.VisualProfile;
            PortalVisualSpriteAssets visualAssets =
                ResolvePortalVisualSpriteAssets(portalOutput, portalFolder, modRoot);
            string bodyTexturePath = ResolvePortalFrameTexturePath(visualAssets.BodySource);
            GeneratedPortalOutlineSpriteAssets outlineMaskAssets =
                EnsureGeneratedPortalOutlineMaskAssets(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    bodyTexturePath);

            // SpriteObject.OnValidate resolves DataBlockRef addresses while the prefab is
            // being saved. Newly generated palette/outline SpriteAssets are already imported
            // and registered in the consumer manifest at this point, but Scriptable Data keeps
            // a separate address cache that does not observe those changes automatically.
            // Refresh it once per explicit Apply operation before any SpriteObject receives the
            // new addresses; otherwise prefab saving logs "Valid DataBlockRef returned null"
            // and can serialize a portal whose first editor resolution is stale.
            AssetDatabase.SaveAssets();
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();

            PortalSpriteObjectSet spriteObjects =
                EnsurePortalSpriteObjectHierarchy(
                    root,
                    outlineMaskAssets,
                    visualAssets,
                    visualProfile,
                    itemPortal);
            portal.shadow = spriteObjects.ShadowRoot;
            ManagedLight portalLight = EnsurePortalManagedLight(root, visualProfile, itemPortal);
            AssignSerializedObjectReference(portal, "optionalLightOptimizer", portalLight);
            EnsurePortalParticleMaterials(
                spriteObjects.CenterParticlesRoot,
                ResolvePortalParticleTextureOverride(visualProfile, false),
                portalFolder,
                SanitizeAssetFileName(
                    portalOutput.AssetStem + "CenterParticles",
                    "DimensionPortalCenterParticles"));
            EnsurePortalParticleMaterials(
                spriteObjects.ReadyFlashRoot,
                ResolvePortalParticleTextureOverride(visualProfile, true),
                portalFolder,
                SanitizeAssetFileName(
                    portalOutput.AssetStem + "ReadyFlash",
                    "DimensionPortalReadyFlash"));

            DimensionPortalVisual visual = EnsureComponent<DimensionPortalVisual>(root);
            AssignSerializedObjectReference(portal, "visual", visual);
            AssignSerializedObjectReference(visual, "portalBody", spriteObjects.Body);
            AssignSerializedObjectReference(visual, "portalBodyRenderer", spriteObjects.BodyRenderer);
            AssignSerializedObjectReference(visual, "portalChargeProgress", spriteObjects.ChargeProgress);
            AssignSerializedObjectReference(visual, "portalEmissiveWave", spriteObjects.EmissiveWave);
            AssignSerializedObjectReference(visual, "portalCenterEffect", spriteObjects.CenterEffect);
            AssignSerializedObjectReference(visual, "portalCustomSwirl", spriteObjects.CustomSwirl);
            AssignSerializedObjectReference(visual, "centerParticlesRoot", spriteObjects.CenterParticlesRoot);
            AssignSerializedObjectReference(visual, "readyFlashRoot", spriteObjects.ReadyFlashRoot);
            AssignSerializedObjectReference(visual, "portalOutlineMask", spriteObjects.OutlineMask);
            AssignSerializedObjectReference(visual, "portalOutlineSupportMask", spriteObjects.OutlineSupportMask);
            AssignSerializedObjectReference(visual, "portalOutlineCap", spriteObjects.OutlineCap);
            AssignSerializedObjectReference(visual, "portalShadow", spriteObjects.Shadow);
            AssignSerializedObjectReference(visual, "portalShadowCaster", spriteObjects.ShadowCaster);
            ConfigurePortalVisualProfile(visual, visualProfile);

            InteractableObject interactable = root.GetComponentInChildren<InteractableObject>(true);
            if (interactable != null)
            {
                portal.interactable = interactable;
                interactable.ignorePlayerDirection = true;
                interactable.useDiscreteOutlineColor = false;
                ClearSerializedObjectReference(interactable, "optionalOutline" + "Controller");
                SetSerializedArraySize(interactable, "additionalOutline" + "Controllers", 0);

                List<Object> outlineSpriteObjects = new List<Object>();
                if (itemPortal)
                {
                    // The instant portal has no frame, so the frame-silhouette outline masks
                    // would draw a ghost frame around nothing. The vanilla interact highlight
                    // outlines the center silhouette directly instead — SpriteObjects render
                    // their own outlineColor, which InteractableObject pulses while hovered.
                    if (spriteObjects.CenterEffect != null)
                    {
                        outlineSpriteObjects.Add(spriteObjects.CenterEffect);
                    }
                }
                else
                {
                    if (spriteObjects.OutlineMask != null)
                    {
                        outlineSpriteObjects.Add(spriteObjects.OutlineMask);
                    }

                    if (spriteObjects.OutlineSupportMask != null)
                    {
                        outlineSpriteObjects.Add(spriteObjects.OutlineSupportMask);
                    }
                }

                AssignSerializedObjectReferenceList(
                    interactable,
                    "spriteObjects",
                    outlineSpriteObjects);
            }

            SetSerializedArraySize(portal, "outlineControllers", 0);
            AssignSerializedObjectReferenceList(portal, "spriteObjects", new List<Object>());
        }
    }
}
