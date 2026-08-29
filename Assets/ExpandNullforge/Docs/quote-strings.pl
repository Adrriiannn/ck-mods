while (<>) {
    chomp;
    next if $_ eq '';
    s/\\/\\\\/g;
    s/"/\\"/g;
    print "            \"$_\",\n";
}
