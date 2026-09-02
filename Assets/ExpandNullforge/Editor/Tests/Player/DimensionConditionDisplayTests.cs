#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using ExpandNullforge.Authoring;
using ExpandNullforge.Conditions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the half of a custom stat effect that a player actually looks at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE GAME KEEPS TWO CONDITION TABLES AND ONLY ONE OF THEM WAS BEING FILLED. The simulation's
    /// copy is a blob the framework already builds. The interface's copy is a plain list on
    /// <c>ConditionsTable</c>, exactly 357 long, read with no bounds check — so the first condition
    /// a mod invented, number 357, threw on the first frame anything looked at it, which the buff
    /// row does twice every frame.
    /// </para>
    /// <para>
    /// These tests hold the four things that decision has to get right: that a vanilla number is
    /// left entirely alone, that a number of ours is answered rather than thrown on, that the
    /// answer carries its own id (without which the wording lookup silently becomes
    /// <c>Conditions/None</c>), and that a number above everything claimed is still answered
    /// instead of falling through to the exception.
    /// </para>
    /// </remarks>
    public sealed class DimensionConditionDisplayTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            for (int i = 0; i < made.Count; i++)
            {
                if (made[i] != null)
                {
                    Object.DestroyImmediate(made[i]);
                }
            }

            made.Clear();
            DimensionConditionRegistry.Clear();
            DimensionConditionInfoPatch.ForgetReports();
        }

        // ---- who answers ---------------------------------------------------------------------

        [Test]
        public void WithNothingClaimedTheGamesOwnTableAnswersEverything()
        {
            ConditionInfo info;

            Assert.That(
                DimensionConditionInfoPatch.TryAnswer(ConditionID.MiningIncrease, out info),
                Is.False);
            Assert.That(
                DimensionConditionInfoPatch.TryAnswer(
                    (ConditionID)DimensionConditionRegistry.FirstFreeNumber, out info),
                Is.False,
                "With no mod conditions there is nothing of ours at that number, and standing in " +
                "front of the game's own lookup would cost every player a check for nothing.");
        }

        [Test]
        public void AVanillaNumberIsLeftToTheGameEvenWhenAModHasItsOwn()
        {
            Claim("mod:brisk");

            ConditionInfo info;
            Assert.That(
                DimensionConditionInfoPatch.TryAnswer(ConditionID.MiningIncrease, out info),
                Is.False);
            Assert.That(
                DimensionConditionInfoPatch.TryAnswer(
                    (ConditionID)(DimensionConditionRegistry.FirstFreeNumber - 1), out info),
                Is.False,
                "The last of the game's own numbers is still one of the game's own.");
        }

        [Test]
        public void ANumberThisModClaimedIsAnswered()
        {
            Claim("mod:brisk");
            ConditionID id = DimensionConditionRegistry.IdFor("mod:brisk");

            ConditionInfo info;
            Assert.That(DimensionConditionInfoPatch.TryAnswer(id, out info), Is.True);
            Assert.That(
                info.Id,
                Is.EqualTo(id),
                "ConditionUI builds the wording key from conditionInfo.Id. Leaving it at None " +
                "makes the key Conditions/None, so the buff shows the game's blank line and " +
                "nothing anywhere says why.");
        }

        // ---- what the answer says ------------------------------------------------------------

        [Test]
        public void TheAnswerCarriesEverythingTheAssetSaid()
        {
            DimensionConditionAsset asset = FullyAnswered("mod:brisk");
            DimensionConditionRegistry.Claim(DimensionCustomCondition.From(asset));

            ConditionInfo info;
            Assert.That(
                DimensionConditionInfoPatch.TryAnswer(
                    DimensionConditionRegistry.IdFor("mod:brisk"), out info),
                Is.True);

            Assert.That(info.effect, Is.EqualTo(ConditionEffect.MiningSpeed));
            Assert.That(info.isNegative, Is.True);
            Assert.That(info.isPermanent, Is.True);
            Assert.That(info.isUnique, Is.True);
            Assert.That(info.isAdditiveWithSelf, Is.True);
            Assert.That(info.isInheritedByProjectiles, Is.True);
            Assert.That(info.overrideIfRemainingValueIsHigher, Is.True);
            Assert.That(info.showDecimal, Is.True);
            Assert.That(info.skipShowingSignInfrontOfValue, Is.True);
            Assert.That(info.skipShowingStatText, Is.True);
            Assert.That(
                info.useSameDescAsId,
                Is.EqualTo(ConditionID.MiningSpeedIncrease),
                "Borrowing another effect's wording is answered by name, and the game's own names " +
                "come first — the same order the spine resolves a condition in.");
        }

        [Test]
        public void OneOfThisModsOwnCanBorrowAnothersWording()
        {
            DimensionConditionAsset borrowed = Condition("mod:aaaFirst");
            DimensionConditionAsset borrower = Condition("mod:zzzSecond");
            SetString(borrower, "readsLikeThisOne", "mod:aaaFirst");

            DimensionConditionRegistry.Claim(DimensionCustomCondition.From(borrowed));
            DimensionConditionRegistry.Claim(DimensionCustomCondition.From(borrower));

            ConditionInfo info;
            Assert.That(
                DimensionConditionInfoPatch.TryAnswer(
                    DimensionConditionRegistry.IdFor("mod:zzzSecond"), out info),
                Is.True);
            Assert.That(
                info.useSameDescAsId,
                Is.EqualTo(DimensionConditionRegistry.IdFor("mod:aaaFirst")),
                "The name is resolved when the game asks, not when the claim is made — the " +
                "borrowed condition's own number is not known until the whole set is claimed.");
        }

        [Test]
        public void ANumberNothingClaimsIsAnsweredBlankRatherThanThrownOn()
        {
            Claim("mod:brisk");
            int past = DimensionConditionRegistry.FirstFreeNumber + 50;

            ConditionInfo info;
            Assert.That(
                DimensionConditionInfoPatch.TryAnswer((ConditionID)past, out info),
                Is.True,
                "This is what an old save looks like after a condition was renamed. Letting it " +
                "through would land on the very indexer the whole patch exists to keep in range.");
            Assert.That(info.Id, Is.EqualTo((ConditionID)past));
            Assert.That(
                info.icon,
                Is.Null,
                "No picture is how the buff row is told to skip an entry, which is the right way " +
                "for a buff nobody can explain to disappear.");
        }

        // ---- one construction site -----------------------------------------------------------

        /// <summary>
        /// Every value on the record has to come from the asset, or a field added to one and not
        /// the other goes to the game as its default with nothing said.
        /// </summary>
        /// <remarks>
        /// Written as a sweep rather than as a list, because a list is the thing that goes stale.
        /// The asset below answers every question differently from the default, so a property left
        /// at its default after <c>From</c> is a property <c>From</c> forgot.
        /// </remarks>
        [Test]
        public void FromFillsEveryValueTheRecordHas()
        {
            DimensionCustomCondition filled =
                DimensionCustomCondition.From(FullyAnswered("mod:brisk"));
            DimensionCustomCondition untouched = new DimensionCustomCondition("mod:brisk");

            PropertyInfo[] properties = typeof(DimensionCustomCondition).GetProperties(
                BindingFlags.Public | BindingFlags.Instance);
            int checkedCount = 0;
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetSetMethod() == null)
                {
                    // Name has no setter: it is the identity, and From passes it to the
                    // constructor rather than assigning it.
                    continue;
                }

                object mine = property.GetValue(filled);
                object theirs = property.GetValue(untouched);
                Assert.That(
                    object.Equals(mine, theirs),
                    Is.False,
                    property.Name + " came out of From with the value a brand new record has, so " +
                    "nothing on the asset reaches it. Fill it in DimensionCustomCondition.From.");
                checkedCount++;
            }

            Assert.That(
                checkedCount,
                Is.GreaterThan(0),
                "A sweep that checks nothing passes for the wrong reason.");
            Assert.That(filled.Name, Is.EqualTo("mod:brisk"));
        }

        // ---- helpers -------------------------------------------------------------------------

        private void Claim(string name)
        {
            DimensionConditionRegistry.Claim(new DimensionCustomCondition(name));
        }

        private DimensionConditionAsset Condition(string name)
        {
            DimensionConditionAsset asset =
                ScriptableObject.CreateInstance<DimensionConditionAsset>();
            made.Add(asset);
            SetString(asset, "conditionName", name);
            SetString(asset, "displayName", name);
            return asset;
        }

        /// <summary>An asset where every question has an answer other than its default.</summary>
        private DimensionConditionAsset FullyAnswered(string name)
        {
            DimensionConditionAsset asset = Condition(name);
            Sprite icon = Sprite.Create(
                new Texture2D(4, 4), new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            made.Add(icon);
            made.Add(icon.texture);

            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("effect").enumValueIndex =
                System.Array.IndexOf(
                    serialized.FindProperty("effect").enumNames,
                    ConditionEffect.MiningSpeed.ToString());
            serialized.FindProperty("isBad").boolValue = true;
            serialized.FindProperty("lastsForever").boolValue = true;
            serialized.FindProperty("addsToItself").boolValue = true;
            serialized.FindProperty("onlyOneAtATime").boolValue = true;
            serialized.FindProperty("passedOnByShots").boolValue = true;
            serialized.FindProperty("aStrongerOneWins").boolValue = true;
            serialized.FindProperty("showsADecimal").boolValue = true;
            serialized.FindProperty("hidesThePlusSign").boolValue = true;
            serialized.FindProperty("hidesTheStatLine").boolValue = true;
            serialized.FindProperty("readsLikeThisOne").stringValue =
                ConditionID.MiningSpeedIncrease.ToString();
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void SetString(Object target, string path, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(path).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
