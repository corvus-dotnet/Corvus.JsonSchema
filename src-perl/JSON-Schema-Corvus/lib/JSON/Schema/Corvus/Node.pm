package JSON::Schema::Corvus::Node;

# The compiled schema graph: one node per distinct (document, pointer), with its keywords digested. A port of the
# Python package's node.py (Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaNode, trimmed to what code generation
# and results collection need). A node is a hash of the fields below; the compiler sets them, and they are fixed once
# compilation finishes, so the derived has_* and is_* properties are computed once.

use 5.026;
use strict;
use warnings;
use Exporter 'import';

# JSON types as bits.
use constant { T_NONE => 0, T_NULL => 1, T_BOOLEAN => 2, T_OBJECT => 4, T_ARRAY => 8, T_NUMBER => 16, T_STRING => 32,
  T_INTEGER => 64, T_ALL => 127 };

# Content kinds.
use constant { CONTENT_NONE => 0, CONTENT_BASE64 => 1, CONTENT_JSON => 2, CONTENT_BASE64_JSON => 3 };

our @EXPORT_OK = qw(T_NONE T_NULL T_BOOLEAN T_OBJECT T_ARRAY T_NUMBER T_STRING T_INTEGER T_ALL
  CONTENT_NONE CONTENT_BASE64 CONTENT_JSON CONTENT_BASE64_JSON);
our %EXPORT_TAGS = (all => \@EXPORT_OK);

# The fields of a node and their defaults (-1 is "none" for a child node id or a count; undef for a list or a map).
my %DEFAULTS = (
  always_true => 0, always_false => 0,
  # Assertions.
  type => T_NONE, has_type => 0, has_const => 0, const_value => undef, enum_values => undef,
  # References. dynamic_ref is { anchor, is_recursive, fallback, by_resource => {resource id => node id} }.
  ref => -1, static_dynamic_ref => -1, static_dynamic_keyword => undef, dynamic_ref => undef,
  # In-place applicators.
  all_of => undef, any_of => undef, one_of => undef, not_ => -1, if_ => -1, then => -1, else_ => -1,
  # Objects. properties is { name => node id } with property_order the names in schema order; pattern_properties is
  # [ { pattern, node } ]; dependencies is [ { keyword, name, required => [names] or undef, schema => node id or
  # undef } ].
  properties => undef, property_order => undef, pattern_properties => undef, additional_properties => -1,
  property_names => -1, required => undef, required_list => undef, dependencies => undef, min_properties => -1,
  max_properties => -1, unevaluated_properties => -1,
  # Arrays.
  prefix_items => undef, prefix_keyword => 'prefixItems', items_keyword => 'items', items => -1, contains => -1,
  min_contains => 1, max_contains => -1, contains_marks_evaluated => 0, min_items => -1, max_items => -1,
  unique_items => 0, unevaluated_items => -1,
  # Strings.
  min_length => -1, max_length => -1, pattern => undef, format => undef, format_kind => 'unknown', assert_format => 0,
  content => CONTENT_NONE, assert_content => 0,
  # Numbers.
  minimum => undef, maximum => undef, exclusive_minimum => undef, exclusive_maximum => undef, multiple_of => undef,
  # Annotations, in schema order: [ { keyword, value, strings_only } ].
  annotations => undef,
  # Analysis. A discriminator is { property, known => [ [value, [branch indexes]] ], unknown => [branch indexes],
  # all_require }.
  marks_properties => 0, marks_items => 0, in_place_cycle => 0, one_of_discriminator => undef,
  any_of_discriminator => undef,
);

sub new {
  my ($class, %fields) = @_;
  return bless { %DEFAULTS, %fields }, $class;
}

sub _is_numeric_format {
  require JSON::Schema::Corvus::Formats;
  return JSON::Schema::Corvus::Formats::is_numeric_format($_[0]);
}

sub has_object_keywords {
  my ($n) = @_;
  return $n->{_has_object} //= (defined $n->{properties} || defined $n->{pattern_properties}
    || $n->{additional_properties} >= 0 || $n->{property_names} >= 0 || defined $n->{required}
    || defined $n->{dependencies} || $n->{min_properties} >= 0 || $n->{max_properties} >= 0
    || $n->{unevaluated_properties} >= 0) ? 1 : 0;
}

