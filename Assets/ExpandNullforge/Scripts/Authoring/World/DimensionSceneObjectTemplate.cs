using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// One object a scene places — a chest, a statue, a torch, a terminal, a merchant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart to a scene's tiles. Tiles make a room; objects make it worth entering. This is
    /// the piece that unblocks loot, quest givers, boss arenas and lore props, all of which are the
    /// same problem wearing different names: put an authored thing at a spot and let each copy differ.
    /// </para>
    /// <para>
    /// The object is named, never resolved here. Core Keeper hands out its own numeric ids at load,
    /// and they are not stable to write down — a name is resolved against the live database when the
    /// scene is injected into a world, which is also the first moment the prefab it refers to exists.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSceneObjectTemplate
    {
        [Tooltip("Where this sits inside the scene, in the scene's own tile coordinates.")]
        [SerializeField] private Vector2Int localPosition;

        [Tooltip("The object to place. A vanilla name such as Chest, or one of your own mod's items.")]
        [SerializeField] private string objectId = string.Empty;

        [Tooltip("Which way it faces. Ignored by objects that have no facing.")]
        [SerializeField] private DimensionSceneObjectFacing facing = DimensionSceneObjectFacing.Unchanged;

        [Tooltip("Repaint this copy from the game's paint palette. Unpainted keeps the object's own look.")]
        [SerializeField] private DimensionScenePaint paint = DimensionScenePaint.Unpainted;

        [Tooltip("Fill this container with a loot table by name, rolled fresh for each world.")]
        [SerializeField] private string lootTableId = string.Empty;

        [Tooltip("Put exact items in this container. Use for a story item that must always be here.")]
        [SerializeField] private DimensionSceneContainerItem[] contents = new DimensionSceneContainerItem[0];

        [SerializeField] private bool enabled = true;

        public Vector2Int LocalPosition { get { return localPosition; } }

        public string ObjectId { get { return objectId ?? string.Empty; } }

        public DimensionSceneObjectFacing Facing { get { return facing; } }

        public DimensionScenePaint Paint { get { return paint; } }

        public bool Enabled { get { return enabled; } }

        /// <summary>
        /// A loot table to roll into this container, or empty for none.
        /// </summary>
        /// <remarks>
        /// Rolled per world rather than baked, so two players opening "the same" chest in two worlds
        /// find different things — which is what makes exploring worth doing twice.
        /// </remarks>
        public string LootTableId { get { return lootTableId ?? string.Empty; } }

        /// <summary>
        /// Exact items placed in this container.
        /// </summary>
        /// <remarks>
        /// The counterpart to a loot table, for the case a table cannot express: the one key, the one
        /// note, the thing that must be here or the room means nothing. Both can be set — the fixed
        /// items go in, and the table fills the rest.
        /// </remarks>
        public DimensionSceneContainerItem[] Contents
        {
            get { return contents ?? new DimensionSceneContainerItem[0]; }
        }

        /// <summary>Whether anything at all was authored for this container.</summary>
        public bool HasContents
        {
            get { return !string.IsNullOrEmpty(LootTableId) || Contents.Length > 0; }
        }

        public void Configure(
            Vector2Int position,
            string id,
            DimensionSceneObjectFacing objectFacing,
            bool isEnabled)
        {
            localPosition = position;
            objectId = id ?? string.Empty;
            facing = objectFacing;
            enabled = isEnabled;
        }
    }

    /// <summary>One exact item inside a placed container.</summary>
    [Serializable]
    public sealed class DimensionSceneContainerItem
    {
        [Tooltip("The item to place. A vanilla name, or one of your own.")]
        [SerializeField] private string itemId = string.Empty;

        [Min(1)]
        [SerializeField] private int amount = 1;

        public string ItemId
        {
            get { return itemId ?? string.Empty; }
        }

        public int Amount
        {
            get { return amount < 1 ? 1 : amount; }
        }
    }

    /// <summary>
    /// Which way a placed object faces.
    /// </summary>
    /// <remarks>
    /// <c>Unchanged</c> exists as the default because most objects either have no facing at all or
    /// look correct in whatever pose they were authored with. Forcing a direction onto everything
    /// would quietly rotate props that were fine.
    /// </remarks>
    public enum DimensionSceneObjectFacing
    {
        Unchanged = 0,
        North = 1,
        East = 2,
        South = 3,
        West = 4
    }

    /// <summary>
    /// Core Keeper's paint palette.
    /// </summary>
    /// <remarks>
    /// Paint is a fixed set of named colours, not a free RGB value — the game stores a palette entry
    /// per object, and the sprite work behind each one is authored. Offering a colour picker would
    /// let an author choose something the game cannot render, so the choice is the palette itself.
    /// Mirrored here rather than used directly so the authoring asset does not depend on a game enum
    /// whose numbering could shift.
    /// </remarks>
    public enum DimensionScenePaint
    {
        Unpainted = 0,
        Yellow,
        Green,
        Red,
        Purple,
        Blue,
        Brown,
        White,
        Black,
        Orange,
        Cyan,
        Pink,
        Gray,
        Peach,
        Teal
    }
}
