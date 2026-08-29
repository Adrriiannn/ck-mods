using System.Collections.Generic;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which serialized values a page's cards have already claimed, worked out from their paths.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A card names the value it edits by its serialized path, and twenty-two of those paths reach
    /// INSIDE a block: <c>cooking.role</c>, <c>fishing.waters</c>, <c>player.aimSitsAt</c>. The
    /// block itself — <c>cooking</c>, <c>fishing</c>, <c>player</c> — is then not among the named
    /// paths, so the catch-all fold at the foot of the page drew it whole, and the same value had
    /// two live editors on one page with neither aware of the other.
    /// </para>
    /// <para>
    /// The rule is one line of string work and it decides whether a page is correct, so it is here
    /// on its own rather than buried in the page: nothing in this file needs Unity, which is what
    /// lets it be run and checked without opening the editor.
    /// </para>
    /// </remarks>
    internal static class DimensionCuratedPaths
    {
        /// <summary>
        /// Every block that a curated path reaches inside, at every depth.
        /// </summary>
        /// <remarks>
        /// <c>a.b.c</c> contributes both <c>a</c> and <c>a.b</c>, because a page that draws either
        /// of them whole puts <c>a.b.c</c> back on screen a second time. Paths with no dot in them
        /// name a value rather than a block and contribute nothing.
        /// </remarks>
        public static HashSet<string> ParentsOf(IEnumerable<string> curatedPaths)
        {
            HashSet<string> parents = new HashSet<string>();
            if (curatedPaths == null)
            {
                return parents;
            }

            foreach (string path in curatedPaths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                for (int dot = path.IndexOf('.'); dot > 0; dot = path.IndexOf('.', dot + 1))
                {
                    parents.Add(path.Substring(0, dot));
                }
            }

            return parents;
        }
    }
}
