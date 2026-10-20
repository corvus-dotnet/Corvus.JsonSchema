use strict;
use warnings;
use utf8;
use Test::More;
use JSON::PP ();
use Scalar::Util ();

use JSON::Schema::Corvus::XS;

my $XS = 'JSON::Schema::Corvus::XS';
my $Error = 'JSON::Schema::Corvus::XS::Error';

sub compile { $XS->compile(@_) }

# The exception a block throws.
sub error_of (&) {
  my ($code) = @_;
  return eval { $code->(); 1 } ? undef : $@;
}

my %resolver = (
  'https://example.com/positive.json' => '{"type": "integer", "minimum": 1}',
);
sub everything_is_known { $resolver{ $_[0] } }

subtest 'validate' => sub {
  my $validator = compile('{"type": "object", "properties": {"id": {"type": "integer"}}, "required": ["id"]}');
  isa_ok $validator, 'JSON::Schema::Corvus::XS::Validator';
  ok $validator->is_valid_json('{"id": 3}'), 'a valid instance';
  ok !$validator->is_valid_json('{"id": "3"}'), 'an invalid instance';
  ok !$validator->is_valid_json('{}'), 'a missing property';
  is $validator->is_valid_json('{"id": 3}'), 1, 'true is 1';
  is $validator->is_valid_json('{}'), '', 'false is the empty string';
  ok compile(JSON::PP::true)->is_valid_json('1'), 'a boolean schema as data';
  ok !compile(JSON::PP::false)->is_valid_json('1'), 'false rejects everything';
  ok compile({ type => 'string' })->is_valid('a'), 'a schema as Perl data';
};

subtest 'errors' => sub {
  my $error = error_of { compile('{"type": ') };
  isa_ok $error, "${Error}::InvalidJson", 'a schema that is not JSON';
  ok length($error->message), 'with a message';
  is "$error", $error->message, 'which it stringifies to';

  $error = error_of { compile('{"$ref": "#/$defs/missing"}') };
  isa_ok $error, "${Error}::Compilation", 'an unresolvable reference does not compile';
  like $error->message, qr/missing/, 'and the message says what';

  $error = error_of { compile('{"pattern": "("}') };
  isa_ok $error, "${Error}::Compilation", 'a pattern that is not valid does not compile';
  ok !$error->isa("${Error}::PatternTooLarge"), 'and is not too large';

  my $validator = compile('{"type": "integer"}');
  $error = error_of { $validator->is_valid_json('[1, 2') };
  isa_ok $error, "${Error}::InvalidJson", 'an instance that is not JSON';
  ok $error->offset > 0, 'has the offset where it was found';

  $error = error_of { $validator->is_valid_json(qq{"\xff"}) };
  isa_ok $error, "${Error}::InvalidJson", 'an instance that is not UTF-8';
  is $error->offset, 1, 'at the offset of the first invalid byte';

  $error = error_of { $validator->is_valid_json(undef) };
  isa_ok $error, $Error, 'undef is not text';

  $error = error_of { compile('{}', colour => 'red') };
  like "$error", qr/unknown option colour/, 'an unknown option';

  $error = error_of { JSON::Schema::Corvus::XS::Validator::is_valid_json('nothing', '1') };
  isa_ok $error, $Error, 'a method on something that is not a validator';
};

subtest 'options' => sub {
  my $validator = compile('{"minimum": 5, "exclusiveMinimum": true}', default_dialect => 'draft4');
  ok !$validator->is_valid_json('5') && $validator->is_valid_json('6'), 'default_dialect draft4';
  $validator = compile('{"$schema": "https://json-schema.org/draft/2020-12/schema", "exclusiveMinimum": 5}',
    default_dialect => 'draft4');
  ok !$validator->is_valid_json('5'), '$schema wins over the default dialect';
  like error_of { compile('{}', default_dialect => 'draft3') }, qr/default_dialect/, 'a dialect that is not one';

  ok compile('{"format": "email"}')->is_valid_json('"not an email"'), 'format annotates by default';
  ok !compile('{"format": "email"}', assert_format => 1)->is_valid_json('"not an email"'), 'assert_format => 1';
  ok compile('{"format": "email"}', assert_format => undef)->is_valid_json('"not an email"'),
    'assert_format => undef follows the vocabularies';
  ok !compile('{"format": "email"}', default_dialect => 'draft7', assert_format_in_legacy_drafts => 1)
    ->is_valid_json('"not an email"'), 'assert_format_in_legacy_drafts';

  ok !compile('{"contentMediaType": "application/json"}', default_dialect => 'draft7')->is_valid_json('"{not json"'),
    'draft 7 asserts content by default';
  ok compile('{"contentMediaType": "application/json"}', default_dialect => 'draft7', assert_content => 0)
    ->is_valid_json('"{not json"'), 'assert_content => 0';

  $validator = compile('{"$defs": {"item": {"type": "integer"}}, "type": "string"}', entry_point => '#/$defs/item');
  ok $validator->is_valid_json('3') && !$validator->is_valid_json('"a"'), 'entry_point';

  $validator = compile('{"$ref": "positive.json"}', base_uri => 'https://example.com/root.json',
    resolve_document => \&everything_is_known);
  ok $validator->is_valid_json('2') && !$validator->is_valid_json('0'), 'base_uri';

  my $loop = '{"$defs": {"a": {"$ref": "#/$defs/b"}, "b": {"$ref": "#/$defs/a"}}, "$ref": "#/$defs/a"}';
  my $error = error_of {
    my $looping = compile($loop, max_depth => 16);
    $looping->is_valid_json('1');
  };
  ok $error && ($error->isa("${Error}::Depth") || $error->isa("${Error}::Compilation")),
    'a loop exceeds the depth, or is refused when it is compiled';
  like error_of { compile('{}', max_depth => -1) }, qr/max_depth/, 'a depth that is not a count';
};

