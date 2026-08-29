using System.Collections.Generic;
using ExpandNullforge.Foundation;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// Fills an offering portal's slot requirements, and — on the server — keeps the ledger the
  /// travel gate reads and takes the offering when someone departs.
  /// </summary>
  /// <remarks>
  /// <para>
  /// HYDRATION RUNS ON BOTH SIDES because both need it: the server enforces which item a slot
  /// accepts, and the client draws the dimmed ghost and its tooltip from the very same buffer.
  /// It runs once per portal — names are resolved to ids only after the mod's own items have
  /// registered, which is guaranteed by the time any world is simulating.
  /// </para>
  /// <para>
  /// SATISFACTION IS SERVER TRUTH. The client reads its ghosted inventory only to decide whether
  /// to open the offering window or travel; whether travel is ALLOWED is answered by the
  /// requirement evaluator from the ledger this system maintains.
  /// </para>
  /// <para>
  /// CONSUMPTION happens here rather than in the travel API because removing items means writing
  /// an entity's inventory buffer, and this system is the one place that already owns that
  /// access. The API queues; this system takes, next tick, only what the entries flagged.
  /// </para>
  /// </remarks>
  [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public partial class DimensionPortalOfferingSystem : SystemBase
  {
    private EntityQuery offeringQuery;
    private bool isServer;

    protected override void OnCreate()
    {
      base.OnCreate();
      offeringQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionPortalOfferingCD>(),
          ComponentType.ReadOnly<DimensionPortalCD>());
      RequireForUpdate(offeringQuery);
      isServer = World.IsServer();
    }

    protected override void OnUpdate()
    {
      NativeArray<Entity> entities = offeringQuery.ToEntityArray(Allocator.Temp);
      try
      {
        List<string> consume = null;
        if (isServer)
        {
          List<string> drained;
          if (DimensionPortalOfferingLedger.TryDrainConsumption(out drained))
          {
            consume = drained;
          }
        }

        for (int i = 0; i < entities.Length; i++)
        {
          Entity entity = entities[i];
          if (!EntityManager.HasBuffer<DimensionPortalOfferingEntry>(entity))
          {
            continue;
          }

          DimensionPortalOfferingCD offering =
              EntityManager.GetComponentData<DimensionPortalOfferingCD>(entity);
          if (offering.Hydrated == 0)
          {
            Hydrate(entity, ref offering);
            EntityManager.SetComponentData(entity, offering);
          }

          if (!isServer)
          {
            continue;
          }

          string portalId =
              EntityManager.GetComponentData<DimensionPortalCD>(entity).PortalId.ToString();
          ReportToLedger(entity, portalId);

          if (consume != null && consume.Contains(portalId))
          {
            TakeOffering(entity);
            ReportToLedger(entity, portalId);
          }
        }
      }
      finally
      {
        entities.Dispose();
      }
    }

    /// <summary>
    /// Resolves the entry names to object ids and writes the slot requirements the game's own
    /// inventory UI and acceptance checks read.
    /// </summary>
    private void Hydrate(Entity entity, ref DimensionPortalOfferingCD offering)
    {
      DynamicBuffer<DimensionPortalOfferingEntry> entries =
          EntityManager.GetBuffer<DimensionPortalOfferingEntry>(entity);

      bool everythingResolved = true;
      for (int i = 0; i < entries.Length; i++)
      {
        DimensionPortalOfferingEntry entry = entries[i];
        if (entry.ResolvedObjectId == ObjectID.None)
        {
          ObjectID resolved = API.Authoring.GetObjectID(entry.ItemName.ToString());
          if (resolved == ObjectID.None)
          {
            everythingResolved = false;
            continue;
          }

          entry.ResolvedObjectId = resolved;
          entries[i] = entry;
        }
      }

      DynamicBuffer<InventorySlotRequirementBuffer> requirements =
          EntityManager.HasBuffer<InventorySlotRequirementBuffer>(entity)
              ? EntityManager.GetBuffer<InventorySlotRequirementBuffer>(entity)
              : EntityManager.AddBuffer<InventorySlotRequirementBuffer>(entity);
      requirements.Clear();

      for (int i = 0; i < entries.Length; i++)
      {
        FixedList32Bytes<ObjectID> accepts = default(FixedList32Bytes<ObjectID>);
        if (entries[i].ResolvedObjectId != ObjectID.None)
        {
          accepts.Add(entries[i].ResolvedObjectId);
        }

        requirements.Add(new InventorySlotRequirementBuffer
        {
          requirementAppliesToAllSlots = false,
          inventoryIndex = 0,
          slotIndex = i,
          dontShowAnyHint = entries[i].Look == (byte)DimensionPortalOfferingLook.Mystery
              ? false // the ghost still draws; the Mystery treatment happens in the hint patch
              : false,
          showInfoText = false,
          acceptsObjectsWithTags = 0UL,
          acceptsObjectIds = accepts,
          denyLegendaryRarity = false,
        });
      }

      // Names that did not resolve stay unhydrated so the next tick retries — a consumer mod's
      // items can register a moment after ours in a different load order.
      offering.Hydrated = everythingResolved ? (byte)1 : (byte)0;
    }

    /// <summary>Reads the portal's slots and tells the ledger where the offering stands.</summary>
    private void ReportToLedger(Entity entity, string portalId)
    {
      DynamicBuffer<DimensionPortalOfferingEntry> entries =
          EntityManager.GetBuffer<DimensionPortalOfferingEntry>(entity);

      Dictionary<string, DimensionPortalOfferingLedger.ItemState> state =
          new Dictionary<string, DimensionPortalOfferingLedger.ItemState>(entries.Length);

      bool hasContents = EntityManager.HasBuffer<ContainedObjectsBuffer>(entity);
      DynamicBuffer<ContainedObjectsBuffer> contents = hasContents
          ? EntityManager.GetBuffer<ContainedObjectsBuffer>(entity, true)
          : default(DynamicBuffer<ContainedObjectsBuffer>);

      for (int i = 0; i < entries.Length; i++)
      {
        int offered = 0;
        if (hasContents && i < contents.Length &&
            contents[i].objectData.objectID == entries[i].ResolvedObjectId &&
            entries[i].ResolvedObjectId != ObjectID.None)
        {
          offered = contents[i].objectData.amount;
        }

        state[entries[i].ItemName.ToString()] = new DimensionPortalOfferingLedger.ItemState
        {
          Required = entries[i].Amount,
          Offered = offered,
          ConsumeOnTravel = entries[i].ConsumeOnTravel != 0,
        };
      }

      DimensionPortalOfferingLedger.Report(portalId, state);
    }

    /// <summary>
    /// Takes the offering: removes each consume-flagged entry's required amount from its slot.
    /// </summary>
    private void TakeOffering(Entity entity)
    {
      if (!EntityManager.HasBuffer<ContainedObjectsBuffer>(entity))
      {
        return;
      }

      DynamicBuffer<DimensionPortalOfferingEntry> entries =
          EntityManager.GetBuffer<DimensionPortalOfferingEntry>(entity);
      DynamicBuffer<ContainedObjectsBuffer> contents =
          EntityManager.GetBuffer<ContainedObjectsBuffer>(entity);

      for (int i = 0; i < entries.Length && i < contents.Length; i++)
      {
        if (entries[i].ConsumeOnTravel == 0 ||
            entries[i].ResolvedObjectId == ObjectID.None ||
            contents[i].objectData.objectID != entries[i].ResolvedObjectId)
        {
          continue;
        }

        ContainedObjectsBuffer slot = contents[i];
        int remaining = slot.objectData.amount - entries[i].Amount;
        if (remaining > 0)
        {
          slot.objectData.amount = remaining;
        }
        else
        {
          slot.objectData = default(ObjectDataCD);
        }

        contents[i] = slot;
      }

      DimensionFrameworkLog.Verbose(
          "A portal's offering was taken as its traveller departed.");
    }
  }
}
