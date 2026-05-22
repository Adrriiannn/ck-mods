using Pug.ECS.Components;
using Pug.ECS.Hybrid;
using Pug.Sprite;
using Pug.Automation;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public sealed class SmartSplitterFilterPanelHost : MonoBehaviour
{
  private static SmartSplitterFilterPanelHost _instance;

  private SmartSplitterFilterPanelController _panel;
  private World _highlightTargetWorld;
  private World _graphicalWorld;
  private CreateGraphicalObjectSystem _graphicalObjectSystem;
  private Entity _highlightedSplitter = Entity.Null;
  private World _panelTargetWorld;
  private Entity _panelTargetSplitter = Entity.Null;
  private bool _panelOpenedInventory;
  private bool _inventoryWasShowingBeforePanel;
  private bool _loggedWaitingForPrefab;
  private bool _isPlayingInteractHint;

  public static void EnsureExists()
  {
    if (_instance != null)
    {
      return;
    }

    GameObject hostObject = new GameObject("SmartSplitterFilterPanelHost");
    DontDestroyOnLoad(hostObject);
    _instance = hostObject.AddComponent<SmartSplitterFilterPanelHost>();
  }

  private void Update()
  {
    if (!SmartSplitterDebugSettings.EnableSmartSplitterPanel)
    {
      if (_panel != null)
      {
        HidePanel();
      }

      return;
    }

    EnsurePanelInstance();

    if (_panel == null)
    {
      return;
    }

    bool hasLookedAtSplitter = SmartSplitterLaneFilterUtility.TryFindLookedAtSmartSplitter(
        out World lookedAtWorld,
        out Entity lookedAtSplitter,
        out _,
        SmartSplitterDebugSettings.SmartSplitterPanelInteractionRadius,
        SmartSplitterDebugSettings.SmartSplitterPanelAimLineRadius);
    bool hasPoweredLookedAtSplitter =
        hasLookedAtSplitter && IsSmartSplitterPowered(lookedAtWorld, lookedAtSplitter);

    UpdateHighlight(hasPoweredLookedAtSplitter ? lookedAtWorld : null, lookedAtSplitter);

    if (_panel.IsShowing &&
        (!IsSmartSplitterPowered(_panelTargetWorld, _panelTargetSplitter) || ShouldClosePanel()))
    {
      HidePanel();
      return;
    }

    if (ShouldTogglePanel())
    {
      if (_panel.IsShowing)
      {
        HidePanel();
      }
      else if (hasPoweredLookedAtSplitter)
      {
        ShowPanel(lookedAtWorld, lookedAtSplitter);
      }
    }
  }

  private void EnsurePanelInstance()
  {
    if (_panel != null)
    {
      return;
    }

    if (!SmartSplitterAssetRegistry.TryGetSmartSplitterPanelPrefab(out GameObject panelPrefab) ||
        panelPrefab == null)
    {
      if (!_loggedWaitingForPrefab)
      {
        Debug.Log("[SmartSplitterFilterPanelHost] Waiting for SmartSplitterPanel prefab");
        _loggedWaitingForPrefab = true;
      }

      return;
    }

    _loggedWaitingForPrefab = false;

    GameObject panelObject = Instantiate(panelPrefab);
    panelObject.name = "SmartSplitterPanelRuntime";
    DontDestroyOnLoad(panelObject);

    _panel = panelObject.GetComponent<SmartSplitterFilterPanelController>();
    if (_panel == null)
    {
      _panel = panelObject.AddComponent<SmartSplitterFilterPanelController>();
    }

    _panel.Hide();
  }

  private bool ShouldTogglePanel()
  {
    if (Input.GetKeyDown(SmartSplitterDebugSettings.SmartSplitterPanelToggleKey))
    {
      return true;
    }

    if (Manager.main == null ||
        Manager.main.player == null ||
        Manager.main.player.inputModule == null ||
        Manager.input == null ||
        Manager.input.textInputWasActiveThisFrame)
    {
      return false;
    }

    return Manager.main.player.inputModule.WasButtonPressedDownThisFrame(
               PlayerInput.InputType.INTERACT_WITH_OBJECT,
               false);
  }

  private bool ShouldClosePanel()
  {
    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
    {
      return true;
    }

    if (Manager.main == null ||
        Manager.main.player == null ||
        Manager.main.player.inputModule == null)
    {
      return false;
    }

    PlayerInput input = Manager.main.player.inputModule;
    return input.WasButtonPressedDownThisFrame(PlayerInput.InputType.CANCEL, false) ||
           input.WasButtonPressedDownThisFrame(PlayerInput.InputType.TOGGLE_INVENTORY, false);
  }

  private void ShowPanel(World world, Entity splitter)
  {
    _inventoryWasShowingBeforePanel = Manager.ui != null && Manager.ui.isAnyInventoryShowing;
    _panelOpenedInventory = true;

    if (Manager.ui != null)
    {
      Manager.ui.HideAllInventoryAndCraftingUI(false);

      if (Manager.ui.mapUI != null && Manager.ui.mapUI.IsShowingBigMap)
      {
        Manager.ui.OnMapToggle();
      }

      Manager.ui.inventoryButton.HideLightUpHint();
      Manager.ui.playerInventoryUI.ShowContainerUI();
      Manager.ui.trashCanUI.ShowContainerUI();
      Manager.ui.characterWindow.Hide();

      if (Manager.ui.activeCraftingUI != null)
      {
        Manager.ui.activeCraftingUI.HideCraftingUI();
      }

      Manager.ui.creativeModeOptionsUI.Hide();
      Manager.ui.creativeModeUI.HideContainerUI();
    }

    _panel.Show(world, splitter);
    _panelTargetWorld = world;
    _panelTargetSplitter = splitter;
  }

  private void HidePanel()
  {
    if (_panel != null)
    {
      _panel.Hide();
    }

    if (_panelOpenedInventory && !_inventoryWasShowingBeforePanel && Manager.ui != null)
    {
      Manager.ui.HideAllInventoryAndCraftingUI(false);
    }

    _panelOpenedInventory = false;
    _panelTargetWorld = null;
    _panelTargetSplitter = Entity.Null;
  }

  private void UpdateHighlight(World world, Entity splitter)
  {
    if (_highlightedSplitter == splitter && _highlightTargetWorld == world)
    {
      UpdateInteractHint(splitter != Entity.Null);
      return;
    }

    SetSplitterOutline(_highlightTargetWorld, _highlightedSplitter, false);

    _highlightTargetWorld = world;
    _highlightedSplitter = splitter;

    SetSplitterOutline(_highlightTargetWorld, _highlightedSplitter, true);
    UpdateInteractHint(splitter != Entity.Null);
  }

  private void OnDisable()
  {
    SetSplitterOutline(_highlightTargetWorld, _highlightedSplitter, false);
    UpdateInteractHint(false);
    _highlightTargetWorld = null;
    _highlightedSplitter = Entity.Null;
  }

  private void UpdateInteractHint(bool show)
  {
    if (Manager.ui == null || Manager.ui.interactHintButton == null)
    {
      _isPlayingInteractHint = false;
      return;
    }

    if (show && !_isPlayingInteractHint)
    {
      _isPlayingInteractHint = true;
      Manager.ui.interactHintButton.ShowLightUpHint();
    }
    else if (!show && _isPlayingInteractHint)
    {
      _isPlayingInteractHint = false;
      Manager.ui.interactHintButton.HideLightUpHint();
    }
  }

  private void SetSplitterOutline(World world, Entity splitter, bool show)
  {
    if (world == null ||
        !world.IsCreated ||
        splitter == Entity.Null ||
        !world.EntityManager.Exists(splitter))
    {
      return;
    }

    if (!TryGetGraphicalObject(world, splitter, out GameObject graphicalObject) || graphicalObject == null)
    {
      return;
    }

    SpriteObject[] spriteObjects = graphicalObject.GetComponentsInChildren<SpriteObject>(true);
    Color color = show && Manager.effects != null
        ? Manager.effects.outlineColor
        : new Color(0.0f, 0.0f, 0.0f, 0.0f);

    for (int i = 0; i < spriteObjects.Length; i++)
    {
      if (spriteObjects[i] != null)
      {
        spriteObjects[i].outlineColor = color;
      }
    }
  }

  private bool TryGetGraphicalObject(World serverWorld, Entity serverSplitter, out GameObject graphicalObject)
  {
    graphicalObject = null;

    if (!TryEnsureGraphicalSystem())
    {
      return false;
    }

    if (_graphicalObjectSystem.GameObjectLookup.TryGetValue(serverSplitter, out graphicalObject))
    {
      return true;
    }

    EntityManager serverEntityManager = serverWorld.EntityManager;
    if (!serverEntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(serverSplitter) ||
        !SmartSplitterLaneFilterUtility.TryGetSplitterCenter(
            serverEntityManager,
            serverEntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(serverSplitter),
            out Unity.Mathematics.int2 center))
    {
      return false;
    }

    if (_graphicalWorld == null || !_graphicalWorld.IsCreated)
    {
      return false;
    }

    EntityManager graphicalEntityManager = _graphicalWorld.EntityManager;
    EntityQuery query = graphicalEntityManager.CreateEntityQuery(
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadOnly<LocalTransform>());

    using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
    using NativeArray<ObjectDataCD> objectData = query.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
    query.Dispose();

    for (int i = 0; i < entities.Length; i++)
    {
      if (objectData[i].objectID != ObjectID.ConveyorBeltSplitter)
      {
        continue;
      }

      int x = Mathf.RoundToInt(transforms[i].Position.x);
      int y = Mathf.RoundToInt(transforms[i].Position.z);

      if (x != center.x || y != center.y)
      {
        continue;
      }

      return _graphicalObjectSystem.GameObjectLookup.TryGetValue(entities[i], out graphicalObject);
    }

    return false;
  }

  private bool TryEnsureGraphicalSystem()
  {
    if (_graphicalWorld != null &&
        _graphicalWorld.IsCreated &&
        _graphicalObjectSystem != null)
    {
      return true;
    }

    _graphicalWorld = null;
    _graphicalObjectSystem = null;

    if (Manager.ecs != null &&
        TryGetGraphicalSystem(Manager.ecs.ClientWorld, out _graphicalObjectSystem))
    {
      _graphicalWorld = Manager.ecs.ClientWorld;
      return true;
    }

    foreach (World world in World.All)
    {
      if (TryGetGraphicalSystem(world, out _graphicalObjectSystem))
      {
        _graphicalWorld = world;
        return true;
      }
    }

    return false;
  }

  private static bool TryGetGraphicalSystem(World world, out CreateGraphicalObjectSystem graphicalSystem)
  {
    graphicalSystem = null;

    if (world == null || !world.IsCreated)
    {
      return false;
    }

    graphicalSystem = world.GetExistingSystemManaged<CreateGraphicalObjectSystem>();
    return graphicalSystem != null;
  }

  private bool IsSmartSplitterPowered(World world, Entity splitter)
  {
    if (world == null ||
        !world.IsCreated ||
        splitter == Entity.Null)
    {
      return false;
    }

    EntityManager entityManager = world.EntityManager;
    if (!entityManager.Exists(splitter) ||
        !entityManager.HasComponent<SmartSplitterOriginalOutputsCD>(splitter))
    {
      return false;
    }

    SmartSplitterOriginalOutputsCD originals =
        entityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(splitter);
    if (!entityManager.Exists(originals.LeftMoverEntity) ||
        !entityManager.Exists(originals.RightMoverEntity) ||
        !entityManager.HasComponent<MoverCD>(originals.LeftMoverEntity) ||
        !entityManager.HasComponent<MoverCD>(originals.RightMoverEntity))
    {
      return false;
    }

    MoverCD leftMover = entityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
    MoverCD rightMover = entityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);
    int splitterX = Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f);
    int splitterY = Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f);

    EntityQuery electricityQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ElectricityCD>(),
        ComponentType.ReadOnly<LocalTransform>()
      },
      None = new[]
      {
        ComponentType.ReadOnly<EntityDestroyedCD>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    using NativeArray<ElectricityCD> electricityData =
        electricityQuery.ToComponentDataArray<ElectricityCD>(Allocator.Temp);
    using NativeArray<LocalTransform> electricityTransforms =
        electricityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
    electricityQuery.Dispose();

    for (int i = 0; i < electricityData.Length; i++)
    {
      ElectricityCD electricity = electricityData[i];
      if (!electricity.hasEnoughElectricityToPowerStuff &&
          electricity.sourceEnergy <= 0)
      {
        continue;
      }

      LocalTransform transform = electricityTransforms[i];
      int powerX = Mathf.RoundToInt(transform.Position.x);
      int powerY = Mathf.RoundToInt(transform.Position.z);
      int dx = Mathf.Abs(powerX - splitterX);
      int dy = Mathf.Abs(powerY - splitterY);
      if ((dx == 0 && dy == 0) ||
          (dx == 1 && dy == 0) ||
          (dx == 0 && dy == 1))
      {
        return true;
      }
    }

    return false;
  }
}
