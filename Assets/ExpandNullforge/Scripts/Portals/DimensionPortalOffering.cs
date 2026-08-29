using System;
using System.Collections.Generic;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// The offering window — a portal that asks for items opens a little window of slots, one per
  /// required item, each showing a dimmed ghost of what belongs in it.
  /// </summary>
  /// <remarks>
  /// <para>
  /// THIS IS BUILT ALMOST ENTIRELY OUT OF THE GAME'S OWN PARTS. A slot whose requirement accepts
  /// exactly one item already renders that item as a dimmed ghost, and hovering it already shows
  /// the item's full name and description — that is vanilla <c>InventorySlotUI</c> behaviour, the
  /// same the locked chest uses for its key. The window itself is the chest window. What the
  /// framework adds is the wiring: the slots on the portal, the rule that travel waits for them,
  /// and the consumption of the offering at the moment of departure.
  /// </para>
  /// <para>
  /// WHY THE SLOT REQUIREMENTS ARE FILLED AT RUNTIME rather than baked: a slot requirement stores
  /// hard <c>ObjectID</c>s, and a mod's own items do not have their ids until the mod loads. So the
  /// entity carries the item NAMES (this buffer), and a small system resolves names to ids on both
  /// client and server once the world is up. Vanilla item names resolve the same way.
  /// </para>
  /// </remarks>
  public enum DimensionPortalOfferingLook : byte
  {
    /// <summary>The game's own hint: the item's icon, dimmed, with its full tooltip.</summary>
    GhostOfTheItem = 0,

    /// <summary>A black silhouette and no tooltip — the requirement is a riddle.</summary>
    Mystery = 1,

    /// <summary>A sprite of the author's own, at a dimness of their choosing.</summary>
    CustomSprite = 2,
  }

  /// <summary>One required item, as authored on the portal's entity prefab.</summary>
  [Serializable]
  public struct DimensionPortalOfferingAuthoringEntry
  {
    [Tooltip("The required item, by its object name.")]
    public string itemName;

    [Tooltip("How many of it the portal asks for.")]
    public int amount;

    [Tooltip("How the empty slot shows what belongs in it.")]
    public DimensionPortalOfferingLook look;

    [Tooltip("How faint the ghost is. 0 keeps the game's own dimming.")]
    [Range(0f, 1f)]
    public float dimness;

    [Tooltip("The offering is taken when the player travels.")]
    public bool consumeOnTravel;
  }

  /// <summary>Marks a portal entity as one that asks for an offering.</summary>
  public sealed class DimensionPortalOfferingAuthoring : MonoBehaviour
  {
    [Tooltip("The items the portal asks for — one slot each.")]
    public List<DimensionPortalOfferingAuthoringEntry> entries =
        new List<DimensionPortalOfferingAuthoringEntry>();
  }

  public struct DimensionPortalOfferingCD : IComponentData
  {
    /// <summary>Set once the slot requirements have been written with resolved ids.</summary>
    public byte Hydrated;
  }

  [InternalBufferCapacity(0)]
  public struct DimensionPortalOfferingEntry : IBufferElementData
  {
    public FixedString64Bytes ItemName;
    public int Amount;
    public byte Look;
    public float Dimness;
    public byte ConsumeOnTravel;

    /// <summary>Filled at hydration; None until then.</summary>
    public ObjectID ResolvedObjectId;
  }

  public sealed class DimensionPortalOfferingConverter :
      SingleAuthoringComponentConverter<DimensionPortalOfferingAuthoring>
  {
    protected override void Convert(DimensionPortalOfferingAuthoring authoring)
    {
      if (authoring == null || authoring.entries == null || authoring.entries.Count == 0)
      {
        return;
      }

      AddComponentData(new DimensionPortalOfferingCD { Hydrated = 0 });

      EnsureHasBuffer<DimensionPortalOfferingEntry>();
      for (int i = 0; i < authoring.entries.Count; i++)
      {
        DimensionPortalOfferingAuthoringEntry entry = authoring.entries[i];
        if (string.IsNullOrEmpty(entry.itemName))
        {
          continue;
        }

        AddToBuffer(new DimensionPortalOfferingEntry
        {
          ItemName = new FixedString64Bytes(entry.itemName),
          Amount = entry.amount < 1 ? 1 : entry.amount,
          Look = (byte)entry.look,
          Dimness = Mathf.Clamp01(entry.dimness),
          ConsumeOnTravel = entry.consumeOnTravel ? (byte)1 : (byte)0,
          ResolvedObjectId = ObjectID.None,
        });
      }
    }
  }

  /// <summary>
  /// What the server currently knows about every offering portal — the piece the travel
  /// requirement evaluator reads its answers from.
  /// </summary>
  /// <remarks>
  /// Written by <see cref="DimensionPortalOfferingServerSystem"/> each tick and read by the
  /// evaluator and the travel API on the same main thread, so there is no locking to get wrong.
  /// </remarks>
  public static class DimensionPortalOfferingLedger
  {
    public struct ItemState
    {
      public int Required;
      public int Offered;
      public bool ConsumeOnTravel;
    }

    private static readonly Dictionary<string, Dictionary<string, ItemState>> portals =
        new Dictionary<string, Dictionary<string, ItemState>>();

    private static readonly List<string> consumptionQueue = new List<string>();

    /// <summary>Whether any offering portal exists at all, so callers can skip lookups.</summary>
    public static bool HasAny
    {
      get { return portals.Count > 0; }
    }

    public static void Clear()
    {
      portals.Clear();
      consumptionQueue.Clear();
    }

    /// <summary>The server system reporting one portal's current state, every tick.</summary>
    public static void Report(string portalId, Dictionary<string, ItemState> state)
    {
      if (string.IsNullOrEmpty(portalId))
      {
        return;
      }

      portals[portalId] = state;
    }

    public static void Forget(string portalId)
    {
      if (!string.IsNullOrEmpty(portalId))
      {
        portals.Remove(portalId);
      }
    }

    /// <summary>Whether this portal has offering slots the ledger knows about.</summary>
    public static bool Knows(string portalId)
    {
      return !string.IsNullOrEmpty(portalId) && portals.ContainsKey(portalId);
    }

    /// <summary>One item's requirement on one portal, if the ledger knows it.</summary>
    public static bool TryGetItem(string portalId, string itemName, out ItemState state)
    {
      state = default(ItemState);
      Dictionary<string, ItemState> portal;
      return !string.IsNullOrEmpty(portalId) &&
             !string.IsNullOrEmpty(itemName) &&
             portals.TryGetValue(portalId, out portal) &&
             portal.TryGetValue(itemName, out state);
    }

    /// <summary>
    /// The travel API announcing a departure whose offering should now be taken. The server
    /// system performs the removal on its next update; queueing twice is harmless because
    /// consumption only ever takes what the requirement asks for.
    /// </summary>
    public static void QueueConsumption(string portalId)
    {
      if (string.IsNullOrEmpty(portalId) || !portals.ContainsKey(portalId))
      {
        return;
      }

      if (!consumptionQueue.Contains(portalId))
      {
        consumptionQueue.Add(portalId);
      }
    }

    /// <summary>The server system draining what it should consume this tick.</summary>
    public static bool TryDrainConsumption(out List<string> portalIds)
    {
      if (consumptionQueue.Count == 0)
      {
        portalIds = null;
        return false;
      }

      portalIds = new List<string>(consumptionQueue);
      consumptionQueue.Clear();
      return true;
    }

    /// <summary>
    /// The pure satisfaction rule, kept static so it can be tested without a world: a slot
    /// satisfies its entry when it holds the required item at the required amount or more.
    /// </summary>
    public static bool SlotSatisfies(
        ObjectID slotObject,
        int slotAmount,
        ObjectID requiredObject,
        int requiredAmount)
    {
      return requiredObject != ObjectID.None &&
             slotObject == requiredObject &&
             slotAmount >= (requiredAmount < 1 ? 1 : requiredAmount);
    }
  }
}
