using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Plants;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating plants did.</summary>
    internal sealed class DimensionPlantGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Removed = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Plants: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }
}
