#!/usr/bin/perl
# Which template hangs off which, so a class can be turned into a path.
#   emits: OwnerClass \t serializedField \t SubTemplateClass
use strict; use warnings;
for my $file (@ARGV) {
    open my $fh,'<:encoding(UTF-8)',$file or next; local $/; my $s=<$fh>; close $fh;
    my ($class) = $s =~ /public sealed class (\w+)/;
    next unless $class;
    while ($s =~ /\[SerializeField\]\s+private\s+(Dimension\w+Template)\s+(\w+)\s*[=;]/g) {
        print join("\t", $class, $2, $1), "\n";
    }
}
