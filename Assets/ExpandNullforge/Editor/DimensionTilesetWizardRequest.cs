using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using ExpandNullforge.EditorTools.Generation;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Everything the Add-block wizard collects; handed to the window as one request when the
    /// modder clicks Create. The utility turns it into a configured tileset asset (with no sheet —
    /// the modder authors that next) plus, in create-item mode, the seeded inventory item.
    /// </summary>
    internal sealed class DimensionTilesetWizardRequest
    {
        public string TypeKey;
        public List<string> StateKeys = new List<string>();
        public DimensionTilesetItemMode ItemMode;
        public int ReskinIndex = -1;
        public string BlockName;
        public string Description = string.Empty;
        public string RarityId = string.Empty;
    }
}
