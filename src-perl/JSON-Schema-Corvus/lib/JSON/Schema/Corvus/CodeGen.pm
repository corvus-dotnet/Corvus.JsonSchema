package JSON::Schema::Corvus::CodeGen;

# Generates Perl from a compiled schema graph: a port of the Python package's codegen.py (itself a port of the
# TypeScript evaluator's generator). Each node becomes one sub whose body is exactly the checks that node needs: the
# C# evaluator's plan, unrolled into straight-line code. The analyses that pick C# plans pick the shape of the code:
# pure-$ref elision, type-only children tested in place, discriminator and type dispatch for oneOf and anyOf, flat
# object compositions merged into one section, unevaluatedProperties decided statically (with guards for conditional
# contributors), a dynamic scope only where a $dynamicRef stays dynamic, and evaluated-property or item tracking only
# where unevaluatedProperties or unevaluatedItems consume it.
#
# Where Perl differs from Python the code takes Perl's form: a type test is ref() for containers and booleans, and how
# a scalar was created for numbers and strings (see JSON::Schema::Corvus::Json); sets are hashes; an object's
# properties are visited with keys (never each, whose iterator a return would leave half way).

use 5.026;
use strict;
use warnings;
use List::Util ();
use JSON::Schema::Corvus::Dialect qw(DRAFT4 DRAFT6);
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Formats ();
use JSON::Schema::Corvus::Json ();
use JSON::Schema::Corvus::Node qw(:all);
use JSON::Schema::Corvus::Pattern ();

my @KINDS = qw(string number object array boolean null);

use constant { REF_START => "\x01", REF_END => "\x02" };

sub _env { my ($name, $fallback) = @_; my $v = $ENV{$name}; defined $v && $v =~ /\A[0-9]+\z/ ? 0 + $v : $fallback }

# Objects with at most this many declared properties (or only required ones) are checked by direct lookups.
my $MAX_PROBED_PROPERTIES = _env('CORVUS_PERL_PROBE', 8);
my $MAX_PROBED_REQUIRED = _env('CORVUS_PERL_PROBE_REQUIRED', 32);
# The number of declared names above which property dispatch goes through a hash of subs.
my $MAX_CHAIN_NAMES = _env('CORVUS_PERL_CHAIN', 4);
# Required names above which presence is tested with a loop over a list.
my $MAX_REQUIRED_TESTS = _env('CORVUS_PERL_REQUIRED_TESTS', 3);
# At most this many guarded coverages decide unevaluatedProperties statically; more falls back to tracking.
use constant MAX_GUARDED_COVERAGES => 32;
# A guarded coverage with more names than this tests them with a hash.
use constant MAX_GUARD_NAMES => 6;
# A leaf schema with at most this many keywords to test is tested at its call site instead of by a call.
my $MAX_INLINE_CONDITIONS = _env('CORVUS_PERL_INLINE', 6);
# Children that are small (non-leaf) schemas are generated into their caller, at most this deep and this long.
my $INLINE_CHILDREN = _env('CORVUS_PERL_INLINE_CHILDREN', 1);
my $MAX_INLINE_DEPTH = _env('CORVUS_PERL_INLINE_DEPTH', 2);
my $MAX_INLINE_LINES = _env('CORVUS_PERL_INLINE_LINES', 40);
# Children that are simple arrays (array type, length bounds, leaf items) are checked in their parent's code.
my $INLINE_ARRAYS = _env('CORVUS_PERL_INLINE_ARRAYS', 1);
# Enums of more values than this are tested by hash lookup.
my $MAX_CHAINED_VALUES = _env('CORVUS_PERL_CHAINED_VALUES', 3);

# The test of how a scalar was created: builtin::created_as_number, an op, where Perl has it (5.36 and later).
my $CAN = defined &builtin::created_as_number ? 'builtin::created_as_number'
  : 'JSON::Schema::Corvus::Json::_created_as_number';

sub kind_allowed {
  my ($mask, $kind) = @_;
  return ($mask & T_NULL) != 0 if $kind eq 'null';
  return ($mask & T_BOOLEAN) != 0 if $kind eq 'boolean';
  return ($mask & T_OBJECT) != 0 if $kind eq 'object';
  return ($mask & T_ARRAY) != 0 if $kind eq 'array';
  return ($mask & (T_NUMBER | T_INTEGER)) != 0 if $kind eq 'number';
  return ($mask & T_STRING) != 0;
}

sub _type { JSON::Schema::Corvus::Json::type_of($_[0]) }

sub _is_primitive {
  my ($x) = @_;
  my $t = _type($x);
  return 1 if $t eq 'null' || $t eq 'string' || $t eq 'boolean';
  return 1 if $t eq 'number' && (ref $x || ($x == $x && $x * 0 == 0));
  return 0;
}

