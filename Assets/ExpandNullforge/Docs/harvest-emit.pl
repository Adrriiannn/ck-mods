#!/usr/bin/perl
# Turns harvested vault values into the framework's borrow-one-of-the-game's table.
#   usage: harvest-emit.pl <attacks.tsv> <revmap.tsv> <props.tsv> <names.tsv> <subs.tsv> <groups.tsv>
#
# The path each value lands on is worked out rather than written down: a value belongs to a template
# property, that property is declared on some template class, and that class hangs off the creature's
# combat block at some serialized field. Following those three facts gives "meleeShape.hitboxOffset"
# without anybody maintaining a list of paths that would rot the moment a field moved.
use strict;
use warnings;
use utf8;
binmode(STDOUT, ":utf8");

my ($attacks, $revmap, $props, $names, $subs, $groups) = @ARGV;

# The block everything is written relative to.
my $ROOT = 'DimensionCreatureCombatTemplate';

# ---- which template property each authoring field is written from ----
my %prop_for;
open my $r, '<:encoding(UTF-8)', $revmap or die "$revmap: $!";
while (<$r>) {
    chomp;
    my ($comp, $field, $props_csv) = split /\t/;
    my @candidates = grep { /^[A-Z]/ } split /,/, ($props_csv // '');
    @candidates = grep { $_ ne 'Mathematics' && $_ ne 'None' && $_ ne 'IsNullOrEmpty' } @candidates;
    next unless @candidates == 1;
    $prop_for{"$comp\t$field"} = $candidates[0] unless exists $prop_for{"$comp\t$field"};
}
close $r;

# ---- which template class declares each property, and under what serialized name ----
my %declared;
open my $p, '<:encoding(UTF-8)', $props or die "$props: $!";
while (<$p>) {
    chomp;
    my ($class, $prop, $field, $type) = split /\t/;
    push @{ $declared{$prop} }, [$class, $field, $type];
}
close $p;

# ---- how the templates nest, so a class can be turned into a path ----
my %children;
open my $s, '<:encoding(UTF-8)', $subs or die "$subs: $!";
while (<$s>) {
    chomp;
    my ($owner, $field, $child) = split /\t/;
    push @{ $children{$owner} }, [$field, $child];
}
close $s;

# Breadth-first from the combat block, so the shallowest path to a class wins.
my %path_to = ($ROOT => '');
{
    my @queue = ($ROOT);
    while (@queue) {
        my $owner = shift @queue;
        for my $link (@{ $children{$owner} || [] }) {
            my ($field, $child) = @$link;
            next if exists $path_to{$child};
            $path_to{$child} = $path_to{$owner} eq '' ? $field : "$path_to{$owner}.$field";
            push @queue, $child;
        }
    }
}

# ---- creature display names ----
# The file is an OVERRIDE list, not a census: most prefab names turn into something readable on
# their own, and only the ones that do not are written down. That way a component that turns out to
# sit on four hundred prefabs does not need four hundred lines typed before it can be offered.
my %name_for;
open my $n, '<:encoding(UTF-8)', $names or die "$names: $!";
while (<$n>) {
    chomp;
    next if /^\s*(?:#|$)/;
    my ($k, $v) = split /\t/;
    $name_for{$k} = $v if defined $v && $v ne '';
}
close $n;

sub readable {
    my $prefab = shift;
    return $name_for{$prefab} if exists $name_for{$prefab};

    # A prefab placed inside a handmade scene is the same thing twice; offering both would put two
    # identical entries in the list under slightly different names.
    return undef if $prefab =~ /_[A-Za-z]+_\d+$/ || $prefab =~ / \(\d+\)/;

    (my $name = $prefab) =~ s/Entity$//;
    $name =~ s/([a-z0-9])([A-Z])/$1 $2/g;
    $name =~ s/([A-Z]+)([A-Z][a-z])/$1 $2/g;
    return $name eq '' ? undef : $name;
}

# ---- which components are offered, and what each is called ----
my (%kind_of, @kind_order);
open my $g, '<:encoding(UTF-8)', $groups or die "$groups: $!";
while (<$g>) {
    chomp;
    next if /^\s*(?:#|$)/;
    my ($comp, $kind, $phrase) = split /\t/;
    $kind_of{$comp} = [$phrase, $kind];
    push @kind_order, $kind unless grep { $_ eq $kind } @kind_order;
}
close $g;

# The three attacks that live on an ability entry rather than flat on the combat block, and the
# number the ability's kind enum gives each. Their values travel with an "@ability." prefix; the
# applier appends one entry to the abilities list and writes them inside it.
my %ability_kind = (
    ChargeAttackStateAuthoring => 0,
    JumpAttackStateAuthoring   => 1,
    ExplodeStateAuthoring      => 3,
);

# Where a declaring class sits inside one ability entry.
my %ability_path = (
    DimensionCreatureAbility       => '',
    DimensionChargeShapeTemplate   => 'chargeShape',
    DimensionMortarBarrageTemplate => 'mortarBarrage',
);

my (%values, %order);
my $skipped_references = 0;
open my $a, '<:encoding(UTF-8)', $attacks or die "$attacks: $!";
while (<$a>) {
    chomp;
    my ($prefab, $comp, $field, $authored, $decode) = split /\t/;
    next unless exists $kind_of{$comp};
    my $creature_name = readable($prefab);
    next unless defined $creature_name;

    my $sub = '';
    if ($field =~ /^(\w+)\.([xyz])$/) { ($field, $sub) = ($1, $2) }

    # A sound field is a one-member struct, so the vault records it as "attackSoundId.value".
    # The wrapper is the game's own packaging, not a setting.
    $field =~ s/\.value$//;

    # The area level decides this one at bake time; an authored number is not a setting.
    next if $field eq 'level';

    # A pointer at another object cannot travel in a preset: it is a reference into the prefab it
    # came from. Most of them are null or point at a sibling component, which is wiring rather than
    # a setting. The two that are not (the Robot Miner and Patroller walk patterns) are counted and
    # said out loud, so a real value is never dropped in silence.
    if ($authored =~ /^\{fileID:/) {
        $skipped_references++ if $authored !~ /fileID: 0[,\}]/ || $authored =~ /guid/;
        next;
    }

    my $prop = $prop_for{"$comp\t$field"} or next;
    # A hashed value (a sound) is written from the X property but declared as the XName field,
    # because the framework asks for the name and does the hashing itself.
    my $decl = $declared{$prop} || $declared{$prop . "Name"} or next;

    # A property name can be declared on more than one template. The one that is reachable from the
    # combat block, and shallowest, is the one this value means.
    my ($chosen, $path);
    if (exists $ability_kind{$comp}) {
        # An ability attack: the declaring class has to sit inside one ability entry.
        for my $candidate (@$decl) {
            if (exists $ability_path{ $candidate->[0] }) { $chosen = $candidate; last }
        }
        next unless $chosen;
        my $prefix = $ability_path{ $chosen->[0] };
        $path = '@ability.' . ($prefix eq '' ? '' : "$prefix.") . $chosen->[1];
    }
    else {
        my $best = 99;
        for my $candidate (@$decl) {
            my $class = $candidate->[0];
            next unless exists $path_to{$class};
            my $depth = ($path_to{$class} =~ tr/.//) + ($path_to{$class} eq '' ? 0 : 1);
            if ($depth < $best) { $best = $depth; $chosen = $candidate }
        }
        next unless $chosen;
        my $prefix = $path_to{ $chosen->[0] };
        $path = $prefix eq '' ? $chosen->[1] : "$prefix." . $chosen->[1];
    }
    my ($class, $sfield, $type) = @$chosen;
    $path .= ".$sub" if $sub ne '';

    # An id is authored as a number and named in the decode, so the readable name is used. An enum
    # keeps its number: every DimensionX enum in the framework mirrors the game's own order on
    # purpose, so the number carries it exactly, and the applier still accepts a name when one fits.
    my $text = $authored;
    if ($type eq 'string' && ($decode // '') =~ /(?:ObjectID|MelodyID|LootTableID)\.(\w+)/) {
        my $named = $1;
        $text = $named eq 'None' ? '' : $named;
    }
    elsif ($type eq 'string' && $authored =~ /^-?\d+$/ && $field =~ /[Ss]ound/i) {
        # A sound: the prefab stores only the hash of its name. Perl cannot run the hash
        # backwards, but the C# applier can — it holds the full name list — so the hash
        # travels marked, and the applier turns it back into the name or says it could not.
        $text = $authored eq "0" ? "" : "#sfx:$authored";
    }

    my $key = "$prefab\t$comp";
    if (exists $ability_kind{$comp} && !exists $order{$key}) {
        push @{ $values{$key} }, ['@ability.kind', $ability_kind{$comp}];
    }
    push @{ $values{$key} }, [$path, $text];
    $order{$key} = 1;
}
close $a;

if ($skipped_references > 0) {
    print STDERR "note: $skipped_references values point at another object and cannot travel in a "
        . "preset; they are left at their defaults
";
}

sub cs { my $s = shift; $s =~ s/\\/\\\\/g; $s =~ s/"/\\"/g; return "\"$s\"" }

print <<'HEAD';
// GENERATED FILE — do not hand-edit.
//
// Produced by Docs/harvest-borrowed-attacks.sh from the Core Keeper dictionary vault, which
// transcribes every authored value out of the game's own prefabs. Re-run that script to refresh it.
using System;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Every attack the game itself authored, ready to be borrowed whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE FIRST OF THE TWO DOORS. An author making a creature can build an attack from
    /// nothing, or take one of these and change as much or as little of it as they like. Taking one
    /// and changing nothing is a finished answer: the numbers are the game's own, measured off the
    /// prefab that ships with it, not approximations.
    /// </para>
    /// <para>
    /// A PRESET IS NOT A LOCK. Picking one writes its values into the ordinary fields, where they
    /// are then just values — every one of them stays editable, and the creature does not remember
    /// which preset it came from.
    /// </para>
    /// </remarks>
    public static class DimensionBorrowedAttacks
    {
        /// <summary>One authored value, and the field it belongs in.</summary>
        [Serializable]
        public struct Value
        {
            /// <summary>The field's path inside the combat block, dotted for nested blocks.</summary>
            public string Path;

            /// <summary>The value the game authored, as text.</summary>
            public string Text;

            public Value(string path, string text)
            {
                Path = path;
                Text = text;
            }
        }

        /// <summary>One of the game's own attacks.</summary>
        [Serializable]
        public sealed class Preset
        {
            /// <summary>What it is called in the list an author picks from.</summary>
            public string Name;

            /// <summary>Which creature it came from.</summary>
            public string Creature;

            /// <summary>Which sort of thing it is — Melee, Ranged, Sounds and so on.</summary>
            public string Kind;

            /// <summary>Every value it carries.</summary>
            public Value[] Values;
        }

HEAD

my @keys = sort keys %order;
printf("        /// <summary>All %d of them, in one list.</summary>\n", scalar @keys);
print "        public static readonly Preset[] All = new Preset[]\n        {\n";
for my $key (@keys) {
    my ($prefab, $comp) = split /\t/, $key;
    my ($what, $kind) = @{ $kind_of{$comp} };
    my $creature = readable($prefab);
    my $label = "$creature — $what";
    print "            new Preset\n            {\n";
    print "                Name = ", cs($label), ",\n";
    print "                Creature = ", cs($creature), ",\n";
    print "                Kind = ", cs($kind), ",\n";
    print "                Values = new Value[]\n                {\n";
    my %seen;
    for my $v (@{ $values{$key} }) {
        next if $seen{ $v->[0] }++;
        print "                    new Value(", cs($v->[0]), ", ", cs($v->[1]), "),\n";
    }
    print "                }\n            },\n";
}
print "        };\n\n";

print "        /// <summary>The sorts of thing that can be borrowed, in the order they are offered.</summary>\n";
print "        public static readonly string[] Kinds = new string[]\n        {\n";
print "            ", cs($_), ",\n" for @kind_order;
print "        };\n";

print <<'TAIL';

        /// <summary>The presets of one kind, for the picker that offers them.</summary>
        public static Preset[] OfKind(string kind)
        {
            int count = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind)
                {
                    count++;
                }
            }

            Preset[] matching = new Preset[count];
            int next = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind)
                {
                    matching[next++] = All[i];
                }
            }

            return matching;
        }

        /// <summary>The preset with that name, or null when nothing is called that.</summary>
        public static Preset ByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Name == name)
                {
                    return All[i];
                }
            }

            return null;
        }
    }
}
TAIL
