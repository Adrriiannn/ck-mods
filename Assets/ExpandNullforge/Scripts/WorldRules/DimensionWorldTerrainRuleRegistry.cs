using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using WorldGen;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.WorldRules
{
    /// <summary>
    /// Puts a mod's blocks and ores into Core Keeper's own procedurally generated ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TABLE HAS ONE READER AND IT LOADS IT BY NAME.
    /// <c>SpawnProceduralTerrainSystem.OnCreate</c> does
    /// <c>Resources.Load&lt;TileTypeMapping&gt;("TileTypeMapping")</c> and copies every rule into a
    /// persistent <c>NativeArray</c> it keeps for the life of the world
    /// (<c>ck-db\WorldGen\SpawnProceduralTerrainSystem.cs:404-412</c>). Same shape as the tileset
    /// colour patch, so the same road.
    /// </para>
    /// <para>
    /// RULES GO IN FRONT, AND THE REASON IS THE ORDER THE GAME PLAYS THEM BACK IN. Matching rules
    /// are pushed into a <c>NativeParallelMultiHashMap</c> in list order, and that map's enumerator
    /// walks a key's values in REVERSE insertion order — <c>UnsafeParallelHashMapBase.TryAdd</c>
    /// links each new entry at the head of its bucket. The generator then calls
    /// <c>TileAccessor.Set</c> once per value as it walks, so for one tile and one layer the
    /// EARLIEST matching rule is written LAST and wins. A rule appended at the end of the list
    /// would therefore lose to every vanilla rule that matched the same tile; put in front, it
    /// wins. Nothing else about the game's own order changes, because inserting at the front keeps
    /// the game's rules in the same order relative to each other.
    /// </para>
    /// <para>
    /// PUT BACK AFTERWARDS. The asset is Core Keeper's own and stays loaded for the whole session,
    /// and the system has taken its own copy by the time the call returns, so the original list is
    /// stashed in the prefix and restored in the postfix. Same law as the upgrade-cost registry.
    /// </para>
    /// <para>
    /// WHAT THIS CANNOT DO. The question half of a rule is the generator's own vocabulary — ten
    /// biomes and twenty-one materials, both handed out from fixed tables inside the generator — so
    /// there is no way to ask about a biome this mod added. Only the answer half is open, and that
    /// is where a custom block or a custom ore goes.
    /// </para>
    /// </remarks>
    public static class DimensionWorldTerrainRuleRegistry
    {
        /// <summary>One rule, in the numbers the game's own table stores.</summary>
        public struct RuleRow
        {
            /// <summary>Which generator biome, or zero for any.</summary>
            public int InBiome;

            /// <summary>What the generator said the tile was made of, or zero for anything.</summary>
            public int MadeOf;

            /// <summary>Open floor: 0 any, 1 only where it is, 2 only where it is not.</summary>
            public int OpenFloor;

            /// <summary>Hole in the roof, on the same three-way scale.</summary>
            public int HoleInTheRoof;

            /// <summary>Great Wall, on the same three-way scale.</summary>
            public int GreatWall;

            /// <summary>Which resource slot: 0 any, 1 none, 2 to 6 for the first to the fifth.</summary>
            public int ResourceSlot;

            /// <summary>Which part of a block to lay. Core Keeper's <c>PugTilemap.TileType</c>.</summary>
            public int LayTilePart;

            /// <summary>Which block to lay it from. A tileset number, the game's own or this mod's.</summary>
            public int LayBlockId;
        }

        private static readonly List<RuleRow> Rows = new List<RuleRow>();

        /// <summary>Queues one rule. Call from the generated bootstrap.</summary>
        /// <remarks>
        /// A RULE ALREADY QUEUED WORD FOR WORD IS NOT QUEUED AGAIN. The generated consumer's
        /// <c>Shutdown()</c> only clears its own "already registered" flag, so a consumer reloaded
        /// without the framework reloading re-runs every <c>Register</c> on a registry nothing
        /// emptied. Five of the six tables replace by key and survive that; this one appended, so
        /// each reload put another copy of every rule at the front of Core Keeper's table and into
        /// the native array the generator keeps per world. Output was the same block laid twice, so
        /// nothing looked wrong and the list grew for the session.
        ///
        /// The comparison is all eight numbers, which is exactly the objection the old comment
        /// raised: two rules differing only in the block they lay differ here too, and both are
        /// kept. Only a rule identical in every answer is dropped, and a second copy of that could
        /// never lay anything the first did not.
        /// </remarks>
        public static void Register(
            int inBiome,
            int madeOf,
            int openFloor,
            int holeInTheRoof,
            int greatWall,
            int resourceSlot,
            int layTilePart,
            int layBlockId)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                RuleRow have = Rows[i];
                if (have.InBiome == inBiome &&
                    have.MadeOf == madeOf &&
                    have.OpenFloor == openFloor &&
                    have.HoleInTheRoof == holeInTheRoof &&
                    have.GreatWall == greatWall &&
                    have.ResourceSlot == resourceSlot &&
                    have.LayTilePart == layTilePart &&
                    have.LayBlockId == layBlockId)
                {
                    return;
                }
            }

            Rows.Add(new RuleRow
            {
                InBiome = inBiome,
                MadeOf = madeOf,
                OpenFloor = openFloor,
                HoleInTheRoof = holeInTheRoof,
                GreatWall = greatWall,
                ResourceSlot = resourceSlot,
                LayTilePart = layTilePart,
                LayBlockId = layBlockId
            });
        }

        /// <summary>Clears queued rules (mod reload).</summary>
        public static void Clear()
        {
            Rows.Clear();
        }

        /// <summary>True once at least one rule has been queued.</summary>
        public static bool HasAny { get { return Rows.Count > 0; } }

        internal static int PendingCount { get { return Rows.Count; } }

        /// <summary>
        /// The rules the generator should read: this mod's first, then the game's own unchanged.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Separated from the patch so the ordering law can be exercised without a running game.
        /// The one thing this must get right is that a mod's rules come FIRST — see the class
        /// remarks for why the front of the list is the winning end.
        /// </para>
        /// <para>
        /// Two rules that differ in any answer are both kept and both play — that is how the game
        /// lays a ground tile and a wall tile from one label. A rule identical in all eight answers
        /// never reaches here twice; see <see cref="Register"/>.
        /// </para>
        /// </remarks>
        internal static List<TileTypeMapping.MappingRule> BuildEditedList(
            List<TileTypeMapping.MappingRule> original)
        {
            List<TileTypeMapping.MappingRule> edited =
                new List<TileTypeMapping.MappingRule>(
                    Rows.Count + (original == null ? 0 : original.Count));

            for (int i = 0; i < Rows.Count; i++)
            {
                RuleRow row = Rows[i];
                edited.Add(new TileTypeMapping.MappingRule
                {
                    biome = (PugWorldGen.CoreKeeper.Biome)row.InBiome,
                    proceduralTileType = (PugWorldGen.CoreKeeper.TileType)row.MadeOf,
                    floorFlag = (TileTypeMapping.FlagState)row.OpenFloor,
                    roofHoleFlag = (TileTypeMapping.FlagState)row.HoleInTheRoof,
                    greatWallFlag = (TileTypeMapping.FlagState)row.GreatWall,
                    resourceIndex = (TileTypeMapping.ResourceIndex)row.ResourceSlot,
                    outputTile = new TileTypeMapping.MappingResult
                    {
                        tileType = (PugTilemap.TileType)row.LayTilePart,

                        // Raw cast: the generator writes this number straight onto the tile, so a
                        // custom tileset id lands there exactly as a vanilla one does.
                        tileset = (PugTilemap.Tileset)row.LayBlockId
                    }
                });
            }

            for (int i = 0; original != null && i < original.Count; i++)
            {
                edited.Add(original[i]);
            }

            return edited;
        }
    }

    /// <summary>
    /// The one moment the game's own terrain rules can be added to: the generator is about to read
    /// them into a native array it keeps for the life of the world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS METHOD IS REACHABLE AT ALL. <c>SpawnProceduralTerrainSystem</c> is a struct
    /// <c>ISystem</c> whose type carries <c>[BurstCompile]</c>, and a method carrying its own
    /// <c>[BurstCompile]</c> is locked and cannot be patched — the law the scene injector already
    /// works to. <c>OnDestroy</c> and <c>OnUpdate</c> carry one; <c>OnCreate</c> does not, and could
    /// not, because it calls <c>Resources.Load</c>. So it stays managed and is the seam.
    /// </para>
    /// <para>
    /// A prefix that swaps the list and a postfix that swaps it back, rather than a prefix that
    /// replaces the method. The generator's own copying code stays the game's, so a change to it in
    /// a future update carries through.
    /// </para>
    /// <para>
    /// AND THE PREFIX PUTS BACK WHAT AN EARLIER CALL LEFT. A postfix does not run when the method
    /// it follows throws, so restoring at the top of the next call keeps one failed world from
    /// leaving a mod's rules on Core Keeper's asset for the rest of the session.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(SpawnProceduralTerrainSystem), "OnCreate")]
    internal static class DimensionWorldTerrainRulesPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        ///
        /// COUNTED IN THE PREFIX ONLY. Both halves used to add, so one world load reported two and
        /// the number could not be read as "how many times this patch ran".
        /// </remarks>
        internal static int Fired;

        private static List<TileTypeMapping.MappingRule> stashed;

        private static TileTypeMapping stashedTable;

        [HarmonyPrefix]
        private static void Before()
        {
            Fired++;

            // See the class remarks: an earlier call whose original threw never reached its
            // postfix, so anything still stashed is put back before this call swaps again.
            PutBackWhatIsStashed();
            if (!DimensionWorldTerrainRuleRegistry.HasAny)
            {
                return;
            }

            try
            {
                TileTypeMapping table = Resources.Load<TileTypeMapping>("TileTypeMapping");
                if (table == null)
                {
                    return;
                }

                List<TileTypeMapping.MappingRule> edited =
                    DimensionWorldTerrainRuleRegistry.BuildEditedList(table.mapping);

                stashedTable = table;
                stashed = table.mapping;
                table.mapping = edited;
            }
            catch (System.Exception exception)
            {
                stashed = null;
                stashedTable = null;
                DimensionLog.Fatal(DimensionLogChannels.WorldRule, null,
                    "Could not add this mod's terrain rules, so the game's own world generates " +
                    "exactly as it always did: " + exception);
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
                stashedTable.mapping = stashed;
            }

            stashed = null;
            stashedTable = null;
        }
    }
}
