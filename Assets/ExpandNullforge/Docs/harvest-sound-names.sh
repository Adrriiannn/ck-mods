#!/usr/bin/env bash
# Rebuilds Scripts/Authoring/Fields/DimensionSoundNames.cs — every name the game can play a sound by.
#
# The game stores a sound as Animator.StringToHash(name) and throws the name away. The names
# survive in two places, and together they are the whole list:
#   1. the shipped sound element assets — their FILE NAMES are the names the hash is made from
#   2. the game's own SfxTableID class — every field hashes one string literal
#
# Run after a Core Keeper update. Arguments: [ripped-assets-root] [ck-db-root]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/.." && pwd)"
ripped="${1:-E:/Tools/CoreKeeperRippedAssets/ExportedProject/Assets}"
ckdb="${2:-E:/ck mods/ck-db}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

ls "$ripped/Resources/sfxtableelements" | grep -v '.meta' | sed 's/\.asset$//' > "$work/names.txt"
grep -oP 'StringToHash\("\K[^"]+' "$ckdb/Pug.Base/SfxTableID.cs" >> "$work/names.txt"
sort -u "$work/names.txt" -o "$work/names.txt"

count=$(wc -l < "$work/names.txt")

# The header lives beside this script so the generated file's prose is versioned with it.
{
  cat "$here/sound-names-header.cs.txt"
  printf '\n        /// <summary>All %s names the game ships sounds under.</summary>\n' "$count"
  printf '        public static readonly string[] All = new string[]\n        {\n'
  perl "$here/quote-strings.pl" "$work/names.txt"
  printf '        };\n\n'
  printf '        private static readonly HashSet<string> Known = new HashSet<string>(All);\n'
  printf '    }\n}\n'
} > "$root/Scripts/Authoring/Fields/DimensionSoundNames.cs"

echo "wrote $count sound names"
