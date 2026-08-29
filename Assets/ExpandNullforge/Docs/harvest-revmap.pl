#!/usr/bin/perl
# Which template property each authoring field is written from, read out of the framework's own
# generator source so the two can never drift.
#   usage: harvest-revmap.pl <groups.tsv> <source.cs> [<source.cs> ...]
#   emits: ComponentAuthoring \t authoringField \t TemplateProperty[,alternatives]
use strict;
use warnings;

my $groups = shift @ARGV or die "groups file\n";
my %want;
open my $g, '<:encoding(UTF-8)', $groups or die "$groups: $!";
while (<$g>) {
    chomp;
    next if /^\s*(?:#|$)/;
    my ($comp) = split /\t/;
    $want{$comp} = 1 if $comp;
}
close $g;

for my $file (@ARGV) {
    open my $fh, '<:encoding(UTF-8)', $file or next;
    local $/;
    my $s = <$fh>;
    close $fh;

    # One method at a time, so two variables of different types cannot be confused for each other.
    for my $block (split /\n(?=        (?:public|private|internal|protected|static)[^\n]*\()/, $s) {
        my %type_of;
        while ($block =~ /(?:^|[\s(,])(?:\w+\.)*(\w+Authoring)\s+(\w+)\s*[=;),]/gm) { $type_of{$2} = $1 }

        while ($block =~ /(\w+)\.([\w.]+)\s*=\s*([^;]{1,400});/g) {
            my ($var, $ck, $rhs) = ($1, $2, $3);
            next unless exists $type_of{$var} && $want{ $type_of{$var} };

            my %props;
            while ($rhs =~ /\b\w+\.([A-Z]\w+)\b/g) { $props{$1} = 1 }
            next unless %props;

            print join("\t", $type_of{$var}, $ck, join(",", sort keys %props)), "\n";
        }
    }
}
