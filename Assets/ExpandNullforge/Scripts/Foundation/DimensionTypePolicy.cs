using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// What a dimension's type actually DOES — the behavior bundle behind the word.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every flag here has an enforcing consumer, and that is the admission rule: a flag
    /// nothing reads is a lie waiting to be authored against. The old space kinds died of
    /// exactly that.
    /// </para>
    /// <para>
    /// World is deliberately all-off: it is today's behavior under its honest name, and the
    /// golden tests hold it byte-identical to what shipped before types existed.
    /// </para>
    /// </remarks>
    public sealed class DimensionTypePolicy
    {
        private static readonly DimensionTypePolicy WorldPolicy = new DimensionTypePolicy();

        private static readonly DimensionTypePolicy DungeonPolicy = new DimensionTypePolicy
        {
            BlocksAmbientSpawns = true,
            HasMusicOverride = true,
            RequiresReturnPortal = true
        };

        private static readonly DimensionTypePolicy ArenaPolicy = new DimensionTypePolicy
        {
            BlocksAmbientSpawns = true,
            ResetsWhenEmpty = true,
            ReturnPortalArmedByVictory = true,
            RequiresReturnPortal = true,
            WarnOnProceduralContent = true
        };

        private static readonly DimensionTypePolicy RoomPolicy = new DimensionTypePolicy
        {
            BlocksAmbientSpawns = true,
            WarnOnProceduralContent = true
        };

        /// <summary>Vanilla ambient wildlife stays out; the authored population is the population.</summary>
        public bool BlocksAmbientSpawns { get; private set; }

        /// <summary>The dimension owns its music, biome and sub-biome rules notwithstanding.</summary>
        public bool HasMusicOverride { get; private set; }

        /// <summary>The dimension wipes and regenerates once the last player leaves.</summary>
        public bool ResetsWhenEmpty { get; private set; }

        /// <summary>The return portal appears on victory rather than being always on.</summary>
        public bool ReturnPortalArmedByVictory { get; private set; }

        /// <summary>A missing return portal is worth a loud diagnostic — the place can trap.</summary>
        public bool RequiresReturnPortal { get; private set; }

        /// <summary>Procedural content registered against it gets a what-are-you-doing warning.</summary>
        public bool WarnOnProceduralContent { get; private set; }

        public static DimensionTypePolicy For(DimensionType type)
        {
            switch (type)
            {
                case DimensionType.Dungeon:
                    return DungeonPolicy;
                case DimensionType.Arena:
                    return ArenaPolicy;
                case DimensionType.Room:
                    return RoomPolicy;
                default:
                    return WorldPolicy;
            }
        }
    }
}
