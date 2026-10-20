package JSON::Schema::Corvus::Json;

# The JSON values the evaluator works on, and a parser that produces them.
#
# A JSON value is a Perl value of one of these forms, which the parser and from_data produce and nothing else changes:
#   null     undef
#   boolean  $TRUE or $FALSE (JSON::PP::Boolean, so that JSON::PP and other modules read them as booleans)
#   object   an unblessed hash reference
#   array    an unblessed array reference
#   number   a scalar created as a number and never used as a string (an integer from an integer literal, a floating
#            point number, created as one, from any other: 1.0 stays apart from 1, as Python's json keeps them), or
#            a Math::BigInt for an integer beyond 64 bits
#   string   a scalar created as a string
# So a number is told from a string by how the scalar was created (is_number), never by what the text looks like.
#
# An object's names have no order in a Perl hash. Where the order matters (a schema's keywords, which decide the order
# of results, and an instance evaluated with a results collector), the parser records it (ordered mode), and names()
# gives the names in that order, or in sorted order for a hash with none recorded (Perl data).

use 5.026;
use strict;
use warnings;
use B ();
use Carp ();
use Hash::Util::FieldHash ();
use JSON::PP ();
use Math::BigInt ();
use Scalar::Util ();
# builtin::created_as_number (5.36 and later) is still experimental, and its warning category is unknown before.
no if $] >= 5.036, warnings => 'experimental::builtin';

our $TRUE = $JSON::PP::true;
our $FALSE = $JSON::PP::false;

# The order of an object's names, for objects parsed in ordered mode. Keyed by the hash itself: an entry goes when its
# hash does.
Hash::Util::FieldHash::fieldhash(my %ORDER);

# Whether a scalar was created as a number (and has not been used as a string since), as JSON::PP decides it.
BEGIN {
  if (defined &builtin::created_as_number) {
    no warnings;
    *_created_as_number = \&builtin::created_as_number;
  }
  else {
    *_created_as_number = sub {
      my $flags = B::svref_2object(\$_[0])->FLAGS;
      return ($flags & (B::SVp_IOK | B::SVp_NOK)) && !($flags & B::SVp_POK) ? 1 : '';
    };
  }
}

sub is_number {
  my $r = ref $_[0];
  return $r eq 'Math::BigInt' if $r;
  return defined $_[0] && _created_as_number($_[0]);
}

sub is_string {
  return !ref $_[0] && defined $_[0] && !_created_as_number($_[0]);
}

sub is_bool { ref $_[0] eq 'JSON::PP::Boolean' }

# Whether a number is an integer: any whole number from draft 6, and in draft 4 one written without a fraction or an
# exponent (an integer literal, or a Math::BigInt).
sub is_integer {
  my ($v, $draft4) = @_;
  return 1 if ref $v eq 'Math::BigInt';
  return '' unless defined $v && !ref $v && _created_as_number($v);
  if ($draft4) {
    my $flags = B::svref_2object(\$v)->FLAGS;
    return ($flags & B::SVf_IOK) ? 1 : '';
  }
  return $v == int($v) && $v - $v == 0 ? 1 : '';
}

# The JSON type of a value: null, boolean, object, array, number or string.
sub type_of {
  my ($v) = @_;
  return 'null' unless defined $v;
  my $r = ref $v;
  return $r eq 'HASH' ? 'object' : $r eq 'ARRAY' ? 'array' : $r eq 'JSON::PP::Boolean' ? 'boolean'
    : $r eq 'Math::BigInt' ? 'number' : Carp::confess("not a JSON value: $r") if $r;
  return _created_as_number($v) ? 'number' : 'string';
}

# An object's names, in the order the text gave them (ordered mode), or sorted.
sub names {
  my ($hash) = @_;
  my $order = $ORDER{$hash};
  return $order && @$order == keys %$hash ? @$order : sort keys %$hash;
}

sub set_order {
  my ($hash, $names) = @_;
  $ORDER{$hash} = $names;
  return $hash;
}

# --- The parser ---

