using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    [Serializable]
    internal sealed partial class DimensionRuntimeManifestSnapshot
    {
        public ContentPackSnapshot[] contentPacks = new ContentPackSnapshot[0];
        public DimensionSnapshot[] dimensions = new DimensionSnapshot[0];
        public ZoneSnapshot[] zones = new ZoneSnapshot[0];
        public MapLayerSnapshot[] mapLayers = new MapLayerSnapshot[0];
        public MapMarkerSnapshot[] mapMarkers = new MapMarkerSnapshot[0];
        public AnchorSnapshot[] anchors = new AnchorSnapshot[0];
        public PortalSnapshot[] portals = new PortalSnapshot[0];
        public PortalPresentationSnapshot[] portalPresentations = new PortalPresentationSnapshot[0];
        public TravelRequirementSnapshot[] travelRequirements = new TravelRequirementSnapshot[0];
        public StarterSnapshot[] starters = new StarterSnapshot[0];
        public SceneTemplateSnapshot[] sceneTemplates = new SceneTemplateSnapshot[0];
        public SceneSnapshot[] scenes = new SceneSnapshot[0];
        public EncounterSnapshot[] encounters = new EncounterSnapshot[0];
        public GenerationPassSnapshot[] generationPasses = new GenerationPassSnapshot[0];
        public ProgressFlagSnapshot[] progressFlags = new ProgressFlagSnapshot[0];
        public WorldEventSnapshot[] worldEvents = new WorldEventSnapshot[0];
        public OwnershipBindingSnapshot[] ownershipBindings = new OwnershipBindingSnapshot[0];
        public AssetReferenceSnapshot[] assetReferences = new AssetReferenceSnapshot[0];
        public BiomeSnapshot[] biomes = new BiomeSnapshot[0];

        public static DimensionRuntimeManifestSnapshot FromManifest(DimensionContentManifest manifest)
        {
            return new DimensionRuntimeManifestSnapshot
            {
                contentPacks = Convert(manifest.ContentPacks, ContentPackSnapshot.From),
                dimensions = Convert(manifest.Dimensions, DimensionSnapshot.From),
                zones = Convert(manifest.Zones, ZoneSnapshot.From),
                mapLayers = Convert(manifest.MapLayers, MapLayerSnapshot.From),
                mapMarkers = Convert(manifest.MapMarkers, MapMarkerSnapshot.From),
                anchors = Convert(manifest.Anchors, AnchorSnapshot.From),
                portals = Convert(manifest.Portals, PortalSnapshot.From),
                portalPresentations = Convert(manifest.PortalPresentations, PortalPresentationSnapshot.From),
                travelRequirements = Convert(manifest.TravelRequirements, TravelRequirementSnapshot.From),
                starters = Convert(manifest.Starters, StarterSnapshot.From),
                sceneTemplates = Convert(manifest.SceneTemplates, SceneTemplateSnapshot.From),
                scenes = Convert(manifest.Scenes, SceneSnapshot.From),
                encounters = Convert(manifest.Encounters, EncounterSnapshot.From),
                generationPasses = Convert(manifest.GenerationPasses, GenerationPassSnapshot.From),
                progressFlags = Convert(manifest.ProgressFlags, ProgressFlagSnapshot.From),
                worldEvents = Convert(manifest.WorldEvents, WorldEventSnapshot.From),
                ownershipBindings = Convert(manifest.OwnershipBindings, OwnershipBindingSnapshot.From),
                assetReferences = Convert(manifest.AssetReferences, AssetReferenceSnapshot.From),
                biomes = Convert(manifest.Biomes, BiomeSnapshot.From)
            };
        }

        public DimensionContentManifest ToManifest()
        {
            DimensionContentManifest baseManifest =
                new DimensionContentManifest(
                    Convert(contentPacks, record => record.ToDefinition()),
                    Convert(dimensions, record => record.ToDefinition()),
                    Convert(zones, record => record.ToDefinition()),
                    Convert(mapLayers, record => record.ToDefinition()),
                    Convert(mapMarkers, record => record.ToDefinition()),
                    Convert(anchors, record => record.ToDefinition()),
                    Convert(portals, record => record.ToDefinition()),
                    Convert(portalPresentations, record => record.ToDefinition()),
                    Convert(travelRequirements, record => record.ToDefinition()),
                    Convert(sceneTemplates, record => record.ToDefinition()),
                    Convert(scenes, record => record.ToDefinition()),
                    Convert(encounters, record => record.ToDefinition()),
                    Convert(generationPasses, record => record.ToDefinition()),
                    Convert(progressFlags, record => record.ToDefinition()),
                    Convert(worldEvents, record => record.ToDefinition()),
                    Convert(ownershipBindings, record => record.ToDefinition()),
                    Convert(assetReferences, record => record.ToDefinition()),
                    Convert(biomes, record => record.ToDefinition()));

            return new DimensionContentManifest(
                baseManifest,
                Convert(starters, record => record.ToDefinition()));
        }

        private static TOut[] Convert<TIn, TOut>(
            IReadOnlyList<TIn> source,
            Func<TIn, TOut> convert)
        {
            if (source == null || source.Count == 0)
            {
                return new TOut[0];
            }

            List<TOut> result = new List<TOut>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(convert(source[i]));
            }

            return result.ToArray();
        }

        private static List<TOut> Convert<TIn, TOut>(
            TIn[] source,
            Func<TIn, TOut> convert)
            where TIn : class
        {
            List<TOut> result = new List<TOut>();
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    result.Add(convert(source[i]));
                }
            }

            return result;
        }

        [Serializable]
        internal sealed class BoundsSnapshot
        {
            public int minX;
            public int minY;
            public int maxX;
            public int maxY;

            public static BoundsSnapshot From(DimensionBounds bounds)
            {
                return new BoundsSnapshot
                {
                    minX = bounds.Min.x,
                    minY = bounds.Min.y,
                    maxX = bounds.MaxExclusive.x,
                    maxY = bounds.MaxExclusive.y
                };
            }

            public DimensionBounds ToBounds()
            {
                return new DimensionBounds(
                    new int2(minX, minY),
                    new int2(maxX, maxY));
            }
        }

        [Serializable]
        internal sealed class Float2Snapshot
        {
            public float x;
            public float y;

            public static Float2Snapshot From(float2 value)
            {
                return new Float2Snapshot { x = value.x, y = value.y };
            }

            public float2 ToFloat2()
            {
                return new float2(x, y);
            }
        }

        [Serializable]
        internal sealed class Int2Snapshot
        {
            public int x;
            public int y;

            public static Int2Snapshot From(int2 value)
            {
                return new Int2Snapshot { x = value.x, y = value.y };
            }

            public int2 ToInt2()
            {
                return new int2(x, y);
            }
        }

        private static string[] CopyStrings(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return new string[0];
            }

            string[] result = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i] ?? string.Empty;
            }

            return result;
        }

        private static DimensionBounds ToBounds(BoundsSnapshot snapshot)
        {
            return snapshot == null
                ? new DimensionBounds(default(int2), default(int2))
                : snapshot.ToBounds();
        }

        private static float2 ToFloat2(Float2Snapshot snapshot)
        {
            return snapshot == null ? default(float2) : snapshot.ToFloat2();
        }

        private static int2 ToInt2(Int2Snapshot snapshot)
        {
            return snapshot == null ? default(int2) : snapshot.ToInt2();
        }
    }
}
