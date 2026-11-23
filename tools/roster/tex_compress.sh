#!/usr/bin/env bash
# VRAM compression for texture .import files: BC7 (high quality) for colour / ORM / emission / masks, BC5 (RGTC) for
# normal maps (*_n.* / *_nrm.* / *_normal.*). Backs up every .import it changes to Y:/tools/roster/tex_backup/<set>/
# before the first change (flat, by file name; restore = copy back + Y:/tools/import.ps1). One perl process (fast).
#   bash tex_compress.sh <set name> <import file> [...]
set=$1; shift
B=Y:/tools/roster/tex_backup/$set; mkdir -p "$B"
perl -e '
  my $B = shift; my ($n, $skip) = (0, 0);
  for my $f (@ARGV) {
    open(my $h, "<", $f) or next; local $/; my $s = <$h>; close $h;
    if ($s !~ /^importer="texture"/m) { $skip++; next; }
    (my $base = $f) =~ s{.*/}{};
    if (!-e "$B/$base") { open(my $o, ">", "$B/$base"); print $o $s; close $o; }
    $s =~ s/^compress\/mode=[01]$/compress\/mode=2/m;
    $s =~ s/"vram_texture": false/"vram_texture": true/;
    if ($f =~ /_(n|nrm|normal)\.(jpg|png)\.import$/) { $s =~ s/^compress\/normal_map=0$/compress\/normal_map=1/m; $s =~ s/^compress\/high_quality=true$/compress\/high_quality=false/m; }
    else { $s =~ s/^compress\/high_quality=false$/compress\/high_quality=true/m; }
    open(my $o, ">", $f) or die; print $o $s; close $o; $n++;
  }
  print "tex_compress: $n texture imports set to VRAM compression ($skip skipped, not textures)\n";
' "$B" "$@" | sed "s/^/$set /"
