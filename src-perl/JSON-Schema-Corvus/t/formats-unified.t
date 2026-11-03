# The cases of the unified JSON-Schema-Test-Suite that the format checks got wrong (the descriptions are the suite's),
# and the UTS #46 mapping that idn-hostname and idn-email apply.
use strict;
use warnings;
use Test::More;
use JSON::Schema::Corvus::Formats;
use JSON::Schema::Corvus::Uts46;
use JSON::Schema::Corvus::Uts46Data;

my %check = (
  'email'        => \&JSON::Schema::Corvus::Formats::email,
  'idn-email'    => \&JSON::Schema::Corvus::Formats::idn_email,
  'idn-hostname' => \&JSON::Schema::Corvus::Formats::idn_hostname,
  'uri-template' => \&JSON::Schema::Corvus::Formats::uri_template,
);

for my $case (
  ['email', "\"test\\\x{a9}\"\@iana.org", 0, 'a non-ASCII character in a quoted pair is not valid'],
  ['email', "\"\\a\"\@iana.org", 1, 'a quoted pair in the local part is valid'],
  ['idn-email', "\x{85}\@example.com", 1, 'a C1 control in the local part is valid'],
  ['idn-email', "\x{ffff}\@example.com", 1, 'a noncharacter in the local part is valid'],
  ['idn-hostname', "a\x{200b}b", 1, 'a zero width space is ignored by the mapping'],
  ['idn-hostname', "\x{ff41}" x 63, 1, 'a label of 63 fullwidth characters is valid after mapping'],
  ['idn-hostname', "\x{ff58}\x{ff4e}--nxasmq6b", 1, 'an ACE prefix produced by the mapping step is decoded as an A-label'],
  ['uri-template', "a'b", 1, 'an apostrophe in a literal is valid'],
  ['uri-template', 'a%', 0, 'a literal with a lone percent sign is invalid'],
  ['uri-template', 'a%4', 0, 'a literal with an incomplete percent-encoded triplet is invalid'],
  ['uri-template', 'a%GG', 0, 'a literal with non-hex percent encoding is invalid'],
  ['uri-template', "a\x{7f}b", 0, 'a delete character in a literal is invalid'],
) {
  my ($format, $text, $want, $description) = @$case;
  is $check{$format}->($text) ? 1 : 0, $want, "$format: $description";
}

ok !JSON::Schema::Corvus::Formats::idn_hostname("\x{e9}" x 507), 'a name of more than 1,012 UTF-8 bytes is refused';

my %mapped = ('example.com' => 'example.com', "a\x{200b}b" => 'ab', "\x{ff41}\x{ff42}" => 'ab',
  "ex\x{ad}ample" => 'example', "a\x{200d}b" => "a\x{200d}b");
for my $in (sort keys %mapped) {
  is JSON::Schema::Corvus::Uts46::map_name($in), $mapped{$in}, sprintf 'UTS #46 maps %s', join ' ', map { sprintf 'U+%04X', ord } split //, $in;
}
is $JSON::Schema::Corvus::Uts46Data::UNICODE_VERSION, '17.0.0', 'the mapping is of Unicode 17.0.0';

done_testing;
