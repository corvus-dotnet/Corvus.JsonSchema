package JSON::Schema::Corvus::Regex::Parse;

# Reads an ECMA-262 pattern into a syntax tree: a port of the Go port's parse.go.
#
# A pattern is read with the grammar of the u flag, which is what JSON Schema specifies, or with the Annex B grammar of
# a pattern with no flag. Without the u flag a pattern is read, and a text matched, a UTF-16 code unit at a time, so a
# character beyond the Basic Multilingual Plane is two atoms.
#
# A node is a hash with a kind:
#   empty                                matches the empty string
#   char      r                          one code point
#   set       set                        a class (JSON::Schema::Corvus::Regex::Charset)
#   cat, alt  subs                       a sequence, alternatives
#   group     subs => [body], group      group is the number of a capturing group, from 1, or 0
#   repeat    subs => [body], min, max, lazy   max is -1 for no bound
#   bol, eol, line_start, line_end       ^ and $, and the same with the m modifier
#   wb, nwb   fold                       \b and \B, fold inside a case-insensitive group with the u flag
#   look      subs => [body], behind, negative
#   backref   groups => [numbers], fold, name (until resolved)
#
# The limits on the size of one pattern bound the time and memory of reading and compiling it whatever it holds. A
# pattern over a limit dies with a too_large error, which is not the error of a pattern that is not valid.

use 5.026;
use strict;
use warnings;
use JSON::Schema::Corvus::Regex::Charset ();

use constant UNBOUNDED => -1;
use constant MAX_COUNT => 2147483647;
# The longest pattern read, in bytes of UTF-8; the deepest groups; the largest size, in the bytes counted for it as it
# is read (NODE_SIZE for each node, RANGE_SIZE for each range a class or a case-insensitive group adds).
use constant { MAX_PATTERN_LENGTH => 1 << 20, MAX_NESTING => 1000, MAX_SIZE => 1 << 20, NODE_SIZE => 32,
  RANGE_SIZE => 8 };

my $C = 'JSON::Schema::Corvus::Regex::Charset';

# The errors: { invalid => message, at => offset } for a pattern that is not valid, and { too_large => limit,
# value => n } for one over a limit of size.
sub _fail {
  my ($p, $message) = @_;
  die { invalid => $message, at => $p->{i} };
}

sub _too_large { die { too_large => $_[0], value => $_[1] } }

# Reads a pattern. Returns (root, parser) with the parser's needs_backtracking, or dies with an error above.
sub parse {
  my ($pattern, $unicode) = @_;
  my $bytes = $pattern;
  utf8::encode($bytes);
  _too_large('length', MAX_PATTERN_LENGTH) if length $bytes > MAX_PATTERN_LENGTH;
  my @cps = unpack 'W*', $pattern;
  @cps = map { $_ > 0xFFFF ? (0xD800 + (($_ - 0x10000) >> 10), 0xDC00 + (($_ - 0x10000) & 0x3FF)) : $_ } @cps
    unless $unicode;
  my $p = bless {
    p => \@cps, i => 0, unicode => $unicode ? 1 : 0, depth => 0, group_count => 0, has_names => 0, groups => 0,
    names => {}, named_refs => [], path => [], disjunctions => 0, icase => 0, multiline => 0, dot_all => 0,
    needs_backtracking => 0, size => 0,
  }, __PACKAGE__;
  $p->_scan_groups;
  my $root = $p->_disjunction;
  if ($p->{i} < @cps) {
    $p->_fail("unmatched ')'") if $cps[ $p->{i} ] == 0x29;
    $p->_fail('unexpected character');
  }
  for my $ref (@{ $p->{named_refs} }) {
    my $named = $p->{names}{ $ref->{name} } or $p->_fail('reference to an unknown group name');
    push @{ $ref->{groups} }, map { $_->[0] } @$named;
  }
  return ($root, $p);
}

sub _charge {
  my ($p, $bytes) = @_;
  $p->{size} += $bytes;
  _too_large('size', MAX_SIZE) if $p->{size} > MAX_SIZE;
}

