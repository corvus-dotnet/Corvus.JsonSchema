package JSON::Schema::Corvus::XS;

use 5.026;
use strict;
use warnings;
use Carp ();
use Scalar::Util ();

our $VERSION = 'v0.1.0';

# The C library this binding is written against: 0.1.4 or later in the 0.1 series.
use constant REQUIRED_LIBRARY_VERSION => (0 << 16) | (1 << 8) | 4;

# On Windows the C library is a DLL beside the share install, which must be loaded before the XS module that links it:
# Windows then finds it by name.
if ($^O eq 'MSWin32') {
  require Alien::CorvusJsonSchema;
  if (Alien::CorvusJsonSchema->install_type eq 'share') {
    require DynaLoader;
    for my $library (Alien::CorvusJsonSchema->dynamic_libs) {
      DynaLoader::dl_load_file($library, 0)
        or die "JSON::Schema::Corvus::XS: cannot load $library: " . DynaLoader::dl_error() . "\n";
    }
  }
}
require XSLoader;
XSLoader::load(__PACKAGE__, $VERSION);

{
  my $version = library_version();
  if ($version < REQUIRED_LIBRARY_VERSION || ($version >> 8) != (REQUIRED_LIBRARY_VERSION >> 8)) {
    die sprintf("JSON::Schema::Corvus::XS %s needs version 0.1.4 or later in the 0.1 series of the corvus-json-schema "
      . "C library, and has %s\n", $VERSION, library_version_string());
  }
}

my %DIALECTS = (
  'draft4'       => 4,
  'draft6'       => 6,
  'draft7'       => 7,
  'draft2019-09' => 2019,
  'draft2020-12' => 2020,
);

my %OPTIONS = map { $_ => 1 } qw(
  default_dialect assert_format assert_format_in_legacy_drafts assert_content formats resolve_document base_uri
  entry_point max_depth pattern_match_limit
);

sub _usage {
  my ($message) = @_;
  die JSON::Schema::Corvus::XS::Error->_new($message, 1);
}

# The options as the XS reads them, checked.
sub _options {
  my (%options) = @_;
  my %native;
  for my $name (sort keys %options) {
    _usage("unknown option $name") unless $OPTIONS{$name};
  }
  if (defined $options{default_dialect}) {
    my $dialect = $DIALECTS{ $options{default_dialect} }
      // _usage("default_dialect is not one of " . join(', ', sort keys %DIALECTS));
    $native{default_dialect} = $dialect;
  }
  for my $flag (qw(assert_format assert_format_in_legacy_drafts assert_content)) {
    $native{$flag} = $options{$flag} ? 1 : 0 if defined $options{$flag};
  }
  for my $text (qw(base_uri entry_point)) {
    $native{$text} = "$options{$text}" if defined $options{$text};
  }
  if (defined $options{max_depth}) {
    _usage('max_depth is not a whole number from 1') unless $options{max_depth} =~ /\A[0-9]+\z/ && $options{max_depth} > 0;
    $native{max_depth} = 0 + $options{max_depth};
  }
  if (exists $options{pattern_match_limit}) {
    # Seconds, as the other packages have it; undef, 0 or less turns the limit off.
    my $seconds = $options{pattern_match_limit};
    if (!defined $seconds || $seconds <= 0) {
      $native{pattern_match_limit_ms} = 0;
    }
    else {
      my $ms = $seconds * 1000;
      $ms = int($ms) == $ms ? int($ms) : int($ms) + 1;
      $native{pattern_match_limit_ms} = $ms < 1 ? 1 : $ms;
    }
  }
  if (defined $options{formats}) {
    _usage('formats is not a hash of code references') unless ref $options{formats} eq 'HASH';
    for my $name (keys %{ $options{formats} }) {
      _usage("format $name is not a code reference") unless ref $options{formats}{$name} eq 'CODE';
    }
    $native{formats} = { %{ $options{formats} } };
  }
  if (defined $options{resolve_document}) {
    _usage('resolve_document is not a code reference') unless ref $options{resolve_document} eq 'CODE';
    $native{resolve_document} = $options{resolve_document};
  }
  return \%native;
}

sub _is_data {
  my ($value) = @_;
  return 1 if ref $value;
  return 1 if defined &builtin::is_bool && do { no warnings; builtin::is_bool($value) };
  return 0;
}

sub compile {
  my ($class, $schema, %options) = @_;
  my $native = _options(%options);
  return _is_data($schema) ? _compile_data($schema, $native) : _compile_text($schema, $native);
}

sub compile_uri {
  my ($class, $uri, %options) = @_;
  _usage('the URI is undef') unless defined $uri;
  return _compile_uri("$uri", _options(%options));
}

