namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What every creature asset has in common: the game object it becomes.
    /// </summary>
    /// <remarks>
    /// Mobs, animals, critters and bosses are four separate assets with no base class between them,
    /// on purpose — each one asks a different set of questions, and a shared base would have to carry
    /// all of them. They do share this, and validation needs to read it across all four. Reading it
    /// with <c>Type.GetField</c> is what the mod sandbox refuses; naming the shape they
    /// already have costs nothing and says the same thing to the compiler.
    /// </remarks>
    public interface IDimensionCreatureAsset
    {
        /// <summary>The object id this creature is registered under.</summary>
        string ObjectId { get; }
    }

    /// <summary>
    /// A creature that drops something when it dies.
    /// </summary>
    /// <remarks>
    /// Mobs, animals and bosses. Critters deliberately do not implement this: they have no loot
    /// table field at all, so asking one whether it drops a given table can only ever answer no.
    /// </remarks>
    public interface IDimensionLootBearingAsset : IDimensionCreatureAsset
    {
        /// <summary>The table rolled when this creature dies, or null if it drops nothing.</summary>
        DimensionLootTableAsset LootTable { get; }
    }
}
