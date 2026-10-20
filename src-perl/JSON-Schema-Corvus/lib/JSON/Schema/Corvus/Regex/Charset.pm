package JSON::Schema::Corvus::Regex::Charset;

# Sets of code points for the regular expression engine: a port of the Go port's charset.go and fold.go.
#
# A set is a hash with ranges, a sorted list of inclusive bounds (pairs that neither overlap nor touch). Its members
# are tested through a Perl class written from the ranges (class), which has the set's members and no others whatever
# Perl's own Unicode tables say, since every member is a code point range.

use 5.026;
use strict;
use warnings;
use JSON::Schema::Corvus::Regex::Unicode ();

use constant MAX_RUNE => 0x10FFFF;

my $U = 'JSON::Schema::Corvus::Regex::Unicode';

# A set from inclusive pairs in any order, which may overlap; with $negate, its complement.
sub new_set {
  my ($pairs, $negate) = @_;
  my @order = sort { $pairs->[2 * $a] <=> $pairs->[2 * $b] } 0 .. @$pairs / 2 - 1;
  my @merged;
  for my $i (@order) {
    my ($lo, $hi) = ($pairs->[2 * $i], $pairs->[2 * $i + 1]);
    if (@merged && $lo <= $merged[-1] + 1) {
      $merged[-1] = $hi if $hi > $merged[-1];
      next;
    }
    push @merged, $lo, $hi;
  }
  if ($negate) {
    my @inverse;
    my $next = 0;
    for (my $i = 0; $i < @merged; $i += 2) {
      push @inverse, $next, $merged[$i] - 1 if $merged[$i] > $next;
      $next = $merged[$i + 1] + 1;
    }
    push @inverse, $next, MAX_RUNE if $next <= MAX_RUNE;
    @merged = @inverse;
  }
  return bless { ranges => \@merged }, __PACKAGE__;
}

sub ranges { $_[0]{ranges} }

sub is_empty { !@{ $_[0]{ranges} } }

# The text of a Perl class with exactly the set's members: [\x{30}-\x{39}...], or (?!) for no member.
sub class_text {
  my ($self) = @_;
  return $self->{text} //= do {
    my $r = $self->{ranges};
    if (!@$r) {
      '(?!)';
    }
    else {
      my $t = '[';
      for (my $i = 0; $i < @$r; $i += 2) {
        $t .= sprintf('\x{%x}', $r->[$i]);
        $t .= sprintf('-\x{%x}', $r->[$i + 1]) if $r->[$i + 1] != $r->[$i];
      }
      "$t]";
    }
  };
}

# The set as an anchored regular expression over one character, for tests of membership.
sub _re {
  my ($self) = @_;
  return $self->{re} //= do {
    my $text = $self->class_text;
    no warnings 'regexp';
    qr/\A$text\z/;
  };
}

# Whether the set holds the code point (a number).
sub contains {
  my ($self, $c) = @_;
  if ($c < 128) {
    my $ascii = $self->{ascii} //= do {
      my @a = (0) x 128;
      my $r = $self->{ranges};
      for (my $i = 0; $i < @$r && $r->[$i] < 128; $i += 2) {
        my $hi = $r->[$i + 1] < 127 ? $r->[$i + 1] : 127;
        $a[$_] = 1 for $r->[$i] .. $hi;
      }
      \@a;
    };
    return $ascii->[$c];
  }
  # A set of at most four characters is a hash: Perl compiles a class whose members are one case folding set (such
  # as [\x{fb05}\x{fb06}]) to a node that compares with full case folding, which answers wrongly for a character whose
  # full folding is more than one character. A case folding set has at most four members.
  my $small = $self->{small} //= do {
    my $r = $self->{ranges};
    my $count = 0;
    $count += $r->[ $_ * 2 + 1 ] - $r->[ $_ * 2 ] + 1 for 0 .. @$r / 2 - 1;
    $count <= 4 ? { map { my $i = $_; map { $_ => 1 } $r->[ 2 * $i ] .. $r->[ 2 * $i + 1 ] } 0 .. @$r / 2 - 1 } : 0;
  };
  return $small->{$c} ? 1 : 0 if $small;
  no warnings qw(surrogate nonchar non_unicode);
  return chr($c) =~ $self->_re ? 1 : 0;
}

# --- The classes ECMA-262 defines ---

