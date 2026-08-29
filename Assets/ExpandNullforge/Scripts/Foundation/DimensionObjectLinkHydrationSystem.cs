using System;
using System.Collections.Generic;
using PugMod;
using Unity.Entities;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Points every deferred reference in a mod's content at the object it names, on the prefab the
    /// game reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE GENERATOR CANNOT BAKE THESE. The fields are raw <c>ObjectID</c> enums, and a mod's
    /// object ids do not exist until the game hands them out while it loads
    /// (<c>ck-db\Pug.ECS.Authoring\ObjectAuthoring.cs</c> asks <c>API.Authoring.GetObjectID</c> at
    /// conversion time). A reference to one of the GAME's own objects is a known number and IS baked
    /// at generate; a reference to one of the mod's own can only be resolved here.
    /// </para>
    /// <para>
    /// IT IS THE PREFAB ENTITY THAT MATTERS. Everything the game spawns is instantiated from the
    /// database's prefab entity, so writing the field there makes every copy come out correct — no
    /// per-instance patching and no race with whatever reads it.
    /// </para>
    /// <para>
    /// ONE SYSTEM FOR BOTH WORLDS, DELIBERATELY. Deciding per field which world needs it would be
    /// correct in principle and a permanent maintenance hazard in practice: the
    /// <c>[WorldSystemFilter]</c> flags and the <c>GetOrCreateSystemManaged</c> calls in
    /// <c>ExpandNullforgeModEntry</c> have to agree, or an instance is constructed in a world it
    /// never ticks in — silently, with the liveness test still green. The cost is asymmetric too.
    /// Writing a server-only field (a loot buffer) onto the client's own prefab copy is inert; the
    /// client never rolls it. FAILING to write a client-read field is visible — the recipe book, and
    /// a bow whose predicted shot disagrees with the one the server fires. So both worlds, always.
    /// </para>
    /// <para>
    /// RETRY, NEVER LATCH ON <c>None</c>. A name that has not resolved yet is not a mistake; mod
    /// registration order is not something any mod can depend on. Once a row settles it costs two
    /// integer comparisons a tick, and the row count is checked before the bank singleton is even
    /// asked for.
    /// </para>
    /// <para>
    /// AND THE COMPANION TO THAT RULE: AFTER A WHILE, SAY SO. Retrying forever in silence is how a
    /// creator ends up with a bow that fires nothing and no line anywhere explaining it —
    /// a reference to an object that was switched off, or removed, or misspelled in a hand-edited
    /// prefab, never arrives and never will. Every world has finished registering content long
    /// before <see cref="SettlingSeconds"/> have passed, so a row still unresolved at that point is
    /// reported once, naming the object, the thing it wanted and which of the two names never came.
    /// It keeps retrying afterwards — a later mod could still bring the name in — it just stops
    /// being quiet about it.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionObjectLinkHydrationSystem : SystemBase
    {
        /// <summary>
        /// How long a name gets to arrive before its absence is reported. Generous on purpose:
        /// content registration finishes in the first frames, and a slow disc or a large mod list
        /// must never turn into a false accusation.
        /// </summary>
        public const double SettlingSeconds = 15d;

        private EntityQuery databaseQuery;

        /// <summary>
        /// Keyed on owner, field, position and owner variation — never on the owner alone. One
        /// object carries many links, so a set keyed on its name would settle the whole object the
        /// first time any one of its references landed. The TARGET name is part of the ledger key
        /// too, though not part of the row's own key: registering the same field again with a
        /// different target replaces the row, and a ledger that ignored the target would read that
        /// replacement as already done and never write it.
        /// </summary>
        private readonly HashSet<string> settled = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> complained = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>When each unresolved row was first looked at, so lateness can be measured.</summary>
        private readonly Dictionary<string, double> firstSeen =
            new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>True when the last full pass left nothing outstanding.</summary>
        private bool everythingSettled;

        /// <summary>The registry version the last settled pass saw.</summary>
        /// <remarks>
        /// A VERSION, NOT A COUNT. The count was the gate, and it could not see a re-registration
        /// that pointed a field at a different object: <c>Register</c> replaces on <c>Key</c>, the
        /// key excludes the target, so the list length is unchanged and the pass returned before
        /// ever reaching the row it had never looked at. The registry moves its version on every
        /// change, replacements included.
        /// </remarks>
        private int registryVersionWhenSettled = -1;

        protected override void OnCreate()
        {
            databaseQuery = GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            RequireForUpdate(databaseQuery);
        }

        protected override void OnUpdate()
        {
            IReadOnlyList<DimensionObjectLinkDefinition> rows = DimensionObjectLinkRegistry.All;

            // NOTHING TO DO ONLY WHEN NOTHING HAS MOVED. This gate was a row COUNT, twice over: it
            // compared the settled ledger's size to the row count, then the row count to what it
            // was when the pass last came up clean. Neither could see the case its own comment
            // named — re-registering a field with a different target replaces on a key that
            // excludes the target, so the count is identical and a live row is never looked at.
            // The registry's version moves on every change, which is the question actually being
            // asked.
            if (rows.Count == 0 ||
                (everythingSettled &&
                 DimensionObjectLinkRegistry.Version == registryVersionWhenSettled))
            {
                return;
            }

            PugDatabase.DatabaseBankCD bank =
                databaseQuery.GetSingleton<PugDatabase.DatabaseBankCD>();

            double now = SystemAPI.Time.ElapsedTime;
            int outstanding = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                DimensionObjectLinkDefinition row = rows[i];
                string ledgerKey = LedgerKeyOf(row);
                if (settled.Contains(ledgerKey))
                {
                    continue;
                }

                ObjectID ownerId = DimensionObjectNames.Resolve(row.OwnerObjectName);
                ObjectID targetId = DimensionObjectNames.Resolve(row.TargetObjectName);
                if (ownerId == ObjectID.None || targetId == ObjectID.None)
                {
                    // Nothing answers to one of the two names yet. That is ordinary while content
                    // is still registering, so it is quiet — until it has gone on too long.
                    if (IsLate(ledgerKey, now))
                    {
                        string missing = ownerId == ObjectID.None
                            ? row.OwnerObjectName
                            : row.TargetObjectName;
                        Complain(
                            "'" + row.OwnerObjectName + "' still has nothing to point at for its " +
                            DimensionObjectLinkWords.For(row.Link) +
                            ": nothing in this world answers to '" + missing +
                            "'. Either that object is switched off in the dashboard, or it was " +
                            "renamed or removed after this was generated, or the mod that owns it " +
                            "is not installed. Turn it back on, or point at something else, and " +
                            "generate again.");
                    }

                    outstanding++;
                    continue;
                }

                Entity prefab = PugDatabase.GetPrimaryPrefabEntity(
                    ownerId, bank.databaseBankBlob, row.OwnerVariation);
                if (prefab == Entity.Null || !EntityManager.Exists(prefab))
                {
                    // The name resolved but the database has no prefab at that variation. Nothing
                    // later in the load will add one, but this is cheap to keep checking and the
                    // report is what a creator needs either way.
                    if (IsLate(ledgerKey, now))
                    {
                        Complain(
                            "'" + row.OwnerObjectName + "' has no object in the game at variation " +
                            row.OwnerVariation + ", so its " +
                            DimensionObjectLinkWords.For(row.Link) + " has nowhere " +
                            "to be written. Generate again, and if it keeps happening the " +
                            "variation number is higher than the object has.");
                    }

                    outstanding++;
                    continue;
                }

                DimensionObjectLinkOutcome outcome =
                    DimensionObjectLinkHydration.Apply(EntityManager, prefab, row, targetId);
                if (outcome.Result == DimensionObjectLinkResult.Impossible)
                {
                    // Provably unrepairable: the component the field lives on is not there, and no
                    // number of ticks will add it. Say it once and stop looking at this row.
                    Complain("'" + row.OwnerObjectName + "' " + outcome.Reason);
                    settled.Add(ledgerKey);
                    firstSeen.Remove(ledgerKey);
                    continue;
                }

                settled.Add(ledgerKey);
                firstSeen.Remove(ledgerKey);
                if (outcome.Result == DimensionObjectLinkResult.Written)
                {
                    DimensionFrameworkLog.Verbose(
                        "'" + row.OwnerObjectName + "' now points at '" +
                        row.TargetObjectName + "' for " + row.Link + ".");
                }
            }

            everythingSettled = outstanding == 0;
            registryVersionWhenSettled = DimensionObjectLinkRegistry.Version;
        }

        /// <summary>
        /// True once this row has been waiting longer than a world takes to finish registering.
        /// </summary>
        /// <remarks>
        /// The clock starts the first time the row is looked at rather than at world start, so a
        /// row registered late by a retrying bootstrap gets the same full grace period.
        /// </remarks>
        private bool IsLate(string ledgerKey, double now)
        {
            double started;
            if (!firstSeen.TryGetValue(ledgerKey, out started))
            {
                firstSeen[ledgerKey] = now;
                return false;
            }

            return now - started >= SettlingSeconds;
        }

        /// <summary>The row's identity plus the target, which the row's own key leaves out.</summary>
        private static string LedgerKeyOf(DimensionObjectLinkDefinition row)
        {
            return row.Key + "|" + row.TargetObjectName;
        }


        /// <summary>Says a thing once. A per-tick repeat of the same line is noise, not information.</summary>
        private void Complain(string message)
        {
            if (complained.Add(message))
            {
                DimensionFrameworkLog.Warning(message);
            }
        }
    }
}
