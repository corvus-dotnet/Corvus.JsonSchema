package JSON::Schema::Corvus::Runtime;

# Helpers that generated validators and the collecting evaluator call. A port of the Python package's runtime.py.
#
# JSON equality: numbers by value (1 equals 1.0), objects by their sets of names and values, arrays element by
# element, and a boolean never equals a number or a string.

use 5.026;
use strict;
use warnings;
use Encode ();
use MIME::Base64 ();
use Math::BigInt ();
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Json ();
no if $] >= 5.036, warnings => 'experimental::builtin';

*_num = \&JSON::Schema::Corvus::Json::_created_as_number;

# The check of a true schema and of a false one (in dispatch tables).
sub always { 1 }
sub never { '' }

sub equal {
  my ($a, $b) = @_;
  my $ra = ref $a;
  my $rb = ref $b;
  if ($ra eq 'HASH') {
    return '' unless $rb eq 'HASH' && keys %$a == keys %$b;
    for my $k (keys %$a) {
      return '' unless exists $b->{$k} && equal($a->{$k}, $b->{$k});
    }
    return 1;
  }
  if ($ra eq 'ARRAY') {
    return '' unless $rb eq 'ARRAY' && @$a == @$b;
    for my $i (0 .. $#$a) {
      return '' unless equal($a->[$i], $b->[$i]);
    }
    return 1;
  }
  if ($ra eq 'JSON::PP::Boolean' || $rb eq 'JSON::PP::Boolean') {
    return $ra eq $rb && (!$a == !$b) ? 1 : '';
  }
  return '' if $rb eq 'HASH' || $rb eq 'ARRAY';
  return defined $b ? '' : 1 unless defined $a;
  return '' unless defined $b;
  my $na = $ra eq 'Math::BigInt' || _num($a);
  my $nb = $rb eq 'Math::BigInt' || _num($b);
  return '' if $na xor $nb;
  return $a == $b ? 1 : '' if $na;
  return $a eq $b ? 1 : '';
}

# Whether some element of a list is JSON-equal to $x.
sub includes {
  my ($values, $x) = @_;
  for my $v (@$values) {
    return 1 if equal($v, $x);
  }
  return '';
}

# A key that agrees with JSON equality: equal values have equal keys, and unequal values unequal keys.
sub key {
  my ($v) = @_;
  return "n" unless defined $v;
  my $r = ref $v;
  return "o{" . join(',', map { _key_text($_) . ':' . key($v->{$_}) } sort keys %$v) . '}' if $r eq 'HASH';
  return 'a[' . join(',', map { key($_) } @$v) . ']' if $r eq 'ARRAY';
  return $v ? 'T' : 'F' if $r eq 'JSON::PP::Boolean';
  return 'd' . _number_key($v) if $r eq 'Math::BigInt' || _num($v);
  return 's' . _key_text($v);
}

sub _key_text {
  my ($s) = @_;
  $s =~ s/([\\,:\[\]{}])/\\$1/g;
  return $s;
}

# The key of a number: its value as exact decimal digits, so that 1 and 1.0 share a key.
sub _number_key {
  my ($n) = @_;
  my ($m, $e) = decimal($n);
  return "0" if $m == 0;
  # Strip trailing zeros of the mantissa into the exponent.
  my $digits = "$m";
  my $zeros = $digits =~ s/(0+)\z// ? length $1 : 0;
  return $digits . 'e' . ($e + $zeros);
}

# uniqueItems.
sub unique {
  my ($a) = @_;
  return 1 if @$a < 2;
  my %seen;
  for my $v (@$a) {
    return '' if $seen{ key($v) }++;
  }
  return 1;
}

# The decimal form of a number, as (mantissa as a Math::BigInt, exponent): exact for integers, and for a floating
# point number its shortest text that reads back as the same value (which is what the JSON text said, for any value the
# parser produced).
sub decimal {
  my ($x) = @_;
  return (Math::BigInt->new("$x"), 0) if ref $x;
  my $text = JSON::Schema::Corvus::Json::number_text($x);
  my ($sign, $int, $frac, $exp) = $text =~ /\A(-?)([0-9]+)(?:\.([0-9]+))?(?:e([+-]?[0-9]+))?\z/
    or return (Math::BigInt->bnan, 0);
  $frac //= '';
  return (Math::BigInt->new("$sign$int$frac"), ($exp // 0) - length $frac);
}

# Exact multipleOf over the decimal forms of both numbers.
sub multiple_of {
  my ($x, $d) = @_;
  if (!ref $x && !ref $d && $x == int($x) && $d == int($d) && abs($x) < 2**53 && abs($d) < 2**53) {
    return '' if $d == 0;
    return $x % $d == 0 ? 1 : '';
  }
  return '' if !ref $x && ($x != $x || ($x == $x * 2 && $x != 0));
  my ($am, $ae) = decimal($x);
  my ($bm, $be) = decimal($d);
  return '' if $bm->is_nan || $am->is_nan || $bm->is_zero;
  # x / d is an integer when am * 10^(ae - be) is divisible by bm.
  my $shift = $ae - $be;
  my $num = $shift >= 0 ? $am->copy->bmul(Math::BigInt->new(10)->bpow($shift)) : $am->copy;
  my $den = $shift >= 0 ? $bm->copy : $bm->copy->bmul(Math::BigInt->new(10)->bpow(-$shift));
  return $num->bmod($den)->is_zero ? 1 : '';
}

my $BASE64_RE = qr/\A(?:[A-Za-z0-9+\/]{4})*(?:[A-Za-z0-9+\/]{2}==|[A-Za-z0-9+\/]{3}=)?\z/;

sub _is_json {
  my ($s) = @_;
  return eval { JSON::Schema::Corvus::Json::decode($s); 1 } ? 1 : '';
}

# The draft 7 content assertion: 1 is base64, 2 application/json, 3 both.
sub content {
  my ($s, $kind) = @_;
  return $s =~ $BASE64_RE ? 1 : '' if $kind == 1;
  return _is_json($s) if $kind == 2;
  if ($kind == 3) {
    return '' unless $s =~ $BASE64_RE;
    my $bytes = MIME::Base64::decode_base64($s);
    my $text = Encode::decode('UTF-8', $bytes, Encode::FB_DEFAULT);
    return _is_json($text);
  }
  return 1;
}

# Called when in-place recursion exceeds the configured depth.
sub depth_exceeded { die JSON::Schema::Corvus::Error::Depth->_new }

1;