package JSON::Schema::Corvus::XS::ResultsCollector;

my %LEVELS = (basic => 0, detailed => 1, verbose => 2);

sub new {
  my ($class, $level) = @_;
  $level //= 'detailed';
  my $code = $LEVELS{ lc $level }
    // JSON::Schema::Corvus::XS::_usage("the level is not basic, detailed or verbose");
  return $class->_new($code);
}

sub annotations {
  my ($self) = @_;
  require JSON::PP;
  return JSON::PP->new->decode($self->annotations_json);
}

package JSON::Schema::Corvus::XS::Error;

use overload '""' => sub { $_[0]{message} }, 'bool' => sub { 1 }, fallback => 1;

sub _new {
  my ($class, $message, $status) = @_;
  return bless { message => $message, status => $status }, $class;
}

sub message { $_[0]{message} }
sub status { $_[0]{status} }

package JSON::Schema::Corvus::XS::Error::InvalidJson;
our @ISA = ('JSON::Schema::Corvus::XS::Error');
sub offset { $_[0]{offset} }

package JSON::Schema::Corvus::XS::Error::Compilation;
our @ISA = ('JSON::Schema::Corvus::XS::Error');

package JSON::Schema::Corvus::XS::Error::PatternTooLarge;
our @ISA = ('JSON::Schema::Corvus::XS::Error::Compilation');

package JSON::Schema::Corvus::XS::Error::Depth;
our @ISA = ('JSON::Schema::Corvus::XS::Error');

package JSON::Schema::Corvus::XS::Error::PatternMatchLimit;
our @ISA = ('JSON::Schema::Corvus::XS::Error');

1;

__END__

=encoding utf8

=head1 NAME

JSON::Schema::Corvus::XS - Fast JSON Schema validation (draft 4 to 2020-12), on the corvus-json-schema C library

=head1 SYNOPSIS

  use JSON::Schema::Corvus::XS;

  my $validator = JSON::Schema::Corvus::XS->compile(<<'JSON');
  {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "properties": { "id": { "type": "integer", "minimum": 1 } },
    "required": ["id"]
  }
  JSON

  $validator->is_valid_json('{"id": 3}');   # true
  $validator->is_valid_json('{"id": 0}');   # false
  $validator->is_valid({ id => 3 });        # Perl data: true

  my $collector = JSON::Schema::Corvus::XS::ResultsCollector->new('detailed');
  $validator->evaluate_json('{"id": "3"}', $collector);
  for my $row (@{ $collector->results }) {
    print "$row->{document_evaluation_location}: $row->{message}\n" unless $row->{is_match};
  }

=head1 DESCRIPTION

A JSON Schema validator for draft 4, 6, 7, 2019-09 and 2020-12, which passes the whole of the
JSON-Schema-Test-Suite, with results and annotations. It is a binding to the corvus-json-schema C library, which
L<Alien::CorvusJsonSchema> finds or installs. L<JSON::Schema::Corvus> is the same evaluator in pure Perl, with the
same API.

JSON text is the main input: it is parsed by the C library, so numbers, strings and booleans are exactly what the
text says. Perl data is accepted too, and is encoded to JSON text first, by the rules L<JSON::PP> follows: a scalar
that has a number and has never been used as a string is a number, and any other defined scalar is a string;
C<undef> is null; L<JSON::PP::Boolean> objects (C<JSON::PP::true>), Perl's own booleans (5.36 and later), C<\1> and
C<\0> are booleans; an object with a C<TO_JSON> method is encoded as what that returns. The names of an object are
written in order of their UTF-8 bytes, so that the order of an evaluation's results does not change from run to run.

A string of JSON text is read as UTF-8: a string with Perl's UTF8 flag on gives its characters, and one without gives
its bytes. So pass the bytes read from a file, or a decoded string, but not a string of characters from 128 to 255
that has been downgraded.

=head1 METHODS

=head2 compile

  my $validator = JSON::Schema::Corvus::XS->compile($schema, %options);

Compiles a schema, given as JSON text or as Perl data (a reference, or a boolean). The options:

=over

=item default_dialect

The dialect of a schema without C<$schema>: C<draft4>, C<draft6>, C<draft7>, C<draft2019-09> or C<draft2020-12>
(the default).

=item assert_format

True asserts C<format>, false never does; left out (or C<undef>), it follows the vocabularies (the 2020-12
format-assertion vocabulary asserts, everything else annotates).

=item assert_format_in_legacy_drafts

With C<assert_format> left out, assert C<format> in drafts 4 to 7 too.

=item assert_content

Assert C<contentEncoding> and C<contentMediaType> in draft 7, the only draft that asserts them (default true).

=item formats