subtest 'formats' => sub {
  my $calls = 0;
  my $even = sub { $calls++; $_[0] =~ /\A[0-9]+\z/ && $_[0] % 2 == 0 };
  my $validator = compile('{"format": "even"}', assert_format => 1, formats => { even => $even });
  ok $validator->is_valid_json('"42"'), 'a custom format accepts';
  ok !$validator->is_valid_json('"41"'), 'a custom format rejects';
  is $calls, 2, 'the format is called';
  $validator = compile('{"format": "email"}', assert_format => 1, formats => { email => $even });
  ok $validator->is_valid_json('"8"') && !$validator->is_valid_json('"a@b.c"'),
    'a custom format takes precedence over the built-in one';

  my $seen;
  $validator = compile('{"format": "seen"}', assert_format => 1, formats => { seen => sub { $seen = $_[0]; 1 } });
  $validator->is_valid_json('"café ☕"');
  is $seen, 'café ☕', 'the format gets the string as characters';

  $validator = compile('{"format": "dies"}', assert_format => 1, formats => { dies => sub { die "no formats today\n" } });
  is error_of { $validator->is_valid_json('"x"') }, "no formats today\n", 'an exception from a format is thrown again';
  ok $validator->is_valid_json('1'), 'and the validator still works';
  my $object = bless {}, 'My::Exception';
  $validator = compile('{"format": "dies"}', assert_format => 1, formats => { dies => sub { die $object } });
  is error_of { $validator->is_valid_json('"x"') }, $object, 'as the object it was';

  like error_of { compile('{}', formats => { even => 'not code' }) }, qr/format even/, 'a format that is not code';
};

subtest 'resolver' => sub {
  my $validator = compile('{"$ref": "https://example.com/positive.json"}', resolve_document => \&everything_is_known);
  ok $validator->is_valid_json('1') && !$validator->is_valid_json('-1'), 'a document from the resolver';
  isa_ok error_of { compile('{"$ref": "https://example.com/unknown.json"}', resolve_document => \&everything_is_known) },
    "${Error}::Compilation", 'a document the resolver does not know';
  $validator = $XS->compile_uri('https://example.com/positive.json', resolve_document => \&everything_is_known);
  ok $validator->is_valid_json('5') && !$validator->is_valid_json('0'), 'compile_uri';
  $validator = $XS->compile_uri('https://json-schema.org/draft/2020-12/schema');
  ok $validator->is_valid_json('{"type": "string"}') && !$validator->is_valid_json('{"type": 1}'),
    'compile_uri of a standard metaschema';

  $validator = compile('{"$ref": "https://example.com/data.json"}',
    resolve_document => sub { { type => 'integer', maximum => 9 } });
  ok $validator->is_valid_json('9') && !$validator->is_valid_json('10'), 'a document returned as Perl data';

  is error_of { compile('{"$ref": "https://example.com/x.json"}', resolve_document => sub { die "offline\n" }) },
    "offline\n", 'an exception from the resolver is thrown again';
  like error_of { compile('{"$ref": "https://example.com/x.json"}', resolve_document => sub { [sub {}] }) },
    qr/no JSON form/, 'so is data that has no JSON form';
};

