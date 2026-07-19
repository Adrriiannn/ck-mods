using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionSpawnRule> GetSpawnRules(DimensionSpawnRuleQuery query)
    {
      List<DimensionSpawnRule> result = new List<DimensionSpawnRule>();
      foreach (DimensionSpawnRule rule in spawnRules.Values)
      {
        if (!SpawnRuleMatchesQuery(rule, query))
        {
          continue;
        }

        result.Add(rule);
      }

      result.Sort(CompareSpawnRules);
      return result;
    }

    public bool TryGetSpawnRule(string ruleId, out DimensionSpawnRule rule)
    {
      if (string.IsNullOrEmpty(ruleId))
      {
        rule = default(DimensionSpawnRule);
        return false;
      }

      return spawnRules.TryGetValue(ruleId, out rule);
    }

    public bool TryRegisterSpawnRule(DimensionSpawnRule rule, out DimensionOperationResult result)
    {
      if (!ValidateSpawnRule(rule, out result))
      {
        return false;
      }

      if (spawnRules.ContainsKey(rule.RuleId))
      {
        result = DimensionOperationResult.Failed("spawn-rule-already-registered", "A spawn rule with that id is already registered.");
        return false;
      }

      spawnRules[rule.RuleId] = rule;
      RaiseSpawnRuleChanged(
          rule,
          DimensionSpawnRuleChangeKind.Registered,
          false,
          rule.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateSpawnRule(
        DimensionSpawnRule rule,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionSpawnRule previous;
      if (!spawnRules.TryGetValue(rule.RuleId, out previous))
      {
        result = DimensionOperationResult.Failed("spawn-rule-not-found", "No spawn rule with that id is registered.");
        return false;
      }

      if (!ValidateSpawnRule(rule, out result))
      {
        return false;
      }

      if (SpawnRuleEquals(previous, rule))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      spawnRules[rule.RuleId] = rule;
      RaiseSpawnRuleChanged(
          rule,
          previous.Enabled == rule.Enabled
              ? DimensionSpawnRuleChangeKind.Updated
              : DimensionSpawnRuleChangeKind.EnabledChanged,
          previous.Enabled,
          rule.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetSpawnRuleEnabled(
        string ruleId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(ruleId))
      {
        result = DimensionOperationResult.Failed("spawn-rule-id-empty", "A spawn rule id is required.");
        return false;
      }

      DimensionSpawnRule rule;
      if (!spawnRules.TryGetValue(ruleId, out rule))
      {
        result = DimensionOperationResult.Failed("spawn-rule-not-found", "No spawn rule with that id is registered.");
        return false;
      }

      if (rule.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionSpawnRule updated =
          new DimensionSpawnRule(
              rule.RuleId,
              rule.DisplayName,
              rule.DimensionId,
              rule.ZoneId,
              rule.HasLocalBounds,
              rule.LocalBounds,
              rule.SubjectId,
              rule.SubjectKind,
              rule.Weight,
              rule.Priority,
              enabled);

      spawnRules[ruleId] = updated;
      RaiseSpawnRuleChanged(
          updated,
          DimensionSpawnRuleChangeKind.EnabledChanged,
          rule.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveSpawnRule(string ruleId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(ruleId))
      {
        result = DimensionOperationResult.Failed("spawn-rule-id-empty", "A spawn rule id is required.");
        return false;
      }

      DimensionSpawnRule rule;
      if (!spawnRules.TryGetValue(ruleId, out rule))
      {
        result = DimensionOperationResult.Failed("spawn-rule-not-found", "No spawn rule with that id is registered.");
        return false;
      }

      spawnRules.Remove(ruleId);
      RaiseSpawnRuleChanged(
          rule,
          DimensionSpawnRuleChangeKind.Removed,
          rule.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
