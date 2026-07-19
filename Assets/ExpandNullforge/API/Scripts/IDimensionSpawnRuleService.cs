using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionSpawnRuleService
    {
        event Action<DimensionSpawnRuleChangedEvent> SpawnRuleChanged;

        IReadOnlyList<DimensionSpawnRule> GetSpawnRules(DimensionSpawnRuleQuery query);

        bool TryGetSpawnRule(string ruleId, out DimensionSpawnRule rule);

        bool TryRegisterSpawnRule(DimensionSpawnRule rule, out DimensionOperationResult result);

        bool TryUpdateSpawnRule(
            DimensionSpawnRule rule,
            string reason,
            out DimensionOperationResult result);

        bool TrySetSpawnRuleEnabled(
            string ruleId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveSpawnRule(string ruleId, out DimensionOperationResult result);
    }
}
