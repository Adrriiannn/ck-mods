#if UNITY_INCLUDE_TESTS
using UnityEditor;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The scratch folder a generator fixture writes its prefabs into, made and taken away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIRTY-SEVEN FIXTURES WROTE THIS OUT BY HAND, character for character, and the only thing
    /// that differed was the folder's name. Both halves are here now and each fixture calls one
    /// line. Nothing else was pulled up with them: the asset a fixture builds, its
    /// <c>Set</c>, its <c>Run</c> and its <c>Load</c> look alike and are not the same — each names
    /// its own asset type, its own generator and its own prefab — so sharing those would mean
    /// giving thirty-seven fixtures one generic shape that fits none of them.
    /// </para>
    /// <para>
    /// IT DERIVES THE LEAF NAME FROM THE ROOT, which the hand-written version could not. Every
    /// fixture had the folder written down twice — once as <c>TestRoot = "Assets/NullforgeXTests"</c>
    /// and once as <c>CreateFolder("Assets", "NullforgeXTests")</c> — with nothing tying them
    /// together. Disagree by one letter and the folder is created in one place, looked for in
    /// another, never found, never deleted, and the fixture leaves a folder behind at the project
    /// root on every run while still going green. Splitting the root once means they cannot
    /// disagree.
    /// </para>
    /// <para>
    /// The existence checks stay. <c>CreateFolder</c> on a folder that is already there logs a
    /// warning, and <c>DeleteAsset</c> on a path that is not there returns false and says so; a
    /// fixture that leaves either message in the console teaches the next reader to ignore the
    /// console.
    /// </para>
    /// </remarks>
    internal static class DimensionTestScratchFolder
    {
        /// <summary>Makes the folder if it is not already there.</summary>
        /// <param name="testRoot">The project-relative folder, as <c>"Assets/Something"</c>.</param>
        public static void Ensure(string testRoot)
        {
            if (string.IsNullOrEmpty(testRoot) || AssetDatabase.IsValidFolder(testRoot))
            {
                return;
            }

            int cut = testRoot.LastIndexOf('/');
            if (cut <= 0)
            {
                return;
            }

            AssetDatabase.CreateFolder(testRoot.Substring(0, cut), testRoot.Substring(cut + 1));
        }

        /// <summary>Takes the folder and everything a test wrote into it away.</summary>
        public static void Remove(string testRoot)
        {
            if (string.IsNullOrEmpty(testRoot) || !AssetDatabase.IsValidFolder(testRoot))
            {
                return;
            }

            AssetDatabase.DeleteAsset(testRoot);
        }
    }
}
#endif
