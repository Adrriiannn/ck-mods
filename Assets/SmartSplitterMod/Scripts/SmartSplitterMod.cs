using PugMod;
using UnityEngine;

public class SmartSplitterMod : IMod
{
  public void EarlyInit()
  {
    Debug.Log("[SmartSplitterMod] EarlyInit");
  }

  public void Init()
  {
    SmartSplitterAssetRegistry.EnsureExists();
    SmartSplitterVisualSwapController.EnsureExists();
    Debug.Log("[SmartSplitterMod] Init");
  }

  public void Shutdown()
  {
  }

  public void ModObjectLoaded(Object obj)
  {
    SmartSplitterAssetRegistry.RegisterLoadedObject(obj);
  }

  public void Update()
  {
  }
}