# \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and . is everything but the four line terminators.
our @DIGIT_PAIRS = (0x30, 0x39);
our @WORD_PAIRS = (0x30, 0x39, 0x41, 0x5A, 0x5F, 0x5F, 0x61, 0x7A);
our @SPACE_PAIRS = (0x9, 0xD, 0x20, 0x20, 0xA0, 0xA0, 0x1680, 0x1680, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x202F,
  0x205F, 0x205F, 0x3000, 0x3000, 0xFEFF, 0xFEFF);
our @LINE_TERMINATOR_PAIRS = (0x0A, 0x0A, 0x0D, 0x0D, 0x2028, 0x2029);
# With the u flag and case-insensitive matching, \w and \b also count the two characters that fold to an ASCII letter
# (U+017F, the long s, and U+212A, the Kelvin sign) as word characters.
our @FOLD_WORD_PAIRS = (@WORD_PAIRS, 0x17F, 0x17F, 0x212A, 0x212A);

our $DIGIT = new_set(\@DIGIT_PAIRS, 0);
our $NOT_DIGIT = new_set(\@DIGIT_PAIRS, 1);
our $WORD = new_set(\@WORD_PAIRS, 0);
our $NOT_WORD = new_set(\@WORD_PAIRS, 1);
our $SPACE = new_set(\@SPACE_PAIRS, 0);
our $NOT_SPACE = new_set(\@SPACE_PAIRS, 1);
our $DOT = new_set(\@LINE_TERMINATOR_PAIRS, 1);
our $ANY = new_set([0, MAX_RUNE], 0);
our $FOLD_WORD = new_set(\@FOLD_WORD_PAIRS, 0);
our $NOT_FOLD_WORD = new_set(\@FOLD_WORD_PAIRS, 1);

sub is_word { my ($c) = @_; ($c >= 0x61 && $c <= 0x7A) || ($c >= 0x41 && $c <= 0x5A) || ($c >= 0x30 && $c <= 0x39)
  || $c == 0x5F }
sub is_fold_word { my ($c) = @_; is_word($c) || $c == 0x17F || $c == 0x212A }
sub is_line_terminator { my ($c) = @_; $c == 0x0A || $c == 0x0D || $c == 0x2028 || $c == 0x2029 }

# --- Unicode properties ---

# The long names and aliases of the General_Category values, to their short names.
my %CATEGORY_NAMES = (
  'Letter' => 'L', 'Lowercase_Letter' => 'Ll', 'Uppercase_Letter' => 'Lu', 'Titlecase_Letter' => 'Lt',
  'Cased_Letter' => 'LC', 'Modifier_Letter' => 'Lm', 'Other_Letter' => 'Lo', 'Mark' => 'M', 'Combining_Mark' => 'M',
  'Nonspacing_Mark' => 'Mn', 'Spacing_Mark' => 'Mc', 'Enclosing_Mark' => 'Me', 'Number' => 'N',
  'Decimal_Number' => 'Nd', 'digit' => 'Nd', 'Letter_Number' => 'Nl', 'Other_Number' => 'No',
  'Punctuation' => 'P', 'punct' => 'P', 'Connector_Punctuation' => 'Pc', 'Dash_Punctuation' => 'Pd',
  'Open_Punctuation' => 'Ps', 'Close_Punctuation' => 'Pe', 'Initial_Punctuation' => 'Pi',
  'Final_Punctuation' => 'Pf', 'Other_Punctuation' => 'Po', 'Symbol' => 'S', 'Math_Symbol' => 'Sm',
  'Currency_Symbol' => 'Sc', 'Modifier_Symbol' => 'Sk', 'Other_Symbol' => 'So', 'Separator' => 'Z',
  'Space_Separator' => 'Zs', 'Line_Separator' => 'Zl', 'Paragraph_Separator' => 'Zp', 'Other' => 'C',
  'Control' => 'Cc', 'cntrl' => 'Cc', 'Format' => 'Cf', 'Surrogate' => 'Cs', 'Private_Use' => 'Co',
  'Unassigned' => 'Cn',
  map { $_ => $_ } qw(L Ll Lu Lt LC Lm Lo M Mn Mc Me N Nd Nl No P Pc Pd Ps Pe Pi Pf Po S Sm Sc Sk So Z Zs Zl Zp C Cc
    Cf Cs Co Cn),
);

# The groups of General_Category values, which have no table of their own.
my %CATEGORY_GROUPS = (
  L => [qw(Lu Ll Lt Lm Lo)], LC => [qw(Lu Ll Lt)], M => [qw(Mn Mc Me)], N => [qw(Nd Nl No)],
  P => [qw(Pc Pd Ps Pe Pi Pf Po)], S => [qw(Sm Sc Sk So)], Z => [qw(Zs Zl Zp)], C => [qw(Cc Cf Cs Co Cn)],
);

