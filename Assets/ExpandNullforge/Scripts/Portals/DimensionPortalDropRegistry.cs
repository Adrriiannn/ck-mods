using System.Collections.Generic;
using HarmonyLib;
using PugMod;
using UnityEngine;

namespace ExpandNullforge.Portals
{
    /// <summary>
    /// Registers portal items (the V1 placed-portal object or the V2 instantaneous-portal item) as
    /// drops on specific mob/boss loot tables. Drops are injected at load through a Harmony prefix on
    /// <c>LootTableConverter.Convert</c> — the same seam Core Keeper converts its own loot on. That
    /// runs after every mod's objects and loot tables exist, so a boss added by ANOTHER mod can be
    /// targeted safely (the "apply as late as possible" requirement). Modelled on the loot-table
    /// modification pattern proven by CoreLib's LootDropModule, reimplemented here so the framework
    /// stays standalone.
    ///
    /// This is authoring-neutral and reused beyond portals later: register a target object name, an
    /// item name, and a weight/amount, and the drop appears on that enemy.
    /// </summary>
    public static class DimensionPortalDropRegistry
    {
        public struct DropRequest
        {
            public string TargetObjectName;
            public string ItemObjectName;
            public float Weight;
            public float ChancePercent;
            public int MinAmount;
            public int MaxAmount;

            /// <summary>Biome this drop is restricted to, or empty for anywhere.</summary>
            /// <remarks>
            /// Core Keeper's own constraint, <c>LootInfo.onlyDropsInBiome</c>. It is a LOOT TABLE
            /// field with no equivalent on per-object custom loot, which is precisely why a
            /// biome-restricted drop has to arrive through this registry rather than being written
            /// onto a prefab at generation time.
            /// </remarks>
            public string OnlyInBiome;

            /// <summary>
            /// The loot table the source carries, when the framework itself stamped it.
            /// </summary>
            /// <remarks>
            /// <para>
            /// THE ONE FIELD THAT MAKES A MOD'S CREATURE DROP A MOD'S ITEM. The source's table used
            /// to be read back off its prefab entity here, through
            /// <c>PugDatabase.TryGetComponent</c>. That reads a lookup built from
            /// <c>Manager.ecs.ClientWorld ?? ServerWorld</c> — and this runs inside a prefix on
            /// <c>LootTableConverter.Convert</c>, which is called from <c>ECSManager.Init</c> with
            /// both worlds set to null, and later from a conversion whose entities have not reached
            /// the target world yet. So the read either threw or answered "no table", and a
            /// perfectly ordinary drop was reported as belonging to a creature with no loot table
            /// — the creature this framework had just stamped one onto.
            /// </para>
            /// <para>
            /// The editor knew the answer all along: it computed the id while writing the prefab.
            /// So it ships the NAME, and the id is minted here by the same arithmetic. Empty for a
            /// drop from one of the game's own creatures, which still has to be looked up.
            /// </para>
            /// </remarks>
            public string SourceLootTableName;
        }

        private static readonly List<DropRequest> Requests = new List<DropRequest>();
        private static readonly List<bool> Landed = new List<bool>();
        private static readonly HashSet<string> Complained =
            new HashSet<string>(System.StringComparer.Ordinal);
        private static bool applied;

        /// <summary>Queues a drop. Call from the generated bootstrap; resolution is deferred to load.</summary>
        public static void Register(
            string targetObjectName,
            string itemObjectName,
            float weight,
            float chancePercent,
            int minAmount,
            int maxAmount,
            string onlyInBiome = "",
            string sourceLootTableName = "")
        {
            if (string.IsNullOrEmpty(targetObjectName) || string.IsNullOrEmpty(itemObjectName))
            {
                return;
            }

            int min = Mathf.Max(1, minAmount);
            Requests.Add(new DropRequest
            {
                TargetObjectName = targetObjectName,
                ItemObjectName = itemObjectName,
                Weight = Mathf.Max(1f, weight),
                ChancePercent = Mathf.Clamp(chancePercent, 0f, 100f),
                MinAmount = min,
                MaxAmount = Mathf.Max(min, maxAmount),
                OnlyInBiome = onlyInBiome ?? string.Empty,
                SourceLootTableName = sourceLootTableName ?? string.Empty
            });

            Landed.Add(false);
            applied = false;
        }

