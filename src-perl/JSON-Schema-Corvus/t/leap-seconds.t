# A leap second is a valid date-time only at 23:59:60 UTC on a day that ended with one, in whatever offset it is
# written, and a valid time whenever it is 23:59 in UTC. The cases of the Go module's test.
use strict;
use warnings;
use Test::More;
use JSON::Schema::Corvus::Formats;

for my $case (
  ['1998-12-31T23:59:60Z', 1],
  ['1998-12-31T15:59:60.123-08:00', 1],
  ['1972-06-30T23:59:60Z', 1],
  ['2016-12-31T23:59:60z', 1],
  ['2017-01-01T00:59:60+01:00', 1],
  ['1998-12-31T00:29:60-23:30', 1],
  ['1999-01-01T00:29:60+00:30', 1],
  ['1998-12-30T23:59:60-24:00', 0],
  ['1998-12-31T23:59:60+00:00', 1],
  ['1999-12-31T23:59:60Z', 0],
  ['2017-12-31T23:59:60Z', 0],
  ['1998-12-31T23:58:60Z', 0],
  ['1998-12-31T22:59:60Z', 0],
  ['1998-12-31T23:59:61Z', 0],
  ['2016-12-31T22:59:60-01:00', 1],
  ['2016-12-31T00:00:60Z', 0],
  ['1998-12-31T23:59:60+23:59', 0],
  ['1999-01-01T23:58:60+23:59', 1],
  ['1998-12-31T00:00:60-23:59', 1],
) {
  my ($text, $want) = @$case;
  is(JSON::Schema::Corvus::Formats::date_time($text), $want, "date-time $text");
}

for my $case (['23:59:60Z', 1], ['15:59:60-08:00', 1], ['00:29:60-23:30', 1], ['23:58:60Z', 0]) {
  my ($text, $want) = @$case;
  is(JSON::Schema::Corvus::Formats::time_format($text), $want, "time $text");
}

done_testing;
