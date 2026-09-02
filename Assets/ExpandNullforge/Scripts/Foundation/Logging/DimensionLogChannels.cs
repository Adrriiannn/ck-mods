namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// The subjects a diagnostic line can be about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NAMED AFTER THE RUNTIME FOLDERS, so a channel name is guessable from the code that prints
    /// it. A tester asking "why isn't my creature attacking" is asking a subject question, not a
    /// volume question, which is why detail is switched on per channel rather than by a number.
    /// </para>
    /// <para>
    /// CONSTANTS AND NOT AN ENUM, deliberately: they are greppable, they survive being written into
    /// a config file by hand, and the generated consumer bootstrap only ever sees strings.
    /// </para>
    /// </remarks>
    internal static class DimensionLogChannels
    {
        public const string Boot = "boot";
        public const string World = "world";
        public const string Service = "service";
        public const string Registry = "registry";
        public const string Audit = "audit";
        public const string Patch = "patch";
        public const string Item = "item";
        public const string Object = "object";
        public const string Tileset = "tileset";
        public const string Portal = "portal";
        public const string Travel = "travel";
        public const string Network = "network";
        public const string Persist = "persist";
        public const string Generate = "generate";
        public const string Scene = "scene";
        public const string Dungeon = "dungeon";
        public const string Biome = "biome";
        public const string Zone = "zone";
        public const string Creature = "creature";
        public const string Boss = "boss";
        public const string Plant = "plant";
        public const string Food = "food";
        public const string Loot = "loot";
        public const string Condition = "condition";
        public const string Skill = "skill";
        public const string Explosive = "explosive";
        public const string Vehicle = "vehicle";
        public const string Container = "container";
        public const string WorldRule = "worldrule";
        public const string Sound = "sound";
        public const string Text = "text";
        public const string Ui = "ui";

        /// <summary>
        /// The channel a call site that has not been given a real one prints on.
        /// </summary>
        /// <remarks>
        /// Every message that still goes through <see cref="DimensionFrameworkLog"/> lands here.
        /// It is not a subject and nobody should switch it on to answer a question; it exists so
        /// that moving 130 call sites onto real channels can happen a few at a time instead of in
        /// one commit, and it goes away with the last of them.
        /// </remarks>
        public const string Legacy = "legacy";

        /// <summary>Every channel, so a config file can list them and a test can check them.</summary>
        public static readonly string[] All =
        {
            Boot, World, Service, Registry, Audit, Patch,
            Item, Object, Tileset, Portal, Travel, Network,
            Persist, Generate, Scene, Dungeon, Biome, Zone,
            Creature, Boss, Plant, Food, Loot, Condition,
            Skill, Explosive, Vehicle, Container, WorldRule, Sound,
            Text, Ui, Legacy,
        };
    }
}
