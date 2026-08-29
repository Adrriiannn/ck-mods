namespace ExpandNullforge.Api
{
    /// <summary>
    /// The retired ancestor of <see cref="DimensionType"/>.
    /// </summary>
    /// <remarks>
    /// Kept for one release so stale generated bootstrap source on modders' disks still
    /// compiles until they regenerate. Values 2–5 never had a single enforcing consumer;
    /// everything normalizes to <see cref="DimensionType.World"/> through
    /// <see cref="DimensionTypeMigration.Normalize"/>.
    /// </remarks>
    [System.Obsolete("Use DimensionType. This enum's values were labels nothing enforced; they normalize to DimensionType.World.")]
    public enum DimensionSpaceKind
    {
        Overworld = 0,
        PocketWorld = 1,
        ExpansionRegion = 2,
        VirtualLayer = 3,
        ExternalWorld = 4,
        RuntimeOnly = 5
    }
}
