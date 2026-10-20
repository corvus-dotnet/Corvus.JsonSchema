package JSON::Schema::Corvus::Regex::Linear;

# Proves that a backtracking engine (Perl's) matches a pattern in time proportional to the text, whatever the text is:
# a port of the Python package's _Linear, over the syntax tree of Regex::Parse. Perl's engine cannot be stopped once a
# match has started, so with the limit of a match in force it is given the patterns this proves and no others; every
# other pattern goes to the matcher of Regex::Vm, which keeps a limit.
#
# The engine tries one way through a pattern and goes back to the last choice when it fails. The proof is that no text
# makes it go over more than a bounded number of characters a second time. It holds when, at every choice (the
# alternatives of an alternation, one more iteration of a repetition or none), the next character of the text says
# which way to go: the sets of characters that the ways can start with, or that can follow the choice where a way can
# match nothing, have no character in common, and at most one way can match nothing. A way that is not taken then fails
# at its first character. The body of a repetition has to take a character.
#
# Some things are allowed beyond that, each for a bounded cost, whose product (factor) times the nodes a character can
# pass more than once bounds the work for a character of the text (see the Python package's pattern.py for each):
# a bounded pattern with no lookahead and few ways through it; a repetition whose end the next character does not
# decide, when what follows can take w characters at most and still fail; alternatives that start alike but take a
# bounded number of characters; a bounded lookahead anywhere, and an unbounded one at the start of a pattern that starts
# with ^; and a pattern that does not start with ^ when a try that fails takes w characters at most.
#
# A pattern with a backreference, a lookbehind, or a case-insensitive word boundary is not proved.

use 5.026;
use strict;
use warnings;
use List::Util ();
use Scalar::Util ();

use constant { MAX_FACTOR => 64, MAX_WORK => 4096, MAX_WAYS => 256, MAX_WAYS_WORK => 1024, INFINITE => 9**9**9 };
use constant EVERY => [0, 0x10FFFF];

# --- Sets of characters, as flat lists of inclusive bounds ---

sub _merged {
  my ($ranges) = @_;
  my @order = sort { $ranges->[2 * $a] <=> $ranges->[2 * $b] } 0 .. @$ranges / 2 - 1;
  my @out;
  for my $i (@order) {
    my ($lo, $hi) = ($ranges->[2 * $i], $ranges->[2 * $i + 1]);
    if (@out && $lo <= $out[-1] + 1) {
      $out[-1] = $hi if $hi > $out[-1];
      next;
    }
    push @out, $lo, $hi;
  }
  return \@out;
}

# Whether two merged sets have a character in common.
sub _meet {
  my ($a, $b) = @_;
  my ($i, $j) = (0, 0);
  while ($i < @$a && $j < @$b) {
    if ($a->[$i + 1] < $b->[$j]) { $i += 2 }
    elsif ($b->[$j + 1] < $a->[$i]) { $j += 2 }
    else { return 1 }
  }
  return 0;
}

# --- Facts about nodes ---

sub _zero_width {
  my ($k) = @_;
  return $k eq 'bol' || $k eq 'eol' || $k eq 'line_start' || $k eq 'line_end' || $k eq 'wb' || $k eq 'nwb';
}

# Whether the node can match without taking a character (it may say yes for one that cannot).
sub matches_nothing {
  my ($n) = @_;
  return $n->{_nothing} //= do {
    my $k = $n->{kind};
    $k eq 'char' || $k eq 'set' ? 0
      : $k eq 'cat' ? (List::Util::all { matches_nothing($_) } @{ $n->{subs} }) ? 1 : 0
      : $k eq 'alt' ? (List::Util::any { matches_nothing($_) } @{ $n->{subs} }) ? 1 : 0
      : $k eq 'group' ? matches_nothing($n->{subs}[0])
      : $k eq 'repeat' ? ($n->{min} == 0 || matches_nothing($n->{subs}[0])) ? 1 : 0
      : 1;
  };
}

# Whether the node matches nothing wherever it matches: it has assertions and lookarounds, and nothing that takes a
# character.
sub only_nothing {
  my ($n) = @_;
  my $k = $n->{kind};
  return 0 if $k eq 'char' || $k eq 'set' || $k eq 'backref';
  return 1 if $k eq 'look' || $k eq 'empty' || _zero_width($k);
  return $n->{max} == 0 || only_nothing($n->{subs}[0]) ? 1 : 0 if $k eq 'repeat';
  return (List::Util::all { only_nothing($_) } @{ $n->{subs} }) ? 1 : 0;
}

sub new {
  my ($class) = @_;
  return bless { factor => 1, nodes => 0, repeated => 0, once => {} }, $class;
}

sub _not_linear { die "not linear\n" }

