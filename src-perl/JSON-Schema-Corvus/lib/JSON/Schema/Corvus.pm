package JSON::Schema::Corvus;

use 5.026;
use strict;
use warnings;
use JSON::Schema::Corvus::Collecting ();
use JSON::Schema::Corvus::Compiler ();
use JSON::Schema::Corvus::Dialect ();
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Json ();
use JSON::Schema::Corvus::ResultsCollector;

our $VERSION = 'v0.1.0';

my %OPTIONS = map { $_ => 1 } qw(
  default_dialect assert_format assert_format_in_legacy_drafts assert_content formats resolve_document base_uri
  entry_point max_depth pattern_match_limit
);

sub _usage { JSON::Schema::Corvus::Error->throw_usage($_[0]) }

# The compile options, checked, with their defaults.
sub _options {
  my (%given) = @_;
  for my $name (sort keys %given) {
    _usage("unknown option $name") unless $OPTIONS{$name};
  }
  my %options = (
    default_dialect => JSON::Schema::Corvus::Dialect::DRAFT202012,
    assert_format => undef,
    assert_format_in_legacy_drafts => 0,
    assert_content => 1,
    formats => {},
    resolve_document => undef,
    base_uri => undef,
    entry_point => undef,
    max_depth => 128,
    pattern_match_limit => 1.0,
  );
  if (defined $given{default_dialect}) {
    $options{default_dialect} = JSON::Schema::Corvus::Dialect::dialect_by_name($given{default_dialect})
      // _usage('default_dialect is not one of draft2019-09, draft2020-12, draft4, draft6, draft7');
  }
  $options{assert_format} = $given{assert_format} ? 1 : 0 if defined $given{assert_format};
  for my $flag (qw(assert_format_in_legacy_drafts assert_content)) {
    $options{$flag} = $given{$flag} ? 1 : 0 if defined $given{$flag};
  }
  for my $text (qw(base_uri entry_point)) {
    $options{$text} = "$given{$text}" if defined $given{$text};
  }
  if (defined $given{max_depth}) {
    _usage('max_depth is not a whole number from 1') unless $given{max_depth} =~ /\A[0-9]+\z/ && $given{max_depth} > 0;
    $options{max_depth} = 0 + $given{max_depth};
  }
  if (exists $given{pattern_match_limit}) {
    # Seconds, as the other packages have it; undef, 0 or less turns the limit off.
    my $seconds = $given{pattern_match_limit};
    _usage('pattern_match_limit is not a number of seconds')
      if defined $seconds && !JSON::Schema::Corvus::Json::is_number($seconds) && $seconds !~ /\A[0-9.eE+-]+\z/;
    $options{pattern_match_limit} = defined $seconds && $seconds > 0 ? 0 + $seconds : undef;
  }
  if (defined $given{formats}) {
    _usage('formats is not a hash of code references') unless ref $given{formats} eq 'HASH';
    for my $name (keys %{ $given{formats} }) {
      _usage("format $name is not a code reference") unless ref $given{formats}{$name} eq 'CODE';
    }
    $options{formats} = { %{ $given{formats} } };
  }
  if (defined $given{resolve_document}) {
    _usage('resolve_document is not a code reference') unless ref $given{resolve_document} eq 'CODE';
    $options{resolve_document} = $given{resolve_document};
  }
  return \%options;
}

sub _is_data {
  my ($value) = @_;
  return 1 if ref $value;
  no warnings;
  return defined &builtin::is_bool && builtin::is_bool($value) ? 1 : 0;
}

# A schema as a JSON value: JSON text parsed (keeping the order of names), or Perl data converted.
sub _schema_value {
  my ($schema) = @_;
  return JSON::Schema::Corvus::Json::from_data($schema) if _is_data($schema);
  return JSON::Schema::Corvus::Json::decode($schema, 1);
}

sub compile {
  my ($class, $schema, %options) = @_;
  my $options = _options(%options);
  my $program = JSON::Schema::Corvus::Compiler->compile(_schema_value($schema), $options);
  return JSON::Schema::Corvus::Validator->_new($program);
}

