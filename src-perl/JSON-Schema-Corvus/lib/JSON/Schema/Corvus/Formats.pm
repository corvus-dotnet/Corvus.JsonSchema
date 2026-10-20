package JSON::Schema::Corvus::Formats;

# Format assertions, applied only when format is asserted (the format-assertion vocabulary, or assert_format). A port
# of the Python package's formats.py; Perl's regular expressions have the Unicode properties and scripts that the
# Python package takes from the regex package.

use 5.026;
use strict;
use warnings;
use Math::BigInt ();
use JSON::Schema::Corvus::Json ();

sub _ascii { $_[0] !~ /[^\x00-\x7f]/ }

my @DAYS = (0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31);

sub _is_leap_year { my ($y) = @_; $y % 4 == 0 && ($y % 100 != 0 || $y % 400 == 0) }

sub date {
  my ($s) = @_;
  my ($y, $mo, $d) = $s =~ /\A([0-9]{4})-([0-9]{2})-([0-9]{2})\z/ or return 0;
  return $mo >= 1 && $mo <= 12 && $d >= 1 && $d <= ($mo == 2 && _is_leap_year($y) ? 29 : $DAYS[$mo]) ? 1 : 0;
}

sub time_format {
  my ($s) = @_;
  my ($h, $mi, $sec, undef, undef, $sign, $oh, $om)
    = $s =~ /\A([0-9]{2}):([0-9]{2}):([0-9]{2})(\.[0-9]+)?([zZ]|([+-])([0-9]{2}):([0-9]{2}))\z/ or return 0;
  $oh //= 0;
  $om //= 0;
  if (defined $sign) {
    return 0 if $oh > 23 || $om > 59;
  }
  return 0 if $h > 23 || $mi > 59 || $sec > 60;
  if ($sec == 60) {
    # A leap second is valid only at 23:59:60 UTC.
    my $direction = defined $sign && $sign eq '-' ? 1 : -1;
    my $utc_minutes = ($h * 60 + $mi + $direction * ($oh * 60 + $om)) % 1440;
    return $utc_minutes == 23 * 60 + 59 ? 1 : 0;
  }
  return 1;
}

sub date_time {
  my ($s) = @_;
  my $t = $s =~ /[tT]/g ? pos($s) - 1 : -1;
  return $t == 10 && date(substr($s, 0, 10)) && time_format(substr($s, 11)) ? 1 : 0;
}

# RFC 3339 appendix A: dur-year = 1*DIGIT "Y" [dur-month], dur-month = 1*DIGIT "M" [dur-day], and so on.
my $DUR_TIME = qr/(?:[0-9]+H(?:[0-9]+M(?:[0-9]+S)?)?|[0-9]+M(?:[0-9]+S)?|[0-9]+S)/;
my $DURATION_RE = qr/\AP(?:[0-9]+W|(?:[0-9]+Y(?:[0-9]+M(?:[0-9]+D)?)?|[0-9]+M(?:[0-9]+D)?|[0-9]+D)(?:T$DUR_TIME)?|T$DUR_TIME)\z/;

sub duration { $_[0] =~ $DURATION_RE ? 1 : 0 }

sub uuid { $_[0] =~ /\A[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z/ ? 1 : 0 }

my $DEC_OCTET = qr/(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])/;

sub ipv4 { $_[0] =~ /\A(?:$DEC_OCTET\.){3}$DEC_OCTET\z/ ? 1 : 0 }

sub ipv6 {
  my ($s) = @_;
  return 0 if length $s < 2 || $s !~ /\A[0-9a-fA-F:.]+\z/;
  my $groups = 8;
  my $tail = $s;
  my $last_colon = rindex($s, ':');
  if (index($s, '.') >= 0) {
    return 0 unless ipv4(substr($s, $last_colon + 1));
    $tail = substr($s, 0, $last_colon + 1) . '0:0';
  }
  my $dbl = index($tail, '::');
  return 0 if $dbl >= 0 && index($tail, '::', $dbl + 1) >= 0;
  my $parts = sub { $_[0] eq '' ? () : split /:/, $_[0], -1 };
  my $hex_ok = sub { $_[0] =~ /\A[0-9a-fA-F]{1,4}\z/ };
  if ($dbl >= 0) {
    my @left = $parts->(substr($tail, 0, $dbl));
    my @right = $parts->(substr($tail, $dbl + 2));
    return (!grep { !$hex_ok->($_) } @left, @right) && @left + @right < $groups ? 1 : 0;
  }
  my @every = split /:/, $tail, -1;
  return @every == $groups && !(grep { !$hex_ok->($_) } @every) ? 1 : 0;
}

