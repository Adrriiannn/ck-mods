using System;
using System.Collections.Generic;
using Pug.Automation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ChunkLoaderRuntimeSystem))]
public partial class ChunkLoaderTelemetrySystem : SystemBase
{
  private sealed class ObservedEntity
  {
    public Entity Entity;
    public ulong RegistrationId;
    public int ObjectId;
    public int Amount;
    public int Health;
    public int2 Position;
    public bool Enemy;
    public bool Player;
  }

  private readonly struct ItemIdentity : IEquatable<ItemIdentity>
  {
    public ItemIdentity(ObjectID objectId, int variation)
    {
      ObjectId = objectId;
      Variation = variation;
    }

    public readonly ObjectID ObjectId;
    public readonly int Variation;

    public bool Equals(ItemIdentity other)
    {
      return ObjectId == other.ObjectId &&
             Variation == other.Variation;
    }

    public override bool Equals(object obj)
    {
      return obj is ItemIdentity other && Equals(other);
    }

    public override int GetHashCode()
    {
      return HashCode.Combine((int)ObjectId, Variation);
    }
  }

  private sealed class ObservedInventory
  {
    public Entity Entity;
    public ulong RegistrationId;
    public int2 Position;
    public string Label;
    public bool IsGroundItem;
    public string PickupTargetLabel;
    public readonly Dictionary<ItemIdentity, int> Items = new();
  }

  private sealed class InventoryDelta
  {
    public ObservedInventory Inventory;
    public ItemIdentity Item;
    public int Amount;
  }

  private readonly Dictionary<long, ObservedEntity> _previous = new();
  private readonly Dictionary<long, ObservedEntity> _current = new();
  private readonly Dictionary<long, ObservedInventory> _previousInventories =
      new();
  private readonly Dictionary<long, ObservedInventory> _currentInventories =
      new();
  private readonly List<InventoryDelta> _inventoryLosses = new();
  private readonly List<InventoryDelta> _inventoryGains = new();
  private readonly List<ChunkLoaderRegistrationRecord> _records = new();
  private readonly HashSet<ulong> _previousActiveRegistrations = new();
  private readonly HashSet<ulong> _currentActiveRegistrations = new();
  private readonly Dictionary<long, ChunkLoaderRegistrationRecord>
      _currentActiveRegistrationsByCoordinate = new();
  private readonly Dictionary<ulong, ChunkLoaderRuntimeState> _knownStates = new();
  private readonly Dictionary<ulong, ChunkLoaderErrorCode> _knownErrors = new();
  private double _nextRegistrationScanAt;
  private double _nextEntityScanAt;
  private double _nextInventoryScanAt;
  private EntityQuery _entityQuery;
  private EntityQuery _inventoryQuery;

