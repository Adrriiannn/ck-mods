using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Generation;
using ExpandNullforge.Networking;
using ExpandNullforge.Persistence;
using ExpandNullforge.Portals;
using ExpandNullforge.UI;
using PlayerEquipment;
using PugMod;
using Unity.Entities;
using UnityEngine;

public sealed class ExpandNullforgeModEntry : IMod
{
  private const string ProviderId = "expandnullforge";
  private const double ReturnPortalSpawnerWakeProbeSeconds = 1.0d;

  private static readonly NullforgeDimensionService DimensionService = new NullforgeDimensionService();
  private static readonly DimensionSafePlatformGenerationProvider SafePlatformGenerationProvider =
      new DimensionSafePlatformGenerationProvider();
  private static readonly DimensionTileMapGenerationProvider TileMapGenerationProvider =
      new DimensionTileMapGenerationProvider();
  private static readonly DimensionScenePlacementPassProvider ScenePlacementProvider =
      new DimensionScenePlacementPassProvider(DimensionService);
  private static readonly DimensionOreScatterPassProvider OreScatterProvider =
      new DimensionOreScatterPassProvider();
  private static readonly DimensionDungeonPlacementPassProvider DungeonPlacementProvider =
      new DimensionDungeonPlacementPassProvider(DimensionService);

  private World registeredServerWorld;
  private World registeredClientWorld;

  /// <summary>Whether Burst has already been switched off for the equipment update.</summary>
  private static bool burstArmedForContent;
  private DimensionReturnPortalSpawnSystem returnPortalSpawnSystem;
  private double nextReturnPortalSpawnerWakeProbeAt;
  private bool serverWorldPersistenceInitialized;

  /// <summary>Whether the declared-item report has already been asked for this session.</summary>
  private bool itemRegistryReported;

  /// <summary>The declaration count the last frame looked at, so a new one re-arms the wait.</summary>
  private int lastSeenDeclarationVersion = -1;

  private int framesSinceLastDeclaration;

  /// <summary>
  /// How many quiet frames count as "content has finished declaring itself". Small on purpose:
  /// the generated bootstrap retries on consecutive frames, so a handful is already generous, and
  /// the only cost of waiting is how late a genuinely missing item is named.
  /// </summary>
  private const int FramesToWaitAfterLastDeclaration = 10;

  /// <summary>Whether Update has thrown and been switched off for the rest of the session.</summary>
  private bool updateDisabled;

  /// <summary>Whether Init threw, which means the framework is not really loaded.</summary>
  private static bool initThrew;

  /// <summary>Whether Init threw. Read by the boot banner and by the self-audit.</summary>
  public static bool InitFailed
  {
    get { return initThrew; }
  }

  // ---------------------------------------------------------------- the five IMod entry points ---
  //
  // EVERY ONE OF THEM IS GUARDED, because PugMod's own guard cannot be relied on. LoadedMods holds
  // ONE _hasPrintedException flag on a container shared by every loaded mod and all four callbacks
  // (ck-db/PugMod.Loader/PugMod/LoadedMods.cs:192), so the first exception from ANY mod is logged
  // and every one after it, from any mod, for the rest of the session, is discarded. Init is worse:
  // the loader marks the mod initialised BEFORE calling it (:101 then :104), so a throw there is
  // never retried and nothing says why.
  //
  // Update is the one that matters most. It drives the whole per-frame belt — bundle diagnostics,
  // the item report, Burst arming, atlas capture, scene-table injection, runtime loading,
  // generation, travel, player contexts, the return-portal wake and the persistence flush — so one
  // throw stops all of it, every frame, and may print nothing at all. It latches off after the
  // first, because sixty identical stack traces a second is not a diagnostic either.

  public void EarlyInit()
  {
    // Before anything else: the switches decide whether the rest of this session says anything.
    DimensionLogConfig.Load();

    try
    {
      EarlyInitInner();
    }
    catch (System.Exception exception)
    {
      DimensionLog.Fatal(
          DimensionLogChannels.Boot,
          null,
          "ExpandNullforge threw out of EarlyInit, so its service was not registered and no " +
          "dimension content can load this session. " + exception);
    }
  }

  public void Init()
  {
    try
    {
      InitInner();
    }
    catch (System.Exception exception)
    {
      initThrew = true;
      DimensionLog.Fatal(
          DimensionLogChannels.Boot,
          null,
          "ExpandNullforge threw out of Init, so it is not attached to any world. The mod loader " +
          "marks a mod initialised before calling Init, so this is never retried: restart the " +
          "game after fixing it. " + exception);
    }
  }

