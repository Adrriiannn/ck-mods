using System;
using UnityEditor;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Makes a project folder exist, through Unity's own asset database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THROUGH THE ASSET DATABASE, NEVER BEHIND IT. Copies of this scattered across the tree take
    /// five shapes, and one of them calls <c>Directory.CreateDirectory</c> and then asks Unity to
    /// import the result. A folder made that way does not exist as far as the asset database is
    /// concerned until it has been imported, and an asset written into it in the same batch lands
    /// nowhere — which is why exactly those sites carry a retry-and-refresh block that the others
    /// do not need. <c>AssetDatabase.CreateFolder</c> has no such window.
    /// </para>
    /// <para>
    /// A FOLDER CREATED INSIDE A BATCH STILL DOES NOT EXIST UNTIL THE BATCH CLOSES. That is
    /// Unity's rule and no helper can work around it, so a caller that opens
    /// <c>StartAssetEditing</c> has to make its folders first. Said here because it is the failure
    /// this family of functions has actually produced: the first generated asset went missing and
    /// later ones appeared, which reads as flakiness rather than as a rule.
    /// </para>
    /// </remarks>
    internal static class DimensionAssetFolders
    {
        /// <summary>
        /// Makes <paramref name="folder"/> and every folder above it exist. Does nothing when the
        /// path is blank or already there.
        /// </summary>
        public static void Ensure(string folder)
        {
            string message;
            TryEnsure(folder, null, out message);
        }

        /// <summary>
        /// The same, answering whether the folder is there at the end of it.
        /// </summary>
        public static bool EnsureExists(string folder)
        {
            string message;
            return TryEnsure(folder, null, out message);
        }

        /// <summary>
        /// The same again, saying what went wrong in words a creator can act on.
        /// </summary>
        /// <param name="subject">
        /// What the folder is for, as it should read in the message — "portal package",
        /// "portal preset". Null when the caller has nowhere to show a message.
        /// </param>
        /// <remarks>
        /// The rename check is the part worth keeping: <c>AssetDatabase.CreateFolder</c> quietly
        /// gives back a different name when one is already taken or the name is not one Unity will
        /// accept, so the folder that comes back can be a sibling of the one that was asked for and
        /// everything written into it afterwards lands beside the work instead of in it.
        /// </remarks>
        public static bool TryEnsure(string folder, string subject, out string message)
        {
            message = string.Empty;

            string normalized = Normalize(folder);
            if (string.IsNullOrEmpty(normalized) ||
                (!string.Equals(normalized, "Assets", StringComparison.Ordinal) &&
                 !normalized.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                message = "The " + Describe(subject) + "folder '" + (folder ?? string.Empty) +
                          "' is not a path inside this project's Assets folder.";
                return false;
            }

            if (AssetDatabase.IsValidFolder(normalized))
            {
                return true;
            }

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    string created = string.IsNullOrEmpty(guid)
                        ? string.Empty
                        : Normalize(AssetDatabase.GUIDToAssetPath(guid));

                    if (!string.Equals(created, next, StringComparison.OrdinalIgnoreCase) &&
                        !AssetDatabase.IsValidFolder(next))
                    {
                        message = "Could not make the " + Describe(subject) + "folder '" +
                                  next + "'.";
                        return false;
                    }
                }

                current = next;
            }

            return AssetDatabase.IsValidFolder(normalized);
        }

        /// <summary>Forward slashes, no trailing slash.</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/').Trim();
            while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        /// <summary>The subject with a trailing space, or nothing at all when there is none.</summary>
        private static string Describe(string subject)
        {
            return string.IsNullOrEmpty(subject) ? string.Empty : subject + " ";
        }
    }
}
