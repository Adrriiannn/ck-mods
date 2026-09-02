using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private const int MaxDiagnostics = 256;
    private const int ParentSubMapSize = 64;
    private const int RuntimeLoadCellSizeTiles = 16;
    private const int MaximumLoadAreaSideTiles = 128;
    private const int MaximumLoadAreaTiles = 4096;
    private const int MaximumRuntimeLoadTickets = 256;
    private const int MaximumRuntimeLoadCells = 256;
    private const float MinimumLoadRadius = 12.0f;
    // Settle window after a load area's parent submaps are first observed before the area is
    // reported resident/simulating. This is only a streaming-stability cushion — tile generation
    // is gated separately (the generation provider reaches Ready before a load ticket stabilizes),
    // so it does not need to guard against unwritten terrain. It was 1.0s, which put a flat ~1s tax
    // on every dimension entry (the return trip skips this pipeline entirely, which is why entry
    // felt sluggish next to the near-instant return); 0.25s keeps a small cushion — about one extra
    // reconcile tick (RuntimeLoadReconcileIntervalSeconds = 0.20s) — while making entry vanilla-quick.
    private const float LoadedStabilizationSeconds = 0.25f;
    private const float DefaultLoadTimeoutSeconds = 30.0f;
    private const float GenerationLoadTimeoutSeconds = 60.0f;
    private const float RuntimeLoadReconcileIntervalSeconds = 0.20f;
    private const float GenerationProgressChangeEpsilon = 0.001f;
    private const double SettledRuntimeLoadHealthIntervalSeconds = 5.0d;
    private const double RuntimeGenerationTickIntervalSeconds = 0.05d;
    private const int MaxPlannedGenerationPassesPerTick = 4;
    private const double PlayerContextTrackIntervalSeconds = 1.0d;
    private const double IdleOverworldPlayerContextTrackIntervalSeconds = 15.0d;
    private const int PlayerContextPersistenceTileThreshold = 8;
    private const int TravelPreloadSideTiles = 16;
    private const float TravelLoadTimeoutSeconds = 45.0f;
    private const float TravelArrivalTimeoutSeconds = 12.0f;
    private const float TravelArrivalDistanceSquared = 1.0f;
    private const float TravelTeleportRetryIntervalSeconds = 1.50f;
    private const int TravelTeleportAttemptLimit = 4;
    private const string TargetAreaNotGeneratedCode = "target-area-not-generated";
    private const string TargetAreaGenerationReadyRetryCode = "target-area-generation-ready-retry";
    private const string BuiltInTravelRequirementAccessProviderId = "expandnullforge:travel-requirement-access";
    private const string BuiltInProgressFlagRequirementEvaluatorId = "expandnullforge:progress-flag-requirements";
    // THE ONLY BUILT-IN ID ANYTHING REFUSES TO REMOVE, and the list of what that leaves open. Three
    // things here are protected: the overworld map layer below, the overworld dimension
    // (IsProtectedDimensionId) and this framework's own content pack (IsProtectedContentPackId).
    //
    // PORTALS, MARKERS, ANCHORS AND STARTERS THIS FRAMEWORK REGISTERS ARE REMOVABLE, like anybody
    // else's. That is deliberate and it is written here because it did not used to be readable
    // anywhere: four predicates named IsProtectedPortalId, IsProtectedMarkerId,
    // IsProtectedAnchorId and IsProtectedStarterId returned false, and eight branches consulted
    // them and raised a "…-protected" refusal nothing could reach. They are gone. The framework
    // ships no built-in content of those four kinds — there is no id below to protect — and a
    // guard over an empty set that reads as a guarantee is the worse of the two states.
    //
    // Ship built-in content of one of those kinds and this is where its id goes, beside this one,
    // with a predicate to match. Docs/protected-ids-decision.md names both mutation points per
    // kind, so the branches go back where they came from.
    private const string BuiltInOverworldMapLayerId = "corekeeper:map-layer-overworld";
    private const uint StableHashOffset = 2166136261u;
    private const uint StableHashPrime = 16777619u;

    private static readonly DimensionContentPackDefinition BuiltInFrameworkContentPack =
        new DimensionContentPackDefinition(
            "expandnullforge",
            "ExpandNullforge Dimension Framework",
            "0.1.0",
            "MariusAlbu + Codex",
            "Core dimension API, travel, loading, map metadata, generation, and modular content framework.",
            DimensionApi.CurrentApiVersion,
            new List<string>(),
            true);

    private static readonly DimensionCapabilityFlags StableCoordinateCapabilities =
        DimensionCapabilityFlags.LocalCoordinates
        | DimensionCapabilityFlags.AbsoluteCoordinates
        | DimensionCapabilityFlags.PlayerContext
        | DimensionCapabilityFlags.Map
        | DimensionCapabilityFlags.Minimap
        | DimensionCapabilityFlags.Markers
        | DimensionCapabilityFlags.Persistence
        | DimensionCapabilityFlags.Multiplayer
        | DimensionCapabilityFlags.CoordinateInterop;

    private static readonly DimensionDefinition OverworldDefinition =
        new DimensionDefinition(
            DimensionIds.Overworld,
            "Overworld",
            int2.zero,
            new DimensionBounds(new int2(-1000000000, -1000000000), new int2(1000000000, 1000000000)),
            1,
            DimensionType.World,
            StableCoordinateCapabilities
                | DimensionCapabilityFlags.PlayerTravel
                | DimensionCapabilityFlags.Respawn
                | DimensionCapabilityFlags.Zones
                | DimensionCapabilityFlags.Bosses
                | DimensionCapabilityFlags.CustomBiomeLookup
                | DimensionCapabilityFlags.AutomationInterop,
            DimensionLifecycleState.Ready);

    private static readonly DimensionMapLayerDefinition OverworldMapLayerDefinition =
        new DimensionMapLayerDefinition(
            BuiltInOverworldMapLayerId,
            DimensionIds.Overworld,
            "Overworld",
            "Vanilla Core Keeper world map layer.",
            "corekeeper.map.overworld",
            true,
            0xBFA67CFFu,
            0,
            true,
            true);
  }
}