# The names and aliases of the binary properties ECMA-262 lists, to their canonical names.
my %BINARY_NAMES = (
  ASCII => 'ASCII', ASCII_Hex_Digit => 'ASCII_Hex_Digit', AHex => 'ASCII_Hex_Digit', Alphabetic => 'Alphabetic',
  Alpha => 'Alphabetic', Any => 'Any', Assigned => 'Assigned', Bidi_Control => 'Bidi_Control',
  Bidi_C => 'Bidi_Control', Bidi_Mirrored => 'Bidi_Mirrored', Bidi_M => 'Bidi_Mirrored',
  Case_Ignorable => 'Case_Ignorable', CI => 'Case_Ignorable', Cased => 'Cased',
  Changes_When_Casefolded => 'Changes_When_Casefolded', CWCF => 'Changes_When_Casefolded',
  Changes_When_Casemapped => 'Changes_When_Casemapped', CWCM => 'Changes_When_Casemapped',
  Changes_When_Lowercased => 'Changes_When_Lowercased', CWL => 'Changes_When_Lowercased',
  Changes_When_NFKC_Casefolded => 'Changes_When_NFKC_Casefolded', CWKCF => 'Changes_When_NFKC_Casefolded',
  Changes_When_Titlecased => 'Changes_When_Titlecased', CWT => 'Changes_When_Titlecased',
  Changes_When_Uppercased => 'Changes_When_Uppercased', CWU => 'Changes_When_Uppercased', Dash => 'Dash',
  Default_Ignorable_Code_Point => 'Default_Ignorable_Code_Point', DI => 'Default_Ignorable_Code_Point',
  Deprecated => 'Deprecated', Dep => 'Deprecated', Diacritic => 'Diacritic', Dia => 'Diacritic', Emoji => 'Emoji',
  Emoji_Component => 'Emoji_Component', EComp => 'Emoji_Component', Emoji_Modifier => 'Emoji_Modifier',
  EMod => 'Emoji_Modifier', Emoji_Modifier_Base => 'Emoji_Modifier_Base', EBase => 'Emoji_Modifier_Base',
  Emoji_Presentation => 'Emoji_Presentation', EPres => 'Emoji_Presentation',
  Extended_Pictographic => 'Extended_Pictographic', ExtPict => 'Extended_Pictographic', Extender => 'Extender',
  Ext => 'Extender', Grapheme_Base => 'Grapheme_Base', Gr_Base => 'Grapheme_Base',
  Grapheme_Extend => 'Grapheme_Extend', Gr_Ext => 'Grapheme_Extend', Hex_Digit => 'Hex_Digit', Hex => 'Hex_Digit',
  IDS_Binary_Operator => 'IDS_Binary_Operator', IDSB => 'IDS_Binary_Operator',
  IDS_Trinary_Operator => 'IDS_Trinary_Operator', IDST => 'IDS_Trinary_Operator', ID_Continue => 'ID_Continue',
  IDC => 'ID_Continue', ID_Start => 'ID_Start', IDS => 'ID_Start', Ideographic => 'Ideographic',
  Ideo => 'Ideographic', Join_Control => 'Join_Control', Join_C => 'Join_Control',
  Logical_Order_Exception => 'Logical_Order_Exception', LOE => 'Logical_Order_Exception', Lowercase => 'Lowercase',
  Lower => 'Lowercase', Math => 'Math', Noncharacter_Code_Point => 'Noncharacter_Code_Point',
  NChar => 'Noncharacter_Code_Point', Pattern_Syntax => 'Pattern_Syntax', Pat_Syn => 'Pattern_Syntax',
  Pattern_White_Space => 'Pattern_White_Space', Pat_WS => 'Pattern_White_Space', Quotation_Mark => 'Quotation_Mark',
  QMark => 'Quotation_Mark', Radical => 'Radical', Regional_Indicator => 'Regional_Indicator',
  RI => 'Regional_Indicator', Sentence_Terminal => 'Sentence_Terminal', STerm => 'Sentence_Terminal',
  Soft_Dotted => 'Soft_Dotted', SD => 'Soft_Dotted', Terminal_Punctuation => 'Terminal_Punctuation',
  Term => 'Terminal_Punctuation', Unified_Ideograph => 'Unified_Ideograph', UIdeo => 'Unified_Ideograph',
  Uppercase => 'Uppercase', Upper => 'Uppercase', Variation_Selector => 'Variation_Selector',
  VS => 'Variation_Selector', White_Space => 'White_Space', space => 'White_Space', XID_Continue => 'XID_Continue',
  XIDC => 'XID_Continue', XID_Start => 'XID_Start', XIDS => 'XID_Start',
);

