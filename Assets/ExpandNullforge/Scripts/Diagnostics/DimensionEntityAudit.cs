using System;
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Foundation;
using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Diagnostics
{
    /// <summary>
    /// Checks that the objects this framework put into the game carry everything the game's own
    /// systems ask for before they will look at them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IT READS THE PREFABS, NOT SPAWNED OBJECTS, and that is the decision worth explaining. The
    /// obvious route is <c>API.Server.OnObjectCreated</c>, and it is the expensive one:
    /// <c>ObjectCreatedSystem</c> sets <c>Enabled</c> once, in <c>OnCreate</c>, from whether
    /// anything is subscribed (<c>ck-db/Pug.Other/PugMod/ObjectCreatedSystem.cs:31</c>). Subscribing
    /// after the world is built leaves it disabled and the handler never fires; subscribing before
    /// leaves it enabled for the rest of the session, running a query and an
    /// <c>AddComponent&lt;ObjectCreatedCalledCD&gt;</c> structural change every server frame — a
    /// permanent cost on every player who installs this framework, in return for a diagnostic.
    /// </para>
    /// <para>
    /// Every spawned object is instantiated from the converted prefab entity, and the whole
    /// converter pipeline has already run on that prefab by the time a world is up. So the same
    /// question is answered by one query over the prefabs, once per world load, at no ongoing cost
    /// at all: no subscription, no per-frame pass, nothing left running when the audit finishes.
    /// This is the cheaper half of what the diagnostics plan proposed, and it answers the question
    /// the plan asked.
    /// </para>
    /// <para>
    /// WHAT THAT COSTS IN COVERAGE, said plainly: a component ADDED to a live entity after it
    /// spawns is not on the prefab and is not seen here. Nothing in this framework adds components
    /// at runtime — the hydration systems write values into components the converters already
    /// wrote — so the two agree today. If a system is ever written that adds one, this check will
    /// report a gap the running object does not have, and the fix is to say so in that system's
    /// row rather than to go back to per-object events.
    /// </para>
    /// <para>
    /// IT COSTS ONE SYNC POINT, ONCE. <c>ToEntityArray</c> waits for running jobs before it hands
    /// the array over. That is why this is called from the mod's own <c>Update</c> on the main
    /// thread, once per world, and never from inside a system: one stall on the frame a world
    /// finishes loading, at a moment the game is already stalling for other reasons, is a price
    /// worth one answer. Anything that made it repeat would not be.
    /// </para>
    /// <para>
    /// EVERYTHING VANILLA IS EXCLUDED BEFORE ANY COMPONENT IS READ. The subject list is
    /// <see cref="DimensionItemObjectRegistry.ResolvedItems"/> together with
    /// <see cref="DimensionGeneratedObjectLedger.Resolved"/> — exactly the ids a content pack
    /// generated and the game answered to — so the game's own thousands of objects are never
    /// touched, and an empty subject list is reported as a finding rather than passing quietly.
    /// </para>
    /// <para>
    /// THE SECOND HALF OF THAT LIST IS THE POINT. With items alone this check could not contain a
    /// creature, a boss, a summoning circle, a plant or a world object — and the companion table
    /// is almost entirely creature and world-object queries, including the summoning circle that
    /// exposed the whole bug class. On the two content packs in this repository the item-only list
    /// was two tileset blocks measured against thirty-nine creature rules, and the audit called
    /// that a clean result. A pack has to be generated again for its manifest to carry the wider
    /// list.
    /// </para>
    /// <para>
    /// AND UNTIL IT IS, THE SUMMARY LINE SAYS SO, WHICH IS THE HALF THAT WAS MISSING. The sentence
    /// above lived only here, in a comment, while the line a player and a modder actually read
    /// counted the item slice against the whole table and ended "all of them carry what the systems
    /// that read them require" — a clean verdict on a subject list that could not contain the thing
    /// the table was written for. <see cref="Result.ItemSubjects"/>,
    /// <see cref="Result.LedgerSubjects"/> and <see cref="Result.RulesApplied"/> exist so that the
    /// line can name its own scope out loud: how many of the rules were a test of anything, and
    /// what kinds of object were not in the list at all.
    /// </para>
    /// </remarks>
    internal static class DimensionEntityAudit
    {
        /// <summary>What one object's check came to.</summary>
        internal struct Finding
        {
            /// <summary>The authored id, as the modder wrote it.</summary>
            public string ItemId;

            /// <summary>The number the game gave it.</summary>
            public int ObjectId;

            /// <summary>The rule that was not satisfied.</summary>
            public DimensionQueryCompanionTable.Rule Rule;

            /// <summary>The components the reading system wants and this object does not have.</summary>
            public List<string> Missing;

            /// <summary>The ones it does have, so the reader can see how far it got.</summary>
            public List<string> Present;
        }

        /// <summary>What one run of the audit looked at.</summary>
        internal struct Result
        {
            /// <summary>How many declared ids — items and other objects — resolved at all.</summary>
            public int ResolvedItems;

            /// <summary>
            /// How many of those came from the item registry, and how many from the wider ledger.
            /// </summary>
            /// <remarks>
            /// THE SPLIT IS HERE SO THE SUMMARY LINE CAN SAY WHAT IT DID NOT LOOK AT. A content
            /// pack generated before <see cref="DimensionGeneratedObjectLedger"/> existed carries
            /// no manifest entry for anything but its items, so the second number is zero and the
            /// subject list is the item slice of a table that is mostly creature and world-object
            /// queries — which is the case that used to be reported as a clean result. Neither
            /// number is a fault on its own; both are needed for the line to be honest about its
            /// own scope.
            /// </remarks>
            public int ItemSubjects;

            /// <summary>How many subjects came from the generated-object ledger.</summary>
            public int LedgerSubjects;

            /// <summary>How many of those had a prefab in this world.</summary>
            public int Checked;

            /// <summary>How many rules were in the table this run was measured against.</summary>
            public int RulesInTable;

            /// <summary>
            /// How many of those rules matched at least one object that was looked at.
            /// </summary>
            /// <remarks>
            /// A COUNT OF RULES THAT WERE A TEST OF SOMETHING. The rest asked for a component
            /// nothing in the subject list carries and passed without looking, so a line that
            /// reports the table's whole length as what the objects were measured against reports
            /// a number the run did not earn.
            /// </remarks>
            public int RulesApplied;

            /// <summary>Ids that resolved and whose prefab this world does not hold.</summary>
            public List<string> WithoutAPrefab;

            /// <summary>Every gap found.</summary>
            public List<Finding> Findings;

            /// <summary>Whether the budget stopped the walk before the end of the list.</summary>
            public bool StoppedEarly;
        }

        /// <summary>
        /// Walks this world's prefabs for the framework's own objects and checks each against the
        /// companion table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The budget exists so that a content pack with thousands of items cannot turn a world
        /// load into a stall. It is a count of objects, not of rules: every object that is looked
        /// at is looked at completely, because a half-checked object would produce a message that
        /// is wrong rather than incomplete.
        /// </para>
        /// <para>
        /// A BUDGET OF ZERO CHECKS NOTHING. It used to mean "no limit", which is the opposite of
        /// what somebody typing a zero into a diagnostics setting is asking for — the switch for
        /// off is <c>entityAudit</c>, and a budget is a ceiling.
        /// </para>
        /// </remarks>
        public static Result Run(World world, int budget)
        {
            Result result = new Result();
            result.WithoutAPrefab = new List<string>();
            result.Findings = new List<Finding>();

            int fromItemRegistry;
            List<KeyValuePair<string, ObjectID>> items = Subjects(out fromItemRegistry);
            result.ResolvedItems = items.Count;
            result.ItemSubjects = fromItemRegistry;
            result.LedgerSubjects = items.Count - fromItemRegistry;
            result.RulesInTable = DimensionQueryCompanionTable.All.Length;
            if (world == null || !world.IsCreated || items.Count == 0 || budget <= 0)
            {
                result.StoppedEarly = budget <= 0 && items.Count > 0;
                return result;
            }

            Dictionary<int, Entity> prefabs = BuildPrefabLookup(world);
            DimensionQueryCompanionTable.Rule[] rules = DimensionQueryCompanionTable.All;
            EntityManager entities = world.EntityManager;

            // WHICH RULES WERE A TEST OF ANYTHING. Marked as the walk goes rather than counted
            // afterwards, because the trigger is the only thing that knows whether a rule looked at
            // an object or passed over it.
            bool[] ruleApplied = new bool[rules.Length];

            for (int i = 0; i < items.Count; i++)
            {
                if (budget > 0 && result.Checked >= budget)
                {
                    result.StoppedEarly = true;
                    break;
                }

                int objectId = (int)items[i].Value;
                Entity prefab;
                if (!prefabs.TryGetValue(objectId, out prefab))
                {
                    result.WithoutAPrefab.Add(items[i].Key + " (id " + objectId + ")");
                    continue;
                }

                result.Checked++;

                for (int r = 0; r < rules.Length; r++)
                {
                    DimensionQueryCompanionTable.Rule rule = rules[r];
                    if (!rule.Trigger.Present(entities, prefab))
                    {
                        continue;
                    }

                    ruleApplied[r] = true;

                    List<string> missing = null;
                    List<string> present = new List<string>();
                    for (int n = 0; n < rule.AlsoNeeds.Length; n++)
                    {
                        DimensionQueryCompanionTable.Need need = rule.AlsoNeeds[n];
                        if (need.Present(entities, prefab))
                        {
                            present.Add(need.Name);
                            continue;
                        }

                        if (missing == null)
                        {
                            missing = new List<string>();
                        }

                        missing.Add(need.Name);
                    }

                    if (missing == null)
                    {
                        continue;
                    }

                    Finding finding = new Finding();
                    finding.ItemId = items[i].Key;
                    finding.ObjectId = objectId;
                    finding.Rule = rule;
                    finding.Missing = missing;
                    finding.Present = present;
                    result.Findings.Add(finding);
                }
            }

            for (int r = 0; r < ruleApplied.Length; r++)
            {
                if (ruleApplied[r])
                {
                    result.RulesApplied++;
                }
            }

            return result;
        }

        /// <summary>
        /// Every object this framework generated that the game answers to, items and the rest.
        /// </summary>
        /// <remarks>
        /// Two ledgers rather than one because they are held to different standards: an item id
        /// that never resolves is named as a fault, and an id from the wider list is not. Reading
        /// both here is what stops the check being aimed at the item slice of a table written for
        /// creatures. An id in both is checked once.
        /// </remarks>
        private static List<KeyValuePair<string, ObjectID>> Subjects(out int fromItemRegistry)
        {
            List<KeyValuePair<string, ObjectID>> subjects =
                DimensionItemObjectRegistry.ResolvedItems();
            fromItemRegistry = subjects.Count;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < subjects.Count; i++)
            {
                seen.Add(subjects[i].Key);
            }

            List<KeyValuePair<string, ObjectID>> objects = DimensionGeneratedObjectLedger.Resolved();
            for (int i = 0; i < objects.Count; i++)
            {
                if (seen.Add(objects[i].Key))
                {
                    subjects.Add(objects[i]);
                }
            }

            return subjects;
        }

        /// <summary>
        /// One sentence naming the thing, what is missing, what reads it, and what breaks.
        /// </summary>
        /// <remarks>
        /// The present list is in it on purpose. "What is missing" says what to add; "what it does
        /// have" is what answers the next question, which is always which pass wrote this object
        /// and how far that pass got.
        /// </remarks>
        public static string Describe(Finding finding)
        {
            StringBuilder text = new StringBuilder();
            text.Append(finding.ItemId);
            text.Append(" (id ");
            text.Append(finding.ObjectId);
            text.Append(") carries ");
            text.Append(finding.Rule.Trigger.Name);
            text.Append(", but the system that reads it — ");
            text.Append(finding.Rule.ReadingSystem);
            text.Append(" — also requires ");
            text.Append(Join(finding.Missing));
            text.Append(", and this object has ");
            text.Append(finding.Missing.Count == 1 ? "not got it" : "none of them");
            text.Append(". ");
            text.Append(UpperFirst(finding.Rule.WhatBreaks));
            text.Append(". ");
            if (!string.IsNullOrEmpty(finding.Rule.VanillaExample))
            {
                text.Append("Core Keeper's own ");
                text.Append(finding.Rule.VanillaExample);
                text.Append(" carries the whole set. ");
            }

            text.Append("Of that set this object already has: ");
            text.Append(finding.Present.Count == 0 ? "nothing" : Join(finding.Present));
            // NO PASS IS NAMED, AND THE ONE IT USED TO NAME WAS WRONG FOR FOUR OF THE RULES. It
            // said "the generator's CloseTheGaps pass fills the companions in", which is true of
            // the item, creature, plant, critter, container, workbench, world-object and vehicle
            // paths and of nothing else: neither DimensionProjectileGenerator nor
            // DimensionExplosionGenerator calls CloseTheGaps, so for the ProjectileCD,
            // MortarProjectileCD, ExplosionCD and SequenceExplosiveCD rules the reader was told to
            // run a pass that does not exist for their object. What is true of every rule is the
            // instruction below.
            text.Append(". Fix: generate this object again and see whether it comes back the same. "
                + "Most generators close a gap like this as they finish an object; if this one "
                + "survives a regenerate, the gap is in the generator for that kind of thing "
                + "rather than in what was authored.");
            return text.ToString();
        }

        private static string UpperFirst(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static string Join(List<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return "nothing";
            }

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(i == names.Count - 1 ? " and " : ", ");
                }

                text.Append(names[i]);
            }

            return text.ToString();
        }

        /// <summary>
        /// Every converted prefab in this world, by the object number it carries.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NAMING <c>Prefab</c> IN THE QUERY IS WHAT MAKES IT MATCH. Entities carrying
        /// <c>Prefab</c> are excluded from every query that does not ask for them, so the plain
        /// <c>ObjectDataCD</c> query returns spawned objects and no prefabs at all.
        /// </para>
        /// <para>
        /// <c>CustomScenePrefab</c> copies are skipped for the same reason Core Keeper's own
        /// <c>PugDatabase</c> lookup skips them: a scene's stored copy of an object carries the
        /// same object number as the real prefab and is not what anything is instantiated from.
        /// Keeping it would make the audit read a different object from the one the game uses.
        /// </para>
        /// <para>
        /// The first entry for a number wins, and a variation-zero entry replaces one that was
        /// stored for a higher variation, because variation zero is the object the generator wrote
        /// and the others are its appearances.
        /// </para>
        /// </remarks>
        private static Dictionary<int, Entity> BuildPrefabLookup(World world)
        {
            Dictionary<int, Entity> byObjectId = new Dictionary<int, Entity>();
            EntityManager entities = world.EntityManager;
            EntityQuery query = entities.CreateEntityQuery(
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<Prefab>());
            try
            {
                NativeArray<Entity> found = query.ToEntityArray(Allocator.Temp);
                try
                {
                    for (int i = 0; i < found.Length; i++)
                    {
                        Entity entity = found[i];
                        if (entities.HasComponent<CustomScenePrefab>(entity))
                        {
                            continue;
                        }

                        ObjectDataCD data = entities.GetComponentData<ObjectDataCD>(entity);
                        int id = (int)data.objectID;
                        if (!byObjectId.ContainsKey(id) || data.variation == 0)
                        {
                            byObjectId[id] = entity;
                        }
                    }
                }
                finally
                {
                    found.Dispose();
                }
            }
            finally
            {
                query.Dispose();
            }

            return byObjectId;
        }
    }
}
