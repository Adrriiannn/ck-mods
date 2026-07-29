using System;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// One ore this block's walls can hold as a vein: the linked item that mining the vein drops —
    /// either a vanilla ore item (by its ObjectID name, e.g. "CopperOre") or a custom item from the
    /// mod's Resources (by its item id). Authored in the block's control panel; the generator emits
    /// the hidden vein object that gives the (ore, tileset) tile its identity and drop.
    /// </summary>
    [Serializable]
    public sealed class DimensionTilesetOreConfig
    {
        /// <summary>Vanilla ObjectID name ("CopperOre") or custom item id, per <see cref="isCustomItem"/>.</summary>
        public string oreItemId = string.Empty;

        /// <summary>True when <see cref="oreItemId"/> is a custom item id from this mod's Resources.</summary>
        public bool isCustomItem;
    }
}
