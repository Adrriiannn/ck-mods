using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Creatures;
using ExpandNullforge.Skills;
using ExpandNullforge.WorldRules;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the six of Core Keeper's shared tables the framework had never reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THESE ARE GUARDING. Each of the six is written at exactly one moment, and each has one
    /// thing about that moment which is easy to get wrong and impossible to see afterwards.
    /// </para>
    /// <para>
    /// Set bonuses: the game builds two lookups keyed by the ITEM and adds to both with methods that
    /// throw on a repeat, so a piece claimed by two sets is an exception thrown while a world is
    /// being built rather than a cosmetic problem. And numbers must come from the names, because a
    /// number that moves between loads is a number that ends up in a save meaning something else.
    /// </para>
    /// <para>
    /// World events: the game keys its bake by the EVENT and adds with a method that throws on a
    /// repeat, so one event must never end up with two rows however many times it is named; and an
    /// event with no row at all is the game's own way of saying it never happens.
    /// </para>
    /// <para>
    /// The game's own terrain: the generator plays a tile's matching rules back in REVERSE, so a
    /// rule appended at the end of the list would lose to every vanilla rule matching the same tile.
    /// The front of the list is the winning end and the tests say so.
    /// </para>
    /// <para>
    /// Backgrounds, skill pictures and pet colours are all answered where the game asks rather than
    /// written into its assets, so what they must get right is answering for what was claimed and
    /// leaving everything else exactly as the game had it.
    /// </para>
    /// <para>
    /// EVERY TEST HERE FAILS ON AN EMPTY SUBJECT. Each one either registers its own subject first,
    /// or is one of the four written specifically to prove that nothing was registered.
    /// </para>
    /// </remarks>
    public sealed partial class DimensionResourceTableReachTests
    {

        [SetUp]
        public void Setup()
        {
            DimensionSetBonusRegistry.Clear();
            DimensionBackgroundRegistry.Clear();
            DimensionEnvironmentEventRegistry.Clear();
            DimensionWorldTerrainRuleRegistry.Clear();
            DimensionSkillIconRegistry.Clear();
            DimensionPetSkinRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DimensionSetBonusRegistry.Clear();
            DimensionBackgroundRegistry.Clear();
            DimensionEnvironmentEventRegistry.Clear();
            DimensionWorldTerrainRuleRegistry.Clear();
            DimensionSkillIconRegistry.Clear();
            DimensionPetSkinRegistry.Clear();
        }

        // ---- the game's own terrain --------------------------------------------------------

        [Test]
        public void AModsTerrainRuleIsWrittenInFrontOfTheGamesOwn()
        {
            DimensionWorldTerrainRuleRegistry.Register(
                (int)PugWorldGen.CoreKeeper.Biome.Stone,
                0,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                0,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                (int)WorldGen.TileTypeMapping.ResourceIndex.Resource4,
                (int)PugTilemap.TileType.ore,
                1234567);

            List<WorldGen.TileTypeMapping.MappingRule> edited =
                DimensionWorldTerrainRuleRegistry.BuildEditedList(
                    new List<WorldGen.TileTypeMapping.MappingRule>
                    {
                        new WorldGen.TileTypeMapping.MappingRule
                        {
                            outputTile = new WorldGen.TileTypeMapping.MappingResult
                            {
                                tileType = PugTilemap.TileType.ore,
                                tileset = (PugTilemap.Tileset)1
                            }
                        }
                    });

            Assert.AreEqual(2, edited.Count);
            Assert.AreEqual(
                1234567,
                (int)edited[0].outputTile.tileset,
                "A mod's rule was not put in front. The generator pushes matching rules into a " +
                "NativeParallelMultiHashMap in list order and that map enumerates a key's values " +
                "in reverse, so the earliest matching rule is written last and wins. At the back " +
                "of the list a mod's rule loses to every vanilla rule that matched.");
            Assert.AreEqual(1, (int)edited[1].outputTile.tileset);
        }

        [Test]
        public void ATerrainRuleKeepsEveryPartOfItsQuestion()
        {
            DimensionWorldTerrainRuleRegistry.Register(
                (int)PugWorldGen.CoreKeeper.Biome.Clay,
                (int)PugWorldGen.CoreKeeper.TileType.Resource,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                (int)WorldGen.TileTypeMapping.FlagState.True,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                (int)WorldGen.TileTypeMapping.ResourceIndex.Resource5,
                (int)PugTilemap.TileType.wall,
                4242);

            WorldGen.TileTypeMapping.MappingRule rule =
                DimensionWorldTerrainRuleRegistry.BuildEditedList(null)[0];

            Assert.AreEqual(PugWorldGen.CoreKeeper.Biome.Clay, rule.biome);
            Assert.AreEqual(PugWorldGen.CoreKeeper.TileType.Resource, rule.proceduralTileType);
            Assert.AreEqual(WorldGen.TileTypeMapping.FlagState.False, rule.floorFlag);
            Assert.AreEqual(WorldGen.TileTypeMapping.FlagState.True, rule.roofHoleFlag);
            Assert.AreEqual(WorldGen.TileTypeMapping.FlagState.False, rule.greatWallFlag);
            Assert.AreEqual(WorldGen.TileTypeMapping.ResourceIndex.Resource5, rule.resourceIndex);
            Assert.AreEqual(PugTilemap.TileType.wall, rule.outputTile.tileType);
            Assert.AreEqual(4242, (int)rule.outputTile.tileset);
        }

        /// <summary>
        /// A rule registered word for word twice is queued once; one that differs is queued again.
        /// </summary>
        /// <remarks>
        /// The generated consumer's <c>Shutdown()</c> clears only its own "already registered"
        /// flag, so a consumer reloaded without the framework reloading re-runs every
        /// <c>Register</c> against a registry nothing emptied. The other five tables replace by key
        /// and survive that; this one appended, so every reload put another copy of every rule at
        /// the front of Core Keeper's table and into the native array the generator keeps per
        /// world. The second half of this test is the objection the old comment raised: two rules
        /// differing only in the block they lay are two rules, and both are kept.
        /// </remarks>
        [Test]
        public void TheSameTerrainRuleTwiceIsOneRuleAndADifferentOneIsTwo()
        {
            for (int i = 0; i < 3; i++)
            {
                DimensionWorldTerrainRuleRegistry.Register(
                    (int)PugWorldGen.CoreKeeper.Biome.Stone, 0, 0, 0, 0,
                    (int)WorldGen.TileTypeMapping.ResourceIndex.Resource4,
                    (int)PugTilemap.TileType.ore,
                    1234567);
            }

            Assert.AreEqual(
                1,
                DimensionWorldTerrainRuleRegistry.PendingCount,
                "Three loads of the same rule left three copies. Nothing looks wrong in the world " +
                "— the same block is laid twice — so the list simply grows for the session.");

            DimensionWorldTerrainRuleRegistry.Register(
                (int)PugWorldGen.CoreKeeper.Biome.Stone, 0, 0, 0, 0,
                (int)WorldGen.TileTypeMapping.ResourceIndex.Resource4,
                (int)PugTilemap.TileType.ore,
                7654321);

            Assert.AreEqual(
                2,
                DimensionWorldTerrainRuleRegistry.PendingCount,
                "Two rules that lay different blocks were treated as one rule. They are not: " +
                "every matching rule puts its answer down.");
        }

        // ---- skill pictures ----------------------------------------------------------------

        [Test]
        public void HalfAClaimKeepsTheGamesOtherHalf()
        {
            Sprite mine = Sprite.Create(
                new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            Sprite theirGold = Sprite.Create(
                new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                DimensionSkillIconRegistry.Register(SkillID.Mining, mine, null);

                SkillIcon theirs = new SkillIcon
                {
                    skillID = SkillID.Mining,
                    icon = null,
                    goldIcon = theirGold
                };

                SkillIcon ours;
                Assert.IsTrue(DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, theirs, out ours));
                Assert.AreSame(mine, ours.icon);
                Assert.AreSame(
                    theirGold,
                    ours.goldIcon,
                    "A mod that repainted only the ordinary picture blanked the game's gold one.");

                SkillIcon untouched;
                Assert.IsFalse(
                    DimensionSkillIconRegistry.TryAnswer(SkillID.Fishing, theirs, out untouched),
                    "A skill nobody claimed was answered for.");
            }
            finally
            {
                Object.DestroyImmediate(mine);
                Object.DestroyImmediate(theirGold);
            }
        }

        /// <summary>
        /// The answer for one skill is built once rather than on every ask.
        /// </summary>
        /// <remarks>
        /// <c>SkillUIElement.LateUpdate</c> asks <c>GetIcon</c> once or twice per element per
        /// frame, so a fresh <c>SkillIcon</c> per answer was an allocation per claimed skill per
        /// frame for as long as the skill window was open. The game's answer for one skill is the
        /// same object every time — it comes out of a serialized list — so the built answer is kept
        /// beside it.
        /// </remarks>
        [Test]
        public void TheAnswerForOneSkillIsBuiltOnceRatherThanOnEveryAsk()
        {
            Sprite mine = Sprite.Create(
                new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                DimensionSkillIconRegistry.Register(SkillID.Mining, mine, null);
                SkillIcon theirs = new SkillIcon { skillID = SkillID.Mining };

                SkillIcon first;
                SkillIcon second;
                DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, theirs, out first);
                DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, theirs, out second);

                Assert.AreSame(
                    first,
                    second,
                    "A second ask built a second answer. This is asked from LateUpdate, so that " +
                    "is one allocation per claimed skill per frame while the window is open.");

                // A different icon from the game is a different question and is answered afresh.
                SkillIcon other = new SkillIcon { skillID = SkillID.Mining };
                SkillIcon third;
                DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, other, out third);
                Assert.AreNotSame(first, third);
            }
            finally
            {
                Object.DestroyImmediate(mine);
            }
        }

        /// <summary>
        /// A skill written as a number is refused rather than registered against NUM_SKILLS.
        /// </summary>
        /// <remarks>
        /// <c>Enum.TryParse("12")</c> on <c>SkillID</c> comes back as <c>NUM_SKILLS</c>, which is
        /// the count of the skills rather than one of them, so the picture registered against it
        /// was one nothing would ever ask for and the spelling complaint was never printed. Checked
        /// through <c>AttachFrom</c> because that is the only caller that turns a written name into
        /// a skill.
        /// </remarks>
        [Test]
        public void ASkillWrittenAsANumberIsRefusedRatherThanRegisteredAgainstTheCount()
        {
            // NUM_SKILLS is 12 and is not a skill. Nothing ever asks GetIcon for it, so a picture
            // registered against it is a control that reaches nothing, and the spelling complaint
            // that would have told the creator so is never printed.
            Assert.AreEqual(
                12,
                (int)SkillID.NUM_SKILLS,
                "The game's skill count moved, so the number this test uses has to move too.");

            SkillID parsed;
            Assert.IsFalse(
                DimensionSkillIconRegistry.TheGamesOwnSkill("12", out parsed),
                "A skill written as a number was taken as a skill. Enum.TryParse answers 12 with " +
                "NUM_SKILLS, which is the count of the skills rather than one of them.");
            Assert.IsFalse(
                DimensionSkillIconRegistry.TheGamesOwnSkill("NUM_SKILLS", out parsed),
                "NUM_SKILLS is the count, not a skill.");
            Assert.IsFalse(DimensionSkillIconRegistry.TheGamesOwnSkill("Woodcutting", out parsed));
            Assert.IsFalse(DimensionSkillIconRegistry.TheGamesOwnSkill("", out parsed));

            string[] real =
            {
                "Mining", "Running", "Melee", "Vitality", "Crafting", "Range",
                "Gardening", "Fishing", "Cooking", "Magic", "Summoning", "Explosives"
            };
            for (int i = 0; i < real.Length; i++)
            {
                Assert.IsTrue(
                    DimensionSkillIconRegistry.TheGamesOwnSkill(real[i], out parsed),
                    real[i] + " is one of the game's own twelve and was refused.");
                Assert.AreEqual(i, (int)parsed, real[i] + " answered the wrong skill.");
            }
        }

        [Test]
        public void ARowWithNoPictureAtAllClaimsNothing()
        {
            DimensionSkillIconRegistry.Register(SkillID.Cooking, null, null);
            Assert.IsFalse(
                DimensionSkillIconRegistry.HasAny,
                "An empty row claimed a skill, which would draw the square blank rather than " +
                "leave the game's own picture on it.");
        }

        // ---- pet colours -------------------------------------------------------------------

        [Test]
        public void APetWithNoColoursIsNotClaimed()
        {
            DimensionPetSkinRegistry.Register("MyMod:Sprout", new GradientMapDataBlock[0]);
            DimensionPetSkinRegistry.Register("MyMod:Sprout", null);

            Assert.IsFalse(
                DimensionPetSkinRegistry.HasAny,
                "A pet with no colours was claimed, which would answer the converter with an " +
                "empty skin list instead of letting the game's own null through.");
        }

        /// <summary>
        /// A pet id written without the mod in front of it still finds the pet.
        /// </summary>
        /// <remarks>
        /// The generator stamps a creature's object name with the mod in front of it, and that is
        /// the only key <c>API.Authoring.GetObjectID</c> answers to. Every other reader of a mob id
        /// qualifies it while generating; this one is read off the template at load. The wizard
        /// seeds the id already qualified, so the ordinary flow worked — and a creator who typed a
        /// plain id, which every other feature accepts, got a pet that still converted with no
        /// skins, which is the bug this table was opened up to fix.
        /// </remarks>
        [Test]
        public void APetIdWrittenWithoutTheModInFrontOfItStillFindsThePet()
        {
            GradientMapDataBlock colour = ScriptableObject.CreateInstance<GradientMapDataBlock>();
            try
            {
                DimensionPetSkinRegistry.Register("fluff", new[] { colour }, "MyMod");

                PetInfosTable.PetSkinInfo info;
                Assert.IsTrue(
                    DimensionPetSkinRegistry.TryAnswer(
                        (ObjectID)ModdedHelm,
                        name => string.Equals(name, "MyMod:fluff", System.StringComparison.Ordinal)
                            ? (ObjectID)ModdedHelm
                            : ObjectID.None,
                        out info),
                    "A pet whose id was written without the mod in front of it was not found, so " +
                    "it converts with no skins at all — silently, with the patch reporting that " +
                    "it fired.");
                Assert.AreEqual(1, info.skins.Count);

                // A name that was already qualified is asked for exactly as written and nothing is
                // stuck in front of it twice.
                DimensionPetSkinRegistry.Clear();
                DimensionPetSkinRegistry.Register("MyMod:fluff", new[] { colour }, "MyMod");

                PetInfosTable.PetSkinInfo already;
                Assert.IsTrue(
                    DimensionPetSkinRegistry.TryAnswer(
                        (ObjectID)ModdedHelm,
                        name => string.Equals(name, "MyMod:fluff", System.StringComparison.Ordinal)
                            ? (ObjectID)ModdedHelm
                            : ObjectID.None,
                        out already),
                    "An id that already carried the mod's name was mangled.");
            }
            finally
            {
                Object.DestroyImmediate(colour);
            }
        }

        [Test]
        public void RegisteringOnePetTwiceReplacesRatherThanStacks()
        {
            GradientMapDataBlock one = ScriptableObject.CreateInstance<GradientMapDataBlock>();
            GradientMapDataBlock two = ScriptableObject.CreateInstance<GradientMapDataBlock>();
            try
            {
                DimensionPetSkinRegistry.Register("MyMod:Sprout", new[] { one });
                DimensionPetSkinRegistry.Register("MyMod:Sprout", new[] { one, two });

                Assert.AreEqual(
                    1,
                    DimensionPetSkinRegistry.Count,
                    "One pet ended up with two claims, so which colours it came in would depend " +
                    "on which claim was found first.");
            }
            finally
            {
                Object.DestroyImmediate(one);
                Object.DestroyImmediate(two);
            }
        }

        // ---- silence is a defect: these fail on an empty subject ------------------------------

        [Test]
        public void AnEmptySetRegistryHandsOutNoNumbersAndAppendsNothing()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>();
                Assert.IsFalse(DimensionSetBonusRegistry.HasAny);
                Assert.AreEqual(-1, DimensionSetBonusRegistry.NumberFor("anything", 100));
                Assert.AreEqual(
                    0,
                    DimensionSetBonusRegistry.AppendTo(
                        table, 100, ResolveModdedPiece, ResolveEffect, null));
                Assert.IsEmpty(table.setBonuses);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void AnEmptyWorldEventRegistryHandsTheGamesOwnListStraightBack()
        {
            List<EnvironmentEventParams> game = new List<EnvironmentEventParams>
            {
                Vanilla(EnvironmentEventType.CaveIn),
                Vanilla(EnvironmentEventType.SpawnLarvas)
            };

            List<EnvironmentEventParams> edited =
                DimensionEnvironmentEventRegistry.BuildEditedList(game, ResolveBiome, null);

            Assert.AreEqual(2, edited.Count);
            Assert.AreEqual(EnvironmentEventType.CaveIn, edited[0].eventType);
            Assert.AreEqual(EnvironmentEventType.SpawnLarvas, edited[1].eventType);
        }

        [Test]
        public void AnEmptyTerrainRegistryAddsNothingInFrontOfTheGamesRules()
        {
            List<WorldGen.TileTypeMapping.MappingRule> game =
                new List<WorldGen.TileTypeMapping.MappingRule>
                {
                    new WorldGen.TileTypeMapping.MappingRule()
                };

            Assert.IsFalse(DimensionWorldTerrainRuleRegistry.HasAny);
            Assert.AreEqual(1, DimensionWorldTerrainRuleRegistry.BuildEditedList(game).Count);
        }

        [Test]
        public void AnEmptyBackgroundRegistryAnswersForNothing()
        {
            Assert.IsFalse(DimensionBackgroundRegistry.HasAny);
            for (int background = 0; background <= 10; background++)
            {
                RolePerksTable.Perks unused;
                Assert.IsFalse(
                    DimensionBackgroundRegistry.TryAnswer(
                        background, TheGamesOwnChef(), ResolveModdedPiece, null, out unused),
                    "Background " + background + " was answered for by an empty registry, so " +
                    "every one of the game's own kits would be replaced by an empty one.");
            }
        }

        // ---- fixtures ------------------------------------------------------------------------

        private static DimensionSetBonusRegistry.LineRow[] OneLine(string effect)
        {
            return new[]
            {
                new DimensionSetBonusRegistry.LineRow
                {
                    EffectName = effect,
                    RequiredPieces = 2,
                    Strength = 1f,
                    Seconds = 0f
                }
            };
        }

        /// <summary>Stands in for the mod's own objects having been given numbers.</summary>
        private static ObjectID ResolveModdedPiece(string name)
        {
            if (string.Equals(name, "MyMod:Helm", System.StringComparison.Ordinal))
            {
                return (ObjectID)ModdedHelm;
            }

            if (string.Equals(name, "MyMod:Chest", System.StringComparison.Ordinal))
            {
                return (ObjectID)ModdedChest;
            }

            return ObjectID.None;
        }

        private static ConditionID ResolveEffect(string name)
        {
            ConditionID parsed;
            return System.Enum.TryParse(name, false, out parsed) ? parsed : ConditionID.None;
        }

        private static Biome ResolveBiome(string id)
        {
            Biome parsed;
            return System.Enum.TryParse(id, false, out parsed) ? parsed : Biome.None;
        }

        /// <summary>
        /// One of Core Keeper's own rows, with the distance from the core its cave-in row carries.
        /// </summary>
        /// <remarks>
        /// Measured on <c>Resources/EnvironmentEventsTable.asset</c>: the cave-in row is 90 tiles
        /// from the core. The number is here so a test that claims a row survived untouched is
        /// comparing against something the game really has.
        /// </remarks>
        private static EnvironmentEventParams Vanilla(EnvironmentEventType type)
        {
            return new EnvironmentEventParams
            {
                eventType = type,
                biomes = new List<Biome> { Biome.Stone },
                minDistanceFromCore = 90f,
                tileRequirements = new List<EnvironmentEventTilesRequirement>()
            };
        }
    }
}
