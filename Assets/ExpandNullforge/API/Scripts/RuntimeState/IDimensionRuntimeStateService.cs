namespace ExpandNullforge.Api
{
    /// <summary>
    /// Optional runtime-state surface exposed by the active dimension service.
    /// Gameplay systems use this to wake only while dimension work is active,
    /// without depending on a concrete built-in dimension implementation.
    /// </summary>
    public interface IDimensionRuntimeStateService
    {
        bool HasAttachedServerWorld { get; }
        bool HasAttachedClientWorld { get; }
        bool HasActiveRuntimeLoadingWork { get; }
        bool HasActiveRuntimeGenerationWork { get; }
        bool HasActiveRuntimeTravelWork { get; }
        bool HasActiveRuntimeWork { get; }
        double RuntimeNow { get; }
        bool HasTrackedPlayerInDimension(string dimensionId);
    }
}
