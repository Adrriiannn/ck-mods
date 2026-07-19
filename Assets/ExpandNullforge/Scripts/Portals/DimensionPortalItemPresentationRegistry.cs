using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Portals
{
  public static class DimensionPortalItemPresentationRegistry
  {
    private static readonly List<DimensionPortalItemPresentationDefinition> Presentations =
        new List<DimensionPortalItemPresentationDefinition>();

    public static int Count
    {
      get { return Presentations.Count; }
    }

    public static void Clear()
    {
      Presentations.Clear();
    }

    public static bool Register(DimensionPortalItemPresentationDefinition definition)
    {
      if (!definition.IsValid)
      {
        return false;
      }

      for (int i = 0; i < Presentations.Count; i++)
      {
        DimensionPortalItemPresentationDefinition existing = Presentations[i];
        if (string.Equals(existing.PortalObjectName, definition.PortalObjectName, StringComparison.Ordinal))
        {
          Presentations[i] = ChooseHigherPriority(existing, definition);
          return true;
        }
      }

      Presentations.Add(definition);
      return true;
    }

    public static bool TryGet(
        int index,
        out DimensionPortalItemPresentationDefinition definition)
    {
      if (index < 0 || index >= Presentations.Count)
      {
        definition = default(DimensionPortalItemPresentationDefinition);
        return false;
      }

      definition = Presentations[index];
      return true;
    }

    public static bool TryFindForPortalObject(
        string portalObjectName,
        out DimensionPortalItemPresentationDefinition definition)
    {
      if (string.IsNullOrEmpty(portalObjectName))
      {
        definition = default(DimensionPortalItemPresentationDefinition);
        return false;
      }

      int bestIndex = -1;
      for (int i = 0; i < Presentations.Count; i++)
      {
        DimensionPortalItemPresentationDefinition candidate = Presentations[i];
        if (!candidate.Enabled ||
            !string.Equals(candidate.PortalObjectName, portalObjectName, StringComparison.Ordinal))
        {
          continue;
        }

        if (bestIndex < 0 || candidate.Priority > Presentations[bestIndex].Priority)
        {
          bestIndex = i;
        }
      }

      if (bestIndex < 0)
      {
        definition = default(DimensionPortalItemPresentationDefinition);
        return false;
      }

      definition = Presentations[bestIndex];
      return true;
    }

    private static DimensionPortalItemPresentationDefinition ChooseHigherPriority(
        DimensionPortalItemPresentationDefinition existing,
        DimensionPortalItemPresentationDefinition incoming)
    {
      if (!existing.Enabled)
      {
        return incoming;
      }

      if (!incoming.Enabled)
      {
        return existing;
      }

      return incoming.Priority >= existing.Priority ? incoming : existing;
    }
  }
}
