using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The files a generator wrote for one authored thing, found from its id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THIS CLOSES. PugMod bundles a mod by scanning folders, not by following references, so
    /// a prefab nobody points at is still shipped, still converted, and still registers an object.
    /// Rename a monster and the player gets both. That is not untidiness: Core Keeper's
    /// <c>DefaultConvertSystem</c> builds a <c>Dictionary&lt;string, int&gt;</c> of object names
    /// with <c>.Add</c>, which throws on a duplicate, and it early-returns on a brand-new world —
    /// so a ghost fails on the player's SECOND load, after they have built in it.
    /// </para>
    /// <para>
    /// <c>DimensionGeneratedArtifactPruner</c> has protected items from this since it was written,
    /// and only items: it has one caller, in the item generator, and
    /// <c>grep DeleteAsset</c> over the creature, container, plant, workbench, world-object,
    /// projectile, explosion, critter and vehicle generators returns nothing. This class is the
    /// other nine domains' half of it, reached from the two places a creator actually renames and
    /// deletes rather than from nine generators.
    /// </para>
    /// <para>
    /// IT ONLY EVER NAMES FILES THE GENERATORS THEMSELVES NAME. Every path here is composed the
    /// same way the generator composes it — the generator's own <c>FolderName</c> constant, its own
    /// <c>SanitizeAuthoredName</c> call, its own suffixes — and a path that does not exist on disk
    /// is dropped. So a hand-authored prefab, a portal object or an ore vein cannot be caught by
    /// it, because none of them is named this way.
    /// </para>
    /// </remarks>
    internal static class DimensionGeneratedFootprint
    {
        /// <summary>
        /// The generated files that exist for <paramref name="id"/> under this dimension's mod root.
        /// Empty when the kind generates no prefabs, or when nothing has been generated yet.
        /// </summary>
        internal static List<string> Of(
            DimensionTemplateAsset template,
            DimensionIdentityField field,
            string id)
        {
            List<string> found = new List<string>();
            if (template == null)
            {
                return found;
            }

            string modRoot = DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(
                AssetDatabase.GetAssetPath(template));
            if (string.IsNullOrEmpty(modRoot))
            {
                return found;
            }

            List<string> candidates = PathsUnder(modRoot, field, id);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(candidates[i]) != null)
                {
                    found.Add(candidates[i]);
                }
            }

            return found;
        }

        /// <summary>
        /// Where a generator would have written this thing's files, whether or not they are there.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Pure string work and no <c>AssetDatabase</c>, so the composition can be checked against
        /// the generators' own without a project on disk — which is the only thing that could
        /// silently go wrong here. A folder or a suffix that stops matching would make this class
        /// find nothing and say nothing, which is exactly the state it was written to end.
        /// </para>
        /// <para>
        /// BOTH FORMS OF THE ID ARE OFFERED. A generator sanitizes the id as it was typed, so a
        /// creator who typed a qualifier got "MyMod_blade.prefab" while one who did not got
        /// "blade.prefab". Rather than ruling on which, both are composed and the caller keeps the
        /// ones that exist — a candidate that names nothing costs nothing.
        /// </para>
        /// </remarks>
        internal static List<string> PathsUnder(
            string modRoot,
            DimensionIdentityField field,
            string id)
        {
            List<string> paths = new List<string>();
            if (string.IsNullOrEmpty(modRoot) || field == null || string.IsNullOrEmpty(id) ||
                field.GeneratedFolder.Length == 0 || field.PrefabSuffixes.Length == 0)
            {
                return paths;
            }

            string folder = modRoot + "/" + field.GeneratedFolder + "/";
            string asTyped = DimensionGeneratedPrefabUtility.SanitizeAuthoredName(
                id, field.PrefabWordWhenBlank);
            string local = DimensionGeneratedPrefabUtility.SanitizeAuthoredName(
                DimensionObjectNamespace.LocalIdOf(id), field.PrefabWordWhenBlank);

            for (int i = 0; i < field.PrefabSuffixes.Length; i++)
            {
                string suffix = field.PrefabSuffixes[i] + ".prefab";
                paths.Add(folder + asTyped + suffix);
                if (!string.Equals(local, asTyped, System.StringComparison.Ordinal))
                {
                    paths.Add(folder + local + suffix);
                }
            }

            return paths;
        }

        /// <summary>
        /// Deletes those files, and returns the ones that would not go.
        /// </summary>
        /// <remarks>
        /// A locked file is reported rather than thrown on: it is still shipped and still registers
        /// an object until it is removed by hand, and saying which one is the only thing that lets
        /// anyone do that. Deliberately not called from a rename — file deletion is outside undo,
        /// so a rename that removed files could be undone into a project missing them.
        /// </remarks>
        internal static List<string> Remove(IReadOnlyList<string> paths)
        {
            List<string> stuck = new List<string>();
            for (int i = 0; paths != null && i < paths.Count; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]) == null)
                {
                    continue;
                }

                if (!AssetDatabase.DeleteAsset(paths[i]))
                {
                    stuck.Add(paths[i]);
                }
            }

            return stuck;
        }
    }
}
