package JSON::Schema::Corvus::Collecting;

# Collecting-mode evaluation: evaluates an instance exhaustively against the compiled node graph and reports every
# keyword to a results collector, reproducing the C# evaluator's Eval<CollectingMode> (keyword order, evaluation,
# schema and instance paths, messages, which subschema results are committed or discarded). A port of the Python
# package's collecting.py.
#
# Validation without a collector never comes here: it runs the generated code. Results collection is exhaustive by
# nature (every keyword is evaluated and reported), so it interprets the graph rather than generating a second program.

use 5.026;
use strict;
use warnings;
use B ();
use JSON::Schema::Corvus::Dialect qw(DRAFT6);
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Formats ();
use JSON::Schema::Corvus::Json ();
use JSON::Schema::Corvus::Node qw(:all);
use JSON::Schema::Corvus::Pattern ();
use JSON::Schema::Corvus::Runtime ();

# Messages (Corvus.Text.Json Strings.resx).
use constant {
  EVALUATED_SUBSCHEMA => 'The value was expected to match the subschema.',
  MATCHED_ALL => 'The value matched all subschema.',
  DID_NOT_MATCH_ALL => 'The value did not match all subschema.',
  MATCHED_AT_LEAST_ONE => 'The value matched at least one subschema.',
  DID_NOT_MATCH_AT_LEAST_ONE => 'The value did not match at least one subschema.',
  MATCHED_NO_SCHEMA => 'The instance matched no schema.',
  MATCHED_EXACTLY_ONE => 'The value matched exactly one subschema.',
  MATCHED_MORE_THAN_ONE => 'The instance matched more than one schema.',
  MATCHED_NOT => 'The value matched the subschema in a not composition, which means the evaluation was not a match.',
  DID_NOT_MATCH_NOT =>
    'The value did not match the subschema in a not composition, which means the evaluation was a match.',
  MATCHED_IF_FOR_THEN => 'The value matched the subschema in a binary or ternay if, which means the evaluation will go '
    . 'on to match the then subschema.',
  MATCHED_IF_FOR_ELSE => 'The value did not match the subschema in a ternary if, which means the evaluation will go on '
    . 'to match the else subschema.',
  MATCHED_THEN => 'The value matched the then subschema corresponding to a binary or ternary if.',
  DID_NOT_MATCH_THEN => 'The value did not match the then subschema corresponding to a binary or ternary if.',
  MATCHED_ELSE => 'The value matched the else subschema corresponding to a ternary if.',
  DID_NOT_MATCH_ELSE => 'The value did not match the else subschema corresponding to a ternary if.',
  UNIQUE_ITEMS => 'The array was expected to contain unique items.',
  PROPERTY_NAME_FAILED => 'The property name did not match the schema.',
};

our %STRING_FORMAT_MESSAGES = (
  'date' => 'Expected an ISO8601 Date string.',
  'date-time' => 'Expected an ISO8601 Offset DateTime string.',
  'time' => 'Expected an ISO8601 Offset Time string.',
  'duration' => 'Expected an ISO8601 Duration string.',
  'email' => 'Expected an RFC5321 Section-4.1.2 Email string.',
  'idn-email' => 'Expected an RFC6531 IDN Email string.',
  'hostname' => 'Expected an RFC1035 hostname.',
  'idn-hostname' => 'Expected an RFC5890 Section-2.3.2.3 IDN hostname.',
  'ipv4' => 'Expected an RFC2673 IP V4 address.',
  'ipv6' => 'Expected an RFC2373 IP V6 address.',
  'uri' => 'Expected an absolute URI.',
  'uri-reference' => 'Expected a URI reference.',
  'iri' => 'Expected an absolute IRI.',
  'iri-reference' => 'Expected an IRI reference.',
  'uuid' => 'Expected an RFC4122 UUID.',
  'uri-template' => 'Expected an RFC6570 URI Template.',
  'json-pointer' => 'Expected an RFC6901 JSON Pointer.',
  'relative-json-pointer' =>
    'Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer).',
  'regex' => 'Expected a regular expression specification.',
);

my $Json = 'JSON::Schema::Corvus::Json';
*_is_number = \&JSON::Schema::Corvus::Json::is_number;
*_is_string = \&JSON::Schema::Corvus::Json::is_string;
*_equal = \&JSON::Schema::Corvus::Runtime::equal;