Custom format assertions, by name: C<< { even => sub { length($_[0]) % 2 == 0 } } >>. Each is called with the
string (for a number, its JSON text) and returns whether it is valid. One takes precedence over the built-in format
of its name.

=item resolve_document

C<< sub ($uri) >> for documents a C<$ref> names: it returns the document as JSON text or as Perl data, or C<undef>
for a document it does not know. The standard metaschemas are built in.

=item base_uri

The base URI of the root document.

=item entry_point

A reference, relative to the root, to evaluate from, such as C<#/$defs/item>.

=item max_depth

The depth of in-place recursion on a cycle at which evaluation is abandoned (default 128).

=item pattern_match_limit

The seconds one match of a C<pattern> or C<patternProperties> regular expression against one string may take
(default 1). C<undef> or 0 turns the limit off, for a caller who trusts both the schema and the instances.

=back

An exception a custom format or the resolver throws is thrown again by the call that ran it.

=head2 compile_uri

  my $validator = JSON::Schema::Corvus::XS->compile_uri($uri, %options);

Compiles the schema at an absolute URI, fetched through C<resolve_document> (or one of the standard metaschemas).

=head2 Validators

=over

=item is_valid_json($text)

Whether the JSON text is valid. The text is parsed and evaluated in place, without creating Perl values.

=item is_valid($data)

Whether the Perl data is valid.

=item is_valid_document($document)

Whether a L</Documents> document is valid.

=item evaluate_json($text, $collector), evaluate($data, $collector), evaluate_document($document, $collector)

Evaluates every keyword, replacing the collector's results with this evaluation's, and returns whether the instance
is valid.

=back

A validator may be used any number of times. Validators, documents and collectors are not copied into new threads
(L<threads>): there they are unblessed references to C<undef>, so compile what a thread needs in that thread.

=head2 Documents

  my $document = JSON::Schema::Corvus::XS::Document->parse($text);
  my $document = JSON::Schema::Corvus::XS::Document->from_data($data);

JSON parsed once, for an instance validated more than once.

=head2 Results

  my $collector = JSON::Schema::Corvus::XS::ResultsCollector->new($level);

A results collector at the C<basic> level (failures, without messages), C<detailed> (failures, with messages, the
default) or C<verbose> (every evaluation, passing and failing, with annotations). Its methods:

=over

=item results

The rows of the last evaluation, as an array of hashes with C<is_match>, C<message>, C<evaluation_location> (the
keywords from the root schema), C<schema_evaluation_location> (the schema or keyword within its document) and
C<document_evaluation_location> (the JSON pointer of the evaluated value).

=item annotations, annotations_json

The annotations of a verbose evaluation, grouped by instance location, keyword and schema location:
C<< { "/name" => { title => { "#/properties/name" => "Name" } } } >>, as Perl data or as JSON text.

=item count, clear

The number of rows, and removing them.

=back

=head2 Other functions

C<< JSON::Schema::Corvus::XS::encode_json($data) >> returns the JSON text (UTF-8 bytes) that the data is validated
as. C<library_version> and C<library_version_string> give the C library's version.

=head1 ERRORS

Failures are thrown as objects of these classes, which stringify to their message:

=over

=item JSON::Schema::Corvus::XS::Error

The base class, with C<message> and C<status> (the C library's C<cjs_status>). Thrown itself for a bad argument or
option.

=item JSON::Schema::Corvus::XS::Error::InvalidJson

Text that is not JSON, or not UTF-8. C<offset> is the byte offset at which it was found to be invalid.

=item JSON::Schema::Corvus::XS::Error::Compilation

A schema that does not compile: a reference that does not resolve, or a regular expression that is not valid.

=item JSON::Schema::Corvus::XS::Error::PatternTooLarge

A C<Compilation> error for a regular expression over a limit of size: 1,048,576 bytes of pattern, 1,000 levels of
groups, or 1,048,576 bytes counted for it as it is read.

=item JSON::Schema::Corvus::XS::Error::Depth

Evaluation recursed in place beyond C<max_depth>.

=item JSON::Schema::Corvus::XS::Error::PatternMatchLimit

A match of a regular expression reached C<pattern_match_limit>: the instance is neither valid nor not valid.
Compiling can throw it too, since a pattern of C<patternProperties> is matched against the names the schema
declares.

=back

Perl data that has no JSON form (a code reference, an object with no C<TO_JSON>, a number that is not finite, or a
structure nested more than 512 deep) dies with a message.

=head1 SEE ALSO

L<JSON::Schema::Corvus>, L<Alien::CorvusJsonSchema>, L<https://github.com/corvus-dotnet/Corvus.JsonSchema>.

=head1 LICENSE

Apache License 2.0.

=cut
