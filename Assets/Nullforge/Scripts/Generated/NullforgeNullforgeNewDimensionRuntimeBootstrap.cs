using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Portals;
using PugMod;
using Unity.Mathematics;
using UnityEngine;

public sealed class NullforgeNullforgeNewDimensionRuntimeBootstrap : IMod
{
  private const string DimensionId = "Nullforge:NewDimension";
  private const string DimensionDisplayName = "New Dimension";
  private const string ContentPackId = "Nullforge:NewDimensionPack";
  private const string ContentPackDisplayName = "New Dimension Pack";
  private const string ContentPackVersion = "1.0.0";
  private const string ContentPackAuthor = "";
  private const string ContentPackDescription = "A tiny first-biome room for validating dimension travel, local coordinates, and export plumbing before you design the full biome layout.";
  private const int ContentPackMinimumApiVersion = 1;
  private const int DimensionAbsoluteOriginX = 0;
  private const int DimensionAbsoluteOriginY = 7000;
  private const int DimensionLocalMinX = -72;
  private const int DimensionLocalMinY = -72;
  private const int DimensionLocalMaxX = 72;
  private const int DimensionLocalMaxY = 72;
  private const int DimensionGenerationVersion = 1;
  private const int DimensionSpaceKindValue = 1;
  private const int DimensionCapabilitiesValue = 352255;
  private const int DimensionLifecycleStateValue = 1;
  private const string EntryPortalId = "Nullforge:NewDimension.portal.entry";
  private const string ReturnPortalId = "Nullforge:NewDimension.portal.return";
  private const string EntryPresentationId = "Nullforge:NewDimension.portal.entry.presentation";
  private const string ReturnPresentationId = "Nullforge:NewDimension.portal.return.presentation";
  private const string PortalObjectName = "Nullforge_NullforgeNewDimension_Portal";
  private const string ReturnPortalObjectName = "Nullforge_NullforgeNewDimension_Portal_Return";
  private const string PortalDisplayName = "Nullforge Portal";
  private const string PortalDescription = "Portal to New Dimension.";
  private const string EntryFromDimensionId = "corekeeper.overworld";
  private const string EntryToDimensionId = "Nullforge:NewDimension";
  private const string ReturnFromDimensionId = "Nullforge:NewDimension";
  private const string ReturnToDimensionId = "corekeeper.overworld";
  private const float EntryFromLocalX = 0f;
  private const float EntryFromLocalY = 0f;
  private const float EntryToLocalX = 0f;
  private const float EntryToLocalY = 0f;
  private const float ReturnFromLocalX = 0f;
  private const float ReturnFromLocalY = 0f;
  private const float ReturnToLocalX = 0f;
  private const float ReturnToLocalY = 0f;
  private const float EntryCooldownSeconds = 2f;
  private const float ReturnCooldownSeconds = 0f;
  private const bool EntryRequireGeneratedArea = true;
  private const bool EntryAllowFallbackPosition = true;
  private const bool EntryInteractable = true;
  private const bool ReturnRequireGeneratedArea = false;
  private const bool ReturnAllowFallbackPosition = true;
  private const bool ReturnInteractable = true;
  private const int ReturnRequiredMinX = -8;
  private const int ReturnRequiredMinY = -8;
  private const int ReturnRequiredMaxX = 8;
  private const int ReturnRequiredMaxY = 8;
  private const string StarterId = "Nullforge:NewDimension.starter";
  private const int StarterGenerationMinX = -8;
  private const int StarterGenerationMinY = -8;
  private const int StarterGenerationMaxX = 8;
  private const int StarterGenerationMaxY = 8;
  private const int StarterLandingMinX = -8;
  private const int StarterLandingMinY = -8;
  private const int StarterLandingMaxX = 8;
  private const int StarterLandingMaxY = 8;
  private const float CraftingTimeSeconds = 2f;

