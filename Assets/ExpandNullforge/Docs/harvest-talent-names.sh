#!/usr/bin/env bash
# Rebuilds Scripts/Authoring/DimensionVanillaTalentNames.cs from the shipped talent table.
#
# The 96 names in Core Keeper's twelve talent trees are the dividing line between "I only want to
# change this talent's numbers" and "I am writing a talent of my own": a row that reuses one of
# these names keeps the game's own wording and picture, and anything else needs a line written for
# it in the mod's own table. That check has to be data read off the shipped asset, not a list
# somebody typed, or it goes stale on the next Core Keeper update.
#
# Usage:  Docs/harvest-talent-names.sh [path-to-ExportedProject/Assets/Resources/SkillTalentsTable.asset]
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
src="${1:-E:/Tools/CoreKeeperRippedAssets/ExportedProject/Assets/Resources/SkillTalentsTable.asset}"
out="$here/../Scripts/Authoring/DimensionVanillaTalentNames.cs"
tsv="$here/vanilla-talent-names.tsv"

awk '
/^  - skillID:/ { skill=$3 }
/^    - name:/ { print skill "\t" $3 }
' "$src" > "$tsv"

{
  cat <<'HEADER'
// GENERATED FILE — do not hand-edit.
//
// Produced by Docs/harvest-talent-names.sh from the shipped SkillTalentsTable, whose twelve trees
// hold eight talents each. Re-run the script after a Core Keeper update.
using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The names Core Keeper's own talents go by, which is what tells a rewrite from a new talent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TALENT'S NAME IS ITS KEY, not its wording. The talent window shows
    /// <c>SkillTalents/&lt;name&gt;</c> looked up in the language table, so writing one of these
    /// names keeps the game's own wording in every language it ships. Writing anything else needs a
    /// line of your own, or the square shows the raw key.
    /// </para>
    /// <para>
    /// The order within a skill is the order the talent window lays the eight squares out in.
    /// </para>
    /// </remarks>
    public static class DimensionVanillaTalentNames
    {
        /// <summary>Whether this is one of the game's own talent names.</summary>
        public static bool IsAGameTalent(string name)
        {
            return !string.IsNullOrEmpty(name) && Known.Contains(name);
        }

        /// <summary>The eight names on one skill's tree, in the order the window shows them.</summary>
        public static IReadOnlyList<string> ForSkill(SkillID skill)
        {
            int index = (int)skill;
            return index >= 0 && index < BySkill.Length ? BySkill[index] : new string[0];
        }

HEADER

  printf '        /// <summary>The eight talents on each of the twelve trees, tree by tree.</summary>\n'
  printf '        public static readonly string[][] BySkill = new string[][]\n        {\n'
  for i in 0 1 2 3 4 5 6 7 8 9 10 11; do
    printf '            new string[]\n            {\n'
    awk -F'\t' -v s="$i" '$1==s { printf "                \"%s\",\n", $2 }' "$tsv"
    printf '            },\n'
  done
  printf '        };\n\n'

  printf '        /// <summary>All 96 of them, for a plain contains check.</summary>\n'
  printf '        public static readonly string[] All = new string[]\n        {\n'
  awk -F'\t' '{ printf "            \"%s\",\n", $2 }' "$tsv"
  printf '        };\n\n'
  printf '        private static readonly HashSet<string> Known = new HashSet<string>(All);\n'
  printf '    }\n}\n'
} > "$out"

echo "wrote $out"
