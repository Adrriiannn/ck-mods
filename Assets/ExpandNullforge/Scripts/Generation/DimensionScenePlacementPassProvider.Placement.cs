using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Scenes;
using ExpandNullforge.Zones;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Stamping one scene down, and arming the triggers it brought.
    /// </summary>
    public sealed partial class DimensionScenePlacementPassProvider
    {
        private PlacementOutcome TryPlaceOne(
            DimensionGenerationContext context,
            DimensionBounds passBounds,
            PlacementJob job,
            PendingPlacement pending)
        {
            int2 localSpot;
            switch (pending.Kind)
            {
                case PendingKind.Exact:
                    // The anchor is the scene's CENTRE (scenes register an explicit middle
                    // pivot), so an authored rectangle is honoured by anchoring at its centre —
                    // the same floor((min+max)/2) the pivot itself uses, so a rectangle sized
                    // like the scene is filled edge to edge. A pool point is a 1x1 rectangle
                    // and comes out unchanged.
                    localSpot = new int2(
                        (pending.AuthoredBounds.Min.x + pending.AuthoredBounds.MaxExclusive.x - 1) >> 1,
                        (pending.AuthoredBounds.Min.y + pending.AuthoredBounds.MaxExclusive.y - 1) >> 1);
                    break;
                case PendingKind.Preferred:
                {
                    DimensionBounds search = Intersect(pending.AuthoredBounds, passBounds);
                    if (!TryFindSpot(job, search, pending, context.Dimension.Id, out localSpot))
                    {
                        pending.LastError = "no clear spot inside its preferred area.";
                        return PlacementOutcome.NoSpot;
                    }

                    break;
                }
                default:
                {
                    if (!TryFindSpot(job, passBounds, pending, context.Dimension.Id, out localSpot))
                    {
                        pending.LastError = "no clear spot in the generated area.";
                        return PlacementOutcome.NoSpot;
                    }

                    break;
                }
            }

            int2 absolute = context.Area.AbsoluteBounds.Min +
                            (localSpot - context.Area.LocalBounds.Min);
            uint instanceSeed = (uint)DimensionOverlayScatter.Hash(job.Seed, absolute, 1);

            DimensionScenePlacementResult result;
            string error;
            if (!DimensionScenePlacement.TryPlace(
                    context.ServerWorld,
                    pending.RegisteredSceneName,
                    absolute,
                    instanceSeed,
                    out result,
                    out error))
            {
                if (result == DimensionScenePlacementResult.AreaNotLoaded)
                {
                    pending.AreaNotLoadedRetries++;
                    if (pending.AreaNotLoadedRetries <= MaxAreaNotLoadedRetries)
                    {
                        return PlacementOutcome.RetryNextTick;
                    }

                    pending.LastError =
                        "its footprint never finished loading (" + MaxAreaNotLoadedRetries +
                        " ticks): " + error;
                    return PlacementOutcome.NoSpot;
                }

                // Refused on player builds. A free placement can look for other ground; an
                // authored position cannot — the author picked that exact spot.
                if (pending.Kind == PendingKind.Free && pending.RefusedSpots < 3)
                {
                    pending.RefusedSpots++;
                    job.Placed.Add(new PlacedFootprint
                    {
                        // Poison the refused spot so the retry does not sample it again.
                        LocalPosition = localSpot,
                        Radius = FootprintRadius(pending)
                    });
                    return PlacementOutcome.RetryNextTick;
                }

                pending.LastError = error;
                return PlacementOutcome.NoSpot;
            }

            job.Placed.Add(new PlacedFootprint
            {
                LocalPosition = localSpot,
                Radius = FootprintRadius(pending)
            });

            RegisterSceneTriggers(context.Dimension.Id, pending.RegisteredSceneName, absolute);
            RecordPlacement(context.Dimension.Id, pending, localSpot);
            return PlacementOutcome.Placed;
        }

        /// <summary>
        /// Arms a placed scene's triggered tiles, now that the scene finally has an anchor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the only moment the framework knows where a scene landed, which is why the
        /// registrations happen here rather than in the bootstrap: a trigger authored in
        /// scene-local coordinates becomes a set of world-coordinate cells only once
        /// <c>TryPlace</c> has succeeded. The stamp uses anchor + (tile − centre), so the same
        /// arithmetic maps each covered cell.
        /// </para>
        /// <para>
        /// One landing shares one trigger id across its whole patch, so a multi-tile plate has a
        /// single cooldown and a once-only trap really fires once — while two placed copies of
        /// the same scene stay independent traps.
        /// </para>
        /// <para>
        /// KNOWN LIMIT. A scene embedded in a generated dungeon room is stamped by Core Keeper's
        /// own machinery, which reports no position — those copies never pass through here, so
        /// their triggers stay silent.
        /// </para>
        /// </remarks>
        private static void RegisterSceneTriggers(
            string dimensionId,
            string registeredSceneName,
            int2 absoluteAnchor)
        {
            DimensionCustomSceneDefinition definition;
            if (string.IsNullOrEmpty(registeredSceneName) ||
                !DimensionCustomSceneRegistry.TryGet(registeredSceneName, out definition) ||
                definition.Triggers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < definition.Triggers.Count; i++)
            {
                DimensionSceneTrigger trigger = definition.Triggers[i];
                string triggerId = registeredSceneName + ":" + trigger.TriggerId + "@" +
                    absoluteAnchor.x + "," + absoluteAnchor.y;
                for (int y = trigger.LocalMin.y; y < trigger.LocalMaxExclusive.y; y++)
                {
                    for (int x = trigger.LocalMin.x; x < trigger.LocalMaxExclusive.x; x++)
                    {
                        int2 world = absoluteAnchor + (new int2(x, y) - definition.CenterPosition);
                        DimensionTriggeredTileRegistry.Register(new DimensionTriggeredTileDefinition(
                            triggerId,
                            dimensionId,
                            world,
                            trigger.Kind,
                            trigger.CarriedItemName,
                            trigger.Action,
                            trigger.ActionTarget,
                            trigger.Amount,
                            trigger.ConditionSeconds,
                            0f, // radius: unauthored on purpose — the system's own 2-tile default applies.
                            trigger.OnceOnly,
                            trigger.CooldownSeconds));
                    }
                }
            }
        }

        /// <summary>
        /// Re-arms the triggers of a scene an earlier run already stamped, recovering the anchor
        /// from the recorded rectangle.
        /// </summary>
        /// <remarks>
        /// The recorded bounds were built as anchor ± half, so the rectangle's centre — the same
        /// floor((min+max)/2) the stamp pivot uses — is the anchor that was used. Registration is
        /// idempotent per trigger id, so revisiting an area costs nothing but the loop.
        /// </remarks>
        private static void RearmPlacedSceneTriggers(
            DimensionSceneDefinition record,
            Dictionary<string, DimensionScenePoolEntry> poolById,
            int2 worldOffset,
            string dimensionId)
        {
            // A fill copy is recorded as "scene#N"; its registered tile data lives under the base id.
            string baseId = record.SceneId;
            int copyMarker = baseId.IndexOf('#');
            if (copyMarker >= 0)
            {
                baseId = baseId.Substring(0, copyMarker);
            }

            DimensionScenePoolEntry entry;
            string sceneName = poolById.TryGetValue(baseId, out entry)
                ? entry.RegisteredSceneName
                : baseId;

            int2 anchor = new int2(
                (record.LocalBounds.Min.x + record.LocalBounds.MaxExclusive.x - 1) >> 1,
                (record.LocalBounds.Min.y + record.LocalBounds.MaxExclusive.y - 1) >> 1);
            RegisterSceneTriggers(dimensionId, sceneName, anchor + worldOffset);
        }

        /// <summary>
        /// Marks the scene Ready in the runtime records, registering a record first when the
        /// placement came from the pool. Ready is what makes re-generation idempotent.
        /// </summary>
        private void RecordPlacement(string dimensionId, PendingPlacement pending, int2 localSpot)
        {
            DimensionOperationResult opResult;
            if (!pending.HasSceneRecord)
            {
                int2 half = pending.Policy != null
                    ? pending.Policy.FootprintSize / 2
                    : new int2(8, 8);
                DimensionSceneDefinition record = new DimensionSceneDefinition(
                    pending.SceneId,
                    pending.SceneId,
                    dimensionId,
                    new DimensionBounds(localSpot - half, localSpot + half + new int2(1, 1)),
                    pending.Policy != null && !string.IsNullOrEmpty(pending.Policy.BiomeId)
                        ? pending.Policy.BiomeId
                        : "scene",
                    pending.Policy != null ? pending.Policy.Priority : 0,
                    DimensionSceneState.Planned);
                service.TryRegisterScene(record, out opResult);
            }

            service.TrySetSceneState(
                pending.SceneId,
                DimensionSceneState.Ready,
                "scene-placement-pass",
                out opResult);
        }
    }
}
