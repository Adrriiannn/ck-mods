using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which files ship, worked out from the asmdef beside them rather than a folder list.
    /// </summary>
    public static partial class DimensionSandboxGuard
    {
        /// <summary>
        /// Says why a ship set of <paramref name="shippedCount"/> files cannot be trusted, or null
        /// when the count is plausible.
        /// </summary>
        /// <remarks>
        /// Every check that walks the ship set has to ask this, because the set coming back empty
        /// and the set coming back clean look identical from inside a loop that never runs. The
        /// three ways it comes back empty are a missing <c>ExpandNullforge</c> folder, an
        /// enumeration that threw, and the root asmdef being marked Editor-only — none of which is
        /// visible to a caller holding an empty list.
        /// </remarks>
        public static string ShipSetProblem(string rootPath, int shippedCount)
        {
            if (shippedCount >= FewestPlausibleShippedFiles)
            {
                return null;
            }

            return "The shipped source set came back with " + shippedCount + " file(s), and " +
                   FewestPlausibleShippedFiles + " is the fewest this framework can plausibly " +
                   "have, so whatever walked it read almost nothing and cannot have found " +
                   "anything. Check that " + rootPath + "/" + ShippedRoot + " exists, that " +
                   ShippedRoot + "/ExpandNullforge.asmdef does not say " +
                   "\"includePlatforms\": [\"Editor\"], and that the folder is readable.";
        }

        /// <summary>
        /// Every C# file under <paramref name="rootPath"/> that is compiled into an assembly the
        /// game loads.
        /// </summary>
        /// <remarks>
        /// Public because it is the answer to "what ships", and more than one check needs it — the
        /// Burst budget test asks the same question. Two places working it out separately is how
        /// one of them ends up looking at a folder the other does not.
        /// </remarks>
        public static List<string> ShippedSourceFiles(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return new List<string>();
            }

            return ShippedSourceFilesUnder(Path.Combine(rootPath, ShippedRoot));
        }

        /// <summary>
        /// The same walk, over any one folder rather than over the framework's own.
        /// </summary>
        /// <remarks>
        /// Split out so a consumer's mod folder gets the identical treatment: the same asmdef
        /// reading, the same nearest-match rule, the same silence on an unreadable folder. A second
        /// walk written beside it would be the way the two end up disagreeing about what ships.
        /// </remarks>
        public static List<string> ShippedSourceFilesUnder(string folder)
        {
            List<string> shipped = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return shipped;
            }

            List<string> editorOnly = EditorOnlyFolders(folder);
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return shipped;
            }

            for (int i = 0; i < files.Length; i++)
            {
                if (IsShipped(files[i], editorOnly))
                {
                    shipped.Add(files[i]);
                }
            }

            return shipped;
        }

        /// <summary>
        /// The name of the assembly a consumer mod's shipped code is compiled into, or empty when
        /// its nearest asmdef is Editor-only or unreadable.
        /// </summary>
        /// <remarks>
        /// Read from the asmdef rather than from the folder name, because Unity names the assembly
        /// from the asmdef and the two need not match.
        /// </remarks>
        public static string ShippedAssemblyNameOf(string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot))
            {
                return string.Empty;
            }

            string[] asmdefs;
            try
            {
                asmdefs = Directory.GetFiles(modRoot, "*.asmdef", SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return string.Empty;
            }

            for (int i = 0; i < asmdefs.Length; i++)
            {
                string text;
                try
                {
                    text = System.IO.File.ReadAllText(asmdefs[i]);
                }
                catch (Exception)
                {
                    continue;
                }

                if (SaysEditorOnly(text))
                {
                    continue;
                }

                string name = NameIn(text);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            return string.Empty;
        }

        /// <summary>An asmdef's <c>name</c>, which is what Unity calls the assembly it builds.</summary>
        private static string NameIn(string asmdef)
        {
            int at = asmdef.IndexOf("\"name\"", StringComparison.Ordinal);
            if (at < 0)
            {
                return string.Empty;
            }

            int colon = asmdef.IndexOf(':', at);
            int open = colon < 0 ? -1 : asmdef.IndexOf('"', colon);
            int close = open < 0 ? -1 : asmdef.IndexOf('"', open + 1);
            if (open < 0 || close < 0)
            {
                return string.Empty;
            }

            return asmdef.Substring(open + 1, close - open - 1);
        }

        /// <summary>Whether one folder is <paramref name="ancestor"/> or sits inside it.</summary>
        /// <remarks>
        /// BOTH SIDES ARE PUT IN ONE SPELLING FIRST, because they arrive in two. The folder comes
        /// from <c>Path.GetDirectoryName</c>, which normalises every separator to a backslash on
        /// Windows; the ancestor comes from <c>Path.Combine(Application.dataPath, …)</c>, and
        /// <c>Application.dataPath</c> is spelled with forward slashes, so <c>Path.Combine</c>
        /// leaves <c>E:/…/Assets\ExpandNullforge</c>. Compared as written, one never starts with
        /// the other, so this framework's own <c>Editor</c> and <c>Editor/Tests</c> folders — whose
        /// asmdefs of course name the framework — were counted as CONSUMER MODS. Both are
        /// Editor-only, so the consumer scan then reported two mods with nothing shipped and
        /// <c>TheGeneratedConsumerModsUseNothingTheSandboxDenies</c> was red on a project where
        /// both real consumer mods were clean.
        /// </remarks>
        private static bool IsWithin(string folder, string ancestor)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(ancestor))
            {
                return false;
            }

            folder = folder.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            ancestor = ancestor.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (folder.Length < ancestor.Length ||
                !folder.StartsWith(ancestor, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return folder.Length == ancestor.Length ||
                folder[ancestor.Length] == Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// The folders under <paramref name="root"/> whose asmdef says the assembly is Editor-only.
        /// </summary>
        /// <remarks>
        /// Unity compiles a script into the assembly of the NEAREST asmdef above it, so the check a
        /// file gets is the check of its closest enclosing asmdef folder — which is why the longest
        /// match wins in <see cref="IsShipped"/> rather than any match.
        /// </remarks>
        private static List<string> EditorOnlyFolders(string root)
        {
            List<string> folders = new List<string>();
            string[] asmdefs;
            try
            {
                asmdefs = Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return folders;
            }

            for (int i = 0; i < asmdefs.Length; i++)
            {
                string text;
                try
                {
                    text = System.IO.File.ReadAllText(asmdefs[i]);
                }
                catch (Exception)
                {
                    continue;
                }

                if (SaysEditorOnly(text))
                {
                    folders.Add(Path.GetDirectoryName(asmdefs[i]));
                }
            }

            return folders;
        }

        /// <summary>
        /// Whether an asmdef's <c>includePlatforms</c> names Editor, which is Unity's own way of
        /// saying the assembly never reaches a build and so never reaches the sandbox.
        /// </summary>
        private static bool SaysEditorOnly(string asmdef)
        {
            int at = asmdef.IndexOf("\"includePlatforms\"", StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }

            int open = asmdef.IndexOf('[', at);
            int close = open < 0 ? -1 : asmdef.IndexOf(']', open);
            if (open < 0 || close < 0)
            {
                return false;
            }

            return asmdef.IndexOf("\"Editor\"", open, close - open, StringComparison.Ordinal) >= 0;
        }

        /// <summary>Whether one file is compiled into an assembly the game loads.</summary>
        private static bool IsShipped(string file, List<string> editorOnly)
        {
            string folder = Path.GetDirectoryName(file);
            if (folder == null)
            {
                return true;
            }

            for (int i = 0; i < editorOnly.Count; i++)
            {
                if (IsWithin(folder, editorOnly[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