  private readonly List<DimensionRuntimeManifestAsset> manifests =
      new List<DimensionRuntimeManifestAsset>();
  private IDimensionService service;
  private bool staticRuntimeExtrasRegistered;
  private bool minimumRuntimeDefinitionsRegistered;
  private bool manifestsApplied;
  private bool portalDefinitionsRegistered;
  private int nextApplyFrame;
  private string lastFailureCode = string.Empty;

  private bool RuntimeOutputReady
  {
    get
    {
      return staticRuntimeExtrasRegistered &&
          minimumRuntimeDefinitionsRegistered &&
          manifestsApplied &&
          portalDefinitionsRegistered;
    }
  }

  public void EarlyInit()
  {
    EnsureStaticRuntimeExtras();
    TryApplyRuntimeOutput();
  }

  public void Init()
  {
    EnsureStaticRuntimeExtras();
    TryApplyRuntimeOutput();
  }

  public void Shutdown()
  {
    manifests.Clear();
    service = null;
    staticRuntimeExtrasRegistered = false;
    minimumRuntimeDefinitionsRegistered = false;
    manifestsApplied = false;
    portalDefinitionsRegistered = false;
    nextApplyFrame = 0;
    lastFailureCode = string.Empty;
  }

  public void ModObjectLoaded(UnityEngine.Object obj)
  {
    DimensionRuntimeManifestAsset manifest = obj as DimensionRuntimeManifestAsset;
    if (manifest == null)
    {
      return;
    }

    if (!manifests.Contains(manifest))
    {
      manifests.Add(manifest);
      manifestsApplied = false;
      portalDefinitionsRegistered = false;
    }

    // Register the painted tile map the moment the manifest asset loads — before the
    // world generates the dimension area. DimensionTileMapRegistry is a plain static
    // store, so this does not need the dimension service (not ready this early);
    // registering here lets the tile-map provider win over the flat safe platform.
    if (manifest.HasTileMap)
    {
      DimensionTileMapRegistry.Register(
          manifest.GeneratedFromDimensionId, manifest.TileMap);
    }

    TryApplyRuntimeOutput();
  }

  public void Update()
  {
    if (RuntimeOutputReady)
    {
      return;
    }

    EnsureStaticRuntimeExtras();
    TryApplyRuntimeOutput();
  }

  private void TryApplyRuntimeOutput()
  {
    EnsureStaticRuntimeExtras();
    if (Time.frameCount < nextApplyFrame)
    {
      return;
    }

    IDimensionService current;
    if (!DimensionApi.TryGetService(out current) || current == null)
    {
      ScheduleRetry();
      return;
    }

    if (!object.ReferenceEquals(service, current))
    {
      service = current;
      minimumRuntimeDefinitionsRegistered = false;
      manifestsApplied = false;
      portalDefinitionsRegistered = false;
    }

    bool minimumRuntimeReady = EnsureMinimumRuntimeDefinitions(current);
    if (minimumRuntimeReady)
    {
      RegisterPortalDefinitions(current);
    }

    bool manifestsReady = ApplyManifests(current);
    if (!minimumRuntimeReady || !manifestsReady)
    {
      return;
    }
  }

