package JSON::Schema::Corvus::Pattern;

# ECMA-262 regular expressions, as JSON Schema's pattern and format: regex specify, run with ECMA-262's meaning.
#
# A pattern is read with the grammar of the u flag, and one that only that grammar refuses is read with the Annex B
# grammar of a pattern with no flag, as the other ports do (Regex::Parse). Two engines run what is read:
#
# - Perl's own engine runs a pattern that Regex::Linear proves it matches in time proportional to the text, written
#   out with ECMA-262's meaning: every literal and class as code points (so Perl's own \d, \w, \s, the dot and its
#   Unicode tables never apply), ^ and $ as the ends of the text, \b with ECMA-262's ASCII word characters. Such a
#   match needs no limit, and Perl's engine has none: it cannot be stopped once it has started.
# - The matcher of Regex::Vm runs every other pattern (a backreference, a lookbehind, a case-insensitive word
#   boundary, a count Perl cannot write, or anything the proof does not prove), with the limit of a match.
#
# A pattern read without the u flag is matched by UTF-16 code unit, so on a text with a character beyond the Basic
# Multilingual Plane it always goes to the matcher, which reads the text that way.

use 5.026;
use strict;
use warnings;
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Regex::Charset ();
use JSON::Schema::Corvus::Regex::Linear ();
use JSON::Schema::Corvus::Regex::Parse ();
use JSON::Schema::Corvus::Regex::Vm ();

use constant DEFAULT_MATCH_LIMIT => 1.0;
# The largest count of a quantifier Perl's engine reads.
use constant MAX_PERL_COUNT => 32766;
# The most compiled patterns kept, by pattern and limit.
use constant MAX_CACHED => 512;

my %CACHE;

# Reads a pattern: (tree, unicode) in the u flag grammar or else Annex B, or () for a pattern neither accepts. Dies with
# a PatternTooLarge error for a pattern over a limit of size.
sub _read {
  my ($pattern) = @_;
  for my $unicode (1, 0) {
    my ($root) = eval { JSON::Schema::Corvus::Regex::Parse::parse($pattern, $unicode) };
    return ($root, $unicode) if $root;
    my $e = $@;
    die $e unless ref $e eq 'HASH';
    _too_large($pattern, $e) if $e->{too_large};
  }
  return ();
}

sub _shown {
  my ($pattern) = @_;
  return length $pattern <= 200 ? $pattern : substr($pattern, 0, 200) . '... (' . length($pattern) . ' characters)';
}

sub _too_large {
  my ($pattern, $e) = @_;
  my $why = $e->{too_large} eq 'length' ? "the pattern is longer than $e->{value} bytes"
    : $e->{too_large} eq 'nesting' ? "the groups of the pattern nest more than $e->{value} deep"
    : "the pattern has a size of more than $e->{value} bytes when it is read";
  die JSON::Schema::Corvus::Error::PatternTooLarge->_new(
    "The regular expression '" . _shown($pattern) . "' is over a limit of size: $why.", pattern => $pattern);
}

# Whether the text is a valid regular expression for format: regex, which takes the u flag grammar only, as the other
# ports do. A text over a limit of reading is not valid.
sub is_valid_ecma_regex {
  my ($s) = @_;
  return eval { JSON::Schema::Corvus::Regex::Parse::parse($s, 1); 1 } ? 1 : 0;
}

