using System;
using System.Collections.Generic;
using HarmonyLib;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.WorldRules
{
    /// <summary>
    /// Gives a fish this mod adds a fight of its own on the line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS NOT A SETTINGS FILE LIKE THE REST OF FISHING. Core Keeper reads fish fights out
    /// of <c>Conf/Fishing/*.json</c>, and that file names the fish by its object <b>number</b>
    /// (<c>ck-db\Pug.Mods\Fishing.cs:151-186</c>, field <c>fish</c>, read with Unity's own JSON
    /// reader, which understands an enum only as a number). A mod's own object has no number until
    /// the mod is loaded and its objects are converted, which happens strictly after every Conf
    /// file has already been read. So the file can speak about Core Keeper's fish and no others,
    /// and a fish this mod adds needs a different moment.
    /// </para>
    /// <para>
    /// THE MOMENT. <c>FishingTableConverter.Convert</c> bakes the table the game actually fishes
    /// from, and it bakes it out of <c>Manager.mod.FishingTable</c> — a managed list still sitting
    /// in memory (<c>ck-db\Pug.ECS.Conversion\FishingTableConverter.cs:10-13</c>). Appending to
    /// that list in a prefix puts a fish in the baked table with no patch anywhere near gameplay.
    /// The names resolve there because conversion is a FIFO queue and Core Keeper enqueues mod
    /// objects BEFORE the object that carries the fishing table
    /// (<c>ck-db\Pug.Other\ECSManager.cs:86-93</c> then
    /// <c>ck-db\PugConversion\Pug\Conversion\ConversionManager.cs:403,413-417</c>), so by the time
    /// this prefix runs every mod object has already registered its name.
    /// </para>
    /// <para>
    /// WHY APPENDING IS SAFE HERE, where the upgrade-cost registry had to copy first.
    /// <c>Manager.mod.FishingTable</c> is not Core Keeper's own asset: <c>ModManager.Init</c>
    /// creates a fresh instance and copies the asset into it before mods touch anything
    /// (<c>ck-db\Pug.Other\ModManager.cs:207-210</c>). It is, however, made once and kept for the
    /// whole session, so a second world loaded in the same sitting runs this prefix a second time
    /// over a table that already carries our fish. Every write here is therefore an upsert keyed
    /// by fish, never an append, and running it ten times leaves exactly what running it once did.
    /// </para>
    /// <para>
    /// NOTHING READS THE FIGHT BY INDEX. The baked lookup is a linear scan comparing object
    /// numbers (<c>ck-db\Pug.ECS.Components\FishingTableCD.cs:45-66</c>) and the array is
    /// allocated at the table's own length, so unlike fishing's by-biome and by-water rules there
    /// is no fixed-size ceiling here — a mod's object number, far above Core Keeper's own, is just
    /// another value the scan can match. A fish with no fight of its own falls through to the
    /// table's default fight rather than breaking.
    /// </para>
    /// </remarks>
    public static class DimensionFishFightRegistry
    {
        /// <summary>One turn of one fish's fight: it pulls, or it rests, for so many seconds.</summary>
        public struct FightTurn
        {
            /// <summary>True for a turn where the fish pulls, false for a turn where it rests.</summary>
            public bool Pulls;

            /// <summary>How long the turn lasts, in seconds.</summary>
            public float Seconds;
        }

        private sealed class PendingFight
        {
            public string FishObjectName;
            public List<FightTurn> Turns;
        }

        private static readonly List<PendingFight> Pending = new List<PendingFight>();
        private static readonly List<string> AppliedFish = new List<string>();

        /// <summary>
        /// Queues one fish's fight. Call from the generated bootstrap; the name is answered when
        /// the game's objects exist.
        /// </summary>
        /// <remarks>
        /// The turns arrive as two rows of plain values rather than as a list of turns because the
        /// caller is generated C# source, which can write numbers and nothing else. The same shape
        /// the vehicle drift curve already uses.
        /// </remarks>
        public static void Register(string fishObjectName, bool[] pulls, float[] seconds)
        {
            if (string.IsNullOrEmpty(fishObjectName) || pulls == null || seconds == null)
            {
                return;
            }

            int count = pulls.Length < seconds.Length ? pulls.Length : seconds.Length;
            List<FightTurn> turns = new List<FightTurn>(count);
            for (int i = 0; i < count; i++)
            {
                turns.Add(new FightTurn { Pulls = pulls[i], Seconds = seconds[i] });
            }

            Register(fishObjectName, turns);
        }

        /// <summary>Queues one fish's fight from turns already in order.</summary>
        public static void Register(string fishObjectName, IReadOnlyList<FightTurn> turns)
        {
            if (string.IsNullOrEmpty(fishObjectName) || turns == null || turns.Count == 0)
            {
                return;
            }

            if (!HasBothKindsOfTurn(turns))
            {
                // A fight that is all pulling or all resting never resolves, so the fish could be
                // hooked and never landed. Core Keeper says the same thing about its own table
                // (FishingTable.OnValidate, "Invalid fish pattern for fish"); saying it here as
                // well means a bootstrap built before this rule existed still cannot ship one.
                Foundation.DimensionFrameworkLog.Warning(
                    "The fight given to '" + fishObjectName + "' is all " +
                    "pulling or all resting, so the fish could be hooked and never landed. It was " +
                    "left out and that fish fights the way the game's other fish do — give it at " +
                    "least one pulling turn and one resting turn, each longer than zero seconds.");
                return;
            }

            for (int i = 0; i < Pending.Count; i++)
            {
                if (string.Equals(Pending[i].FishObjectName, fishObjectName, StringComparison.Ordinal))
                {
                    // Same fish again is a replace, so a bootstrap read twice cannot leave one
                    // fish with two fights and no way to say which one wins.
                    Pending.RemoveAt(i);
                    break;
                }
            }

            Pending.Add(new PendingFight
            {
                FishObjectName = fishObjectName,
                Turns = new List<FightTurn>(turns)
            });
        }

        /// <summary>Clears queued fights (mod reload).</summary>
        public static void Clear()
        {
            Pending.Clear();
            AppliedFish.Clear();
        }

        /// <summary>True once at least one fish has been given a fight.</summary>
        public static bool HasAny { get { return Pending.Count > 0; } }

        internal static int PendingCount { get { return Pending.Count; } }

        /// <summary>The fish whose fights reached the table, by object name, in the order applied.</summary>
        internal static IReadOnlyList<string> Applied { get { return AppliedFish; } }

        /// <summary>
        /// Writes every queued fight into the table the converter is about to bake. Safe to run
        /// again on a table it has already written to.
        /// </summary>
        internal static void ApplyToFishingTable(
            FishingTable table,
            Func<string, ObjectID> resolveFish,
            Action<string> report)
        {
            AppliedFish.Clear();
            if (table == null || Pending.Count == 0)
            {
                return;
            }

            if (table.fishStruggleInfos == null)
            {
                table.fishStruggleInfos = new List<FishingTable.FishStruggleInfo>();
            }

            for (int i = 0; i < Pending.Count; i++)
            {
                PendingFight pending = Pending[i];
                ObjectID fish = resolveFish == null ? ObjectID.None : resolveFish(pending.FishObjectName);
                if (fish == ObjectID.None)
                {
                    if (report != null)
                    {
                        report(
                            "A fish fight was written for '" + pending.FishObjectName + "', which " +
                            "is not a thing this game has. It was left out — check the spelling " +
                            "against the item's own name, and make sure that item is switched on.");
                    }

                    continue;
                }

                FishingTable.FishStruggleInfo info = BuildStruggleInfo(fish, pending.Turns);

                int existing = -1;
                for (int j = 0; j < table.fishStruggleInfos.Count; j++)
                {
                    if (table.fishStruggleInfos[j].fishID == fish)
                    {
                        existing = j;
                        break;
                    }
                }

                if (existing >= 0)
                {
                    table.fishStruggleInfos[existing] = info;
                }
                else
                {
                    table.fishStruggleInfos.Add(info);
                }

                // The table keeps its own name-to-fight dictionary, built once at start-up and so
                // built before this ran. Nothing in the shipped game reads it — every fight lookup
                // in play goes through the baked blob — but leaving it disagreeing with the list
                // beside it would be a trap for whatever reads it next.
                if (table.fishStruggleInfosLookUp != null)
                {
                    table.fishStruggleInfosLookUp[fish] = info;
                }

                AppliedFish.Add(pending.FishObjectName);
            }
        }

        /// <summary>
        /// One fish's fight in the shape the table holds, including the difficulty number Core
        /// Keeper derives rather than authors.
        /// </summary>
        /// <remarks>
        /// <c>difficultyRatio</c> is computed the way <c>FishingTable.OnValidate</c> computes it —
        /// pulling share against resting share, signed by which of the two is larger. That method
        /// is editor-only, and the merge path that reads mod settings files does not carry the
        /// number at all, so in a modded game every fish's ratio is zero including Core Keeper's
        /// own. Nothing in the decompiled game reads it back (the only mentions are its
        /// declaration and this one assignment), so this is fidelity rather than behaviour — but a
        /// derived number left wrong on purpose is the kind of thing that bites two updates later.
        /// </remarks>
        internal static FishingTable.FishStruggleInfo BuildStruggleInfo(
            ObjectID fish,
            IReadOnlyList<FightTurn> turns)
        {
            List<FishingTable.FishStruggleData> data =
                new List<FishingTable.FishStruggleData>(turns.Count);
            float pulling = 0f;
            float resting = 0f;

            for (int i = 0; i < turns.Count; i++)
            {
                float seconds = turns[i].Seconds < 0f ? 0f : turns[i].Seconds;
                data.Add(new FishingTable.FishStruggleData
                {
                    isStruggling = turns[i].Pulls,
                    time = seconds
                });

                float share = seconds / turns.Count;
                if (turns[i].Pulls)
                {
                    pulling += share;
                }
                else
                {
                    resting += share;
                }
            }

            float ratio = 0f;
            if (pulling > 0f && resting > 0f)
            {
                ratio = pulling >= resting ? pulling / resting : -resting / pulling;
            }

            return new FishingTable.FishStruggleInfo
            {
                fishID = fish,
                struggleData = data,
                difficultyRatio = ratio
            };
        }

        /// <summary>A fight only resolves if the fish both pulls and rests for a real length of time.</summary>
        internal static bool HasBothKindsOfTurn(IReadOnlyList<FightTurn> turns)
        {
            bool pulls = false;
            bool rests = false;
            for (int i = 0; i < turns.Count; i++)
            {
                if (turns[i].Seconds <= 0f)
                {
                    continue;
                }

                if (turns[i].Pulls)
                {
                    pulls = true;
                }
                else
                {
                    rests = true;
                }
            }

            return pulls && rests;
        }

        /// <summary>Answers an object name, this mod's own included, once the mod is loaded.</summary>
        internal static ObjectID ResolveFishName(string name)
        {
            return ExpandNullforge.Foundation.DimensionObjectNames.Resolve(name);
        }
    }

    /// <summary>
    /// The one moment a fish this mod adds can be given a fight: the converter is about to read
    /// the fishing table out of memory and bake it, and everything a player fishes afterwards runs
    /// from the baked copy.
    /// </summary>
    /// <remarks>
    /// A prefix that edits the list rather than one that replaces the method, for the same reason
    /// as the upgrade-cost patch: the blob-building code stays Core Keeper's, so a change to it in
    /// a future update carries through instead of being silently replaced by a copy of the old one.
    /// There is no postfix to undo the edit, and there must not be — the entries have to survive
    /// into the blob, and the list they go into belongs to the mod loader rather than to the game.
    /// </remarks>
    [HarmonyPatch(typeof(FishingTableConverter), "Convert", new Type[] { typeof(FishingTableAuthoring) })]
    internal static class DimensionFishingTableConverterPatch
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
        private static void Prefix()
        {
            Fired++;

            if (!DimensionFishFightRegistry.HasAny)
            {
                return;
            }

            try
            {
                FishingTable table = Manager.mod == null ? null : Manager.mod.FishingTable;
                if (table == null)
                {
                    return;
                }

                DimensionFishFightRegistry.ApplyToFishingTable(
                    table,
                    DimensionFishFightRegistry.ResolveFishName,
                    Foundation.DimensionFrameworkLog.Warning);

                IReadOnlyList<string> applied = DimensionFishFightRegistry.Applied;
                if (applied.Count > 0)
                {
                    // Named rather than counted: a fight that silently did not arrive is the whole
                    // failure mode here, and a list of the fish that did arrive is the only thing
                    // that tells them apart in a player's log.
                    Foundation.DimensionFrameworkLog.Info(
                        "Gave " + applied.Count + " fish a fight of their own: " +
                        string.Join(", ", applied) + ".");
                }
            }
            catch (Exception exception)
            {
                // A mod's fish fights are never worth losing the game's fishing table over. The
                // fish stays catchable; it just fights the way the table's default fish does.
                DimensionLog.Fatal(DimensionLogChannels.WorldRule, null, 
                    "Could not give this mod's fish their own fights, so they " +
                    "fight the way the game's default fish does: " + exception);
            }
        }
    }
}
