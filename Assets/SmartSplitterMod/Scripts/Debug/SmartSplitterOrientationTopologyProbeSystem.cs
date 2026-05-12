using Pug.Automation;
using Pug.Automation.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterOrientationTopologyProbeSystem : SystemBase
{
    private double _nextLogTime;
    private EntityQuery _splitterQuery;
    private EntityQuery _moverQuery;

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

        RequireForUpdate(_splitterQuery);
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableOrientationTopologyProbe)
        {
            return;
        }

        if (!SmartSplitterDebugSettings.ShouldRunProbe(World.Time.ElapsedTime, ref _nextLogTime, 1.50d))
        {
            return;
        }

        ComponentLookup<MoverCD> moverLookup = GetComponentLookup<MoverCD>(true);

        using NativeArray<Entity> splitters = _splitterQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<Entity> moverEntities = _moverQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<MoverCD> movers = _moverQuery.ToComponentDataArray<MoverCD>(Allocator.Temp);

        int loggedSplitters = 0;

        for (int i = 0; i < splitters.Length; i++)
        {
            Entity orchestrator = splitters[i];

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
                Debug.Log($"[SmartSplitterDebug][OrientationTopologyProbe] SKIP orchestrator={orchestrator} reason=could-not-infer-topology");
                continue;
            }

            Debug.Log(
                $"[SmartSplitterDebug][OrientationTopologyProbe] SPLITTER orchestrator={orchestrator} " +
                $"center={Format(topology.Center)} " +
                $"leftMover={topology.LeftMover} leftStop={Format(topology.LeftStop)} leftDir={Format(topology.LeftDirection)} " +
                $"rightMover={topology.RightMover} rightStop={Format(topology.RightStop)} rightDir={Format(topology.RightDirection)} " +
                SmartSplitterDebugUtility.BuildMoverOrchestratorText(EntityManager, "orchestrator", orchestrator) + " " +
                SmartSplitterDebugUtility.BuildSmartSplitterStateText(EntityManager, orchestrator)
            );

            LogCardinalLaneSummary(topology, moverEntities, movers, orchestrator);
            LogNearbyMovers(topology, moverEntities, movers, orchestrator);

            loggedSplitters++;

            if (loggedSplitters >= SmartSplitterDebugSettings.MaxRowsPerProbe)
            {
                Debug.Log("[SmartSplitterDebug][OrientationTopologyProbe] stopped at splitter row limit.");
                break;
            }
        }
    }

    private void LogCardinalLaneSummary(
        SplitterTopology topology,
        NativeArray<Entity> moverEntities,
        NativeArray<MoverCD> movers,
        Entity orchestrator)
    {
        int2 north = new int2(topology.Center.x, topology.Center.y + 1);
        int2 south = new int2(topology.Center.x, topology.Center.y - 1);
        int2 east = new int2(topology.Center.x + 1, topology.Center.y);
        int2 west = new int2(topology.Center.x - 1, topology.Center.y);

        Debug.Log(
            $"[SmartSplitterDebug][OrientationTopologyProbe] CARDINALS orchestrator={orchestrator} " +
            $"north={Format(north)} {BuildLaneTouchSummary(north, topology.Center, moverEntities, movers, orchestrator)} | " +
            $"south={Format(south)} {BuildLaneTouchSummary(south, topology.Center, moverEntities, movers, orchestrator)} | " +
            $"east={Format(east)} {BuildLaneTouchSummary(east, topology.Center, moverEntities, movers, orchestrator)} | " +
            $"west={Format(west)} {BuildLaneTouchSummary(west, topology.Center, moverEntities, movers, orchestrator)}"
        );
    }

    private string BuildLaneTouchSummary(
        int2 laneTile,
        int2 center,
        NativeArray<Entity> moverEntities,
        NativeArray<MoverCD> movers,
        Entity orchestrator)
    {
        int towardCenter = 0;
        int awayFromCenter = 0;
        int sameOrchestrator = 0;
        string examples = string.Empty;

        for (int i = 0; i < movers.Length; i++)
        {
            MoverCD mover = movers[i];

            bool laneToCenter = Equals(mover.start, laneTile) && Equals(mover.stop, center);
            bool centerToLane = Equals(mover.start, center) && Equals(mover.stop, laneTile);

            if (!laneToCenter && !centerToLane)
            {
                continue;
            }

            if (laneToCenter)
            {
                towardCenter++;
            }

            if (centerToLane)
            {
                awayFromCenter++;
            }

            if (mover.moverOrchestratorEntity == orchestrator)
            {
                sameOrchestrator++;
            }

            if (examples.Length < 180)
            {
                examples += $" entity={moverEntities[i]} {Format(mover.start)}->{Format(mover.stop)} splits={mover.splitsIntoOnMove} idx={mover.indexInOrchestrator};";
            }
        }

        return $"towardCenter={towardCenter} awayFromCenter={awayFromCenter} sameOrchestrator={sameOrchestrator}{examples}";
    }

    private void LogNearbyMovers(
        SplitterTopology topology,
        NativeArray<Entity> moverEntities,
        NativeArray<MoverCD> movers,
        Entity orchestrator)
    {
        int logged = 0;
        int nearbyCount = 0;

        for (int i = 0; i < moverEntities.Length; i++)
        {
            Entity moverEntity = moverEntities[i];
            MoverCD mover = movers[i];

            float minDistance = Mathf.Min(
                SmartSplitterDebugUtility.Distance2D(mover.start.x, mover.start.y, topology.Center.x, topology.Center.y),
                SmartSplitterDebugUtility.Distance2D(mover.stop.x, mover.stop.y, topology.Center.x, topology.Center.y));

            bool inBuffer = moverEntity == topology.LeftMover || moverEntity == topology.RightMover;
            bool sameOrchestrator = mover.moverOrchestratorEntity == orchestrator;
            bool touchesCenter = Equals(mover.start, topology.Center) || Equals(mover.stop, topology.Center);
            bool nearby = minDistance <= SmartSplitterDebugSettings.OrientationTopologyProbeRadius;

            if (!nearby && !inBuffer && !sameOrchestrator && !touchesCenter)
            {
                continue;
            }

            nearbyCount++;

            string classification = ClassifyMover(topology, moverEntity, mover, orchestrator);

            Debug.Log(
                $"[SmartSplitterDebug][OrientationTopologyProbe] MOVER orchestrator={orchestrator} mover={moverEntity} " +
                $"classification={classification} minDistance={minDistance:0.00} " +
                SmartSplitterDebugUtility.BuildMoverText(EntityManager, moverEntity) + " " +
                SmartSplitterDebugUtility.BuildMoverOrchestratorText(EntityManager, "mover", moverEntity)
            );

            logged++;

            if (logged >= SmartSplitterDebugSettings.MaxRowsPerProbe)
            {
                Debug.Log($"[SmartSplitterDebug][OrientationTopologyProbe] nearby mover log stopped at row limit. nearbyCountSoFar={nearbyCount}");
                return;
            }
        }

        Debug.Log($"[SmartSplitterDebug][OrientationTopologyProbe] nearbyMoverCount={nearbyCount} orchestrator={orchestrator}");
    }

    private static string ClassifyMover(
        SplitterTopology topology,
        Entity moverEntity,
        MoverCD mover,
        Entity orchestrator)
    {
        bool inBuffer = moverEntity == topology.LeftMover || moverEntity == topology.RightMover;
        bool sameOrchestrator = mover.moverOrchestratorEntity == orchestrator;
        bool startsAtCenter = Equals(mover.start, topology.Center);
        bool stopsAtCenter = Equals(mover.stop, topology.Center);
        bool isKnownSideOutput = inBuffer && startsAtCenter;
        bool candidateInputToCenter = stopsAtCenter && !inBuffer;
        bool candidateExtraOutputFromCenter = startsAtCenter && !inBuffer;
        bool candidateStraightThrough = IsStraightThroughCandidate(mover, topology.Center);

        return
            $"inBuffer={inBuffer},sameOrchestrator={sameOrchestrator}," +
            $"knownSideOutput={isKnownSideOutput},candidateInputToCenter={candidateInputToCenter}," +
            $"candidateExtraOutputFromCenter={candidateExtraOutputFromCenter}," +
            $"candidateStraightThrough={candidateStraightThrough}";
    }

    private static bool IsStraightThroughCandidate(MoverCD mover, int2 center)
    {
        if (Equals(mover.start, center) || Equals(mover.stop, center))
        {
            return false;
        }

        bool sameXAndCrossesCenter =
            mover.start.x == center.x &&
            mover.stop.x == center.x &&
            ((mover.start.y < center.y && mover.stop.y > center.y) ||
             (mover.start.y > center.y && mover.stop.y < center.y));

        bool sameYAndCrossesCenter =
            mover.start.y == center.y &&
            mover.stop.y == center.y &&
            ((mover.start.x < center.x && mover.stop.x > center.x) ||
             (mover.start.x > center.x && mover.stop.x < center.x));

        return sameXAndCrossesCenter || sameYAndCrossesCenter;
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
            RightStop = rightMoverData.stop,
            LeftDirection = new int2(leftMoverData.stop.x - leftMoverData.start.x, leftMoverData.stop.y - leftMoverData.start.y),
            RightDirection = new int2(rightMoverData.stop.x - rightMoverData.start.x, rightMoverData.stop.y - rightMoverData.start.y)
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

    private struct SplitterTopology
    {
        public int2 Center;
        public Entity LeftMover;
        public Entity RightMover;
        public int2 LeftStop;
        public int2 RightStop;
        public int2 LeftDirection;
        public int2 RightDirection;
    }
}
