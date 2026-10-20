package JSON::Schema::Corvus::ResultsCollector;

# Results collection: a port of the Python package's results.py (Corvus.Text.Json's JsonSchemaResultsCollector and
# JsonSchemaAnnotationProducer).
#
# The evaluator opens a context for each application of a subschema and writes keyword rows into the open context.
# Closing a context either commits it (a summary row, then its own rows newest first, after its committed descendants)
# or pops it (everything it and its descendants wrote is discarded). The level decides which rows exist and which
# carry message text: basic has failures without text, detailed adds text to failures, verbose keeps every row with
# text, annotations included.

use 5.026;
use strict;
use warnings;
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Json ();

use constant { BASIC => 0, DETAILED => 1, VERBOSE => 2 };

my %LEVELS = (basic => BASIC, detailed => DETAILED, verbose => VERBOSE);

# A collector at a level: basic, detailed (the default) or verbose.
sub new {
  my ($class, $level) = @_;
  $level //= 'detailed';
  my $code = $LEVELS{ lc $level } // JSON::Schema::Corvus::Error->throw_usage(
    'the level is not basic, detailed or verbose');
  return bless { level => $code, committed => [], frames => [], eval_path => '', schema_path => '', doc_path => '' },
    $class;
}

sub level { (qw(basic detailed verbose))[ $_[0]{level} ] }

# The rows of the last evaluation, in commit order: hashes with is_match, message, evaluation_location,
# schema_evaluation_location and document_evaluation_location.
sub results { [ @{ $_[0]{committed} } ] }

sub count { scalar @{ $_[0]{committed} } }

sub clear {
  my ($self) = @_;
  @{ $self->{committed} } = ();
  @{ $self->{frames} } = ();
  $self->{eval_path} = $self->{schema_path} = $self->{doc_path} = '';
}

# --- The evaluator's side ---

sub _reset { $_[0]->clear }

# Opens a child context: the evaluation path is extended by $eval_segment (verbatim), the schema path replaced by
# $schema_location, and the document path extended by $doc_segment (already pointer-encoded), each when given.
sub begin_child_context {
  my ($self, $eval_segment, $schema_location, $doc_segment) = @_;
  push @{ $self->{frames} }, [ $self->{eval_path}, $self->{schema_path}, $self->{doc_path},
    scalar @{ $self->{committed} }, [] ];
  $self->{eval_path} .= "/$eval_segment" if defined $eval_segment;
  $self->{schema_path} = $schema_location if defined $schema_location;
  $self->{doc_path} .= "/$doc_segment" if defined $doc_segment;
}

# Closes a child context. When the parent does not need the child's results ($parent_is_match) they are discarded below
# verbose; otherwise the context's summary row is written and its rows are committed.
sub commit_child_context {
  my ($self, $parent_is_match, $child_is_match, $message) = @_;
  if ($parent_is_match && $self->{level} != VERBOSE) {
    $self->pop_child_context;
    return;
  }
  my $frame = pop @{ $self->{frames} };
  push @{ $frame->[4] }, $self->_row($child_is_match, $message, $self->{eval_path}, $self->{schema_path},
    $self->{doc_path});
  push @{ $self->{committed} }, reverse @{ $frame->[4] };
  @$self{qw(eval_path schema_path doc_path)} = @$frame[0 .. 2];
}

# Closes a child context and discards everything it and its descendants wrote.
sub pop_child_context {
  my ($self) = @_;
  my $frame = pop @{ $self->{frames} };
  splice @{ $self->{committed} }, $frame->[3];
  @$self{qw(eval_path schema_path doc_path)} = @$frame[0 .. 2];
}


sub evaluated_keyword {
  my ($self, $is_match, $message, $keyword) = @_;
  return if $is_match && $self->{level} != VERBOSE;
  my $k = '/' . _segment($keyword);
  $self->_write($self->_row($is_match, $message, $self->{eval_path} . $k, $self->{schema_path} . $k,
    $self->{doc_path}));
}

sub evaluated_keyword_for_property {
  my ($self, $is_match, $message, $property_name, $keyword) = @_;
  return if $is_match && $self->{level} != VERBOSE;
  my $k = '/' . _segment($keyword);
  $self->_write($self->_row($is_match, $message, $self->{eval_path} . $k, $self->{schema_path} . $k,
    $self->{doc_path} . '/' . _segment($property_name)));
}

