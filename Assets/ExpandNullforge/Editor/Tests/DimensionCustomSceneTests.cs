using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Scenes;
using ExpandNullforge.Tilesets;
using NUnit.Framework;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the scene rules whose failure mode is silence.
    /// </summary>
    /// <remarks>
    /// Almost everything that can go wrong with a scene goes wrong invisibly: a name too long to be
    /// queued registers fine and never spawns, a duplicate name shadows another mod's structure, an
    /// oversized scene misbehaves at placement with no error. None of those produce a log line in
    /// game, so they are worth catching in a test where the message can say what happened.
    /// </remarks>
    public sealed class DimensionCustomSceneTests
    {
        [SetUp]
        public void ResetRegistry()
        {
            DimensionCustomSceneRegistry.Clear();
        }

        [TearDown]
        public void ClearRegistry()
        {
            DimensionCustomSceneRegistry.Clear();
        }

        private static DimensionCustomSceneDefinition Scene(string name, int tiles = 1)
        {
            List<DimensionSceneTile> list = new List<DimensionSceneTile>();
            for (int i = 0; i < tiles; i++)
            {
                list.Add(new DimensionSceneTile(new int2(i, 0), 1000, TileType.ground));
            }

            return new DimensionCustomSceneDefinition(name, list);
        }

        [Test]
        public void ANameTooLongToBeQueuedIsRefusedAtRegistration()
        {
            // The blob would hold this fine; the spawn request it has to travel through would not.
            string tooLong = new string('a', DimensionCustomSceneNames.MaxNameBytes + 1);

            string error;
            Assert.IsFalse(
                DimensionCustomSceneRegistry.Register(Scene(tooLong), out error),
                "A name that cannot survive being queued must be refused here, because in game it " +
                "registers and then never spawns with nothing logged.");
            StringAssert.Contains("bytes", error);
        }

        [Test]
        public void ANameThatExactlyFillsTheLimitIsAccepted()
        {
            string exact = new string('a', DimensionCustomSceneNames.MaxNameBytes);

            string error;
            Assert.IsTrue(
                DimensionCustomSceneRegistry.Register(Scene(exact), out error),
                "The limit is inclusive; refusing a name that fits would cost authors a character for " +
                "no reason. " + error);
        }

        [Test]
        public void NonAsciiNamesAreMeasuredInBytesNotCharacters()
        {
            // Each of these is one char and three UTF-8 bytes, so a character-based check would let a
            // name three times over the real limit through.
            int chars = (DimensionCustomSceneNames.MaxNameBytes / 3) + 1;
            string wide = new string('中', chars);

            Assert.Greater(
                DimensionCustomSceneNames.Utf8ByteCount(wide),
                DimensionCustomSceneNames.MaxNameBytes,
                "This fixture is only meaningful if the name really does exceed the byte limit.");

            string error;
            Assert.IsFalse(DimensionCustomSceneRegistry.Register(Scene(wide), out error));
        }

        [Test]
        public void SurrogatePairsCountAsFourBytesNotSix()
        {
            // U+1F600, one code point spread over two chars. Counting each char separately would
            // report six bytes and reject names that actually fit.
            Assert.AreEqual(4, DimensionCustomSceneNames.Utf8ByteCount("\U0001F600"));
        }

        [Test]
        public void TwoScenesCannotShareAName()
        {
            string error;
            Assert.IsTrue(DimensionCustomSceneRegistry.Register(Scene("mod:hall"), out error), error);
            Assert.IsFalse(
                DimensionCustomSceneRegistry.Register(Scene("mod:hall"), out error),
                "Scenes resolve by name, so a duplicate means one of them can never be placed.");
            StringAssert.Contains("already registered", error);
        }

        [Test]
        public void ASceneWithNoTilesIsRefused()
        {
            string error;
            Assert.IsFalse(
                DimensionCustomSceneRegistry.Register(Scene("mod:empty", 0), out error),
                "An empty scene places nothing but still occupies a scene slot in the world.");
        }

        [Test]
        public void CompilingResolvesACustomBlockToTheIdItsNameDerivesTo()
        {
            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile(
                "mod:hall",
                new[]
                {
                    new DimensionSceneTileRequest(new int2(0, 0), "MyMod:stone", DimensionTileRole.Ground)
                });

            Assert.IsEmpty(result.Skipped);
            Assert.AreEqual(1, result.Tiles.Count);
            Assert.AreEqual(
                DimensionTilesetRegistry.ComputeTilesetId("MyMod:stone"),
                result.Tiles[0].Tileset,
                "A scene and the terrain around it must agree about what a named block is.");
            Assert.AreEqual(TileType.ground, result.Tiles[0].TileType);
        }

        [Test]
        public void AGroundAndAWallCanShareACellBecauseThatIsHowBlocksStack()
        {
            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile(
                "mod:hall",
                new[]
                {
                    new DimensionSceneTileRequest(new int2(2, 3), "MyMod:stone", DimensionTileRole.Ground),
                    new DimensionSceneTileRequest(new int2(2, 3), "MyMod:stone", DimensionTileRole.Wall)
                });

            Assert.IsEmpty(result.Skipped, "Ground beneath a wall is the normal case, not a conflict.");
            Assert.AreEqual(2, result.Tiles.Count);
        }

        [Test]
        public void TwoTilesOfTheSameTypeInOneCellAreReportedRatherThanSilentlyResolved()
        {
            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile(
                "mod:hall",
                new[]
                {
                    new DimensionSceneTileRequest(new int2(1, 1), "MyMod:stone", DimensionTileRole.Ground),
                    new DimensionSceneTileRequest(new int2(1, 1), "MyMod:moss", DimensionTileRole.Ground)
                });

            Assert.AreEqual(1, result.Tiles.Count);
            Assert.AreEqual(
                1,
                result.Skipped.Count,
                "Which of the two would win depends on list order, so the author has to be told.");
        }

        [Test]
        public void ATileNamingNoBlockIsSkippedWithAReason()
        {
            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile(
                "mod:hall",
                new[] { new DimensionSceneTileRequest(new int2(0, 0), string.Empty, DimensionTileRole.Ground) });

            Assert.IsEmpty(result.Tiles);
            Assert.AreEqual(1, result.Skipped.Count);
        }

        [Test]
        public void ASceneLargerThanTheEngineAllowsIsRejectedWhole()
        {
            List<DimensionSceneTileRequest> requests = new List<DimensionSceneTileRequest>();
            for (int x = 0; x <= DimensionSceneTileCompiler.MaxSceneSize; x++)
            {
                requests.Add(new DimensionSceneTileRequest(new int2(x, 0), "MyMod:stone", DimensionTileRole.Ground));
            }

            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile("mod:wide", requests);

            Assert.IsEmpty(
                result.Tiles,
                "Half-placing an oversized scene is worse than not placing it: the engine does not " +
                "reject it with a message, it misbehaves.");
            Assert.AreEqual(1, result.Skipped.Count);
            StringAssert.Contains("limit", result.Skipped[0]);
        }

        [Test]
        public void ASceneExactlyAtTheSizeLimitIsAccepted()
        {
            List<DimensionSceneTileRequest> requests = new List<DimensionSceneTileRequest>();
            for (int x = 0; x < DimensionSceneTileCompiler.MaxSceneSize; x++)
            {
                requests.Add(new DimensionSceneTileRequest(new int2(x, 0), "MyMod:stone", DimensionTileRole.Ground));
            }

            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile("mod:wide", requests);

            Assert.AreEqual(DimensionSceneTileCompiler.MaxSceneSize, result.Tiles.Count);
            Assert.IsEmpty(result.Skipped);
        }

        [Test]
        public void SceneSizeIsMeasuredAsExtentNotAsDistanceFromTheOrigin()
        {
            // A scene authored around a negative origin is the same size as one authored from zero;
            // measuring from (0,0) would reject perfectly legal scenes.
            List<DimensionSceneTileRequest> requests = new List<DimensionSceneTileRequest>();
            for (int x = -30; x < 30; x++)
            {
                requests.Add(new DimensionSceneTileRequest(new int2(x, 0), "MyMod:stone", DimensionTileRole.Ground));
            }

            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile("mod:centred", requests);

            Assert.AreEqual(60, result.Tiles.Count);
            Assert.IsEmpty(result.Skipped);
        }

        [Test]
        public void RolesMapToTheSameTileTypesTerrainUses()
        {
            DimensionSceneTileCompileResult result = DimensionSceneTileCompiler.Compile(
                "mod:mix",
                new[]
                {
                    new DimensionSceneTileRequest(new int2(0, 0), "MyMod:stone", DimensionTileRole.Ground),
                    new DimensionSceneTileRequest(new int2(1, 0), "MyMod:stone", DimensionTileRole.Wall),
                    new DimensionSceneTileRequest(new int2(2, 0), "MyMod:stone", DimensionTileRole.Liquid),
                    new DimensionSceneTileRequest(new int2(3, 0), "MyMod:stone", DimensionTileRole.Pit),
                    new DimensionSceneTileRequest(new int2(4, 0), "MyMod:stone", DimensionTileRole.Ceiling)
                });

            Assert.IsEmpty(result.Skipped);
            Assert.AreEqual(TileType.ground, result.Tiles[0].TileType);
            Assert.AreEqual(TileType.wall, result.Tiles[1].TileType);
            Assert.AreEqual(TileType.water, result.Tiles[2].TileType);
            Assert.AreEqual(TileType.pit, result.Tiles[3].TileType);
            Assert.AreEqual(
                TileType.roofHole,
                result.Tiles[4].TileType,
                "Ceiling paints the opening in the roof — Core Keeper draws the solid roof itself.");
        }

        [Test]
        public void ASceneWithNoObjectsStillHasAnObjectListRatherThanNull()
        {
            // The injector walks this list unconditionally. A null here would be an exception during
            // world load, which costs every scene rather than the one that had no objects.
            Assert.IsNotNull(Scene("mod:bare").Objects);
            Assert.IsEmpty(Scene("mod:bare").Objects);
        }

        [Test]
        public void ObjectsSurviveRegistrationInTheOrderTheyWereAuthored()
        {
            // Order matters beyond tidiness: the blob's parallel arrays (prefabs, positions, colours,
            // sizes) are all indexed together, so a reordering here would paint the wrong object.
            List<DimensionSceneTile> tiles = new List<DimensionSceneTile>
            {
                new DimensionSceneTile(new int2(0, 0), 1000, TileType.ground)
            };
            List<DimensionSceneObject> objects = new List<DimensionSceneObject>
            {
                new DimensionSceneObject(
                    new int2(1, 2), "Chest", DimensionSceneFacing.North, DimensionScenePaintChoice.Red),
                new DimensionSceneObject(
                    new int2(3, 4), "Torch", DimensionSceneFacing.Unchanged, DimensionScenePaintChoice.Unpainted)
            };

            string error;
            Assert.IsTrue(
                DimensionCustomSceneRegistry.Register(
                    new DimensionCustomSceneDefinition("mod:objects", tiles, objects: objects),
                    out error),
                error);

            IReadOnlyList<DimensionSceneObject> stored = DimensionCustomSceneRegistry.All[0].Objects;
            Assert.AreEqual(2, stored.Count);
            Assert.AreEqual("Chest", stored[0].ObjectName);
            Assert.AreEqual(new int2(1, 2), stored[0].LocalPosition);
            Assert.AreEqual(DimensionScenePaintChoice.Red, stored[0].Paint);
            Assert.AreEqual("Torch", stored[1].ObjectName);
        }

        [Test]
        public void ASceneMadeOnlyOfObjectsIsStillRefusedBecauseTheEngineNeedsTiles()
        {
            // Worth stating: objects ride along with terrain, they are not a scene on their own. A
            // tileless scene registers fine in a naive implementation and then places nothing.
            List<DimensionSceneObject> objects = new List<DimensionSceneObject>
            {
                new DimensionSceneObject(
                    new int2(0, 0), "Chest", DimensionSceneFacing.Unchanged, DimensionScenePaintChoice.Unpainted)
            };

            string error;
            Assert.IsFalse(
                DimensionCustomSceneRegistry.Register(
                    new DimensionCustomSceneDefinition(
                        "mod:objectsonly", new List<DimensionSceneTile>(), objects: objects),
                    out error));
        }

        [Test]
        public void EveryFacingResolvesToAGroundPlaneDirection()
        {
            // Y must stay zero. A direction with any height in it points the object at the sky, which
            // reads in game as a rendering fault rather than the maths error it is.
            DimensionSceneFacing[] facings =
            {
                DimensionSceneFacing.North,
                DimensionSceneFacing.East,
                DimensionSceneFacing.South,
                DimensionSceneFacing.West
            };

            for (int i = 0; i < facings.Length; i++)
            {
                float3 direction;
                Assert.IsTrue(DimensionSceneFacings.TryGetDirection(facings[i], out direction), facings[i].ToString());
                Assert.AreEqual(0f, direction.y, facings[i] + " must stay on the ground plane.");
                Assert.AreEqual(1f, math.length(direction), 0.0001f, facings[i] + " must be a unit direction.");
            }

            float3 none;
            Assert.IsFalse(
                DimensionSceneFacings.TryGetDirection(DimensionSceneFacing.Unchanged, out none),
                "Unchanged must produce no direction at all, so the object keeps its authored pose.");
        }

        [Test]
        public void NorthAndSouthAreOppositeAndEastAndWestAreOpposite()
        {
            float3 north;
            float3 south;
            float3 east;
            float3 west;
            DimensionSceneFacings.TryGetDirection(DimensionSceneFacing.North, out north);
            DimensionSceneFacings.TryGetDirection(DimensionSceneFacing.South, out south);
            DimensionSceneFacings.TryGetDirection(DimensionSceneFacing.East, out east);
            DimensionSceneFacings.TryGetDirection(DimensionSceneFacing.West, out west);

            Assert.AreEqual(-1f, math.dot(north, south), 0.0001f);
            Assert.AreEqual(-1f, math.dot(east, west), 0.0001f);
            Assert.AreEqual(0f, math.dot(north, east), 0.0001f, "The four facings must be axis-aligned.");
        }

        [Test]
        public void ThePaintChoiceMatchesTheGamesOwnPaletteEntryForEntry()
        {
            // The injector casts our choice straight to the game's enum, so a divergence in either
            // numbering silently repaints every object — green statues where red were authored. The
            // cast is the cheap part; this test is what makes it safe.
            foreach (DimensionScenePaintChoice choice in
                     System.Enum.GetValues(typeof(DimensionScenePaintChoice)))
            {
                string ours = choice.ToString();
                string theirs = ((PaintableColor)(int)choice).ToString();
                Assert.AreEqual(
                    ours,
                    theirs,
                    "Paint choice " + (int)choice + " is '" + ours + "' here but '" + theirs +
                    "' in Core Keeper. The injector casts between them, so they must not drift.");
            }
        }

        [Test]
        public void TheAuthoringPaletteMatchesTheRuntimePaletteEntryForEntry()
        {
            // Two enums exist on purpose — authoring must not depend on a game enum whose numbering
            // could shift under it — but the generator writes one as the other by name, so they have
            // to agree.
            System.Array authoring = System.Enum.GetValues(typeof(Authoring.DimensionScenePaint));
            System.Array runtime = System.Enum.GetValues(typeof(DimensionScenePaintChoice));
            Assert.AreEqual(runtime.Length, authoring.Length, "The two palettes must hold the same colours.");

            for (int i = 0; i < authoring.Length; i++)
            {
                Assert.AreEqual(
                    ((DimensionScenePaintChoice)runtime.GetValue(i)).ToString(),
                    ((Authoring.DimensionScenePaint)authoring.GetValue(i)).ToString(),
                    "Palette entry " + i + " differs between authoring and runtime.");
            }
        }

        [Test]
        public void TheAuthoringFacingMatchesTheRuntimeFacingEntryForEntry()
        {
            System.Array authoring = System.Enum.GetValues(typeof(Authoring.DimensionSceneObjectFacing));
            System.Array runtime = System.Enum.GetValues(typeof(DimensionSceneFacing));
            Assert.AreEqual(runtime.Length, authoring.Length);

            for (int i = 0; i < authoring.Length; i++)
            {
                Assert.AreEqual(
                    ((DimensionSceneFacing)runtime.GetValue(i)).ToString(),
                    ((Authoring.DimensionSceneObjectFacing)authoring.GetValue(i)).ToString(),
                    "Facing " + i + " differs between authoring and runtime.");
            }
        }
    }
}
