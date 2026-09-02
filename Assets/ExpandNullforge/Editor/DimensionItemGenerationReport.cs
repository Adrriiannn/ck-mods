using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a generation run did and what it could not do. Nothing is reported as generated
    /// unless the prefab was actually written, and any value the framework could not apply is
    /// surfaced as a warning rather than being dropped silently.
    /// </summary>
    internal sealed class DimensionItemGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();

        /// <summary>
        /// Prefabs deleted because the item that produced them no longer exists. Its own list
        /// because a deletion is neither a failure nor a routine write — it is the one outcome a
        /// modder should be able to scan for and object to.
        /// </summary>
        public readonly List<string> Removed = new List<string>();

        public readonly List<string> Skipped = new List<string>();

        /// <summary>
        /// Answers the generator worked out for an item that did not give one, and what it settled
        /// on: what the item is, the time between uses when none was set, and whether several of it
        /// share a slot when what it is settles that on its own.
        /// </summary>
        /// <remarks>
        /// Its own list for the same reason <see cref="Removed"/> has one: it is neither a failure
        /// nor a routine write. An item that never answered "what is this" gets an answer chosen for
        /// it, and that answer decides which slot it lands in and how much use it takes before it
        /// breaks — so it has to be readable somewhere rather than happening quietly. Warnings are
        /// the wrong home: on a project of two hundred items every one of them would be a warning,
        /// and a list nobody can finish reading is a list nobody reads. The item card's own
        /// validator already warns where a warning is the right shape.
        /// </remarks>
        public readonly List<string> Derived = new List<string>();

        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>
        /// Every localization key this run wrote, so the coverage check can ask the table itself
        /// what is named rather than re-deriving it and drifting from what was written.
        /// </summary>
        public readonly HashSet<string> LocalizationKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public int GeneratedCount => Created.Count + Updated.Count;

        public bool HasProblems => Warnings.Count > 0 || Errors.Count > 0;

        public string Summarize()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(Created.Count).Append(" created, ")
                .Append(Updated.Count).Append(" updated, ")
                .Append(Skipped.Count).Append(" skipped");
            if (Derived.Count > 0)
            {
                builder.Append(", ").Append(Derived.Count)
                    .Append(" had answers filled in");
            }

            if (Warnings.Count > 0)
            {
                builder.Append(", ").Append(Warnings.Count).Append(" warning(s)");
            }

            if (Errors.Count > 0)
            {
                builder.Append(", ").Append(Errors.Count).Append(" error(s)");
            }

            return builder.ToString();
        }
    }
}
