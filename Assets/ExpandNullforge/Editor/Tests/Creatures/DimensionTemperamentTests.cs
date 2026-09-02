using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Holds the Temperament wiring open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Temperament sat in the Monster Studio for a long time doing nothing: every generated
    /// creature was written with <c>IsEnemy = true</c> and an EMPTY <c>wantsToAttackTags</c>, and
    /// an empty wants list is the one thing <c>ChaseStateRequest</c> checks before it will pick a
    /// target at all — so every mob in the framework was, in the game's terms, passive. These
    /// tests pin the three tempers to the two vanilla levers they ride on, in both directions,
    /// because "the tag list happens to be empty" and "the tag list was deliberately emptied" look
    /// identical in a prefab and behave identically in game.
    /// </para>
    /// <para>
    /// The two levers, verified against the decompile: <c>BehaviourTagsCD.WantsToAndCanAttack</c>
    /// gating chase-target selection (`ck-db\Pug.Other\ChaseStateRequest.cs:216`), and
    /// <c>chaseAtDistance</c> being overridden to a forced 20 tiles for the last attacker
    /// (`ChaseStateRequest.cs:224-228`), which is the whole of Defensive.
    /// </para>
    /// </remarks>
    public sealed class DimensionTemperamentTests
    {
        private const string TestRoot = "Assets/NullforgeTemperamentTests";

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeTemperamentTests");
            }
        }

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private static DimensionCreatureStatsTemplate Stats()
        {
            return Stats(0f);
        }

        /// <summary>A stat block, optionally with the author's own chase distance already in it.</summary>
        /// <remarks>
        /// The chase distance is a STATS field, not a Pursuit one — <c>ApplyChase</c> reads
        /// <c>DimensionCreatureStatsTemplate.ChaseAtDistance</c>. Tests that want to prove a temper
        /// leaves an answered distance alone have to answer it here.
        /// </remarks>
        private static DimensionCreatureStatsTemplate Stats(float chaseAtDistance)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty block = holder.FindProperty("creatureStats");
            block.FindPropertyRelative("maxHealth").intValue = 100;
            block.FindPropertyRelative("chaseAtDistance").floatValue = chaseAtDistance;
            holder.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureStatsTemplate stats = host.CreatureStats;
            Object.DestroyImmediate(host);
            return stats;
        }

        private static DimensionCreatureCombatTemplate Combat(
            System.Action<SerializedProperty> write)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject serialized = new SerializedObject(host);
            if (write != null)
            {
                write(serialized.FindProperty("combat"));
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureCombatTemplate built = host.Combat;
            Object.DestroyImmediate(host);
            return built;
        }

        /// <summary>A melee creature that also chases, which is the ordinary shape.</summary>
        private static DimensionCreatureCombatTemplate Fighter()
        {
            return Combat(delegate(SerializedProperty combat)
            {
                combat.FindPropertyRelative("attackKind").enumValueIndex = 1;
                combat.FindPropertyRelative("detectionRadius").floatValue = 9f;
                combat.FindPropertyRelative("pursuit")
                    .FindPropertyRelative("keepsAtLeastThisFarAway").floatValue = 1f;
            });
        }

        private static GameObject Generate(
            string id,
            DimensionSpawnAggressionKind temper,
            DimensionCreatureCombatTemplate combat,
            out DimensionCreatureGenerationReport report)
        {
            return Generate(id, temper, combat, Stats(), false, out report);
        }

        private static GameObject Generate(
            string id,
            DimensionSpawnAggressionKind temper,
            DimensionCreatureCombatTemplate combat,
            DimensionCreatureStatsTemplate stats,
            bool isBoss,
            out DimensionCreatureGenerationReport report)
        {
            report = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = id,
                        DisplayName = id,
                        Stats = stats,
                        IsEnemy = true,
                        Aggression = temper,
                        IsBoss = isBoss,
                        Combat = combat
                    }
                },
                TestRoot,
                default(DimensionNamingContext));

            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
        }

        private static bool Wants(GameObject prefab, ObjectCategoryTag tag)
        {
            BehaviourTagsAuthoring tags = prefab.GetComponent<BehaviourTagsAuthoring>();
            return tags != null && tags.wantsToAttackTags != null &&
                   tags.wantsToAttackTags.Contains(tag);
        }

        private static bool Cannot(GameObject prefab, ObjectCategoryTag tag)
        {
            BehaviourTagsAuthoring tags = prefab.GetComponent<BehaviourTagsAuthoring>();
            return tags != null && tags.cantAttackTags != null &&
                   tags.cantAttackTags.Contains(tag);
        }

        // ---- what it counts as ----

        [Test]
        public void EveryCreatureCountsAsACreatureToEverythingElse()
        {
            DimensionCreatureGenerationReport report;
            GameObject hostile = Generate(
                "tagged", DimensionSpawnAggressionKind.Hostile, Fighter(), out report);
            GameObject harmless = Generate(
                "taggedcalm", DimensionSpawnAggressionKind.Passive, Fighter(), out report);

            Assert.IsTrue(
                hostile.GetComponent<ObjectAuthoring>().tags
                    .Contains(ObjectCategoryTag.HostileCreature),
                "targeting reads the TARGET's own tag list; with none, a player's pet cannot " +
                "chase this creature and a ranged pet cannot shoot it at all");
            Assert.IsTrue(
                harmless.GetComponent<ObjectAuthoring>().tags
                    .Contains(ObjectCategoryTag.HostileCreature),
                "the game's own Cow and Roly Poly carry it too — the tag says 'is a creature', " +
                "not 'is dangerous', and without it nothing can hunt an animal either");
        }

        // ---- hostile ----

        [Test]
        public void AHostileCreatureWantsToAttackPlayersAndMonsters()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "hostile", DimensionSpawnAggressionKind.Hostile, Fighter(), out report);

            Assert.IsTrue(
                Wants(prefab, ObjectCategoryTag.Player),
                "without the Player tag no chase target is ever picked, so Hostile means nothing");
            Assert.IsTrue(
                Wants(prefab, ObjectCategoryTag.HostileCreature),
                "the game's own enemies list both tags; only listing Player leaves it standing " +
                "next to a summoned ally");
            Assert.IsFalse(Cannot(prefab, ObjectCategoryTag.Player));
        }

        [Test]
        public void AHostileCreatureWithNoChaseDistanceBorrowsHowFarItCanSee()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "hostilerange", DimensionSpawnAggressionKind.Hostile, Fighter(), out report);

            ChaseStateAuthoring chase = prefab.GetComponent<ChaseStateAuthoring>();
            Assert.IsNotNull(chase);
            Assert.AreEqual(
                9f,
                chase.chaseAtDistance,
                "a hostile creature with a chase but no distance would never start one");
        }

        /// <summary>
        /// A temper that promises pursuit has to produce one, not a warning about the lack of one.
        /// </summary>
        /// <remarks>
        /// A generator that warns here and moves on ships the two commonest tempers on
        /// a creature with no <c>ChaseStateAuthoring</c> — and with none,
        /// <c>ChaseStateRequest.ShouldUpdate</c> returns false for ever, so the creature can only
        /// ever swing at whatever is already touching it.
        /// </remarks>
        [Test]
        public void APursuingTemperGetsAChaseEvenWhenPursuitWasNeverAnswered()
        {
            foreach (DimensionSpawnAggressionKind temper in new[]
            {
                DimensionSpawnAggressionKind.Hostile,
                DimensionSpawnAggressionKind.Defensive
            })
            {
                DimensionCreatureGenerationReport report;
                GameObject prefab = Generate(
                    "pursues" + temper,
                    temper,
                    Combat(delegate(SerializedProperty combat)
                    {
                        combat.FindPropertyRelative("attackKind").enumValueIndex = 1;
                        combat.FindPropertyRelative("detectionRadius").floatValue = 7f;
                    }),
                    out report);

                ChaseStateAuthoring chase = prefab.GetComponent<ChaseStateAuthoring>();
                Assert.IsNotNull(chase, temper + " promises pursuit and needs a chase to keep it");
                Assert.AreEqual(
                    7f,
                    chase.chaseAtDistance,
                    "how far it can see is the only pursuit distance the author has given");
                Assert.AreEqual(
                    1f,
                    chase.moveSpeedMultiplier,
                    "a zero multiplier enters the chase state and then stands still");
            }
        }

        [Test]
        public void APursuingTemperThatCannotSeeAnythingIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "hostileblind",
                DimensionSpawnAggressionKind.Hostile,
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 1;
                    combat.FindPropertyRelative("detectionRadius").floatValue = 0f;
                }),
                out report);

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("cannot see anything")),
                "with nothing to borrow a distance from, a mob that cannot follow anyone is the " +
                "kind of mistake only play reveals");
        }

        /// <summary>A chase distance the author already answered is never overwritten by a temper.</summary>
        /// <remarks>
        /// This is what lets a borrowed behaviour keep chase distances tuned to how its attack
        /// works. The temper pass runs last precisely so it can tell "nobody answered" from "the
        /// answer is four tiles" and only fill in the first.
        /// </remarks>
        [Test]
        public void ATemperNeverOverwritesAChaseDistanceThatWasAlreadyAnswered()
        {
            foreach (DimensionSpawnAggressionKind temper in new[]
            {
                DimensionSpawnAggressionKind.Hostile,
                DimensionSpawnAggressionKind.Defensive
            })
            {
                DimensionCreatureGenerationReport report;
                GameObject prefab = Generate(
                    "tuned" + temper,
                    temper,
                    Combat(delegate(SerializedProperty combat)
                    {
                        combat.FindPropertyRelative("attackKind").enumValueIndex = 1;
                        combat.FindPropertyRelative("detectionRadius").floatValue = 20f;
                    }),
                    Stats(4f),
                    false,
                    out report);

                Assert.AreEqual(
                    4f,
                    prefab.GetComponent<ChaseStateAuthoring>().chaseAtDistance,
                    "a chase distance tuned to how the attack works must survive the temper pass");
            }
        }

        // ---- passive ----

        [Test]
        public void APassiveCreatureWillNotAttackPlayersEvenWithAnAttack()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "passive", DimensionSpawnAggressionKind.Passive, Fighter(), out report);

            Assert.IsFalse(
                Wants(prefab, ObjectCategoryTag.Player),
                "an empty wants list is what stops it choosing a chase target");
            Assert.IsTrue(
                Cannot(prefab, ObjectCategoryTag.Player),
                "clearing the wants list is not enough: a melee swing at something already " +
                "adjacent only consults the cannot list");
            Assert.IsTrue(Cannot(prefab, ObjectCategoryTag.HostileCreature));
            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("Passive and also has an attack")),
                "an attack that can never fire has to be said out loud");
        }

        [Test]
        public void APassiveCreatureKeepsTheChaseItWasGiven()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "passivechase", DimensionSpawnAggressionKind.Passive, Fighter(), out report);

            ChaseStateAuthoring chase = prefab.GetComponent<ChaseStateAuthoring>();
            Assert.IsNotNull(
                chase,
                "the game's own cows chase — that is how they reach a feed trough — so a passive " +
                "temper must not take the chase away");
            Assert.IsFalse(chase.disabled);
        }

        // ---- defensive ----

        [Test]
        public void ADefensiveCreatureKeepsTheTagsItNeedsToAnswerAnAttacker()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "defensive", DimensionSpawnAggressionKind.Defensive, Fighter(), out report);

            Assert.IsTrue(
                Wants(prefab, ObjectCategoryTag.Player),
                "the last-attacker candidate is run through the same WantsToAndCanAttack check " +
                "as everyone else, so clearing this would make a defender unable to answer the " +
                "person hitting it");
            Assert.IsFalse(
                Cannot(prefab, ObjectCategoryTag.Player),
                "a defender that may not swing at players cannot defend itself from one");
        }

        /// <summary>
        /// The regression that made Defensive a lie, pinned from the outside.
        /// </summary>
        /// <remarks>
        /// The first implementation baked <c>chaseAtDistance = 0</c>, which does close the aggro
        /// gate — but <c>ChaseStateConverter</c> hands the same number to
        /// <c>PathFindingConversion.CreatePathfindingEntity</c> as the path search radius, and
        /// <c>PathFindSystem.cs:757</c> refuses to expand a search past that radius. Zero meant no
        /// path could ever exist. The gate now closes at runtime instead, so this asserts the
        /// prefab keeps a real radius — the shape of the bug, not the shape of the fix.
        /// </remarks>
        [Test]
        public void ADefensiveCreatureKeepsARealChaseDistanceSoItsPathSearchHasARadius()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "defensivepath",
                DimensionSpawnAggressionKind.Defensive,
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 1;
                    combat.FindPropertyRelative("detectionRadius").floatValue = 9f;
                    combat.FindPropertyRelative("pursuit")
                        .FindPropertyRelative("needsAPathToChase").boolValue = true;
                }),
                out report);

            ChaseStateAuthoring chase = prefab.GetComponent<ChaseStateAuthoring>();
            Assert.IsNotNull(chase);
            Assert.Greater(
                chase.chaseAtDistance,
                0f,
                "a zero here sizes the path search to zero tiles, and a defender that insists on " +
                "a path then deadlocks: no chase without a path, no path without a chase");
            Assert.IsFalse(
                report.Warnings.Exists(w => w.Contains("pathfinding")),
                "pathfinding and Defensive are a legal combination now; warning about it would " +
                "be telling authors to give up something that works");
        }

        [Test]
        public void OnlyADefensiveCreatureCarriesTheMarkerThatClosesItsAggroGate()
        {
            DimensionCreatureGenerationReport report;

            Assert.IsNotNull(
                Generate("holdfire", DimensionSpawnAggressionKind.Defensive, Fighter(), out report)
                    .GetComponent<ExpandNullforge.Creatures.DimensionHoldsFireAuthoring>(),
                "without the marker nothing zeroes chaseAtDistanceSq and the creature is simply " +
                "hostile at its authored range");

            // Regenerating the SAME id under a different temper: the marker has to come back off,
            // or a creature the author retempered keeps holding its fire and nobody can see why.
            GameObject retempered = Generate(
                "holdfire", DimensionSpawnAggressionKind.Hostile, Fighter(), out report);
            Assert.IsNull(
                retempered.GetComponent<ExpandNullforge.Creatures.DimensionHoldsFireAuthoring>(),
                "a prefab regenerated as Hostile must not keep the marker its Defensive pass left");

            foreach (DimensionSpawnAggressionKind temper in new[]
            {
                DimensionSpawnAggressionKind.Passive,
                DimensionSpawnAggressionKind.Custom
            })
            {
                Assert.IsNull(
                    Generate("nohold" + temper, temper, Fighter(), out report)
                        .GetComponent<ExpandNullforge.Creatures.DimensionHoldsFireAuthoring>(),
                    temper + " does not hold its fire, it never opens fire");
            }
        }

        [Test]
        public void ADefensiveCreatureWithNothingToHitBackWithIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "defensivetoothless",
                DimensionSpawnAggressionKind.Defensive,
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("pursuit")
                        .FindPropertyRelative("keepsAtLeastThisFarAway").floatValue = 1f;
                }),
                out report);

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("Defensive but has no attack")));
        }

        // ---- custom ----

        [Test]
        public void CustomLeavesTheAuthoredTagsExactlyAsTyped()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "custom",
                DimensionSpawnAggressionKind.Custom,
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 1;
                    SerializedProperty wants = combat.FindPropertyRelative("wantsToAttackTags");
                    wants.arraySize = 1;
                    wants.GetArrayElementAtIndex(0).stringValue = "Ore";
                }),
                out report);

            Assert.IsTrue(
                Wants(prefab, ObjectCategoryTag.Ore),
                "Custom is the escape hatch for an author answering the expert question");
            Assert.IsFalse(
                Wants(prefab, ObjectCategoryTag.Player),
                "Custom must not quietly add the two tags the other tempers write");
        }

        /// <summary>
        /// An author who wants no tags gets no tags, and still gets a creature the game looks at.
        /// </summary>
        /// <remarks>
        /// THE OPPOSITE IS A CREATURE THAT HAS QUIETLY LEFT THE GAME: the component taken off
        /// entirely when the temper is Custom and both lists
        /// are empty. Its presence is a hard gate on the chase and on every attack —
        /// <c>ChaseStateRequest</c>, <c>MeleeAttackStateRequest</c>,
        /// <c>RangeAttackStateRequest</c>, <c>ChargeAttackStateRequest</c>,
        /// <c>JumpAttackStateRequest</c>, <c>EatStateRequest</c> and <c>ExplodeStateRequest</c> all
        /// ask for it before they look at anything else, and six systems name it in their work
        /// query. Empty lists are a real answer, meaning "nothing here is my enemy". No component
        /// at all is not an answer; it is the creature dropping out of every one of those passes,
        /// with nothing said. Clearing the lists is what makes generation authoritative, and that
        /// is what is checked here.
        /// </remarks>
        [Test]
        public void CustomWithNoTagsAtAllKeepsTheComponentWithNothingInIt()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "customblank",
                DimensionSpawnAggressionKind.Custom,
                Combat(null),
                out report);

            BehaviourTagsAuthoring tags = prefab.GetComponent<BehaviourTagsAuthoring>();

            Assert.IsNotNull(
                tags,
                "the game skips a creature with no tag component at all — it cannot chase, cannot " +
                "attack, cannot eat and cannot explode, whatever else is on it");

            Assert.That(
                tags.wantsToAttackTags,
                Is.Empty,
                "an author who wants no tags at all still gets none");

            Assert.That(
                tags.cantAttackTags,
                Is.Empty,
                "an author who wants no tags at all still gets none");
        }

        [Test]
        public void CustomWithAnAttackAndNoCategoriesIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate("customarmed", DimensionSpawnAggressionKind.Custom, Fighter(), out report);

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("Custom and no attack categories")),
                "an empty wants list fails WantsToAndCanAttack for everything, so this creature " +
                "picks nobody — and the field used to promise the faction would carry it");
        }

        // ---- what everything else thinks of it ----

        /// <summary>
        /// The tag that is the difference between a cow and a punchbag.
        /// </summary>
        /// <remarks>
        /// Measured over the game's own prefabs: all twelve cattle entities tag themselves
        /// <c>HostileCreature + Cattle</c> while all forty-four hostiles tag themselves
        /// <c>HostileCreature</c> alone. <c>Cattle</c> is not decoration — every one of the ten pet
        /// prefabs and six minion prefabs lists it in its own <c>cantAttackTags</c>, and the
        /// player's kill-everything command skips anything carrying it
        /// (`ck-db\Pug.Other\PlayerCommand\ServerSystem.cs:235`). Without it the first tamed pet to
        /// wander past butchers the author's whole herd.
        /// </remarks>
        [Test]
        public void OnlyAPassiveCreatureStopsCountingAsPrey()
        {
            DimensionCreatureGenerationReport report;

            Assert.IsTrue(
                Generate("prey", DimensionSpawnAggressionKind.Passive, Fighter(), out report)
                    .GetComponent<ObjectAuthoring>().tags.Contains(ObjectCategoryTag.Cattle),
                "a passive creature every pet in the game still hunts is not passive");

            foreach (DimensionSpawnAggressionKind temper in new[]
            {
                DimensionSpawnAggressionKind.Hostile,
                DimensionSpawnAggressionKind.Defensive,
                DimensionSpawnAggressionKind.Custom
            })
            {
                Assert.IsFalse(
                    Generate("notprey" + temper, temper, Fighter(), out report)
                        .GetComponent<ObjectAuthoring>().tags.Contains(ObjectCategoryTag.Cattle),
                    temper + " is meant to be fought, so nothing should be told to spare it");
            }
        }

        [Test]
        public void RetemperingAPassiveCreatureTakesItsHarmlessnessBackOff()
        {
            DimensionCreatureGenerationReport report;
            Generate("wasprey", DimensionSpawnAggressionKind.Passive, Fighter(), out report);

            Assert.IsFalse(
                Generate("wasprey", DimensionSpawnAggressionKind.Hostile, Fighter(), out report)
                    .GetComponent<ObjectAuthoring>().tags.Contains(ObjectCategoryTag.Cattle),
                "a stale Cattle tag would leave a hostile creature that pets refuse to fight and " +
                "nothing in the prefab explains why");
        }

        // ---- bosses ----

        /// <summary>
        /// Every temper reaches a boss, and the generator refuses none of them.
        /// </summary>
        /// <remarks>
        /// Pinning bosses to Hostile, on the claim that a boss has no temperament
        /// question, builds a boss that sleeps in its arena until you swing first — a real shape —
        /// as an ordinary aggressive boss instead. Nothing in
        /// <c>BossAuthoring</c>, the health bar or the map pin reads the temperament, so all three
        /// behave on a boss exactly as they do on a mob.
        /// </remarks>
        [Test]
        public void ABossCanHoldItsFireLikeAnythingElse()
        {
            DimensionCreatureGenerationReport report;
            GameObject boss = Generate(
                "sleepingboss",
                DimensionSpawnAggressionKind.Defensive,
                Fighter(),
                Stats(),
                true,
                out report);

            Assert.IsNotNull(boss.GetComponent<BossAuthoring>(), "still a boss");
            Assert.IsNotNull(
                boss.GetComponent<ExpandNullforge.Creatures.DimensionHoldsFireAuthoring>(),
                "a boss that ignores you until you touch it is the shape the hardcode forbade");
            Assert.IsTrue(
                Wants(boss, ObjectCategoryTag.Player),
                "it still has to be able to answer whoever woke it");
        }

        [Test]
        public void ADefenderWithoutTheEnemyTagIsReported()
        {
            DimensionCreatureGenerationReport report = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = "defensivenotenemy",
                        DisplayName = "defensivenotenemy",
                        Stats = Stats(),
                        IsEnemy = false,
                        Aggression = DimensionSpawnAggressionKind.Defensive,
                        Combat = Fighter()
                    }
                },
                TestRoot,
                default(DimensionNamingContext));

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("does not carry the game's enemy tag")),
                "EnemyConverter is the only thing that adds LastAttackerCD, and the player's " +
                "attack path only sets that component where it already exists — without the tag " +
                "a defender never learns who hit it");
        }

        // ---- the half that only exists at runtime ----

        /// <summary>
        /// Holds the mod entry to naming the system the Defensive marker depends on.
        /// </summary>
        /// <remarks>
        /// <para>
        /// MOD SYSTEMS ARE CREATED AUTOMATICALLY, whatever a reading of mod load order says: the game
        /// builds its worlds after the mod assembly is in memory and creates every system it finds
        /// there, into the group its <c>[UpdateInGroup]</c> names. So a name missing from the mod
        /// entry is an incomplete list, not a dead feature, and this test is worth keeping for the
        /// list rather than for a liveness claim it cannot make.
        /// </para>
        /// <para>
        /// The stakes are unchanged and worth having written down: without
        /// <c>DimensionHoldsFireSystem</c> running, the marker sits on the prefab,
        /// <c>chaseAtDistanceSq</c> keeps its authored value, and every Defensive creature in the
        /// mod is quietly Hostile. Whether it runs is what <c>DimensionSystemLivenessTests</c>
        /// checks offline, off the class's own attributes, and what <c>DimensionSelfAudit</c>
        /// answers in game.
        /// </para>
        /// </remarks>
        [Test]
        public void TheSystemThatMakesDefensiveRealIsActuallyCreated()
        {
            string source = DimensionFrameworkSourceScanner.ReadByName("ExpandNullforgeModEntry.cs");
            StringAssert.Contains(
                "GetOrCreateSystemManaged<ExpandNullforge.Creatures.DimensionHoldsFireSystem>",
                source,
                "Defensive creatures carry DimensionHoldsFireAuthoring and nothing reads it. Add " +
                "world.GetOrCreateSystemManaged<ExpandNullforge.Creatures.DimensionHoldsFireSystem>(); " +
                "to the SERVER block of ExpandNullforgeModEntry, beside the other Creatures " +
                "systems. Until then every Defensive creature behaves as Hostile.");
        }

        [Test]
        public void ABossCanBePassiveAndABossCanBeHostile()
        {
            DimensionCreatureGenerationReport report;

            GameObject calm = Generate(
                "calmboss", DimensionSpawnAggressionKind.Passive, Fighter(), Stats(), true,
                out report);
            Assert.IsFalse(Wants(calm, ObjectCategoryTag.Player));
            Assert.IsTrue(Cannot(calm, ObjectCategoryTag.Player));
            Assert.IsNotNull(calm.GetComponent<BossAuthoring>());

            GameObject angry = Generate(
                "angryboss", DimensionSpawnAggressionKind.Hostile, Fighter(), Stats(), true,
                out report);
            Assert.IsTrue(Wants(angry, ObjectCategoryTag.Player));
            Assert.IsNotNull(angry.GetComponent<ChaseStateAuthoring>());
        }
    }
}
