#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a biome's Ground and Walls rows resolve to, and the four things the compiler now says
    /// out loud about them.
    /// </summary>
    /// <remarks>
    /// The world builds terrain from the FIRST entry of each list. Nothing on the page says so, and
    /// a new biome pre-filled with ids no block could ever produce would read as finished — so the
    /// resolver and its messages are the half of the feature a creator actually meets.
    /// </remarks>
    internal sealed class DimensionBiomeTerrainMaterialTests
    {
        private const string DimensionId = "test.terrain";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        // ------------------------------------------------------------------- resolving ---

        [Test]
        public void AnEmptyListResolvesToNothing()
        {
            int tileset;
            string named;
            bool hasGround;
            DimensionBiomeTerrainSource source = DimensionBiomeTerrainMaterial.ResolveFirst(
                new string[0], new DimensionTilesetAsset[0], out tileset, out named, out hasGround);

            Assert.That(source, Is.EqualTo(DimensionBiomeTerrainSource.Empty));
        }

        [Test]
        public void AGeneratedGroundBlockIdResolvesToItsTileset()
        {
            DimensionTilesetAsset block = Tileset("Eerie Stone");

            int tileset;
            string named;
            bool hasGround;
            DimensionBiomeTerrainSource source = DimensionBiomeTerrainMaterial.ResolveFirst(
                new[] { block.GroundBlockItemId },
                new[] { block },
                out tileset,
                out named,
                out hasGround);

            Assert.That(source, Is.EqualTo(DimensionBiomeTerrainSource.ModBlock));
            Assert.That(tileset, Is.EqualTo(block.TilesetId));
            Assert.That(named, Is.EqualTo(block.GroundBlockItemId));
        }

        [Test]
        public void AGeneratedWallBlockIdResolvesToTheSameTileset()
        {
            DimensionTilesetAsset block = Tileset("Eerie Stone");

            int tileset;
            string named;
            bool hasGround;
            DimensionBiomeTerrainMaterial.ResolveFirst(
                new[] { block.WallBlockItemId },
                new[] { block },
                out tileset,
                out named,
                out hasGround);

            // One tileset is the whole block: its ground and its wall are two objects over one id.
            Assert.That(tileset, Is.EqualTo(block.TilesetId));
        }

        [Test]
        public void APickedVanillaBlockResolvesToItsEngineIndex()
        {
            int tileset;
            bool hasGround;
            DimensionBiomeTerrainSource source = DimensionBiomeTerrainMaterial.Resolve(
                DimensionBiomeTerrainMaterial.VanillaId("Stone"),
                new DimensionTilesetAsset[0],
                out tileset,
                out hasGround);

            Assert.That(source, Is.EqualTo(DimensionBiomeTerrainSource.VanillaBlock));
            Assert.That(tileset, Is.EqualTo(1), "Stone is index 1 in the vanilla catalog.");
            Assert.That(hasGround, Is.True);
        }

        [Test]
        public void AWallOnlyVanillaBlockReportsThatItHasNoGround()
        {
            int tileset;
            bool hasGround;
            DimensionBiomeTerrainMaterial.Resolve(
                DimensionBiomeTerrainMaterial.VanillaId("Eerie"),
                new DimensionTilesetAsset[0],
                out tileset,
                out hasGround);

            Assert.That(hasGround, Is.False, "Eerie is catalogued as wall-only.");
        }

        [Test]
        public void AnIdThatMatchesNothingIsUnknown_NotDirt()
        {
            int tileset;
            bool hasGround;
            DimensionBiomeTerrainSource source = DimensionBiomeTerrainMaterial.Resolve(
                "Mod:GroundBiome1Block",
                new[] { Tileset("Eerie Stone") },
                out tileset,
                out hasGround);

            Assert.That(source, Is.EqualTo(DimensionBiomeTerrainSource.Unknown));
        }

        [Test]
        public void TheFirstNonEmptyEntryIsTheOneThatCounts()
        {
            DimensionTilesetAsset first = Tileset("Eerie Stone");
            DimensionTilesetAsset second = Tileset("Pale Chalk");

            int tileset;
            string named;
            bool hasGround;
            DimensionBiomeTerrainMaterial.ResolveFirst(
                new[] { string.Empty, first.GroundBlockItemId, second.GroundBlockItemId },
                new[] { first, second },
                out tileset,
                out named,
                out hasGround);

            Assert.That(tileset, Is.EqualTo(first.TilesetId));
        }

        // ------------------------------------------------------------------- what it says ---

        [Test]
        public void AnUnresolvableGroundBlockIsReported()
        {
            BiomeTemplateAsset biome = Biome("cavern");
            biome.ApplySemanticTerrainPreset(
                new[] { "Mod:GroundBiome1Block" }, new string[0], new string[0], true);

            Assert.That(CodesOf(Compile(Template(biome))), Does.Contain("biome-terrain-block-unresolved"));
        }

        /// <summary>
        /// It is a warning, not a blocker: the dimension still generates, it generates dirt. An
        /// export blocker here would stop every project still carrying the placeholder ids an
        /// older framework wrote into a new biome.
        /// </summary>
        [Test]
        public void AnUnresolvableBlockDoesNotBlockTheExport()
        {
            BiomeTemplateAsset biome = Biome("cavern");
            biome.ApplySemanticTerrainPreset(
                new[] { "Mod:GroundBiome1Block" }, new string[0], new string[0], true);

            IReadOnlyList<DimensionAuthoringIssue> issues = Compile(Template(biome)).Issues;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Code == "biome-terrain-block-unresolved")
                {
                    Assert.That(issues[i].Severity, Is.EqualTo(DimensionAuthoringSeverity.Warning));
                    return;
                }
            }

            Assert.Fail("The unresolved-block issue was not raised at all.");
        }

        [Test]
        public void AResolvableBlockIsNotReported()
        {
            DimensionTilesetAsset block = Tileset("Eerie Stone");
            BiomeTemplateAsset biome = Biome("cavern");
            biome.ApplySemanticTerrainPreset(
                new[] { block.GroundBlockItemId },
                new[] { block.WallBlockItemId },
                new string[0],
                true);

            DimensionTemplateAsset template = Template(biome);
            template.SetTilesets(new[] { block });

            Assert.That(
                CodesOf(Compile(template)),
                Does.Not.Contain("biome-terrain-block-unresolved"));
        }

        [Test]
        public void AWallOnlyBlockUsedAsGroundIsReported()
        {
            BiomeTemplateAsset biome = Biome("cavern");
            biome.ApplySemanticTerrainPreset(
                new[] { DimensionBiomeTerrainMaterial.VanillaId("Eerie") },
                new[] { DimensionBiomeTerrainMaterial.VanillaId("Eerie") },
                new string[0],
                true);

            Assert.That(
                CodesOf(Compile(Template(biome))),
                Does.Contain("biome-terrain-block-not-ground"));
        }

        [Test]
        public void AGroundBlockWithAGroundSurfaceIsNotReportedAsGroundless()
        {
            BiomeTemplateAsset biome = Biome("cavern");
            biome.ApplySemanticTerrainPreset(
                new[] { DimensionBiomeTerrainMaterial.VanillaId("Stone") },
                new string[0],
                new string[0],
                true);

            Assert.That(
                CodesOf(Compile(Template(biome))),
                Does.Not.Contain("biome-terrain-block-not-ground"));
        }

        [Test]
        public void MoreThanOneBlockInARowIsSaidOutLoud()
        {
            BiomeTemplateAsset biome = Biome("cavern");
            biome.ApplySemanticTerrainPreset(
                new[]
                {
                    DimensionBiomeTerrainMaterial.VanillaId("Stone"),
                    DimensionBiomeTerrainMaterial.VanillaId("Clay")
                },
                new string[0],
                new string[0],
                true);

            Assert.That(CodesOf(Compile(Template(biome))), Does.Contain("biome-terrain-extra-blocks"));
        }

        [Test]
        public void OneBlockInARowIsNotWorthSaying()
        {
            BiomeTemplateAsset biome = Biome("cavern");
            biome.ApplySemanticTerrainPreset(
                new[] { DimensionBiomeTerrainMaterial.VanillaId("Stone") },
                new string[0],
                new string[0],
                true);

            Assert.That(
                CodesOf(Compile(Template(biome))),
                Does.Not.Contain("biome-terrain-extra-blocks"));
        }

        [Test]
        public void TwoBiomesOverTheSameGroundAreReported()
        {
            BiomeTemplateAsset first = Biome("cavern");
            BiomeTemplateAsset second = Biome("meadow");
            second.ApplyFallbackLocalBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32));

            DimensionTemplateAsset template = Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            ConfigureTemplate(template);
            template.SetBiomes(new[] { first, second });

            Assert.That(CodesOf(Compile(template)), Does.Contain("biome-terrain-regions-overlap"));
        }

        [Test]
        public void BiomesThatDoNotTouchAreNotReported()
        {
            BiomeTemplateAsset first = Biome("cavern");
            BiomeTemplateAsset second = Biome("meadow");
            second.ApplyFallbackLocalBounds(new Vector2Int(128, 128), new Vector2Int(192, 192));

            DimensionTemplateAsset template = Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            ConfigureTemplate(template);
            template.SetBiomes(new[] { first, second });

            Assert.That(
                CodesOf(Compile(template)),
                Does.Not.Contain("biome-terrain-regions-overlap"));
        }

        // ------------------------------------------------------------------- helpers ---

        private static List<string> CodesOf(DimensionCompiledGenerationPlan plan)
        {
            List<string> codes = new List<string>();
            IReadOnlyList<DimensionAuthoringIssue> issues = plan.Issues;
            if (issues == null)
            {
                return codes;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                codes.Add(issues[i].Code);
            }

            return codes;
        }

        private static DimensionCompiledGenerationPlan Compile(DimensionTemplateAsset template)
        {
            return DimensionTemplateCompiler.Compile(template);
        }

        private DimensionTemplateAsset Template(BiomeTemplateAsset biome)
        {
            DimensionTemplateAsset template = Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            ConfigureTemplate(template);
            template.SetBiomes(new[] { biome });
            return template;
        }

        private static void ConfigureTemplate(DimensionTemplateAsset template)
        {
            template.name = DimensionId;
            template.ConfigureIdentity(
                DimensionId,
                "Terrain Test",
                string.Empty,
                DimensionId + ".pack",
                "Terrain Test Pack",
                "1.0.0",
                string.Empty,
                1,
                DimensionType.World,
                DimensionTemplateStarterFactory.DefaultCapabilities);
            template.ConfigurePlacement(new Vector2Int(0, 7000), 1);
            template.ApplyReservedLocalBounds(
                new DimensionBounds(new int2(-320, -320), new int2(320, 320)));
        }

        private BiomeTemplateAsset Biome(string biomeId)
        {
            BiomeTemplateAsset biome = Track(ScriptableObject.CreateInstance<BiomeTemplateAsset>());
            biome.name = biomeId;
            biome.ConfigureIdentity(biomeId, biomeId, Color.white, 0, true);
            biome.ApplyFallbackLocalBounds(new Vector2Int(-64, -64), new Vector2Int(64, 64));
            return biome;
        }

        private DimensionTilesetAsset Tileset(string blockName)
        {
            DimensionTilesetAsset tileset =
                Track(ScriptableObject.CreateInstance<DimensionTilesetAsset>());
            tileset.name = blockName;
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(tileset);
            serialized.FindProperty("blockName").stringValue = blockName;
            serialized.FindProperty("modPrefix").stringValue = "TestMod";
            serialized.FindProperty("enabled").boolValue = true;
            serialized.FindProperty("blockType").stringValue = "terrain";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tileset;
        }

        private T Track<T>(T asset)
            where T : Object
        {
            created.Add(asset);
            return asset;
        }
    }
}
#endif
