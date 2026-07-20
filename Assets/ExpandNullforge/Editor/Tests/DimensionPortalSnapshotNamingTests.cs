#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Regression cover for a subtle, high-impact bug: rollback snapshots are taken with
    /// <see cref="Object.Instantiate(Object)"/>, which appends "(Clone)" to the copy's name, and
    /// the restore paths wrote that name back onto the real asset. Unity's importer then rejected
    /// the asset ("Main Object Name 'X(Clone)' does not match filename 'X'"), after which it
    /// could no longer be resolved in Scriptable Data — which surfaced far away as portal presets
    /// failing to find their Swirls and Center SpriteAssets.
    /// </summary>
    internal sealed class DimensionPortalSnapshotNamingTests
    {
        private const string TestFolder = "Assets/__ExpandNullforgeSnapshotNamingTests";

        [SetUp]
        public void SetUp()
        {
            Cleanup();
        }

        [TearDown]
        public void TearDown()
        {
            Cleanup();
        }

        [Test]
        public void ASnapshotKeepsTheOriginalNameInsteadOfGainingCloneSuffix()
        {
            DimensionPortalVisualProfileAsset source =
                ScriptableObject.CreateInstance<DimensionPortalVisualProfileAsset>();
            source.name = "PortalProfile_Swirls";

            DimensionPortalVisualProfileAsset snapshot =
                DimensionPortalArtworkEditorUtility.InstantiateSnapshot(source);

            try
            {
                Assert.That(snapshot, Is.Not.Null);
                Assert.That(snapshot.name, Is.EqualTo("PortalProfile_Swirls"));
                Assert.That(
                    snapshot.name,
                    Does.Not.Contain("(Clone)"),
                    "A '(Clone)' suffix here ends up on the real asset and breaks its import.");
            }
            finally
            {
                Object.DestroyImmediate(snapshot);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void ANullSourceSnapshotsToNullRatherThanThrowing()
        {
            Assert.That(
                DimensionPortalArtworkEditorUtility.InstantiateSnapshot(
                    (DimensionPortalVisualProfileAsset)null),
                Is.Null);
        }

        [Test]
        public void AnOnDiskAssetResolvesToItsFileNameNotItsInMemoryName()
        {
            AssetDatabase.CreateFolder("Assets", "__ExpandNullforgeSnapshotNamingTests");
            DimensionPortalVisualProfileAsset asset =
                ScriptableObject.CreateInstance<DimensionPortalVisualProfileAsset>();
            string path = TestFolder + "/PortalProfile.asset";
            AssetDatabase.CreateAsset(asset, path);

            // Simulate the corruption: an in-memory name that disagrees with the file.
            asset.name = "PortalProfile(Clone)";

            Assert.That(
                DimensionPortalArtworkEditorUtility.ResolveAssetFileName(asset, asset.name),
                Is.EqualTo("PortalProfile"));
        }

        [Test]
        public void AnInMemoryOnlyObjectFallsBackToTheSuppliedName()
        {
            DimensionPortalVisualProfileAsset asset =
                ScriptableObject.CreateInstance<DimensionPortalVisualProfileAsset>();
            try
            {
                Assert.That(
                    DimensionPortalArtworkEditorUtility.ResolveAssetFileName(asset, "Fallback"),
                    Is.EqualTo("Fallback"));
                Assert.That(
                    DimensionPortalArtworkEditorUtility.ResolveAssetFileName(null, "Fallback"),
                    Is.EqualTo("Fallback"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
