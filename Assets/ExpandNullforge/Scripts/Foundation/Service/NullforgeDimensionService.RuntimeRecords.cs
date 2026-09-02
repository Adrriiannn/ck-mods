using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private sealed class PendingTravelRecord
    {
      public string TravelId;
      public Entity Player;
      public string PlayerId;
      public string PreviousDimensionId;
      public float2 PreviousAbsolutePosition;
      public string TargetDimensionId;
      public float2 TargetLocalPosition;
      public float2 TargetAbsolutePosition;
      public string LoadTicketId;
      public string PortalId;
      public string Reason;
      public bool RequireGeneratedArea;
      public bool AllowFallbackPosition;
      public bool FallbackAttempted;
      public bool WaitingForGeneration;
      public DimensionBounds GenerationBounds;
      public DimensionTravelState State;
      public string Message;
      public double CreatedAt;
      public double UpdatedAt;
      public double LoadRequestedAt;
      public double TeleportQueuedAt;
      public double NextTeleportRetryAt;
      public int TeleportAttemptCount;
      public NetworkTick ScheduledTick;
    }

    // ONE FIELD PER SET THE PREFLIGHT REALLY FILLS. Four more were declared here —
    // ResourceNodeIds, EnvironmentProfileIds, TableIds, EntryIds — and the preflight's object
    // initialiser named none of them, so every one was a null nothing wrote and nothing read. They
    // were the only CS0649 warnings in the whole ship set. A null set on a validation context is
    // worse than an absent one: the first thing that reaches for it takes a
    // NullReferenceException in the middle of a manifest check.
    //
    // WHAT THEY WERE THE RESIDUE OF, KEPT HERE SO DELETING THEM DOES NOT ERASE IT. The resource
    // node, environment profile and generation table registries were amputated. Their ids are
    // still authored on BiomeTemplateAsset, still carried into DimensionRuntimeManifestSnapshot,
    // still filtered in the biome internals and still counted in DimensionBiomeGenerationBudget —
    // and nothing acts on any of them. That is the open decision the plan records as D4: restore
    // the service partials, or take the authoring surface away end to end. These four fields were
    // not that decision and could not stand in for it; four compiler warnings are a poor way to
    // remember an amputated feature, and this sentence is a better one.
    private sealed class ManifestValidationContext
    {
      public Dictionary<string, bool> ContentPackIds;
      public Dictionary<string, bool> DimensionIds;
      public Dictionary<string, bool> ZoneIds;
      public Dictionary<string, bool> MapLayerIds;
      public Dictionary<string, bool> MapMarkerIds;
      public Dictionary<string, bool> AnchorIds;
      public Dictionary<string, bool> PortalIds;
      public Dictionary<string, bool> PortalPresentationIds;
      public Dictionary<string, bool> TravelRequirementIds;
      public Dictionary<string, bool> StarterIds;
      public Dictionary<string, bool> SceneTemplateIds;
      public Dictionary<string, bool> SceneIds;
      public Dictionary<string, bool> EncounterIds;
      public Dictionary<string, bool> ProgressFlagIds;
      public Dictionary<string, bool> WorldEventIds;
      public Dictionary<string, bool> GenerationPassIds;
      public Dictionary<string, bool> AssetReferenceIds;
      public Dictionary<string, bool> BiomeIds;
      public Dictionary<string, bool> OwnershipKeys;
    }

    private sealed class TrackedPlayerContextRecord
    {
      public string DimensionId;
      public int2 LocalTile;
      public int2 AbsoluteTile;
      public float2 LocalPosition;
      public float2 AbsolutePosition;
      public bool HasPersistedCheckpoint;
      public string PersistedDimensionId;
      public int2 PersistedLocalTile;
      public int2 PersistedAbsoluteTile;
    }

    private sealed class ProgressFlagTravelRequirementEvaluator : IDimensionTravelRequirementEvaluator
    {
      private readonly NullforgeDimensionService owner;

      public ProgressFlagTravelRequirementEvaluator(NullforgeDimensionService owner)
      {
        this.owner = owner;
      }

      public string ProviderId
      {
        get { return BuiltInProgressFlagRequirementEvaluatorId; }
      }

      public int Priority
      {
        get { return -1000; }
      }

      public bool TryEvaluateTravelRequirement(
          DimensionTravelRequirementEvaluationContext context,
          out DimensionTravelRequirementEvaluationResult result)
      {
        DimensionTravelRequirementDefinition requirement = context.Requirement;
        if (requirement.Kind != DimensionTravelRequirementKind.ProgressFlag)
        {
          result = default(DimensionTravelRequirementEvaluationResult);
          return false;
        }

        if (owner.IsProgressFlagSet(requirement.SubjectId))
        {
          result =
              DimensionTravelRequirementEvaluationResult.Met(
                  requirement,
                  ProviderId,
                  "Progress flag requirement is met.");
          return true;
        }

        result =
            DimensionTravelRequirementEvaluationResult.Unmet(
                requirement,
                ProviderId,
                "progress-flag-not-set",
                string.IsNullOrEmpty(requirement.FailureMessage)
                    ? "Required progress has not been completed."
                    : requirement.FailureMessage);
        return true;
      }
    }

    private sealed class TravelRequirementAccessProvider : IDimensionAccessProvider
    {
      private readonly NullforgeDimensionService owner;

      public TravelRequirementAccessProvider(NullforgeDimensionService owner)
      {
        this.owner = owner;
      }

      public string ProviderId
      {
        get { return BuiltInTravelRequirementAccessProviderId; }
      }

      public int Priority
      {
        get { return -900; }
      }

      public bool TryEvaluateTravelAccess(
          DimensionAccessContext context,
          out DimensionAccessResult result)
      {
        IReadOnlyList<DimensionTravelRequirementEvaluationResult> evaluations =
            owner.EvaluateTravelRequirements(context, true);
        if (evaluations.Count == 0)
        {
          result = DimensionAccessResult.Allow();
          return false;
        }

        for (int i = 0; i < evaluations.Count; i++)
        {
          DimensionTravelRequirementEvaluationResult evaluation = evaluations[i];
          DimensionTravelRequirementDefinition requirement = evaluation.Requirement;
          if (!evaluation.Evaluated)
          {
            string requirementName = TravelRequirementName(requirement);
            result =
                DimensionAccessResult.Deny(
                    string.IsNullOrEmpty(evaluation.Code)
                        ? "travel-requirement-unevaluated"
                        : evaluation.Code,
                    "Cannot evaluate travel requirement: " + requirementName + ".");
            return true;
          }

          if (!evaluation.Satisfied)
          {
            result =
                DimensionAccessResult.Deny(
                    string.IsNullOrEmpty(evaluation.Code)
                        ? "travel-requirement-unmet"
                        : evaluation.Code,
                    string.IsNullOrEmpty(evaluation.Message)
                        ? "A travel requirement is not met: " + TravelRequirementName(requirement) + "."
                        : evaluation.Message);
            return true;
          }
        }

        result = DimensionAccessResult.Allow();
        return true;
      }

      private static string TravelRequirementName(DimensionTravelRequirementDefinition requirement)
      {
        if (!string.IsNullOrEmpty(requirement.DisplayName))
        {
          return requirement.DisplayName;
        }

        if (!string.IsNullOrEmpty(requirement.SubjectId))
        {
          return requirement.SubjectId;
        }

        return requirement.RequirementId;
      }
    }

    private sealed class RuntimeGenerationRecord
    {
      public string Key;
      public DimensionGenerationRequest Request;
      public string LoadTicketId;
      public string ReservationId;
      public string ProviderId;
      public List<DimensionGenerationPassDefinition> PlannedPasses;
      public int PlannedPassIndex;
      public bool UsePlannedPasses;
      public DimensionGenerationState State;
      public double CreatedAt;
      public double UpdatedAt;
      public double ProviderStartedAt;
    }

    private sealed class RuntimeLoadRecord
    {
      public string TicketId;
      public ulong TicketHash;
      public DimensionArea Area;
      public bool KeepTilesResident;
      public bool EnableSimulation;
      public float TimeoutSeconds;
      public Entity Anchor;
      public double CreatedAt;
      public double SubMapsObservedAt;
      public bool ImmediateLoadEnabled;
      public DimensionLoadState State;
      public string Message;
      public readonly List<long> RequiredSubMaps = new List<long>();
    }

    private readonly struct MergedSimulationRegion
    {
      public MergedSimulationRegion(int minX, int minY, int sizeX, int sizeY)
      {
        MinX = minX;
        MinY = minY;
        SizeX = sizeX;
        SizeY = sizeY;
      }

      public readonly int MinX;
      public readonly int MinY;
      public readonly int SizeX;
      public readonly int SizeY;

      public long Key
      {
        get
        {
          unchecked
          {
            long hash = 1469598103934665603L;
            hash = (hash ^ MinX) * 1099511628211L;
            hash = (hash ^ MinY) * 1099511628211L;
            hash = (hash ^ SizeX) * 1099511628211L;
            hash = (hash ^ SizeY) * 1099511628211L;
            return hash;
          }
        }
      }
    }
  }
}
