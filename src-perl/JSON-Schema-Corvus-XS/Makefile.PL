use 5.026;
use strict;
use warnings;
use ExtUtils::MakeMaker;
use Alien::Base::Wrapper qw(Alien::CorvusJsonSchema !export);

WriteMakefile(
  Alien::Base::Wrapper->mm_args,
  NAME             => 'JSON::Schema::Corvus::XS',
  DISTNAME         => 'JSON-Schema-Corvus-XS',
  VERSION_FROM     => 'lib/JSON/Schema/Corvus/XS.pm',
  ABSTRACT_FROM    => 'lib/JSON/Schema/Corvus/XS.pm',
  AUTHOR           => 'endjin',
  LICENSE          => 'apache_2_0',
  MIN_PERL_VERSION => '5.026',
  CONFIGURE_REQUIRES => {
    'Alien::Base::Wrapper'    => '2.38',
    'Alien::CorvusJsonSchema' => 'v0.1.0',
    'ExtUtils::MakeMaker'     => '6.64',
  },
  PREREQ_PM => {
    'Alien::CorvusJsonSchema' => 'v0.1.0',
    'JSON::PP'                => '2.27',
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
        web  => 'https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-perl/JSON-Schema-Corvus-XS',
      },
      bugtracker => { web => 'https://github.com/corvus-dotnet/Corvus.JsonSchema/issues' },
      homepage   => 'https://corvus-oss.org/Corvus.JsonSchema/',
    },
  },
);
