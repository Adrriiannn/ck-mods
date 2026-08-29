using System;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// One tile a scene stamps, authored against a block from the dimension's palette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The block is named, not resolved: a scene refers to <c>MyMod:obsidian</c> and the tileset id is
    /// worked out at generation time. That matters because a custom tileset's id is derived from its
    /// name, so storing the number here would bake a value that a rename silently invalidates — the
    /// scene would keep stamping tiles of a tileset nobody owns any more, and they would render as
    /// whatever happened to land on that id.
    /// </para>
    /// <para>
    /// The role is the same <see cref="DimensionTileRole"/> the terrain painter uses, not a
    /// scene-specific vocabulary. A block behaves the same whether it was painted into a dimension's
    /// terrain or into a structure, so the two should not be able to drift apart — and it means
    /// <c>DimensionBlockTileMapping.ToTileType</c> is the single place that decides what a role
    /// becomes, for both.
    /// </para>
    /// <para>
    /// Positions are the scene's own local coordinates, so a scene can be placed anywhere without
    /// rewriting it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSceneTileTemplate
    {
        [SerializeField] private Vector2Int localPosition;

        [Tooltip("Which block from the dimension's palette this tile is made of.")]
        [SerializeField] private string blockId = string.Empty;

        [Tooltip("Whether this tile is the block's floor, its wall, water, a pit or a skylight.")]
        [SerializeField] private DimensionTileRole role = DimensionTileRole.Ground;

        [SerializeField] private bool enabled = true;

        public Vector2Int LocalPosition { get { return localPosition; } }

        public string BlockId { get { return blockId ?? string.Empty; } }

        public DimensionTileRole Role { get { return role; } }

        public bool Enabled { get { return enabled; } }

        public void Configure(Vector2Int position, string block, DimensionTileRole tileRole, bool isEnabled)
        {
            localPosition = position;
            blockId = block ?? string.Empty;
            role = tileRole;
            enabled = isEnabled;
        }
    }
}