# Whether the pattern (its tree, after Regex::Vm's simplify) is proved.
sub proves {
  my ($self, $root) = @_;
  my ($anchored, $bounded);
  my $ok = eval {
    $anchored = $self->_anchored($root);
    if ($self->_bounded($root, $anchored)) {
      $bounded = 1;
      return 1;
    }
    if ($anchored) {
      $self->_mark_once($root);
    }
    else {
      my $top = $root;
      $top = $top->{subs}[0] while $top->{kind} eq 'group';
      # An alternative that starts with ^ fails at once everywhere but at the start of the text.
      my @ways = $top->{kind} eq 'alt' ? @{ $top->{subs} } : ($top);
      my $widest = 0;
      for my $x (@ways) {
        next if $self->_anchored($x);
        my $w = $self->_fails_within($x, 1);
        $widest = $w if $w > $widest;
      }
      $self->_cost($widest);
    }
    $self->_visit($root, [], 1, 0, 1, 1);
    1;
  };
  if (!defined $ok) {
    return 0 if $@ eq "not linear\n";
    die $@;
  }
  return 1 if $bounded;
  return $self->{factor} * ($anchored ? $self->{repeated} : $self->{nodes}) <= MAX_WORK ? 1 : 0;
}

sub _cost {
  my ($self, $width) = @_;
  $self->{factor} *= 1 + $width;
  _not_linear() if $self->{factor} > MAX_FACTOR;
}

# Whether the pattern takes a bounded number of characters, has no lookahead, and has few ways through it.
sub _bounded {
  my ($self, $root, $anchored) = @_;
  return 0 if $self->_length($root) > MAX_WORK || _has_look($root);
  my $ways = $self->_ways($root) or return 0;
  my $work = List::Util::sum(0, map { @$_ + 1 } @$ways);
  return $work <= ($anchored ? MAX_WORK : MAX_WAYS_WORK) ? 1 : 0;
}

sub _has_look {
  my ($n) = @_;
  return 1 if $n->{kind} eq 'look' || $n->{kind} eq 'backref';
  return (List::Util::any { _has_look($_) } @{ $n->{subs} // [] }) ? 1 : 0;
}

# The ways through a node that takes a bounded number of characters, each as the sets of characters it takes in order.
# undef: there are too many, or no bound.
sub _ways {
  my ($self, $n) = @_;
  my $k = $n->{kind};
  return [ [ $self->_first($n) ] ] if $k eq 'char' || $k eq 'set';
  return [ [] ] if $k eq 'empty' || $k eq 'look' || _zero_width($k);
  return undef if $k eq 'backref';
  return $self->_ways($n->{subs}[0]) if $k eq 'group';
  if ($k eq 'alt') {
    my @out;
    for my $x (@{ $n->{subs} }) {
      my $more = $self->_ways($x);
      return undef if !$more || @out + @$more > MAX_WAYS;
      push @out, @$more;
    }
    return \@out;
  }
  if ($k eq 'cat') {
    my @out = ([]);
    for my $x (@{ $n->{subs} }) {
      my $more = $self->_ways($x);
      return undef if !$more || @out * @$more > MAX_WAYS;
      next if @$more == 1 && !@{ $more->[0] };
      @out = map { my $a = $_; map { [ @$a, @$_ ] } @$more } @out;
    }
    return \@out;
  }
  # A repetition.
  return [ [] ] if $n->{max} == 0 || only_nothing($n->{subs}[0]);
  return undef if $n->{max} == -1 || $n->{max} > MAX_WAYS;
  my $body = $self->_ways($n->{subs}[0]) or return undef;
  my @out;
  my @some = ([]);
  for my $count (0 .. $n->{max}) {
    if ($count >= $n->{min}) {
      return undef if @out + @some > MAX_WAYS;
      push @out, @some;
    }
    if ($count < $n->{max}) {
      return undef if @some * @$body > MAX_WAYS || @{ $some[0] } > MAX_WAYS;
      @some = map { my $a = $_; map { [ @$a, @$_ ] } @$body } @some;
    }
  }
  return \@out;
}

# Whether some text that one way takes can start with a text that the other takes.
sub _starts_with {
  my ($a, $b) = @_;
  my $n = @$a < @$b ? @$a : @$b;
  for my $i (0 .. $n - 1) {
    return 0 unless _meet($a->[$i], $b->[$i]);
  }
  return 1;
}

# The characters the text that the node matches can start with.
sub _first {
  my ($self, $n) = @_;
  return $n->{_first} //= do {
    my $k = $n->{kind};
    if ($k eq 'char') { [ $n->{r}, $n->{r} ] }
    elsif ($k eq 'set') { [ @{ $n->{set}{ranges} } ] }
    elsif ($k eq 'cat') {
      my @parts;
      for my $x (@{ $n->{subs} }) {
        push @parts, @{ $self->_first($x) };
        last unless matches_nothing($x);
      }
      _merged(\@parts);
    }
    elsif ($k eq 'look' || $k eq 'backref' || $k eq 'empty' || _zero_width($k) || ($k eq 'repeat' && $n->{max} == 0)) {
      [];
    }
    else {
      _merged([ map { @{ $self->_first($_) } } @{ $n->{subs} } ]);
    }
  };
}

# The most characters the node can take.
sub _length {
  my ($self, $n) = @_;
  return $n->{_length} //= do {
    my $k = $n->{kind};
    if ($k eq 'char' || $k eq 'set') { 1 }
    elsif ($k eq 'look' || $k eq 'backref' || $k eq 'empty' || _zero_width($k)) { 0 }
    elsif ($k eq 'alt') { List::Util::max(map { $self->_length($_) } @{ $n->{subs} }) }
    elsif ($k eq 'repeat') {
      my $body = $self->_length($n->{subs}[0]);
      $n->{max} == 0 || $body == 0 ? 0 : $n->{max} == -1 ? INFINITE : $n->{max} * $body;
    }
    else { List::Util::sum(0, map { $self->_length($_) } @{ $n->{subs} }) }
  };
}

