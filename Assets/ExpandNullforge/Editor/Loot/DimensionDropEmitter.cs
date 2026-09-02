using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Writes a source's collected drops onto the prefab that produces them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of the drop-location inversion: <c>DimensionDropCollector</c> turns "this item
    /// drops from these places" into "this place drops these items", and this puts that result onto
    /// the place.
    /// </para>
    /// <para>
    /// <b>THE ROUTING RULE, AND WHY IT MATTERS.</b> Core Keeper has two ways to carry loot and they
    /// are not equivalent. <c>CustomLoot</c> belongs to one object and its <c>LootDrop</c> entries
    /// have a single <c>amount</c> — <b>no range</b>. A shared <c>LootTable</c>'s <c>LootInfo</c>
    /// entries have a <c>RangeInt amount</c>, a <c>weight</c>, and <c>onlyDropsInBiome</c>. So a drop
    /// that wants "2 to 5 of these", or that only happens in one biome, or that competes by weight
    /// with other entries, <b>cannot</b> be expressed as custom loot. Writing it there anyway would
    /// silently flatten the range to a single number and discard the biome — the drop would still
    /// happen, just never the way it was authored.
    /// </para>
    /// <para>
    /// So each drop is routed rather than assumed, and anything needing a table says so instead of
    /// being quietly downgraded.
    /// </para>
    /// </remarks>
    internal static class DimensionDropEmitter
    {
        /// <summary>
        /// Whether a drop can be carried by per-object custom loot at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Custom loot can say "this object gives 3 of these, 40% of the time". It cannot say a range,
        /// cannot restrict to a biome, and has no weight to compete with.
        /// </para>
        /// <para>
        /// AND IT CANNOT NAME A MOD'S OWN ITEM. <c>DropsLootBuffer.lootDropID</c> is an
        /// <c>ObjectID</c> baked onto the prefab, and a mod's ids do not exist while the prefab is
        /// being written, so <see cref="ApplyCustomLoot"/> can only skip such an entry. Routing one
        /// here anyway loses it entirely: the bootstrap emitter skips it because "it is already
        /// written onto the prefab" and the prefab write skips it because the name resolves to
        /// nothing, so the drop is written NOWHERE. Failing the fit here is what sends it down the
        /// runtime road instead, which resolves names at load and already works.
        /// </para>
        /// </remarks>
        public static bool FitsInCustomLoot(DimensionDropSource source, string itemId)
        {
            if (source == null)
            {
                return false;
            }

            return source.MinAmount == source.MaxAmount
                && string.IsNullOrEmpty(source.OnlyInBiomeId)
                && !DimensionObjectBinder.CouldOnlyResolveAtRuntime(itemId);
        }

        /// <summary>
        /// The one chance an object's custom loot can carry for everything on it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Custom loot has ONE chance for the whole list — <c>ChanceToDropLootCD</c> is a single
        /// component on the object and <c>DropLootSystem</c> rolls every row of the buffer against
        /// it. So a list holding a 90% drop and a 10% drop cannot be right for both.
        /// </para>
        /// <para>
        /// Taking the highest and writing it for all of them makes a one-in-ten item drop nine
        /// times in ten with nothing said. So the object carries the chance the most drops asked
        /// for — highest wins a tie — and every drop that asked for a different one is sent to the
        /// source's loot table instead, where a chance of its own can be made real.
        /// </para>
        /// </remarks>
        public static float TheOneChanceCustomLootCanCarry(DimensionDropsForSource source)
        {
            if (source == null || source.Drops == null || source.Drops.Count == 0)
            {
                return 1f;
            }

            float best = 1f;
            int bestCount = 0;
            for (int i = 0; i < source.Drops.Count; i++)
            {
                DimensionDropSource candidate = source.Drops[i].Source;
                if (candidate == null || candidate.DropsNothing ||
                    !FitsInCustomLoot(candidate, source.Drops[i].ItemId))
                {
                    continue;
                }

                int count = 0;
                for (int j = 0; j < source.Drops.Count; j++)
                {
                    DimensionDropSource other = source.Drops[j].Source;
                    if (other == null || other.DropsNothing ||
                        !FitsInCustomLoot(other, source.Drops[j].ItemId))
                    {
                        continue;
                    }

                    if (Mathf.Approximately(other.Chance, candidate.Chance))
                    {
                        count++;
                    }
                }

                if (count > bestCount ||
                    (count == bestCount && candidate.Chance > best))
                {
                    best = candidate.Chance;
                    bestCount = count;
                }
            }

            return bestCount == 0 ? 1f : best;
        }

        /// <summary>
        /// Whether this drop is written onto the object itself rather than registered into a table.
        /// </summary>
        /// <remarks>
        /// ONE ANSWER FOR BOTH HALVES. The prefab writer and the bootstrap emitter each decide what
        /// they are responsible for, and the moment those two answers differ a drop is either
        /// written twice or written nowhere. So they ask this, and only this.
        /// </remarks>
        public static bool GoesOnTheObjectItself(
            DimensionDropsForSource source,
            DimensionResolvedDrop drop)
        {
            if (source == null || drop == null || drop.Source == null)
            {
                return false;
            }

            return FitsInCustomLoot(drop.Source, drop.ItemId)
                && Mathf.Approximately(
                    drop.Source.Chance,
                    TheOneChanceCustomLootCanCarry(source));
        }

        /// <summary>
        /// Gives a source somewhere to hang the drops that custom loot cannot carry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE HOLE THIS FILLS, AND IT WAS THE WHOLE POINT. A drop that needs a loot table — a range,
        /// a biome, or an item of this mod's own, whose number does not exist while the prefab is
        /// being written — is registered against the SOURCE's loot table at load. Core Keeper finds
        /// that table through <c>DropsLootFromLootTableCD</c>, and <c>DropLootConverter</c> writes
        /// that component only when the object says it has a loot table. A creature authored the
        /// ordinary way — no loot table asset, just an item saying "I drop from Grubling" — said no,
        /// so there was nothing to add the drop to and the item dropped from nothing, ever, with one
        /// line in the game log that read like the creator had mistyped something.
        /// </para>
        /// <para>
        /// So the source is given a table of its own here: an id minted from its full name by the
        /// same pure arithmetic both sides use, stamped onto the prefab now. Nothing has to exist
        /// under that id yet — <c>DimensionPortalDropRegistry</c> creates the table the first time it
        /// finds a source pointing at one that is not there. An authored loot table always wins;
        /// this only fills in for a source that has none.
        /// </para>
        /// </remarks>
        public static void EnsureALootTableToHangDropsOn(
            GameObject root,
            string qualifiedSourceName,
            Action<string> report = null)
        {
            if (root == null || string.IsNullOrEmpty(qualifiedSourceName))
            {
                return;
            }

            // The component may genuinely not be there: when EVERY collected drop needs a table,
            // ApplyCustomLoot writes no custom loot and adds nothing — which is the exact case an
            // item of this mod's own falls into, and the exact case that dropped nothing.
            DropLootAuthoring loot = root.GetComponent<DropLootAuthoring>();
            if (loot == null)
            {
                loot = root.AddComponent<DropLootAuthoring>();
            }

            if (loot.hasLootTable)
            {
                // THE FLAG IS ONLY EVER TRUE BECAUSE THIS GENERATE SET IT. Generation reloads the
                // prefab it wrote last time, so nothing but the take-back sets hasLootTable back
                // to false. Without it, a creature given the game's AncientChest table once keeps
                // it after the field is cleared, and every drop authored against that creature is
                // appended to the game's shared ancient-chest table.
                //
                // All three generators that reach here call ClearAnyLootTheLastGenerateWrote
                // above any decision they make — creatures at the top of ApplyLoot, chests before
                // the guard on their drop list, world objects before the early return in
                // ApplyCollectedDrops. It is checked by a test rather than believed: the claim used
                // to be written here as an invariant while ONE of the three did it.
                //
                // Sharing a game table is still a legitimate thing to author, so it is said out
                // loud rather than quietly redirected.
                if (report != null &&
                    loot.lootTableID != LootTableID.Empty &&
                    (int)loot.lootTableID < ExpandNullforge.Loot.DimensionLootTableRegistry
                        .MinCustomLootTableId)
                {
                    report(
                        "'" + qualifiedSourceName + "' uses the game's own '" + loot.lootTableID +
                        "' loot table, so anything set to drop from it is added to that table — " +
                        "which means everything else in the game that draws from that table drops " +
                        "it too. Clear its loot table if you want the drop to be this one's alone.");
                }

                return;
            }

            loot.hasLootTable = true;
            loot.lootTableID = LootTableIdFor(
                ExpandNullforge.Loot.DimensionLootTableRegistry.AutoTableNameFor(
                    qualifiedSourceName));
        }

        /// <summary>
        /// Takes back everything the last generate's drops wrote, before this one decides anything.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ALL THREE GENERATORS THAT WRITE DROPS HAVE TO CALL THIS, and the invariant only holds
        /// while they all do. Each of them reloads the prefab it wrote last time, and each skips
        /// its whole loot pass when the drop list is empty. A generator that misses the take-back
        /// leaves last time's drops baked onto a chest the author has since emptied, and a renamed
        /// mod leaves the prefab pointing at a table minted from the OLD name while the runtime
        /// fills one minted from the new: table drops stop, and the report says the object
        /// generated fine.
        /// </para>
        /// <para>
        /// Both halves the drop path owns are taken back here — the loot table it stamps and the
        /// custom loot it writes — and nothing else on the component is touched, because shedding,
        /// on-use loot and seasonal loot belong to a different pass.
        /// </para>
        /// <para>
        /// It never adds the component. An object with no <c>DropLootAuthoring</c> has nothing of
        /// the last generate's to take back.
        /// </para>
        /// </remarks>
        public static void ClearAnyLootTheLastGenerateWrote(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            DropLootAuthoring loot = root.GetComponent<DropLootAuthoring>();
            if (loot == null)
            {
                return;
            }

            loot.hasLootTable = false;
            loot.lootTableID = LootTableID.Empty;
            loot.hasCustomLoot = false;
            if (loot.customLoot != null)
            {
                loot.customLoot.Values = new List<LootDrop>();
                loot.customLoot.chance = 0f;
            }
        }

        /// <summary>
        /// The id a loot-table name carries: the game's own when it names one, ours otherwise.
        /// </summary>
        /// <remarks>
        /// The one place the two roads meet, so the prefab a generator stamps and the row the
        /// bootstrap emits can never mint different numbers from the same name. Vanilla-first for
        /// the reason <c>DimensionLootTableRegistry.TryResolve</c> gives: "AncientChest" means the
        /// game's table, and minting an id for it would point at a table nothing carries.
        /// </remarks>
        public static LootTableID LootTableIdFor(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return LootTableID.Empty;
            }

            LootTableID vanilla;
            if (Enum.TryParse(tableName, false, out vanilla) && vanilla != LootTableID.Empty)
            {
                return vanilla;
            }

            return (LootTableID)ExpandNullforge.Loot.DimensionLootTableRegistry
                .ComputeLootTableId(tableName);
        }

        /// <summary>
        /// The name an authored loot table carries, or null when it names nothing usable.
        /// </summary>
        /// <remarks>
        /// A table asset naming one of the game's tables IS that table. A table of the mod's own
        /// only becomes real once it has rows — an empty one registers nothing at load, so pointing
        /// a creature at it would leave the creature hanging on a table that never exists.
        /// </remarks>
        public static string AuthoredLootTableNameOf(DimensionLootTableAsset authored)
        {
            if (authored == null || string.IsNullOrEmpty(authored.LootTableId))
            {
                return null;
            }

            LootTableID vanilla;
            if (Enum.TryParse(authored.LootTableId, false, out vanilla) &&
                vanilla != LootTableID.Empty)
            {
                return authored.LootTableId;
            }

            return authored.EnabledEntryCount > 0 ? authored.LootTableId : null;
        }

        /// <summary>
        /// The name of the loot table a drop registered against this source will land in.
        /// </summary>
        /// <remarks>
        /// ONE ANSWER, TWO CALLERS. The generator stamps this table's id onto the prefab and the
        /// bootstrap emitter ships the same name in the drop row, so the load-time injection never
        /// has to read the id back off a prefab entity. It cannot: that read is a database lookup,
        /// and the database cannot answer while it is being converted.
        /// </remarks>
        public static string LootTableNameFor(
            DimensionLootTableAsset authored,
            string qualifiedSourceName)
        {
            string authoredName = AuthoredLootTableNameOf(authored);
            return authoredName ?? ExpandNullforge.Loot.DimensionLootTableRegistry
                .AutoTableNameFor(qualifiedSourceName);
        }

        /// <summary>
        /// Writes the drops that fit as custom loot, and reports the ones that do not.
        /// </summary>
        /// <remarks>
        /// Returns the drops it could not carry, so the caller can put them in a loot table rather
        /// than lose them. Returning them — instead of warning and moving on — is what stops a range
        /// or a biome restriction disappearing without a trace.
        /// </remarks>
        public static List<DimensionResolvedDrop> ApplyCustomLoot(
            GameObject root,
            DimensionDropsForSource source,
            Func<string, ObjectID> resolveItem,
            Action<string> report,
            Func<string, bool> isDeferred = null,
            bool theTableIsMintedForThisSourceAlone = true)
        {
            List<DimensionResolvedDrop> needsATable = new List<DimensionResolvedDrop>();
            if (root == null || source == null || source.Drops.Count == 0)
            {
                return needsATable;
            }

            List<LootDrop> carried = new List<LootDrop>();
            float chance = 0f;

            for (int i = 0; i < source.Drops.Count; i++)
            {
                DimensionResolvedDrop drop = source.Drops[i];
                DimensionDropSource settings = drop.Source;

                if (settings.DropsNothing)
                {
                    if (report != null)
                    {
                        report(
                            "'" + drop.ItemId + "' is listed as dropping from '" + source.SourceId +
                            "' but is set never to drop, so it will never appear.");
                    }

                    continue;
                }

                // THE MISSPELLING IS CAUGHT BEFORE THE ROUTING, and it has to be. Once a name that
                // is not one of the game's fails the custom-loot fit, it goes down the loot-table
                // road — where a misspelling generates clean and only turns up as a line in the
                // game log hours later. This is the one moment both halves are known at once.
                ObjectID resolved = resolveItem == null ? ObjectID.None : resolveItem(drop.ItemId);
                if (resolved == ObjectID.None &&
                    !(isDeferred != null && isDeferred(drop.ItemId)))
                {
                    if (report != null)
                    {
                        report(
                            "'" + drop.ItemId + "' is set to drop from '" + source.SourceId +
                            "', but it is neither one of this mod's items nor one the game has, " +
                            "so nothing will drop for it.");
                    }

                    continue;
                }

                if (!GoesOnTheObjectItself(source, drop))
                {
                    needsATable.Add(drop);
                    continue;
                }

                ObjectID id = resolved;

                carried.Add(new LootDrop
                {
                    lootDropID = id,
                    amount = settings.MaxAmount
                });

                // CustomLoot carries ONE chance for the whole list, not one per entry, and
                // GoesOnTheObjectItself only lets through the drops that asked for this one. So
                // reading it off any of them gives the same answer, and no drop is written under a
                // chance it did not ask for.
                chance = settings.Chance;
            }

            if (carried.Count == 0)
            {
                DropLootAuthoring existing = root.GetComponent<DropLootAuthoring>();
                if (existing != null)
                {
                    existing.hasCustomLoot = false;
                }

                ReportWhatNeedsATable(source, needsATable, report, theTableIsMintedForThisSourceAlone);
                return needsATable;
            }

            DropLootAuthoring loot = root.GetComponent<DropLootAuthoring>();
            if (loot == null)
            {
                loot = root.AddComponent<DropLootAuthoring>();
            }

            loot.hasCustomLoot = true;
            if (loot.customLoot == null)
            {
                loot.customLoot = new CustomLoot();
            }

            loot.customLoot.chance = chance;
            loot.customLoot.Values = carried;

            ReportWhatNeedsATable(source, needsATable, report, theTableIsMintedForThisSourceAlone);
            return needsATable;
        }

        /// <summary>
        /// Says which drops go in a loot table rather than onto the object, and why.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The reason is not "an amount range or a biome restriction". A drop of this mod's own
        /// item comes down here as well and asks for neither, and a reason that does not apply is
        /// worse than none: it sends somebody looking for a range they never typed.
        /// </para>
        /// <para>
        /// Nor does it end "so nothing is lost", which is not true of two always-drops entries
        /// on one table. A loot roll walks the guaranteed pool for the FIRST entry above a single
        /// throw and stops there (<c>PugDatabase.GetRandomLoot</c>), so a table hands out one
        /// guaranteed item per kill however many are in it. That is the game's shape, not something
        /// this framework can arrange around, so it is said instead of hidden.
        /// </para>
        /// <para>
        /// And it says what the chances turned into. A loot table has no chance per row — it has a
        /// number of picks and a weight each — so the registry works out how many picks the authored
        /// chances need and sets every weight from there
        /// (<c>DimensionPortalDropRegistry.ShapeAMintedTable</c>). One pick carries chances adding
        /// up to a whole; more than that needs more picks, and more picks mean the same item can
        /// come out twice. That is a thing a creator would want to know before it happens in front
        /// of a player.
        /// </para>
        /// </remarks>
        private static void ReportWhatNeedsATable(
            DimensionDropsForSource source,
            List<DimensionResolvedDrop> needsATable,
            Action<string> report,
            bool theTableIsMintedForThisSourceAlone)
        {
            if (report == null || needsATable == null || needsATable.Count == 0)
            {
                return;
            }

            report(
                "'" + source.SourceId + "' has " + needsATable.Count + " drop(s) that go in a loot " +
                "table rather than onto the object itself — an amount range, a biome, or one of " +
                "this mod's own items, none of which fit on the object. They are added to its " +
                "table when the game loads.");

            int alwaysDrops = 0;
            int alwaysDropsTheGameHas = 0;
            int weightsThatDoNothing = 0;
            string alwaysDropOfSeveral = null;
            List<float> chances = new List<float>();
            for (int i = 0; i < needsATable.Count; i++)
            {
                DimensionDropSource settings = needsATable[i].Source;
                if (settings == null)
                {
                    continue;
                }

                if (settings.AlwaysDrops || settings.Chance >= 1f)
                {
                    alwaysDrops++;
                    if (!DimensionObjectBinder.CouldOnlyResolveAtRuntime(needsATable[i].ItemId))
                    {
                        alwaysDropsTheGameHas++;
                    }

                    if (settings.MaxAmount > 1 && alwaysDropOfSeveral == null)
                    {
                        alwaysDropOfSeveral = needsATable[i].ItemId;
                    }

                    continue;
                }

                chances.Add(settings.Chance);
                if (settings.Weight != 1)
                {
                    weightsThatDoNothing++;
                }
            }

            if (alwaysDrops > 1)
            {
                string howToFix = alwaysDropsTheGameHas > 1
                    ? " Give the ones that should come every time a fixed amount and no biome, and " +
                      "they go on the object itself instead."
                    : " There is no way round it for items of this mod's own — they can only travel " +
                      "in a table — so pick one to always drop and give the rest a chance under 1.";

                report(
                    alwaysDrops + " of those are set to always drop from '" + source.SourceId +
                    "'. A loot table gives out one always-drop per kill, so they will take turns " +
                    "rather than all appear together." + howToFix);
            }

            if (!theTableIsMintedForThisSourceAlone)
            {
                if (chances.Count > 0)
                {
                    report(
                        chances.Count + " drop(s) from '" + source.SourceId + "' set a chance under " +
                        "1, but they go into a loot table that is not this mod's to shape — so the " +
                        "chance cannot be honoured and the share decides how often they come up " +
                        "against everything else already in that table. Clear the loot table if you " +
                        "want the chances to mean what they say.");
                }

                return;
            }

            if (chances.Count > 0)
            {
                int rolls = ExpandNullforge.Portals.DimensionPortalDropRegistry
                    .RollsNeededForChances(chances);
                if (rolls > 1)
                {
                    report(
                        "The drop chances on '" + source.SourceId + "' add up to more than one " +
                        "whole drop, so its table is rolled " + rolls + " times a kill to make " +
                        "each of them come out as often as you asked. Each one still drops as " +
                        "often as its number says; the cost is that the same item can come out of " +
                        "two of those rolls at once. Lower them until they add up to 1 if you want " +
                        "at most one thing per kill.");
                }
            }

            if (weightsThatDoNothing > 0)
            {
                report(
                    weightsThatDoNothing + " drop(s) from '" + source.SourceId + "' set a share as " +
                    "well as a chance. This mod makes the table, so nothing else is competing for " +
                    "the pick and the chance decides it on its own — the share is only read when " +
                    "the drop goes into a table the game already owns, or one you wrote rows into.");
            }

            // THE ONE THING THE PICK COUNT CANNOT BE MADE RIGHT FOR. An always-drop is filled first
            // and the rest of the picks are what is left. For an item that does not stack, the game
            // reads an amount range as a NUMBER OF COPIES and each copy takes a pick — so several
            // copies of an always-drop can eat every pick the chance drops were given, and how many
            // it eats depends on whether that item stacks, which is not something this pass can
            // know for one of the mod's own items.
            if (alwaysDropOfSeveral != null && chances.Count > 0)
            {
                report(
                    "'" + source.SourceId + "' always drops several of '" + alwaysDropOfSeveral +
                    "' and also has drops with a chance. If that item does not stack, each copy " +
                    "takes one of the picks the kill has, and the chance drops can be crowded out " +
                    "of the ones that are left. Drop one of it, or move the chance drops to a " +
                    "different source, if they matter.");
            }
        }
    }
}
