using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The saved shape of a world: its packs, dimensions, zones, layers, markers, biomes.
    /// </summary>
    internal sealed partial class DimensionRuntimeManifestSnapshot
    {
        [Serializable]
        internal sealed class ContentPackSnapshot
        {
            public string id;
            public string displayName;
            public string version;
            public string author;
            public string description;
            public int minimumApiVersion;
            public string[] dependencyIds = new string[0];
            public bool enabled;

            public static ContentPackSnapshot From(DimensionContentPackDefinition value)
            {
                return new ContentPackSnapshot
                {
                    id = value.ContentPackId,
                    displayName = value.DisplayName,
                    version = value.Version,
                    author = value.Author,
                    description = value.Description,
                    minimumApiVersion = value.MinimumApiVersion,
                    dependencyIds = CopyStrings(value.DependencyIds),
                    enabled = value.Enabled
                };
            }

            public DimensionContentPackDefinition ToDefinition()
            {
                return new DimensionContentPackDefinition(
                    id,
                    displayName,
                    version,
                    author,
                    description,
                    minimumApiVersion,
                    dependencyIds,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class DimensionSnapshot
        {
            public string id;
            public string displayName;
            public Int2Snapshot absoluteOrigin;
            public BoundsSnapshot localBounds;
            public int generationVersion;
            public int spaceKind;
            public int capabilities;
            public int lifecycleState;

            public static DimensionSnapshot From(DimensionDefinition value)
            {
                return new DimensionSnapshot
                {
                    id = value.Id,
                    displayName = value.DisplayName,
                    absoluteOrigin = Int2Snapshot.From(value.AbsoluteOrigin),
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    generationVersion = value.GenerationVersion,
                    spaceKind = (int)value.Type,
                    capabilities = (int)value.Capabilities,
                    lifecycleState = (int)value.LifecycleState
                };
            }

            public DimensionDefinition ToDefinition()
            {
                return new DimensionDefinition(
                    id,
                    displayName,
                    ToInt2(absoluteOrigin),
                    ToBounds(localBounds),
                    generationVersion,
                    DimensionTypeMigration.Normalize(spaceKind),
                    (DimensionCapabilityFlags)capabilities,
                    (DimensionLifecycleState)lifecycleState);
            }
        }

        [Serializable]
        internal sealed class ZoneSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public BoundsSnapshot localBounds;
            public string kind;
            public int priority;
            public bool enabled;

            public static ZoneSnapshot From(DimensionZoneDefinition value)
            {
                return new ZoneSnapshot
                {
                    id = value.ZoneId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    kind = value.Kind,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionZoneDefinition ToDefinition()
            {
                return new DimensionZoneDefinition(
                    id,
                    displayName,
                    dimensionId,
                    ToBounds(localBounds),
                    kind,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class MapLayerSnapshot
        {
            public string id;
            public string dimensionId;
            public string displayName;
            public string description;
            public string iconId;
            public bool hasTintColor;
            public uint tintColorRgba;
            public int priority;
            public bool visible;
            public bool selectable;

            public static MapLayerSnapshot From(DimensionMapLayerDefinition value)
            {
                return new MapLayerSnapshot
                {
                    id = value.LayerId,
                    dimensionId = value.DimensionId,
                    displayName = value.DisplayName,
                    description = value.Description,
                    iconId = value.IconId,
                    hasTintColor = value.HasTintColor,
                    tintColorRgba = value.TintColorRgba,
                    priority = value.Priority,
                    visible = value.Visible,
                    selectable = value.Selectable
                };
            }

            public DimensionMapLayerDefinition ToDefinition()
            {
                return new DimensionMapLayerDefinition(
                    id,
                    dimensionId,
                    displayName,
                    description,
                    iconId,
                    hasTintColor,
                    tintColorRgba,
                    priority,
                    visible,
                    selectable);
            }
        }

        [Serializable]
        internal sealed class MapMarkerSnapshot
        {
            public string id;
            public string dimensionId;
            public Float2Snapshot localPosition;
            public string label;
            public string kind;
            public bool visible;

            public static MapMarkerSnapshot From(DimensionMapMarker value)
            {
                return new MapMarkerSnapshot
                {
                    id = value.MarkerId,
                    dimensionId = value.DimensionId,
                    localPosition = Float2Snapshot.From(value.LocalPosition),
                    label = value.Label,
                    kind = value.Kind,
                    visible = value.Visible
                };
            }

            public DimensionMapMarker ToDefinition()
            {
                return new DimensionMapMarker(
                    id,
                    dimensionId,
                    ToFloat2(localPosition),
                    label,
                    kind,
                    visible);
            }
        }

        [Serializable]
        internal sealed class AnchorSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public Float2Snapshot localPosition;
            public int kind;
            public int priority;
            public bool enabled;

            public static AnchorSnapshot From(DimensionAnchorDefinition value)
            {
                return new AnchorSnapshot
                {
                    id = value.AnchorId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    localPosition = Float2Snapshot.From(value.LocalPosition),
                    kind = (int)value.Kind,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionAnchorDefinition ToDefinition()
            {
                return new DimensionAnchorDefinition(
                    id,
                    displayName,
                    dimensionId,
                    ToFloat2(localPosition),
                    (DimensionAnchorKind)kind,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class BiomeSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string environmentProfileId;
            public string paletteAssetId;
            public string spawnTableId;
            public string resourceTableId;
            public string worldEventTableId;
            public uint mapColorRgba;
            public int priority;
            public bool enabled;
            public string notes;

            public static BiomeSnapshot From(DimensionBiomeDefinition value)
            {
                return new BiomeSnapshot
                {
                    id = value.BiomeId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    environmentProfileId = value.EnvironmentProfileId,
                    paletteAssetId = value.PaletteAssetId,
                    spawnTableId = value.SpawnTableId,
                    resourceTableId = value.ResourceTableId,
                    worldEventTableId = value.WorldEventTableId,
                    mapColorRgba = value.MapColorRgba,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    notes = value.Notes
                };
            }

            public DimensionBiomeDefinition ToDefinition()
            {
                return new DimensionBiomeDefinition(
                    id,
                    displayName,
                    dimensionId,
                    environmentProfileId,
                    paletteAssetId,
                    spawnTableId,
                    resourceTableId,
                    worldEventTableId,
                    mapColorRgba,
                    priority,
                    enabled,
                    notes);
            }
        }
    }
}
