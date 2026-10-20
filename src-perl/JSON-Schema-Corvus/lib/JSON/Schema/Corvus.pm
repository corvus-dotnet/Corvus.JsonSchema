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