my $LABEL_RE = qr/\A[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\z/;

sub _is_a_label { lc(substr($_[0], 0, 4)) eq 'xn--' }

sub _hostname_label_ok {
  my ($label) = @_;
  return 0 if length $label == 0 || length $label > 63 || $label !~ $LABEL_RE;
  # "--" in the third and fourth positions is reserved for A-labels (xn--).
  return !(length $label >= 4 && substr($label, 2, 2) eq '--' && !_is_a_label($label)) ? 1 : 0;
}

# RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels.
sub legacy_hostname {
  my ($s) = @_;
  return 0 if length $s == 0 || length $s > 253;
  for my $label (split /\./, $s, -1) {
    return 0 unless length $label > 0 && length $label <= 63 && $label =~ $LABEL_RE;
  }
  return 1;
}

sub hostname {
  my ($s) = @_;
  return 0 if length $s == 0 || length $s > 253;
  for my $label (split /\./, $s, -1) {
    return 0 unless _hostname_label_ok($label);
    return 0 if _is_a_label($label) && !_punycode_label_ok(substr($label, 4));
  }
  return 1;
}

sub _punycode_label_ok {
  my ($encoded) = @_;
  my $decoded = _punycode_decode($encoded);
  return 0 if !defined $decoded || length $decoded == 0 || _ascii($decoded);
  # The encoding must be canonical: encoding the U-label again gives the same A-label.
  return 0 if _punycode_encode($decoded) ne lc $encoded;
  return _idn_label_ok($decoded) && !_disallowed(_strip_exceptions($decoded))
    && _bidi_label_ok($decoded, _bidi_domain($decoded)) ? 1 : 0;
}

# Code points IDNA2008 disallows that the tests exercise: controls, format characters, spaces, unassigned, uppercase
# and titlecase letters (mapped away, never PVALID), and symbols.
sub _strip_exceptions { (my $s = $_[0]) =~ s/[\x{06fd}\x{06fe}\x{0f0b}\x{00b7}\x{05f3}\x{05f4}\x{30fb}\-]//g; $s }
sub _disallowed { $_[0] =~ /[\p{Cc}\p{Cs}\p{Co}\p{Cn}\p{Zs}\p{Zl}\p{Zp}\p{Lu}\p{Lt}\p{Sm}\p{So}\p{P}]/ }

use constant { BASE => 36, T_MIN => 1, T_MAX => 26, SKEW => 38, DAMP => 700 };

sub _adapt {
  my ($delta, $num_points, $first_time) = @_;
  $delta = $first_time ? int($delta / DAMP) : $delta >> 1;
  $delta += int($delta / $num_points);
  my $k = 0;
  while ($delta > ((BASE - T_MIN) * T_MAX) >> 1) {
    $delta = int($delta / (BASE - T_MIN));
    $k += BASE;
  }
  return $k + int(((BASE - T_MIN + 1) * $delta) / ($delta + SKEW));
}

# RFC 3492 encoding, for the canonical round trip and the A-label length checks.
sub _punycode_encode {
  my ($text) = @_;
  my @cps = map { ord } split //, $text;
  my $out = join '', map { chr } grep { $_ < 0x80 } @cps;
  my $basic_length = length $out;
  my $h = $basic_length;
  $out .= '-' if $basic_length > 0;
  my ($n, $delta, $bias) = (128, 0, 72);
  my $digit = sub { chr($_[0] < 26 ? $_[0] + 97 : $_[0] + 22) };
  while ($h < @cps) {
    my $m;
    for (@cps) { $m = $_ if $_ >= $n && (!defined $m || $_ < $m) }
    $delta += ($m - $n) * ($h + 1);
    $n = $m;
    for my $c (@cps) {
      $delta++ if $c < $n;
      if ($c == $n) {
        my $q = $delta;
        my $k = BASE;
        while (1) {
          my $t = $k <= $bias ? T_MIN : $k >= $bias + T_MAX ? T_MAX : $k - $bias;
          last if $q < $t;
          $out .= $digit->($t + ($q - $t) % (BASE - $t));
          $q = int(($q - $t) / (BASE - $t));
          $k += BASE;
        }
        $out .= $digit->($q);
        $bias = _adapt($delta, $h + 1, $h == $basic_length);
        $delta = 0;
        $h++;
      }
    }
    $delta++;
    $n++;
  }
  return $out;
}

# The RFC 5893 Bidi rule, with Bidi classes approximated by script and general category, as the TypeScript evaluator
# does.
sub _bidi_class {
  my ($c) = @_;
  my $cp = ord $c;
  return 'NSM' if $c =~ /\A[\p{Mn}\p{Me}]/;
  return 'AN' if ($cp >= 0x660 && $cp <= 0x669) || $cp == 0x66B || $cp == 0x66C;
  return 'EN' if ($cp >= 0x30 && $cp <= 0x39) || ($cp >= 0x6F0 && $cp <= 0x6F9);
  return 'R' if $c =~ /\A\p{Script=Hebrew}/;
  return 'AL' if $c =~ /\A[\p{Script=Arabic}\p{Script=Syriac}\p{Script=Thaana}\p{Script=Nko}]/;
  return 'L' if $c =~ /\A[\p{L}\p{Mc}]/;
  return 'ON';
}

sub _bidi_domain {
  my ($label) = @_;
  return 0 unless $label =~ /[\p{Script=Hebrew}\p{Script=Arabic}\p{Script=Syriac}\p{Script=Thaana}\p{Script=Nko}\x{0660}-\x{0669}\x{066b}\x{066c}]/;
  for my $c (split //, $label) {
    my $class = _bidi_class($c);
    return 1 if $class eq 'R' || $class eq 'AL' || $class eq 'AN';
  }
  return 0;
}

sub _bidi_label_ok {
  my ($label, $is_bidi_domain) = @_;
  return 1 unless $is_bidi_domain;
  my @classes = map { _bidi_class($_) } split //, $label;
  my $first = $classes[0];
  my $last = $#classes;
  $last-- while $last > 0 && $classes[$last] eq 'NSM';
  my %has = map { $_ => 1 } @classes;
  if ($first eq 'R' || $first eq 'AL') {
    return 0 if $has{L};
    return 0 unless $classes[$last] =~ /\A(?:R|AL|EN|AN)\z/;
    return !($has{EN} && $has{AN}) ? 1 : 0;
  }
  if ($first eq 'L') {
    return 0 if $has{R} || $has{AL} || $has{AN};
    return $classes[$last] eq 'L' || $classes[$last] eq 'EN' ? 1 : 0;
  }
  return 0;
}

# RFC 3492 decoding, enough to validate A-labels.
sub _punycode_decode {
  my ($text) = @_;
  my ($n, $i, $bias) = (128, 0, 72);
  my @output;
  my $basic = rindex($text, '-');
  $basic = 0 if $basic < 0;
  for my $j (0 .. $basic - 1) {
    my $c = ord substr($text, $j, 1);
    return undef if $c >= 0x80;
    push @output, $c;
  }
  my $index = $basic > 0 ? $basic + 1 : 0;
  while ($index < length $text) {
    my $oldi = $i;
    my $w = 1;
    my $k = BASE;
    while (1) {
      return undef if $index >= length $text;
      my $c = ord substr($text, $index++, 1);
      my $digit = $c - 48 < 10 ? $c - 22 : $c - 65 < 26 ? $c - 65 : $c - 97 < 26 ? $c - 97 : BASE;
      return undef if $digit < 0 || $digit >= BASE;
      $i += $digit * $w;
      my $t = $k <= $bias ? T_MIN : $k >= $bias + T_MAX ? T_MAX : $k - $bias;
      last if $digit < $t;
      $w *= BASE - $t;
      $k += BASE;
    }
    $bias = _adapt($i - $oldi, @output + 1, $oldi == 0);
    $n += int($i / (@output + 1));
    $i %= @output + 1;
    return undef if $n > 0x10FFFF;
    splice @output, $i, 0, $n;
    $i++;
  }
  no warnings 'surrogate';
  return join '', map { chr } @output;
}

my %VIRAMAS = map { $_ => 1 } (
  0x094D, 0x09CD, 0x0A4D, 0x0ACD, 0x0B4D, 0x0BCD, 0x0C4D, 0x0CCD, 0x0D3B, 0x0D3C, 0x0D4D, 0x0DCA, 0x0E3A, 0x0EBA,
  0x0F84, 0x1039, 0x103A, 0x1714, 0x1734, 0x17D2, 0x1A60, 0x1B44, 0x1BAA, 0x1BAB, 0x1BF2, 0x1BF3, 0x2D7F, 0xA806,
  0xA8C4, 0xA953, 0xA9C0, 0xAAF6, 0xABED,
);

my %FORBIDDEN = map { $_ => 1 } (0x302E, 0x302F, 0x0640, 0x07FA, 0x3031, 0x3032, 0x3033, 0x3034, 0x3035, 0x303B);

# The contextual and disallowed code points of RFC 5892 that the test suite exercises.
sub _idn_label_ok {
  my ($label) = @_;
  return 0 if length $label == 0;
  return 0 if substr($label, 0, 1) eq '-' || substr($label, -1) eq '-';
  return 0 if length $label >= 4 && substr($label, 2, 2) eq '--';
  my @cps = map { ord } split //, $label;
  return 0 if $label =~ /\A\p{M}/;
  return 0 if (grep { $_ >= 0x660 && $_ <= 0x669 } @cps) && (grep { $_ >= 0x6F0 && $_ <= 0x6F9 } @cps);
  for my $i (0 .. $#cps) {
    my $c = $cps[$i];
    return 0 if $FORBIDDEN{$c};
    if ($c == 0x00B7) {
      # MIDDLE DOT: between two 'l'.
      return 0 unless $i > 0 && $i < $#cps && $cps[$i - 1] == 0x6C && $cps[$i + 1] == 0x6C;
    }
    elsif ($c == 0x0375) {
      # GREEK KERAIA: followed by Greek.
      return 0 unless $i < $#cps && chr($cps[$i + 1]) =~ /\p{Script=Greek}/;
    }
    elsif ($c == 0x05F3 || $c == 0x05F4) {
      # HEBREW GERESH or GERSHAYIM: preceded by Hebrew.
      return 0 unless $i > 0 && chr($cps[$i - 1]) =~ /\p{Script=Hebrew}/;
    }
    elsif ($c == 0x30FB) {
      # KATAKANA MIDDLE DOT: the label has Hiragana, Katakana or Han.
      return 0 unless grep { $_ != 0x30FB && chr($_) =~ /[\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Han}]/ } @cps;
    }
    elsif ($c == 0x200D) {
      # ZERO WIDTH JOINER: preceded by a virama.
      return 0 unless $i > 0 && $VIRAMAS{ $cps[$i - 1] };
    }
    elsif ($c == 0x200C) {
      # ZERO WIDTH NON-JOINER: preceded by a virama (the joining-type rule is approximated).
      return 0 unless ($i > 0 && $VIRAMAS{ $cps[$i - 1] }) || _zwnj_joining_context(\@cps, $i);
    }
  }
  return 1;
}

# (Joining_Type:{L,D})(Joining_Type:T)*‌(Joining_Type:T)*(Joining_Type:{R,D}), approximated with Arabic letters.
sub _zwnj_joining_context {
  my ($cps, $i) = @_;
  my $is_joiner = sub { ($_[0] >= 0x0620 && $_[0] <= 0x064A) || ($_[0] >= 0x066E && $_[0] <= 0x06D3) };
  my $left = $i - 1;
  $left-- while $left >= 0 && chr($cps->[$left]) =~ /\p{Mn}/;
  my $right = $i + 1;
  $right++ while $right < @$cps && chr($cps->[$right]) =~ /\p{Mn}/;
  return $left >= 0 && $right < @$cps && $is_joiner->($cps->[$left]) && $is_joiner->($cps->[$right]) ? 1 : 0;
}

sub idn_hostname {
  my ($s) = @_;
  return 0 if length $s == 0;
  # Label separators: full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
  my @labels = split /[.\x{3002}\x{ff0e}\x{ff61}]/, $s, -1;
  my @unicode_labels = map { _is_a_label($_) ? (_punycode_decode(substr($_, 4)) || $_) : $_ } @labels;
  my $is_bidi_domain = grep { _bidi_domain($_) } @unicode_labels;
  my $ascii_length = 0;
  for my $i (0 .. $#labels) {
    my $label = $labels[$i];
    return 0 if length $label == 0;
    if (_ascii($label)) {
      return 0 unless _hostname_label_ok($label);
      return 0 if _is_a_label($label) && !_punycode_label_ok(substr($label, 4));
      return 0 unless _bidi_label_ok($unicode_labels[$i], $is_bidi_domain);
      $ascii_length += length($label) + 1;
    }
    else {
      return 0 unless _idn_label_ok($label);
      (my $without_joiners = $label) =~ s/[\x{200c}\x{200d}]//g;
      return 0 if $without_joiners =~ /[\p{Cc}\p{Cf}\p{Zs}\p{Cn}]/ || _disallowed(_strip_exceptions($without_joiners));
      return 0 unless _bidi_label_ok($label, $is_bidi_domain);
      my $a_label = 'xn--' . _punycode_encode($label);
      return 0 if length $a_label > 63;
      $ascii_length += length($a_label) + 1;
    }
  }
  return $ascii_length - 1 <= 253 ? 1 : 0;
}

my $EMAIL_LOCAL_RE = qr/\A(?:[A-Za-z0-9!#\$%&'*+\/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#\$%&'*+\/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")\z/s;
my $IDN_EMAIL_LOCAL_RE = qr/\A(?:[\p{L}\p{M}\p{N}!#\$%&'*+\/=?^_`{|}~-]+(?:\.[\p{L}\p{M}\p{N}!#\$%&'*+\/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")\z/s;

sub _email_domain {
  my ($domain, $idn) = @_;
  if (substr($domain, 0, 1) eq '[' && substr($domain, -1) eq ']') {
    my $inner = substr($domain, 1, -1);
    return ipv6(substr($inner, 5)) if lc(substr($inner, 0, 5)) eq 'ipv6:';
    return ipv4($inner);
  }
  return $idn ? idn_hostname($domain) : hostname($domain);
}

sub email {
  my ($s) = @_;
  my $at = rindex($s, '@');
  return 0 if $at <= 0;
  return substr($s, 0, $at) =~ $EMAIL_LOCAL_RE && _email_domain(substr($s, $at + 1), 0) ? 1 : 0;
}

sub idn_email {
  my ($s) = @_;
  my $at = rindex($s, '@');
  return 0 if $at <= 0;
  return substr($s, 0, $at) =~ $IDN_EMAIL_LOCAL_RE && _email_domain(substr($s, $at + 1), 1) ? 1 : 0;
}

# The RFC 3986 (URI) and RFC 3987 (IRI) grammars.
my $HEX = '[0-9A-Fa-f]';
my $PCT = "%$HEX\{2}";
my $SUB = q{[!$&'()*+,;=]};
my $UNRESERVED = '[A-Za-z0-9\-._~]';
my $UCSCHAR = '[\x{a0}-\x{d7ff}\x{f900}-\x{fdcf}\x{fdf0}-\x{ffef}\x{10000}-\x{efffd}]';

sub _uri_regex {
  my ($iri, $reference) = @_;
  my $unreserved = $iri ? "(?:$UNRESERVED|$UCSCHAR)" : $UNRESERVED;
  my $pchar = "(?:$unreserved|$PCT|$SUB|[:\@])";
  my $query = $iri ? "(?:$pchar|[/?]|[\\x{e000}-\\x{f8ff}\\x{f0000}-\\x{ffffd}\\x{100000}-\\x{10fffd}])*"
    : "(?:$pchar|[/?])*";
  my $fragment = "(?:$pchar|[/?])*";
  my $dec_octet = '(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])';
  my $ip4 = "$dec_octet(?:\\.$dec_octet){3}";
  my $h16 = "$HEX\{1,4}";
  my $ls32 = "(?:$h16:$h16|$ip4)";
  my $ip6 = "(?:(?:$h16:){6}$ls32|::(?:$h16:){5}$ls32|(?:$h16)?::(?:$h16:){4}$ls32"
    . "|(?:(?:$h16:){0,1}$h16)?::(?:$h16:){3}$ls32"
    . "|(?:(?:$h16:){0,2}$h16)?::(?:$h16:){2}$ls32|(?:(?:$h16:){0,3}$h16)?::$h16:$ls32"
    . "|(?:(?:$h16:){0,4}$h16)?::$ls32"
    . "|(?:(?:$h16:){0,5}$h16)?::$h16|(?:(?:$h16:){0,6}$h16)?::)";
  my $ip_literal = "\\[(?:$ip6|v$HEX+\\.(?:$UNRESERVED|$SUB|:)+)\\]";
  my $reg_name = "(?:$unreserved|$PCT|$SUB)*";
  my $authority = "(?:(?:$unreserved|$PCT|$SUB|:)*\@)?(?:$ip_literal|$ip4|$reg_name)(?::[0-9]*)?";
  my $segment = "$pchar*";
  my $segment_nz = "$pchar+";
  my $segment_nz_nc = "(?:$unreserved|$PCT|$SUB|\@)+";
  my $hier_part = "(?://$authority(?:/$segment)*|/(?:$segment_nz(?:/$segment)*)?|$segment_nz(?:/$segment)*|)";
  my $relative_part = "(?://$authority(?:/$segment)*|/(?:$segment_nz(?:/$segment)*)?|$segment_nz_nc(?:/$segment)*|)";
  my $scheme = '[A-Za-z][A-Za-z0-9+\-.]*';
  my $absolute = "$scheme:$hier_part(?:\\?$query)?(?:#$fragment)?";
  my $relative = "$relative_part(?:\\?$query)?(?:#$fragment)?";
  my $source = '\A(?:' . ($reference ? "$absolute|$relative" : $absolute) . ')\z';
  return qr/$source/;
}

# The URI and IRI grammars, compiled on first use (they are long).
my %URI_RES;

sub _uri_re {
  my ($iri, $reference) = @_;
  return $URI_RES{"$iri$reference"} //= _uri_regex($iri, $reference);
}

sub uri { $_[0] =~ _uri_re(0, 0) ? 1 : 0 }
sub uri_reference { $_[0] =~ _uri_re(0, 1) ? 1 : 0 }
sub iri { $_[0] =~ _uri_re(1, 0) ? 1 : 0 }
sub iri_reference { $_[0] =~ _uri_re(1, 1) ? 1 : 0 }

my $VARCHAR = '(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})';
my $VARSPEC = "$VARCHAR(?:\\.?$VARCHAR)*(?::[1-9][0-9]{0,3}|\\*)?";
my $URI_TEMPLATE_SOURCE = "\\A(?:[^\\x00-\\x20\"'<>\\\\^`{|}]|\\{[+#./;?&=,!\@|]?$VARSPEC(?:,$VARSPEC)*\\})*\\z";
my $URI_TEMPLATE_RE = qr/$URI_TEMPLATE_SOURCE/;

sub uri_template { $_[0] =~ $URI_TEMPLATE_RE ? 1 : 0 }

sub json_pointer { $_[0] =~ /\A(?:\/(?:[^~\/]|~[01])*)*\z/s ? 1 : 0 }
sub relative_json_pointer { $_[0] =~ /\A(?:0|[1-9][0-9]*)(?:#|(?:\/(?:[^~\/]|~[01])*)*)\z/s ? 1 : 0 }

sub regex_format {
  require JSON::Schema::Corvus::Pattern;
  return JSON::Schema::Corvus::Pattern::is_valid_ecma_regex($_[0]) ? 1 : 0;
}

# The built-in format assertions, by format name.
our %FORMAT_VALIDATORS = (
  'date' => \&date,
  'time' => \&time_format,
  'date-time' => \&date_time,
  'duration' => \&duration,
  'uuid' => \&uuid,
  'ipv4' => \&ipv4,
  'ipv6' => \&ipv6,
  'hostname' => \&hostname,
  'idn-hostname' => \&idn_hostname,
  'email' => \&email,
  'idn-email' => \&idn_email,
  'uri' => \&uri,
  'uri-reference' => \&uri_reference,
  'iri' => \&iri,
  'iri-reference' => \&iri_reference,
  'uri-template' => \&uri_template,
  'json-pointer' => \&json_pointer,
  'relative-json-pointer' => \&relative_json_pointer,
  'regex' => \&regex_format,
);

# Numeric formats (a Corvus extension): integers of a given width, and floating-point and decimal magnitudes.
sub _integer_range {
  my ($lo, $hi) = map { Math::BigInt->new($_) } @_;
  return sub {
    my ($x) = @_;
    return 0 unless JSON::Schema::Corvus::Json::is_integer($x, 0);
    # Exactly: an integer's digits (a copy's text, which for an integer is its digits), or a whole double's.
    my $text = ref $x ? undef : do { my $copy = $x; "$copy" };
    my $big = ref $x ? $x : Math::BigInt->new($text =~ /\A-?[0-9]+\z/ ? $text : sprintf('%.0f', $x));
    return $big >= $lo && $big <= $hi ? 1 : 0;
  };
}

sub _magnitude {
  my ($limit) = @_;
  return sub {
    my ($x) = @_;
    my $v = ref $x ? $x->numify : $x;
    return abs($v) <= $limit ? 1 : 0;
  };
}

our %NUMERIC_FORMAT_VALIDATORS = (
  'byte' => _integer_range(0, 255),
  'uint16' => _integer_range(0, 65535),
  'uint32' => _integer_range(0, 4294967295),
  'uint64' => _integer_range(0, '18446744073709551615'),
  'uint128' => _integer_range(0, '340282366920938463463374607431768211455'),
  'sbyte' => _integer_range(-128, 127),
  'int16' => _integer_range(-32768, 32767),
  'int32' => _integer_range(-2147483648, 2147483647),
  'int64' => _integer_range('-9223372036854775808', '9223372036854775807'),
  'int128' => _integer_range('-170141183460469231731687303715884105728', '170141183460469231731687303715884105727'),
  'half' => _magnitude(65504),
  'single' => _magnitude(3.40282346638528859e38),
  'double' => _magnitude(1.7976931348623157e308),
  'decimal' => _magnitude(79228162514264337593543950335),
);

my %SINCE = ('uri-reference' => 1, 'uri-template' => 1, 'json-pointer' => 1, 'date' => 2, 'time' => 2, 'regex' => 2,
  'relative-json-pointer' => 2, 'idn-email' => 2, 'idn-hostname' => 2, 'iri' => 2, 'iri-reference' => 2,
  'duration' => 3, 'uuid' => 3);
my %ALWAYS = map { $_ => 1 } qw(byte uint16 uint32 uint64 uint128 sbyte int16 int32 int64 int128 half single double
  decimal date-time email hostname ipv4 ipv6 uri);

# The format a dialect recognises for a format value (SchemaCompiler.GetFormatKind): its canonical name, or 'unknown'
# for names the dialect does not define (which always match).
sub format_kind {
  my ($format, $dialect) = @_;
  return 'single' if $format eq 'float';
  return $format if $ALWAYS{$format};
  my $since = $SINCE{$format};
  return defined $since && $dialect >= $since ? $format : 'unknown';
}

sub is_numeric_format { exists $NUMERIC_FORMAT_VALIDATORS{ $_[0] } }

1;
