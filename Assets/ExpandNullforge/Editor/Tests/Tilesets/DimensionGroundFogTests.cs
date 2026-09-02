using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers ground fog generation, which is the whole feature — the fog itself is Core Keeper's own
    /// render pass reading a data block we write.
    /// </summary>
    /// <remarks>
    /// The failure worth guarding is a stale block: fog is turned off in the authoring asset but the
    /// generated block stays on disk, so the biome keeps rendering fog with nothing in the project
    /// still asking for it. Nobody would think to look in a generated folder for that.
    /// </remarks>
    public sealed class DimensionGroundFogTests
    {
        private const string TestRoot = "Assets/NullforgeGroundFogTests";

        private DimensionTilesetAsset tileset;

        [SetUp]
        public void CreateScratchFolder()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            tileset = ScriptableObject.CreateInstance<DimensionTilesetAsset>();

            SerializedObject serialized = new SerializedObject(tileset);
            serialized.FindProperty("blockName").stringValue = "Fogstone";
            serialized.FindProperty("modPrefix").stringValue = "TestMod";
            serialized.FindProperty("enabled").boolValue = true;
            serialized.FindProperty("hasGroundFog").boolValue = true;
            serialized.FindProperty("groundFogTint").colorValue = new Color(0.2f, 0.4f, 0.6f, 0.3f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void RemoveScratchFolder()
        {
            if (tileset != null)
            {
                Object.DestroyImmediate(tileset);
            }

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private DimensionGroundFogReport Run()
        {
            return DimensionGroundFogGenerator.Generate(
                new List<DimensionTilesetAsset> { tileset },
                TestRoot);
        }

        private static void SetFog(DimensionTilesetAsset asset, bool enabled)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("hasGroundFog").boolValue = enabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ABlockThatAsksForFogGetsADataBlockNamingItsOwnTileset()
        {
            DimensionGroundFogReport report = Run();

            Assert.AreEqual(1, report.Created.Count, "Expected exactly one fog block.");
            Assert.IsEmpty(report.Warnings);

            GroundFogDataBlock block =
                AssetDatabase.LoadAssetAtPath<GroundFogDataBlock>(report.Created[0]);
            Assert.IsNotNull(block);
            Assert.AreEqual(
                tileset.TilesetId,
                (int)block.tileset,
                "The fog must name the block's own tileset, or it renders over someone else's ground.");
            Assert.AreEqual(
                TileType.ground,
                block.tileType,
                "Fog sits on the floor — a wall never carries it.");
            Assert.AreEqual(0.3f, block.tint.a, 0.0001f, "Alpha carries the fog's density.");
        }

        [Test]
        public void GeneratingTwiceUpdatesTheSameBlockRatherThanAddingASecond()
        {
            Run();
            DimensionGroundFogReport second = Run();

            Assert.IsEmpty(second.Created);
            Assert.AreEqual(1, second.Updated.Count);
            Assert.AreEqual(
                1,
                AssetDatabase.FindAssets("t:GroundFogDataBlock", new[] { TestRoot }).Length,
                "A second block would double the fog and render it twice as thick.");
        }

        [Test]
        public void TurningFogOffRemovesTheBlockRatherThanLeavingItBehind()
        {
            Run();
            SetFog(tileset, false);

            DimensionGroundFogReport report = Run();

            Assert.AreEqual(1, report.Removed.Count);
            Assert.AreEqual(
                0,
                AssetDatabase.FindAssets("t:GroundFogDataBlock", new[] { TestRoot }).Length,
                "A block left behind keeps rendering fog nothing in the project asks for.");
        }

        [Test]
        public void ADisabledBlockGetsNoFogEvenIfItAsksForIt()
        {
            SerializedObject serialized = new SerializedObject(tileset);
            serialized.FindProperty("enabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionGroundFogReport report = Run();
            Assert.IsEmpty(report.Created);
        }

        [Test]
        public void TheBlocksAddressIsStableAcrossRegeneration()
        {
            // An address that changed every build would look like a different block to anything
            // holding a reference, and would make two builds of one mod disagree about its fog.
            Run();
            string path = AssetDatabase.FindAssets("t:GroundFogDataBlock", new[] { TestRoot })[0];
            GroundFogDataBlock block =
                AssetDatabase.LoadAssetAtPath<GroundFogDataBlock>(AssetDatabase.GUIDToAssetPath(path));
            DataBlockAddress before = block.address;

            Run();
            Assert.AreEqual(before, block.address);
            Assert.AreNotEqual(DataBlockAddress.Empty, before, "A block with no address may not load.");
        }

        [Test]
        public void GeneratingWithNoModFolderReportsRatherThanWritingSomewhereUnexpected()
        {
            DimensionGroundFogReport report = DimensionGroundFogGenerator.Generate(
                new List<DimensionTilesetAsset> { tileset },
                string.Empty);

            Assert.IsEmpty(report.Created);
            Assert.IsNotEmpty(report.Warnings);
        }
    }
}
