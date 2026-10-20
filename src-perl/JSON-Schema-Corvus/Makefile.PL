use 5.026;
use strict;
use warnings;
use ExtUtils::MakeMaker;

WriteMakefile(
  NAME             => 'JSON::Schema::Corvus',
  DISTNAME         => 'JSON-Schema-Corvus',
  VERSION_FROM     => 'lib/JSON/Schema/Corvus.pm',
  ABSTRACT_FROM    => 'lib/JSON/Schema/Corvus.pm',
  AUTHOR           => 'endjin',
  LICENSE          => 'apache_2_0',
  MIN_PERL_VERSION => '5.026',
  CONFIGURE_REQUIRES => {
    'ExtUtils::MakeMaker' => '6.64',
  },
  # Modules of Perl's core only.
  PREREQ_PM => {
    'B'                     => '0',
    'Encode'                => '0',
    'Hash::Util::FieldHash' => '0',
    'JSON::PP'              => '2.27',
    'List::Util'            => '1.33',
    'Math::BigInt'          => '1.999',
    'MIME::Base64'          => '0',
    'POSIX'                 => '0',
    'Scalar::Util'          => '1.18',
    'Symbol'                => '0',
    'Time::HiRes'           => '0',
  },
  TEST_REQUIRES => {
    'Test::More' => '0.98',
  },
  META_MERGE => {
    'meta-spec' => { version => 2 },
    resources   => {
      repository => {
        type => 'git',
        url  => 'https://github.com/corvus-dotnet/Corvus.JsonSchema.git',
        web  => 'https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-perl/JSON-Schema-Corvus',
      },
      bugtracker => { web => 'https://github.com/corvus-dotnet/Corvus.JsonSchema/issues' },
      homepage   => 'https://corvus-oss.org/Corvus.JsonSchema/',
    },
  },
);
