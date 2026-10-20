package JSON::Schema::Corvus::Compiler;

# Compiles loaded schema documents into a node graph and runs the compile-time analyses. A port of the Python
# package's compiler.py (Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaCompiler, flag-mode subset).

use 5.026;
use strict;
use warnings;
use List::Util ();
use JSON::Schema::Corvus::Dialect qw(:all);
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Formats ();
use JSON::Schema::Corvus::Json;
use JSON::Schema::Corvus::Loader;
use JSON::Schema::Corvus::Node qw(:all);
use JSON::Schema::Corvus::Pattern ();
use JSON::Schema::Corvus::Uri;

# Keywords CompileNode handles itself; anything else is an unknown keyword (an annotation from 2019-09).
my %KNOWN_KEYWORDS = map { $_ => 1 } qw(
  if not type enum $ref then else const items allOf anyOf oneOf title $defs format pattern maximum minimum default
  $schema $anchor required contains maxItems minItems examples readOnly $comment maxLength minLength writeOnly
  properties multipleOf deprecated uniqueItems prefixItems minContains maxContains description $vocabulary definitions
  $dynamicRef dependencies propertyNames maxProperties minProperties contentSchema $recursiveRef $dynamicAnchor
  contentEncoding additionalItems exclusiveMaximum exclusiveMinimum unevaluatedItems contentMediaType $recursiveAnchor
  dependentSchemas patternProperties dependentRequired additionalProperties unevaluatedProperties id $id
);

my %TYPE_MASKS = (null => T_NULL, boolean => T_BOOLEAN, object => T_OBJECT, array => T_ARRAY, number => T_NUMBER,
  string => T_STRING, integer => T_INTEGER);

sub _is_number { JSON::Schema::Corvus::Json::is_number($_[0]) }
sub _is_string { JSON::Schema::Corvus::Json::is_string($_[0]) }
sub _is_true { JSON::Schema::Corvus::Json::is_bool($_[0]) && $_[0] }
sub _num { _is_number($_[0]) ? $_[0] : undef }

sub _get_int {
  my ($v, $fallback) = @_;
  return $fallback unless _is_number($v);
  return ref $v ? (0 + "$v") : int($v) if JSON::Schema::Corvus::Json::is_integer($v, 0);
  return $fallback;
}

sub _strings { [ grep { _is_string($_) } @{ $_[0] } ] }

sub _kw { '/' . JSON::Schema::Corvus::Uri::escape_pointer_token($_[0]) }

sub _target {
  my ($document, $pointer, $value, $resource) = @_;
  return { document => $document, pointer => $pointer, value => $value, resource => $resource };
}

# Compiles a schema (a JSON value) with the options: the program { nodes, root, root_resource, uses_dynamic_scope,
# options, annotation_sources }.
sub compile {
  my ($class, $schema, $options) = @_;
  my $loader = JSON::Schema::Corvus::Loader->new($options);
  my $root_resource = $loader->load_root($schema, $options->{base_uri});
  return $class->_compile_from($loader, $options, $root_resource, $schema);
}

sub compile_from_uri {
  my ($class, $uri, $options) = @_;
  my $loader = JSON::Schema::Corvus::Loader->new($options);
  my $root_resource = $loader->load_root_from_uri($uri);
  return $class->_compile_from($loader, $options, $root_resource,
    JSON::Schema::Corvus::Loader::resource_root($root_resource));
}

sub _compile_from {
  my ($class, $loader, $options, $root_resource, $schema) = @_;
  my $self = bless {
    loader => $loader,
    options => $options,
    nodes => [],
    targets => [],
    worklist => [],
    worklist_head => 0,
    pending_dynamic_refs => [],
    annotation_sources => [],
    entry_node => -1,
  }, $class;
  my $target = _target($root_resource->{document}, '', $schema, $root_resource);
  if (defined $options->{entry_point}) {
    $target = $loader->try_resolve_reference($root_resource, $options->{entry_point})
      or die JSON::Schema::Corvus::Error::Compilation->_new(
        "Unable to resolve the entry point '$options->{entry_point}'.");
  }
  $self->{entry_node} = $self->_get_node($target);
  $self->_compile_all;
  my $uses_dynamic_scope = (List::Util::any { defined $_->{dynamic_ref} } @{ $self->{nodes} }) ? 1 : 0;
  $self->_analyse;
  return {
    nodes => $self->{nodes},
    root => $self->{entry_node},
    root_resource => $self->{nodes}[ $self->{entry_node} ]{resource_id},
    uses_dynamic_scope => $uses_dynamic_scope,
    options => $options,
    annotation_sources => $self->{annotation_sources},
  };
}

