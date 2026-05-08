using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct SmartSplitterProbeSystem : ISystem
{
  private static bool logged;

  public void OnCreate(ref SystemState state)
  {
    Debug.Log("[SmartSplitter] Probe system created");
  }

  public void OnUpdate(ref SystemState state)
  {
    if (logged)
    {
      return;
    }

    logged = true;
    Debug.Log("[SmartSplitter] Probe system updated");
  }
}
