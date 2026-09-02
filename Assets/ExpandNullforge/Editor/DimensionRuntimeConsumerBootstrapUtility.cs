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
    internal sealed class DimensionRuntimeConsumerBootstrapResult
    {
        public DimensionRuntimeConsumerBootstrapResult(
            bool executed,
            DimensionRuntimeManifestAsset runtimeManifestAsset,
            string message)
        {
            Executed = executed;
            RuntimeManifestAsset = runtimeManifestAsset;
            Message = message ?? string.Empty;
        }

        public bool Executed { get; private set; }

        public DimensionRuntimeManifestAsset RuntimeManifestAsset { get; private set; }

        public string Message { get; private set; }
    }

    // PARTIAL ON PURPOSE. Every domain the framework grows needs a few lines emitted into the
    // generated bootstrap, and funnelling all of them through this one file made it both huge
    // and a merge bottleneck. A domain now brings its own file — see
    // DimensionRuntimeConsumerBootstrapUtility.<Domain>.cs — and this file keeps the shape of
    // the generated bootstrap plus the ordered list of calls that fills it.
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private const string FrameworkModName = "ExpandNullforge";
        private const string FrameworkAssemblyReference = "ExpandNullforge";
        private const string ApiAssemblyReference = "ExpandNullforge.API";
        private const string PortalEntityTemplatePath = "Assets/ExpandNullforge/Editor/VanillaPortalReference/Prefabs/DimensionPortalEntityTemplate.prefab";
        private const string PortalVisualTemplatePath = "Assets/ExpandNullforge/Editor/VanillaPortalReference/Prefabs/DimensionPortalVisualTemplate.prefab";
        private const string UgcSpriteObjectLitMaterialPath = "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject Lit.mat";
        private const string UgcSpriteObjectUnlitMaterialPath = "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject Unlit.mat";
        private const string PortalBodyMaterialPath = "Assets/ExpandNullforge/VanillaPortalReference/Material/PortalAmplify.mat";
        private const string PortalFloorShadowMaterialPath = "Assets/ExpandNullforge/VanillaPortalReference/Material/FloorShadowAmplify_Tall.mat";
        private const string PortalShadowCasterMaterialPath = "Assets/ExpandNullforge/VanillaPortalReference/Material/ShadowCastingSprite.mat";
        private const string PortalShadowSpritePath = "Assets/ExpandNullforge/VanillaPortalReference/Sprite/portal_shadow.asset";
        private const string PortalShadowCasterSpritePath = "Assets/ExpandNullforge/VanillaPortalReference/Sprite/portal_shadowcaster.asset";
        private const string PortalParticleMaterialPath = "Assets/ExpandNullforge/VanillaPortalReference/Material/ParticleAdd.mat";
        private const string PortalLightningMaterialPath = "Assets/ExpandNullforge/VanillaPortalReference/Material/CoreSpawnLightning.mat";
        private const string PortalBodyTexturePath = "Assets/ExpandNullforge/VanillaPortalReference/Texture2D/portal.png";
        private const string PortalBodySpritePath = "Assets/ExpandNullforge/VanillaPortalReference/Sprite/portal.asset";
        private const string PortalBodySpriteAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalBody.asset";
        private const string PortalChargeProgressSpriteAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalChargeProgress.asset";
        private const string PortalEmissiveWaveSpriteAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalEmissiveWave.asset";
        private const string PortalCenterEffectSpriteAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCenterEffect.asset";
        private const string PortalInventoryIconPath = "Assets/ExpandNullforge/VanillaPortalReference/Sprite/lootsprites_667.asset";
        private const string PortalSmallIconPath = "Assets/ExpandNullforge/VanillaPortalReference/Sprite/lootsprites_668.asset";
        private const string PortalLightObjectName = "PugLight";
        private const long PortalBodySpriteAssetAddressLow = -3868348571319720596L;
        private const long PortalBodySpriteAssetAddressHigh = 8501189908206782088L;
        private const long PortalChargeProgressSpriteAssetAddressLow = 4724424159173606565L;
        private const long PortalChargeProgressSpriteAssetAddressHigh = 2046921296165717419L;
        private const long PortalEmissiveWaveSpriteAssetAddressLow = 7016284293423569344L;
        private const long PortalEmissiveWaveSpriteAssetAddressHigh = 7926038299096422560L;
        private const long PortalCenterEffectSpriteAssetAddressLow = 5570163353262010590L;
        private const long PortalCenterEffectSpriteAssetAddressHigh = -2439472303744231237L;
        private const long PortalCustomSwirlSpriteAssetAddressLow = -6475803387308123471L;
        private const long PortalCustomSwirlSpriteAssetAddressHigh = 7342631739284561011L;
        private static readonly Vector3 PortalSpritePivotPosition =
            new Vector3(1.0f, 0.0f, -0.4375f);
        // The instant portal occupies a single tile, so its visual centers on the entity tile
        // instead of the middle tile of the placed portal's 3-wide footprint.
        private static readonly Vector3 ItemPortalSpritePivotPosition =
            new Vector3(0.0f, 0.0f, -0.4375f);
        private static readonly Color PortalLoadPointEmissiveColor =
            new Color(0.0f, 2.568409f, 3.7735853f, 1.0f);
        private static readonly Color32[] PortalEffectSourcePalette =
        {
            new Color32(20, 43, 92, 255),
            new Color32(20, 62, 171, 255),
            new Color32(22, 93, 217, 255),
            new Color32(24, 133, 216, 255),
            new Color32(25, 189, 198, 255)
        };
        private static readonly Color32[] PortalCenterSourcePalette =
        {
            new Color32(20, 43, 92, 255),
            new Color32(29, 48, 137, 255),
            new Color32(20, 62, 171, 255),
            new Color32(22, 93, 217, 255),
            new Color32(25, 189, 198, 255),
            new Color32(255, 255, 255, 255)
        };
        private const string TextDataBlockScriptGuid = "e853a5af7d19630282ad0af7b5dabadc";
        private const long EnglishLanguageAddressLow = 681352171529052915L;
        private const long EnglishLanguageAddressHigh = 2314507893210082619L;
        private const int CraftedPortalAmount = 1;
        private const int VanillaPortalDamageReduction = 30;
        private const int TravelPreloadSideTiles = 16;

        public static DimensionRuntimeConsumerBootstrapResult EnsureGeneratedRuntime(
            DimensionTemplateAsset template,
            DimensionTemplateManifestExportPreview preview)
        {
            if (template == null)
            {
                return Failure("No Dimension Asset was supplied for runtime generation.");
            }

            if (!preview.ManifestBuilt || !preview.ReadyForManifestExport)
            {
                return Failure(
                    "The Dimension Asset manifest still has blockers. Validate the Export section first. " +
                    DescribeFirstManifestBlocker(preview));
            }

            string templatePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(template));
            if (string.IsNullOrEmpty(templatePath))
            {
                return Failure("Save the Dimension Asset before generating runtime output.");
            }

            string templateFolder = GetFolder(templatePath);
            string modRoot = DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath);
            if (string.IsNullOrEmpty(modRoot))
            {
                return Failure("Could not find the PugMod root that owns " + templatePath + ".");
            }

            if (modRoot == "Assets/ExpandNullforge" || modRoot.StartsWith("Assets/ExpandNullforge/"))
            {
                return Failure("Generated consumer runtime output must be created inside a dimension mod, not the ExpandNullforge framework folder.");
            }

            string dimensionId = string.IsNullOrEmpty(template.DimensionId)
                ? preview.DimensionId
                : template.DimensionId;
            if (string.IsNullOrEmpty(dimensionId))
            {
                return Failure("The Dimension Asset needs a stable dimension id before runtime output can be generated.");
            }

            string modDisplayName = DimensionApiModFolderUtility.ResolveModDisplayNameForAssetFolder(modRoot);
            if (string.IsNullOrEmpty(modDisplayName))
            {
                modDisplayName = GetLastPathSegment(modRoot);
            }

            // The instant item portal's default center artwork (idle/opening/closing) is a
            // framework asset built in editor code; make sure it exists before any profile or
            // generated prefab resolves it.
            DimensionPortalInstantArtworkEditorUtility.EnsureFrameworkCenterAsset(out _);

            DimensionRuntimePortalOutput portalOutput =
                ResolvePortalOutput(template, preview, modDisplayName, dimensionId);

            string generatedFolder = templateFolder + "/Generated";
            string portalFolder = generatedFolder + "/Portal";
            string scriptFolder = modRoot + "/Scripts/Generated";
            DimensionAssetFolders.Ensure(generatedFolder);
            DimensionAssetFolders.Ensure(portalFolder);
            DimensionAssetFolders.Ensure(scriptFolder);

            DimensionRuntimeManifestAsset manifestAsset =
                EnsureRuntimeManifestAsset(template, preview, generatedFolder);

            // The painted map lives on the layout asset, which is authoring truth; the manifest
            // carries a copy so it ships in the bundle and the runtime can register it. This is
            // the whole of the wiring between the Paint tab and the game — everything downstream
            // of SetTileMap was already finished and had no caller.
            //
            // It runs BEFORE the tileMapBounds read below, or the minimum zones and generation
            // passes would be sized to the PREVIOUS export's map.
            if (manifestAsset != null)
            {
                manifestAsset.SetTileMap(
                    template.LayoutTemplate == null ? null : template.LayoutTemplate.PaintedTileMap);
                EditorUtility.SetDirty(manifestAsset);
            }

            // A painted tile map defines the biome's real extent. The compiled starter zone and
            // terrain pass default to the small landing pad, which would clip the painted map at
            // their edges — expand every minimum zone/pass to cover the map.
            DimensionBounds tileMapBounds = default(DimensionBounds);
            if (manifestAsset != null && manifestAsset.HasTileMap)
            {
                tileMapBounds = manifestAsset.TileMap.LocalBounds;
            }

            // The entry portal's access rule rides along so a RequiredItems portal gains its
            // offering window — the slots, their ghosts, and the look each author chose. It is
            // resolved before the prefabs because both halves need it: the visual prefab carries
            // the look table, the entity carries the slots themselves.
            DimensionPortalAccessRuleAsset entryRule = FindPortalRule(
                template,
                DimensionPortalAccessKind.PlacedPortal,
                portalOutput.DimensionId,
                false);

            GameObject visualPrefab =
                EnsurePortalVisualPrefab(portalOutput, portalFolder, modRoot, false, entryRule);
            GameObject itemVisualPrefab =
                EnsurePortalVisualPrefab(portalOutput, portalFolder, modRoot, true, entryRule);
            EnsurePortalEntityPrefab(portalOutput, visualPrefab, portalFolder, PortalEntityVariant.Entry, entryRule);
            EnsurePortalEntityPrefab(portalOutput, visualPrefab, portalFolder, PortalEntityVariant.Return, null);
            EnsurePortalEntityPrefab(portalOutput, itemVisualPrefab, portalFolder, PortalEntityVariant.Item, null);
            EnsureGeneratedBootstrapScript(portalOutput, modDisplayName, scriptFolder, tileMapBounds, template);
            EnsurePortalTextDataBlocks(portalOutput, modRoot);
            EnsurePortalLocalization(portalOutput, modRoot);
            EnsureConsumerAssemblyReferences(modRoot);
            EnsureFrameworkModDependency(templatePath);
            EnsureTheModCanLoadAndBeJoined(templatePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new DimensionRuntimeConsumerBootstrapResult(
                true,
                manifestAsset,
                "Generated runtime manifest, portal prefabs (entry, return, instant item), bootstrap script, text data, and framework dependency for " +
                portalOutput.DimensionDisplayName +
                ".");
        }

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
                // despawn on their own timer. Neither may be broken or picked up (breaking the shared
                // entry object used to hand players a free placed portal).
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

        private struct PortalSpriteObjectSet
        {
            public SpriteObject Body;
            public SpriteRenderer BodyRenderer;
            public SpriteObject ChargeProgress;
            public SpriteObject EmissiveWave;
            public SpriteObject CenterEffect;
            public SpriteObject CustomSwirl;
            public GameObject CenterParticlesRoot;
            public GameObject ReadyFlashRoot;
            public SpriteObject OutlineMask;
            public SpriteObject OutlineSupportMask;
            public SpriteObject OutlineCap;
            public GameObject ShadowRoot;
            public Renderer Shadow;
            public Renderer ShadowCaster;
        }

        private struct GeneratedPortalSpriteAsset
        {
            public long AddressLow;
            public long AddressHigh;
            public SpriteAsset Asset;
        }

        private struct GeneratedPortalOutlineSpriteAssets
        {
            public GeneratedPortalSpriteAsset Main;
            public GeneratedPortalSpriteAsset Support;
            public GeneratedPortalSpriteAsset Cap;
        }

        private struct PortalVisualSpriteAssets
        {
            public GeneratedPortalSpriteAsset Body;
            public SpriteAsset BodySource;
            public Sprite BodyRendererSprite;
            public Material BodyRendererMaterial;
            public GeneratedPortalSpriteAsset ChargeProgress;
            public GeneratedPortalSpriteAsset EmissiveWave;
            public GeneratedPortalSpriteAsset CenterEffect;
            public GeneratedPortalSpriteAsset CustomSwirl;
        }

        private static PortalVisualSpriteAssets ResolvePortalVisualSpriteAssets(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot)
        {
            DimensionPortalVisualProfileAsset profile = portalOutput.VisualProfile;
            SpriteAsset bodyOverride = profile == null
                ? null
                : ResolvePortalSpriteAssetReference(
                    profile.PortalFrameSpriteAsset,
                    "portal frame");
            SpriteAsset bodySource = bodyOverride != null
                ? bodyOverride
                : AssetDatabase.LoadAssetAtPath<SpriteAsset>(PortalBodySpriteAssetPath);
            if (bodySource == null)
            {
                throw new System.InvalidOperationException(
                    "Could not load the framework portal frame SpriteAsset at " +
                    PortalBodySpriteAssetPath + ".");
            }

            GeneratedPortalSpriteAsset body = ResolvePortalSpriteAssetOverride(
                bodyOverride,
                PortalBodySpriteAssetAddressLow,
                PortalBodySpriteAssetAddressHigh,
                modRoot,
                "portal frame");
            GeneratedPortalSpriteAsset customSwirl = ResolvePortalCustomSwirlSpriteAsset(
                profile,
                modRoot);

            // When the profile's center reference is the framework instant center (the item
            // portal's default three-animation contract), treat it as the framework baseline so
            // palette-only recolors bake from the instant sheets instead of the reference being
            // misread as an external override.
            bool instantCenterReference = profile != null &&
                profile.CenterEffectSpriteAsset.hasAddress &&
                profile.CenterEffectSpriteAsset.address.lowBits ==
                    DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressLow &&
                profile.CenterEffectSpriteAsset.address.highBits ==
                    DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressHigh;
            long centerFallbackAddressLow = instantCenterReference
                ? DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressLow
                : PortalCenterEffectSpriteAssetAddressLow;
            long centerFallbackAddressHigh = instantCenterReference
                ? DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressHigh
                : PortalCenterEffectSpriteAssetAddressHigh;
            string centerFallbackAssetPath = instantCenterReference
                ? DimensionPortalInstantArtworkEditorUtility.InstantCenterAssetPath
                : PortalCenterEffectSpriteAssetPath;

            GeneratedPortalSpriteAsset chargeProgress;
            GeneratedPortalSpriteAsset centerEffect;
            if (profile == null)
            {
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalMilestonesPalette");
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalChargeWavePalette");
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalCenterPalette");
                chargeProgress = ResolvePortalSpriteAssetOverride(
                    null,
                    PortalChargeProgressSpriteAssetAddressLow,
                    PortalChargeProgressSpriteAssetAddressHigh,
                    modRoot,
                    "portal milestones");
                centerEffect = ResolvePortalSpriteAssetOverride(
                    null,
                    PortalCenterEffectSpriteAssetAddressLow,
                    PortalCenterEffectSpriteAssetAddressHigh,
                    modRoot,
                    "activated portal center");
            }
            else
            {
                chargeProgress = ResolvePortalPaletteSpriteAsset(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    profile.MilestoneSpriteAsset,
                    PortalChargeProgressSpriteAssetAddressLow,
                    PortalChargeProgressSpriteAssetAddressHigh,
                    PortalChargeProgressSpriteAssetPath,
                    "PortalMilestonesPalette",
                    "milestones",
                    "portal milestones",
                    PortalEffectSourcePalette,
                    new[]
                    {
                        profile.MilestoneDarkColor,
                        profile.MilestoneDeepColor,
                        profile.MilestoneMidColor,
                        profile.MilestoneBrightColor,
                        profile.MilestoneCoreColor
                    });
                centerEffect = ResolvePortalPaletteSpriteAsset(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    profile.CenterEffectSpriteAsset,
                    centerFallbackAddressLow,
                    centerFallbackAddressHigh,
                    centerFallbackAssetPath,
                    "PortalCenterPalette",
                    "center",
                    "activated portal center",
                    PortalCenterSourcePalette,
                    new[]
                    {
                        profile.CenterDarkColor,
                        profile.CenterDeepColor,
                        profile.CenterMidColor,
                        profile.CenterBrightColor,
                        profile.CenterCoreColor,
                        profile.CenterHighlightColor
                    });
            }

            bool frameUsesAuthoredNormal = bodySource.staticSpriteData != null &&
                bodySource.staticSpriteData.normalTexture != null;
            bool customFrameArtwork = profile != null &&
                profile.PortalFrameSpriteAsset.hasAddress &&
                !DimensionPortalArtworkEditorUtility.IsFrameworkReference(
                    profile.PortalFrameSpriteAsset,
                    DimensionPortalArtworkLayer.Frame);
            bool customAnimatedCharge = profile != null &&
                ((profile.ChargeWaveSpriteAsset.hasAddress &&
                  !DimensionPortalArtworkEditorUtility.IsFrameworkReference(
                      profile.ChargeWaveSpriteAsset,
                      DimensionPortalArtworkLayer.ChargeSweep)) ||
                 customFrameArtwork ||
                 RequiresIndependentChargeOverlay(profile) ||
                 frameUsesAuthoredNormal);
            DeleteLegacyIntegratedPortalChargingAssets(
                portalOutput,
                portalFolder,
                modRoot);
            GeneratedPortalSpriteAsset chargeLayer;
            Sprite bodyRendererSprite = null;
            Material bodyRendererMaterial = null;
            if (customAnimatedCharge)
            {
                DeletePortalShaderBodyArtifacts(portalOutput, portalFolder);
                GeneratedPortalSpriteAsset chargeWaveMask = ResolvePortalPaletteSpriteAsset(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    profile.ChargeWaveSpriteAsset,
                    PortalEmissiveWaveSpriteAssetAddressLow,
                    PortalEmissiveWaveSpriteAssetAddressHigh,
                    PortalEmissiveWaveSpriteAssetPath,
                    "PortalChargeWavePalette",
                    "charge-wave",
                    "portal charge sweep",
                    PortalEffectSourcePalette,
                    new[]
                    {
                        profile.ChargeWaveDarkColor,
                        profile.ChargeWaveDeepColor,
                        profile.ChargeWaveMidColor,
                        profile.ChargeWaveBrightColor,
                        profile.ChargeWaveCoreColor
                    });
                // Custom charging artwork is a true overlay. Keeping it separate from
                // the frame is what makes independent offsets, scale, rotation and
                // visibility deterministic in both Portal Studio and the built mod.
                chargeLayer = chargeWaveMask;
            }
            else
            {
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalChargeWavePalette");
                chargeLayer = ResolvePortalSpriteAssetOverride(
                    null,
                    PortalEmissiveWaveSpriteAssetAddressLow,
                    PortalEmissiveWaveSpriteAssetAddressHigh,
                    modRoot,
                    "portal charge sweep");
                Texture2D shaderEmissiveTexture = ResolvePortalShaderEmissiveTexture(
                    portalOutput,
                    portalFolder,
                    bodySource,
                    profile);
                bodyRendererSprite = ResolvePortalBodyRendererSprite(
                    portalOutput,
                    portalFolder,
                    bodyOverride,
                    bodySource);
                bodyRendererMaterial = EnsurePortalBodyRendererMaterial(
                    portalOutput,
                    portalFolder,
                    shaderEmissiveTexture,
                    profile);
            }

            return new PortalVisualSpriteAssets
            {
                Body = body,
                BodySource = bodySource,
                BodyRendererSprite = bodyRendererSprite,
                BodyRendererMaterial = bodyRendererMaterial,
                ChargeProgress = chargeProgress,
                EmissiveWave = chargeLayer,
                CenterEffect = centerEffect,
                CustomSwirl = customSwirl
            };
        }

        private static GeneratedPortalSpriteAsset ResolvePortalCustomSwirlSpriteAsset(
            DimensionPortalVisualProfileAsset profile,
            string modRoot)
        {
            if (profile == null || !profile.CenterSwirlOverrideVanilla)
            {
                return default(GeneratedPortalSpriteAsset);
            }

            if (!profile.CenterSwirlSpriteAsset.hasAddress)
            {
                throw new System.InvalidOperationException(
                    "Custom portal swirl override is enabled, but Artwork override is empty. " +
                    "Assign a looping 48 x 48 SpriteAsset before applying this profile.");
            }

            SpriteAsset configured = ResolvePortalSpriteAssetReference(
                profile.CenterSwirlSpriteAsset,
                "custom portal center swirl");
            if (configured == null)
            {
                throw new System.InvalidOperationException(
                    "Custom portal swirl override is enabled, but its Artwork override " +
                    "address could not be resolved in Scriptable Data.");
            }

            FrameAnimation animation = configured.hasAnimations && configured.animationCount > 0
                ? configured.GetAnimationAt(0)
                : null;
            if (animation == null || animation.spriteData == null || animation.srcFrameCount <= 0)
            {
                throw new System.InvalidOperationException(
                    "The custom portal center swirl SpriteAsset must provide at least one " +
                    "frame in animation 0: " + AssetDatabase.GetAssetPath(configured) + ".");
            }

            if (!animation.loop)
            {
                throw new System.InvalidOperationException(
                    "Animation 0 of the custom portal center swirl SpriteAsset must loop: " +
                    AssetDatabase.GetAssetPath(configured) + ".");
            }

            Texture2D sourceTexture = animation.spriteData.GetSrcTexture();
            int frameCount = animation.srcFrameCount;
            if (sourceTexture == null ||
                frameCount <= 0 ||
                sourceTexture.width % frameCount != 0 ||
                sourceTexture.width / frameCount <= 0 ||
                sourceTexture.width / frameCount >
                    DimensionPortalVisualContract.CanonicalFramePixels ||
                sourceTexture.height > DimensionPortalVisualContract.CanonicalFramePixels)
            {
                throw new System.InvalidOperationException(
                    "Animation 0 of the custom portal center swirl must use horizontal " +
                    "frames that fit within the 48 x 48 portal canvas: " +
                    AssetDatabase.GetAssetPath(configured) + ".");
            }

            return ResolvePortalSpriteAssetOverride(
                configured,
                PortalCustomSwirlSpriteAssetAddressLow,
                PortalCustomSwirlSpriteAssetAddressHigh,
                modRoot,
                "custom portal center swirl");
        }

        private static bool UsesDefaultChargeLayout(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return true;
            }

            return profile.ChargeWaveVisible &&
                profile.ChargeWaveOffsetPixels.sqrMagnitude <= 0.0001f &&
                (profile.ChargeWaveScale - Vector2.one).sqrMagnitude <= 0.0001f &&
                Mathf.Abs(profile.ChargeWaveRotationDegrees) <= 0.0001f &&
                !profile.ChargeWaveFlipX &&
                !profile.ChargeWaveFlipY;
        }

        private static bool RequiresIndependentChargeOverlay(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return false;
            }

            // The vanilla Portal shader draws charging as part of the frame renderer.
            // Any independently-authored pose or hidden frame must therefore use the
            // separate charge SpriteObject that Portal Studio previews.
            return !UsesDefaultChargeLayout(profile) ||
                !profile.FrameVisible ||
                profile.FrameOffsetPixels.sqrMagnitude > 0.0001f ||
                (profile.FrameScale - Vector2.one).sqrMagnitude > 0.0001f ||
                Mathf.Abs(profile.FrameRotationDegrees) > 0.0001f ||
                profile.FrameFlipX ||
                profile.FrameFlipY;
        }

        private static Texture2D ResolvePortalShaderEmissiveTexture(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            SpriteAsset bodySource,
            DimensionPortalVisualProfileAsset profile)
        {
            Texture2D source = bodySource == null || bodySource.staticSpriteData == null
                ? null
                : bodySource.staticSpriteData.emissiveTexture;
            if (source == null)
            {
                SpriteAsset fallback =
                    AssetDatabase.LoadAssetAtPath<SpriteAsset>(PortalBodySpriteAssetPath);
                source = fallback == null || fallback.staticSpriteData == null
                    ? null
                    : fallback.staticSpriteData.emissiveTexture;
            }

            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "The selected portal frame needs an emissive texture for the continuous shader sweep.");
            }

            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalChargeShaderMask",
                "DimensionPortalChargeShaderMask");
            string generatedPath = portalFolder + "/" + assetName + ".png";
            bool useSource = profile == null || PortalPaletteMatchesSource(
                PortalEffectSourcePalette,
                new[]
                {
                    profile.ChargeWaveDarkColor,
                    profile.ChargeWaveDeepColor,
                    profile.ChargeWaveMidColor,
                    profile.ChargeWaveBrightColor,
                    profile.ChargeWaveCoreColor
                });
            if (useSource)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(generatedPath) != null)
                {
                    AssetDatabase.DeleteAsset(generatedPath);
                }

                return source;
            }

            return EnsurePaletteBakedPortalTexture(
                source,
                generatedPath,
                1,
                "portal shader charge mask",
                PortalEffectSourcePalette,
                new[]
                {
                    profile.ChargeWaveDarkColor,
                    profile.ChargeWaveDeepColor,
                    profile.ChargeWaveMidColor,
                    profile.ChargeWaveBrightColor,
                    profile.ChargeWaveCoreColor
                });
        }

        private static Sprite ResolvePortalBodyRendererSprite(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            SpriteAsset bodyOverride,
            SpriteAsset bodySource)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyRendererSprite",
                "DimensionPortalBodyRendererSprite");
            string outputPath = portalFolder + "/" + assetName + ".asset";
            string bodyOverridePath = NormalizeAssetPath(
                bodyOverride == null ? string.Empty : AssetDatabase.GetAssetPath(bodyOverride));
            if (bodyOverride == null ||
                string.Equals(
                    bodyOverridePath,
                    PortalBodySpriteAssetPath,
                    System.StringComparison.Ordinal))
            {
                if (AssetDatabase.LoadAssetAtPath<Sprite>(outputPath) != null)
                {
                    AssetDatabase.DeleteAsset(outputPath);
                }

                return RequirePortalSprite(PortalBodySpritePath);
            }

            Texture2D texture = bodySource == null || bodySource.staticSpriteData == null
                ? null
                : bodySource.staticSpriteData.texture;
            if (texture == null)
            {
                throw new System.InvalidOperationException(
                    "The selected portal frame SpriteAsset needs a static source texture.");
            }

            Vector2 pivot = new Vector2(
                bodySource.staticSpriteData.pivot.x,
                bodySource.staticSpriteData.pivot.y);
            Sprite staged = Sprite.Create(
                texture,
                new Rect(0.0f, 0.0f, texture.width, texture.height),
                pivot,
                16.0f,
                1,
                SpriteMeshType.FullRect);
            staged.name = assetName;
            Sprite generated = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (generated == null)
            {
                AssetDatabase.CreateAsset(staged, outputPath);
                return AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            }

            try
            {
                EditorUtility.CopySerialized(staged, generated);
                generated.name = assetName;
                EditorUtility.SetDirty(generated);
                AssetDatabase.SaveAssetIfDirty(generated);
                return generated;
            }
            finally
            {
                Object.DestroyImmediate(staged);
            }
        }

        private static void DeletePortalShaderBodyArtifacts(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder)
        {
            string spriteName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyRendererSprite",
                "DimensionPortalBodyRendererSprite");
            string materialName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyShader",
                "DimensionPortalBodyShader");
            string maskName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalChargeShaderMask",
                "DimensionPortalChargeShaderMask");
            DeleteGeneratedPortalAssetIfPresent(portalFolder + "/" + spriteName + ".asset");
            DeleteGeneratedPortalAssetIfPresent(portalFolder + "/" + materialName + ".mat");
            DeleteGeneratedPortalAssetIfPresent(portalFolder + "/" + maskName + ".png");
        }

        private static void DeleteGeneratedPortalAssetIfPresent(string assetPath)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static Material EnsurePortalBodyRendererMaterial(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            Texture2D emissiveTexture,
            DimensionPortalVisualProfileAsset profile)
        {
            Material source = RequirePortalMaterial(PortalBodyMaterialPath);
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyShader",
                "DimensionPortalBodyShader");
            string outputPath = portalFolder + "/" + assetName + ".mat";
            Material generated = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            if (generated == null)
            {
                generated = new Material(source);
                generated.name = assetName;
                AssetDatabase.CreateAsset(generated, outputPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, generated);
                generated.name = assetName;
            }

            generated.SetTexture("_EmissiveTex", emissiveTexture);
            generated.SetFloat("_loadingMul", 1.0f);
            generated.SetFloat("_emissiveStrengthMul", 0.0f);
            generated.SetFloat("_activeStrength", 5.0f);
            generated.SetFloat("_activeHeight", 0.9f);
            generated.SetFloat(
                "_loadingSpeed",
                profile == null ? 1.0f : profile.ChargeWaveSpeed);
            generated.SetColor(
                "_emissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                    : profile.ChargeWaveEmissiveColor);
            EditorUtility.SetDirty(generated);
            return generated;
        }

        private static void DeleteLegacyIntegratedPortalChargingAssets(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalChargingBody",
                "DimensionPortalChargingBody");
            string assetPath = portalFolder + "/" + assetName + ".asset";
            SpriteAsset generated = AssetDatabase.LoadAssetAtPath<SpriteAsset>(assetPath);
            if (generated != null)
            {
                RemoveSpriteAssetManifestReference(modRoot, generated);
                AssetDatabase.DeleteAsset(assetPath);
            }

            string bodyPath = portalFolder + "/" + assetName + "Body.png";
            string emissivePath = portalFolder + "/" + assetName + "Emissive.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(bodyPath) != null)
            {
                AssetDatabase.DeleteAsset(bodyPath);
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(emissivePath) != null)
            {
                AssetDatabase.DeleteAsset(emissivePath);
            }
        }

        private static GeneratedPortalSpriteAsset ResolvePortalPaletteSpriteAsset(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            DataBlockRef<SpriteAsset> overrideReference,
            long fallbackAddressLow,
            long fallbackAddressHigh,
            string sourceSpriteAssetPath,
            string assetSuffix,
            string layerKey,
            string label,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            bool frameworkReference =
                overrideReference.hasAddress &&
                overrideReference.address.lowBits == fallbackAddressLow &&
                overrideReference.address.highBits == fallbackAddressHigh;
            if (overrideReference.hasAddress && !frameworkReference)
            {
                SpriteAsset overrideAsset = ResolvePortalSpriteAssetReference(
                    overrideReference,
                    label);
                string generatedAssetPath = GetPaletteBakedPortalSpriteAssetPath(
                    portalOutput,
                    portalFolder,
                    assetSuffix);
                string overrideAssetPath = NormalizeAssetPath(
                    AssetDatabase.GetAssetPath(overrideAsset));
                string normalizedPortalFolder = NormalizeAssetPath(portalFolder);
                if (overrideAssetPath == generatedAssetPath ||
                    overrideAssetPath.StartsWith(
                        normalizedPortalFolder + "/",
                        System.StringComparison.Ordinal))
                {
                    throw new System.InvalidOperationException(
                        "The configured " + label +
                        " override points at a generator-owned portal asset. " +
                        "Choose an authored SpriteAsset outside the Generated/Portal folder instead.");
                }

                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    assetSuffix);
                return ResolvePortalSpriteAssetOverride(
                    overrideAsset,
                    fallbackAddressLow,
                    fallbackAddressHigh,
                    modRoot,
                    label);
            }

            if (PortalPaletteMatchesSource(sourcePalette, targetPalette))
            {
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    assetSuffix);
                return new GeneratedPortalSpriteAsset
                {
                    AddressLow = fallbackAddressLow,
                    AddressHigh = fallbackAddressHigh,
                    Asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(sourceSpriteAssetPath)
                };
            }

            return EnsurePaletteBakedPortalSpriteAsset(
                portalOutput,
                portalFolder,
                modRoot,
                sourceSpriteAssetPath,
                assetSuffix,
                layerKey,
                label,
                sourcePalette,
                targetPalette);
        }

        private static string GetPaletteBakedPortalSpriteAssetPath(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string assetSuffix)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + assetSuffix,
                "Dimension" + assetSuffix);
            return portalFolder + "/" + assetName + ".asset";
        }

        private static void DeletePaletteBakedPortalSpriteAssetIfPresent(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            string assetSuffix)
        {
            string outputAssetPath = GetPaletteBakedPortalSpriteAssetPath(
                portalOutput,
                portalFolder,
                assetSuffix);
            string assetName = Path.GetFileNameWithoutExtension(outputAssetPath);
            SpriteAsset generated =
                AssetDatabase.LoadAssetAtPath<SpriteAsset>(outputAssetPath);
            if (generated != null)
            {
                RemoveSpriteAssetManifestReference(modRoot, generated);
                AssetDatabase.DeleteAsset(outputAssetPath);
            }

            if (!AssetDatabase.IsValidFolder(portalFolder))
            {
                return;
            }

            string texturePrefix = assetName + "Anim";
            string[] textureGuids = AssetDatabase.FindAssets(
                texturePrefix + " t:Texture2D",
                new[] { portalFolder });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string texturePath = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(textureGuids[i]));
                if (!texturePath.StartsWith(portalFolder + "/", System.StringComparison.Ordinal) ||
                    !texturePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileNameWithoutExtension(texturePath).StartsWith(
                        texturePrefix,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(texturePath);
            }
        }

        private static bool PortalPaletteMatchesSource(
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            if (sourcePalette == null ||
                targetPalette == null ||
                sourcePalette.Length == 0 ||
                sourcePalette.Length != targetPalette.Length)
            {
                return false;
            }

            for (int i = 0; i < sourcePalette.Length; i++)
            {
                Color32 target = targetPalette[i];
                Color32 source = sourcePalette[i];
                if (source.r != target.r ||
                    source.g != target.g ||
                    source.b != target.b ||
                    source.a != target.a)
                {
                    return false;
                }
            }

            return true;
        }

        private static SpriteAsset ResolvePortalSpriteAssetReference(
            DataBlockRef<SpriteAsset> reference,
            string label)
        {
            if (!reference.hasAddress)
            {
                return null;
            }

            SpriteAsset resolved;
            if (reference.TryGet(out resolved) &&
                resolved != null &&
                !IsGeneratorOwnedPortalSpriteAsset(resolved))
            {
                return resolved;
            }

            SpriteAsset generatedFallback = resolved;
            string[] candidateGuids = AssetDatabase.FindAssets(
                "t:SpriteAsset",
                new[] { "Assets" });
            for (int i = 0; i < candidateGuids.Length; i++)
            {
                string candidatePath = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(candidateGuids[i]));
                SpriteAsset candidate =
                    AssetDatabase.LoadAssetAtPath<SpriteAsset>(candidatePath);
                if (candidate != null && candidate.address == reference.address)
                {
                    if (!IsGeneratorOwnedPortalSpriteAsset(candidate))
                    {
                        return candidate;
                    }

                    if (generatedFallback == null)
                    {
                        generatedFallback = candidate;
                    }
                }
            }

            if (generatedFallback != null)
            {
                return generatedFallback;
            }

            throw new System.InvalidOperationException(
                "Could not resolve the configured " + label +
                " SpriteAsset data-block reference at address " +
                reference.address +
                ". Make sure the SpriteAsset is saved inside the target dimension mod or ExpandNullforge.");
        }

        private static bool IsGeneratorOwnedPortalSpriteAsset(SpriteAsset asset)
        {
            string path = asset == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            return path.IndexOf(
                "/Generated/Portal/",
                System.StringComparison.Ordinal) >= 0;
        }

        private static GeneratedPortalSpriteAsset EnsurePaletteBakedPortalSpriteAsset(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            string sourceSpriteAssetPath,
            string assetSuffix,
            string layerKey,
            string label,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            SpriteAsset source = AssetDatabase.LoadAssetAtPath<SpriteAsset>(sourceSpriteAssetPath);
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "Could not load the framework " + label + " SpriteAsset at " +
                    sourceSpriteAssetPath + ".");
            }

            if (sourcePalette == null ||
                targetPalette == null ||
                sourcePalette.Length == 0 ||
                sourcePalette.Length != targetPalette.Length)
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " palette is incomplete.");
            }

            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + assetSuffix,
                "Dimension" + assetSuffix);
            string outputAssetPath = portalFolder + "/" + assetName + ".asset";
            string addressSeed =
                portalOutput.PortalObjectName + ":generated-portal-palette:" + layerKey;
            long addressLow = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x70616C657474656CUL);
            long addressHigh = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x706F7274616C7669UL);

            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceAnimations = serializedSource.FindProperty("m_animations");
            if (sourceAnimations == null || sourceAnimations.arraySize == 0)
            {
                throw new System.InvalidOperationException(
                    "The framework " + label + " SpriteAsset does not contain animations.");
            }

            Texture2D[] bakedTextures = new Texture2D[sourceAnimations.arraySize];
            Texture2D[] bakedEmissiveTextures = new Texture2D[sourceAnimations.arraySize];
            for (int i = 0; i < sourceAnimations.arraySize; i++)
            {
                SerializedProperty sourceAnimation = sourceAnimations.GetArrayElementAtIndex(i);
                SerializedProperty frameCountProperty =
                    sourceAnimation.FindPropertyRelative("srcFrameCount");
                int frameCount = frameCountProperty == null
                    ? 0
                    : frameCountProperty.intValue;
                SerializedProperty sourceSpriteData =
                    sourceAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty sourceTextureProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("texture");
                SerializedProperty sourceEmissiveTextureProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("emissiveTexture");
                Texture2D sourceTexture = sourceTextureProperty == null
                    ? null
                    : sourceTextureProperty.objectReferenceValue as Texture2D;
                Texture2D sourceEmissiveTexture = sourceEmissiveTextureProperty == null
                    ? null
                    : sourceEmissiveTextureProperty.objectReferenceValue as Texture2D;
                if (sourceTexture == null || frameCount <= 0)
                {
                    throw new System.InvalidOperationException(
                        "Animation " + i + " in the framework " + label +
                        " SpriteAsset has no valid source texture or frame count.");
                }

                string animationStem = assetName + "Anim" + i.ToString(CultureInfo.InvariantCulture);
                bakedTextures[i] = EnsurePaletteBakedPortalTexture(
                    sourceTexture,
                    portalFolder + "/" + animationStem + ".png",
                    frameCount,
                    label,
                    sourcePalette,
                    targetPalette);
                if (sourceEmissiveTexture == null)
                {
                    bakedEmissiveTextures[i] = null;
                }
                else if (sourceEmissiveTexture == sourceTexture)
                {
                    bakedEmissiveTextures[i] = bakedTextures[i];
                }
                else
                {
                    bakedEmissiveTextures[i] = EnsurePaletteBakedPortalTexture(
                        sourceEmissiveTexture,
                        portalFolder + "/" + animationStem + "Emissive.png",
                        frameCount,
                        label + " emissive",
                        sourcePalette,
                        targetPalette);
                }
            }

            SpriteAsset generated = AssetDatabase.LoadAssetAtPath<SpriteAsset>(outputAssetPath);
            if (generated == null)
            {
                generated = Object.Instantiate(source);
                generated.name = assetName;
                AssetDatabase.CreateAsset(generated, outputAssetPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, generated);
                generated.name = assetName;
            }

            SerializedObject serializedGenerated = new SerializedObject(generated);
            serializedGenerated.Update();
            SetSerializedLong(serializedGenerated, "m_address.m_low", addressLow);
            SetSerializedLong(serializedGenerated, "m_address.m_high", addressHigh);
            SerializedProperty generatedAnimations = serializedGenerated.FindProperty("m_animations");
            if (generatedAnimations == null || generatedAnimations.arraySize != bakedTextures.Length)
            {
                throw new System.InvalidOperationException(
                    "Could not preserve the animation layout while generating the " + label +
                    " palette SpriteAsset.");
            }

            for (int i = 0; i < generatedAnimations.arraySize; i++)
            {
                SerializedProperty generatedAnimation = generatedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty generatedSpriteData =
                    generatedAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty generatedTextureProperty = generatedSpriteData == null
                    ? null
                    : generatedSpriteData.FindPropertyRelative("texture");
                SerializedProperty generatedEmissiveTextureProperty = generatedSpriteData == null
                    ? null
                    : generatedSpriteData.FindPropertyRelative("emissiveTexture");
                if (generatedTextureProperty == null || generatedEmissiveTextureProperty == null)
                {
                    throw new System.InvalidOperationException(
                        "Could not assign generated textures to animation " + i +
                        " in the " + label + " SpriteAsset.");
                }

                generatedTextureProperty.objectReferenceValue = bakedTextures[i];
                generatedEmissiveTextureProperty.objectReferenceValue = bakedEmissiveTextures[i];
            }

            serializedGenerated.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generated);
            EnsureSpriteAssetManifestContains(modRoot, outputAssetPath);
            return new GeneratedPortalSpriteAsset
            {
                AddressLow = addressLow,
                AddressHigh = addressHigh,
                Asset = generated
            };
        }

        private static Texture2D EnsurePaletteBakedPortalTexture(
            Texture2D source,
            string targetTexturePath,
            int frameCount,
            string label,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            string sourceTexturePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            string sourceAbsolutePath = AssetPathToAbsolutePath(sourceTexturePath);
            if (string.IsNullOrEmpty(sourceTexturePath) ||
                !sourceTexturePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(sourceAbsolutePath) ||
                !File.Exists(sourceAbsolutePath))
            {
                throw new System.InvalidOperationException(
                    "The " + label + " palette source must be a saved PNG texture.");
            }

            Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D targetTexture = null;
            try
            {
                if (!sourceTexture.LoadImage(File.ReadAllBytes(sourceAbsolutePath)))
                {
                    throw new System.InvalidOperationException(
                        "Could not decode the " + label + " palette source at " +
                        sourceTexturePath + ".");
                }

                int nativeFrameWidth = frameCount <= 0
                    ? 0
                    : sourceTexture.width / frameCount;
                if (frameCount <= 0 ||
                    sourceTexture.width <= 0 ||
                    sourceTexture.height <= 0 ||
                    sourceTexture.width % frameCount != 0 ||
                    nativeFrameWidth <= 0)
                {
                    throw new System.InvalidOperationException(
                        "The " + label + " animation must be a horizontal strip of evenly sized frames. " +
                        "Current texture: " + sourceTexture.width + " x " +
                        sourceTexture.height + ", frames: " + frameCount + ".");
                }

                Color32[] sourcePixels = sourceTexture.GetPixels32();
                Color32[] targetPixels = new Color32[sourcePixels.Length];
                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color32 sourcePixel = sourcePixels[i];
                    if (sourcePixel.a == 0)
                    {
                        targetPixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    int paletteIndex = FindClosestPortalPaletteIndex(
                        sourcePixel,
                        sourcePalette);
                    Color targetColor = targetPalette[paletteIndex];
                    Color32 targetPixel = targetColor;
                    targetPixel.a = (byte)Mathf.Clamp(
                        Mathf.RoundToInt(sourcePixel.a * Mathf.Clamp01(targetColor.a)),
                        0,
                        255);
                    targetPixels[i] = targetPixel;
                }

                targetTexture = new Texture2D(
                    sourceTexture.width,
                    sourceTexture.height,
                    TextureFormat.RGBA32,
                    false);
                targetTexture.SetPixels32(targetPixels);
                targetTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(targetTexturePath, targetTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetTexturePath,
                    sourceTexture.width,
                    sourceTexture.height);
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
                if (targetTexture != null)
                {
                    Object.DestroyImmediate(targetTexture);
                }
            }

            Texture2D generated = AssetDatabase.LoadAssetAtPath<Texture2D>(targetTexturePath);
            if (generated == null)
            {
                throw new System.InvalidOperationException(
                    "Could not import the generated " + label + " texture at " +
                    targetTexturePath + ".");
            }

            return generated;
        }

        private static int FindClosestPortalPaletteIndex(
            Color32 color,
            Color32[] palette)
        {
            int closestIndex = 0;
            int closestDistance = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int red = color.r - palette[i].r;
                int green = color.g - palette[i].g;
                int blue = color.b - palette[i].b;
                int distance = red * red + green * green + blue * blue;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private static GeneratedPortalSpriteAsset ResolvePortalSpriteAssetOverride(
            SpriteAsset overrideAsset,
            long fallbackAddressLow,
            long fallbackAddressHigh,
            string modRoot,
            string label)
        {
            if (overrideAsset == null)
            {
                return new GeneratedPortalSpriteAsset
                {
                    AddressLow = fallbackAddressLow,
                    AddressHigh = fallbackAddressHigh,
                    Asset = null
                };
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(overrideAsset));
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " SpriteAsset has not been saved as a project asset.");
            }

            bool frameworkAsset =
                assetPath == "Assets/ExpandNullforge" ||
                assetPath.StartsWith("Assets/ExpandNullforge/");
            bool consumerAsset =
                assetPath == modRoot ||
                assetPath.StartsWith(modRoot + "/");
            if (!frameworkAsset && !consumerAsset)
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " SpriteAsset must belong to the target dimension mod or ExpandNullforge. " +
                    "Current asset: " + assetPath + ".");
            }

            SerializedObject serializedAsset = new SerializedObject(overrideAsset);
            serializedAsset.Update();
            SerializedProperty address = serializedAsset.FindProperty("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null || (low.longValue == 0L && high.longValue == 0L))
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " SpriteAsset needs a non-zero SpriteAsset address before portal generation: " +
                    assetPath + ".");
            }

            EnsureSpriteAssetManifestContains(
                frameworkAsset ? "Assets/ExpandNullforge" : modRoot,
                assetPath);

            return new GeneratedPortalSpriteAsset
            {
                AddressLow = low.longValue,
                AddressHigh = high.longValue,
                Asset = overrideAsset
            };
        }

        private static string ResolvePortalFrameTexturePath(SpriteAsset frameAsset)
        {
            if (frameAsset == null)
            {
                return PortalBodyTexturePath;
            }

            Texture2D texture = frameAsset.staticSpriteData == null
                ? null
                : frameAsset.staticSpriteData.texture;
            string texturePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
            if (texture == null || string.IsNullOrEmpty(texturePath))
            {
                throw new System.InvalidOperationException(
                    "The configured portal frame SpriteAsset needs a static source texture so the interaction outline can be generated.");
            }

            if (!texturePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.InvalidOperationException(
                    "The configured portal frame texture must be a PNG so the generated outline can use its alpha silhouette: " +
                    texturePath + ".");
            }

            return texturePath;
        }

        private static void ConfigurePortalVisualProfile(
            DimensionPortalVisual visual,
            DimensionPortalVisualProfileAsset profile)
        {
            if (visual == null)
            {
                return;
            }

            SerializedObject serializedVisual = new SerializedObject(visual);
            serializedVisual.Update();
            SetSerializedBool(
                serializedVisual,
                "portalBodyVisible",
                profile == null || profile.FrameVisible);
            SetSerializedBool(
                serializedVisual,
                "chargeWaveVisible",
                profile == null || profile.ChargeWaveVisible);
            SetSerializedBool(
                serializedVisual,
                "milestoneVisible",
                profile == null || profile.MilestonesVisible);
            SetSerializedBool(
                serializedVisual,
                "centerVisible",
                profile == null || profile.CenterVisible);
            SetSerializedBool(
                serializedVisual,
                "centerParticlesVisible",
                profile == null || profile.CenterSwirlVisible);
            SetSerializedFloat(
                serializedVisual,
                "customSwirlPlaybackSpeed",
                profile == null ? 1.0f : profile.CenterSwirlPlaybackSpeed);
            SetSerializedBool(
                serializedVisual,
                "projectedShadowVisible",
                profile == null || profile.PortalShadowEnabled);
            SetSerializedColor(
                serializedVisual,
                "portalBodyColor",
                profile == null ? Color.white : profile.FrameTint);
            SetSerializedColor(
                serializedVisual,
                "portalBodyEmissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                    : profile.FrameEmissiveColor);
            SetSerializedColor(
                serializedVisual,
                "chargeWaveColor",
                profile == null ? Color.white : profile.ChargeWaveTint);
            SetSerializedColor(
                serializedVisual,
                "chargeWaveEmissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                    : profile.ChargeWaveEmissiveColor);
            SetSerializedFloat(
                serializedVisual,
                "chargeWaveSpeed",
                profile == null ? 1.0f : profile.ChargeWaveSpeed);
            SetSerializedColor(
                serializedVisual,
                "milestoneColor",
                profile == null ? Color.white : profile.MilestoneTint);
            SetSerializedColor(
                serializedVisual,
                "milestoneEmissiveColor",
                profile == null
                    ? PortalLoadPointEmissiveColor
                    : profile.MilestoneEmissiveColor);
            SetSerializedFloat(
                serializedVisual,
                "firstMilestone",
                profile == null ? 0.25f : profile.FirstMilestone);
            SetSerializedFloat(
                serializedVisual,
                "secondMilestone",
                profile == null ? 0.5f : profile.SecondMilestone);
            SetSerializedFloat(
                serializedVisual,
                "thirdMilestone",
                profile == null ? 0.75f : profile.ThirdMilestone);
            // Milestone stage->frame mapping is fixed in DimensionPortalVisual (vanilla sheet
            // order); nothing to bake for it.
            SetSerializedColor(
                serializedVisual,
                "centerColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaCenterTint
                    : profile.CenterTint);
            SetSerializedColor(
                serializedVisual,
                "centerEmissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaCenterEmissiveColor
                    : profile.CenterEmissiveColor);
            SetSerializedFloat(
                serializedVisual,
                "centerGlowIntensity",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaCenterGlowIntensity
                    : profile.CenterGlowIntensity);
            SetSerializedInt(
                serializedVisual,
                "centerIdleAnimationIndex",
                profile == null ? 0 : profile.CenterIdleAnimationIndex);
            SetSerializedInt(
                serializedVisual,
                "centerOpeningAnimationIndex",
                profile == null ? 1 : profile.CenterOpeningAnimationIndex);
            SetSerializedBool(
                serializedVisual,
                "playReadyFlash",
                profile == null || profile.PlayReadyFlash);
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color MultiplyPortalColors(Color left, Color right)
        {
            return new Color(
                left.r * right.r,
                left.g * right.g,
                left.b * right.b,
                left.a * right.a);
        }

        private static Color ScalePortalColor(Color color, float scale)
        {
            return new Color(
                color.r * scale,
                color.g * scale,
                color.b * scale,
                color.a);
        }

        private static Transform EnsurePortalXScaler(GameObject root)
        {
            Transform xScaler = FindDescendantTransform(root.transform, "XScaler");
            if (xScaler == null)
            {
                xScaler = new GameObject("XScaler").transform;
                xScaler.SetParent(root.transform, false);
            }

            xScaler.localPosition = Vector3.zero;
            xScaler.localRotation = Quaternion.identity;
            xScaler.localScale = Vector3.one;
            xScaler.gameObject.layer = root.layer;
            return xScaler;
        }

        private static PortalSpriteObjectSet EnsurePortalSpriteObjectHierarchy(
            GameObject root,
            GeneratedPortalOutlineSpriteAssets outlineMaskAssets,
            PortalVisualSpriteAssets visualAssets,
            DimensionPortalVisualProfileAsset visualProfile,
            bool itemPortal)
        {
            Transform xScaler = EnsurePortalXScaler(root);
            Transform animPositionRotation = EnsureChild(xScaler, "AnimPositionRotation");
            animPositionRotation.localPosition = Vector3.zero;
            animPositionRotation.localRotation = Quaternion.identity;
            animPositionRotation.localScale = Vector3.one;
            animPositionRotation.gameObject.layer = root.layer;

            Transform animScale = EnsureChild(animPositionRotation, "AnimScale");
            animScale.localPosition = Vector3.zero;
            animScale.localRotation = Quaternion.identity;
            animScale.localScale = Vector3.one;
            animScale.gameObject.layer = root.layer;

            Transform spritePivot = EnsureChild(animScale, "SRPivot");
            spritePivot.localPosition = itemPortal
                ? ItemPortalSpritePivotPosition
                : PortalSpritePivotPosition;
            spritePivot.localRotation = Quaternion.identity;
            spritePivot.localScale = Vector3.one;
            spritePivot.gameObject.layer = root.layer;

            Transform spriteRoot = EnsureChild(spritePivot, "PortalSpriteObjects");
            spriteRoot.localPosition = Vector3.zero;
            spriteRoot.localRotation = Quaternion.identity;
            spriteRoot.localScale = Vector3.one;
            spriteRoot.gameObject.layer = root.layer;
            Transform shadowRoot = EnsureChild(spriteRoot, "PortalShadowGroup");
            Vector2 shadowOffset = visualProfile == null
                ? Vector2.zero
                : visualProfile.PortalShadowOffsetPixels;
            shadowRoot.localPosition = new Vector3(
                -0.5f + shadowOffset.x / DimensionPortalVisualContract.PixelsPerUnit,
                0.0f,
                0.8125f + shadowOffset.y / DimensionPortalVisualContract.PixelsPerUnit);
            shadowRoot.localRotation = Quaternion.identity;
            shadowRoot.localScale = Vector3.one;
            shadowRoot.gameObject.layer = root.layer;

            Material litMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(UgcSpriteObjectLitMaterialPath);
            Material unlitMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(UgcSpriteObjectUnlitMaterialPath);
            Material floorShadowMaterial =
                RequirePortalMaterial(PortalFloorShadowMaterialPath);
            Material shadowCasterMaterial =
                RequirePortalMaterial(PortalShadowCasterMaterialPath);
            Material outlineMaterial = unlitMaterial != null ? unlitMaterial : litMaterial;
            bool customShadowSprite =
                visualProfile != null && visualProfile.PortalShadowSprite != null;
            bool customShadowCasterSprite =
                visualProfile != null && visualProfile.PortalShadowCasterSprite != null;
            Sprite shadowSprite = customShadowSprite
                ? visualProfile.PortalShadowSprite
                : RequirePortalSprite(PortalShadowSpritePath);
            Sprite shadowCasterSprite = customShadowCasterSprite
                ? visualProfile.PortalShadowCasterSprite
                : RequirePortalSprite(PortalShadowCasterSpritePath);

            Vector3 bodyPosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Frame,
                visualProfile == null ? Vector2.zero : visualProfile.FrameOffsetPixels);
            Quaternion bodyRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.FrameRotationDegrees);
            Vector3 bodyScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.FrameScale,
                visualProfile != null && visualProfile.FrameFlipX,
                visualProfile != null && visualProfile.FrameFlipY);
            Vector3 chargePosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Milestones,
                visualProfile == null ? Vector2.zero : visualProfile.MilestoneOffsetPixels);
            Quaternion chargeRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.MilestoneRotationDegrees);
            Vector3 chargeScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.MilestoneScale,
                visualProfile != null && visualProfile.MilestoneFlipX,
                visualProfile != null && visualProfile.MilestoneFlipY);
            Vector3 wavePosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.ChargeSweep,
                visualProfile == null ? Vector2.zero : visualProfile.ChargeWaveOffsetPixels);
            Quaternion waveRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.ChargeWaveRotationDegrees);
            Vector3 waveScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.ChargeWaveScale,
                visualProfile != null && visualProfile.ChargeWaveFlipX,
                visualProfile != null && visualProfile.ChargeWaveFlipY);
            Vector3 centerPosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Center,
                visualProfile == null ? Vector2.zero : visualProfile.CenterOffsetPixels);
            Quaternion centerRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.CenterRotationDegrees);
            Vector3 centerScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.CenterScale,
                visualProfile != null && visualProfile.CenterFlipX,
                visualProfile != null && visualProfile.CenterFlipY);
            bool customSwirlEnabled = visualProfile != null &&
                visualProfile.CenterSwirlOverrideVanilla &&
                visualProfile.CenterSwirlVisible;
            // Anchor the swirl to the activated center/aperture (not the outer frame) so a
            // full-canvas sheet whose flecks are drawn about its own centre lands exactly in
            // the inner circle with no manual offset. CenterParticleOffsetPixels then nudges
            // from that natural anchor.
            Vector3 customSwirlPosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Center,
                visualProfile == null
                    ? Vector2.zero
                    : visualProfile.CenterParticleOffsetPixels);
            // Place the artwork just behind the activated ring at the same projected point.
            const float customSwirlDepthInset = 0.0002f;
            float customSwirlTargetDepth =
                DimensionPortalVisualContract.CenterLocalPosition.z +
                customSwirlDepthInset;
            float customSwirlDepthDelta =
                customSwirlTargetDepth - customSwirlPosition.z;
            customSwirlPosition.y -= customSwirlDepthDelta;
            customSwirlPosition.z += customSwirlDepthDelta;
            Quaternion customSwirlRotation =
                DimensionPortalVisualContract.GetLocalRotation(
                    visualProfile == null
                        ? 0.0f
                        : visualProfile.CenterParticleRotationDegrees);
            Vector3 customSwirlScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null
                    ? Vector2.one
                    : visualProfile.CenterParticleScale,
                visualProfile != null && visualProfile.CenterSwirlFlipX,
                visualProfile != null && visualProfile.CenterSwirlFlipY);
            // The swirl color is baked into the profile-owned sheet, so the SpriteObject is
            // drawn with a neutral tint. Only the emissive glow hue and intensity remain
            // runtime material properties (the game shader adds emissiveColor * pixels).
            Color customSwirlTint = Color.white;
            Color customSwirlEmission = visualProfile == null
                ? Color.white
                : ScalePortalColor(
                    visualProfile.CenterSwirlEmissiveColor,
                    visualProfile.CenterParticleEmissionMultiplier);
            Transform staleCustomSwirl = spriteRoot.Find("PortalCustomSwirlSO");
            if (!customSwirlEnabled && staleCustomSwirl != null)
            {
                Object.DestroyImmediate(staleCustomSwirl.gameObject, true);
            }
            Vector3 outlinePosition = bodyPosition +
                (DimensionPortalVisualContract.OutlineLocalPosition -
                 DimensionPortalVisualContract.FrameLocalPosition);
            Vector3 shadowPosition = new Vector3(0.5f, 0.0625f, -0.5f);
            Vector3 shadowCasterPosition = new Vector3(0.5f, 2.099f, -0.5f);
            Vector2 shadowScale2D = visualProfile == null
                ? Vector2.one
                : visualProfile.PortalShadowScale;
            Vector3 shadowScale = new Vector3(
                visualProfile != null && visualProfile.PortalShadowFlipX
                    ? -shadowScale2D.x
                    : shadowScale2D.x,
                visualProfile != null && visualProfile.PortalShadowFlipY
                    ? -shadowScale2D.y
                    : shadowScale2D.y,
                1.0f);
            Quaternion shadowRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f) *
                DimensionPortalVisualContract.GetLocalRotation(
                    visualProfile == null
                        ? 0.0f
                        : visualProfile.PortalShadowRotationDegrees);
            bool shadowVisible = visualProfile == null || visualProfile.PortalShadowEnabled;

            PortalSpriteObjectSet result = new PortalSpriteObjectSet
            {
                Body = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalBodySO",
                    visualAssets.Body.AddressLow,
                    visualAssets.Body.AddressHigh,
                    litMaterial,
                    bodyPosition,
                    bodyRotation,
                    bodyScale,
                    visualProfile == null ? Color.white : visualProfile.FrameTint,
                    ScalePortalColor(
                        MultiplyPortalColors(
                            visualProfile == null
                                ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                                : visualProfile.FrameEmissiveColor,
                            visualProfile == null ? Color.white : visualProfile.FrameTint),
                        visualAssets.BodyRendererSprite == null ? 0.0f : 5.0f),
                    (visualProfile == null || visualProfile.FrameVisible) &&
                    visualAssets.BodyRendererSprite == null),
                BodyRenderer = visualAssets.BodyRendererSprite == null ||
                    visualAssets.BodyRendererMaterial == null
                    ? null
                    : EnsurePortalBodyRenderer(
                        spriteRoot,
                        "PortalBodyRenderer",
                        visualAssets.BodyRendererSprite,
                        visualAssets.BodyRendererMaterial,
                        bodyPosition,
                        bodyRotation,
                        bodyScale,
                        visualProfile == null || visualProfile.FrameVisible),
                ChargeProgress = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalChargeProgressSO",
                    visualAssets.ChargeProgress.AddressLow,
                    visualAssets.ChargeProgress.AddressHigh,
                    litMaterial,
                    chargePosition,
                    chargeRotation,
                    chargeScale,
                    visualProfile == null ? Color.white : visualProfile.MilestoneTint,
                    visualProfile == null
                        ? PortalLoadPointEmissiveColor
                        : visualProfile.MilestoneEmissiveColor,
                    false),
                EmissiveWave = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalEmissiveWaveSO",
                    visualAssets.EmissiveWave.AddressLow,
                    visualAssets.EmissiveWave.AddressHigh,
                    litMaterial,
                    wavePosition,
                    waveRotation,
                    waveScale,
                    visualProfile == null ? Color.white : visualProfile.ChargeWaveTint,
                    ScalePortalColor(
                        MultiplyPortalColors(
                            visualProfile == null
                                ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                                : visualProfile.ChargeWaveEmissiveColor,
                            visualProfile == null ? Color.white : visualProfile.ChargeWaveTint),
                        3.5f),
                    false),
                CenterEffect = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalCenterEffectSO",
                    visualAssets.CenterEffect.AddressLow,
                    visualAssets.CenterEffect.AddressHigh,
                    litMaterial,
                    centerPosition,
                    centerRotation,
                    centerScale,
                    visualProfile == null
                        ? DimensionPortalVisualProfileAsset.VanillaCenterTint
                        : visualProfile.CenterTint,
                    ScalePortalColor(
                        visualProfile == null
                            ? MultiplyPortalColors(
                                DimensionPortalVisualProfileAsset.VanillaCenterEmissiveColor,
                                DimensionPortalVisualProfileAsset.VanillaCenterTint)
                            : MultiplyPortalColors(
                                visualProfile.CenterEmissiveColor,
                                visualProfile.CenterTint),
                        visualProfile == null
                            ? DimensionPortalVisualProfileAsset.VanillaCenterGlowIntensity
                            : visualProfile.CenterGlowIntensity),
                    false),
                CustomSwirl = customSwirlEnabled
                    ? EnsurePortalSpriteObject(
                        spriteRoot,
                        "PortalCustomSwirlSO",
                        visualAssets.CustomSwirl.AddressLow,
                        visualAssets.CustomSwirl.AddressHigh,
                        litMaterial,
                        customSwirlPosition,
                        customSwirlRotation,
                        customSwirlScale,
                        customSwirlTint,
                        customSwirlEmission,
                        false)
                    : null,
                OutlineMask = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalOutlineMaskSO",
                    outlineMaskAssets.Main.AddressLow,
                    outlineMaskAssets.Main.AddressHigh,
                    outlineMaterial,
                    outlinePosition,
                    bodyRotation,
                    bodyScale,
                    Color.clear,
                    Color.clear,
                    true),
                OutlineSupportMask = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalOutlineSupportMaskSO",
                    outlineMaskAssets.Support.AddressLow,
                    outlineMaskAssets.Support.AddressHigh,
                    outlineMaterial,
                    outlinePosition,
                    bodyRotation,
                    bodyScale,
                    Color.clear,
                    Color.clear,
                    true),
                OutlineCap = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalOutlineCapSO",
                    outlineMaskAssets.Cap.AddressLow,
                    outlineMaskAssets.Cap.AddressHigh,
                    outlineMaterial,
                    outlinePosition,
                    bodyRotation,
                    bodyScale,
                    Color.clear,
                    Color.clear,
                    false),
                ShadowRoot = shadowRoot.gameObject,
                Shadow = EnsurePortalShadowRenderer(
                    shadowRoot,
                    "Shadow",
                    shadowSprite,
                    floorShadowMaterial,
                    shadowPosition,
                    shadowRotation,
                    shadowScale,
                    shadowVisible,
                    !customShadowSprite,
                    22,
                    -10),
                ShadowCaster = EnsurePortalShadowRenderer(
                    shadowRoot,
                    "ShadowCaster",
                    shadowCasterSprite,
                    shadowCasterMaterial,
                    shadowCasterPosition,
                    shadowRotation,
                    shadowScale,
                    shadowVisible,
                    !customShadowCasterSprite,
                    19,
                    0)
            };

            TrySetUnityTag(result.Shadow.gameObject, "ExcludeFromSpriteAutoSort");
            TrySetUnityTag(result.ShadowCaster.gameObject, "ExcludeFromSpriteAutoSort");
            PortalParticleRootSet particleRoots = EnsurePortalCenterParticleEffects(
                spriteRoot,
                visualProfile);
            result.CenterParticlesRoot = particleRoots.Persistent;
            result.ReadyFlashRoot = particleRoots.ReadyFlash;
            return result;
        }

        private static GeneratedPortalOutlineSpriteAssets EnsureGeneratedPortalOutlineMaskAssets(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            string sourceBodyTexturePath)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalOutlineMask",
                "DimensionPortalOutlineMask");
            string supportAssetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalOutlineSupportMask",
                "DimensionPortalOutlineSupportMask");
            string capAssetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalOutlineCap",
                "DimensionPortalOutlineCap");
            string texturePath = portalFolder + "/" + assetName + ".png";
            string supportTexturePath = portalFolder + "/" + supportAssetName + ".png";
            string capTexturePath = portalFolder + "/" + capAssetName + ".png";
            string spriteAssetPath = portalFolder + "/" + assetName + ".asset";
            string supportSpriteAssetPath = portalFolder + "/" + supportAssetName + ".asset";
            string capSpriteAssetPath = portalFolder + "/" + capAssetName + ".asset";
            string addressSeed =
                portalOutput.PortalObjectName + ":generated-portal-outline-mask";
            string supportAddressSeed =
                portalOutput.PortalObjectName + ":generated-portal-outline-support-mask";
            string capAddressSeed =
                portalOutput.PortalObjectName + ":generated-portal-outline-cap";
            long addressLow = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x706F7274616C6F75UL);
            long addressHigh = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x746C696E656D6173UL);
            long supportAddressLow = DimensionSpriteAssetAddress.Part(
                supportAddressSeed,
                0x737570706F72746DUL);
            long supportAddressHigh = DimensionSpriteAssetAddress.Part(
                supportAddressSeed,
                0x61736B706F727461UL);
            long capAddressLow = DimensionSpriteAssetAddress.Part(
                capAddressSeed,
                0x6361706F75746C69UL);
            long capAddressHigh = DimensionSpriteAssetAddress.Part(
                capAddressSeed,
                0x6E656361706F7274UL);

            EnsureGeneratedPortalOutlineMaskTextures(
                sourceBodyTexturePath,
                texturePath,
                supportTexturePath,
                capTexturePath);

            string textureGuid = AssetDatabase.AssetPathToGUID(texturePath);
            if (string.IsNullOrEmpty(textureGuid))
            {
                AssetDatabase.ImportAsset(
                    texturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                textureGuid = AssetDatabase.AssetPathToGUID(texturePath);
            }

            if (string.IsNullOrEmpty(textureGuid))
            {
                throw new System.InvalidOperationException(
                    "Could not create generated portal outline mask texture at " +
                    texturePath +
                    ".");
            }

            string supportTextureGuid = AssetDatabase.AssetPathToGUID(supportTexturePath);
            if (string.IsNullOrEmpty(supportTextureGuid))
            {
                AssetDatabase.ImportAsset(
                    supportTexturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                supportTextureGuid = AssetDatabase.AssetPathToGUID(supportTexturePath);
            }

            if (string.IsNullOrEmpty(supportTextureGuid))
            {
                throw new System.InvalidOperationException(
                    "Could not create generated portal outline support mask texture at " +
                    supportTexturePath +
                    ".");
            }

            string capTextureGuid = AssetDatabase.AssetPathToGUID(capTexturePath);
            if (string.IsNullOrEmpty(capTextureGuid))
            {
                AssetDatabase.ImportAsset(
                    capTexturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                capTextureGuid = AssetDatabase.AssetPathToGUID(capTexturePath);
            }

            if (string.IsNullOrEmpty(capTextureGuid))
            {
                throw new System.InvalidOperationException(
                    "Could not create generated portal outline cap texture at " +
                    capTexturePath +
                    ".");
            }

            string spriteAssetYaml = BuildPortalOutlineMaskSpriteAssetYaml(
                assetName,
                addressLow,
                addressHigh,
                textureGuid);
            WriteTextAssetIfChanged(spriteAssetPath, spriteAssetYaml);
            EnsureSpriteAssetImported(spriteAssetPath);
            EnsureSpriteAssetManifestContains(modRoot, spriteAssetPath);

            string supportSpriteAssetYaml = BuildPortalOutlineMaskSpriteAssetYaml(
                supportAssetName,
                supportAddressLow,
                supportAddressHigh,
                supportTextureGuid);
            WriteTextAssetIfChanged(supportSpriteAssetPath, supportSpriteAssetYaml);
            EnsureSpriteAssetImported(supportSpriteAssetPath);
            EnsureSpriteAssetManifestContains(modRoot, supportSpriteAssetPath);

            string capSpriteAssetYaml = BuildPortalOutlineMaskSpriteAssetYaml(
                capAssetName,
                capAddressLow,
                capAddressHigh,
                capTextureGuid);
            WriteTextAssetIfChanged(capSpriteAssetPath, capSpriteAssetYaml);
            EnsureSpriteAssetImported(capSpriteAssetPath);
            EnsureSpriteAssetManifestContains(modRoot, capSpriteAssetPath);

            return new GeneratedPortalOutlineSpriteAssets
            {
                Main = new GeneratedPortalSpriteAsset
                {
                    AddressLow = addressLow,
                    AddressHigh = addressHigh
                },
                Support = new GeneratedPortalSpriteAsset
                {
                    AddressLow = supportAddressLow,
                    AddressHigh = supportAddressHigh
                },
                Cap = new GeneratedPortalSpriteAsset
                {
                    AddressLow = capAddressLow,
                    AddressHigh = capAddressHigh
                }
            };
        }

        private static void EnsureGeneratedPortalOutlineMaskTextures(
            string sourceBodyTexturePath,
            string targetMaskTexturePath,
            string targetSupportMaskTexturePath,
            string targetCapTexturePath)
        {
            string sourceAbsolutePath = AssetPathToAbsolutePath(sourceBodyTexturePath);
            if (string.IsNullOrEmpty(sourceAbsolutePath) || !File.Exists(sourceAbsolutePath))
            {
                throw new System.InvalidOperationException(
                    "Could not read the source portal body texture at " +
                    sourceBodyTexturePath +
                    ".");
            }

            Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D maskTexture = null;
            Texture2D supportMaskTexture = null;
            Texture2D capTexture = null;
            try
            {
                if (!sourceTexture.LoadImage(File.ReadAllBytes(sourceAbsolutePath)))
                {
                    throw new System.InvalidOperationException(
                        "Unity could not decode the source portal body texture at " +
                        sourceBodyTexturePath +
                        ".");
                }

                int sourceWidth = sourceTexture.width;
                int sourceHeight = sourceTexture.height;
                int maskWidth = sourceWidth;
                int maskHeight = sourceHeight;
                Color32[] sourcePixels = sourceTexture.GetPixels32();
                Color32[] maskPixels = new Color32[maskWidth * maskHeight];
                Color32[] supportMaskPixels = new Color32[maskWidth * maskHeight];
                Color32[] capPixels = new Color32[maskWidth * maskHeight];
                Color32 opaqueMaskPixel = new Color32(255, 255, 255, 255);
                Color32 transparentMaskPixel = new Color32(255, 255, 255, 0);
                bool[] bodyPixels = new bool[maskWidth * maskHeight];
                bool[] desiredEdgePixels = new bool[maskWidth * maskHeight];
                bool[] mainMaskPixels = new bool[maskWidth * maskHeight];
                bool[] mainOutlinePixels = new bool[maskWidth * maskHeight];
                bool[] supportPixels = new bool[maskWidth * maskHeight];
                bool[] supportOutlinePixels = new bool[maskWidth * maskHeight];

                for (int i = 0; i < maskPixels.Length; i++)
                {
                    maskPixels[i] = transparentMaskPixel;
                    supportMaskPixels[i] = transparentMaskPixel;
                    capPixels[i] = transparentMaskPixel;
                }

                for (int y = 0; y < sourceHeight; y++)
                {
                    for (int x = 0; x < sourceWidth; x++)
                    {
                        int index = y * maskWidth + x;
                        bool bodyPixel =
                            IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x, y);
                        bodyPixels[index] = bodyPixel;
                        if (!bodyPixel)
                        {
                            continue;
                        }

                        bool desiredEdge =
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x - 1, y) ||
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x + 1, y) ||
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x, y - 1) ||
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x, y + 1);
                        desiredEdgePixels[index] = desiredEdge;
                        mainMaskPixels[index] = !desiredEdge;
                        if (!desiredEdge)
                        {
                            maskPixels[index] = opaqueMaskPixel;
                        }
                    }
                }

                BuildSpriteObjectOutlinePixels(
                    mainMaskPixels,
                    bodyPixels,
                    maskWidth,
                    maskHeight,
                    mainOutlinePixels);
                AddOutlineSupportPixels(
                    bodyPixels,
                    desiredEdgePixels,
                    mainOutlinePixels,
                    supportPixels,
                    maskWidth,
                    maskHeight);
                BuildSpriteObjectOutlinePixels(
                    supportPixels,
                    bodyPixels,
                    maskWidth,
                    maskHeight,
                    supportOutlinePixels);

                for (int i = 0; i < supportPixels.Length; i++)
                {
                    if (supportPixels[i])
                    {
                        supportMaskPixels[i] = opaqueMaskPixel;
                    }
                }

                for (int i = 0; i < capPixels.Length; i++)
                {
                    if (desiredEdgePixels[i] &&
                        !mainOutlinePixels[i] &&
                        !supportOutlinePixels[i])
                    {
                        capPixels[i] = opaqueMaskPixel;
                    }
                }

                maskTexture = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false);
                maskTexture.SetPixels32(maskPixels);
                maskTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(targetMaskTexturePath, maskTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetMaskTexturePath,
                    maskWidth,
                    maskHeight);

                supportMaskTexture = new Texture2D(
                    maskWidth,
                    maskHeight,
                    TextureFormat.RGBA32,
                    false);
                supportMaskTexture.SetPixels32(supportMaskPixels);
                supportMaskTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(
                    targetSupportMaskTexturePath,
                    supportMaskTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetSupportMaskTexturePath,
                    maskWidth,
                    maskHeight);

                capTexture = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false);
                capTexture.SetPixels32(capPixels);
                capTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(targetCapTexturePath, capTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetCapTexturePath,
                    maskWidth,
                    maskHeight);
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
                if (maskTexture != null)
                {
                    Object.DestroyImmediate(maskTexture);
                }

                if (supportMaskTexture != null)
                {
                    Object.DestroyImmediate(supportMaskTexture);
                }

                if (capTexture != null)
                {
                    Object.DestroyImmediate(capTexture);
                }
            }
        }

        private static void BuildSpriteObjectOutlinePixels(
            bool[] maskPixels,
            bool[] bodyPixels,
            int width,
            int height,
            bool[] outlinePixels)
        {
            for (int i = 0; i < outlinePixels.Length; i++)
            {
                outlinePixels[i] = false;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (!bodyPixels[index] || maskPixels[index])
                    {
                        continue;
                    }

                    outlinePixels[index] =
                        IsMaskPixelSet(maskPixels, width, height, x - 1, y) ||
                        IsMaskPixelSet(maskPixels, width, height, x + 1, y) ||
                        IsMaskPixelSet(maskPixels, width, height, x, y - 1) ||
                        IsMaskPixelSet(maskPixels, width, height, x, y + 1);
                }
            }
        }

        private static void AddOutlineSupportPixels(
            bool[] bodyPixels,
            bool[] desiredEdgePixels,
            bool[] mainOutlinePixels,
            bool[] supportPixels,
            int width,
            int height)
        {
            bool[] supportOutlinePixels = new bool[bodyPixels.Length];
            bool[] coveredPixels = new bool[bodyPixels.Length];
            for (int i = 0; i < coveredPixels.Length; i++)
            {
                coveredPixels[i] = mainOutlinePixels[i];
            }

            bool addedPixel;
            do
            {
                addedPixel = false;
                BuildSpriteObjectOutlinePixels(
                    supportPixels,
                    bodyPixels,
                    width,
                    height,
                    supportOutlinePixels);
                for (int i = 0; i < coveredPixels.Length; i++)
                {
                    coveredPixels[i] = mainOutlinePixels[i] || supportOutlinePixels[i];
                }

                for (int y = 0; y < height && !addedPixel; y++)
                {
                    for (int x = 0; x < width && !addedPixel; x++)
                    {
                        int index = y * width + x;
                        if (!desiredEdgePixels[index] || coveredPixels[index])
                        {
                            continue;
                        }

                        int supportIndex;
                        if (TryFindOutlineSupportPixel(
                            bodyPixels,
                            desiredEdgePixels,
                            coveredPixels,
                            supportPixels,
                            width,
                            height,
                            x,
                            y,
                            out supportIndex))
                        {
                            supportPixels[supportIndex] = true;
                            addedPixel = true;
                        }
                    }
                }
            }
            while (addedPixel);
        }

        private static bool TryFindOutlineSupportPixel(
            bool[] bodyPixels,
            bool[] desiredEdgePixels,
            bool[] coveredPixels,
            bool[] supportPixels,
            int width,
            int height,
            int x,
            int y,
            out int supportIndex)
        {
            int index;
            if (TryGetSafeOutlineSupportPixel(
                bodyPixels,
                desiredEdgePixels,
                coveredPixels,
                supportPixels,
                width,
                height,
                x - 1,
                y,
                out index) ||
                TryGetSafeOutlineSupportPixel(
                    bodyPixels,
                    desiredEdgePixels,
                    coveredPixels,
                    supportPixels,
                    width,
                    height,
                    x + 1,
                    y,
                    out index) ||
                TryGetSafeOutlineSupportPixel(
                    bodyPixels,
                    desiredEdgePixels,
                    coveredPixels,
                    supportPixels,
                    width,
                    height,
                    x,
                    y - 1,
                    out index) ||
                TryGetSafeOutlineSupportPixel(
                    bodyPixels,
                    desiredEdgePixels,
                    coveredPixels,
                    supportPixels,
                    width,
                    height,
                    x,
                    y + 1,
                    out index))
            {
                supportIndex = index;
                return true;
            }

            supportIndex = -1;
            return false;
        }

        private static bool TryGetSafeOutlineSupportPixel(
            bool[] bodyPixels,
            bool[] desiredEdgePixels,
            bool[] coveredPixels,
            bool[] supportPixels,
            int width,
            int height,
            int x,
            int y,
            out int index)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                index = -1;
                return false;
            }

            index = y * width + x;
            if (!bodyPixels[index] || supportPixels[index])
            {
                return false;
            }

            bool addsUsefulPixel = false;
            for (int direction = 0; direction < 4; direction++)
            {
                int neighborX = x;
                int neighborY = y;
                switch (direction)
                {
                    case 0:
                        neighborX--;
                        break;
                    case 1:
                        neighborX++;
                        break;
                    case 2:
                        neighborY--;
                        break;
                    default:
                        neighborY++;
                        break;
                }

                if (IsMaskPixelSet(supportPixels, width, height, neighborX, neighborY))
                {
                    continue;
                }

                if (neighborX < 0 ||
                    neighborY < 0 ||
                    neighborX >= width ||
                    neighborY >= height)
                {
                    return false;
                }

                int neighborIndex = neighborY * width + neighborX;
                if (!bodyPixels[neighborIndex] || !desiredEdgePixels[neighborIndex])
                {
                    return false;
                }

                if (!coveredPixels[neighborIndex])
                {
                    addsUsefulPixel = true;
                }
            }

            return addsUsefulPixel;
        }

        private static bool IsMaskPixelSet(
            bool[] pixels,
            int width,
            int height,
            int x,
            int y)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x < width &&
                   y < height &&
                   pixels[y * width + x];
        }

        private static bool IsOpaquePortalPixel(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x < width &&
                   y < height &&
                   pixels[y * width + x].a > 127;
        }

        private static void ConfigureGeneratedSpriteTextureImporter(
            string textureAssetPath,
            int sourceWidth,
            int sourceHeight)
        {
            TextureImporter importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(
                    textureAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new System.InvalidOperationException(
                        "Could not configure the generated sprite texture importer at " +
                        textureAssetPath + ".");
                }
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 16.0f))
            {
                importer.spritePixelsPerUnit = 16.0f;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            int largestSourceDimension = Mathf.Max(sourceWidth, sourceHeight);
            if (largestSourceDimension > 0)
            {
                int requiredMaximumSize = Mathf.NextPowerOfTwo(largestSourceDimension);
                if (requiredMaximumSize > importer.maxTextureSize)
                {
                    importer.maxTextureSize = requiredMaximumSize;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static string BuildPortalOutlineMaskSpriteAssetYaml(
            string assetName,
            long addressLow,
            long addressHigh,
            string textureGuid)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("%YAML 1.1");
            builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            builder.AppendLine("--- !u!114 &11400000");
            builder.AppendLine("MonoBehaviour:");
            builder.AppendLine("  m_ObjectHideFlags: 0");
            builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
            builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
            builder.AppendLine("  m_GameObject: {fileID: 0}");
            builder.AppendLine("  m_Enabled: 1");
            builder.AppendLine("  m_EditorHideFlags: 0");
            builder.AppendLine("  m_Script: {fileID: -217761678, guid: 292700ef68995bdb2163e35989fc7eb0, type: 3}");
            builder.Append("  m_Name: ").AppendLine(ToUnityYamlString(assetName));
            builder.AppendLine("  m_EditorClassIdentifier: ");
            builder.AppendLine("  m_overload:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_address:");
            builder.Append("    m_low: ").AppendLine(addressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append("    m_high: ").AppendLine(addressHigh.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("  m_dynamicCollections:");
            builder.AppendLine("    m_list: []");
            builder.AppendLine("  m_defaultPrimaryGradientMap: {fileID: 0}");
            builder.AppendLine("  m_defaultSecondaryGradientMap: {fileID: 0}");
            builder.AppendLine("  m_defaultTertiaryGradientMap: {fileID: 0}");
            builder.AppendLine("  m_defaultPrimaryGradientMapRef:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_defaultSecondaryGradientMapRef:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_defaultTertiaryGradientMapRef:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_editorHideEmissive: 0");
            builder.AppendLine("  m_staticSpriteData:");
            builder.Append("    texture: {fileID: 2800000, guid: ")
                .Append(textureGuid)
                .AppendLine(", type: 3}");
            builder.AppendLine("    emissiveTexture: {fileID: 0}");
            builder.AppendLine("    normalTexture: {fileID: 0}");
            builder.AppendLine("    pivot: {x: 0.5, y: 0.5}");
            builder.AppendLine("    positionalData: []");
            builder.AppendLine("    inheritPivot: 1");
            builder.AppendLine("  m_staticVariants: []");
            builder.AppendLine("  m_animations: []");
            builder.AppendLine("  references:");
            builder.AppendLine("    version: 2");
            builder.AppendLine("    RefIds: []");
            return builder.ToString();
        }

        private static void EnsureSpriteAssetManifestContains(
            string modRoot,
            string spriteAssetPath)
        {
            string normalizedRoot = NormalizeAssetPath(modRoot);
            string normalizedSpriteAssetPath = NormalizeAssetPath(spriteAssetPath);
            if (string.IsNullOrEmpty(normalizedRoot) ||
                string.IsNullOrEmpty(normalizedSpriteAssetPath))
            {
                return;
            }

            SpriteAssetBase spriteAsset =
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(normalizedSpriteAssetPath);
            if (spriteAsset == null)
            {
                throw new System.InvalidOperationException(
                    "Could not load the generated SpriteAsset at " +
                    normalizedSpriteAssetPath + ".");
            }

            string manifestPath = normalizedRoot + "/SpriteAssetManifest.asset";
            string absoluteManifestPath = AssetPathToAbsolutePath(manifestPath);
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (manifest == null)
            {
                if (!string.IsNullOrEmpty(absoluteManifestPath) &&
                    File.Exists(absoluteManifestPath))
                {
                    throw new System.InvalidOperationException(
                        "The SpriteAsset manifest at " + manifestPath +
                        " exists but Unity could not load it as a SpriteAssetManifest.");
                }

                manifest = ScriptableObject.CreateInstance<SpriteAssetManifest>();
                manifest.name = "SpriteAssetManifest";
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }

            if (manifest.spriteAssets == null)
            {
                manifest.spriteAssets = new List<SpriteAssetBase>();
            }

            bool changed = false;
            bool alreadyRegistered = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                SpriteAssetBase registeredAsset = manifest.spriteAssets[i];
                if (registeredAsset == null)
                {
                    manifest.spriteAssets.RemoveAt(i);
                    changed = true;
                }
                else if (registeredAsset == spriteAsset)
                {
                    alreadyRegistered = true;
                }
            }

            if (!alreadyRegistered)
            {
                manifest.spriteAssets.Add(spriteAsset);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
            }
        }

        private static void RemoveSpriteAssetManifestReference(
            string modRoot,
            SpriteAssetBase spriteAsset)
        {
            string normalizedRoot = NormalizeAssetPath(modRoot);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return;
            }

            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(
                    normalizedRoot + "/SpriteAssetManifest.asset");
            if (manifest == null || manifest.spriteAssets == null)
            {
                return;
            }

            bool changed = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                SpriteAssetBase registeredAsset = manifest.spriteAssets[i];
                if (registeredAsset == null || registeredAsset == spriteAsset)
                {
                    manifest.spriteAssets.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
            }
        }

        private static void EnsureSpriteAssetImported(string spriteAssetPath)
        {
            string normalizedPath = NormalizeAssetPath(spriteAssetPath);
            if (string.IsNullOrEmpty(normalizedPath) ||
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(normalizedPath) != null)
            {
                return;
            }

            AssetDatabase.ImportAsset(
                normalizedPath,
                ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(normalizedPath) == null)
            {
                throw new System.InvalidOperationException(
                    "Unity could not import the generated SpriteAsset at " +
                    normalizedPath + ".");
            }
        }

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

        private static SpriteObject FindDescendantSpriteObject(
            Transform root,
            string childName)
        {
            Transform transform = FindDescendantTransform(root, childName);
            return transform == null ? null : transform.GetComponent<SpriteObject>();
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    transforms[i].gameObject.layer = layer;
                }
            }
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            return child;
        }

        private static Material RequirePortalMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual requires material " +
                    path +
                    ".");
            }

            return material;
        }

        private static Sprite RequirePortalSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual requires sprite " +
                    path +
                    ".");
            }

            return sprite;
        }

        private static void TrySetUnityTag(GameObject gameObject, string tag)
        {
            if (gameObject == null || string.IsNullOrEmpty(tag))
            {
                return;
            }

            try
            {
                gameObject.tag = tag;
            }
            catch (UnityException)
            {
                // The SDK project defines this vanilla sorting tag. If a trimmed
                // project removes it, shadow behavior still falls back to layer order.
            }
        }

        private static SpriteObject EnsurePortalSpriteObject(
            Transform parent,
            string name,
            long addressLow,
            long addressHigh,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Color color,
            Color emissiveColor,
            bool active,
            int layer = -1)
        {
            Transform transform = EnsureChild(parent, name);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
            transform.gameObject.layer = layer >= 0 ? layer : parent.gameObject.layer;

            SpriteObject spriteObject = transform.GetComponent<SpriteObject>();
            if (spriteObject == null)
            {
                spriteObject = transform.gameObject.AddComponent<SpriteObject>();
            }

            AssignSpriteAssetAddress(spriteObject, addressLow, addressHigh);
            if (material != null)
            {
                spriteObject.material = material;
            }

            spriteObject.color = color;
            spriteObject.emissiveColor = emissiveColor;
            spriteObject.flashColor = Color.clear;
            spriteObject.outlineColor = Color.clear;
            spriteObject.animationTimescale = 1.0f;
            spriteObject.syncAnimation = false;
            spriteObject.syncVariant = false;
            spriteObject.syncSprite = false;
            spriteObject.ApplyVisualChange();
            transform.gameObject.SetActive(active);
            return spriteObject;
        }

        private static SpriteRenderer EnsurePortalBodyRenderer(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            bool active)
        {
            Transform transform = EnsureChild(parent, name);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
            transform.gameObject.layer = parent.gameObject.layer;

            SpriteObject spriteObject = transform.GetComponent<SpriteObject>();
            if (spriteObject != null)
            {
                Object.DestroyImmediate(spriteObject, true);
            }

            SpriteRenderer renderer = transform.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = transform.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sharedMaterial = material;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.flipX = false;
            renderer.flipY = false;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.spriteSortPoint = SpriteSortPoint.Center;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.renderingLayerMask = 1;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            renderer.sortingOrder = 0;
            transform.gameObject.SetActive(active);
            return renderer;
        }

        private static SpriteRenderer EnsurePortalShadowRenderer(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            bool active,
            bool sliced,
            int layer,
            int sortingOrder)
        {
            Transform transform = EnsureChild(parent, name);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
            transform.gameObject.layer = layer;

            SpriteObject spriteObject = transform.GetComponent<SpriteObject>();
            if (spriteObject != null)
            {
                Object.DestroyImmediate(spriteObject, true);
            }

            SpriteRenderer renderer = transform.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = transform.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sharedMaterial = material;
            renderer.sprite = sprite;
            renderer.color = new Color(0.0f, 0.0f, 0.0f, 0.7019608f);
            renderer.drawMode = sliced ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
            if (sliced)
            {
                renderer.size = new Vector2(3.5f, 0.75f);
            }
            renderer.flipX = false;
            renderer.flipY = false;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.spriteSortPoint = SpriteSortPoint.Center;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderingLayerMask = uint.MaxValue;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
            if (sortingOrder == 0)
            {
                renderer.sortingLayerID = 1861650685;
            }

            transform.gameObject.SetActive(active);
            return renderer;
        }

        private static void AssignSpriteAssetAddress(
            SpriteObject spriteObject,
            long addressLow,
            long addressHigh)
        {
            SerializedObject serializedObject = new SerializedObject(spriteObject);
            serializedObject.Update();
            SetSerializedLong(
                serializedObject,
                "m_assetRef.m_address.m_low",
                addressLow);
            SetSerializedLong(
                serializedObject,
                "m_assetRef.m_address.m_high",
                addressHigh);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveLegacyPortalVisualArtifacts(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            RemoveComponentsByTypeName(root, "Sprite" + "Renderer");
            RemoveComponentsByTypeName(root, "Outline" + "Controller");
            DestroyLegacyVisualObjects(root);
        }

        private static void RemoveComponentsByTypeName(GameObject root, string typeName)
        {
            if (root == null || string.IsNullOrEmpty(typeName))
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                {
                    continue;
                }

                Component[] components = transforms[i].GetComponents<Component>();
                for (int j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component != null && component.GetType().Name == typeName)
                    {
                        Object.DestroyImmediate(component, true);
                    }
                }
            }
        }

        private static void DestroyLegacyVisualObjects(GameObject root)
        {
            List<GameObject> legacyObjects = new List<GameObject>();
            CollectLegacyVisualObjects(root.transform, root, legacyObjects);
            for (int i = 0; i < legacyObjects.Count; i++)
            {
                if (legacyObjects[i] != null)
                {
                    Object.DestroyImmediate(legacyObjects[i], true);
                }
            }
        }

        private static void CollectLegacyVisualObjects(
            Transform current,
            GameObject root,
            List<GameObject> legacyObjects)
        {
            if (current == null)
            {
                return;
            }

            if (current.gameObject != root && IsLegacyPortalVisualObjectName(current.name))
            {
                legacyObjects.Add(current.gameObject);
                return;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CollectLegacyVisualObjects(current.GetChild(i), root, legacyObjects);
            }
        }

        private static bool IsLegacyPortalVisualObjectName(string name)
        {
            return name == "SR" ||
                name.StartsWith("SR (", System.StringComparison.Ordinal) ||
                name == "portalEffect" + "SR" ||
                name == "load" + "Points" ||
                name == "PortalBodyRenderer" ||
                name == "PortalBodySpriteObject" ||
                name == "PortalChargeWaveSO" ||
                name == "PortalIndirectLightSO" ||
                name == "PortalShadowSO" ||
                name == "PortalShadowCasterSO" ||
                name == "Shadow";
        }

        private static void ValidateSpriteObjectOnlyPortal(GameObject root, bool itemPortal)
        {
            Vector3 expectedPivot = itemPortal
                ? ItemPortalSpritePivotPosition
                : PortalSpritePivotPosition;
            string forbiddenComponent = FindForbiddenPortalVisualComponent(root);
            if (!string.IsNullOrEmpty(forbiddenComponent))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visible visuals must use the approved portal render paths; " +
                    "only the analytic portal body plus the vanilla Shadow and ShadowCaster renderer children are allowed. " +
                    "Forbidden component remains: " +
                    forbiddenComponent +
                    ".");
            }

            Transform spriteRoot = root == null
                ? null
                : root.transform.Find(
                    "XScaler/AnimPositionRotation/AnimScale/SRPivot/PortalSpriteObjects");
            if (spriteRoot == null)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing the vanilla animator hierarchy " +
                    "XScaler/AnimPositionRotation/AnimScale/SRPivot/PortalSpriteObjects.");
            }

            Transform spritePivot = spriteRoot.parent;
            Transform animScale = spritePivot == null ? null : spritePivot.parent;
            Transform animPositionRotation = animScale == null ? null : animScale.parent;
            if (spritePivot == null ||
                animScale == null ||
                animPositionRotation == null ||
                (spritePivot.localPosition - expectedPivot).sqrMagnitude > 0.000001f ||
                spriteRoot.localPosition.sqrMagnitude > 0.000001f ||
                animScale.localPosition.sqrMagnitude > 0.000001f ||
                animPositionRotation.localPosition.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(spritePivot.localRotation, Quaternion.identity) > 0.001f ||
                Quaternion.Angle(spriteRoot.localRotation, Quaternion.identity) > 0.001f ||
                Quaternion.Angle(animScale.localRotation, Quaternion.identity) > 0.001f ||
                Quaternion.Angle(animPositionRotation.localRotation, Quaternion.identity) > 0.001f ||
                (spritePivot.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                (spriteRoot.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                (animScale.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                (animPositionRotation.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual has invalid vanilla animator wrapper " +
                    "transforms. SRPivot must be anchored at " +
                    expectedPivot +
                    " and all wrapper rotations/scales plus the SpriteObject root transform " +
                    "must remain at their vanilla defaults.");
            }

            string missingLayer = FindMissingPortalSpriteObjectLayer(spriteRoot);
            if (!string.IsNullOrEmpty(missingLayer))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing required SpriteObject layer " +
                    missingLayer +
                    ".");
            }

            string unresolvedLayer = FindUnresolvedPortalSpriteObjectLayer(spriteRoot);
            if (!string.IsNullOrEmpty(unresolvedLayer))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual has an unresolved SpriteAsset reference: " +
                    unresolvedLayer +
                    ". The portal was not saved; reapply after Scriptable Data finishes importing.");
            }

            string missingShadow = FindMissingPortalShadowRendererLayer(spriteRoot);
            if (!string.IsNullOrEmpty(missingShadow))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing required vanilla shadow renderer " +
                    missingShadow +
                    ".");
            }

            string missingLight = FindMissingPortalLightLayer(root);
            if (!string.IsNullOrEmpty(missingLight))
            {
                throw new System.InvalidOperationException(
                    "Generated dimension portal visual is missing required vanilla light layer " +
                    missingLight +
                    ".");
            }
        }

        private static string FindMissingPortalSpriteObjectLayer(Transform spriteRoot)
        {
            string[] requiredLayers =
            {
                "PortalBodySO",
                "PortalChargeProgressSO",
                "PortalEmissiveWaveSO",
                "PortalCenterEffectSO",
                "PortalOutlineMaskSO",
                "PortalOutlineSupportMaskSO",
                "PortalOutlineCapSO"
            };

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                Transform layer = spriteRoot.Find(requiredLayers[i]);
                if (layer == null || layer.GetComponent<SpriteObject>() == null)
                {
                    return requiredLayers[i];
                }
            }

            return string.Empty;
        }

        private static string FindUnresolvedPortalSpriteObjectLayer(Transform spriteRoot)
        {
            string[] requiredLayers =
            {
                "PortalBodySO",
                "PortalChargeProgressSO",
                "PortalEmissiveWaveSO",
                "PortalCenterEffectSO",
                "PortalOutlineMaskSO",
                "PortalOutlineSupportMaskSO",
                "PortalOutlineCapSO"
            };

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                Transform layer = spriteRoot.Find(requiredLayers[i]);
                SpriteObject spriteObject = layer == null
                    ? null
                    : layer.GetComponent<SpriteObject>();
                if (spriteObject == null || spriteObject.asset != null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(spriteObject);
                serialized.Update();
                SerializedProperty low = serialized.FindProperty(
                    "m_assetRef.m_address.m_low");
                SerializedProperty high = serialized.FindProperty(
                    "m_assetRef.m_address.m_high");
                return requiredLayers[i] + " (address low " +
                    (low == null ? "?" : low.longValue.ToString(CultureInfo.InvariantCulture)) +
                    ", high " +
                    (high == null ? "?" : high.longValue.ToString(CultureInfo.InvariantCulture)) +
                    ")";
            }

            return string.Empty;
        }

        private static string FindMissingPortalShadowRendererLayer(Transform spriteRoot)
        {
            string[] requiredLayers =
            {
                "PortalShadowGroup/Shadow",
                "PortalShadowGroup/ShadowCaster"
            };

            for (int i = 0; i < requiredLayers.Length; i++)
            {
                Transform layer = spriteRoot.Find(requiredLayers[i]);
                if (layer == null || layer.GetComponent<SpriteRenderer>() == null)
                {
                    return requiredLayers[i];
                }
            }

            return string.Empty;
        }

        private static string FindMissingPortalLightLayer(GameObject root)
        {
            Transform lightRoot = root == null
                ? null
                : root.transform.Find("XScaler/" + PortalLightObjectName);
            if (lightRoot == null)
            {
                return PortalLightObjectName;
            }

            ManagedLight managedLight = lightRoot.GetComponent<ManagedLight>();
            if (managedLight == null)
            {
                return PortalLightObjectName + "/ManagedLight";
            }

            Light pointLight = lightRoot.GetComponentInChildren<Light>(true);
            if (pointLight == null)
            {
                return PortalLightObjectName + "/Point Light";
            }

            SpriteObject fallback = FindDescendantSpriteObject(
                lightRoot,
                "IndirectLightSprite");
            if (fallback == null)
            {
                return PortalLightObjectName + "/IndirectLightSprite";
            }

            return string.Empty;
        }

        private static string FindForbiddenPortalVisualComponent(GameObject root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                {
                    continue;
                }

                Component[] components = transforms[i].GetComponents<Component>();
                for (int j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component == null)
                    {
                        continue;
                    }

                    string typeName = component.GetType().Name;
                    if (typeName == "Sprite" + "Renderer")
                    {
                        string path = GetTransformPath(transforms[i], root.transform);
                        if (!IsAllowedPortalRendererPath(path))
                        {
                            return typeName + " on " + path;
                        }
                    }
                    else if (typeName == "Outline" + "Controller")
                    {
                        return typeName + " on " + GetTransformPath(transforms[i], root.transform);
                    }
                }
            }

            return string.Empty;
        }

        private static bool IsAllowedPortalRendererPath(string path)
        {
            return path.EndsWith(
                    "/XScaler/AnimPositionRotation/AnimScale/SRPivot/" +
                    "PortalSpriteObjects/PortalBodyRenderer",
                    System.StringComparison.Ordinal) ||
                path.EndsWith(
                    "/XScaler/AnimPositionRotation/AnimScale/SRPivot/" +
                    "PortalSpriteObjects/PortalShadowGroup/Shadow",
                    System.StringComparison.Ordinal) ||
                path.EndsWith(
                    "/XScaler/AnimPositionRotation/AnimScale/SRPivot/" +
                    "PortalSpriteObjects/PortalShadowGroup/ShadowCaster",
                    System.StringComparison.Ordinal);
        }

        private static string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            if (transform == root || transform.parent == null)
            {
                return transform.name;
            }

            return GetTransformPath(transform.parent, root) + "/" + transform.name;
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

        private static void EnsurePortalHitAuthoring(GameObject root, bool destructible)
        {
            MineableAuthoring mineable = EnsureComponent<MineableAuthoring>(root);
            mineable.playFailedEffectOnZeroDamage = true;

            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            health.dontCalculateHealthFromLevel = false;
            health.overrideStartHealth = false;
            health.normalizedOverrideStartHealth = 1.0f;
            health.startHealth = 3;
            health.maxHealth = 3;
            health.maxHealthMultiplier = 1.0f;
            health.hasHealthRegeneration = true;
            health.healInCombatAsWell = false;
            health.healthIncreasePercentPerFiveSeconds = 100;
            health.healDelayAfterLeavingCombat = 5.0f;

            EnsureComponent<DamageableObjectAuthoring>(root);
            if (destructible)
            {
                EnsureComponent<DestructibleObjectAuthoring>(root).requiresDrill = false;
            }
            else
            {
                RemoveComponentIfPresent<DestructibleObjectAuthoring>(root);
            }
            EnsureComponent<DamageEffectAuthoring>(root);
            EnsureComponent<IdleStateAuthoring>(root).playIdleAnimation = true;

            TookDamageStateAuthoring tookDamage = EnsureComponent<TookDamageStateAuthoring>(root);
            tookDamage.duration = 0.0f;
            tookDamage.refreshStateOnNewDamageTaken = false;

            DeathStateAuthoring death = EnsureComponent<DeathStateAuthoring>(root);
            death.overrideTimeBeforeDestroy = false;
            death.timeBeforeDestroy = 0.0f;
            death.timeBeforeLootDrop = 0.0f;
            death.skipDeathAnimation = false;

            DamageReductionAuthoring damageReduction = EnsureComponent<DamageReductionAuthoring>(root);
            damageReduction.calculateReductionFromLevel = false;
            damageReduction.reductionMultiplier = 1.0f;
            damageReduction.reduction = VanillaPortalDamageReduction;
            damageReduction.maxDamagePerHit = 1;
            damageReduction.minDamagePerHit = 0;
            damageReduction.ignoreReductionWhenDamagedByDrill = false;
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }

        private static void RemoveComponentByName(
            GameObject root,
            string componentTypeName,
            Component exceptComponent = null)
        {
            if (root == null || string.IsNullOrEmpty(componentTypeName))
            {
                return;
            }

            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null ||
                    component == exceptComponent ||
                    component.GetType().Name != componentTypeName)
                {
                    continue;
                }

                Object.DestroyImmediate(component, true);
                return;
            }
        }

        private static void RemoveMissingMonoBehaviours(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
                }
            }
        }

        private static Transform FindDescendantTransform(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDescendantTransform(root.GetChild(i), childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void AssignSerializedObjectReferenceList(
            Object target,
            string propertyName,
            List<Object> values)
        {
            if (target == null || string.IsNullOrEmpty(propertyName) || values == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSerializedObjectReference(
            Object target,
            string propertyName,
            Object value)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null ||
                property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClearSerializedObjectReference(
            Object target,
            string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null &&
                property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGhostAuthoringComponent(GameObject root, string assetPath)
        {
            if (root == null)
            {
                return;
            }

            Component ghost = FindComponentByName(root, "GhostAuthoringComponent");
            if (ghost == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(ghost);
            serializedObject.Update();
            SetSerializedInt(serializedObject, "DefaultGhostMode", 0);
            SetSerializedInt(serializedObject, "SupportedGhostModes", 3);
            SetSerializedInt(serializedObject, "OptimizationMode", 1);
            SetSerializedString(serializedObject, "prefabId", AssetDatabase.AssetPathToGUID(assetPath));
            SetSerializedBool(serializedObject, "HasOwner", false);
            SetSerializedBool(serializedObject, "SupportAutoCommandTarget", false);
            SetSerializedBool(serializedObject, "TrackInterpolationDelay", false);
            SetSerializedBool(serializedObject, "GhostGroup", false);
            SetSerializedBool(serializedObject, "UsePreSerialization", false);
            SetSerializedBool(serializedObject, "DontUsePredictionBackup", false);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Component FindComponentByName(GameObject root, string componentTypeName)
        {
            if (root == null || string.IsNullOrEmpty(componentTypeName))
            {
                return null;
            }

            Component[] components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().Name == componentTypeName)
                {
                    return component;
                }
            }

            return null;
        }

        private static void SetSerializedString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetSerializedBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = value;
            }
        }

        private static void SetSerializedInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = value;
            }
            else if (property.propertyType == SerializedPropertyType.Enum)
            {
                // Enum values are not guaranteed to be contiguous popup indices.
                // This is especially important for flag enums such as
                // SupportedGhostModes, where 3 is a valid combined value but not
                // necessarily a valid enumValueIndex.
                property.intValue = value;
            }
        }

        private static void SetSerializedFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
            {
                property.floatValue = value;
            }
        }

        private static void SetSerializedColor(
            SerializedObject serializedObject,
            string propertyName,
            Color value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Color)
            {
                property.colorValue = value;
            }
        }

        private static void SetSerializedLong(
            SerializedObject serializedObject,
            string propertyName,
            long value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Integer)
            {
                property.longValue = value;
            }
        }

        private static void SetSpriteAssetAddress(
            SpriteAsset asset,
            long lowValue,
            long highValue)
        {
            if (asset == null)
            {
                throw new System.ArgumentNullException("asset");
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                throw new System.InvalidOperationException(
                    "The generated SpriteAsset address could not be assigned.");
            }

            low.longValue = lowValue;
            high.longValue = highValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureGeneratedBootstrapScript(
            DimensionRuntimePortalOutput portalOutput,
            string modDisplayName,
            string scriptFolder,
            DimensionBounds tileMapBounds,
            DimensionTemplateAsset template)
        {
            string className =
                SanitizeIdentifier(modDisplayName, "DimensionMod") +
                SanitizeIdentifier(portalOutput.DimensionId, "Dimension") +
                "RuntimeBootstrap";
            string path = scriptFolder + "/" + className + ".cs";

            // Resolved the same way DimensionItemGenerator resolves it — settings.metadata.name, or
            // empty when there is no ModBuilderSettings. Using the display-name helper instead would
            // fall back to a FOLDER name where the generator falls back to empty, and the two would
            // then qualify object names differently: every recipe would point at a name that does
            // not exist.
            ModBuilderSettings modSettings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(scriptFolder);
            string modName = modSettings == null ? string.Empty : (modSettings.metadata.name ?? string.Empty);

            string content =
                BuildBootstrapScript(portalOutput, className, tileMapBounds, template, modName);
            WriteTextAssetIfChanged(path, content);
        }

        private static void EnsurePortalTextDataBlocks(
            DimensionRuntimePortalOutput portalOutput,
            string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot) ||
                string.IsNullOrEmpty(portalOutput.PortalObjectName))
            {
                return;
            }

            string folder = modRoot + "/Data/TextDataBlock/Items";
            DimensionAssetFolders.Ensure(folder);

            string displayName = portalOutput.PortalDisplayName;
            string description = BuildPortalDescription(portalOutput);
            EnsurePortalTextDataBlockAsset(
                folder,
                portalOutput.PortalObjectName,
                displayName,
                description);

            string aliasObjectName = BuildPortalLocalizationAliasObjectName(portalOutput.PortalObjectName);
            if (!string.IsNullOrEmpty(aliasObjectName) &&
                !string.Equals(aliasObjectName, portalOutput.PortalObjectName, System.StringComparison.Ordinal))
            {
                EnsurePortalTextDataBlockAsset(
                    folder,
                    aliasObjectName,
                    displayName,
                    description);
            }
        }

        private static void EnsurePortalTextDataBlockAsset(
            string folder,
            string objectName,
            string displayName,
            string description)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            string path = folder + "/" + SanitizeAssetFileName(objectName, "DimensionPortal") + ".asset";
            string content = BuildPortalTextDataBlockYaml(objectName, displayName, description);
            WriteTextAssetIfChanged(path, content);
        }

        private static string BuildPortalTextDataBlockYaml(
            string objectName,
            string displayName,
            string description)
        {
            long addressLow = DimensionSpriteAssetAddress.Part(objectName, 0x6E756C6C666F7267UL);
            long addressHigh = DimensionSpriteAssetAddress.Part(objectName, 0x657870616E646E66UL);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("%YAML 1.1");
            builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            builder.AppendLine("--- !u!114 &11400000");
            builder.AppendLine("MonoBehaviour:");
            builder.AppendLine("  m_ObjectHideFlags: 0");
            builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
            builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
            builder.AppendLine("  m_GameObject: {fileID: 0}");
            builder.AppendLine("  m_Enabled: 1");
            builder.AppendLine("  m_EditorHideFlags: 0");
            builder.Append("  m_Script: {fileID: 2108018792, guid: ")
                .Append(TextDataBlockScriptGuid)
                .AppendLine(", type: 3}");
            builder.Append("  m_Name: ").AppendLine(ToUnityYamlString(objectName));
            builder.AppendLine("  m_EditorClassIdentifier: ");
            builder.AppendLine("  m_overload:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_address:");
            builder.Append("    m_low: ").AppendLine(addressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append("    m_high: ").AppendLine(addressHigh.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("  m_dynamicCollections:");
            builder.AppendLine("    m_list: []");
            builder.AppendLine("  m_localizedTexts:");
            builder.AppendLine("    keys:");
            AppendEnglishLanguageAddressListItem(builder);
            builder.AppendLine("    values:");
            builder.AppendLine("    - m_language:");
            builder.AppendLine("        m_address:");
            AppendEnglishLanguageAddress(builder, "          ");
            builder.Append("      title: ").AppendLine(ToUnityYamlString(displayName));
            builder.Append("      description: ").AppendLine(ToUnityYamlString(description));
            builder.AppendLine("  m_localizationHint: ");
            builder.AppendLine("  m_prevImportPrimaryEntry:");
            builder.AppendLine("    m_language:");
            builder.AppendLine("      m_address:");
            AppendEnglishLanguageAddress(builder, "        ");
            builder.Append("    title: ").AppendLine(ToUnityYamlString(displayName));
            builder.Append("    description: ").AppendLine(ToUnityYamlString(description));
            builder.AppendLine("  m_shouldBeLocalized: 1");
            builder.AppendLine("  m_header: Items");
            builder.AppendLine("  references:");
            builder.AppendLine("    version: 2");
            builder.AppendLine("    RefIds: []");
            return builder.ToString();
        }

        private static void AppendEnglishLanguageAddressListItem(StringBuilder builder)
        {
            builder.Append("    - m_low: ")
                .AppendLine(EnglishLanguageAddressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append("      m_high: ")
                .AppendLine(EnglishLanguageAddressHigh.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendEnglishLanguageAddress(StringBuilder builder, string indent)
        {
            builder.Append(indent)
                .Append("m_low: ")
                .AppendLine(EnglishLanguageAddressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append(indent)
                .Append("m_high: ")
                .AppendLine(EnglishLanguageAddressHigh.ToString(CultureInfo.InvariantCulture));
        }

        private static void EnsurePortalLocalization(
            DimensionRuntimePortalOutput portalOutput,
            string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot) ||
                string.IsNullOrEmpty(portalOutput.PortalObjectName))
            {
                return;
            }

            string localizationFolder = modRoot + "/Localization";
            DimensionAssetFolders.Ensure(localizationFolder);

            string path = localizationFolder + "/Localization.csv";
            string absolutePath = AssetPathToAbsolutePath(path);
            string displayName = portalOutput.PortalDisplayName;
            string description = BuildPortalDescription(portalOutput);
            List<GeneratedLocalizationRow> generatedRows = new List<GeneratedLocalizationRow>();
            AddPortalLocalizationRows(
                generatedRows,
                portalOutput.PortalObjectName,
                displayName,
                description);

            string aliasObjectName = BuildPortalLocalizationAliasObjectName(portalOutput.PortalObjectName);
            if (!string.IsNullOrEmpty(aliasObjectName) &&
                !string.Equals(aliasObjectName, portalOutput.PortalObjectName, System.StringComparison.Ordinal))
            {
                AddPortalLocalizationRows(
                    generatedRows,
                    aliasObjectName,
                    displayName,
                    description);
            }

            string content = BuildPortalLocalizationCsv(
                absolutePath,
                portalOutput.PortalObjectName,
                generatedRows);
            WriteTextAssetIfChanged(path, content);
        }

        private static void AddPortalLocalizationRows(
            List<GeneratedLocalizationRow> rows,
            string objectName,
            string displayName,
            string description)
        {
            if (rows == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            // The game's LocalizationManager replaces ':' with '_' in every term before lookup,
            // so keys must be written in that form or they can never resolve.
            string itemKey = "Items/" + objectName.Replace(':', '_');
            rows.Add(new GeneratedLocalizationRow(itemKey, displayName));
            rows.Add(new GeneratedLocalizationRow(itemKey + "Desc", description));
        }

        private static string BuildPortalLocalizationAliasObjectName(string portalObjectName)
        {
            const string portalSuffix = "_Portal";
            if (string.IsNullOrEmpty(portalObjectName) ||
                !portalObjectName.EndsWith(portalSuffix, System.StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return portalObjectName.Substring(0, portalObjectName.Length - portalSuffix.Length) + "Portal";
        }

        private static string BuildPortalLocalizationCsv(
            string absolutePath,
            string portalObjectName,
            List<GeneratedLocalizationRow> generatedRows)
        {
            const string fallbackHeader = "Key\tType\tDesc\tEnglish";
            List<string> lines = new List<string>();
            List<string> ownedKeys = BuildOwnedPortalLocalizationKeys(portalObjectName, generatedRows);
            if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
            {
                string[] existingLines = File.ReadAllText(absolutePath)
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split('\n');
                for (int i = 0; i < existingLines.Length; i++)
                {
                    string line = existingLines[i];
                    if (i == 0)
                    {
                        line = line.TrimStart('\ufeff');
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (lines.Count == 0 && !line.StartsWith("Key\t", System.StringComparison.Ordinal))
                    {
                        lines.Add(fallbackHeader);
                    }

                    if (CsvLineHasAnyKey(line, ownedKeys))
                    {
                        continue;
                    }

                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
            {
                lines.Add(fallbackHeader);
            }

            int columnCount = lines[0].Split('\t').Length;
            if (columnCount < 4)
            {
                lines[0] = fallbackHeader;
                columnCount = 4;
            }

            if (generatedRows != null)
            {
                foreach (GeneratedLocalizationRow row in generatedRows)
                {
                    lines.Add(BuildLocalizationRow(row.Key, row.EnglishText, columnCount));
                }
            }

            return string.Join("\n", lines.ToArray()) + "\n";
        }

        private static List<string> BuildOwnedPortalLocalizationKeys(
            string portalObjectName,
            List<GeneratedLocalizationRow> generatedRows)
        {
            List<string> ownedKeys = new List<string>();
            if (!string.IsNullOrEmpty(portalObjectName))
            {
                ownedKeys.Add("terms/" + portalObjectName);
                ownedKeys.Add("terms/" + portalObjectName + "Desc");

                // Colon-form keys are unresolvable (the game replaces ':' with '_' before every
                // lookup) — own them so stale rows written before the fix are dropped on rewrite.
                if (portalObjectName.IndexOf(':') >= 0)
                {
                    ownedKeys.Add("Items/" + portalObjectName);
                    ownedKeys.Add("Items/" + portalObjectName + "Desc");
                }
            }

            if (generatedRows != null)
            {
                foreach (GeneratedLocalizationRow row in generatedRows)
                {
                    if (!string.IsNullOrEmpty(row.Key))
                    {
                        ownedKeys.Add(row.Key);
                    }
                }
            }

            return ownedKeys;
        }

        private static bool CsvLineHasAnyKey(string line, List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            foreach (string key in keys)
            {
                if (CsvLineHasKey(line, key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CsvLineHasKey(string line, string key)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            int tabIndex = line.IndexOf('\t');
            string rowKey = tabIndex < 0 ? line : line.Substring(0, tabIndex);
            return string.Equals(rowKey, key, System.StringComparison.Ordinal);
        }

        private static string BuildLocalizationRow(
            string key,
            string englishText,
            int columnCount)
        {
            string[] columns = new string[Mathf.Max(4, columnCount)];
            columns[0] = SanitizeLocalizationCell(key);
            columns[1] = "Text";
            columns[2] = string.Empty;
            columns[3] = SanitizeLocalizationCell(englishText);
            return string.Join("\t", columns);
        }

        private readonly struct GeneratedLocalizationRow
        {
            public readonly string Key;
            public readonly string EnglishText;

            public GeneratedLocalizationRow(string key, string englishText)
            {
                Key = key ?? string.Empty;
                EnglishText = englishText ?? string.Empty;
            }
        }

        private static string SanitizeLocalizationCell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        private static string ToUnityYamlString(string value)
        {
            string sanitized = SanitizeLocalizationCell(value);
            return "\"" +
                sanitized
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"") +
                "\"";
        }

        private static string SanitizeAssetFileName(string value, string fallback)
        {
            string source = string.IsNullOrEmpty(value) ? fallback : value;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                bool invalid = c == '/' || c == '\\';
                for (int j = 0; j < invalidChars.Length && !invalid; j++)
                {
                    invalid = c == invalidChars[j];
                }

                builder.Append(invalid ? '_' : c);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        private static string BuildPortalDescription(DimensionRuntimePortalOutput portalOutput)
        {
            return "Portal to " + portalOutput.DimensionDisplayName + ".";
        }

        private static string BuildBootstrapScript(
            DimensionRuntimePortalOutput portalOutput,
            string className,
            DimensionBounds tileMapBounds,
            DimensionTemplateAsset template,
            string modName)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using ExpandNullforge.Api;");
            builder.AppendLine("using ExpandNullforge.Authoring;");
            builder.AppendLine("using ExpandNullforge.Creatures;");
            builder.AppendLine("using ExpandNullforge.Foundation;");
            builder.AppendLine("using ExpandNullforge.Loot;");
            builder.AppendLine("using ExpandNullforge.Plants;");
            builder.AppendLine("using ExpandNullforge.Portals;");
            builder.AppendLine("using ExpandNullforge.Scenes;");
            builder.AppendLine("using ExpandNullforge.Tilesets;");
            builder.AppendLine("using ExpandNullforge.Zones;");
            builder.AppendLine("using PugMod;");
            builder.AppendLine("using Unity.Mathematics;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.Append("public sealed class ").Append(className).AppendLine(" : IMod");
            builder.AppendLine("{");
            AppendConstant(builder, "DimensionId", portalOutput.DimensionId);
            AppendConstant(builder, "DimensionDisplayName", portalOutput.Dimension.DisplayName);
            AppendConstant(builder, "ContentPackId", portalOutput.ContentPack.ContentPackId);
            AppendConstant(builder, "ContentPackDisplayName", portalOutput.ContentPack.DisplayName);
            AppendConstant(builder, "ContentPackVersion", portalOutput.ContentPack.Version);
            AppendConstant(builder, "ContentPackAuthor", portalOutput.ContentPack.Author);
            AppendConstant(builder, "ContentPackDescription", portalOutput.ContentPack.Description);
            AppendIntConstant(builder, "ContentPackMinimumApiVersion", portalOutput.ContentPack.MinimumApiVersion);
            AppendIntConstant(builder, "DimensionAbsoluteOriginX", portalOutput.Dimension.AbsoluteOrigin.x);
            AppendIntConstant(builder, "DimensionAbsoluteOriginY", portalOutput.Dimension.AbsoluteOrigin.y);
            AppendIntConstant(builder, "DimensionLocalMinX", portalOutput.Dimension.LocalBounds.Min.x);
            AppendIntConstant(builder, "DimensionLocalMinY", portalOutput.Dimension.LocalBounds.Min.y);
            AppendIntConstant(builder, "DimensionLocalMaxX", portalOutput.Dimension.LocalBounds.MaxExclusive.x);
            AppendIntConstant(builder, "DimensionLocalMaxY", portalOutput.Dimension.LocalBounds.MaxExclusive.y);
            AppendIntConstant(builder, "DimensionGenerationVersion", portalOutput.Dimension.GenerationVersion);
            AppendIntConstant(builder, "DimensionTypeValue", (int)portalOutput.Dimension.Type);
            AppendIntConstant(builder, "DimensionCapabilitiesValue", (int)portalOutput.Dimension.Capabilities);
            AppendIntConstant(builder, "DimensionLifecycleStateValue", (int)portalOutput.Dimension.LifecycleState);
            AppendConstant(builder, "EntryPortalId", portalOutput.EntryPortal.PortalId);
            AppendConstant(builder, "ReturnPortalId", portalOutput.ReturnPortal.PortalId);
            AppendConstant(builder, "EntryPresentationId", portalOutput.EntryPresentation.PresentationId);
            AppendConstant(builder, "ReturnPresentationId", portalOutput.ReturnPresentation.PresentationId);
            AppendConstant(builder, "PortalObjectName", portalOutput.PortalObjectName);
            AppendConstant(builder, "ReturnPortalObjectName", portalOutput.ReturnPortalObjectName);
            AppendConstant(builder, "PortalDisplayName", portalOutput.PortalDisplayName);
            AppendConstant(builder, "PortalDescription", BuildPortalDescription(portalOutput));
            AppendConstant(builder, "EntryFromDimensionId", portalOutput.EntryPortal.FromDimensionId);
            AppendConstant(builder, "EntryToDimensionId", portalOutput.EntryPortal.ToDimensionId);
            AppendConstant(builder, "ReturnFromDimensionId", portalOutput.ReturnPortal.FromDimensionId);
            AppendConstant(builder, "ReturnToDimensionId", portalOutput.ReturnPortal.ToDimensionId);
            AppendFloatConstant(builder, "EntryFromLocalX", portalOutput.EntryPortal.FromLocalPosition.x);
            AppendFloatConstant(builder, "EntryFromLocalY", portalOutput.EntryPortal.FromLocalPosition.y);
            AppendFloatConstant(builder, "EntryToLocalX", portalOutput.EntryPortal.ToLocalPosition.x);
            AppendFloatConstant(builder, "EntryToLocalY", portalOutput.EntryPortal.ToLocalPosition.y);
            AppendFloatConstant(builder, "ReturnFromLocalX", portalOutput.ReturnPortal.FromLocalPosition.x);
            AppendFloatConstant(builder, "ReturnFromLocalY", portalOutput.ReturnPortal.FromLocalPosition.y);
            AppendFloatConstant(builder, "ReturnToLocalX", portalOutput.ReturnPortal.ToLocalPosition.x);
            AppendFloatConstant(builder, "ReturnToLocalY", portalOutput.ReturnPortal.ToLocalPosition.y);
            AppendFloatConstant(builder, "EntryCooldownSeconds", portalOutput.EntryPresentation.CooldownSeconds);
            AppendFloatConstant(builder, "ReturnCooldownSeconds", portalOutput.ReturnPresentation.CooldownSeconds);
            AppendBoolConstant(builder, "EntryRequireGeneratedArea", portalOutput.EntryPresentation.RequireGeneratedAreaOnUse);
            AppendBoolConstant(builder, "EntryAllowFallbackPosition", portalOutput.EntryPresentation.AllowFallbackPositionOnUse);
            AppendBoolConstant(builder, "EntryInteractable", portalOutput.EntryPresentation.Interactable);
            AppendBoolConstant(builder, "ReturnRequireGeneratedArea", portalOutput.ReturnPresentation.RequireGeneratedAreaOnUse);
            AppendBoolConstant(builder, "ReturnAllowFallbackPosition", portalOutput.ReturnPresentation.AllowFallbackPositionOnUse);
            AppendBoolConstant(builder, "ReturnInteractable", portalOutput.ReturnPresentation.Interactable);
            AppendIntConstant(builder, "ReturnRequiredMinX", portalOutput.ReturnRequiredBounds.Min.x);
            AppendIntConstant(builder, "ReturnRequiredMinY", portalOutput.ReturnRequiredBounds.Min.y);
            AppendIntConstant(builder, "ReturnRequiredMaxX", portalOutput.ReturnRequiredBounds.MaxExclusive.x);
            AppendIntConstant(builder, "ReturnRequiredMaxY", portalOutput.ReturnRequiredBounds.MaxExclusive.y);
            AppendConstant(builder, "StarterId", portalOutput.StarterId);
            AppendIntConstant(builder, "StarterGenerationMinX", portalOutput.StarterGenerationBounds.Min.x);
            AppendIntConstant(builder, "StarterGenerationMinY", portalOutput.StarterGenerationBounds.Min.y);
            AppendIntConstant(builder, "StarterGenerationMaxX", portalOutput.StarterGenerationBounds.MaxExclusive.x);
            AppendIntConstant(builder, "StarterGenerationMaxY", portalOutput.StarterGenerationBounds.MaxExclusive.y);
            AppendIntConstant(builder, "StarterLandingMinX", portalOutput.StarterTargetLandingBounds.Min.x);
            AppendIntConstant(builder, "StarterLandingMinY", portalOutput.StarterTargetLandingBounds.Min.y);
            AppendIntConstant(builder, "StarterLandingMaxX", portalOutput.StarterTargetLandingBounds.MaxExclusive.x);
            AppendIntConstant(builder, "StarterLandingMaxY", portalOutput.StarterTargetLandingBounds.MaxExclusive.y);
            AppendFloatConstant(builder, "CraftingTimeSeconds", portalOutput.CraftingTimeSeconds);
            builder.AppendLine();
            builder.AppendLine("  private readonly List<DimensionRuntimeManifestAsset> manifests =");
            builder.AppendLine("      new List<DimensionRuntimeManifestAsset>();");
            builder.AppendLine("  private IDimensionService service;");
            builder.AppendLine("  private bool staticRuntimeExtrasRegistered;");
            builder.AppendLine("  private bool minimumRuntimeDefinitionsRegistered;");
            builder.AppendLine("  private bool manifestsApplied;");
            builder.AppendLine("  private bool portalDefinitionsRegistered;");
            builder.AppendLine("  private int nextApplyFrame;");
            builder.AppendLine("  private string lastFailureCode = string.Empty;");
            builder.AppendLine();
            builder.AppendLine("  private bool RuntimeOutputReady");
            builder.AppendLine("  {");
            builder.AppendLine("    get");
            builder.AppendLine("    {");
            builder.AppendLine("      return staticRuntimeExtrasRegistered &&");
            builder.AppendLine("          minimumRuntimeDefinitionsRegistered &&");
            builder.AppendLine("          manifestsApplied &&");
            builder.AppendLine("          portalDefinitionsRegistered;");
            builder.AppendLine("    }");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  public void EarlyInit()");
            builder.AppendLine("  {");
            builder.AppendLine("    EnsureStaticRuntimeExtras();");
            builder.AppendLine("    TryApplyRuntimeOutput();");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  public void Init()");
            builder.AppendLine("  {");
            builder.AppendLine("    EnsureStaticRuntimeExtras();");
            builder.AppendLine("    TryApplyRuntimeOutput();");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  public void Shutdown()");
            builder.AppendLine("  {");
            builder.AppendLine("    manifests.Clear();");
            builder.AppendLine("    service = null;");
            builder.AppendLine("    staticRuntimeExtrasRegistered = false;");
            builder.AppendLine("    minimumRuntimeDefinitionsRegistered = false;");
            builder.AppendLine("    manifestsApplied = false;");
            builder.AppendLine("    portalDefinitionsRegistered = false;");
            builder.AppendLine("    nextApplyFrame = 0;");
            builder.AppendLine("    lastFailureCode = string.Empty;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  public void ModObjectLoaded(UnityEngine.Object obj)");
            builder.AppendLine("  {");
            builder.AppendLine("    // Custom tilesets register the moment their asset loads — mod load runs before");
            builder.AppendLine("    // the ECS worlds are created, so rendering, placement and the map-color table");
            builder.AppendLine("    // are all wired before any tile of the set can appear.");
            builder.AppendLine("    DimensionTilesetAsset tilesetAsset = obj as DimensionTilesetAsset;");
            builder.AppendLine("    if (tilesetAsset != null)");
            builder.AppendLine("    {");
            builder.AppendLine("      DimensionTilesetAssetRuntime.Register(tilesetAsset);");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    // A condition must claim its number BEFORE the game builds its condition");
            builder.AppendLine("    // table during world conversion; asset load is the only window. Without this");
            builder.AppendLine("    // branch a shipped mod's conditions never register — the baked ConditionIDs");
            builder.AppendLine("    // on its items would point past the game's table into nothing.");
            builder.AppendLine("    ExpandNullforge.Authoring.DimensionConditionAsset conditionAsset =");
            builder.AppendLine("        obj as ExpandNullforge.Authoring.DimensionConditionAsset;");
            builder.AppendLine("    if (conditionAsset != null)");
            builder.AppendLine("    {");
            builder.AppendLine("      ExpandNullforge.Conditions.DimensionConditionAssetRuntime.Register(conditionAsset);");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionRuntimeManifestAsset manifest = obj as DimensionRuntimeManifestAsset;");
            builder.AppendLine("    if (manifest == null)");
            builder.AppendLine("    {");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!manifests.Contains(manifest))");
            builder.AppendLine("    {");
            builder.AppendLine("      manifests.Add(manifest);");
            builder.AppendLine("      manifestsApplied = false;");
            builder.AppendLine("      portalDefinitionsRegistered = false;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    // The sprite half of the boss presentation: pin icons and body sprites are");
            builder.AppendLine("    // Unity objects that only exist now, with the bundle loaded. The names and");
            builder.AppendLine("    // terms were baked above; this joins the two halves.");
            builder.AppendLine("    DimensionBossPresentationRegistry.AttachIcons(manifest.SourceTemplate);");
            builder.AppendLine();
            builder.AppendLine("    // Talent pictures are the same shape of problem as the boss pins: the file the");
            builder.AppendLine("    // game takes a mod's talents from carries a name, an effect and a value, and no");
            builder.AppendLine("    // field for a picture. The sprite only exists once the bundle is loaded, so it");
            builder.AppendLine("    // is read off the template here and handed to the talent window as it draws.");
            builder.AppendLine("    ExpandNullforge.Skills.DimensionTalentIconRegistry.AttachFrom(manifest.SourceTemplate);");
            builder.AppendLine();
            builder.AppendLine("    // Skill pictures and a pet's colours are the same shape again: both are Unity");
            builder.AppendLine("    // objects rather than numbers, so neither can be written into generated source.");
            builder.AppendLine("    // They are read off the template here and answered where the game asks for them.");
            builder.AppendLine("    ExpandNullforge.Skills.DimensionSkillIconRegistry.AttachFrom(");
            builder.AppendLine("        manifest.SourceTemplate, ExpandNullforge.Foundation.DimensionFrameworkLog.Warning);");
            // The mod's own name goes with the pet colours because a mob id is an OBJECT name, and
            // the object the generator wrote is registered under the qualified form. Every other
            // reader of a mob id qualifies it at generate time; this one is read off the template
            // at load, so the name has to travel with it or a plainly written id finds nothing.
            builder.AppendLine("    ExpandNullforge.Creatures.DimensionPetSkinRegistry.AttachFrom(");
            builder.AppendLine("        manifest.SourceTemplate, ExpandNullforge.Foundation.DimensionFrameworkLog.Warning,");
            builder.AppendLine("        " + ToCSharpString(modName) + ");");
            builder.AppendLine();
            builder.AppendLine("    // Register the painted tile map the moment the manifest asset loads — before the");
            builder.AppendLine("    // world generates the dimension area. DimensionTileMapRegistry is a plain static");
            builder.AppendLine("    // store, so this does not need the dimension service (not ready this early);");
            builder.AppendLine("    // registering here lets the tile-map provider win over the flat safe platform.");
            builder.AppendLine("    if (manifest.HasTileMap)");
            builder.AppendLine("    {");
            builder.AppendLine("      DimensionTileMapRegistry.Register(");
            builder.AppendLine("          manifest.GeneratedFromDimensionId, manifest.TileMap);");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    TryApplyRuntimeOutput();");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  public void Update()");
            builder.AppendLine("  {");
            builder.AppendLine("    if (RuntimeOutputReady)");
            builder.AppendLine("    {");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    EnsureStaticRuntimeExtras();");
            builder.AppendLine("    TryApplyRuntimeOutput();");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private void TryApplyRuntimeOutput()");
            builder.AppendLine("  {");
            builder.AppendLine("    EnsureStaticRuntimeExtras();");
            builder.AppendLine("    if (Time.frameCount < nextApplyFrame)");
            builder.AppendLine("    {");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    IDimensionService current;");
            builder.AppendLine("    if (!DimensionApi.TryGetService(out current) || current == null)");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!object.ReferenceEquals(service, current))");
            builder.AppendLine("    {");
            builder.AppendLine("      service = current;");
            builder.AppendLine("      minimumRuntimeDefinitionsRegistered = false;");
            builder.AppendLine("      manifestsApplied = false;");
            builder.AppendLine("      portalDefinitionsRegistered = false;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    bool minimumRuntimeReady = EnsureMinimumRuntimeDefinitions(current);");
            builder.AppendLine("    if (minimumRuntimeReady)");
            builder.AppendLine("    {");
            builder.AppendLine("      RegisterPortalDefinitions(current);");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    bool manifestsReady = ApplyManifests(current);");
            builder.AppendLine("    if (!minimumRuntimeReady || !manifestsReady)");
            builder.AppendLine("    {");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private void EnsureStaticRuntimeExtras()");
            builder.AppendLine("  {");
            builder.AppendLine("    if (staticRuntimeExtrasRegistered)");
            builder.AppendLine("    {");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            // The placed portal (V1) is only craftable while an enabled placed-portal rule says
            // so — this is what makes the Portal Studio's Enabled toggle real for V1. (A template
            // without any placed rule keeps the legacy always-craftable default.)
            if (IsPlacedPortalCraftable(template))
            {
                builder.AppendLine("    DimensionCraftingRegistry.Register(");
                builder.AppendLine("        new DimensionCraftingRecipeDefinition(");
                builder.AppendLine("            PortalObjectName,");
                builder.Append("            ")
                    .Append(ResolveCraftingStationArgument(
                        FindCraftablePlacedRule(template),
                        "the placed portal"))
                    .AppendLine(",");
                builder.AppendLine("            1,");
                builder.AppendLine("            CraftingTimeSeconds,");
                builder.AppendLine("            PortalDisplayName));");
            }
            builder.AppendLine("    DimensionPortalItemPresentationRegistry.Register(");
            builder.AppendLine("        new DimensionPortalItemPresentationDefinition(");
            builder.AppendLine("            PortalObjectName,");
            builder.AppendLine("            PortalDisplayName,");
            builder.AppendLine("            PortalDescription,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            0,");
            builder.AppendLine("            true));");
            builder.AppendLine("    DimensionReturnPortalSpawnRegistry.Register(");
            builder.AppendLine("        new DimensionReturnPortalSpawnDefinition(");
            builder.AppendLine("            ReturnPortalId,");
            builder.AppendLine("            DimensionId,");
            builder.AppendLine("            ReturnPortalObjectName,");
            builder.AppendLine("            ReturnToDimensionId,");
            builder.AppendLine("            new float2(ReturnFromLocalX, ReturnFromLocalY),");
            builder.AppendLine("            new float2(ReturnToLocalX, ReturnToLocalY),");
            builder.AppendLine("            new DimensionBounds(");
            builder.AppendLine("                new int2(ReturnRequiredMinX, ReturnRequiredMinY),");
            builder.AppendLine("                new int2(ReturnRequiredMaxX, ReturnRequiredMaxY)),");
            builder.AppendLine("            ReturnCooldownSeconds,");
            builder.AppendLine("            ReturnRequireGeneratedArea,");
            builder.AppendLine("            ReturnAllowFallbackPosition,");
            builder.AppendLine("            PortalDisplayName + \" Return\",");
            builder.AppendLine("            ReturnInteractable,");
            // Only an Arena's exit waits to be earned; every other type keeps the always-on
            // guarantee that a dimension can never trap a player.
            builder.AppendLine(
                "            armedByVictory: " +
                (portalOutput.Dimension.Type == DimensionType.Arena ? "true" : "false") + "));");

            // Portal sounds, straight from the template's dashboard settings. The placed portal
            // only has an activation sound (Peak mode by construction); the instant portal uses
            // whichever exclusive mode the creator chose. The return portal shares the placed
            // portal's activation sound — same object family, same "lights up" moment.
            string placedActivationSound = template == null ? string.Empty : template.PlacedPortalActivationSound;
            int instantSoundMode = template == null ? 0 : template.InstantPortalSoundMode;
            string instantActivationSound = template == null ? string.Empty : template.InstantPortalActivationSound;
            string instantDeactivationSound = template == null ? string.Empty : template.InstantPortalDeactivationSound;
            string instantLoopSound = template == null ? string.Empty : template.InstantPortalLoopSound;
            builder.AppendLine("    DimensionPortalSoundRegistry.Register(");
            builder.AppendLine("        PortalObjectName,");
            builder.AppendLine("        0,");
            builder.Append("        ").Append(ToCSharpString(placedActivationSound)).AppendLine(",");
            builder.AppendLine("        string.Empty,");
            builder.AppendLine("        string.Empty);");
            builder.AppendLine("    DimensionPortalSoundRegistry.Register(");
            builder.AppendLine("        ReturnPortalObjectName,");
            builder.AppendLine("        0,");
            builder.Append("        ").Append(ToCSharpString(placedActivationSound)).AppendLine(",");
            builder.AppendLine("        string.Empty,");
            builder.AppendLine("        string.Empty);");
            builder.AppendLine("    DimensionPortalSoundRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(portalOutput.ItemPortalObjectName)).AppendLine(",");
            builder.Append("        ").Append(instantSoundMode.ToString(System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(instantActivationSound)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(instantDeactivationSound)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(instantLoopSound)).AppendLine(");");
            if (pendingPortalDrops.TryGetValue(portalOutput.DimensionId, out List<PortalDropEmission> portalDrops) &&
                portalDrops.Count > 0)
            {
                System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                for (int i = 0; i < portalDrops.Count; i++)
                {
                    PortalDropEmission drop = portalDrops[i];

                    // A portal key can be set to drop off one of the mod's own creatures like any
                    // other item, so both names go through the same qualifier the authored-drop
                    // walk uses, and the source's table travels with the row for the same reason.
                    string portalDropSource = IsModOwnedObjectName(template, drop.Target)
                        ? DimensionObjectNamespace.Qualify(modName, drop.Target)
                        : drop.Target;
                    string portalDropItem = IsModOwnedObjectName(template, drop.Item)
                        ? DimensionObjectNamespace.Qualify(modName, drop.Item)
                        : drop.Item;
                    string portalDropTable = SourceLootTableNameOf(template, modName, drop.Target);

                    builder.AppendLine("    DimensionPortalDropRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(portalDropSource)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(portalDropItem)).AppendLine(",");
                    builder.Append("        ").Append(drop.Weight.ToString(inv)).AppendLine("f,");
                    builder.Append("        ").Append(drop.Chance.ToString(inv)).AppendLine("f,");
                    builder.Append("        ").Append(drop.Min.ToString(inv)).AppendLine(",");
                    builder.Append("        ").Append(drop.Max.ToString(inv)).AppendLine(",");
                    builder.AppendLine("        string.Empty,");
                    builder.Append("        ").Append(ToCSharpString(portalDropTable)).AppendLine(");");
                }
            }

            if (pendingItemPortals.TryGetValue(portalOutput.DimensionId, out List<PortalItemEmission> itemPortalEmissions) &&
                itemPortalEmissions.Count > 0)
            {
                System.Globalization.CultureInfo invItem = System.Globalization.CultureInfo.InvariantCulture;
                for (int i = 0; i < itemPortalEmissions.Count; i++)
                {
                    PortalItemEmission ip = itemPortalEmissions[i];
                    builder.AppendLine("    DimensionItemPortalRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(ip.Item)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(ip.PortalObject)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(ip.PortalId)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(ip.ToDimension)).AppendLine(",");
                    builder.Append("        ").Append(ip.Duration.ToString(invItem)).AppendLine("f);");

                    // The item portal is craftable at whichever station its own rule names, and at
                    // the Wooden Workbench when it names none. Ingredients come from the item's own
                    // InventoryItem authoring (empty unless the creator adds a recipe that outputs it).
                    if (ip.Craftable)
                    {
                        builder.AppendLine("    DimensionCraftingRegistry.Register(");
                        builder.AppendLine("        new DimensionCraftingRecipeDefinition(");
                        builder.Append("            ").Append(ToCSharpString(ip.Item)).AppendLine(",");
                        builder.Append("            ")
                            .Append(ResolveCraftingStationArgument(ip.Station, "the item portal"))
                            .AppendLine(",");
                        builder.AppendLine("            1,");
                        builder.AppendLine("            CraftingTimeSeconds,");
                        builder.Append("            ").Append(ToCSharpString(ip.DisplayName)).AppendLine("));");
                    }
                }
            }

            SayWhenOneOfOursSharesAGameObjectsName(template);

            AppendPortalWorldSceneRegistration(builder, template, portalOutput, modName);
            AppendBlockCraftingRegistrations(builder, template);
            AppendRegionTitleRegistrations(builder, template, modName);
            AppendOreBiomeGateRegistrations(builder, template, modName);
            AppendTerrainMaterialRegistrations(builder, template);
            AppendCreatureSpawnRegistrations(builder, template, modName);
            AppendBossPhaseRegistrations(builder, template, modName);
            AppendBossPresentationRegistrations(builder, template, modName);
            AppendLootTableRegistrations(builder, template, modName);
            AppendRespawnRegistrations(builder, template, modName);
            AppendDungeonRegistrations(builder, template, modName);
            AppendRecipeCraftingRegistrations(builder, template, modName);
            AppendAuthoredDropRegistrations(builder, template, modName);
            AppendSceneRegistrations(builder, template, modName);
            // The domains that live in their own partial files. Each one is the single line
            // that makes its emission reachable — a partial with no caller is exactly the
            // written-and-never-run failure this framework keeps finding.
            AppendCreaturePresentationRegistrations(builder, template, modName);
            AppendPlantPresentationRegistrations(builder, template, modName);
            AppendEmittedLightRegistrations(builder, template, modName);
            AppendFoodRegistrations(builder, template, modName);
            AppendExplosiveRegistrations(builder, template, modName);
            AppendObjectLinkRegistrations(builder, template, modName);
            AppendWorldRulesRegistrations(builder, template, modName);
            AppendSkillExperienceRegistrations(builder, template, modName);
            AppendDimensionMusicRegistrations(builder, template);

            builder.AppendLine("    staticRuntimeExtrasRegistered = true;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private bool EnsureMinimumRuntimeDefinitions(IDimensionService current)");
            builder.AppendLine("  {");
            builder.AppendLine("    if (minimumRuntimeDefinitionsRegistered)");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionContentPackDefinition existingContentPack;");
            builder.AppendLine("    if (!current.TryGetContentPack(ContentPackId, out existingContentPack))");
            builder.AppendLine("    {");
            builder.AppendLine("      DimensionOperationResult result;");
            builder.AppendLine("      if (!current.TryRegisterContentPack(");
            builder.AppendLine("          new DimensionContentPackDefinition(");
            builder.AppendLine("              ContentPackId,");
            builder.AppendLine("              ContentPackDisplayName,");
            builder.AppendLine("              ContentPackVersion,");
            builder.AppendLine("              ContentPackAuthor,");
            builder.AppendLine("              ContentPackDescription,");
            builder.AppendLine("              ContentPackMinimumApiVersion,");
            builder.AppendLine("              CreateContentPackDependencyIds(),");
            builder.AppendLine("              true),");
            builder.AppendLine("          out result))");
            builder.AppendLine("      {");
            builder.AppendLine("        WarnOnce(result.Code, result.Message);");
            builder.AppendLine("        ScheduleRetry();");
            builder.AppendLine("        return false;");
            builder.AppendLine("      }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionDefinition existingDimension;");
            builder.AppendLine("    if (!current.TryGetDimension(DimensionId, out existingDimension))");
            builder.AppendLine("    {");
            builder.AppendLine("      DimensionOperationResult result;");
            builder.AppendLine("      if (!current.TryRegisterDimension(CreateDimensionDefinition(), out result))");
            builder.AppendLine("      {");
            builder.AppendLine("        WarnOnce(result.Code, result.Message);");
            builder.AppendLine("        ScheduleRetry();");
            builder.AppendLine("        return false;");
            builder.AppendLine("      }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!TryEnsureStarter(current, CreateStarterDefinition()))");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return false;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!EnsureMinimumZones(current))");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return false;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!EnsureMinimumGenerationPasses(current))");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return false;");
            builder.AppendLine("    }");
            builder.AppendLine();

            // Last, and deliberately so: the pin can only put an older layout back once the current
            // one is fully in place, because it works by replacing zones rather than pre-empting them.
            builder.AppendLine("    RegisterLayoutVersions();");
            builder.AppendLine("    DimensionLayoutPinService.ApplyForCurrentWorld(current);");
            builder.AppendLine();
            builder.AppendLine("    minimumRuntimeDefinitionsRegistered = true;");
            builder.AppendLine("    return true;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private static DimensionDefinition CreateDimensionDefinition()");
            builder.AppendLine("  {");
            builder.AppendLine("    return new DimensionDefinition(");
            builder.AppendLine("        DimensionId,");
            builder.AppendLine("        DimensionDisplayName,");
            builder.AppendLine("        new int2(DimensionAbsoluteOriginX, DimensionAbsoluteOriginY),");
            builder.AppendLine("        new DimensionBounds(");
            builder.AppendLine("            new int2(DimensionLocalMinX, DimensionLocalMinY),");
            builder.AppendLine("            new int2(DimensionLocalMaxX, DimensionLocalMaxY)),");
            builder.AppendLine("        DimensionGenerationVersion,");
            builder.AppendLine("        DimensionTypeMigration.Normalize(DimensionTypeValue),");
            builder.AppendLine("        (DimensionCapabilityFlags)DimensionCapabilitiesValue,");
            builder.AppendLine("        (DimensionLifecycleState)DimensionLifecycleStateValue);");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private static DimensionStarterDefinition CreateStarterDefinition()");
            builder.AppendLine("  {");
            builder.AppendLine("    return new DimensionStarterDefinition(");
            builder.AppendLine("        StarterId,");
            builder.AppendLine("        DimensionId,");
            builder.AppendLine("        DimensionDisplayName + \" Starter\",");
            builder.AppendLine("        \"Starter generation and travel loop for \" + DimensionDisplayName + \" Starter.\",");
            builder.AppendLine("        ContentPackId,");
            builder.AppendLine("        true,");
            builder.AppendLine("        new DimensionContentReadinessRequest(");
            builder.AppendLine("            StarterId + \".readiness\",");
            builder.AppendLine("            DimensionDisplayName + \" Starter Readiness\",");
            builder.AppendLine("            new List<DimensionContentReadinessRequirement>()),");
            builder.AppendLine("        new DimensionTravelLoopPreflightRequest(");
            builder.AppendLine("            EntryFromDimensionId,");
            builder.AppendLine("            EntryToDimensionId,");
            builder.AppendLine("            EntryPortalId,");
            builder.AppendLine("            ReturnPortalId,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            new DimensionBounds(");
            builder.AppendLine("                new int2(StarterLandingMinX, StarterLandingMinY),");
            builder.AppendLine("                new int2(StarterLandingMaxX, StarterLandingMaxY)),");
            builder.AppendLine("            true,");
            builder.AppendLine("            false,");
            builder.AppendLine("            false,");
            builder.AppendLine("            false,");
            builder.AppendLine("            true,");
            builder.AppendLine("            false),");
            builder.AppendLine("        new DimensionStarterGenerationRequest(");
            builder.AppendLine("            StarterId,");
            builder.AppendLine("            \"dimension-starter:\" + StarterId,");
            builder.AppendLine("            DimensionId,");
            builder.AppendLine("            ResolveStarterGenerationBounds(),");
            builder.AppendLine("            100,");
            builder.AppendLine("            true,");
            builder.AppendLine("            \"Starter generation area for \" + DimensionDisplayName + \" Starter.\"));");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private static DimensionBounds ResolveStarterGenerationBounds()");
            builder.AppendLine("  {");
            builder.AppendLine("    // A painted tile map defines the biome's extent; generate exactly that region so the");
            builder.AppendLine("    // whole authored map lands and nothing is clipped. Fall back to the authored starter");
            builder.AppendLine("    // area when the dimension has no painted map (a flat safe platform is generated there).");
            builder.AppendLine("    DimensionTileMapModel tileMap;");
            builder.AppendLine("    if (DimensionTileMapRegistry.TryGet(DimensionId, out tileMap) &&");
            builder.AppendLine("        tileMap != null && tileMap.PaintedTileCount() > 0)");
            builder.AppendLine("    {");
            builder.AppendLine("      return tileMap.LocalBounds;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    return new DimensionBounds(");
            builder.AppendLine("        new int2(StarterGenerationMinX, StarterGenerationMinY),");
            builder.AppendLine("        new int2(StarterGenerationMaxX, StarterGenerationMaxY));");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private static List<string> CreateContentPackDependencyIds()");
            builder.AppendLine("  {");
            builder.AppendLine("    List<string> dependencies = new List<string>();");
            IReadOnlyList<string> dependencyIds = portalOutput.ContentPack.DependencyIds;
            if (dependencyIds != null)
            {
                for (int i = 0; i < dependencyIds.Count; i++)
                {
                    if (string.IsNullOrEmpty(dependencyIds[i]))
                    {
                        continue;
                    }

                    builder.Append("    dependencies.Add(");
                    builder.Append(ToCSharpString(dependencyIds[i]));
                    builder.AppendLine(");");
                }
            }

            builder.AppendLine("    return dependencies;");
            builder.AppendLine("  }");
            builder.AppendLine();
            AppendMinimumZonesMethod(builder, portalOutput, tileMapBounds);
            AppendMinimumGenerationPassesMethod(builder, portalOutput, tileMapBounds);
            AppendLayoutVersionsMethod(builder, portalOutput, template);
            builder.AppendLine("  private bool ApplyManifests(IDimensionService current)");
            builder.AppendLine("  {");
            builder.AppendLine("    if (manifestsApplied)");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (manifests.Count == 0)");
            builder.AppendLine("    {");
            builder.AppendLine("      manifestsApplied = true;");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    for (int i = 0; i < manifests.Count; i++)");
            builder.AppendLine("    {");
            builder.AppendLine("      DimensionRuntimeManifestAsset manifest = manifests[i];");
            builder.AppendLine("      if (manifest == null)");
            builder.AppendLine("      {");
            builder.AppendLine("        continue;");
            builder.AppendLine("      }");
            builder.AppendLine();
            builder.AppendLine("      // Declare generated items so the framework can name any that never");
            builder.AppendLine("      // registered with the game. Reporting is deliberately not done here:");
            builder.AppendLine("      // object ids can still arrive after this point.");
            builder.AppendLine("      DimensionItemObjectRegistry.Declare(");
            builder.AppendLine("          manifest.GeneratedFromContentPackId, manifest.GeneratedItemIds);");
            builder.AppendLine();
            builder.AppendLine("      // Everything else this pack generates an object under — creatures, bosses,");
            builder.AppendLine("      // summoning circles, plants, containers, workbenches, world objects. Nothing");
            builder.AppendLine("      // complains about one of these that the game does not answer to; the list is");
            builder.AppendLine("      // what the world-load check walks to see whether each finished object carries");
            builder.AppendLine("      // what the game's own systems require of it.");
            builder.AppendLine("      DimensionGeneratedObjectLedger.Declare(");
            builder.AppendLine("          manifest.GeneratedFromContentPackId, manifest.GeneratedObjectIds);");
            builder.AppendLine();
            builder.AppendLine("      // The dimension's painted tile map is registered early in ModObjectLoaded, before");
            builder.AppendLine("      // world generation — not here, which runs too late in the service-gated apply loop.");
            builder.AppendLine();
            builder.AppendLine("      DimensionContentManifestResult applyResult;");
            builder.AppendLine("      DimensionOperationResult buildResult;");
            builder.AppendLine("      if (!manifest.TryApplyTo(");
            builder.AppendLine("          current,");
            builder.AppendLine("          \"Apply generated dimension runtime manifest.\",");
            builder.AppendLine("          out applyResult,");
            builder.AppendLine("          out buildResult))");
            builder.AppendLine("      {");
            builder.AppendLine("        string code = buildResult.Success ? \"manifest-apply-failed\" : buildResult.Code;");
            builder.AppendLine("        string message = buildResult.Success");
            builder.AppendLine("            ? \"The generated dimension manifest could not be applied.\"");
            builder.AppendLine("            : buildResult.Message;");
            builder.AppendLine("        WarnOnce(code, message);");
            builder.AppendLine("        if (code == \"runtime-manifest-snapshot-missing\" || code == \"template-null\")");
            builder.AppendLine("        {");
            builder.AppendLine("          continue;");
            builder.AppendLine("        }");
            builder.AppendLine("        ScheduleRetry();");
            builder.AppendLine("        return false;");
            builder.AppendLine("      }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    manifestsApplied = true;");
            builder.AppendLine("    lastFailureCode = string.Empty;");
            builder.AppendLine("    return true;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private void RegisterPortalDefinitions(IDimensionService current)");
            builder.AppendLine("  {");
            builder.AppendLine("    if (portalDefinitionsRegistered)");
            builder.AppendLine("    {");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!TryEnsurePortal(");
            builder.AppendLine("        current,");
            builder.AppendLine("        new DimensionPortalDefinition(");
            builder.AppendLine("            EntryPortalId,");
            builder.AppendLine("            PortalDisplayName,");
            builder.AppendLine("            EntryFromDimensionId,");
            builder.AppendLine("            new float2(EntryFromLocalX, EntryFromLocalY),");
            builder.AppendLine("            EntryToDimensionId,");
            builder.AppendLine("            new float2(EntryToLocalX, EntryToLocalY),");
            builder.AppendLine("            DimensionPortalState.Available)))");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!TryEnsurePortal(");
            builder.AppendLine("        current,");
            builder.AppendLine("        new DimensionPortalDefinition(");
            builder.AppendLine("            ReturnPortalId,");
            builder.AppendLine("            PortalDisplayName + \" Return\",");
            builder.AppendLine("            ReturnFromDimensionId,");
            builder.AppendLine("            new float2(ReturnFromLocalX, ReturnFromLocalY),");
            builder.AppendLine("            ReturnToDimensionId,");
            builder.AppendLine("            new float2(ReturnToLocalX, ReturnToLocalY),");
            builder.AppendLine("            DimensionPortalState.Available)))");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!TryEnsurePortalPresentation(");
            builder.AppendLine("        current,");
            builder.AppendLine("        new DimensionPortalPresentationDefinition(");
            builder.AppendLine("            EntryPresentationId,");
            builder.AppendLine("            EntryPortalId,");
            builder.AppendLine("            PortalDisplayName,");
            builder.AppendLine("            \"Enter \" + PortalDisplayName,");
            builder.AppendLine("            \"The portal is not active yet.\",");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            EntryCooldownSeconds,");
            builder.AppendLine("            0,");
            builder.AppendLine("            true,");
            builder.AppendLine("            EntryRequireGeneratedArea,");
            builder.AppendLine("            EntryAllowFallbackPosition,");
            builder.AppendLine("            EntryInteractable)))");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    if (!TryEnsurePortalPresentation(");
            builder.AppendLine("        current,");
            builder.AppendLine("        new DimensionPortalPresentationDefinition(");
            builder.AppendLine("            ReturnPresentationId,");
            builder.AppendLine("            ReturnPortalId,");
            builder.AppendLine("            PortalDisplayName + \" Return\",");
            builder.AppendLine("            \"Return to the core.\",");
            builder.AppendLine("            \"The return portal is not active yet.\",");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            string.Empty,");
            builder.AppendLine("            ReturnCooldownSeconds,");
            builder.AppendLine("            0,");
            builder.AppendLine("            true,");
            builder.AppendLine("            ReturnRequireGeneratedArea,");
            builder.AppendLine("            ReturnAllowFallbackPosition,");
            builder.AppendLine("            ReturnInteractable)))");
            builder.AppendLine("    {");
            builder.AppendLine("      ScheduleRetry();");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            if (pendingItemPortals.TryGetValue(portalOutput.DimensionId, out List<PortalItemEmission> itemPortalDefinitions) &&
                itemPortalDefinitions.Count > 0)
            {
                for (int i = 0; i < itemPortalDefinitions.Count; i++)
                {
                    PortalItemEmission ip = itemPortalDefinitions[i];
                    string ipPortalId = ToCSharpString(ip.PortalId);
                    string ipDisplayName = string.IsNullOrEmpty(ip.DisplayName)
                        ? "PortalDisplayName"
                        : ToCSharpString(ip.DisplayName);
                    builder.AppendLine("    // Instant item portal (V2): the spawned temporary portal carries this id,");
                    builder.AppendLine("    // so travel must resolve it like any other portal. It lands at the entry");
                    builder.AppendLine("    // portal's arrival point.");
                    builder.AppendLine("    if (!TryEnsurePortal(");
                    builder.AppendLine("        current,");
                    builder.AppendLine("        new DimensionPortalDefinition(");
                    builder.AppendLine("            " + ipPortalId + ",");
                    builder.AppendLine("            " + ipDisplayName + ",");
                    builder.AppendLine("            EntryFromDimensionId,");
                    builder.AppendLine("            float2.zero,");
                    builder.AppendLine("            " + ToCSharpString(ip.ToDimension) + ",");
                    builder.AppendLine("            new float2(EntryToLocalX, EntryToLocalY),");
                    builder.AppendLine("            DimensionPortalState.Available)))");
                    builder.AppendLine("    {");
                    builder.AppendLine("      ScheduleRetry();");
                    builder.AppendLine("      return;");
                    builder.AppendLine("    }");
                    builder.AppendLine();
                    builder.AppendLine("    if (!TryEnsurePortalPresentation(");
                    builder.AppendLine("        current,");
                    builder.AppendLine("        new DimensionPortalPresentationDefinition(");
                    builder.AppendLine("            " + ipPortalId + " + \".presentation\",");
                    builder.AppendLine("            " + ipPortalId + ",");
                    builder.AppendLine("            " + ipDisplayName + ",");
                    builder.AppendLine("            \"Enter \" + " + ipDisplayName + ",");
                    builder.AppendLine("            \"The portal is not active yet.\",");
                    builder.AppendLine("            string.Empty,");
                    builder.AppendLine("            string.Empty,");
                    builder.AppendLine("            string.Empty,");
                    builder.AppendLine("            0f,");
                    builder.AppendLine("            0,");
                    builder.AppendLine("            true,");
                    builder.AppendLine("            EntryRequireGeneratedArea,");
                    builder.AppendLine("            EntryAllowFallbackPosition,");
                    builder.AppendLine("            true)))");
                    builder.AppendLine("    {");
                    builder.AppendLine("      ScheduleRetry();");
                    builder.AppendLine("      return;");
                    builder.AppendLine("    }");
                    builder.AppendLine();
                    builder.AppendLine("    // Its \".back\" twin: using the portal item INSIDE the target dimension");
                    builder.AppendLine("    // opens a temporary portal home instead. Dimension -> overworld, so travel");
                    builder.AppendLine("    // lands at the player's tracked overworld exit point; the entry portal's");
                    builder.AppendLine("    // overworld position is only the fallback when no visit is recorded.");
                    builder.AppendLine("    if (!TryEnsurePortal(");
                    builder.AppendLine("        current,");
                    builder.AppendLine("        new DimensionPortalDefinition(");
                    builder.AppendLine("            " + ipPortalId + " + \".back\",");
                    builder.AppendLine("            " + ipDisplayName + " + \" Return\",");
                    builder.AppendLine("            " + ToCSharpString(ip.ToDimension) + ",");
                    builder.AppendLine("            float2.zero,");
                    builder.AppendLine("            EntryFromDimensionId,");
                    builder.AppendLine("            new float2(EntryFromLocalX, EntryFromLocalY),");
                    builder.AppendLine("            DimensionPortalState.Available)))");
                    builder.AppendLine("    {");
                    builder.AppendLine("      ScheduleRetry();");
                    builder.AppendLine("      return;");
                    builder.AppendLine("    }");
                    builder.AppendLine();
                    builder.AppendLine("    if (!TryEnsurePortalPresentation(");
                    builder.AppendLine("        current,");
                    builder.AppendLine("        new DimensionPortalPresentationDefinition(");
                    builder.AppendLine("            " + ipPortalId + " + \".back.presentation\",");
                    builder.AppendLine("            " + ipPortalId + " + \".back\",");
                    builder.AppendLine("            " + ipDisplayName + " + \" Return\",");
                    builder.AppendLine("            \"Return home.\",");
                    builder.AppendLine("            \"The portal is not active yet.\",");
                    builder.AppendLine("            string.Empty,");
                    builder.AppendLine("            string.Empty,");
                    builder.AppendLine("            string.Empty,");
                    builder.AppendLine("            0f,");
                    builder.AppendLine("            0,");
                    builder.AppendLine("            true,");
                    builder.AppendLine("            false,");
                    builder.AppendLine("            true,");
                    builder.AppendLine("            true)))");
                    builder.AppendLine("    {");
                    builder.AppendLine("      ScheduleRetry();");
                    builder.AppendLine("      return;");
                    builder.AppendLine("    }");
                    builder.AppendLine();
                }
            }

            builder.AppendLine("    portalDefinitionsRegistered = true;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private bool TryEnsurePortal(");
            builder.AppendLine("      IDimensionService current,");
            builder.AppendLine("      DimensionPortalDefinition portal)");
            builder.AppendLine("  {");
            builder.AppendLine("    DimensionPortalDefinition existing;");
            builder.AppendLine("    if (current.TryGetPortal(portal.PortalId, out existing))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionOperationResult result;");
            builder.AppendLine("    if (current.TryRegisterPortal(portal, out result))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    WarnOnce(result.Code, result.Message);");
            builder.AppendLine("    return false;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private bool TryEnsurePortalPresentation(");
            builder.AppendLine("      IDimensionService current,");
            builder.AppendLine("      DimensionPortalPresentationDefinition presentation)");
            builder.AppendLine("  {");
            builder.AppendLine("    DimensionPortalPresentationDefinition existing;");
            builder.AppendLine("    if (current.TryGetPortalPresentation(presentation.PresentationId, out existing))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionOperationResult result;");
            builder.AppendLine("    if (current.TryRegisterPortalPresentation(presentation, out result))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    WarnOnce(result.Code, result.Message);");
            builder.AppendLine("    return false;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private bool TryEnsureZone(");
            builder.AppendLine("      IDimensionService current,");
            builder.AppendLine("      DimensionZoneDefinition zone)");
            builder.AppendLine("  {");
            builder.AppendLine("    DimensionZoneDefinition existing;");
            builder.AppendLine("    if (current.TryGetZoneDefinition(zone.ZoneId, out existing))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionOperationResult result;");
            builder.AppendLine("    if (current.TryRegisterZoneDefinition(zone, out result))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    WarnOnce(result.Code, result.Message);");
            builder.AppendLine("    return false;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private bool TryEnsureGenerationPass(");
            builder.AppendLine("      IDimensionService current,");
            builder.AppendLine("      DimensionGenerationPassDefinition generationPass)");
            builder.AppendLine("  {");
            builder.AppendLine("    DimensionGenerationPassDefinition existing;");
            builder.AppendLine("    if (current.TryGetGenerationPass(generationPass.PassId, out existing))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionOperationResult result;");
            builder.AppendLine("    if (current.TryRegisterGenerationPass(generationPass, out result))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    WarnOnce(result.Code, result.Message);");
            builder.AppendLine("    return false;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private bool TryEnsureStarter(");
            builder.AppendLine("      IDimensionService current,");
            builder.AppendLine("      DimensionStarterDefinition starter)");
            builder.AppendLine("  {");
            builder.AppendLine("    DimensionStarterDefinition existing;");
            builder.AppendLine("    if (current.TryGetStarter(starter.StarterId, out existing))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    DimensionOperationResult result;");
            builder.AppendLine("    if (current.TryRegisterStarter(starter, out result))");
            builder.AppendLine("    {");
            builder.AppendLine("      return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    WarnOnce(result.Code, result.Message);");
            builder.AppendLine("    return false;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private void ScheduleRetry()");
            builder.AppendLine("  {");
            builder.AppendLine("    nextApplyFrame = Time.frameCount + 60;");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private void WarnOnce(string code, string message)");
            builder.AppendLine("  {");
            builder.AppendLine("    string failureCode = string.IsNullOrEmpty(code) ? \"dimension-runtime-bootstrap\" : code;");
            builder.AppendLine("    if (lastFailureCode == failureCode)");
            builder.AppendLine("    {");
            builder.AppendLine("      return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    lastFailureCode = failureCode;");
            // Through the framework's own door, not a raw Debug call: an emitted Debug.LogWarning
            // ships ungated logging with a prefix of its own inside every mod built with this
            // framework, and nobody can quieten it.
            builder.AppendLine("    DimensionConsumerLog.ProblemOnce(DimensionId, failureCode, message);");
            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Emits every layout version the author published, so an existing save can be generated from
        /// the one that made it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each archived version is written out as its own set of zone registrations. That is more
        /// generated code than emitting only the current layout, but it is the only way pinning can be
        /// real: a world that says "I was made by v2" needs v2 to still exist inside the shipped mod,
        /// long after the author has rebuilt the layout out of different rings.
        /// </para>
        /// <para>
        /// The drift policy is emitted alongside, because it is the author's current intent and has to
        /// be able to change without invalidating anything already published.
        /// </para>
        /// </remarks>
        private static void AppendLayoutVersionsMethod(
            StringBuilder builder,
            DimensionRuntimePortalOutput portalOutput,
            DimensionTemplateAsset template)
        {
            builder.AppendLine("  private void RegisterLayoutVersions()");
            builder.AppendLine("  {");

            DimensionLayoutTemplateAsset layout = template == null ? null : template.LayoutTemplate;
            DimensionLayoutArchiveEntry[] published =
                layout == null ? new DimensionLayoutArchiveEntry[0] : layout.PublishedVersions;

            if (layout == null || published.Length == 0)
            {
                // Nothing published means nothing to pin to. Saves still work — they simply generate
                // from whatever layout is installed, which is the behaviour before this existed.
                builder.AppendLine("  }");
                builder.AppendLine();
                return;
            }

            string dimensionId = ToCSharpString(portalOutput.DimensionId);

            builder.AppendLine("    DimensionLayoutDriftPolicyRegistry.Register(");
            builder.Append("        ").Append(dimensionId).AppendLine(",");
            builder.Append("        DimensionLayoutDriftPolicy.")
                .Append(layout.DriftPolicy.ToString()).AppendLine(");");
            builder.AppendLine();

            builder.AppendLine("    DimensionLayoutVersionRegistry.RegisterCurrent(");
            builder.Append("        ").Append(dimensionId).AppendLine(",");
            builder.Append("        ")
                .Append(layout.LayoutVersion.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(layout.CurrentFingerprint)).AppendLine(");");
            builder.AppendLine();

            for (int v = 0; v < published.Length; v++)
            {
                DimensionLayoutArchiveEntry entry = published[v];
                if (entry == null || entry.Regions.Length == 0)
                {
                    continue;
                }

                builder.AppendLine("    {");
                builder.AppendLine("      var zones = new System.Collections.Generic.List<DimensionZoneDefinition>();");

                for (int r = 0; r < entry.Regions.Length; r++)
                {
                    DimensionLayoutArchivedRegion region = entry.Regions[r];
                    if (region == null || string.IsNullOrEmpty(region.ZoneId))
                    {
                        continue;
                    }

                    builder.AppendLine("      zones.Add(new DimensionZoneDefinition(");
                    builder.Append("          ").Append(ToCSharpString(region.ZoneId)).AppendLine(",");
                    builder.Append("          ").Append(ToCSharpString(
                        string.IsNullOrEmpty(region.DisplayName) ? region.BiomeId : region.DisplayName))
                        .AppendLine(",");
                    builder.Append("          ").Append(dimensionId).AppendLine(",");
                    AppendBoundsConstructor(builder, region.LocalBounds, "          ");
                    builder.AppendLine(",");
                    builder.Append("          ").Append(ToCSharpString(region.BiomeId)).AppendLine(",");
                    builder.Append("          ")
                        .Append(region.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                    builder.AppendLine("          true));");
                }

                builder.AppendLine("      DimensionLayoutVersionRegistry.RegisterVersion(");
                builder.Append("          ").Append(dimensionId).AppendLine(",");
                builder.Append("          ")
                    .Append(entry.Version.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("          ").Append(ToCSharpString(entry.Fingerprint)).AppendLine(",");
                builder.AppendLine("          zones);");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("  }");
            builder.AppendLine();
        }

        private static void AppendMinimumZonesMethod(
            StringBuilder builder,
            DimensionRuntimePortalOutput portalOutput,
            DimensionBounds tileMapBounds)
        {
            builder.AppendLine("  private bool EnsureMinimumZones(IDimensionService current)");
            builder.AppendLine("  {");
            IReadOnlyList<DimensionZoneDefinition> zones = portalOutput.MinimumZones;
            if (zones == null || zones.Count == 0)
            {
                builder.AppendLine("    return true;");
                builder.AppendLine("  }");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                DimensionZoneDefinition zone = zones[i];
                if (string.IsNullOrEmpty(zone.ZoneId))
                {
                    continue;
                }

                builder.AppendLine("    if (!TryEnsureZone(");
                builder.AppendLine("        current,");
                builder.AppendLine("        new DimensionZoneDefinition(");
                builder.Append("            ").Append(ToCSharpString(zone.ZoneId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(zone.DisplayName)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(zone.DimensionId)).AppendLine(",");
                AppendBoundsConstructor(builder, UnionBounds(zone.LocalBounds, tileMapBounds), "            ");
                builder.AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(zone.Kind)).AppendLine(",");
                builder.Append("            ").Append(zone.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("            ").Append(zone.Enabled ? "true" : "false").AppendLine(")))");
                builder.AppendLine("    {");
                builder.AppendLine("      return false;");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("    return true;");
            builder.AppendLine("  }");
            builder.AppendLine();
        }

        private static void AppendMinimumGenerationPassesMethod(
            StringBuilder builder,
            DimensionRuntimePortalOutput portalOutput,
            DimensionBounds tileMapBounds)
        {
            builder.AppendLine("  private bool EnsureMinimumGenerationPasses(IDimensionService current)");
            builder.AppendLine("  {");
            IReadOnlyList<DimensionGenerationPassDefinition> generationPasses =
                portalOutput.MinimumGenerationPasses;
            if (generationPasses == null || generationPasses.Count == 0)
            {
                builder.AppendLine("    return true;");
                builder.AppendLine("  }");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < generationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition generationPass = generationPasses[i];
                if (string.IsNullOrEmpty(generationPass.PassId))
                {
                    continue;
                }

                builder.AppendLine("    if (!TryEnsureGenerationPass(");
                builder.AppendLine("        current,");
                builder.AppendLine("        new DimensionGenerationPassDefinition(");
                builder.Append("            ").Append(ToCSharpString(generationPass.PassId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.DisplayName)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.DimensionId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.ZoneId)).AppendLine(",");
                builder.Append("            ").Append(generationPass.HasLocalBounds ? "true" : "false").AppendLine(",");
                AppendBoundsConstructor(builder, UnionBounds(generationPass.LocalBounds, tileMapBounds), "            ");
                builder.AppendLine(",");
                builder.Append("            (DimensionGenerationPassPhase)")
                    .Append(((int)generationPass.Phase).ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",");
                builder.Append("            ").Append(generationPass.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.ProviderId)).AppendLine(",");
                builder.Append("            ").Append(generationPass.Enabled ? "true" : "false").AppendLine(")))");
                builder.AppendLine("    {");
                builder.AppendLine("      return false;");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("    return true;");
            builder.AppendLine("  }");
            builder.AppendLine();
        }

        private static void AppendBoundsConstructor(
            StringBuilder builder,
            DimensionBounds bounds,
            string indent)
        {
            string safeIndent = indent ?? string.Empty;
            builder.Append(safeIndent).AppendLine("new DimensionBounds(");
            builder.Append(safeIndent)
                .Append("    new int2(")
                .Append(bounds.Min.x.ToString(CultureInfo.InvariantCulture))
                .Append(", ")
                .Append(bounds.Min.y.ToString(CultureInfo.InvariantCulture))
                .AppendLine("),");
            builder.Append(safeIndent)
                .Append("    new int2(")
                .Append(bounds.MaxExclusive.x.ToString(CultureInfo.InvariantCulture))
                .Append(", ")
                .Append(bounds.MaxExclusive.y.ToString(CultureInfo.InvariantCulture))
                .Append("))");
        }

        /// <summary>
        /// True when the placed portal (V1) should be craftable: an enabled user-accessible rule
        /// with Craftable set exists — or no placed rule was authored at all (legacy default).
        /// </summary>
        /// <summary>
        /// Makes every generated block item show up at the Wooden Workbench, alongside the portals.
        /// A block with no authored recipe still registers — it simply crafts from nothing, which is
        /// what lets a creator place and look at a brand-new block before designing its cost.
        /// Ingredients, once authored, ride the item's own InventoryItem authoring like vanilla.
        /// </summary>
        private static void AppendBlockCraftingRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template)
        {
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            for (int i = 0; i < tilesets.Length; i++)
            {
                DimensionTilesetAsset tileset = tilesets[i];
                if (tileset == null || !tileset.Enabled || !tileset.GenerateWallBlock)
                {
                    continue;
                }

                string itemId = tileset.WallBlockItemId;
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                builder.AppendLine("    DimensionCraftingRegistry.Register(");
                builder.AppendLine("        new DimensionCraftingRecipeDefinition(");
                builder.Append("            ").Append(ToCSharpString(itemId)).AppendLine(",");
                builder.AppendLine("            ObjectID.WoodenWorkBench,");
                builder.AppendLine("            1,");
                builder.AppendLine("            0f,");
                builder.Append("            ").Append(ToCSharpString(tileset.BlockName + " Block")).AppendLine("));");
            }
        }

        /// <summary>
        /// Emits the title card each custom biome shows the first time a player walks into it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The tilesets a biome is recognised by are DERIVED, not authored a second time: a biome
        /// already names the floor and wall blocks it builds itself from, and each of those blocks
        /// belongs to a tileset. Asking the author to also list "which tilesets mean this biome" would
        /// be asking the same question twice, and the two answers would eventually disagree — at which
        /// point a title fires for a place the player is not standing in.
        /// </para>
        /// <para>
        /// A biome made entirely of vanilla blocks emits nothing. Its tilesets are Core Keeper's own,
        /// and claiming them would mean walking onto ordinary stone announced this mod's biome.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Emits each biome's ore list as a waiting gate registration.
        /// </summary>
        /// <remarks>
        /// The bootstrap knows which ores a biome names but not where the biome lies — the
        /// bounds only exist once the manifest's zones apply. So the emission registers the
        /// LIST, and the manifest-apply path marries it to each zone carrying the biome's id
        /// (<c>DimensionOreBiomeGate.BindZone</c>). Before this, the gate's one Register call
        /// sat on a method with no callers and every biome ore chip was decorative.
        /// </remarks>
        private static void AppendOreBiomeGateRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes == null)
            {
                return;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                System.Collections.Generic.IReadOnlyList<string> ores =
                    biome.OreObjectIds;
                if (ores == null || ores.Count == 0)
                {
                    continue;
                }

                builder.Append("    ExpandNullforge.Generation.DimensionOreBiomeGate.RegisterBiomeOres(")
                    .Append(ToCSharpString(template.DimensionId))
                    .Append(", ")
                    .Append(ToCSharpString(biome.BiomeId))
                    .Append(", new string[] { ");
                bool wroteOre = false;
                for (int o = 0; o < ores.Count; o++)
                {
                    if (string.IsNullOrEmpty(ores[o]))
                    {
                        continue;
                    }

                    string oreName = IsModOwnedObjectName(template, ores[o])
                        ? DimensionObjectNamespace.Qualify(modName, ores[o])
                        : ores[o];
                    if (wroteOre)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(ToCSharpString(oreName));
                    wroteOre = true;
                }

                builder.AppendLine(" });");
            }
        }

        /// <summary>
        /// Emits what each biome's ground and walls are made of.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The other half of the same shape the ore gate uses: the bootstrap knows which block a
        /// biome names but not where the biome lies, so it registers the CHOICE and zone
        /// registration marries it to the geography. Before this the terrain provider carried one
        /// hardcoded tileset and every generated dimension came out a dirt platform, whatever the
        /// Biome page said.
        /// </para>
        /// <para>
        /// A biome whose Ground and Walls both name something unresolvable emits nothing at all,
        /// rather than a row of dirt. The rows are read last-one-wins, so a meaningless row would
        /// take a cell away from an overlapping biome that did resolve. The unresolved entry is
        /// reported by the compiler's <c>biome-terrain-block-unresolved</c> issue instead.
        /// </para>
        /// <para>
        /// A biome that resolves only one half gets that half and dirt for the other, so naming a
        /// floor and no wall gives the floor rather than nothing.
        /// </para>
        /// </remarks>
        private static void AppendTerrainMaterialRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes == null)
            {
                return;
            }

            DimensionTilesetAsset[] tilesets = template.Tilesets;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled || string.IsNullOrEmpty(biome.BiomeId))
                {
                    continue;
                }

                int groundTileset;
                string groundNamed;
                bool groundHasGround;
                DimensionBiomeTerrainSource groundSource = DimensionBiomeTerrainMaterial.ResolveFirst(
                    biome.FloorObjectIds, tilesets, out groundTileset, out groundNamed, out groundHasGround);

                int wallTileset;
                string wallNamed;
                bool wallHasGround;
                DimensionBiomeTerrainSource wallSource = DimensionBiomeTerrainMaterial.ResolveFirst(
                    biome.WallObjectIds, tilesets, out wallTileset, out wallNamed, out wallHasGround);

                bool groundResolved = groundSource == DimensionBiomeTerrainSource.ModBlock ||
                                      groundSource == DimensionBiomeTerrainSource.VanillaBlock;
                bool wallResolved = wallSource == DimensionBiomeTerrainSource.ModBlock ||
                                    wallSource == DimensionBiomeTerrainSource.VanillaBlock;
                if (!groundResolved && !wallResolved)
                {
                    continue;
                }

                System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                builder
                    .Append("    ExpandNullforge.Generation.DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(")
                    .Append(ToCSharpString(template.DimensionId))
                    .Append(", ")
                    .Append(ToCSharpString(biome.BiomeId))
                    .Append(", ")
                    .Append((groundResolved
                        ? groundTileset
                        : ExpandNullforge.Generation.DimensionTerrainMaterialRegistry.DefaultTileset)
                        .ToString(inv))
                    .Append(", ")
                    .Append((wallResolved
                        ? wallTileset
                        : ExpandNullforge.Generation.DimensionTerrainMaterialRegistry.DefaultTileset)
                        .ToString(inv))
                    .AppendLine(");");
            }
        }

        private static void AppendRegionTitleRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (biomes == null || tilesets == null)
            {
                return;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                List<int> tilesetIds = CollectBiomeTilesetIds(biome, tilesets);
                if (tilesetIds.Count == 0)
                {
                    continue;
                }

                AppendBiomeAtmosphereRegistration(builder, biome, tilesetIds);

                if (!biome.ShowTitleOnDiscovery)
                {
                    continue;
                }

                UnityEngine.Color color = biome.TitleColor;
                string iconName = string.IsNullOrEmpty(biome.TitleIconObjectId)
                    ? string.Empty
                    : (IsModOwnedObjectName(template, biome.TitleIconObjectId)
                        ? DimensionObjectNamespace.Qualify(modName, biome.TitleIconObjectId)
                        : biome.TitleIconObjectId);

                builder.AppendLine("    DimensionRegionTitleRegistry.Register(");
                builder.Append("        ").Append(ToCSharpString(biome.BiomeId)).AppendLine(",");

                // The localization term, not the text. The generator writes the biome's display name
                // into the mod's own CSV under this key, so a translated mod translates its titles too.
                builder.Append("        ")
                    .Append(ToCSharpString(DimensionBiomeTitleTerms.ForBiome(modName, biome.BiomeId)))
                    .AppendLine(",");

                builder.Append("        new UnityEngine.Color(")
                    .Append(color.r.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                    .Append(color.g.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                    .Append(color.b.ToString("R", CultureInfo.InvariantCulture)).Append("f, 1f),");
                builder.AppendLine();

                builder.Append("        new int[] { ");
                for (int t = 0; t < tilesetIds.Count; t++)
                {
                    if (t > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(tilesetIds[t].ToString(CultureInfo.InvariantCulture));
                }

                builder.AppendLine(" },");
                builder.Append("        ").Append(ToCSharpString(iconName)).AppendLine(");");
            }

            AppendNamedAreaRegistrations(builder, template, modName);
        }

        /// <summary>
        /// Emits each named area: one name fanned out to the title, ambience and music
        /// registries under a synthetic area id, carried by its signature blocks.
        /// </summary>
        /// <remarks>
        /// The game has no "area" object — the Meadow is three tile-counting systems agreeing.
        /// The synthetic "area:" id keeps a named area from ever colliding with a real biome's
        /// id in the shared registries, while the framework's own current-biome derivation
        /// (top tileset → registered id) makes standing among the area's blocks read as being
        /// IN the area, which is what fires its title and music.
        /// </remarks>
        private static void AppendNamedAreaRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionNamedAreaAsset[] areas = template == null ? null : template.NamedAreas;
            if (areas == null)
            {
                return;
            }

            for (int i = 0; i < areas.Length; i++)
            {
                DimensionNamedAreaAsset area = areas[i];
                if (area == null || !area.Enabled || string.IsNullOrEmpty(area.AreaId))
                {
                    continue;
                }

                List<int> tilesetIds = new List<int>();
                DimensionTilesetAsset[] blocks = area.Blocks;
                for (int b = 0; b < blocks.Length; b++)
                {
                    if (blocks[b] != null && blocks[b].Enabled && !tilesetIds.Contains(blocks[b].TilesetId))
                    {
                        tilesetIds.Add(blocks[b].TilesetId);
                    }
                }

                if (tilesetIds.Count == 0)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Named area '" + area.AreaId + "' names no blocks, so " +
                        "nothing could ever stand inside it. It was left out.");
                    continue;
                }

                string syntheticId = "area:" + area.AreaId;
                string tilesetLiteral = BuildIntArrayLiteral(tilesetIds);

                if (area.ShowTitleOnDiscovery)
                {
                    UnityEngine.Color color = area.TitleColor;
                    string iconName = string.IsNullOrEmpty(area.TitleIconObjectId)
                        ? string.Empty
                        : (IsModOwnedObjectName(template, area.TitleIconObjectId)
                            ? DimensionObjectNamespace.Qualify(modName, area.TitleIconObjectId)
                            : area.TitleIconObjectId);

                    builder.AppendLine("    DimensionRegionTitleRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(syntheticId)).AppendLine(",");
                    builder.Append("        ")
                        .Append(ToCSharpString(DimensionBiomeTitleTerms.ForBiome(modName, syntheticId)))
                        .AppendLine(",");
                    builder.Append("        new UnityEngine.Color(")
                        .Append(color.r.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                        .Append(color.g.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                        .Append(color.b.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f, 1f),");
                    builder.Append("        ").Append(tilesetLiteral).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(iconName)).AppendLine(");");
                }

                if (!string.IsNullOrEmpty(area.AmbienceSoundKey) ||
                    !string.IsNullOrEmpty(area.MusicRosterName))
                {
                    builder.AppendLine("    DimensionBiomeAtmosphereRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(syntheticId)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(area.AmbienceSoundKey)).AppendLine(",");
                    builder.Append("        ")
                        .Append(area.AmbienceVolume.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f,");
                    builder.Append("        ").Append(ToCSharpString(area.MusicRosterName)).AppendLine(",");
                    builder.Append("        ").Append(tilesetLiteral).AppendLine(");");
                }
            }
        }

        private static string BuildIntArrayLiteral(List<int> values)
        {
            StringBuilder literal = new StringBuilder("new int[] { ");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    literal.Append(", ");
                }

                literal.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }

            literal.Append(" }");
            return literal.ToString();
        }

        /// <summary>
        /// Emits each dungeon: its size, where it may grow, and which scenes fill which rooms.
        /// </summary>
        /// <remarks>
        /// A dungeon with no entrance is reported but still emitted. It is a real mistake — the player
        /// finds a sealed pocket of rooms — but it is also a legitimate mid-build state, and refusing
        /// to generate it would stop an author testing the rooms they have so far.
        /// </remarks>
        private static void AppendDungeonRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionDungeonAsset[] dungeons = template == null ? null : template.GlobalDungeons;
            if (dungeons == null || dungeons.Length == 0)
            {
                return;
            }

            for (int i = 0; i < dungeons.Length; i++)
            {
                DimensionDungeonAsset dungeon = dungeons[i];
                if (dungeon == null || !dungeon.Enabled)
                {
                    continue;
                }

                if (!dungeon.HasEntrance)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' has no entrance rooms. " +
                        "It will still generate, but players will find a sealed pocket of rooms with " +
                        "no way in.");
                }

                builder.AppendLine("    {");
                builder.AppendLine(
                    "      var dungeonRooms = new System.Collections.Generic.List<DimensionDungeonRoomGroup>();");

                DimensionDungeonRoomGroupTemplate[] groups = dungeon.RoomGroups;
                for (int g = 0; g < groups.Length; g++)
                {
                    DimensionDungeonRoomGroupTemplate group = groups[g];
                    if (group == null || group.Rooms.Length == 0)
                    {
                        continue;
                    }

                    builder.Append("      dungeonRooms.Add(new DimensionDungeonRoomGroup(")
                        .Append("DimensionDungeonRoomRole.").Append(group.Role.ToString()).Append(", ")
                        .Append(group.MinRooms.ToString(CultureInfo.InvariantCulture)).Append(", ")
                        .Append(group.MaxRooms.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(", new string[] {");

                    for (int r = 0; r < group.Rooms.Length; r++)
                    {
                        SceneTemplateAsset room = group.Rooms[r];
                        if (room == null || string.IsNullOrEmpty(room.SceneId))
                        {
                            continue;
                        }

                        builder.Append("        ")
                            .Append(ToCSharpString(
                                DimensionObjectNamespace.Qualify(modName, room.SceneId)))
                            .AppendLine(",");
                    }

                    builder.AppendLine("      }));");
                }

                builder.AppendLine("      DimensionDungeonRegistry.Register(new DimensionDungeonDefinition(");
                builder.Append("          ")
                    .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, dungeon.DungeonId)))
                    .AppendLine(",");
                builder.Append("          ").Append(ToCSharpString(dungeon.BiomeId)).AppendLine(",");
                builder.Append("          ")
                    .Append(dungeon.Radius.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("          ")
                    .Append(dungeon.RoomSize.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                builder.Append("          ")
                    .Append(dungeon.PathSize.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                builder.Append("          ")
                    .Append(dungeon.SpawnChance.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                builder.Append("          ")
                    .Append(dungeon.MinDistanceFromCentre.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",");
                builder.AppendLine("          " + (dungeon.BlockOtherSpawns ? "true" : "false") + ",");
                builder.Append("          dungeonRooms");
                AppendDungeonShapeArguments(builder, template, dungeon, modName);
                // Fillings are independent of the shape template: a dungeon of default rules
                // with authored contents is the common first dungeon.
                AppendDungeonFillings(builder, template, dungeon, modName);

                // Where it grows inside the author's own dimension — vanilla's placer never
                // runs there, so without these arguments the dungeon could only ever appear
                // in the Overworld.
                if (dungeon.GrowsInThisDimension)
                {
                    builder.AppendLine(",");
                    builder.Append("          dimensionId: ")
                        .Append(ToCSharpString(template.DimensionId)).AppendLine(",");
                    builder.Append("          dimensionPlacement: DimensionScenePlacementMode.")
                        .Append(dungeon.DimensionPlacement.ToString()).AppendLine(",");
                    builder.Append("          exactLocalPosition: new Unity.Mathematics.int2(")
                        .Append(dungeon.DimensionExactPosition.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", ")
                        .Append(dungeon.DimensionExactPosition.y.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("),");
                    builder.Append("          minRadiusTiles: ")
                        .Append(dungeon.DimensionMinRadius.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(",");
                    builder.Append("          maxRadiusTiles: ")
                        .Append(dungeon.DimensionMaxRadius.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(",");
                    builder.Append("          countPerArea: ")
                        .Append(dungeon.DimensionCount.ToString(CultureInfo.InvariantCulture));
                }

                builder.AppendLine("));");

                // The Overworld pin: one guaranteed copy on the game's own unique-placement
                // rails. The pin's NAME is the save's memory of the dungeon — it is derived
                // from the qualified id and must never change once worlds exist, or an
                // updated mod places a second copy.
                if (dungeon.PinnedInOverworld)
                {
                    string pinName = DimensionObjectNamespace.Qualify(modName, dungeon.DungeonId);
                    string pinError;
                    if (!DimensionCustomSceneNames.IsValid(pinName, out pinError))
                    {
                        Debug.LogWarning(
                            "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' cannot be " +
                            "pinned into the Overworld: " + pinError);
                    }
                    else
                    {
                        builder.AppendLine("      DimensionUniqueDungeonRegistry.Register(");
                        builder.AppendLine("          new DimensionUniqueDungeonDefinition(");
                        builder.Append("              ")
                            .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, dungeon.DungeonId)))
                            .AppendLine(",");
                        builder.Append("              ").Append(ToCSharpString(pinName)).AppendLine(",");
                        builder.AppendLine("              " + (dungeon.PinnedAtExactSpot ? "true" : "false") + ",");
                        builder.Append("              new int2(")
                            .Append(dungeon.PinnedPosition.x.ToString(CultureInfo.InvariantCulture))
                            .Append(", ")
                            .Append(dungeon.PinnedPosition.y.ToString(CultureInfo.InvariantCulture))
                            .AppendLine("),");
                        builder.Append("              ")
                            .Append(dungeon.PinnedDistanceFromCore.ToString(CultureInfo.InvariantCulture))
                            .AppendLine(",");
                        builder.Append("              ").Append(ToCSharpString(dungeon.PinnedBiomeName)).AppendLine(",");
                        builder.AppendLine("              " + (dungeon.PinnedSpawnsImmediately ? "true" : "false") + "));");
                    }
                }

                builder.AppendLine("    }");
            }
        }

        /// <summary>
        /// Emits the shape template's runtime mirrors as extra registration arguments, so the
        /// authored shape survives into the built mod instead of stopping at the asset.
        /// </summary>
        /// <remarks>
        /// A dungeon whose shape template was never switched on emits nothing extra and behaves
        /// exactly as before this existed. Generated-dungeon and single-handmade-room are
        /// alternatives; asking for both gets the generated dungeon and a warning, because
        /// letting the game decide which wins is how content works in testing and not in worlds.
        /// </remarks>
        private static void AppendDungeonShapeArguments(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionDungeonAsset dungeon,
            string modName)
        {
            DimensionDungeonShapeTemplate shape = dungeon.GeneratedShape;
            if (shape == null)
            {
                return;
            }

            if (shape.IsBothGeneratedAndHandmade)
            {
                Debug.LogWarning(
                    "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' is marked as both a " +
                    "generated dungeon and a single handmade room. They are alternatives; the " +
                    "generated dungeon wins.");
            }

            if (shape.IsASingleHandmadeRoom && !shape.GeneratesADungeon)
            {
                // The author names WHICH of the dungeon's own places it is; the first place in
                // its room groups is only the fallback. The named place must be one of them,
                // because a dungeon room is looked up by the name this dimension registered it
                // under — a name from anywhere else resolves to nothing and the dungeon is
                // skipped at assembly with no way for the author to see why.
                string singleSceneName = NamedRoomSceneName(dungeon, modName, shape.SingleRoomSceneId);
                if (singleSceneName == null && !string.IsNullOrEmpty(shape.SingleRoomSceneId))
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' is the single " +
                        "handmade room '" + shape.SingleRoomSceneId + "', which is not one of " +
                        "the places in its room groups. Add that place to a room group, or " +
                        "clear the field to use the first place it names. It falls back to the " +
                        "first place for this build.");
                }

                if (string.IsNullOrEmpty(singleSceneName))
                {
                    singleSceneName = FirstRoomSceneName(dungeon, modName);
                }

                if (string.IsNullOrEmpty(singleSceneName))
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' is a single " +
                        "handmade room but its room groups name no place, so there is nothing " +
                        "to be. It will assemble as a generated dungeon instead.");
                    return;
                }

                builder.AppendLine(",");
                builder.Append("          singleScene: new DimensionDungeonSingleScene(")
                    .Append(ToCSharpString(singleSceneName))
                    .Append(", ")
                    .Append(shape.KeepsClearRadius.ToString(CultureInfo.InvariantCulture))
                    .Append(")");
                return;
            }

            if (!shape.GeneratesADungeon)
            {
                return;
            }

            builder.AppendLine(",");
            builder.AppendLine("          shape: new DimensionDungeonShape(");
            builder.Append("              ")
                .Append(shape.Seed.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("              ")
                .Append(shape.Radius.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.AppendLine("              " + (shape.HasAShapedOutline ? "true" : "false") + ",");
            builder.Append("              ")
                .Append(shape.OutlineWobble.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.Append("              ")
                .Append(shape.OutlineBusyness.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.AppendLine("              " + (shape.OutlineFollowsTheRooms ? "true" : "false") + ",");
            builder.AppendLine("              " + (shape.IsRectangular ? "true" : "false") + ",");
            builder.Append("              ")
                .Append(shape.RoomFillSize.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.Append("              ")
                .Append(shape.PathFillSize.ToString("R", CultureInfo.InvariantCulture)).Append("f)");

            DimensionDungeonRoom[] rooms = shape.Rooms;
            if (rooms.Length > 0)
            {
                builder.AppendLine(",");
                builder.AppendLine("          roomRules: new DimensionDungeonRoomRule[] {");
                for (int i = 0; i < rooms.Length; i++)
                {
                    DimensionDungeonRoom room = rooms[i];
                    builder.Append("            new DimensionDungeonRoomRule { Placement = DimensionRoomPlacement.")
                        .Append(room.Placement.ToString())
                        .Append(", Kind = (DimensionRoomKind)")
                        .Append(((int)room.Kind).ToString(CultureInfo.InvariantCulture))
                        .Append(", MinCount = ").Append(room.HowMany.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxCount = ").Append(room.HowMany.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", MinRadius = ").Append(room.HowBig.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxRadius = ").Append(room.HowBig.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", MinSpacing = ").Append(room.HowFarApart.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxSpacing = ").Append(room.HowFarApart.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", AngleMinDegrees = ").Append(room.AtWhatAngle.x.ToString(CultureInfo.InvariantCulture))
                        .Append("f, AngleMaxDegrees = ").Append(room.AtWhatAngle.y.ToString(CultureInfo.InvariantCulture))
                        .Append("f, AlignedWithTheCore = ").Append(room.AlignedWithTheCore ? "true" : "false")
                        .Append(", StraightPaths = ").Append(room.StraightPathsToThem ? "true" : "false")
                        .Append(", MayOverlapOtherRooms = ").Append(room.MayOverlapOtherRooms ? "true" : "false")
                        .Append(", MayOverlapKinds = (DimensionRoomKind)")
                        .Append(((int)room.MayOverlapKinds).ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" },");
                }

                builder.Append("          }");
            }

            DimensionDungeonPath[] paths = shape.Paths;
            if (paths.Length > 0)
            {
                builder.AppendLine(",");
                builder.AppendLine("          pathRules: new DimensionDungeonPathRule[] {");
                for (int i = 0; i < paths.Length; i++)
                {
                    DimensionDungeonPath path = paths[i];
                    builder.Append("            new DimensionDungeonPathRule { Placement = DimensionPathPlacement.")
                        .Append(path.Placement.ToString())
                        .Append(", Kind = (DimensionRoomKind)")
                        .Append(((int)path.Kind).ToString(CultureInfo.InvariantCulture))
                        .Append(", MinCount = ").Append(path.HowMany.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxCount = ").Append(path.HowMany.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", Width = ").Append(path.Width.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, Straight = ").Append(path.Straight ? "true" : "false")
                        .Append(", MayCrossPaths = ").Append(path.MayCrossPaths ? "true" : "false")
                        .Append(", MayCrossPathKinds = (DimensionRoomKind)")
                        .Append(((int)path.MayCrossPathKinds).ToString(CultureInfo.InvariantCulture))
                        .Append(", MayCrossRooms = ").Append(path.MayCrossRooms ? "true" : "false")
                        .Append(", MayCrossRoomKinds = (DimensionRoomKind)")
                        .Append(((int)path.MayCrossRoomKinds).ToString(CultureInfo.InvariantCulture))
                        .Append(", StartsFrom = (DimensionRoomKind)")
                        .Append(((int)path.StartsFrom).ToString(CultureInfo.InvariantCulture))
                        .Append(", EndsAt = (DimensionRoomKind)")
                        .Append(((int)path.EndsAt).ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" },");
                }

                builder.Append("          }");
            }

            string[] outlineBlocks = shape.OutlineBlockIds;
            bool wroteOutline = false;
            for (int i = 0; i < outlineBlocks.Length; i++)
            {
                if (string.IsNullOrEmpty(outlineBlocks[i]))
                {
                    continue;
                }

                if (!wroteOutline)
                {
                    builder.AppendLine(",");
                    builder.Append("          outlineBlockIds: new string[] { ");
                    wroteOutline = true;
                }
                else
                {
                    builder.Append(", ");
                }

                // A mod-owned block ships under its qualified name; a vanilla one keeps the
                // name the game already knows it by.
                string blockName = IsModOwnedObjectName(template, outlineBlocks[i])
                    ? DimensionObjectNamespace.Qualify(modName, outlineBlocks[i])
                    : outlineBlocks[i];
                builder.Append(ToCSharpString(blockName));
            }

            if (wroteOutline)
            {
                builder.Append(" }");
            }

            DimensionDungeonSwap[] swaps = shape.Swaps;
            bool wroteSwaps = false;
            for (int i = 0; i < swaps.Length; i++)
            {
                DimensionDungeonSwap swap = swaps[i];
                if (string.IsNullOrEmpty(swap.ReplaceId) || string.IsNullOrEmpty(swap.WithId))
                {
                    continue;
                }

                if (!wroteSwaps)
                {
                    builder.AppendLine(",");
                    builder.AppendLine("          swaps: new DimensionDungeonSwapRule[] {");
                    wroteSwaps = true;
                }

                string replaceName = IsModOwnedObjectName(template, swap.ReplaceId)
                    ? DimensionObjectNamespace.Qualify(modName, swap.ReplaceId)
                    : swap.ReplaceId;
                string withName = IsModOwnedObjectName(template, swap.WithId)
                    ? DimensionObjectNamespace.Qualify(modName, swap.WithId)
                    : swap.WithId;

                builder.Append("            new DimensionDungeonSwapRule { ReplaceId = ")
                    .Append(ToCSharpString(replaceName))
                    .Append(", WithId = ").Append(ToCSharpString(withName))
                    .Append(", OnlyOneLook = ").Append(swap.OnlyOneLook ? "true" : "false")
                    .Append(", TheLook = ").Append(swap.TheLook.ToString(CultureInfo.InvariantCulture))
                    .Append(", MinReplacementLook = ")
                    .Append(swap.ReplacementLooks.x.ToString(CultureInfo.InvariantCulture))
                    .Append(", MaxReplacementLook = ")
                    .Append(swap.ReplacementLooks.y.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" },");
            }

            if (wroteSwaps)
            {
                builder.Append("          }");
            }
        }

        /// <summary>
        /// Emits the dungeon's room fillings — the procedural content layer. Ids that belong to
        /// the mod ship qualified; vanilla names pass through, and the assembler resolves both
        /// at runtime where the object database exists.
        /// </summary>
        private static void AppendDungeonFillings(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionDungeonAsset dungeon,
            string modName)
        {
            DimensionRoomFillingAsset[] fillings = dungeon.RoomFillings;
            bool wroteAny = false;
            for (int i = 0; i < fillings.Length; i++)
            {
                DimensionRoomFillingAsset filling = fillings[i];
                if (filling == null || !filling.Enabled || filling.Entries.Length == 0)
                {
                    continue;
                }

                if (!wroteAny)
                {
                    builder.AppendLine(",");
                    builder.AppendLine("          fillings: new DimensionDungeonFillingRule[] {");
                    wroteAny = true;
                }

                builder.Append("            new DimensionDungeonFillingRule(")
                    .Append(ToCSharpString(filling.FillingId))
                    .Append(", (DimensionRoomKind)")
                    .Append(((int)filling.FillsRooms).ToString(CultureInfo.InvariantCulture))
                    .Append(", ").Append(filling.FillsCorridors ? "true" : "false")
                    .Append(", ").Append(filling.OnlyIfAtLeastThisBig.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(", new DimensionDungeonFillingEntry[] {");

                DimensionRoomFillingEntry[] entries = filling.Entries;
                for (int e = 0; e < entries.Length; e++)
                {
                    DimensionRoomFillingEntry entry = entries[e];
                    if (entry == null || string.IsNullOrEmpty(entry.ObjectId))
                    {
                        continue;
                    }

                    builder.Append("              new DimensionDungeonFillingEntry { ObjectId = ")
                        .Append(ToCSharpString(QualifyIfOwn(template, modName, entry.ObjectId)))
                        .Append(", Look = ").Append(entry.Look.ToString(CultureInfo.InvariantCulture))
                        .Append(", MinPatches = ").Append(entry.HowManyPatches.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxPatches = ").Append(entry.HowManyPatches.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", PatchShape = ").Append(((int)entry.PatchShape).ToString(CultureInfo.InvariantCulture))
                        .Append(", PatchSize = ").Append(entry.PatchSize.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, ChanceToAppear = ").Append(entry.ChanceToAppear.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, Density = ").Append(entry.Density.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, MayLandOn = ").Append(QualifiedArrayLiteral(template, modName, entry.MayLandOn))
                        .Append(", NeverOn = ").Append(QualifiedArrayLiteral(template, modName, entry.NeverOn))
                        .Append(", StackCount = ").Append(entry.StackCount.ToString(CultureInfo.InvariantCulture))
                        .Append(", ChestLoot = ").Append(ToCSharpString(entry.ChestLoot))
                        .AppendLine(" },");
                }

                builder.AppendLine("            }),");
            }

            if (wroteAny)
            {
                builder.Append("          }");
            }
        }

        private static string QualifyIfOwn(
            DimensionTemplateAsset template,
            string modName,
            string objectId)
        {
            return IsModOwnedObjectName(template, objectId)
                ? DimensionObjectNamespace.Qualify(modName, objectId)
                : objectId;
        }

        private static string QualifiedArrayLiteral(
            DimensionTemplateAsset template,
            string modName,
            string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return "new string[0]";
            }

            StringBuilder literal = new StringBuilder("new string[] { ");
            bool wrote = false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                {
                    continue;
                }

                if (wrote)
                {
                    literal.Append(", ");
                }

                literal.Append(ToCSharpString(QualifyIfOwn(template, modName, ids[i])));
                wrote = true;
            }

            literal.Append(" }");
            return wrote ? literal.ToString() : "new string[0]";
        }

        /// <summary>
        /// The registered name of the room the author named, or null when no room group holds it.
        /// </summary>
        /// <remarks>
        /// Matching is against the room groups rather than every scene in the project on purpose:
        /// the single room is also the dungeon's whole content, so a place that is not in a room
        /// group would be a dungeon whose one room is not one of its rooms.
        /// </remarks>
        private static string NamedRoomSceneName(
            DimensionDungeonAsset dungeon,
            string modName,
            string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return null;
            }

            DimensionDungeonRoomGroupTemplate[] groups = dungeon.RoomGroups;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null)
                {
                    continue;
                }

                SceneTemplateAsset[] rooms = groups[g].Rooms;
                for (int r = 0; r < rooms.Length; r++)
                {
                    if (rooms[r] != null &&
                        string.Equals(rooms[r].SceneId, sceneId, System.StringComparison.Ordinal))
                    {
                        return DimensionObjectNamespace.Qualify(modName, rooms[r].SceneId);
                    }
                }
            }

            return null;
        }

        private static string FirstRoomSceneName(DimensionDungeonAsset dungeon, string modName)
        {
            DimensionDungeonRoomGroupTemplate[] groups = dungeon.RoomGroups;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null)
                {
                    continue;
                }

                SceneTemplateAsset[] rooms = groups[g].Rooms;
                for (int r = 0; r < rooms.Length; r++)
                {
                    if (rooms[r] != null && !string.IsNullOrEmpty(rooms[r].SceneId))
                    {
                        return DimensionObjectNamespace.Qualify(modName, rooms[r].SceneId);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Emits each boss's phases: the health thresholds and what happens at them.
        /// </summary>
        /// <remarks>
        /// A phase whose action needs a target it does not have is dropped here rather than shipped —
        /// a summon with nothing to summon, or a condition with no condition named, would register
        /// fine and then do nothing at the most visible moment of a fight.
        /// </remarks>
        private static void AppendBossPhaseRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionBossAsset[] bosses = template == null ? null : template.GlobalBosses;
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled)
                {
                    continue;
                }

                DimensionBossPhaseTemplate[] phases = boss.Phases;
                if (phases == null || phases.Length == 0)
                {
                    continue;
                }

                string bossName = DimensionObjectNamespace.Qualify(modName, boss.BossId);

                for (int p = 0; p < phases.Length; p++)
                {
                    DimensionBossPhaseTemplate phase = phases[p];
                    if (phase == null || !phase.Enabled)
                    {
                        continue;
                    }

                    bool needsTarget =
                        phase.Action == DimensionBossPhaseActionKind.SummonAdds ||
                        phase.Action == DimensionBossPhaseActionKind.ApplyConditionToSelf ||
                        phase.Action == DimensionBossPhaseActionKind.ApplyConditionToPlayers;

                    if (needsTarget && string.IsNullOrEmpty(phase.ActionTarget))
                    {
                        Debug.LogWarning(
                            "[ExpandNullforge] Boss '" + boss.BossId + "' phase '" + phase.PhaseId +
                            "' is set to " + phase.Action + " but names nothing to act on, so it was " +
                            "left out. The fight will reach that health and do nothing.");
                        continue;
                    }

                    // A summoned creature is one of the mod's own; a condition is one of the game's.
                    // Qualifying a condition name would point it at an object that does not exist.
                    string target = phase.Action == DimensionBossPhaseActionKind.SummonAdds
                        ? DimensionObjectNamespace.Qualify(modName, phase.ActionTarget)
                        : phase.ActionTarget;

                    builder.AppendLine("    DimensionBossPhaseRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(phase.PhaseId)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(bossName)).AppendLine(",");
                    builder.Append("        ")
                        .Append(phase.HealthThreshold.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f,");
                    builder.Append("        DimensionBossPhaseAction.")
                        .Append(phase.Action.ToString()).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(target)).AppendLine(",");
                    builder.Append("        ")
                        .Append(phase.ActionAmount.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                    builder.Append("        ")
                        .Append(phase.ActionDuration.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f,");
                    builder.Append("        ")
                        .Append(phase.Radius.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                    // The tenth field, which used to be dropped here: the phase's own music.
                    builder.Append("        ").Append(ToCSharpString(phase.MusicCueId)).AppendLine(");");
                }
            }
        }

        /// <summary>
        /// Emits every authored loot table as a real registered table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The half that makes <c>DimensionLootTableAsset</c> more than paperwork: before this,
        /// a table's entries only mattered where a generator copied them onto a prefab, and any
        /// field that wanted the table BY NAME (a dungeon chest, a melody reward) found nothing.
        /// The runtime registry builds the table under its minted id at the game's own loot
        /// conversion seam; item names qualify here because only the editor knows which names
        /// the mod owns.
        /// </para>
        /// <para>
        /// Tables hang off several asset kinds (global list, mobs, elites, bosses, animals), so
        /// the walk deduplicates by table id — registering twice would replace, not stack, but
        /// the log noise would read as a bug.
        /// </para>
        /// </remarks>
        private static void AppendLootTableRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            List<DimensionLootTableAsset> tables = new List<DimensionLootTableAsset>();
            HashSet<string> seenIds = new HashSet<string>(System.StringComparer.Ordinal);

            void Collect(DimensionLootTableAsset table)
            {
                if (table != null && table.Enabled && !string.IsNullOrEmpty(table.LootTableId) &&
                    table.EnabledEntryCount > 0 && seenIds.Add(table.LootTableId))
                {
                    tables.Add(table);
                }
            }

            DimensionLootTableAsset[] globals = template.GlobalLootTables;
            for (int i = 0; globals != null && i < globals.Length; i++)
            {
                Collect(globals[i]);
            }

            DimensionMobAsset[] mobs = template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                if (mobs[i] == null)
                {
                    continue;
                }

                Collect(mobs[i].LootTable);
                Collect(mobs[i].EliteVariant == null ? null : mobs[i].EliteVariant.LootTable);
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            for (int i = 0; bosses != null && i < bosses.Length; i++)
            {
                Collect(bosses[i] == null ? null : bosses[i].LootTable);
            }

            DimensionAnimalAsset[] animals = template.GlobalAnimals;
            for (int i = 0; animals != null && i < animals.Length; i++)
            {
                Collect(animals[i] == null ? null : animals[i].LootTable);
            }

            for (int i = 0; i < tables.Count; i++)
            {
                DimensionLootTableAsset table = tables[i];

                // A vanilla name would resolve to the vanilla table everywhere the resolver
                // runs, so registering a custom table under it could never be reached — say so
                // instead of emitting a dead registration.
                LootTableID vanilla;
                if (System.Enum.TryParse(table.LootTableId, false, out vanilla))
                {
                    Debug.LogWarning(
                        "[Dimensions API] Loot table '" + table.LootTableId + "' shares its " +
                        "name with one of the game's own tables, so the game's is the one " +
                        "everything will roll. Rename yours to make it reachable.");
                    continue;
                }

                builder.Append("    DimensionLootTableRegistry.Register(")
                    .Append(ToCSharpString(table.LootTableId))
                    .Append(", ")
                    .Append(table.AllowEmptyRoll ? "true" : "false")
                    .AppendLine(", new DimensionLootTableRegistry.Entry[] {");

                DimensionLootEntryTemplate[] entries = table.Entries;
                bool wroteEntry = false;
                for (int e = 0; e < entries.Length; e++)
                {
                    DimensionLootEntryTemplate entry = entries[e];
                    if (entry == null || !entry.Enabled || string.IsNullOrEmpty(entry.ItemId))
                    {
                        continue;
                    }

                    string itemName = IsModOwnedObjectName(template, entry.ItemId)
                        ? DimensionObjectNamespace.Qualify(modName, entry.ItemId)
                        : entry.ItemId;

                    // The share used to decide the odds and the chance was thrown away. It is the
                    // other way round now — the chance is made true when the table is built — which
                    // leaves the share nothing to divide, so it is no longer drawn. A table
                    // authored before that still carries whatever was typed into it, and a number
                    // that no longer reaches anything is exactly the thing this framework refuses
                    // to leave unsaid.
                    if (entry.Weight != 1)
                    {
                        Debug.LogWarning(
                            "[Dimensions API] Loot table '" + table.LootTableId + "' gives '" +
                            entry.ItemId + "' a share of " + entry.Weight + ". Shares are no " +
                            "longer how the odds are decided — the drop chance is, and it now " +
                            "means exactly what it says. This row drops " +
                            (entry.DropChance * 100f).ToString("0.##", CultureInfo.InvariantCulture) +
                            "% of the time. Set its drop chance if that is not what you wanted.");
                    }

                    if (wroteEntry)
                    {
                        builder.AppendLine(",");
                    }

                    builder.Append("        new DimensionLootTableRegistry.Entry { ItemObjectName = ")
                        .Append(ToCSharpString(itemName))
                        .Append(", Weight = ")
                        .Append(((float)entry.Weight).ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, DropChance = ")
                        .Append(entry.DropChance.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, MinAmount = ")
                        .Append(entry.MinAmount.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxAmount = ")
                        .Append(entry.MaxAmount.ToString(CultureInfo.InvariantCulture))
                        .Append(" }");
                    wroteEntry = true;
                }

                builder.AppendLine();
                builder.AppendLine("    });");
            }
        }

        /// <summary>
        /// Emits each mob's keeps-coming-back rule into the game's own periodic respawn table.
        /// </summary>
        /// <remarks>
        /// The tileset resolves at emission because only the editor knows whether a name is one
        /// of this template's tilesets (minted id, computed at runtime from the same name) or a
        /// vanilla tileset (enum literal). A name that is neither is refused here, loudly,
        /// rather than emitted as a rule that can never match a tile.
        /// </remarks>
        private static void AppendRespawnRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionMobAsset[] mobs = template == null ? null : template.GlobalMobs;
            if (mobs == null)
            {
                return;
            }

            for (int i = 0; i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null || !mob.Enabled || mob.Respawn == null ||
                    !mob.Respawn.KeepsComingBack || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                DimensionRespawnTemplate respawn = mob.Respawn;
                string creatureName = DimensionObjectNamespace.Qualify(modName, mob.MobId);

                string tilesetExpression = null;
                string tilesetName = respawn.OnTileset;
                if (string.IsNullOrEmpty(tilesetName))
                {
                    tilesetExpression = string.Empty;
                }
                else if (FindTileset(template, tilesetName) != null)
                {
                    tilesetExpression =
                        "DimensionTilesetRegistry.ComputeTilesetId(" +
                        ToCSharpString(tilesetName) + ")";
                }
                else if (System.Enum.TryParse(tilesetName, false, out PugTilemap.Tileset vanillaTileset))
                {
                    tilesetExpression = "(int)PugTilemap.Tileset." + vanillaTileset;
                }
                else
                {
                    Debug.LogWarning(
                        "[Dimensions API] '" + mob.DisplayName + "' keeps coming back on " +
                        "tileset '" + tilesetName + "', which is neither one of this mod's " +
                        "tilesets nor a vanilla one. The rule was left out — fix the name.");
                    continue;
                }

                // The four surfaces vanilla's own respawn files key on; the value map lives
                // here, the one place allowed to know the game's numbers.
                string tileType;
                switch (respawn.Surface)
                {
                    case DimensionRespawnSurface.Nest:
                        tileType = "PugTilemap.TileType.chrysalis";
                        break;
                    case DimensionRespawnSurface.SlimeCoat:
                        tileType = "PugTilemap.TileType.groundSlime";
                        break;
                    case DimensionRespawnSurface.Water:
                        tileType = "PugTilemap.TileType.water";
                        break;
                    default:
                        tileType = "PugTilemap.TileType.ground";
                        break;
                }

                builder.AppendLine("    DimensionRespawnRegistry.Register(new DimensionRespawnRegistry.RespawnRule");
                builder.AppendLine("    {");
                builder.Append("        RuleName = ")
                    .Append(ToCSharpString(creatureName + ":respawn")).AppendLine(",");
                builder.Append("        CreatureObjectName = ")
                    .Append(ToCSharpString(creatureName)).AppendLine(",");
                builder.Append("        TileType = ").Append(tileType).AppendLine(",");
                builder.Append("        Tilesets = ")
                    .Append(string.IsNullOrEmpty(tilesetExpression)
                        ? "new int[0]"
                        : "new int[] { " + tilesetExpression + " }")
                    .AppendLine(",");
                builder.Append("        Chance = ")
                    .Append(respawn.Chance.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.Append("        ChanceDecayPerExisting = ")
                    .Append(respawn.CrowdSlowdown.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.Append("        MaxPerTile = ")
                    .Append(respawn.MostPerTile.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.Append("        MaxPerSweep = ")
                    .Append(respawn.MostPerSweep.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",");
                builder.Append("        MinTilesRequired = ")
                    .Append(respawn.FewestTilesNeeded.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.AppendLine("        OnlyInBiome = \"\"");
                builder.AppendLine("    });");
            }
        }

        private static DimensionTilesetAsset FindTileset(
            DimensionTemplateAsset template,
            string tilesetName)
        {
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            for (int i = 0; tilesets != null && i < tilesets.Length; i++)
            {
                if (tilesets[i] != null &&
                    string.Equals(tilesets[i].TilesetName, tilesetName, System.StringComparison.Ordinal))
                {
                    return tilesets[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Emits each boss's presentation row (pin, floating name) and respawn cooldown.
        /// </summary>
        /// <remarks>
        /// The literals half of the two-half registry: names and terms bake here; the sprites
        /// attach at runtime when the manifest loads, because a Sprite only exists once the
        /// bundle does. The respawn row only exists for a positive cooldown — zero means
        /// vanilla's own "gone means summonable", which needs no machinery.
        /// </remarks>
        private static void AppendBossPresentationRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionBossAsset[] bosses = template == null ? null : template.GlobalBosses;
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId))
                {
                    continue;
                }

                string bossName = DimensionObjectNamespace.Qualify(modName, boss.BossId);
                string nameTerm = "Names/" + DimensionLocalizationCsv.ToLookupKeyName(bossName);
                string hoverTerm = string.IsNullOrEmpty(boss.MapPin.HoverName)
                    ? nameTerm
                    : "Names/" + DimensionLocalizationCsv.ToLookupKeyName(bossName + "-pin");

                builder.AppendLine("    DimensionBossPresentationRegistry.Register(");
                builder.AppendLine("        new DimensionBossPresentationDefinition(");
                builder.Append("            ").Append(ToCSharpString(bossName)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(boss.BossId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(nameTerm)).AppendLine(",");
                builder.Append("            ")
                    .Append(boss.MapPin.ShowsOnTheMap ? "true" : "false").AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(hoverTerm)).AppendLine("));");

                if (boss.RespawnCooldownMinutes > 0f)
                {
                    builder.Append("    DimensionBossRespawnRegistry.Register(")
                        .Append(ToCSharpString(bossName))
                        .Append(", ")
                        .Append(boss.RespawnCooldownMinutes.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f);");
                }

                // A fight-music name that is not a vanilla roster and carries its own tracks is
                // the mod's own cue; the runtime registry gives it a roster the game can pick.
                MusicRosterType vanillaRoster;
                string[] trackKeys = boss.FightMusic.CustomTrackKeys;
                if (trackKeys.Length > 0 &&
                    !string.IsNullOrEmpty(boss.FightMusic.MusicId) &&
                    !System.Enum.TryParse(boss.FightMusic.MusicId, false, out vanillaRoster))
                {
                    builder.Append("    DimensionMusicRosterRegistry.RegisterCue(")
                        .Append(ToCSharpString(boss.FightMusic.MusicId))
                        .Append(", new string[] { ");
                    bool wroteTrack = false;
                    for (int t = 0; t < trackKeys.Length; t++)
                    {
                        if (string.IsNullOrEmpty(trackKeys[t]))
                        {
                            continue;
                        }

                        if (wroteTrack)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(ToCSharpString(trackKeys[t]));
                        wroteTrack = true;
                    }

                    builder.AppendLine(" });");
                }
            }
        }

        /// <summary>
        /// Emits each creature's claim on where in the world it appears.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A creature with no biome listed is emitted once, unrestricted — Core Keeper reads an empty
        /// biome as "anywhere", which is what an author means by leaving it blank. A creature listing
        /// several biomes is emitted once per biome, because that is how the game's table is shaped:
        /// each row answers one "where", and a bat common in caves and rare outside is genuinely two
        /// rows rather than one with a condition.
        /// </para>
        /// <para>
        /// The tilesets come from the biome's own blocks, the same derivation the title cards use, so
        /// "in this biome" means the same thing to spawning as it does to everything else.
        /// </para>
        /// </remarks>
        private static void AppendCreatureSpawnRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionMobAsset[] mobs = template == null ? null : template.GlobalMobs;
            DimensionAnimalAsset[] animals = template == null ? null : template.GlobalAnimals;
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            if (mobs != null)
            {
                for (int i = 0; i < mobs.Length; i++)
                {
                    DimensionMobAsset mob = mobs[i];
                    if (mob == null || !mob.Enabled || !mob.SpawnsInWorld)
                    {
                        continue;
                    }

                    AppendSpawnRowsFor(
                        builder,
                        template,
                        tilesets,
                        DimensionObjectNamespace.Qualify(modName, mob.MobId),
                        mob.AllowedBiomeIds,
                        mob.SpawnChance,
                        mob.SpawnAmount,
                        mob.SpawnsInGroups,
                        mob.CanSpawnInBlockedArea);

                    // The elite spawns wherever its parent does, just far less often — which is the
                    // whole of what makes it feel like a rare encounter rather than a second enemy.
                    DimensionEliteVariantTemplate elite = mob.EliteVariant;
                    if (elite.Enabled)
                    {
                        AppendSpawnRowsFor(
                            builder,
                            template,
                            tilesets,
                            DimensionObjectNamespace.Qualify(
                                modName, DimensionEliteVariantTemplate.IdFor(mob.MobId)),
                            mob.AllowedBiomeIds,
                            mob.SpawnChance / elite.RarityFactor,
                            1,
                            false,
                            mob.CanSpawnInBlockedArea);
                    }
                }
            }

            if (animals != null)
            {
                for (int i = 0; i < animals.Length; i++)
                {
                    DimensionAnimalAsset animal = animals[i];
                    if (animal == null || !animal.Enabled || !animal.SpawnsInWorld)
                    {
                        continue;
                    }

                    AppendSpawnRowsFor(
                        builder,
                        template,
                        tilesets,
                        DimensionObjectNamespace.Qualify(modName, animal.AnimalId),
                        animal.AllowedBiomeIds,
                        animal.SpawnChance,
                        animal.SpawnAmount,
                        animal.SpawnsInGroups,
                        animal.CanSpawnInBlockedArea);
                }
            }
        }

        private static void AppendSpawnRowsFor(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionTilesetAsset[] tilesets,
            string objectName,
            string[] biomeIds,
            float spawnChance,
            int amount,
            bool clustered,
            bool canSpawnInBlockedArea)
        {
            if (biomeIds == null || biomeIds.Length == 0)
            {
                AppendSpawnRow(
                    builder, objectName, string.Empty, new List<int>(),
                    spawnChance, amount, clustered, canSpawnInBlockedArea);
                return;
            }

            for (int i = 0; i < biomeIds.Length; i++)
            {
                BiomeTemplateAsset biome = FindBiome(template, biomeIds[i]);
                List<int> tilesetIds = biome == null
                    ? new List<int>()
                    : CollectBiomeTilesetIds(biome, tilesets);

                AppendSpawnRow(
                    builder, objectName, biomeIds[i], tilesetIds,
                    spawnChance, amount, clustered, canSpawnInBlockedArea);
            }
        }

        private static void AppendSpawnRow(
            StringBuilder builder,
            string objectName,
            string biomeId,
            List<int> tilesetIds,
            float spawnChance,
            int amount,
            bool clustered,
            bool canSpawnInBlockedArea)
        {
            builder.AppendLine("    DimensionCreatureSpawnRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(objectName)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(biomeId)).AppendLine(",");

            builder.Append("        new int[] { ");
            for (int t = 0; t < tilesetIds.Count; t++)
            {
                if (t > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(tilesetIds[t].ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine(" },");

            // Ground: the surface almost everything walks on. A creature that belongs in water or on
            // a wall is a different shape of authoring and is not offered yet rather than guessed at.
            builder.AppendLine("        PugTilemap.TileType.ground,");
            builder.Append("        ")
                .Append(spawnChance.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.AppendLine("        1,");
            builder.Append("        ").Append(amount.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.AppendLine("        " + (clustered ? "true" : "false") + ",");
            builder.AppendLine("        " + (canSpawnInBlockedArea ? "true" : "false") + ");");
        }

        private static BiomeTemplateAsset FindBiome(DimensionTemplateAsset template, string biomeId)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes == null || string.IsNullOrEmpty(biomeId))
            {
                return null;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i] != null &&
                    string.Equals(biomes[i].BiomeId, biomeId, StringComparison.Ordinal))
                {
                    return biomes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Emits what a biome sounds like: its ambience loop and which music playlist it plays.
        /// </summary>
        /// <remarks>
        /// Emitted even when a biome has neither, because the registration is also what claims the
        /// biome's tilesets — and a biome with no sound of its own still needs the game to know the
        /// player is standing in it.
        /// </remarks>
        private static void AppendBiomeAtmosphereRegistration(
            StringBuilder builder,
            BiomeTemplateAsset biome,
            List<int> tilesetIds)
        {
            builder.AppendLine("    DimensionBiomeAtmosphereRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(biome.BiomeId)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(biome.AmbienceSoundKey)).AppendLine(",");
            builder.Append("        ")
                .Append(biome.AmbienceVolume.ToString("R", CultureInfo.InvariantCulture))
                .AppendLine("f,");
            builder.Append("        ").Append(ToCSharpString(biome.MusicRosterName)).AppendLine(",");

            builder.Append("        new int[] { ");
            for (int t = 0; t < tilesetIds.Count; t++)
            {
                if (t > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(tilesetIds[t].ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine(" });");
        }

        /// <summary>
        /// The custom tilesets a biome's own floor and wall blocks belong to.
        /// </summary>
        /// <remarks>
        /// Matched on the generated block item ids rather than on the tileset name, because that is
        /// what a biome actually references — the author picks blocks, and the tileset is what those
        /// blocks are made of.
        /// </remarks>
        private static List<int> CollectBiomeTilesetIds(
            BiomeTemplateAsset biome,
            DimensionTilesetAsset[] tilesets)
        {
            List<int> ids = new List<int>();
            string[] floors = biome.FloorObjectIds;
            string[] walls = biome.WallObjectIds;

            for (int i = 0; i < tilesets.Length; i++)
            {
                DimensionTilesetAsset tileset = tilesets[i];
                if (tileset == null || !tileset.Enabled)
                {
                    continue;
                }

                if (!ContainsOrdinal(floors, tileset.GroundBlockItemId) &&
                    !ContainsOrdinal(floors, tileset.WallBlockItemId) &&
                    !ContainsOrdinal(walls, tileset.WallBlockItemId) &&
                    !ContainsOrdinal(walls, tileset.GroundBlockItemId))
                {
                    continue;
                }

                int id = tileset.TilesetId;
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static bool ContainsOrdinal(string[] values, string candidate)
        {
            if (values == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Emits the drops that could not be written onto a prefab as custom loot.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The far end of the drop-location inversion. An item saying "2 to 5 of me drop off slimes,
        /// but only in the desert" cannot become per-object custom loot — <c>LootDrop</c> has a single
        /// <c>amount</c> and no biome field — so it has to be registered against the source loot table
        /// at load instead.
        /// </para>
        /// <para>
        /// The plan is rebuilt here rather than carried over from prefab generation, because the two
        /// run as separate passes and a value threaded between them is a value that can go stale.
        /// Rebuilding is cheap and cannot disagree with itself.
        /// </para>
        /// <para>
        /// Chance is expressed as a percentage because that is what the registry takes; a drop the
        /// author marked as always-dropping is sent as 100, which is what the registry reads as
        /// guaranteed.
        /// </para>
        /// <para>
        /// Internal rather than private so a test can drive the one capability this whole wave
        /// exists for — a mod's creature dropping a mod's item — instead of grepping for the call.
        /// </para>
        /// </remarks>
        internal static void AppendAuthoredDropRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            DimensionDropPlan plan = DimensionDropPlan.Build(
                template.GlobalItems,
                template.GlobalWorldObjects);
            if (plan.IsEmpty)
            {
                return;
            }

            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;

            for (int s = 0; s < plan.Sources.Count; s++)
            {
                DimensionDropsForSource source = plan.Sources[s];
                for (int d = 0; d < source.Drops.Count; d++)
                {
                    DimensionResolvedDrop drop = source.Drops[d];
                    DimensionDropSource settings = drop.Source;

                    // Anything written onto the prefab is skipped here; writing it twice would give
                    // the player two of everything. The question is asked through the one shared
                    // answer, because a drop the prefab writer declined and this one skipped is a
                    // drop that happens nowhere.
                    if (DimensionDropEmitter.GoesOnTheObjectItself(source, drop) ||
                        settings.DropsNothing)
                    {
                        continue;
                    }

                    float chancePercent = settings.AlwaysDrops ? 100f : settings.Chance * 100f;

                    // Mod-own names must ship QUALIFIED, like every other emission — this was
                    // the one emitter that never qualified, so colon-less ids resolved to nothing
                    // at load and the drop silently vanished.
                    string sourceName = IsModOwnedObjectName(template, source.SourceId)
                        ? DimensionObjectNamespace.Qualify(modName, source.SourceId)
                        : source.SourceId;
                    string itemName = IsModOwnedObjectName(template, drop.ItemId)
                        ? DimensionObjectNamespace.Qualify(modName, drop.ItemId)
                        : drop.ItemId;

                    // THE TABLE TRAVELS WITH THE ROW. At load the registry cannot read a source's
                    // loot table off its prefab — it runs inside database conversion, where no
                    // entity world can answer — so a source of this mod's own ships the name of
                    // the table the generator stamped it with. Empty for one of the game's, which
                    // the registry reads off the game's own authoring prefab instead.
                    string sourceTable = SourceLootTableNameOf(template, modName, source.SourceId);

                    builder.AppendLine("    DimensionPortalDropRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(sourceName)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(itemName)).AppendLine(",");
                    builder.Append("        ").Append(((float)settings.Weight).ToString(inv)).AppendLine("f,");
                    builder.Append("        ").Append(chancePercent.ToString(inv)).AppendLine("f,");
                    builder.Append("        ").Append(settings.MinAmount.ToString(inv)).AppendLine(",");
                    builder.Append("        ").Append(settings.MaxAmount.ToString(inv)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(settings.OnlyInBiomeId)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(sourceTable)).AppendLine(");");
                }
            }
        }

        /// <summary>
        /// Emits a scene registration for every authored scene that stamps terrain.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Emitted into the mod's own bootstrap rather than discovered at runtime because the scene
        /// table can only be rebuilt in one narrow window during world start — by then, nothing is
        /// going to go looking through asset files. Registration has to have already happened.
        /// </para>
        /// <para>
        /// Only scenes with tiles are emitted. A scene of pure props and spawns is placed by this
        /// framework's own systems and has no business occupying one of the world's scene slots.
        /// </para>
        /// <para>
        /// The name is mod-qualified and then checked here, at generation time, against the limit a
        /// spawn request can carry. Left to runtime it would register, never spawn, and log nothing.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Grows the placed portal in the game's own world, for a rule that asks to be found rather
        /// than crafted.
        /// </summary>
        /// <remarks>
        /// <para>
        /// There is no separate "put this object in the Overworld" road in Core Keeper: the only
        /// thing world generation grows on its own is a custom scene. So this builds the smallest
        /// honest one — a single cleared tile with the portal standing on it — and hands it the
        /// same natural-growth arguments an authored scene uses. Everything after that is the
        /// game's: it picks a spot with enough clearance, clears the cell, lays the ground, and
        /// instantiates the prefab.
        /// </para>
        /// <para>
        /// The one tile is not decoration. Placement clears a scene's own cells before writing
        /// them, so the tile is what guarantees the portal is never found buried inside a wall or
        /// standing in water. It is laid as plain dirt, which reads as a small pad in biomes that
        /// are not dirt already.
        /// </para>
        /// <para>
        /// A rule that names no biome the Overworld actually samples registers nothing at all —
        /// <see cref="BuildOverworldSpawnArguments"/> says so out loud — because a scene with no
        /// biomes is invisible to the placer and would be dead weight in the table.
        /// </para>
        /// </remarks>
        private static void AppendPortalWorldSceneRegistration(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionRuntimePortalOutput portalOutput,
            string modName)
        {
            DimensionPortalAccessRuleAsset rule = FindPortalRule(
                template,
                DimensionPortalAccessKind.PlacedPortal,
                portalOutput.DimensionId,
                false);
            if (rule == null || !rule.GeneratedInWorld)
            {
                return;
            }

            string sceneName = DimensionObjectNamespace.Qualify(
                modName,
                portalOutput.DimensionId + ".portal");
            string nameError;
            if (!DimensionCustomSceneNames.IsValid(sceneName, out nameError))
            {
                Debug.LogWarning(
                    "[ExpandNullforge] The portal for '" + portalOutput.DimensionId + "' cannot be " +
                    "found in the world. " + nameError + " Shorten the dimension's id, or turn off " +
                    "\"Found in the world\" and let players craft the portal instead.");
                return;
            }

            string spawnArguments = BuildOverworldSpawnArguments(
                "The portal for '" + portalOutput.DimensionId + "'",
                rule.WorldBiomeNames,
                rule.WorldMaxOccurrences,
                rule.WorldMinDistanceFromCore);
            if (spawnArguments.Length == 0)
            {
                return;
            }

            builder.AppendLine("    {");
            builder.AppendLine("      var portalSpotTiles = new System.Collections.Generic.List<DimensionSceneTileRequest>();");
            builder.AppendLine("      portalSpotTiles.Add(new DimensionSceneTileRequest(new int2(0, 0), \"0\", DimensionTileRole.Ground));");
            builder.AppendLine("      var portalSpotObjects = new System.Collections.Generic.List<DimensionSceneObject>();");
            builder.AppendLine(
                "      portalSpotObjects.Add(new DimensionSceneObject(new int2(0, 0), PortalObjectName, " +
                "DimensionSceneFacing.Down, DimensionScenePaintChoice.Unpainted, \"\", null));");
            builder.Append("      var portalSpotResult = DimensionSceneTileCompiler.Compile(")
                .Append(ToCSharpString(sceneName))
                .AppendLine(", portalSpotTiles);");
            builder.AppendLine("      if (portalSpotResult.Tiles.Count > 0)");
            builder.AppendLine("      {");
            builder.Append("        DimensionCustomSceneRegistry.Register(new DimensionCustomSceneDefinition(")
                .Append(ToCSharpString(sceneName))
                .Append(", portalSpotResult.Tiles, centerPosition: new int2(0, 0)")
                .Append(", objects: portalSpotObjects")
                .Append(spawnArguments)
                .AppendLine("));");
            builder.AppendLine("      }");
            builder.AppendLine("    }");
        }

        private static void AppendSceneRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            List<SceneTemplateAsset> scenes = CollectSceneTemplates(template);
            Dictionary<SceneTemplateAsset, List<string>> owningBiomes = CollectSceneOwners(template);
            for (int i = 0; i < scenes.Count; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene == null || !scene.Enabled || !scene.HasTiles)
                {
                    continue;
                }

                string sceneName = DimensionObjectNamespace.Qualify(modName, scene.SceneId);
                string nameError;
                if (!DimensionCustomSceneNames.IsValid(sceneName, out nameError))
                {
                    Debug.LogWarning("[ExpandNullforge] Scene '" + scene.SceneId + "' cannot be registered. " + nameError);
                    continue;
                }

                // Build the tile lines first and only open the block if there are any: a scene whose
                // tiles are all disabled or blank would otherwise emit a block that registers nothing.
                List<string> tileLines = new List<string>();
                DimensionSceneTileTemplate[] tiles = scene.Tiles;
                for (int t = 0; t < tiles.Length; t++)
                {
                    DimensionSceneTileTemplate tile = tiles[t];
                    if (tile == null || !tile.Enabled || string.IsNullOrEmpty(tile.BlockId))
                    {
                        continue;
                    }

                    WarnIfTilesetHasNoBlockObject(template, scene, tile);

                    tileLines.Add(
                        "      sceneTiles.Add(new DimensionSceneTileRequest(new int2(" +
                        tile.LocalPosition.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        tile.LocalPosition.y.ToString(CultureInfo.InvariantCulture) + "), " +
                        ToCSharpString(DimensionObjectNamespace.Qualify(modName, tile.BlockId)) +
                        ", DimensionTileRole." + tile.Role + "));");
                }

                if (tileLines.Count == 0)
                {
                    continue;
                }

                // Objects are gathered the same way and for the same reason: a scene whose objects
                // are all disabled should emit no object list rather than an empty one.
                List<string> objectLines = new List<string>();
                DimensionSceneObjectTemplate[] sceneObjects = scene.SceneObjects;
                for (int o = 0; o < sceneObjects.Length; o++)
                {
                    DimensionSceneObjectTemplate placed = sceneObjects[o];
                    if (placed == null || !placed.Enabled || string.IsNullOrEmpty(placed.ObjectId))
                    {
                        continue;
                    }

                    // One of the mod's own objects is namespaced like everything else it generates;
                    // a vanilla one keeps the name the game already knows it by.
                    string objectName = IsModOwnedObjectName(template, placed.ObjectId)
                        ? DimensionObjectNamespace.Qualify(modName, placed.ObjectId)
                        : placed.ObjectId;

                    // Contents are built inline so each container carries its own list; a shared one
                    // would let two chests in a scene end up holding the same objects.
                    string contentsExpression = "null";
                    DimensionSceneContainerItem[] contents = placed.Contents;
                    if (contents.Length > 0)
                    {
                        StringBuilder contentsBuilder = new StringBuilder();
                        contentsBuilder.Append("new DimensionSceneContent[] { ");
                        bool wroteAny = false;

                        for (int c = 0; c < contents.Length; c++)
                        {
                            DimensionSceneContainerItem item = contents[c];
                            if (item == null || string.IsNullOrEmpty(item.ItemId))
                            {
                                continue;
                            }

                            if (wroteAny)
                            {
                                contentsBuilder.Append(", ");
                            }

                            string itemName = IsModOwnedObjectName(template, item.ItemId)
                                ? DimensionObjectNamespace.Qualify(modName, item.ItemId)
                                : item.ItemId;

                            contentsBuilder
                                .Append("new DimensionSceneContent(")
                                .Append(ToCSharpString(itemName)).Append(", ")
                                .Append(item.Amount.ToString(CultureInfo.InvariantCulture)).Append(")");
                            wroteAny = true;
                        }

                        contentsBuilder.Append(" }");
                        if (wroteAny)
                        {
                            contentsExpression = contentsBuilder.ToString();
                        }
                    }

                    objectLines.Add(
                        "      sceneObjects.Add(new DimensionSceneObject(new int2(" +
                        placed.LocalPosition.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        placed.LocalPosition.y.ToString(CultureInfo.InvariantCulture) + "), " +
                        ToCSharpString(objectName) +
                        ", DimensionSceneFacing." + placed.Facing +
                        ", DimensionScenePaintChoice." + placed.Paint +
                        ", " + ToCSharpString(placed.LootTableId) +
                        ", " + contentsExpression + "));");
                }

                AppendArenaBosses(objectLines, template, scene, modName);

                // Triggers travel with the scene the same way objects do: scene-local here, turned
                // into world-anchored registrations by the placement pass at stamp time — the first
                // moment anything knows where the scene landed.
                List<string> triggerLines = new List<string>();
                DimensionSceneTriggerTemplate[] triggers = scene.Triggers;
                for (int g = 0; g < triggers.Length; g++)
                {
                    DimensionSceneTriggerTemplate trigger = triggers[g];
                    if (trigger == null || !trigger.Enabled || string.IsNullOrEmpty(trigger.TriggerId))
                    {
                        continue;
                    }

                    string carriedItemName = IsModOwnedObjectName(template, trigger.CarriedItemId)
                        ? DimensionObjectNamespace.Qualify(modName, trigger.CarriedItemId)
                        : trigger.CarriedItemId;

                    // Only a summoned creature is one of the mod's own objects; a condition target
                    // is a name in the game's own vocabulary and must reach the runtime untouched.
                    string actionTarget = trigger.Action == ExpandNullforge.Zones.DimensionTileAction.SummonCreatures &&
                        IsModOwnedObjectName(template, trigger.Target)
                        ? DimensionObjectNamespace.Qualify(modName, trigger.Target)
                        : trigger.Target;

                    triggerLines.Add(
                        "      sceneTriggers.Add(new DimensionSceneTrigger(" +
                        ToCSharpString(trigger.TriggerId) +
                        ", new int2(" +
                        trigger.LocalMin.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        trigger.LocalMin.y.ToString(CultureInfo.InvariantCulture) + ")" +
                        ", new int2(" +
                        trigger.LocalMaxExclusive.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        trigger.LocalMaxExclusive.y.ToString(CultureInfo.InvariantCulture) + ")" +
                        ", DimensionTileTrigger." + trigger.Kind +
                        ", " + ToCSharpString(carriedItemName) +
                        ", DimensionTileAction." + trigger.Action +
                        ", " + ToCSharpString(actionTarget) +
                        ", " + trigger.Amount.ToString(CultureInfo.InvariantCulture) +
                        ", " + trigger.ConditionSeconds.ToString(CultureInfo.InvariantCulture) + "f" +
                        ", " + trigger.CooldownSeconds.ToString(CultureInfo.InvariantCulture) + "f" +
                        ", " + (trigger.OnceOnly ? "true" : "false") + "));");
                }

                builder.AppendLine("    {");
                builder.AppendLine("      var sceneTiles = new System.Collections.Generic.List<DimensionSceneTileRequest>();");
                for (int t = 0; t < tileLines.Count; t++)
                {
                    builder.AppendLine(tileLines[t]);
                }

                builder.AppendLine("      var sceneObjects = new System.Collections.Generic.List<DimensionSceneObject>();");
                for (int o = 0; o < objectLines.Count; o++)
                {
                    builder.AppendLine(objectLines[o]);
                }

                builder.AppendLine("      var sceneTriggers = new System.Collections.Generic.List<DimensionSceneTrigger>();");
                for (int g = 0; g < triggerLines.Count; g++)
                {
                    builder.AppendLine(triggerLines[g]);
                }

                builder.Append("      var sceneResult = DimensionSceneTileCompiler.Compile(")
                    .Append(ToCSharpString(sceneName))
                    .AppendLine(", sceneTiles);");
                builder.AppendLine("      for (int i = 0; i < sceneResult.Skipped.Count; i++)");
                builder.AppendLine("      {");
                builder.AppendLine("        DimensionConsumerLog.Problem(DimensionId, sceneResult.Skipped[i]);");
                builder.AppendLine("      }");
                builder.AppendLine("      if (sceneResult.Tiles.Count > 0)");
                builder.AppendLine("      {");
                // The explicit pivot is the whole alignment story: the game stamps a scene as
                // anchor + (tile − centre), corridors aim at room CENTRES, and their band is
                // always odd and centred on that line. Without this argument the pivot defaults
                // to (0,0) — the scene's corner — and every dungeon room built from the scene
                // reads as shoved half its size off its corridors.
                builder.Append("        DimensionCustomSceneRegistry.Register(new DimensionCustomSceneDefinition(")
                    .Append(ToCSharpString(sceneName))
                    .Append(", sceneResult.Tiles, centerPosition: DimensionSceneGeometry.CentreOf(sceneResult.Tiles)")
                    .Append(", objects: sceneObjects")
                    .Append(", triggers: sceneTriggers")
                    .Append(BuildOverworldSpawnArguments(scene))
                    .AppendLine("));");

                // The placement policy rides beside the tile data. Without this line the Studio's
                // placement page — mode, radial band, weight, unique, required — was authored and
                // then thrown away: nothing at runtime ever read it.
                AppendScenePoolRegistration(builder, template, scene, sceneName, owningBiomes);

                builder.AppendLine("      }");
                builder.AppendLine("    }");
            }
        }

        /// <summary>
        /// Warns when a scene tile uses a block whose tileset generates no object for that role.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This catches a bug that is invisible everywhere except inside a dungeon. A directly-placed
        /// scene writes its tiles straight into the map, so any tileset works. A scene embedded in a
        /// generated dungeon room does NOT: <c>DungeonGenerateRoomsSystem</c> resolves each
        /// (tileset, tileType) to an ObjectID through the object database and <b>drops the tile</b>
        /// when nothing matches.
        /// </para>
        /// <para>
        /// The only thing that registers a (tileset, tileType) pair is an object that IS that tile —
        /// the generated block. So a tileset used purely as scene decoration, with both block toggles
        /// off, has no entry, and every tile of it silently vanishes from dungeon rooms while looking
        /// perfectly fine in the open world. Someone hitting that would reasonably conclude their
        /// dungeon was broken, not their block.
        /// </para>
        /// <para>
        /// Warned at generation rather than blocked: the scene is still valid outside dungeons, and
        /// refusing to generate would be worse than telling the author what they will see.
        /// </para>
        /// </remarks>
        private static void WarnIfTilesetHasNoBlockObject(
            DimensionTemplateAsset template,
            SceneTemplateAsset scene,
            DimensionSceneTileTemplate tile)
        {
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            DimensionTilesetAsset match = null;
            for (int i = 0; i < tilesets.Length; i++)
            {
                if (tilesets[i] != null &&
                    string.Equals(tilesets[i].TilesetName, tile.BlockId, System.StringComparison.Ordinal))
                {
                    match = tilesets[i];
                    break;
                }
            }

            if (match == null)
            {
                // A vanilla tileset, or a block from another mod. Vanilla always has its block
                // objects, and another mod's content is not ours to vet.
                return;
            }

            bool needsGround = tile.Role == DimensionTileRole.Ground;
            bool needsWall = tile.Role == DimensionTileRole.Wall;
            if ((needsGround && match.GenerateGroundBlock) || (needsWall && match.GenerateWallBlock) ||
                (!needsGround && !needsWall))
            {
                return;
            }

            Debug.LogWarning(
                "[ExpandNullforge] Scene '" + scene.SceneId + "' places '" + tile.BlockId + "' as " +
                tile.Role + " at " + tile.LocalPosition + ", but that block does not generate a " +
                (needsGround ? "ground" : "wall") + " object. The tile will appear in the open world " +
                "and be silently dropped from any dungeon room this scene is embedded in. Turn on the " +
                "matching block in the Tileset Studio, or accept that this scene is not dungeon-safe.");
        }

        /// <summary>Every scene a template can reach, global and per-biome, without duplicates.</summary>
        /// <summary>
        /// Whether an object a scene places is one this mod defines, rather than one of the game's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Scenes are the one place both kinds of name meet. A scene's tiles are always the mod's own
        /// blocks, so those are namespaced unconditionally; its objects are mostly vanilla — a chest,
        /// a torch, a statue — with the occasional item the mod itself defines. Namespacing everything
        /// would rename <c>Chest</c> into something the game has never heard of; namespacing nothing
        /// would let a mod's own item collide with another mod's item of the same name.
        /// </para>
        /// <para>
        /// So the mod's own declared content is the authority: if the name is something this template
        /// generates, it is qualified; otherwise it is passed through untouched for the database to
        /// resolve at injection time. An unknown name is a warning there, not a silent nothing.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Whether a name is one of the mod's own objects, asked of the one walk both sides share.
        /// </summary>
        /// <remarks>
        /// This used to be its own hand-written list of the asset kinds it happened to think of —
        /// items, workbenches, tileset blocks and creatures — which left containers, world objects,
        /// vehicles, projectiles, plants and explosions out. A drop from one of the mod's own chests
        /// therefore shipped its source name unqualified, resolved to nothing at load, and the item
        /// dropped from nowhere. <c>DimensionGeneratedObjectIds.Collect</c> is the walk the binder
        /// and the link emitter already agree on, so asking it here means one list rather than two.
        /// </remarks>
        /// <summary>
        /// Says so when one of the mod's own things is called the same as one of the game's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE WARNING THAT WAS PROMISED AND NEVER WRITTEN. <c>DimensionNamingContext.Owns</c>'s
        /// own remark says "an id that shadows a game object is reported at generate time", and
        /// nothing anywhere reported it. It matters because the two halves of the framework answer
        /// differently for such a name: the generators stamp the object as the mod's own, while
        /// every reference to it — a drop's source, a recipe ingredient, a summoning item's boss —
        /// asks <c>Owns</c>, which puts the game's names first and answers no.
        /// </para>
        /// <para>
        /// What that does to somebody: they call their boss <c>Larva</c> and tick an item as
        /// dropping from it. Generation is clean. In the game, every wild larva in the world drops
        /// their item and their own boss drops nothing. The same split reaches recipe ingredients
        /// and the list of bosses a summoning item can call.
        /// </para>
        /// <para>
        /// It is said rather than fixed by renaming, because the name is the author's to choose and
        /// silently changing it would break every reference they have already written by hand.
        /// </para>
        /// </remarks>
        private static void SayWhenOneOfOursSharesAGameObjectsName(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return;
            }

            List<string> ours = DimensionGeneratedObjectIds.Collect(template);
            for (int i = 0; i < ours.Count; i++)
            {
                string id = ours[i];
                if (string.IsNullOrEmpty(id) || DimensionObjectBinder.Vanilla(id) == ObjectID.None)
                {
                    continue;
                }

                Debug.LogWarning(
                    "[Dimensions API] One of your own things is called '" + id + "', which is also " +
                    "the name of something the game already has. Anything that points at that " +
                    "name — a drop, a recipe ingredient, a summoning item's boss — will reach the " +
                    "game's one and not yours, and yours will never be reached at all. Rename it.");
            }
        }

        private static bool IsModOwnedObjectName(DimensionTemplateAsset template, string objectId)
        {
            if (template == null || string.IsNullOrEmpty(objectId))
            {
                return false;
            }

            // ONE OWNERSHIP ANSWER, THE SAME ONE THE GENERATORS BAKE FROM. This used to be the walk
            // above plus five hand-written ones underneath it, and neither half asked the ObjectID
            // enum first. So a mod item called Torch was baked by every generator as the GAME's
            // torch (DimensionObjectBinder.Vanilla wins there) while this said "ours" and shipped
            // "MyMod:Torch" in the drop row and the recipe output — the two halves of the same
            // authored thing pointing at different objects. DimensionNamingContext.Owns is the
            // predicate the generators use, vanilla-first and all; asking it here is what makes
            // "both sides call this" true rather than aspirational.
            //
            // The hand-written walks also counted switched-off assets as ours. A switched-off asset
            // generates no object, so a name qualified against it resolves to nothing at load —
            // which is the silent loss Collect's own remark says it exists to prevent.
            return new DimensionNamingContext(
                    string.Empty,
                    DimensionGeneratedObjectIds.Collect(template))
                .Owns(objectId);
        }

        /// <summary>
        /// The loot table a drop registered against this source will land in, or empty when the
        /// source is one of the game's own and the game has to be asked.
        /// </summary>
        /// <remarks>
        /// The load-time injection cannot read a source's loot table off its prefab entity — it
        /// runs inside database conversion, when no entity world can answer. So the answer travels
        /// with the row instead, computed by the same
        /// <see cref="DimensionDropEmitter.LootTableNameFor"/> the generator stamps the prefab from.
        /// </remarks>
        private static string SourceLootTableNameOf(
            DimensionTemplateAsset template,
            string modName,
            string sourceId)
        {
            if (!IsModOwnedObjectName(template, sourceId))
            {
                return string.Empty;
            }

            return DimensionDropEmitter.LootTableNameFor(
                AuthoredLootTableOf(template, sourceId),
                DimensionObjectNamespace.Qualify(modName, sourceId));
        }

        /// <summary>
        /// The loot table asset a creature of this mod's own was pointed at, or null for anything
        /// with no such field (a container, a world object, a critter).
        /// </summary>
        private static DimensionLootTableAsset AuthoredLootTableOf(
            DimensionTemplateAsset template,
            string sourceId)
        {
            if (template == null || string.IsNullOrEmpty(sourceId))
            {
                return null;
            }

            string local = DimensionObjectNamespace.LocalIdOf(sourceId);

            DimensionMobAsset[] mobs = template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                if (mobs[i] == null)
                {
                    continue;
                }

                if (string.Equals(mobs[i].MobId, local, StringComparison.Ordinal))
                {
                    return mobs[i].LootTable;
                }

                // An elite is generated as a second creature under "<mobId>.elite", sharing the
                // base mob's loot unless it was given its own.
                if (string.Equals(mobs[i].MobId + ".elite", local, StringComparison.Ordinal))
                {
                    DimensionEliteVariantTemplate elite = mobs[i].EliteVariant;
                    return elite != null && elite.LootTable != null
                        ? elite.LootTable
                        : mobs[i].LootTable;
                }
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            for (int i = 0; bosses != null && i < bosses.Length; i++)
            {
                if (bosses[i] != null &&
                    string.Equals(bosses[i].BossId, local, StringComparison.Ordinal))
                {
                    return bosses[i].LootTable;
                }
            }

            DimensionAnimalAsset[] animals = template.GlobalAnimals;
            for (int i = 0; animals != null && i < animals.Length; i++)
            {
                if (animals[i] != null &&
                    string.Equals(animals[i].AnimalId, local, StringComparison.Ordinal))
                {
                    return animals[i].LootTable;
                }
            }

            return null;
        }

        /// <summary>
        /// Puts a boss into the handmade place it waits in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A boss names its arena scene, and until now that name was only checked — the author
        /// still had to place the boss into the scene by hand, or walk into an empty arena. This
        /// closes it: the boss becomes an ordinary placed object at the middle of its scene,
        /// riding the same path every other scene object rides.
        /// </para>
        /// <para>
        /// An author who placed the boss themselves keeps their placement; only a scene that
        /// does not already contain it gets one, so this can never double a boss.
        /// </para>
        /// </remarks>
        private static void AppendArenaBosses(
            List<string> objectLines,
            DimensionTemplateAsset template,
            SceneTemplateAsset scene,
            string modName)
        {
            DimensionBossAsset[] bosses = template == null ? null : template.GlobalBosses;
            if (bosses == null || scene == null || string.IsNullOrEmpty(scene.SceneId))
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId) ||
                    !string.Equals(boss.ArenaSceneId, scene.SceneId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (SceneAlreadyPlaces(scene, boss.BossId))
                {
                    continue;
                }

                Vector2Int centre = ResolveSceneCentre(scene);
                string bossName = DimensionObjectNamespace.Qualify(modName, boss.BossId);
                objectLines.Add(
                    "      sceneObjects.Add(new DimensionSceneObject(new int2(" +
                    centre.x.ToString(CultureInfo.InvariantCulture) + ", " +
                    centre.y.ToString(CultureInfo.InvariantCulture) + "), " +
                    ToCSharpString(bossName) +
                    ", DimensionSceneFacing.Down, DimensionScenePaintChoice.Unpainted, \"\", null));");
            }
        }

        private static bool SceneAlreadyPlaces(SceneTemplateAsset scene, string objectId)
        {
            DimensionSceneObjectTemplate[] placed = scene.SceneObjects;
            for (int i = 0; i < placed.Length; i++)
            {
                if (placed[i] != null && placed[i].Enabled &&
                    string.Equals(placed[i].ObjectId, objectId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The middle of a scene's painted ground — the same floor((min+max)/2) the scene's own
        /// pivot uses, so a boss lands exactly where the room centres itself.
        /// </summary>
        private static Vector2Int ResolveSceneCentre(SceneTemplateAsset scene)
        {
            DimensionSceneTileTemplate[] tiles = scene.Tiles;
            bool any = false;
            int minX = 0;
            int minY = 0;
            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null || !tiles[i].Enabled)
                {
                    continue;
                }

                Vector2Int p = tiles[i].LocalPosition;
                if (!any)
                {
                    minX = maxX = p.x;
                    minY = maxY = p.y;
                    any = true;
                    continue;
                }

                if (p.x < minX) { minX = p.x; }
                if (p.x > maxX) { maxX = p.x; }
                if (p.y < minY) { minY = p.y; }
                if (p.y > maxY) { maxY = p.y; }
            }

            if (!any)
            {
                return Vector2Int.zero;
            }

            return new Vector2Int((minX + maxX) >> 1, (minY + maxY) >> 1);
        }

        /// <summary>
        /// The extra ctor arguments that let vanilla Overworld generation grow this scene, or an
        /// empty string when the scene has not opted in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Biome names resolve against the game's own <c>Biome</c> enum here, at build time,
        /// because the game's availability filter is an exact-match scan with no wildcard: a name
        /// that is not a vanilla biome would compile fine and then never match anything. That is
        /// also why custom biome ids are refused — the Overworld's sampler only ever produces
        /// vanilla values, so a custom id here would be dead weight dressed as configuration.
        /// </para>
        /// <para>
        /// A scene that opts in but resolves zero biomes keeps <c>maxOccurrences</c> 0, which is
        /// the game's own "invisible to natural spawn" — the safe state, loudly explained.
        /// </para>
        /// </remarks>
        private static string BuildOverworldSpawnArguments(SceneTemplateAsset scene)
        {
            if (!scene.SpawnInOverworld)
            {
                return string.Empty;
            }

            return BuildOverworldSpawnArguments(
                "Scene '" + scene.SceneId + "'",
                scene.OverworldBiomeNames,
                scene.OverworldMaxOccurrences,
                scene.MinDistanceFromCore);
        }

        /// <summary>
        /// The shared body of the above, so a portal that grows in the world takes exactly the same
        /// road a scene does — including the same refusal of biome names the Overworld never samples.
        /// </summary>
        private static string BuildOverworldSpawnArguments(
            string subject,
            string[] names,
            int maxOccurrences,
            int minDistanceFromCore)
        {
            List<string> resolved = new List<string>();
            for (int i = 0; names != null && i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }

                Biome biome;
                if (!System.Enum.TryParse(names[i], false, out biome) || biome == Biome.None)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] " + subject + " wants to grow in " +
                        "Overworld biome '" + names[i] + "', which is not a vanilla biome name. " +
                        "The entry is dropped — the Overworld only ever samples vanilla biomes, " +
                        "so it could never match.");
                    continue;
                }

                string literal = "Biome." + biome;
                if (!resolved.Contains(literal))
                {
                    resolved.Add(literal);
                }
            }

            if (resolved.Count == 0)
            {
                Debug.LogWarning(
                    "[ExpandNullforge] " + subject + " opted into Overworld " +
                    "spawning but names no valid vanilla biome, so it will not grow naturally.");
                return string.Empty;
            }

            StringBuilder args = new StringBuilder();
            args.Append(", maxOccurrences: ")
                .Append((maxOccurrences < 1 ? 1 : maxOccurrences).ToString(CultureInfo.InvariantCulture));
            args.Append(", overworldBiomes: new Biome[] { ");
            for (int i = 0; i < resolved.Count; i++)
            {
                if (i > 0)
                {
                    args.Append(", ");
                }

                args.Append(resolved[i]);
            }

            args.Append(" }");
            args.Append(", minDistanceFromCoreInClassicWorlds: ")
                .Append((minDistanceFromCore < 0 ? 0 : minDistanceFromCore)
                    .ToString(CultureInfo.InvariantCulture));
            return args.ToString();
        }

        /// <summary>
        /// Emits the scene's placement policy into the runtime pool, so the placement pass can
        /// honor what the Studio authored: mode, radial band, biome filters, weight, unique,
        /// required. Uses the raw scene id so the entry lines up with the compiled scene record.
        /// </summary>
        private static void AppendScenePoolRegistration(
            StringBuilder builder,
            DimensionTemplateAsset template,
            SceneTemplateAsset scene,
            string sceneName,
            Dictionary<SceneTemplateAsset, List<string>> owningBiomes)
        {
            List<string> owners;
            owningBiomes.TryGetValue(scene, out owners);
            string owningBiome = owners != null && owners.Count > 0 ? owners[0] : string.Empty;

            // Extra owners plus the authored filter merge into one allow list; the pass treats
            // any match as permission.
            List<string> allowed = new List<string>();
            if (owners != null)
            {
                for (int i = 1; i < owners.Count; i++)
                {
                    if (!allowed.Contains(owners[i]))
                    {
                        allowed.Add(owners[i]);
                    }
                }
            }

            string[] authoredAllowed = scene.AllowedBiomeIds;
            for (int i = 0; i < authoredAllowed.Length; i++)
            {
                if (!string.IsNullOrEmpty(authoredAllowed[i]) && !allowed.Contains(authoredAllowed[i]))
                {
                    allowed.Add(authoredAllowed[i]);
                }
            }

            StringBuilder allowedList = new StringBuilder("new string[] { ");
            for (int i = 0; i < allowed.Count; i++)
            {
                if (i > 0)
                {
                    allowedList.Append(", ");
                }

                allowedList.Append(ToCSharpString(allowed[i]));
            }

            allowedList.Append(" }");

            DimensionBounds preferred = scene.PreferredLocalBounds;
            bool hasPreferred = scene.PlacementMode == DimensionScenePlacementMode.PreferredBounds;

            builder.Append("        DimensionScenePoolRegistry.Register(")
                .Append(ToCSharpString(template.DimensionId))
                .AppendLine(", new DimensionScenePoolEntry(");
            builder.Append("            ").Append(ToCSharpString(sceneName)).AppendLine(",");
            builder.Append("            ").Append(ToCSharpString(scene.SceneId)).AppendLine(",");
            builder.Append("            ").Append(ToCSharpString(owningBiome)).AppendLine(",");
            builder.Append("            ").Append(allowedList.ToString()).AppendLine(",");
            builder.Append("            DimensionScenePlacementMode.")
                .Append(scene.PlacementMode.ToString()).AppendLine(",");
            builder.Append("            new int2(")
                .Append(scene.ExactLocalPosition.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(scene.ExactLocalPosition.y.ToString(CultureInfo.InvariantCulture)).AppendLine("),");
            builder.Append("            new DimensionBounds(new int2(")
                .Append(preferred.Min.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(preferred.Min.y.ToString(CultureInfo.InvariantCulture)).Append("), new int2(")
                .Append(preferred.MaxExclusive.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(preferred.MaxExclusive.y.ToString(CultureInfo.InvariantCulture)).AppendLine(")),");
            builder.Append("            ").Append(hasPreferred ? "true" : "false").AppendLine(",");
            builder.Append("            ")
                .Append(scene.MinRadiusTiles.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("            ")
                .Append(scene.MaxRadiusTiles.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("            ").Append(ToCSharpString(scene.RadialBiomeId)).AppendLine(",");
            builder.Append("            new int2(")
                .Append(scene.FootprintSize.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(scene.FootprintSize.y.ToString(CultureInfo.InvariantCulture)).AppendLine("),");
            builder.Append("            ")
                .Append(scene.Weight.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("            ").Append(scene.Unique ? "true" : "false").AppendLine(",");
            builder.Append("            ").Append(scene.Required ? "true" : "false").AppendLine(",");
            builder.Append("            ")
                .Append(scene.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine("));");
        }

        /// <summary>
        /// Which biomes carry each scene in their pool. The flattening in
        /// <see cref="CollectSceneTemplates"/> deliberately loses this — registration wants each
        /// scene once — but the placement policy needs to remember whose pool it came from.
        /// </summary>
        private static Dictionary<SceneTemplateAsset, List<string>> CollectSceneOwners(
            DimensionTemplateAsset template)
        {
            Dictionary<SceneTemplateAsset, List<string>> owners =
                new Dictionary<SceneTemplateAsset, List<string>>();
            if (template == null)
            {
                return owners;
            }

            BiomeTemplateAsset[] biomes = template.Biomes;
            if (biomes == null)
            {
                return owners;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || string.IsNullOrEmpty(biome.BiomeId))
                {
                    continue;
                }

                SceneTemplateAsset[] pool = biome.ScenePool;
                if (pool == null)
                {
                    continue;
                }

                for (int s = 0; s < pool.Length; s++)
                {
                    if (pool[s] == null)
                    {
                        continue;
                    }

                    List<string> list;
                    if (!owners.TryGetValue(pool[s], out list))
                    {
                        list = new List<string>();
                        owners[pool[s]] = list;
                    }

                    if (!list.Contains(biome.BiomeId))
                    {
                        list.Add(biome.BiomeId);
                    }
                }
            }

            return owners;
        }

        private static List<SceneTemplateAsset> CollectSceneTemplates(DimensionTemplateAsset template)
        {
            List<SceneTemplateAsset> scenes = new List<SceneTemplateAsset>();
            if (template == null)
            {
                return scenes;
            }

            AddScenes(template.GlobalScenes, scenes);

            BiomeTemplateAsset[] biomes = template.Biomes;
            if (biomes != null)
            {
                for (int i = 0; i < biomes.Length; i++)
                {
                    if (biomes[i] != null)
                    {
                        AddScenes(biomes[i].ScenePool, scenes);
                    }
                }
            }

            return scenes;
        }

        private static void AddScenes(SceneTemplateAsset[] source, List<SceneTemplateAsset> destination)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                // A scene shared between biomes must still be registered exactly once: the registry
                // refuses a duplicate name, so emitting it twice would log a warning about the mod's
                // own content.
                if (source[i] != null && !destination.Contains(source[i]))
                {
                    destination.Add(source[i]);
                }
            }
        }

        private static bool IsPlacedPortalCraftable(DimensionTemplateAsset template)
        {
            DimensionPortalAccessRuleAsset[] rules = template == null
                ? null
                : template.PortalAccessRules;
            if (rules == null || rules.Length == 0)
            {
                return true;
            }

            bool anyPlacedRule = false;
            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !DimensionPortalVersions.IsUserAccessible(rule.AccessKind))
                {
                    continue;
                }

                anyPlacedRule = true;
                if (rule.Enabled && rule.Craftable)
                {
                    return true;
                }
            }

            return !anyPlacedRule;
        }

        /// <summary>
        /// The rule whose settings decide how the placed portal is crafted, or null when the
        /// dimension has no placed rule at all and keeps the legacy always-craftable default.
        /// </summary>
        /// <remarks>
        /// Deliberately the same walk as <see cref="IsPlacedPortalCraftable"/>: the rule that made
        /// the portal craftable is the rule that gets to name its bench. Reading the toggle from
        /// one rule and the bench from another would put the recipe somewhere nobody asked for.
        /// </remarks>
        private static DimensionPortalAccessRuleAsset FindCraftablePlacedRule(
            DimensionTemplateAsset template)
        {
            DimensionPortalAccessRuleAsset[] rules = template == null
                ? null
                : template.PortalAccessRules;
            if (rules == null)
            {
                return null;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !DimensionPortalVersions.IsUserAccessible(rule.AccessKind))
                {
                    continue;
                }

                if (rule.Enabled && rule.Craftable)
                {
                    return rule;
                }
            }

            return null;
        }

        private static string ResolveCraftingStationArgument(
            DimensionPortalAccessRuleAsset rule,
            string what)
        {
            return ResolveCraftingStationArgument(
                rule == null ? string.Empty : rule.CraftingStationObjectId,
                what);
        }

        /// <summary>
        /// The crafting-station argument for a generated recipe: a number when the framework can
        /// prove one, otherwise the station's name for the runtime to resolve.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three cases, and they exist because a station is not always one of the game's. A blank
        /// setting means the Wooden Workbench, which is what the field has always promised. A name
        /// the game's own object list knows becomes that number here, at build time, where it can
        /// be proven. Anything else is taken to be another mod's bench and is emitted as a NAME:
        /// another mod's objects have no number until the game hands them one during world
        /// conversion, so resolving it here would produce nothing and silently fall back.
        /// </para>
        /// <para>
        /// A name that never resolves leaves the recipe parked instead of dropping it onto the
        /// Wooden Workbench, which is why the warning below names the typo case out loud: a recipe
        /// that quietly appeared at the wrong bench is far harder to notice than one that has not
        /// appeared at all.
        /// </para>
        /// </remarks>
        private static string ResolveCraftingStationArgument(string stationObjectId, string what)
        {
            string station = (stationObjectId ?? string.Empty).Trim();
            if (station.Length == 0)
            {
                return "ObjectID.WoodenWorkBench";
            }

            ObjectID vanillaStation;
            if (System.Enum.TryParse(station, false, out vanillaStation) &&
                vanillaStation != ObjectID.None)
            {
                return "ObjectID." + vanillaStation;
            }

            Debug.LogWarning(
                "[ExpandNullforge] The crafting station for " + what + " is '" + station +
                "', which is not one of the game's own objects. It is taken to be a workbench from " +
                "another mod and looked up by that name while the world loads. If that mod is not " +
                "installed, or the name is misspelled, the recipe never appears — clear the field " +
                "to use the Wooden Workbench, or type the station's exact object name.");
            return ToCSharpString(station);
        }

        private static DimensionRuntimePortalOutput ResolvePortalOutput(
            DimensionTemplateAsset template,
            DimensionTemplateManifestExportPreview preview,
            string modDisplayName,
            string dimensionId)
        {
            string dimensionDisplayName = string.IsNullOrEmpty(template.DisplayName)
                ? dimensionId
                : template.DisplayName;
            string legacyDimensionPortalDisplayName = dimensionDisplayName + " Portal";
            string portalDisplayName = BuildDefaultPortalDisplayName(modDisplayName, dimensionDisplayName);
            string portalObjectName =
                SanitizeIdentifier(modDisplayName, "DimensionMod") +
                "_" +
                SanitizeIdentifier(dimensionId, "Dimension") +
                "_Portal";
            string returnPortalObjectName = portalObjectName + "_Return";
            // "_Instant" (not "_Item") so the spawned-portal OBJECT can never collide with the
            // modder-named portal ITEM id from the V2 access rule.
            string itemPortalObjectName = portalObjectName + "_Instant";
            string assetStem =
                SanitizeIdentifier(dimensionDisplayName, "Dimension") +
                "Runtime";

            // Invariant: at least one player-facing entry version (placed portal V1 or item portal
            // V2) must stay enabled so the dimension is always reachable. If the modder disabled
            // both, re-enable the placed portal before anything reads the rules.
            if (template != null)
            {
                DimensionPortalVersions.EnsureAtLeastOneEntryEnabled(template.PortalAccessRules);
            }

            DimensionPortalDefinition entryPortal;
            DimensionPortalPresentationDefinition entryPresentation;
            ResolveEntryPortal(template, dimensionId, portalDisplayName, out entryPortal, out entryPresentation);

            portalDisplayName = ResolvePortalDisplayName(
                portalDisplayName,
                legacyDimensionPortalDisplayName,
                entryPortal,
                entryPresentation);

            DimensionPortalDefinition returnPortal;
            DimensionPortalPresentationDefinition returnPresentation;
            ResolveReturnPortal(template, dimensionId, portalDisplayName, out returnPortal, out returnPresentation);

            DimensionBounds requiredBounds = ResolveReturnRequiredBounds(returnPortal.FromLocalPosition);
            DimensionBounds starterTargetLandingBounds =
                ResolveReturnRequiredBounds(entryPortal.ToLocalPosition);

            Color mapColor = new Color(0.35f, 0.55f, 0.75f, 1.0f);
            if (preview.ManifestBuilt)
            {
                mapColor = ResolveMapColor(template, mapColor);
            }

            DimensionContentPackDefinition contentPack =
                ResolveRuntimeContentPackDefinition(template, dimensionId, dimensionDisplayName);
            DimensionDefinition dimension =
                ResolveRuntimeDimensionDefinition(template, preview, dimensionId, dimensionDisplayName);
            DimensionBounds starterGenerationBounds =
                ResolveStarterGenerationBounds(preview, dimension, entryPortal.ToLocalPosition);
            IReadOnlyList<DimensionZoneDefinition> minimumZones =
                preview.ManifestBuilt && preview.Manifest.Zones != null
                    ? preview.Manifest.Zones
                    : new List<DimensionZoneDefinition>();
            IReadOnlyList<DimensionGenerationPassDefinition> minimumGenerationPasses =
                preview.ManifestBuilt &&
                    preview.CompiledPlan.GenerationPasses != null
                    ? preview.CompiledPlan.GenerationPasses
                    : new List<DimensionGenerationPassDefinition>();

            StorePortalDrops(template, dimensionId, portalObjectName, itemPortalObjectName);

            return new DimensionRuntimePortalOutput(
                dimensionId,
                dimensionDisplayName,
                portalObjectName,
                returnPortalObjectName,
                itemPortalObjectName,
                portalDisplayName,
                assetStem,
                contentPack,
                dimension,
                entryPortal,
                entryPresentation,
                returnPortal,
                returnPresentation,
                requiredBounds,
                dimensionId + ".starter",
                starterGenerationBounds,
                starterTargetLandingBounds,
                minimumZones,
                minimumGenerationPasses,
                mapColor,
                2.0f,
                template.PortalActivationChargeSeconds,
                template.PortalVisualProfile,
                template.ItemPortalVisualProfile,
                template.ItemPortalVisualProfile != null);
        }

        private static string BuildDefaultPortalDisplayName(
            string modDisplayName,
            string dimensionDisplayName)
        {
            string displayBase = string.IsNullOrEmpty(modDisplayName)
                ? dimensionDisplayName
                : modDisplayName;
            if (string.IsNullOrEmpty(displayBase))
            {
                displayBase = "Dimension";
            }

            return displayBase + " Portal";
        }

        private static string ResolvePortalDisplayName(
            string generatedDefault,
            string legacyDimensionDefault,
            DimensionPortalDefinition entryPortal,
            DimensionPortalPresentationDefinition entryPresentation)
        {
            string candidate = entryPresentation.DisplayName;
            if (string.IsNullOrEmpty(candidate))
            {
                candidate = entryPortal.DisplayName;
            }

            if (string.IsNullOrEmpty(candidate) ||
                string.Equals(candidate, legacyDimensionDefault, System.StringComparison.Ordinal))
            {
                return generatedDefault;
            }

            return candidate;
        }

        private static void ResolveEntryPortal(
            DimensionTemplateAsset template,
            string dimensionId,
            string portalDisplayName,
            out DimensionPortalDefinition portal,
            out DimensionPortalPresentationDefinition presentation)
        {
            DimensionPortalAccessRuleAsset rule =
                FindPortalRule(template, DimensionPortalAccessKind.PlacedPortal, dimensionId, false);
            if (rule != null)
            {
                portal = EnsurePortalDefinition(
                    rule.ToPortalDefinition(),
                    dimensionId + ".portal.entry",
                    portalDisplayName,
                    DimensionIds.Overworld,
                    float2.zero,
                    dimensionId,
                    float2.zero);
                presentation = EnsurePortalPresentation(
                    rule.ToPortalPresentationDefinition(),
                    portal.PortalId + ".presentation",
                    portal.PortalId,
                    portal.DisplayName,
                    "Enter " + portal.DisplayName,
                    "The portal is not active yet.");
                return;
            }

            portal = new DimensionPortalDefinition(
                dimensionId + ".portal.entry",
                portalDisplayName,
                DimensionIds.Overworld,
                float2.zero,
                dimensionId,
                float2.zero,
                DimensionPortalState.Available);
            presentation = new DimensionPortalPresentationDefinition(
                portal.PortalId + ".presentation",
                portal.PortalId,
                portal.DisplayName,
                "Enter " + portal.DisplayName,
                "The portal is not active yet.",
                string.Empty,
                string.Empty,
                string.Empty,
                2.0f,
                0,
                true,
                true,
                true);
        }

        private static void ResolveReturnPortal(
            DimensionTemplateAsset template,
            string dimensionId,
            string portalDisplayName,
            out DimensionPortalDefinition portal,
            out DimensionPortalPresentationDefinition presentation)
        {
            DimensionPortalAccessRuleAsset rule =
                FindPortalRule(template, DimensionPortalAccessKind.GeneratedReturnPortal, dimensionId, true);
            if (rule != null)
            {
                portal = EnsurePortalDefinition(
                    rule.ToPortalDefinition(),
                    dimensionId + ".portal.return",
                    portalDisplayName + " Return",
                    dimensionId,
                    float2.zero,
                    DimensionIds.Overworld,
                    float2.zero);
                presentation = EnsurePortalPresentation(
                    rule.ToPortalPresentationDefinition(),
                    portal.PortalId + ".presentation",
                    portal.PortalId,
                    portal.DisplayName,
                    "Return to the core.",
                    "The return portal is not active yet.");
                return;
            }

            portal = new DimensionPortalDefinition(
                dimensionId + ".portal.return",
                portalDisplayName + " Return",
                dimensionId,
                float2.zero,
                DimensionIds.Overworld,
                float2.zero,
                DimensionPortalState.Available);
            presentation = new DimensionPortalPresentationDefinition(
                portal.PortalId + ".presentation",
                portal.PortalId,
                portal.DisplayName,
                "Return to the core.",
                "The return portal is not active yet.",
                string.Empty,
                string.Empty,
                string.Empty,
                0.0f,
                0,
                true,
                false,
                true);
        }

        private struct PortalDropEmission
        {
            public string Target;
            public string Item;
            public float Weight;
            public float Chance;
            public int Min;
            public int Max;
        }

        private static readonly Dictionary<string, List<PortalDropEmission>> pendingPortalDrops =
            new Dictionary<string, List<PortalDropEmission>>();

        private struct PortalItemEmission
        {
            public string Item;
            public string PortalObject;
            public string PortalId;
            public string ToDimension;
            public float Duration;
            public bool Craftable;

            /// <summary>The bench this item is made at, exactly as the rule spells it. Blank means the Wooden Workbench.</summary>
            public string Station;
            public string DisplayName;
        }

        private static readonly Dictionary<string, List<PortalItemEmission>> pendingItemPortals =
            new Dictionary<string, List<PortalItemEmission>>();

        /// <summary>
        /// Resolves every droppable portal version's mob/boss targets into a flat emission list keyed
        /// by dimension id, consumed by the bootstrap emitter. The dropped item is the placed portal
        /// object for V1/generated versions and the portal item id for the V2 item version.
        /// </summary>
        private static void StorePortalDrops(
            DimensionTemplateAsset template,
            string dimensionId,
            string portalObjectName,
            string itemPortalObjectName)
        {
            List<PortalDropEmission> drops = new List<PortalDropEmission>();
            DimensionPortalAccessRuleAsset[] rules = template == null ? null : template.PortalAccessRules;
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    DimensionPortalAccessRuleAsset rule = rules[i];
                    if (rule == null || !rule.Enabled || !rule.Droppable)
                    {
                        continue;
                    }

                    string itemName = rule.AccessKind == DimensionPortalAccessKind.InventoryItem
                        ? rule.PortalItemObjectId
                        : portalObjectName;
                    if (string.IsNullOrEmpty(itemName))
                    {
                        continue;
                    }

                    DimensionPortalDropTarget[] targets = rule.DropTargets;
                    for (int t = 0; t < targets.Length; t++)
                    {
                        DimensionPortalDropTarget target = targets[t];
                        if (string.IsNullOrEmpty(target.TargetObjectId))
                        {
                            continue;
                        }

                        drops.Add(new PortalDropEmission
                        {
                            Target = target.TargetObjectId,
                            Item = itemName,
                            Weight = target.Weight,
                            Chance = target.ChancePercent,
                            Min = target.MinAmount,
                            Max = target.MaxAmount
                        });
                    }
                }
            }

            pendingPortalDrops[dimensionId ?? string.Empty] = drops;

            List<PortalItemEmission> itemPortals = new List<PortalItemEmission>();
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    DimensionPortalAccessRuleAsset rule = rules[i];
                    if (rule == null || !rule.Enabled ||
                        rule.AccessKind != DimensionPortalAccessKind.InventoryItem ||
                        string.IsNullOrEmpty(rule.PortalItemObjectId))
                    {
                        continue;
                    }

                    itemPortals.Add(new PortalItemEmission
                    {
                        Item = rule.PortalItemObjectId,
                        // The dedicated frameless instant-portal object, not the shared placed-portal
                        // object — this is what DimensionItemPortalSpawnSystem spawns.
                        PortalObject = itemPortalObjectName,
                        PortalId = rule.PortalId,
                        ToDimension = rule.ToDimensionId,
                        Duration = rule.ItemPortalDurationSeconds,
                        Craftable = rule.Craftable,
                        Station = rule.CraftingStationObjectId,
                        DisplayName = rule.DisplayName
                    });
                }
            }

            pendingItemPortals[dimensionId ?? string.Empty] = itemPortals;
        }

        /// <summary>
        /// The first enabled rule of a kind, which is the only one this generator ever uses.
        /// </summary>
        /// <remarks>
        /// The loop itself lives in <see cref="DimensionPortalRuleOwnership"/> because the Portal
        /// Studio has to name the same owner and warn about the same ignored rules. Two copies of
        /// a first-match rule is exactly the kind of thing that drifts apart and leaves the studio
        /// editing a rule the game never reads.
        /// </remarks>
        private static DimensionPortalAccessRuleAsset FindPortalRule(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind accessKind,
            string dimensionId,
            bool matchFromDimension)
        {
            return DimensionPortalRuleOwnership.Find(
                template == null ? null : template.PortalAccessRules,
                accessKind,
                dimensionId,
                matchFromDimension);
        }

        private static DimensionPortalDefinition EnsurePortalDefinition(
            DimensionPortalDefinition portal,
            string fallbackPortalId,
            string fallbackDisplayName,
            string fallbackFromDimensionId,
            float2 fallbackFromLocalPosition,
            string fallbackToDimensionId,
            float2 fallbackToLocalPosition)
        {
            return new DimensionPortalDefinition(
                string.IsNullOrEmpty(portal.PortalId) ? fallbackPortalId : portal.PortalId,
                string.IsNullOrEmpty(portal.DisplayName) ? fallbackDisplayName : portal.DisplayName,
                string.IsNullOrEmpty(portal.FromDimensionId) ? fallbackFromDimensionId : portal.FromDimensionId,
                portal.FromLocalPosition,
                string.IsNullOrEmpty(portal.ToDimensionId) ? fallbackToDimensionId : portal.ToDimensionId,
                portal.ToLocalPosition,
                portal.State == DimensionPortalState.Unknown ? DimensionPortalState.Available : portal.State);
        }

        private static DimensionPortalPresentationDefinition EnsurePortalPresentation(
            DimensionPortalPresentationDefinition presentation,
            string fallbackPresentationId,
            string fallbackPortalId,
            string fallbackDisplayName,
            string fallbackPrompt,
            string fallbackLockedPrompt)
        {
            return new DimensionPortalPresentationDefinition(
                string.IsNullOrEmpty(presentation.PresentationId)
                    ? fallbackPresentationId
                    : presentation.PresentationId,
                string.IsNullOrEmpty(presentation.PortalId)
                    ? fallbackPortalId
                    : presentation.PortalId,
                string.IsNullOrEmpty(presentation.DisplayName)
                    ? fallbackDisplayName
                    : presentation.DisplayName,
                string.IsNullOrEmpty(presentation.PromptText)
                    ? fallbackPrompt
                    : presentation.PromptText,
                string.IsNullOrEmpty(presentation.LockedPromptText)
                    ? fallbackLockedPrompt
                    : presentation.LockedPromptText,
                presentation.IconId,
                presentation.VisualEffectId,
                presentation.AudioCueId,
                presentation.CooldownSeconds,
                presentation.Priority,
                presentation.Enabled,
                presentation.RequireGeneratedAreaOnUse,
                presentation.AllowFallbackPositionOnUse,
                presentation.Interactable);
        }

        private static DimensionBounds ResolveReturnRequiredBounds(float2 returnPortalLocalPosition)
        {
            int minX = Mathf.FloorToInt(returnPortalLocalPosition.x) - TravelPreloadSideTiles / 2;
            int minY = Mathf.FloorToInt(returnPortalLocalPosition.y) - TravelPreloadSideTiles / 2;
            return new DimensionBounds(
                new int2(minX, minY),
                new int2(minX + TravelPreloadSideTiles, minY + TravelPreloadSideTiles));
        }

        private static DimensionBounds ResolveStarterGenerationBounds(
            DimensionTemplateManifestExportPreview preview,
            DimensionDefinition dimension,
            float2 targetLocalPosition)
        {
            if (preview.ManifestBuilt && IsValidBounds(preview.PlayableLocalBounds))
            {
                return preview.PlayableLocalBounds;
            }

            if (IsValidBounds(dimension.LocalBounds) &&
                dimension.LocalBounds.Contains(targetLocalPosition))
            {
                return dimension.LocalBounds;
            }

            return ResolveReturnRequiredBounds(targetLocalPosition);
        }

        private static bool IsValidBounds(DimensionBounds bounds)
        {
            return bounds.MaxExclusive.x > bounds.Min.x &&
                   bounds.MaxExclusive.y > bounds.Min.y;
        }

        private static DimensionBounds UnionBounds(DimensionBounds bounds, DimensionBounds other)
        {
            if (!IsValidBounds(other))
            {
                return bounds;
            }

            if (!IsValidBounds(bounds))
            {
                return other;
            }

            return new DimensionBounds(
                new int2(
                    math.min(bounds.Min.x, other.Min.x),
                    math.min(bounds.Min.y, other.Min.y)),
                new int2(
                    math.max(bounds.MaxExclusive.x, other.MaxExclusive.x),
                    math.max(bounds.MaxExclusive.y, other.MaxExclusive.y)));
        }

        private static Color ResolveMapColor(
            DimensionTemplateAsset template,
            Color fallback)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes != null && biomes.Length > 0 && biomes[0] != null)
            {
                SerializedObject serialized = new SerializedObject(biomes[0]);
                SerializedProperty mapColor = serialized.FindProperty("mapColor");
                if (mapColor != null)
                {
                    return mapColor.colorValue;
                }
            }

            return fallback;
        }

        private static DimensionContentPackDefinition ResolveRuntimeContentPackDefinition(
            DimensionTemplateAsset template,
            string dimensionId,
            string dimensionDisplayName)
        {
            string contentPackId = template == null ? string.Empty : template.ContentPackId;
            if (string.IsNullOrEmpty(contentPackId))
            {
                contentPackId = dimensionId + ".pack";
            }

            string displayName = template == null ? string.Empty : template.ContentPackDisplayName;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = dimensionDisplayName + " Pack";
            }

            string version = template == null ? string.Empty : template.ContentPackVersion;
            if (string.IsNullOrEmpty(version))
            {
                version = "1.0.0";
            }

            return new DimensionContentPackDefinition(
                contentPackId,
                displayName,
                version,
                template == null ? string.Empty : template.ContentPackAuthor,
                template == null ? string.Empty : template.Description,
                template == null ? DimensionApi.CurrentApiVersion : Mathf.Max(1, template.MinimumApiVersion),
                template == null ? new List<string>() : template.DependencyContentPackIds,
                true);
        }

        private static DimensionDefinition ResolveRuntimeDimensionDefinition(
            DimensionTemplateAsset template,
            DimensionTemplateManifestExportPreview preview,
            string dimensionId,
            string dimensionDisplayName)
        {
            DimensionDefinition dimension = template == null
                ? new DimensionDefinition(
                    dimensionId,
                    dimensionDisplayName,
                    new int2(10000, 10000),
                    new DimensionBounds(new int2(-640, -640), new int2(640, 640)),
                    1,
                    DimensionType.World,
                    DimensionTemplateStarterFactory.DefaultCapabilities,
                    DimensionLifecycleState.Registered)
                : template.ToDimensionDefinition();

            if (preview.ManifestBuilt && preview.Manifest.Dimensions != null)
            {
                for (int i = 0; i < preview.Manifest.Dimensions.Count; i++)
                {
                    DimensionDefinition candidate = preview.Manifest.Dimensions[i];
                    if (string.Equals(candidate.Id, dimensionId, System.StringComparison.Ordinal))
                    {
                        dimension = candidate;
                        break;
                    }
                }
            }

            DimensionCapabilityFlags capabilities = dimension.Capabilities;
            if ((capabilities & DimensionCapabilityFlags.PlayerTravel) == DimensionCapabilityFlags.PlayerTravel)
            {
                capabilities |= DimensionCapabilityFlags.AreaLoading;
                capabilities |= DimensionCapabilityFlags.SimulationLoading;
            }

            return new DimensionDefinition(
                string.IsNullOrEmpty(dimension.Id) ? dimensionId : dimension.Id,
                string.IsNullOrEmpty(dimension.DisplayName) ? dimensionDisplayName : dimension.DisplayName,
                dimension.AbsoluteOrigin,
                dimension.LocalBounds,
                dimension.GenerationVersion <= 0 ? 1 : dimension.GenerationVersion,
                dimension.Type,
                capabilities,
                dimension.LifecycleState == DimensionLifecycleState.Unknown
                    ? DimensionLifecycleState.Registered
                    : dimension.LifecycleState);
        }

        private static void TryAddGhostAuthoringComponent(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (root.GetComponent<GhostAuthoringComponent>() != null)
            {
                return;
            }

            root.AddComponent<GhostAuthoringComponent>();
        }

        private static void EnsureConsumerAssemblyReferences(string modRoot)
        {
            string absoluteRoot = AssetPathToAbsolutePath(modRoot);
            if (string.IsNullOrEmpty(absoluteRoot) || !Directory.Exists(absoluteRoot))
            {
                return;
            }

            string[] asmdefs = Directory.GetFiles(absoluteRoot, "*.asmdef", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < asmdefs.Length; i++)
            {
                string asmdefPath = AbsolutePathToAssetPath(asmdefs[i]);
                if (string.IsNullOrEmpty(asmdefPath))
                {
                    continue;
                }

                EnsureAssemblyReferences(asmdefPath);
            }
        }

        private static void EnsureAssemblyReferences(string asmdefPath)
        {
            string absolutePath = AssetPathToAbsolutePath(asmdefPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                return;
            }

            string text = File.ReadAllText(absolutePath);
            bool useGuids = text.Contains("\"useGUIDs\": true");
            string frameworkReference = useGuids
                ? AssetDatabase.AssetPathToGUID("Assets/ExpandNullforge/ExpandNullforge.asmdef")
                : FrameworkAssemblyReference;
            string apiReference = useGuids
                ? AssetDatabase.AssetPathToGUID("Assets/ExpandNullforge/API/ExpandNullforge.API.asmdef")
                : ApiAssemblyReference;
            if (EnsureJsonStringArrayContains(
                absolutePath,
                "references",
                new[] { frameworkReference, apiReference }))
            {
                AssetDatabase.ImportAsset(asmdefPath);
            }
        }

        /// <summary>
        /// Sets the two switches on the creator's mod that decide whether it runs at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both are off on a new mod, and neither shows a problem while you are building. The first
        /// one, "accesses extra assemblies", decides which assemblies the game lets the mod's code
        /// see when it compiles the mod ON LOAD. Everything this framework generates is written
        /// against the framework's own namespaces, so without it the generated script can fail to
        /// compile inside the game while it compiles perfectly here.
        /// </para>
        /// <para>
        /// The second one says the mod has to be present on both sides of a multiplayer game. A
        /// dimension is made of objects, blocks and systems that the server spawns and the client
        /// draws, so a player without the mod cannot join a server that has it. Left off, the game
        /// refuses the join with "BadProtocolVersion" and never names the mod, which is a long
        /// afternoon for whoever is trying to work out why their friend cannot connect.
        /// </para>
        /// <para>
        /// Both are set every generate rather than only when absent: they are ordinary tick boxes in
        /// the mod's own window, and a creator who turns one off gets a mod that does not load or
        /// cannot be joined, with nothing anywhere saying why. The report says what was changed.
        /// </para>
        /// </remarks>
        private static void EnsureTheModCanLoadAndBeJoined(string templatePath)
        {
            ModBuilderSettings settings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(templatePath);
            if (settings == null)
            {
                return;
            }

            bool changed = false;

            if (!settings.metadata.accessesExtraAssemblies)
            {
                settings.metadata.accessesExtraAssemblies = true;
                changed = true;
                Debug.Log(
                    "[ExpandNullforge] Turned on \"accesses extra assemblies\" for '" +
                    settings.metadata.name + "'. Without it the game cannot compile the generated " +
                    "script when the mod loads, even though it builds here.");
            }

            if (settings.metadata.requiredOn != ModMetadata.ModExistsOn.ClientAndServer)
            {
                settings.metadata.requiredOn = ModMetadata.ModExistsOn.ClientAndServer;
                changed = true;
                Debug.Log(
                    "[ExpandNullforge] Marked '" + settings.metadata.name + "' as needed on both " +
                    "the client and the server. A dimension is spawned by the server and drawn by " +
                    "the client, so a player without the mod cannot join a server that has it.");
            }

            if (changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }

        private static void EnsureFrameworkModDependency(string templatePath)
        {
            ModBuilderSettings settings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(templatePath);
            if (settings == null)
            {
                return;
            }

            if (settings.metadata.dependencies == null)
            {
                settings.metadata.dependencies = new List<ModMetadata.Dependency>();
            }

            for (int i = 0; i < settings.metadata.dependencies.Count; i++)
            {
                ModMetadata.Dependency dependency = settings.metadata.dependencies[i];
                if (dependency.modName == FrameworkModName)
                {
                    if (!dependency.required)
                    {
                        dependency.required = true;
                        settings.metadata.dependencies[i] = dependency;
                        EditorUtility.SetDirty(settings);
                    }

                    return;
                }
            }

            settings.metadata.dependencies.Add(new ModMetadata.Dependency
            {
                modName = FrameworkModName,
                required = true
            });
            EditorUtility.SetDirty(settings);
        }

        private static bool EnsureJsonStringArrayContains(
            string absoluteJsonPath,
            string propertyName,
            IReadOnlyList<string> values)
        {
            if (string.IsNullOrEmpty(absoluteJsonPath) ||
                string.IsNullOrEmpty(propertyName) ||
                values == null ||
                values.Count == 0)
            {
                return false;
            }

            string text = File.ReadAllText(absoluteJsonPath);
            int nameIndex = text.IndexOf("\"" + propertyName + "\"", System.StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return false;
            }

            int bracketStart = text.IndexOf('[', nameIndex);
            if (bracketStart < 0)
            {
                return false;
            }

            int bracketEnd = FindMatchingArrayBracket(text, bracketStart);
            if (bracketEnd < 0)
            {
                return false;
            }

            string body = text.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            List<string> missing = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (!body.Contains(ToJsonString(value)))
                {
                    missing.Add(value);
                }
            }

            if (missing.Count == 0)
            {
                return false;
            }

            string itemIndent = ResolveArrayItemIndent(text, bracketStart);
            string closingIndent = ResolveClosingIndent(text, bracketEnd);
            StringBuilder insertion = new StringBuilder();
            string trimmedBody = body.Trim();
            if (trimmedBody.Length > 0)
            {
                int lastNonWhitespace = FindLastNonWhitespace(text, bracketEnd - 1, bracketStart + 1);
                if (lastNonWhitespace >= 0 && text[lastNonWhitespace] != ',')
                {
                    insertion.Append(",");
                }
            }

            for (int i = 0; i < missing.Count; i++)
            {
                insertion.AppendLine();
                insertion.Append(itemIndent);
                insertion.Append(ToJsonString(missing[i]));
                if (i < missing.Count - 1)
                {
                    insertion.Append(",");
                }
            }

            insertion.AppendLine();
            insertion.Append(closingIndent);

            string updated =
                text.Substring(0, bracketEnd) +
                insertion +
                text.Substring(bracketEnd);
            File.WriteAllText(absoluteJsonPath, updated, Encoding.UTF8);
            return true;
        }

        private static int FindMatchingArrayBracket(string text, int bracketStart)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = bracketStart; i < text.Length; i++)
            {
                char character = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == '[')
                {
                    depth++;
                }
                else if (character == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string ResolveArrayItemIndent(string text, int bracketStart)
        {
            int lineStart = text.LastIndexOf('\n', bracketStart);
            string baseIndent = lineStart < 0
                ? string.Empty
                : ReadIndent(text, lineStart + 1);
            return baseIndent + "    ";
        }

        private static string ResolveClosingIndent(string text, int bracketEnd)
        {
            int lineStart = text.LastIndexOf('\n', bracketEnd);
            return lineStart < 0 ? string.Empty : ReadIndent(text, lineStart + 1);
        }

        private static string ReadIndent(string text, int start)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = start; i < text.Length; i++)
            {
                char character = text[i];
                if (character != ' ' && character != '\t')
                {
                    break;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private static int FindLastNonWhitespace(string text, int start, int min)
        {
            for (int i = start; i >= min; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void WriteTextAssetIfChanged(string assetPath, string content)
        {
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath))
            {
                return;
            }

            string folder = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (File.Exists(absolutePath) &&
                File.ReadAllText(absolutePath) == content)
            {
                return;
            }

            File.WriteAllText(absolutePath, content, Encoding.UTF8);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static void WriteBinaryAssetIfChanged(string assetPath, byte[] content)
        {
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath) || content == null)
            {
                return;
            }

            string folder = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (File.Exists(absolutePath))
            {
                byte[] existing = File.ReadAllBytes(absolutePath);
                if (existing.Length == content.Length)
                {
                    bool equal = true;
                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (existing[i] != content[i])
                        {
                            equal = false;
                            break;
                        }
                    }

                    if (equal)
                    {
                        return;
                    }
                }
            }

            File.WriteAllBytes(absolutePath, content);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static void SetSerializedArraySize(Object target, string propertyName, int size)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.isArray)
            {
                property.arraySize = Mathf.Max(0, size);
                serialized.ApplyModifiedProperties();
            }
        }

        private static void AppendConstant(StringBuilder builder, string name, string value)
        {
            builder.Append("  private const string ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(ToCSharpString(value));
            builder.AppendLine(";");
        }

        private static void AppendFloatConstant(StringBuilder builder, string name, float value)
        {
            builder.Append("  private const float ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(FormatFloat(value));
            builder.AppendLine(";");
        }

        private static void AppendIntConstant(StringBuilder builder, string name, int value)
        {
            builder.Append("  private const int ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(";");
        }

        private static void AppendBoolConstant(StringBuilder builder, string name, bool value)
        {
            builder.Append("  private const bool ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(value ? "true" : "false");
            builder.AppendLine(";");
        }

        private static string ToCSharpString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return ToJsonString(value);
        }

        private static string ToJsonString(string value)
        {
            string source = value ?? string.Empty;
            return "\"" +
                source
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t") +
                "\"";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "f";
        }

        private static string SanitizeIdentifier(string value, string fallback)
        {
            string source = string.IsNullOrEmpty(value) ? fallback : value;
            StringBuilder builder = new StringBuilder();
            bool makeUpper = true;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9');
                if (!valid)
                {
                    makeUpper = true;
                    continue;
                }

                if (builder.Length == 0 && character >= '0' && character <= '9')
                {
                    builder.Append(fallback);
                }

                if (makeUpper && character >= 'a' && character <= 'z')
                {
                    character = (char)(character - 32);
                }

                builder.Append(character);
                makeUpper = false;
            }

            return builder.Length == 0 ? fallback : builder.ToString();
        }

        private static string GetLastPathSegment(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }

        private static string GetFolder(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            int slash = normalized.LastIndexOf('/');
            return slash <= 0 ? string.Empty : normalized.Substring(0, slash);
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/').Trim();
            while (normalized.EndsWith("/"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith("Assets/"))
            {
                return string.Empty;
            }

            string assetsRoot = NormalizeAssetPath(Application.dataPath);
            return Path.Combine(
                assetsRoot,
                normalized.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static string AbsolutePathToAssetPath(string absolutePath)
        {
            string normalized = NormalizeAssetPath(absolutePath);
            string assetsRoot = NormalizeAssetPath(Application.dataPath);
            if (normalized == assetsRoot)
            {
                return "Assets";
            }

            string prefix = assetsRoot + "/";
            if (!normalized.StartsWith(prefix))
            {
                return string.Empty;
            }

            return "Assets/" + normalized.Substring(prefix.Length);
        }

        private static DimensionRuntimeConsumerBootstrapResult Failure(string message)
        {
            return new DimensionRuntimeConsumerBootstrapResult(false, null, message);
        }

        private static string DescribeFirstManifestBlocker(
            DimensionTemplateManifestExportPreview preview)
        {
            DimensionAuthoringOperationPlan operationPlan = preview.OperationPlan;
            IReadOnlyList<DimensionAuthoringOperationItem> operations = operationPlan.Operations;
            if (operations != null)
            {
                for (int i = 0; i < operations.Count; i++)
                {
                    DimensionAuthoringOperationItem operation = operations[i];
                    if (!operation.BlocksManifestExport)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(operation.Message))
                    {
                        return "First blocker: " + operation.Title + " - " + operation.Message;
                    }

                    if (!string.IsNullOrEmpty(operation.Title))
                    {
                        return "First blocker: " + operation.Title + ".";
                    }

                    break;
                }
            }

            if (!string.IsNullOrEmpty(preview.Message))
            {
                return preview.Message;
            }

            return "No detailed blocker was reported.";
        }

        private readonly struct DimensionRuntimePortalOutput
        {
            public readonly string DimensionId;
            public readonly string DimensionDisplayName;
            public readonly string PortalObjectName;
            public readonly string ReturnPortalObjectName;
            public readonly string ItemPortalObjectName;
            public readonly string PortalDisplayName;
            public readonly string AssetStem;
            public readonly DimensionContentPackDefinition ContentPack;
            public readonly DimensionDefinition Dimension;
            public readonly DimensionPortalDefinition EntryPortal;
            public readonly DimensionPortalPresentationDefinition EntryPresentation;
            public readonly DimensionPortalDefinition ReturnPortal;
            public readonly DimensionPortalPresentationDefinition ReturnPresentation;
            public readonly DimensionBounds ReturnRequiredBounds;
            public readonly string StarterId;
            public readonly DimensionBounds StarterGenerationBounds;
            public readonly DimensionBounds StarterTargetLandingBounds;
            public readonly IReadOnlyList<DimensionZoneDefinition> MinimumZones;
            public readonly IReadOnlyList<DimensionGenerationPassDefinition> MinimumGenerationPasses;
            public readonly Color MapColor;
            public readonly float CraftingTimeSeconds;
            public readonly float ActivationChargeSeconds;
            public readonly DimensionPortalVisualProfileAsset VisualProfile;
            public readonly DimensionPortalVisualProfileAsset ItemVisualProfile;
            public readonly bool ItemProfileIsDedicated;

            public DimensionRuntimePortalOutput(
                string dimensionId,
                string dimensionDisplayName,
                string portalObjectName,
                string returnPortalObjectName,
                string itemPortalObjectName,
                string portalDisplayName,
                string assetStem,
                DimensionContentPackDefinition contentPack,
                DimensionDefinition dimension,
                DimensionPortalDefinition entryPortal,
                DimensionPortalPresentationDefinition entryPresentation,
                DimensionPortalDefinition returnPortal,
                DimensionPortalPresentationDefinition returnPresentation,
                DimensionBounds returnRequiredBounds,
                string starterId,
                DimensionBounds starterGenerationBounds,
                DimensionBounds starterTargetLandingBounds,
                IReadOnlyList<DimensionZoneDefinition> minimumZones,
                IReadOnlyList<DimensionGenerationPassDefinition> minimumGenerationPasses,
                Color mapColor,
                float craftingTimeSeconds,
                float activationChargeSeconds,
                DimensionPortalVisualProfileAsset visualProfile,
                DimensionPortalVisualProfileAsset itemVisualProfile,
                bool itemProfileIsDedicated)
            {
                DimensionId = dimensionId ?? string.Empty;
                DimensionDisplayName = string.IsNullOrEmpty(dimensionDisplayName)
                    ? DimensionId
                    : dimensionDisplayName;
                PortalObjectName = portalObjectName ?? string.Empty;
                ReturnPortalObjectName = string.IsNullOrEmpty(returnPortalObjectName)
                    ? PortalObjectName + "_Return"
                    : returnPortalObjectName;
                ItemPortalObjectName = string.IsNullOrEmpty(itemPortalObjectName)
                    ? PortalObjectName + "_Instant"
                    : itemPortalObjectName;
                PortalDisplayName = string.IsNullOrEmpty(portalDisplayName)
                    ? PortalObjectName
                    : portalDisplayName;
                AssetStem = string.IsNullOrEmpty(assetStem) ? "DimensionRuntime" : assetStem;
                ContentPack = contentPack;
                Dimension = dimension;
                EntryPortal = entryPortal;
                EntryPresentation = entryPresentation;
                ReturnPortal = returnPortal;
                ReturnPresentation = returnPresentation;
                ReturnRequiredBounds = returnRequiredBounds;
                StarterId = starterId ?? string.Empty;
                StarterGenerationBounds = starterGenerationBounds;
                StarterTargetLandingBounds = starterTargetLandingBounds;
                MinimumZones = minimumZones ?? new List<DimensionZoneDefinition>();
                MinimumGenerationPasses = minimumGenerationPasses ?? new List<DimensionGenerationPassDefinition>();
                MapColor = mapColor;
                CraftingTimeSeconds = craftingTimeSeconds < 0.0f ? 0.0f : craftingTimeSeconds;
                ActivationChargeSeconds = activationChargeSeconds < 0.0f
                    ? 0.0f
                    : activationChargeSeconds;
                VisualProfile = visualProfile;
                ItemVisualProfile = itemVisualProfile != null ? itemVisualProfile : visualProfile;
                ItemProfileIsDedicated = itemProfileIsDedicated && itemVisualProfile != null;
            }

            /// <summary>
            /// A copy whose visual identity belongs to the instant item portal: the item profile
            /// drives the look, and the asset-stem / object-name seeds that name generated
            /// artwork files and derive SpriteAsset addresses are the item portal's own, so the
            /// item visual pass can never clobber the placed portal's generated assets.
            /// </summary>
            public DimensionRuntimePortalOutput WithInstantVisualIdentity()
            {
                return new DimensionRuntimePortalOutput(
                    DimensionId,
                    DimensionDisplayName,
                    ItemPortalObjectName,
                    ReturnPortalObjectName,
                    ItemPortalObjectName,
                    PortalDisplayName,
                    AssetStem + "Item",
                    ContentPack,
                    Dimension,
                    EntryPortal,
                    EntryPresentation,
                    ReturnPortal,
                    ReturnPresentation,
                    ReturnRequiredBounds,
                    StarterId,
                    StarterGenerationBounds,
                    StarterTargetLandingBounds,
                    MinimumZones,
                    MinimumGenerationPasses,
                    MapColor,
                    CraftingTimeSeconds,
                    ActivationChargeSeconds,
                    ItemVisualProfile,
                    ItemVisualProfile,
                    ItemProfileIsDedicated);
            }
        }
    }
}
