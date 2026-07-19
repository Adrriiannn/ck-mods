using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Spawn Rule Template")]
    public sealed class SpawnRuleTemplateAsset : ScriptableObject
    {
        [SerializeField] private string ruleId = "spawn-rule";
        [SerializeField] private string displayName = "Spawn Rule";
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private bool hasLocalBounds;
        [SerializeField] private Vector2Int localMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int localMaxExclusive = new Vector2Int(64, 64);
        [SerializeField] private string subjectId = string.Empty;
        [SerializeField] private DimensionSpawnSubjectKind subjectKind = DimensionSpawnSubjectKind.Mob;
        [SerializeField] private int weight = 1;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;

        public string RuleId
        {
            get { return ruleId ?? string.Empty; }
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
            string newRuleId,
            string newDisplayName,
            string newZoneId,
            string newSubjectId,
            DimensionSpawnSubjectKind newSubjectKind,
            int newWeight,
            int newPriority,
            bool newEnabled)
        {
            ruleId = newRuleId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            zoneId = newZoneId ?? string.Empty;
            subjectId = newSubjectId ?? string.Empty;
            subjectKind = newSubjectKind;
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

        public DimensionSpawnRule ToRule(string dimensionId, string fallbackZoneId)
        {
            string resolvedZoneId = string.IsNullOrEmpty(zoneId) ? fallbackZoneId : zoneId;
            return new DimensionSpawnRule(
                ruleId,
                displayName,
                dimensionId,
                resolvedZoneId,
                hasLocalBounds,
                LocalBounds,
                subjectId,
                subjectKind,
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
