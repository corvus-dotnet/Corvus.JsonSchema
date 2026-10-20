#!/usr/bin/env perl
# Builds and tests a Perl distribution of src-perl, as CI does (perl.yml) and as it can be run locally. In Perl, so
# that it runs with the Perl it is given on every platform (a shell may find another Perl first, as Git's bash on
# Windows does).
#   perl src-perl/ci.pl test <directory>          perl Makefile.PL, make, make test, in the source tree
#   perl src-perl/ci.pl dist-install <directory>  checks the MANIFEST, makes the tarball CPAN would get, and installs
#                                                 it with cpanm (which runs its tests)
#   perl src-perl/ci.pl smoke                     uses the installed JSON::Schema::Corvus::XS and JSON::Schema::Corvus
use strict;
use warnings;
use Config;
use File::Glob ':bsd_glob';

my ($command, $directory) = @ARGV;
my $make = $ENV{MAKE} || $Config{make};

sub run {
  print "> @_\n";
  system(@_) == 0 or die "failed ($?): @_\n";
}

if ($command eq 'smoke') {
  require JSON::Schema::Corvus::XS;
  require JSON::Schema::Corvus;
  for my $class (qw(JSON::Schema::Corvus::XS JSON::Schema::Corvus)) {
    my $v = $class->compile('{"type": "integer"}');
    die "$class: wrong answer\n" unless $v->is_valid_json('3') && !$v->is_valid_json('"3"');
  }
  print "JSON::Schema::Corvus::XS $JSON::Schema::Corvus::XS::VERSION on C library ",
    JSON::Schema::Corvus::XS::library_version_string(), ", JSON::Schema::Corvus $JSON::Schema::Corvus::VERSION\n";
  exit 0;
}

chdir $directory or die "cannot enter $directory: $!\n";
if ($command eq 'test') {
  run($^X, 'Makefile.PL');
  run($make);
  run($make, 'test');
}
elsif ($command eq 'dist-install') {
  run($^X, 'Makefile.PL');
  # The files on disk and the MANIFEST must agree (build output is in MANIFEST.SKIP).
  require ExtUtils::Manifest;
  { no warnings "once"; $ExtUtils::Manifest::Quiet = 1; }
  my ($missing, $extra) = ExtUtils::Manifest::fullcheck();
  die "missing from the tree: @$missing\n" if @$missing;
  die "not in the MANIFEST: @$extra\n" if @$extra;
  unlink bsd_glob('*.tar.gz');
  run($make, 'dist');
  my ($tarball) = bsd_glob('*.tar.gz') or die "make dist made no tarball\n";
  run('cpanm', '--verbose', "./$tarball");
}
else {
  die "usage: ci.pl test|dist-install <directory> | smoke\n";
}