sub _get_node {
  my ($self, $target) = @_;
  my $loader = $self->{loader};
  my $node_id = $loader->node_of($target->{document}, $target->{value}, $target->{pointer});
  if (!defined $node_id) {
    $node_id = scalar @{ $self->{nodes} };
    push @{ $self->{nodes} }, JSON::Schema::Corvus::Node->new(
      id => $node_id,
      resource_id => $target->{resource}{id},
      dialect => $target->{resource}{dialect},
      location => "$target->{document}{retrieval_uri}#$target->{pointer}",
      pointer => $target->{pointer},
    );
    push @{ $self->{targets} }, $target;
    push @{ $self->{annotation_sources} }, undef;
    $loader->set_node_of($target->{document}, $target->{value}, $target->{pointer}, $node_id);
    push @{ $self->{worklist} }, $node_id;
  }
  return $node_id;
}

sub _drain {
  my ($self) = @_;
  while ($self->{worklist_head} < @{ $self->{worklist} }) {
    my $node_id = $self->{worklist}[ $self->{worklist_head}++ ];
    $self->_compile_node($self->{nodes}[$node_id], $self->{targets}[$node_id]);
  }
}

sub _compile_all {
  my ($self) = @_;
  while (1) {
    $self->_drain;
    last if !@{ $self->{pending_dynamic_refs} }
      || (!$self->_expand_dynamic_refs && $self->{worklist_head} == @{ $self->{worklist} });
  }
  # Most schemas have no dynamic reference: skip the finalisation.
  $self->_finalize_dynamic_refs if @{ $self->{pending_dynamic_refs} };
}

sub _child {
  my ($self, $parent, $value, $relative) = @_;
  my $pointer = $parent->{pointer} . $relative;
  my $resource = $self->{loader}->resource_of($parent->{document}, $value, $pointer) || $parent->{resource};
  return $self->_get_node(_target($parent->{document}, $pointer, $value, $resource));
}

