#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Pure-logic coverage for the consumer folder conventions. These paths decide where a
    /// dimension's generated/owned assets live, so their normalization (and the "collapse to
    /// the owning DimensionAssets root" rule) must be exact and platform-independent.
    /// </summary>
    internal sealed class DimensionApiModFolderUtilityTests
    {
        [Test]
        public void NormalizeFolder_UsesForwardSlashesAndTrimsTrailingSeparators()
        {
            Assert.That(
                DimensionApiModFolderUtility.NormalizeFolder("Assets\\MyMod\\"),
                Is.EqualTo("Assets/MyMod"));
            Assert.That(
                DimensionApiModFolderUtility.NormalizeFolder("Assets/MyMod//"),
                Is.EqualTo("Assets/MyMod"));
            Assert.That(
                DimensionApiModFolderUtility.NormalizeFolder("  Assets/MyMod  "),
                Is.EqualTo("Assets/MyMod"));
            Assert.That(DimensionApiModFolderUtility.NormalizeFolder(""), Is.Empty);
            Assert.That(DimensionApiModFolderUtility.NormalizeFolder(null), Is.Empty);
        }

        [Test]
        public void NormalizeDimensionAssetFolder_ResolvesToTheDimensionAssetsRoot()
        {
            // Appends the convention folder when it is absent.
            Assert.That(
                DimensionApiModFolderUtility.NormalizeDimensionAssetFolder("Assets\\MyMod"),
                Is.EqualTo("Assets/MyMod/DimensionAssets"));
            // Already at the root — returned unchanged.
            Assert.That(
                DimensionApiModFolderUtility.NormalizeDimensionAssetFolder(
                    "Assets/MyMod/DimensionAssets"),
                Is.EqualTo("Assets/MyMod/DimensionAssets"));
            // A deeper path collapses back to the owning DimensionAssets root.
            Assert.That(
                DimensionApiModFolderUtility.NormalizeDimensionAssetFolder(
                    "Assets/MyMod/DimensionAssets/MyDim/deep"),
                Is.EqualTo("Assets/MyMod/DimensionAssets"));
            Assert.That(
                DimensionApiModFolderUtility.NormalizeDimensionAssetFolder(""),
                Is.Empty);
        }
    }
}
#endif
