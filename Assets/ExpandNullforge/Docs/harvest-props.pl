use strict; use warnings;
for my $file (@ARGV) {
    open my $fh,'<:encoding(UTF-8)',$file or next; local $/; my $s=<$fh>; close $fh;
    my ($class) = $s =~ /public sealed class (\w+)/;
    next unless $class;
    my %field_type;
    while ($s =~ /\[SerializeField\]\s+private\s+([\w.<>\[\]]+)\s+(\w+)\s*[=;]/g) { $field_type{$2} = $1 }
    # property header, then its getter body up to the matching close
    while ($s =~ /public\s+([\w.<>\[\]]+)\s+([A-Z]\w*)\s*(\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\})/g) {
        my ($type,$prop,$body) = ($1,$2,$3);
        next if $body =~ /\bset\b/;
        my @hits = grep { exists $field_type{$_} } ($body =~ /\b([a-z]\w*)\b/g);
        next unless @hits;
        my %seen; my @uniq = grep { !$seen{$_}++ } @hits;
        next if @uniq != 1;
        print join("\t", $class, $prop, $uniq[0], $field_type{$uniq[0]}), "\n";
    }
}