# Counts the capturing groups and notes whether any is named: the meaning of \1 and \k depends on groups that may
# follow them.
sub _scan_groups {
  my ($p) = @_;
  my $s = $p->{p};
  my $in_class = 0;
  for (my $k = 0; $k < @$s; $k++) {
    my $c = $s->[$k];
    if ($c == 0x5C) { $k++ }
    elsif ($c == 0x5B) { $in_class = 1 }
    elsif ($c == 0x5D) { $in_class = 0 }
    elsif ($c == 0x28 && !$in_class) {
      if ($k + 1 >= @$s || $s->[$k + 1] != 0x3F) {
        $p->{group_count}++;
      }
      elsif ($k + 3 < @$s && $s->[$k + 2] == 0x3C && $s->[$k + 3] != 0x3D && $s->[$k + 3] != 0x21) {
        $p->{group_count}++;
        $p->{has_names} = 1;
      }
    }
  }
}

sub _more { $_[0]{i} < @{ $_[0]{p} } }
sub _peek { my ($p) = @_; $p->{i} < @{ $p->{p} } ? $p->{p}[ $p->{i} ] : -1 }

sub _eat {
  my ($p, $c) = @_;
  if ($p->{i} < @{ $p->{p} } && $p->{p}[ $p->{i} ] == $c) {
    $p->{i}++;
    return 1;
  }
  return 0;
}

sub _disjunction {
  my ($p) = @_;
  _too_large('nesting', MAX_NESTING) if ++$p->{depth} > MAX_NESTING;
  $p->{disjunctions}++;
  push @{ $p->{path} }, [ $p->{disjunctions}, 0 ];
  my $first = $p->_alternative;
  if ($p->_peek != 0x7C) {
    pop @{ $p->{path} };
    $p->{depth}--;
    return $first;
  }
  my $alt = { kind => 'alt', subs => [$first] };
  while ($p->_eat(0x7C)) {
    $p->_charge(NODE_SIZE);
    $p->{path}[-1][1]++;
    push @{ $alt->{subs} }, $p->_alternative;
  }
  pop @{ $p->{path} };
  $p->{depth}--;
  return $alt;
}

sub _alternative {
  my ($p) = @_;
  my @terms;
  while ($p->_more && $p->_peek != 0x7C && $p->_peek != 0x29) {
    push @terms, $p->_term;
  }
  return { kind => 'empty' } unless @terms;
  return $terms[0] if @terms == 1;
  return { kind => 'cat', subs => \@terms };
}

# The node of one literal character: inside a case-insensitive group, the class of the characters equivalent to it.
sub _char_node {
  my ($p, $c) = @_;
  if ($p->{icase}) {
    my ($set) = JSON::Schema::Corvus::Regex::Charset::fold_closure(JSON::Schema::Corvus::Regex::Charset::new_set([$c, $c], 0),
      $p->{unicode});
    my $r = $set->{ranges};
    return { kind => 'set', set => $set } if @$r != 2 || $r->[0] != $r->[1];
  }
  return { kind => 'char', r => $c };
}

# The node of a class: inside a case-insensitive group, with every character equivalent to one of its members.
sub _set_node {
  my ($p, $set) = @_;
  $set = $p->_fold($set) if $p->{icase};
  return { kind => 'set', set => $set };
}

sub _fold {
  my ($p, $set) = @_;
  my ($closed, $variants) = JSON::Schema::Corvus::Regex::Charset::fold_closure($set, $p->{unicode});
  $p->_charge($variants + RANGE_SIZE * @{ $closed->{ranges} } / 2);
  return $closed;
}

sub _term {
  my ($p) = @_;
  $p->_charge(2 * NODE_SIZE);
  my $c = $p->_peek;
  my $quantifiable = 1;
  my $atom;
  if ($c == 0x5E) {
    $p->{i}++;
    $atom = { kind => 'bol' };
    $quantifiable = 0;
    if ($p->{multiline}) {
      $atom->{kind} = 'line_start';
      $p->{needs_backtracking} = 1;
    }
  }
  elsif ($c == 0x24) {
    $p->{i}++;
    $atom = { kind => 'eol' };
    $quantifiable = 0;
    if ($p->{multiline}) {
      $atom->{kind} = 'line_end';
      $p->{needs_backtracking} = 1;
    }
  }
  elsif ($c == 0x28) {
    ($atom, $quantifiable) = $p->_group;
  }
  elsif ($c == 0x2E) {
    $p->{i}++;
    $atom = { kind => 'set', set => $p->{dot_all} ? $JSON::Schema::Corvus::Regex::Charset::ANY
      : $JSON::Schema::Corvus::Regex::Charset::DOT };
  }
  elsif ($c == 0x5B) {
    $atom = $p->_class;
  }
  elsif ($c == 0x5C) {
    ($atom, $quantifiable) = $p->_atom_escape;
  }
  elsif ($c == 0x2A || $c == 0x2B || $c == 0x3F) {
    $p->_fail('nothing to repeat');
  }
  elsif ($c == 0x7B) {
    $p->_fail('nothing to repeat') if $p->{unicode} || $p->_looks_like_quantifier;
    $p->{i}++;
    $atom = $p->_char_node(0x7B);
  }
  elsif ($c == 0x5D || $c == 0x7D) {
    $p->_fail('lone bracket') if $p->{unicode};
    $p->{i}++;
    $atom = $p->_char_node($c);
  }
  else {
    $p->{i}++;
    $atom = $p->_char_node($c);
  }
  my ($min, $max, $lazy, $ok) = $p->_quantifier;
  return $atom unless $ok;
  $p->_fail('nothing to repeat') unless $quantifiable;
  return { kind => 'repeat', subs => [$atom], min => $min, max => $max, lazy => $lazy };
}