  private void EnsureStaticRuntimeExtras()
  {
    if (staticRuntimeExtrasRegistered)
    {
      return;
    }

    DimensionPortalCraftingRegistry.Register(
        new DimensionPortalCraftingRecipeDefinition(
            PortalObjectName,
            ObjectID.WoodenWorkBench,
            1,
            CraftingTimeSeconds,
            PortalDisplayName));
    DimensionPortalItemPresentationRegistry.Register(
        new DimensionPortalItemPresentationDefinition(
            PortalObjectName,
            PortalDisplayName,
            PortalDescription,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            true));
    DimensionReturnPortalSpawnRegistry.Register(
        new DimensionReturnPortalSpawnDefinition(
            ReturnPortalId,
            DimensionId,
            ReturnPortalObjectName,
            ReturnToDimensionId,
            new float2(ReturnFromLocalX, ReturnFromLocalY),
            new float2(ReturnToLocalX, ReturnToLocalY),
            new DimensionBounds(
                new int2(ReturnRequiredMinX, ReturnRequiredMinY),
                new int2(ReturnRequiredMaxX, ReturnRequiredMaxY)),
            ReturnCooldownSeconds,
            ReturnRequireGeneratedArea,
            ReturnAllowFallbackPosition,
            PortalDisplayName + " Return",
            ReturnInteractable));
    DimensionPortalSoundRegistry.Register(
        PortalObjectName,
        0,
        "Assets/Audio/AutoLoad/SFX/AF_portal_appear.ogg",
        string.Empty,
        string.Empty);
    DimensionPortalSoundRegistry.Register(
        ReturnPortalObjectName,
        0,
        "Assets/Audio/AutoLoad/SFX/AF_portal_appear.ogg",
        string.Empty,
        string.Empty);
    DimensionPortalSoundRegistry.Register(
        "Nullforge_NullforgeNewDimension_Portal_Instant",
        1,
        "AF_portal_appear",
        "AF_portal_collapse",
        "Assets/Audio/Audio Ambient/Ambience, Designed, Forest, Magic, Abstract, Eerie, Creatures In Distance 01 SND39860 3.ogg");
    DimensionItemPortalRegistry.Register(
        "Nullforge:NewDimension.portal.item.object",
        "Nullforge_NullforgeNewDimension_Portal_Instant",
        "Nullforge:NewDimension.portal.item",
        "Nullforge:NewDimension",
        10f);
    DimensionPortalCraftingRegistry.Register(
        new DimensionPortalCraftingRecipeDefinition(
            "Nullforge:NewDimension.portal.item.object",
            ObjectID.WoodenWorkBench,
            1,
            CraftingTimeSeconds,
            "New Dimension Portal (Item)"));
    staticRuntimeExtrasRegistered = true;
  }

  private bool EnsureMinimumRuntimeDefinitions(IDimensionService current)
  {
    if (minimumRuntimeDefinitionsRegistered)
    {
      return true;
    }

    DimensionContentPackDefinition existingContentPack;
    if (!current.TryGetContentPack(ContentPackId, out existingContentPack))
    {
      DimensionOperationResult result;
      if (!current.TryRegisterContentPack(
          new DimensionContentPackDefinition(
              ContentPackId,
              ContentPackDisplayName,
              ContentPackVersion,
              ContentPackAuthor,
              ContentPackDescription,
              ContentPackMinimumApiVersion,
              CreateContentPackDependencyIds(),
              true),
          out result))
      {
        WarnOnce(result.Code, result.Message);
        ScheduleRetry();
        return false;
      }
    }

    DimensionDefinition existingDimension;
    if (!current.TryGetDimension(DimensionId, out existingDimension))
    {
      DimensionOperationResult result;
      if (!current.TryRegisterDimension(CreateDimensionDefinition(), out result))
      {
        WarnOnce(result.Code, result.Message);
        ScheduleRetry();
        return false;
      }
    }

    if (!TryEnsureStarter(current, CreateStarterDefinition()))
    {
      ScheduleRetry();
      return false;
    }

    if (!EnsureMinimumZones(current))
    {
      ScheduleRetry();
      return false;
    }

    if (!EnsureMinimumGenerationPasses(current))
    {
      ScheduleRetry();
      return false;
    }

    minimumRuntimeDefinitionsRegistered = true;
    return true;
  }

  private static DimensionDefinition CreateDimensionDefinition()
  {
    return new DimensionDefinition(
        DimensionId,
        DimensionDisplayName,
        new int2(DimensionAbsoluteOriginX, DimensionAbsoluteOriginY),
        new DimensionBounds(
            new int2(DimensionLocalMinX, DimensionLocalMinY),
            new int2(DimensionLocalMaxX, DimensionLocalMaxY)),
        DimensionGenerationVersion,
        (DimensionSpaceKind)DimensionSpaceKindValue,
        (DimensionCapabilityFlags)DimensionCapabilitiesValue,
        (DimensionLifecycleState)DimensionLifecycleStateValue);
  }