  public void Shutdown()
  {
    try
    {
      ShutdownInner();
    }
    catch (System.Exception exception)
    {
      DimensionLog.Fatal(
          DimensionLogChannels.Boot,
          null,
          "ExpandNullforge threw out of Shutdown, so some of its registries still hold the last " +
          "session's content. Reload the game rather than the mod. " + exception);
    }
    finally
    {
      DimensionLog.ResetAll();
      DimensionLogConfig.Reset();
      updateDisabled = false;
      initThrew = false;
    }
  }

  public void ModObjectLoaded(Object obj)
  {
    try
    {
      ModObjectLoadedInner(obj);
    }
    catch (System.Exception exception)
    {
      DimensionLog.Problem(
          DimensionLogChannels.Boot,
          null,
          "ExpandNullforge threw while taking an object the game handed it, so whatever that " +
          "object was did not register. " + exception);
    }
  }

  public void Update()
  {
    if (updateDisabled)
    {
      return;
    }

    try
    {
      UpdateInner();
    }
    catch (System.Exception exception)
    {
      updateDisabled = true;
      DimensionLog.Fatal(
          DimensionLogChannels.Boot,
          null,
          "ExpandNullforge threw out of Update and its whole per-frame belt has stopped: " +
          "scene-table injection, the item report, Burst arming, runtime loading, generation, " +
          "travel, player contexts and the persistence flush. Nothing else in the framework will " +
          "run this session. " + exception);
    }
  }

  private void EarlyInitInner()
  {
    if (API.Authoring != null)
    {
      API.Authoring.OnObjectTypeAdded -= DimensionPortalRecipeInjector.OnObjectTypeAdded;
      API.Authoring.OnObjectTypeAdded += DimensionPortalRecipeInjector.OnObjectTypeAdded;
    }

    DimensionApi.RegisterService(ProviderId, DimensionService);

    // THE SERVICE'S OWN DIAGNOSTIC RING HAD NO READER. Sixty-three call sites write into it with a
    // severity and a dimension id, and across the whole tree — runtime, editor and tests — nothing
    // subscribed to this event and nothing called any of the fifteen snapshot methods. One
    // subscription makes all sixty-three reachable without writing a single new call site. Errors
    // and warnings print as problems; the rest is detail on the service channel.
    DimensionService.DiagnosticEmitted -= DimensionLog.OnServiceDiagnostic;
    DimensionService.DiagnosticEmitted += DimensionLog.OnServiceDiagnostic;

    RegisterFrameworkGenerationProviders();
    DimensionLog.Milestone(
        DimensionLogChannels.Boot,
        null,
        "ExpandNullforge loaded. Detail channels: " + DimensionLogConfig.DescribeChannels() +
        ". Add -nfchannels portal,travel to the command line, or set channels in " +
        "diagnostics-channels.json, to see more.");
  }

  private void InitInner()
  {
    // Burst is NOT switched off here any more. Arming it costs every player who has this framework
    // installed, forever, whether or not they ever load custom content — so it now happens per world,
    // and only when there is content that needs it. See EnsureBurstDisabledForWorld.

    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterServerWorld;
      API.Server.OnWorldCreated += RegisterServerWorld;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
      API.Server.OnWorldDestroyed += OnServerWorldDestroyed;
    }

    if (API.Client != null)
    {
      API.Client.OnWorldCreated -= RegisterClientWorld;
      API.Client.OnWorldCreated += RegisterClientWorld;
      API.Client.OnWorldDestroyed -= OnClientWorldDestroyed;
      API.Client.OnWorldDestroyed += OnClientWorldDestroyed;
    }

