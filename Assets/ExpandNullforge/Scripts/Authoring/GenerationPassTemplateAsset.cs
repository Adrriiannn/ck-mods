using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Generation Pass Template")]
    public sealed class GenerationPassTemplateAsset : ScriptableObject
    {
        [SerializeField] private string passId = "terrain";
        [SerializeField] private string displayName = "Terrain";
        [SerializeField] private DimensionGenerationPassPhase phase = DimensionGenerationPassPhase.Terrain;
        [SerializeField] private string providerId = string.Empty;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool hasExplicitLocalBounds;
        [SerializeField] private Vector2Int explicitLocalMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int explicitLocalMaxExclusive = new Vector2Int(64, 64);

        public string PassId
        {
            get { return passId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public DimensionGenerationPassPhase Phase
        {
            get { return phase; }
        }

        public string ProviderId
        {
            get { return providerId ?? string.Empty; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public bool HasExplicitLocalBounds
        {
            get { return hasExplicitLocalBounds; }
        }

        public DimensionBounds ExplicitLocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(explicitLocalMin.x, explicitLocalMin.y),
                    new int2(explicitLocalMaxExclusive.x, explicitLocalMaxExclusive.y));
            }
        }

        public void ConfigureIdentity(
            string newPassId,
            string newDisplayName,
            DimensionGenerationPassPhase newPhase,
            string newProviderId,
            int newPriority,
            bool newEnabled)
        {
            passId = newPassId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            phase = newPhase;
            providerId = newProviderId ?? string.Empty;
            priority = newPriority;
            enabled = newEnabled;
        }

        public void ApplyExplicitLocalBounds(Vector2Int newLocalMin, Vector2Int newLocalMaxExclusive)
        {
            explicitLocalMin = newLocalMin;
            explicitLocalMaxExclusive = EnsureExclusiveMax(newLocalMin, newLocalMaxExclusive);
            hasExplicitLocalBounds = true;
        }

        public void ClearExplicitLocalBounds()
        {
            hasExplicitLocalBounds = false;
        }

        public DimensionGenerationPassDefinition ToDefinition(
            string passIdOverride,
            string dimensionId,
            string zoneId,
            bool hasFallbackLocalBounds,
            DimensionBounds fallbackLocalBounds)
        {
            bool hasBounds = hasExplicitLocalBounds || hasFallbackLocalBounds;
            DimensionBounds bounds = hasExplicitLocalBounds ? ExplicitLocalBounds : fallbackLocalBounds;
            string resolvedPassId = string.IsNullOrEmpty(passIdOverride) ? passId : passIdOverride;
            string resolvedDisplayName = string.IsNullOrEmpty(displayName) ? PassId : displayName;

            return new DimensionGenerationPassDefinition(
                resolvedPassId,
                resolvedDisplayName,
                dimensionId,
                zoneId,
                hasBounds,
                bounds,
                phase,
                priority,
                providerId,
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
