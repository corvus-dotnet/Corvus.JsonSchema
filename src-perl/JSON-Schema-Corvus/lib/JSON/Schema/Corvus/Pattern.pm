package JSON::Schema::Corvus::Pattern;

# TEMPORARY: a minimal translation of ECMA-262 patterns to Perl's, with no limits, so that the rest of the evaluator
# can be tested. It is replaced by the port of the Python package's pattern.py and the Go port's regex engine.

use 5.026;
use strict;
use warnings;

use constant DEFAULT_MATCH_LIMIT => 1.0;

my %CACHE;

sub _translate {
  my ($source) = @_;
  my $out = '';
  my $in_class = 0;
  my $i = 0;
  my $n = length $source;
  while ($i < $n) {
    my $c = substr($source, $i, 1);
    if ($c eq '\\') {
      my $d = substr($source, $i + 1, 1);
      if ($d eq 'd') { $out .= $in_class ? '0-9' : '[0-9]' }
      elsif ($d eq 'D') { $out .= $in_class ? '\D' : '[^0-9]' }
      elsif ($d eq 'w') { $out .= $in_class ? 'A-Za-z0-9_' : '[A-Za-z0-9_]' }
      elsif ($d eq 'W') { $out .= $in_class ? '\W' : '[^A-Za-z0-9_]' }
      elsif ($d eq 'u' && substr($source, $i + 2, 1) eq '{') {
        my $end = index($source, '}', $i);
        $out .= '\x{' . substr($source, $i + 3, $end - $i - 3) . '}';
        $i = $end + 1;
        next;
      }
      elsif ($d eq 'u') { $out .= '\x{' . substr($source, $i + 2, 4) . '}'; $i += 6; next }
      elsif ($d eq 'c') { $out .= '\c' . substr($source, $i + 2, 1); $i += 3; next }
      else { $out .= $c . $d }
      $i += 2;
      next;
    }
    if ($in_class) {
      $in_class = 0 if $c eq ']';
      $out .= $c eq '$' || $c eq '@' ? "\\$c" : $c;
    }
    elsif ($c eq '[') {
      $in_class = 1;
      $out .= $c;
      if (substr($source, $i + 1, 1) eq ']') { $out .= '^\s\S]'; $i += 2; $in_class = 0; next }
      if (substr($source, $i + 1, 2) eq '^]') { $out .= '\s\S]'; $i += 3; $in_class = 0; next }
    }
    elsif ($c eq '$') { $out .= '\z' }
    elsif ($c eq '^') { $out .= '\A' }
    elsif ($c eq '.') { $out .= '[^\n\r\x{2028}\x{2029}]' }
    elsif ($c eq '@') { $out .= '\@' }
    else { $out .= $c }
    $i++;
  }
  return $out;
}

# The search function of a pattern (true on a match), or undef for a pattern that is not valid.
sub compile_pattern {
  my ($source, $limit) = @_;
  return $CACHE{$source} if exists $CACHE{$source};
  my $perl = _translate($source);
  my $re = eval { qr/$perl/u };
  return $CACHE{$source} = $re ? sub { $_[0] =~ $re } : undef;
}

sub is_valid_ecma_regex { defined compile_pattern($_[0]) }

1;