sub _is_digit { $_[0] >= 0x30 && $_[0] <= 0x39 }

# Whether the text at the current '{' reads as {n}, {n,} or {n,m}.
sub _looks_like_quantifier {
  my ($p) = @_;
  my $s = $p->{p};
  my $k = $p->{i} + 1;
  my $start = $k;
  $k++ while $k < @$s && _is_digit($s->[$k]);
  return 0 if $k == $start || $k >= @$s;
  return 1 if $s->[$k] == 0x7D;
  return 0 if $s->[$k] != 0x2C;
  $k++;
  $k++ while $k < @$s && _is_digit($s->[$k]);
  return $k < @$s && $s->[$k] == 0x7D ? 1 : 0;
}

# Whether the first of two runs of decimal digits is the greater number, compared as written (a count has no limit
# of size in the grammar, and number() stops at MAX_COUNT).
sub _greater_as_written {
  my ($a, $b) = @_;
  my $x = join '', map { chr } @$a;
  my $y = join '', map { chr } @$b;
  $x =~ s/\A0+//;
  $y =~ s/\A0+//;
  return length $x > length $y if length $x != length $y;
  return $x gt $y;
}

# A run of decimal digits, saturating at MAX_COUNT.
sub _number {
  my ($p) = @_;
  my $start = $p->{i};
  my $v = 0;
  while ($p->{i} < @{ $p->{p} } && _is_digit($p->{p}[ $p->{i} ])) {
    if ($v < MAX_COUNT) {
      $v = $v * 10 + ($p->{p}[ $p->{i} ] - 0x30);
      $v = MAX_COUNT if $v > MAX_COUNT;
    }
    $p->{i}++;
  }
  $p->_fail('expected a number') if $start == $p->{i};
  return $v;
}

sub _quantifier {
  my ($p) = @_;
  my $c = $p->_peek;
  my ($min, $max);
  if ($c == 0x2A) { $p->{i}++; ($min, $max) = (0, UNBOUNDED) }
  elsif ($c == 0x2B) { $p->{i}++; ($min, $max) = (1, UNBOUNDED) }
  elsif ($c == 0x3F) { $p->{i}++; ($min, $max) = (0, 1) }
  elsif ($c == 0x7B && $p->_looks_like_quantifier) {
    $p->{i}++;
    my $first = $p->{i};
    $min = $p->_number;
    $max = $min;
    my $first_end = $p->{i};
    if ($p->_eat(0x2C)) {
      if ($p->_peek == 0x7D) {
        $max = UNBOUNDED;
      }
      else {
        my $second = $p->{i};
        $max = $p->_number;
        # Both counts stop at MAX_COUNT, so the numbers are compared as they are written.
        $p->_fail('numbers out of order in quantifier') if _greater_as_written(
          [ @{ $p->{p} }[ $first .. $first_end - 1 ] ], [ @{ $p->{p} }[ $second .. $p->{i} - 1 ] ]);
      }
    }
    $p->_fail('malformed quantifier') unless $p->_eat(0x7D);
  }
  else {
    return (0, 0, 0, 0);
  }
  return ($min, $max, $p->_eat(0x3F), 1);
}

