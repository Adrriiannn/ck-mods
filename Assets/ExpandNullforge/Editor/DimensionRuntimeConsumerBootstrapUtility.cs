using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Portals;
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

    internal static class DimensionRuntimeConsumerBootstrapUtility
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
        private const string PortalCustomSwirlSpriteAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCustomSwirl.asset";
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
        private const long PortalShadowSpriteAssetAddressLow = 4349564516605416516L;
        private const long PortalShadowSpriteAssetAddressHigh = 255574181269321872L;
        private const long PortalShadowCasterSpriteAssetAddressLow = -5105114853745537339L;
        private const long PortalShadowCasterSpriteAssetAddressHigh = -6927483553930170639L;
        private static readonly Vector3 PortalSpritePivotPosition =
            new Vector3(1.0f, 0.0f, -0.4375f);
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

            DimensionRuntimePortalOutput portalOutput =
                ResolvePortalOutput(template, preview, modDisplayName, dimensionId);

            string generatedFolder = templateFolder + "/Generated";
            string portalFolder = generatedFolder + "/Portal";
            string scriptFolder = modRoot + "/Scripts/Generated";
            EnsureFolder(generatedFolder);
            EnsureFolder(portalFolder);
            EnsureFolder(scriptFolder);

            DimensionRuntimeManifestAsset manifestAsset =
                EnsureRuntimeManifestAsset(template, preview, generatedFolder);
            GameObject visualPrefab = EnsurePortalVisualPrefab(portalOutput, portalFolder, modRoot);
            EnsurePortalEntityPrefab(portalOutput, visualPrefab, portalFolder, false);
            EnsurePortalEntityPrefab(portalOutput, visualPrefab, portalFolder, true);
            EnsureGeneratedBootstrapScript(portalOutput, modDisplayName, scriptFolder);
            EnsurePortalTextDataBlocks(portalOutput, modRoot);
            EnsurePortalLocalization(portalOutput, modRoot);
            EnsureConsumerAssemblyReferences(modRoot);
            EnsureFrameworkModDependency(templatePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new DimensionRuntimeConsumerBootstrapResult(
                true,
                manifestAsset,
                "Generated runtime manifest, portal prefab, bootstrap script, text data, and framework dependency for " +
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
            string modRoot)
        {
            string path = portalFolder + "/" + portalOutput.AssetStem + "PortalVisual.prefab";
            GameObject root = CreateCleanPortalVisualTemplate();
            try
            {
                root.name = portalOutput.AssetStem + "PortalVisual";
                DimensionPortal portal = EnsureComponent<DimensionPortal>(root);
                RemoveComponentByName(root, "Portal", portal);
                RemoveMissingMonoBehaviours(root);
                ConfigurePortalRuntimeFields(portal, portalOutput);
                WirePortalVisualReferences(root, portal, portalOutput, portalFolder, modRoot);
                RewriteInteractableCallbacks(root, portal);
                ValidateSpriteObjectOnlyPortal(root);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsurePortalEntityPrefab(
            DimensionRuntimePortalOutput portalOutput,
            GameObject visualPrefab,
            string portalFolder,
            bool returnPortal)
        {
            string portalKind = returnPortal ? "ReturnPortal" : "Portal";
            string path = portalFolder + "/" + portalOutput.AssetStem + portalKind + "Entity.prefab";
            bool unloadPrefabContents;
            GameObject root = LoadRequiredPortalEntityTemplate(out unloadPrefabContents);
            try
            {
                root.name = portalOutput.AssetStem + portalKind + "Entity";
                EnsurePortalEntityAuthoring(root, portalOutput, visualPrefab, returnPortal);

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

        private static void EnsurePortalEntityAuthoring(
            GameObject root,
            DimensionRuntimePortalOutput portalOutput,
            GameObject visualPrefab,
            bool returnPortal)
        {
            DimensionPortalDefinition portalDefinition =
                returnPortal ? portalOutput.ReturnPortal : portalOutput.EntryPortal;
            DimensionPortalPresentationDefinition presentation =
                returnPortal ? portalOutput.ReturnPresentation : portalOutput.EntryPresentation;
            string objectName = returnPortal
                ? portalOutput.ReturnPortalObjectName
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

            InteractWithEnvironmentAuthoring interact = EnsureComponent<InteractWithEnvironmentAuthoring>(root);
            interact.radius = 1.4f;

            RemoveComponentIfPresent<PortalAuthoring>(root);
            if (returnPortal)
            {
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

            EnsurePortalHitAuthoring(root, !returnPortal);

            DimensionPortalAuthoring portal = EnsureComponent<DimensionPortalAuthoring>(root);
            portal.PortalId = portalDefinition.PortalId;
            portal.ActivationCooldownSeconds = presentation.CooldownSeconds;
            portal.ActivationChargeSeconds = returnPortal
                ? 0.0f
                : portalOutput.ActivationChargeSeconds;
            portal.RequireGeneratedArea = presentation.RequireGeneratedAreaOnUse;
            portal.AllowFallbackPosition = presentation.AllowFallbackPositionOnUse;
            portal.ActiveByDefault = true;
            portal.InteractableByDefault = presentation.Interactable;
            portal.IndestructibleByDefault = returnPortal;
            portal.PreviewTargetDimensionId = portalDefinition.ToDimensionId;
            portal.PreviewTargetLocalX = portalDefinition.ToLocalPosition.x;
            portal.PreviewTargetLocalY = portalDefinition.ToLocalPosition.y;

            TryAddGhostAuthoringComponent(root);
            RemoveMissingMonoBehaviours(root);
        }

        private static GameObject CreateCleanPortalVisualTemplate()
        {
            GameObject root = new GameObject("DimensionPortalVisualTemplate");
            root.AddComponent<DimensionPortal>();
            root.AddComponent<DimensionPortalVisual>();
            root.AddComponent<Animator>();

            Transform xScaler = new GameObject("XScaler").transform;
            xScaler.SetParent(root.transform, false);

            GameObject interactableObject = new GameObject("Interactable");
            interactableObject.transform.SetParent(root.transform, false);
            interactableObject.transform.localPosition = new Vector3(1.0f, 0.0f, 0.0f);
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
            DimensionRuntimePortalOutput portalOutput)
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
                "Enter " + portalOutput.DimensionDisplayName + " through a placed portal.");

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
            string modRoot)
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
                    visualProfile);
            portal.shadow = spriteObjects.ShadowRoot;
            ManagedLight portalLight = EnsurePortalManagedLight(root, visualProfile);
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
                if (spriteObjects.OutlineMask != null)
                {
                    outlineSpriteObjects.Add(spriteObjects.OutlineMask);
                }

                if (spriteObjects.OutlineSupportMask != null)
                {
                    outlineSpriteObjects.Add(spriteObjects.OutlineSupportMask);
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
                    PortalCenterEffectSpriteAssetAddressLow,
                    PortalCenterEffectSpriteAssetAddressHigh,
                    PortalCenterEffectSpriteAssetPath,
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

        private static GeneratedPortalSpriteAsset EnsureIntegratedPortalChargingSpriteAsset(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            SpriteAsset bodySource,
            SpriteAsset waveSource,
            bool trimDuplicateTerminalFrame)
        {
            if (bodySource == null || waveSource == null)
            {
                throw new System.InvalidOperationException(
                    "The animated portal charge fallback requires both frame and charge SpriteAssets.");
            }

            Texture2D bodyTexture = bodySource.staticSpriteData == null
                ? null
                : bodySource.staticSpriteData.texture;
            SerializedObject serializedWave = new SerializedObject(waveSource);
            serializedWave.Update();
            SerializedProperty sourceAnimations = serializedWave.FindProperty("m_animations");
            if (bodyTexture == null || sourceAnimations == null || sourceAnimations.arraySize == 0)
            {
                throw new System.InvalidOperationException(
                    "The animated portal charge fallback needs a static frame texture and animation index 0.");
            }

            SerializedProperty sourceAnimation = sourceAnimations.GetArrayElementAtIndex(0);
            SerializedProperty sourceSpriteData =
                sourceAnimation.FindPropertyRelative("m_spriteData");
            SerializedProperty sourceTextureProperty = sourceSpriteData == null
                ? null
                : sourceSpriteData.FindPropertyRelative("texture");
            SerializedProperty sourceEmissiveProperty = sourceSpriteData == null
                ? null
                : sourceSpriteData.FindPropertyRelative("emissiveTexture");
            SerializedProperty sourceFrameCountProperty =
                sourceAnimation.FindPropertyRelative("srcFrameCount");
            SerializedProperty sourceFpsProperty = sourceAnimation.FindPropertyRelative("fps");
            Texture2D waveTexture = sourceEmissiveProperty == null
                ? null
                : sourceEmissiveProperty.objectReferenceValue as Texture2D;
            if (waveTexture == null && sourceTextureProperty != null)
            {
                waveTexture = sourceTextureProperty.objectReferenceValue as Texture2D;
            }

            int sourceFrameCount = sourceFrameCountProperty == null
                ? 0
                : sourceFrameCountProperty.intValue;
            float sourceFps = sourceFpsProperty == null
                ? 0.0f
                : sourceFpsProperty.floatValue;
            if (waveTexture == null || sourceFrameCount <= 0 || sourceFps <= 0.0f)
            {
                throw new System.InvalidOperationException(
                    "Animation index 0 in the charge sweep SpriteAsset has no valid texture, frame count, or FPS.");
            }

            int outputFrameCount = trimDuplicateTerminalFrame && sourceFrameCount > 1
                ? sourceFrameCount - 1
                : sourceFrameCount;
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalChargingBody",
                "DimensionPortalChargingBody");
            string outputAssetPath = portalFolder + "/" + assetName + ".asset";
            string bodySheetPath = portalFolder + "/" + assetName + "Body.png";
            string emissiveSheetPath = portalFolder + "/" + assetName + "Emissive.png";

            WriteIntegratedPortalChargingTextures(
                bodyTexture,
                waveTexture,
                sourceFrameCount,
                outputFrameCount,
                bodySheetPath,
                emissiveSheetPath);
            Texture2D generatedBody = AssetDatabase.LoadAssetAtPath<Texture2D>(bodySheetPath);
            Texture2D generatedEmissive =
                AssetDatabase.LoadAssetAtPath<Texture2D>(emissiveSheetPath);
            if (generatedBody == null || generatedEmissive == null)
            {
                throw new System.InvalidOperationException(
                    "Could not import the generated animated portal charge textures.");
            }

            string addressSeed = portalOutput.PortalObjectName + ":generated-portal-charging-body";
            long addressLow = ComputeStableAddressPart(addressSeed, 0x6368617267656264UL);
            long addressHigh = ComputeStableAddressPart(addressSeed, 0x706F7274616C7761UL);
            float outputFps = sourceFps * outputFrameCount / sourceFrameCount;
            SpriteAsset staged = Object.Instantiate(waveSource);
            try
            {
                staged.name = assetName;
                SerializedObject serializedStaged = new SerializedObject(staged);
                serializedStaged.Update();
                SetSerializedLong(serializedStaged, "m_address.m_low", addressLow);
                SetSerializedLong(serializedStaged, "m_address.m_high", addressHigh);
                SerializedProperty animations = serializedStaged.FindProperty("m_animations");
                animations.arraySize = 1;
                SerializedProperty animation = animations.GetArrayElementAtIndex(0);
                SerializedProperty spriteData = animation.FindPropertyRelative("m_spriteData");
                spriteData.FindPropertyRelative("texture").objectReferenceValue = generatedBody;
                spriteData.FindPropertyRelative("emissiveTexture").objectReferenceValue =
                    generatedEmissive;
                SerializedProperty pivotProperty = spriteData.FindPropertyRelative("pivot");
                if (pivotProperty != null && bodySource.staticSpriteData != null)
                {
                    pivotProperty.vector2Value = new Vector2(
                        bodySource.staticSpriteData.pivot.x,
                        bodySource.staticSpriteData.pivot.y);
                }

                animation.FindPropertyRelative("srcFrameCount").intValue = outputFrameCount;
                animation.FindPropertyRelative("fps").floatValue = outputFps;
                SerializedProperty loopProperty = animation.FindPropertyRelative("loop");
                if (loopProperty != null)
                {
                    loopProperty.boolValue = true;
                }

                SerializedProperty frameData = animation.FindPropertyRelative("frameData");
                if (frameData != null)
                {
                    frameData.arraySize = outputFrameCount;
                }

                serializedStaged.ApplyModifiedPropertiesWithoutUndo();

                SpriteAsset generated = AssetDatabase.LoadAssetAtPath<SpriteAsset>(outputAssetPath);
                if (generated == null)
                {
                    AssetDatabase.CreateAsset(staged, outputAssetPath);
                    generated = staged;
                    staged = null;
                }
                else
                {
                    EditorUtility.CopySerialized(staged, generated);
                    generated.name = assetName;
                }

                // CopySerialized carries the source data-block address. Reapply the generated
                // address after every copy, then make it durable before the manifest can observe
                // the asset. Creating/registering the clone first leaves a duplicate source
                // address in the manifest while the prefab points at an address no asset owns.
                SetSpriteAssetAddress(generated, addressLow, addressHigh);
                EditorUtility.SetDirty(generated);
                AssetDatabase.SaveAssetIfDirty(generated);
                AssetDatabase.ImportAsset(
                    outputAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                generated = AssetDatabase.LoadAssetAtPath<SpriteAsset>(outputAssetPath);
                ValidateIntegratedPortalChargingSpriteAsset(
                    generated,
                    outputAssetPath,
                    addressLow,
                    addressHigh,
                    generatedBody,
                    generatedEmissive,
                    outputFrameCount,
                    outputFps);

                EnsureSpriteAssetManifestContains(modRoot, outputAssetPath);
                AssetDatabase.SaveAssets();

                // A previously generated duplicate can already be cached under the authored
                // charge asset's address. Rebuild both resolver layers only after the repaired
                // asset and manifest are fully durable.
                DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                return new GeneratedPortalSpriteAsset
                {
                    AddressLow = addressLow,
                    AddressHigh = addressHigh,
                    Asset = generated
                };
            }
            finally
            {
                if (staged != null)
                {
                    Object.DestroyImmediate(staged);
                }
            }
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

        private static void ValidateIntegratedPortalChargingSpriteAsset(
            SpriteAsset generated,
            string assetPath,
            long addressLow,
            long addressHigh,
            Texture2D bodyTexture,
            Texture2D emissiveTexture,
            int frameCount,
            float fps)
        {
            if (generated == null)
            {
                throw new System.InvalidOperationException(
                    "Could not reload the generated portal charging SpriteAsset at " +
                    assetPath + ".");
            }

            if (generated.address.lowBits != addressLow ||
                generated.address.highBits != addressHigh)
            {
                throw new System.InvalidOperationException(
                    "The generated portal charging SpriteAsset did not retain its unique " +
                    "data-block address after import.");
            }

            SerializedObject serialized = new SerializedObject(generated);
            serialized.Update();
            SerializedProperty animations = serialized.FindProperty("m_animations");
            SerializedProperty animation = animations == null || animations.arraySize != 1
                ? null
                : animations.GetArrayElementAtIndex(0);
            SerializedProperty spriteData = animation == null
                ? null
                : animation.FindPropertyRelative("m_spriteData");
            SerializedProperty textureProperty = spriteData == null
                ? null
                : spriteData.FindPropertyRelative("texture");
            SerializedProperty emissiveProperty = spriteData == null
                ? null
                : spriteData.FindPropertyRelative("emissiveTexture");
            SerializedProperty frameCountProperty = animation == null
                ? null
                : animation.FindPropertyRelative("srcFrameCount");
            SerializedProperty fpsProperty = animation == null
                ? null
                : animation.FindPropertyRelative("fps");
            if (textureProperty == null || emissiveProperty == null ||
                frameCountProperty == null || fpsProperty == null ||
                textureProperty.objectReferenceValue != bodyTexture ||
                emissiveProperty.objectReferenceValue != emissiveTexture ||
                frameCountProperty.intValue != frameCount ||
                !Mathf.Approximately(fpsProperty.floatValue, fps))
            {
                throw new System.InvalidOperationException(
                    "The generated portal charging SpriteAsset did not retain its generated " +
                    "textures or animation contract after import.");
            }
        }

        private static void WriteIntegratedPortalChargingTextures(
            Texture2D bodySource,
            Texture2D waveSource,
            int sourceFrameCount,
            int outputFrameCount,
            string bodySheetPath,
            string emissiveSheetPath)
        {
            string bodyPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(bodySource));
            string wavePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(waveSource));
            string bodyAbsolute = AssetPathToAbsolutePath(bodyPath);
            string waveAbsolute = AssetPathToAbsolutePath(wavePath);
            if (string.IsNullOrEmpty(bodyAbsolute) ||
                string.IsNullOrEmpty(waveAbsolute) ||
                !bodyPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                !wavePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(bodyAbsolute) ||
                !File.Exists(waveAbsolute))
            {
                throw new System.InvalidOperationException(
                    "Portal frame and animated charge override textures must be saved PNG files.");
            }

            Texture2D decodedBody = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D decodedWave = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D bodySheet = null;
            Texture2D emissiveSheet = null;
            try
            {
                if (!decodedBody.LoadImage(File.ReadAllBytes(bodyAbsolute)) ||
                    !decodedWave.LoadImage(File.ReadAllBytes(waveAbsolute)))
                {
                    throw new System.InvalidOperationException(
                        "Could not decode the portal frame or animated charge override PNG.");
                }

                int frameWidth = decodedWave.width / sourceFrameCount;
                if (sourceFrameCount <= 0 ||
                    decodedWave.width % sourceFrameCount != 0 ||
                    frameWidth != decodedBody.width ||
                    decodedWave.height != decodedBody.height)
                {
                    throw new System.InvalidOperationException(
                        "The animated charge override must use the same per-frame dimensions as the selected portal frame. " +
                        "Frame: " + decodedBody.width + " x " + decodedBody.height +
                        ", charge sheet: " + decodedWave.width + " x " + decodedWave.height +
                        ", frames: " + sourceFrameCount + ".");
                }

                int outputWidth = frameWidth * outputFrameCount;
                Color32[] sourceBodyPixels = decodedBody.GetPixels32();
                Color32[] sourceWavePixels = decodedWave.GetPixels32();
                Color32[] bodyPixels = new Color32[outputWidth * decodedBody.height];
                Color32[] emissivePixels = new Color32[outputWidth * decodedBody.height];
                for (int y = 0; y < decodedBody.height; y++)
                {
                    int bodyRow = y * frameWidth;
                    int outputRow = y * outputWidth;
                    for (int frame = 0; frame < outputFrameCount; frame++)
                    {
                        System.Array.Copy(
                            sourceBodyPixels,
                            bodyRow,
                            bodyPixels,
                            outputRow + frame * frameWidth,
                            frameWidth);
                    }

                    System.Array.Copy(
                        sourceWavePixels,
                        y * decodedWave.width,
                        emissivePixels,
                        outputRow,
                        outputWidth);
                }

                bodySheet = new Texture2D(
                    outputWidth,
                    decodedBody.height,
                    TextureFormat.RGBA32,
                    false);
                bodySheet.SetPixels32(bodyPixels);
                bodySheet.Apply(false, false);
                emissiveSheet = new Texture2D(
                    outputWidth,
                    decodedBody.height,
                    TextureFormat.RGBA32,
                    false);
                emissiveSheet.SetPixels32(emissivePixels);
                emissiveSheet.Apply(false, false);
                WriteBinaryAssetIfChanged(bodySheetPath, bodySheet.EncodeToPNG());
                WriteBinaryAssetIfChanged(emissiveSheetPath, emissiveSheet.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    bodySheetPath,
                    outputWidth,
                    decodedBody.height);
                ConfigureGeneratedSpriteTextureImporter(
                    emissiveSheetPath,
                    outputWidth,
                    decodedBody.height);
            }
            finally
            {
                Object.DestroyImmediate(decodedBody);
                Object.DestroyImmediate(decodedWave);
                if (bodySheet != null)
                {
                    Object.DestroyImmediate(bodySheet);
                }

                if (emissiveSheet != null)
                {
                    Object.DestroyImmediate(emissiveSheet);
                }
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
            long addressLow = ComputeStableAddressPart(
                addressSeed,
                0x70616C657474656CUL);
            long addressHigh = ComputeStableAddressPart(
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
            SetSerializedInt(
                serializedVisual,
                "milestoneEmptyFrame",
                profile == null ? 0 : profile.MilestoneEmptyFrame);
            SetSerializedInt(
                serializedVisual,
                "milestoneFirstFrame",
                profile == null ? 1 : profile.MilestoneFirstFrame);
            SetSerializedInt(
                serializedVisual,
                "milestoneSecondFrame",
                profile == null ? 3 : profile.MilestoneSecondFrame);
            SetSerializedInt(
                serializedVisual,
                "milestoneThirdFrame",
                profile == null ? 4 : profile.MilestoneThirdFrame);
            SetSerializedInt(
                serializedVisual,
                "milestoneReadyFrame",
                profile == null ? 7 : profile.MilestoneReadyFrame);
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
            DimensionPortalVisualProfileAsset visualProfile)
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
            spritePivot.localPosition = PortalSpritePivotPosition;
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
            long addressLow = ComputeStableAddressPart(
                addressSeed,
                0x706F7274616C6F75UL);
            long addressHigh = ComputeStableAddressPart(
                addressSeed,
                0x746C696E656D6173UL);
            long supportAddressLow = ComputeStableAddressPart(
                supportAddressSeed,
                0x737570706F72746DUL);
            long supportAddressHigh = ComputeStableAddressPart(
                supportAddressSeed,
                0x61736B706F727461UL);
            long capAddressLow = ComputeStableAddressPart(
                capAddressSeed,
                0x6361706F75746C69UL);
            long capAddressHigh = ComputeStableAddressPart(
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
        /// Gives Portal Studio the same ParticleAdd/Lightning material selection and backing
        /// texture that the generated runtime portal receives, while keeping the temporary
        /// material instances owned by the preview renderer rather than writing assets.
        /// </summary>
        internal static void ConfigurePortalPersistentParticlePreviewMaterials(
            GameObject effectRoot,
            DimensionPortalVisualProfileAsset visualProfile,
            ICollection<Material> ownedMaterials)
        {
            if (effectRoot == null || ownedMaterials == null)
            {
                return;
            }

            Texture2D textureOverride = ResolvePortalParticleTextureOverride(
                visualProfile,
                false);
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
            DimensionPortalVisualProfileAsset visualProfile)
        {
            if (root == null)
            {
                return null;
            }

            Transform xScaler = EnsurePortalXScaler(root);
            Transform existing = xScaler.Find(PortalLightObjectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject, true);
            }

            GameObject template =
                AssetDatabase.LoadAssetAtPath<GameObject>(PortalVisualTemplatePath);
            Transform source = template == null
                ? null
                : FindDescendantTransform(template.transform, PortalLightObjectName);
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "Could not find the vanilla portal PugLight subtree at " +
                    PortalVisualTemplatePath +
                    ".");
            }

            GameObject clone = Object.Instantiate(source.gameObject);
            clone.name = PortalLightObjectName;
            clone.transform.SetParent(xScaler, false);
            Vector2 lightOffset = visualProfile == null
                ? Vector2.zero
                : visualProfile.GroundLightOffsetPixels;
            clone.transform.localPosition = source.localPosition + new Vector3(
                lightOffset.x / DimensionPortalVisualContract.PixelsPerUnit,
                0.0f,
                lightOffset.y / DimensionPortalVisualContract.PixelsPerUnit);
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;

            ManagedLight managedLight = clone.GetComponent<ManagedLight>();
            Light light = clone.GetComponentInChildren<Light>(true);
            SpriteObject fallback = FindDescendantSpriteObject(clone.transform, "IndirectLightSprite");
            if (managedLight == null || light == null || fallback == null)
            {
                throw new System.InvalidOperationException(
                    "The generated portal PugLight subtree is incomplete. " +
                    "Expected ManagedLight, Point Light, and IndirectLightSprite SpriteObject.");
            }

            managedLight.lightContainer = light.transform.parent != null
                ? light.transform.parent.gameObject
                : light.gameObject;
            managedLight.lightToOptimize = light;
            managedLight.fallbackRenderer = fallback;
            fallback.gameObject.SetActive(false);

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

            light.color = lightColor;
            light.intensity = lightIntensity;
            light.range = lightRange;
            light.shadows = castsShadows ? LightShadows.Hard : LightShadows.None;

            LightFlickerEffect flicker = clone.GetComponentInChildren<LightFlickerEffect>(true);
            if (flicker != null)
            {
                flicker.flickeringLight = light;
                flicker.enableMovement = movement;
                flicker.SetIntensityRange(
                    minimumLightIntensity,
                    maximumLightIntensity);
            }

            bool lightEnabled = visualProfile == null || visualProfile.GroundLightEnabled;
            clone.SetActive(lightEnabled);

            // EntityMonoBehaviour automatically re-enables an assigned optional light
            // when the visual is hydrated. Leaving the disabled subtree unassigned is
            // therefore required for an authored "no ground light" preset to persist.
            return lightEnabled ? managedLight : null;
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

        private static void ValidateSpriteObjectOnlyPortal(GameObject root)
        {
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
                (spritePivot.localPosition - PortalSpritePivotPosition).sqrMagnitude > 0.000001f ||
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
                    PortalSpritePivotPosition +
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
            if (root == null)
            {
                return;
            }

            T component = root.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component, true);
            }
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

        private static GameObject FindDescendantGameObject(Transform root, string childName)
        {
            Transform transform = FindDescendantTransform(root, childName);
            return transform != null ? transform.gameObject : null;
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
            string scriptFolder)
        {
            string className =
                SanitizeIdentifier(modDisplayName, "DimensionMod") +
                SanitizeIdentifier(portalOutput.DimensionId, "Dimension") +
                "RuntimeBootstrap";
            string path = scriptFolder + "/" + className + ".cs";
            string content = BuildBootstrapScript(portalOutput, className);
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
            EnsureFolder(folder);

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
            long addressLow = ComputeStableAddressPart(objectName, 0x6E756C6C666F7267UL);
            long addressHigh = ComputeStableAddressPart(objectName, 0x657870616E646E66UL);
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
            EnsureFolder(localizationFolder);

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

            string itemKey = "Items/" + objectName;
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

        private static long ComputeStableAddressPart(string value, ulong salt)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis ^ salt;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= prime;
                }

                if (hash == 0UL)
                {
                    hash = salt | 1UL;
                }

                return (long)hash;
            }
        }

        private static string BuildPortalDescription(DimensionRuntimePortalOutput portalOutput)
        {
            return "Portal to " + portalOutput.DimensionDisplayName + ".";
        }

        private static string BuildBootstrapScript(
            DimensionRuntimePortalOutput portalOutput,
            string className)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using ExpandNullforge.Api;");
            builder.AppendLine("using ExpandNullforge.Authoring;");
            builder.AppendLine("using ExpandNullforge.Portals;");
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
            AppendIntConstant(builder, "DimensionSpaceKindValue", (int)portalOutput.Dimension.SpaceKind);
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
            builder.AppendLine("    DimensionPortalCraftingRegistry.Register(");
            builder.AppendLine("        new DimensionPortalCraftingRecipeDefinition(");
            builder.AppendLine("            PortalObjectName,");
            builder.AppendLine("            ObjectID.WoodenWorkBench,");
            builder.AppendLine("            1,");
            builder.AppendLine("            CraftingTimeSeconds,");
            builder.AppendLine("            PortalDisplayName));");
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
            builder.AppendLine("            ReturnInteractable));");
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
            builder.AppendLine("        (DimensionSpaceKind)DimensionSpaceKindValue,");
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
            builder.AppendLine("            new DimensionBounds(");
            builder.AppendLine("                new int2(StarterGenerationMinX, StarterGenerationMinY),");
            builder.AppendLine("                new int2(StarterGenerationMaxX, StarterGenerationMaxY)),");
            builder.AppendLine("            100,");
            builder.AppendLine("            true,");
            builder.AppendLine("            \"Starter generation area for \" + DimensionDisplayName + \" Starter.\"));");
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
            AppendMinimumZonesMethod(builder, portalOutput);
            AppendMinimumGenerationPassesMethod(builder, portalOutput);
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
            builder.AppendLine("    Debug.LogWarning(\"[\" + DimensionId + \"] \" + message);");
            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendMinimumZonesMethod(
            StringBuilder builder,
            DimensionRuntimePortalOutput portalOutput)
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
                AppendBoundsConstructor(builder, zone.LocalBounds, "            ");
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
            DimensionRuntimePortalOutput portalOutput)
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
                AppendBoundsConstructor(builder, generationPass.LocalBounds, "            ");
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
            string assetStem =
                SanitizeIdentifier(dimensionDisplayName, "Dimension") +
                "Runtime";

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

            return new DimensionRuntimePortalOutput(
                dimensionId,
                dimensionDisplayName,
                portalObjectName,
                returnPortalObjectName,
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
                template.PortalVisualProfile);
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

        private static DimensionPortalAccessRuleAsset FindPortalRule(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind accessKind,
            string dimensionId,
            bool matchFromDimension)
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
                if (rule == null || !rule.Enabled || rule.AccessKind != accessKind)
                {
                    continue;
                }

                string matchedDimensionId = matchFromDimension
                    ? rule.FromDimensionId
                    : rule.ToDimensionId;
                if (string.IsNullOrEmpty(dimensionId) ||
                    string.Equals(matchedDimensionId, dimensionId, System.StringComparison.Ordinal))
                {
                    return rule;
                }
            }

            return null;
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
                    DimensionSpaceKind.PocketWorld,
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
                dimension.SpaceKind,
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

        private static void EnsureFolder(string folder)
        {
            string normalized = NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] parts = normalized.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
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

            public DimensionRuntimePortalOutput(
                string dimensionId,
                string dimensionDisplayName,
                string portalObjectName,
                string returnPortalObjectName,
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
                DimensionPortalVisualProfileAsset visualProfile)
            {
                DimensionId = dimensionId ?? string.Empty;
                DimensionDisplayName = string.IsNullOrEmpty(dimensionDisplayName)
                    ? DimensionId
                    : dimensionDisplayName;
                PortalObjectName = portalObjectName ?? string.Empty;
                ReturnPortalObjectName = string.IsNullOrEmpty(returnPortalObjectName)
                    ? PortalObjectName + "_Return"
                    : returnPortalObjectName;
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
            }
        }
    }
}
