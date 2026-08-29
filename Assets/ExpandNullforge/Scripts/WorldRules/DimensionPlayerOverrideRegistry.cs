using HarmonyLib;
using UnityEngine;

namespace ExpandNullforge.WorldRules
{
    /// <summary>
    /// Writes over the numbers on Core Keeper's own player, just before a loading world reads them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A MOD CANNOT SHIP A PLAYER. There is one player object, the game puts it in the database
    /// before any mod is read, and the database keeps whichever copy arrived first — so a mod's own
    /// player is dropped without a word. Overriding is the only thing that works, and it works
    /// because the game's player object is a plain loaded object whose fields are read once, at the
    /// moment a world converts it.
    /// </para>
    /// <para>
    /// PUT BACK AFTERWARDS, ALWAYS. The object stays loaded for the whole session. If the override
    /// were left in place, a vanilla world opened later in the same sitting would still be playing
    /// with this mod's turning delay, and there would be nothing left holding the real number. So
    /// the old value is stashed in the prefix and restored in the postfix, and the override is
    /// re-applied the next time a world converts.
    /// </para>
    /// <para>
    /// ONLY THE PLAYER, NOT ANYTHING PLAYER-SHAPED. The guard is the object's own id: Core Keeper
    /// numbers the player 6000, and anything else carrying the same authoring is left alone.
    /// </para>
    /// </remarks>
    public static class DimensionPlayerOverrideRegistry
    {
        private static bool turningIsOverridden;
        private static float turningCatchesUpAfter;

        private static bool driftIsOverridden;
        private static AnimationCurve vehicleDrift;

        private static bool aimIsOverridden;
        private static Vector3 aimSitsAt;

        /// <summary>
        /// Overrides how long the player carries on facing the old way after turning, in seconds.
        /// </summary>
        public static void RegisterTurningDelay(float seconds)
        {
            turningCatchesUpAfter = seconds < 0f ? 0f : seconds;
            turningIsOverridden = true;
        }

        /// <summary>
        /// Overrides how much a vehicle slides sideways as it turns, as a curve over the turn.
        /// </summary>
        /// <remarks>
        /// Taken as the times and the amounts side by side rather than as a curve object, because
        /// the generated bootstrap is C# source: two arrays of numbers can be written into it, an
        /// <c>AnimationCurve</c> cannot. An empty pair is refused — the game samples this curve at
        /// every moment of a turn, and an empty curve answers zero forever, which would stop
        /// vehicles drifting rather than change how they drift.
        /// </remarks>
        public static void RegisterVehicleDrift(float[] times, float[] amounts)
        {
            if (times == null || amounts == null || times.Length == 0 ||
                times.Length != amounts.Length)
            {
                return;
            }

            Keyframe[] keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                keys[i] = new Keyframe(times[i], amounts[i]);
            }

            vehicleDrift = new AnimationCurve(keys);
            driftIsOverridden = true;
        }

        /// <summary>Overrides where what the player is aiming at sits, relative to them.</summary>
        public static void RegisterAimOffset(float x, float y, float z)
        {
            aimSitsAt = new Vector3(x, y, z);
            aimIsOverridden = true;
        }

        /// <summary>Clears every override (mod reload).</summary>
        public static void Clear()
        {
            turningIsOverridden = false;
            driftIsOverridden = false;
            aimIsOverridden = false;
            vehicleDrift = null;
        }

        internal static bool OverridesMovement
        {
            get { return turningIsOverridden || driftIsOverridden; }
        }

        internal static bool OverridesAim { get { return aimIsOverridden; } }

        /// <summary>
        /// Writes the movement overrides onto the game's player and hands back what was there.
        /// </summary>
        internal static PlayerMovementBackup ApplyMovement(PlayerAuthoring authoring)
        {
            PlayerMovementBackup backup = new PlayerMovementBackup
            {
                Applied = false,
                TurningCatchesUpAfter = authoring.walkingReorientationDelay,
                VehicleDrift = authoring.vehicleDriftingAmountCurve
            };

            if (turningIsOverridden)
            {
                authoring.walkingReorientationDelay = turningCatchesUpAfter;
                backup.Applied = true;
            }

            if (driftIsOverridden && vehicleDrift != null && vehicleDrift.length > 0)
            {
                authoring.vehicleDriftingAmountCurve = vehicleDrift;
                backup.Applied = true;
            }

            return backup;
        }