# Whether every way through the node starts with ^, so that a search is tried at the start of the text and nowhere
# else.
sub _anchored {
  my ($self, $n) = @_;
  my $k = $n->{kind};
  return 1 if $k eq 'bol';
  if ($k eq 'cat') {
    for my $x (@{ $n->{subs} }) {
      return 1 if $self->_anchored($x);
      # Before the ^ there may be what takes no character, other than a lookahead, which would be tried everywhere.
      return 0 if $self->_length($x) != 0 || _has_look($x);
    }
    return 0;
  }
  return (List::Util::all { $self->_anchored($_) } @{ $n->{subs} }) ? 1 : 0 if $k eq 'alt';
  return $self->_anchored($n->{subs}[0]) if $k eq 'group';
  return $n->{min} >= 1 && $self->_anchored($n->{subs}[0]) ? 1 : 0 if $k eq 'repeat';
  return 0;
}

# Marks the lookaheads at the start of an anchored pattern, before anything that takes a character or is a choice.
# Each is tried once.
sub _mark_once {
  my ($self, $n) = @_;
  $n = $n->{subs}[0] while $n->{kind} eq 'group';
  return unless $n->{kind} eq 'cat';
  for my $x (@{ $n->{subs} }) {
    if ($x->{kind} eq 'look') {
      $self->{once}{ Scalar::Util::refaddr($x) } = 1;
    }
    elsif (!(_zero_width($x->{kind}) || $x->{kind} eq 'empty')) {
      return;
    }
  }
}

sub _never_fails {
  my ($self, $n) = @_;
  my $k = $n->{kind};
  return 1 if $k eq 'empty';
  return 0 if $k eq 'char' || $k eq 'set' || $k eq 'look' || $k eq 'backref' || _zero_width($k);
  return $n->{min} == 0 || $n->{max} == 0 ? 1 : 0 if $k eq 'repeat';
  return (List::Util::any { $self->_never_fails($_) } @{ $n->{subs} }) ? 1 : 0 if $k eq 'alt';
  return (List::Util::all { $self->_never_fails($_) } @{ $n->{subs} }) ? 1 : 0;
}

# The most characters a try at the node can take and still fail. $last: once the node has matched, nothing after it
# can fail.
sub _fails_within {
  my ($self, $n, $last) = @_;
  my $k = $n->{kind};
  return $k ne 'look' ? $self->_length($n) : $self->_length($n->{subs}[0]) unless $last;
  return 1 if $k eq 'char' || $k eq 'set';
  return 0 if $k eq 'empty' || $k eq 'backref' || _zero_width($k);
  return $self->_length($n->{subs}[0]) if $k eq 'look';
  return $self->_fails_within($n->{subs}[0], 1) if $k eq 'group';
  return List::Util::max(map { $self->_fails_within($_, 1) } @{ $n->{subs} }) if $k eq 'alt';
  if ($k eq 'repeat') {
    return 0 if $n->{max} == 0 || $n->{min} == 0;
    my $body = $n->{subs}[0];
    my $before = $n->{min} > 1 ? ($n->{min} - 1) * $self->_length($body) : 0;
    return $before + $self->_fails_within($body, 1);
  }
  # A sequence: the items after the last one that can fail cost nothing.
  my $items = $n->{subs};
  my $j = $#$items;
  $j-- while $j >= 0 && $self->_never_fails($items->[$j]);
  return 0 if $j < 0;
  my $width = $self->_fails_within($items->[$j], 1);
  $width += $self->_fails_within($items->[$_], 0) for 0 .. $j - 1;
  return $width;
}