sub compile_uri {
  my ($class, $uri, %options) = @_;
  _usage('the URI is undef') unless defined $uri;
  my $program = JSON::Schema::Corvus::Compiler->compile_from_uri("$uri", _options(%options));
  return JSON::Schema::Corvus::Validator->_new($program);
}

# The JSON text (UTF-8 bytes) that Perl data is validated as.
sub encode_json {
  my ($data) = @_;
  my $text = JSON::Schema::Corvus::Json::encode(JSON::Schema::Corvus::Json::from_data($data));
  utf8::encode($text);
  return $text;
}

package JSON::Schema::Corvus::Validator;

use Scalar::Util ();
use Symbol ();

my $PACKAGES = 0;

sub _new {
  my ($class, $program) = @_;
  my $self = bless { program => $program, collecting => undef }, $class;
  $self->_generate;
  return $self;
}

# Generates the validator's Perl and compiles it into a package of its own, which DESTROY deletes.
sub _generate {
  my ($self) = @_;
  require JSON::Schema::Corvus::CodeGen;
  require JSON::Schema::Corvus::Runtime;
  my $g = JSON::Schema::Corvus::CodeGen->new($self->{program})->generate;
  my $package = 'JSON::Schema::Corvus::Generated::G' . ++$PACKAGES;
  my $source = $self->{source} = assemble($g, $package);
  my $validate = do {
    no strict;
    no warnings;
    local $@;
    my $code = eval $source;
    die JSON::Schema::Corvus::Error::Compilation->_new("The schema produced invalid code: $@") unless $code;
    $code;
  };
  {
    no strict 'refs';
    ${"${package}::F"} = $g->{custom_formats};
  }
  $self->{package} = $package;
  $self->{validate} = $validate;
}

# The package's source: its subs, the constants (after the subs, as a dispatch table names them), and the sub that
# validates, which keeps the dynamic scope and the depth count of one evaluation when the program has them.
sub assemble {
  my ($g, $package) = @_;
  my @s = ("package $package;", 'use strict;', 'no warnings;', 'our ($F, @DS, $depth, $MAXDEPTH);');
  push @s, 'our (' . join(', ', map { $_->[0] } @{ $g->{constants} }) . ');' if @{ $g->{constants} };
  push @s, map { $_->[1] } @{ $g->{functions} };
  push @s, "$_->[0] = $_->[1];" for @{ $g->{constants} };
  push @s, "\$MAXDEPTH = $g->{max_depth};" if $g->{uses_depth};
  if ($g->{uses_dynamic_scope} || $g->{uses_depth}) {
    push @s, 'sub validate {';
    push @s, "  local \@DS = ($g->{root_resource});" if $g->{uses_dynamic_scope};
    push @s, '  local $depth = 0;' if $g->{uses_depth};
    push @s, "  return $g->{root}(\$_[0]);", '}', '\&validate;';
  }
  else {
    push @s, "\\&$g->{root};";
  }
  return join "\n", @s, '';
}

sub DESTROY {
  my ($self) = @_;
  Symbol::delete_package($self->{package}) if $self->{package};
}

# The generated Perl of the validator.
sub source { $_[0]{source} }

# The program for evaluation with a collector, with its annotations (built on first use).
sub _collecting {
  my ($self) = @_;
  return $self->{collecting} //= do {
    JSON::Schema::Corvus::Compiler::resolve_annotations($self->{program});
    my $p = $self->{program};
    {
      nodes => $p->{nodes},
      root => $p->{root},
      uses_dynamic_scope => $p->{uses_dynamic_scope},
      max_depth => $p->{options}{max_depth},
      formats => $p->{options}{formats},
      pattern_match_limit => $p->{options}{pattern_match_limit},
    };
  };
}

sub _self {
  JSON::Schema::Corvus::Error->throw_usage('expected a JSON::Schema::Corvus::Validator')
    unless Scalar::Util::blessed($_[0]) && $_[0]->isa('JSON::Schema::Corvus::Validator');
}

