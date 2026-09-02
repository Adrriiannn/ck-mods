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
    }
}
