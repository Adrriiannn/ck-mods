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
    /// The parts of the emitted script that are the mod itself: its constants, its fields, its lifecycle.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// The using lines the generated mod needs, and the class it is.
        /// </summary>
        private static void AppendBootstrapHeader(StringBuilder builder, string className)
        {
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
        }

        /// <summary>
        /// Every value the generator already knows, written into the mod as a constant.
        /// </summary>
        private static void AppendBootstrapConstants(StringBuilder builder, DimensionRuntimePortalOutput portalOutput)
        {
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
        }

        /// <summary>
        /// What the generated mod keeps between calls, and the field that says it is done.
        /// </summary>
        private static void AppendBootstrapFields(StringBuilder builder)
        {
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
        }

        /// <summary>
        /// EarlyInit, Init and Shutdown, which is all PugMod asks a mod for.
        /// </summary>
        private static void AppendBootstrapLifecycle(StringBuilder builder)
        {
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
        }

        /// <summary>
        /// What the generated mod does as each of its own assets finishes loading.
        /// </summary>
        private static void AppendBootstrapModObjectLoaded(StringBuilder builder, string modName)
        {
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
        }

        /// <summary>
        /// Update, and the retry that keeps asking until the service is there.
        /// </summary>
        private static void AppendBootstrapUpdateLoop(StringBuilder builder)
        {
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
        }

        /// <summary>
        /// Everything registered once before any world: recipes, tables, spawns, scenes.
        /// </summary>
        private static void AppendBootstrapStaticExtras(StringBuilder builder, DimensionRuntimePortalOutput portalOutput, DimensionTemplateAsset template, string modName)
        {
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
        }
    }
}