sub has_array_keywords {
  my ($n) = @_;
  return $n->{_has_array} //= (defined $n->{prefix_items} || $n->{items} >= 0 || $n->{contains} >= 0
    || $n->{min_items} >= 0 || $n->{max_items} >= 0 || $n->{unique_items} || $n->{unevaluated_items} >= 0) ? 1 : 0;
}

sub has_string_keywords {
  my ($n) = @_;
  return $n->{_has_string} //= ($n->{min_length} >= 0 || $n->{max_length} >= 0 || defined $n->{pattern}
    || ($n->{assert_format} && defined $n->{format} && !_is_numeric_format($n->{format_kind}))
    || $n->{assert_content}) ? 1 : 0;
}

sub has_number_keywords {
  my ($n) = @_;
  return $n->{_has_number} //= (defined $n->{minimum} || defined $n->{maximum} || defined $n->{exclusive_minimum}
    || defined $n->{exclusive_maximum} || defined $n->{multiple_of}
    || ($n->{assert_format} && _is_numeric_format($n->{format_kind}))) ? 1 : 0;
}

sub has_in_place_applicators {
  my ($n) = @_;
  return $n->{_has_in_place} //= ($n->{ref} >= 0 || $n->{static_dynamic_ref} >= 0 || defined $n->{dynamic_ref}
    || defined $n->{all_of} || defined $n->{any_of} || defined $n->{one_of} || $n->{not_} >= 0 || $n->{if_} >= 0
    || (defined $n->{dependencies} && grep { defined $_->{schema} } @{ $n->{dependencies} })) ? 1 : 0;
}

# Only type (a type test at the call site).
sub is_type_only {
  my ($n) = @_;
  return $n->{_type_only} //= ($n->{has_type} && !$n->{has_const} && !defined $n->{enum_values}
    && !$n->has_object_keywords && !$n->has_array_keywords && !$n->has_string_keywords
    && !$n->has_number_keywords && !$n->has_in_place_applicators) ? 1 : 0;
}

# Nothing but $ref.
sub is_pure_ref {
  my ($n) = @_;
  return $n->{_pure_ref} //= ($n->{ref} >= 0 && !$n->{has_type} && !$n->{has_const} && !defined $n->{enum_values}
    && !$n->has_object_keywords && !$n->has_array_keywords && !$n->has_string_keywords
    && !$n->has_number_keywords && !defined $n->{dynamic_ref} && $n->{static_dynamic_ref} < 0
    && !defined $n->{all_of} && !defined $n->{any_of} && !defined $n->{one_of} && $n->{not_} < 0
    && $n->{if_} < 0 && !defined $n->{dependencies}) ? 1 : 0;
}

# The in-place children (the instance is evaluated at the same location).
sub in_place_children {
  my ($n, $include_not) = @_;
  my @out;
  push @out, $n->{ref} if $n->{ref} >= 0;
  push @out, $n->{static_dynamic_ref} if $n->{static_dynamic_ref} >= 0;
  if (my $d = $n->{dynamic_ref}) {
    push @out, $d->{fallback};
    push @out, map { $d->{by_resource}{$_} } sort { $a <=> $b } keys %{ $d->{by_resource} };
  }
  push @out, @{ $n->{all_of} } if $n->{all_of};
  push @out, @{ $n->{any_of} } if $n->{any_of};
  push @out, @{ $n->{one_of} } if $n->{one_of};
  push @out, $n->{not_} if $include_not && $n->{not_} >= 0;
  push @out, $n->{if_} if $n->{if_} >= 0;
  push @out, $n->{then} if $n->{then} >= 0;
  push @out, $n->{else_} if $n->{else_} >= 0;
  if ($n->{dependencies}) {
    push @out, map { defined $_->{schema} ? $_->{schema} : () } @{ $n->{dependencies} };
  }
  return \@out;
}

# Every child node.
sub children {
  my ($n) = @_;
  my @out = @{ $n->in_place_children(1) };
  push @out, map { $n->{properties}{$_} } @{ $n->{property_order} } if $n->{properties};
  push @out, map { $_->{node} } @{ $n->{pattern_properties} } if $n->{pattern_properties};
  for my $c (@$n{qw(additional_properties property_names unevaluated_properties items contains unevaluated_items)}) {
    push @out, $c if $c >= 0;
  }
  push @out, @{ $n->{prefix_items} } if $n->{prefix_items};
  return \@out;
}

1;