# " 'v'", or nothing for an empty value (JsonSchemaEvaluation.AppendSingleQuotedValue).
sub _q { length $_[0] ? " '$_[0]'" : '' }

# A number as the Rust evaluator writes it (serde_json): an integer exactly, and a floating point number in its
# shortest form that reads back, with .0 kept on whole values and no leading zeros in the exponent (2.0, 1e+21,
# 1.5e-7).
sub number_text {
  my ($n) = @_;
  return "$n" if ref $n;
  my $copy = $n;
  return "$copy" if JSON::Schema::Corvus::Json::is_integer($n) && B::svref_2object(\$n)->FLAGS & B::SVf_IOK;
  return 'NaN' if $n != $n;
  return $n > 0 ? 'Infinity' : '-Infinity' if $n == $n * 2 && $n != 0;
  my $text = JSON::Schema::Corvus::Json::number_text($n);
  if ($text =~ /\A(-?[0-9.]+)e([+-]?)0*([0-9]+)\z/) {
    (my $mantissa = $1) =~ s/\.0\z//;
    return "${mantissa}e" . ($2 eq '-' ? '-' : '+') . $3;
  }
  $text .= '.0' unless $text =~ /[.e]/;
  return $text;
}

my @TYPE_ORDER = ([T_ARRAY, 'array'], [T_OBJECT, 'object'], [T_NULL, 'null'], [T_BOOLEAN, 'boolean'],
  [T_NUMBER, 'number'], [T_INTEGER, 'integer'], [T_STRING, 'string']);

sub _type_message {
  my ($mask) = @_;
  my @names = map { $mask & $_->[0] ? $_->[1] : () } @TYPE_ORDER;
  return undef unless @names;
  return "The value was expected to be of type '$names[0]'" if @names == 1;
  return q{The value was expected to be of type '[} . join(', ', map { qq{"$_"} } @names) . q{]'};
}

sub _matches_type {
  my ($mask, $x, $dialect) = @_;
  my $type = JSON::Schema::Corvus::Json::type_of($x);
  return ($mask & T_STRING) ? 1 : 0 if $type eq 'string';
  if ($type eq 'number') {
    return 1 if $mask & T_NUMBER;
    return 0 unless $mask & T_INTEGER;
    return JSON::Schema::Corvus::Json::is_integer($x) ? 1 : 0;
  }
  return ($mask & T_BOOLEAN) ? 1 : 0 if $type eq 'boolean';
  return ($mask & T_NULL) ? 1 : 0 if $type eq 'null';
  return ($mask & T_ARRAY) ? 1 : 0 if $type eq 'array';
  return ($mask & T_OBJECT) ? 1 : 0;
}

sub _const_message {
  my ($value) = @_;
  my $type = JSON::Schema::Corvus::Json::type_of($value);
  return 'Expected the value to be the string' . _q($value) if $type eq 'string';
  return "Expected the value to be '" . ($value ? 'true' : 'false') . "'" if $type eq 'boolean';
  return 'The value was expected to be equal to' . _q(number_text($value)) if $type eq 'number';
  return "Expected the value to be 'null'" if $type eq 'null';
  return undef;
}

sub _json_text { JSON::Schema::Corvus::Json::encode($_[0]) }

sub _segment { JSON::Schema::Corvus::Uri::escape_pointer_token($_[0]) }

# Evaluates an instance (a JSON value, in ordered mode for the order of results) against the program's entry,
# reporting to the collector; returns whether it is valid.
sub evaluate_with_collector {
  my ($program, $instance, $collector) = @_;
  my $self = bless { program => $program, c => $collector, nodes => $program->{nodes}, scope => [], depth => 0 },
    __PACKAGE__;
  # A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path.
  my ($root) = $self->_resolve($program->{root});
  $collector->begin_child_context(undef, $self->{nodes}[$root]{pointer}, undef);
  my $ok = $self->_eval_node($root, $instance, undef);
  $collector->commit_child_context(0, $ok, EVALUATED_SUBSCHEMA);
  return $ok;
}

