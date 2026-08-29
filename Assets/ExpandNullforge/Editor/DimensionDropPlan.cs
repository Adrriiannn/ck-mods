using System.Collections.Generic;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Reads every item's "where I drop from" and works out what each source drops.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bridge between authoring and generation. Items are authored one at a time, each naming the
    /// places it comes from; generators need the opposite view, one source at a time. This walks the
    /// template once, inverts, and hands out buckets.
    /// </para>
    /// <para>
    /// Built as a plan rather than looked up per generator so the inversion happens ONCE per generate.
    /// Doing it per creature would re-walk every item in the mod for every creature in it, and — worse
    /// — would make it easy for two generators to disagree about what a source drops.
    /// </para>
    /// </remarks>
    internal sealed class DimensionDropPlan
    {
        private readonly Dictionary<string, DimensionDropsForSource> byKey =
            new Dictionary<string, DimensionDropsForSource>();

        private DimensionDropPlan()
        {
        }

        /// <summary>Every source that something drops from, in the order first encountered.</summary>
        public List<DimensionDropsForSource> Sources { get; private set; }

        /// <summary>
        /// Builds the plan from everything in a template that can name a drop source.
        /// </summary>
        /// <remarks>
        /// Items are the main case; world objects can drop too, and both carry the same
        /// <c>dropsFrom</c> list. A null template is not an error — it simply means nothing drops.
        /// </remarks>
        public static DimensionDropPlan Build(
            IEnumerable<DimensionItemAsset> items,
            IEnumerable<DimensionWorldObjectAsset> worldObjects)
        {
            List<KeyValuePair<string, DimensionDropSource[]>> authored =
                new List<KeyValuePair<string, DimensionDropSource[]>>();

            if (items != null)
            {
                foreach (DimensionItemAsset item in items)
                {
                    if (item == null || !item.Enabled)
                    {
                        continue;
                    }

                    DimensionDropSource[] sources = item.DropsFrom;
                    if (sources.Length > 0)
                    {
                        authored.Add(new KeyValuePair<string, DimensionDropSource[]>(
                            item.ItemId,
                            sources));
                    }
                }
            }

            if (worldObjects != null)
            {
                foreach (DimensionWorldObjectAsset worldObject in worldObjects)
                {
                    if (worldObject == null || !worldObject.Enabled)
                    {
                        continue;
                    }

                    DimensionDropSource[] sources = worldObject.DropsFrom;
                    if (sources.Length > 0)
                    {
                        authored.Add(new KeyValuePair<string, DimensionDropSource[]>(
                            worldObject.ObjectIdentifier,
                            sources));
                    }
                }
            }

            DimensionDropPlan plan = new DimensionDropPlan();
            plan.Sources = DimensionDropCollector.GroupBySource(authored);

            for (int i = 0; i < plan.Sources.Count; i++)
            {
                DimensionDropsForSource bucket = plan.Sources[i];
                plan.byKey[KeyFor(bucket.Kind, bucket.SourceId)] = bucket;
            }

            return plan;
        }

        /// <summary>
        /// What a particular source drops, or null when nothing named it.
        /// </summary>
        /// <remarks>
        /// Null rather than an empty bucket, so a generator can tell "nothing drops from this" from
        /// "something does" without inspecting a count — and so the common case costs no allocation.
        /// </remarks>
        public DimensionDropsForSource For(DimensionDropSourceKind kind, string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                return null;
            }

            DimensionDropsForSource bucket;
            return byKey.TryGetValue(KeyFor(kind, sourceId), out bucket) ? bucket : null;
        }

        /// <summary>Whether anything in the mod drops from anywhere.</summary>
        public bool IsEmpty
        {
            get { return Sources.Count == 0; }
        }

        /// <summary>
        /// Sources nothing in this mod defines, which are almost always a typo.
        /// </summary>
        /// <remarks>
        /// A drop naming a source that never generates is silent: no creature carries it, no error
        /// appears, and the item simply never turns up. Comparing against the ids the mod actually
        /// produced is the only way to catch it before someone plays the mod looking for the item.
        /// </remarks>
        public List<string> SourcesNothingDefines(HashSet<string> knownSourceIds)
        {
            List<string> unknown = new List<string>();
            if (knownSourceIds == null)
            {
                return unknown;
            }

            for (int i = 0; i < Sources.Count; i++)
            {
                string id = Sources[i].SourceId;
                if (!knownSourceIds.Contains(id) && !unknown.Contains(id))
                {
                    unknown.Add(id);
                }
            }

            return unknown;
        }

        private static string KeyFor(DimensionDropSourceKind kind, string sourceId)
        {
            return ((int)kind).ToString() + "|" + sourceId;
        }
    }
}
