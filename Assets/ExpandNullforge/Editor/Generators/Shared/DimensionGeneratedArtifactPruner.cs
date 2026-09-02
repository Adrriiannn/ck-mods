using System;
using System.Collections.Generic;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Works out which prefabs a previous generation left behind, so renaming an item does not leave
    /// a ghost of its old self in the mod.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY AN ORPHAN IS NOT MERELY UNTIDY. PugMod bundles a mod by scanning its folders, not by
    /// following references — so a prefab nobody points at is still shipped, still converted, and
    /// still registers an object. Rename <c>blade</c> to <c>sword</c> and the player gets both: a
    /// working Sword and a Blade that no recipe makes, no loot drops, and no dashboard field can
    /// reach. It also keeps consuming a name in the object table, which is the one place collisions
    /// hard-fail a world load.
    /// </para>
    /// <para>
    /// WHY IT COMPARES AGAINST THE PREVIOUS RUN RATHER THAN THE FOLDER. Deleting "every prefab not
    /// in the current item list" would also delete things the framework never made — a modder's
    /// hand-authored prefab, a portal object, the ore vein objects — because none of those are
    /// items. The runtime manifest already records exactly what the last generation produced, so the
    /// safe set is precisely <c>previous - current</c>: files this framework created and no longer
    /// wants. Anything it did not create is invisible to this class by construction.
    /// </para>
    /// <para>
    /// The ids in the manifest are mod-qualified (<c>MyMod:blade</c>) while prefab files are named
    /// from the bare id (<c>blade.prefab</c>), so the qualifier is stripped before matching. Pure
    /// string work, deliberately separated from the asset deletion that consumes it, so the decision
    /// of what to remove is unit-testable without touching a project.
    /// </para>
    /// </remarks>
    public static class DimensionGeneratedArtifactPruner
    {
        /// <summary>
        /// Bare item ids that the previous run generated and this one did not, in the order they
        /// appeared. Empty when nothing went stale.
        /// </summary>
        public static List<string> FindOrphanedItemIds(
            IEnumerable<string> previouslyGeneratedIds,
            IEnumerable<string> currentGeneratedIds)
        {
            List<string> orphans = new List<string>();
            if (previouslyGeneratedIds == null)
            {
                return orphans;
            }

            HashSet<string> current = new HashSet<string>(StringComparer.Ordinal);
            if (currentGeneratedIds != null)
            {
                foreach (string id in currentGeneratedIds)
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        current.Add(DimensionObjectNamespace.LocalIdOf(id));
                    }
                }
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in previouslyGeneratedIds)
            {
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                string local = DimensionObjectNamespace.LocalIdOf(id);
                if (local.Length == 0 || current.Contains(local) || !seen.Add(local))
                {
                    continue;
                }

                orphans.Add(local);
            }

            return orphans;
        }
    }
}
