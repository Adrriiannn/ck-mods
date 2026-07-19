using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Resource Node Template")]
    public sealed class ResourceNodeTemplateAsset : ScriptableObject
    {
        [SerializeField] private string nodeId = "resource-node";
        [SerializeField] private string displayName = "Resource Node";
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private bool hasLocalBounds;
        [SerializeField] private Vector2Int localMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int localMaxExclusive = new Vector2Int(64, 64);
        [SerializeField] private string resourceId = string.Empty;
        [SerializeField] private DimensionResourceNodeKind kind = DimensionResourceNodeKind.Custom;
        [SerializeField] private string providerId = string.Empty;
        [SerializeField] private string generationPassId = string.Empty;
        [SerializeField] private int weight = 1;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;

        public string NodeId
        {
            get { return nodeId ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public bool HasLocalBounds
        {
            get { return hasLocalBounds; }
        }

        public DimensionBounds LocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(localMin.x, localMin.y),
                    new int2(localMaxExclusive.x, localMaxExclusive.y));
            }
        }

        public void ConfigureIdentity(
            string newNodeId,
            string newDisplayName,
            string newZoneId,
            string newResourceId,
            DimensionResourceNodeKind newKind,
            string newProviderId,
            string newGenerationPassId,
            int newWeight,
            int newPriority,
            bool newEnabled)
        {
            nodeId = newNodeId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            zoneId = newZoneId ?? string.Empty;
            resourceId = newResourceId ?? string.Empty;
            kind = newKind;
            providerId = newProviderId ?? string.Empty;
            generationPassId = newGenerationPassId ?? string.Empty;
            weight = Mathf.Max(1, newWeight);
            priority = newPriority;
            enabled = newEnabled;
        }

        public void ApplyLocalBounds(Vector2Int newLocalMin, Vector2Int newLocalMaxExclusive)
        {
            localMin = newLocalMin;
            localMaxExclusive = EnsureExclusiveMax(newLocalMin, newLocalMaxExclusive);
            hasLocalBounds = true;
        }

        public void ClearLocalBounds()
        {
            hasLocalBounds = false;
        }

        public DimensionResourceNodeDefinition ToDefinition(string dimensionId, string fallbackZoneId)
        {
            string resolvedZoneId = string.IsNullOrEmpty(zoneId) ? fallbackZoneId : zoneId;
            return new DimensionResourceNodeDefinition(
                nodeId,
                displayName,
                dimensionId,
                resolvedZoneId,
                hasLocalBounds,
                LocalBounds,
                resourceId,
                kind,
                providerId,
                generationPassId,
                weight,
                priority,
                enabled);
        }

        private static Vector2Int EnsureExclusiveMax(Vector2Int localMin, Vector2Int localMaxExclusive)
        {
            return new Vector2Int(
                Mathf.Max(localMin.x + 1, localMaxExclusive.x),
                Mathf.Max(localMin.y + 1, localMaxExclusive.y));
        }
    }
}
