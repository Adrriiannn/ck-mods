using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// The rectangle one generation pass is allowed to touch, and the world-coordinate
    /// conversion that goes with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scoped pass is a promise in the author's own words — "run this step over THIS
    /// rectangle". A provider that reads <c>context.Area.LocalBounds</c> instead runs over the
    /// whole generated area, and nothing says so: the scoping simply has no effect. Three of the
    /// five providers did exactly that, so the resolution lives here now, in one place a fourth
    /// provider can reuse rather than forget.
    /// </para>
    /// <para>
    /// THE INTERSECTION IS NOT OPTIONAL. An authored rectangle is in the dimension's own local
    /// coordinates and can reach past the area currently being generated; a provider that
    /// trusted it unclipped would sample, scan or paint outside the area it was handed. When the
    /// two do not overlap at all the pass has no work — that is a Ready, not a failure, because
    /// the same pass runs again for the next area, which may well overlap.
    /// </para>
    /// <para>
    /// LOCAL AND ABSOLUTE ARE A FIXED OFFSET APART. The area carries both rectangles for the
    /// same tiles, so any local subset converts by the same shift — which is what
    /// <see cref="ToAbsolute"/> exists to keep providers from recomputing by hand, each with
    /// their own chance of subtracting in the wrong direction.
    /// </para>
    /// </remarks>
    public static class DimensionGenerationPassBounds
    {
        /// <summary>
        /// The local rectangle a pass may work in: its authored scope clipped to the area, or
        /// the whole area when the pass carries no scope.
        /// </summary>
        public static DimensionBounds Resolve(DimensionGenerationPassContext context)
        {
            DimensionBounds area = context.GenerationContext.Area.LocalBounds;
            return context.Pass.HasLocalBounds
                ? Intersect(context.Pass.LocalBounds, area)
                : area;
        }

        /// <summary>The overlap of two rectangles, which may come out empty.</summary>
        public static DimensionBounds Intersect(DimensionBounds left, DimensionBounds right)
        {
            return new DimensionBounds(
                math.max(left.Min, right.Min),
                math.min(left.MaxExclusive, right.MaxExclusive));
        }

        /// <summary>Whether a rectangle holds at least one tile.</summary>
        public static bool HasArea(DimensionBounds bounds)
        {
            return bounds.MaxExclusive.x > bounds.Min.x &&
                   bounds.MaxExclusive.y > bounds.Min.y;
        }

        /// <summary>
        /// A local subset of an area, in the world coordinates that area occupies.
        /// </summary>
        public static DimensionBounds ToAbsolute(DimensionArea area, DimensionBounds localSubset)
        {
            int2 shift = area.AbsoluteBounds.Min - area.LocalBounds.Min;
            return new DimensionBounds(
                localSubset.Min + shift,
                localSubset.MaxExclusive + shift);
        }

        /// <summary>
        /// A rectangle as a job-key fragment.
        /// </summary>
        /// <remarks>
        /// Providers cache per-area work under a key. Leaving the pass rectangle out of that key
        /// means two passes scoped to different halves of one area share the first one's job:
        /// the second finds the work already done and places nothing, which reads exactly like a
        /// pass that ran and found no room.
        /// </remarks>
        public static string Key(DimensionBounds bounds)
        {
            return bounds.Min.x + "," + bounds.Min.y + "," +
                   bounds.MaxExclusive.x + "," + bounds.MaxExclusive.y;
        }
    }
}
