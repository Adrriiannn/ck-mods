#!/usr/bin/env bash
# Audits whether authored data is REACHABLE FROM THE UI.
#
# The field-coverage audit answers "does the generator write this game field". This one answers the
# question that comes before it: can a creator sitting in the dashboard actually set the value? A
# field the generator honours but no panel draws is the unreachable-code class — asset, generator
# and tests all green, and nothing a person can click. We have shipped that bug three times.
#
# TWO THINGS THIS HAS TO MODEL, or the number it prints is a lie.
#
# 1. TESTS ARE NOT A UI. Editor/Tests reaches fields through SerializedObject exactly the way a panel
#    does, so grepping all of Editor/ makes every field a test touches look reachable. The first
#    run of this script reported the creature combat template as reachable purely because a test
#    written minutes earlier called FindProperty("combat").
#
# 2. DRAWING A TEMPLATE DRAWS ITS CHILDREN. Field(name) defaults to includeChildren: true, so a
#    PropertyField on a template-typed field renders every field inside it as a foldout. Nested
#    fields therefore inherit their parent's reachability and must not be counted one by one.
#
# So the real question is which TOP-LEVEL asset fields are drawn, plus which template types are
# orphaned — never referenced by any reachable field, and so unreachable however complete they are.
#
# SINCE THE CATCH-ALL LANDED, a "NOT DRAWN" line is a UI-polish signal, not a functionality gap.
# DrawSerializedAsset now renders every serialized field the curated list omits, under an
# "Everything else" foldout, so nothing is unreachable. What this script reports is which fields
# still have no considered place in a panel — the worklist for when the UI is designed properly.
#
# Generated-output assets are excluded by name: nobody authors them.
cd "$(dirname "$0")/.."

EXCLUDE_ASSETS="DimensionRuntimeManifestAsset DimensionPortalPackageAsset"

# Every property name bound by real UI code — the curated Field() lists and the Studios' own
# FindProperty binding — with tests excluded.
ui_sources() {
  find Editor -name '*.cs' -not -path 'Editor/Tests/*'
}

# Any exact quoted literal in non-test editor code counts as a binding. There are at least three
# binding mechanisms in this codebase — the curated Field("x") lists, the Studios' own
# FindProperty("x"), and DrawProperty(profile, "x", label) / GetFloat / SetSerializedString — and
# matching them one at a time kept producing false gaps. The field names are distinctive enough
# that an exact quoted match is a truer test than enumerating call shapes.
BOUND=$(ui_sources | xargs grep -hoE '"[A-Za-z_][A-Za-z0-9_]*"' 2>/dev/null | tr -d '"' | sort -u)

is_bound() { echo "$BOUND" | grep -qx "$1"; }

# Field name -> declared type, for every serialized field in the authoring layer.
decl() { grep -oP '(?<=\[SerializeField\] private )[\w\[\]<>., ]+ \w+(?= =|;)' "$1"; }

echo "=== assets: are the top-level fields drawn? ==="
total_f=0; total_d=0
REACHABLE_TYPES=""
for f in Scripts/Authoring/*Asset.cs; do
  a=$(basename "$f" .cs)
  case " $EXCLUDE_ASSETS " in *" $a "*) continue;; esac

  # DOES THIS ASSET REACH A PANEL AT ALL?
  #
  # DrawSerializedAsset renders every serialized field its curated list omits, under an "Everything
  # else" foldout. So for any asset passed to it, EVERY field is reachable and the curated list is
  # only about ordering. For an asset nothing passes to it, only what a Studio binds by name is.
  #
  # That distinction is the whole point of this pass. Without it a template held by an uncurated
  # field reads as orphaned when a creator can already reach it, and the audit sends you off to
  # re-solve a solved problem.
  if ui_sources | xargs grep -lw "$a" 2>/dev/null | xargs grep -l "DrawSerializedAsset" 2>/dev/null | grep -q .; then
    drawn_wholesale=yes
  else
    drawn_wholesale=no
  fi

  nf=0; nd=0; miss=""
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    name=${line##* }
    type=${line% *}
    nf=$((nf+1))
    if [ "$drawn_wholesale" = yes ]; then
      REACHABLE_TYPES="$REACHABLE_TYPES $(echo "$type" | tr -d '[]' )"
    fi

    if is_bound "$name"; then
      nd=$((nd+1))
      REACHABLE_TYPES="$REACHABLE_TYPES $(echo "$type" | tr -d '[]' )"
    else
      miss="$miss $name"
    fi
  done <<< "$(decl "$f")"

  [ "$nf" -eq 0 ] && continue
  total_f=$((total_f+nf)); total_d=$((total_d+nd))
  [ "$nd" -lt "$nf" ] && printf '%-38s %3d/%3d  NOT DRAWN:%s\n' "$a" "$nd" "$nf" "$miss"
done

# A template reached by a drawn field renders its children too, and anything IT holds is reached in
# turn — so close the set over nesting before judging any template orphaned.
#
# The closure walks every authoring class, not only *Template.cs. DimensionCreatureAbility is a
# plain serializable class that holds two templates, so a Template-only walk stopped at it and
# called both of them orphaned when an ability list already reaches them.
for pass in 1 2 3 4 5; do
  for f in Scripts/Authoring/Dimension*.cs; do
    t=$(basename "$f" .cs)
    case " $REACHABLE_TYPES " in *" $t "*) ;; *) continue;; esac
    while IFS= read -r line; do
      [ -z "$line" ] && continue
      REACHABLE_TYPES="$REACHABLE_TYPES $(echo "${line% *}" | tr -d '[]')"
    done <<< "$(decl "$f")"
  done
done

echo
echo "=== templates: orphaned, or reached through a drawn parent? ==="
t_all=0; t_ok=0
for f in Scripts/Authoring/Dimension*Template.cs; do
  t=$(basename "$f" .cs)
  n=$(decl "$f" | wc -l)
  [ "$n" -eq 0 ] && continue
  t_all=$((t_all+1))
  case " $REACHABLE_TYPES " in
    *" $t "*) t_ok=$((t_ok+1));;
    *) printf '%-44s ORPHANED (%s fields no panel can reach)\n' "$t" "$n";;
  esac
done

echo
echo "asset fields drawn: $total_d / $total_f"
echo "templates reached:  $t_ok / $t_all"
