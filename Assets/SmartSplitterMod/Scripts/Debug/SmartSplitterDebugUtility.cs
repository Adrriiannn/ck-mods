using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public static class SmartSplitterDebugUtility
{
    public static bool LooksLikeSplitter(
        EntityManager entityManager,
        Entity entity,
        ComponentLookup<MoverCD> moverLookup)
    {
        if (!entityManager.Exists(entity) ||
            !entityManager.HasBuffer<MoversWithSharedStateBuffer>(entity))
        {
            return false;
        }

        DynamicBuffer<MoversWithSharedStateBuffer> buffer =
            entityManager.GetBuffer<MoversWithSharedStateBuffer>(entity);

        if (buffer.Length != 2)
        {
            return false;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            Entity moverEntity = buffer[i].moverEntity;

            if (!moverLookup.HasComponent(moverEntity))
            {
                return false;
            }

            MoverCD mover = moverLookup[moverEntity];

            if (mover.splitsIntoOnMove != 2 && mover.splitsIntoOnMove != 1)
            {
                return false;
            }
        }

        return true;
    }

    public static string BuildMoverText(EntityManager entityManager, Entity moverEntity)
    {
        if (!entityManager.Exists(moverEntity))
        {
            return "moverEntity=missing";
        }

        if (!entityManager.HasComponent<MoverCD>(moverEntity))
        {
            return "MoverCD=missing";
        }

        MoverCD mover = entityManager.GetComponentData<MoverCD>(moverEntity);
        string timerText = "MoverTimerCD=missing";

        if (entityManager.HasComponent<MoverTimerCD>(moverEntity))
        {
            MoverTimerCD timer = entityManager.GetComponentData<MoverTimerCD>(moverEntity);
            timerText = $"timer={timer.timer}";
        }

        return
            $"start={mover.start} stop={mover.stop} " +
            $"moveTime={mover.moveTime} cooldownTime={mover.cooldownTime} " +
            $"inventoryEntity={mover.inventoryEntity} " +
            $"moverOrchestratorEntity={mover.moverOrchestratorEntity} " +
            $"splitsIntoOnMove={mover.splitsIntoOnMove} " +
            $"indexInOrchestrator={mover.indexInOrchestrator} " +
            $"cycleEnabledMoverAfterActivation={mover.cycleEnabledMoverAfterActivation} " +
            $"enableAllMoversAfterActivation={mover.enableAllMoversAfterActivation} " +
            $"allowPickupFromInventories={mover.allowPickupFromInventories} " +
            timerText;
    }

    public static string BuildMoverOrchestratorText(EntityManager entityManager, string label, Entity entity)
    {
        if (!entityManager.Exists(entity))
        {
            return $"{label}.MoverOrchestratorCD=entity-missing";
        }

        if (!entityManager.HasComponent<MoverOrchestratorCD>(entity))
        {
            return $"{label}.MoverOrchestratorCD=missing";
        }

        MoverOrchestratorCD data = entityManager.GetComponentData<MoverOrchestratorCD>(entity);

        return
            $"{label}.enabledMoverIndex={data.enabledMoverIndex} " +
            $"{label}.nextMoverCycleIncrement={data.nextMoverCycleIncrement}";
    }

    public static string BuildSmartSplitterStateText(EntityManager entityManager, Entity orchestrator)
    {
        string tagText = entityManager.HasComponent<SmartSplitterTag>(orchestrator)
            ? "hasSmartSplitterTag=True"
            : "hasSmartSplitterTag=False";

        string configText = "SmartSplitterConfigCD=missing";

        if (entityManager.HasComponent<SmartSplitterConfigCD>(orchestrator))
        {
            SmartSplitterConfigCD config = entityManager.GetComponentData<SmartSplitterConfigCD>(orchestrator);

            configText =
                $"config.enabled={config.Enabled} " +
                $"leftFilter={config.LeftFilterObject}/{config.LeftFilterVariation} " +
                $"rightFilter={config.RightFilterObject}/{config.RightFilterVariation}";
        }

        string routeText = "SmartSplitterArmedRouteCD=missing";

        if (entityManager.HasComponent<SmartSplitterArmedRouteCD>(orchestrator))
        {
            SmartSplitterArmedRouteCD route = entityManager.GetComponentData<SmartSplitterArmedRouteCD>(orchestrator);

            routeText =
                $"armed.has={route.HasArmedRoute} " +
                $"armed.decision={route.Decision} " +
                $"armed.item={route.ItemObject}/{route.ItemVariation} " +
                $"armed.amount={route.ItemAmount} " +
                $"armed.expiresAt={route.ExpiresAt:0.000}";
        }

        string originalsText = "SmartSplitterOriginalOutputsCD=missing";

        if (entityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
        {
            SmartSplitterOriginalOutputsCD originals =
                entityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

            originalsText =
                $"originals.has={originals.HasOriginalOutputs} " +
                $"leftMover={originals.LeftMoverEntity} leftIndex={originals.LeftMoverIndex} " +
                $"leftDir={originals.LeftCachedDirection} leftStart={originals.LeftCachedStart} " +
                $"rightMover={originals.RightMoverEntity} rightIndex={originals.RightMoverIndex} " +
                $"rightDir={originals.RightCachedDirection} rightStart={originals.RightCachedStart}";
        }

        return $"{tagText} {configText} {routeText} {originalsText}";
    }

    public static bool IsDirtOrTurf(ObjectID objectID)
    {
        return objectID == ObjectID.WallDirtBlock || objectID == ObjectID.WallTurfBlock;
    }

    public static float Distance2D(float ax, float ay, float bx, float by)
    {
        float dx = ax - bx;
        float dy = ay - by;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