my %ESCAPES = ('"' => '"', '\\' => '\\', '/' => '/', b => "\b", f => "\f", n => "\n", r => "\r", t => "\t");

# Parses JSON text into a JSON value. The text is characters or UTF-8 bytes (a string with the UTF8 flag off is
# decoded as UTF-8). Dies with a JSON::Schema::Corvus::Error::InvalidJson for text that is not JSON, or not UTF-8.
# With $ordered, objects keep the order of their names (see names).
sub decode {
  my ($text, $ordered) = @_;
  JSON::Schema::Corvus::Error->throw_usage('the text is undef') unless defined $text;
  if (!utf8::is_utf8($text)) {
    my $copy = $text;
    if (!utf8::decode($copy) && $copy =~ /[\x80-\xff]/) {
      _invalid_utf8($text);
    }
    $text = $copy;
  }
  pos($text) = 0;
  my $value = _value(\$text, $ordered, 0);
  $text =~ /\G[ \t\r\n]*/gc;
  _fail(\$text, 'unexpected text after the value') if pos($text) < length $text;
  return $value;
}

sub _invalid_utf8 {
  my ($bytes) = @_;
  # The offset of the first byte that does not start or continue a valid UTF-8 sequence.
  my $offset = 0;
  while ($bytes =~ /\G(?:[\x00-\x7f]|[\xc2-\xdf][\x80-\xbf]|\xe0[\xa0-\xbf][\x80-\xbf]|[\xe1-\xec\xee\xef][\x80-\xbf]{2}
      |\xed[\x80-\x9f][\x80-\xbf]|\xf0[\x90-\xbf][\x80-\xbf]{2}|[\xf1-\xf3][\x80-\xbf]{3}|\xf4[\x80-\x8f][\x80-\xbf]{2})/gcx) {
    $offset = pos $bytes;
  }
  die JSON::Schema::Corvus::Error::InvalidJson->_new("invalid UTF-8 at byte $offset", $offset);
}

sub _fail {
  my ($text, $what) = @_;
  my $at = pos($$text) // 0;
  # The offset in UTF-8 bytes, as the C library reports it.
  my $prefix = substr($$text, 0, $at);
  utf8::encode($prefix);
  my $offset = length $prefix;
  die JSON::Schema::Corvus::Error::InvalidJson->_new("invalid JSON: $what at byte $offset", $offset);
}

sub _value {
  my ($text, $ordered, $depth) = @_;
  _fail($text, 'the value is nested too deeply') if $depth > 10000;
  $$text =~ /\G[ \t\r\n]*/gc;
  if ($$text =~ /\G"/gc) {
    return _string($text);
  }
  if ($$text =~ /\G\{/gc) {
    my %object;
    my @order;
    $$text =~ /\G[ \t\r\n]*/gc;
    if ($$text =~ /\G\}/gc) {
      set_order(\%object, []) if $ordered;
      return \%object;
    }
    while (1) {
      $$text =~ /\G[ \t\r\n]*/gc;
      $$text =~ /\G"/gc or _fail($text, 'expected a name');
      my $name = _string($text);
      $$text =~ /\G[ \t\r\n]*:/gc or _fail($text, 'expected :');
      push @order, $name if $ordered && !exists $object{$name};
      $object{$name} = _value($text, $ordered, $depth + 1);
      $$text =~ /\G[ \t\r\n]*/gc;
      next if $$text =~ /\G,/gc;
      last if $$text =~ /\G\}/gc;
      _fail($text, 'expected , or }');
    }
    set_order(\%object, \@order) if $ordered;
    return \%object;
  }
  if ($$text =~ /\G\[/gc) {
    my @array;
    $$text =~ /\G[ \t\r\n]*/gc;
    return \@array if $$text =~ /\G\]/gc;
    while (1) {
      push @array, _value($text, $ordered, $depth + 1);
      $$text =~ /\G[ \t\r\n]*/gc;
      next if $$text =~ /\G,/gc;
      last if $$text =~ /\G\]/gc;
      _fail($text, 'expected , or ]');
    }
    return \@array;
  }
  if ($$text =~ /\G(-?(?:0|[1-9][0-9]*))((?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)/gc) {
    return _number($1, $2);
  }
  return $TRUE if $$text =~ /\Gtrue/gc;
  return $FALSE if $$text =~ /\Gfalse/gc;
  return undef if $$text =~ /\Gnull/gc;
  _fail($text, pos($$text) >= length $$text ? 'unexpected end of input' : 'unexpected character');
}