sub _table_pairs {
  my ($name) = @_;
  return defined $name ? [ @{ JSON::Schema::Corvus::Regex::Unicode::ranges($name) } ] : undef;
}

sub _category_pairs {
  my ($value) = @_;
  my $short = $CATEGORY_NAMES{$value} // return undef;
  if (my $group = $CATEGORY_GROUPS{$short}) {
    return [ map { @{ _table_pairs($JSON::Schema::Corvus::Regex::Unicode::CATEGORY{$_}) } } @$group ];
  }
  return _table_pairs($JSON::Schema::Corvus::Regex::Unicode::CATEGORY{$short});
}

# The Unknown script: unassigned, private use and surrogates.
sub _unknown_script { [ map { @{ _table_pairs($JSON::Schema::Corvus::Regex::Unicode::CATEGORY{$_}) } } qw(Cn Co Cs) ] }

sub _script_pairs {
  my ($map, $value) = @_;
  return _unknown_script() if $value eq 'Unknown' || $value eq 'Zzzz';
  # Katakana_Or_Hiragana (Hrkt) is a value with no code point.
  return [] if $value eq 'Katakana_Or_Hiragana' || $value eq 'Hrkt';
  return _table_pairs($map->{$value});
}

my %PROPERTY_CACHE;

# The set of the expression between the braces of \p{...}, or undef for a name or value ECMA-262 does not define.
sub property_set {
  my ($expr) = @_;
  return $PROPERTY_CACHE{$expr} if exists $PROPERTY_CACHE{$expr};
  my $pairs = _property_pairs($expr);
  return $PROPERTY_CACHE{$expr} = $pairs ? new_set($pairs, 0) : undef;
}

sub _property_pairs {
  my ($expr) = @_;
  if ($expr =~ /\A([^=]*)=(.*)\z/s) {
    my ($name, $value) = ($1, $2);
    return _category_pairs($value) if $name eq 'General_Category' || $name eq 'gc';
    return _script_pairs(\%JSON::Schema::Corvus::Regex::Unicode::SCRIPT, $value) if $name eq 'Script' || $name eq 'sc';
    return _script_pairs(\%JSON::Schema::Corvus::Regex::Unicode::SCRIPT_EXTENSIONS, $value)
      if $name eq 'Script_Extensions' || $name eq 'scx';
    return undef;
  }
  my $category = _category_pairs($expr);
  return $category if $category;
  my $canonical = $BINARY_NAMES{$expr} // return undef;
  return [0, 0x7F] if $canonical eq 'ASCII';
  return [0, MAX_RUNE] if $canonical eq 'Any';
  if ($canonical eq 'Assigned') {
    return new_set(_table_pairs($JSON::Schema::Corvus::Regex::Unicode::CATEGORY{Cn}), 1)->{ranges};
  }
  return _table_pairs($JSON::Schema::Corvus::Regex::Unicode::BINARY{$canonical});
}

# --- Case folding (fold.go) ---
#
# Case-insensitive matching exists only inside a modifier group such as (?i:...). ECMA-262 defines it through a
# Canonicalize function, which differs between the two grammars: with the u flag it is Unicode simple case folding;
# with no flag it is the single character uppercase mapping of a UTF-16 code unit, which never maps a character outside
# ASCII into ASCII. The parser applies it to every literal and class as it reads them; only a backreference needs the
# function when matching.

sub _map_case {
  my ($table, $r) = @_;
  my ($lo, $hi) = (0, scalar @$table);
  while ($lo < $hi) {
    my $mid = ($lo + $hi) >> 1;
    my $row = $table->[$mid];
    if ($r < $row->[0]) {
      $hi = $mid;
    }
    elsif ($r - $row->[0] >= $row->[2]) {
      $lo = $mid + 1;
    }
    elsif ((($r - $row->[0]) & $row->[3]) == 0) {
      return $r + $row->[1];
    }
    else {
      return $r;
    }
  }
  return $r;
}

sub fold {
  my ($r) = @_;
  return $r >= 0x41 && $r <= 0x5A ? $r + 32 : $r if $r < 0x80;
  return _map_case(\@JSON::Schema::Corvus::Regex::Unicode::CASE_FOLDS, $r);
}

