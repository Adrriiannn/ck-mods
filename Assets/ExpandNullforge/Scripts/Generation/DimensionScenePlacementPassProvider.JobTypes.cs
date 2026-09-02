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
    /// What the provider keeps about one planned placement while it works through them.
    /// </summary>
    public sealed partial class DimensionScenePlacementPassProvider
    {
        private enum PendingKind
        {
            // The order IS the placement order.
            Exact = 0,
            Preferred = 1,
            Free = 2
        }

        private enum PlacementOutcome
        {
            Placed,
            RetryNextTick,
            NoSpot
        }

        private sealed class PendingPlacement
        {
            public string SceneId;
            public string RegisteredSceneName;
            public DimensionScenePoolEntry Policy;
            public PendingKind Kind;

            /// <summary>The author's own ranking, higher first. Read by the placement order.</summary>
            public int Priority;

            public DimensionBounds AuthoredBounds;
            public bool Required;
            public bool HasSceneRecord;
            public int AreaNotLoadedRetries;
            public int RefusedSpots;
            public string LastError;
        }

        private struct PlacedFootprint
        {
            public int2 LocalPosition;
            public int Radius;
        }

        private sealed class FillCandidate
        {
            public DimensionScenePoolEntry Entry;

            /// <summary>A policy carrier for the shared spot-eligibility check.</summary>
            public PendingPlacement Probe;

            public readonly List<int2> PlacedAt = new List<int2>();
            public bool Exhausted;
            public bool EligibleThisRoll;
        }

        private sealed class PlacementJob
        {
            public readonly List<PendingPlacement> Pending = new List<PendingPlacement>();
            public readonly List<PlacedFootprint> Placed = new List<PlacedFootprint>();
            public readonly List<FillCandidate> FillPool = new List<FillCandidate>();
            public bool SeedResolved;
            public ulong Seed;
            public bool Planned;
            public int TotalPlanned;
            public int PlacedCount;
            public int FillRemaining = FillBudget;
            public int FillFailureStreak;
            public bool Completed;
            public bool Failed;
            public string CompletionMessage = string.Empty;
        }
    }
}
