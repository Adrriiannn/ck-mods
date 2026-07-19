using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateSpawnRule(
        DimensionSpawnRule rule,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(rule.RuleId))
      {
        result = DimensionOperationResult.Failed("spawn-rule-id-empty", "A spawn rule id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(rule.SubjectId))
      {
        result = DimensionOperationResult.Failed("spawn-rule-subject-empty", "A spawn rule subject id is required.");
        return false;
      }

      if (!IsValidSpawnSubjectKind(rule.SubjectKind) || rule.SubjectKind == DimensionSpawnSubjectKind.Any)
      {
        result = DimensionOperationResult.Failed("spawn-rule-kind-invalid", "The spawn rule subject kind is not supported.");
        return false;
      }

      if (rule.Weight <= 0)
      {
        result = DimensionOperationResult.Failed("spawn-rule-weight-invalid", "The spawn rule weight must be greater than zero.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(rule.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("spawn-rule-dimension-not-found", "The spawn rule dimension is not registered.");
        return false;
      }

      if (rule.HasLocalBounds)
      {
        if (rule.LocalBounds.Size.x <= 0 || rule.LocalBounds.Size.y <= 0)
        {
          result = DimensionOperationResult.Failed("spawn-rule-bounds-invalid", "The spawn rule local bounds must have a positive size.");
          return false;
        }

        if (!dimension.LocalBounds.Contains(rule.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(rule.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          result = DimensionOperationResult.Failed("spawn-rule-bounds-out-of-dimension", "The spawn rule bounds are outside the spawn rule dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool IsValidSpawnSubjectKind(DimensionSpawnSubjectKind kind)
    {
      return kind == DimensionSpawnSubjectKind.Any ||
             kind == DimensionSpawnSubjectKind.Mob ||
             kind == DimensionSpawnSubjectKind.Boss ||
             kind == DimensionSpawnSubjectKind.Critter ||
             kind == DimensionSpawnSubjectKind.Object ||
             kind == DimensionSpawnSubjectKind.Ore ||
             kind == DimensionSpawnSubjectKind.Item ||
             kind == DimensionSpawnSubjectKind.Event ||
             kind == DimensionSpawnSubjectKind.Custom;
    }

    private static bool SpawnRuleMatchesQuery(
        DimensionSpawnRule rule,
        DimensionSpawnRuleQuery query)
    {
      if (query.EnabledOnly && !rule.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(rule.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.ZoneId) &&
          !string.IsNullOrEmpty(rule.ZoneId) &&
          !string.Equals(rule.ZoneId, query.ZoneId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.SubjectKind != DimensionSpawnSubjectKind.Any &&
          rule.SubjectKind != query.SubjectKind)
      {
        return false;
      }

      if (query.HasLocalPosition &&
          rule.HasLocalBounds &&
          !rule.LocalBounds.Contains(query.LocalPosition))
      {
        return false;
      }

      return true;
    }

    private static int CompareSpawnRules(
        DimensionSpawnRule left,
        DimensionSpawnRule right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int kind = left.SubjectKind.CompareTo(right.SubjectKind);
      if (kind != 0)
      {
        return kind;
      }

      int weight = right.Weight.CompareTo(left.Weight);
      if (weight != 0)
      {
        return weight;
      }

      return string.Compare(left.RuleId, right.RuleId, StringComparison.Ordinal);
    }

    private bool SpawnRuleEquals(
        DimensionSpawnRule a,
        DimensionSpawnRule b)
    {
      return string.Equals(a.RuleId, b.RuleId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             a.HasLocalBounds == b.HasLocalBounds &&
             (!a.HasLocalBounds || BoundsEqual(a.LocalBounds, b.LocalBounds)) &&
             string.Equals(a.SubjectId, b.SubjectId, StringComparison.Ordinal) &&
             a.SubjectKind == b.SubjectKind &&
             a.Weight == b.Weight &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}