# An annotation: verbose only; the keyword extends the evaluation path but not the schema path.
sub ignored_keyword {
  my ($self, $message, $keyword) = @_;
  return if $self->{level} != VERBOSE;
  $self->_write($self->_row(1, $message, $self->{eval_path} . '/' . _segment($keyword), $self->{schema_path},
    $self->{doc_path}));
}

sub evaluated_boolean_schema {
  my ($self, $is_match, $message) = @_;
  return if $is_match && $self->{level} != VERBOSE;
  $self->_write($self->_row($is_match, $message, $self->{eval_path}, $self->{schema_path}, $self->{doc_path}));
}

sub _segment {
  my ($s) = @_;
  return $s if index($s, '~') < 0 && index($s, '/') < 0;
  $s =~ s/~/~0/g;
  $s =~ s/\//~1/g;
  return $s;
}

sub _row {
  my ($self, $is_match, $message, $evaluation, $schema, $document) = @_;
  my $with_text = $self->{level} == VERBOSE || (!$is_match && $self->{level} >= DETAILED);
  my $text = $with_text && defined $message ? (ref $message eq 'CODE' ? $message->() : $message) : '';
  return { is_match => $is_match ? 1 : '', message => $text, evaluation_location => $evaluation,
    schema_evaluation_location => $schema, document_evaluation_location => $document };
}

sub _write { push @{ $_[0]{frames}[-1][4] }, $_[1] }

# --- Annotations ---

my %JSON_VALUE_START = map { $_ => 1 } split //, '"{[tfn-0123456789';

# The annotations in a verbose collector's results (JsonSchemaAnnotationProducer.EnumerateAnnotations):
# [instance location, keyword, schema location, JSON text].
sub enumerate_annotations {
  my ($self) = @_;
  my @out;
  for my $r (@{ $self->{committed} }) {
    next if !$r->{is_match} || $r->{message} eq '';
    my $slash = rindex($r->{evaluation_location}, '/');
    next if $slash < 0 || $r->{evaluation_location} eq $r->{schema_evaluation_location};
    my $keyword = substr($r->{evaluation_location}, $slash + 1);
    next if $keyword eq '' || !$JSON_VALUE_START{ substr($r->{message}, 0, 1) };
    push @out, [ $r->{document_evaluation_location}, $keyword, $r->{schema_evaluation_location}, $r->{message} ];
  }
  return @out;
}

# '#' and the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8).
sub schema_location_fragment {
  my ($location) = @_;
  my $bytes = $location;
  utf8::encode($bytes);
  $bytes =~ s/([^A-Za-z0-9\-._~!\$&'()*+,;=:\@\/?])/sprintf('%%%02X', ord $1)/ge;
  return "#$bytes";
}

# The annotations grouped by instance location, then keyword, then schema location fragment, as Perl data:
# { "/name" => { title => { "#/properties/name" => "Name" } } }.
sub annotations {
  my ($self) = @_;
  require JSON::PP;
  my $json = JSON::PP->new->allow_nonref;
  my %out;
  for my $a ($self->enumerate_annotations) {
    $out{ $a->[0] }{ $a->[1] }{ schema_location_fragment($a->[2]) } = $json->decode($a->[3]);
  }
  return \%out;
}

# The same as JSON text, with names in the order the results give them, as the C library writes it.
sub annotations_json {
  my ($self) = @_;
  my (%out, @locations, %keywords, %schemas);
  for my $a ($self->enumerate_annotations) {
    my ($location, $keyword, $schema, $value) = @$a;
    push @locations, $location unless exists $out{$location};
    push @{ $keywords{$location} }, $keyword unless exists $out{$location}{$keyword};
    my $fragment = schema_location_fragment($schema);
    push @{ $schemas{$location}{$keyword} }, $fragment unless exists $out{$location}{$keyword}{$fragment};
    $out{$location}{$keyword}{$fragment} = $value;
  }
  my $q = \&JSON::Schema::Corvus::Json::_quote;
  return '{' . join(',', map {
    my $l = $_;
    $q->($l) . ':{' . join(',', map {
      my $k = $_;
      $q->($k) . ':{' . join(',', map { $q->($_) . ':' . $out{$l}{$k}{$_} } @{ $schemas{$l}{$k} }) . '}'
    } @{ $keywords{$l} }) . '}'
  } @locations) . '}';
}

1;
