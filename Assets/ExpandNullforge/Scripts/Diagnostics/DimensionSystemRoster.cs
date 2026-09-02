using System;
using Unity.Entities;

namespace ExpandNullforge.Diagnostics
{
    /// <summary>
    /// Every system this framework runs, which side of the game it belongs on, what stops working
    /// when it does not run, and what would be waiting on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A TABLE RATHER THAN A SWEEP. The audit cannot go looking for our systems: finding types
    /// by name needs <c>System.Reflection</c>, which Core Keeper's mod sandbox rejects outright
    /// (<c>Docs/SandboxDenyList.txt</c>, <c>namespace: System.Reflection.*</c>). So each row names
    /// its system with a closure over <c>GetExistingSystemManaged&lt;T&gt;</c>, which the compiler
    /// resolves. The cost of that is a table somebody has to keep up to date, and the answer to
    /// that is <c>DimensionSelfAuditTests</c>: it reads the shipped sources, counts the
    /// classes deriving from <c>SystemBase</c>, and fails when this table does not name all of
    /// them.
    /// </para>
    /// <para>
    /// WHAT "WORK" MEANS HERE. A system that has never run is only a problem when something was
    /// waiting on it. <see cref="Row.CountWork"/> answers "how many rows are registered that this
    /// system is the only consumer of", so the audit can tell an idle feature nobody used apart
    /// from a dead feature somebody paid for. Some registries expose no count at all; those rows
    /// return <see cref="WorkUnknown"/> and the audit says so rather than inventing a number.
    /// </para>
    /// <para>
    /// THE CAPABILITY TEXT IS WHAT A PLAYER WOULD CALL IT, because the line it lands in is read by
    /// somebody who is trying to work out why their creature stands still, not by somebody who
    /// knows what an <c>EntityQuery</c> is.
    /// </para>
    /// </remarks>
    internal static class DimensionSystemRoster
    {
        /// <summary>What <see cref="Row.CountWork"/> returns when nothing can count it.</summary>
        public const int WorkUnknown = -1;

        /// <summary>Which world a system is created into.</summary>
        internal enum Peer
        {
            /// <summary>Server only.</summary>
            Server,

            /// <summary>Client only.</summary>
            Client,

            /// <summary>Created into both, and reported separately for each.</summary>
            Both,
        }

        internal sealed class Row
        {
            public Row(
                string name,
                Peer where,
                string capability,
                Func<World, ComponentSystemBase> find,
                string workName,
                Func<int> countWork,
                bool countGrowsDuringPlay = false)
            {
                Name = name;
                Where = where;
                Capability = capability;
                Find = find;
                WorkName = workName;
                CountWork = countWork;
                CountGrowsDuringPlay = countGrowsDuringPlay;
            }

            /// <summary>The class name, as it appears in the source and in a stack trace.</summary>
            public string Name { get; private set; }

            /// <summary>Which world it is created into.</summary>
            public Peer Where { get; private set; }

            /// <summary>What stops working when it does not run, in a player's words.</summary>
            public string Capability { get; private set; }

            /// <summary>Looks it up in a world. Null when it is not there.</summary>
            public Func<World, ComponentSystemBase> Find { get; private set; }

            /// <summary>What holds the rows this system is there to act on.</summary>
            public string WorkName { get; private set; }

            /// <summary>How many such rows there are, or <see cref="WorkUnknown"/>.</summary>
            public Func<int> CountWork { get; private set; }

            /// <summary>
            /// Whether playing the game adds rows to this, as opposed to loading a content pack.
            /// </summary>
            /// <remarks>
            /// <para>
            /// READ BY ONE CHECK ONLY: the across-worlds growth comparison, which sees a registry
            /// hold more rows for the second world of a session than it held for the first and calls
            /// that a content pack registering itself twice. That inference is sound for a registry
            /// only the generated bootstrap writes, and false for one the game fills as somebody
            /// plays: load a world, walk into a dimension, quit to the menu and load another, and
            /// the second world's audit finds a count that grew for an entirely ordinary reason.
            /// </para>
            /// <para>
            /// It says nothing about the liveness pass, which is a different question — a count
            /// that only gameplay can raise is still exactly what "was anything waiting on this
            /// system" means there.
            /// </para>
            /// </remarks>
            public bool CountGrowsDuringPlay { get; private set; }

            /// <summary>Whether this row belongs to the world the audit is looking at.</summary>
            public bool AppliesTo(bool isClient)
            {
                if (Where == Peer.Both)
                {
                    return true;
                }

                return isClient ? Where == Peer.Client : Where == Peer.Server;
            }
        }

