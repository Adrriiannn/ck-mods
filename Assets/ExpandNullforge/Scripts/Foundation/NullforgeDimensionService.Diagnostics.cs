using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private readonly List<DimensionDiagnosticEntry> diagnostics =
        new List<DimensionDiagnosticEntry>();

    public event Action<DimensionDiagnosticEntry> DiagnosticEmitted;

    public IReadOnlyList<DimensionDiagnosticEntry> GetRecentDiagnostics(int maxCount)
    {
      if (maxCount <= 0 || diagnostics.Count <= maxCount)
      {
        return new List<DimensionDiagnosticEntry>(diagnostics);
      }

      int start = diagnostics.Count - maxCount;
      List<DimensionDiagnosticEntry> result = new List<DimensionDiagnosticEntry>(maxCount);
      for (int i = start; i < diagnostics.Count; i++)
      {
        result.Add(diagnostics[i]);
      }

      return result;
    }

    public void ClearDiagnostics()
    {
      diagnostics.Clear();
    }

    private void AddDiagnostic(
        DimensionDiagnosticSeverity severity,
        string dimensionId,
        string message)
    {
      DimensionDiagnosticEntry entry =
          new DimensionDiagnosticEntry(
              Time.unscaledTime,
              severity,
              dimensionId ?? string.Empty,
              message ?? string.Empty);

      diagnostics.Add(entry);
      if (diagnostics.Count > MaxDiagnostics)
      {
        int removeCount = diagnostics.Count - MaxDiagnostics;
        diagnostics.RemoveRange(0, removeCount);
      }

      Action<DimensionDiagnosticEntry> handler = DiagnosticEmitted;
      if (handler != null)
      {
        handler(entry);
      }
    }
  }
}