sub _validate {
  my ($self, $value) = @_;
  return $self->{validate}->($value) ? 1 : '';
}

sub is_valid_json {
  my ($self, $json) = @_;
  _self($self);
  return $self->_validate(JSON::Schema::Corvus::Json::decode($json));
}

sub is_valid {
  my ($self, $data) = @_;
  _self($self);
  return $self->_validate(JSON::Schema::Corvus::Json::from_data($data));
}

sub is_valid_document {
  my ($self, $document) = @_;
  _self($self);
  return $self->_validate($document->{value});
}

sub _evaluate {
  my ($self, $value, $collector) = @_;
  JSON::Schema::Corvus::Error->throw_usage('expected a JSON::Schema::Corvus::ResultsCollector')
    unless ref $collector && $collector->isa('JSON::Schema::Corvus::ResultsCollector');
  $collector->_reset;
  return JSON::Schema::Corvus::Collecting::evaluate_with_collector($self->_collecting, $value, $collector) ? 1 : '';
}

sub evaluate_json {
  my ($self, $json, $collector) = @_;
  return $self->_evaluate(JSON::Schema::Corvus::Json::decode($json, 1), $collector);
}

sub evaluate {
  my ($self, $data, $collector) = @_;
  return $self->_evaluate(JSON::Schema::Corvus::Json::from_data($data), $collector);
}

sub evaluate_document {
  my ($self, $document, $collector) = @_;
  return $self->_evaluate($document->{value}, $collector);
}

package JSON::Schema::Corvus::Document;

# JSON parsed once, for an instance validated more than once.
sub parse {
  my ($class, $json) = @_;
  return bless { value => JSON::Schema::Corvus::Json::decode($json, 1) }, $class;
}

sub from_data {
  my ($class, $data) = @_;
  return bless { value => JSON::Schema::Corvus::Json::from_data($data) }, $class;
}

1;

__END__

=encoding utf8

=head1 NAME

JSON::Schema::Corvus - Fast JSON Schema validation (draft 4 to 2020-12) in pure Perl

=head1 SYNOPSIS

  use JSON::Schema::Corvus;

  my $validator = JSON::Schema::Corvus->compile(<<'JSON');
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

  my $collector = JSON::Schema::Corvus::ResultsCollector->new('detailed');
  $validator->evaluate_json('{"id": "3"}', $collector);
  for my $row (@{ $collector->results }) {
    print "$row->{document_evaluation_location}: $row->{message}\n" unless $row->{is_match};
  }

=head1 DESCRIPTION

A JSON Schema validator for draft 4, 6, 7, 2019-09 and 2020-12, which passes the whole of the
JSON-Schema-Test-Suite, with results and annotations. It is pure Perl, with no dependency beyond Perl's core: a port of
the Corvus.Text.Json evaluator by way of its Python port. A schema is compiled into Perl, which validates with exactly
the checks the schema needs. L<JSON::Schema::Corvus::XS> is the same evaluator on the corvus-json-schema C library,
with the same API, and is faster still.

JSON text is parsed by the package's own parser, which keeps numbers exactly as the text says: an integer of any size
is an integer, and a number with a fraction or an exponent is a floating point number. A whole number is an integer in
every draft, C<1.0> included, as it is in the XS package. Parsing is in Perl, so for an instance validated more than
once, parse it once into a L</Documents> document.

Perl data is accepted too, by the rules L<JSON::PP> follows: a scalar that has a number and has never been used as a
string is a number, and any other defined scalar is a string; C<undef> is null; L<JSON::PP::Boolean> objects, Perl's
own booleans (5.36 and later), C<\1> and C<\0> are booleans; a L<Math::BigInt> is an integer, exactly; a
L<Math::BigFloat> is an integer when it is whole and otherwise the nearest floating point number; an object with a
C<TO_JSON> method is what that returns. The data is copied before it is validated.

A string of JSON text is read as UTF-8: a string with Perl's UTF8 flag on gives its characters, and one without gives
its bytes.

