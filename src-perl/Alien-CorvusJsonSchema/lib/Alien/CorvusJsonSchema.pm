package Alien::CorvusJsonSchema;

use 5.026;
use strict;
use warnings;
use parent 'Alien::Base';

our $VERSION = 'v0.1.0';

1;

__END__

=head1 NAME

Alien::CorvusJsonSchema - The corvus-json-schema C library, for JSON Schema validation

=head1 SYNOPSIS

In a F<Makefile.PL>:

  use Alien::Base::Wrapper qw(Alien::CorvusJsonSchema !export);

  WriteMakefile(
    Alien::Base::Wrapper->mm_args,
    CONFIGURE_REQUIRES => { 'Alien::CorvusJsonSchema' => 'v0.1.0' },
    ...
  );

and in the XS:

  #include "corvus_json_schema.h"

=head1 DESCRIPTION

This distribution finds or installs the corvus-json-schema C library, a JSON Schema validator (draft 4, 6, 7,
2019-09 and 2020-12) built from the Rust crate of the same name, for
L<JSON::Schema::Corvus::XS>. See L<https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rs/corvus-json-schema-capi>
for the library and its API.

An installed C library, version 0.1.4 or later, that C<pkg-config> finds as C<corvus-json-schema> is used as it is.
Otherwise the C library's release package for the platform (Linux on x86_64 and aarch64 with glibc or musl, macOS,
and Windows on x64 and arm64 with Strawberry Perl) is downloaded from its GitHub release, checked against the SHA-256
digest this distribution records, and installed with it. There the static library is linked, except on Windows,
where the library is a DLL that C<dynamic_libs> names and that must be loaded before the XS module that links it.

Set C<ALIEN_INSTALL_TYPE=share> to install the release package even when the library is installed, and
C<ALIEN_INSTALL_TYPE=system> to require the installed one.

=head1 METHODS

The methods of L<Alien::Base>: C<cflags>, C<libs>, C<dynamic_libs>, C<install_type> and C<version>. C<version> is the
C library's version.

=head1 LICENSE

Apache License 2.0. The C library's package holds the notices of the third-party material it is built from, in
F<THIRD-PARTY-NOTICES.txt>.

=cut
