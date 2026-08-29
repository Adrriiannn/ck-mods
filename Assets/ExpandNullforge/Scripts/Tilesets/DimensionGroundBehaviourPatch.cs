using HarmonyLib;
using PugTilemap;
using Unity.Entities;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Lets a custom block's slime read as one of Core Keeper's own — so it puffs, splashes, sounds
    /// and feels like the thing it looks like.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT VANILLA DOES. <c>PlayerController.UpdateOnTileEffects</c> reads the tile under the player
    /// and sets one flag per slime kind by comparing the tileset against a hardcoded list — Dirt is
    /// ordinary slime, Larva Hive is acid, Nature is poison, Sea is slippery, Excavation is oil.
    /// Everything downstream keys off those flags: which footstep puff plays, which splat sprite is
    /// stamped, whether the run dust is suppressed, whether the acid loop starts.
    /// </para>
    /// <para>
    /// WHY A POSTFIX AND NOT A REPLACEMENT. The original does considerably more than slime — wood
    /// floors, glass, and other surface work — and all of it should keep running exactly as it does.
    /// This runs afterwards and only ever <em>adds</em> a flag the original could not have set,
    /// because the tileset it was looking at is one vanilla has never heard of. A vanilla tileset
    /// takes the early exit and nothing here executes at all.
    /// </para>
    /// <para>
    /// SCOPE, AND WHAT THIS DELIBERATELY DOES NOT DO. This is the presentation half — the part the
    /// player sees and hears the instant they step in. The <em>conditions</em> (acid burning, mold
    /// slowing, oil drenching) are applied by a separate Burst-compiled system and are not reachable
    /// from here; extending those is its own piece of work. A block set to Acid will therefore look
    /// and sound like acid before it burns like acid, which is a partial result worth having and
    /// worth being honest about.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(PlayerController), "UpdateOnTileEffects")]
    internal static class DimensionGroundBehaviourPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        /// <summary>
        /// Takes the six flags as parameters instead of reaching for them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A patch parameter named <c>___someField</c> is Harmony's own way of handing a patch a
        /// private field of the class being patched: it writes the field load into this method's
        /// prologue, and for a <c>ref</c> parameter it loads the address, so assigning the parameter
        /// assigns the field. The six names below are the six <c>private bool</c> flags on
        /// <c>PlayerController</c> that <c>UpdateOnTileEffects</c> sets itself.
        /// </para>
        /// <para>
        /// This replaced six <c>AccessTools.FieldRef</c> fields. Core Keeper's mod sandbox denies
        /// <c>HarmonyLib.AccessTools</c> outright and refuses the whole mod over one reference to
        /// it, so the framework may declare a patch but may not drive the patcher. The lookup still
        /// happens — inside Harmony, at bind time rather than in a static initialiser — so a
        /// misspelt name now fails while the loader is binding, and every patch after it in the
        /// class order never applies. <c>DimensionHarmonyPatchTargetTests</c> resolves all of these
        /// so that failure cannot reach the game.
        /// </para>
        /// </remarks>
        [HarmonyPostfix]
        private static void After(
            PlayerController __instance,
            ref bool ___onSlime,
            ref bool ___onOrangeSlime,
            ref bool ___onAcid,
            ref bool ___onPoisonSlime,
            ref bool ___onSlipperySlime,
            ref bool ___onOil)
        {
            Fired++;

            if (!DimensionTilesetBehaviourRegistry.HasAny || __instance == null)
            {
                return;
            }

            CurrentTileCD tile;
            if (!EntityUtility.TryGetComponentData<CurrentTileCD>(__instance.entity, __instance.world, out tile))
            {
                return;
            }

            DimensionTilesetGroundBehaviour behaviour =
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(tile.TileType, (int)tile.Tileset);
            if (behaviour == DimensionTilesetGroundBehaviour.None)
            {
                return;
            }

            // Every slime is slime first. Vanilla sets this for any groundSlime regardless of
            // tileset, so it is already true here — but setting it explicitly keeps this method
            // readable as "here is the complete state for our tile" rather than "here is the
            // difference from vanilla's".
            ___onSlime = true;

            switch (behaviour)
            {
                case DimensionTilesetGroundBehaviour.Slime:
                    ___onOrangeSlime = true;
                    break;
                case DimensionTilesetGroundBehaviour.Acid:
                    ___onAcid = true;
                    break;
                case DimensionTilesetGroundBehaviour.PoisonSlime:
                    ___onPoisonSlime = true;
                    break;
                case DimensionTilesetGroundBehaviour.SlipperySlime:
                    ___onSlipperySlime = true;
                    break;
                case DimensionTilesetGroundBehaviour.Oil:
                    ___onOil = true;
                    break;
            }
        }
    }
}
