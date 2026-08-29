namespace ExpandNullforge.Api
{
    public static class DimensionGenerationProviderIds
    {
        public const string SafePlatform = "expandnullforge:safe-platform";

        /// <summary>Writes a painted tile map into the dimension via the Burst TileAccessor path.</summary>
        public const string TileMap = "expandnullforge:tile-map";

        /// <summary>Places the dimension's authored scenes after terrain, honouring their placement rules.</summary>
        public const string ScenePlacement = "expandnullforge:scene-placement";

        /// <summary>Grows ore veins in generated walls, for dimensions without a painted map.</summary>
        public const string OreScatter = "expandnullforge:ore-scatter";

        /// <summary>Grows a dimension's authored dungeons inside it, after terrain, before scenes.</summary>
        public const string DungeonPlacement = "expandnullforge:dungeon-placement";
    }
}