        /// <summary>
        /// How many drops are queued onto other things' loot tables. Read by the self-audit.
        /// </summary>
        /// <remarks>
        /// Same reason as the loot registry's own count: the patch never running matters only when
        /// somebody asked for a drop.
        /// </remarks>
        public static int QueuedDropCount
        {
            get { return Requests.Count; }
        }

        /// <summary>Clears queued drops (mod reload).</summary>
        public static void Clear()
        {
            Requests.Clear();
            Landed.Clear();
            Complained.Clear();
            mintedHere.Clear();
            missedItsTable.Clear();
            applied = false;
        }

        /// <summary>
        /// Applies every queued drop into the game's loot tables. Finds each source's table — the
        /// name the generator shipped for one of the mod's own, the game's prefab for one of the
        /// game's — and appends the item as a weighted entry, or a guaranteed one when chance is
        /// 100. Idempotent per load, and safe to run again.
        /// </summary>
        /// <remarks>
        /// IT MUST NOT SPEND ITS ONE CHANCE. Latching <c>applied</c> whether or not anything has
        /// landed leaves every drop of a run made during a moment the game could not answer
        /// reported as broken and never tried again. So each row is ticked off as it lands, an unlanded row
        /// is retried on the next conversion, and the latch only closes once nothing is left. Every
        /// append is guarded against being made twice, which is what makes the retry free.
        /// </remarks>
        internal static void ApplyToLootTables()
        {
            if (applied || Requests.Count == 0)
            {
                return;
            }

            List<LootTable> lootTables = Manager.mod == null ? null : Manager.mod.LootTable;
            if (lootTables == null)
            {
                return;
            }

            while (Landed.Count < Requests.Count)
            {
                Landed.Add(false);
            }

            Dictionary<LootTableID, LootTable> byId = new Dictionary<LootTableID, LootTable>();
            for (int i = 0; i < lootTables.Count; i++)
            {
                LootTable table = lootTables[i];
                if (table != null)
                {
                    byId[table.id] = table;
                }
            }

            bool everythingLanded = true;

            for (int i = 0; i < Requests.Count; i++)
            {
                if (Landed[i])
                {
                    continue;
                }

                DropRequest request = Requests[i];
                ObjectID targetId = API.Authoring.GetObjectID(request.TargetObjectName);
                ObjectID itemId = API.Authoring.GetObjectID(request.ItemObjectName);
                if (targetId == ObjectID.None || itemId == ObjectID.None)
                {
                    // Named separately, because "could not resolve X or Y" made a creator check
                    // both and learn nothing, and "portal" is a word they never used.
                    string missing = targetId == ObjectID.None
                        ? request.TargetObjectName
                        : request.ItemObjectName;
                    WarnOnce(
                        "'" + request.ItemObjectName + "' is set to drop from '" +
                        request.TargetObjectName + "', but nothing in this world is called '" +
                        missing + "'. Check the spelling, or that it is still switched on, and " +
                        "generate again.");
                    everythingLanded = false;
                    continue;
                }

                LootTableID tableId;
                if (!TryResolveLootTableId(request, targetId, out tableId))
                {
                    // SAID ON THE SECOND MISS, NOT THE FIRST, and the sentence no longer claims to
                    // know why. Another mod's creature is read off PugDatabase.entityMonobehaviours,
                    // and the game only adds other mods' authoring to that list AFTER the conversion
                    // this prefix runs inside (ECSManager.cs:98 comes after :95) — so a first miss
                    // is very often just "not yet", and the row lands on the next conversion. The
                    // old sentence stated flatly that the object had no loot table and told the
                    // author to pick between two explanations, neither of which fitted that case,
                    // and WarnOnce left the false line in the log for the session.
                    if (MissedItsTableBefore(i))
                    {
                        WarnOnce(
                            "'" + request.ItemObjectName + "' is set to drop " +
                            "from '" + request.TargetObjectName + "', and no loot table for it " +
                            "could be found to put the drop in, so it has not dropped yet. If it " +
                            "is one of yours, generate again; if it is one of the game's, it may " +
                            "be something that does not drop loot from a table at all; if it " +
                            "belongs to another mod, it will be picked up once that mod's objects " +
                            "are loaded.");
                    }

                    everythingLanded = false;
                    continue;
                }

                LootTable lootTable;
                if (!byId.TryGetValue(tableId, out lootTable))
                {
                    // THE TABLE THE GENERATOR PROMISED. A source of this mod's own with no loot
                    // table of its own is stamped at generate time with an id minted from its name
                    // (DimensionDropEmitter.EnsureALootTableToHangDropsOn), precisely so there is
                    // somewhere to put drops that per-object custom loot cannot carry. Nothing
                    // creates that table but this: it exists the moment something wants to drop
                    // out of it. Only a MINTED id is created this way — a vanilla table id that is
                    // missing from the bank is a real anomaly and still says so.
                    if ((int)tableId < Loot.DimensionLootTableRegistry.MinCustomLootTableId)
                    {
                        WarnOnce(
                            "'" + request.ItemObjectName + "' is set to drop " +
                            "from '" + request.TargetObjectName + "', whose loot table this " +
                            "version of the game does not have, so it will never drop.");
                        everythingLanded = false;
                        continue;
                    }

                    // AN AUTHORED TABLE STILL BEING BUILT IS NOT A MISSING ONE. The authored-table
                    // patch runs first, but it holds a table back while more of its rows are still
                    // resolving — and creating one here under the same id would put two tables with
                    // one number into the game's list.
                    if (Loot.DimensionLootTableRegistry.HasQueuedTableWithId(tableId))
                    {
                        everythingLanded = false;
                        continue;
                    }

                    // Shape is decided by ShapeAMintedTable once the rows are in — see there for
                    // why min and max end up equal and why duplicates are allowed. This is only the
                    // empty table; every number on it is rewritten below.
                    lootTable = new LootTable
                    {
                        id = tableId,
                        minUniqueDrops = 1,
                        maxUniqueDrops = 1,
                        dontAllowDuplicates = false,
                        lootInfos = new List<LootInfo>(),
                        guaranteedLootInfos = new List<LootInfo>()
                    };
                    lootTables.Add(lootTable);
                    byId[tableId] = lootTable;
                    mintedHere.Add(tableId);
                }

                bool guaranteed = request.ChancePercent >= 100f;
                List<LootInfo> pool = guaranteed ? lootTable.guaranteedLootInfos : lootTable.lootInfos;
                if (pool == null)
                {
                    WarnOnce(
                        "'" + request.ItemObjectName + "' is set to drop from '" +
                        request.TargetObjectName + "', but its loot table has no list to put the " +
                        "drop in, so it will never drop.");
                    everythingLanded = false;
                    continue;
                }

                bool alreadyPresent = false;
                for (int j = 0; j < pool.Count; j++)
                {
                    if (pool[j].objectID == itemId)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (alreadyPresent)
                {
                    // Already in from an earlier pass over the same list — the row has landed.
                    Landed[i] = true;
                    continue;
                }

                bool minted = mintedHere.Contains(tableId);

                // ON A TABLE WE MINTED, THE AUTHORED CHANCE DECIDES THE WEIGHT — ShapeAMintedTable
                // works it out below and rewrites every row. On a table something else owns — one
                // of the game's, or one the author wrote rows into — the entry can only compete on
                // the author's share, because rewriting that table's weights would change what
                // everything else drawing from it drops. The generator says which of the two
                // happened rather than leaving the author to guess.
                //
                // The chance rides on editorVisualDropChance, which is where Core Keeper's own bank
                // keeps a row's percentage (LootTableBank.InitLoot). Nothing at runtime reads it:
                // LootTableConverter does not copy it into the blob, so it survives every retry as
                // the one record of what the author actually typed.
                LootInfo entry = new LootInfo
                {
                    objectID = itemId,
                    amount = new Pug.UnityExtensions.RangeInt { min = request.MinAmount, max = request.MaxAmount },
                    isPartOfGuaranteedDrop = guaranteed,
                    weight = request.Weight,
                    editorVisualDropChance = request.ChancePercent
                };

                // Only set when asked. Biome's zero value IS "anywhere" — Biome.None = 0 — so an
                // untouched field already means everywhere, and a misspelled name parsed onto the
                // field would land on None and quietly widen the drop rather than narrow it.
                if (!string.IsNullOrEmpty(request.OnlyInBiome))
                {
                    Biome biome;
                    if (System.Enum.TryParse(request.OnlyInBiome, false, out biome))
                    {
                        entry.onlyDropsInBiome = biome;
                    }
                    else
                    {
                        WarnOnce(
                            "Drop for '" + request.ItemObjectName + "' names biome '" +
                            request.OnlyInBiome + "', which the game does not have. It will drop " +
                            "everywhere instead of nowhere.");
                    }
                }

                pool.Add(entry);
                Landed[i] = true;
                if (guaranteed)
                {
                    touchedGuaranteedPools.Add(lootTable);
                }

                if (minted && !touchedMintedTables.Contains(lootTable))
                {
                    touchedMintedTables.Add(lootTable);
                }
            }

            for (int i = 0; i < touchedGuaranteedPools.Count; i++)
            {
                RecomputeGuaranteedDropChances(touchedGuaranteedPools[i]);
            }

            for (int i = 0; i < touchedMintedTables.Count; i++)
            {
                ShapeAMintedTable(touchedMintedTables[i]);
            }

            touchedMintedTables.Clear();
            touchedGuaranteedPools.Clear();
            applied = everythingLanded;
        }

        private static readonly List<LootTable> touchedGuaranteedPools = new List<LootTable>();

        private static readonly List<LootTable> touchedMintedTables = new List<LootTable>();

        /// <summary>
        /// The most draws a minted table will ever be given.
        /// </summary>
        /// <remarks>
        /// Only reached by chances that all but add up to certainty on their own. Every roll is a
        /// loop iteration in <c>PugDatabase.GetRandomLoot</c>, so this is a ceiling on the work one
        /// kill can cost, not a design limit anyone is expected to meet.
        /// </remarks>
        public const int MostRollsAMintedTableGets = 32;

        /// <summary>
        /// How many times a minted table has to roll for every authored chance to come out right.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A Core Keeper loot table does not hold a chance per row. It holds a count of how many
        /// things it hands out and a weight per row, and each hand-out is one independent pick
        /// across the weights (<c>PugDatabase.GetRandomLoot</c>). So a row of share <c>p</c> in a
        /// table that picks <c>n</c> times comes out at least once with probability
        /// <c>1 - (1 - p)^n</c> — which is exactly the percentage Core Keeper's own bank writes into
        /// <c>LootInfo.editorVisualDropChance</c> (<c>LootTableBank.InitLoot</c>). Turning that
        /// round, a row that wants to drop <c>c</c> of the time needs a share of
        /// <c>1 - (1 - c)^(1/n)</c>.
        /// </para>
        /// <para>
        /// The shares of a table have to fit inside one whole, so one roll can only carry chances
        /// that add up to 1. Two things at 80% each do not fit in one roll and do fit in three. So
        /// this is the smallest number of rolls the authored chances fit in.
        /// </para>
        /// </remarks>
        public static int RollsNeededForChances(IList<float> chancesFrom0To1)
        {
            if (chancesFrom0To1 == null || chancesFrom0To1.Count == 0)
            {
                return 1;
            }

            for (int rolls = 1; rolls <= MostRollsAMintedTableGets; rolls++)
            {
                float shares = 0f;
                for (int i = 0; i < chancesFrom0To1.Count; i++)
                {
                    shares += ShareForChance(chancesFrom0To1[i], rolls);
                }

                if (shares <= 1f)
                {
                    return rolls;
                }
            }

            return MostRollsAMintedTableGets;
        }

        /// <summary>The share one row needs to come out <paramref name="chance"/> of the time.</summary>
        public static float ShareForChance(float chance, int rolls)
        {
            if (chance <= 0f)
            {
                return 0f;
            }

            if (rolls < 1)
            {
                rolls = 1;
            }

            // A chance of exactly 1 has no finite share; the always-drops pool is where certainty
            // lives. Anything that got here asking for it is held just under, so the arithmetic
            // stays finite and the row still all but always drops.
            float wanted = chance >= NearlyCertain ? NearlyCertain : chance;
            if (rolls == 1)
            {
                return wanted;
            }

            return 1f - Mathf.Pow(1f - wanted, 1f / rolls);
        }

        private const float NearlyCertain = 0.999f;

        /// <summary>
        /// Rewrites a minted table so every row drops as often as its author said it would.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE NUMBER THE AUTHOR TYPED HAS TO SURVIVE THIS. A drop's chance reaches here and decides
        /// whether the row goes in the always-drops pool; letting it stop there — the row carrying
        /// the author's SHARE instead, the table minted with <c>minUniqueDrops = 1</c> — throws it
        /// away. One weighted row in a table that hands out one thing wins every
        /// roll, so an item written as "one in twenty" would drop on every single kill. A control that
        /// reads as a probability and behaves as a weight is the worst thing this framework can
        /// ship, so the table is built to make it true.
        /// </para>
        /// <para>
        /// Three pieces, all of them Core Keeper's own:
        /// </para>
        /// <list type="number">
        /// <item><description>
        /// Rolls are fixed — minimum equals maximum — at the count the chances fit in
        /// (<see cref="RollsNeededForChances"/>). A varying count would make every row's chance vary
        /// with it.
        /// </description></item>
        /// <item><description>
        /// Each row's weight becomes the share that produces its chance over that many rolls.
        /// </description></item>
        /// <item><description>
        /// A row of nothing takes up the rest of the whole. <c>GetRandomLoot</c> spends a roll on an
        /// <c>ObjectID.None</c> row and adds nothing, which is how a table hands out nothing at all
        /// — 74 of the game's 176 loot tables carry exactly such a row, and it is the only way a
        /// weighted pool can be allowed to come up empty.
        /// </description></item>
        /// </list>
        /// <para>
        /// The always-drops pool takes a roll of its own on top, because <c>GetRandomLoot</c> fills
        /// its guaranteed pick first and then only fills the REMAINING slots from the weights. A
        /// table that had one always-drop and one chance drop, both fighting over a single slot,
        /// gave the chance drop no slot to be picked in and it could never drop.
        /// </para>
        /// <para>
        /// Duplicates stay allowed, as they are on all 176 of the game's own tables. Barring them
        /// costs more than it saves: <c>GetRandomLoot</c> reads the amount range of a
        /// NON-STACKABLE item as a number of copies and stops after the first when duplicates are
        /// barred, so "2 to 5 of my sword" would quietly ship exactly one.
        /// </para>
        /// </remarks>
        public static void ShapeAMintedTable(LootTable lootTable)
        {
            if (lootTable == null || lootTable.lootInfos == null)
            {
                return;
            }

            lootTable.dontAllowDuplicates = false;

            List<LootInfo> rows = lootTable.lootInfos;

            // The row of nothing is ours and is rebuilt every time; it must not be counted as one of
            // the author's while the shares are worked out.
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (rows[i] != null && rows[i].objectID == ObjectID.None)
                {
                    rows.RemoveAt(i);
                }
            }

            int guaranteedRolls =
                lootTable.guaranteedLootInfos != null && lootTable.guaranteedLootInfos.Count > 0
                    ? 1
                    : 0;

            if (rows.Count == 0)
            {
                int onlyGuaranteed = guaranteedRolls > 0 ? guaranteedRolls : 1;
                lootTable.minUniqueDrops = onlyGuaranteed;
                lootTable.maxUniqueDrops = onlyGuaranteed;
                return;
            }

            List<float> chances = new List<float>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                chances.Add(Mathf.Clamp01(rows[i].editorVisualDropChance / 100f));
            }

