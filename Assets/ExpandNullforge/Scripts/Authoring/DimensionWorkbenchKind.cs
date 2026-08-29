namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What kind of crafting station this is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are Core Keeper's own <c>CraftingType</c> values, and the counts are how many of the
    /// game's 85 crafting stations use each. <see cref="Workbench"/> is not merely the default — it is
    /// 50 of the 85, and it is the only one whose behaviour is simply "shows a recipe list". The rest
    /// are machines with their own UI and their own rules, and naming a station one of them without
    /// building the rest of the machine produces a station that opens the wrong window.
    /// </para>
    /// <para>
    /// The specialised kinds are exposed because a modder reskinning a furnace or a cooking pot has
    /// every right to, and refusing would be the framework deciding for them. They are simply not the
    /// easy path.
    /// </para>
    /// </remarks>
    public enum DimensionWorkbenchKind
    {
        /// <summary>An ordinary workbench: pick a recipe, it makes the thing. (50 of 85)</summary>
        Workbench = 0,

        /// <summary>A machine that turns one resource into another, like a furnace. (7)</summary>
        ProcessResources = 1,

        /// <summary>A boss summoning statue. (3)</summary>
        BossStatue = 2,

        /// <summary>A cooking pot. (1)</summary>
        Cooking = 3,

        /// <summary>A cattle station — a feed trough or breeding pen. (12)</summary>
        Cattle = 4,

        /// <summary>Pulls something out of something else, like a seed extractor. (2)</summary>
        Extract = 5,

        /// <summary>Destroys what is put into it. (1)</summary>
        Incinerate = 6,

        /// <summary>A biome boss statue. (6)</summary>
        BiomeBossStatue = 7,

        /// <summary>A fishing station. (1)</summary>
        Fishing = 8,

        /// <summary>The Hydra's biome boss statue. (1)</summary>
        BiomeBossHydraStatue = 9,

        /// <summary>A critter-catching station. (1)</summary>
        CritterCatching = 10
    }
}
