#!/usr/bin/perl
# Harvests authored component values out of the Obsidian dictionary vault.
#   usage: harvest.pl <vault-dir> <ComponentAuthoring> [<ComponentAuthoring> ...]
#   emits: prefab \t component \t field \t authored \t decode
use strict;
use warnings;
binmode(STDOUT, ":utf8");

my $dir = shift @ARGV or die "vault dir\n";
my %want = map { $_ => 1 } @ARGV;

opendir(my $dh, $dir) or die "$dir: $!";
my @pages = sort grep { /\.md$/ } readdir($dh);
closedir $dh;

for my $page (@pages) {
    open my $fh, '<:encoding(UTF-8)', "$dir/$page" or next;
    local $/;
    my $text = <$fh>;
    close $fh;

    (my $prefab = $page) =~ s/\.md$//;

    my @parts = split /^### \d+\. (?:\[\[)?([A-Za-z0-9_]+)(?:\]\])?\s*$/m, $text;
    shift @parts;

    while (@parts >= 2) {
        my $component = shift @parts;
        my $body      = shift @parts;
        next unless $want{$component};

        for my $line (split /\n/, $body) {
            next unless $line =~ /^\|\s*`([\w.\[\]]+)`\s*\|\s*(.*?)\s*\|\s*(.*?)\s*\|\s*$/;
            my ($field, $authored, $decode) = ($1, $2, $3);
            next if $field eq 'Field';
            for ($authored, $decode) { s/^`//; s/`$//; s/`//g }
            print join("\t", $prefab, $component, $field, $authored, $decode), "\n";
        }
    }
}