# A group or a lookaround, and whether a quantifier may follow it.
sub _group {
  my ($p) = @_;
  $p->{i}++;
  return ($p->_capture(''), 1) unless $p->_eat(0x3F);
  if ($p->_eat(0x3A)) {
    my $body = $p->_disjunction;
    $p->_close;
    return ({ kind => 'group', subs => [$body], group => 0 }, 1);
  }
  my $c = $p->_peek;
  return ($p->_modifier_group, 1) if $c == 0x69 || $c == 0x6D || $c == 0x73 || $c == 0x2D;
  if ($p->_eat(0x3D) || $p->_eat(0x21)) {
    my $negative = $p->{p}[ $p->{i} - 1 ] == 0x21 ? 1 : 0;
    my $body = $p->_disjunction;
    $p->_close;
    $p->{needs_backtracking} = 1;
    # Annex B lets a quantifier follow a lookahead. The u flag does not.
    return ({ kind => 'look', subs => [$body], negative => $negative, behind => 0 }, !$p->{unicode});
  }
  if ($p->_eat(0x3C)) {
    if ($p->_eat(0x3D) || $p->_eat(0x21)) {
      my $negative = $p->{p}[ $p->{i} - 1 ] == 0x21 ? 1 : 0;
      my $body = $p->_disjunction;
      $p->_close;
      $p->{needs_backtracking} = 1;
      return ({ kind => 'look', subs => [$body], negative => $negative, behind => 1 }, 0);
    }
    my $name = $p->_group_name;
    # Two groups may share a name only when they are in separate alternatives of one disjunction, so that no match can
    # go through both.
    for my $other (@{ $p->{names}{$name} // [] }) {
      $p->_fail('duplicate group name') unless _separate_alternatives($other->[1], $p->{path});
    }
    return ($p->_capture($name), 1);
  }
  $p->_fail('invalid group');
}

# Whether two positions lie in different alternatives of some disjunction.
sub _separate_alternatives {
  my ($a, $b) = @_;
  for (my $i = 0; $i < @$a && $i < @$b && $a->[$i][0] == $b->[$i][0]; $i++) {
    return 1 if $a->[$i][1] != $b->[$i][1];
  }
  return 0;
}

# A group that turns modifiers on or off for its body, such as (?i:...) or (?s-i:...). The position is after the "(?".
sub _modifier_group {
  my ($p) = @_;
  my @saved = @$p{qw(icase multiline dot_all)};
  my %seen;
  my ($removing, $count) = (0, 0);
  until ($p->_eat(0x3A)) {
    my $c = $p->_peek;
    $p->{i}++;
    my $flag;
    if ($c == 0x69) { $flag = 'icase' }
    elsif ($c == 0x6D) { $flag = 'multiline' }
    elsif ($c == 0x73) { $flag = 'dot_all' }
    elsif ($c == 0x2D) {
      $p->_fail('invalid group modifier') if $removing;
      $removing = 1;
      next;
    }
    else {
      $p->_fail('invalid group modifier');
    }
    $p->_fail('repeated group modifier') if $seen{$flag}++;
    $p->{$flag} = $removing ? 0 : 1;
    $count++;
  }
  $p->_fail('invalid group modifier') if $count == 0;
  my $body = $p->_disjunction;
  $p->_close;
  @$p{qw(icase multiline dot_all)} = @saved;
  return { kind => 'group', subs => [$body], group => 0 };
}

sub _capture {
  my ($p, $name) = @_;
  my $g = { kind => 'group', group => ++$p->{groups} };
  if (length $name) {
    # The path is kept, and every group of the same name is compared with it.
    $p->_charge(RANGE_SIZE * (@{ $p->{path} } + @{ $p->{names}{$name} // [] }));
    push @{ $p->{names}{$name} }, [ $g->{group}, [ map { [@$_] } @{ $p->{path} } ] ];
  }
  $g->{subs} = [ $p->_disjunction ];
  $p->_close;
  return $g;
}

sub _close { $_[0]->_fail('unterminated group') unless $_[0]->_eat(0x29) }

# A group name up to and including its '>'. A character of the name may be written as a \u escape.
sub _group_name {
  my ($p) = @_;
  my @name;
  until ($p->_eat(0x3E)) {
    $p->_fail('invalid group name') unless $p->_more;
    my $c = $p->_peek;
    $p->{i}++;
    if ($c == 0x5C) {
      $p->_fail('invalid group name') unless $p->_eat(0x75);
      $c = $p->_unicode_escape(1);
      $p->_fail('invalid group name') if $c < 0;
    }
    elsif ($c >= 0xD800 && $c <= 0xDBFF && $p->_peek >= 0xDC00 && $p->_peek <= 0xDFFF) {
      # Without the u flag the pattern is code units, and a name still takes a surrogate pair as one character.
      $c = 0x10000 + (($c - 0xD800) << 10) + ($p->_peek - 0xDC00);
      $p->{i}++;
    }
    if (!@name) {
      $p->_fail('invalid group name') unless $c == 0x24 || $c == 0x5F || _id_start($c);
    }
    else {
      $p->_fail('invalid group name') unless $c == 0x24 || $c == 0x200C || $c == 0x200D || _id_continue($c);
    }
    push @name, $c;
  }
  $p->_fail('invalid group name') unless @name;
  no warnings qw(surrogate nonchar non_unicode);
  return join '', map { chr } @name;
}

my ($ID_START, $ID_CONTINUE);
sub _id_start { ($ID_START //= JSON::Schema::Corvus::Regex::Charset::property_set('ID_Start'))->contains($_[0]) }
sub _id_continue {
  ($ID_CONTINUE //= JSON::Schema::Corvus::Regex::Charset::property_set('ID_Continue'))->contains($_[0]);
}

# An escape outside a class, and whether a quantifier may follow it.
sub _atom_escape {
  my ($p) = @_;
  $p->{i}++;
  $p->_fail('\\ at end of pattern') unless $p->_more;
  my $c = $p->_peek;
  if ($c == 0x62 || $c == 0x42) {
    $p->{i}++;
    my $boundary = { kind => $c == 0x42 ? 'nwb' : 'wb', fold => 0 };
    # With the u flag, a case-insensitive word boundary counts two more characters as word characters.
    if ($p->{unicode} && $p->{icase}) {
      $boundary->{fold} = 1;
      $p->{needs_backtracking} = 1;
    }
    return ($boundary, 0);
  }
  if ($c == 0x6B) {
    # Annex B reads \k as the letter k in a pattern with no named group.
    if ($p->{unicode} || $p->{has_names}) {
      $p->{i}++;
      $p->_fail('invalid named reference') unless $p->_eat(0x3C);
      my $ref = { kind => 'backref', name => $p->_group_name, fold => $p->{icase}, groups => [] };
      push @{ $p->{named_refs} }, $ref;
      $p->{needs_backtracking} = 1;
      return ($ref, 1);
    }
    $p->{i}++;
    return ($p->_char_node(0x6B), 1);
  }
  if ($c >= 0x31 && $c <= 0x39) {
    my $start = $p->{i};
    my $n = $p->_number;
    if ($n <= $p->{group_count}) {
      $p->{needs_backtracking} = 1;
      return ({ kind => 'backref', groups => [$n], fold => $p->{icase} }, 1);
    }
    $p->_fail('reference to a group that does not exist') if $p->{unicode};
    # Annex B reads it as an octal escape, or as the digit itself.
    $p->{i} = $start;
    return ($p->_char_node($p->_legacy_octal), 1);
  }
  if (my $set = $p->_class_escape($c)) {
    return ($p->_set_node($set), 1);
  }
  return ($p->_char_node($p->_character_escape(0)), 1);
}

# An Annex B octal escape of up to three digits with a value below 256. A digit that cannot start one (8 or 9) stands
# for itself.
sub _legacy_octal {
  my ($p) = @_;
  my ($v, $k) = (0, 0);
  while ($k < 3 && $p->_more && $p->_peek >= 0x30 && $p->_peek <= 0x37 && $v * 8 + ($p->_peek - 0x30) <= 0377) {
    $v = $v * 8 + ($p->_peek - 0x30);
    $p->{i}++;
    $k++;
  }
  if ($k == 0) {
    my $d = $p->_peek;
    $p->{i}++;
    return $d;
  }
  return $v;
}

# A class escape such as \d or \p{...} whose letter is c, or undef, consuming nothing, when c does not start one.
sub _class_escape {
  my ($p, $c) = @_;
  if ($c == 0x64) { $p->{i}++; return $JSON::Schema::Corvus::Regex::Charset::DIGIT }
  if ($c == 0x44) { $p->{i}++; return $JSON::Schema::Corvus::Regex::Charset::NOT_DIGIT }
  if ($c == 0x77) {
    $p->{i}++;
    return $p->{unicode} && $p->{icase} ? $JSON::Schema::Corvus::Regex::Charset::FOLD_WORD
      : $JSON::Schema::Corvus::Regex::Charset::WORD;
  }
  if ($c == 0x57) {
    $p->{i}++;
    return $p->{unicode} && $p->{icase} ? $JSON::Schema::Corvus::Regex::Charset::NOT_FOLD_WORD
      : $JSON::Schema::Corvus::Regex::Charset::NOT_WORD;
  }
  if ($c == 0x73) { $p->{i}++; return $JSON::Schema::Corvus::Regex::Charset::SPACE }
  if ($c == 0x53) { $p->{i}++; return $JSON::Schema::Corvus::Regex::Charset::NOT_SPACE }
  if ($c == 0x70 || $c == 0x50) {
    # Without the u flag \p is the letter p.
    return undef unless $p->{unicode};
    $p->{i}++;
    $p->_fail('invalid property name') unless $p->_eat(0x7B);
    my $start = $p->{i};
    $p->{i}++ while $p->_more && $p->_peek != 0x7D;
    my $expr = join '', map { chr } @{ $p->{p} }[ $start .. $p->{i} - 1 ];
    $p->_fail('invalid property name') unless $p->_eat(0x7D);
    my $set = JSON::Schema::Corvus::Regex::Charset::property_set($expr) or $p->_fail('invalid property name');
    if ($c == 0x50) {
      $p->_charge(RANGE_SIZE * @{ $set->{ranges} } / 2);
      return JSON::Schema::Corvus::Regex::Charset::new_set($set->{ranges}, 1);
    }
    return $set;
  }
  return undef;
}

sub _hex_value {
  my ($c) = @_;
  return $c - 0x30 if $c >= 0x30 && $c <= 0x39;
  return $c - 0x61 + 10 if $c >= 0x61 && $c <= 0x66;
  return $c - 0x41 + 10 if $c >= 0x41 && $c <= 0x46;
  return -1;
}

# Exactly n hexadecimal digits, or -1, consuming nothing, when they are not there.
sub _hex {
  my ($p, $n) = @_;
  return -1 if $p->{i} + $n > @{ $p->{p} };
  my $v = 0;
  for my $k (0 .. $n - 1) {
    my $d = _hex_value($p->{p}[ $p->{i} + $k ]);
    return -1 if $d < 0;
    $v = $v * 16 + $d;
  }
  $p->{i} += $n;
  return $v;
}

sub _is_ascii_letter { ($_[0] >= 0x61 && $_[0] <= 0x7A) || ($_[0] >= 0x41 && $_[0] <= 0x5A) }

# The characters the u flag lets an identity escape name.
my %SYNTAX = map { ord($_) => 1 } split //, '^$\\.*+?()[]{}|/';

# The escape after a backslash, in or out of a class: the code point it names.
sub _character_escape {
  my ($p, $in_class) = @_;
  my $c = $p->_peek;
  $p->{i}++;
  return 0x0C if $c == 0x66;
  return 0x0A if $c == 0x6E;
  return 0x0D if $c == 0x72;
  return 0x09 if $c == 0x74;
  return 0x0B if $c == 0x76;
  if ($c == 0x63) {
    my $l = $p->_peek;
    if (_is_ascii_letter($l)) {
      $p->{i}++;
      return $l % 32;
    }
    if (!$p->{unicode} && $in_class && (_is_digit($l) || $l == 0x5F)) {
      $p->{i}++;
      return $l % 32;
    }
    $p->_fail('invalid control escape') if $p->{unicode};
    # Annex B reads a \c that is not a control escape as a backslash followed by the letter c.
    $p->{i}--;
    return 0x5C;
  }
  if ($c == 0x30) {
    if ($p->_more && _is_digit($p->_peek)) {
      $p->_fail('invalid decimal escape') if $p->{unicode};
      $p->{i}--;
      return $p->_legacy_octal;
    }
    return 0;
  }
  if ($c == 0x78) {
    my $h = $p->_hex(2);
    if ($h < 0) {
      $p->_fail('invalid \\x escape') if $p->{unicode};
      return 0x78;
    }
    return $h;
  }
  if ($c == 0x75) {
    my $u = $p->_unicode_escape($p->{unicode});
    if ($u < 0) {
      $p->_fail('invalid \\u escape') if $p->{unicode};
      return 0x75;
    }
    return $u;
  }
  if ($c >= 0x31 && $c <= 0x39 && !$p->{unicode} && $in_class) {
    $p->{i}--;
    return $p->_legacy_octal;
  }
  # Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B allows any
  # character, except k in a pattern with a named group.
  $p->_fail('invalid escape') if $p->{unicode} && !$SYNTAX{$c} && !($in_class && $c == 0x2D);
  $p->_fail('invalid escape') if $c == 0x6B && $p->{has_names};
  return $c;
}

# What follows a \u: the code point, or -1, consuming nothing, when it is not a well-formed escape. In unicode mode (the
# u flag, and always in a group name) the \u{...} form is read, and a surrogate pair written as two escapes is one code
# point.
sub _unicode_escape {
  my ($p, $unicode_mode) = @_;
  my $start = $p->{i};
  if ($unicode_mode && $p->_eat(0x7B)) {
    my $digits = $p->{i};
    my $v = 0;
    while ($p->_more && _hex_value($p->_peek) >= 0) {
      $v = $v * 16 + _hex_value($p->_peek) if $v <= 0x10FFFF;
      $p->{i}++;
    }
    if ($digits == $p->{i} || !$p->_eat(0x7D) || $v > 0x10FFFF) {
      $p->{i} = $start;
      return -1;
    }
    return $v;
  }
  my $u = $p->_hex(4);
  return -1 if $u < 0;
  if ($unicode_mode && $u >= 0xD800 && $u <= 0xDBFF && $p->{i} + 6 <= @{ $p->{p} } && $p->{p}[ $p->{i} ] == 0x5C
    && $p->{p}[ $p->{i} + 1 ] == 0x75) {
    my $save = $p->{i};
    $p->{i} += 2;
    my $low = $p->_hex(4);
    return 0x10000 + (($u - 0xD800) << 10) + ($low - 0xDC00) if $low >= 0xDC00 && $low <= 0xDFFF;
    $p->{i} = $save;
  }
  return $u;
}

# A character class from its '[' to its ']'.
sub _class {
  my ($p) = @_;
  $p->{i}++;
  my $negated = $p->_eat(0x5E);
  my @pairs;
  my $charged = 0;
  while (1) {
    $p->_fail('unterminated character class') unless $p->_more;
    last if $p->_eat(0x5D);
    $p->_charge(RANGE_SIZE * (@pairs / 2 - $charged));
    $charged = @pairs / 2;
    my ($first, $first_set) = $p->_class_atom;
    if ($p->_peek == 0x2D && $p->{i} + 1 < @{ $p->{p} } && $p->{p}[ $p->{i} + 1 ] != 0x5D) {
      $p->{i}++;
      my ($second, $second_set) = $p->_class_atom;
      if ($first_set || $second_set) {
        $p->_fail('invalid character class range') if $p->{unicode};
        # Annex B reads the '-' as itself when a class escape is at either end.
        push @pairs, $first_set ? @{ $first_set->{ranges} } : ($first, $first);
        push @pairs, 0x2D, 0x2D;
        push @pairs, $second_set ? @{ $second_set->{ranges} } : ($second, $second);
        next;
      }
      $p->_fail('range out of order in character class') if $second < $first;
      push @pairs, $first, $second;
      next;
    }
    push @pairs, $first_set ? @{ $first_set->{ranges} } : ($first, $first);
  }
  # Inside a case-insensitive group a character matches the class when it is equivalent to a member, and a negated
  # class excludes exactly those characters.
  $p->_charge(RANGE_SIZE * (@pairs / 2 - $charged));
  my $set = JSON::Schema::Corvus::Regex::Charset::new_set(\@pairs, 0);
  $set = $p->_fold($set) if $p->{icase};
  $set = JSON::Schema::Corvus::Regex::Charset::new_set($set->{ranges}, 1) if $negated;
  return { kind => 'set', set => $set };
}

# One member of a class: a code point, or (0, set) for a class escape.
sub _class_atom {
  my ($p) = @_;
  my $c = $p->_peek;
  $p->{i}++;
  return ($c, undef) if $c != 0x5C;
  $p->_fail('\\ at end of pattern') unless $p->_more;
  my $e = $p->_peek;
  if ($e == 0x62) {
    $p->{i}++;
    return (0x08, undef);
  }
  if (my $set = $p->_class_escape($e)) {
    return (0, $set);
  }
  return ($p->_character_escape(1), undef);
}

1;
