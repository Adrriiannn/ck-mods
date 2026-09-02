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
    /// The parts of the emitted script that talk to the service: definitions, manifests, portals.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Registering the content pack, dimension and starter the mod cannot run without.
        /// </summary>
        private static void AppendBootstrapMinimumDefinitions(StringBuilder builder)
        {
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
        }

        /// <summary>
        /// The small factory methods those registrations call, and the layout they pin to.
        /// </summary>
        private static void AppendBootstrapDefinitionFactories(StringBuilder builder, DimensionRuntimePortalOutput portalOutput, DimensionBounds tileMapBounds, DimensionTemplateAsset template)
        {
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
        }

        /// <summary>
        /// Handing the loaded manifests to the service, and what to do when it will not take them.
        /// </summary>
        private static void AppendBootstrapManifestApply(StringBuilder builder)
        {
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
        }

        /// <summary>
        /// Registering the portals, their presentations, and what an item portal turns into.
        /// </summary>
        private static void AppendBootstrapPortalRegistration(StringBuilder builder, DimensionRuntimePortalOutput portalOutput)
        {
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
        }

        /// <summary>
        /// The helpers that register one thing at a time, the retry, and the one warning.
        /// </summary>
        private static void AppendBootstrapEnsureHelpers(StringBuilder builder)
        {
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
        }
    }
}
