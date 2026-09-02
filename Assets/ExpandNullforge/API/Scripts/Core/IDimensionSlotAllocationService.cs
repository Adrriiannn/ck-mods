namespace ExpandNullforge.Api
{
    /// <summary>
    /// Allocates collision-safe absolute coordinate slots for custom dimensions.
    /// Dimension mods should use this before registering a non-overworld dimension
    /// instead of hardcoding shared coordinates such as 100000,100000.
    /// </summary>
    public interface IDimensionSlotAllocationService
    {
        DimensionSlotAllocationResult AllocateDimensionSlot(DimensionSlotAllocationRequest request);
        DimensionSlotAllocationResult ReserveDimensionSlot(
            DimensionSlotAllocationRequest request,
            out DimensionSlotRecord slot);
        bool TryGetReservedDimensionSlot(string dimensionId, out DimensionSlotRecord slot);
    }
}
