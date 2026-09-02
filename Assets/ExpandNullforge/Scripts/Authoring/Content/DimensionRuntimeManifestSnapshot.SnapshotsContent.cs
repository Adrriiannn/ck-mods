using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The saved shape of the content a world is filled with.
    /// </summary>
    internal sealed partial class DimensionRuntimeManifestSnapshot
    {
        [Serializable]
        internal sealed class SceneTemplateSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public string kind;
            public string providerId;
            public Int2Snapshot footprintSize;
            public int weight;
            public int priority;
            public bool enabled;

            public static SceneTemplateSnapshot From(DimensionSceneTemplateDefinition value)
            {
                return new SceneTemplateSnapshot
                {
                    id = value.TemplateId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    kind = value.Kind,
                    providerId = value.ProviderId,
                    footprintSize = Int2Snapshot.From(value.FootprintSize),
                    weight = value.Weight,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionSceneTemplateDefinition ToDefinition()
            {
                return new DimensionSceneTemplateDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    kind,
                    providerId,
                    ToInt2(footprintSize),
                    weight,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class SceneSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public BoundsSnapshot localBounds;
            public string kind;
            public int priority;
            public int state;

            public static SceneSnapshot From(DimensionSceneDefinition value)
            {
                return new SceneSnapshot
                {
                    id = value.SceneId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    kind = value.Kind,
                    priority = value.Priority,
                    state = (int)value.State
                };
            }

            public DimensionSceneDefinition ToDefinition()
            {
                return new DimensionSceneDefinition(
                    id,
                    displayName,
                    dimensionId,
                    ToBounds(localBounds),
                    kind,
                    priority,
                    (DimensionSceneState)state);
            }
        }

        [Serializable]
        internal sealed class EncounterSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public string sceneId;
            public string markerId;
            public string defeatFlagId;
            public int kind;
            public int priority;
            public bool enabled;

            public static EncounterSnapshot From(DimensionEncounterDefinition value)
            {
                return new EncounterSnapshot
                {
                    id = value.EncounterId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    sceneId = value.SceneId,
                    markerId = value.MarkerId,
                    defeatFlagId = value.DefeatFlagId,
                    kind = (int)value.Kind,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionEncounterDefinition ToDefinition()
            {
                return new DimensionEncounterDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    sceneId,
                    markerId,
                    defeatFlagId,
                    (DimensionEncounterKind)kind,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class GenerationPassSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public bool hasLocalBounds;
            public BoundsSnapshot localBounds;
            public int phase;
            public int priority;
            public string providerId;
            public bool enabled;

            public static GenerationPassSnapshot From(DimensionGenerationPassDefinition value)
            {
                return new GenerationPassSnapshot
                {
                    id = value.PassId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    hasLocalBounds = value.HasLocalBounds,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    phase = (int)value.Phase,
                    priority = value.Priority,
                    providerId = value.ProviderId,
                    enabled = value.Enabled
                };
            }

            public DimensionGenerationPassDefinition ToDefinition()
            {
                return new DimensionGenerationPassDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    hasLocalBounds,
                    ToBounds(localBounds),
                    (DimensionGenerationPassPhase)phase,
                    priority,
                    providerId,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class ProgressFlagSnapshot
        {
            public string id;
            public string dimensionId;
            public string category;
            public bool value;
            public long updatedUtcTicks;

            public static ProgressFlagSnapshot From(DimensionProgressFlag flag)
            {
                return new ProgressFlagSnapshot
                {
                    id = flag.FlagId,
                    dimensionId = flag.DimensionId,
                    category = flag.Category,
                    value = flag.Value,
                    updatedUtcTicks = flag.UpdatedUtcTicks
                };
            }

            public DimensionProgressFlag ToDefinition()
            {
                return new DimensionProgressFlag(
                    id,
                    dimensionId,
                    category,
                    value,
                    updatedUtcTicks);
            }
        }

        [Serializable]
        internal sealed class WorldEventSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public bool hasLocalBounds;
            public BoundsSnapshot localBounds;
            public int kind;
            public string providerId;
            public string progressFlagId;
            public int weight;
            public int priority;
            public float cooldownSeconds;
            public bool enabled;

            public static WorldEventSnapshot From(DimensionWorldEventDefinition value)
            {
                return new WorldEventSnapshot
                {
                    id = value.EventId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    hasLocalBounds = value.HasLocalBounds,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    kind = (int)value.Kind,
                    providerId = value.ProviderId,
                    progressFlagId = value.ProgressFlagId,
                    weight = value.Weight,
                    priority = value.Priority,
                    cooldownSeconds = value.CooldownSeconds,
                    enabled = value.Enabled
                };
            }

            public DimensionWorldEventDefinition ToDefinition()
            {
                return new DimensionWorldEventDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    hasLocalBounds,
                    ToBounds(localBounds),
                    (DimensionWorldEventKind)kind,
                    providerId,
                    progressFlagId,
                    weight,
                    priority,
                    cooldownSeconds,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class OwnershipBindingSnapshot
        {
            public string contentPackId;
            public int recordKind;
            public string recordId;
            public string displayName;
            public string notes;

            public static OwnershipBindingSnapshot From(DimensionContentOwnershipBinding value)
            {
                return new OwnershipBindingSnapshot
                {
                    contentPackId = value.ContentPackId,
                    recordKind = (int)value.RecordKind,
                    recordId = value.RecordId,
                    displayName = value.DisplayName,
                    notes = value.Notes
                };
            }

            public DimensionContentOwnershipBinding ToDefinition()
            {
                return new DimensionContentOwnershipBinding(
                    contentPackId,
                    (DimensionContentRecordKind)recordKind,
                    recordId,
                    displayName,
                    notes);
            }
        }

        [Serializable]
        internal sealed class AssetReferenceSnapshot
        {
            public string id;
            public string contentPackId;
            public string displayName;
            public int kind;
            public string resourceKey;
            public string dimensionId;
            public string zoneId;
            public string variantId;
            public int priority;
            public bool enabled;
            public string notes;

            public static AssetReferenceSnapshot From(DimensionAssetReferenceDefinition value)
            {
                return new AssetReferenceSnapshot
                {
                    id = value.AssetId,
                    contentPackId = value.ContentPackId,
                    displayName = value.DisplayName,
                    kind = (int)value.Kind,
                    resourceKey = value.ResourceKey,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    variantId = value.VariantId,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    notes = value.Notes
                };
            }

            public DimensionAssetReferenceDefinition ToDefinition()
            {
                return new DimensionAssetReferenceDefinition(
                    id,
                    contentPackId,
                    displayName,
                    (DimensionAssetReferenceKind)kind,
                    resourceKey,
                    dimensionId,
                    zoneId,
                    variantId,
                    priority,
                    enabled,
                    notes);
            }
        }
    }
}
