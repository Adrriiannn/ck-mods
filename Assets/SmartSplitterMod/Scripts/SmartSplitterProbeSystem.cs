using Pug.Automation;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct SmartSplitterProbeSystem : ISystem
{
  private static int tickCounter;
  private static bool applied;

  public void OnUpdate(ref SystemState state)
  {
    if (applied)
    {
      return;
    }

    tickCounter++;

    if (tickCounter < 300)
    {
      return;
    }

    ComponentLookup<MoverCD> moverLookup = SystemAPI.GetComponentLookup<MoverCD>(false);

    foreach (DynamicBuffer<MoversWithSharedStateBuffer> buffer
             in SystemAPI.Query<DynamicBuffer<MoversWithSharedStateBuffer>>())
    {
      if (buffer.Length != 2)
      {
        continue;
      }

      for (int i = 0; i < buffer.Length; i++)
      {
        Entity moverEntity = buffer[i].moverEntity;

        if (!moverLookup.HasComponent(moverEntity))
        {
          continue;
        }

        MoverCD mover = moverLookup[moverEntity];

        if (mover.splitsIntoOnMove != 2)
        {
          continue;
        }

        if (mover.indexInOrchestrator == 1)
        {
          mover.splitsIntoOnMove = 1;
          moverLookup[moverEntity] = mover;

          Debug.Log($"[SmartSplitter] Changed output index 1 splitsIntoOnMove to 1 on mover {moverEntity}");
        }
        else
        {
          Debug.Log($"[SmartSplitter] Left output index {mover.indexInOrchestrator} unchanged on mover {moverEntity}");
        }
      }

      applied = true;
      return;
    }
  }
}