# The single reference of a pure-$ref node (SchemaCompiler.IsPureRef), or -1.
sub _pure_ref_target {
  my ($self, $n) = @_;
  my $refs = ($n->{ref} >= 0 ? 1 : 0) + ($n->{static_dynamic_ref} >= 0 ? 1 : 0);
  return -1 if $refs != 1 || $n->{always_true} || $n->{always_false} || defined $n->{annotations};
  return -1 if $n->{has_type} || $n->{has_const} || defined $n->{enum_values} || $n->has_number_keywords
    || $n->has_string_keywords || $n->has_object_keywords || $n->has_array_keywords || defined $n->{dynamic_ref}
    || defined $n->{all_of} || defined $n->{any_of} || defined $n->{one_of} || $n->{not_} >= 0 || $n->{if_} >= 0;
  return $n->{ref} >= 0 ? $n->{ref} : $n->{static_dynamic_ref};
}

# Follows pure-reference hops (at most 16; not across resources when a dynamic scope is kept). The suffix extends the
# evaluation path by the keyword of each hop: /$ref, or /$dynamicRef or /$recursiveRef for a statically resolved
# dynamic reference.
sub _resolve {
  my ($self, $node_id) = @_;
  my $current = $node_id;
  my $suffix = '';
  for (1 .. 16) {
    my $n = $self->{nodes}[$current];
    my $next = $self->_pure_ref_target($n);
    last if $next < 0;
    last if $self->{program}{uses_dynamic_scope} && $self->{nodes}[$next]{resource_id} != $n->{resource_id};
    $suffix .= $n->{ref} >= 0 ? '/$ref' : '/' . ($n->{static_dynamic_keyword} // '');
    $current = $next;
  }
  return ($current, $suffix);
}

sub _eval_node {
  my ($self, $node_id, $x, $bits) = @_;
  my $n = $self->{nodes}[$node_id];
  if ($n->{always_true} || $n->{always_false}) {
    $self->{c}->evaluated_boolean_schema($n->{always_true}, undef);
    return $n->{always_true} ? 1 : 0;
  }
  my $pushed = 0;
  my $scope = $self->{scope};
  if ($self->{program}{uses_dynamic_scope} && (!@$scope || $scope->[-1] != $n->{resource_id})) {
    push @$scope, $n->{resource_id};
    $pushed = 1;
  }
  if (!$bits && (($n->{unevaluated_properties} >= 0 && ref $x eq 'HASH')
    || ($n->{unevaluated_items} >= 0 && ref $x eq 'ARRAY'))) {
    $bits = {};
  }
  my $ok = eval { $self->_eval_core($n, $x, $bits) };
  my $error = $@;
  pop @$scope if $pushed;
  die $error unless defined $ok;
  return $ok;
}

sub _eval_core {
  my ($self, $n, $x, $bits) = @_;
  my $c = $self->{c};
  my $ok = 1;
  if ($n->{has_type}) {
    my $m = _matches_type($n->{type}, $x, $n->{dialect});
    $c->evaluated_keyword($m, _type_message($n->{type}), 'type');
    $ok &&= $m;
  }
  if ($n->{has_const}) {
    my $m = _equal($x, $n->{const_value}) ? 1 : 0;
    $c->evaluated_keyword($m, _const_message($n->{const_value}), 'const');
    $ok &&= $m;
  }
  if (defined $n->{enum_values}) {
    my $m = JSON::Schema::Corvus::Runtime::includes($n->{enum_values}, $x) ? 1 : 0;
    $c->evaluated_keyword($m, $m ? MATCHED_AT_LEAST_ONE : DID_NOT_MATCH_AT_LEAST_ONE, 'enum');
    $ok &&= $m;
  }
  my $type = JSON::Schema::Corvus::Json::type_of($x);
  if ($type eq 'number') {
    $ok = $self->_eval_number($n, $x) && $ok if $n->has_number_keywords;
  }
  elsif ($type eq 'string') {
    $ok = $self->_eval_string($n, $x) && $ok if $n->has_string_keywords;
  }
  elsif ($type eq 'object') {
    $ok = $self->_eval_object($n, $x, $bits) && $ok if $n->has_object_keywords;
  }
  elsif ($type eq 'array') {
    $ok = $self->_eval_array($n, $x, $bits) && $ok if $n->has_array_keywords;
  }
  $ok = $self->_eval_in_place($n, $x, $bits) && $ok;
  if ($type eq 'object' && $n->{unevaluated_properties} >= 0) {
    $ok = $self->_eval_unevaluated_properties($n, $x, $bits) && $ok;
  }
  elsif ($type eq 'array' && $n->{unevaluated_items} >= 0) {
    $ok = $self->_eval_unevaluated_items($n, $x, $bits) && $ok;
  }
  if ($n->{annotations}) {
    for my $a (@{ $n->{annotations} }) {
      next if $a->{strings_only} && $type ne 'string';
      my $value = $a->{value};
      $c->ignored_keyword(sub { _json_text($value) }, $a->{keyword});
    }
  }
  return $ok ? 1 : 0;
}

# --- Numbers and strings ---

sub _eval_number {
  my ($self, $n, $x) = @_;
  my $c = $self->{c};
  my $ok = 1;
  if ($n->{assert_format} && defined $n->{format} && JSON::Schema::Corvus::Formats::is_numeric_format($n->{format_kind})) {
    my $custom = ($self->{program}{formats} // {})->{ $n->{format} };
    my $m = ($custom ? $custom->(number_text($x))
      : $JSON::Schema::Corvus::Formats::NUMERIC_FORMAT_VALIDATORS{ $n->{format_kind} }->($x)) ? 1 : 0;
    $c->evaluated_keyword($m,
      "The value was expected to be in a supported format, and within bounds for '$n->{format_kind}'", 'format');
    $ok &&= $m;
  }
  my $bound = sub {
    my ($value, $test, $text, $keyword) = @_;
    return unless defined $value;
    my $m = $test->($value) ? 1 : 0;
    $c->evaluated_keyword($m, sub { $text . _q(number_text($value)) }, $keyword);
    $ok &&= $m;
  };
  $bound->($n->{minimum}, sub { $x >= $_[0] }, 'The value was expected to be greater than or equal to', 'minimum');
  $bound->($n->{maximum}, sub { $x <= $_[0] }, 'The value was expected to be less than or equal to', 'maximum');
  $bound->($n->{exclusive_minimum}, sub { $x > $_[0] }, 'The value was expected to be greater than',
    'exclusiveMinimum');
  $bound->($n->{exclusive_maximum}, sub { $x < $_[0] }, 'The value was expected to be less than', 'exclusiveMaximum');
  $bound->($n->{multiple_of}, sub { JSON::Schema::Corvus::Runtime::multiple_of($x, $_[0]) },
    'The value was expected to be a multiple of', 'multipleOf');
  return $ok;
}

sub _search {
  my ($self, $pattern, $s) = @_;
  my $m = JSON::Schema::Corvus::Pattern::compile_pattern($pattern, $self->{program}{pattern_match_limit});
  return $m && $m->($s) ? 1 : 0;
}

sub _eval_string {
  my ($self, $n, $x) = @_;
  my $c = $self->{c};
  my $ok = 1;
  my $length;
  if ($n->{min_length} >= 0 || $n->{max_length} >= 0) {
    # The length in code points (a JSON string is UTF-16, but its length here is in characters, as the other ports
    # count it).
    $length = length $x;
  }
  if ($n->{min_length} >= 0) {
    my $m = $length >= $n->{min_length} ? 1 : 0;
    $c->evaluated_keyword($m, "Expected the length of the value to be greater than or equal to '$n->{min_length}'",
      'minLength');
    $ok &&= $m;
  }
  if ($n->{max_length} >= 0) {
    my $m = $length <= $n->{max_length} ? 1 : 0;
    $c->evaluated_keyword($m, "Expected the length of the value to be less than or equal to '$n->{max_length}'",
      'maxLength');
    $ok &&= $m;
  }
  if (defined $n->{pattern}) {
    my $pattern = $n->{pattern};
    my $m = $self->_search($pattern, $x);
    $c->evaluated_keyword($m, sub { 'Expected the value to match the regular expression' . _q($pattern) }, 'pattern');
    $ok &&= $m;
  }
  if ($n->{assert_format} && defined $n->{format} && !JSON::Schema::Corvus::Formats::is_numeric_format($n->{format_kind})) {
    my $custom = ($self->{program}{formats} // {})->{ $n->{format} };
    my ($m, $message);
    if ($custom) {
      $m = $custom->($x) ? 1 : 0;
      $message = "Expected a string in the '$n->{format}' format.";
    }
    elsif ($n->{format_kind} eq 'unknown') {
      $m = 1;
    }
    else {
      my $validate = $n->{format_kind} eq 'hostname' && $n->{dialect} <= DRAFT6
        ? \&JSON::Schema::Corvus::Formats::legacy_hostname
        : $JSON::Schema::Corvus::Formats::FORMAT_VALIDATORS{ $n->{format_kind} };
      $m = $validate->($x) ? 1 : 0;
      $message = $STRING_FORMAT_MESSAGES{ $n->{format_kind} };
    }
    $c->evaluated_keyword($m, $message, 'format');
    $ok &&= $m;
  }
  if ($n->{assert_content}) {
    my $kind = $n->{content} == CONTENT_BASE64 ? 1 : $n->{content} == CONTENT_JSON ? 2 : 3;
    my $m = JSON::Schema::Corvus::Runtime::content($x, $kind) ? 1 : 0;
    my $text = $kind == 1 ? 'Expected a valid Base64-encoded string.' : $kind == 2 ? 'Expected valid JSON content.'
      : 'Expected valid Base64-encoded JSON content.';
    $c->evaluated_keyword($m, $text, $kind == 1 ? 'contentEncoding' : 'contentMediaType');
    $ok &&= $m;
  }
  return $ok;
}

# --- Objects ---

# A child application at a new instance location (a property value or an array item).
sub _eval_at {
  my ($self, $child_id, $path, $value, $doc_segment) = @_;
  my ($target, $suffix) = $self->_resolve($child_id);
  $self->{c}->begin_child_context($path . $suffix, $self->{nodes}[$target]{pointer}, $doc_segment);
  my $ok = $self->_eval_node($target, $value, undef);
  $self->{c}->commit_child_context($ok, $ok, EVALUATED_SUBSCHEMA);
  return $ok;
}

sub _eval_object {
  my ($self, $n, $x, $bits) = @_;
  my $c = $self->{c};
  my $ok = 1;
  my $count = keys %$x;
  if ($n->{min_properties} >= 0) {
    my $m = $count >= $n->{min_properties} ? 1 : 0;
    $c->evaluated_keyword($m, "Expected the property count to be greater than or equal to '$n->{min_properties}'",
      'minProperties');
    $ok &&= $m;
  }
  if ($n->{max_properties} >= 0) {
    my $m = $count <= $n->{max_properties} ? 1 : 0;
    $c->evaluated_keyword($m, "Expected the property count to be less than or equal to '$n->{max_properties}'",
      'maxProperties');
    $ok &&= $m;
  }
  if (defined $n->{properties} || defined $n->{pattern_properties} || $n->{additional_properties} >= 0
    || $n->{property_names} >= 0) {
    for my $k (JSON::Schema::Corvus::Json::names($x)) {
      my $v = $x->{$k};
      my $doc = _segment($k);
      my $matched = 0;
      my $p = $n->{properties} ? $n->{properties}{$k} : undef;
      if (defined $p) {
        $matched = 1;
        $bits->{$k} = 1 if $bits;
        $ok = $self->_eval_at($p, 'properties/' . _segment($k), $v, $doc) && $ok;
      }
      for my $pp (@{ $n->{pattern_properties} // [] }) {
        next unless $self->_search($pp->{pattern}, $k);
        $matched = 1;
        $bits->{$k} = 1 if $bits;
        $ok = $self->_eval_at($pp->{node}, 'patternProperties/' . _segment($pp->{pattern}), $v, $doc) && $ok;
      }
      if ($n->{additional_properties} >= 0 && !$matched) {
        $bits->{$k} = 1 if $bits;
        $ok = $self->_eval_at($n->{additional_properties}, 'additionalProperties', $v, $doc) && $ok;
      }
      if ($n->{property_names} >= 0) {
        # Not elided; the document path stays the object's.
        $c->begin_child_context('propertyNames', $self->{nodes}[ $n->{property_names} ]{pointer}, undef);
        my $m = $self->_eval_node($n->{property_names}, $k, undef);
        $c->commit_child_context($m, $m, EVALUATED_SUBSCHEMA);
        if (!$m) {
          $c->evaluated_keyword(0, PROPERTY_NAME_FAILED, 'propertyNames');
          $ok = 0;
        }
      }
    }
  }
  if ($n->{required_list}) {
    for my $r (@{ $n->{required_list} }) {
      my $present = exists $x->{$r} ? 1 : 0;
      $c->evaluated_keyword_for_property($present, _required_message($present, $r), $r, 'required');
      $ok &&= $present;
    }
  }
  if ($n->{dependencies}) {
    # Rows are reported under the keyword the schema used (dependencies, dependentRequired or dependentSchemas).
    for my $d (@{ $n->{dependencies} }) {
      next unless exists $x->{ $d->{name} };
      for my $r (@{ $d->{required} // [] }) {
        my $present = exists $x->{$r} ? 1 : 0;
        $c->evaluated_keyword_for_property($present, _required_message($present, $r), $r, $d->{keyword});
        $ok &&= $present;
      }
      if (defined $d->{schema}) {
        my ($m) = $self->_eval_in_place_child($d->{schema}, "$d->{keyword}/" . _segment($d->{name}), $x, $bits, 1);
        $c->evaluated_keyword_for_property($m, _dependency_message($d->{name}), $d->{name}, $d->{keyword});
        $ok &&= $m;
      }
    }
  }
  return $ok;
}

sub _eval_unevaluated_properties {
  my ($self, $n, $x, $bits) = @_;
  my $ok = 1;
  for my $k (JSON::Schema::Corvus::Json::names($x)) {
    next if $bits->{$k};
    $bits->{$k} = 1;
    $ok = $self->_eval_at($n->{unevaluated_properties}, 'unevaluatedProperties', $x->{$k}, _segment($k)) && $ok;
  }
  $self->{c}->evaluated_keyword($ok, undef, 'unevaluatedProperties');
  return $ok;
}

# --- Arrays ---

sub _eval_array {
  my ($self, $n, $x, $bits) = @_;
  my $c = $self->{c};
  my $ok = 1;
  my $length = @$x;
  if ($n->{min_items} >= 0) {
    my $m = $length >= $n->{min_items} ? 1 : 0;
    $c->evaluated_keyword($m, "Expected the item count to be greater than or equal to '$n->{min_items}'", 'minItems');
    $ok &&= $m;
  }
  if ($n->{max_items} >= 0) {
    my $m = $length <= $n->{max_items} ? 1 : 0;
    $c->evaluated_keyword($m, "Expected the item count to be less than or equal to '$n->{max_items}'", 'maxItems');
    $ok &&= $m;
  }
  return $ok if !defined $n->{prefix_items} && $n->{items} < 0 && $n->{contains} < 0 && !$n->{unique_items};
  my $count = 0;
  my $unique = 1;
  my $prefix = $n->{prefix_items};
  for my $i (0 .. $length - 1) {
    if ($prefix && $i < @$prefix) {
      $bits->{"#$i"} = 1 if $bits;
      $ok = $self->_eval_at($prefix->[$i], "$n->{prefix_keyword}/$i", $x->[$i], "$i") && $ok;
    }
    elsif ($n->{items} >= 0) {
      $bits->{"#$i"} = 1 if $bits;
      $ok = $self->_eval_at($n->{items}, $n->{items_keyword}, $x->[$i], "$i") && $ok;
    }
    if ($n->{contains} >= 0) {
      my ($target, $suffix) = $self->_resolve($n->{contains});
      $c->begin_child_context("contains$suffix", $self->{nodes}[$target]{pointer}, "$i");
      if ($self->_eval_node($target, $x->[$i], undef)) {
        $c->commit_child_context(1, 1, EVALUATED_SUBSCHEMA);
        $count++;
        $bits->{"#$i"} = 1 if $n->{contains_marks_evaluated} && $bits;
      }
      else {
        $c->pop_child_context;
      }
    }
    if ($n->{unique_items} && $unique) {
      for my $j (0 .. $i - 1) {
        if (_equal($x->[$i], $x->[$j])) {
          $unique = 0;
          last;
        }
      }
    }
  }
  if ($n->{unique_items}) {
    $c->evaluated_keyword($unique, UNIQUE_ITEMS, 'uniqueItems');
    $ok &&= $unique;
  }
  if ($n->{contains} >= 0) {
    my $hi = $n->{max_contains};
    my $m = ($count >= $n->{min_contains} && ($hi < 0 || $count <= $hi)) ? 1 : 0;
    my $message = $hi >= 0 && $count > $hi ? "Expected the contains count to be less than or equal to '$hi'"
      : "Expected the contains count to be greater than or equal to '$n->{min_contains}'";
    $c->evaluated_keyword($m, $message, 'contains');
    $ok &&= $m;
  }
  return $ok;
}

sub _eval_unevaluated_items {
  my ($self, $n, $x, $bits) = @_;
  my $ok = 1;
  for my $i (0 .. $#$x) {
    next if $bits->{"#$i"};
    $bits->{"#$i"} = 1;
    $ok = $self->_eval_at($n->{unevaluated_items}, 'unevaluatedItems', $x->[$i], "$i") && $ok;
  }
  $self->{c}->evaluated_keyword($ok, undef, 'unevaluatedItems');
  return $ok;
}

# --- In-place applicators ---

sub _can_mark {
  my ($self, $node_id, $x) = @_;
  my $n = $self->{nodes}[$node_id];
  return ref $x eq 'HASH' ? $n->{marks_properties} : $n->{marks_items};
}

# Evaluates an in-place child (EvalInPlaceCore): a new context at the same instance location, on a fresh scratch set of
# evaluated properties or items merged into the parent's on success. A failing child is committed or popped.
sub _eval_in_place_child {
  my ($self, $child_id, $path, $x, $bits, $commit_on_failure, $no_elide) = @_;
  my ($target, $suffix) = $no_elide ? ($child_id, '') : $self->_resolve($child_id);
  my $scratch = $bits && $self->_can_mark($child_id, $x) ? {} : undef;
  my $guarded = $self->{nodes}[$target]{in_place_cycle};
  if ($guarded) {
    if (++$self->{depth} > $self->{program}{max_depth}) {
      $self->{depth} = 0;
      JSON::Schema::Corvus::Runtime::depth_exceeded();
    }
  }
  my $ok = eval {
    $self->{c}->begin_child_context($path . $suffix, $self->{nodes}[$target]{pointer}, undef);
    my $r = $self->_eval_node($target, $x, $scratch);
    if ($r || $commit_on_failure) {
      $self->{c}->commit_child_context($r, $r, EVALUATED_SUBSCHEMA);
    }
    else {
      $self->{c}->pop_child_context;
    }
    @$bits{ keys %$scratch } = values %$scratch if $r && $scratch && $bits;
    $r;
  };
  my $error = $@;
  $self->{depth}-- if $guarded && $self->{depth} > 0;
  die $error unless defined $ok;
  return ($ok, $scratch);
}

sub _resolve_dynamic {
  my ($self, $d) = @_;
  for my $resource (@{ $self->{scope} }) {
    my $target = $d->{by_resource}{$resource};
    return $target if defined $target;
  }
  return $d->{fallback};
}

sub _eval_in_place {
  my ($self, $n, $x, $bits) = @_;
  my $c = $self->{c};
  my $ok = 1;
  if ($n->{ref} >= 0) {
    my ($m) = $self->_eval_in_place_child($n->{ref}, '$ref', $x, $bits, 1);
    $c->evaluated_keyword($m, $m ? MATCHED_ALL : DID_NOT_MATCH_ALL, '$ref');
    $ok &&= $m;
  }
  if ($n->{static_dynamic_ref} >= 0) {
    my $keyword = $n->{static_dynamic_keyword} // '$dynamicRef';
    my ($m) = $self->_eval_in_place_child($n->{static_dynamic_ref}, $keyword, $x, $bits, 1);
    $c->evaluated_keyword($m, $m ? MATCHED_ALL : DID_NOT_MATCH_ALL, $keyword);
    $ok &&= $m;
  }
  if (my $d = $n->{dynamic_ref}) {
    my $keyword = $d->{is_recursive} ? '$recursiveRef' : '$dynamicRef';
    # The resolved target is elided, with no hops in the path.
    my ($target) = $self->_resolve($self->_resolve_dynamic($d));
    my ($m) = $self->_eval_in_place_child($target, $keyword, $x, $bits, 1, 1);
    $c->evaluated_keyword($m, $m ? MATCHED_ALL : DID_NOT_MATCH_ALL, $keyword);
    $ok &&= $m;
  }
  if ($n->{all_of}) {
    my $all_ok = 1;
    for my $i (0 .. $#{ $n->{all_of} }) {
      my ($m) = $self->_eval_in_place_child($n->{all_of}[$i], "allOf/$i", $x, $bits, 1);
      $all_ok = 0 unless $m;
    }
    $c->evaluated_keyword($all_ok, $all_ok ? MATCHED_ALL : DID_NOT_MATCH_ALL, 'allOf');
    $ok &&= $all_ok;
  }
  if ($n->{any_of}) {
    my $any_ok = 0;
    for my $i (0 .. $#{ $n->{any_of} }) {
      my ($m) = $self->_eval_in_place_child($n->{any_of}[$i], "anyOf/$i", $x, $bits, 0);
      $any_ok = 1 if $m;
    }
    $c->evaluated_keyword($any_ok, $any_ok ? MATCHED_AT_LEAST_ONE : DID_NOT_MATCH_AT_LEAST_ONE, 'anyOf');
    $ok &&= $any_ok;
  }
  if ($n->{one_of}) {
    my $matched = 0;
    my $only;
    for my $i (0 .. $#{ $n->{one_of} }) {
      # Evaluated properties and items are merged only when exactly one branch matched, so they are collected aside.
      my ($r_ok, $r_scratch) = $self->_eval_in_place_child($n->{one_of}[$i], "oneOf/$i", $x, $bits ? {} : undef, 0);
      if ($r_ok) {
        $matched++;
        $only = $r_scratch;
      }
    }
    @$bits{ keys %$only } = values %$only if $matched == 1 && $bits && $only;
    my $message = $matched == 0 ? MATCHED_NO_SCHEMA : $matched == 1 ? MATCHED_EXACTLY_ONE : MATCHED_MORE_THAN_ONE;
    $c->evaluated_keyword($matched == 1 ? 1 : 0, $message, 'oneOf');
    $ok &&= $matched == 1;
  }
  if ($n->{not_} >= 0) {
    # Not elided, and never contributes results or evaluated properties or items.
    $c->begin_child_context('not', $self->{nodes}[ $n->{not_} ]{pointer}, undef);
    my $inner = $self->_eval_node($n->{not_}, $x, undef);
    $c->pop_child_context;
    $c->evaluated_keyword($inner ? 0 : 1, $inner ? MATCHED_NOT : DID_NOT_MATCH_NOT, 'not');
    $ok &&= !$inner;
  }
  if ($n->{if_} >= 0) {
    my ($cond) = $self->_eval_in_place_child($n->{if_}, 'if', $x, $bits, 0);
    $c->evaluated_keyword(1, $cond ? MATCHED_IF_FOR_THEN : MATCHED_IF_FOR_ELSE, 'if');
    if ($cond && $n->{then} >= 0) {
      my ($m) = $self->_eval_in_place_child($n->{then}, 'then', $x, $bits, 1);
      $c->evaluated_keyword($m, $m ? MATCHED_THEN : DID_NOT_MATCH_THEN, 'then');
      $ok &&= $m;
    }
    elsif (!$cond && $n->{else_} >= 0) {
      my ($m) = $self->_eval_in_place_child($n->{else_}, 'else', $x, $bits, 1);
      $c->evaluated_keyword($m, $m ? MATCHED_ELSE : DID_NOT_MATCH_ELSE, 'else');
      $ok &&= $m;
    }
  }
  return $ok ? 1 : 0;
}

sub _dependency_message {
  my ($name) = @_;
  return sub { "The value did match the schema applied because it contained the property '$name'" };
}

sub _required_message {
  my ($present, $name) = @_;
  return sub { 'Required property ' . ($present ? '' : 'not ') . "present '$name'" };
}

1;
