using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// That a biome still runs exactly the generation passes it ran when half of them lived in a
    /// separate Biome Generation Profile asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The profile was a second list of passes hanging off the biome, prepended to the biome's own
    /// before the compiler saw them. It had no creation path anywhere in the product, so it was
    /// folded into the biome and deleted. The danger in that fold is silent: the starter factory
    /// put its terrain pass ONLY on the profile and left the biome's own list empty, so a fold that
    /// forgot either the factory or the already-authored assets would produce a dimension that
    /// compiles clean, reports no issues, and generates nothing at all.
    /// </para>
    /// <para>
    /// These pin the compiler's input, the starter factory's output, and the reader the migration
    /// uses to recover passes out of a profile file whose script no longer exists.
    /// </para>
    /// </remarks>
    public sealed class DimensionBiomeGenerationProfileFoldTests
    {
        private const string DimensionId = "test.fold";

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

        /// <summary>
        /// Both passes reach the compiled plan, in the order the profile-plus-biome pair produced:
        /// the profile's first, the biome's own after it.
        /// </summary>
        [Test]
        public void CompilerReceivesProfilePassesAndBiomePassesInOrder()
        {
            GenerationPassTemplateAsset fromProfile = Pass("carve", 0);
            GenerationPassTemplateAsset fromBiome = Pass("scatter", 10);

            BiomeTemplateAsset biome = Biome("cavern");
            biome.SetGenerationPasses(new[] { fromProfile, fromBiome });

            DimensionCompiledGenerationPlan plan = Compile(Template(biome));

            Assert.AreEqual(2, plan.GenerationPasses.Count, "Both passes should have compiled.");
            Assert.AreEqual(DimensionId + ".cavern.carve", plan.GenerationPasses[0].PassId);
            Assert.AreEqual(DimensionId + ".cavern.scatter", plan.GenerationPasses[1].PassId);
        }

        /// <summary>Global passes still come before the biome's, exactly as before the fold.</summary>
        [Test]
        public void GlobalPassesStillCompileAheadOfBiomePasses()
        {
            GenerationPassTemplateAsset global = Pass("prepare", 0);
            GenerationPassTemplateAsset local = Pass("carve", 0);

            BiomeTemplateAsset biome = Biome("cavern");
            biome.SetGenerationPasses(new[] { local });

            DimensionTemplateAsset template = Template(biome);
            template.SetGlobalGenerationPasses(new[] { global });

            DimensionCompiledGenerationPlan plan = Compile(template);

            Assert.AreEqual(2, plan.GenerationPasses.Count);
            Assert.AreEqual(DimensionId + ".prepare", plan.GenerationPasses[0].PassId);
            Assert.AreEqual(DimensionId + ".cavern.carve", plan.GenerationPasses[1].PassId);
        }

        /// <summary>
        /// An empty slot in the biome's list must not reach the compiler, because the list is one a
        /// creator grows in the inspector and a freshly grown row is null until they fill it.
        /// </summary>
        [Test]
        public void EmptyPassSlotsAreDroppedBeforeCompiling()
        {
            BiomeTemplateAsset biome = Biome("cavern");
            biome.SetGenerationPasses(new GenerationPassTemplateAsset[] { null, Pass("carve", 0), null });

            Assert.AreEqual(1, biome.GenerationPasses.Length);
            Assert.AreEqual(1, Compile(Template(biome)).GenerationPasses.Count);
        }

        /// <summary>
        /// The starter's terrain pass lands on the biome itself. Before the fold it landed only on
        /// the profile, so this is the assertion that a new dimension still generates terrain.
        /// </summary>
        [Test]
        public void StarterBiomeOwnsItsTerrainPass()
        {
            DimensionTemplateStarterRequest request =
                DimensionTemplateStarterRequest.CreateDefault(DimensionId, "Fold Test");
            DimensionTemplateStarterGraph graph =
                DimensionTemplateStarterFactory.CreateSingleBiomeStarter(request);
            Track(graph);

            Assert.AreEqual(1, graph.GenerationPasses.Length, "The starter should make one terrain pass.");
            Assert.AreEqual(
                1,
                graph.Biome.GenerationPasses.Length,
                "The starter biome must carry the pass itself now that no profile holds it.");
            Assert.AreSame(graph.GenerationPasses[0], graph.Biome.GenerationPasses[0]);
        }

        /// <summary>A dimension straight out of the starter still compiles a runnable pass.</summary>
        [Test]
        public void StarterDimensionCompilesWithItsTerrainPass()
        {
            DimensionTemplateStarterRequest request =
                DimensionTemplateStarterRequest.CreateDefault(DimensionId, "Fold Test");
            DimensionTemplateStarterGraph graph =
                DimensionTemplateStarterFactory.CreateSingleBiomeStarter(request);
            Track(graph);

            DimensionCompiledGenerationPlan plan = DimensionTemplateCompiler.Compile(graph.Dimension);

            Assert.AreEqual(1, plan.GenerationPasses.Count, "The starter's terrain pass should compile.");
            Assert.AreEqual(
                DimensionGenerationProviderIds.SafePlatform,
                plan.GenerationPasses[0].ProviderId);
        }

        /// <summary>
        /// The migration recovers the profile GUID out of a biome file authored before the fold.
        /// </summary>
        /// <remarks>
        /// The text is the shape the starter factory actually wrote, copied from a shipped biome
        /// asset, because the reader is the only thing standing between an old project and losing
        /// its passes.
        /// </remarks>
        [Test]
        public void MigrationReadsTheProfileReferenceOffAnAuthoredBiome()
        {
            string yaml =
                "  scenePool: []\n" +
                "  generationProfile: {fileID: 11400000, guid: 9414bd5bb38e4104792c9378f58d0195, type: 2}\n" +
                "  generationPasses: []\n";

            Assert.AreEqual(
                "9414bd5bb38e4104792c9378f58d0195",
                DimensionBiomeGenerationProfileFold.ReadReferenceGuid(yaml, "generationProfile"));
        }

        /// <summary>A biome that never had a profile reports nothing, rather than a stray guid.</summary>
        [Test]
        public void MigrationReadsNoProfileWhenTheFieldIsUnassigned()
        {
            string yaml =
                "  generationProfile: {fileID: 0}\n" +
                "  generationPasses:\n" +
                "  - {fileID: 11400000, guid: d4c5a56b7c3efbf40b9d321fd453aa83, type: 2}\n";

            Assert.AreEqual(
                string.Empty,
                DimensionBiomeGenerationProfileFold.ReadReferenceGuid(yaml, "generationProfile"));
        }

        /// <summary>
        /// The pass list is read in order and stops at the next field, so the eleven table arrays
        /// that followed it in a profile file cannot bleed in as passes.
        /// </summary>
        [Test]
        public void MigrationReadsTheProfilePassListAndStopsAtTheNextField()
        {
            string yaml =
                "  profileId: Nullforge:Iceborne.generation\n" +
                "  generationPasses:\n" +
                "  - {fileID: 11400000, guid: d4c5a56b7c3efbf40b9d321fd453aa83, type: 2}\n" +
                "  - {fileID: 11400000, guid: 405a4778f663b1b469ea0cadd4e51d09, type: 2}\n" +
                "  terrainTables:\n" +
                "  - {fileID: 11400000, guid: 4961b1c735775df43afbedf0400670ac, type: 2}\n" +
                "  floorTables: []\n";

            IReadOnlyList<string> guids =
                DimensionBiomeGenerationProfileFold.ReadReferenceListGuids(yaml, "generationPasses");

            Assert.AreEqual(2, guids.Count, "Only the two passes belong to this field.");
            Assert.AreEqual("d4c5a56b7c3efbf40b9d321fd453aa83", guids[0]);
            Assert.AreEqual("405a4778f663b1b469ea0cadd4e51d09", guids[1]);
        }

        /// <summary>An empty pass list reads as none, not as whatever field comes next.</summary>
        [Test]
        public void MigrationReadsAnEmptyPassListAsNothing()
        {
            string yaml =
                "  generationPasses: []\n" +
                "  terrainTables:\n" +
                "  - {fileID: 11400000, guid: 4961b1c735775df43afbedf0400670ac, type: 2}\n";

            Assert.AreEqual(
                0,
                DimensionBiomeGenerationProfileFold.ReadReferenceListGuids(yaml, "generationPasses").Count);
        }

        private DimensionCompiledGenerationPlan Compile(DimensionTemplateAsset template)
        {
            return DimensionTemplateCompiler.Compile(template);
        }

        private DimensionTemplateAsset Template(BiomeTemplateAsset biome)
        {
            DimensionTemplateAsset template = Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            template.name = DimensionId;
            template.ConfigureIdentity(
                DimensionId,
                "Fold Test",
                string.Empty,
                DimensionId + ".pack",
                "Fold Test Pack",
                "1.0.0",
                string.Empty,
                1,
                DimensionType.World,
                DimensionTemplateStarterFactory.DefaultCapabilities);
            template.ConfigurePlacement(new Vector2Int(0, 7000), 1);
            template.ApplyReservedLocalBounds(
                new DimensionBounds(new int2(-320, -320), new int2(320, 320)));
            template.SetBiomes(new[] { biome });
            return template;
        }

        private BiomeTemplateAsset Biome(string biomeId)
        {
            BiomeTemplateAsset biome = Track(ScriptableObject.CreateInstance<BiomeTemplateAsset>());
            biome.name = biomeId;
            biome.ConfigureIdentity(biomeId, biomeId, Color.white, 0, true);
            biome.ApplyFallbackLocalBounds(new Vector2Int(-64, -64), new Vector2Int(64, 64));
            return biome;
        }

        /// <summary>
        /// A pass with explicit bounds, so it compiles without needing a layout to place its biome.
        /// </summary>
        private GenerationPassTemplateAsset Pass(string passId, int priority)
        {
            GenerationPassTemplateAsset pass =
                Track(ScriptableObject.CreateInstance<GenerationPassTemplateAsset>());
            pass.name = passId;
            pass.ConfigureIdentity(
                passId,
                passId,
                DimensionGenerationPassPhase.Terrain,
                DimensionGenerationProviderIds.SafePlatform,
                priority,
                true);
            pass.ApplyExplicitLocalBounds(new Vector2Int(-64, -64), new Vector2Int(64, 64));
            return pass;
        }

        private T Track<T>(T asset)
            where T : Object
        {
            created.Add(asset);
            return asset;
        }

        private void Track(DimensionTemplateStarterGraph graph)
        {
            Track(graph.Dimension);
            Track(graph.Layout);
            Track(graph.Biome);
            Track(graph.PortalVisualProfile);
            for (int i = 0; i < graph.GenerationPasses.Length; i++)
            {
                Track(graph.GenerationPasses[i]);
            }

            for (int i = 0; i < graph.PortalAccessRules.Length; i++)
            {
                Track(graph.PortalAccessRules[i]);
            }
        }
    }
}
