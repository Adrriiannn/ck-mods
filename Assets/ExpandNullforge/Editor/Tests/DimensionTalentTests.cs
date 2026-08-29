#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.Skills;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the three things a talent needs beyond its numbers: a position, a word and a picture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TALENT'S POSITION IN ITS SKILL'S LIST IS ITS IDENTITY. The game merges a mod's talent file
    /// into its own tree by position, so which square a row lands on is decided entirely by the
    /// grouping. Three separate passes depend on it — the file, the wording row, the picture — and
    /// if any two counted differently a talent would take its neighbour's picture with nothing
    /// said. These tests hold them to one count.
    /// </para>
    /// <para>
    /// The wording half is its own silent failure: the talent window renders
    /// <c>"SkillTalents/" + name</c> through the language table, and nothing in the framework ever
    /// wrote such a row, so a talent of a mod's own showed its raw key on the square.
    /// </para>
    /// </remarks>
    public sealed class DimensionTalentTests
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
            DimensionTalentIconRegistry.Clear();
        }

        // ---- the count ------------------------------------------------------------------------

        [Test]
        public void TalentsAreGroupedBySkillInTheOrderTheSkillsWereFirstMentioned()
        {
            List<DimensionTalentGroup> groups = DimensionTalentsTemplate.Group(
                new[]
                {
                    Talent("Melee", "First melee"),
                    Talent("Mining", "First mining"),
                    Talent("Melee", "Second melee")
                },
                null);

            Assert.That(groups.Count, Is.EqualTo(2));
            Assert.That(groups[0].Skill, Is.EqualTo(SkillID.Melee));
            Assert.That(groups[0].Talents.Length, Is.EqualTo(2));
            Assert.That(groups[0].Talents[0].TalentName, Is.EqualTo("First melee"));
            Assert.That(groups[0].Talents[1].TalentName, Is.EqualTo("Second melee"));
            Assert.That(groups[1].Skill, Is.EqualTo(SkillID.Mining));
        }

        [Test]
        public void ASkillTheGameDoesNotHaveIsHandedBackRatherThanGuessedAt()
        {
            List<string> unknown = new List<string>();
            List<DimensionTalentGroup> groups = DimensionTalentsTemplate.Group(
                new[] { Talent("Woodcutting", "Chopper"), Talent("Mining", "Digger") },
                unknown);

            Assert.That(unknown, Is.EqualTo(new[] { "Woodcutting" }));
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].Skill, Is.EqualTo(SkillID.Mining));
        }

        [Test]
        public void ARowWithNoSkillAtAllIsLeftOutRatherThanReported()
        {
            List<string> unknown = new List<string>();
            List<DimensionTalentGroup> groups = DimensionTalentsTemplate.Group(
                new[] { Talent(string.Empty, "Nowhere") },
                unknown);

            Assert.That(
                unknown,
                Is.Empty,
                "An empty row is a row nobody filled in yet, not a skill the game is missing.");
            Assert.That(groups, Is.Empty);
        }

        // ---- the word -------------------------------------------------------------------------

        [Test]
        public void ATalentReusingTheGamesOwnNameGetsNoLineOfOurs()
        {
            List<DimensionLocalizationCsv.Row> rows = RowsFor(
                Talent("Mining", "MiningDamagePercentage"));

            Assert.That(
                TalentRows(rows),
                Is.Empty,
                "That name already has wording in every language Core Keeper ships. Writing a row " +
                "over it would replace twelve translations with one, and the whole point of " +
                "reusing the name is to change only the numbers.");
        }

        [Test]
        public void ATalentWithANameOfItsOwnGetsTheLineTheSquareReads()
        {
            List<DimensionLocalizationCsv.Row> rows = RowsFor(
                Talent("Mining", "modDeepDigger", "Deep Digger"));

            List<DimensionLocalizationCsv.Row> talents = TalentRows(rows);
            Assert.That(talents.Count, Is.EqualTo(1));
            Assert.That(
                talents[0].Key,
                Is.EqualTo("SkillTalents/modDeepDigger"),
                "The talent window builds its term as \"SkillTalents/\" plus the talent's name, so " +
                "the key is the name and not something minted beside it.");
            Assert.That(talents[0].EnglishText, Is.EqualTo("Deep Digger"));
        }

        [Test]
        public void ATalentWithNoWordingOfItsOwnStillReadsAsSomething()
        {
            List<DimensionLocalizationCsv.Row> talents =
                TalentRows(RowsFor(Talent("Mining", "modDeepDigger")));

            Assert.That(talents.Count, Is.EqualTo(1));
            Assert.That(
                talents[0].EnglishText,
                Is.EqualTo("modDeepDigger"),
                "Showing the name is worse than showing a written line and far better than showing " +
                "SkillTalents/modDeepDigger, which is what a missing row leaves on the square.");
        }

        [Test]
        public void AKeyIsWrittenWithTheUnderscoreTheGameLooksFor()
        {
            List<DimensionLocalizationCsv.Row> rows = new List<DimensionLocalizationCsv.Row>();
            DimensionLocalizationCsv.AddTalentRow(rows, "mod:deepDigger", "Deep Digger");

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(
                rows[0].Key,
                Is.EqualTo("SkillTalents/mod_deepDigger"),
                "Core Keeper rewrites a term with Replace(':', '_') before it searches, and it " +
                "does that to the key it builds from the talent's name too — so the stored key " +
                "has to be the rewritten one for the two to meet.");
        }

        // ---- the picture ----------------------------------------------------------------------

        [Test]
        public void APictureIsHeldAgainstTheSquareItWasWrittenFor()
        {
            Sprite icon = Icon();
            DimensionTalentIconRegistry.Register(SkillID.Mining, 3, icon);

            Sprite found;
            Assert.That(DimensionTalentIconRegistry.TryGet(SkillID.Mining, 3, out found), Is.True);
            Assert.That(found, Is.SameAs(icon));

            Assert.That(
                DimensionTalentIconRegistry.TryGet(SkillID.Mining, 4, out found),
                Is.False,
                "The square next door keeps whatever picture the game gave it.");
            Assert.That(
                DimensionTalentIconRegistry.TryGet(SkillID.Melee, 3, out found),
                Is.False,
                "The same position on another tree is another square.");
        }

        [Test]
        public void NothingIsHeldUntilSomethingIsRegistered()
        {
            Assert.That(DimensionTalentIconRegistry.HasAny, Is.False);
            DimensionTalentIconRegistry.Register(SkillID.Mining, 0, Icon());
            Assert.That(DimensionTalentIconRegistry.HasAny, Is.True);
            DimensionTalentIconRegistry.Clear();
            Assert.That(DimensionTalentIconRegistry.HasAny, Is.False);
        }

        [Test]
        public void ASquareWithNoPictureIsNotRegisteredAsHavingOne()
        {
            DimensionTalentIconRegistry.Register(SkillID.Mining, 0, null);

            Assert.That(
                DimensionTalentIconRegistry.HasAny,
                Is.False,
                "Registering a blank would make the talent window ask about every square for " +
                "nothing, and would overwrite the game's own picture with none.");
        }

        // ---- helpers --------------------------------------------------------------------------

        /// <summary>
        /// One authored talent row. Built through the serialized fields, which is the only way to
        /// fill a struct whose fields are private.
        /// </summary>
        private DimensionTalent Talent(string skill, string name, string shownAs = null)
        {
            return Setup(new[] { Row(skill, name, shownAs) }).Talents.Talents[0];
        }

        private static KeyValuePair<string, string[]> Row(
            string skill,
            string name,
            string shownAs)
        {
            return new KeyValuePair<string, string[]>(skill, new[] { name, shownAs });
        }

        private List<DimensionLocalizationCsv.Row> RowsFor(params DimensionTalent[] talents)
        {
            List<KeyValuePair<string, string[]>> rows =
                new List<KeyValuePair<string, string[]>>();
            for (int i = 0; i < talents.Length; i++)
            {
                rows.Add(Row(
                    talents[i].Skill,
                    talents[i].TalentName,
                    talents[i].ShownAs));
            }

            // The rows come out of the whole plan rather than out of the csv helper, so the test
            // fails if AddTalents is ever left uncalled — the shape of bug this domain keeps
            // finding.
            DimensionTemplateAsset template =
                ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            made.Add(template);
            template.SetGlobalGameSetups(new[] { Setup(rows) });

            return DimensionLocalizationPlan.Build(
                template, new DimensionNamingContext("MyMod", null)).Rows;
        }

        private static List<DimensionLocalizationCsv.Row> TalentRows(
            List<DimensionLocalizationCsv.Row> rows)
        {
            List<DimensionLocalizationCsv.Row> found =
                new List<DimensionLocalizationCsv.Row>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key.StartsWith("SkillTalents/", System.StringComparison.Ordinal))
                {
                    found.Add(rows[i]);
                }
            }

            return found;
        }

        private DimensionGameSetupAsset Setup(IList<KeyValuePair<string, string[]>> rows)
        {
            DimensionGameSetupAsset setup =
                ScriptableObject.CreateInstance<DimensionGameSetupAsset>();
            made.Add(setup);

            SerializedObject serialized = new SerializedObject(setup);
            serialized.FindProperty("setupIdentifier").stringValue = "rules";
            serialized.FindProperty("displayName").stringValue = "rules";
            serialized.FindProperty("enabled").boolValue = true;
            serialized.FindProperty("talents.changesWhatTalentsGive").boolValue = true;
            SerializedProperty array = serialized.FindProperty("talents.talents");
            array.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("skill").stringValue = rows[i].Key;
                element.FindPropertyRelative("talentName").stringValue = rows[i].Value[0];
                element.FindPropertyRelative("gives").stringValue = string.Empty;
                element.FindPropertyRelative("perPoint").intValue = 1;
                element.FindPropertyRelative("shownAs").stringValue =
                    rows[i].Value[1] ?? string.Empty;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return setup;
        }

        private Sprite Icon()
        {
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            made.Add(sprite);
            made.Add(texture);
            return sprite;
        }
    }
}
#endif