sub _child_array {
  my ($self, $parent, $value, $keyword) = @_;
  return undef unless ref $value eq 'ARRAY';
  return [ map { $self->_child($parent, $value->[$_], "/$keyword/$_") } 0 .. $#$value ];
}

sub _compile_node {
  my ($self, $node, $target) = @_;
  my $e = $target->{value};
  if (JSON::Schema::Corvus::Json::is_bool($e)) {
    $node->{$e ? 'always_true' : 'always_false'} = 1;
    return;
  }
  if (ref $e ne 'HASH') {
    $node->{always_true} = 1;
    return;
  }

  my $dialect = $target->{resource}{dialect};
  my $vocab = $target->{resource}{vocabularies};
  my $legacy = $dialect <= DRAFT7;

  if ($legacy && _is_string($e->{'$ref'})) {
    # In draft 7 and earlier, $ref replaces every sibling keyword.
    $self->_compile_ref($node, $target, $e->{'$ref'});
    return;
  }

  my $applicator = $legacy || ($vocab & V_APPLICATOR);
  my $validation = $legacy || ($vocab & V_VALIDATION);
  my $unevaluated = $dialect == DRAFT201909 ? $applicator : ($vocab & V_UNEVALUATED);
  my $content = $legacy || ($vocab & V_CONTENT);
  my $assert_format = $self->{options}{assert_format};
  my $format_assert = defined $assert_format ? $assert_format
    : ($legacy && $self->{options}{assert_format_in_legacy_drafts}) || ($vocab & V_FORMAT_ASSERTION);

  my $dependencies;

  # References.
  $self->_compile_ref($node, $target, $e->{'$ref'}) if _is_string($e->{'$ref'});
  $self->_compile_dynamic_ref($node, $target, $e->{'$dynamicRef'}, 0)
    if $dialect >= DRAFT202012 && _is_string($e->{'$dynamicRef'});
  $self->_compile_dynamic_ref($node, $target, $e->{'$recursiveRef'}, 1)
    if $dialect == DRAFT201909 && _is_string($e->{'$recursiveRef'});

  # Applicators.
  if ($applicator) {
    $node->{all_of} = $self->_child_array($target, $e->{allOf}, 'allOf') if exists $e->{allOf};
    $node->{any_of} = $self->_child_array($target, $e->{anyOf}, 'anyOf') if exists $e->{anyOf};
    $node->{one_of} = $self->_child_array($target, $e->{oneOf}, 'oneOf') if exists $e->{oneOf};
    $node->{not_} = $self->_child($target, $e->{not}, _kw('not')) if exists $e->{not};
    if ($dialect >= DRAFT7 && exists $e->{if}) {
      $node->{if_} = $self->_child($target, $e->{if}, _kw('if'));
      $node->{then} = $self->_child($target, $e->{then}, _kw('then')) if exists $e->{then};
      $node->{else_} = $self->_child($target, $e->{else}, _kw('else')) if exists $e->{else};
    }
    my $props = $e->{properties};
    if (ref $props eq 'HASH') {
      my @order = JSON::Schema::Corvus::Json::names($props);
      $node->{property_order} = \@order;
      $node->{properties} = { map {
        $_ => $self->_child($target, $props->{$_}, '/properties/' . JSON::Schema::Corvus::Uri::escape_pointer_token($_))
      } @order };
    }
    my $pattern_props = $e->{patternProperties};
    if (ref $pattern_props eq 'HASH') {
      $self->_check_pattern($_) for JSON::Schema::Corvus::Json::names($pattern_props);
      $node->{pattern_properties} = [ map {
        { pattern => $_, node => $self->_child($target, $pattern_props->{$_},
          '/patternProperties/' . JSON::Schema::Corvus::Uri::escape_pointer_token($_)) }
      } JSON::Schema::Corvus::Json::names($pattern_props) ];
    }
    $node->{additional_properties} = $self->_child($target, $e->{additionalProperties}, _kw('additionalProperties'))
      if exists $e->{additionalProperties};
    $node->{property_names} = $self->_child($target, $e->{propertyNames}, _kw('propertyNames'))
      if $dialect >= DRAFT6 && exists $e->{propertyNames};
    $node->{contains} = $self->_child($target, $e->{contains}, _kw('contains'))
      if $dialect >= DRAFT6 && exists $e->{contains};

    # "dependencies" is honoured in every dialect: in 2019-09 and later it is an optional compatibility keyword.
    if (ref $e->{dependencies} eq 'HASH' || ($dialect >= DRAFT201909 && ref $e->{dependentSchemas} eq 'HASH')) {
      $dependencies = $self->_compile_dependency_schemas($target, $e, $dialect);
    }

    # Array applicators.
    if ($dialect >= DRAFT202012) {
      $node->{prefix_items} = $self->_child_array($target, $e->{prefixItems}, 'prefixItems')
        if ref $e->{prefixItems} eq 'ARRAY';
      $node->{items} = $self->_child($target, $e->{items}, _kw('items'))
        if exists $e->{items} && ref $e->{items} ne 'ARRAY';
    }
    elsif (exists $e->{items}) {
      if (ref $e->{items} eq 'ARRAY') {
        $self->_compile_items_array($node, $target, $e);
      }
      else {
        $node->{items} = $self->_child($target, $e->{items}, _kw('items'));
      }
    }
    $node->{contains_marks_evaluated} = $dialect >= DRAFT202012 ? 1 : 0;
  }

  if ($unevaluated && $dialect >= DRAFT201909) {
    $node->{unevaluated_properties} = $self->_child($target, $e->{unevaluatedProperties}, _kw('unevaluatedProperties'))
      if exists $e->{unevaluatedProperties};
    $node->{unevaluated_items} = $self->_child($target, $e->{unevaluatedItems}, _kw('unevaluatedItems'))
      if exists $e->{unevaluatedItems};
  }

  if ($validation) {
    $self->_compile_type($node, $e->{type}) if exists $e->{type};
    if ($dialect >= DRAFT6 && exists $e->{const}) {
      $node->{has_const} = 1;
      $node->{const_value} = $e->{const};
    }
    $node->{enum_values} = $e->{enum} if ref $e->{enum} eq 'ARRAY';
    if (ref $e->{required} eq 'ARRAY') {
      $node->{required_list} = _strings($e->{required});
      my %seen;
      $node->{required} = [ grep { !$seen{$_}++ } @{ $node->{required_list} } ];
    }
    if ($dialect >= DRAFT201909 && ref $e->{dependentRequired} eq 'HASH') {
      $dependencies //= [];
      for my $name (JSON::Schema::Corvus::Json::names($e->{dependentRequired})) {
        my $v = $e->{dependentRequired}{$name};
        push @$dependencies, { keyword => 'dependentRequired', name => $name, required => _strings($v) }
          if ref $v eq 'ARRAY';
      }
    }
    $node->{min_properties} = _get_int($e->{minProperties}, -1);
    $node->{max_properties} = _get_int($e->{maxProperties}, -1);
    $node->{min_items} = _get_int($e->{minItems}, -1);
    $node->{max_items} = _get_int($e->{maxItems}, -1);
    $node->{unique_items} = _is_true($e->{uniqueItems}) ? 1 : 0;
    $node->{min_length} = _get_int($e->{minLength}, -1);
    $node->{max_length} = _get_int($e->{maxLength}, -1);
    if (_is_string($e->{pattern})) {
      $node->{pattern} = $e->{pattern};
      $self->_check_pattern($e->{pattern});
    }
    $node->{multiple_of} = $e->{multipleOf} if _is_number($e->{multipleOf});

    if ($dialect == DRAFT4) {
      _compile_draft4_bounds($node, $e);
    }
    else {
      $node->{maximum} = _num($e->{maximum});
      $node->{minimum} = _num($e->{minimum});
      $node->{exclusive_maximum} = _num($e->{exclusiveMaximum});
      $node->{exclusive_minimum} = _num($e->{exclusiveMinimum});
    }

    if ($dialect >= DRAFT201909 && $node->{contains} >= 0) {
      $node->{min_contains} = _get_int($e->{minContains}, 1) if exists $e->{minContains};
      $node->{max_contains} = _get_int($e->{maxContains}, -1) if exists $e->{maxContains};
    }
  }

  if (_is_string($e->{format})) {
    $node->{format} = $e->{format};
    $node->{format_kind} = JSON::Schema::Corvus::Formats::format_kind($e->{format}, $dialect);
    $node->{assert_format} = $format_assert ? 1 : 0;
  }

  # Content keywords are asserted only in draft 7 (and annotations elsewhere).
  if ($content && $dialect >= DRAFT7 && (exists $e->{contentEncoding} || exists $e->{contentMediaType})) {
    $self->_compile_content($node, $e, $dialect);
  }

  if ($dependencies) {
    # In the order the three keywords appear in the schema, as CompileNode meets them.
    my @order = JSON::Schema::Corvus::Json::names($e);
    my %position = map { $order[$_] => $_ } 0 .. $#order;
    @$dependencies = map { $_->[1] } sort { $a->[0] <=> $b->[0] } map { [ $position{ $_->{keyword} }, $_ ] }
      @$dependencies;
  }
  $node->{dependencies} = $dependencies;
  $self->{annotation_sources}[ $node->{id} ] = { e => $e, dialect => $dialect, vocab => $vocab, legacy => $legacy,
    content => $content };
}

# A regular expression that is not valid does not compile, and one over a limit of size dies with a PatternTooLarge
# error.
sub _check_pattern {
  my ($self, $pattern) = @_;
  JSON::Schema::Corvus::Pattern::compile_pattern($pattern, $self->{options}{pattern_match_limit})
    or die JSON::Schema::Corvus::Error::Compilation->_new("Invalid regular expression '$pattern'.");
}

sub _compile_draft4_bounds {
  my ($node, $e) = @_;
  my $maximum = _num($e->{maximum});
  my $minimum = _num($e->{minimum});
  if (defined $maximum) {
    $node->{ _is_true($e->{exclusiveMaximum}) ? 'exclusive_maximum' : 'maximum' } = $maximum;
  }
  if (defined $minimum) {
    $node->{ _is_true($e->{exclusiveMinimum}) ? 'exclusive_minimum' : 'minimum' } = $minimum;
  }
}

sub _compile_dependency_schemas {
  my ($self, $target, $e, $dialect) = @_;
  my @dependencies;
  my $deps = $e->{dependencies};
  if (ref $deps eq 'HASH') {
    for my $name (JSON::Schema::Corvus::Json::names($deps)) {
      my $v = $deps->{$name};
      if (ref $v eq 'ARRAY') {
        push @dependencies, { keyword => 'dependencies', name => $name, required => _strings($v) };
      }
      else {
        my $child = $self->_child($target, $v, '/dependencies/' . JSON::Schema::Corvus::Uri::escape_pointer_token($name));
        push @dependencies, { keyword => 'dependencies', name => $name, schema => $child };
      }
    }
  }
  my $schemas = $e->{dependentSchemas};
  if ($dialect >= DRAFT201909 && ref $schemas eq 'HASH') {
    for my $name (JSON::Schema::Corvus::Json::names($schemas)) {
      my $child = $self->_child($target, $schemas->{$name},
        '/dependentSchemas/' . JSON::Schema::Corvus::Uri::escape_pointer_token($name));
      push @dependencies, { keyword => 'dependentSchemas', name => $name, schema => $child };
    }
  }
  return \@dependencies;
}

# Array-form items (before 2020-12): positional schemas, then additionalItems for the rest.
sub _compile_items_array {
  my ($self, $node, $target, $e) = @_;
  $node->{prefix_items} = $self->_child_array($target, $e->{items}, 'items');
  $node->{prefix_keyword} = 'items';
  if (exists $e->{additionalItems}) {
    $node->{items} = $self->_child($target, $e->{additionalItems}, '/additionalItems');
    $node->{items_keyword} = 'additionalItems';
  }
}

sub _compile_content {
  my ($self, $node, $e, $dialect) = @_;
  my $base64 = _is_string($e->{contentEncoding}) && $e->{contentEncoding} eq 'base64';
  my $is_json = _is_string($e->{contentMediaType}) && $e->{contentMediaType} eq 'application/json';
  $node->{content} = $base64 ? ($is_json ? CONTENT_BASE64_JSON : CONTENT_BASE64) : $is_json ? CONTENT_JSON
    : CONTENT_NONE;
  $node->{assert_content} = ($dialect == DRAFT7 && $self->{options}{assert_content} && $node->{content} != CONTENT_NONE)
    ? 1 : 0;
}

sub _compile_type {
  my ($self, $node, $value) = @_;
  my $mask = T_NONE;
  if (ref $value eq 'ARRAY') {
    $mask |= (_is_string($_) ? $TYPE_MASKS{$_} // T_NONE : T_NONE) for @$value;
  }
  elsif (_is_string($value)) {
    $mask = $TYPE_MASKS{$value} // T_NONE;
  }
  $node->{type} = $mask;
  $node->{has_type} = 1;
}

sub _compile_ref {
  my ($self, $node, $target, $reference) = @_;
  my $resolved = $self->{loader}->try_resolve_reference($target->{resource}, $reference)
    or die JSON::Schema::Corvus::Error::Compilation->_new(
      "Unable to resolve reference '$reference' from '$target->{resource}{uri}'.");
  $node->{ref} = $self->_get_node($resolved);
}

sub _compile_dynamic_ref {
  my ($self, $node, $target, $reference, $is_recursive) = @_;
  my $resolved = $self->{loader}->try_resolve_reference($target->{resource}, $reference)
    or die JSON::Schema::Corvus::Error::Compilation->_new(
      "Unable to resolve reference '$reference' from '$target->{resource}{uri}'.");
  my $fragment = JSON::Schema::Corvus::Uri::decode_fragment((JSON::Schema::Corvus::Uri::split_fragment($reference))[1]);
  my $dynamic;
  if ($is_recursive) {
    $dynamic = $resolved->{resource}{recursive_anchor} && $resolved->{pointer} eq $resolved->{resource}{root_pointer};
  }
  else {
    my $anchors = $resolved->{resource}{dynamic_anchors};
    $dynamic = length $fragment && substr($fragment, 0, 1) ne '/' && $anchors && defined $anchors->{$fragment}
      && $anchors->{$fragment} eq $resolved->{pointer};
  }
  if (!$dynamic) {
    # A static reference, kept apart from any sibling $ref (C# overwrites the $ref; both apply here).
    $node->{static_dynamic_ref} = $self->_get_node($resolved);
    $node->{static_dynamic_keyword} = $is_recursive ? '$recursiveRef' : '$dynamicRef';
    return;
  }
  push @{ $self->{pending_dynamic_refs} }, { node_id => $node->{id}, anchor => $fragment, is_recursive => $is_recursive,
    initial_target => $resolved, seen_resources => {}, candidates => [] };
}

sub _expand_dynamic_refs {
  my ($self) = @_;
  my $added = 0;
  for my $pending (@{ $self->{pending_dynamic_refs} }) {
    for my $resource (@{ $self->{loader}{resources} }) {
      next if $pending->{seen_resources}{ $resource->{id} }++;
      my $pointer;
      if ($pending->{is_recursive}) {
        next unless $resource->{recursive_anchor};
        $pointer = $resource->{root_pointer};
      }
      else {
        $pointer = $resource->{dynamic_anchors} ? $resource->{dynamic_anchors}{ $pending->{anchor} } : undef;
        next unless defined $pointer;
      }
      my $before = @{ $self->{nodes} };
      my $node_id = $self->_get_node($self->_target_in($resource, $pointer));
      $added ||= @{ $self->{nodes} } != $before;
      push @{ $pending->{candidates} }, [ $resource->{id}, $node_id ];
    }
  }
  return $added;
}

sub _target_in {
  my ($self, $resource, $pointer) = @_;
  my $t = $self->{loader}->try_resolve_fragment($resource,
    $pointer eq $resource->{root_pointer} ? '' : substr($pointer, length $resource->{root_pointer}));
  return $t || _target($resource->{document}, $pointer, JSON::Schema::Corvus::Loader::resource_root($resource),
    $resource);
}

sub _finalize_dynamic_refs {
  my ($self) = @_;
  my $reachable;
  for my $pending (@{ $self->{pending_dynamic_refs} }) {
    my $node = $self->{nodes}[ $pending->{node_id} ];
    my $fallback = $self->_get_node($pending->{initial_target});
    my $set_static = sub {
      $node->{static_dynamic_ref} = $_[0];
      $node->{static_dynamic_keyword} = $pending->{is_recursive} ? '$recursiveRef' : '$dynamicRef';
    };
    if (@{ $pending->{candidates} } <= 1) {
      # Only the initial target's resource defines the anchor: resolution is static.
      $set_static->($fallback);
      next;
    }
    # The dynamic scope is searched outermost first, and its outermost entry is always the resource evaluation started
    # in: when the entry resource defines the anchor, that target is the answer on every path.
    $reachable //= $self->_compute_reachability;
    if (!$reachable->[ $pending->{node_id} ]) {
      $set_static->($fallback);
      next;
    }
    my $entry_resource = $self->{nodes}[ $self->{entry_node} ]{resource_id};
    my ($uniform) = grep { $_->[0] == $entry_resource } @{ $pending->{candidates} };
    if ($uniform) {
      $set_static->($uniform->[1]);
      next;
    }
    $node->{dynamic_ref} = { anchor => $pending->{anchor}, is_recursive => $pending->{is_recursive},
      fallback => $fallback, by_resource => { map { $_->[0] => $_->[1] } @{ $pending->{candidates} } } };
  }
  $self->_drain;
}

# The nodes reachable from the entry, counting every candidate of a pending dynamic reference as a child.
sub _compute_reachability {
  my ($self) = @_;
  my %extra;
  for my $p (@{ $self->{pending_dynamic_refs} }) {
    push @{ $extra{ $p->{node_id} } }, $self->_get_node($p->{initial_target}), map { $_->[1] } @{ $p->{candidates} };
  }
  my @reached;
  my @stack = ($self->{entry_node});
  $reached[ $self->{entry_node} ] = 1;
  while (@stack) {
    my $node_id = pop @stack;
    for my $c (@{ $self->{nodes}[$node_id]->children }, @{ $extra{$node_id} // [] }) {
      next if $reached[$c];
      $reached[$c] = 1;
      push @stack, $c;
    }
  }
  return \@reached;
}

# --- Analyses ---

sub _analyse {
  my ($self) = @_;
  # The in-place edges, built once for both analyses; discriminators only where a node has a oneOf or anyOf.
  my @edges;
  my ($in_place, $branches) = (0, 0);
  for my $n (@{ $self->{nodes} }) {
    my $children = $n->in_place_children(1);
    push @edges, $children;
    $in_place = 1 if @$children;
    $branches = 1 if defined $n->{one_of} || defined $n->{any_of};
  }
  $self->_compute_marking($in_place ? \@edges : undef);
  $self->_compute_in_place_cycles(\@edges) if $in_place;
  $self->_compute_discriminators if $branches;
}

# Which nodes can contribute evaluated-property or item annotations (ComputeMarking): a node marks if it has the
# keywords itself or an in-place child (not counting not) marks.
sub _compute_marking {
  my ($self, $edges) = @_;
  my $nodes = $self->{nodes};
  for my $n (@$nodes) {
    $n->{marks_properties} = (defined $n->{properties} || defined $n->{pattern_properties}
      || $n->{additional_properties} >= 0 || $n->{unevaluated_properties} >= 0) ? 1 : 0;
    $n->{marks_items} = (defined $n->{prefix_items} || $n->{items} >= 0
      || ($n->{contains} >= 0 && $n->{contains_marks_evaluated}) || $n->{unevaluated_items} >= 0) ? 1 : 0;
  }
  return unless $edges;
  my @parents = map { [] } @$nodes;
  for my $i (0 .. $#$nodes) {
    my $n = $nodes->[$i];
    # not contributes no annotations: a node with one uses the edges without it.
    my $children = $n->{not_} >= 0 ? $n->in_place_children(0) : $edges->[$i];
    push @{ $parents[$_] }, $i for @$children;
  }
  for my $flag (qw(marks_properties marks_items)) {
    my @work = grep { $nodes->[$_]{$flag} } 0 .. $#$nodes;
    while (@work) {
      for my $p (@{ $parents[ pop @work ] }) {
        next if $nodes->[$p]{$flag};
        $nodes->[$p]{$flag} = 1;
        push @work, $p;
      }
    }
  }
}

# Marks the nodes on a cycle of in-place applicators (an iterative Tarjan), the only ones that need a depth guard.
sub _compute_in_place_cycles {
  my ($self, $edges) = @_;
  my $count = @{ $self->{nodes} };
  my @index = (-1) x $count;
  my @low = (0) x $count;
  my @on_stack = (0) x $count;
  my @stack;
  my $next_index = 0;
  for my $start (0 .. $count - 1) {
    next if $index[$start] >= 0;
    my @work = ([ $start, 0 ]);
    $index[$start] = $low[$start] = $next_index++;
    push @stack, $start;
    $on_stack[$start] = 1;
    while (@work) {
      my $frame = $work[-1];
      my ($v, $ei) = @$frame;
      if ($ei < @{ $edges->[$v] }) {
        $frame->[1]++;
        my $w = $edges->[$v][$ei];
        if ($index[$w] < 0) {
          $index[$w] = $low[$w] = $next_index++;
          push @stack, $w;
          $on_stack[$w] = 1;
          push @work, [ $w, 0 ];
        }
        elsif ($on_stack[$w]) {
          $low[$v] = $index[$w] if $index[$w] < $low[$v];
        }
      }
      else {
        pop @work;
        if (@work) {
          my $parent = $work[-1][0];
          $low[$parent] = $low[$v] if $low[$v] < $low[$parent];
        }
        if ($low[$v] == $index[$v]) {
          my @component;
          while (1) {
            my $w = pop @stack;
            $on_stack[$w] = 0;
            push @component, $w;
            last if $w == $v;
          }
          if (@component > 1 || grep { $_ == $v } @{ $edges->[$v] }) {
            $self->{nodes}[$_]{in_place_cycle} = 1 for @component;
          }
        }
      }
    }
  }
}

# Follows pure $ref nodes to the node that carries constraints.
sub effective_node {
  my ($self, $node_id) = @_;
  my $node = $self->{nodes}[$node_id];
  for (1 .. 16) {
    last unless $node->is_pure_ref;
    $node = $self->{nodes}[ $node->{ref} ];
  }
  return $node;
}

sub _compute_discriminators {
  my ($self) = @_;
  for my $n (@{ $self->{nodes} }) {
    $n->{one_of_discriminator} = $self->_build_discriminator($n->{one_of}) if $n->{one_of} && @{ $n->{one_of} } > 1;
    $n->{any_of_discriminator} = $self->_build_discriminator($n->{any_of}) if $n->{any_of} && @{ $n->{any_of} } > 1;
  }
}

# Classifies the branches by the constraint their properties[X] places on the value (positive: a const or enum of
# primitives; negative: a string not in an enum; wildcard: anything else), and builds the table of values to branches.
sub _build_discriminator {
  my ($self, $branches) = @_;
  my @candidates;
  for my $b (@$branches) {
    my $eff = $self->effective_node($b);
    next unless $eff->{properties};
    for my $name (@{ $eff->{property_order} }) {
      push @candidates, $name if $self->_classify($eff, $name)->[0] ne 'wildcard';
    }
    last;
  }
  for my $name (@candidates) {
    my @classes = map { $self->_classify($self->effective_node($_), $name) } @$branches;
    next if (grep { $_->[0] ne 'wildcard' } @classes) < 2;
    my @values;
    for my $c (@classes) {
      for my $v (@{ $c->[1] }) {
        push @values, $v unless grep { _same_primitive($v, $_) } @values;
      }
    }
    my @known;
    for my $value (@values) {
      my @selected;
      for my $i (0 .. $#classes) {
        my ($kind, $s) = @{ $classes[$i] };
        my $contains = (grep { _same_primitive($value, $_) } @$s) ? 1 : 0;
        push @selected, $i if $kind eq 'positive' ? $contains : $kind eq 'negative' ? !$contains : 1;
      }
      push @known, [ $value, \@selected ];
    }
    my @unknown = grep { $classes[$_][0] ne 'positive' } 0 .. $#classes;
    my $all_require = (List::Util::all {
      my $required = $self->effective_node($_)->{required};
      $required && grep { $_ eq $name } @$required
    } @$branches) ? 1 : 0;
    return { property => $name, known => \@known, unknown => \@unknown, all_require => $all_require };
  }
  return undef;
}

sub _classify {
  my ($self, $branch, $name) = @_;
  my $child = $branch->{properties} ? $branch->{properties}{$name} : undef;
  return [ 'wildcard', [] ] unless defined $child;
  my $p = $self->effective_node($child);
  return [ 'positive', [ $p->{const_value} ] ] if $p->{has_const} && _is_discriminator_primitive($p->{const_value});
  if ($p->{enum_values} && @{ $p->{enum_values} } && List::Util::all { _is_discriminator_primitive($_) }
    @{ $p->{enum_values} }) {
    return [ 'positive', [ @{ $p->{enum_values} } ] ];
  }
  if ($p->{not_} >= 0 && $p->{has_type} && $p->{type} == T_STRING) {
    my $not_node = $self->effective_node($p->{not_});
    if ($not_node->{enum_values} && (List::Util::all { _is_string($_) } @{ $not_node->{enum_values} })
      && !$not_node->{has_type} && !$not_node->{has_const} && !$not_node->has_string_keywords
      && !$not_node->has_in_place_applicators) {
      return [ 'negative', [ @{ $not_node->{enum_values} } ] ];
    }
  }
  return [ 'wildcard', [] ];
}

sub _is_discriminator_primitive {
  my ($v) = @_;
  return _is_string($v) || _is_number($v) || JSON::Schema::Corvus::Json::is_bool($v);
}

# JSON equality of two discriminator primitives (a boolean never equals a number).
sub _same_primitive {
  my ($a, $b) = @_;
  my $ta = JSON::Schema::Corvus::Json::type_of($a);
  my $tb = JSON::Schema::Corvus::Json::type_of($b);
  return 0 if $ta ne $tb;
  return ($a ? 1 : 0) == ($b ? 1 : 0) if $ta eq 'boolean';
  return $a == $b if $ta eq 'number';
  return $a eq $b;
}

# --- Annotations ---

# Fills every node's annotations (once), for results collection.
sub resolve_annotations {
  my ($program) = @_;
  my $sources = $program->{annotation_sources} or return;
  $program->{annotation_sources} = undef;
  my $assert_format_set = defined $program->{options}{assert_format};
  for my $node_id (0 .. $#$sources) {
    my $src = $sources->[$node_id] or next;
    $program->{nodes}[$node_id]{annotations} = _collect_annotations($src, $assert_format_set);
  }
}

# The annotation keywords of a schema object, in the order CompileNode records them.
sub _collect_annotations {
  my ($src, $assert_format_set) = @_;
  my ($e, $dialect, $vocab, $legacy, $content) = @$src{qw(e dialect vocab legacy content)};
  my $meta_data = $legacy || ($vocab & V_META_DATA);
  my $format_annotate = $legacy || ($vocab & (V_FORMAT_ANNOTATION | V_FORMAT_ASSERTION)) || $assert_format_set;
  my @out;
  my $add = sub { push @out, { keyword => $_[0], value => $e->{ $_[0] }, strings_only => $_[1] ? 1 : 0 } };
  for my $name (JSON::Schema::Corvus::Json::names($e)) {
    if ($name eq 'title' || $name eq 'description' || $name eq 'default') {
      $add->($name) if $meta_data;
    }
    elsif ($name eq 'examples') {
      $add->($name) if $meta_data && $dialect >= DRAFT6;
    }
    elsif ($name eq 'readOnly' || $name eq 'writeOnly') {
      $add->($name) if $meta_data && $dialect >= DRAFT7;
    }
    elsif ($name eq 'deprecated') {
      $add->($name) if $meta_data && $dialect >= DRAFT201909;
    }
    elsif ($name eq 'format') {
      $add->($name) if _is_string($e->{format}) && $format_annotate;
    }
    elsif ($dialect >= DRAFT201909 && !$KNOWN_KEYWORDS{$name}) {
      # Unknown keywords are collected as annotations from 2019-09 onwards.
      $add->($name);
    }
  }
  if ($content && $dialect >= DRAFT7
    && (exists $e->{contentEncoding} || exists $e->{contentMediaType} || exists $e->{contentSchema})) {
    $add->('contentEncoding', 1) if exists $e->{contentEncoding};
    if (exists $e->{contentMediaType}) {
      $add->('contentMediaType', 1);
      # contentSchema is meaningful only alongside contentMediaType.
      $add->('contentSchema', 1) if exists $e->{contentSchema} && $dialect >= DRAFT201909;
    }
  }
  return @out ? \@out : undef;
}

1;
