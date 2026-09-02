namespace ExpandNullforge.Api
{
    public enum DimensionAuthoringContentSummaryKind
    {
        Dimension = 0,
        LayoutTemplate = 1,
        Biome = 2,
        BiomeContentPreset = 3,
        EnvironmentProfile = 4,
        BiomePalette = 5,
        BiomePaletteEntry = 6,
        // 7 was BiomeGenerationProfile, the separate asset that held a biome's generation passes.
        // Its passes were folded into the biome itself, so there is nothing left to summarise. The
        // number stays retired rather than reused, so an old saved preview cannot come back as a
        // different kind of record.
        GenerationPass = 8,
        GenerationTable = 9,
        GenerationTableEntry = 10,
        SceneTemplate = 11,
        ResourceNode = 12,
        SpawnRule = 13,
        SemanticFloorObject = 14,
        SemanticWallObject = 15,
        SemanticOreObject = 16,
        SemanticWaterObject = 17,
        Item = 18,
        Recipe = 19,
        Workbench = 20,
        LootTable = 21,
        Animal = 22,
        Critter = 23,
        Mob = 24,
        Boss = 25,
        SceneProp = 26,
        SceneLootContainer = 27,
        SceneSpawnPoint = 28,
        SceneTrigger = 29
    }
}
