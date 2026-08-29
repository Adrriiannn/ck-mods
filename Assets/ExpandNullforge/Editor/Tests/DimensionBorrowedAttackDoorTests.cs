#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Holds the way in to the game's own moves open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FAILURE THIS GUARDS. Every piece of borrowing a vanilla move existed and compiled — the
    /// harvested table, the applier, a picker, tests over all three — and none of it was reachable,
    /// because the picker was drawn by a panel the studio stopped showing when the stage pages
    /// replaced it. Nothing was red. Nothing was missing. It simply could not be got at. So the
    /// first test here is not about attacks at all: it is that the card which opens the list is
    /// still built by the page a creator actually sees.
    /// </para>
    /// <para>
    /// The rest hold the two things a creator is promised at that door. That the names in the list
    /// are the game's names rather than file names. And that a move which says it arrives complete
    /// does — including the parts the harvest could not carry and the switches that decide whether
    /// anything reads it.
    /// </para>
    /// </remarks>
    public sealed class DimensionBorrowedAttackDoorTests
    {
        // ---- the door itself ----

        /// <summary>
        /// The page a creator sees still builds the card that opens the list.
        /// </summary>
        /// <remarks>
        /// Read out of the source rather than by building the page, because building it needs a
        /// dimension open and a selection made, and the thing worth pinning is smaller than that:
        /// the one call that turns a written picker into a reachable one.
        /// </remarks>
        [Test]
        public void TheCreaturePageStillBuildsTheCardThatOpensTheList()
        {
            // Found by name rather than by path: the page is allowed to move, and a read that
            // cannot find its file must fail rather than report success over nothing.
            Assert.IsTrue(
                DimensionFrameworkSourceScanner
                    .ReadByName("DimensionCollectionStagePage.cs")
                    .Contains("DimensionBorrowedAttackCard.Build"),
                "nothing on the creature page opens the list of the game's own moves any more, " +
                "so every one of them is unreachable again");
        }

        [Test]
        public void ACreatureIsOfferedMovesAndAnItemIsNot()
        {
            DimensionMobAsset creature = ScriptableObject.CreateInstance<DimensionMobAsset>();
            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            try
            {
                Assert.IsTrue(
                    DimensionBorrowedAttackPickerWindow.CanTakeAMove(creature),
                    "a creature has fight settings, so it can take a move");
                Assert.IsFalse(
                    DimensionBorrowedAttackPickerWindow.CanTakeAMove(item),
                    "an item has no fight settings, and offering it a swing would be a button " +
                    "that cannot do what it says");
                Assert.IsFalse(DimensionBorrowedAttackPickerWindow.CanTakeAMove(null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creature);
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        // ---- the names ----

        [Test]
        public void EveryMoveIsListedUnderAReadableName()
        {
            for (int i = 0; i < DimensionBorrowedAttacks.All.Length; i++)
            {
                DimensionBorrowedAttacks.Preset preset = DimensionBorrowedAttacks.All[i];
                string label = DimensionBorrowedAttackCatalog.Label(preset);

                Assert.IsFalse(string.IsNullOrEmpty(label), "a move with nothing to call it");
                Assert.IsFalse(
                    label.Contains("_"),
                    "'" + label + "' still reads like a file name");
                Assert.IsFalse(
                    label.Contains("Entity"),
                    "'" + label + "' still reads like a file name");
            }
        }

        /// <summary>
        /// Every name written down here still names something the table carries.
        /// </summary>
        /// <remarks>
        /// A correction keyed to a source that no longer exists is worse than none: it is silent,
        /// and the file name it was written to replace goes back to being what a creator reads.
        /// </remarks>
        [Test]
        public void EveryNameCorrectionStillPointsAtSomethingInTheTable()
        {
            HashSet<string> sources = new HashSet<string>();
            for (int i = 0; i < DimensionBorrowedAttacks.All.Length; i++)
            {
                sources.Add(DimensionBorrowedAttacks.All[i].Creature ?? string.Empty);
            }

            string[] corrected = DimensionBorrowedAttackCatalog.SourcesWithABetterName();
            Assert.Greater(corrected.Length, 0, "nothing is being renamed at all");
            for (int i = 0; i < corrected.Length; i++)
            {
                Assert.IsTrue(
                    sources.Contains(corrected[i]),
                    "'" + corrected[i] + "' is given a better name, but nothing in the table " +
                    "comes from it any more");
            }
        }

        [Test]
        public void TheHydraIsListedByTheNameThePlayerReads()
        {
            DimensionBorrowedAttacks.Preset swing =
                DimensionBorrowedAttacks.ByName("Desert Hydra — its close-up swing");
            Assert.IsNotNull(swing, "the desert hydra's swing is one of the game's own moves");
            Assert.AreEqual(
                "Pyrdra the Fire Titan — its close-up swing",
                DimensionBorrowedAttackCatalog.Label(swing));
        }

        [Test]
        public void ASourceWithNoBetterNameKeepsTheOneItHas()
        {
            DimensionBorrowedAttacks.Preset swing =
                DimensionBorrowedAttacks.ByName("Larva — its close-up swing");
            Assert.IsNotNull(swing);
            Assert.AreEqual("Larva — its close-up swing", DimensionBorrowedAttackCatalog.Label(swing));
        }

        // ---- what a creator is told before they choose ----

        [Test]
        public void EverySortOfMoveSaysWhatItIs()
        {
            string[] kinds = DimensionBorrowedAttacks.Kinds;
            Assert.Greater(kinds.Length, 0);
            for (int i = 0; i < kinds.Length; i++)
            {
                Assert.IsFalse(
                    string.IsNullOrEmpty(DimensionBorrowedAttackCatalog.WhatAKindIs(kinds[i])),
                    "'" + kinds[i] + "' is offered with nothing said about what it is");
            }
        }

        /// <summary>
        /// The patrol route that cannot travel is said out loud, and only where it applies.
        /// </summary>
        /// <remarks>
        /// Of the shipped prefabs carrying a random walk, only the robot miner and the robot
        /// patroller name a walk-pattern asset, and a mod cannot point at one. Every other wander
        /// arrives whole, so a warning on any of them would be noise.
        /// </remarks>
        [Test]
        public void TheWanderThatLeavesItsRouteBehindSaysSo()
        {
            DimensionBorrowedAttacks.Preset patrolling =
                DimensionBorrowedAttacks.ByName("Robot Miner — the way it wanders");
            Assert.IsNotNull(patrolling);
            Assert.IsNotEmpty(
                DimensionBorrowedAttackCatalog.Caveats(patrolling),
                "the route it walks lives inside the game and nobody is being told");

            DimensionBorrowedAttacks.Preset plain =
                DimensionBorrowedAttacks.ByName("Larva — the way it wanders");
            Assert.IsNotNull(plain);
            Assert.IsEmpty(
                DimensionBorrowedAttackCatalog.Caveats(plain),
                "a wander that arrives whole is being warned about for no reason");
        }

        /// <summary>
        /// A swing authored for a five-tile body is measured as one.
        /// </summary>
        /// <remarks>
        /// The reach is read off the move's own numbers, so this holds whatever the table is
        /// refreshed to. The hydra's swing reaches five tiles and the larva's two.
        /// </remarks>
        [Test]
        public void ASwingBuiltForABigBodyIsMeasuredAsOne()
        {
            DimensionBorrowedAttacks.Preset hydra =
                DimensionBorrowedAttacks.ByName("Desert Hydra — its close-up swing");
            DimensionBorrowedAttacks.Preset larva =
                DimensionBorrowedAttacks.ByName("Larva — its close-up swing");
            Assert.IsNotNull(hydra);
            Assert.IsNotNull(larva);

            Assert.GreaterOrEqual(
                DimensionBorrowedAttackCatalog.WidestReach(hydra),
                DimensionBorrowedAttackCatalog.SwingBuiltForABigBody);
            Assert.Less(
                DimensionBorrowedAttackCatalog.WidestReach(larva),
                DimensionBorrowedAttackCatalog.SwingBuiltForABigBody);
        }

        [Test]
        public void AMoveOutOfABossFightSaysThatIsWhatItIs()
        {
            Assert.IsTrue(DimensionBorrowedAttackCatalog.IsFromABossFight("Desert Hydra"));
            Assert.IsFalse(DimensionBorrowedAttackCatalog.IsFromABossFight("Larva"));
        }

        [Test]
        public void ADamageNumberIsRecognisedAsOne()
        {
            DimensionBorrowedAttacks.Preset swing =
                DimensionBorrowedAttacks.ByName("Larva — its close-up swing");
            DimensionBorrowedAttacks.Preset wander =
                DimensionBorrowedAttacks.ByName("Larva — the way it wanders");
            Assert.IsNotNull(swing);
            Assert.IsNotNull(wander);

            Assert.IsTrue(
                DimensionBorrowedAttackCatalog.CarriesADamageNumber(swing),
                "a swing carries a damage number, and whether it survives depends on the creature");
            Assert.IsFalse(
                DimensionBorrowedAttackCatalog.CarriesADamageNumber(wander),
                "a wander carries no damage, so warning about damage there would be noise");
        }

        // ---- what the harvest could not carry ----

        /// <summary>
        /// Every explosion named here is one the game actually has.
        /// </summary>
        /// <remarks>
        /// The numbers were read off the shipped prefabs and turned back into names against the
        /// game's own list. A typo would leave the creature with a timer and no bang, and the
        /// generator's own warning would blame the creator for it.
        /// </remarks>
        [Test]
        public void EveryExplosionNamedIsOneTheGameHas()
        {
            string[] sources = DimensionBorrowedAttackCatalog.SourcesWithAnExplosionNamed();
            Assert.Greater(sources.Length, 0);
            for (int i = 0; i < sources.Length; i++)
            {
                string explosion = DimensionBorrowedAttackCatalog.ExplosionFor(sources[i]);
                Assert.IsFalse(
                    string.IsNullOrEmpty(explosion),
                    "'" + sources[i] + "' is listed with no explosion");
                Assert.IsTrue(
                    Enum.IsDefined(typeof(ObjectID), explosion),
                    "'" + explosion + "' is named for '" + sources[i] +
                    "' and the game has nothing called that");
            }
        }

        /// <summary>
        /// Every explosion the table carries is one this file finishes off.
        /// </summary>
        /// <remarks>
        /// The other direction of the same check: a new thing that blows up, added to the table by
        /// a refresh, would arrive without its blast and nothing would say so.
        /// </remarks>
        [Test]
        public void EveryThingThatBlowsUpArrivesWithItsExplosion()
        {
            DimensionBorrowedAttacks.Preset[] explosions = DimensionBorrowedAttacks.OfKind("Explode");
            Assert.Greater(explosions.Length, 0);
            for (int i = 0; i < explosions.Length; i++)
            {
                DimensionBorrowedAttacks.Value[] finishing =
                    DimensionBorrowedAttackCatalog.FinishingValues(explosions[i]);
                Assert.IsNotNull(
                    finishing,
                    "'" + explosions[i].Name + "' arrives with no explosion, so it would run its " +
                    "timer and produce nothing");
                Assert.AreEqual("@ability.targetObjectId", finishing[0].Path);
            }
        }

        [Test]
        public void BorrowingAnExplosionWritesTheExplosionItSetsOff()
        {
            DimensionBorrowedAttacks.Preset scarab =
                DimensionBorrowedAttacks.ByName("Bomb Scarab — the way it explodes");
            Assert.IsNotNull(scarab);

            DimensionMobAsset asset = ScriptableObject.CreateInstance<DimensionMobAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                SerializedProperty combat = serialized.FindProperty("combat");
                Assert.IsNotNull(combat);

                DimensionBorrowedAttackUtility.Apply(combat, scarab, null);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual(1, asset.Combat.Abilities.Length);
                Assert.AreEqual(
                    DimensionCreatureAbilityKind.Explode,
                    asset.Combat.Abilities[0].Kind);
                Assert.AreEqual("Explosion", asset.Combat.Abilities[0].TargetObjectId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        // ---- the switch that decides whether anything reads it ----

        /// <summary>
        /// Taking a wander turns wandering on.
        /// </summary>
        /// <remarks>
        /// The generator only writes the random walk when idle movement says wander nearby, so a
        /// creature set to stand still used to take seven numbers, keep them, and never walk.
        /// </remarks>
        [Test]
        public void TakingAWanderTurnsWanderingOn()
        {
            DimensionBorrowedAttacks.Preset wander =
                DimensionBorrowedAttacks.ByName("Larva — the way it wanders");
            Assert.IsNotNull(wander);

            DimensionMobAsset asset = ScriptableObject.CreateInstance<DimensionMobAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                SerializedProperty combat = serialized.FindProperty("combat");
                Assert.IsNotNull(combat);

                SerializedProperty idle = combat.FindPropertyRelative("idleMovement");
                Assert.IsNotNull(idle, "a creature has to be able to say it stands still");
                idle.enumValueIndex = (int)DimensionCreatureIdleMovement.StandStill;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(
                    DimensionCreatureIdleMovement.StandStill,
                    asset.Combat.IdleMovement);

                DimensionBorrowedAttackUtility.Apply(combat, wander, null);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual(
                    DimensionCreatureIdleMovement.WanderNearby,
                    asset.Combat.IdleMovement,
                    "the wander landed its numbers into a creature that will never walk");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
#endif
