using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public readonly struct DimensionTemplateAuthoringExampleDescriptor
    {
        public readonly string ExampleId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string LayoutPresetId;
        public readonly int RecommendedHalfSizeTiles;
        public readonly int RecommendedShellPaddingTiles;

        public DimensionTemplateAuthoringExampleDescriptor(
            string exampleId,
            string displayName,
            string description,
            string layoutPresetId,
            int recommendedHalfSizeTiles,
            int recommendedShellPaddingTiles)
        {
            ExampleId = exampleId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            LayoutPresetId = layoutPresetId ?? string.Empty;
            RecommendedHalfSizeTiles = recommendedHalfSizeTiles < 1 ? 1 : recommendedHalfSizeTiles;
            RecommendedShellPaddingTiles = recommendedShellPaddingTiles < 0
                ? 0
                : recommendedShellPaddingTiles;
        }
    }

    public static class DimensionTemplateAuthoringExampleCatalog
    {
        public const string MinimalRoomExampleId = "minimal-room";
        public const string GridDungeonExampleId = "grid-dungeon";
        public const string RadialBiomeBandsExampleId = "radial-biome-bands";

        private static readonly DimensionTemplateAuthoringExampleDescriptor[] Examples =
        {
            new DimensionTemplateAuthoringExampleDescriptor(
                MinimalRoomExampleId,
                "Starter Room",
                "A tiny first-biome room for validating dimension travel, local coordinates, and export plumbing before you design the full biome layout.",
                DimensionLayoutTemplatePresetCatalog.SingleBiomeSquarePresetId,
                8,
                128),
            new DimensionTemplateAuthoringExampleDescriptor(
                RadialBiomeBandsExampleId,
                "Radial Starter",
                "A ring-based bootstrap for testing vanilla-like outward progression. Use the layout editor for sectors, rings, sub-biomes, and final biome placement.",
                DimensionLayoutTemplatePresetCatalog.RadialRingsPresetId,
                128,
                512)
        };

        public static IReadOnlyList<DimensionTemplateAuthoringExampleDescriptor> GetExamples()
        {
            return Examples;
        }

        public static bool TryGetExample(
            string exampleId,
            out DimensionTemplateAuthoringExampleDescriptor example)
        {
            string resolvedId = exampleId ?? string.Empty;
            for (int i = 0; i < Examples.Length; i++)
            {
                if (Examples[i].ExampleId == resolvedId)
                {
                    example = Examples[i];
                    return true;
                }
            }

            example = default(DimensionTemplateAuthoringExampleDescriptor);
            return false;
        }

        public static DimensionTemplateStarterRequest CreateStarterRequest(
            string exampleId,
            string dimensionId,
            string displayName)
        {
            DimensionTemplateAuthoringExampleDescriptor example;
            if (!TryGetExample(exampleId, out example))
            {
                TryGetExample(MinimalRoomExampleId, out example);
            }

            DimensionTemplateStarterRequest request =
                DimensionTemplateStarterRequest.CreateDefault(
                    string.IsNullOrEmpty(dimensionId)
                        ? "example.dimension." + example.ExampleId
                        : dimensionId,
                    string.IsNullOrEmpty(displayName)
                        ? example.DisplayName
                        : displayName);
            request.DimensionDescription = example.Description;
            request.HalfSizeTiles = example.RecommendedHalfSizeTiles;
            request.ReservedShellPaddingTiles = example.RecommendedShellPaddingTiles;
            ApplyExamplePalette(request, example.ExampleId);
            return request;
        }

        public static DimensionTemplateStarterGraph CreateStarterGraph(
            string exampleId,
            string dimensionId,
            string displayName)
        {
            DimensionTemplateAuthoringExampleDescriptor example;
            if (!TryGetExample(exampleId, out example))
            {
                TryGetExample(MinimalRoomExampleId, out example);
            }

            DimensionTemplateStarterRequest request =
                CreateStarterRequest(example.ExampleId, dimensionId, displayName);
            DimensionTemplateStarterGraph graph =
                DimensionTemplateStarterFactory.CreateSingleBiomeStarter(request);
            ApplyExampleLayout(graph, request, example);
            return graph;
        }

        public static DimensionTemplateStarterGraph CreateStarterGraph(
            string exampleId,
            DimensionTemplateStarterRequest request)
        {
            DimensionTemplateAuthoringExampleDescriptor example;
            if (!TryGetExample(exampleId, out example))
            {
                TryGetExample(MinimalRoomExampleId, out example);
            }

            DimensionTemplateStarterRequest resolvedRequest =
                request ?? CreateStarterRequest(
                    example.ExampleId,
                    "example.dimension." + example.ExampleId,
                    example.DisplayName);
            DimensionTemplateStarterGraph graph =
                DimensionTemplateStarterFactory.CreateSingleBiomeStarter(resolvedRequest);
            ApplyExampleLayout(graph, resolvedRequest, example);
            return graph;
        }

        public static DimensionTemplateAuthoringWorkspace CreateWorkspace(
            string exampleId,
            string dimensionId,
            string displayName)
        {
            DimensionTemplateStarterGraph graph =
                CreateStarterGraph(exampleId, dimensionId, displayName);
            return DimensionTemplateAuthoringWorkspaceUtility.BuildWorkspace(
                graph,
                string.Empty);
        }

        private static void ApplyExampleLayout(
            DimensionTemplateStarterGraph graph,
            DimensionTemplateStarterRequest request,
            DimensionTemplateAuthoringExampleDescriptor example)
        {
            if (graph == null || graph.Layout == null)
            {
                return;
            }

            DimensionLayoutTemplatePresetRequest presetRequest =
                new DimensionLayoutTemplatePresetRequest();
            presetRequest.PresetId = example.LayoutPresetId;
            presetRequest.PrimaryBiomeId = request.BiomeId;
            presetRequest.SecondaryBiomeId = request.BiomeId;
            presetRequest.ZoneId = request.ZoneId;
            presetRequest.HalfSizeTiles = request.HalfSizeTiles;
            presetRequest.Columns = 3;
            presetRequest.Rows = 3;
            presetRequest.CellSizeTiles = Mathf.Max(16, request.HalfSizeTiles / 2);
            presetRequest.RingWidthTiles = Mathf.Max(16, request.HalfSizeTiles / 3);
            presetRequest.RingCount = 3;
            presetRequest.BiomeIds = new[] { request.BiomeId };
            string unusedMessage;
            DimensionLayoutTemplatePresetCatalog.TryApplyBuiltInPreset(
                graph.Layout,
                presetRequest,
                out unusedMessage);
        }

        private static void ApplyExamplePalette(
            DimensionTemplateStarterRequest request,
            string exampleId)
        {
            if (request == null)
            {
                return;
            }

            if (exampleId == GridDungeonExampleId)
            {
                request.BiomeMapColor = new Color(0.25f, 0.32f, 0.55f, 1f);
                request.FloorResourceKey = "example.grid.floor";
                request.WallResourceKey = "example.grid.wall";
                request.OreResourceKey = "example.grid.ore";
                request.ObjectResourceKey = "example.grid.object";
                return;
            }

            if (exampleId == RadialBiomeBandsExampleId)
            {
                request.BiomeMapColor = new Color(0.30f, 0.50f, 0.40f, 1f);
                request.FloorResourceKey = "example.radial.floor";
                request.WallResourceKey = "example.radial.wall";
                request.LiquidResourceKey = "example.radial.liquid";
                request.OreResourceKey = "example.radial.ore";
                return;
            }

            request.BiomeMapColor = new Color(0.22f, 0.44f, 0.52f, 1f);
            request.FloorResourceKey = "example.room.floor";
            request.WallResourceKey = "example.room.wall";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    return values[i];
                }
            }

            return string.Empty;
        }
    }
}
