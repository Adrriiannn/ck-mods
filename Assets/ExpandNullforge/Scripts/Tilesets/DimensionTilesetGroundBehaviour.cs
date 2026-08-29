using System.Collections.Generic;
using PugTilemap;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// What a block's slime-family ground does to whoever walks on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are Core Keeper's own behaviours, named after what the player experiences rather than
    /// after the tileset that happens to carry them in vanilla. A custom block picks one, and inherits
    /// the whole package the game already ships for it — the condition, the footstep puff, the splat
    /// sprite, the audio loop.
    /// </para>
    /// <para>
    /// The list is short on purpose. It is not a menu of effects to invent; it is the set vanilla
    /// already implements, which is exactly what makes it free to adopt.
    /// </para>
    /// </remarks>
    public enum DimensionTilesetGroundBehaviour
    {
        /// <summary>Walk over it and nothing happens. Purely a look.</summary>
        None = 0,

        /// <summary>Plain slime — the ordinary orange kind. Muffles the run dust.</summary>
        Slime = 1,

        /// <summary>Burns. Carries its own looping sound while you stand in it.</summary>
        Acid = 2,

        /// <summary>Poisons.</summary>
        PoisonSlime = 3,

        /// <summary>Slippery — movement carries you further than you asked.</summary>
        SlipperySlime = 4,

        /// <summary>Oil. Drenches whatever walks through, which matters near fire.</summary>
        Oil = 5
    }

    /// <summary>
    /// Which vanilla ground behaviour each custom tileset borrows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE PROBLEM THIS SOLVES, which is bigger than slime. Several complete, working Core Keeper
    /// systems decide what to do by comparing a tile's tileset against a fixed list of vanilla
    /// tilesets — the slime effects here, the terrain conditions beside them, the region-title
    /// handler elsewhere. None of those lists has a row for an id we invented, so a custom block
    /// renders perfectly and then behaves like nothing at all. The systems are not broken and do not
    /// need replacing; they need to be told that our id counts as one they already know.
    /// </para>
    /// <para>
    /// This registry is that translation, kept in one place because the same shape keeps recurring.
    /// A block declares the behaviour it wants, generation records it, and the patches that sit over
    /// vanilla's comparisons consult it. Adding the next system that has this problem means one more
    /// lookup here, not another parallel mechanism.
    /// </para>
    /// <para>
    /// Only ids in the custom range are ever answered for. A vanilla tileset asking about itself gets
    /// <see cref="DimensionTilesetGroundBehaviour.None"/> and falls through to vanilla's own logic
    /// untouched — this must never change how the base game behaves on its own ground.
    /// </para>
    /// </remarks>
    public static class DimensionTilesetBehaviourRegistry
    {
        private static readonly Dictionary<int, DimensionTilesetGroundBehaviour> groundBehaviours =
            new Dictionary<int, DimensionTilesetGroundBehaviour>();

        /// <summary>
        /// Behaviours carried by a block's ORDINARY ground rather than by slime sitting on it.
        /// </summary>
        /// <remarks>
        /// A separate table because they are separate claims. Core Keeper has both kinds: a puddle of
        /// slime on otherwise safe stone, and mold ground that is itself the hazard with nothing on
        /// top of it. Folding them together would mean a block could not have a harmless surface and a
        /// dangerous slime — which is the more common of the two.
        /// </remarks>
        private static readonly Dictionary<int, DimensionTilesetGroundBehaviour> surfaceBehaviours =
            new Dictionary<int, DimensionTilesetGroundBehaviour>();

        /// <summary>Whether anything at all is registered, so callers can skip the lookup entirely.</summary>
        public static bool HasAny
        {
            get { return groundBehaviours.Count > 0 || surfaceBehaviours.Count > 0; }
        }

        /// <summary>
        /// Records that <paramref name="tilesetId"/>'s slime-family ground behaves as
        /// <paramref name="behaviour"/>.
        /// </summary>
        /// <remarks>
        /// Registering <see cref="DimensionTilesetGroundBehaviour.None"/> removes the entry rather
        /// than storing a "does nothing" row, so the common case — a block with no hazard — costs
        /// nothing to look up and leaves the table empty.
        /// </remarks>
        public static void RegisterGroundBehaviour(int tilesetId, DimensionTilesetGroundBehaviour behaviour)
        {
            if (!DimensionTilesetRegistry.IsCustomTilesetId(tilesetId) ||
                behaviour == DimensionTilesetGroundBehaviour.None)
            {
                groundBehaviours.Remove(tilesetId);
                return;
            }

            groundBehaviours[tilesetId] = behaviour;
        }

        /// <summary>
        /// The behaviour <paramref name="tilesetId"/> borrows, or <c>None</c> for anything vanilla
        /// or unregistered.
        /// </summary>
        public static DimensionTilesetGroundBehaviour GetGroundBehaviour(int tilesetId)
        {
            DimensionTilesetGroundBehaviour behaviour;
            return groundBehaviours.TryGetValue(tilesetId, out behaviour)
                ? behaviour
                : DimensionTilesetGroundBehaviour.None;
        }

        /// <summary>
        /// The behaviour for a tile, accounting for the fact that only slime-family ground has one.
        /// </summary>
        /// <remarks>
        /// The tile type check lives here rather than at every call site because forgetting it is the
        /// obvious mistake: a custom tileset's ordinary <em>ground</em> would otherwise start behaving
        /// like acid simply because its slime does.
        /// </remarks>
        public static DimensionTilesetGroundBehaviour GetGroundBehaviour(TileType tileType, int tilesetId)
        {
            if (tileType == TileType.groundSlime)
            {
                return GetGroundBehaviour(tilesetId);
            }

            // Mold is the reference for the other case: ground that is itself the hazard, with no
            // slime on it at all. Restricted to plain ground on purpose — a block's tilled soil,
            // its water and its dug-up dirt are different surfaces and should not inherit a hazard
            // the author put on the untouched ground.
            if (tileType == TileType.ground)
            {
                return GetSurfaceBehaviour(tilesetId);
            }

            return DimensionTilesetGroundBehaviour.None;
        }

        /// <summary>
        /// Records that <paramref name="tilesetId"/>'s ordinary ground is itself hazardous.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="RegisterGroundBehaviour"/> so a block can have a safe surface and
        /// a dangerous slime, which is what most hazardous blocks in the game actually are.
        /// </remarks>
        public static void RegisterSurfaceBehaviour(int tilesetId, DimensionTilesetGroundBehaviour behaviour)
        {
            if (!DimensionTilesetRegistry.IsCustomTilesetId(tilesetId) ||
                behaviour == DimensionTilesetGroundBehaviour.None)
            {
                surfaceBehaviours.Remove(tilesetId);
                return;
            }

            surfaceBehaviours[tilesetId] = behaviour;
        }

        /// <summary>
        /// What a tileset's ordinary ground does, or <c>None</c> for anything vanilla or unregistered.
        /// </summary>
        public static DimensionTilesetGroundBehaviour GetSurfaceBehaviour(int tilesetId)
        {
            DimensionTilesetGroundBehaviour behaviour;
            return surfaceBehaviours.TryGetValue(tilesetId, out behaviour)
                ? behaviour
                : DimensionTilesetGroundBehaviour.None;
        }

        /// <summary>
        /// The vanilla tileset a behaviour is carried by, for handing to systems that only understand
        /// vanilla ids.
        /// </summary>
        /// <remarks>
        /// The inverse of the mapping the game hardcodes. Where a vanilla system cannot be reached
        /// directly, passing it the tileset it already associates with a behaviour is the next best
        /// thing — it gets an id it recognises and does the right work.
        /// </remarks>
        public static bool TryGetVanillaTilesetFor(DimensionTilesetGroundBehaviour behaviour, out Tileset tileset)
        {
            switch (behaviour)
            {
                case DimensionTilesetGroundBehaviour.Slime:
                    tileset = Tileset.Dirt;
                    return true;
                case DimensionTilesetGroundBehaviour.Acid:
                    tileset = Tileset.LarvaHive;
                    return true;
                case DimensionTilesetGroundBehaviour.PoisonSlime:
                    tileset = Tileset.Nature;
                    return true;
                case DimensionTilesetGroundBehaviour.SlipperySlime:
                    tileset = Tileset.Sea;
                    return true;
                case DimensionTilesetGroundBehaviour.Oil:
                    tileset = Tileset.Excavation;
                    return true;
                default:
                    tileset = Tileset.Dirt;
                    return false;
            }
        }

        /// <summary>Forgets everything. For tests; the runtime registers once per load.</summary>
        public static void Clear()
        {
            groundBehaviours.Clear();
            surfaceBehaviours.Clear();
        }
    }
}
