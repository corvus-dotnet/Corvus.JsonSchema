package JSON::Schema::Corvus::Regex::Vm;

# The backtracking matcher for the patterns that Perl's own engine is not given: a port of the Go port's compile.go and
# vm.go. It runs every pattern exactly as ECMA-262 says, and keeps a limit of time.
#
# The text is an array of code points (with the u flag) or of UTF-16 code units (without it), so a position is an
# index into it. Four things keep the matcher from the time a backtracking matcher can take, as in the Go port: a
# repetition whose body cannot consume a character is run once or not at all (simplify); a counted loop gives up at a
# position from which the rest of the pattern needs more text than is left (needs); a way the next character rules
# out is not taken (quick); and where the pattern has no backreference the matcher remembers the places from which
# every way has failed (memo), so a repetition inside a repetition takes time proportional to the pattern times the
# text. What is left is ended by the limit of the match.

use 5.026;
use strict;
use warnings;
use Time::HiRes ();
use JSON::Schema::Corvus::Regex::Charset ();

# Opcodes.
use constant {
  OP_CHAR => 0, OP_CHAR_BACK => 1, OP_SET => 2, OP_SET_BACK => 3, OP_FAIL => 4, OP_BOL => 5, OP_EOL => 6,
  OP_WB => 7, OP_NWB => 8, OP_LINE_START => 9, OP_LINE_END => 10, OP_WB_FOLD => 11, OP_NWB_FOLD => 12,
  OP_JMP => 13, OP_SPLIT => 14, OP_SAVE_TMP => 15, OP_CAPTURE => 16, OP_CLEAR => 17, OP_BACKREF => 18,
  OP_BACKREF_BACK => 19, OP_BACKREF_FOLD => 20, OP_BACKREF_FOLD_BACK => 21, OP_LOOK => 22, OP_LOOK_OK => 23,
  OP_LOOK_FAIL => 24, OP_REP => 25, OP_REP_LAZY => 26, OP_LOOP_INIT => 27, OP_LOOP => 28, OP_LOOP_ITER => 29,
  OP_MATCH => 30,
};

# The fields of an instruction (an array).
use constant { I_OP => 0, I_X => 1, I_Y => 2, I_A => 3, I_B => 4, I_R => 5, I_SET => 6, I_MIN => 7, I_MAX => 8,
  I_FLAG => 9, I_M => 10, I_NEED => 11, I_ITER => 12 };

# Entries of the backtracking stack: [kind, pc, pos, a].
use constant { E_CHOICE => 0, E_REP => 1, E_REP_LAZY => 2, E_LOOK => 3, E_LOOK_NEG => 4, E_MARK => 5, E_TMP => 6,
  E_CAP => 7, E_COUNTER => 8 };

use constant UNBOUNDED => -1;
use constant NO_MATCH => 1 << 30;
# The number of steps between two looks at the clock, and the most entries the stack of a match with a limit may hold.
use constant { CHECK_EVERY => 1 << 12, MAX_STACK => 1 << 20 };
# The number of loops whose state a memo point can tell apart; the largest memory of failed places, in bits; the steps
# for each position of the text that a match makes before it takes the memory into use; the depth of the stack beyond
# which a memo point is not marked.
use constant { MAX_MEMO_LOOPS => 2, MAX_MEMO_BITS => 1 << 26, MEMO_STEPS_PER_POSITION => 64,
  MAX_MARK_DEPTH => (1 << 20) / 2 };

my $C = 'JSON::Schema::Corvus::Regex::Charset';

# --- Compilation ---

# A program for the tree of a pattern read with or without the u flag.
sub compile_program {
  my ($root, $unicode) = @_;
  $root = _simplify($root, []);
  my %referenced;
  _collect_backrefs($root, \%referenced);
  # Slots follow group order, so the groups inside any one subtree take a contiguous range of slots.
  my %tracked;
  my $slot = 0;
  $tracked{$_} = $slot++ for sort { $a <=> $b } keys %referenced;
  my $c = { insts => [], tracked => \%tracked, loops => 0, unicode => $unicode ? 1 : 0, memo => !%tracked,
    open => [], points => [] };
  _emit($c, $root, 0);
  _add($c, [OP_MATCH]);
  my $p = {
    insts => $c->{insts}, groups => scalar keys %tracked, loops => $c->{loops}, units => $unicode ? 0 : 1,
    points => $c->{points},
  };
  $p->{need} = _set_needs($p->{insts});
  $p->{quick} = _set_quick($p->{insts});
  $p->{anchored} = _starts_at_bol($root);
  if (!$p->{anchored}) {
    my ($pairs, $empty, $ok) = _first_chars($root, []);
    $p->{first} = JSON::Schema::Corvus::Regex::Charset::new_set($pairs, 0) if $ok && !$empty;
  }
  return bless $p, __PACKAGE__;
}

# The tree with two rewrites V8 makes, neither of which changes what the pattern matches or what any group holds: a
# backreference to a group it is inside is the empty string (the group is not set until it closes, and a repetition
# clears it at the start of each iteration), and a repetition whose body cannot consume a character is the body once
# (a minimum of one or more) or nothing (a minimum of zero).
sub _simplify {
  my ($n, $open) = @_;
  if ($n->{kind} eq 'backref') {
    for my $g (@{ $n->{groups} }) {
      return $n unless grep { $_ == $g } @$open;
    }
    return { kind => 'empty' };
  }
  return $n unless $n->{subs} && @{ $n->{subs} };
  $open = [ @$open, $n->{group} ] if $n->{kind} eq 'group' && $n->{group};
  my @subs = map { _simplify($_, $open) } @{ $n->{subs} };
  my $out = { %$n, subs => \@subs };
  if ($out->{kind} eq 'repeat' && $out->{max} != 0 && !_consumes($subs[0])) {
    return $out->{min} == 0 ? { kind => 'empty' } : $subs[0];
  }
  return $out;
}

