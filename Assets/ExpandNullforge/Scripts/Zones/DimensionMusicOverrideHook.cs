using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using HarmonyLib;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Which dimensions own their music outright, and which roster each plays.
    /// </summary>
    /// <remarks>
    /// Filled from the service's dimension list plus any authored override; refreshed lazily
    /// because the music decision runs per frame and dimensions change roughly never.
    /// </remarks>
    public static class DimensionMusicOverrideRegistry
    {
        private struct Row
        {
            public DimensionBounds AbsoluteBounds;
            public MusicRosterType Roster;
        }

        private static readonly List<Row> Rows = new List<Row>();

        private static readonly Dictionary<string, MusicRosterType> AuthoredRosters =
            new Dictionary<string, MusicRosterType>(System.StringComparer.Ordinal);

        private static double nextRefreshAt;

        /// <summary>The roster a dungeon plays unless its author names another.</summary>
        private const MusicRosterType DefaultDungeonRoster = MusicRosterType.MOLD_DUNGEON;

        /// <summary>Authors a specific roster for one dimension, by roster or cue name.</summary>
        public static void SetRoster(string dimensionId, string rosterName)
        {
            if (string.IsNullOrEmpty(dimensionId) || string.IsNullOrEmpty(rosterName))
            {
                return;
            }

            MusicRosterType roster;
            if (System.Enum.TryParse(rosterName, false, out roster))
            {
                AuthoredRosters[dimensionId] = roster;
            }
            else if (DimensionMusicRosterRegistry.IsCue(rosterName))
            {
                AuthoredRosters[dimensionId] = (MusicRosterType)DimensionMusicRosterIds.For(rosterName);
            }
            else
            {
                DimensionFrameworkLog.Warning(
                    "Dimension '" + dimensionId + "' asked for music '" +
                    rosterName + "', which is neither a game roster nor a registered cue. " +
                    "It plays the default dungeon roster instead.");
            }

            nextRefreshAt = 0.0d;
        }

        private static MusicRosterType phaseOverrideRoster;
        private static bool phaseOverrideActive;

        /// <summary>
        /// A boss phase's music, outranking the dimension's own while the phase holds.
        /// </summary>
        /// <remarks>
        /// Set by the phase system when a phase carrying a music cue fires, cleared when the
        /// boss dies or despawns. This is what makes a phase's <c>musicCueId</c> real — the
        /// field was authored, validated and then dropped on the floor at emission.
        /// </remarks>
        public static void SetPhaseOverride(string rosterName)
        {
            if (string.IsNullOrEmpty(rosterName))
            {
                return;
            }

            MusicRosterType roster;
            if (System.Enum.TryParse(rosterName, false, out roster))
            {
                phaseOverrideRoster = roster;
                phaseOverrideActive = true;
            }
            else if (DimensionMusicRosterRegistry.IsCue(rosterName))
            {
                phaseOverrideRoster = (MusicRosterType)DimensionMusicRosterIds.For(rosterName);
                phaseOverrideActive = true;
            }
            else
            {
                DimensionFrameworkLog.Warning(
                    "A boss phase asked for music '" + rosterName +
                    "', which is neither a game roster nor a registered cue. The music " +
                    "stays as it was.");
            }
        }

        public static void ClearPhaseOverride()
        {
            phaseOverrideActive = false;
        }

        public static bool TryGetPhaseOverride(out MusicRosterType roster)
        {
            roster = phaseOverrideRoster;
            return phaseOverrideActive;
        }

        public static bool TryGetRoster(float2 absolutePosition, out MusicRosterType roster)
        {
            RefreshIfDue();
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].AbsoluteBounds.Contains(absolutePosition))
                {
                    roster = Rows[i].Roster;
                    return true;
                }
            }

            roster = default;
            return false;
        }

        private static void RefreshIfDue()
        {
            double now = UnityEngine.Time.unscaledTimeAsDouble;
            if (now < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = now + 5.0d;

            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null)
            {
                return;
            }

            Rows.Clear();
            IReadOnlyList<DimensionDefinition> dimensions = service.GetDimensions();
            for (int i = 0; i < dimensions.Count; i++)
            {
                DimensionDefinition dimension = dimensions[i];

                // ASKING FOR MUSIC IS ITSELF THE PERMISSION. The type policy answers "does this
                // kind of dimension own its music by default", which is true only of dungeons — and
                // for a dimension nobody has authored music for, that is still the whole rule. But
                // an author who has named this dimension's music has said it owns it, whatever type
                // it is, and refusing them because a Room is not a Dungeon would be a field that
                // silently does nothing for three of the four types.
                MusicRosterType roster;
                bool authored = AuthoredRosters.TryGetValue(dimension.Id, out roster);
                if (!authored)
                {
                    if (!DimensionTypePolicy.For(dimension.Type).HasMusicOverride)
                    {
                        continue;
                    }

                    roster = DefaultDungeonRoster;
                }

                Rows.Add(new Row
                {
                    AbsoluteBounds = dimension.AbsoluteBounds,
                    Roster = roster
                });
            }
        }

        public static void Clear()
        {
            Rows.Clear();
            AuthoredRosters.Clear();
            nextRefreshAt = 0.0d;
            phaseOverrideActive = false;
        }
    }

    /// <summary>
    /// Gives a Dungeon-type dimension its music, without touching anything that outranks it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE SEAM IS THE DECISION FUNCTION, not a giant music-area entity. An entity's circle
    /// bleeds past rectangular bounds, has to fight legitimate boss music on priority, and
    /// ghosts server state for a pure client concern. A postfix on the decision leaves every
    /// higher-ranking verdict alone: a boss's own <c>MusicAreaCD</c> win stands (the fight
    /// music must beat the dungeon's), and so does the death and screen-fade silence.
    /// </para>
    /// <para>
    /// Without this, a dungeon on vanilla tilesets plays whatever biome those tiles imply,
    /// and a dungeon on custom tilesets plays permanent carried-over silence.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(GameMusicHandler), "GetActiveMusicArea")]
    public static class DimensionMusicOverrideHook
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
        /// Takes the handler's own active-area entity as a parameter instead of reaching for it.
        /// </summary>
        /// <remarks>
        /// A patch parameter named <c>___someField</c> is Harmony's own way of handing a patch a
        /// private field of the class being patched — here <c>GameMusicHandler</c>'s
        /// <c>activeAreaMusicEntity</c>, which it sets in the very method this runs after. It is
        /// only read, so it is taken by value. This replaced an <c>AccessTools.FieldRef</c>, which
        /// Core Keeper's mod sandbox denies: the framework may declare a patch but may not drive
        /// the patcher. <c>DimensionHarmonyPatchTargetTests</c> resolves the name, because a
        /// misspelt one now fails while Harmony is binding rather than on first use.
        /// </remarks>
        private static void Postfix(ref MusicAreaCD __result, Entity ___activeAreaMusicEntity)
        {
            Fired++;

            PlayerController player = Manager.main == null ? null : Manager.main.player;
            if (player == null || Manager.load.IsScreenFadingOutOrBlack())
            {
                return;
            }

            if (EntityUtility.GetComponentData<PlayerState.PlayerStateCD>(player.entity, player.world)
                    .HasAnyState(PlayerState.PlayerStateEnum.Death))
            {
                return;
            }

            // A phase's music outranks even the fight's own area — that is the entire point
            // of a mid-fight cue. It is checked before the active-area bail below, and the
            // phase system clears it the moment the boss dies, so death silence still lands.
            if (DimensionMusicOverrideRegistry.TryGetPhaseOverride(out MusicRosterType phaseRoster))
            {
                __result = new MusicAreaCD { musicRosterType = phaseRoster };
                return;
            }

            // A music-area entity won this frame — boss or event music. It outranks us.
            if (___activeAreaMusicEntity != Entity.Null)
            {
                return;
            }

            float2 absolute = new float2(player.WorldPosition.x, player.WorldPosition.z);
            MusicRosterType roster;
            if (DimensionMusicOverrideRegistry.TryGetRoster(absolute, out roster))
            {
                // A bare-roster CD is vanilla's own idiom for a forced verdict.
                __result = new MusicAreaCD { musicRosterType = roster };
            }
        }
    }
}
