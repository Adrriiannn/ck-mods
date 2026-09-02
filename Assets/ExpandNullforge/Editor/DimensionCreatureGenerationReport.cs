using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating creatures did.</summary>
    internal sealed class DimensionCreatureGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>
        /// Art files this run threw away because nothing in the project describes them any more.
        /// </summary>
        /// <remarks>
        /// Reported rather than done quietly. Deleting a file is the one thing a generator does
        /// that an author cannot undo by regenerating, so the paths are named where the rest of the
        /// run's outcome is named.
        /// </remarks>
        public readonly List<string> Removed = new List<string>();

        /// <summary>
        /// Drops that could not be written as custom loot and need a loot-table registration.
        /// </summary>
        /// <remarks>
        /// Carried out of the generator rather than warned about and forgotten: an amount range or a
        /// biome restriction cannot live on per-object custom loot, so these have to be registered
        /// against the source loot table at load instead. Losing them here would silently flatten
        /// exactly what the author asked for.
        /// </remarks>
        public readonly List<DimensionResolvedDrop> DropsNeedingALootTable =
            new List<DimensionResolvedDrop>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Creatures: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }
}