# Whether n can match one character or more (erring towards true).
sub _consumes {
  my ($n) = @_;
  my $k = $n->{kind};
  return 1 if $k eq 'char' || $k eq 'set' || $k eq 'backref';
  if ($k eq 'cat' || $k eq 'alt' || $k eq 'group') {
    for (@{ $n->{subs} }) { return 1 if _consumes($_) }
    return 0;
  }
  return $n->{max} != 0 && _consumes($n->{subs}[0]) if $k eq 'repeat';
  return 0;
}

sub _collect_backrefs {
  my ($n, $out) = @_;
  $out->{$_} = 1 for $n->{kind} eq 'backref' ? @{ $n->{groups} } : ();
  _collect_backrefs($_, $out) for @{ $n->{subs} // [] };
}

sub _add { my ($c, $i) = @_; push @{ $c->{insts} }, $i; return $#{ $c->{insts} } }
sub _pc { scalar @{ $_[0]{insts} } }

# The number of a new memo point for an instruction written here, or 0 when a choice here cannot be remembered: the
# pattern has a backreference, or the place is inside a loop with an upper bound, a least count over one, or more
# loops than a point can tell apart.
sub _point {
  my ($c) = @_;
  return 0 if !$c->{memo} || @{ $c->{open} } > MAX_MEMO_LOOPS;
  my (@loops, @mins);
  for my $l (@{ $c->{open} }) {
    return 0 unless $l->[2];
    push @loops, $l->[0];
    push @mins, $l->[1];
  }
  push @{ $c->{points} }, [ \@loops, \@mins ];
  return scalar @{ $c->{points} };
}

# The first and last slot of the tracked groups inside n, or () when it has none.
sub _group_range {
  my ($c, $n) = @_;
  my ($lo, $hi) = (-1, -1);
  my $walk;
  $walk = sub {
    my ($n) = @_;
    if ($n->{kind} eq 'group' && $n->{group} && exists $c->{tracked}{ $n->{group} }) {
      my $t = $c->{tracked}{ $n->{group} };
      $lo = $t if $lo < 0 || $t < $lo;
      $hi = $t if $t > $hi;
    }
    $walk->($_) for @{ $n->{subs} // [] };
  };
  $walk->($n);
  undef $walk;
  return $lo >= 0 ? ($lo, $hi) : ();
}

# Writes the instructions for n; with $back they match right to left, as the body of a lookbehind must.
sub _emit {
  my ($c, $n, $back) = @_;
  my $k = $n->{kind};
  if ($k eq 'empty') { return }
  if ($k eq 'char') { _add($c, [ $back ? OP_CHAR_BACK : OP_CHAR, 0, 0, 0, 0, $n->{r} ]); return }
  if ($k eq 'set') {
    if ($n->{set}->is_empty) { _add($c, [OP_FAIL]) }
    else { _add($c, [ $back ? OP_SET_BACK : OP_SET, 0, 0, 0, 0, 0, $n->{set} ]) }
    return;
  }
  if ($k eq 'cat') {
    my @subs = @{ $n->{subs} };
    @subs = reverse @subs if $back;
    _emit($c, $_, $back) for @subs;
    return;
  }
  if ($k eq 'alt') {
    my @jumps;
    my $subs = $n->{subs};
    for my $i (0 .. $#$subs) {
      if ($i == $#$subs) {
        _emit($c, $subs->[$i], $back);
        last;
      }
      my $split = _add($c, [ OP_SPLIT, 0, 0, 0, 0, 0, undef, 0, 0, 0, _point($c) ]);
      $c->{insts}[$split][I_X] = _pc($c);
      _emit($c, $subs->[$i], $back);
      push @jumps, _add($c, [OP_JMP]);
      $c->{insts}[$split][I_Y] = _pc($c);
    }
    $c->{insts}[$_][I_X] = _pc($c) for @jumps;
    return;
  }
  if ($k eq 'group') {
    if (!$n->{group} || !exists $c->{tracked}{ $n->{group} }) {
      _emit($c, $n->{subs}[0], $back);
      return;
    }
    my $t = $c->{tracked}{ $n->{group} };
    _add($c, [ OP_SAVE_TMP, 0, 0, $t ]);
    _emit($c, $n->{subs}[0], $back);
    _add($c, [ OP_CAPTURE, 0, 0, $t, 0, 0, undef, 0, 0, $back ? 1 : 0 ]);
    return;
  }
  if ($k eq 'bol') { _add($c, [OP_BOL]); return }
  if ($k eq 'eol') { _add($c, [OP_EOL]); return }
  if ($k eq 'line_start') { _add($c, [OP_LINE_START]); return }
  if ($k eq 'line_end') { _add($c, [OP_LINE_END]); return }
  if ($k eq 'wb') { _add($c, [ $n->{fold} ? OP_WB_FOLD : OP_WB ]); return }
  if ($k eq 'nwb') { _add($c, [ $n->{fold} ? OP_NWB_FOLD : OP_NWB ]); return }
  if ($k eq 'backref') {
    my $op = $n->{fold} ? ($back ? OP_BACKREF_FOLD_BACK : OP_BACKREF_FOLD) : ($back ? OP_BACKREF_BACK : OP_BACKREF);
    # When several groups share the name, at most one has taken part and the others match the empty string, so
    # matching each in turn matches the one that took part.
    for my $g (@{ $n->{groups} }) {
      _add($c, [ $op, 0, 0, $c->{tracked}{$g}, 0, 0, undef, 0, 0, $c->{unicode} ]);
    }
    return;
  }
  if ($k eq 'look') {
    my $look = _add($c, [ OP_LOOK, 0, 0, 0, 0, 0, undef, 0, 0, $n->{negative} ? 1 : 0 ]);
    # Whether the body matches depends on nothing outside it.
    my $outer = $c->{open};
    $c->{open} = [];
    _emit($c, $n->{subs}[0], $n->{behind});
    $c->{open} = $outer;
    _add($c, [ $n->{negative} ? OP_LOOK_FAIL : OP_LOOK_OK ]);
    $c->{insts}[$look][I_X] = _pc($c);
    return;
  }
  if ($k eq 'repeat') { _emit_repeat($c, $n, $back); return }
  die "unknown node $k";
}

sub _emit_repeat {
  my ($c, $n, $back) = @_;
  my $body = $n->{subs}[0];
  return if $n->{max} == 0;
  my ($min, $max) = ($n->{min}, $n->{max});
  if (!$back && ($body->{kind} eq 'char' || ($body->{kind} eq 'set' && !$body->{set}->is_empty))) {
    _add($c, [ $n->{lazy} ? OP_REP_LAZY : OP_REP, 0, 0, 0, 0, $body->{r} // 0, $body->{set}, $min, $max, 0,
      _point($c) ]);
    return;
  }
  my @clear = _group_range($c, $body);
  my $emit_body = sub {
    _add($c, [ OP_CLEAR, 0, 0, $clear[0], $clear[1] ]) if @clear;
    _emit($c, $body, $back);
  };
  my $split = sub { _add($c, [ OP_SPLIT, 0, 0, 0, 0, 0, undef, 0, 0, 0, _point($c) ]) };
  my $set_split = sub {
    my ($at, $enter, $exit) = @_;
    @{ $c->{insts}[$at] }[ I_X, I_Y ] = $n->{lazy} ? ($exit, $enter) : ($enter, $exit);
  };
  # A body that always consumes something needs no check for empty iterations.
  if (!_nullable($body)) {
    if ($min == 0 && $max == UNBOUNDED) {
      my $at = $split->();
      my $enter = _pc($c);
      $emit_body->();
      _add($c, [ OP_JMP, $at ]);
      $set_split->($at, $enter, _pc($c));
      return;
    }
    if ($min == 1 && $max == UNBOUNDED) {
      my $enter = _pc($c);
      $emit_body->();
      my $at = $split->();
      $set_split->($at, $enter, _pc($c));
      return;
    }
    if ($min == 0 && $max == 1) {
      my $at = $split->();
      my $enter = _pc($c);
      $emit_body->();
      $set_split->($at, $enter, _pc($c));
      return;
    }
  }
  my $k = $c->{loops}++;
  _add($c, [ OP_LOOP_INIT, 0, 0, $k ]);
  my $iter = $back ? 0 : _min_length($body);
  my $simple = $max == UNBOUNDED && $min <= 1;
  # The loop is left or entered from here, and what follows either depends on the loops around this one.
  my $point = $simple ? _point($c) : 0;
  my $loop = _add($c, [ OP_LOOP, 0, 0, $k, 0, 0, undef, $min, $max, $n->{lazy} ? 1 : 0, $point, 0, $iter ]);
  _add($c, [ OP_LOOP_ITER, 0, 0, $k ]);
  push @{ $c->{open} }, [ $k, $min, $simple ? 1 : 0 ];
  $emit_body->();
  pop @{ $c->{open} };
  _add($c, [ OP_JMP, $loop ]);
  $c->{insts}[$loop][I_X] = _pc($c);
}

# The least number of characters a match of n consumes, capped at NO_MATCH.
sub _min_length {
  my ($n) = @_;
  my $k = $n->{kind};
  return 1 if $k eq 'char';
  return $n->{set}->is_empty ? NO_MATCH : 1 if $k eq 'set';
  if ($k eq 'cat') {
    my $total = 0;
    for (@{ $n->{subs} }) {
      $total += _min_length($_);
      return NO_MATCH if $total >= NO_MATCH;
    }
    return $total;
  }
  if ($k eq 'alt') {
    my $least = NO_MATCH;
    for (@{ $n->{subs} }) { my $l = _min_length($_); $least = $l if $l < $least }
    return $least;
  }
  return _min_length($n->{subs}[0]) if $k eq 'group';
  return _times_need($n->{min}, _min_length($n->{subs}[0])) if $k eq 'repeat';
  return 0;
}

sub _times_need {
  my ($count, $need) = @_;
  return 0 if $count == 0 || $need == 0;
  return NO_MATCH if $need >= NO_MATCH || $count >= NO_MATCH / $need;
  return $count * $need;
}

# Whether n can match without consuming anything (erring towards true).
sub _nullable {
  my ($n) = @_;
  my $k = $n->{kind};
  return 0 if $k eq 'char' || $k eq 'set';
  if ($k eq 'cat') {
    for (@{ $n->{subs} }) { return 0 unless _nullable($_) }
    return 1;
  }
  if ($k eq 'alt') {
    for (@{ $n->{subs} }) { return 1 if _nullable($_) }
    return 0;
  }
  return _nullable($n->{subs}[0]) if $k eq 'group';
  return $n->{min} == 0 || _nullable($n->{subs}[0]) if $k eq 'repeat';
  return 1;
}

# For every instruction, the least number of characters any way from it to a match consumes going forward, written
# into the counted loops; returns the need of the whole program (setNeeds).
sub _set_needs {
  my ($insts) = @_;
  my @need = (NO_MATCH) x @$insts;
  my $changed = 1;
  while ($changed) {
    $changed = 0;
    for (my $pc = $#$insts; $pc >= 0; $pc--) {
      my $i = $insts->[$pc];
      my $op = $i->[I_OP];
      my $n = NO_MATCH;
      my $next = sub {
        my ($to, $weight) = @_;
        my $v = $need[$to] + $weight;
        $n = $v if $v < $n;
      };
      if ($op == OP_MATCH || $op == OP_LOOK_OK || $op == OP_LOOK_FAIL) { $n = 0 }
      elsif ($op == OP_FAIL) { }
      elsif ($op == OP_CHAR || $op == OP_SET) { $next->($pc + 1, 1) }
      elsif ($op == OP_REP || $op == OP_REP_LAZY) { $next->($pc + 1, $i->[I_MIN]) }
      elsif ($op == OP_JMP) {
        my $head = $insts->[ $i->[I_X] ];
        if ($i->[I_X] < $pc && $head->[I_OP] == OP_LOOP) { $next->($head->[I_X], 0) }
        elsif ($i->[I_X] < $pc && $head->[I_OP] == OP_SPLIT) { $next->($head->[I_X], 0); $next->($head->[I_Y], 0) }
        else { $next->($i->[I_X], 0) }
      }
      elsif ($op == OP_LOOK) { $next->($i->[I_X], 0) }
      elsif ($op == OP_SPLIT) { $next->($i->[I_X], 0); $next->($i->[I_Y], 0) }
      elsif ($op == OP_LOOP) { $next->($pc + 1, 0); $next->($i->[I_X], 0) }
      elsif ($op == OP_LOOP_INIT) {
        my $loop = $insts->[ $pc + 1 ];
        $next->($pc + 1, _times_need($loop->[I_MIN], $loop->[I_ITER]));
      }
      else { $next->($pc + 1, 0) }
      $n = NO_MATCH if $n > NO_MATCH;
      if ($n < $need[$pc]) {
        $need[$pc] = $n;
        $changed = 1;
      }
    }
  }
  for my $pc (0 .. $#$insts) {
    $insts->[$pc][I_NEED] = $need[$pc] if $insts->[$pc][I_OP] == OP_LOOP;
  }
  return $need[0];
}

# A quick says what can come next on a way from an instruction to the end of its match: [ascii (128 flags as a string
# of bits), other, mid, end]. The matcher looks at it before it takes a way, and a way the next character rules out is
# not taken (setQuick).
sub _set_quick {
  my ($insts) = @_;
  my $all_bits = '';
  vec($all_bits, $_, 1) = 1 for 0 .. 127;
  my $none_bits = '';
  vec($none_bits, 127, 1) = 0;
  my $all = [ $all_bits, 1, 1, 1 ];
  my @q = map { [ $none_bits, 0, 0, 0 ] } @$insts;
  my $union = sub {
    my ($a, $b) = @_;
    return [ $a->[0] | $b->[0], $a->[1] || $b->[1], $a->[2] || $b->[2], $a->[3] || $b->[3] ];
  };
  my $add_set = sub {
    my ($n, $set) = @_;
    my $bits = $n->[0];
    my $r = $set->{ranges};
    for (my $i = 0; $i < @$r && $r->[$i] < 128; $i += 2) {
      my $hi = $r->[$i + 1] < 127 ? $r->[$i + 1] : 127;
      vec($bits, $_, 1) = 1 for $r->[$i] .. $hi;
    }
    return [ $bits, $n->[1] || (@$r && $r->[-1] >= 128) ? 1 : 0, $n->[2], $n->[3] ];
  };
  my $add_rune = sub {
    my ($n, $c) = @_;
    my $bits = $n->[0];
    if ($c < 128) {
      vec($bits, $c, 1) = 1;
      return [ $bits, $n->[1], $n->[2], $n->[3] ];
    }
    return [ $bits, 1, $n->[2], $n->[3] ];
  };
  my $changed = 1;
  while ($changed) {
    $changed = 0;
    for (my $pc = $#$insts; $pc >= 0; $pc--) {
      my $i = $insts->[$pc];
      my $op = $i->[I_OP];
      my $n = [ $none_bits, 0, 0, 0 ];
      if ($op == OP_MATCH || $op == OP_LOOK_OK || $op == OP_LOOK_FAIL) { $n = $all }
      elsif ($op == OP_FAIL) { }
      elsif ($op == OP_CHAR) { $n = $add_rune->($n, $i->[I_R]) }
      elsif ($op == OP_SET) { $n = $add_set->($n, $i->[I_SET]) }
      elsif ($op == OP_REP || $op == OP_REP_LAZY) {
        $n = $i->[I_SET] ? $add_set->($n, $i->[I_SET]) : $add_rune->($n, $i->[I_R]);
        $n = $union->($n, $q[ $pc + 1 ]) if $i->[I_MIN] == 0;
      }
      elsif ($op == OP_JMP) {
        $n = $q[ $i->[I_X] ];
        my $head = $insts->[ $i->[I_X] ];
        if ($i->[I_X] < $pc && $head->[I_OP] == OP_LOOP) { $n = $union->($n, $q[ $head->[I_X] ]) }
        elsif ($i->[I_X] < $pc && $head->[I_OP] == OP_SPLIT) {
          $n = $union->($union->($n, $q[ $head->[I_X] ]), $q[ $head->[I_Y] ]);
        }
      }
      elsif ($op == OP_SPLIT) { $n = $union->($q[ $i->[I_X] ], $q[ $i->[I_Y] ]) }
      elsif ($op == OP_LOOP) { $n = $union->($q[ $pc + 1 ], $q[ $i->[I_X] ]) }
      elsif ($op == OP_EOL) { $n = [ $none_bits, 0, 0, $q[ $pc + 1 ][3] ] }
      elsif ($op == OP_LOOK) {
        $n = $q[ $i->[I_X] ];
        if (!$i->[I_FLAG]) {
          my $b = $q[ $pc + 1 ];
          $n = [ $n->[0] & $b->[0], $n->[1] && $b->[1], $n->[2] && $b->[2], $n->[3] && $b->[3] ];
        }
      }
      elsif ($op == OP_CHAR_BACK || $op == OP_SET_BACK || $op == OP_BACKREF || $op == OP_BACKREF_BACK
        || $op == OP_BACKREF_FOLD || $op == OP_BACKREF_FOLD_BACK) {
        $n = $all;
      }
      else { $n = $q[ $pc + 1 ] }
      my $old = $q[$pc];
      if ($n->[0] ne $old->[0] || ($n->[1] ? 1 : 0) != ($old->[1] ? 1 : 0) || ($n->[2] ? 1 : 0) != ($old->[2] ? 1 : 0)
        || ($n->[3] ? 1 : 0) != ($old->[3] ? 1 : 0)) {
        $q[$pc] = [ $n->[0], $n->[1] ? 1 : 0, $n->[2] ? 1 : 0, $n->[3] ? 1 : 0 ];
        $changed = 1;
      }
    }
  }
  return \@q;
}

# Whether every way through n begins with ^.
sub _starts_at_bol {
  my ($n) = @_;
  my $k = $n->{kind};
  return 1 if $k eq 'bol';
  return _starts_at_bol($n->{subs}[0]) if $k eq 'cat' || $k eq 'group';
  if ($k eq 'alt') {
    for (@{ $n->{subs} }) { return 0 unless _starts_at_bol($_) }
    return 1;
  }
  return $n->{min} >= 1 && _starts_at_bol($n->{subs}[0]) if $k eq 'repeat';
  return 0;
}

# The code points a match of n can start with, as inclusive pairs; whether n can match the empty string; and whether
# the set is known. Assertions are taken to match the empty string, which can only make the set too large.
sub _first_chars {
  my ($n, $pairs) = @_;
  my $k = $n->{kind};
  return ([ @$pairs, $n->{r}, $n->{r} ], 0, 1) if $k eq 'char';
  return ([ @$pairs, @{ $n->{set}{ranges} } ], 0, 1) if $k eq 'set';
  if ($k eq 'cat') {
    for my $sub (@{ $n->{subs} }) {
      my ($p, $empty, $ok) = _first_chars($sub, $pairs);
      return ([], 0, 0) unless $ok;
      $pairs = $p;
      return ($pairs, 0, 1) unless $empty;
    }
    return ($pairs, 1, 1);
  }
  if ($k eq 'alt') {
    my $any_empty = 0;
    for my $sub (@{ $n->{subs} }) {
      my ($p, $empty, $ok) = _first_chars($sub, $pairs);
      return ([], 0, 0) unless $ok;
      $pairs = $p;
      $any_empty ||= $empty;
    }
    return ($pairs, $any_empty, 1);
  }
  return _first_chars($n->{subs}[0], $pairs) if $k eq 'group';
  if ($k eq 'repeat') {
    return ($pairs, 1, 1) if $n->{max} == 0;
    my ($p, $empty, $ok) = _first_chars($n->{subs}[0], $pairs);
    return ($p, $empty || $n->{min} == 0, $ok);
  }
  return ([], 0, 0) if $k eq 'backref';
  return ($pairs, 1, 1);
}

# --- Matching ---

# The text as the program reads it: code points, or UTF-16 code units for a pattern read without the u flag.
sub text_of {
  my ($self, $s) = @_;
  my @in = unpack 'W*', $s;
  if ($self->{units} && grep { $_ > 0xFFFF } @in) {
    @in = map { $_ > 0xFFFF ? (0xD800 + (($_ - 0x10000) >> 10), 0xDC00 + (($_ - 0x10000) & 0x3FF)) : $_ } @in;
  }
  return \@in;
}

# Whether the program matches anywhere in the text (a Perl string). With a limit (seconds) above zero the match is
# abandoned when it runs for longer: it then returns (undef, 'time') or (undef, 'memory').
sub match {
  my ($self, $s, $limit) = @_;
  my $in = $self->text_of($s);
  my $st = {
    caps => [ (-1) x (2 * $self->{groups}) ], tmp => [ (-1) x $self->{groups} ], counts => [ (0) x (2 * $self->{loops}) ],
    limit => $limit // 0, deadline => undef, stop => undef, checks => 0, memo => undef,
    memo_off => !@{ $self->{points} }, width => @$in + 1,
  };
  my $ok = $self->_search($st, $in);
  return (undef, $st->{stop}) if $st->{stop};
  return ($ok ? 1 : 0);
}

sub _search {
  my ($self, $st, $in) = @_;
  my $len = @$in;
  return 0 if $len < $self->{need};
  return $self->_run($st, $in, 0) if $self->{anchored};
  my $first = $self->{first};
  for (my $at = 0; ; $at++) {
    return 0 if $len - $at < $self->{need};
    if ($at == $len) {
      return !$first && $self->_run($st, $in, $at) ? 1 : 0;
    }
    next if $first && !$first->contains($in->[$at]);
    return 1 if $self->_run($st, $in, $at);
    return 0 if $st->{stop};
  }
}

# Takes the memory of failed places into use when the match has made enough steps to pay for it.
sub _remember {
  my ($self, $st) = @_;
  my $bits = @{ $self->{points} } * $st->{width} << MAX_MEMO_LOOPS;
  if ($bits > MAX_MEMO_BITS) {
    $st->{memo_off} = 1;
    return;
  }
  return if $st->{checks} * CHECK_EVERY < $bits / 64 + MEMO_STEPS_PER_POSITION * $st->{width};
  my $memo = '';
  vec($memo, $bits - 1, 1) = 0 if $bits;
  $st->{memo} = \$memo;
}

# The bit of the memory for memo point m at a position, with the state of the loops around the point: for each,
# whether it would refuse an iteration that ended here as empty.
sub _memo_index {
  my ($self, $st, $m, $pos) = @_;
  my ($loops, $mins) = @{ $self->{points}[ $m - 1 ] };
  my $idx = (($m - 1) * $st->{width} + $pos) << MAX_MEMO_LOOPS;
  my $counts = $st->{counts};
  for my $k (0 .. $#$loops) {
    my $l = $loops->[$k];
    $idx |= 1 << $k if $counts->[ 2 * $l ] > $mins->[$k] && $counts->[ 2 * $l + 1 ] == $pos;
  }
  return $idx;
}

# Called every CHECK_EVERY steps: whether the match may go on.
sub _check {
  my ($self, $st, $depth) = @_;
  if (!$st->{memo} && !$st->{memo_off}) {
    $st->{checks}++;
    $self->_remember($st);
  }
  return 1 if $st->{limit} <= 0;
  if ($depth > MAX_STACK) {
    $st->{stop} = 'memory';
    return 0;
  }
  my $now = Time::HiRes::time();
  if (!defined $st->{deadline}) {
    $st->{deadline} = $now + $st->{limit};
    return 1;
  }
  if ($now > $st->{deadline}) {
    $st->{stop} = 'time';
    return 0;
  }
  return 1;
}

sub _allows {
  my ($q, $in, $pos) = @_;
  return $q->[3] if $pos >= @$in;
  return 1 if $q->[2];
  my $c = $in->[$pos];
  return $c < 128 ? vec($q->[0], $c, 1) : $q->[1];
}

sub _undo {
  my ($st, $e) = @_;
  my $kind = $e->[0];
  if ($kind == E_TMP) { $st->{tmp}[ $e->[1] ] = $e->[2] }
  elsif ($kind == E_CAP) { @{ $st->{caps} }[ 2 * $e->[1], 2 * $e->[1] + 1 ] = ($e->[2], $e->[3]) }
  elsif ($kind == E_COUNTER) { @{ $st->{counts} }[ 2 * $e->[1], 2 * $e->[1] + 1 ] = ($e->[2], $e->[3]) }
}

# The width (0 or 1) of the character at a position when a single-character loop's character or class matches it.
sub _one {
  my ($i, $in, $at) = @_;
  return 0 if $at >= @$in;
  my $c = $in->[$at];
  return ($i->[I_SET] ? $i->[I_SET]->contains($c) : $c == $i->[I_R]) ? 1 : 0;
}

sub _is_word { JSON::Schema::Corvus::Regex::Charset::is_word($_[0]) }

# Whether the program matches at the position start. Choices are kept on an explicit stack; when the match fails,
# every group and counter is back to the value it had on entry.
sub _run {
  my ($self, $st, $in, $start) = @_;
  my $insts = $self->{insts};
  my $quick = $self->{quick};
  my $caps = $st->{caps};
  my $counts = $st->{counts};
  my $tmp = $st->{tmp};
  my $len = @$in;
  my @stack;
  my ($pc, $pos) = (0, $start);
  my $steps = $st->{steps} // CHECK_EVERY;
  my $memo = $st->{memo};
  my $unicode_fold;
  while (1) {
    if (--$steps < 0) {
      if (!$self->_check($st, scalar @stack)) {
        $st->{steps} = 0;
        return 0;
      }
      $steps = CHECK_EVERY;
      $memo = $st->{memo};
    }
    my $i = $insts->[$pc];
    my $op = $i->[I_OP];
    my $failed = 1;
    if ($op == OP_CHAR) {
      if ($pos < $len && $in->[$pos] == $i->[I_R]) { $pos++; $pc++; next }
    }
    elsif ($op == OP_SET) {
      if ($pos < $len && $i->[I_SET]->contains($in->[$pos])) { $pos++; $pc++; next }
    }
    elsif ($op == OP_REP) {
      my ($at, $n, $floor) = ($pos, 0, $pos);
      my ($max, $min) = ($i->[I_MAX], $i->[I_MIN]);
      if (my $set = $i->[I_SET]) {
        while ($n != $max && $at < $len && $set->contains($in->[$at])) {
          $at++;
          $n++;
          $floor = $at if $n == $min;
        }
      }
      else {
        my $r = $i->[I_R];
        while ($n != $max && $at < $len && $in->[$at] == $r) {
          $at++;
          $n++;
          $floor = $at if $n == $min;
        }
      }
      $steps -= ($at - $pos) >> 3;
      if ($n >= $min) {
        if ($memo && $i->[I_M]) {
          my $idx = $self->_memo_index($st, $i->[I_M], $at);
          if (vec($$memo, $idx, 1)) {
            # What follows the loop has failed from here before. The loop gives a character back.
            push @stack, [ E_REP, $pc + 1, $at, $floor ] if $at > $floor;
            goto BACKTRACK;
          }
          push @stack, [ E_MARK, 0, $idx ] if $at == $floor && @stack < MAX_MARK_DEPTH;
        }
        push @stack, [ E_REP, $pc + 1, $at, $floor ] if $at > $floor;
        $pos = $at;
        $pc++;
        next;
      }
    }
    elsif ($op == OP_SPLIT) {
      # A place from which every way has failed before fails at once. The mark is under whatever comes of this place,
      # so that the place is remembered when it has all failed.
      if ($memo && $i->[I_M]) {
        my $idx = $self->_memo_index($st, $i->[I_M], $pos);
        goto BACKTRACK if vec($$memo, $idx, 1);
        push @stack, [ E_MARK, 0, $idx ] if @stack < MAX_MARK_DEPTH;
      }
      # A way the next character rules out is not taken, and no choice is kept for it.
      my $x_ok = _allows($quick->[ $i->[I_X] ], $in, $pos);
      if (!_allows($quick->[ $i->[I_Y] ], $in, $pos)) {
        goto BACKTRACK unless $x_ok;
        $pc = $i->[I_X];
        next;
      }
      if (!$x_ok) {
        $pc = $i->[I_Y];
        next;
      }
      push @stack, [ E_CHOICE, $i->[I_Y], $pos ];
      $pc = $i->[I_X];
      next;
    }
    elsif ($op == OP_JMP) { $pc = $i->[I_X]; next }
    elsif ($op == OP_BOL) { if ($pos == 0) { $pc++; next } }
    elsif ($op == OP_EOL) { if ($pos == $len) { $pc++; next } }
    elsif ($op == OP_FAIL) { }
    elsif ($op == OP_CHAR_BACK) {
      if ($pos > 0 && $in->[ $pos - 1 ] == $i->[I_R]) { $pos--; $pc++; next }
    }
    elsif ($op == OP_SET_BACK) {
      if ($pos > 0 && $i->[I_SET]->contains($in->[ $pos - 1 ])) { $pos--; $pc++; next }
    }
    elsif ($op == OP_WB || $op == OP_NWB) {
      my $before = $pos > 0 && _is_word($in->[ $pos - 1 ]) ? 1 : 0;
      my $after = $pos < $len && _is_word($in->[$pos]) ? 1 : 0;
      if (($before != $after) == ($op == OP_WB)) { $pc++; next }
    }
    elsif ($op == OP_WB_FOLD || $op == OP_NWB_FOLD) {
      my $before = $pos > 0 && JSON::Schema::Corvus::Regex::Charset::is_fold_word($in->[ $pos - 1 ]) ? 1 : 0;
      my $after = $pos < $len && JSON::Schema::Corvus::Regex::Charset::is_fold_word($in->[$pos]) ? 1 : 0;
      if (($before != $after) == ($op == OP_WB_FOLD)) { $pc++; next }
    }
    elsif ($op == OP_LINE_START) {
      if ($pos == 0 || JSON::Schema::Corvus::Regex::Charset::is_line_terminator($in->[ $pos - 1 ])) { $pc++; next }
    }
    elsif ($op == OP_LINE_END) {
      if ($pos == $len || JSON::Schema::Corvus::Regex::Charset::is_line_terminator($in->[$pos])) { $pc++; next }
    }
    elsif ($op == OP_SAVE_TMP) {
      push @stack, [ E_TMP, $i->[I_A], $tmp->[ $i->[I_A] ] ];
      $tmp->[ $i->[I_A] ] = $pos;
      $pc++;
      next;
    }
    elsif ($op == OP_CAPTURE) {
      my $g = $i->[I_A];
      push @stack, [ E_CAP, $g, $caps->[ 2 * $g ], $caps->[ 2 * $g + 1 ] ];
      if ($i->[I_FLAG]) { @$caps[ 2 * $g, 2 * $g + 1 ] = ($pos, $tmp->[$g]) }
      else { @$caps[ 2 * $g, 2 * $g + 1 ] = ($tmp->[$g], $pos) }
      $pc++;
      next;
    }
    elsif ($op == OP_CLEAR) {
      $steps -= $i->[I_B] - $i->[I_A];
      for my $g ($i->[I_A] .. $i->[I_B]) {
        if ($caps->[ 2 * $g ] >= 0) {
          push @stack, [ E_CAP, $g, $caps->[ 2 * $g ], $caps->[ 2 * $g + 1 ] ];
          @$caps[ 2 * $g, 2 * $g + 1 ] = (-1, -1);
        }
      }
      $pc++;
      next;
    }
    elsif ($op == OP_BACKREF || $op == OP_BACKREF_FOLD) {
      # A group that has not taken part matches the empty string.
      my ($from, $to) = @$caps[ 2 * $i->[I_A], 2 * $i->[I_A] + 1 ];
      if ($from < 0) { $pc++; next }
      my $n = $to - $from;
      if ($len - $pos >= $n) {
        my $ok = 1;
        for my $k (0 .. $n - 1) {
          my ($got, $want) = ($in->[ $pos + $k ], $in->[ $from + $k ]);
          next if $got == $want;
          if ($op == OP_BACKREF_FOLD
            && JSON::Schema::Corvus::Regex::Charset::canonicalize($got, $i->[I_FLAG])
              == JSON::Schema::Corvus::Regex::Charset::canonicalize($want, $i->[I_FLAG])) {
            next;
          }
          $ok = 0;
          last;
        }
        if ($ok) {
          $steps -= $n >> 3;
          $pos += $n;
          $pc++;
          next;
        }
      }
    }
    elsif ($op == OP_BACKREF_BACK || $op == OP_BACKREF_FOLD_BACK) {
      my ($from, $to) = @$caps[ 2 * $i->[I_A], 2 * $i->[I_A] + 1 ];
      if ($from < 0) { $pc++; next }
      my $n = $to - $from;
      if ($pos >= $n) {
        my $ok = 1;
        for my $k (1 .. $n) {
          my ($got, $want) = ($in->[ $pos - $k ], $in->[ $to - $k ]);
          next if $got == $want;
          if ($op == OP_BACKREF_FOLD_BACK
            && JSON::Schema::Corvus::Regex::Charset::canonicalize($got, $i->[I_FLAG])
              == JSON::Schema::Corvus::Regex::Charset::canonicalize($want, $i->[I_FLAG])) {
            next;
          }
          $ok = 0;
          last;
        }
        if ($ok) {
          $steps -= $n >> 3;
          $pos -= $n;
          $pc++;
          next;
        }
      }
    }
    elsif ($op == OP_LOOK) {
      # What follows a lookaround starts where the lookaround does, and so does the body of a lookahead.
      if (_allows($quick->[$pc], $in, $pos)) {
        push @stack, [ $i->[I_FLAG] ? E_LOOK_NEG : E_LOOK, $i->[I_X], $pos ];
        $pc++;
        next;
      }
    }
    elsif ($op == OP_LOOK_OK) {
      # The body matched. A lookaround is atomic, so the choices made inside it are dropped; the undo entries stay,
      # because the groups it set remain set until something below this point is retried.
      my $f = $#stack;
      $f-- while $stack[$f][0] != E_LOOK;
      $pos = $stack[$f][2];
      my @kept = grep { $_->[0] >= E_TMP } @stack[ $f + 1 .. $#stack ];
      splice @stack, $f, @stack - $f, @kept;
      $pc++;
      next;
    }
    elsif ($op == OP_LOOK_FAIL) {
      # The body of a negative lookaround matched, so the lookaround fails and everything it did is undone.
      while (1) {
        my $e = pop @stack;
        last if $e->[0] == E_LOOK_NEG;
        _undo($st, $e);
      }
    }
    elsif ($op == OP_REP_LAZY) {
      my ($at, $n) = ($pos, 0);
      while ($n < $i->[I_MIN]) {
        my $w = _one($i, $in, $at) or last;
        $at += $w;
        $n++;
      }
      $steps -= $n >> 3;
      if ($n == $i->[I_MIN]) {
        if ($memo && $i->[I_M]) {
          my $idx = $self->_memo_index($st, $i->[I_M], $at);
          if (vec($$memo, $idx, 1)) {
            # What follows the loop has failed from here before. The loop takes one more character.
            push @stack, [ E_REP_LAZY, $pc, $at, $n ] if $n != $i->[I_MAX];
            goto BACKTRACK;
          }
          push @stack, [ E_MARK, 0, $idx ] if $n == $i->[I_MAX] && @stack < MAX_MARK_DEPTH;
        }
        push @stack, [ E_REP_LAZY, $pc, $at, $n ] if $n != $i->[I_MAX];
        $pos = $at;
        $pc++;
        next;
      }
    }
    elsif ($op == OP_LOOP_INIT) {
      my $k = $i->[I_A];
      push @stack, [ E_COUNTER, $k, $counts->[ 2 * $k ], $counts->[ 2 * $k + 1 ] ];
      @$counts[ 2 * $k, 2 * $k + 1 ] = (0, -1);
      $pc++;
      next;
    }
    elsif ($op == OP_LOOP) {
      my $k = $i->[I_A];
      my $n = $counts->[ 2 * $k ];
      my $left = $len - $pos;
      # Fewer characters are left than any way from here to a match consumes.
      goto BACKTRACK if $left < $i->[I_NEED]
        || ($n < $i->[I_MIN] && $i->[I_ITER] && $left - $i->[I_NEED] < ($i->[I_MIN] - $n) * $i->[I_ITER]);
      # Once the minimum is met, ECMA-262 rejects an iteration that consumed nothing.
      goto BACKTRACK if $n > $i->[I_MIN] && $pos == $counts->[ 2 * $k + 1 ];
      if ($n < $i->[I_MIN]) { $pc++; next }
      if ($i->[I_MAX] >= 0 && $n >= $i->[I_MAX]) { $pc = $i->[I_X]; next }
      # The loop may be left or entered from here. A place from which both have failed before fails at once.
      if ($memo && $i->[I_M]) {
        my $idx = $self->_memo_index($st, $i->[I_M], $pos);
        goto BACKTRACK if vec($$memo, $idx, 1);
        push @stack, [ E_MARK, 0, $idx ] if @stack < MAX_MARK_DEPTH;
      }
      if (!_allows($quick->[ $i->[I_X] ], $in, $pos)) { $pc++ }
      elsif (!_allows($quick->[ $pc + 1 ], $in, $pos)) { $pc = $i->[I_X] }
      elsif ($i->[I_FLAG]) { push @stack, [ E_CHOICE, $pc + 1, $pos ]; $pc = $i->[I_X] }
      else { push @stack, [ E_CHOICE, $i->[I_X], $pos ]; $pc++ }
      next;
    }
    elsif ($op == OP_LOOP_ITER) {
      my $k = $i->[I_A];
      push @stack, [ E_COUNTER, $k, $counts->[ 2 * $k ], $counts->[ 2 * $k + 1 ] ];
      $counts->[ 2 * $k ]++;
      $counts->[ 2 * $k + 1 ] = $pos;
      $pc++;
      next;
    }
    elsif ($op == OP_MATCH) {
      $st->{steps} = $steps;
      return 1;
    }

    # The instruction failed. Take up the most recent choice, undoing what was recorded since.
    BACKTRACK:
    while (1) {
      if (!@stack) {
        $st->{steps} = $steps;
        return 0;
      }
      my $e = $stack[-1];
      my $kind = $e->[0];
      if ($kind == E_CHOICE) {
        ($pc, $pos) = ($e->[1], $e->[2]);
        pop @stack;
        last;
      }
      if ($kind == E_REP) {
        # What followed the loop from the position it stood at has failed.
        my $rep = $insts->[ $e->[1] - 1 ];
        my $marked = $memo && $rep->[I_M];
        vec($$memo, $self->_memo_index($st, $rep->[I_M], $e->[2]), 1) = 1 if $marked;
        # Give back the last character taken.
        $e->[2]--;
        ($pc, $pos) = ($e->[1], $e->[2]);
        my $last = $e->[2] <= $e->[3];
        # A position from which what follows the loop cannot go on is passed over, and so is one from which it has
        # failed before.
        my $pass = !_allows($quick->[$pc], $in, $pos);
        if ($marked && !$pass) {
          my $idx = $self->_memo_index($st, $rep->[I_M], $pos);
          if (vec($$memo, $idx, 1)) {
            $pass = 1;
          }
          elsif ($last) {
            # The loop has nothing more to give back. The entry stays to note this last way.
            $stack[-1] = [ E_MARK, 0, $idx ];
            last;
          }
        }
        pop @stack if $last;
        if ($pass) {
          $steps--;
          next;
        }
        last;
      }
      if ($kind == E_REP_LAZY) {
        my $rep = $insts->[ $e->[1] ];
        my $marked = $memo && $rep->[I_M];
        vec($$memo, $self->_memo_index($st, $rep->[I_M], $e->[2]), 1) = 1 if $marked;
        my $w = _one($rep, $in, $e->[2]);
        if (!$w) {
          pop @stack;
          next;
        }
        $e->[2] += $w;
        $e->[3]++;
        ($pc, $pos) = ($e->[1] + 1, $e->[2]);
        my $last = $e->[3] == $rep->[I_MAX];
        if ($marked) {
          my $idx = $self->_memo_index($st, $rep->[I_M], $pos);
          if (vec($$memo, $idx, 1)) {
            pop @stack if $last;
            $steps--;
            next;
          }
          if ($last) {
            $stack[-1] = [ E_MARK, 0, $idx ];
            last;
          }
        }
        pop @stack if $last;
        last;
      }
      if ($kind == E_MARK) {
        # Every way from a memo point has failed.
        vec($$memo, $e->[2], 1) = 1 if $memo;
        pop @stack;
        next;
      }
      if ($kind == E_LOOK) {
        # The body of a positive lookaround has no way left to match.
        pop @stack;
        next;
      }
      if ($kind == E_LOOK_NEG) {
        # The body of a negative lookaround has no way to match, so the lookaround holds.
        ($pc, $pos) = ($e->[1], $e->[2]);
        pop @stack;
        last;
      }
      _undo($st, $e);
      pop @stack;
    }
  }
}

1;
