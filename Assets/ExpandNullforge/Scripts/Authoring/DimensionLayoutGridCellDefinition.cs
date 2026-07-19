using System;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [Serializable]
    public sealed class DimensionLayoutGridCellDefinition
    {
        [SerializeField] private string cellId = "grid-cell";
        [SerializeField] private string biomeId = string.Empty;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Vector2Int cell = new Vector2Int(0, 0);
        [SerializeField] private Vector2Int cellSpan = new Vector2Int(1, 1);
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;

        public DimensionLayoutGridCellDefinition()
        {
        }

        public DimensionLayoutGridCellDefinition(
            string cellId,
            string biomeId,
            string zoneId,
            string displayName,
            Vector2Int cell,
            Vector2Int cellSpan,
            int priority,
            bool enabled)
        {
            this.cellId = cellId ?? string.Empty;
            this.biomeId = biomeId ?? string.Empty;
            this.zoneId = zoneId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.cell = cell;
            this.cellSpan = cellSpan;
            this.priority = priority;
            this.enabled = enabled;
        }

        public string CellId
        {
            get { return cellId ?? string.Empty; }
        }

        public string BiomeId
        {
            get { return biomeId ?? string.Empty; }
        }

        public string ZoneId
        {
            get { return zoneId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public Vector2Int Cell
        {
            get { return cell; }
        }

        public Vector2Int CellSpan
        {
            get
            {
                return new Vector2Int(
                    cellSpan.x < 1 ? 1 : cellSpan.x,
                    cellSpan.y < 1 ? 1 : cellSpan.y);
            }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public DimensionBounds ToLocalBounds(Vector2Int gridLocalMin, Vector2Int gridCellSize)
        {
            Vector2Int span = CellSpan;
            int2 min = new int2(
                gridLocalMin.x + cell.x * gridCellSize.x,
                gridLocalMin.y + cell.y * gridCellSize.y);
            int2 maxExclusive = new int2(
                min.x + span.x * gridCellSize.x,
                min.y + span.y * gridCellSize.y);
            return new DimensionBounds(min, maxExclusive);
        }
    }
}
