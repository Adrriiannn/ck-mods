using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating world objects did.</summary>
    internal sealed class DimensionWorldObjectGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>Drops needing a loot-table registration rather than per-object custom loot.</summary>
        public readonly List<DimensionResolvedDrop> DropsNeedingALootTable =
            new List<DimensionResolvedDrop>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "World objects: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }
}