# A Perl literal for a string.
sub perl_string {
  my ($s) = @_;
  if ($s !~ /[^\x20-\x7e]/) {
    (my $q = $s) =~ s/([\\'])/\\$1/g;
    return "'$q'";
  }
  no warnings qw(surrogate nonchar non_unicode);
  my $q = join '', map {
    my $c = ord $_;
    $c < 0x20 || $c > 0x7e ? sprintf('\x{%x}', $c) : $_ eq '\\' ? '\\\\' : $_ eq '"' ? '\\"' : $_ eq '$' ? '\\$'
      : $_ eq '@' ? '\\@' : $_
  } split //, $s;
  return qq{"$q"};
}

# A Perl literal for a number: an integer exactly, a floating point number as its shortest text that reads back.
sub perl_number {
  my ($self, $v) = @_;
  return $self->constant('Math::BigInt->new(' . perl_string("$v") . ')') if ref $v;
  if (JSON::Schema::Corvus::Json::is_integer($v, 1)) {
    my $copy = $v;
    my $text = "$copy";
    return $text if $text =~ /\A-?[0-9]+\z/;
  }
  # A floating point number as a constant with its exact bits: Perl's reading of a decimal literal is not correctly
  # rounded on every version, so a literal could come back as a neighbouring double.
  return $self->constant("unpack('d', pack('H*', '" . unpack('H*', pack('d', $v)) . "'))");
}

sub _wrap {
  my ($expr) = @_;
  return $expr if $expr =~ /\A[\$\w:\x01\x02]+(?:\([^()]*\))?\z/ || $expr =~ /\A-?[0-9.]+\z/;
  return $expr if substr($expr, 0, 1) eq '(' && _balanced($expr);
  return "($expr)";
}

sub _balanced {
  my ($expr) = @_;
  my $depth = 0;
  my $in_string = '';
  my @chars = split //, $expr;
  for my $i (0 .. $#chars) {
    my $ch = $chars[$i];
    if ($in_string) {
      if ($ch eq '\\') { $i++; next }
      $in_string = '' if $ch eq $in_string;
      next;
    }
    if ($ch eq "'" || $ch eq '"') { $in_string = $ch; next }
    if ($ch eq '(') { $depth++ }
    elsif ($ch eq ')') {
      $depth--;
      return 0 if $depth == 0 && $i < $#chars;
    }
  }
  return $depth == 0;
}

sub _indent {
  my ($lines, $depth) = @_;
  my $pad = '  ' x ($depth // 1);
  return map { "$pad$_" } @$lines;
}

# "return 0 unless CHECK" (nothing for a check that always holds).
sub _push {
  my ($out, $check) = @_;
  return if $check eq '1';
  if ($check eq '0') { push @$out, 'return 0;' }
  else { push @$out, "return 0 unless $check;" }
}

sub new {
  my ($class, $program) = @_;
  return bless {
    program => $program,
    nodes => $program->{nodes},
    templates => {},
    template_order => [],
    queue => [],
    constants => {},
    constant_order => [],
    custom_formats => [],
    custom_format_index => {},
    uses_depth => 0,
    tmp => 0,
    uses_tx => 0,
    elided => {},
    generating => [],
    inline_depth => 0,
    trivial => {},
    generated => {},
  }, $class;
}

# The generated code: { functions => [[name, text]], constants => [[name, init]], root => name, custom_formats,
# uses_dynamic_scope, uses_depth, root_resource, max_depth }.
sub generate {
  my ($self) = @_;
  my $root_name = $self->request($self->entry_name($self->{program}{root}));
  for (my $q = 0; $q < @{ $self->{queue} }; $q++) {
    my $name = $self->{queue}[$q];
    $self->{templates}{$name} = $self->generate_function($name);
  }
  my ($functions, $constants, $root) = $self->link($root_name);
  return {
    functions => $functions,
    constants => $constants,
    root => $root,
    custom_formats => $self->{custom_formats},
    uses_dynamic_scope => $self->{program}{uses_dynamic_scope},
    uses_depth => $self->{uses_depth},
    root_resource => $self->{program}{root_resource},
    max_depth => $self->{program}{options}{max_depth},
  };
}

# --- Names and references ---

sub entry_name { 'v' . $_[0]->elide($_[1]) }

sub request {
  my ($self, $name) = @_;
  if (!exists $self->{templates}{$name}) {
    $self->{templates}{$name} = '';
    push @{ $self->{template_order} }, $name;
    push @{ $self->{queue} }, $name;
  }
  return $name;
}

sub ref_to { REF_START . $_[0]->request($_[1]) . REF_END }

# A constant of the generated package, by its initialiser: its variable, such as $C3.
sub constant {
  my ($self, $init) = @_;
  my $name = $self->{constants}{$init};
  if (!defined $name) {
    $name = '$C' . scalar @{ $self->{constant_order} };
    $self->{constants}{$init} = $name;
    push @{ $self->{constant_order} }, $init;
  }
  return $name;
}

# Follows pure-$ref chains (jump-target inlining); not across resources when a dynamic scope is kept.
sub elide {
  my ($self, $node_id) = @_;
  return $self->{elided}{$node_id} if exists $self->{elided}{$node_id};
  my $n = $self->{nodes}[$node_id];
  for (1 .. 64) {
    last if !$n->is_pure_ref || $n->{in_place_cycle};
    my $next = $self->{nodes}[ $n->{ref} ];
    last if $self->{program}{uses_dynamic_scope} && $next->{resource_id} != $n->{resource_id};
    $n = $next;
  }
  return $self->{elided}{$node_id} = $n->{id};
}

sub _tx {
  my ($self, $v) = @_;
  if ($v eq '$x') {
    $self->{uses_tx} = 1;
    return '$tx';
  }
  return "ref($v)";
}

sub kind_test {
  my ($self, $kind, $v) = @_;
  return "!defined($v)" if $kind eq 'null';
  my $t = $self->_tx($v);
  return "$t eq 'JSON::PP::Boolean'" if $kind eq 'boolean';
  return "$t eq 'HASH'" if $kind eq 'object';
  return "$t eq 'ARRAY'" if $kind eq 'array';
  if ($kind eq 'number') {
    return "($t eq '' ? defined($v) && $CAN($v) : $t eq 'Math::BigInt')";
  }
  return "($t eq '' && defined($v) && !$CAN($v))";
}

# A test that a number (known to be one) is an integer: a whole number from draft 6; in draft 4, one written without
# a fraction or an exponent.
sub _integer_test {
  my ($self, $v, $dialect) = @_;
  return "JSON::Schema::Corvus::Json::is_integer($v, 1)" if $dialect == DRAFT4;
  return "(ref($v) || $v == int($v))";
}

# An expression that is true when v has one of the types in the mask.
sub type_expr {
  my ($self, $mask, $v, $dialect) = @_;
  my @parts;
  for my $kind (qw(string object array boolean null)) {
    next unless kind_allowed($mask, $kind);
    push @parts, $self->kind_test($kind, $v);
  }
  if ($mask & T_NUMBER) {
    push @parts, $self->kind_test('number', $v);
  }
  elsif ($mask & T_INTEGER) {
    push @parts, '(' . $self->kind_test('number', $v) . ' && ' . $self->_integer_test($v, $dialect) . ')';
  }
  return '0' unless @parts;
  return @parts == 1 ? $parts[0] : '(' . join(' || ', @parts) . ')';
}

# An expression that evaluates child node_id against v from a node in resource source. e is the evaluated set to mark
# (tracking variant), or undef (flag variant).
sub call {
  my ($self, $node_id, $v, $source, $e) = @_;
  my $target = $self->elide($node_id);
  my $n = $self->{nodes}[$target];
  return '1' if $n->{always_true};
  return '0' if $n->{always_false};
  my $crosses = $self->{program}{uses_dynamic_scope} && $n->{resource_id} != $source;
  if (!defined $e && !$crosses) {
    return $self->type_expr($n->{type}, $v, $n->{dialect}) if $n->is_type_only;
    return $self->inline_leaf($n, $v) if $self->is_inline_leaf($n);
    return '1' if $self->is_trivial($target);
  }
  my $variant = defined $e ? 't' : 'v';
  my $name = ($crosses ? 's' : '') . $variant . $target;
  return $self->ref_to($name) . "($v" . (defined $e ? ", $e" : '') . ')';
}

# A reference to the flag sub of child node_id (for dispatch tables), or '1' for a schema that accepts everything.
sub function_ref {
  my ($self, $node_id, $source) = @_;
  my $target = $self->elide($node_id);
  my $n = $self->{nodes}[$target];
  return '1' if $n->{always_true} || $self->call($node_id, '$v', $source) eq '1';
  return '\&JSON::Schema::Corvus::Runtime::never' if $n->{always_false};
  my $crosses = $self->{program}{uses_dynamic_scope} && $n->{resource_id} != $source;
  return '\&' . $self->ref_to(($crosses ? 'sv' : 'v') . $target);
}

# A leaf (type, const or enum of primitives, string and number keywords, nothing that applies a subschema) small
# enough to test at the call site, without a call.
sub is_inline_leaf {
  my ($self, $n) = @_;
  return 0 if $n->has_object_keywords || $n->has_array_keywords || $n->has_in_place_applicators || $n->{in_place_cycle};
  return 0 if $n->{has_const} && !_is_primitive($n->{const_value});
  return 0 if $n->{enum_values} && grep { !_is_primitive($_) } @{ $n->{enum_values} };
  my $conditions = $self->_string_condition_count($n) + ($n->{has_const} ? 1 : 0) + (defined $n->{enum_values} ? 1 : 0);
  my $numbers = grep { defined } @$n{qw(minimum maximum exclusive_minimum exclusive_maximum multiple_of)};
  return $conditions + $numbers <= $MAX_INLINE_CONDITIONS;
}

sub _string_condition_count {
  my ($self, $n) = @_;
  return scalar grep { $_ } ($n->{min_length} > 0, $n->{max_length} >= 0, defined $n->{pattern},
    $n->{assert_format} && defined $n->{format} && !JSON::Schema::Corvus::Formats::is_numeric_format($n->{format_kind}),
    $n->{assert_content});
}

# A leaf's keywords as one expression over v: per kind, its type test and the conditions for that kind, then const
# and enum.
sub inline_leaf {
  my ($self, $n, $v) = @_;
  my %sections = (
    string => [ $self->string_conditions($n, $v) ],
    number => [ (grep { defined } $self->integer_condition($n, $v)), $self->number_conditions($n, $v) ],
  );
  my @parts;
  if ($n->{has_type}) {
    my @clauses;
    for my $kind (@KINDS) {
      next unless kind_allowed($n->{type}, $kind);
      my $test = $self->kind_test($kind, $v);
      my $conditions = $sections{$kind} // [];
      push @clauses, @$conditions ? '(' . join(' && ', $test, @$conditions) . ')' : $test;
    }
    return '0' unless @clauses;
    push @parts, @clauses == 1 ? $clauses[0] : '(' . join(' || ', @clauses) . ')';
  }
  else {
    for my $kind (qw(string number)) {
      my $conditions = $sections{$kind};
      next unless @$conditions;
      push @parts, '(!' . _wrap($self->kind_test($kind, $v)) . ' || ' . join(' && ', map { _wrap($_) } @$conditions)
        . ')';
    }
  }
  push @parts, $self->eq_test($v, $n->{const_value}) if $n->{has_const};
  push @parts, $self->enum_test($n->{enum_values}, $v) if defined $n->{enum_values};
  return '1' unless @parts;
  return @parts == 1 ? $parts[0] : '(' . join(' && ', @parts) . ')';
}

# A test that v is JSON-equal to the primitive value.
sub eq_test {
  my ($self, $v, $value) = @_;
  my $t = _type($value);
  return "!defined($v)" if $t eq 'null';
  return "(ref($v) eq 'JSON::PP::Boolean' && " . ($value ? $v : "!$v") . ')' if $t eq 'boolean';
  if ($t eq 'string') {
    return '(' . $self->kind_test('string', $v) . " && $v eq " . perl_string($value) . ')';
  }
  return '(' . $self->kind_test('number', $v) . " && $v == " . $self->perl_number($value) . ')';
}

# A test that v is one of a list of primitives: a hash lookup for more than a few strings, a chain of tests otherwise.
sub enum_test {
  my ($self, $values, $v) = @_;
  return '0' unless @$values;
  if (@$values > $MAX_CHAINED_VALUES && !grep { _type($_) ne 'string' } @$values) {
    my $set = $self->constant('{' . join(', ', map { perl_string($_) . ' => 1' } @$values) . '}');
    return '(' . $self->kind_test('string', $v) . " && exists $set\->{$v})";
  }
  if (@$values > $MAX_CHAINED_VALUES) {
    my $keys = $self->constant('{ map { JSON::Schema::Corvus::Runtime::key($_) => 1 } @{ '
      . $self->json_value($values) . ' } }');
    return "exists $keys\->{JSON::Schema::Corvus::Runtime::key($v)}";
  }
  return '(' . join(' || ', map { $self->eq_test($v, $_) } @$values) . ')';
}

# The initialiser of a JSON value constant.
sub json_value {
  my ($self, $value) = @_;
  return 'JSON::Schema::Corvus::Json::decode(' . perl_string(JSON::Schema::Corvus::Json::encode($value)) . ')';
}

sub json_constant { $_[0]->constant($_[0]->json_value($_[1])) }

sub scratch {
  my ($self, $prefix) = @_;
  return '$' . $prefix . $self->{tmp}++;
}

# --- Functions ---

sub _params { $_[0] eq 'v' ? 'my $x = $_[0];' : 'my ($x, $ev) = @_;' }

sub generate_function {
  my ($self, $name) = @_;
  my $kind = substr($name, 0, 1);
  if ($kind eq 's') {
    my $variant = substr($name, 1, 1);
    my $node_id = substr($name, 2);
    my $n = $self->{nodes}[$node_id];
    my $args = $variant eq 'v' ? '$x' : '$x, $ev';
    return join "\n", 'sub @ {', '  ' . _params($variant), "  push \@DS, $n->{resource_id};",
      '  my $r = ' . $self->ref_to($variant . $node_id) . "($args);", '  pop @DS;', '  return $r;', '}';
  }
  return $self->generate_dynamic_resolver(substr($name, 2), substr($name, 1, 1)) if $kind eq 'd';
  return $self->generate_node_function(substr($name, 2), substr($name, 1, 1)) if $kind eq 'b';
  my $variant = $kind;
  my $node_id = substr($name, 1);
  my $n = $self->{nodes}[$node_id];
  if ($n->{in_place_cycle}) {
    $self->{uses_depth} = 1;
    my $args = $variant eq 'v' ? '$x' : '$x, $ev';
    return join "\n", 'sub @ {', '  ' . _params($variant),
      '  JSON::Schema::Corvus::Runtime::depth_exceeded() if ++$depth > $MAXDEPTH;',
      '  my $r = ' . $self->ref_to("b$variant$node_id") . "($args);", '  $depth--;', '  return $r;', '}';
  }
  return $self->generate_node_function($node_id, $variant);
}

sub generate_dynamic_resolver {
  my ($self, $node_id, $variant) = @_;
  my $n = $self->{nodes}[$node_id];
  my $d = $n->{dynamic_ref};
  my $e = $variant eq 't' ? '$ev' : undef;
  $self->{uses_tx} = 0;
  my @lines = ('for my $r (@DS) {');
  for my $resource (sort { $a <=> $b } keys %{ $d->{by_resource} }) {
    push @lines, "  return " . $self->call($d->{by_resource}{$resource}, '$x', $n->{resource_id}, $e)
      . " if \$r == $resource;";
  }
  push @lines, '}', 'return ' . $self->call($d->{fallback}, '$x', $n->{resource_id}, $e) . ';';
  unshift @lines, 'my $tx = ref($x);' if $self->{uses_tx};
  return join "\n", 'sub @ {', '  ' . _params($variant), _indent(\@lines), '}';
}

sub generate_node_function {
  my ($self, $node_id, $variant) = @_;
  # A node's text depends only on the node and the inlining depth (which names nested inlines).
  my $key = "$node_id $variant $self->{inline_depth}";
  return $self->{generated}{$key} //= $self->_generate_node_function($node_id, $variant);
}

sub _generate_node_function {
  my ($self, $node_id, $variant) = @_;
  my $n = $self->{nodes}[$node_id];
  # Scratch names restart per function so that structurally identical functions produce identical text.
  $self->{tmp} = 0;
  $self->{uses_tx} = 0;
  push @{ $self->{generating} }, $node_id;
  my $tracking = $variant eq 't' || $n->{unevaluated_properties} >= 0 || $n->{unevaluated_items} >= 0;
  my $body = $tracking ? $self->tracking_body($n, $variant) : $self->flag_body($n);
  pop @{ $self->{generating} };
  unshift @$body, 'my $tx = ref($x);' if $self->{uses_tx};
  return join "\n", 'sub @ {', '  ' . _params($variant), _indent($body), '}';
}

# Whether a node accepts everything without testing anything (no assertion of its own, and only $ref and allOf to
# nodes that are themselves trivial), so a call to it can be dropped.
sub is_trivial {
  my ($self, $node_id) = @_;
  return $self->{trivial}{$node_id} if exists $self->{trivial}{$node_id};
  $self->{trivial}{$node_id} = 0;
  my $n = $self->{nodes}[$node_id];
  my @children = map { $self->elide($_) } grep { $_ >= 0 } $n->{ref}, $n->{static_dynamic_ref}, @{ $n->{all_of} // [] };
  my $crosses = $self->{program}{uses_dynamic_scope}
    && grep { $self->{nodes}[$_]{resource_id} != $n->{resource_id} } @children;
  my $trivial = $n->{always_true} || (!$crosses && !$n->{always_false} && !$n->{in_place_cycle} && !$n->{has_type}
    && !$n->{has_const} && !defined $n->{enum_values} && !$n->has_object_keywords && !$n->has_array_keywords
    && !$n->has_string_keywords && !$n->has_number_keywords && !defined $n->{dynamic_ref} && !defined $n->{any_of}
    && !defined $n->{one_of} && $n->{not_} < 0 && $n->{if_} < 0 && !defined $n->{dependencies}
    && !grep { !$self->is_trivial($_) } @children);
  return $self->{trivial}{$node_id} = $trivial ? 1 : 0;
}

# The statements of a child's sub, to run in place of "return 0 unless child(subject)", or undef when the child is not
# worth or not safe to inline. The child's body is generated as usual, its locals are renamed (its subject becomes the
# subject, every other name gets a suffix for the inlining depth, string literals and sub references untouched) and its
# final "return 1" dropped: its "return 0" fails the caller, as the call it replaces would.
my $LOCAL_RE = qr/('(?:[^'\\]|\\.)*'|"(?:[^"\\]|\\.)*"|\x01[^\x02]*\x02)|\$(x|tx|p|v|k|f|w|i|r|(?:[nmesgdcl]|any|one)\d+(?:_\d+)?)\b/;

sub inline_child {
  my ($self, $node_id, $subject, $source) = @_;
  return undef if !$INLINE_CHILDREN || $self->{inline_depth} >= $MAX_INLINE_DEPTH;
  my $target = $self->elide($node_id);
  my $n = $self->{nodes}[$target];
  my $crosses = $self->{program}{uses_dynamic_scope} && $n->{resource_id} != $source;
  return undef if $crosses || $n->{always_true} || $n->{always_false} || $n->{in_place_cycle}
    || $n->{unevaluated_properties} >= 0 || $n->{unevaluated_items} >= 0 || (grep { $_ == $target } @{ $self->{generating} })
    || $n->is_type_only || $self->is_inline_leaf($n);
  my @saved = @$self{qw(tmp uses_tx)};
  $self->{inline_depth}++;
  my $text = $self->generate_node_function($target, 'v');
  $self->{inline_depth}--;
  @$self{qw(tmp uses_tx)} = @saved;
  my @lines = split /\n/, $text;
  # sub @ {, my $x = $_[0];, the body, }
  my @body = map { substr($_, 2) } @lines[ 2 .. $#lines - 1 ];
  return undef if @body > $MAX_INLINE_LINES || @body < 2 || $body[-1] ne 'return 1;';
  return undef if grep { /return 1|\bnext\b|\blast\b/ } @body[ 0 .. $#body - 1 ];
  my $suffix = '_' . ($self->{inline_depth} + 1);
  (my $bare = $subject) =~ s/\A\$//;
  for (@body[ 0 .. $#body - 1 ]) {
    s/$LOCAL_RE/defined $1 ? $1 : $2 eq 'x' ? "\$$bare" : "\$$2$suffix"/ge;
  }
  pop @body;
  return \@body;
}

# Flag mode: type-directed blocks, then const and enum, then the in-place applicators.
sub flag_body {
  my ($self, $n) = @_;
  return ['return 0;'] if $n->{always_false};
  return ['return 1;'] if $n->{always_true};
  my @out;
  my $flat = $self->flat_object($n);
  if ($flat) {
    # Objects take the merged section and return; the code below still serves every other kind.
    push @out, 'if (' . $self->kind_test('object', '$x') . ') {';
    push @out, _indent([ @{ $self->object_section($flat, undef, 1) }, 'return 1;' ]);
    push @out, '}';
  }
  push @out, @{ $self->kind_blocks($n, sub { $self->kind_section($n, $_[0], undef, 1) }) };
  push @out, @{ $self->const_enum($n) };
  push @out, @{ $self->in_place($n, undef, undef) };
  push @out, 'return 1;';
  return \@out;
}

# A flat composition, for object values: a node whose in-place applicators are $ref and allOf chains, where the node
# and every branch are plain object schemas (declared properties, required and count bounds, no type that excludes
# objects) and each property name resolves to one schema. Returns a node holding the merged object keywords (the C#
# and Rust flat fused plan), or undef.
sub flat_object {
  my ($self, $n) = @_;
  return undef if $n->{ref} < 0 && $n->{static_dynamic_ref} < 0 && !defined $n->{all_of};
  my @branches;
  my %visited;
  my $collect;
  $collect = sub {
    my ($m) = @_;
    return 1 if $visited{ $m->{id} }++;
    return 1 if $m->{always_true};
    return 0 if $m->{always_false} || $m->{in_place_cycle};
    return 0 if $self->{program}{uses_dynamic_scope} && $m->{resource_id} != $n->{resource_id};
    return 0 if $m->{has_const} || defined $m->{enum_values} || ($m->{has_type} && !kind_allowed($m->{type}, 'object'));
    return 0 if defined $m->{pattern_properties} || $m->{additional_properties} >= 0 || $m->{property_names} >= 0
      || defined $m->{dependencies};
    return 0 if $m->{unevaluated_properties} >= 0 || $m->{unevaluated_items} >= 0;
    return 0 if defined $m->{dynamic_ref} || defined $m->{any_of} || defined $m->{one_of} || $m->{not_} >= 0
      || $m->{if_} >= 0;
    push @branches, $m;
    for my $c ($m->{ref}, $m->{static_dynamic_ref}, @{ $m->{all_of} // [] }) {
      return 0 if $c >= 0 && !$collect->($self->{nodes}[ $self->elide($c) ]);
    }
    return 1;
  };
  my $ok = $collect->($n);
  undef $collect;
  return undef unless $ok;
  my @effective = grep { defined $_->{properties} || ($_->{required} && @{ $_->{required} })
    || $_->{min_properties} >= 0 || $_->{max_properties} >= 0 } @branches;
  return undef if @effective < 2;
  my $same = sub {
    my ($a, $b) = @_;
    my $x = $self->{nodes}[ $self->elide($a) ];
    my $y = $self->{nodes}[ $self->elide($b) ];
    return $x->{id} == $y->{id} || ($x->is_type_only && $y->is_type_only && $x->{type} == $y->{type}
      && $x->{dialect} == $y->{dialect});
  };
  my $merged = JSON::Schema::Corvus::Node->new(id => $n->{id}, resource_id => $n->{resource_id},
    dialect => $n->{dialect}, location => $n->{location}, pointer => $n->{pointer});
  my (%properties, @order, @required, %required);
  for my $b (@branches) {
    for my $name (@{ $b->{property_order} // [] }) {
      my $child = $b->{properties}{$name};
      my $existing = $properties{$name};
      if (!defined $existing || $self->{nodes}[ $self->elide($existing) ]{always_true}) {
        push @order, $name unless defined $existing;
        $properties{$name} = $child;
      }
      elsif (!$same->($existing, $child) && !$self->{nodes}[ $self->elide($child) ]{always_true}) {
        return undef;
      }
    }
    for my $name (@{ $b->{required} // [] }) {
      push @required, $name unless $required{$name}++;
    }
    $merged->{min_properties} = $b->{min_properties} if $b->{min_properties} > $merged->{min_properties};
    if ($b->{max_properties} >= 0) {
      $merged->{max_properties} = $merged->{max_properties} < 0 || $b->{max_properties} < $merged->{max_properties}
        ? $b->{max_properties} : $merged->{max_properties};
    }
  }
  if (%properties) {
    $merged->{properties} = \%properties;
    $merged->{property_order} = \@order;
  }
  $merged->{required} = \@required if @required;
  return $merged;
}

# The per-kind sections: kinds with work get a branch, allowed kinds without work fall through, and a type restriction
# fails every other kind.
sub kind_blocks {
  my ($self, $n, $section) = @_;
  my $mask = $n->{has_type} ? $n->{type} : T_ALL;
  my @allowed = grep { kind_allowed($mask, $_) } @KINDS;
  return ['return 0;'] unless @allowed;
  my @with_work = grep { @{ $_->[1] } } map { [ $_, $section->($_) ] } @allowed;
  my @out;
  if ($n->{has_type} && @allowed == 1) {
    push @out, 'return 0 unless ' . _wrap($self->kind_test($allowed[0], '$x')) . ';';
    push @out, @{ $with_work[0][1] } if @with_work;
    return \@out;
  }
  my %worked = map { $_->[0] => 1 } @with_work;
  my @rest_ok = grep { !$worked{$_} } @allowed;
  for my $i (0 .. $#with_work) {
    my ($k, $lines) = @{ $with_work[$i] };
    push @out, ($i == 0 ? 'if (' : '} elsif (') . $self->kind_test($k, '$x') . ') {';
    push @out, _indent($lines);
  }
  my $restricted = $n->{has_type} && @allowed < @KINDS;
  if (@with_work) {
    if ($restricted) {
      if (!@rest_ok) {
        push @out, '} else {';
      }
      else {
        push @out, '} elsif (!(' . join(' || ', map { $self->kind_test($_, '$x') } @rest_ok) . ')) {';
      }
      push @out, '  return 0;';
    }
    push @out, '}';
  }
  elsif ($restricted) {
    push @out, 'return 0 unless ' . join(' || ', map { $self->kind_test($_, '$x') } @rest_ok) . ';';
  }
  return \@out;
}

sub kind_section {
  my ($self, $n, $kind, $e, $flag_layout) = @_;
  my $integer_only = $n->{has_type} && ($n->{type} & T_INTEGER) && !($n->{type} & T_NUMBER);
  return $self->object_section($n, $e, $flag_layout) if $kind eq 'object';
  return $self->array_section($n, $e) if $kind eq 'array';
  return $self->string_section($n) if $kind eq 'string';
  if ($kind eq 'number') {
    my @lines;
    push @lines, 'return 0 unless ' . $self->_integer_test('$x', $n->{dialect}) . ';' if $integer_only;
    return [ @lines, @{ $self->number_section($n) } ];
  }
  return [];
}

sub const_enum {
  my ($self, $n) = @_;
  my @out;
  if ($n->{has_const}) {
    if (_is_primitive($n->{const_value})) {
      push @out, 'return 0 unless ' . _wrap($self->eq_test('$x', $n->{const_value})) . ';';
    }
    else {
      push @out, 'return 0 unless JSON::Schema::Corvus::Runtime::equal($x, ' . $self->json_constant($n->{const_value})
        . ');';
    }
  }
  if (defined $n->{enum_values}) {
    my $values = $n->{enum_values};
    if (!@$values) {
      push @out, 'return 0;';
    }
    elsif (!grep { !_is_primitive($_) } @$values) {
      push @out, 'return 0 unless ' . _wrap($self->enum_test($values, '$x')) . ';';
    }
    else {
      push @out, 'return 0 unless JSON::Schema::Corvus::Runtime::includes(' . $self->json_constant($values) . ', $x);';
    }
  }
  return \@out;
}

# --- Objects ---

sub object_section {
  my ($self, $n, $e, $flag_layout) = @_;
  my @out;
  my $props = $n->{properties};
  my @names = @{ $n->{property_order} // [] };
  my $required = $n->{required} // [];
  my $all_patterns = $n->{pattern_properties} // [];
  my $additional_needs_match = $n->{additional_properties} >= 0
    && !$self->{nodes}[ $self->elide($n->{additional_properties}) ]{always_true};
  # A pattern property whose schema is true does nothing unless marking or additionalProperties needs it.
  my @patterns = defined $e || $additional_needs_match ? @$all_patterns
    : grep { !$self->{nodes}[ $self->elide($_->{node}) ]{always_true} } @$all_patterns;
  my $ap = $n->{additional_properties} >= 0 ? $self->{nodes}[ $self->elide($n->{additional_properties}) ] : undef;
  my $pn = $n->{property_names} >= 0 ? $self->{nodes}[ $self->elide($n->{property_names}) ] : undef;
  my $ap_needs_loop = $ap && !$ap->{always_true};
  my $pn_needs_loop = $pn && !$pn->{always_true};
  my %required = map { $_ => 1 } @$required;
  my $all_required = !$props || !grep { !$required{$_} } @names;
  my $probeable = @names <= $MAX_PROBED_PROPERTIES || ($all_required && @names <= $MAX_PROBED_REQUIRED);

  # Counts and required names first: they fail fast without looking at any value.
  push @out, "return 0 if keys(%\$x) < $n->{min_properties};" if $n->{min_properties} > 0;
  push @out, "return 0 if keys(%\$x) > $n->{max_properties};" if $n->{max_properties} >= 0;
  push @out, @{ $self->required_test('$x', $required) };

  my $simple = !defined $e && !@patterns && !$pn_needs_loop;
  if ($simple && !$ap_needs_loop && $probeable) {
    push @out, @{ $self->object_probe($n, \@names, $required) };
  }
  elsif ($simple && $ap && $ap->{always_false} && $probeable) {
    # additionalProperties: false: the names must all be declared, then each declared name is probed.
    if (@names) {
      my $set = $self->constant('{' . join(', ', map { perl_string($_) . ' => 1' } @names) . '}');
      push @out, "for (keys %\$x) { return 0 unless exists $set\->{\$_} }";
    }
    else {
      push @out, 'return 0 if %$x;';
    }
    push @out, @{ $self->object_probe($n, \@names, $required) };
  }
  elsif ($simple && !@names && $ap && $ap_needs_loop) {
    push @out, @{ $self->object_values($n, $ap) };
  }
  else {
    push @out, @{ $self->object_loop($n, $e, \@patterns, $ap, $pn, $ap_needs_loop, $pn_needs_loop) };
  }
  push @out, @{ $self->object_dependencies($n, $e, $flag_layout) } if $n->{dependencies};
  return \@out;
}

sub required_test {
  my ($self, $v, $required) = @_;
  return [] unless @$required;
  if (@$required <= $MAX_REQUIRED_TESTS) {
    return [ 'return 0 if ' . join(' || ', map { '!exists ' . $v . '->{' . perl_string($_) . '}' } @$required) . ';' ];
  }
  my $names = $self->constant('[' . join(', ', map { perl_string($_) } @$required) . ']');
  return [ "for ($names\->\@*) { return 0 unless exists $v\->{\$_} }" ];
}

# Each declared name looked up directly (required names are already known to be present).
sub object_probe {
  my ($self, $n, $names, $required) = @_;
  my @out;
  my %required = map { $_ => 1 } @$required;
  for my $name (@$names) {
    my $child = $n->{properties}{$name};
    my $check = $self->call($child, '$p', $n->{resource_id});
    next if $check eq '1';
    my $key = perl_string($name);
    my $nested = $INLINE_ARRAYS ? $self->simple_array_lines($self->{nodes}[ $self->elide($child) ], '$p', '$w',
      $n->{resource_id}) : undef;
    my $lines;
    if ($nested) {
      $lines = $nested;
    }
    elsif ($check eq '0') {
      push @out, "return 0 if exists \$x->{$key};";
      next;
    }
    else {
      my $inlined = $self->inline_child($child, '$p', $n->{resource_id});
      $lines = $inlined // [ 'return 0 unless ' . _wrap($check) . ';' ];
    }
    if ($required{$name}) {
      push @out, '{', "  my \$p = \$x->{$key};", _indent($lines), '}';
    }
    else {
      push @out, "if (exists \$x->{$key}) {", "  my \$p = \$x->{$key};", _indent($lines), '}';
    }
  }
  return \@out;
}

# A map: only the values matter.
sub object_values {
  my ($self, $n, $ap) = @_;
  my $check = $self->call($ap->{id}, '$v', $n->{resource_id});
  return ['return 0 if %$x;'] if $check eq '0';
  return [ 'for my $v (values %$x) {', '  return 0 unless ' . _wrap($check) . ';', '}' ];
}

# One pass over the instance's properties: names dispatched to their subschemas, then patterns and additional.
sub object_loop {
  my ($self, $n, $e, $patterns, $ap, $pn, $ap_needs_loop, $pn_needs_loop) = @_;
  my @out;
  my @names = @{ $n->{property_order} // [] };
  my $props = $n->{properties} // {};
  my $needs_value = @names || @$patterns || $ap_needs_loop;
  my @body;
  push @body, 'my $v = $x->{$k};' if $needs_value;
  push @body, 'return 0 unless ' . _wrap($self->call($pn->{id}, '$k', $n->{resource_id})) . ';' if $pn_needs_loop;
  my $mark = defined $e ? "$e\->{\$k} = 1;" : undef;
  my $has_ap_tail = $ap && ($ap_needs_loop || defined $mark);
  # When nothing follows the declared names (no patterns), a declared name goes on to the next property.
  my $matched = @$patterns && $has_ap_tail ? $self->scratch('m') : undef;
  push @body, "my $matched = 0;" if $matched;
  if (@names) {
    my $continue_after = !@$patterns;
    my $case_body = sub {
      my ($name) = @_;
      my @lines;
      my $check = $self->call($props->{$name}, '$v', $n->{resource_id});
      if ($check ne '1') {
        my $inlined = $self->inline_child($props->{$name}, '$v', $n->{resource_id});
        push @lines, @{ $inlined // [ 'return 0 unless ' . _wrap($check) . ';' ] };
      }
      push @lines, $mark if $mark;
      push @lines, "$matched = 1;" if $matched;
      push @lines, 'next;' if $continue_after && $has_ap_tail;
      return \@lines;
    };
    if (@names <= $MAX_CHAIN_NAMES) {
      for my $i (0 .. $#names) {
        push @body, ($i == 0 ? 'if' : '} elsif') . ' ($k eq ' . perl_string($names[$i]) . ') {';
        push @body, _indent($case_body->($names[$i]));
      }
      push @body, '}';
    }
    else {
      # A hash from names to subs: one lookup instead of a chain of comparisons.
      my ($table, $trivial) = $self->dispatch_table($n, \@names);
      push @body, "if (my \$f = $table\->{\$k}) {";
      my @lines;
      push @lines, 'return 0 unless ' . ($trivial ? '!ref($f) || ' : '') . '$f->($v);';
      push @lines, $mark if $mark;
      push @lines, "$matched = 1;" if $matched;
      push @lines, 'next;' if $continue_after && $has_ap_tail;
      push @body, _indent(\@lines), '}';
    }
  }
  for my $p (@$patterns) {
    my $check = $self->call($p->{node}, '$v', $n->{resource_id});
    my @lines;
    push @lines, 'return 0 unless ' . _wrap($check) . ';' if $check ne '1';
    push @lines, $mark if $mark;
    push @lines, "$matched = 1;" if $matched;
    if (@lines) {
      push @body, 'if (' . $self->pattern_test($p->{pattern}, '$k') . ') {', _indent(\@lines), '}';
    }
  }
  if ($ap && ($ap_needs_loop || $mark)) {
    my @lines;
    my $check = $self->call($ap->{id}, '$v', $n->{resource_id});
    if ($check eq '0') { push @lines, 'return 0;' }
    elsif ($check ne '1') { push @lines, 'return 0 unless ' . _wrap($check) . ';' }
    push @lines, $mark if $mark;
    if (@lines) {
      if ($matched) {
        push @body, "unless ($matched) {", _indent(\@lines), '}';
      }
      else {
        push @body, @lines;
      }
    }
  }
  return [] unless grep { $_ ne 'my $v = $x->{$k};' } @body;
  return [ 'for my $k (keys %$x) {', _indent(\@body), '}' ];
}

# A hash constant from declared names to their check subs (1 for a schema that accepts everything); and whether any
# entry is 1.
sub dispatch_table {
  my ($self, $n, $names) = @_;
  my @refs = map { $self->function_ref($n->{properties}{$_}, $n->{resource_id}) } @$names;
  my $entries = join ', ', map { perl_string($names->[$_]) . " => $refs[$_]" } 0 .. $#$names;
  return ($self->constant("{$entries}"), (grep { $_ eq '1' } @refs) ? 1 : 0);
}

# Dependencies: required lists always; schemas here only in the flag layout (tracking evaluates them in place).
sub object_dependencies {
  my ($self, $n, $e, $flag_layout) = @_;
  my @out;
  for my $d (@{ $n->{dependencies} // [] }) {
    my @lines = @{ $self->required_test('$x', $d->{required} // []) };
    if (defined $d->{schema} && $flag_layout && !defined $e) {
      my $check = $self->call($d->{schema}, '$x', $n->{resource_id});
      push @lines, 'return 0 unless ' . _wrap($check) . ';' if $check ne '1';
    }
    push @out, 'if (exists $x->{' . perl_string($d->{name}) . '}) {', _indent(\@lines), '}' if @lines;
  }
  return \@out;
}

# --- Arrays ---

sub array_section {
  my ($self, $n, $e) = @_;
  my @out;
  my $prefix = $n->{prefix_items} // [];
  my $has_length = $n->{min_items} > 0 || $n->{max_items} >= 0 || @$prefix || $n->{items} >= 0 || $n->{contains} >= 0
    || defined $e;
  return \@out if !$has_length && !$n->{unique_items};
  my $length = $self->scratch('n');
  push @out, "my $length = \@\$x;";
  push @out, "return 0 if $length < $n->{min_items};" if $n->{min_items} > 0;
  push @out, "return 0 if $length > $n->{max_items};" if $n->{max_items} >= 0;
  for my $i (0 .. $#$prefix) {
    my $check = $self->call($prefix->[$i], "\$x->[$i]", $n->{resource_id});
    next if $check eq '1';
    push @out, "return 0 if $length > $i && !" . _wrap($check) . ';';
  }
  if (defined $e && @$prefix) {
    my $count = scalar @$prefix;
    push @out, "$e\->{\$_} = 1 for 0 .. ($length < $count ? $length : $count) - 1;";
  }
  if ($n->{items} >= 0) {
    my $item = $self->{nodes}[ $self->elide($n->{items}) ];
    my $count = scalar @$prefix;
    if ($item->{always_false}) {
      push @out, "return 0 if $length > $count;";
    }
    else {
      my $check = $self->call($item->{id}, '$v', $n->{resource_id});
      my $source = $count ? "\@\$x[$count .. \$#\$x]" : '@$x';
      my $nested = $INLINE_ARRAYS ? $self->simple_array_lines($item, '$v', '$w', $n->{resource_id}) : undef;
      if ($nested) {
        # Items that are simple arrays (GeoJSON's positions): checked here, with no call per item.
        push @out, "for my \$v ($source) {", _indent($nested), '}';
      }
      elsif ($check ne '1') {
        my $inlined = $self->inline_child($item->{id}, '$v', $n->{resource_id});
        push @out, "for my \$v ($source) {", _indent($inlined // [ 'return 0 unless ' . _wrap($check) . ';' ]), '}';
      }
      push @out, "$e\->{\$_} = 1 for $count .. $length - 1;" if defined $e;
    }
  }
  if ($n->{contains} >= 0) {
    my $check = $self->call($n->{contains}, '$v', $n->{resource_id});
    my ($lo, $hi) = ($n->{min_contains}, $n->{max_contains});
    my $mark_contains = defined $e && $n->{contains_marks_evaluated};
    if (!($lo <= 0 && $hi < 0 && !$mark_contains)) {
      my $c = $self->scratch('c');
      push @out, "my $c = 0;";
      if ($mark_contains) {
        push @out, 'for my $i (0 .. $#$x) {', '  my $v = $x->[$i];', "  if ($check) {", "    $c++;", "    $e\->{\$i} = 1;",
          '  }', '}';
      }
      else {
        push @out, 'for my $v (@$x) {', "  if ($check) {", "    $c++;";
        push @out, "    last if $c >= $lo;" if $hi < 0;
        push @out, '  }', '}';
      }
      push @out, "return 0 if $c < $lo;" if $lo > 0;
      push @out, "return 0 if $c > $hi;" if $hi >= 0;
    }
  }
  if ($n->{unique_items}) {
    if ($self->items_are_strings($n)) {
      # Every item passed its schema above, so all are strings, which a hash compares as JSON does.
      (my $seen = $self->scratch('s')) =~ s/\A\$//;
      push @out, "if ($length > 1) {", "  my %$seen;", '  @' . $seen . '{@$x} = ();',
        "  return 0 if keys(%$seen) != $length;", '}';
    }
    else {
      push @out, "return 0 if $length > 1 && !JSON::Schema::Corvus::Runtime::unique(\$x);";
    }
  }
  shift @out unless grep { index($_, $length) >= 0 } @out[ 1 .. $#out ];
  return \@out;
}

# The statements that check v against a simple array schema (the type array, optional length bounds and a leaf items
# schema, nothing else), or undef for any other schema. Its items are visited as w.
sub simple_array_lines {
  my ($self, $n, $v, $w, $source) = @_;
  my $crosses = $self->{program}{uses_dynamic_scope} && $n->{resource_id} != $source;
  return undef if $crosses || $n->{in_place_cycle} || !($n->{has_type} && $n->{type} == T_ARRAY) || $n->{items} < 0
    || ($n->{prefix_items} && @{ $n->{prefix_items} }) || $n->{contains} >= 0 || $n->{unique_items}
    || $n->{unevaluated_items} >= 0 || $n->{has_const} || defined $n->{enum_values} || $n->has_in_place_applicators
    || $n->has_object_keywords;
  my $item = $self->{nodes}[ $self->elide($n->{items}) ];
  return undef unless $item->{always_true} || $item->is_type_only || $self->is_inline_leaf($item);
  my $check = $self->call($item->{id}, $w, $source);
  my @lines = ("return 0 unless ref($v) eq 'ARRAY';");
  my @bounds;
  push @bounds, "\@$v < $n->{min_items}" if $n->{min_items} > 0;
  push @bounds, "\@$v > $n->{max_items}" if $n->{max_items} >= 0;
  push @lines, 'return 0 if ' . join(' || ', @bounds) . ';' if @bounds;
  if ($check eq '0') {
    push @lines, "return 0 if \@{$v};";
  }
  elsif ($check ne '1') {
    push @lines, "for my $w (\@{$v}) {", '  return 0 unless ' . _wrap($check) . ';', '}';
  }
  return \@lines;
}

# Whether every item of an array this node accepts is a string (by its items schema).
sub items_are_strings {
  my ($self, $n) = @_;
  return 0 if ($n->{prefix_items} && @{ $n->{prefix_items} }) || $n->{items} < 0;
  my $item = $self->{nodes}[ $self->elide($n->{items}) ];
  return 1 if $item->{has_type} && $item->{type} == T_STRING;
  return $item->{enum_values} && @{ $item->{enum_values} } && !grep { _type($_) ne 'string' } @{ $item->{enum_values} };
}

# --- Strings and numbers ---

sub string_conditions {
  my ($self, $n, $v) = @_;
  my @out;
  push @out, "length($v) >= $n->{min_length}" if $n->{min_length} > 0;
  push @out, "length($v) <= $n->{max_length}" if $n->{max_length} >= 0;
  push @out, $self->pattern_test($n->{pattern}, $v) if defined $n->{pattern};
  if ($n->{assert_format} && defined $n->{format} && !JSON::Schema::Corvus::Formats::is_numeric_format($n->{format_kind})) {
    my $f = $self->format_function($n->{format}, $n->{format_kind}, $n->{dialect});
    push @out, "$f\->($v)" if defined $f;
  }
  if ($n->{assert_content}) {
    my $kind = $n->{content} == CONTENT_BASE64 ? 1 : $n->{content} == CONTENT_JSON ? 2 : 3;
    push @out, "JSON::Schema::Corvus::Runtime::content($v, $kind)";
  }
  return @out;
}

sub number_conditions {
  my ($self, $n, $v) = @_;
  my @out;
  if ($n->{assert_format} && JSON::Schema::Corvus::Formats::is_numeric_format($n->{format_kind})) {
    if (my $custom = $self->{program}{options}{formats}{ $n->{format} }) {
      my $f = $self->_custom_format($custom);
      push @out, "$f\->(JSON::Schema::Corvus::Collecting::number_text($v))";
    }
    else {
      my $f = $self->constant('$JSON::Schema::Corvus::Formats::NUMERIC_FORMAT_VALIDATORS{'
        . perl_string($n->{format_kind}) . '}');
      push @out, "$f\->($v)";
    }
  }
  push @out, "$v >= " . $self->perl_number($n->{minimum}) if defined $n->{minimum};
  push @out, "$v <= " . $self->perl_number($n->{maximum}) if defined $n->{maximum};
  push @out, "$v > " . $self->perl_number($n->{exclusive_minimum}) if defined $n->{exclusive_minimum};
  push @out, "$v < " . $self->perl_number($n->{exclusive_maximum}) if defined $n->{exclusive_maximum};
  if (defined $n->{multiple_of}) {
    push @out, "JSON::Schema::Corvus::Runtime::multiple_of($v, " . $self->perl_number($n->{multiple_of}) . ')';
  }
  return @out;
}

# For a number restricted to integers, the condition that it is one.
sub integer_condition {
  my ($self, $n, $v) = @_;
  return undef unless $n->{has_type} && ($n->{type} & T_INTEGER) && !($n->{type} & T_NUMBER);
  return $self->_integer_test($v, $n->{dialect});
}

sub string_section {
  my ($self, $n) = @_;
  return [ map { 'return 0 unless ' . _wrap($_) . ';' } $self->string_conditions($n, '$x') ];
}

sub number_section {
  my ($self, $n) = @_;
  return [ map { 'return 0 unless ' . _wrap($_) . ';' } $self->number_conditions($n, '$x') ];
}

my $PLAIN_RE = qr/\A[A-Za-z0-9 _\-\/:@,;=!%&'"<>~`#]*\z/;

# A test of v (a string) against an ECMAScript pattern. Common shapes match without a regular expression, as
# PatternMatcher does in C#: a literal prefix, an exact literal or literal alternation, and a contained literal.
sub pattern_test {
  my ($self, $pattern, $v) = @_;
  my $plain = sub { $_[0] =~ $PLAIN_RE };
  if (substr($pattern, 0, 1) eq '^' && $plain->(substr($pattern, 1)) && substr($pattern, -1) ne '$') {
    return "index($v, " . perl_string(substr($pattern, 1)) . ') == 0';
  }
  if (length $pattern >= 2 && substr($pattern, 0, 1) eq '^' && substr($pattern, -1) eq '$'
    && $plain->(substr($pattern, 1, -1))) {
    return "$v eq " . perl_string(substr($pattern, 1, -1));
  }
  if ($pattern =~ /\A\^\(\?:([^()]*)\)\$\z/ || $pattern =~ /\A\^\(([^()]*)\)\$\z/) {
    my @alternatives = split /\|/, $1, -1;
    if (!grep { !$plain->($_) } @alternatives) {
      return '(' . join(' || ', map { "$v eq " . perl_string($_) } @alternatives) . ')' if @alternatives <= 6;
      my $set = $self->constant('{' . join(', ', map { perl_string($_) . ' => 1' } @alternatives) . '}');
      return "exists $set\->{$v}";
    }
  }
  return 'index(' . $v . ', ' . perl_string($pattern) . ') >= 0' if length $pattern && $plain->($pattern);
  my $limit = $self->{program}{options}{pattern_match_limit};
  JSON::Schema::Corvus::Pattern::compile_pattern($pattern, $limit)
    or die JSON::Schema::Corvus::Error::Compilation->_new("Invalid regular expression '$pattern'.");
  my $matcher = $self->constant('JSON::Schema::Corvus::Pattern::compile_pattern(' . perl_string($pattern) . ', '
    . (defined $limit ? JSON::Schema::Corvus::Json::number_text(0.0 + $limit) : 'undef') . ')');
  return "$matcher\->($v)";
}

sub _custom_format {
  my ($self, $custom) = @_;
  my $i = $self->{custom_format_index}{$custom};
  if (!defined $i) {
    $i = @{ $self->{custom_formats} };
    push @{ $self->{custom_formats} }, $custom;
    $self->{custom_format_index}{$custom} = $i;
  }
  return "\$F->[$i]";
}

sub format_function {
  my ($self, $format, $kind, $dialect) = @_;
  if (my $custom = $self->{program}{options}{formats}{$format}) {
    return $self->_custom_format($custom);
  }
  # Draft 4 and 6 host names are RFC 1123 names; later drafts apply the IDNA rules.
  return $self->constant('\&JSON::Schema::Corvus::Formats::legacy_hostname') if $kind eq 'hostname' && $dialect <= DRAFT6;
  # Names the dialect does not define are unknown formats, which always match.
  return undef unless exists $JSON::Schema::Corvus::Formats::FORMAT_VALIDATORS{$kind};
  return $self->constant('$JSON::Schema::Corvus::Formats::FORMAT_VALIDATORS{' . perl_string($kind) . '}');
}

# --- In-place applicators ---

# The in-place applicators. e is the evaluated set being marked (property names for objects, indexes for arrays), with
# kind saying which; undef for flag evaluation.
sub in_place {
  my ($self, $n, $e, $kind) = @_;
  my @out;
  my $marks = sub {
    my ($node_id) = @_;
    return 0 unless defined $e;
    my $c = $self->{nodes}[ $self->elide($node_id) ];
    return $kind eq 'object' ? $c->{marks_properties} : $c->{marks_items};
  };
  my $direct = sub { $self->call($_[0], '$x', $n->{resource_id}, $marks->($_[0]) ? $e : undef) };

  _push(\@out, $direct->($n->{ref})) if $n->{ref} >= 0;
  _push(\@out, $direct->($n->{static_dynamic_ref})) if $n->{static_dynamic_ref} >= 0;
  if ($n->{dynamic_ref}) {
    my $variant = defined $e && defined $kind && $self->dynamic_marks($n, $kind) ? 't' : 'v';
    push @out, 'return 0 unless ' . $self->ref_to("d$variant$n->{id}") . '($x' . ($variant eq 't' && $e ? ", $e" : '')
      . ');';
  }
  _push(\@out, $direct->($_)) for @{ $n->{all_of} // [] };

  if ($n->{any_of}) {
    if (defined $e && grep { $marks->($_) } @{ $n->{any_of} }) {
      # Every passing branch contributes its annotations, so every branch is evaluated.
      my $any = $self->scratch('any');
      push @out, "my $any = 0;";
      for my $c (@{ $n->{any_of} }) {
        if ($marks->($c)) {
          my $s = $self->scratch('s');
          push @out, "my $s = {};";
          push @out, 'if (' . $self->call($c, '$x', $n->{resource_id}, $s) . ') {', "  $any = 1;",
            '  @{' . $e . '}{keys %{' . $s . '}} = values %{' . $s . '};', '}';
        }
        else {
          push @out, "$any = 1 if !$any && " . _wrap($self->call($c, '$x', $n->{resource_id})) . ';';
        }
      }
      push @out, "return 0 unless $any;";
    }
    else {
      push @out, @{ $self->select_branches($n, $n->{any_of}, $n->{any_of_discriminator},
        sub { $self->any_of_check($n, $_[0]) }) };
    }
  }

  if ($n->{one_of}) {
    if (defined $e && grep { $marks->($_) } @{ $n->{one_of} }) {
      my $count = $self->scratch('one');
      push @out, "my $count = 0;";
      for my $c (@{ $n->{one_of} }) {
        if ($marks->($c)) {
          my $s = $self->scratch('s');
          push @out, "my $s = {};";
          push @out, 'if (' . $self->call($c, '$x', $n->{resource_id}, $s) . ') {', "  $count++;",
            '  @{' . $e . '}{keys %{' . $s . '}} = values %{' . $s . '};', '}';
        }
        else {
          push @out, "$count++ if " . _wrap($self->call($c, '$x', $n->{resource_id})) . ';';
        }
      }
      push @out, "return 0 if $count != 1;";
    }
    else {
      push @out, @{ $self->select_branches($n, $n->{one_of}, $n->{one_of_discriminator},
        sub { $self->one_of_check($n, $_[0]) }) };
    }
  }

  if ($n->{not_} >= 0) {
    my $check = $self->call($n->{not_}, '$x', $n->{resource_id});
    if ($check eq '1') { push @out, 'return 0;' }
    elsif ($check ne '0') { push @out, "return 0 if $check;" }
  }

  if ($n->{if_} >= 0) {
    my $then_check = $n->{then} >= 0 ? $direct->($n->{then}) : '1';
    my $else_check = $n->{else_} >= 0 ? $direct->($n->{else_}) : '1';
    if ($then_check ne '1' || $else_check ne '1' || $marks->($n->{if_})) {
      if ($marks->($n->{if_})) {
        my $s = $self->scratch('s');
        push @out, "my $s = {};";
        my $cond = $self->call($n->{if_}, '$x', $n->{resource_id}, $s);
        push @out, "if ($cond) {", '  @{' . $e . '}{keys %{' . $s . '}} = values %{' . $s . '};';
      }
      else {
        push @out, 'if (' . $self->call($n->{if_}, '$x', $n->{resource_id}) . ') {';
      }
      push @out, '  return 0 unless ' . _wrap($then_check) . ';' if $then_check ne '1';
      if ($else_check ne '1') {
        push @out, '} else {', '  return 0 unless ' . _wrap($else_check) . ';';
      }
      push @out, '}';
    }
  }

  # Dependent schemas are in-place applicators; the flag layout emits them in the object section.
  if (defined $e && defined $kind && $kind eq 'object') {
    for my $d (@{ $n->{dependencies} // [] }) {
      next unless defined $d->{schema};
      my $check = $direct->($d->{schema});
      push @out, 'return 0 if exists $x->{' . perl_string($d->{name}) . '} && !' . _wrap($check) . ';' if $check ne '1';
    }
  }
  return \@out;
}

sub dynamic_marks {
  my ($self, $n, $kind) = @_;
  my $d = $n->{dynamic_ref};
  for my $node_id ($d->{fallback}, values %{ $d->{by_resource} }) {
    my $c = $self->{nodes}[ $self->elide($node_id) ];
    return 1 if $kind eq 'object' ? $c->{marks_properties} : $c->{marks_items};
  }
  return 0;
}

sub any_of_check {
  my ($self, $n, $branches) = @_;
  return ['return 0;'] unless @$branches;
  my $dispatch = $self->type_dispatch($n, $branches);
  return $dispatch if $dispatch;
  my @checks = map { $self->call($_, '$x', $n->{resource_id}) } @$branches;
  return [] if grep { $_ eq '1' } @checks;
  return [ 'return 0 unless ' . join(' || ', map { _wrap($_) } @checks) . ';' ];
}

sub one_of_check {
  my ($self, $n, $branches) = @_;
  return ['return 0;'] unless @$branches;
  if (@$branches == 1) {
    my $check = $self->call($branches->[0], '$x', $n->{resource_id});
    return $check eq '1' ? [] : [ 'return 0 unless ' . _wrap($check) . ';' ];
  }
  my $dispatch = $self->type_dispatch($n, $branches);
  return $dispatch if $dispatch;
  my $count = $self->scratch('one');
  my @out = ("my $count = 0;");
  for my $i (0 .. $#$branches) {
    my $check = $self->call($branches->[$i], '$x', $n->{resource_id});
    if ($i == 0) {
      push @out, "$count = 1 if " . _wrap($check) . ';';
    }
    else {
      push @out, 'if (' . $check . ') {', "  return 0 if $count;", "  $count = 1;", '}';
    }
  }
  push @out, "return 0 unless $count;";
  return \@out;
}

# When every branch asserts a type and no two accept the same kind, the instance's kind selects the only branch that
# can pass (SchemaCompiler.ComputeTypeDispatch). Correct for anyOf and oneOf alike.
sub type_dispatch {
  my ($self, $n, $branches) = @_;
  my %owner;
  for my $b (@$branches) {
    my $c = $self->{nodes}[ $self->elide($b) ];
    next if $c->{always_false};
    return undef if $c->{always_true} || !$c->{has_type};
    for my $k (@KINDS) {
      next unless kind_allowed($c->{type}, $k);
      return undef if exists $owner{$k};
      $owner{$k} = $b;
    }
  }
  return undef if @$branches < 2;
  my @out;
  my $first = 1;
  for my $b (@$branches) {
    my @kinds = grep { exists $owner{$_} && $owner{$_} == $b } @KINDS;
    next unless @kinds;
    my $check = $self->call($b, '$x', $n->{resource_id});
    my $test = join ' || ', map { $self->kind_test($_, '$x') } @kinds;
    push @out, ($first ? 'if (' : '} elsif (') . "$test) {";
    push @out, '  return 0 unless ' . _wrap($check) . ';' if $check ne '1';
    $first = 0;
  }
  return ['return 0;'] if $first;
  push @out, '} else {', '  return 0;', '}';
  return \@out;
}

# Narrows oneOf or anyOf branches by a discriminator property when the instance is an object that has it.
sub select_branches {
  my ($self, $n, $branches, $disc, $check) = @_;
  return $check->($branches) unless $disc && !grep { !_is_primitive($_->[0]) } @{ $disc->{known} };
  my $d = $self->scratch('d');
  my $key = perl_string($disc->{property});
  my @out;
  push @out, 'if (' . $self->kind_test('object', '$x') . ') {';
  push @out, "  if (!exists \$x->{$key}) {";
  push @out, _indent($disc->{all_require} ? ['return 0;'] : $check->($branches), 2);
  push @out, '  } else {';
  push @out, "    my $d = \$x->{$key};";
  # The values that select the same subset of branches, together.
  my (%groups, @group_order);
  for my $entry (@{ $disc->{known} }) {
    my ($value, $subset) = @$entry;
    my $k = join ',', @$subset;
    push @group_order, $k unless exists $groups{$k};
    push @{ $groups{$k} }, $value;
  }
  my $first = 1;
  for my $k (@group_order) {
    my @subset = map { $branches->[$_] } length $k ? split /,/, $k : ();
    my $test = join ' || ', map { $self->eq_test($d, $_) } @{ $groups{$k} };
    push @out, '    ' . ($first ? 'if (' : '} elsif (') . "$test) {";
    push @out, _indent($check->(\@subset), 3);
    $first = 0;
  }
  push @out, $first ? '    {' : '    } else {';
  push @out, _indent($check->([ map { $branches->[$_] } @{ $disc->{unknown} } ]), 3);
  push @out, '    }', '  }', '} else {';
  push @out, _indent($check->($branches));
  push @out, '}';
  return \@out;
}

# --- The tracking layout (unevaluatedProperties and unevaluatedItems) ---

sub _coverage { { names => {}, patterns => [], prefix => 0, all => 0 } }

sub _within {
  my ($main, $c, $check_prefix) = @_;
  return 1 if $main->{all};
  return 0 if $c->{all};
  return 0 if $check_prefix && $c->{prefix} > $main->{prefix};
  return 0 if grep { !$main->{names}{$_} } keys %{ $c->{names} };
  my %main_patterns = map { $_ => 1 } @{ $main->{patterns} };
  return 0 if grep { !$main_patterns{$_} } @{ $c->{patterns} };
  return 1;
}

# When every in-place contributor of evaluated properties or items is unconditional ($ref and allOf chains, with no
# marking anyOf, oneOf, if, dependentSchemas or $dynamicRef), the evaluated set is static: declared names, patterns,
# a prefix length, or everything, and unevaluatedProperties or unevaluatedItems needs no tracking at run time. undef
# when some contribution is conditional.
sub static_coverage {
  my ($self, $n, $kind) = @_;
  my $main = _coverage();
  my @conditional;
  my $marks = sub {
    my $c = $self->{nodes}[ $self->elide($_[0]) ];
    return $kind eq 'object' ? $c->{marks_properties} : $c->{marks_items};
  };
  my $visit;
  $visit = sub {
    my ($node_id, $root, $coverage, $visited) = @_;
    my $m = $self->{nodes}[$node_id];
    return 1 if $visited->{$node_id}++;
    return 1 if $m->{always_true} || $m->{always_false};
    my @conditional_children = (@{ $m->{any_of} // [] }, @{ $m->{one_of} // [] },
      (grep { $_ >= 0 } $m->{if_}, $m->{then}, $m->{else_}),
      (map { defined $_->{schema} ? $_->{schema} : () } @{ $m->{dependencies} // [] }),
      ($m->{dynamic_ref} ? ($m->{dynamic_ref}{fallback}, values %{ $m->{dynamic_ref}{by_resource} }) : ()));
    for my $c (@conditional_children) {
      next unless $marks->($c);
      my $sub = _coverage();
      return 0 unless $visit->($self->elide($c), 0, $sub, {});
      push @conditional, $sub;
    }
    if ($kind eq 'object') {
      $coverage->{names}{$_} = 1 for @{ $m->{property_order} // [] };
      for my $p (@{ $m->{pattern_properties} // [] }) {
        push @{ $coverage->{patterns} }, $p->{pattern} unless grep { $_ eq $p->{pattern} } @{ $coverage->{patterns} };
      }
      $coverage->{all} = 1 if $m->{additional_properties} >= 0 || (!$root && $m->{unevaluated_properties} >= 0);
    }
    else {
      my $count = @{ $m->{prefix_items} // [] };
      $coverage->{prefix} = $count if $count > $coverage->{prefix};
      $coverage->{all} = 1 if $m->{items} >= 0 || (!$root && $m->{unevaluated_items} >= 0);
      return 0 if $m->{contains} >= 0 && $m->{contains_marks_evaluated};
    }
    for my $c ($m->{ref}, $m->{static_dynamic_ref}, @{ $m->{all_of} // [] }) {
      return 0 if $c >= 0 && $marks->($c) && !$visit->($self->elide($c), 0, $coverage, $visited);
    }
    return 1;
  };
  my $ok = $visit->($n->{id}, 1, $main, {});
  undef $visit;
  return undef unless $ok;
  return (grep { !_within($main, $_, 1) } @conditional) ? undef : $main;
}

# Static coverage for objects with guards: a contribution under if, then or else or a dependency's schema is kept with
# the conditions it applies under, so an object's evaluated names are the unconditional coverage plus every guarded
# coverage whose guards hold, all tested statically. Returns (coverage, guarded) or ().
sub guarded_coverage {
  my ($self, $n) = @_;
  my $main = _coverage();
  my (@guarded, @unguarded);
  my $marks = sub { $self->{nodes}[ $self->elide($_[0]) ]{marks_properties} };
  my $visit;
  $visit = sub {
    my ($node_id, $root, $coverage, $guards, $visited) = @_;
    my $m = $self->{nodes}[$node_id];
    return 1 if $visited->{$node_id}++;
    return 1 if $m->{always_true} || $m->{always_false};
    return 0 if $m->{in_place_cycle} || ($self->{program}{uses_dynamic_scope} && $m->{resource_id} != $n->{resource_id});
    my $branch = sub {
      my ($c, $g) = @_;
      my $sub = _coverage();
      if (defined $g) { push @guarded, [ $g, $sub ] } else { push @unguarded, $sub }
      return $visit->($self->elide($c), 0, $sub, $g, {});
    };
    my @alternatives = (@{ $m->{any_of} // [] }, @{ $m->{one_of} // [] },
      ($m->{dynamic_ref} ? ($m->{dynamic_ref}{fallback}, values %{ $m->{dynamic_ref}{by_resource} }) : ()));
    for my $c (@alternatives) {
      return 0 if $marks->($c) && !$branch->($c, undef);
    }
    if ($m->{if_} >= 0) {
      my $test = $self->call($m->{if_}, '$x', $n->{resource_id});
      for my $pair ([ $m->{if_}, 1 ], [ $m->{then}, 1 ], [ $m->{else_}, 0 ]) {
        my ($c, $holds) = @$pair;
        next unless $c >= 0 && $marks->($c);
        my $g = defined $guards ? [ @$guards, $holds ? $test : '!' . _wrap($test) ] : undef;
        return 0 unless $branch->($c, $g);
      }
    }
    for my $dep (@{ $m->{dependencies} // [] }) {
      next if !defined $dep->{schema} || !$marks->($dep->{schema});
      my $g = defined $guards ? [ @$guards, 'exists $x->{' . perl_string($dep->{name}) . '}' ] : undef;
      return 0 unless $branch->($dep->{schema}, $g);
    }
    $coverage->{names}{$_} = 1 for @{ $m->{property_order} // [] };
    for my $p (@{ $m->{pattern_properties} // [] }) {
      push @{ $coverage->{patterns} }, $p->{pattern} unless grep { $_ eq $p->{pattern} } @{ $coverage->{patterns} };
    }
    $coverage->{all} = 1 if $m->{additional_properties} >= 0 || (!$root && $m->{unevaluated_properties} >= 0);
    for my $c ($m->{ref}, $m->{static_dynamic_ref}, @{ $m->{all_of} // [] }) {
      return 0 if $c >= 0 && $marks->($c) && !$visit->($self->elide($c), 0, $coverage, $guards, $visited);
    }
    return 1;
  };
  my $ok = $visit->($n->{id}, 1, $main, [], {});
  undef $visit;
  return () unless $ok;
  return () if grep { !_within($main, $_, 0) } @unguarded;
  my @adding = grep { !_within($main, $_->[1], 0) } @guarded;
  return @adding <= MAX_GUARDED_COVERAGES ? ($main, \@adding) : ();
}

# The object or array branch of a node whose unevaluated keyword is decided by static coverage.
sub fused_unevaluated {
  my ($self, $n, $kind, $coverage, $variant, $guarded) = @_;
  my @lines;
  push @lines, @{ $self->kind_section($n, $kind, undef, 1) };
  push @lines, @{ $self->in_place($n, undef, undef) };
  if (!$coverage->{all}) {
    if ($kind eq 'object') {
      my $check = $self->call($n->{unevaluated_properties}, '$v', $n->{resource_id});
      push @lines, @{ $self->uncovered_properties($coverage, $guarded // [], $check) } if $check ne '1';
    }
    else {
      my $check = $self->call($n->{unevaluated_items}, '$v', $n->{resource_id});
      if ($check eq '0') {
        push @lines, "return 0 if \@\$x > $coverage->{prefix};";
      }
      elsif ($check ne '1') {
        my $source = $coverage->{prefix} ? "\@\$x[$coverage->{prefix} .. \$#\$x]" : '@$x';
        push @lines, "for my \$v ($source) {", '  return 0 unless ' . _wrap($check) . ';', '}';
      }
    }
  }
  if ($variant eq 't') {
    push @lines, $kind eq 'object' ? '$ev->{$_} = 1 for keys %$x;' : '$ev->{$_} = 1 for 0 .. $#$x;';
  }
  push @lines, 'return 1;';
  return \@lines;
}

# The test of every property no coverage evaluated, against unevaluatedProperties.
sub uncovered_properties {
  my ($self, $coverage, $guarded, $check) = @_;
  my @lines;
  my @sorted = sort keys %{ $coverage->{names} };
  my $names = @sorted ? $self->constant('{' . join(', ', map { perl_string($_) . ' => 1' } @sorted) . '}') : undef;
  if (!@{ $coverage->{patterns} } && !@$guarded && $check eq '0') {
    # Nothing beyond the declared names may appear.
    push @lines, $names ? "for (keys %\$x) { return 0 unless exists $names\->{\$_} }" : 'return 0 if %$x;';
    return \@lines;
  }
  # Each distinct guard is decided once for an object, before the pass.
  my (%guard_names, @guard_order);
  for my $g (@$guarded) {
    for my $expr (@{ $g->[0] }) {
      next if exists $guard_names{$expr};
      my $name = $self->scratch('g');
      $guard_names{$expr} = $name;
      push @lines, "my $name = $expr;";
    }
  }
  my @body = ('my $v = $x->{$k};');
  push @body, "next if exists $names\->{\$k};" if $names;
  push @body, 'next if ' . $self->pattern_test($_, '$k') . ';' for @{ $coverage->{patterns} };
  for my $g (@$guarded) {
    my ($guards, $c) = @$g;
    my $when = join ' && ', map { $guard_names{$_} } @$guards;
    my @covered;
    my @extra = sort keys %{ $c->{names} };
    if (@extra && @extra <= MAX_GUARD_NAMES) {
      push @covered, map { '$k eq ' . perl_string($_) } @extra;
    }
    elsif (@extra) {
      push @covered, 'exists ' . $self->constant('{' . join(', ', map { perl_string($_) . ' => 1' } @extra) . '}')
        . '->{$k}';
    }
    push @covered, map { $self->pattern_test($_, '$k') } @{ $c->{patterns} };
    if ($c->{all}) {
      push @body, 'next if ' . ($when || '1') . ';';
    }
    elsif (@covered) {
      push @body, 'next if ' . ($when ? "$when && " : '') . '(' . join(' || ', @covered) . ');';
    }
  }
  push @body, $check eq '0' ? 'return 0;' : 'return 0 unless ' . _wrap($check) . ';';
  push @lines, 'for my $k (keys %$x) {', _indent(\@body), '}';
  return \@lines;
}

sub tracking_body {
  my ($self, $n, $variant) = @_;
  return ['return 0;'] if $n->{always_false};
  return ['return 1;'] if $n->{always_true};
  my @out = @{ $self->const_enum($n) };
  my $mask = $n->{has_type} ? $n->{type} : T_ALL;
  for my $kind (qw(array object)) {
    my $test = $self->kind_test($kind, '$x');
    if (!kind_allowed($mask, $kind)) {
      push @out, "return 0 if $test;";
      next;
    }
    my $own = $kind eq 'object' ? $n->{unevaluated_properties} >= 0 : $n->{unevaluated_items} >= 0;
    my $coverage = $own ? $self->static_coverage($n, $kind) : undef;
    if ($coverage) {
      push @out, "if ($test) {", _indent($self->fused_unevaluated($n, $kind, $coverage, $variant)), '}';
      next;
    }
    my @guarded = $own && $kind eq 'object' ? $self->guarded_coverage($n) : ();
    if (@guarded) {
      push @out, "if ($test) {", _indent($self->fused_unevaluated($n, $kind, $guarded[0], $variant, $guarded[1])), '}';
      next;
    }
    my $e = $own ? $self->scratch('e') : $variant eq 't' ? '$ev' : undef;
    my @lines;
    push @lines, "my $e = {};" if $own;
    push @lines, @{ $self->kind_section($n, $kind, $e, !defined $e) };
    push @lines, @{ $self->in_place($n, $e, defined $e ? $kind : undef) };
    if ($own) {
      if ($kind eq 'object') {
        my $check = $self->call($n->{unevaluated_properties}, '$v', $n->{resource_id});
        if ($check ne '1') {
          push @lines, 'for my $k (keys %$x) {', '  my $v = $x->{$k};',
            "  return 0 if !exists $e\->{\$k} && !" . _wrap($check) . ';', '}';
        }
        push @lines, '$ev->{$_} = 1 for keys %$x;' if $variant eq 't';
      }
      else {
        my $check = $self->call($n->{unevaluated_items}, '$v', $n->{resource_id});
        if ($check ne '1') {
          push @lines, 'for my $i (0 .. $#$x) {', '  my $v = $x->[$i];',
            "  return 0 if !exists $e\->{\$i} && !" . _wrap($check) . ';', '}';
        }
        push @lines, '$ev->{$_} = 1 for 0 .. $#$x;' if $variant eq 't';
      }
    }
    push @lines, 'return 1;';
    push @out, "if ($test) {", _indent(\@lines), '}';
  }
  # Scalars: flag evaluation.
  my @scalar_kinds = grep { $_ ne 'object' && $_ ne 'array' } @KINDS;
  my @allowed = grep { kind_allowed($mask, $_) } @scalar_kinds;
  if (!@allowed) {
    push @out, 'return 0;';
    return \@out;
  }
  for my $k (@allowed) {
    my $lines = $self->kind_section($n, $k, undef, 1);
    push @out, 'if (' . $self->kind_test($k, '$x') . ') {', _indent($lines), '}' if @$lines;
  }
  if ($n->{has_type} && @allowed < @scalar_kinds) {
    push @out, 'return 0 unless ' . join(' || ', map { $self->kind_test($_, '$x') } @allowed) . ';';
  }
  push @out, @{ $self->in_place($n, undef, undef) };
  push @out, 'return 1;';
  return \@out;
}

# --- Linking: merge structurally identical subs, then emit the reachable ones ---

my $REF_RE = qr/\x01([^\x02]+)\x02/;

sub link {
  my ($self, $root_name) = @_;
  my @names = @{ $self->{template_order} };
  my %index_of = map { $names[$_] => $_ } 0 .. $#names;
  # Each template split once: literal parts (even indexes) around references (odd indexes).
  my @parts = map { [ split /\x01([^\x02]+)\x02/, $self->{templates}{$_}, -1 ] } @names;
  my @refs = map { my $p = $_; [ map { $index_of{ $p->[$_] } } grep { $_ % 2 } 0 .. $#$p ] } @parts;

  # Partition refinement: classes from the literal parts alone, refined by the classes of the references until the
  # number of classes stops growing. Bisimilar subs (identical text up to equivalent callees) merge.
  my %intern;
  my @class_of;
  for my $p (@parts) {
    my $sig = join "\x01", map { $p->[$_] } grep { !($_ % 2) } 0 .. $#$p;
    $intern{$sig} = scalar keys %intern unless exists $intern{$sig};
    push @class_of, $intern{$sig};
  }
  my $count = keys %intern;
  @class_of = $count < @names ? @{ _refine_classes(\@class_of, $count, \@refs) } : 0 .. $#names;

  my $root = $index_of{$root_name};
  my @representative = (-1) x @names;
  $representative[ $class_of[$root] ] = $root;
  for my $i (0 .. $#names) {
    $representative[ $class_of[$i] ] = $i if $representative[ $class_of[$i] ] < 0;
  }
  # The reachable representatives: from the root, and from the subs that dispatch tables name.
  my @emitted = (0) x @names;
  my @out;
  my @stack = ($representative[ $class_of[$root] ]);
  for my $init (@{ $self->{constant_order} }) {
    push @stack, map { $representative[ $class_of[ $index_of{$_} ] ] } $init =~ /\x01([^\x02]+)\x02/g;
  }
  while (@stack) {
    my $i = pop @stack;
    next if $emitted[$i]++;
    my $p = $parts[$i];
    (my $text = $p->[0]) =~ s/\Asub \@ \{/sub _$names[$i] {/;
    for (my $k = 1; $k < @$p; $k += 2) {
      my $target = $representative[ $class_of[ $refs[$i][ ($k - 1) / 2 ] ] ];
      $text .= "_$names[$target]" . $p->[ $k + 1 ];
      push @stack, $target unless $emitted[$target];
    }
    push @out, [ $names[$i], $text ];
  }
  my @constants = map {
    my $init = $_;
    $init =~ s/\x01([^\x02]+)\x02/_$names[ $representative[ $class_of[ $index_of{$1} ] ] ]/g;
    [ $self->{constants}{$_}, $init ];
  } @{ $self->{constant_order} };
  # Every sub's name starts with _: v1 alone would be a version string.
  return (\@out, \@constants, '_' . $names[ $representative[ $class_of[$root] ] ]);
}

sub _refine_classes {
  my ($initial, $initial_count, $refs) = @_;
  my @class_of = @$initial;
  my $count = $initial_count;
  my $n = @class_of;
  my @size = (0) x $count;
  $size[$_]++ for @class_of;
  while (1) {
    my %intern;
    my @next = (0) x $n;
    my $next_count = 0;
    my @singleton = (-1) x $count;
    for my $i (0 .. $n - 1) {
      my $cls = $class_of[$i];
      if ($size[$cls] == 1) {
        $singleton[$cls] = $next_count++ if $singleton[$cls] < 0;
        $next[$i] = $singleton[$cls];
        next;
      }
      my $sig = join ',', $cls, map { $class_of[$_] } @{ $refs->[$i] };
      $intern{$sig} //= $next_count++;
      $next[$i] = $intern{$sig};
    }
    @class_of = @next;
    return \@class_of if $next_count == $count;
    $count = $next_count;
    @size = (0) x $count;
    $size[$_]++ for @class_of;
  }
}

1;
