using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.WorldRules
{
    /// <summary>
    /// Adds armour sets of a mod's own to the table the game reads set bonuses out of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TABLE HAS ONE READER AND IT LOADS IT BY NAME. <c>SummarizeConditionsSystem.OnCreate</c>
    /// does <c>Resources.Load&lt;SetBonusesTable&gt;("SetBonusesTable")</c> and bakes the result
    /// into two native maps for the rest of the session
    /// (<c>ck-db\Pug.Other\SummarizeConditionsSystem.cs:29-47</c>). That is the same shape the
    /// tileset colour patch already works through, so this works the same way: append to the loaded
    /// table in a prefix, and let the game's own baking code do the rest.
    /// </para>
    /// <para>
    /// THE TOOLTIP IS THE SAME ASSET, AND THAT IS TRUE OF THE ROWS BUT NOT OF ITS LOOKUP.
    /// <c>UIMouse</c> carries its own <c>setBonusesTable</c> reference rather than loading it, but
    /// there is exactly one such asset in the game — <c>Resources/SetBonusesTable.asset</c>, guid
    /// <c>44aaba0691b626c4c82b0f332f9a23b0</c>, referenced by exactly one prefab,
    /// <c>Resources/Global Objects (Main Manager).prefab</c>. One file is one loaded instance, so
    /// the rows appended here are the rows the hover panel reads.
    /// </para>
    /// <para>
    /// THE ONE THING THAT DOES NOT COME ALONG, SAID PLAINLY. <c>GetSetBonusID</c> builds a private
    /// <c>objectIDToSetBonus</c> dictionary the first time it is asked and nothing ever clears it —
    /// there is no <c>OnValidate</c> reset the way <c>PetInfosTable</c> has for its talents
    /// (<c>ck-db\Pug.Other\SetBonusesTable.cs:10-27</c>). If anything asks that question before the
    /// first world is created, the map is built without this framework's rows and stays that way
    /// for the process; and after an in-editor reload with a changed set list, a second world reads
    /// the first world's piece-to-set map. Nothing here can clear it without reflection, which the
    /// sandbox denies. It has not been seen happening in a running game, and it is written down
    /// rather than claimed away because it is the same trap this file refuses to take on for pets.
    /// </para>
    /// <para>
    /// APPEND AT THE END, NEVER INSERT. <c>UpdateSetBonusDatas</c> runs right after this prefix and
    /// recomputes EVERY set's numbers, drawing a random jitter per bonus line from a stream seeded
    /// at 1337 and advanced in list order. Rows added at the end leave every draw before them
    /// untouched, so the game's own 62 sets come out with exactly the numbers they always had.
    /// Inserting anywhere else would quietly re-roll all of them.
    /// </para>
    /// <para>
    /// A PIECE MAY BELONG TO ONE SET ONLY. The baked lookup is built with
    /// <c>NativeParallelHashMap.Add</c> keyed by the item, which throws on a repeat, and the
    /// managed lookup behind the tooltip uses <c>Dictionary.Add</c>, which throws too. A duplicate
    /// piece is therefore not a cosmetic problem: it is an exception inside <c>OnCreate</c> as a
    /// world is being built. Every piece is checked against the game's own sets and against the
    /// sets already appended, and a clashing piece is dropped with a line saying so.
    /// </para>
    /// <para>
    /// ONCE PER SET, HOWEVER MANY WORLDS AND HOWEVER MANY MOD RELOADS. The table is loaded once and
    /// kept for the whole process, and the system that reads it is created again for every world.
    /// Rows this framework wrote earlier are stripped before anything is appended, so writing twice
    /// leaves exactly what writing once left — and so does a mod reload, which empties this
    /// registry but cannot empty Core Keeper's table. The strip runs even when there is nothing
    /// left to put back, so switching the block off and reloading takes the sets away instead of
    /// leaving them behind with nothing to explain them.
    /// </para>
    /// </remarks>
    public static class DimensionSetBonusRegistry
    {
        /// <summary>One armour set queued for the game's table, in names rather than numbers.</summary>
        public sealed class SetRow
        {
            /// <summary>The framework's own id for the set. Never shown to a player.</summary>
            public string SetId;

            /// <summary>The gear that belongs to it, by object name.</summary>
            public List<string> PieceNames;

            /// <summary>Which tier of the world it belongs to. Core Keeper's <c>AreaLevel</c>.</summary>
            public int Tier;

            /// <summary>How good it is. Core Keeper's <c>Rarity</c>.</summary>
            public int Rarity;

            /// <summary>What wearing enough of it gives.</summary>
            public List<LineRow> Lines;
        }

        /// <summary>One line of one set's bonus.</summary>
        public struct LineRow
        {
            /// <summary>The effect's name — the game's own, or one this mod registers.</summary>
            public string EffectName;

            /// <summary>How many pieces have to be worn for it.</summary>
            public int RequiredPieces;

            /// <summary>How much stronger than the tier alone would make it.</summary>
            public float Strength;

            /// <summary>How long it lasts once applied, in seconds. Zero is "while worn".</summary>
            public float Seconds;
        }

        /// <summary>
        /// How many pieces and how many bonus lines the hover panel can draw.
        /// </summary>
        /// <remarks>
        /// Measured on <c>Resources/Global Objects (Main Manager).prefab</c>: <c>setBonusesPieces</c>
        /// and <c>setBonusesStats</c> each hold six <c>PugText</c> slots, and <c>UIMouse</c> logs an
        /// error and stops at the seventh of either. Nothing breaks past it; it is simply not drawn.
        /// </remarks>
        public const int MostTheTooltipDraws = 6;

        private static readonly List<SetRow> Rows = new List<SetRow>();

        /// <summary>Set ids that reached the game's table on the most recent append.</summary>
        private static readonly List<string> AppendedSetIds = new List<string>();

        /// <summary>
        /// Every row this framework has ever put into the game's table this session, by identity.
        /// </summary>
        /// <remarks>
        /// <para>
        /// DELIBERATELY NOT CLEARED BY <see cref="Clear"/>. The table is Core Keeper's, loaded once
        /// and kept for the whole process; the registry is this mod's and is emptied when the mod
        /// reloads. Forgetting the rows already in the table would mean writing them a second time
        /// on the next load, and the game's item-to-set lookup throws on the repeat. So the rows
        /// this framework put there are stripped before anything is appended, which makes a reload
        /// leave exactly what a first load leaves.
        /// </para>
        /// <para>
        /// THE ROWS THEMSELVES, NOT THEIR NUMBERS, AND THAT IS THE CORRECTION. This used to hold
        /// the set NUMBERS and strip every row carrying one, under a comment saying another mod's
        /// sets were safe. They were not: <see cref="DimensionSetBonusesPatch.FirstFreeNumber"/> is
        /// the highest <c>SetBonusID</c> plus one, which is the number any other mod appending a
        /// set picks by exactly the same reasoning — so a second mod's set written at 63 was
        /// deleted by this framework's next append. A row is this framework's only if this
        /// framework is holding the object it added.
        /// </para>
        /// </remarks>
        private static readonly List<SetBonusInfo> rowsWeHaveWritten = new List<SetBonusInfo>();

        /// <summary>The table those rows were written into.</summary>
        /// <remarks>
        /// One <c>SetBonusesTable</c> exists in a running game and it is loaded once for the whole
        /// process, so this never changes there. It is held because the record only describes rows
        /// inside one table: handed a different one, what we are holding says nothing about it and
        /// would otherwise read as "there is still something of ours to take away" forever.
        /// </remarks>
        private static SetBonusesTable tableWeHaveWrittenInto;

        /// <summary>Whether this framework has ever written a row into the game's table.</summary>
        /// <remarks>
        /// Read by the patch so a load that switches the armour-set block OFF still comes in to
        /// take away what an earlier load left. Without it, unticking "Add sets" and reloading left
        /// the sets in Core Keeper's table with nothing left in this registry to explain them: the
        /// patch returned on <c>!HasAny</c> and <see cref="AppendTo"/> returned on an empty row
        /// list, both before the strip. For a player with no content pack this is false forever, so
        /// nothing new is loaded and nothing new runs.
        /// </remarks>
        internal static bool AnythingWasWrittenBefore
        {
            get { return rowsWeHaveWritten.Count > 0; }
        }

        /// <summary>Queues one set. Call from the generated bootstrap; names resolve at load.</summary>
        public static void Register(
            string setId,
            string[] pieceNames,
            int tier,
            int rarity,
            LineRow[] lines)
        {
            if (string.IsNullOrEmpty(setId) || pieceNames == null || pieceNames.Length == 0)
            {
                return;
            }

            SetRow row = new SetRow
            {
                SetId = setId,
                PieceNames = new List<string>(pieceNames.Length),
                Tier = tier,
                Rarity = rarity,
                Lines = new List<LineRow>()
            };

            for (int i = 0; i < pieceNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(pieceNames[i]))
                {
                    row.PieceNames.Add(pieceNames[i]);
                }
            }

            for (int i = 0; lines != null && i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i].EffectName))
                {
                    row.Lines.Add(lines[i]);
                }
            }

            if (row.PieceNames.Count == 0 || row.Lines.Count == 0)
            {
                return;
            }

            // Registering the same id twice replaces the earlier claim, so a mod reloaded in the
            // editor does not end up with two copies of its own set.
            for (int i = 0; i < Rows.Count; i++)
            {
                if (string.Equals(Rows[i].SetId, setId, System.StringComparison.Ordinal))
                {
                    Rows[i] = row;
                    return;
                }
            }

            Rows.Add(row);
        }

        /// <summary>Clears queued sets (mod reload).</summary>
        public static void Clear()
        {
            Rows.Clear();
            AppendedSetIds.Clear();
        }

        /// <summary>True once at least one set has been queued.</summary>
        public static bool HasAny { get { return Rows.Count > 0; } }

        internal static int PendingCount { get { return Rows.Count; } }

        /// <summary>Sets that actually reached the game's table.</summary>
        internal static IReadOnlyList<string> Applied { get { return AppendedSetIds; } }

        /// <summary>
        /// The number the game will know a queued set by, given the first number free after its own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NUMBERS COME FROM THE ORDER OF THE NAMES, not the order of registration, so the same mod
        /// gives the same set the same number on every load. That is the law the condition registry
        /// already follows, and for the same reason: a number that moves between loads is a number
        /// that ends up in a save file meaning something else.
        /// </para>
        /// <para>
        /// Pure on purpose: it is handed the first free number rather than reading the game's enum,
        /// so it can be asked the question without a running game.
        /// </para>
        /// </remarks>
        public static int NumberFor(string setId, int firstFreeNumber)
        {
            if (string.IsNullOrEmpty(setId))
            {
                return -1;
            }

            List<string> names = new List<string>(Rows.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                names.Add(Rows[i].SetId);
            }

            names.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], setId, System.StringComparison.Ordinal))
                {
                    return firstFreeNumber + i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Which pieces of a queued set may be used, given what is already claimed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS THE CHECK THAT KEEPS A WORLD LOADING. Both lookups the game builds off this
        /// table are keyed by the item and added with a method that throws on a repeat, so one item
        /// in two sets is an exception thrown while a world is being created. Numbers are compared
        /// rather than names, because two different names can answer the same object.
        /// </para>
        /// <para>
        /// Pure and testable: the caller says what is already claimed and gets back what is left.
        /// </para>
        /// </remarks>
        /// <param name="pieceNumbers">The set's pieces, already answered to object numbers.</param>
        /// <param name="alreadyClaimed">Every object number some other set already holds.</param>
        /// <param name="rejected">The numbers that had to be dropped, in the order found.</param>
        public static List<int> PiecesThatMayBeUsed(
            IReadOnlyList<int> pieceNumbers,
            HashSet<int> alreadyClaimed,
            out List<int> rejected)
        {
            List<int> kept = new List<int>();
            rejected = new List<int>();
            if (pieceNumbers == null)
            {
                return kept;
            }

            for (int i = 0; i < pieceNumbers.Count; i++)
            {
                int piece = pieceNumbers[i];
                if (alreadyClaimed != null && alreadyClaimed.Contains(piece))
                {
                    rejected.Add(piece);
                    continue;
                }

                // A set naming the same piece twice would collide with itself, which is the same
                // exception by a shorter road.
                if (kept.Contains(piece))
                {
                    rejected.Add(piece);
                    continue;
                }

                kept.Add(piece);
            }

            return kept;
        }

        /// <summary>
        /// Appends every queued set to the table the game is about to bake.
        /// </summary>
        /// <remarks>
        /// Separated from the patch so the whole decision can be exercised against a table built by
        /// hand. Returns how many sets were appended by this call.
        /// </remarks>
        internal static int AppendTo(
            SetBonusesTable table,
            int firstFreeNumber,
            System.Func<string, ObjectID> resolveItem,
            System.Func<string, ConditionID> resolveEffect,
            System.Action<string> report)
        {
            if (table == null)
            {
                return 0;
            }

            if (table.setBonuses == null)
            {
                table.setBonuses = new List<SetBonusInfo>();
            }

            // A different table knows nothing about the rows we are holding — see the field's
            // remarks. ReferenceEquals rather than ==, because the question is which object this
            // is and not whether Unity considers it alive.
            if (!ReferenceEquals(tableWeHaveWrittenInto, table))
            {
                rowsWeHaveWritten.Clear();
                tableWeHaveWrittenInto = table;
            }

            // Rows this framework wrote on an earlier world or an earlier load of the mod come out
            // first, and they come out even when there is nothing left to put back — a creator who
            // unticks "Add sets" and reloads is saying the sets should be gone. Without this, a
            // second write would give one set two rows and the game's own item-to-set lookup would
            // throw on the repeated piece as the world was being built.
            //
            // Matched by IDENTITY, not by set number: another mod appending a set picks its number
            // the same way this one does, so removing "every row carrying a number we handed out"
            // removed that mod's set too. A row is ours only if we are holding the object.
            for (int i = table.setBonuses.Count - 1; i >= 0; i--)
            {
                SetBonusInfo row = table.setBonuses[i];
                for (int w = 0; w < rowsWeHaveWritten.Count; w++)
                {
                    if (!ReferenceEquals(rowsWeHaveWritten[w], row))
                    {
                        continue;
                    }

                    table.setBonuses.RemoveAt(i);
                    rowsWeHaveWritten.RemoveAt(w);
                    break;
                }
            }

            AppendedSetIds.Clear();

            if (Rows.Count == 0)
            {
                return 0;
            }

            HashSet<int> claimed = new HashSet<int>();
            for (int i = 0; i < table.setBonuses.Count; i++)
            {
                SetBonusInfo existing = table.setBonuses[i];
                if (existing == null || existing.availablePieces == null)
                {
                    continue;
                }

                for (int p = 0; p < existing.availablePieces.Count; p++)
                {
                    claimed.Add((int)existing.availablePieces[p]);
                }
            }

            int appended = 0;
            for (int i = 0; i < Rows.Count; i++)
            {
                SetRow row = Rows[i];
                List<int> pieceNumbers = new List<int>(row.PieceNames.Count);
                for (int p = 0; p < row.PieceNames.Count; p++)
                {
                    ObjectID item = resolveItem == null
                        ? ObjectID.None
                        : resolveItem(row.PieceNames[p]);
                    if (item == ObjectID.None)
                    {
                        if (report != null)
                        {
                            report(
                                "The set '" + row.SetId + "' lists '" + row.PieceNames[p] +
                                "' as one of its pieces, which is not a thing this game has. That " +
                                "piece was left out — check the spelling against the item's own name.");
                        }

                        continue;
                    }

                    pieceNumbers.Add((int)item);
                }

                List<int> rejected;
                List<int> kept = PiecesThatMayBeUsed(pieceNumbers, claimed, out rejected);
                for (int r = 0; r < rejected.Count; r++)
                {
                    if (report != null)
                    {
                        report(
                            "The set '" + row.SetId + "' claims a piece that already belongs to " +
                            "another set, so that piece was left out of this one. A piece of gear " +
                            "can be in one set only.");
                    }
                }

                if (kept.Count == 0)
                {
                    if (report != null)
                    {
                        report(
                            "The set '" + row.SetId + "' has no pieces left that this game can " +
                            "use, so it was not added at all.");
                    }

                    continue;
                }

                int number = NumberFor(row.SetId, firstFreeNumber);
                if (number < 0)
                {
                    continue;
                }

                SetBonusInfo info = new SetBonusInfo
                {
                    setBonusID = (SetBonusID)number,
                    areaLevel = (AreaLevel)row.Tier,
                    rarity = (Rarity)row.Rarity,
                    availablePieces = new List<ObjectID>(kept.Count),
                    setBonusDatas = new List<SetBonusData>(row.Lines.Count)
                };

                for (int k = 0; k < kept.Count; k++)
                {
                    info.availablePieces.Add((ObjectID)kept[k]);
                    claimed.Add(kept[k]);
                }

                for (int l = 0; l < row.Lines.Count; l++)
                {
                    LineRow line = row.Lines[l];
                    ConditionID effect = resolveEffect == null
                        ? ConditionID.None
                        : resolveEffect(line.EffectName);
                    if (effect == ConditionID.None)
                    {
                        if (report != null)
                        {
                            report(
                                "The set '" + row.SetId + "' gives '" + line.EffectName +
                                "', which is not an effect this game has. That line was left out.");
                        }

                        continue;
                    }

                    info.setBonusDatas.Add(new SetBonusData
                    {
                        // The value is deliberately left at zero: UpdateSetBonusDatas overwrites it
                        // from the tier, the rarity and this multiplier before anything reads it.
                        conditionData = new ConditionData
                        {
                            conditionID = effect,
                            duration = line.Seconds,
                            value = 0,
                            valueMultiplier = line.Strength
                        },
                        requiredPieces = line.RequiredPieces
                    });
                }

                if (info.setBonusDatas.Count == 0)
                {
                    if (report != null)
                    {
                        report(
                            "The set '" + row.SetId + "' has no effects left that this game can " +
                            "give, so it was not added at all.");
                    }

                    continue;
                }

                if (info.availablePieces.Count > MostTheTooltipDraws && report != null)
                {
                    report(
                        "The set '" + row.SetId + "' has " + info.availablePieces.Count +
                        " pieces. The hover panel draws six, so the rest of them count but are " +
                        "never listed.");
                }

                if (info.setBonusDatas.Count > MostTheTooltipDraws && report != null)
                {
                    report(
                        "The set '" + row.SetId + "' gives " + info.setBonusDatas.Count +
                        " lines. The hover panel draws six, so the rest of them apply but are " +
                        "never listed.");
                }

                table.setBonuses.Add(info);
                AppendedSetIds.Add(row.SetId);
                rowsWeHaveWritten.Add(info);
                appended++;
            }

            return appended;
        }

        /// <summary>Answers an object name, this mod's own included, once the mod is loaded.</summary>
        internal static ObjectID ResolveItemName(string name)
        {
            return DimensionObjectNames.Resolve(name);
        }

        /// <summary>
        /// Answers an effect name — one this mod registered, or the game's own by its enum name.
        /// </summary>
        internal static ConditionID ResolveEffectName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return ConditionID.None;
            }

            ConditionID ours = Conditions.DimensionConditionRegistry.IdFor(name);
            if (ours != ConditionID.None)
            {
                return ours;
            }

            ConditionID theirs;
            return System.Enum.TryParse(name, false, out theirs) ? theirs : ConditionID.None;
        }
    }

    /// <summary>
    /// The one moment a set bonus can be added: the system that bakes them is about to read the
    /// table, and everything after runs from the baked copy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS METHOD IS REACHABLE AT ALL. <c>SummarizeConditionsSystem</c> is a struct
    /// <c>ISystem</c> whose type carries <c>[BurstCompile]</c>, and a method carrying its own
    /// <c>[BurstCompile]</c> is locked and cannot be patched — the law the scene injector already
    /// works to. <c>OnDestroy</c> and <c>OnUpdate</c> carry one; <c>OnCreate</c> does not, and could
    /// not, because it calls <c>Resources.Load</c>. So it stays managed and is the seam.
    /// </para>
    /// <para>
    /// A prefix that appends, rather than a replacement of the method — the game's own baking code
    /// stays the game's, so a change to it in a future update carries through. It never throws:
    /// every stat effect in the game is baked a line further on, and a mod's armour sets are not
    /// worth losing that over.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(SummarizeConditionsSystem), "OnCreate")]
    internal static class DimensionSetBonusesPatch
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
        /// The first set number free for a mod to use.
        /// </summary>
        /// <remarks>
        /// <c>SetBonusID</c> has no MAX_VALUES sentinel the way <c>ConditionID</c> does, so the
        /// ceiling is read off the enum's own values rather than written down. A Core Keeper update
        /// that adds sets therefore moves this on its own instead of silently colliding with the
        /// new ones.
        /// </remarks>
        internal static int FirstFreeNumber()
        {
            int highest = 0;
            System.Array values = System.Enum.GetValues(typeof(SetBonusID));
            for (int i = 0; i < values.Length; i++)
            {
                int value = (int)values.GetValue(i);
                if (value > highest)
                {
                    highest = value;
                }
            }

            return highest + 1;
        }

        [HarmonyPrefix]
        private static void Before()
        {
            Fired++;

            // Also entered with nothing to add, but only once something has been added before: a
            // load that switches the block off has to come in and take the earlier rows away, and
            // a game with no content pack has written nothing and so loads nothing here.
            if (!DimensionSetBonusRegistry.HasAny &&
                !DimensionSetBonusRegistry.AnythingWasWrittenBefore)
            {
                return;
            }

            try
            {
                SetBonusesTable table = Resources.Load<SetBonusesTable>("SetBonusesTable");
                if (table == null)
                {
                    return;
                }

                DimensionSetBonusRegistry.AppendTo(
                    table,
                    FirstFreeNumber(),
                    DimensionSetBonusRegistry.ResolveItemName,
                    DimensionSetBonusRegistry.ResolveEffectName,
                    DimensionFrameworkLog.Warning);
            }
            catch (System.Exception exception)
            {
                // Every stat effect in the game is baked one line further on. A mod's armour sets
                // are never worth losing that over.
                DimensionLog.Fatal(DimensionLogChannels.WorldRule, null,
                    "Could not add this mod's armour sets, so only the game's own sets give a " +
                    "bonus: " + exception);
            }
        }
    }
}
