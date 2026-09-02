using System;
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Newtonsoft.Json;
using Pug.ECS.Components;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Persistence
{
  /// <summary>
  /// The rows the registry writes to disk. These names are the save format: move them, never rename them.
  /// </summary>
  public static partial class DimensionWorldRegistry
  {
    [Serializable]
    public sealed class DimensionDefinitionRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string dimensionId = string.Empty;
      public string displayName = string.Empty;
      public int absoluteOriginX;
      public int absoluteOriginY;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public int generationVersion;
      public int spaceKind;
      public int capabilities;
      public int lifecycleState;
      public bool builtIn;

      /// <summary>
      /// The layout version this world's terrain was generated from, or 0 for a world made before
      /// layout versions existed.
      /// </summary>
      /// <remarks>
      /// Written once, at the first load that has a layout to record, and then left alone. Core
      /// Keeper generates terrain the first time a player walks somewhere, so changing this later
      /// would not reshape what already exists — it would only make the unexplored half disagree
      /// with the explored half.
      /// </remarks>
      public int layoutVersion;

      /// <summary>
      /// The shape code of the layout that generated this world.
      /// </summary>
      /// <remarks>
      /// Kept alongside the version because the version alone cannot tell an unpublished edit from
      /// the version it claims to be. See DimensionLayoutFingerprint.
      /// </remarks>
      public string layoutFingerprint = string.Empty;
    }

    [Serializable]
    public sealed class DimensionSlotPersistenceRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string dimensionId = string.Empty;
      public int absoluteOriginX;
      public int absoluteOriginY;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public int candidateIndex;
      public bool usedFixedOrigin;
      public long assignedUtcTicks;
      public string allocationCode = string.Empty;
      public string allocationMessage = string.Empty;
    }

    [Serializable]
    public sealed class DimensionPlayerStateRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string playerId = string.Empty;
      public string dimensionId = DimensionIds.Overworld;
      public float localX;
      public float localY;
      public float absoluteX;
      public float absoluteY;
      public long savedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionPlayerVisitStateRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string playerId = string.Empty;
      public string dimensionId = DimensionIds.Overworld;
      public float localX;
      public float localY;
      public float absoluteX;
      public float absoluteY;
      public long savedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionPortalRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string portalId = string.Empty;
      public string displayName = string.Empty;
      public string fromDimensionId = string.Empty;
      public float fromLocalX;
      public float fromLocalY;
      public string toDimensionId = string.Empty;
      public float toLocalX;
      public float toLocalY;
      public int state;
    }

    [Serializable]
    public sealed class DimensionMarkerRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string markerId = string.Empty;
      public string dimensionId = string.Empty;
      public float localX;
      public float localY;
      public string label = string.Empty;
      public string kind = string.Empty;
      public bool visible = true;
    }

    [Serializable]
    public sealed class DimensionAnchorRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string anchorId = string.Empty;
      public string displayName = string.Empty;
      public string dimensionId = string.Empty;
      public float localX;
      public float localY;
      public int kind;
      public int priority;
      public bool enabled = true;
    }

    [Serializable]
    public sealed class DimensionSceneRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string sceneId = string.Empty;
      public string displayName = string.Empty;
      public string dimensionId = string.Empty;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public string kind = string.Empty;
      public int priority;
      public int state;
    }

    [Serializable]
    public sealed class DimensionProgressFlagRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string flagId = string.Empty;
      public string dimensionId = string.Empty;
      public string category = string.Empty;
      public bool value;
      public long updatedUtcTicks;
    }

    /// <summary>
    /// When a boss went down, so its summon can honor a real-time cooldown.
    /// </summary>
    /// <remarks>
    /// Vanilla has no such record — its respawn logic is purely "is the boss entity gone" —
    /// which is why the authored cooldown minutes had no consumer until this. UTC wall clock on
    /// purpose: the cooldown keeps running while the server is down, which is how a player
    /// experiences "come back in half an hour".
    /// </remarks>
    [Serializable]
    public sealed class DimensionBossDefeatRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string bossObjectName = string.Empty;
      public long defeatedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionGeneratedAreaRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string dimensionId = string.Empty;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public int state;
      public float progress01;
      public string message = string.Empty;
      public long updatedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionContentOwnershipRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string contentPackId = string.Empty;
      public int recordKind;
      public string recordId = string.Empty;
      public string displayName = string.Empty;
      public string notes = string.Empty;
    }

    [Serializable]
    private sealed class RegistryPayload
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string worldKey = string.Empty;
      public ulong registryRevision;
      public List<DimensionDefinitionRecord> dimensions = new List<DimensionDefinitionRecord>();
      public List<DimensionSlotPersistenceRecord> dimensionSlots =
          new List<DimensionSlotPersistenceRecord>();
      public List<DimensionPlayerStateRecord> players = new List<DimensionPlayerStateRecord>();
      public List<DimensionPlayerVisitStateRecord> playerVisits = new List<DimensionPlayerVisitStateRecord>();
      public List<DimensionPortalRecord> portals = new List<DimensionPortalRecord>();
      public List<DimensionMarkerRecord> markers = new List<DimensionMarkerRecord>();
      public List<DimensionAnchorRecord> anchors = new List<DimensionAnchorRecord>();
      public List<DimensionSceneRecord> scenes = new List<DimensionSceneRecord>();
      public List<DimensionProgressFlagRecord> progressFlags = new List<DimensionProgressFlagRecord>();
      public List<DimensionBossDefeatRecord> bossDefeats = new List<DimensionBossDefeatRecord>();
      public List<DimensionGeneratedAreaRecord> generatedAreas = new List<DimensionGeneratedAreaRecord>();
      public List<DimensionContentOwnershipRecord> contentOwnership = new List<DimensionContentOwnershipRecord>();
    }

    [Serializable]
    private sealed class RegistryEnvelope
    {
      public long generation;
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string worldKey = string.Empty;
      public string payload = string.Empty;
      public string payloadChecksum = string.Empty;
    }
  }
}
