#!/usr/bin/env bash
# Field-level coverage audit.
#
# The component count answers "do we touch this component at all". This answers the question that
# actually decides a creator's freedom: OF THE FIELDS ON THAT COMPONENT, WHICH ONES CAN THEY REACH?
# A component written with three of its eleven fields counts as covered and is silently limiting
# what anyone can make with it.
#
# NOT EVERY UNWRITTEN FIELD IS A GAP. Two kinds are excluded because writing them would be wrong:
#
#   * self-wired siblings — a field the component assigns itself in OnValidate/Reset via
#     GetComponent, such as `level` and `entityMono`. Measured across the game these are always
#     [HideInInspector]; setting one from outside fights the component for a reference it
#     recomputes anyway.
#   * runtime state that happens to be public — listed by name in RUNTIME_STATE below, because
#     there is no marker in the source that distinguishes it. Each entry is a judgement, so each
#     one carries the component it belongs to rather than being a bare name.
#
# Usage:  bash Docs/audit-field-coverage.sh            # partial components only
#         bash Docs/audit-field-coverage.sh --detail   # every component, including full ones
#
# Reads:  the decompiled game at $CKDB, our Editor + Scripts sources at $ROOT.
# Writes: nothing. Prints a report.

set -u

CKDB="${CKDB:-E:/ck mods/ck-db}"
ROOT="${ROOT:-E:/ck mods/CoreKeeperModSDK/Assets/ExpandNullforge}"
DETAIL=0
[ "${1:-}" = "--detail" ] && DETAIL=1

# Public fields that are live state the systems write while the game runs. Format: Component.field
RUNTIME_STATE="
RandomWalkGravityAuthoring.isAffected
RandomWalkGravityAuthoring.timer
RandomWalkGravityAuthoring.position
MinecartAuthoring.currentSpeed
MinecartAuthoring.isBreaking
MusicAreaAuthoring.isInactive
RangeAttackStateAuthoring.ceasingToShoot
AffectObjectWhenMelodyPlayedAuthoring.listening
SummonAreaAuthoring.internalState
SummonAreaAuthoring.internalTimer
BossStatueAuthoring.hasCrystal
BossStatueAuthoring.doneLoadingUp
BossStatueAuthoring.delayedActivationTimer
SpawnerAuthoring.lastPosition
RandomWalkGravityWellAuthoring.timer
ElectricOrbAuthoring.internalState
HatchWhenPlayerNearbyStateAuthoring.timer
HatchWhenPlayerNearbyStateAuthoring.internalState
HatchWhenPlayerNearbyStateAuthoring.hatchAnimationIsPlaying
CoreBossBeamAuthoring.internalState
CoreBossBeamAuthoring.timer
CoreBossBeamAuthoring.dealDamageTimer
SpawnDroppedItemAuthoring.timer
WaterSpreaderAuthoring.timer
WaterSpreaderAuthoring.position
BirdBossBeamAuthoring.internalState
BirdBossBeamAuthoring.timer
BirdBossBeamAuthoring.dealDamageTimer
"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

# Every line of our generator-side code, once, so the field grep is a single pass per component.
cat "$ROOT"/Editor/*.cs "$ROOT"/Editor/**/*.cs "$ROOT"/Scripts/**/*.cs 2>/dev/null > "$WORK/src"