  private static DimensionStarterDefinition CreateStarterDefinition()
  {
    return new DimensionStarterDefinition(
        StarterId,
        DimensionId,
        DimensionDisplayName + " Starter",
        "Starter generation and travel loop for " + DimensionDisplayName + " Starter.",
        ContentPackId,
        true,
        new DimensionContentReadinessRequest(
            StarterId + ".readiness",
            DimensionDisplayName + " Starter Readiness",
            new List<DimensionContentReadinessRequirement>()),
        new DimensionTravelLoopPreflightRequest(
            EntryFromDimensionId,
            EntryToDimensionId,
            EntryPortalId,
            ReturnPortalId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            new DimensionBounds(
                new int2(StarterLandingMinX, StarterLandingMinY),
                new int2(StarterLandingMaxX, StarterLandingMaxY)),
            true,
            false,
            false,
            false,
            true,
            false),
        new DimensionStarterGenerationRequest(
            StarterId,
            "dimension-starter:" + StarterId,
            DimensionId,
            ResolveStarterGenerationBounds(),
            100,
            true,
            "Starter generation area for " + DimensionDisplayName + " Starter."));
  }

  private static DimensionBounds ResolveStarterGenerationBounds()
  {
    // A painted tile map defines the biome's extent; generate exactly that region so the
    // whole authored map lands and nothing is clipped. Fall back to the authored starter
    // area when the dimension has no painted map (a flat safe platform is generated there).
    DimensionTileMapModel tileMap;
    if (DimensionTileMapRegistry.TryGet(DimensionId, out tileMap) &&
        tileMap != null && tileMap.PaintedTileCount() > 0)
    {
      return tileMap.LocalBounds;
    }

    return new DimensionBounds(
        new int2(StarterGenerationMinX, StarterGenerationMinY),
        new int2(StarterGenerationMaxX, StarterGenerationMaxY));
  }

  private static List<string> CreateContentPackDependencyIds()
  {
    List<string> dependencies = new List<string>();
    return dependencies;
  }

  private bool EnsureMinimumZones(IDimensionService current)
  {
    if (!TryEnsureZone(
        current,
        new DimensionZoneDefinition(
            "Nullforge:NewDimensionStarterBiome",
            "Single Biome",
            "Nullforge:NewDimension",
            new DimensionBounds(
                new int2(-16, -16),
                new int2(16, 16)),
            "Nullforge:NewDimensionStarterBiome",
            0,
            true)))
    {
      return false;
    }

    return true;
  }

  private bool EnsureMinimumGenerationPasses(IDimensionService current)
  {
    if (!TryEnsureGenerationPass(
        current,
        new DimensionGenerationPassDefinition(
            "Nullforge:NewDimension.single-biome.terrain-pass",
            "Terrain",
            "Nullforge:NewDimension",
            "Nullforge:NewDimensionStarterBiome",
            true,
            new DimensionBounds(
                new int2(-16, -16),
                new int2(16, 16)),
            (DimensionGenerationPassPhase)0,
            0,
            "expandnullforge:safe-platform",
            true)))
    {
      return false;
    }

    return true;
  }

  private bool ApplyManifests(IDimensionService current)
  {
    if (manifestsApplied)
    {
      return true;
    }

    if (manifests.Count == 0)
    {
      manifestsApplied = true;
      return true;
    }

    for (int i = 0; i < manifests.Count; i++)
    {
      DimensionRuntimeManifestAsset manifest = manifests[i];
      if (manifest == null)
      {
        continue;
      }

      // Declare generated items so the framework can name any that never
      // registered with the game. Reporting is deliberately not done here:
      // object ids can still arrive after this point.
      DimensionItemObjectRegistry.Declare(
          manifest.GeneratedFromContentPackId, manifest.GeneratedItemIds);

      // The dimension's painted tile map is registered early in ModObjectLoaded, before
      // world generation — not here, which runs too late in the service-gated apply loop.

      DimensionContentManifestResult applyResult;
      DimensionOperationResult buildResult;
      if (!manifest.TryApplyTo(
          current,
          "Apply generated dimension runtime manifest.",
          out applyResult,
          out buildResult))
      {
        string code = buildResult.Success ? "manifest-apply-failed" : buildResult.Code;
        string message = buildResult.Success
            ? "The generated dimension manifest could not be applied."
            : buildResult.Message;
        WarnOnce(code, message);
        if (code == "runtime-manifest-snapshot-missing" || code == "template-null")
        {
          continue;
        }
        ScheduleRetry();
        return false;
      }
    }

    manifestsApplied = true;
    lastFailureCode = string.Empty;
    return true;
  }

