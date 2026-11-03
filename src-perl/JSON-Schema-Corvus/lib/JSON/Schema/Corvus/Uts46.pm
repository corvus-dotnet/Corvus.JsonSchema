package JSON::Schema::Corvus::Uts46;

# The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the idn-hostname and
# idn-email formats apply to a name before they check it.
#
# An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
# "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and
# meet the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse. ASCII is
# kept as it is: its mapping only lowercases the letters, which does not change whether a name is valid. The
# normalization step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name, as
# the JSON-Schema-Test-Suite expects. The table (Uts46Data.pm) is the one the .NET engine and the Rust crate use.

use strict;
use warnings;
use JSON::Schema::Corvus::Uts46Data;

my $ENTRIES = $JSON::Schema::Corvus::Uts46Data::ENTRIES;
my $POOL = $JSON::Schema::Corvus::Uts46Data::POOL;
my $COUNT = length($ENTRIES) / 8;

# The replacement of an ignored or mapped code point outside ASCII ('' when it is ignored), or undef.
sub _find {
  my ($cp) = @_;
  my ($low, $high) = (0, $COUNT);
  while ($low < $high) {
    my $middle = ($low + $high) >> 1;
    my ($s1, $s2, $s3, $count, $offset, $length) = unpack 'C C C n n C', substr($ENTRIES, $middle * 8, 8);
    my $start = ($s1 << 16) | ($s2 << 8) | $s3;
    if ($cp < $start) {
      $high = $middle;
    }
    elsif ($cp > $start + $count) {
      $low = $middle + 1;
    }
    else {
      return '' if $length == 0;
      my $text = substr($POOL, $offset, $length);
      utf8::decode($text);
      return $text;
    }
  }
  return undef;
}

# The mapped form of a name (a string of characters).
sub map_name {
  my ($name) = @_;
  return $name if $name !~ /[^\x00-\x7f]/;
  my $out = '';
  for my $c (split //, $name) {
    my $cp = ord $c;
    if ($cp >= 0x80) {
      my $replacement = _find($cp);
      if (defined $replacement) {
        $out .= $replacement;
        next;
      }
    }
    $out .= $c;
  }
  return $out;
}

1;