# A number from its integer part and the rest (a fraction and an exponent). An integer literal is an integer (a
# Math::BigInt beyond 64 bits); any other is a floating point number, created as one even when it is whole.
sub _number {
  my ($integer, $rest) = @_;
  if ($rest eq '') {
    # The digits of the largest 64-bit values: shorter integers fit.
    return 0 + $integer if length($integer) < 19;
    my $n = 0 + $integer;
    return $n if "$n" eq $integer && $n == int($n) && B::svref_2object(\$n)->FLAGS & B::SVf_IOK;
    return Math::BigInt->new($integer);
  }
  return to_double($integer . $rest);
}

# A decimal number's text as the nearest double, created as a floating point number. Perl reads decimal text correctly
# rounded from 5.30; before that, a long mantissa can come back as a neighbouring double, so the C library's strtod
# reads it there (Perl keeps the numeric locale of C for it outside 'use locale').
BEGIN {
  if ($] >= 5.030) {
    *to_double = sub { unpack('d', pack('d', $_[0])) };
  }
  else {
    require POSIX;
    *to_double = sub { (POSIX::strtod($_[0]))[0] };
  }
}

sub _string {
  my ($text) = @_;
  my $s = '';
  while (1) {
    if ($$text =~ /\G([^"\\\x00-\x1f]+)/gc) {
      $s .= $1;
    }
    if ($$text =~ /\G"/gc) {
      return $s;
    }
    if ($$text =~ /\G\\/gc) {
      if ($$text =~ /\G(["\\\/bfnrt])/gc) {
        $s .= $ESCAPES{$1};
      }
      elsif ($$text =~ /\Gu([0-9a-fA-F]{4})/gc) {
        my $unit = hex $1;
        if ($unit >= 0xd800 && $unit <= 0xdbff && $$text =~ /\G\\u([dD][c-fC-F][0-9a-fA-F]{2})/gc) {
          $s .= chr(0x10000 + (($unit - 0xd800) << 10) + (hex($1) - 0xdc00));
        }
        else {
          # A lone surrogate is kept as its code point, as JavaScript keeps it.
          no warnings 'surrogate';
          $s .= chr $unit;
        }
      }
      else {
        _fail($text, 'invalid escape');
      }
      next;
    }
    _fail($text, pos($$text) >= length $$text ? 'unterminated string' : 'control character in a string');
  }
}

# --- Perl data ---

# Converts Perl data into a JSON value, by the rules JSON::PP follows (see JSON::Schema::Corvus): a copy, in which
# every scalar has the form above. Dies for data that has no JSON form.
sub from_data {
  my ($data, $depth) = @_;
  $depth //= 0;
  Carp::croak('JSON::Schema::Corvus: the data is nested more than 512 deep (or holds a cycle)') if $depth > 512;
  return undef unless defined $data;
  if (my $r = ref $data) {
    return { map { $_ => from_data($data->{$_}, $depth + 1) } keys %$data } if $r eq 'HASH';
    return [ map { from_data($_, $depth + 1) } @$data ] if $r eq 'ARRAY';
    return $data ? $TRUE : $FALSE if $r eq 'JSON::PP::Boolean';
    if ($r eq 'SCALAR' && !ref $$data && defined $$data) {
      return $TRUE if $$data eq '1';
      return $FALSE if $$data eq '0';
    }
    if (Scalar::Util::blessed($data)) {
      return $data->copy if $data->isa('Math::BigInt') && $data->is_int;
      if ($data->isa('Math::BigInt') || $data->isa('Math::BigFloat')) {
        Carp::croak("JSON::Schema::Corvus: the $r $data has no JSON form") if $data->is_nan || $data->is_inf;
        return _from_decimal("$data");
      }
      return $data ? $TRUE : $FALSE if $data->isa('JSON::PP::Boolean');
      if (my $to_json = $data->can('TO_JSON')) {
        my $value = $data->$to_json;
        Carp::croak("JSON::Schema::Corvus: ${r}::TO_JSON returned the object itself")
          if ref $value && Scalar::Util::refaddr($value) == Scalar::Util::refaddr($data);
        return from_data($value, $depth + 1);
      }
      Carp::croak("JSON::Schema::Corvus: an object of class $r has no JSON form (it has no TO_JSON method)");
    }
    Carp::croak('JSON::Schema::Corvus: a reference that is not to a hash or an array has no JSON form '
      . '(\1 and \0 are true and false)');
  }
  if (defined &builtin::is_bool) {
    no warnings;
    return $data ? $TRUE : $FALSE if builtin::is_bool($data);
  }
  # A number by JSON::PP's rule: a scalar with a number and no string, by its flags (builtin::created_as_number says
  # number for a number that has since been used as a string, which JSON::PP writes as a string).
  my $flags = B::svref_2object(\$data)->FLAGS;
  if (($flags & (B::SVp_IOK | B::SVp_NOK)) && !($flags & B::SVp_POK)) {
    my $copy = $data;
    Carp::croak('JSON::Schema::Corvus: ' . ($copy != $copy ? 'NaN' : 'an infinite number') . ' has no JSON form')
      if $copy != $copy || ($copy == $copy * 2 && $copy != 0);
    return $copy + 0;
  }
  return "$data";
}

# A number from its decimal text (a Math::BigFloat's): an integer when it has no fraction, a floating point number
# otherwise.
sub _from_decimal {
  my ($text) = @_;
  return Math::BigInt->new($text) if $text =~ /\A-?[0-9]+\z/ && length($text) >= 19;
  return 0 + $text if $text =~ /\A-?[0-9]+\z/;
  return to_double($text);
}

# --- Writing JSON text ---

# The JSON text of a value: names in the order names() gives (canonical for Perl data), UTF-8 characters (not bytes).
sub encode {
  my ($v) = @_;
  return 'null' unless defined $v;
  my $r = ref $v;
  if ($r eq 'HASH') {
    return '{' . join(',', map { _quote($_) . ':' . encode($v->{$_}) } names($v)) . '}';
  }
  return '[' . join(',', map { encode($_) } @$v) . ']' if $r eq 'ARRAY';
  return $v ? 'true' : 'false' if $r eq 'JSON::PP::Boolean';
  return "$v" if $r eq 'Math::BigInt';
  return number_text($v) if _created_as_number($v);
  return _quote($v);
}

# The text of a number: the shortest that reads back as the same double (as JavaScript and Python write it).
sub number_text {
  my ($n) = @_;
  return "$n" if ref $n;
  return sprintf('%d', $n) if $n == int($n) && abs($n) < 1e15 && B::svref_2object(\$n)->FLAGS & B::SVf_IOK;
  return 'null' if $n != $n || ($n == $n * 2 && $n != 0);
  for my $precision (15 .. 17) {
    my $text = sprintf("%.${precision}g", $n);
    return _tidy($text) if to_double($text) == $n;
  }
  return _tidy(sprintf('%.17g', $n));
}

sub _tidy {
  my ($text) = @_;
  $text =~ s/e([+-])0*([0-9])/e$1$2/;
  $text =~ s/e\+/e+/;
  return $text;
}

sub _quote {
  my ($s) = @_;
  $s =~ s/(["\\\x00-\x1f])/$1 eq '"' ? '\\"' : $1 eq '\\' ? '\\\\' : $1 eq "\n" ? '\\n' : $1 eq "\t" ? '\\t'
    : $1 eq "\r" ? '\\r' : sprintf('\\u%04x', ord $1)/ge;
  return qq{"$s"};
}

1;