  private void RegisterPortalDefinitions(IDimensionService current)
  {
    if (portalDefinitionsRegistered)
    {
      return;
    }

    if (!TryEnsurePortal(
        current,
        new DimensionPortalDefinition(
            EntryPortalId,
            PortalDisplayName,
            EntryFromDimensionId,
            new float2(EntryFromLocalX, EntryFromLocalY),
            EntryToDimensionId,
            new float2(EntryToLocalX, EntryToLocalY),
            DimensionPortalState.Available)))
    {
      ScheduleRetry();
      return;
    }

    if (!TryEnsurePortal(
        current,
        new DimensionPortalDefinition(
            ReturnPortalId,
            PortalDisplayName + " Return",
            ReturnFromDimensionId,
            new float2(ReturnFromLocalX, ReturnFromLocalY),
            ReturnToDimensionId,
            new float2(ReturnToLocalX, ReturnToLocalY),
            DimensionPortalState.Available)))
    {
      ScheduleRetry();
      return;
    }

    if (!TryEnsurePortalPresentation(
        current,
        new DimensionPortalPresentationDefinition(
            EntryPresentationId,
            EntryPortalId,
            PortalDisplayName,
            "Enter " + PortalDisplayName,
            "The portal is not active yet.",
            string.Empty,
            string.Empty,
            string.Empty,
            EntryCooldownSeconds,
            0,
            true,
            EntryRequireGeneratedArea,
            EntryAllowFallbackPosition,
            EntryInteractable)))
    {
      ScheduleRetry();
      return;
    }

    if (!TryEnsurePortalPresentation(
        current,
        new DimensionPortalPresentationDefinition(
            ReturnPresentationId,
            ReturnPortalId,
            PortalDisplayName + " Return",
            "Return to the core.",
            "The return portal is not active yet.",
            string.Empty,
            string.Empty,
            string.Empty,
            ReturnCooldownSeconds,
            0,
            true,
            ReturnRequireGeneratedArea,
            ReturnAllowFallbackPosition,
            ReturnInteractable)))
    {
      ScheduleRetry();
      return;
    }

    // Instant item portal (V2): the spawned temporary portal carries this id,
    // so travel must resolve it like any other portal. It lands at the entry
    // portal's arrival point.
    if (!TryEnsurePortal(
        current,
        new DimensionPortalDefinition(
            "Nullforge:NewDimension.portal.item",
            "New Dimension Portal (Item)",
            EntryFromDimensionId,
            float2.zero,
            "Nullforge:NewDimension",
            new float2(EntryToLocalX, EntryToLocalY),
            DimensionPortalState.Available)))
    {
      ScheduleRetry();
      return;
    }

    if (!TryEnsurePortalPresentation(
        current,
        new DimensionPortalPresentationDefinition(
            "Nullforge:NewDimension.portal.item" + ".presentation",
            "Nullforge:NewDimension.portal.item",
            "New Dimension Portal (Item)",
            "Enter " + "New Dimension Portal (Item)",
            "The portal is not active yet.",
            string.Empty,
            string.Empty,
            string.Empty,
            0f,
            0,
            true,
            EntryRequireGeneratedArea,
            EntryAllowFallbackPosition,
            true)))
    {
      ScheduleRetry();
      return;
    }

    // Its ".back" twin: using the portal item INSIDE the target dimension
    // opens a temporary portal home instead. Dimension -> overworld, so travel
    // lands at the player's tracked overworld exit point; the entry portal's
    // overworld position is only the fallback when no visit is recorded.
    if (!TryEnsurePortal(
        current,
        new DimensionPortalDefinition(
            "Nullforge:NewDimension.portal.item" + ".back",
            "New Dimension Portal (Item)" + " Return",
            "Nullforge:NewDimension",
            float2.zero,
            EntryFromDimensionId,
            new float2(EntryFromLocalX, EntryFromLocalY),
            DimensionPortalState.Available)))
    {
      ScheduleRetry();
      return;
    }

    if (!TryEnsurePortalPresentation(
        current,
        new DimensionPortalPresentationDefinition(
            "Nullforge:NewDimension.portal.item" + ".back.presentation",
            "Nullforge:NewDimension.portal.item" + ".back",
            "New Dimension Portal (Item)" + " Return",
            "Return home.",
            "The portal is not active yet.",
            string.Empty,
            string.Empty,
            string.Empty,
            0f,
            0,
            true,
            false,
            true,
            true)))
    {
      ScheduleRetry();
      return;
    }

    portalDefinitionsRegistered = true;
  }

