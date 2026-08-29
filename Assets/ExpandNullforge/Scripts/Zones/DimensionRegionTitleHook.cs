using HarmonyLib;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Hands this mod's biome title cards to Core Keeper's region-title handler the moment that
    /// handler exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Awake</c> is the right seam and the only one needed: it is the first moment the handler and
    /// its serialized title list are real, and it runs before <c>LateUpdate</c> ever looks at that
    /// list. A per-frame hook would work too and would cost a comparison every frame for nothing.
    /// </para>
    /// <para>
    /// It is a MonoBehaviour method, so there is no Burst to work around — this is an ordinary postfix,
    /// and it adds data rather than changing behaviour. Everything the player then sees is vanilla's.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(RegionTitleHandler), "Awake")]
    public static class DimensionRegionTitleHook
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        private static void Postfix(RegionTitleHandler __instance)
        {
            Fired++;

            // A fresh handler means a fresh scene, so anything remembered about the previous one is
            // stale — including which handler was filled.
            DimensionRegionTitleInstaller.ResetForNewScene();
            DimensionRegionTitleInstaller.EnsureInstalled(__instance);
        }
    }
}
