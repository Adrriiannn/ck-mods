using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What sort of thing drops an item.
    /// </summary>
    /// <remarks>
    /// Measured from how Core Keeper actually delivers loot. There are only two mechanisms underneath
    /// — a shared weighted <c>LootTable</c> that many things draw from (313 prefabs), and
    /// <c>CustomLoot</c> that belongs to one object (180) — but a person does not think in mechanisms.
    /// They think "this drops off slimes", "this is in desert chests", "this comes out of the rubble".
    /// These are those thoughts; the generator picks the mechanism.
    /// </remarks>
    public enum DimensionDropSourceKind
    {
        /// <summary>A creature drops it when killed.</summary>
        Creature = 0,

        /// <summary>It is found inside a container that spawns in the world.</summary>
        Container = 1,

        /// <summary>Breaking something in the world yields it — rubble, a pot, a vein.</summary>
        Destructible = 2,

        /// <summary>It is placed by hand into a particular scene or dungeon room.</summary>
        Scene = 3
    }

    /// <summary>
    /// One place an item drops from, said from the item's side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE INVERSION, AND WHY IT IS WORTH THE WORK. Core Keeper stores loot the other way round: every
    /// creature, chest and breakable carries its own list of what it gives. Authored that way, adding
    /// one new item to a mod means opening nine unrelated assets and adding a line to each — and
    /// answering "what drops my Ember Shard?" means reading all nine back.
    /// </para>
    /// <para>
    /// A person designing an item thinks about it from the item's side: this is rare, it comes off
    /// desert creatures and out of desert chests, roughly one in twenty. So that is what is authored
    /// here, and the generator inverts it — collecting every item that named a given source and
    /// writing that source's loot in one pass. The game gets exactly the shape it wants; the author
    /// never has to hold it that way round.
    /// </para>
    /// <para>
    /// <see cref="OnlyInBiomeId"/> is not an invention either: <c>LootInfo.onlyDropsInBiome</c> is a
    /// real field on Core Keeper's own loot entries, so "only in the desert" is a constraint the game
    /// already understands.
    /// </para>
    /// <para>
    /// WHAT <see cref="Chance"/> MEANS, EXACTLY. Core Keeper's loot tables hold no chance per row —
    /// they hold a count of how many things the table hands out and a weight each. So the framework
    /// works the chance back into a weight and a count when it builds a source's own table
    /// (<c>DimensionPortalDropRegistry.ShapeAMintedTable</c>), including the row of nothing that
    /// lets the table come up empty, which is how the game's own tables express a rare drop — 74 of
    /// its 176 carry one. Two things it cannot cover, and the generator says both when they happen:
    /// a drop going into a table this mod did not make can only compete on <see cref="Weight"/>,
    /// and a BOSS's table is rolled more times with more players in the world or in hard mode
    /// (<c>DropLootSystem</c>), so a chance off a boss rises with the party exactly as the game's
    /// own boss drops do.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionDropSource
    {
        [Tooltip("What sort of thing drops it.")]
        [SerializeField] private DimensionDropSourceKind kind = DimensionDropSourceKind.Creature;

        [Tooltip("Which one. A creature, container or object id — vanilla or your own.")]
        [SerializeField] private string sourceId = string.Empty;

        [Tooltip("How likely it is to drop at all, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 1f;

        [Tooltip("Its share when the drop goes into a loot table this mod did not make — one of the game's, or one you wrote rows into. Higher means more often. Ignored otherwise.")]
        [Min(1)]
        [SerializeField] private int weight = 1;

        [Tooltip("Fewest that drop when it does.")]
        [Min(0)]
        [SerializeField] private int minAmount = 1;

        [Tooltip("Most that drop when it does.")]
        [Min(0)]
        [SerializeField] private int maxAmount = 1;

        [Tooltip("It always drops rather than taking its chance. A loot table hands out one always-drop per kill, so only mark one thing per source this way.")]
        [SerializeField] private bool alwaysDrops;

        [Tooltip("Only drops in this biome. Leave empty for anywhere. This is Core Keeper's own constraint.")]
        [SerializeField] private string onlyInBiomeId = string.Empty;

        [SerializeField] private bool enabled = true;

        public DimensionDropSourceKind Kind
        {
            get { return kind; }
        }

        public string SourceId
        {
            get { return sourceId ?? string.Empty; }
        }

        public float Chance
        {
            get
            {
                if (chance < 0f)
                {
                    return 0f;
                }

                return chance > 1f ? 1f : chance;
            }
        }

        public int Weight
        {
            get { return weight < 1 ? 1 : weight; }
        }

        public int MinAmount
        {
            get { return minAmount < 0 ? 0 : minAmount; }
        }

        /// <summary>Most that drop, never fewer than the minimum.</summary>
        public int MaxAmount
        {
            get { return maxAmount < MinAmount ? MinAmount : maxAmount; }
        }

        public bool AlwaysDrops
        {
            get { return alwaysDrops; }
        }

        public string OnlyInBiomeId
        {
            get { return onlyInBiomeId ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>Whether this entry names a source at all.</summary>
        public bool NamesASource
        {
            get { return !string.IsNullOrEmpty(SourceId); }
        }

        /// <summary>Whether it is set up to drop nothing.</summary>
        /// <remarks>
        /// Either it never rolls, or it rolls and yields zero. Both look like a configured drop and
        /// give the player nothing.
        /// </remarks>
        public bool DropsNothing
        {
            get { return Chance <= 0f || MaxAmount == 0; }
        }
    }

    /// <summary>
    /// One source, with every item that named it.
    /// </summary>
    /// <remarks>
    /// The result of the inversion — what the generator needs to write a single creature's or
    /// container's loot in one pass.
    /// </remarks>
    public sealed class DimensionDropsForSource
    {
        public DimensionDropsForSource(DimensionDropSourceKind kind, string sourceId)
        {
            Kind = kind;
            SourceId = sourceId ?? string.Empty;
            Drops = new List<DimensionResolvedDrop>();
        }

        public DimensionDropSourceKind Kind { get; private set; }

        public string SourceId { get; private set; }

        public List<DimensionResolvedDrop> Drops { get; private set; }
    }

    /// <summary>One item dropping from one source, after the inversion.</summary>
    public sealed class DimensionResolvedDrop
    {
        public DimensionResolvedDrop(string itemId, DimensionDropSource source)
        {
            ItemId = itemId ?? string.Empty;
            Source = source;
        }

        public string ItemId { get; private set; }

        public DimensionDropSource Source { get; private set; }
    }

    /// <summary>
    /// Turns "this item drops from these places" into "this place drops these items".
    /// </summary>
    /// <remarks>
    /// The whole of the inversion, kept apart from any generator so it can be tested on its own and
    /// reused by every generator that needs to write loot.
    /// </remarks>
    public static class DimensionDropCollector
    {
        /// <summary>
        /// Groups drops by the source that produces them.
        /// </summary>
        /// <remarks>
        /// Sources are matched on kind AND id together, because a creature and a container may
        /// legitimately share a name and must not have their loot merged.
        /// </remarks>
        public static List<DimensionDropsForSource> GroupBySource(
            IEnumerable<KeyValuePair<string, DimensionDropSource[]>> itemsAndTheirSources)
        {
            List<DimensionDropsForSource> grouped = new List<DimensionDropsForSource>();
            if (itemsAndTheirSources == null)
            {
                return grouped;
            }

            Dictionary<string, DimensionDropsForSource> byKey =
                new Dictionary<string, DimensionDropsForSource>();

            foreach (KeyValuePair<string, DimensionDropSource[]> pair in itemsAndTheirSources)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                for (int i = 0; i < pair.Value.Length; i++)
                {
                    DimensionDropSource source = pair.Value[i];
                    if (source == null || !source.Enabled || !source.NamesASource)
                    {
                        continue;
                    }

                    string key = ((int)source.Kind).ToString() + "|" + source.SourceId;

                    DimensionDropsForSource bucket;
                    if (!byKey.TryGetValue(key, out bucket))
                    {
                        bucket = new DimensionDropsForSource(source.Kind, source.SourceId);
                        byKey.Add(key, bucket);
                        grouped.Add(bucket);
                    }

                    bucket.Drops.Add(new DimensionResolvedDrop(pair.Key, source));
                }
            }

            return grouped;
        }
    }
}