    RegisterServerWorld();
    RegisterClientWorld();
    DimensionLog.Trace(DimensionLogChannels.Boot, null, "world callbacks registered.");
  }

  private void ShutdownInner()
  {
    // So a rebuilt mod is re-examined rather than inheriting the previous session's verdict.
    ExpandNullforge.Foundation.DimensionModBundleDiagnostics.Reset();

    // The mod loader clears its own world handles on reload, so a stale latch here would leave a
    // reloaded mod believing Burst was already switched off when it no longer is.
    burstArmedForContent = false;

    // Same reasoning: a reloaded mod must re-answer "did my items register?" rather than
    // inherit the previous session's silence.
    itemRegistryReported = false;
    lastSeenDeclarationVersion = -1;
    framesSinceLastDeclaration = 0;
    ExpandNullforge.Foundation.DimensionItemObjectRegistry.Clear();
    ExpandNullforge.Foundation.DimensionObjectLinkRegistry.Clear();
    ExpandNullforge.Foundation.DimensionObjectNames.ClearWarnings();

    if (API.Authoring != null)
    {
      API.Authoring.OnObjectTypeAdded -= DimensionPortalRecipeInjector.OnObjectTypeAdded;
    }

    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterServerWorld;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
    }

    if (API.Client != null)
    {
      API.Client.OnWorldCreated -= RegisterClientWorld;
      API.Client.OnWorldDestroyed -= OnClientWorldDestroyed;
    }

    OnServerWorldDestroyed();
    OnClientWorldDestroyed();
    SafePlatformGenerationProvider.ClearJobs();
    TileMapGenerationProvider.ClearJobs();
    ScenePlacementProvider.ClearJobs();
    OreScatterProvider.ClearJobs();
    DungeonPlacementProvider.ClearJobs();
    ExpandNullforge.Generation.DimensionOreVeinRuleRegistry.Clear();
    ExpandNullforge.Generation.DimensionOreBiomeGate.ClearAll();
    ExpandNullforge.Creatures.DimensionBossPresentationRegistry.Clear();
    ExpandNullforge.Creatures.DimensionCreaturePresentationRegistry.Clear();
    ExpandNullforge.WorldRules.DimensionUpgradeCostRegistry.Clear();
    ExpandNullforge.WorldRules.DimensionPlayerOverrideRegistry.Clear();
    ExpandNullforge.Creatures.DimensionBossRespawnRegistry.Clear();
    ExpandNullforge.Zones.DimensionMusicRosterRegistry.Clear();
    ExpandNullforge.Scenes.DimensionUniqueDungeonRegistry.Clear();
    ExpandNullforge.Scenes.DimensionUniqueDungeonInjector.ResetForNewSession();
    ExpandNullforge.Zones.DimensionAmbientSpawnPolicyCache.Clear();
    ExpandNullforge.Zones.DimensionMusicOverrideRegistry.Clear();
    ExpandNullforge.Portals.DimensionReturnPortalSpawnRegistry.DisarmAll();
    DimensionService.DiagnosticEmitted -= DimensionLog.OnServiceDiagnostic;
    DimensionApi.UnregisterService(ProviderId);
    DimensionPortalRecipeInjector.Reset();
    DimensionPortalObjectIdCache.Clear();
    DimensionLog.Trace(DimensionLogChannels.Boot, null, "ExpandNullforge unloaded.");
  }

  private void ModObjectLoadedInner(Object obj)
  {
    // Tileset assets shipped in the framework's own bundle register here; consumer-mod
    // tilesets register through the generated bootstrap's identical branch.
    ExpandNullforge.Authoring.DimensionTilesetAsset tilesetAsset =
        obj as ExpandNullforge.Authoring.DimensionTilesetAsset;
    if (tilesetAsset != null)
    {
      ExpandNullforge.Tilesets.DimensionTilesetAssetRuntime.Register(tilesetAsset);
    }

    // A condition has to claim its number BEFORE Core Keeper builds its condition table, and the
    // table is built as a world is converted. Loading is the only window there is.
    ExpandNullforge.Authoring.DimensionConditionAsset conditionAsset =
        obj as ExpandNullforge.Authoring.DimensionConditionAsset;
    if (conditionAsset != null)
    {
      ExpandNullforge.Conditions.DimensionConditionAssetRuntime.Register(conditionAsset);
    }
  }

  private void UpdateInner()
  {
    // A mod whose build produced two asset bundles loses every DataBlock in the second one, because
    // the engine keys the DataBlock loader by mod rather than by bundle. Nothing at authoring time
    // can see that — bundles are a build output — so it is checked here, once, against the loaded
    // mod list. Self-latching; costs one null check per frame afterwards.
    ExpandNullforge.Foundation.DimensionModBundleDiagnostics.ReportMultiBundleModsOnce();

    // The item registry knows which declared items never registered with the game, and until
    // now nobody ever asked it: every item id a content pack declared was recorded and the
    // answer thrown away. Once a world exists, content loading is done, so this is the moment
    // the question has an answer. RefreshAll re-checks the stragglers and ReportMissing names
    // whatever is still absent once per id, so a per-frame call cannot spam the console.
    //
    // IT WAITS FOR DECLARATIONS TO STOP ARRIVING, which the first version did not. The Declare
    // call is emitted into the generated ApplyManifests, behind a service gate with its own
    // across-frames retry, so it can easily land AFTER the first frame a world exists — and a
    // report that fired first read an empty ledger and latched, so nothing was ever reported.
    // A new declaration re-arms the wait; the report goes out once nothing has changed for a
    // few frames.
    if (!itemRegistryReported && (registeredServerWorld != null || registeredClientWorld != null))
    {
      int version = ExpandNullforge.Foundation.DimensionItemObjectRegistry.DeclarationVersion;
      if (version != lastSeenDeclarationVersion)
      {
        lastSeenDeclarationVersion = version;
        framesSinceLastDeclaration = 0;
      }
      else if (framesSinceLastDeclaration < FramesToWaitAfterLastDeclaration)
      {
        framesSinceLastDeclaration++;
      }
      else
      {
        itemRegistryReported = true;
        ExpandNullforge.Foundation.DimensionItemObjectRegistry.RefreshAll();
        ExpandNullforge.Foundation.DimensionItemObjectRegistry.ReportMissing();
      }
    }

    // Content normally finishes loading before any world exists, so the check at world creation is
    // the one that fires. This is the safety net for the reverse order — a mod that registers a
    // tileset or a portal item late would otherwise find Burst still on and lose its tile writes
    // silently. Self-latching; one bool per frame once armed, and nothing at all if no world is up.
    if (!burstArmedForContent && (registeredServerWorld != null || registeredClientWorld != null))
    {
      EnsureBurstDisabledForWorld(registeredServerWorld);
      EnsureBurstDisabledForWorld(registeredClientWorld);
    }

    // Core Keeper's real adaptive tileset lookup isn't shipped to the SDK, so it has to be read
    // in-game and baked into DimensionTilesetAtlasData. That bake is done, so the capture stays
    // quiet — re-dumping the whole atlas into the player log every launch when we already have the
    // answer buries everything else. It runs again only if the bake ever comes up empty, which is
    // what a Core Keeper update that invalidates the layout would look like.
    if (!ExpandNullforge.Tilesets.DimensionTilesetAtlas.IsReady)
    {
      ExpandNullforge.Tilesets.DimensionTilesetAtlasCapture.TryCaptureOnce();
    }

    if (DimensionService.HasAttachedServerWorld)
    {
      // THE INJECTION BELT. The dungeon room generator caches the scene table blob ONCE, in
      // its OnStartRunning — if our scenes are not in the table by the time the first dungeon
      // entity exists, every custom room resolves no scene and dungeons generate as empty
      // caves, with nothing logged. The world-gen Harmony patch covers worlds that still
      // generate; this covers the ones that don't. Idempotent per world: after the first
      // success this is one reference compare per frame, and with no custom scenes at all it
      // is one integer compare.
      ExpandNullforge.Scenes.DimensionCustomSceneTableInjector.TryInject(registeredServerWorld);

      TryInitializeServerWorldPersistence();

      if (DimensionService.HasActiveRuntimeLoadingWork)
      {
        DimensionService.UpdateRuntimeLoading();
      }

      if (DimensionService.HasActiveRuntimeGenerationWork)
      {
        DimensionService.UpdateRuntimeGeneration();
      }

      if (DimensionService.HasActiveRuntimeTravelWork)
      {
        DimensionService.UpdateRuntimeTravel();
      }

      if (DimensionService.ShouldRunRuntimePlayerContextTracking ||
          DimensionReturnPortalSpawnRegistry.Count > 0)
      {
        DimensionService.UpdateRuntimePlayerContexts();
      }

      WakeReturnPortalSpawnerIfNeeded();

      if (DimensionService.ShouldFlushPersistence)
      {
        DimensionWorldRegistry.FlushIfDue();
      }
    }

    if (DimensionService.HasAttachedClientWorld)
    {
      DimensionCoordinatePresentation.EnsureAttached();
      DimensionTravelNetworkState.UpdateDeferredClientRequests();

      if (DimensionPlayerContextNetworkState.IsCurrentContextHydrationActive)
      {
        DimensionPlayerContextNetworkState.UpdateCurrentContextHydration();
      }
    }
  }

  /// <summary>
  /// Switches Burst off for <c>EquipmentUpdateSystem</c> in one world, if this session has content
  /// that needs it.
  /// </summary>
  /// <remarks>
  /// <para>
  /// BOTH CALLS ARE REQUIRED, and each fails differently on its own.
  /// <c>DisableBurstForSystem&lt;T&gt;</c> registers the system TYPE; for an unmanaged system the
  /// disabler still has to resolve that type to each world's own SystemHandle, which is what
  /// <c>AddWorld</c> does. A world nobody registers keeps running the system Burst-compiled, which
  /// silently defeats every managed patch on the placement path: on a dedicated server the tile write
  /// reached vanilla's untouched <c>EntityUtility.AddTile</c>, which rejects any tileset id above 74
  /// ("Trying to add invalid tileset 45378 for tileType 35"), so placed custom blocks were never
  /// created server-side and the client's prediction was simply corrected away.
  /// </para>
  /// <para>
  /// Both are idempotent — the disabler keeps types and handles in HashSets — and both are safe this
  /// late, because <c>AddWorld</c> reads the registered types at call time and a world is created
  /// before its systems first update.
  /// </para>
  /// </remarks>
  private static void EnsureBurstDisabledForWorld(World world)
  {
    if (world == null || !world.IsCreated)
    {
      return;
    }

    if (!NeedsUnburstedEquipmentUpdate())
    {
      // Nothing needs it yet. Content that loads later is caught by the latch in Update.
      return;
    }

    // Registers the TYPE (installing the process-wide detour on the first call anywhere) and then
    // resolves it to this world's handle. Both have to happen, and doing them together here is what
    // keeps a content-free session from paying for either.
    BurstDisabler.DisableBurstForSystem<EquipmentUpdateSystem>();
    BurstDisabler.AddWorld(world);
    burstArmedForContent = true;
    DimensionLog.Milestone(
        DimensionLogChannels.Tileset,
        world,
        world.Name + " has custom content on the placement path, so " +
        "EquipmentUpdateSystem runs un-Bursted there.");
  }

  /// <summary>
  /// Whether anything is loaded that actually needs the equipment update to run un-Bursted.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Four of Core Keeper's calls into <c>EntityUtility.AddTile</c> can carry one of our tileset ids
  /// — placing a block, tilling, watering, and emptying a bucket — and all four sit inside the
  /// Burst-compiled equipment job, where a Harmony patch is never reached. Switching Burst off for
  /// that one system is what lets our patches run. The item portal rides the same seam.
  /// </para>
  /// <para>
  /// The cost is not confined to this system, which is why the question is asked at all. The first
  /// <c>DisableBurstFor*</c> call ANYWHERE in the process installs a Harmony prefix and postfix on
  /// <c>Unity.Entities.WorldUnmanagedImpl.UpdateSystem</c> — the dispatch point for every unmanaged
  /// system in every world — and only a domain reload takes it back off. A player who installs this
  /// framework and then spends the evening in a vanilla world should not pay for that, so the
  /// question is asked per world, once content has finished loading.
  /// </para>
  /// </remarks>
  private static bool NeedsUnburstedEquipmentUpdate()
  {
    System.Collections.Generic.IReadOnlyCollection<ExpandNullforge.Tilesets.DimensionCustomTileset>
        tilesets = ExpandNullforge.Tilesets.DimensionTilesetRegistry.All;
    if (tilesets != null && tilesets.Count > 0)
    {
      return true;
    }

    System.Collections.Generic.IReadOnlyCollection<string> itemPortals =
        ExpandNullforge.Portals.DimensionItemPortalRegistry.RegisteredItemNames;
    return itemPortals != null && itemPortals.Count > 0;
  }

  private void RegisterServerWorld()
  {
    if (API.Server == null ||
        API.Server.World == null ||
        !API.Server.World.IsCreated)
    {
      return;
    }

    World world = API.Server.World;
    if (registeredServerWorld == world)
    {
      return;
    }

    registeredServerWorld = world;
    serverWorldPersistenceInitialized = false;
    EnsureBurstDisabledForWorld(world);
    // Created before the ordering call below, which looks the capture system up with
    // GetExistingSystemManaged and only WARNS when it is missing — so on auto-creation
    // the failure mode is custom tiles silently not surviving a reload.
    world.GetOrCreateSystemManaged<ExpandNullforge.Tilesets.DimensionCustomTileCaptureSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Tilesets.DimensionCustomTileRestoreSystem>();
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.EnsureSystemOrdering(world);
    world.GetOrCreateSystemManaged<DimensionTravelServerRpcSystem>();
    world.GetOrCreateSystemManaged<DimensionPlayerContextServerRpcSystem>();
    DimensionReturnPortalSpawnSystem returnPortalSpawnSystem =
        world.GetOrCreateSystemManaged<DimensionReturnPortalSpawnSystem>();
    returnPortalSpawnSystem.Enabled = false;
    this.returnPortalSpawnSystem = returnPortalSpawnSystem;
    nextReturnPortalSpawnerWakeProbeAt = 0.0d;
    world.GetOrCreateSystemManaged<DimensionPortalHydrationSystem>();
    world.GetOrCreateSystemManaged<DimensionPortalChargeSystem>();
    world.GetOrCreateSystemManaged<DimensionPortalActivationSystem>();
    world.GetOrCreateSystemManaged<DimensionItemPortalSpawnSystem>();

    // Explicit creation is the liveness guarantee for every framework system — auto-creation
    // of mod-assembly systems is exactly the kind of thing that works on one loader version
    // and silently stops on the next, and a system that never runs fails without a log line.
    // The boss phase system had precisely that gap: written, tested, referenced by nothing.
    world.GetOrCreateSystemManaged<ExpandNullforge.Zones.DimensionBossPhaseSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Creatures.DimensionBossMarkerHydrationSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Creatures.DimensionSummonHydrationSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Creatures.DimensionBossRespawnSystem>();
    // What makes a Defensive creature real. The zero that stops it hunting has to be written
    // at RUNTIME, not baked: the prefab's chase distance also sizes the path search, so a
    // zero on the asset leaves the creature unable to find a path to anything — and a
    // creature that needs a path to chase then deadlocks and never fights back at all.
    world.GetOrCreateSystemManaged<ExpandNullforge.Creatures.DimensionHoldsFireSystem>();
    // The skill experience a mod's own creature is worth. The registry filled, the tests for it
    // passed, and this line was never written — so the system that reads the registry never
    // ticked and no kill ever paid out. Server only; the buffers it writes are the server's.
    world.GetOrCreateSystemManaged<ExpandNullforge.Skills.DimensionSkillXpSystem>();
    // This was relying on ECS auto-creation, which is precisely what the note above says
    // never to trust: the offering system is what consumes what a player puts into a portal's
    // window, so a load order that skipped it would take the items and open nothing.
    world.GetOrCreateSystemManaged<ExpandNullforge.Portals.DimensionPortalOfferingSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Zones.DimensionAmbientSpawnGateSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Arenas.DimensionArenaResetSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Arenas.DimensionArenaVictorySystem>();
    // The biome-identity heartbeat: writes player.currentBiome for custom biome ints, which
    // is what the game's own title card, discovery list, map naming and biome music key off.
    // It sat written and tested with no creation call — every one of those features dead.
    world.GetOrCreateSystemManaged<ExpandNullforge.Zones.DimensionCurrentBiomeSystem>();
    // Crops: the roll decides which version a planting becomes, and the sprout system carries
    // that decision across the moment the seed turns into a plant. The game's own grow job
    // collapses anything but its one authored golden pair, so without these two a rarer
    // version would be rolled and then quietly lost on sprouting.
    world.GetOrCreateSystemManaged<ExpandNullforge.Plants.DimensionCropTierRollSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Plants.DimensionCropTierSproutSystem>();
    // Cooking reads ingredient names off the prefab, and a name only becomes an ObjectID once
    // the world exists. Without this the pot refuses every custom ingredient.
    world.GetOrCreateSystemManaged<ExpandNullforge.Food.DimensionFoodIngredientHydrationSystem>();
    // The damage half of tile hazards (burn/poison/drench conditions); presentation was
    // wired, this half rode auto-creation roulette.
    world.GetOrCreateSystemManaged<ExpandNullforge.Tilesets.DimensionHazardConditionSystem>();
    // Fires scene-authored triggered tiles — traps, pressure plates. The system sat fully
    // written with no creation call and no registrations; the scene placement pass now arms
    // tiles as it stamps scenes, and this line is what makes them go off.
    world.GetOrCreateSystemManaged<ExpandNullforge.Zones.DimensionTriggeredTileSystem>();
    // A bomb whose blast is one of the mod's own objects carries a name, not an id, until
    // these run; without them CreateExplosion spawns nothing and returns without a log.
    world.GetOrCreateSystemManaged<ExpandNullforge.Explosives.DimensionExplosiveHydrationSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Explosives.DimensionBlastFireSystem>();
    // Every reference in the mod's content that names one of the mod's OWN objects — the shot a bow
    // fires, the chest a boss leaves, what a container turns into. The editor had no number for
    // those, so the prefab carries None until this runs. Created in both worlds because the
    // system is declared for both; see its remarks for why one filter rather than one per field.
    world.GetOrCreateSystemManaged<ExpandNullforge.Foundation.DimensionObjectLinkHydrationSystem>();
    DimensionService.SetServerWorld(world);
    RegisterFrameworkGenerationProviders();
    TryInitializeServerWorldPersistence();
    DimensionLog.Milestone(
        DimensionLogChannels.World,
        world,
        "bound to the server world. Dimensions, travel, generation and persistence are live here.");
  }

  private static void RegisterFrameworkGenerationProviders()
  {
    RegisterGenerationProvider(SafePlatformGenerationProvider, "safe-platform");
    RegisterGenerationProvider(TileMapGenerationProvider, "tile-map");
    RegisterGenerationProvider(ScenePlacementProvider, "scene-placement");
    RegisterGenerationProvider(OreScatterProvider, "ore-scatter");
    RegisterGenerationProvider(DungeonPlacementProvider, "dungeon-placement");
  }

  private static void RegisterGenerationProvider(
      IDimensionGenerationProvider provider,
      string label)
  {
    if (!DimensionService.TryRegisterGenerationProvider(provider, out DimensionOperationResult result) &&
        !string.Equals(result.Code, "generation-provider-duplicate", System.StringComparison.Ordinal))
    {
      DimensionLog.Problem(
          DimensionLogChannels.Generate,
          null,
          "could not register the " + label + " generation provider: " +
          result.Message);
    }
  }

  private void WakeReturnPortalSpawnerIfNeeded()
  {
    if (registeredServerWorld == null || !registeredServerWorld.IsCreated)
    {
      return;
    }

    if (returnPortalSpawnSystem == null)
    {
      returnPortalSpawnSystem =
          registeredServerWorld.GetOrCreateSystemManaged<DimensionReturnPortalSpawnSystem>();
    }

    if (returnPortalSpawnSystem.Enabled)
    {
      return;
    }

    if (DimensionService.HasActiveRuntimeTravelWork ||
        DimensionService.HasActiveRuntimeGenerationWork)
    {
      returnPortalSpawnSystem.Enabled = true;
      return;
    }

    double now = Time.realtimeSinceStartupAsDouble;
    if (now < nextReturnPortalSpawnerWakeProbeAt)
    {
      return;
    }

    nextReturnPortalSpawnerWakeProbeAt = now + ReturnPortalSpawnerWakeProbeSeconds;
    if (!HasTrackedPlayerInAnyReturnPortalSource(DimensionService))
    {
      return;
    }

    returnPortalSpawnSystem.Enabled = true;
  }

  private static bool HasTrackedPlayerInAnyReturnPortalSource(
      IDimensionRuntimeStateService runtimeState)
  {
    if (runtimeState == null)
    {
      return false;
    }

    for (int i = 0; i < DimensionReturnPortalSpawnRegistry.Count; i++)
    {
      DimensionReturnPortalSpawnDefinition definition;
      if (!DimensionReturnPortalSpawnRegistry.TryGet(i, out definition) ||
          !definition.IsValid)
      {
        continue;
      }

      if (runtimeState.HasTrackedPlayerInDimension(definition.SourceDimensionId))
      {
        return true;
      }
    }

    return false;
  }

  private void RegisterClientWorld()
  {
    if (API.Client == null ||
        API.Client.World == null ||
        !API.Client.World.IsCreated)
    {
      return;
    }

    World world = API.Client.World;
    if (registeredClientWorld == world)
    {
      return;
    }

    registeredClientWorld = world;
    EnsureBurstDisabledForWorld(world);
    // Created before the ordering call below, which looks the capture system up with
    // GetExistingSystemManaged and only WARNS when it is missing — so on auto-creation
    // the failure mode is custom tiles silently not surviving a reload. These two sat
    // BELOW the early return above and so had never once run on a client.
    world.GetOrCreateSystemManaged<ExpandNullforge.Tilesets.DimensionCustomTileCaptureSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Tilesets.DimensionCustomTileRestoreSystem>();
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.EnsureSystemOrdering(world);
    world.GetOrCreateSystemManaged<DimensionTravelClientRpcSystem>();
    world.GetOrCreateSystemManaged<DimensionPlayerContextClientRpcSystem>();
    world.GetOrCreateSystemManaged<DimensionPortalMapMarkerScopeSystem>();
    // The marker's MapMarkerCD never replicates, so the client hydrates its own copy — a
    // server-only write would leave every client pin idless and icon-less forever.
    world.GetOrCreateSystemManaged<ExpandNullforge.Creatures.DimensionBossMarkerHydrationSystem>();
    // Declared for BOTH simulations: the client needs its own currentBiome for the title
    // card and music, which read local player state, never a replicated value.
    world.GetOrCreateSystemManaged<ExpandNullforge.Zones.DimensionCurrentBiomeSystem>();
    // The client resolves ingredient names too: the pot's preview slot and the cook book work
    // off client-side lookups, so without this they would disagree with what the pot produces.
    world.GetOrCreateSystemManaged<ExpandNullforge.Food.DimensionFoodIngredientHydrationSystem>();
    // Declared for both simulations, so it is created in both rather than left to auto-creation.
    world.GetOrCreateSystemManaged<ExpandNullforge.Portals.DimensionPortalOfferingSystem>();
    // A bomb whose blast is one of the mod's own objects carries a name, not an id, until
    // these run; without them CreateExplosion spawns nothing and returns without a log.
    world.GetOrCreateSystemManaged<ExpandNullforge.Explosives.DimensionExplosiveHydrationSystem>();
    world.GetOrCreateSystemManaged<ExpandNullforge.Explosives.DimensionBlastFireSystem>();
    // The client half of the same link hydration. Not optional: the recipe book and a weapon's
    // predicted shot are read on the client off its own copy of the prefab, so a client left
    // holding None disagrees with the server about what the item does.
    world.GetOrCreateSystemManaged<ExpandNullforge.Foundation.DimensionObjectLinkHydrationSystem>();
    DimensionTravelFeedbackState.Initialize();
    DimensionService.SetClientWorld(world);
    DimensionPlayerContextNetworkState.StartCurrentContextHydration(
        true,
        "Client world attached; hydrate current dimension context.");
    DimensionCoordinatePresentation.EnsureAttached();
    DimensionLog.Milestone(
        DimensionLogChannels.World,
        world,
        "bound to the client world. Travel feedback and the coordinate readout are live here.");
  }

  private void OnServerWorldDestroyed()
  {
    // So a player who quits to the menu, fixes something and comes back is told the same things
    // again. Every ad-hoc "say this once" store in the framework is process-static, which is why
    // the two most useful tileset lines never appear on a second load today.
    DimensionLog.ResetForWorld(registeredServerWorld);
    DimensionPortalRecipeInjector.ClearWorldState("server world destroyed");
    DimensionPortalObjectIdCache.Clear();
    // Only this world's records — on a host the client world is still live and still needs its own.
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.Clear(registeredServerWorld);
    DimensionPortalRuntime.Reset();
    DimensionService.PreparePersistenceForWorldUnload("server world destroyed");
    registeredServerWorld = null;
    returnPortalSpawnSystem = null;
    nextReturnPortalSpawnerWakeProbeAt = 0.0d;
    serverWorldPersistenceInitialized = false;
    DimensionService.ClearServerWorld();
    DimensionWorldRegistry.ResetLoadedState();
  }

  private void OnClientWorldDestroyed()
  {
    DimensionLog.ResetForWorld(registeredClientWorld);
    DimensionPortalRecipeInjector.ClearWorldState("client world destroyed");
    // Only this world's records — on a host the server world is still live and still needs its own.
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.Clear(registeredClientWorld);
    DimensionPortalRuntime.Reset();
    registeredClientWorld = null;
    DimensionService.ClearClientWorld();
    DimensionTravelFeedbackState.Reset();
    DimensionTravelNetworkState.Reset();
    DimensionPlayerContextNetworkState.Reset();
    DimensionCoordinatePresentation.Reset();
  }

  private void TryInitializeServerWorldPersistence()
  {
    if (serverWorldPersistenceInitialized ||
        registeredServerWorld == null ||
        !registeredServerWorld.IsCreated)
    {
      return;
    }

    DimensionWorldRegistry.EnsureLoadedForCurrentWorld();
    if (!DimensionWorldRegistry.IsLoaded)
    {
      return;
    }

    DimensionService.LoadPersistedDefinitionsForCurrentWorld();
    serverWorldPersistenceInitialized = true;
    DimensionLog.Milestone(
        DimensionLogChannels.Persist,
        registeredServerWorld,
        "persistence is open for world=" +
        DimensionWorldRegistry.WorldKey +
        ".");
  }
}
