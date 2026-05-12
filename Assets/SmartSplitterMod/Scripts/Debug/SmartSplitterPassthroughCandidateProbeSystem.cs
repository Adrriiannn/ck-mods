using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterPassthroughCandidateProbeSystem : SystemBase
{
    private double _nextLogTime;
    private EntityQuery _splitterQuery;
    private EntityQuery _moverQuery;
    private EntityQuery _droppedItemQuery;
    private EntityQuery _moveeQuery;

    protected override void OnCreate()
    {
        _splitterQuery = GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>());

        _moverQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<MoverCD>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });

        _droppedItemQuery = GetEntityQuery(
            ComponentType.ReadOnly<ObjectDataCD>(),
            ComponentType.ReadOnly<ContainedObjectsBuffer>(),
            ComponentType.ReadOnly<LocalTransform>());

        _moveeQuery = GetEntityQuery(ComponentType.ReadOnly<MoveeCD>());

        RequireForUpdate(_splitterQuery);
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnablePassthroughCandidateProbe)
        {
            return;
        }

        if (!SmartSplitterDebugSettings.ShouldRunProbe(World.Time.ElapsedTime, ref _nextLogTime, 0.50d))
        {
            return;
        }

        ComponentLookup<MoverCD> moverLookup = GetComponentLookup<MoverCD>(true);

        using NativeArray<Entity> splitters = _splitterQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<Entity> moverEntities = _moverQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<MoverCD> movers = _moverQuery.ToComponentDataArray<MoverCD>(Allocator.Temp);
        using NativeArray<Entity> droppedEntities = _droppedItemQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<ObjectDataCD> droppedOuterObjects = _droppedItemQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
        using NativeArray<LocalTransform> droppedTransforms = _droppedItemQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using NativeArray<Entity> moveeEntities = _moveeQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<MoveeCD> movees = _moveeQuery.ToComponentDataArray<MoveeCD>(Allocator.Temp);

        int loggedRows = 0;

        for (int s = 0; s < splitters.Length; s++)
        {
            Entity orchestrator = splitters[s];

            if (SmartSplitterDebugSettings.OnlyLogLikelySplitters &&
                !SmartSplitterDebugUtility.LooksLikeSplitter(EntityManager, orchestrator, moverLookup))
            {
                continue;
            }

            if (SmartSplitterDebugSettings.OnlyLogConfiguredSmartSplitters &&
                !EntityManager.HasComponent<SmartSplitterTag>(orchestrator))
            {
                continue;
            }

            if (!TryGetSplitterTopology(orchestrator, moverLookup, out SplitterTopology topology))
            {
                continue;
            }

            OrientationInfo orientation = InferOrientation(topology, moverEntities, movers);

            Debug.Log(
                $"[SmartSplitterDebug][PassthroughCandidateProbe] ORIENTATION orchestrator={orchestrator} " +
                $"center={Format(topology.Center)} left={Format(topology.LeftStop)} right={Format(topology.RightStop)} " +
                $"hasInput={orientation.HasInput} ambiguousInput={orientation.AmbiguousInput} " +
                $"back={FormatNullable(orientation.BackTile, orientation.HasInput)} forward={FormatNullable(orientation.ForwardTile, orientation.HasInput)} " +
                $"inputMover={orientation.InputMover} inputDir={FormatNullable(orientation.InputDirection, orientation.HasInput)} " +
                $"hasForwardConveyor={orientation.HasForwardConveyor} forwardMover={orientation.ForwardMover} " +
                $"forwardConveyor={FormatNullable(orientation.ForwardMoverStart, orientation.HasForwardConveyor)}->{FormatNullable(orientation.ForwardMoverStop, orientation.HasForwardConveyor)} " +
                SmartSplitterDebugUtility.BuildSmartSplitterStateText(EntityManager, orchestrator));

            for (int i = 0; i < droppedEntities.Length; i++)
            {
                if (droppedOuterObjects[i].objectID != ObjectID.DroppedItem)
                {
                    continue;
                }

                Entity dropped = droppedEntities[i];

                if (!EntityManager.Exists(dropped) ||
                    !EntityManager.HasBuffer<ContainedObjectsBuffer>(dropped))
                {
                    continue;
                }

                DynamicBuffer<ContainedObjectsBuffer> contained = EntityManager.GetBuffer<ContainedObjectsBuffer>(dropped);

                if (contained.Length == 0)
                {
                    continue;
                }

                var item = contained[0].objectData;
                float itemX = droppedTransforms[i].Position.x;
                float itemY = droppedTransforms[i].Position.z;
                float distanceToCenter = SmartSplitterDebugUtility.Distance2D(itemX, itemY, topology.Center.x, topology.Center.y);

                if (distanceToCenter > SmartSplitterDebugSettings.PassthroughCandidateProbeRadius)
                {
                    continue;
                }

                DecisionInfo decision = ClassifyDecision(orchestrator, item.objectID, item.variation);
                bool nearCapturePoint = distanceToCenter <= SmartSplitterDebugSettings.PassthroughCenterCaptureRadius;
                MoveeMatch moveeMatch = FindNearestMovee(itemX, itemY, moveeEntities, movees);

                Debug.Log(
                    $"[SmartSplitterDebug][PassthroughCandidateProbe] ITEM orchestrator={orchestrator} dropped={dropped} " +
                    $"item={item.objectID}/{item.variation} amount={item.amount} " +
                    $"pos=({itemX:0.00},{itemY:0.00}) center={Format(topology.Center)} distance={distanceToCenter:0.00} nearCapturePoint={nearCapturePoint} " +
                    $"decision={decision.Decision} reason={decision.Reason} matchesLeft={decision.MatchesLeft} matchesRight={decision.MatchesRight} " +
                    $"readyForForwardPrototype={decision.Decision == PassthroughDecision.ForwardPassthrough && orientation.HasInput && !orientation.AmbiguousInput && orientation.HasForwardConveyor && nearCapturePoint} " +
                    $"orientationReady={orientation.HasInput && !orientation.AmbiguousInput} forwardReady={orientation.HasForwardConveyor} " +
                    $"back={FormatNullable(orientation.BackTile, orientation.HasInput)} forward={FormatNullable(orientation.ForwardTile, orientation.HasInput)} " +
                    $"nearestMovee={moveeMatch.Entity} moveeDistance={moveeMatch.Distance:0.00} moveePos={moveeMatch.PositionText}");

                loggedRows++;

                if (loggedRows >= SmartSplitterDebugSettings.MaxRowsPerProbe)
                {
                    Debug.Log("[SmartSplitterDebug][PassthroughCandidateProbe] stopped at row limit.");
                    return;
                }
            }
        }
    }

    private DecisionInfo ClassifyDecision(Entity orchestrator, ObjectID objectID, int variation)
    {
        if (!EntityManager.Exists(orchestrator) ||
            !EntityManager.HasComponent<SmartSplitterConfigCD>(orchestrator))
        {
            return new DecisionInfo
            {
                Decision = PassthroughDecision.Unknown,
                Reason = "missing_config"
            };
        }

        SmartSplitterConfigCD config = EntityManager.GetComponentData<SmartSplitterConfigCD>(orchestrator);

        if (!config.Enabled)
        {
            return new DecisionInfo
            {
                Decision = PassthroughDecision.VanillaDisabled,
                Reason = "config_disabled"
            };
        }

        bool matchesLeft = MatchesFilter(objectID, variation, config.LeftFilterObject, config.LeftFilterVariation);
        bool matchesRight = MatchesFilter(objectID, variation, config.RightFilterObject, config.RightFilterVariation);

        PassthroughDecision decision;
        string reason;

        if (matchesLeft && matchesRight)
        {
            decision = PassthroughDecision.Both;
            reason = "both_filters_match";
        }
        else if (matchesLeft)
        {
            decision = PassthroughDecision.LeftOnly;
            reason = "left_filter_match";
        }
        else if (matchesRight)
        {
            decision = PassthroughDecision.RightOnly;
            reason = "right_filter_match";
        }
        else
        {
            decision = PassthroughDecision.ForwardPassthrough;
            reason = "no_side_filter_match";
        }

        return new DecisionInfo
        {
            Decision = decision,
            Reason = reason,
            MatchesLeft = matchesLeft,
            MatchesRight = matchesRight
        };
    }

    private static bool MatchesFilter(ObjectID itemObject, int itemVariation, ObjectID filterObject, int filterVariation)
    {
        return itemObject == filterObject && itemVariation == filterVariation;
    }

    private OrientationInfo InferOrientation(
        SplitterTopology topology,
        NativeArray<Entity> moverEntities,
        NativeArray<MoverCD> movers)
    {
        OrientationInfo info = new OrientationInfo();
        int inputCount = 0;

        for (int i = 0; i < movers.Length; i++)
        {
            Entity moverEntity = moverEntities[i];
            MoverCD mover = movers[i];

            if (moverEntity == topology.LeftMover || moverEntity == topology.RightMover)
            {
                continue;
            }

            if (!Equals(mover.stop, topology.Center))
            {
                continue;
            }

            inputCount++;

            if (!info.HasInput)
            {
                info.HasInput = true;
                info.InputMover = moverEntity;
                info.BackTile = mover.start;
                info.InputDirection = new int2(topology.Center.x - mover.start.x, topology.Center.y - mover.start.y);
                info.ForwardTile = new int2(topology.Center.x + info.InputDirection.x, topology.Center.y + info.InputDirection.y);
            }
        }

        info.AmbiguousInput = inputCount > 1;

        if (!info.HasInput || info.AmbiguousInput)
        {
            return info;
        }

        int2 expectedForwardStop = new int2(
            info.ForwardTile.x + info.InputDirection.x,
            info.ForwardTile.y + info.InputDirection.y);

        for (int i = 0; i < movers.Length; i++)
        {
            MoverCD mover = movers[i];

            if (Equals(mover.start, info.ForwardTile) && Equals(mover.stop, expectedForwardStop))
            {
                info.HasForwardConveyor = true;
                info.ForwardMover = moverEntities[i];
                info.ForwardMoverStart = mover.start;
                info.ForwardMoverStop = mover.stop;
                return info;
            }
        }

        return info;
    }

    private MoveeMatch FindNearestMovee(
        float itemX,
        float itemY,
        NativeArray<Entity> moveeEntities,
        NativeArray<MoveeCD> movees)
    {
        MoveeMatch best = new MoveeMatch
        {
            Entity = Entity.Null,
            Distance = 9999f,
            PositionText = "none"
        };

        for (int i = 0; i < movees.Length; i++)
        {
            MoveeCD movee = movees[i];
            float distance = SmartSplitterDebugUtility.Distance2D(itemX, itemY, movee.position.x, movee.position.y);

            if (distance >= best.Distance)
            {
                continue;
            }

            best.Entity = moveeEntities[i];
            best.Distance = distance;
            best.PositionText = movee.position.ToString();
        }

        return best;
    }

    private bool TryGetSplitterTopology(
        Entity orchestrator,
        ComponentLookup<MoverCD> moverLookup,
        out SplitterTopology topology)
    {
        topology = default;

        if (!EntityManager.Exists(orchestrator) ||
            !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
        {
            return false;
        }

        DynamicBuffer<MoversWithSharedStateBuffer> buffer =
            EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

        if (buffer.Length < 2)
        {
            return false;
        }

        MoversWithSharedStateBuffer entry0 = buffer[0];
        MoversWithSharedStateBuffer entry1 = buffer[1];

        if (!moverLookup.HasComponent(entry0.moverEntity) ||
            !moverLookup.HasComponent(entry1.moverEntity))
        {
            return false;
        }

        MoverCD mover0 = moverLookup[entry0.moverEntity];
        MoverCD mover1 = moverLookup[entry1.moverEntity];

        bool entry0IsLeft;

        if (mover0.stop.x != mover1.stop.x)
        {
            entry0IsLeft = mover0.stop.x < mover1.stop.x;
        }
        else
        {
            entry0IsLeft = mover0.stop.y < mover1.stop.y;
        }

        Entity leftMover = entry0IsLeft ? entry0.moverEntity : entry1.moverEntity;
        Entity rightMover = entry0IsLeft ? entry1.moverEntity : entry0.moverEntity;
        MoverCD leftMoverData = entry0IsLeft ? mover0 : mover1;
        MoverCD rightMoverData = entry0IsLeft ? mover1 : mover0;

        int2 center;

        if (Equals(leftMoverData.start, rightMoverData.start))
        {
            center = leftMoverData.start;
        }
        else
        {
            center = new int2(
                Mathf.RoundToInt((leftMoverData.stop.x + rightMoverData.stop.x) * 0.5f),
                Mathf.RoundToInt((leftMoverData.stop.y + rightMoverData.stop.y) * 0.5f));
        }

        topology = new SplitterTopology
        {
            Center = center,
            LeftMover = leftMover,
            RightMover = rightMover,
            LeftStop = leftMoverData.stop,
            RightStop = rightMoverData.stop
        };

        return true;
    }

    private static bool Equals(int2 a, int2 b)
    {
        return a.x == b.x && a.y == b.y;
    }

    private static string Format(int2 value)
    {
        return $"({value.x},{value.y})";
    }

    private static string FormatNullable(int2 value, bool hasValue)
    {
        return hasValue ? Format(value) : "none";
    }

    private struct SplitterTopology
    {
        public int2 Center;
        public Entity LeftMover;
        public Entity RightMover;
        public int2 LeftStop;
        public int2 RightStop;
    }

    private struct OrientationInfo
    {
        public bool HasInput;
        public bool AmbiguousInput;
        public Entity InputMover;
        public int2 BackTile;
        public int2 InputDirection;
        public int2 ForwardTile;
        public bool HasForwardConveyor;
        public Entity ForwardMover;
        public int2 ForwardMoverStart;
        public int2 ForwardMoverStop;
    }

    private struct DecisionInfo
    {
        public PassthroughDecision Decision;
        public string Reason;
        public bool MatchesLeft;
        public bool MatchesRight;
    }

    private struct MoveeMatch
    {
        public Entity Entity;
        public float Distance;
        public string PositionText;
    }

    private enum PassthroughDecision
    {
        Unknown,
        VanillaDisabled,
        LeftOnly,
        RightOnly,
        Both,
        ForwardPassthrough
    }
}