subtest 'limits' => sub {
  my $validator = compile('{"pattern": "^(?:(a+)\\\\1*)+$"}', pattern_match_limit => 0.25);
  my $error = error_of { $validator->is_valid_json('"' . ('a' x 60) . 'b"') };
  isa_ok $error, "${Error}::PatternMatchLimit", 'a match is abandoned at its limit';
  like $error->message, qr/\(a\+\)/, 'the message names the pattern';
  ok $validator->is_valid_json('"aaaa"'), 'a match within the limit is an answer';
  $validator = compile('{"pattern": "^(?:(a+)\\\\1*)+$"}', pattern_match_limit => 0);
  ok !$validator->is_valid_json('"' . ('a' x 12) . 'b"'), 'zero turns the limit off';
  $validator = compile('{"pattern": "^(?:(a+)\\\\1*)+$"}', pattern_match_limit => undef);
  ok !$validator->is_valid_json('"' . ('a' x 12) . 'b"'), 'so does undef';
  my $schema = '{"pattern": "' . ('a' x (1048576 + 1)) . '"}';
  $error = error_of { compile($schema) };
  isa_ok $error, "${Error}::PatternTooLarge", 'a pattern over the limit of size does not compile';
  isa_ok $error, "${Error}::Compilation", 'which is a compilation error';
};

subtest 'collector' => sub {
  my $validator = compile('{"type": "object", "properties": {"id": {"type": "integer"}}, "required": ["name"]}');
  my $basic = JSON::Schema::Corvus::XS::ResultsCollector->new('basic');
  ok !$validator->evaluate_json('{"id": "x"}', $basic), 'evaluate gives the verdict';
  ok $basic->count >= 2, 'basic records the failures';
  is $basic->results->[0]{message}, '', 'basic has no messages';
  ok !$basic->results->[-1]{is_match}, "the root's own result is the last row";

  my $detailed = JSON::Schema::Corvus::XS::ResultsCollector->new;
  $validator->evaluate_json('{"id": "x"}', $detailed);
  my @rows = @{ $detailed->results };
  is scalar(grep { !$_->{is_match} } @rows), scalar(@rows), 'detailed (the default) records failures only';
  ok((grep { $_->{document_evaluation_location} eq '/id' && $_->{evaluation_location} eq '/properties/id/type' } @rows),
    'a row has the instance and evaluation locations');
  ok length($rows[0]{message}) > 0, 'detailed has messages';
  ok length($rows[0]{schema_evaluation_location}) > 0, 'a row has a schema location';

  my $verbose = JSON::Schema::Corvus::XS::ResultsCollector->new('VERBOSE');
  ok $validator->evaluate({ id => 1, name => 'a' }, $verbose), 'evaluate takes Perl data';
  ok $verbose->count > 0, 'verbose records passing results';
  $verbose->clear;
  is $verbose->count, 0, 'clear removes the results';
  like error_of { JSON::Schema::Corvus::XS::ResultsCollector->new('chatty') }, qr/level/, 'a level that is not one';
};

subtest 'annotations' => sub {
  my $validator = compile('{"properties": {"name": {"title": "Name"}}}');
  my $collector = JSON::Schema::Corvus::XS::ResultsCollector->new('verbose');
  $validator->evaluate_json('{"name": "a"}', $collector);
  is_deeply $collector->annotations, { '/name' => { title => { '#/properties/name' => 'Name' } } }, 'annotations';
  ok compile('{"const": {"/name": {"title": {"#/properties/name": "Name"}}}}')->is_valid_json($collector->annotations_json),
    'annotations_json';
};

# The annotation test compares values by validating one against {"const": <the other>}: it must tell them apart, and
# not only find them equal.
subtest 'const comparison' => sub {
  my $same = compile({ const => { '#/a' => 1, '#/b' => [1, { x => 'y' }] } });
  ok $same->is_valid_json('{"#/b": [1.0, {"x": "y"}], "#/a": 1}'), 'numbers by value, members in any order';
  ok !$same->is_valid_json('{"#/a": 1}'), 'a member missing';
  ok !$same->is_valid_json('{"#/a": 2, "#/b": [1, {"x": "y"}]}'), 'a different value';
  ok !$same->is_valid_json('{}'), 'no annotations';
};

subtest 'documents' => sub {
  my $validator = compile('{"type": "array", "items": {"type": "integer"}}');
  my $document = JSON::Schema::Corvus::XS::Document->parse('[1, 2, 3]');
  isa_ok $document, 'JSON::Schema::Corvus::XS::Document';
  ok $validator->is_valid_document($document), 'a valid document';
  my $collector = JSON::Schema::Corvus::XS::ResultsCollector->new('verbose');
  ok $validator->evaluate_document($document, $collector), 'evaluate_document';
  ok !$validator->is_valid_document(JSON::Schema::Corvus::XS::Document->from_data([1, 'two'])), 'from_data';
  isa_ok error_of { JSON::Schema::Corvus::XS::Document->parse('[1,') }, "${Error}::InvalidJson",
    'a document that is not JSON';
};