        /// <summary>
        /// How many rows a registry's read-only view holds.
        /// </summary>
        /// <remarks>
        /// IT ENUMERATES WHEN IT CANNOT ASK. Most of these views are backed by a list or a
        /// dictionary and answer <c>ICollection.Count</c> directly; a wrapper that does not would
        /// have come back as a null cast, and a null cast reads as zero, and zero here is the word
        /// "nothing is waiting on this" in a message somebody acts on. Enumerating a registry once
        /// per world load costs nothing and cannot be quietly wrong.
        /// </remarks>
        internal static int CountOf(System.Collections.IEnumerable rows)
        {
            if (rows == null)
            {
                return 0;
            }

            System.Collections.ICollection collection = rows as System.Collections.ICollection;
            if (collection != null)
            {
                return collection.Count;
            }

            int count = 0;
            foreach (object unused in rows)
            {
                count++;
            }

            return count;
        }

        private static readonly Row[] RowsValue =
        {
            // ---- portals and travel -------------------------------------------------------
            new Row(
                "DimensionPortalHydrationSystem",
                Peer.Server,
                "a portal spawned in the world knows where it goes",
                w => w.GetExistingSystemManaged<Portals.DimensionPortalHydrationSystem>(),
                "the portals registered with the dimension service",
                null),
            new Row(
                "DimensionPortalChargeSystem",
                Peer.Server,
                "a portal fills up while a player stands near it",
                w => w.GetExistingSystemManaged<Portals.DimensionPortalChargeSystem>(),
                "the portals registered with the dimension service",
                null),
            new Row(
                "DimensionPortalActivationSystem",
                Peer.Server,
                "stepping into a charged portal takes the player somewhere",
                w => w.GetExistingSystemManaged<Portals.DimensionPortalActivationSystem>(),
                "the portals registered with the dimension service",
                null),
            new Row(
                "DimensionPortalOfferingSystem",
                Peer.Both,
                "what a player puts into a portal's window is taken and counted",
                w => w.GetExistingSystemManaged<Portals.DimensionPortalOfferingSystem>(),
                "the portals registered with the dimension service",
                null),
            new Row(
                "DimensionItemPortalSpawnSystem",
                Peer.Server,
                "a portal placed from an item appears where the player used it",
                w => w.GetExistingSystemManaged<Portals.DimensionItemPortalSpawnSystem>(),
                "DimensionItemPortalRegistry",
                () => CountOf(
                    Portals.DimensionItemPortalRegistry.RegisteredItemNames)),
            new Row(
                "DimensionReturnPortalSpawnSystem",
                Peer.Server,
                "a way back appears where a player arrived",
                w => w.GetExistingSystemManaged<Portals.DimensionReturnPortalSpawnSystem>(),
                "DimensionReturnPortalSpawnRegistry",
                () => Portals.DimensionReturnPortalSpawnRegistry.Count),
            new Row(
                "DimensionPortalMapMarkerScopeSystem",
                Peer.Client,
                "a portal's pin only shows on the map of the dimension it is in",
                w => w.GetExistingSystemManaged<Portals.DimensionPortalMapMarkerScopeSystem>(),
                "the portals registered with the dimension service",
                null),
            new Row(
                "DimensionTravelServerRpcSystem",
                Peer.Server,
                "a travel request a client sends is answered",
                w => w.GetExistingSystemManaged<Networking.DimensionTravelServerRpcSystem>(),
                "nothing registers rows for this; it answers whatever clients send",
                null),
            new Row(
                "DimensionTravelClientRpcSystem",
                Peer.Client,
                "the reply to a travel request reaches the player who asked",
                w => w.GetExistingSystemManaged<Networking.DimensionTravelClientRpcSystem>(),
                "nothing registers rows for this; it receives whatever the server sends",
                null),
            new Row(
                "DimensionPlayerContextServerRpcSystem",
                Peer.Server,
                "the server tells a client which dimension it is standing in",
                w => w.GetExistingSystemManaged<Networking.DimensionPlayerContextServerRpcSystem>(),
                "nothing registers rows for this; it answers whatever clients send",
                null),
            new Row(
                "DimensionPlayerContextClientRpcSystem",
                Peer.Client,
                "the coordinate readout and the map know which dimension the player is in",
                w => w.GetExistingSystemManaged<Networking.DimensionPlayerContextClientRpcSystem>(),
                "nothing registers rows for this; it receives whatever the server sends",
                null),

            // ---- tilesets -----------------------------------------------------------------
            // Server, not both. Everything that touches a serialized submap is server-side, so the
            // client copies of these two could never do anything — and this table saying Both is
            // what would have had the audit certify them as working there.
            // NO COUNT ON PURPOSE: a count here is a false alarm. Its
            // RequireForUpdate is a query over SERIALIZED submaps, which exist only once the game
            // has read one back off disk; the tileset registry counts CONTENT. On a world that has
            // just been generated nothing has been serialized yet, so five seconds in this system
            // has correctly never run while the registry holds rows — and a liveness pass reading a
            // count would call that a problem and predict lost terrain. The failure it is meant to
            // catch is the
            // registration pass's business: CheckTileRescueBracket asks whether the system is in
            // SerializationSystemGroup at all, which is the thing that can actually be wrong.
            new Row(
                "DimensionCustomTileCaptureSystem",
                Peer.Server,
                "custom terrain survives a reload",
                w => w.GetExistingSystemManaged<Tilesets.DimensionCustomTileCaptureSystem>(),
                "nothing counts the submaps waiting to be read back; this runs when the game "
                    + "deserializes one, which a freshly generated world has not done yet",
                null),
            new Row(
                "DimensionCustomTileRestoreSystem",
                Peer.Server,
                "custom terrain is put back after a reload",
                w => w.GetExistingSystemManaged<Tilesets.DimensionCustomTileRestoreSystem>(),
                "DimensionTilesetRegistry",
                () => CountOf(
                    Tilesets.DimensionTilesetRegistry.All)),
            new Row(
                "DimensionHazardConditionSystem",
                Peer.Server,
                "standing on a hazardous custom tile actually hurts",
                w => w.GetExistingSystemManaged<Tilesets.DimensionHazardConditionSystem>(),
                "DimensionTilesetRegistry",
                () => CountOf(
                    Tilesets.DimensionTilesetRegistry.All)),

            // ---- biomes, zones, arenas ----------------------------------------------------
            new Row(
                "DimensionCurrentBiomeSystem",
                Peer.Both,
                "the biome title card, the biome music, the map name and biome discovery",
                w => w.GetExistingSystemManaged<Zones.DimensionCurrentBiomeSystem>(),
                "the biomes registered with the dimension service",
                null),
            new Row(
                "DimensionAmbientSpawnGateSystem",
                Peer.Server,
                "a named area can stop the game spawning its own creatures there",
                w => w.GetExistingSystemManaged<Zones.DimensionAmbientSpawnGateSystem>(),
                "the ambient spawn policies registered for zones",
                null),
            // THE ONE COUNT ON THIS TABLE THAT PLAYING RAISES. Every other registry here is filled
            // by the generated bootstrap as a pack declares itself; this one is filled by
            // DimensionScenePlacementPassProvider as a dimension is generated, which happens when a
            // player first travels into one. Nothing clears it between worlds, so the second world
            // of a session sees a bigger number than the first and the across-worlds comparison
            // read that as the pack having registered itself twice.
            new Row(
                "DimensionTriggeredTileSystem",
                Peer.Server,
                "traps and pressure plates placed inside a scene go off",
                w => w.GetExistingSystemManaged<Zones.DimensionTriggeredTileSystem>(),
                "DimensionTriggeredTileRegistry",
                () => Zones.DimensionTriggeredTileRegistry.ArmedCellCount,
                true),
            new Row(
                "DimensionArenaResetSystem",
                Peer.Server,
                "a failed arena attempt resets so the fight can be tried again",
                w => w.GetExistingSystemManaged<Arenas.DimensionArenaResetSystem>(),
                "the arenas registered with the dimension service",
                null),
            new Row(
                "DimensionArenaVictorySystem",
                Peer.Server,
                "clearing an arena is recorded and its reward opens",
                w => w.GetExistingSystemManaged<Arenas.DimensionArenaVictorySystem>(),
                "the arenas registered with the dimension service",
                null),

            // ---- creatures and bosses -----------------------------------------------------
            new Row(
                "DimensionBossPhaseSystem",
                Peer.Server,
                "a boss changes phase as its health drops",
                w => w.GetExistingSystemManaged<Zones.DimensionBossPhaseSystem>(),
                "DimensionBossPhaseRegistry",
                () => CountOf(
                    Zones.DimensionBossPhaseRegistry.BossNames)),
            new Row(
                "DimensionBossMarkerHydrationSystem",
                Peer.Both,
                "a boss's map pin carries its picture and its name",
                w => w.GetExistingSystemManaged<Creatures.DimensionBossMarkerHydrationSystem>(),
                "the boss map markers registered by generated content",
                null),
            new Row(
                "DimensionBossRespawnSystem",
                Peer.Server,
                "a defeated boss comes back after its timer",
                w => w.GetExistingSystemManaged<Creatures.DimensionBossRespawnSystem>(),
                "DimensionBossRespawnRegistry",
                () => CountOf(
                    Creatures.DimensionBossRespawnRegistry.All)),
            new Row(
                "DimensionSummonHydrationSystem",
                Peer.Server,
                "a summoning circle knows which creature it calls",
                w => w.GetExistingSystemManaged<Creatures.DimensionSummonHydrationSystem>(),
                "the summon links registered by generated content",
                null),
            new Row(
                "DimensionHoldsFireSystem",
                Peer.Server,
                "a creature set to fight back only when attacked holds its fire",
                w => w.GetExistingSystemManaged<Creatures.DimensionHoldsFireSystem>(),
                "the creatures generated with a defensive temperament",
                null),
            new Row(
                "DimensionSkillXpSystem",
                Peer.Server,
                "killing a mod's creature pays out the skill experience it is worth",
                w => w.GetExistingSystemManaged<Skills.DimensionSkillXpSystem>(),
                "DimensionSkillXpRegistry",
                () => Skills.DimensionSkillXpRegistry.Count),

            // ---- plants and food ----------------------------------------------------------
            new Row(
                "DimensionCropTierRollSystem",
                Peer.Server,
                "planting a seed can roll the rarer version of the crop",
                w => w.GetExistingSystemManaged<Plants.DimensionCropTierRollSystem>(),
                "the crops registered by generated content",
                null),
            new Row(
                "DimensionCropTierSproutSystem",
                Peer.Server,
                "the rarer version survives the moment the seed turns into a plant",
                w => w.GetExistingSystemManaged<Plants.DimensionCropTierSproutSystem>(),
                "the crops registered by generated content",
                null),
            new Row(
                "DimensionFoodIngredientHydrationSystem",
                Peer.Both,
                "the cooking pot accepts a mod's ingredients",
                w => w.GetExistingSystemManaged<Food.DimensionFoodIngredientHydrationSystem>(),
                "DimensionFoodIngredientRegistry",
                () => CountOf(
                    Food.DimensionFoodIngredientRegistry.All)),

            // ---- explosives and links -----------------------------------------------------
            new Row(
                "DimensionExplosiveHydrationSystem",
                Peer.Both,
                "a bomb whose blast is one of the mod's own objects has something to spawn",
                w => w.GetExistingSystemManaged<Explosives.DimensionExplosiveHydrationSystem>(),
                "DimensionExplosiveRegistry",
                () => CountOf(
                    Explosives.DimensionExplosiveRegistry.All)),
            // NO COUNT, FOR THE SAME REASON AS THE TILE CAPTURE ROW ABOVE. Its RequireForUpdate is
            // a query over live blasts, and the registry counts explosives a pack DECLARED. A
            // session where nobody has set a bomb off is the normal session, so counting the
            // registry here made every pack with one explosive in it produce a failure line five
            // seconds into every world. Its sibling, DimensionExplosiveHydrationSystem, keeps its
            // count: that one waits on the database rather than on a blast, so it ticks from the
            // first frame and a zero there really is a fault.
            new Row(
                "DimensionBlastFireSystem",
                Peer.Both,
                "a blast that is meant to leave fire behind leaves it",
                w => w.GetExistingSystemManaged<Explosives.DimensionBlastFireSystem>(),
                "nothing counts the blasts waiting to go off; a bomb has to explode before this "
                    + "has anything to do",
                null),
            new Row(
                "DimensionObjectLinkHydrationSystem",
                Peer.Both,
                "every reference in the mod's content that names one of the mod's own objects: "
                    + "the shot a bow fires, the chest a boss leaves, what a container turns into",
                w => w.GetExistingSystemManaged<Foundation.DimensionObjectLinkHydrationSystem>(),
                "DimensionObjectLinkRegistry",
                () => CountOf(
                    Foundation.DimensionObjectLinkRegistry.All)),
        };

        /// <summary>Every framework system, one row each.</summary>
        public static Row[] All
        {
            get { return RowsValue; }
        }
    }
}
