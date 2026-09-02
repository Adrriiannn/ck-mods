using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The box arithmetic the compiler works in: aligning, merging, overlapping.
    /// </summary>
    public static partial class DimensionTemplateCompiler
    {
        private static bool TryResolvePlayableBounds(
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionCompiledScenePlacement> scenePlacements,
            List<DimensionGenerationPassDefinition> generationPasses,
            out DimensionBounds bounds)
        {
            bool found = false;
            DimensionBounds result = new DimensionBounds(new int2(0, 0), new int2(0, 0));

            for (int i = 0; i < biomeRegions.Count; i++)
            {
                Merge(ref found, ref result, biomeRegions[i].LocalBounds);
            }

            for (int i = 0; i < scenePlacements.Count; i++)
            {
                DimensionCompiledScenePlacement scene = scenePlacements[i];
                if (scene.HasLocalBounds)
                {
                    Merge(ref found, ref result, scene.LocalBounds);
                }
            }

            for (int i = 0; i < generationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition pass = generationPasses[i];
                if (pass.HasLocalBounds)
                {
                    Merge(ref found, ref result, pass.LocalBounds);
                }
            }

            bounds = result;
            return found;
        }

        private static DimensionBounds AlignToSquareBounds(DimensionBounds source, int alignment)
        {
            int2 size = source.Size;
            int side = math.max(size.x, size.y);
            side = AlignUp(math.max(side, alignment), alignment);

            int extraX = side - size.x;
            int extraY = side - size.y;
            int2 min = new int2(
                source.Min.x - extraX / 2,
                source.Min.y - extraY / 2);
            int2 max = min + new int2(side, side);

            return new DimensionBounds(min, max);
        }

        private static DimensionBounds CreateCenteredBounds(int side)
        {
            int half = side / 2;
            return new DimensionBounds(new int2(-half, -half), new int2(side - half, side - half));
        }

        private static int ResolveShellPadding(DimensionBounds playableBounds)
        {
            int2 size = playableBounds.Size;
            int dominantSide = math.max(size.x, size.y);
            int padding = math.max(MinimumShellPaddingTiles, dominantSide);
            padding = math.min(MaximumShellPaddingTiles, padding);
            return AlignUp(padding, BoundsAlignmentTiles);
        }

        private static void Merge(ref bool found, ref DimensionBounds result, DimensionBounds next)
        {
            if (!IsValidBounds(next))
            {
                return;
            }

            if (!found)
            {
                result = next;
                found = true;
                return;
            }

            result = Union(result, next);
        }

        private static bool IsValidBounds(DimensionBounds bounds)
        {
            return bounds.MaxExclusive.x > bounds.Min.x &&
                   bounds.MaxExclusive.y > bounds.Min.y;
        }

        private static bool Contains(DimensionBounds outer, DimensionBounds inner)
        {
            return inner.Min.x >= outer.Min.x &&
                   inner.Min.y >= outer.Min.y &&
                   inner.MaxExclusive.x <= outer.MaxExclusive.x &&
                   inner.MaxExclusive.y <= outer.MaxExclusive.y;
        }

        private static bool Intersects(DimensionBounds a, DimensionBounds b)
        {
            return a.Min.x < b.MaxExclusive.x &&
                   a.MaxExclusive.x > b.Min.x &&
                   a.Min.y < b.MaxExclusive.y &&
                   a.MaxExclusive.y > b.Min.y;
        }

        private static DimensionBounds Union(DimensionBounds a, DimensionBounds b)
        {
            return new DimensionBounds(
                new int2(math.min(a.Min.x, b.Min.x), math.min(a.Min.y, b.Min.y)),
                new int2(math.max(a.MaxExclusive.x, b.MaxExclusive.x), math.max(a.MaxExclusive.y, b.MaxExclusive.y)));
        }

        private static DimensionBounds ExpandBounds(DimensionBounds bounds, int padding)
        {
            int resolvedPadding = math.max(0, padding);
            return new DimensionBounds(
                bounds.Min - new int2(resolvedPadding, resolvedPadding),
                bounds.MaxExclusive + new int2(resolvedPadding, resolvedPadding));
        }

        private static DimensionBounds Intersection(DimensionBounds a, DimensionBounds b)
        {
            return new DimensionBounds(
                new int2(math.max(a.Min.x, b.Min.x), math.max(a.Min.y, b.Min.y)),
                new int2(math.min(a.MaxExclusive.x, b.MaxExclusive.x), math.min(a.MaxExclusive.y, b.MaxExclusive.y)));
        }

        private static int AlignUp(int value, int alignment)
        {
            if (alignment <= 1)
            {
                return value;
            }

            int remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        private static int FloorToMultiple(int value, int alignment)
        {
            if (alignment <= 1)
            {
                return value;
            }

            int remainder = value % alignment;
            if (remainder == 0)
            {
                return value;
            }

            return value < 0 ? value - alignment - remainder : value - remainder;
        }
    }
}