            int rolls = RollsNeededForChances(chances);

            float shares = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                LootInfo row = rows[i];
                row.weight = ShareForChance(chances[i], rolls);
                shares += row.weight;
                rows[i] = row;
            }

            float nothing = 1f - shares;
            if (nothing > 0.0001f)
            {
                rows.Add(new LootInfo
                {
                    objectID = ObjectID.None,
                    weight = nothing,
                    amount = new Pug.UnityExtensions.RangeInt { min = 1, max = 1 }
                });
            }

            lootTable.minUniqueDrops = rolls + guaranteedRolls;
            lootTable.maxUniqueDrops = rolls + guaranteedRolls;
        }

        /// <summary>
        /// The tables this pass created, kept for the session so a later retry that appends to one
        /// still grows how many rows it can hand out.
        /// </summary>
        private static readonly HashSet<LootTableID> mintedHere = new HashSet<LootTableID>();

        /// <summary>
        /// Says a thing once per session, however many conversions run.
        /// </summary>
        /// <remarks>
        /// The pass is retried until every row lands, and a row that names a misspelling never
        /// will. Repeating the same sentence on every retry would bury the ones that matter.
        /// </remarks>
        /// <summary>
        /// Whether this row has already failed to find its source's loot table on an earlier pass.
        /// </summary>
        /// <remarks>
        /// Records the miss and answers for the miss BEFORE it, so nothing is said the first time.
        /// A conversion that runs before another mod's authoring reaches the game's list is an
        /// ordinary event, not something to tell a creator about.
        /// </remarks>
        private static bool MissedItsTableBefore(int requestIndex)
        {
            bool before = missedItsTable.Contains(requestIndex);
            missedItsTable.Add(requestIndex);
            return before;
        }

        private static readonly HashSet<int> missedItsTable = new HashSet<int>();

        private static void WarnOnce(string message)
        {
            if (Complained.Add(message))
            {
                Foundation.DimensionFrameworkLog.Warning(message);
            }
        }

        /// <summary>
        /// Rebuilds the running drop chance across a guaranteed pool after appending to it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A guaranteed drop is chosen by walking the pool for the first entry whose
        /// <c>accumulatedDropChance</c> is at or above a roll of 0 to 1
        /// (<c>PugDatabase.cs:543</c>). That field is a running share of the pool's total weight,
        /// and Core Keeper computes it once while preparing the bank
        /// (<c>LootTableBank.cs:88</c>) — strictly before a mod gets to append anything.
        /// </para>
        /// <para>
        /// So an appended entry carries zero there, and zero is never at or above a roll: the drop
        /// is in the table, reads as guaranteed everywhere, and can never come out. Recomputing the
        /// running total ourselves is what makes an appended guaranteed drop real. It has to cover
        /// the whole pool rather than our own rows, because adding weight changes every existing
        /// entry's share of it.
        /// </para>
        /// </remarks>
        private static void RecomputeGuaranteedDropChances(LootTable lootTable)
        {
            List<LootInfo> pool = lootTable == null ? null : lootTable.guaranteedLootInfos;
            if (pool == null || pool.Count == 0)
            {
                return;
            }

            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += pool[i].weight;
            }

            if (totalWeight <= 0f)
            {
                return;
            }

            float running = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                LootInfo entry = pool[i];
                running += entry.weight / totalWeight;
                entry.accumulatedDropChance = running;
                pool[i] = entry;
            }
        }

        /// <summary>
        /// The loot table a drop should be appended to, without asking the entity world anything.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS WHERE A MOD'S CREATURE DROPPING A MOD'S ITEM STOPS, IF IT IS ANSWERED THE
        /// OBVIOUS WAY. That answer is
        /// <c>PugDatabase.TryGetComponent(objectData, out DropsLootFromLootTableCD)</c>, which
        /// builds a prefab lookup out of <c>Manager.ecs.ClientWorld ?? ServerWorld</c>
        /// (<c>PugDatabase.InitObjectPrefabEntityLookup</c>). Every moment this prefix runs is a
        /// moment that query is wrong: <c>ECSManager.Init</c> nulls both worlds before the
        /// conversion that reaches <c>LootTableConverter.Convert</c>, and the later conversions
        /// write into a staging world whose entities have not been moved across yet. The read
        /// either dereferences a null dictionary or answers "no table", so the framework would tell
        /// the author their creature had no loot table one line after stamping one onto it. It
        /// would also cache that empty dictionary against the world for the rest of the session,
        /// making every other <c>PugDatabase</c> component read on the client answer false.
        /// </para>
        /// <para>
        /// Two answers replace it, neither of which touches an entity world:
        /// </para>
        /// <para>
        /// <b>One of the mod's own</b> — the generator already decided the table and shipped its
        /// name in the row, so the id is minted here from that name by the same arithmetic that
        /// stamped the prefab.
        /// </para>
        /// <para>
        /// <b>One of the game's</b> — read off the prefab's own <c>DropLootAuthoring</c>, through
        /// <c>PugDatabase.entityMonobehaviours</c>. That list is plain MonoBehaviours, filled by
        /// <c>UpdateEntityMonos</c> from the database's prefab list BEFORE the conversion that
        /// reaches this prefix (<c>ECSManager.cs:85</c> against <c>:95</c>), so it can answer while
        /// the worlds cannot.
        /// </para>
        /// <para>
        /// ONE THING IT CANNOT ANSWER ON THE FIRST PASS: another mod's object. Only the game's own
        /// prefabs are in that list at <c>:85</c>; the other mods' authoring is added at
        /// <c>ECSManager.cs:98</c>, after the conversion at <c>:95</c>. So a drop aimed at another
        /// mod's creature misses here the first time and lands on a later conversion, which is why
        /// nothing is said about it until it has missed twice.
        /// </para>
        /// </remarks>
        private static bool TryResolveLootTableId(
            DropRequest request,
            ObjectID sourceId,
            out LootTableID lootTableId)
        {
            if (!string.IsNullOrEmpty(request.SourceLootTableName))
            {
                // The game's own name wins, exactly as DimensionLootTableRegistry.TryResolve reads
                // it — otherwise a source pointed at "AncientChest" would be given a minted id no
                // table carries. Anything else is one of ours and is minted from the name, whether
                // it is a table the author wrote or the one the generator gave the source to hang
                // drops on. The editor's DimensionDropEmitter.LootTableIdFor is the same two lines.
                if (!System.Enum.TryParse(request.SourceLootTableName, false, out lootTableId) ||
                    lootTableId == LootTableID.Empty)
                {
                    lootTableId = (LootTableID)Loot.DimensionLootTableRegistry.ComputeLootTableId(
                        request.SourceLootTableName);
                }

                return true;
            }

            return TryReadLootTableOffThePrefab(sourceId, out lootTableId);
        }

        /// <summary>
        /// One of the game's own objects' loot table, read off the authoring prefab it was built
        /// from rather than out of an entity world.
        /// </summary>
        private static bool TryReadLootTableOffThePrefab(
            ObjectID sourceId,
            out LootTableID lootTableId)
        {
            lootTableId = LootTableID.Empty;

            List<IEntityMonoBehaviourData> prefabs = PugDatabase.entityMonobehaviours;
            if (prefabs == null)
            {
                return false;
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                IEntityMonoBehaviourData prefab = prefabs[i];
                if (prefab == null ||
                    prefab.ObjectInfo == null ||
                    prefab.ObjectInfo.objectID != sourceId)
                {
                    continue;
                }

                GameObject go = prefab.GameObject;
                if (go == null)
                {
                    continue;
                }

                DropLootAuthoring dropLoot = go.GetComponent<DropLootAuthoring>();
                if (dropLoot == null || !dropLoot.hasLootTable)
                {
                    continue;
                }

                lootTableId = dropLoot.lootTableID;
                if (lootTableId != LootTableID.Empty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Harmony prefix that injects the framework's queued drops the moment Core Keeper builds its
    /// loot tables. Attribute-applied by the mod loader like the ModSDK example patches.
    /// </summary>
    /// <remarks>
    /// LAST, DELIBERATELY. <c>DimensionLootTableConverterPatch</c> creates this mod's own loot
    /// tables in a prefix on the same method, and a drop aimed at one of them has to find it
    /// already there — this pass skips what it cannot find and then latches for the session. Two
    /// prefixes at the same priority are ordered arbitrarily by Harmony, so the pair is pinned:
    /// <c>Priority.First</c> to create, <c>Priority.Last</c> to append.
    /// </remarks>
    [HarmonyPatch(typeof(LootTableConverter), nameof(LootTableConverter.Convert))]
    internal static class DimensionPortalLootTablePatch
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
        [HarmonyPriority(Priority.Last)]
        private static void Prefix()
        {
            Fired++;

            DimensionPortalDropRegistry.ApplyToLootTables();
        }
    }
}
