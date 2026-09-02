using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What an object does to the tile it stands on, when it is damaged and when it is destroyed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two of Core Keeper's components, kept together because they are the same idea at two moments.
    /// <c>CrackableTileAuthoring</c> (93 vanilla prefabs) is what a tile becomes when it has been
    /// hit but not broken — the cracked version. <c>SpawnTileOnDeathAuthoring</c> (146) is what is
    /// left standing where the object was.
    /// </para>
    /// <para>
    /// Both name a tileset by <b>our</b> id rather than by the game's enum, because a custom block
    /// is the most likely thing an author wants left behind. That is the whole point of the tileset
    /// system, and a mechanism that could only leave vanilla dirt would be a strange one to ship
    /// alongside it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionTileOutcomeTemplate
    {
        [Header("When it is destroyed")]
        [Tooltip("The tileset of the tile left where it stood. Empty leaves nothing.")]
        [SerializeField] private string leavesTilesetId = string.Empty;

        [Tooltip("Which layer of that tileset — ground, wall, water and so on.")]
        [SerializeField] private PugTilemap.TileType leavesTileType = PugTilemap.TileType.ground;

        [Tooltip("How often it is left. 1 is always.")]
        [Range(0f, 1f)]
        [SerializeField] private float leavesChance = 1f;

        [Tooltip("Wipes whatever else was on that tile first.")]
        [SerializeField] private bool clearsWhatWasThere;

        [Header("When it is damaged but not destroyed")]
        [Tooltip("The tileset of the cracked version. Empty means it does not crack.")]
        [SerializeField] private string cracksIntoTilesetId = string.Empty;

        [Tooltip("Which layer of that tileset the cracked version is.")]
        [SerializeField] private PugTilemap.TileType cracksIntoTileType = PugTilemap.TileType.ground;

        public string LeavesTilesetId
        {
            get { return leavesTilesetId ?? string.Empty; }
        }

        public PugTilemap.TileType LeavesTileType
        {
            get { return leavesTileType; }
        }

        public float LeavesChance
        {
            get
            {
                if (leavesChance < 0f)
                {
                    return 0f;
                }

                return leavesChance > 1f ? 1f : leavesChance;
            }
        }

        public bool ClearsWhatWasThere
        {
            get { return clearsWhatWasThere; }
        }

        public string CracksIntoTilesetId
        {
            get { return cracksIntoTilesetId ?? string.Empty; }
        }

        public PugTilemap.TileType CracksIntoTileType
        {
            get { return cracksIntoTileType; }
        }

        /// <summary>Whether destroying it leaves a tile behind.</summary>
        public bool LeavesATileBehind
        {
            get { return !string.IsNullOrEmpty(LeavesTilesetId) && LeavesChance > 0f; }
        }

        /// <summary>Whether it has a cracked state at all.</summary>
        public bool CracksFirst
        {
            get { return !string.IsNullOrEmpty(CracksIntoTilesetId); }
        }

        /// <summary>Whether anything here is set.</summary>
        public bool ChangesTheTile
        {
            get { return LeavesATileBehind || CracksFirst; }
        }

        /// <summary>Whether a tile was named to leave behind and then set never to be left.</summary>
        /// <remarks>
        /// Worth saying because it looks configured: a tileset is chosen, the field is filled in,
        /// and the chance is zero, so it can never happen.
        /// </remarks>
        public bool NamesATileItWillNeverLeave
        {
            get { return !string.IsNullOrEmpty(LeavesTilesetId) && LeavesChance <= 0f; }
        }
    }
}
