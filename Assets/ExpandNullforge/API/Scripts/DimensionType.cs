namespace ExpandNullforge.Api
{
    /// <summary>
    /// What kind of place a dimension is — a behavior bundle, not a label.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each value carries a policy the runtime enforces: a Dungeon keeps ambient wildlife out
    /// and owns its music; an Arena resets between visits and arms its exit on victory; a Room
    /// is an authored stamp that stays exactly as authored. World is today's behavior under its
    /// honest name.
    /// </para>
    /// <para>
    /// Zero is deliberately reserved and never written again: it is the raw int the legacy
    /// Overworld record carries, and it normalizes to World on read. The old enum's values 2–5
    /// were labels nothing ever enforced — the kind of configuration that teaches authors not
    /// to trust configuration — and they normalize to World too.
    /// </para>
    /// </remarks>
    public enum DimensionType
    {
        World = 1,
        Dungeon = 2,
        Arena = 3,
        Room = 4
    }

    /// <summary>The one bridge between persisted raw ints and the live enum.</summary>
    public static class DimensionTypeMigration
    {
        /// <summary>
        /// Raw persisted int to a type. 0 (legacy Overworld) and 1 (legacy PocketWorld) are
        /// the only values any existing record carries; both — and anything else unknown —
        /// come back as World, so an old world loads with today's behavior rather than none.
        /// </summary>
        public static DimensionType Normalize(int raw)
        {
            return raw >= (int)DimensionType.World && raw <= (int)DimensionType.Room
                ? (DimensionType)raw
                : DimensionType.World;
        }
    }
}
