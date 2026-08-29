#!/usr/bin/env bash
# Rebuilds Editor/Authoring/Catalogs/DimensionBorrowedAttacks.cs from the Core Keeper dictionary vault.
#
# The vault (an Obsidian corpus of every shipped prefab) is the only place that records what Core
# Keeper's own designers authored on each creature. This turns those transcriptions into the
# framework's borrow-one-of-the-game's table, so a modder picks "Desert Hydra — its close-up swing"
# and gets the game's exact numbers rather than someone's guess at them.
#
# Four of the five inputs are derived from the framework's own source, so none of them can drift
# from it:
#   vault   — the authored values, per prefab, per component
#   revmap  — which template property each authoring field is written from (read from the spine)
#   props   — which template class declares each property, and its serialized field name
#   subs    — how the templates nest, which is what turns a class into a dotted path
# Only two are written by hand: borrowed-attack-names.tsv (the creature names an author reads) and
# borrowed-groups.tsv (which components are offered, and what each is called).
#
# Adding a new sort of borrowable thing is one line in borrowed-groups.tsv, as long as its template
# hangs off the creature's combat block somewhere.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/.." && pwd)"
vault="${1:-E:/ck mods/Core Keeper Dictionary/Assets}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

components=$(grep -v '^\s*#' "$here/borrowed-groups.tsv" | grep -v '^\s*$' | cut -f1 | tr '\n' ' ')

# shellcheck disable=SC2086
perl "$here/harvest-vault.pl" "$vault" $components > "$work/values.tsv"

perl "$here/harvest-revmap.pl" "$here/borrowed-groups.tsv" \
  "$root/Editor/DimensionCreatureGenerator.cs" \
  "$root/Editor/DimensionObjectSpine.cs" | sort -u > "$work/revmap.tsv"

# Two fields are written through a resolve call rather than a plain assignment, so the reader
# cannot see them. They are named here instead of being silently missing from every preset.
cat >> "$work/revmap.tsv" <<'EXTRA'
RangeAttackStateAuthoring	projectileID	ProjectileItemId
MeleeAttackStateAuthoring	objectToSpawnOnHitTiles	SpawnsOnBrokenTilesId
EXTRA
sort -u "$work/revmap.tsv" -o "$work/revmap.tsv"

( cd "$root/Scripts/Authoring" && perl "$here/harvest-props.pl" Dimension*.cs ) \
  | sort -u > "$work/props.tsv"

( cd "$root/Scripts/Authoring" && perl "$here/harvest-subs.pl" Dimension*.cs ) \
  | sort -u > "$work/subs.tsv"

perl "$here/harvest-emit.pl" \
  "$work/values.tsv" \
  "$work/revmap.tsv" \
  "$work/props.tsv" \
  "$here/borrowed-attack-names.tsv" \
  "$work/subs.tsv" \
  "$here/borrowed-groups.tsv" \
  > "$root/Editor/Authoring/Catalogs/DimensionBorrowedAttacks.cs"

presets=$(grep -c 'new Preset' "$root/Editor/Authoring/Catalogs/DimensionBorrowedAttacks.cs")
values=$(grep -c 'new Value(' "$root/Editor/Authoring/Catalogs/DimensionBorrowedAttacks.cs")
echo "wrote $presets presets carrying $values measured values"