=head2 Regular expressions

C<pattern>, C<patternProperties> and C<format: regex> are ECMA-262 regular expressions, run with ECMA-262's meaning
(Unicode 17, whatever version of Unicode the Perl has). A pattern is read with the grammar of the C<u> flag, and one that
only that grammar refuses with the Annex B grammar of a pattern with no flag. Perl's own engine runs a pattern that is
proved to match in time proportional to the text, written out so that none of Perl's own meanings applies; every other
pattern (a backreference, a lookbehind, or anything the proof does not prove) runs on the package's own matcher, with
the limit of a match (C<pattern_match_limit>). That matcher is in Perl, so a deliberately difficult pattern can reach the
limit of one second where the XS package answers: raise the limit, or turn it off, for such patterns from a source you
trust.

=head1 METHODS

=head2 compile

  my $validator = JSON::Schema::Corvus->compile($schema, %options);

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

An exception a custom format or the resolver throws goes up through the call that ran it.

=head2 compile_uri

  my $validator = JSON::Schema::Corvus->compile_uri($uri, %options);

Compiles the schema at an absolute URI, fetched through C<resolve_document> (or one of the standard metaschemas).

=head2 Validators

=over

=item is_valid_json($text)

Whether the JSON text is valid.

=item is_valid($data)

Whether the Perl data is valid.

=item is_valid_document($document)

Whether a L</Documents> document is valid.

=item evaluate_json($text, $collector), evaluate($data, $collector), evaluate_document($document, $collector)

Evaluates every keyword, replacing the collector's results with this evaluation's, and returns whether the instance
is valid.

=item source

The Perl the schema was compiled into.

=back

A validator may be used any number of times, and in more than one thread.

=head2 Documents

  my $document = JSON::Schema::Corvus::Document->parse($text);
  my $document = JSON::Schema::Corvus::Document->from_data($data);

JSON parsed once, for an instance validated more than once.

=head2 Results

  my $collector = JSON::Schema::Corvus::ResultsCollector->new($level);

A results collector at the C<basic> level (failures, without messages), C<detailed> (failures, with messages, the
default) or C<verbose> (every evaluation, passing and failing, with annotations). Its rows are those of the other
Corvus evaluators: the same locations, messages and order. Its methods:

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

C<< JSON::Schema::Corvus::encode_json($data) >> returns the JSON text (UTF-8 bytes) of the JSON value that Perl data is
validated as.

=head1 ERRORS

Failures are thrown as objects of these classes, which stringify to their message:

=over

=item JSON::Schema::Corvus::Error

The base class, with C<message>. Thrown itself for a bad argument or option.

=item JSON::Schema::Corvus::Error::InvalidJson

Text that is not JSON, or not UTF-8. C<offset> is the byte offset at which it was found to be invalid.

=item JSON::Schema::Corvus::Error::Compilation

A schema that does not compile: a reference that does not resolve, or a regular expression that is not valid.

=item JSON::Schema::Corvus::Error::PatternTooLarge

A C<Compilation> error for a regular expression over a limit of size: 1,048,576 bytes of pattern, 1,000 levels of
groups, or 1,048,576 bytes counted for it as it is read.

=item JSON::Schema::Corvus::Error::Depth

Evaluation recursed in place beyond C<max_depth>.

=item JSON::Schema::Corvus::Error::PatternMatchLimit

A match of a regular expression reached C<pattern_match_limit>: the instance is neither valid nor not valid. It has
the C<pattern> and the C<limit>.

=back

Perl data that has no JSON form (a code reference, an object with no C<TO_JSON>, a number that is not finite, or a
structure nested more than 512 deep) dies with a message.

=head1 SEE ALSO

L<JSON::Schema::Corvus::XS>, L<https://github.com/corvus-dotnet/Corvus.JsonSchema>.

=head1 LICENSE

Apache License 2.0. The package holds tables derived from the Unicode Character Database, whose notice is in
F<THIRD-PARTY-NOTICES.txt>.

=cut
