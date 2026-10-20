use 5.026;
use strict;
use warnings;
use ExtUtils::MakeMaker;
use Alien::Build::MM;

my $abmm = Alien::Build::MM->new;

WriteMakefile($abmm->mm_args(
  NAME             => 'Alien::CorvusJsonSchema',
  DISTNAME         => 'Alien-CorvusJsonSchema',
  VERSION_FROM     => 'lib/Alien/CorvusJsonSchema.pm',
  ABSTRACT_FROM    => 'lib/Alien/CorvusJsonSchema.pm',
  AUTHOR           => 'endjin',
  LICENSE          => 'apache_2_0',
  MIN_PERL_VERSION => '5.026',
  CONFIGURE_REQUIRES => {
    'Alien::Build'        => '2.38',
    'Alien::Build::MM'    => '0.32',
    'ExtUtils::MakeMaker' => '6.64',
  },
  BUILD_REQUIRES => {
    'Alien::Build' => '2.38',
  },
  PREREQ_PM => {
    'Alien::Base' => '2.38',
  },
  TEST_REQUIRES => {
    'Test2::V0'   => '0.000060',
    'Test::Alien' => '0',
  },
  META_MERGE => {
    'meta-spec' => { version => 2 },
    resources   => {
      repository => {
        type => 'git',
        url  => 'https://github.com/corvus-dotnet/Corvus.JsonSchema.git',
        web  => 'https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-perl/Alien-CorvusJsonSchema',
      },
      bugtracker => { web => 'https://github.com/corvus-dotnet/Corvus.JsonSchema/issues' },
      homepage   => 'https://corvus-oss.org/Corvus.JsonSchema/',
    },
  },
));

sub MY::postamble {
  $abmm->mm_postamble(@_);
}

sub MY::install {
  $abmm->can('mm_install') ? $abmm->mm_install(@_) : shift->SUPER::install(@_);
}
