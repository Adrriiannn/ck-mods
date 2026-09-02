using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.WorldRules
{
    /// <summary>
    /// Changes where and when the world acts on a player of its own accord.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TABLE HAS ONE READER AND IT LOADS IT BY NAME.
    /// <c>EnvironmentEventSystem.OnCreate</c> does
    /// <c>Resources.Load&lt;EnvironmentEventsTable&gt;("EnvironmentEventsTable")</c> and bakes every
    /// row into native containers for the life of the world
    /// (<c>ck-db\EnvironmentEvents\EnvironmentEventSystem.cs:1047-1086</c>). Same shape as the
    /// tileset colour patch, so the same road: change the loaded table in a prefix, and let the
    /// game's own baking run over it.
    /// </para>
    /// <para>
    /// ONE ROW PER EVENT, OR THE WORLD DOES NOT LOAD. The bake ends in
    /// <c>eventRequirements.Add((int)eventType, item)</c> on a <c>NativeParallelHashMap</c>, which
    /// throws on a repeated key. So this never appends a second row for an event that already has
    /// one; it replaces the row that is there, and a queued event named twice keeps the last.
    /// </para>
    /// <para>
    /// PUT BACK AFTERWARDS. Unlike a set bonus, which is a row of a mod's own added at the end,
    /// this writes over rows that belong to Core Keeper. The asset stays loaded for the whole
    /// session and there would be nothing left holding the real numbers, so the original list is
    /// stashed in the prefix and put back in the postfix — by then the system has copied everything
    /// it needs into its own native containers. This is the same law the upgrade-cost registry
    /// follows for the same reason.
    /// </para>
    /// <para>
    /// A FIFTH EVENT IS NOT POSSIBLE. The behaviour of each event is four sets of Burst-compiled
    /// function pointers written out one by one in the same OnCreate, keyed 1 to 4. A row for a
    /// fifth kind would be looked up in that table, find nothing, and do nothing.
    /// </para>
    /// </remarks>
    public static class DimensionEnvironmentEventRegistry
    {
        /// <summary>One kind of tile an event needs around the player.</summary>
        public struct GroundRow
        {
            /// <summary>Which part of a block. Core Keeper's <c>PugTilemap.TileType</c>.</summary>
            public int TilePart;

            /// <summary>Which blocks count. Tileset numbers, the game's own or this mod's.</summary>
            public int[] TilesetIds;

            /// <summary>How many such tiles have to be around the player.</summary>
            public int HowMany;
        }

        private sealed class Row
        {
            public int WorldEvent;
            public bool CanHappen;
            public List<string> BiomeIds;
            public float TilesFromTheCore;
            public int MostThingsNearby;
            public int TilesNeededInTotal;
            public List<GroundRow> Ground;
            public bool IgnoresTheSharedCooldown;
            public bool MayStartNearABoss;
            public bool SetsItsOwnCooldown;
            public float CooldownShortestSeconds;
            public float CooldownLongestSeconds;
        }

        /// <summary>
        /// The game's own wait between two events when nothing overrides it, in seconds.
        /// </summary>
        /// <remarks>
        /// Written into the bake as <c>new Vector2(1800f, 3600f)</c> whenever a row does not
        /// override its own cooldown. Repeated here so a row that sets its own can be compared
        /// against what it replaced.
        /// </remarks>
        public const float DefaultShortestCooldownSeconds = 1800f;

        /// <summary>The other end of that wait.</summary>
        public const float DefaultLongestCooldownSeconds = 3600f;

        /// <summary>The highest event number the game has behaviour for.</summary>
        /// <remarks>
        /// The events table is built with four entries, keyed 1 to 4, each holding three compiled
        /// function pointers. A row above this is looked up, finds nothing, and does nothing.
        /// </remarks>
        public const int HighestEventTheGameCanRun = 4;

        private static readonly List<Row> Rows = new List<Row>();

        /// <summary>Queues one event's rules. Call from the generated bootstrap.</summary>
        public static void Register(
            int worldEvent,
            bool canHappen,
            string[] biomeIds,
            float tilesFromTheCore,
            int mostThingsNearby,
            int tilesNeededInTotal,
            bool ignoresTheSharedCooldown,
            bool mayStartNearABoss,
            bool setsItsOwnCooldown,
            float cooldownShortestSeconds,
            float cooldownLongestSeconds)
        {
            if (worldEvent <= 0)
            {
                return;
            }

            Row row = new Row
            {
                WorldEvent = worldEvent,
                CanHappen = canHappen,
                BiomeIds = new List<string>(),
                TilesFromTheCore = tilesFromTheCore < 0f ? 0f : tilesFromTheCore,
                MostThingsNearby = mostThingsNearby < 0 ? 0 : mostThingsNearby,
                TilesNeededInTotal = tilesNeededInTotal < 0 ? 0 : tilesNeededInTotal,
                Ground = new List<GroundRow>(),
                IgnoresTheSharedCooldown = ignoresTheSharedCooldown,
                MayStartNearABoss = mayStartNearABoss,
                SetsItsOwnCooldown = setsItsOwnCooldown,
                CooldownShortestSeconds = cooldownShortestSeconds < 0f ? 0f : cooldownShortestSeconds,
                CooldownLongestSeconds = cooldownLongestSeconds < 0f ? 0f : cooldownLongestSeconds
            };

            for (int i = 0; biomeIds != null && i < biomeIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(biomeIds[i]))
                {
                    row.BiomeIds.Add(biomeIds[i]);
                }
            }

            // Naming the same event twice keeps the last, because the game's own bake would throw
            // on the second row rather than merge it.
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].WorldEvent == worldEvent)
                {
                    Rows[i] = row;
                    return;
                }
            }

            Rows.Add(row);
        }

        /// <summary>Adds one ground requirement to an event already registered.</summary>
        public static void AddGround(int worldEvent, int tilePart, int[] tilesetIds, int howMany)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].WorldEvent != worldEvent)
                {
                    continue;
                }

                Rows[i].Ground.Add(new GroundRow
                {
                    TilePart = tilePart,
                    TilesetIds = tilesetIds ?? new int[0],
                    HowMany = howMany < 0 ? 0 : howMany
                });
                return;
            }
        }

        /// <summary>Clears queued events (mod reload).</summary>
        public static void Clear()
        {
            Rows.Clear();
        }

        /// <summary>True once at least one event has been changed.</summary>
        public static bool HasAny { get { return Rows.Count > 0; } }

        internal static int PendingCount { get { return Rows.Count; } }

        /// <summary>
        /// Whether a queued event is one the game actually has behaviour for.
        /// </summary>
        /// <remarks>
        /// Pure so the same question can be asked in a test and in the editor's checks.
        /// </remarks>
        public static bool TheGameCanRunThisEvent(int worldEvent)
        {
            return worldEvent > 0 && worldEvent <= HighestEventTheGameCanRun;
        }

        /// <summary>
        /// The list the game should bake, built out of its own list and everything queued here.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Separated from the patch so the whole decision can be exercised against a list built by
        /// hand: what is replaced, what is removed, what is left alone, and that no event ends up
        /// with two rows.
        /// </para>
        /// <para>
        /// The order of the game's own rows is kept, because a row's position is the only thing an
        /// author has to go on when reading the table back.
        /// </para>
        /// </remarks>
        internal static List<EnvironmentEventParams> BuildEditedList(
            List<EnvironmentEventParams> original,
            System.Func<string, Biome> resolveBiome,
            System.Action<string> report)
        {
            List<EnvironmentEventParams> edited = new List<EnvironmentEventParams>();
            HashSet<int> written = new HashSet<int>();

            for (int i = 0; original != null && i < original.Count; i++)
            {
                EnvironmentEventParams theirs = original[i];
                Row ours = Find((int)theirs.eventType);
                if (ours == null)
                {
                    // Nobody named it, so it keeps every requirement the game gave it.
                    if (written.Add((int)theirs.eventType))
                    {
                        edited.Add(theirs);
                    }

                    continue;
                }

                written.Add(ours.WorldEvent);
                if (!ours.CanHappen)
                {
                    // No row at all is the game's own way of saying an event never happens: the
                    // check looks it up first and returns false when it is missing.
                    continue;
                }

                edited.Add(Build(ours, resolveBiome, report));
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                Row ours = Rows[i];
                if (written.Contains(ours.WorldEvent) || !ours.CanHappen)
                {
                    continue;
                }

                if (!TheGameCanRunThisEvent(ours.WorldEvent) && report != null)
                {
                    report(
                        "A world event numbered " + ours.WorldEvent + " was given requirements, " +
                        "and the game has behaviour for four events only. Its requirements are " +
                        "kept and nothing will ever run them.");
                }

                written.Add(ours.WorldEvent);
                edited.Add(Build(ours, resolveBiome, report));
            }

            return edited;
        }

        private static Row Find(int worldEvent)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].WorldEvent == worldEvent)
                {
                    return Rows[i];
                }
            }

            return null;
        }

        private static EnvironmentEventParams Build(
            Row ours,
            System.Func<string, Biome> resolveBiome,
            System.Action<string> report)
        {
            EnvironmentEventParams built = new EnvironmentEventParams
            {
                eventType = (EnvironmentEventType)ours.WorldEvent,
                biomes = new List<Biome>(),
                minDistanceFromCore = ours.TilesFromTheCore,
                maxAmountOfNearbyObjects = ours.MostThingsNearby,
                minTotalTilesFulfillingRequirements = ours.TilesNeededInTotal,
                tileRequirements = new List<EnvironmentEventTilesRequirement>(),
                ignoreGlobalEventCooldown = ours.IgnoresTheSharedCooldown,
                overrideEventSpecificCooldown = ours.SetsItsOwnCooldown,
                minMaxEventSpecificCooldownSeconds = new Vector2(
                    ours.CooldownShortestSeconds,
                    ours.CooldownLongestSeconds < ours.CooldownShortestSeconds
                        ? ours.CooldownShortestSeconds
                        : ours.CooldownLongestSeconds),
                allowSpawningNearBosses = ours.MayStartNearABoss
            };

            for (int i = 0; i < ours.BiomeIds.Count; i++)
            {
                Biome biome = resolveBiome == null ? Biome.None : resolveBiome(ours.BiomeIds[i]);
                if (biome == Biome.None)
                {
                    if (report != null)
                    {
                        report(
                            "World event " + ours.WorldEvent + " was set to happen in '" +
                            ours.BiomeIds[i] + "', which is not one of Core Keeper's own biomes. " +
                            "The game works out which biome a player is standing in from its own " +
                            "radial ranges, so only its own biomes can be named here: Slime, " +
                            "Larva, Stone, Nature, Sea, Desert, Crystal, Passage, Excavation. " +
                            "That biome was left out.");
                    }

                    continue;
                }

                built.biomes.Add(biome);
            }

            if (built.biomes.Count == 0 && report != null)
            {
                report(
                    "World event " + ours.WorldEvent + " was left able to happen and given no " +
                    "biome to happen in. The game tests the player's biome against that list, so " +
                    "an empty one means it never happens anywhere.");
            }

            for (int i = 0; i < ours.Ground.Count; i++)
            {
                GroundRow ground = ours.Ground[i];
                List<PugTilemap.Tileset> tilesets = new List<PugTilemap.Tileset>();
                for (int t = 0; ground.TilesetIds != null && t < ground.TilesetIds.Length; t++)
                {
                    // Raw cast: the check compares the tile's own tileset number, so a custom id
                    // matches here exactly as a vanilla one does.
                    tilesets.Add((PugTilemap.Tileset)ground.TilesetIds[t]);
                }

                built.tileRequirements.Add(new EnvironmentEventTilesRequirement
                {
                    minimumAmountOfTiles = ground.HowMany,
                    tileType = (PugTilemap.TileType)ground.TilePart,
                    tilesets = tilesets
                });
            }

            return built;
        }

        /// <summary>
        /// Answers a biome id, and only one of Core Keeper's own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS THE ONE PLACE IN THE FRAMEWORK THAT DELIBERATELY DOES NOT FALL THROUGH TO
        /// <c>DimensionBiomeIdentity</c>. Everywhere else a biome id that is not one of the game's
        /// takes a minted number, because the thing reading it compares against a number the
        /// framework also writes. Not here: the biome this check compares against comes from
        /// <c>BiomeLookup.GetBiome</c>, which reads a byte per sampled tile or a fixed list indexed
        /// by biome, and nothing in the framework writes either. Minting a number for a custom
        /// biome would produce a rule that matched nothing, silently.
        /// </para>
        /// <para>
        /// So an unknown name answers <c>Biome.None</c> and the caller says so. <c>None</c> is the
        /// game's own zero and matches no player standing anywhere, which is the correct reading of
        /// "a biome that does not exist".
        /// </para>
        /// <para>
        /// AND THE NAME IS MATCHED AGAINST A WRITTEN LIST, NOT PARSED. <c>Enum.TryParse</c> accepts
        /// a number as readily as a name, so a row that said "1000" — the shape a framework biome
        /// id takes — came back as <c>(Biome)1000</c> and walked straight past the refusal this
        /// method exists to make, ending up as the silent never-matching rule the refusal is for.
        /// It also accepted <c>None</c>, <c>__MAX_VALUE__</c>, and <c>Obsidian</c> and
        /// <c>GreatWall</c>, which Core Keeper marks
        /// <c>[Obsolete("Not used in full release world generation")]</c>
        /// (<c>ck-db\Pug.Base\Biome.cs</c>). Nine names are real biomes a player can stand in, and
        /// they are the nine the framework's own message already lists, so the list is what is
        /// compared against.
        /// </para>
        /// </remarks>
        internal static Biome ResolveBiomeId(string biomeId)
        {
            Biome real;
            return TheGamesOwnBiome(biomeId, out real) ? real : Biome.None;
        }

        /// <summary>
        /// Whether a biome name is one the world-event check can ever match.
        /// </summary>
        /// <remarks>
        /// Pure, so the editor and the runtime ask the same question and cannot drift. See
        /// <see cref="ResolveBiomeId"/> for why a custom biome — and a number, and the two obsolete
        /// names — are not among them.
        /// </remarks>
        public static bool TheEventCheckCanSeeThisBiome(string biomeId)
        {
            Biome real;
            return TheGamesOwnBiome(biomeId, out real);
        }

        /// <summary>
        /// The nine biomes of Core Keeper's own that a player can be standing in.
        /// </summary>
        /// <remarks>
        /// <c>Biome</c> has thirteen names. <c>None</c> is the absence of one, <c>__MAX_VALUE__</c>
        /// is the enum's own end marker, and <c>Obsidian</c> and <c>GreatWall</c> both carry
        /// <c>[Obsolete("Not used in full release world generation")]</c>. Nine are left, and they
        /// are the nine the refusal message names.
        /// </remarks>
        private static readonly string[] TheGamesOwnBiomeNames =
        {
            "Slime", "Larva", "Stone", "Nature", "Sea", "Desert", "Crystal", "Passage", "Excavation"
        };

        /// <summary>Answers a biome name, and only one of the nine a player can stand in.</summary>
        private static bool TheGamesOwnBiome(string biomeId, out Biome biome)
        {
            biome = Biome.None;
            if (string.IsNullOrEmpty(biomeId))
            {
                return false;
            }

            for (int i = 0; i < TheGamesOwnBiomeNames.Length; i++)
            {
                if (!string.Equals(TheGamesOwnBiomeNames[i], biomeId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                Biome named;
                if (System.Enum.TryParse(TheGamesOwnBiomeNames[i], false, out named))
                {
                    biome = named;
                    return true;
                }

                return false;
            }

            return false;
        }
    }

    /// <summary>
    /// The one moment the world's own events can be re-aimed: the system is about to read the table
    /// and bake it into native containers it keeps for the life of the world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS METHOD IS REACHABLE AT ALL. <c>EnvironmentEventSystem</c> is a struct
    /// <c>ISystem</c> whose type carries <c>[BurstCompile]</c>, and a method carrying its own
    /// <c>[BurstCompile]</c> is locked and cannot be patched — the law the scene injector already
    /// works to. <c>OnCreate</c> carries none, and could not, because it calls
    /// <c>Resources.Load</c> and compiles function pointers. So it stays managed and is the seam.
    /// </para>
    /// <para>
    /// A prefix that swaps the list and a postfix that swaps it back, rather than a prefix that
    /// replaces the method. The game's own baking code stays the game's, so a change to it in a
    /// future update carries through instead of being overwritten by a copy of the old one.
    /// </para>
    /// <para>
    /// AND THE PREFIX PUTS BACK WHAT AN EARLIER CALL LEFT. A postfix does not run when the method
    /// it follows throws, and world creation can throw for reasons that have nothing to do with
    /// this. Restoring at the top of the next call means one failed world cannot leave a mod's
    /// list on Core Keeper's asset for the rest of the session.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(EnvironmentEventSystem), "OnCreate")]
    internal static class DimensionEnvironmentEventsPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        ///
        /// COUNTED IN THE PREFIX ONLY. Both halves used to add, so one world load reported two and
        /// the number could not be read as "how many times this patch ran". Counting at the top
        /// also keeps the one distinction the stash-and-restore design exists to survive: a prefix
        /// that ran and an original that then threw leaves the count at one, where counting in both
        /// would have left it at one too and looked identical to a clean run only by accident.
        /// </remarks>
        internal static int Fired;

        /// <summary>The game's own list, held for the length of one call.</summary>
        private static List<EnvironmentEventParams> stashed;

        /// <summary>The table the list was taken off, so the postfix puts it back on the same one.</summary>
        private static EnvironmentEventsTable stashedTable;

        [HarmonyPrefix]
        private static void Before()
        {
            Fired++;

            // See the class remarks: an earlier call whose original threw never reached its
            // postfix, so anything still stashed is put back before this call swaps again.
            PutBackWhatIsStashed();
            if (!DimensionEnvironmentEventRegistry.HasAny)
            {
                return;
            }

            try
            {
                EnvironmentEventsTable table =
                    Resources.Load<EnvironmentEventsTable>("EnvironmentEventsTable");
                if (table == null)
                {
                    return;
                }

                List<EnvironmentEventParams> edited =
                    DimensionEnvironmentEventRegistry.BuildEditedList(
                        table.eventRequirements,
                        DimensionEnvironmentEventRegistry.ResolveBiomeId,
                        DimensionFrameworkLog.Warning);

                stashedTable = table;
                stashed = table.eventRequirements;
                table.eventRequirements = edited;
            }
            catch (System.Exception exception)
            {
                stashed = null;
                stashedTable = null;
                DimensionLog.Fatal(DimensionLogChannels.WorldRule, null,
                    "Could not change when the world acts on its own, so cave-ins and swarms " +
                    "happen where the game says they do: " + exception);
            }
        }

        [HarmonyPostfix]
        private static void After()
        {
            // Deliberately does not add to Fired — see the field's remarks. The prefix counts.
            PutBackWhatIsStashed();
        }

        private static void PutBackWhatIsStashed()
        {
            if (stashedTable != null && stashed != null)
            {
                stashedTable.eventRequirements = stashed;
            }

            stashed = null;
            stashedTable = null;
        }
    }
}
