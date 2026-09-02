using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Conditions;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers stat effects a mod invented — the numbers they get, and the one rule that matters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A CUSTOM CONDITION IS A NUMBER, and every way of getting that number wrong is silent. Too low
    /// and it means one of Core Keeper's own conditions instead — a mod's speed buff quietly becomes
    /// being on fire. Different at generate time than at load time, and a creature is authored with
    /// one buff and given another. Neither throws, neither logs, and neither is visible until
    /// somebody plays it.
    /// </para>
    /// <para>
    /// So these tests hold three things: that a number is never inside the game's own range, that
    /// the same set of conditions always produces the same numbers, and that a name resolves the
    /// same way through the spine as it does through the registry.
    /// </para>
    /// </remarks>
    public sealed class DimensionConditionTests
    {
        [TearDown]
        public void Cleanup()
        {
            DimensionConditionRegistry.Clear();
        }

        private static DimensionConditionAsset MakeCondition(
            string id,
            ConditionEffect effect = ConditionEffect.MovementSpeed)
        {
            DimensionConditionAsset asset = ScriptableObject.CreateInstance<DimensionConditionAsset>();
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("conditionName").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("effect").enumValueIndex = (int)effect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        // ---- the numbers ----

        [Test]
        public void ACustomConditionNeverTakesANumberTheGameIsUsing()
        {
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("mod:first"));
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("mod:second"));

            int first = DimensionConditionRegistry.NumberFor("mod:first");
            int second = DimensionConditionRegistry.NumberFor("mod:second");

            Assert.GreaterOrEqual(
                first,
                (int)ConditionID.MAX_VALUES,
                "a number inside the game's own range would silently mean one of its conditions");
            Assert.GreaterOrEqual(second, (int)ConditionID.MAX_VALUES);
            Assert.AreNotEqual(first, second, "two conditions cannot share a number");
        }

        [Test]
        public void TheSameSetAlwaysProducesTheSameNumbers()
        {
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("zeta"));
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("alpha"));
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("mid"));
            int alphaFirst = DimensionConditionRegistry.NumberFor("alpha");
            int zetaFirst = DimensionConditionRegistry.NumberFor("zeta");

            // The same three, claimed in a completely different order — as a different bundle load
            // order would produce.
            DimensionConditionRegistry.Clear();
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("mid"));
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("zeta"));
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("alpha"));

            Assert.AreEqual(
                alphaFirst,
                DimensionConditionRegistry.NumberFor("alpha"),
                "load order must not decide what a condition means");
            Assert.AreEqual(zetaFirst, DimensionConditionRegistry.NumberFor("zeta"));
            Assert.Less(
                DimensionConditionRegistry.NumberFor("alpha"),
                DimensionConditionRegistry.NumberFor("zeta"),
                "numbers are handed out in name order");
        }

        [Test]
        public void ClaimingTheSameNameTwiceReplacesRatherThanDuplicates()
        {
            DimensionConditionRegistry.Claim(
                new DimensionCustomCondition("mod:buff") { Effect = ConditionEffect.Mining });
            DimensionConditionRegistry.Claim(
                new DimensionCustomCondition("mod:buff") { Effect = ConditionEffect.Health });

            Assert.AreEqual(1, DimensionConditionRegistry.Count, "an editor reload must not stack up copies");
            Assert.AreEqual(
                ConditionEffect.Health,
                DimensionConditionRegistry.InNumberOrder()[0].Effect,
                "the later claim is the one that counts");
        }

        [Test]
        public void ANamelessConditionIsRefusedRatherThanGivenANumber()
        {
            Assert.IsFalse(DimensionConditionRegistry.Claim(new DimensionCustomCondition(string.Empty)));
            Assert.IsFalse(DimensionConditionRegistry.Claim(null));
            Assert.AreEqual(0, DimensionConditionRegistry.Count);
        }

        [Test]
        public void TheTableHasToGrowByExactlyTheNumberClaimed()
        {
            Assert.AreEqual(
                (int)ConditionID.MAX_VALUES,
                DimensionConditionRegistry.RequiredTableSize,
                "with nothing claimed the game's own table is exactly the right size");

            DimensionConditionRegistry.Claim(new DimensionCustomCondition("mod:one"));
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("mod:two"));

            Assert.AreEqual(
                (int)ConditionID.MAX_VALUES + 2,
                DimensionConditionRegistry.RequiredTableSize);
        }

        [Test]
        public void NothingClaimedMeansNothingToDo()
        {
            Assert.IsFalse(
                DimensionConditionRegistry.HasAny,
                "with nothing claimed the game's own table build must be left completely alone");
            Assert.AreEqual(-1, DimensionConditionRegistry.NumberFor("mod:nothing"));
            Assert.AreEqual(ConditionID.None, DimensionConditionRegistry.IdFor("mod:nothing"));
        }

        // ---- resolving a name ----

        [Test]
        public void AConditionResolvesWhetherItIsTheGamesOrTheModsOwn()
        {
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("mod:hasteOfMine"));

            ConditionID vanilla;
            Assert.IsTrue(DimensionObjectSpine.TryResolveCondition("Poisoned", out vanilla));
            Assert.AreEqual(ConditionID.Poisoned, vanilla);

            ConditionID mine;
            Assert.IsTrue(DimensionObjectSpine.TryResolveCondition("mod:hasteOfMine", out mine));
            Assert.AreEqual(DimensionConditionRegistry.IdFor("mod:hasteOfMine"), mine);

            ConditionID nothing;
            Assert.IsFalse(DimensionObjectSpine.TryResolveCondition("notACondition", out nothing));
            Assert.AreEqual(ConditionID.None, nothing);
            Assert.IsFalse(DimensionObjectSpine.TryResolveCondition(null, out nothing));
        }

        [Test]
        public void AModCannotShadowOneOfTheGamesOwnConditions()
        {
            DimensionConditionRegistry.Claim(new DimensionCustomCondition("Poisoned"));

            ConditionID resolved;
            Assert.IsTrue(DimensionObjectSpine.TryResolveCondition("Poisoned", out resolved));
            Assert.AreEqual(
                ConditionID.Poisoned,
                resolved,
                "naming a custom condition after a vanilla one must not steal the vanilla name");
        }

        // ---- the scope the generator runs inside ----

        [Test]
        public void TheGenerateScopeMakesTheModsConditionsAnswerableAndThenPutsThemBack()
        {
            DimensionConditionAsset first = MakeCondition("mod:alpha");
            DimensionConditionAsset second = MakeCondition("mod:beta", ConditionEffect.Mining);
            try
            {
                using (DimensionConditionScope scope =
                    new DimensionConditionScope(new[] { first, second }))
                {
                    Assert.AreEqual(2, scope.Count);

                    ConditionID id;
                    Assert.IsTrue(DimensionObjectSpine.TryResolveCondition("mod:beta", out id));
                    Assert.GreaterOrEqual((int)id, (int)ConditionID.MAX_VALUES);
                }

                Assert.IsFalse(
                    DimensionConditionRegistry.HasAny,
                    "an editor session must not accumulate the conditions of every mod it opens");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void AConditionThatIsSwitchedOffIsNotGivenANumber()
        {
            DimensionConditionAsset off = MakeCondition("mod:off");
            try
            {
                SerializedObject serialized = new SerializedObject(off);
                serialized.FindProperty("enabled").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                using (new DimensionConditionScope(new[] { off }))
                {
                    Assert.AreEqual(0, DimensionConditionRegistry.Count);
                }
            }
            finally
            {
                Object.DestroyImmediate(off);
            }
        }

        // ---- the asset's own warnings ----

        [Test]
        public void AConditionThatAppliesNothingKnowsItDoesNothing()
        {
            DimensionConditionAsset empty = MakeCondition("mod:empty", ConditionEffect.None);
            try
            {
                Assert.IsTrue(empty.DoesNothing);
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        // ---- what a creature does with one ----

        [Test]
        public void ACreatureCanStartWithAConditionThisModInvented()
        {
            DimensionConditionAsset mine = MakeCondition("mod:startsBlessed", ConditionEffect.Mining);
            DimensionMobAsset mob = ScriptableObject.CreateInstance<DimensionMobAsset>();
            try
            {
                using (new DimensionConditionScope(new[] { mine }))
                {
                    GameObject root = new GameObject("testconditionmob");
                    try
                    {
                        List<string> unknown = new List<string>();
                        SerializedObject serialized = new SerializedObject(mob);
                        SerializedProperty conditions = serialized
                            .FindProperty("combat")
                            .FindPropertyRelative("conditions")
                            .FindPropertyRelative("startsWith");
                        conditions.arraySize = 1;
                        SerializedProperty entry = conditions.GetArrayElementAtIndex(0);
                        entry.FindPropertyRelative("conditionId").stringValue = "mod:startsBlessed";
                        entry.FindPropertyRelative("strength").intValue = 12;
                        entry.FindPropertyRelative("chance").floatValue = 1f;
                        serialized.ApplyModifiedPropertiesWithoutUndo();

                        DimensionObjectSpine.ApplyInitialConditions(
                            root,
                            mob.Combat.Conditions,
                            unknown.Add);

                        Assert.IsEmpty(
                            unknown,
                            "a condition this mod invented must not be reported as one the game " +
                            "does not have");

                        SupportsConditionsAuthoring supports =
                            root.GetComponent<SupportsConditionsAuthoring>();
                        Assert.IsNotNull(supports);
                        Assert.AreEqual(1, supports.initialConditions.Count);
                        Assert.AreEqual(
                            DimensionConditionRegistry.IdFor("mod:startsBlessed"),
                            supports.initialConditions[0].conditionID);
                        Assert.AreEqual(12, supports.initialConditions[0].value);
                    }
                    finally
                    {
                        Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(mob);
                Object.DestroyImmediate(mine);
            }
        }
    }
}
