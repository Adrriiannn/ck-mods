using System;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Semantic role of a tile inside a consumer-authored tileset. The role decides the
    /// behaviour the framework must generate (collision, digging, drops, liquid handling), so a
    /// creator paints artwork and picks a role rather than wiring engine components by hand.
    /// </summary>
    public enum DimensionTileRole
    {
        /// <summary>Walkable floor.</summary>
        Ground = 0,

        /// <summary>Solid diggable wall.</summary>
        Wall = 1,

        /// <summary>Hole the player falls through or cannot cross.</summary>
        Pit = 2,

        /// <summary>Liquid surface such as water or lava.</summary>
        Liquid = 3,

        /// <summary>Overhead layer drawn above the play space.</summary>
        Ceiling = 4,

        /// <summary>Non-colliding visual overlay (grass tufts, cracks).</summary>
        Decoration = 5,

        /// <summary>Ore-bearing wall that yields resources when mined.</summary>
        Vein = 6,

        /// <summary>Creator-defined; only the shared basics are validated.</summary>
        Custom = 100
    }

    /// <summary>Behaviours a tile can carry. Roles declare their defaults through these flags.</summary>
    [Flags]
    public enum DimensionTileCapabilities
    {
        None = 0,

        /// <summary>Entities may stand on it.</summary>
        Walkable = 1 << 0,

        /// <summary>Blocks movement.</summary>
        Collides = 1 << 1,

        /// <summary>Can be mined/dug by a tool.</summary>
        Diggable = 1 << 2,

        /// <summary>Yields items when removed (requires a loot table).</summary>
        Drops = 1 << 3,

        /// <summary>Uses an animated frame sequence.</summary>
        Animated = 1 << 4,

        /// <summary>Contributes light / uses an emissive channel.</summary>
        Emissive = 1 << 5,

        /// <summary>Entities move through it as liquid.</summary>
        Swimmable = 1 << 6,

        /// <summary>Participates in adjacency/auto-tiling with its neighbours.</summary>
        AutoTiled = 1 << 7
    }

    /// <summary>
    /// Maps a tile role to the behaviour it must generate. Read by the tileset generator, the
    /// validator, and the authoring UI so the fields a creator sees always match what is emitted.
    /// </summary>
    public static class DimensionTileRoleRules
    {
        public static DimensionTileCapabilities GetDefaultCapabilities(DimensionTileRole role)
        {
            switch (role)
            {
                case DimensionTileRole.Ground:
                    return DimensionTileCapabilities.Walkable |
                           DimensionTileCapabilities.AutoTiled;

                case DimensionTileRole.Wall:
                    return DimensionTileCapabilities.Collides |
                           DimensionTileCapabilities.Diggable |
                           DimensionTileCapabilities.Drops |
                           DimensionTileCapabilities.AutoTiled;

                case DimensionTileRole.Vein:
                    return DimensionTileCapabilities.Collides |
                           DimensionTileCapabilities.Diggable |
                           DimensionTileCapabilities.Drops |
                           DimensionTileCapabilities.AutoTiled |
                           DimensionTileCapabilities.Emissive;

                case DimensionTileRole.Pit:
                    return DimensionTileCapabilities.AutoTiled;

                case DimensionTileRole.Liquid:
                    return DimensionTileCapabilities.Swimmable |
                           DimensionTileCapabilities.Animated |
                           DimensionTileCapabilities.AutoTiled;

                case DimensionTileRole.Ceiling:
                    return DimensionTileCapabilities.AutoTiled;

                case DimensionTileRole.Decoration:
                    return DimensionTileCapabilities.None;

                case DimensionTileRole.Custom:
                default:
                    return DimensionTileCapabilities.None;
            }
        }

        public static bool Requires(DimensionTileRole role, DimensionTileCapabilities capability)
        {
            return (GetDefaultCapabilities(role) & capability) == capability;
        }

        /// <summary>True when the role must supply a loot table to be complete.</summary>
        public static bool RequiresLootTable(DimensionTileRole role)
        {
            return Requires(role, DimensionTileCapabilities.Drops);
        }

        /// <summary>True when the role blocks movement (walls, veins).</summary>
        public static bool IsSolid(DimensionTileRole role)
        {
            return Requires(role, DimensionTileCapabilities.Collides);
        }

        public static string Describe(DimensionTileRole role)
        {
            switch (role)
            {
                case DimensionTileRole.Ground: return "Ground";
                case DimensionTileRole.Wall: return "Wall";
                case DimensionTileRole.Pit: return "Pit";
                case DimensionTileRole.Liquid: return "Liquid";
                case DimensionTileRole.Ceiling: return "Ceiling";
                case DimensionTileRole.Decoration: return "Decoration";
                case DimensionTileRole.Vein: return "Ore vein";
                case DimensionTileRole.Custom: return "Custom";
                default: return role.ToString();
            }
        }
    }

    /// <summary>Outcome of asking a tileset provider to register a tileset.</summary>
    public readonly struct DimensionTilesetRegistrationResult
    {
        private DimensionTilesetRegistrationResult(
            bool accepted,
            string providerId,
            string code,
            string message)
        {
            Accepted = accepted;
            ProviderId = providerId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Accepted { get; }

        /// <summary>Provider that owns the registration (this framework, or an adapted mod).</summary>
        public string ProviderId { get; }

        public string Code { get; }

        public string Message { get; }

        public static DimensionTilesetRegistrationResult Success(string providerId, string message)
        {
            return new DimensionTilesetRegistrationResult(true, providerId, "ok", message);
        }

        public static DimensionTilesetRegistrationResult Failed(
            string providerId,
            string code,
            string message)
        {
            return new DimensionTilesetRegistrationResult(false, providerId, code, message);
        }
    }

    /// <summary>
    /// Registration seam for tilesets. Dimensions API owns tilesets, but another tileset API mod
    /// may be installed at the same time; a compatibility adapter implements this interface and
    /// delegates, so neither mod fights the other for the tile registry. Consumers always depend
    /// on Dimensions API, never on the third-party mod directly, and adapters are optional and
    /// gated on that mod being present.
    /// </summary>
    public interface IDimensionTilesetProvider
    {
        /// <summary>Stable id of the provider (e.g. the framework, or an adapter's target mod).</summary>
        string ProviderId { get; }

        /// <summary>
        /// Priority used when several providers can serve a tileset. Higher wins; the built-in
        /// framework provider uses zero so an explicit adapter can take precedence.
        /// </summary>
        int Priority { get; }

        /// <summary>True when this provider is able to own the given tileset id right now.</summary>
        bool CanRegister(string tilesetId);

        /// <summary>Registers (or re-registers) a tileset owned by a content pack.</summary>
        DimensionTilesetRegistrationResult Register(
            string contentPackId,
            string tilesetId);

        /// <summary>Releases a tileset, e.g. when its content pack is removed.</summary>
        bool Release(string tilesetId);

        /// <summary>Resolves an authored tile to the runtime tile id the game will use.</summary>
        bool TryResolveRuntimeTileId(string tilesetId, string tileId, out int runtimeTileId);
    }
}