# Checks the node. $follow: the characters that can come after it. $end: the pattern (or the lookahead the node is in)
# can end after it. $after: the most characters that what comes after it can take and still fail, and $never: what
# comes after it cannot fail. $single: the node is in no repetition.
sub _visit {
  my ($self, $n, $follow, $end, $after, $never, $single) = @_;
  $self->{nodes}++;
  $self->{repeated}++ unless $single;
  my $k = $n->{kind};
  return if $k eq 'char' || $k eq 'set' || $k eq 'empty' || $k eq 'bol' || $k eq 'eol' || $k eq 'line_start'
    || $k eq 'line_end' || (($k eq 'wb' || $k eq 'nwb') && !$n->{fold});
  _not_linear() if $k eq 'backref' || $k eq 'wb' || $k eq 'nwb';
  if ($k eq 'group') {
    $self->_visit($n->{subs}[0], $follow, $end, $after, $never, $single);
    return;
  }
  if ($k eq 'look') {
    _not_linear() if $n->{behind};
    my $width = $self->_length($n->{subs}[0]);
    if ($width == INFINITE) {
      _not_linear() unless $self->{once}{ Scalar::Util::refaddr($n) };
    }
    else {
      $self->_cost($width);
    }
    $self->_visit($n->{subs}[0], [], 1, 0, 1, $single);
    return;
  }
  if ($k eq 'cat') {
    my $items = $n->{subs};
    for (my $i = $#$items; $i >= 0; $i--) {
      my $x = $items->[$i];
      $self->_visit($x, $follow, $end, $after, $never, $single);
      if (matches_nothing($x)) {
        $follow = _merged([ @$follow, @{ $self->_first($x) } ]);
      }
      else {
        ($follow, $end) = ($self->_first($x), 0);
      }
      if ($never) {
        ($after, $never) = ($self->_fails_within($x, 1), 0) unless $self->_never_fails($x);
      }
      else {
        $after += $self->_length($x);
      }
    }
    return;
  }
  if ($k eq 'alt') {
    my $seen = [];
    my ($nothing, $clash) = (0, 0);
    for my $x (@{ $n->{subs} }) {
      my $starts = $self->_first($x);
      if (matches_nothing($x)) {
        $clash ||= $nothing;
        $nothing = 1;
        $starts = _merged([ @$starts, @$follow ]);
      }
      $clash ||= _meet($seen, $starts);
      $seen = _merged([ @$seen, @$starts ]);
    }
    if ($clash) {
      # The next character does not say which alternative to take.
      my $ways = $self->_length($n) <= MAX_WORK ? $self->_ways($n) : undef;
      _not_linear() unless $ways;
      my $overlap = 0;
      OUTER: for my $i (0 .. $#$ways) {
        for my $j (0 .. $i - 1) {
          if (_starts_with($ways->[$i], $ways->[$j])) {
            $overlap = 1;
            last OUTER;
          }
        }
      }
      if ($overlap) {
        # A text can match two of them. Each is a way to go on.
        _not_linear() unless $single;
        $self->_cost(@$ways - 1);
      }
      my $count = List::Util::sum(0, map { scalar @$_ } @$ways);
      $self->{nodes} += $count;
      $self->{repeated} += $count unless $single;
    }
    $self->_visit($_, $follow, $end, $after, $never, $single) for @{ $n->{subs} };
    return;
  }
  # A repetition.
  return if $n->{max} == 0;
  my $body = $n->{subs}[0];
  if (only_nothing($body)) {
    # Written as the body once, or as nothing.
    $self->_visit($body, $follow, $end, $after, $never, $single) if $n->{min};
    return;
  }
  if ($n->{max} == 1 && $n->{min} == 1) {
    $self->_visit($body, $follow, $end, $after, $never, $single);
    return;
  }
  _not_linear() if matches_nothing($body);
  my $starts = $self->_first($body);
  if ($n->{min} != $n->{max} && _meet($starts, $follow)) {
    # The next character does not say whether to leave the repetition. Each place to leave it is tried.
    _not_linear() if $after == INFINITE;
    $self->_cost($after);
  }
  if ($n->{max} == 1) {
    $self->_visit($body, $follow, $end, $after, $never, $single);
  }
  else {
    my $rest = $n->{max} == -1 ? INFINITE : $after + ($n->{max} - 1) * $self->_length($body);
    $self->_visit($body, _merged([ @$follow, @$starts ]), $end, $rest, 0, 0);
  }
}

1;