sub to_upper {
  my ($r) = @_;
  return $r >= 0x61 && $r <= 0x7A ? $r - 32 : $r if $r < 0x80;
  return _map_case(\@JSON::Schema::Corvus::Regex::Unicode::UPPER_CASES, $r);
}

# The characters of the Basic Multilingual Plane whose uppercase form is more than one character: Canonicalize leaves
# them alone.
my $MULTI_UPPER = new_set([
  0xDF, 0xDF, 0x149, 0x149, 0x1F0, 0x1F0, 0x390, 0x390, 0x3B0, 0x3B0, 0x587, 0x587, 0x1E96, 0x1E9A, 0x1F50, 0x1F50,
  0x1F52, 0x1F52, 0x1F54, 0x1F54, 0x1F56, 0x1F56, 0x1F80, 0x1FAF, 0x1FB2, 0x1FB4, 0x1FB6, 0x1FB7, 0x1FBC, 0x1FBC,
  0x1FC2, 0x1FC4, 0x1FC6, 0x1FC7, 0x1FCC, 0x1FCC, 0x1FD2, 0x1FD3, 0x1FD6, 0x1FD7, 0x1FE2, 0x1FE4, 0x1FE6, 0x1FE7,
  0x1FF2, 0x1FF4, 0x1FF6, 0x1FF7, 0x1FFC, 0x1FFC, 0xFB00, 0xFB06, 0xFB13, 0xFB17,
], 0);

# The Canonicalize function of ECMA-262 for case-insensitive matching.
sub canonicalize {
  my ($r, $unicode) = @_;
  # Every character of a simple case folding class folds to the same character.
  return fold($r) if $unicode;
  # A character beyond the Basic Multilingual Plane is two code units, and neither has an uppercase form.
  return $r if $r > 0xFFFF || $MULTI_UPPER->contains($r);
  my $upper = to_upper($r);
  return $r if ($r >= 128 && $upper < 128) || $upper > 0xFFFF;
  return $upper;
}

# The table of a grammar's equivalence classes: [sorted members of a class of two or more, { member => class }].
my %FOLD_TABLES;

sub _fold_table {
  my ($unicode) = @_;
  return $FOLD_TABLES{ $unicode ? 1 : 0 } //= do {
    my %classes;
    if ($unicode) {
      # A class is a character that others fold to, with those others.
      for my $row (@JSON::Schema::Corvus::Regex::Unicode::CASE_FOLDS) {
        my ($first, $delta, $length, $mask) = @$row;
        next if $delta == 0;
        for my $offset (0 .. $length - 1) {
          next if $offset & $mask;
          my $r = $first + $offset;
          my $key = fold($r);
          $classes{$key} //= [$key];
          push @{ $classes{$key} }, $r;
        }
      }
      # The ASCII letters, which the table has as its first row only above U+007F.
      for my $r (0x41 .. 0x5A) {
        my $key = $r + 32;
        $classes{$key} //= [$key];
        push @{ $classes{$key} }, $r unless grep { $_ == $r } @{ $classes{$key} };
      }
    }
    else {
      for my $r (0 .. 0xFFFF) {
        push @{ $classes{ canonicalize($r, 0) } }, $r;
      }
    }
    my %by_member;
    for my $class (values %classes) {
      next if @$class < 2;
      my %unique = map { $_ => 1 } @$class;
      my @members = sort { $a <=> $b } keys %unique;
      next if @members < 2;
      $by_member{$_} = \@members for @members;
    }
    [ [ sort { $a <=> $b } keys %by_member ], \%by_member ];
  };
}

# The set of every character equivalent to some member of the set, and the number of characters with a case variant
# that the set holds (the work it did).
sub fold_closure {
  my ($set, $unicode) = @_;
  my ($points, $classes) = @{ _fold_table($unicode) };
  my @extra;
  my $variants = 0;
  my $r = $set->{ranges};
  for (my $i = 0; $i < @$r; $i += 2) {
    my ($lo, $hi) = ($r->[$i], $r->[$i + 1]);
    # The first point at or above lo.
    my ($a, $b) = (0, scalar @$points);
    while ($a < $b) {
      my $mid = ($a + $b) >> 1;
      if ($points->[$mid] < $lo) { $a = $mid + 1 } else { $b = $mid }
    }
    for (my $k = $a; $k < @$points && $points->[$k] <= $hi; $k++) {
      $variants++;
      for my $member (@{ $classes->{ $points->[$k] } }) {
        push @extra, $member, $member unless $set->contains($member);
      }
    }
  }
  return ($set, $variants) unless @extra;
  return (new_set([ @extra, @$r ], 0), $variants);
}

1;