# The search function of a pattern (true on a match), or undef for a pattern that is not valid. $limit is the seconds
# one match may take (undef: no limit); a match that reaches it dies with a PatternMatchLimit error. Dies with a
# PatternTooLarge error for a pattern over a limit of size.
sub compile_pattern {
  my ($pattern, $limit) = @_;
  my $key = ($limit // 'none') . "\0" . $pattern;
  return $CACHE{$key} if exists $CACHE{$key};
  %CACHE = () if keys %CACHE >= MAX_CACHED;
  return $CACHE{$key} = _compile($pattern, $limit);
}

sub _compile {
  my ($pattern, $limit) = @_;
  my ($root, $unicode) = _read($pattern) or return undef;
  my $tree = JSON::Schema::Corvus::Regex::Vm::_simplify($root, []);
  my $perl = _perl_regex($tree);
  my $vm;
  my $vm_search = sub {
    my ($s) = @_;
    $vm //= JSON::Schema::Corvus::Regex::Vm::compile_program($root, $unicode);
    my ($ok, $stop) = $vm->match($s, $limit);
    return $ok if defined $ok;
    my $time = !defined $limit ? '' : $limit >= 1 ? sprintf('%gs', $limit) : $limit >= 1e-3
      ? sprintf('%gms', $limit * 1e3) : sprintf("%g\x{b5}s", $limit * 1e6);
    my $message = $stop eq 'time'
      ? "A match of the regular expression '" . _shown($pattern) . "' was abandoned: it took longer than the limit of $time."
      : "A match of the regular expression '" . _shown($pattern)
        . "' was abandoned: it needed more memory for its backtracking than the limit.";
    die JSON::Schema::Corvus::Error::PatternMatchLimit->_new($message, pattern => $pattern, limit => $limit);
  };
  # With no limit, Perl's engine runs what it can write, proved or not.
  return $vm_search unless defined $perl && (!defined $limit || JSON::Schema::Corvus::Regex::Linear->new->proves($tree));
  my $re = do { no warnings; qr/$perl/ };
  return sub { $_[0] =~ $re ? 1 : 0 } if $unicode;
  # A pattern read by code unit: Perl's engine reads code points, which differ only for a text with a character beyond
  # the Basic Multilingual Plane.
  return sub {
    return $vm_search->($_[0]) if $_[0] =~ /[\x{10000}-\x{10FFFF}]/;
    return $_[0] =~ $re ? 1 : 0;
  };
}

# A class for Perl's engine. A class of at most four characters is written as an alternation of them: Perl compiles a
# class whose members are one case folding set (such as [\x{1f80}\x{1f88}]) to a node that compares with full case
# folding, which answers wrongly for a character whose full folding is more than one character (5.40 matches neither of
# those two with it). A case folding set has at most four members.
sub _perl_set {
  my ($set) = @_;
  my $r = $set->{ranges};
  my $count = 0;
  for (my $i = 0; $i < @$r; $i += 2) {
    $count += $r->[$i + 1] - $r->[$i] + 1;
    last if $count > 4;
  }
  return $set->class_text if $count > 4 || $count == 0;
  my @members;
  for (my $i = 0; $i < @$r; $i += 2) {
    push @members, map { sprintf('\x{%x}', $_) } $r->[$i] .. $r->[$i + 1];
  }
  return @members == 1 ? $members[0] : '(?:' . join('|', @members) . ')';
}

# The pattern written for Perl's engine, or undef when it has what that engine cannot run with ECMA-262's meaning: a
# backreference, a lookbehind, a case-insensitive word boundary, or a count Perl cannot write.
sub _perl_regex {
  my ($n) = @_;
  my $k = $n->{kind};
  return '(?:)' if $k eq 'empty';
  return sprintf('\x{%x}', $n->{r}) if $k eq 'char';
  return _perl_set($n->{set}) if $k eq 'set';
  if ($k eq 'cat') {
    my $out = '';
    for (@{ $n->{subs} }) {
      my $part = _perl_regex($_) // return undef;
      $out .= $_->{kind} eq 'alt' ? "(?:$part)" : $part;
    }
    return $out;
  }
  if ($k eq 'alt') {
    my @parts;
    for (@{ $n->{subs} }) {
      push @parts, _perl_regex($_) // return undef;
    }
    return join '|', @parts;
  }
  if ($k eq 'group') {
    my $body = _perl_regex($n->{subs}[0]) // return undef;
    return "(?:$body)";
  }
  if ($k eq 'repeat') {
    return '(?:)' if $n->{max} == 0;
    return undef if $n->{min} > MAX_PERL_COUNT || $n->{max} > MAX_PERL_COUNT;
    my $body = _perl_regex($n->{subs}[0]) // return undef;
    my $q = $n->{min} == 0 && $n->{max} == -1 ? '*' : $n->{min} == 1 && $n->{max} == -1 ? '+'
      : $n->{min} == 0 && $n->{max} == 1 ? '?' : $n->{max} == -1 ? "{$n->{min},}"
      : $n->{min} == $n->{max} ? "{$n->{min}}" : "{$n->{min},$n->{max}}";
    return "(?:$body)$q" . ($n->{lazy} ? '?' : '');
  }
  return '\A' if $k eq 'bol';
  return '\z' if $k eq 'eol';
  return '(?<![^\n\r\x{2028}\x{2029}])' if $k eq 'line_start';
  return '(?![^\n\r\x{2028}\x{2029}])' if $k eq 'line_end';
  if ($k eq 'wb' || $k eq 'nwb') {
    return undef if $n->{fold};
    return $k eq 'wb' ? '(?a:\b)' : '(?a:\B)';
  }
  if ($k eq 'look') {
    return undef if $n->{behind};
    my $body = _perl_regex($n->{subs}[0]) // return undef;
    return ($n->{negative} ? '(?!' : '(?=') . "$body)";
  }
  return undef;
}

1;
