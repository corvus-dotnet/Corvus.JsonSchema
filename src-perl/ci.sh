#!/usr/bin/env bash
# Builds and tests a Perl distribution of src-perl, as CI does (perl.yml) and as it can be run locally.
#   ci.sh test <directory>           perl Makefile.PL, make, make test, in the source tree
#   ci.sh dist-install <directory>   checks the MANIFEST, makes the tarball CPAN would get, and installs it with cpanm
#                                    (which runs its tests)
# MAKE is the make Perl was built with ($Config{make}): gmake with Strawberry Perl.
set -euo pipefail
command=$1
directory=$2
make=${MAKE:-$(perl -MConfig -e 'print $Config{make}')}
cd "$directory"

case "$command" in
  test)
    perl Makefile.PL
    "$make"
    "$make" test
    ;;
  dist-install)
    perl Makefile.PL
    # The files on disk and the MANIFEST must agree (build output is in MANIFEST.SKIP).
    perl -MExtUtils::Manifest=fullcheck -e '
      $ExtUtils::Manifest::Quiet = 1;
      my ($missing, $extra) = fullcheck();
      print STDERR "missing from the tree: @$missing\n" if @$missing;
      print STDERR "not in the MANIFEST: @$extra\n" if @$extra;
      exit(@$missing || @$extra ? 1 : 0);'
    rm -f ./*.tar.gz
    "$make" dist
    tarball=$(ls ./*.tar.gz)
    echo "Installing $tarball"
    cpanm --verbose "$tarball"
    ;;
  *)
    echo "usage: ci.sh test|dist-install <directory>" >&2
    exit 2
    ;;
esac
