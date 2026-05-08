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
    Debug.Log("[SmartSplitterMod] Init");
  }

  public void Shutdown()
  {
    Debug.Log("[SmartSplitterMod] Shutdown");
  }

  public void ModObjectLoaded(Object obj)
  {
    Debug.Log($"[SmartSplitterMod] Loaded object: {obj.name}");
  }

  public void Update()
  {
  }
}
