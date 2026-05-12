using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterMoveeHandoffProbeSystem : SystemBase
{
    private double _nextLogTime;

    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>()));
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableMoveeHandoffProbe)
        {
            return;
        }

        if (!SmartSplitterDebugSettings.ShouldRunProbe(
                World.Time.ElapsedTime,
                ref _nextLogTime,
                SmartSplitterDebugSettings.MoveeHandoffProbeIntervalSeconds))
        {
            return;
        }

        ComponentLookup<MoverCD> moverLookup = GetComponentLookup<MoverCD>(true);

        EntityQuery splitterQuery = GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>());
        using NativeArray<Entity> splitters = splitterQuery.ToEntityArray(Allocator.Temp);

        EntityQuery moverQuery = GetEntityQuery(ComponentType.ReadOnly<MoverCD>());
        using NativeArray<Entity> moverEntities = moverQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<MoverCD> movers = moverQuery.ToComponentDataArray<MoverCD>(Allocator.Temp);

        EntityQuery droppedItemQuery = GetEntityQuery(
            ComponentType.ReadOnly<ObjectDataCD>(),
            ComponentType.ReadOnly<ContainedObjectsBuffer>(),
            ComponentType.ReadOnly<LocalTransform>());
        using NativeArray<Entity> droppedEntities = droppedItemQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<ObjectDataCD> droppedObjects = droppedItemQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
        using NativeArray<LocalTransform> droppedTransforms = droppedItemQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        EntityQuery moveeQuery = GetEntityQuery(ComponentType.ReadOnly<MoveeCD>());
        using NativeArray<Entity> moveeEntities = moveeQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<MoveeCD> movees = moveeQuery.ToComponentDataArray<MoveeCD>(Allocator.Temp);

        int loggedRows = 0;

        for (int s = 0; s < splitters.Length; s++)
        {
            Entity orchestrator = splitters[s];

            if (SmartSplitterDebugSettings.OnlyLogLikelySplitters &&
                !SmartSplitterDebugUtility.LooksLikeSplitter(EntityManager, orchestrator, moverLookup))
            {
                continue;
            }

            if (!TryBuildTopology(
                    orchestrator,
                    moverLookup,
                    moverEntities,
                    movers,
                    out SplitterHandoffTopology topology))
            {
                continue;
            }

            Debug.Log(
                $"[SmartSplitterDebug][MoveeHandoffProbe] TOPOLOGY orchestrator={orchestrator} " +
                $"center={Format(topology.Center)} back={Format(topology.Back)} forward={Format(topology.Forward)} " +
                $"inputMover={topology.InputMover} inputPath={Path(topology.InputMoverData)} " +
                $"forwardMover={topology.ForwardMover} forwardPath={Path(topology.ForwardMoverData)} " +
                $"leftMover={topology.LeftMover} leftPath={Path(topology.LeftMoverData)} " +
                $"rightMover={topology.RightMover} rightPath={Path(topology.RightMoverData)} " +
                $"{SmartSplitterDebugUtility.BuildSmartSplitterStateText(EntityManager, orchestrator)}");

            for (int i = 0; i < droppedEntities.Length; i++)
            {
                if (droppedObjects[i].objectID != ObjectID.DroppedItem)
                {
                    continue;
                }

                Entity dropped = droppedEntities[i];

                if (!EntityManager.HasBuffer<ContainedObjectsBuffer>(dropped))
                {
                    continue;
                }

                DynamicBuffer<ContainedObjectsBuffer> contained = EntityManager.GetBuffer<ContainedObjectsBuffer>(dropped);
                if (contained.Length == 0)
                {
                    continue;
                }

                ObjectDataCD inner = contained[0].objectData;

                if (SmartSplitterDebugSettings.MoveeHandoffOnlyUnmatchedItems &&
                    (inner.objectID == ObjectID.WallDirtBlock || inner.objectID == ObjectID.WallTurfBlock))
                {
                    continue;
                }

                float itemX = droppedTransforms[i].Position.x;
                float itemY = droppedTransforms[i].Position.z;

                if (!IsInsideProbeCorridor(itemX, itemY, topology))
                {
                    continue;
                }

                Entity nearestMovee = Entity.Null;
                MoveeCD nearestMoveeData = default;
                float nearestMoveeDistance = float.MaxValue;

                FindNearestMovee(
                    itemX,
                    itemY,
                    moveeEntities,
                    movees,
                    ref nearestMovee,
                    ref nearestMoveeData,
                    ref nearestMoveeDistance);

                string phase = ClassifyPhase(itemX, itemY, topology);
                string nearestMoverText = BuildNearestMoverText(itemX, itemY, moverEntities, movers, topology);

                Debug.Log(
                    $"[SmartSplitterDebug][MoveeHandoffProbe] ITEM orchestrator={orchestrator} dropped={dropped} " +
                    $"item={inner.objectID}/{inner.variation} amount={inner.amount} " +
                    $"itemPos=({itemX:0.000},{itemY:0.000}) phase={phase} " +
                    $"backProgress={Progress(itemX, itemY, topology.Back, topology.Center):0.000} " +
                    $"centerForwardProgress={Progress(itemX, itemY, topology.Center, topology.Forward):0.000} " +
                    $"forwardBeltProgress={Progress(itemX, itemY, topology.Forward, topology.ForwardNext):0.000} " +
                    $"nearestMovee={nearestMovee} moveeDistance={nearestMoveeDistance:0.000} " +
                    $"moveePos={MoveePositionText(nearestMovee, nearestMoveeData)} " +
                    $"moveeFields={BuildMoveeFieldDump(nearestMoveeData)} " +
                    $"moveeComponents={BuildComponentList(nearestMovee)} " +
                    $"topologyMoverTimers={BuildTopologyMoverTimerText(topology)} " +
                    $"{nearestMoverText}");

        loggedRows++;
                if (loggedRows >= SmartSplitterDebugSettings.MaxRowsPerProbe)
                {
                    Debug.Log("[SmartSplitterDebug][MoveeHandoffProbe] stopped at row limit.");
                    return;
                }
            }
        }
    }

  private static string BuildMoveeFieldDump(MoveeCD movee)
  {
    return
        $"position={movee.position} " +
        $"target={movee.target} " +
        $"moveTimer={movee.moveTimer}";
  }

  private string BuildTopologyMoverTimerText(SplitterHandoffTopology topology)
  {
    return
        $"input={BuildMoverTimerText(topology.InputMover)} " +
        $"forward={BuildMoverTimerText(topology.ForwardMover)} " +
        $"left={BuildMoverTimerText(topology.LeftMover)} " +
        $"right={BuildMoverTimerText(topology.RightMover)}";
  }

  private string BuildMoverTimerText(Entity mover)
  {
    if (mover == Entity.Null || !EntityManager.Exists(mover))
    {
      return "none";
    }

    if (!EntityManager.HasComponent<MoverTimerCD>(mover))
    {
      return $"{mover}:noTimer";
    }

    MoverTimerCD timer = EntityManager.GetComponentData<MoverTimerCD>(mover);

    return $"{mover}:timer={timer.timer}";
  }

  private string BuildComponentList(Entity entity)
  {
    if (entity == Entity.Null || !EntityManager.Exists(entity))
    {
      return "none";
    }

    using NativeArray<ComponentType> types =
        EntityManager.GetComponentTypes(entity, Allocator.Temp);

    string result = string.Empty;

    for (int i = 0; i < types.Length; i++)
    {
      if (i > 0)
      {
        result += "|";
      }

      result += types[i].ToString();
    }

    return result;
  }

  private bool TryBuildTopology(
        Entity orchestrator,
        ComponentLookup<MoverCD> moverLookup,
        NativeArray<Entity> moverEntities,
        NativeArray<MoverCD> movers,
        out SplitterHandoffTopology topology)
    {
        topology = default;

        if (!EntityManager.Exists(orchestrator) ||
            !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
        {
            return false;
        }

        DynamicBuffer<MoversWithSharedStateBuffer> buffer =
            EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

        if (buffer.Length < 2 ||
            !moverLookup.HasComponent(buffer[0].moverEntity) ||
            !moverLookup.HasComponent(buffer[1].moverEntity))
        {
            return false;
        }

        MoverCD output0 = moverLookup[buffer[0].moverEntity];
        MoverCD output1 = moverLookup[buffer[1].moverEntity];

        int2 center = output0.start;

        Entity leftMover = buffer[0].moverEntity;
        Entity rightMover = buffer[1].moverEntity;
        MoverCD leftMoverData = output0;
        MoverCD rightMoverData = output1;

        if (output0.stop.x > output1.stop.x ||
            (output0.stop.x == output1.stop.x && output0.stop.y > output1.stop.y))
        {
            leftMover = buffer[1].moverEntity;
            rightMover = buffer[0].moverEntity;
            leftMoverData = output1;
            rightMoverData = output0;
        }

        Entity inputMover = Entity.Null;
        MoverCD inputMoverData = default;

        for (int i = 0; i < moverEntities.Length; i++)
        {
            MoverCD mover = movers[i];
            if (mover.moverOrchestratorEntity == orchestrator)
            {
                continue;
            }

            if (mover.stop.x == center.x && mover.stop.y == center.y)
            {
                inputMover = moverEntities[i];
                inputMoverData = mover;
                break;
            }
        }

        if (inputMover == Entity.Null)
        {
            return false;
        }

        int2 inputDirection = new int2(center.x - inputMoverData.start.x, center.y - inputMoverData.start.y);
        inputDirection = NormalizeCardinal(inputDirection);

        int2 back = new int2(center.x - inputDirection.x, center.y - inputDirection.y);
        int2 forward = new int2(center.x + inputDirection.x, center.y + inputDirection.y);
        int2 forwardNext = new int2(forward.x + inputDirection.x, forward.y + inputDirection.y);

        Entity forwardMover = Entity.Null;
        MoverCD forwardMoverData = default;

        for (int i = 0; i < moverEntities.Length; i++)
        {
            MoverCD mover = movers[i];
            if (mover.start.x == forward.x && mover.start.y == forward.y)
            {
                int2 dir = NormalizeCardinal(new int2(mover.stop.x - mover.start.x, mover.stop.y - mover.start.y));
                if (dir.x == inputDirection.x && dir.y == inputDirection.y)
                {
                    forwardMover = moverEntities[i];
                    forwardMoverData = mover;
                    forwardNext = mover.stop;
                    break;
                }
            }
        }

        topology = new SplitterHandoffTopology
        {
            Center = center,
            Back = back,
            Forward = forward,
            ForwardNext = forwardNext,
            Direction = inputDirection,
            InputMover = inputMover,
            InputMoverData = inputMoverData,
            ForwardMover = forwardMover,
            ForwardMoverData = forwardMoverData,
            LeftMover = leftMover,
            LeftMoverData = leftMoverData,
            RightMover = rightMover,
            RightMoverData = rightMoverData
        };

        return true;
    }

    private static void FindNearestMovee(
        float itemX,
        float itemY,
        NativeArray<Entity> moveeEntities,
        NativeArray<MoveeCD> movees,
        ref Entity nearestMovee,
        ref MoveeCD nearestMoveeData,
        ref float nearestMoveeDistance)
    {
        for (int i = 0; i < moveeEntities.Length; i++)
        {
            MoveeCD movee = movees[i];
            float distance = SmartSplitterDebugUtility.Distance2D(itemX, itemY, movee.position.x, movee.position.y);
            if (distance < nearestMoveeDistance)
            {
                nearestMovee = moveeEntities[i];
                nearestMoveeData = movee;
                nearestMoveeDistance = distance;
            }
        }
    }

    private static bool IsInsideProbeCorridor(float x, float y, SplitterHandoffTopology topology)
    {
        float distanceToCenter = SmartSplitterDebugUtility.Distance2D(x, y, topology.Center.x, topology.Center.y);
        float distanceToBack = SmartSplitterDebugUtility.Distance2D(x, y, topology.Back.x, topology.Back.y);
        float distanceToForward = SmartSplitterDebugUtility.Distance2D(x, y, topology.Forward.x, topology.Forward.y);
        float distanceToForwardNext = SmartSplitterDebugUtility.Distance2D(x, y, topology.ForwardNext.x, topology.ForwardNext.y);

        return distanceToCenter <= SmartSplitterDebugSettings.MoveeHandoffProbeRadius ||
               distanceToBack <= SmartSplitterDebugSettings.MoveeHandoffProbeRadius ||
               distanceToForward <= SmartSplitterDebugSettings.MoveeHandoffProbeRadius ||
               distanceToForwardNext <= SmartSplitterDebugSettings.MoveeHandoffProbeRadius;
    }

    private static string BuildNearestMoverText(
        float x,
        float y,
        NativeArray<Entity> moverEntities,
        NativeArray<MoverCD> movers,
        SplitterHandoffTopology topology)
    {
        Entity nearest = Entity.Null;
        MoverCD nearestMover = default;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < moverEntities.Length; i++)
        {
            MoverCD mover = movers[i];
            float distanceToStart = SmartSplitterDebugUtility.Distance2D(x, y, mover.start.x, mover.start.y);
            float distanceToStop = SmartSplitterDebugUtility.Distance2D(x, y, mover.stop.x, mover.stop.y);
            float distance = Mathf.Min(distanceToStart, distanceToStop);

            if (distance < nearestDistance)
            {
                nearest = moverEntities[i];
                nearestMover = mover;
                nearestDistance = distance;
            }
        }

        string role = "unknown";
        if (nearest == topology.InputMover) role = "inputMover";
        else if (nearest == topology.ForwardMover) role = "forwardMover";
        else if (nearest == topology.LeftMover) role = "leftOutputMover";
        else if (nearest == topology.RightMover) role = "rightOutputMover";

        return $"nearestMover={nearest} nearestMoverRole={role} nearestMoverDistance={nearestDistance:0.000} nearestMoverPath={Path(nearestMover)}";
    }

    private static string ClassifyPhase(float x, float y, SplitterHandoffTopology topology)
    {
        float backToCenter = Progress(x, y, topology.Back, topology.Center);
        float centerToForward = Progress(x, y, topology.Center, topology.Forward);
        float forwardToNext = Progress(x, y, topology.Forward, topology.ForwardNext);

        if (backToCenter >= -0.25f && backToCenter < 0.85f)
        {
            return "input_to_center";
        }

        if (centerToForward >= -0.25f && centerToForward < 0.85f)
        {
            return "center_to_forward";
        }

        if (forwardToNext >= -0.25f && forwardToNext < 1.25f)
        {
            return "forward_belt";
        }

        return "nearby";
    }

    private static float Progress(float x, float y, int2 start, int2 stop)
    {
        float dx = stop.x - start.x;
        float dy = stop.y - start.y;
        float lengthSq = dx * dx + dy * dy;
        if (lengthSq <= 0.0001f)
        {
            return 0f;
        }

        return ((x - start.x) * dx + (y - start.y) * dy) / lengthSq;
    }

    private static int2 NormalizeCardinal(int2 value)
    {
        if (math.abs(value.x) >= math.abs(value.y))
        {
            return new int2(value.x >= 0 ? 1 : -1, 0);
        }

        return new int2(0, value.y >= 0 ? 1 : -1);
    }

    private static string MoveePositionText(Entity movee, MoveeCD data)
    {
        if (movee == Entity.Null)
        {
            return "none";
        }

        return $"({data.position.x:0.000},{data.position.y:0.000}) raw={data.position}";
    }

    private static string Path(MoverCD mover)
    {
        return $"{mover.start}->{mover.stop} splits={mover.splitsIntoOnMove} idx={mover.indexInOrchestrator}";
    }

    private static string Format(int2 value)
    {
        return $"({value.x},{value.y})";
    }

    private struct SplitterHandoffTopology
    {
        public int2 Center;
        public int2 Back;
        public int2 Forward;
        public int2 ForwardNext;
        public int2 Direction;

        public Entity InputMover;
        public MoverCD InputMoverData;

        public Entity ForwardMover;
        public MoverCD ForwardMoverData;

        public Entity LeftMover;
        public MoverCD LeftMoverData;

        public Entity RightMover;
        public MoverCD RightMoverData;
    }
}
