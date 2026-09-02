using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The saved shape of portals, travel and where a player starts.
    /// </summary>
    internal sealed partial class DimensionRuntimeManifestSnapshot
    {
        [Serializable]
        internal sealed class PortalSnapshot
        {
            public string id;
            public string displayName;
            public string fromDimensionId;
            public Float2Snapshot fromLocalPosition;
            public string toDimensionId;
            public Float2Snapshot toLocalPosition;
            public int state;

            public static PortalSnapshot From(DimensionPortalDefinition value)
            {
                return new PortalSnapshot
                {
                    id = value.PortalId,
                    displayName = value.DisplayName,
                    fromDimensionId = value.FromDimensionId,
                    fromLocalPosition = Float2Snapshot.From(value.FromLocalPosition),
                    toDimensionId = value.ToDimensionId,
                    toLocalPosition = Float2Snapshot.From(value.ToLocalPosition),
                    state = (int)value.State
                };
            }

            public DimensionPortalDefinition ToDefinition()
            {
                return new DimensionPortalDefinition(
                    id,
                    displayName,
                    fromDimensionId,
                    ToFloat2(fromLocalPosition),
                    toDimensionId,
                    ToFloat2(toLocalPosition),
                    (DimensionPortalState)state);
            }
        }

        [Serializable]
        internal sealed class PortalPresentationSnapshot
        {
            public string id;
            public string portalId;
            public string displayName;
            public string promptText;
            public string lockedPromptText;
            public string iconId;
            public string visualEffectId;
            public string audioCueId;
            public float cooldownSeconds;
            public int priority;
            public bool enabled;
            public bool interactable = true;
            public bool requireGeneratedAreaOnUse;
            public bool allowFallbackPositionOnUse;

            public static PortalPresentationSnapshot From(DimensionPortalPresentationDefinition value)
            {
                return new PortalPresentationSnapshot
                {
                    id = value.PresentationId,
                    portalId = value.PortalId,
                    displayName = value.DisplayName,
                    promptText = value.PromptText,
                    lockedPromptText = value.LockedPromptText,
                    iconId = value.IconId,
                    visualEffectId = value.VisualEffectId,
                    audioCueId = value.AudioCueId,
                    cooldownSeconds = value.CooldownSeconds,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    interactable = value.Interactable,
                    requireGeneratedAreaOnUse = value.RequireGeneratedAreaOnUse,
                    allowFallbackPositionOnUse = value.AllowFallbackPositionOnUse
                };
            }

            public DimensionPortalPresentationDefinition ToDefinition()
            {
                return new DimensionPortalPresentationDefinition(
                    id,
                    portalId,
                    displayName,
                    promptText,
                    lockedPromptText,
                    iconId,
                    visualEffectId,
                    audioCueId,
                    cooldownSeconds,
                    priority,
                    enabled,
                    requireGeneratedAreaOnUse,
                    allowFallbackPositionOnUse,
                    interactable);
            }
        }

        [Serializable]
        internal sealed class TravelRequirementSnapshot
        {
            public string id;
            public string displayName;
            public string portalId;
            public string dimensionId;
            public int kind;
            public string subjectId;
            public int requiredAmount;
            public bool consumeOnTravel;
            public string failureMessage;
            public int priority;
            public bool enabled;

            public static TravelRequirementSnapshot From(DimensionTravelRequirementDefinition value)
            {
                return new TravelRequirementSnapshot
                {
                    id = value.RequirementId,
                    displayName = value.DisplayName,
                    portalId = value.PortalId,
                    dimensionId = value.DimensionId,
                    kind = (int)value.Kind,
                    subjectId = value.SubjectId,
                    requiredAmount = value.RequiredAmount,
                    consumeOnTravel = value.ConsumeOnTravel,
                    failureMessage = value.FailureMessage,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionTravelRequirementDefinition ToDefinition()
            {
                return new DimensionTravelRequirementDefinition(
                    id,
                    displayName,
                    portalId,
                    dimensionId,
                    (DimensionTravelRequirementKind)kind,
                    subjectId,
                    requiredAmount,
                    consumeOnTravel,
                    failureMessage,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class StarterSnapshot
        {
            public string id;
            public string dimensionId;
            public string displayName;
            public string description;
            public string contentPackId;
            public bool enabled;
            public ContentReadinessSnapshot readiness;
            public TravelLoopSnapshot travelLoop;
            public StarterGenerationSnapshot generation;

            public static StarterSnapshot From(DimensionStarterDefinition value)
            {
                return new StarterSnapshot
                {
                    id = value.StarterId,
                    dimensionId = value.DimensionId,
                    displayName = value.DisplayName,
                    description = value.Description,
                    contentPackId = value.ContentPackId,
                    enabled = value.Enabled,
                    readiness = ContentReadinessSnapshot.From(value.ContentReadinessRequest),
                    travelLoop = TravelLoopSnapshot.From(value.TravelLoopPreflightRequest),
                    generation = StarterGenerationSnapshot.From(value.GenerationRequest)
                };
            }

            public DimensionStarterDefinition ToDefinition()
            {
                return new DimensionStarterDefinition(
                    id,
                    dimensionId,
                    displayName,
                    description,
                    contentPackId,
                    enabled,
                    readiness == null ? default(DimensionContentReadinessRequest) : readiness.ToRequest(),
                    travelLoop == null ? default(DimensionTravelLoopPreflightRequest) : travelLoop.ToRequest(),
                    generation == null ? default(DimensionStarterGenerationRequest) : generation.ToRequest());
            }
        }

        [Serializable]
        internal sealed class ContentReadinessSnapshot
        {
            public string id;
            public string displayName;
            public ContentReadinessRequirementSnapshot[] requirements =
                new ContentReadinessRequirementSnapshot[0];

            public static ContentReadinessSnapshot From(DimensionContentReadinessRequest value)
            {
                return new ContentReadinessSnapshot
                {
                    id = value.RequestId,
                    displayName = value.DisplayName,
                    requirements = Convert(value.Requirements, ContentReadinessRequirementSnapshot.From)
                };
            }

            public DimensionContentReadinessRequest ToRequest()
            {
                return new DimensionContentReadinessRequest(
                    id,
                    displayName,
                    Convert(requirements, record => record.ToRequirement()));
            }
        }

        [Serializable]
        internal sealed class ContentReadinessRequirementSnapshot
        {
            public int recordKind;
            public string recordId;
            public string displayName;
            public bool requireOwnership;
            public string requiredOwnerContentPackId;
            public bool requireEnabledOwnerContentPack;

            public static ContentReadinessRequirementSnapshot From(DimensionContentReadinessRequirement value)
            {
                return new ContentReadinessRequirementSnapshot
                {
                    recordKind = (int)value.RecordKind,
                    recordId = value.RecordId,
                    displayName = value.DisplayName,
                    requireOwnership = value.RequireOwnership,
                    requiredOwnerContentPackId = value.RequiredOwnerContentPackId,
                    requireEnabledOwnerContentPack = value.RequireEnabledOwnerContentPack
                };
            }

            public DimensionContentReadinessRequirement ToRequirement()
            {
                return new DimensionContentReadinessRequirement(
                    (DimensionContentRecordKind)recordKind,
                    recordId,
                    displayName,
                    requireOwnership,
                    requiredOwnerContentPackId,
                    requireEnabledOwnerContentPack);
            }
        }

        [Serializable]
        internal sealed class TravelLoopSnapshot
        {
            public string sourceDimensionId;
            public string targetDimensionId;
            public string entryPortalId;
            public string returnPortalId;
            public string sourceAnchorId;
            public string targetAnchorId;
            public string sourceMarkerId;
            public string targetMarkerId;
            public BoundsSnapshot targetLandingBounds;
            public bool requireReturnPortal;
            public bool requireSourceAnchor;
            public bool requireTargetAnchor;
            public bool requireMarkers;
            public bool requireTargetAreaReady;
            public bool requireMapLayers;

            public static TravelLoopSnapshot From(DimensionTravelLoopPreflightRequest value)
            {
                return new TravelLoopSnapshot
                {
                    sourceDimensionId = value.SourceDimensionId,
                    targetDimensionId = value.TargetDimensionId,
                    entryPortalId = value.EntryPortalId,
                    returnPortalId = value.ReturnPortalId,
                    sourceAnchorId = value.SourceAnchorId,
                    targetAnchorId = value.TargetAnchorId,
                    sourceMarkerId = value.SourceMarkerId,
                    targetMarkerId = value.TargetMarkerId,
                    targetLandingBounds = BoundsSnapshot.From(value.TargetLandingBounds),
                    requireReturnPortal = value.RequireReturnPortal,
                    requireSourceAnchor = value.RequireSourceAnchor,
                    requireTargetAnchor = value.RequireTargetAnchor,
                    requireMarkers = value.RequireMarkers,
                    requireTargetAreaReady = value.RequireTargetAreaReady,
                    requireMapLayers = value.RequireMapLayers
                };
            }

            public DimensionTravelLoopPreflightRequest ToRequest()
            {
                return new DimensionTravelLoopPreflightRequest(
                    sourceDimensionId,
                    targetDimensionId,
                    entryPortalId,
                    returnPortalId,
                    sourceAnchorId,
                    targetAnchorId,
                    sourceMarkerId,
                    targetMarkerId,
                    ToBounds(targetLandingBounds),
                    requireReturnPortal,
                    requireSourceAnchor,
                    requireTargetAnchor,
                    requireMarkers,
                    requireTargetAreaReady,
                    requireMapLayers);
            }
        }

        [Serializable]
        internal sealed class StarterGenerationSnapshot
        {
            public string starterId;
            public string requesterId;
            public string dimensionId;
            public BoundsSnapshot localBounds;
            public int priority;
            public bool createIfMissing;
            public string reason;

            public static StarterGenerationSnapshot From(DimensionStarterGenerationRequest value)
            {
                return new StarterGenerationSnapshot
                {
                    starterId = value.StarterId,
                    requesterId = value.RequesterId,
                    dimensionId = value.DimensionId,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    priority = value.Priority,
                    createIfMissing = value.CreateIfMissing,
                    reason = value.Reason
                };
            }

            public DimensionStarterGenerationRequest ToRequest()
            {
                return new DimensionStarterGenerationRequest(
                    starterId,
                    requesterId,
                    dimensionId,
                    ToBounds(localBounds),
                    priority,
                    createIfMissing,
                    reason);
            }
        }
    }
}
