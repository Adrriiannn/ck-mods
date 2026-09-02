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
    /// Working out what a portal ends up as: its definition, its drops, and the item it comes from.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
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
