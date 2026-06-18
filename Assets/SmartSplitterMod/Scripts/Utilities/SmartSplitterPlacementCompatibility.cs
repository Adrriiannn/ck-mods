using System;
using PugMod;
using UnityEngine;

public static class SmartSplitterPlacementCompatibility
{
  private const string PlacementPlusModName = "PlacementPlus";

  private static bool _checkedLoadedMods;
  private static bool _placementPlusLoaded;
  private static bool _loggedPlacementPlusMode;

  public static bool ShouldRemapEntityCreationVariation
  {
    get
    {
      EnsureLoadedModsChecked();
      return _placementPlusLoaded;
    }
  }

  public static bool ShouldRemapInventoryPlacementRequest
  {
    get
    {
      EnsureLoadedModsChecked();
      return !_placementPlusLoaded;
    }
  }

  public static void RefreshLoadedMods()
  {
    _checkedLoadedMods = true;
    _placementPlusLoaded = IsModLoaded(PlacementPlusModName);

    if (_placementPlusLoaded && !_loggedPlacementPlusMode)
    {
      _loggedPlacementPlusMode = true;
      Debug.Log("[SmartSplitterMod] PlacementPlus detected; using entity creation placement compatibility.");
    }
  }

  private static void EnsureLoadedModsChecked()
  {
    if (!_checkedLoadedMods)
    {
      RefreshLoadedMods();
    }
  }

  private static bool IsModLoaded(string modName)
  {
    if (API.ModLoader == null || API.ModLoader.LoadedMods == null)
    {
      return false;
    }

    foreach (LoadedMod mod in API.ModLoader.LoadedMods)
    {
      if (mod.Metadata.name != null &&
          mod.Metadata.name.Equals(modName, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }
}