        internal static void RestoreMovement(
            PlayerAuthoring authoring,
            PlayerMovementBackup backup)
        {
            if (authoring == null || !backup.Applied)
            {
                return;
            }

            authoring.walkingReorientationDelay = backup.TurningCatchesUpAfter;
            authoring.vehicleDriftingAmountCurve = backup.VehicleDrift;
        }

        internal static Vector3 AimSitsAt { get { return aimSitsAt; } }

        /// <summary>What the game's player had before the override, so it can be put back.</summary>
        internal struct PlayerMovementBackup
        {
            public bool Applied;
            public float TurningCatchesUpAfter;
            public AnimationCurve VehicleDrift;
        }

        /// <summary>
        /// True when this authoring sits on Core Keeper's own player rather than on something else
        /// that happens to carry the same component.
        /// </summary>
        internal static bool IsTheGamesPlayer(Component authoring)
        {
            if (authoring == null)
            {
                return false;
            }

            EntityMonoBehaviourData data = authoring.GetComponent<EntityMonoBehaviourData>();
            return data != null && data.objectInfo != null &&
                   data.objectInfo.objectID == ObjectID.Player;
        }
    }

    /// <summary>
    /// The moment a loading world reads the player's movement numbers off the game's own object.
    /// </summary>
    [HarmonyPatch(typeof(PlayerAuthoringConverter), "Convert")]
    internal static class DimensionPlayerAuthoringConverterPatch
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
        private static void Prefix(
            PlayerAuthoring authoring,
            out DimensionPlayerOverrideRegistry.PlayerMovementBackup __state)
        {
            Fired++;

            __state = default(DimensionPlayerOverrideRegistry.PlayerMovementBackup);
            if (authoring == null ||
                !DimensionPlayerOverrideRegistry.OverridesMovement ||
                !DimensionPlayerOverrideRegistry.IsTheGamesPlayer(authoring))
            {
                return;
            }

            __state = DimensionPlayerOverrideRegistry.ApplyMovement(authoring);
        }

        [HarmonyPostfix]
        private static void Postfix(
            PlayerAuthoring authoring,
            DimensionPlayerOverrideRegistry.PlayerMovementBackup __state)
        {
            Fired++;

            DimensionPlayerOverrideRegistry.RestoreMovement(authoring, __state);
        }
    }

    /// <summary>
    /// The same moment for where the player's aim sits, which the game keeps on its own component.
    /// </summary>
    [HarmonyPatch(typeof(PlayerAimPositionConverter), "Convert")]
    internal static class DimensionPlayerAimPositionConverterPatch
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
        private static void Prefix(
            PlayerAimPositionAuthoring authoring,
            out Unity.Mathematics.float3 __state)
        {
            Fired++;

            __state = default(Unity.Mathematics.float3);
            if (authoring == null ||
                !DimensionPlayerOverrideRegistry.OverridesAim ||
                !DimensionPlayerOverrideRegistry.IsTheGamesPlayer(authoring))
            {
                return;
            }

            __state = authoring.position;
            Vector3 offset = DimensionPlayerOverrideRegistry.AimSitsAt;
            authoring.position = new Unity.Mathematics.float3(offset.x, offset.y, offset.z);
        }

        [HarmonyPostfix]
        private static void Postfix(
            PlayerAimPositionAuthoring authoring,
            Unity.Mathematics.float3 __state)
        {
            Fired++;

            if (authoring == null ||
                !DimensionPlayerOverrideRegistry.OverridesAim ||
                !DimensionPlayerOverrideRegistry.IsTheGamesPlayer(authoring))
            {
                return;
            }

            authoring.position = __state;
        }
    }
}
