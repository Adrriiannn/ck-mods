using System.Text;
using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterForwardTopologyComponentProbeSystem : SystemBase
{
  private EntityQuery _smartSplitterQuery;
  private EntityQuery _moverQuery;
  private double _nextLogAt;

  protected override void OnCreate()
  {
    _smartSplitterQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterTag>(),
        ComponentType.ReadOnly<SmartSplitterOriginalOutputsCD>());

    _moverQuery = GetEntityQuery(ComponentType.ReadOnly<MoverCD>());
  }

  protected override void OnUpdate()
  {
    if (!SmartSplitterDebugSettings.EnableDebugProbes ||
        !SmartSplitterDebugSettings.EnableMoveeHandoffProbe)
    {
      return;
    }

    double now = World.Time.ElapsedTime;
    if (now < _nextLogAt)
    {
      return;
    }

    _nextLogAt = now + 1.00d;

    using NativeArray<Entity> orchestrators = _smartSplitterQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<Entity> moverEntities = _moverQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<MoverCD> moverData = _moverQuery.ToComponentDataArray<MoverCD>(Allocator.Temp);

    foreach (Entity orchestrator in orchestrators)
    {
      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        continue;
      }

      SmartSplitterOriginalOutputsCD originals = EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);
      if (!IsOriginalOutputStateValid(originals))
      {
        continue;
      }

      MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
      MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

      int2 center = new int2(
          Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
          Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

      Entity inputMover = Entity.Null;
      Entity forwardMover = Entity.Null;
      int2 back = default;
      int2 forward = default;
      int2 forwardDirection = default;
      int inputCount = 0;

      for (int i = 0; i < moverEntities.Length; i++)
      {
        Entity moverEntity = moverEntities[i];
        MoverCD mover = moverData[i];

        if (moverEntity == originals.LeftMoverEntity || moverEntity == originals.RightMoverEntity)
        {
          continue;
        }

        if (mover.stop.x == center.x && mover.stop.y == center.y)
        {
          inputCount++;
          inputMover = moverEntity;
          back = mover.start;
          forwardDirection = new int2(center.x - mover.start.x, center.y - mover.start.y);
        }
      }

      if (inputCount == 1 && !(forwardDirection.x == 0 && forwardDirection.y == 0))
      {
        forward = new int2(center.x + forwardDirection.x, center.y + forwardDirection.y);

        for (int i = 0; i < moverEntities.Length; i++)
        {
          Entity moverEntity = moverEntities[i];
          MoverCD mover = moverData[i];

          if (mover.start.x != forward.x || mover.start.y != forward.y)
          {
            continue;
          }

          int2 direction = new int2(mover.stop.x - mover.start.x, mover.stop.y - mover.start.y);
          if (direction.x == forwardDirection.x && direction.y == forwardDirection.y)
          {
            forwardMover = moverEntity;
            break;
          }
        }
      }

      Debug.Log(
          $"[SmartSplitterDebug][ForwardTopologyComponentProbe] SUMMARY orchestrator={orchestrator} " +
          $"center={center} inputCount={inputCount} back={back} forward={forward} forwardDirection={forwardDirection} " +
          $"leftMover={originals.LeftMoverEntity} rightMover={originals.RightMoverEntity} " +
          $"inputMover={inputMover} forwardMover={forwardMover} " +
          $"buffer={BuildBufferText(orchestrator)} orchestratorCD={BuildOrchestratorText(orchestrator)}");

      LogMover("leftOutput", originals.LeftMoverEntity, center);
      LogMover("rightOutput", originals.RightMoverEntity, center);
      LogMover("input", inputMover, center);
      LogMover("forwardBelt", forwardMover, center);
    }
  }

  private bool IsOriginalOutputStateValid(SmartSplitterOriginalOutputsCD originals)
  {
    return originals.HasOriginalOutputs &&
        EntityManager.Exists(originals.LeftMoverEntity) &&
        EntityManager.Exists(originals.RightMoverEntity) &&
        EntityManager.HasComponent<MoverCD>(originals.LeftMoverEntity) &&
        EntityManager.HasComponent<MoverCD>(originals.RightMoverEntity);
  }

  private void LogMover(string role, Entity entity, int2 center)
  {
    if (entity == Entity.Null || !EntityManager.Exists(entity))
    {
      Debug.Log($"[SmartSplitterDebug][ForwardTopologyComponentProbe] MOVER role={role} entity={entity} missing=True");
      return;
    }

    string moverText = "MoverCD=missing";
    if (EntityManager.HasComponent<MoverCD>(entity))
    {
      MoverCD mover = EntityManager.GetComponentData<MoverCD>(entity);
      moverText =
          $"path=({mover.start.x},{mover.start.y})->({mover.stop.x},{mover.stop.y}) " +
          $"start={mover.start} stop={mover.stop} " +
          $"dir=({mover.stop.x - mover.start.x},{mover.stop.y - mover.start.y}) " +
          $"moveTime={mover.moveTime} cooldownTime={mover.cooldownTime} " +
          $"inventoryEntity={mover.inventoryEntity} moverOrchestratorEntity={mover.moverOrchestratorEntity} " +
          $"splitsIntoOnMove={mover.splitsIntoOnMove} indexInOrchestrator={mover.indexInOrchestrator} " +
          $"cycleEnabledMoverAfterActivation={mover.cycleEnabledMoverAfterActivation} " +
          $"enableAllMoversAfterActivation={mover.enableAllMoversAfterActivation} " +
          $"allowPickupFromInventories={mover.allowPickupFromInventories} " +
          $"centerDistance={DistanceFromSegment(center, mover.start, mover.stop):0.00}";
    }

    Debug.Log(
        $"[SmartSplitterDebug][ForwardTopologyComponentProbe] MOVER role={role} entity={entity} " +
        $"{moverText} timer={BuildMoverTimerText(entity)} " +
        $"hasEnabledMoverFromSharedState={EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(entity)} " +
        $"components={BuildComponentList(entity)}");
  }

  private string BuildBufferText(Entity orchestrator)
  {
    if (!EntityManager.Exists(orchestrator) || !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
    {
      return "none";
    }

    DynamicBuffer<MoversWithSharedStateBuffer> buffer = EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);
    StringBuilder sb = new StringBuilder();
    sb.Append("len=");
    sb.Append(buffer.Length);

    for (int i = 0; i < buffer.Length; i++)
    {
      MoversWithSharedStateBuffer entry = buffer[i];
      sb.Append(" | ");
      sb.Append(i);
      sb.Append(":entity=");
      sb.Append(entry.moverEntity);
      sb.Append(" cachedStart=");
      sb.Append(entry.cachedStart);
      sb.Append(" cachedDirection=");
      sb.Append(entry.cachedDirection);
    }

    return sb.ToString();
  }

  private string BuildOrchestratorText(Entity orchestrator)
  {
    if (!EntityManager.Exists(orchestrator) || !EntityManager.HasComponent<MoverOrchestratorCD>(orchestrator))
    {
      return "missing";
    }

    MoverOrchestratorCD data = EntityManager.GetComponentData<MoverOrchestratorCD>(orchestrator);
    return $"enabledMoverIndex={data.enabledMoverIndex} nextMoverCycleIncrement={data.nextMoverCycleIncrement}";
  }

  private string BuildComponentList(Entity entity)
  {
    if (entity == Entity.Null || !EntityManager.Exists(entity))
    {
      return "none";
    }

    using NativeArray<ComponentType> types = EntityManager.GetComponentTypes(entity, Allocator.Temp);
    StringBuilder sb = new StringBuilder();

    for (int i = 0; i < types.Length; i++)
    {
      if (i > 0)
      {
        sb.Append("|");
      }

      sb.Append(types[i].ToString());
    }

    return sb.ToString();
  }

  private string BuildMoverTimerText(Entity mover)
  {
    if (mover == Entity.Null || !EntityManager.Exists(mover))
    {
      return "none";
    }

    if (!EntityManager.HasComponent<MoverTimerCD>(mover))
    {
      return "noTimer";
    }

    MoverTimerCD timer = EntityManager.GetComponentData<MoverTimerCD>(mover);
    return $"timer={timer.timer}";
  }

  private static float DistanceFromSegment(int2 point, int2 start, int2 stop)
  {
    float px = point.x;
    float py = point.y;
    float sx = start.x;
    float sy = start.y;
    float ex = stop.x;
    float ey = stop.y;

    float dx = ex - sx;
    float dy = ey - sy;
    float lengthSq = dx * dx + dy * dy;

    if (lengthSq <= 0.0001f)
    {
      float ox = px - sx;
      float oy = py - sy;
      return Mathf.Sqrt(ox * ox + oy * oy);
    }

    float t = ((px - sx) * dx + (py - sy) * dy) / lengthSq;
    t = Mathf.Clamp01(t);

    float cx = sx + t * dx;
    float cy = sy + t * dy;
    float diffX = px - cx;
    float diffY = py - cy;
    return Mathf.Sqrt(diffX * diffX + diffY * diffY);
  }
}