# WHICH FIELD ON WHICH COMPONENT — the thing a plain "\.field" grep cannot tell you.
#
# Field names repeat across Core Keeper's components: damageMultiplier is on eight of them,
# skipVisibilityCheck on five, canHitLowTriggers on two. A global grep therefore scored a field as
# covered for EVERY component that has it as soon as ONE of them was written. That is how
# MeleeAttackStateAuthoring read as 14/29 when it was really 12/29, and RangeAttackStateAuthoring as
# 14/42 when it was 9/43 — its damageMultiplier, the only ranged damage a tiered creature keeps, was
# scored as reached because a different component's identically-named field was written.
#
# So resolve each write to the type of the variable it is written through, per file, and record
# Component<TAB>field pairs. Same-named locals in different files stay separate because the map is
# rebuilt for each one.
: > "$WORK/writes"
find "$ROOT/Editor" "$ROOT/Scripts" -name '*.cs' 2>/dev/null | while IFS= read -r file; do
    # PER METHOD, not per file. DimensionObjectSpine declares "RangeWeaponAuthoring ranged" in one
    # method and "RangeAttackStateAuthoring ranged" in another; a file-wide map keeps only the last
    # and mis-attributes every write through that name. That scored RangeWeaponAuthoring at 1 of 22
    # when seven of its fields were written a few lines away.
    perl -0ne '
        for my $block (split /\n(?=        (?:public|private|internal|protected|static)[^\n]*\()/, $_) {
            my %type_of;
            # The (?:\w+\.)* allows a namespace-qualified declaration. Several Core Keeper
            # components live in their own namespace and have to be written as
            # "RayAttackState.RayAttackStateAuthoring ray = ..."; without this the character before
            # the type is a dot, the map never learns the variable, and every field written through
            # it reads as a gap. That scored RayAttackStateAuthoring at 0 of 16 with all sixteen set.
            while ($block =~ /(?:^|[\s(,])(?:\w+\.)*(\w+Authoring)\s+(\w+)\s*[=;),]/gm) { $type_of{$2} = $1 }

            # ApplyComponent<T>(root, ..., c => { c.field = ... }) — the lambda parameter takes its
            # type from the generic argument, and there is no declaration anywhere to read it from.
            #
            # EACH LAMBDA IS ITS OWN SCOPE, and it has to be, because every one of them names the
            # parameter "component". Folding them into the enclosing method map means the last call
            # wins and every earlier lambda body is attributed to the wrong component — which read
            # as DurabilityAuthoring 2/5 while all five were being written.
            #
            # So resolve each lambda body against its own generic argument, then blank those regions
            # out before the ordinary declaration pass runs over what is left.
            while ($block =~ /ApplyComponent\s*<[\w.]*?(\w+Authoring)>\s*\((?:[^;()]|\([^()]*\))*?(\w+)\s*=>\s*((?<brace>\{(?:[^{}]++|(?&brace))*\})|[^;]*)/gs) {
                my ($lambda_type, $param, $body) = ($1, $2, $3);
                while ($body =~ /\Q$param\E\.(\w+)/g) {
                    print "$lambda_type\t$1\n";
                }
            }

            $block =~ s/ApplyComponent\s*<[\w.]*?\w+Authoring>\s*\((?:[^;()]|\([^()]*\))*?\w+\s*=>\s*((?<brace2>\{(?:[^{}]++|(?&brace2))*\})|[^;]*)//gs;
            while ($block =~ /(\w+)\.(\w+)\b/g) {
                print "$type_of{$1}\t$2\n" if exists $type_of{$1};
            }
            while ($block =~ /(?:EnsureComponent|AddComponent|GetComponent|ApplyComponent)<[\w.]*?(\w+Authoring)>\s*\([^;]*?\)\s*\.\s*(\w+)/g) {
                print "$1\t$2\n";
            }
        }
    ' "$file" >> "$WORK/writes"
done
sort -u -o "$WORK/writes" "$WORK/writes"

COMPONENTS=$(grep -rhoP '(?:EnsureComponent|RemoveComponentIfPresent|AddComponent|GetComponent|ApplyComponent|Toggle)<\K[\w.]*?\K\w+Authoring' \
    "$ROOT"/Editor/*.cs "$ROOT"/Editor/**/*.cs "$ROOT"/Scripts/**/*.cs 2>/dev/null | sed 's/.*\.//' | sort -u)

total=0; reachable=0; excluded=0
n_all=0; n_full=0; n_partial=0; n_marker=0

printf '%-44s %4s %4s  %s\n' "COMPONENT" "HAVE" "OF" "NOT REACHABLE BY A CREATOR"
printf '%s\n' "--------------------------------------------------------------------------------"

for component in $COMPONENTS; do
    path=$(find "$CKDB" -name "$component.cs" 2>/dev/null | head -1)
    [ -z "$path" ] && continue

    # All public instance fields: not methods, properties, consts, statics or nested types.
    grep -oP '^\tpublic (?!static|const|abstract|virtual|override|class|struct|enum|readonly)[\w\.<>\[\],\s]+? \K\w+(?=\s*(?:=|;))' \
        "$path" 2>/dev/null | sort -u > "$WORK/all"

    # Excluded: self-wired siblings, hidden fields, and the named runtime state.
    {
        grep -oP 'this\.\K\w+(?=\s*=\s*base\.GetComponent)' "$path" 2>/dev/null
        grep -B 1 -oP '^\tpublic [\w\.<>\[\],\s]+? \K\w+(?=\s*(?:=|;))' "$path" 2>/dev/null | \
            grep -A 1 'HideInInspector' | grep -oP '^\d*[-:]?\K\w+$'
        awk -v c="$component" -F. '$1 == c { print $2 }' <<< "$RUNTIME_STATE"
    } 2>/dev/null | grep -v '^$' | sort -u > "$WORK/skip"

    # A [HideInInspector] field can sit anywhere; catch it by paragraph rather than by line pairs.
    perl -0ne 'while (/\[HideInInspector\][^]]*?public\s+[\w\.<>\[\],\s]+?\s(\w+)\s*[=;]/gs) { print "$1\n" }' \
        "$path" 2>/dev/null >> "$WORK/skip"
    sort -u -o "$WORK/skip" "$WORK/skip"

    comm -23 "$WORK/all" "$WORK/skip" > "$WORK/fields"

    skipped=$(comm -12 "$WORK/all" "$WORK/skip" | grep -c '' || true)
    excluded=$((excluded + skipped))

    count=$(grep -c '' < "$WORK/fields" || true)
    n_all=$((n_all + 1))

    if [ "$count" -eq 0 ]; then
        n_marker=$((n_marker + 1))
        continue
    fi

    have=0
    missing=""
    while IFS= read -r field; do
        if grep -qxF "$(printf '%s\t%s' "$component" "$field")" "$WORK/writes"; then
            have=$((have + 1))
        else
            missing="$missing $field"
        fi
    done < "$WORK/fields"

    total=$((total + count))
    reachable=$((reachable + have))

    if [ "$have" -eq "$count" ]; then
        n_full=$((n_full + 1))
        [ "$DETAIL" -eq 1 ] && printf '%-44s %4s %4s  -\n' "$component" "$have" "$count"
    else
        n_partial=$((n_partial + 1))
        printf '%-44s %4s %4s %s\n' "$component" "$have" "$count" "$missing"
    fi
done

printf '\n%s\n' "================================================================================"
printf 'Components written ................. %s\n' "$n_all"
printf '  fully reachable ................. %s\n' "$n_full"
printf '  partially reachable ............. %s\n' "$n_partial"
printf '  markers, nothing to reach ....... %s\n' "$n_marker"
printf 'Authorable fields on them ......... %s\n' "$total"
printf '  reachable by a creator .......... %s\n' "$reachable"
printf '  excluded as not ours ............ %s\n' "$excluded"
if [ "$total" -gt 0 ]; then
    printf '  FIELD COVERAGE .................. %s%%\n' "$(( reachable * 100 / total ))"
fi