  protected override void OnCreate()
  {
    ChunkLoaderRegistry.RecordChanged += OnRegistrationChanged;
    _entityQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[] { ComponentType.ReadOnly<LocalTransform>() },
      None = new[]
      {
        ComponentType.ReadOnly<Prefab>(),
        ComponentType.ReadOnly<EntityDestroyedCD>(),
        ComponentType.ReadOnly<ChunkLoaderRuntimeAnchorCD>()
      }
    });
    _inventoryQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[] { ComponentType.ReadOnly<ContainedObjectsBuffer>() },
      None = new[]
      {
        ComponentType.ReadOnly<Prefab>(),
        ComponentType.ReadOnly<EntityDestroyedCD>(),
        ComponentType.ReadOnly<ChunkLoaderRuntimeAnchorCD>()
      }
    });
  }

  protected override void OnDestroy()
  {
    ChunkLoaderRegistry.RecordChanged -= OnRegistrationChanged;
    ChunkLoaderTelemetry.FlushNow();
  }

  protected override void OnUpdate()
  {
    if (!ChunkLoaderSettings.Current.telemetryEnabled)
    {
      ClearObservationState();
      return;
    }

    ChunkLoaderTelemetry.EnsureLoaded();
    double now = SystemAPI.Time.ElapsedTime;
    if (now >= _nextRegistrationScanAt)
    {
      _nextRegistrationScanAt = now + 0.50d;
      RefreshActiveRegistrations();
    }

    if (_currentActiveRegistrations.Count == 0)
    {
      _previous.Clear();
      _previousInventories.Clear();
      _previousActiveRegistrations.Clear();
      return;
    }

    if (now >= _nextInventoryScanAt)
    {
      _nextInventoryScanAt = now + GetInventoryScanInterval();
      ScanInventories();
    }

    if (now < _nextEntityScanAt)
    {
      return;
    }
    _nextEntityScanAt = now + GetEntityScanInterval();
    ScanEntities();
  }

  private void RefreshActiveRegistrations()
  {
    ChunkLoaderRegistry.GetRecords(_records);
    _currentActiveRegistrations.Clear();
    _currentActiveRegistrationsByCoordinate.Clear();
    for (int i = 0; i < _records.Count; i++)
    {
      if (_records[i].desiredEnabled &&
          _records[i].actualState == ChunkLoaderRuntimeState.Loaded)
      {
        _currentActiveRegistrations.Add(_records[i].registrationId);
        _currentActiveRegistrationsByCoordinate[
            _records[i].Coordinate.ToKey()] = _records[i];
      }
    }
  }

  private void ClearObservationState()
  {
    _previous.Clear();
    _current.Clear();
    _previousInventories.Clear();
    _currentInventories.Clear();
    _inventoryLosses.Clear();
    _inventoryGains.Clear();
    _records.Clear();
    _previousActiveRegistrations.Clear();
    _currentActiveRegistrations.Clear();
    _currentActiveRegistrationsByCoordinate.Clear();
  }

  private double GetInventoryScanInterval()
  {
    int active = _currentActiveRegistrations.Count;
    if (active >= 96) return 3.0d;
    if (active >= 64) return 2.0d;
    if (active >= 32) return 1.0d;
    return 0.50d;
  }

  private double GetEntityScanInterval()
  {
    int active = _currentActiveRegistrations.Count;
    if (active >= 96) return 4.0d;
    if (active >= 64) return 3.0d;
    if (active >= 32) return 2.0d;
    return 1.0d;
  }

  private void ScanEntities()
  {
    _current.Clear();

    using NativeArray<Entity> entities =
        _entityQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<LocalTransform> transforms =
        _entityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      float2 position = new float2(
          transforms[i].Position.x,
          transforms[i].Position.z);
      if (!TryFindRegistration(position, out ChunkLoaderRegistrationRecord registration))
      {
        continue;
      }

      Entity entity = entities[i];
      bool hasObject = EntityManager.HasComponent<ObjectDataCD>(entity);
      bool isEnemy = EntityManager.HasComponent<EnemyCD>(entity);
      bool isPlayer = EntityManager.HasComponent<PlayerGhost>(entity);
      bool hasHealth = EntityManager.HasComponent<HealthCD>(entity);
      if (!hasObject && !isEnemy && !isPlayer && !hasHealth)
      {
        continue;
      }

      ObjectDataCD objectData = hasObject
          ? EntityManager.GetComponentData<ObjectDataCD>(entity)
          : default;
      HealthCD health = hasHealth
          ? EntityManager.GetComponentData<HealthCD>(entity)
          : default;
      ObservedEntity observed = new ObservedEntity
      {
        Entity = entity,
        RegistrationId = registration.registrationId,
        ObjectId = (int)objectData.objectID,
        Amount = objectData.amount,
        Health = hasHealth ? health.health : -1,
        Position = (int2)math.floor(position),
        Enemy = isEnemy,
        Player = isPlayer
      };

      long key = EntityKey(entity);
      _current[key] = observed;
      if (!_previous.TryGetValue(key, out ObservedEntity prior))
      {
        if (_previousActiveRegistrations.Contains(observed.RegistrationId) &&
            observed.ObjectId != (int)ObjectID.DroppedItem)
        {
          RecordLifecycle(observed, "Spawned");
        }
      }
      else
      {
        if (prior.Amount != observed.Amount &&
            observed.ObjectId != (int)ObjectID.None &&
            observed.ObjectId != (int)ObjectID.DroppedItem)
        {
          ChunkLoaderTelemetry.Record(
              observed.RegistrationId,
              "Item",
              $"{ObjectName(observed)} amount changed {prior.Amount} -> {observed.Amount}",
              observed.Position.x,
              observed.Position.y);
        }

        if (prior.Health > 0 && observed.Health <= 0)
        {
          ChunkLoaderTelemetry.Record(
              observed.RegistrationId,
              observed.Player ? "Player" : observed.Enemy ? "Mob" : "Entity",
              ObjectName(observed) + " died",
              observed.Position.x,
              observed.Position.y);
        }
        else if (prior.Health <= 0 &&
                 observed.Health > 0 &&
                 observed.Player)
        {
          ChunkLoaderTelemetry.Record(
              observed.RegistrationId,
              "Player",
              "Player respawned",
              observed.Position.x,
              observed.Position.y);
        }
      }
    }

    foreach (KeyValuePair<long, ObservedEntity> entry in _previous)
    {
      if (!_current.ContainsKey(entry.Key) &&
          _currentActiveRegistrations.Contains(entry.Value.RegistrationId) &&
          entry.Value.ObjectId != (int)ObjectID.DroppedItem)
      {
        RecordLifecycle(entry.Value, "Despawned");
      }
    }

    _previous.Clear();
    foreach (KeyValuePair<long, ObservedEntity> entry in _current)
    {
      _previous.Add(entry.Key, entry.Value);
    }

    _previousActiveRegistrations.Clear();
    foreach (ulong registrationId in _currentActiveRegistrations)
    {
      _previousActiveRegistrations.Add(registrationId);
    }
  }

  private void ScanInventories()
  {
    _currentInventories.Clear();
    _inventoryLosses.Clear();
    _inventoryGains.Clear();

    using NativeArray<Entity> entities =
        _inventoryQuery.ToEntityArray(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      Entity inventoryEntity = entities[i];
      if (!TryResolveInventoryOwner(
              inventoryEntity,
              out Entity owner,
              out float2 position) ||
          !TryFindRegistration(
              position,
              out ChunkLoaderRegistrationRecord registration))
      {
        continue;
      }

      bool isGroundItem =
          TryGetObjectId(owner, out ObjectID ownerObjectId) &&
          ownerObjectId == ObjectID.DroppedItem;
      ObservedInventory observed = new ObservedInventory
      {
        Entity = inventoryEntity,
        RegistrationId = registration.registrationId,
        Position = (int2)math.floor(position),
        Label = ResolveEntityLabel(owner),
        IsGroundItem = isGroundItem,
        PickupTargetLabel = ResolvePickupTargetLabel(inventoryEntity)
      };
      DynamicBuffer<ContainedObjectsBuffer> contents =
          EntityManager.GetBuffer<ContainedObjectsBuffer>(
              inventoryEntity,
              true);
      for (int slot = 0; slot < contents.Length; slot++)
      {
        ContainedObjectsBuffer item = contents[slot];
        if (item.objectID == ObjectID.None || item.amount <= 0)
        {
          continue;
        }

        ItemIdentity identity =
            new ItemIdentity(item.objectID, item.variation);
        observed.Items.TryGetValue(identity, out int amount);
        observed.Items[identity] = amount + item.amount;
      }
      _currentInventories[EntityKey(inventoryEntity)] = observed;
    }

    foreach (KeyValuePair<long, ObservedInventory> entry
             in _currentInventories)
    {
      if (_previousInventories.TryGetValue(
              entry.Key,
              out ObservedInventory prior))
      {
        CollectInventoryDeltas(prior, entry.Value);
      }
      else if (_previousActiveRegistrations.Contains(
                   entry.Value.RegistrationId))
      {
        CollectAllInventoryItems(entry.Value, _inventoryGains);
      }
    }

    foreach (KeyValuePair<long, ObservedInventory> entry
             in _previousInventories)
    {
      if (!_currentInventories.ContainsKey(entry.Key) &&
          _currentActiveRegistrations.Contains(
              entry.Value.RegistrationId))
      {
        CollectAllInventoryItems(entry.Value, _inventoryLosses);
      }
    }

    CorrelateInventoryDeltas();

    _previousInventories.Clear();
    foreach (KeyValuePair<long, ObservedInventory> entry
             in _currentInventories)
    {
      _previousInventories.Add(entry.Key, entry.Value);
    }
  }

  private void CollectInventoryDeltas(
      ObservedInventory prior,
      ObservedInventory current)
  {
    foreach (KeyValuePair<ItemIdentity, int> item in prior.Items)
    {
      current.Items.TryGetValue(item.Key, out int currentAmount);
      int difference = currentAmount - item.Value;
      if (difference < 0)
      {
        _inventoryLosses.Add(new InventoryDelta
        {
          Inventory = prior,
          Item = item.Key,
          Amount = -difference
        });
      }
      else if (difference > 0)
      {
        _inventoryGains.Add(new InventoryDelta
        {
          Inventory = current,
          Item = item.Key,
          Amount = difference
        });
      }
    }

    foreach (KeyValuePair<ItemIdentity, int> item in current.Items)
    {
      if (!prior.Items.ContainsKey(item.Key))
      {
        _inventoryGains.Add(new InventoryDelta
        {
          Inventory = current,
          Item = item.Key,
          Amount = item.Value
        });
      }
    }
  }

  private static void CollectAllInventoryItems(
      ObservedInventory inventory,
      List<InventoryDelta> output)
  {
    foreach (KeyValuePair<ItemIdentity, int> item in inventory.Items)
    {
      output.Add(new InventoryDelta
      {
        Inventory = inventory,
        Item = item.Key,
        Amount = item.Value
      });
    }
  }

  private void CorrelateInventoryDeltas()
  {
    const int maximumTransferDistanceSquared = 12 * 12;
    for (int lossIndex = 0;
         lossIndex < _inventoryLosses.Count;
         lossIndex++)
    {
      InventoryDelta loss = _inventoryLosses[lossIndex];
      while (loss.Amount > 0)
      {
        int bestGain = -1;
        int bestDistance = int.MaxValue;
        for (int gainIndex = 0;
             gainIndex < _inventoryGains.Count;
             gainIndex++)
        {
          InventoryDelta candidate = _inventoryGains[gainIndex];
          if (candidate.Amount <= 0 ||
              candidate.Inventory.RegistrationId !=
                  loss.Inventory.RegistrationId ||
              !candidate.Item.Equals(loss.Item))
          {
            continue;
          }

          int2 difference =
              candidate.Inventory.Position - loss.Inventory.Position;
          int distance = math.dot(difference, difference);
          if (distance < bestDistance)
          {
            bestDistance = distance;
            bestGain = gainIndex;
          }
        }

        if (bestGain < 0 ||
            bestDistance > maximumTransferDistanceSquared)
        {
          break;
        }

        InventoryDelta gain = _inventoryGains[bestGain];
        int amount = math.min(loss.Amount, gain.Amount);
        RecordTransfer(loss, gain, amount);
        loss.Amount -= amount;
        gain.Amount -= amount;
      }
    }

    for (int i = 0; i < _inventoryLosses.Count; i++)
    {
      InventoryDelta loss = _inventoryLosses[i];
      if (loss.Amount <= 0)
      {
        continue;
      }

      if (loss.Inventory.IsGroundItem)
      {
        if (!string.IsNullOrWhiteSpace(
                loss.Inventory.PickupTargetLabel))
        {
          RecordItemEvent(
              loss.Inventory,
              $"{loss.Inventory.PickupTargetLabel} picked up " +
              FormatItem(loss.Item, loss.Amount));
        }
        continue;
      }
    }

    for (int i = 0; i < _inventoryGains.Count; i++)
    {
      InventoryDelta gain = _inventoryGains[i];
      if (gain.Amount <= 0)
      {
        continue;
      }

      if (gain.Inventory.IsGroundItem)
      {
        RecordItemEvent(
            gain.Inventory,
            $"{FormatItem(gain.Item, gain.Amount)} appeared on the ground");
      }
    }
  }

  private static void RecordTransfer(
      InventoryDelta loss,
      InventoryDelta gain,
      int amount)
  {
    string item = FormatItem(loss.Item, amount);
    string message;
    if (loss.Inventory.IsGroundItem &&
        !gain.Inventory.IsGroundItem)
    {
      message = $"{gain.Inventory.Label} picked up {item}";
    }
    else if (!loss.Inventory.IsGroundItem &&
             gain.Inventory.IsGroundItem)
    {
      message = $"{loss.Inventory.Label} dropped {item}";
    }
    else if (!loss.Inventory.IsGroundItem &&
             !gain.Inventory.IsGroundItem)
    {
      message =
          $"{loss.Inventory.Label} transferred {item} to " +
          gain.Inventory.Label;
    }
    else
    {
      return;
    }

    RecordItemEvent(gain.Inventory, message);
  }

  private static void RecordItemEvent(
      ObservedInventory inventory,
      string message)
  {
    ChunkLoaderTelemetry.Record(
        inventory.RegistrationId,
        "Item Transfer",
        message,
        inventory.Position.x,
        inventory.Position.y);
  }

  private bool TryResolveInventoryOwner(
      Entity inventoryEntity,
      out Entity owner,
      out float2 position)
  {
    owner = inventoryEntity;
    if (EntityManager.HasComponent<BigEntityRefCD>(inventoryEntity))
    {
      Entity referenced =
          EntityManager.GetComponentData<BigEntityRefCD>(
              inventoryEntity).Value;
      if (referenced != Entity.Null &&
          EntityManager.Exists(referenced))
      {
        owner = referenced;
      }
    }

    if (EntityManager.HasComponent<LocalTransform>(owner))
    {
      LocalTransform transform =
          EntityManager.GetComponentData<LocalTransform>(owner);
      position = transform.Position.xz;
      return true;
    }
    if (owner != inventoryEntity &&
        EntityManager.HasComponent<LocalTransform>(inventoryEntity))
    {
      LocalTransform transform =
          EntityManager.GetComponentData<LocalTransform>(
              inventoryEntity);
      position = transform.Position.xz;
      return true;
    }

    position = default;
    return false;
  }

  private string ResolveEntityLabel(Entity entity)
  {
    if (entity == Entity.Null || !EntityManager.Exists(entity))
    {
      return "Unknown inventory";
    }
    if (EntityManager.HasComponent<PlayerGhost>(entity))
    {
      return "Player";
    }
    if (TryGetObjectId(entity, out ObjectID objectId))
    {
      return objectId == ObjectID.DroppedItem
          ? "Ground"
          : HumanizeObjectId(objectId);
    }
    if (EntityManager.HasComponent<EnemyCD>(entity))
    {
      return "Mob";
    }
    return "Inventory";
  }

  private string ResolvePickupTargetLabel(Entity inventoryEntity)
  {
    if (!EntityManager.HasComponent<PickUpItemCD>(inventoryEntity))
    {
      return null;
    }

    PickUpItemCD pickup =
        EntityManager.GetComponentData<PickUpItemCD>(inventoryEntity);
    return pickup.targetEntity != Entity.Null
        ? ResolveEntityLabel(pickup.targetEntity)
        : null;
  }

  private bool TryGetObjectId(
      Entity entity,
      out ObjectID objectId)
  {
    if (EntityManager.HasComponent<ObjectDataCD>(entity))
    {
      objectId =
          EntityManager.GetComponentData<ObjectDataCD>(entity).objectID;
      return true;
    }

    objectId = ObjectID.None;
    return false;
  }

  private static string FormatItem(
      ItemIdentity item,
      int amount)
  {
    string amountText = amount > 1 ? $" x{amount}" : string.Empty;
    return HumanizeObjectId(item.ObjectId) + amountText;
  }

  private void OnRegistrationChanged(ChunkLoaderRegistrationRecord record)
  {
    bool hadState = _knownStates.TryGetValue(
        record.registrationId,
        out ChunkLoaderRuntimeState previousState);
    _knownErrors.TryGetValue(
        record.registrationId,
        out ChunkLoaderErrorCode previousError);
    _knownStates[record.registrationId] = record.actualState;
    _knownErrors[record.registrationId] = record.lastErrorCode;

    if (hadState &&
        previousState == record.actualState &&
        previousError == record.lastErrorCode)
    {
      return;
    }

    string message = hadState
        ? $"State changed {previousState} -> {record.actualState}"
        : $"Registration entered state {record.actualState}";
    if (record.lastErrorCode != ChunkLoaderErrorCode.None)
    {
      message += $": {record.lastErrorCode} {record.lastErrorText}";
    }

    ChunkLoaderTelemetry.Record(
        record.registrationId,
        "Chunk",
        message,
        record.Coordinate.Center.x,
        record.Coordinate.Center.y);
  }

  private bool TryFindRegistration(
      float2 position,
      out ChunkLoaderRegistrationRecord record)
  {
    return _currentActiveRegistrationsByCoordinate.TryGetValue(
        ChunkCoordinate.FromWorldPosition(position).ToKey(),
        out record);
  }

  private static void RecordLifecycle(ObservedEntity entity, string action)
  {
    string category = entity.Player ? "Player" : entity.Enemy ? "Mob" : "Entity";
    ChunkLoaderTelemetry.Record(
        entity.RegistrationId,
        category,
        action + " " + ObjectName(entity),
        entity.Position.x,
        entity.Position.y);
  }

  private static string ObjectName(ObservedEntity entity)
  {
    if (entity.Player)
    {
      return "Player";
    }
    if (entity.Enemy &&
        entity.ObjectId == (int)ObjectID.None)
    {
      return "Mob";
    }
    return entity.ObjectId == (int)ObjectID.None
        ? $"entity {entity.Entity.Index}:{entity.Entity.Version}"
        : HumanizeObjectId((ObjectID)entity.ObjectId);
  }

  private static string HumanizeObjectId(ObjectID objectId)
  {
    string raw = objectId.ToString();
    if (string.IsNullOrEmpty(raw))
    {
      return "Unknown Item";
    }

    System.Text.StringBuilder builder =
        new System.Text.StringBuilder(raw.Length + 8);
    for (int i = 0; i < raw.Length; i++)
    {
      char current = raw[i];
      if (i > 0 &&
          char.IsUpper(current) &&
          (char.IsLower(raw[i - 1]) ||
           (i + 1 < raw.Length && char.IsLower(raw[i + 1]))))
      {
        builder.Append(' ');
      }
      builder.Append(current);
    }
    return builder.ToString();
  }

  private static long EntityKey(Entity entity)
  {
    return ((long)entity.Index << 32) ^ (uint)entity.Version;
  }
}
