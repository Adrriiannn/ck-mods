using HarmonyLib;
using PugWorldGen;
using Unity.Entities;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// Adds this framework's scenes to the world's scene table in the one moment world generation will
    /// still notice them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE WINDOW IS ONE METHOD WIDE. <c>SpawnDungeonAndSceneSystem</c> reads the scene-table singleton
    /// exactly once, in <c>OnStartRunning</c>, and immediately copies the scene list into its own
    /// once-per-run state. Everything afterwards picks from that copy. Inject a moment too late and the
    /// scenes are genuinely in the table, and world generation will still never choose one — no error,
    /// no warning, nothing to grep for. Running as a prefix on that method puts our scenes in the table
    /// before the snapshot is taken.
    /// </para>
    /// <para>
    /// WHY THIS METHOD IS REACHABLE AT ALL. The system's struct carries <c>[BurstCompile]</c>, and
    /// <c>OnCreate</c>, <c>OnStopRunning</c>, <c>OnDestroy</c> and <c>OnUpdate</c> each carry their own —
    /// which locks them. <c>OnStartRunning</c> carries none, so it stays managed and is patchable. It is
    /// the only such method on the system, which is what makes it the seam rather than merely a
    /// convenient one.
    /// </para>
    /// <para>
    /// A prefix that returns void and never throws: this must not be able to stop world generation. If
    /// the injection fails the world still generates, just without our structures — which is a bad day
    /// for one mod rather than a broken save.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(SpawnDungeonAndSceneSystem), "OnStartRunning")]
    internal static class DimensionSpawnDungeonAndScenePatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Before(ref SystemState state)
        {
            Fired++;

            DimensionCustomSceneTableInjector.TryInject(state.World);

            // Dungeons after scenes, and it has to be this way round: a dungeon's rooms are named
            // scenes, so the scene table must already contain them or the dungeon would reference
            // rooms the game cannot find and generate as an empty cave.
            DimensionDungeonAssembler.TryAssemble(state.World);
        }
    }
}