subtest 'Perl data' => sub {
  my $encode = \&JSON::Schema::Corvus::XS::encode_json;
  is $encode->(undef), 'null', 'undef is null';
  is $encode->(3), '3', 'a number';
  is $encode->(-2.5), '-2.5', 'a fraction';
  is $encode->('3'), '"3"', 'a string of digits is a string';
  my $used = 3; my $text = "$used";
  is $encode->($used), '"3"', 'a number used as a string is a string, as JSON::PP has it';
  my $counted = '7'; my $sum = $counted + 0;
  is $encode->($sum), '7', 'arithmetic gives a number';
  is $encode->(JSON::PP::true), 'true', 'JSON::PP::true';
  is $encode->(JSON::PP::false), 'false', 'JSON::PP::false';
  is $encode->(\1), 'true', '\1';
  is $encode->(\0), 'false', '\0';
  SKIP: {
    skip 'Perl has its own booleans from 5.36', 2 unless $] >= 5.036;
    is $encode->(eval '!!1'), 'true', 'a boolean';
    is $encode->(eval '!!0'), 'false', 'and its false';
  }
  is $encode->({ b => 1, a => [1, { c => undef }] }), '{"a":[1,{"c":null}],"b":1}', 'names in order';
  is $encode->("tab\tquote\"back\\slash\x01"), '"tab\tquote\"back\\\\slash\u0001"', 'escapes';
  my $characters = $encode->({ 'café' => '☕' });
  ok !utf8::is_utf8($characters), 'the text is bytes';
  is $characters, qq({"caf\xc3\xa9":"\xe2\x98\x95"}), 'as UTF-8';
  is $encode->(bless({ v => 1 }, 'With::TO_JSON')), '[1]', 'TO_JSON';
  like error_of { $encode->(bless {}, 'Plain') }, qr/class Plain has no JSON form/, 'an object without TO_JSON';
  like error_of { $encode->(sub {}) }, qr/no JSON form/, 'a code reference';
  like error_of { $encode->(9**9**9) }, qr/infinite number has no JSON form/, 'infinity';
  require Math::BigInt;
  require Math::BigFloat;
  my $big = '98249283749234923498293171823948729348710298301928331';
  is $encode->(Math::BigInt->new($big)), $big, 'a Math::BigInt, exactly';
  is $encode->(Math::BigInt->new("-$big")), "-$big", 'a negative one';
  is $encode->(Math::BigFloat->new('12345678910111213141516171819202122232425262728293031.5')),
    '12345678910111213141516171819202122232425262728293031.5', 'a Math::BigFloat, exactly';
  like error_of { $encode->(Math::BigInt->binf) }, qr/Math::BigInt inf has no JSON form/, 'an infinite Math::BigInt';
  ok compile({ type => 'integer' })->is_valid(Math::BigInt->new($big)), 'a big integer is an integer';
  my @cycle; push @cycle, \@cycle;
  like error_of { $encode->(\@cycle) }, qr/nested more than 512 deep/, 'a cycle';

  my $validator = compile({ type => 'object', properties => { n => { type => 'integer' }, s => { type => 'string' } } });
  ok $validator->is_valid({ n => 1, s => 'x' }), 'data that is valid';
  ok !$validator->is_valid({ n => '1' }), 'a string is not an integer';
  ok !$validator->is_valid({ s => 1 }), 'a number is not a string';
  my $unicode = compile({ const => 'café' });
  ok $unicode->is_valid('café'), 'a character string';
  ok $unicode->is_valid_json(qq("caf\xc3\xa9")), 'and JSON text as UTF-8 bytes';
  ok $unicode->is_valid_json('"café"'), 'or as characters';
};

subtest 'threads' => sub {
  plan skip_all => 'this Perl has no threads' unless eval { require threads; 1 };
  my $validator = compile('{"type": "integer"}');
  my $in_thread = threads->create(sub {
    my $fresh = compile('{"type": "string"}');
    my $copy = Scalar::Util::blessed($validator) ? 'copied' : ref $validator eq 'SCALAR' && !defined $$validator ? 'undef' : 'other';
    my $method = eval { $validator->is_valid_json('1'); 1 } ? 'called' : 'refused';
    return join ',', $copy, $method, $fresh->is_valid_json('"a"') ? 'works' : 'fails';
  })->join;
  is $in_thread, 'undef,refused,works',
    'a validator is not copied into a thread (it is a reference to undef there), and one compiled there works';
  ok $validator->is_valid_json('1'), 'and the original still works';
};

done_testing;

package With::TO_JSON;
sub TO_JSON { [ $_[0]{v} ] }
