using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Convenience helpers for mods that want to be dimension-aware without
    /// depending on the concrete ExpandNullforge runtime implementation.
    /// </summary>
    /// <remarks>
    /// NOTHING IN THIS FRAMEWORK CALLS ANY OF THESE. The whole class has no reference outside its
    /// own file: it exists for consumer mods, and the framework reaches the service directly. That
    /// makes it look dead to every reference check, so the note is here rather than being
    /// rediscovered — these are kept on purpose, and removing one is a breaking change for a
    /// consumer, not a tidy-up.
    /// </remarks>
    public static class DimensionApiCoordinates
    {
        public static bool TryGetContextForAbsolute(
            float2 absolutePosition,
            out DimensionContext context)
        {
            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                context = DimensionContext.Unknown(absolutePosition);
                return false;
            }

            context = service.GetContextForAbsolute(absolutePosition);
            return context.IsKnown;
        }

        /// <summary>
        /// Strict conversion helper. The absolute position must be inside the
        /// dimension's playable bounds.
        /// </summary>
        public static bool TryGetLocalPosition(
            float2 absolutePosition,
            out string dimensionId,
            out float2 localPosition)
        {
            DimensionContext context;
            if (!TryGetContextForAbsolute(absolutePosition, out context))
            {
                dimensionId = string.Empty;
                localPosition = default(float2);
                return false;
            }

            dimensionId = context.DimensionId;
            localPosition = context.LocalPosition;
            return true;
        }

        /// <summary>
        /// Presentation/interop conversion helper. The absolute position may be
        /// inside a dimension's coordinate domain shell even when it is just
        /// outside the playable area.
        /// </summary>
        public static bool TryGetCoordinateContextForAbsolute(
            float2 absolutePosition,
            out DimensionContext context)
        {
            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                context = DimensionContext.Unknown(absolutePosition);
                return false;
            }

            context = service.GetCoordinateContextForAbsolute(absolutePosition);
            return context.IsKnown;
        }

        /// <summary>
        /// Presentation/interop local-position helper for map cursors, chat UI,
        /// and command previews.
        /// </summary>
        public static bool TryGetCoordinateLocalPosition(
            float2 absolutePosition,
            out string dimensionId,
            out float2 localPosition)
        {
            DimensionContext context;
            if (!TryGetCoordinateContextForAbsolute(absolutePosition, out context))
            {
                dimensionId = string.Empty;
                localPosition = default(float2);
                return false;
            }

            dimensionId = context.DimensionId;
            localPosition = context.LocalPosition;
            return true;
        }

        public static bool TryResolvePlayerLocalPosition(
            Entity player,
            float2 localPosition,
            out DimensionCoordinateCompatibilityResult result)
        {
            return TryResolveForDimensionAwareOperation(
                new DimensionCoordinateCompatibilityRequest(
                    player,
                    string.Empty,
                    localPosition,
                    true,
                    true,
                    "player-local-coordinate",
                    "Resolve a local coordinate in the player's current dimension."),
                out result);
        }

        public static bool TryGetCoordinateDomain(
            string dimensionId,
            out DimensionCoordinateDomain domain)
        {
            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                domain = default(DimensionCoordinateDomain);
                return false;
            }

            return service.TryGetCoordinateDomain(dimensionId, out domain);
        }

        public static bool TryGetCoordinateDomainAtAbsolute(
            float2 absolutePosition,
            out DimensionCoordinateDomain domain)
        {
            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                domain = default(DimensionCoordinateDomain);
                return false;
            }

            return service.TryGetCoordinateDomainAtAbsolute(absolutePosition, out domain);
        }

        public static bool TryGetAbsolutePosition(
            string dimensionId,
            float2 localPosition,
            out float2 absolutePosition)
        {
            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                absolutePosition = default(float2);
                return false;
            }

            return service.TryToAbsolute(dimensionId, localPosition, out absolutePosition);
        }

        public static bool TryGetPlayerContext(
            Entity player,
            out DimensionContext context)
        {
            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                context = DimensionContext.Unknown(default(float2));
                return false;
            }

            return service.TryGetPlayerContext(player, out context) && context.IsKnown;
        }

        public static bool TryResolveForDimensionAwareOperation(
            DimensionCoordinateCompatibilityRequest request,
            out DimensionCoordinateCompatibilityResult result)
        {
            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                result =
                    DimensionCoordinateCompatibilityResult.Failed(
                        "dimension-service-unavailable",
                        "The dimension service is not available.",
                        DimensionContext.Unknown(default(float2)));
                return false;
            }

            result = service.ResolveCoordinateForDimensionAwareOperation(request);
            return result.Resolved;
        }
    }
}