  private bool TryEnsurePortal(
      IDimensionService current,
      DimensionPortalDefinition portal)
  {
    DimensionPortalDefinition existing;
    if (current.TryGetPortal(portal.PortalId, out existing))
    {
      return true;
    }

    DimensionOperationResult result;
    if (current.TryRegisterPortal(portal, out result))
    {
      return true;
    }

    WarnOnce(result.Code, result.Message);
    return false;
  }

  private bool TryEnsurePortalPresentation(
      IDimensionService current,
      DimensionPortalPresentationDefinition presentation)
  {
    DimensionPortalPresentationDefinition existing;
    if (current.TryGetPortalPresentation(presentation.PresentationId, out existing))
    {
      return true;
    }

    DimensionOperationResult result;
    if (current.TryRegisterPortalPresentation(presentation, out result))
    {
      return true;
    }

    WarnOnce(result.Code, result.Message);
    return false;
  }

  private bool TryEnsureZone(
      IDimensionService current,
      DimensionZoneDefinition zone)
  {
    DimensionZoneDefinition existing;
    if (current.TryGetZoneDefinition(zone.ZoneId, out existing))
    {
      return true;
    }

    DimensionOperationResult result;
    if (current.TryRegisterZoneDefinition(zone, out result))
    {
      return true;
    }

    WarnOnce(result.Code, result.Message);
    return false;
  }

  private bool TryEnsureGenerationPass(
      IDimensionService current,
      DimensionGenerationPassDefinition generationPass)
  {
    DimensionGenerationPassDefinition existing;
    if (current.TryGetGenerationPass(generationPass.PassId, out existing))
    {
      return true;
    }

    DimensionOperationResult result;
    if (current.TryRegisterGenerationPass(generationPass, out result))
    {
      return true;
    }

    WarnOnce(result.Code, result.Message);
    return false;
  }

  private bool TryEnsureStarter(
      IDimensionService current,
      DimensionStarterDefinition starter)
  {
    DimensionStarterDefinition existing;
    if (current.TryGetStarter(starter.StarterId, out existing))
    {
      return true;
    }

    DimensionOperationResult result;
    if (current.TryRegisterStarter(starter, out result))
    {
      return true;
    }

    WarnOnce(result.Code, result.Message);
    return false;
  }

  private void ScheduleRetry()
  {
    nextApplyFrame = Time.frameCount + 60;
  }

  private void WarnOnce(string code, string message)
  {
    string failureCode = string.IsNullOrEmpty(code) ? "dimension-runtime-bootstrap" : code;
    if (lastFailureCode == failureCode)
    {
      return;
    }

    lastFailureCode = failureCode;
    Debug.LogWarning("[" + DimensionId + "] " + message);
  }
}
