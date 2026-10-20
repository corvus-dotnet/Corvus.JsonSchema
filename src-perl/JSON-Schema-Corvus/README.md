# JSON::Schema::Corvus

A JSON Schema validator for Perl (draft 4, 6, 7, 2019-09 and 2020-12), in pure Perl with no dependency beyond Perl's
core. It is a port of the Corvus.Text.Json evaluator, by way of its [Python port](../../src-py/corvus-json-schema): a
schema is compiled into Perl, which validates with exactly the checks the schema needs.
[JSON::Schema::Corvus::XS](../JSON-Schema-Corvus-XS) is the same evaluator on the corvus-json-schema C library, with
the same API.

- **Conformant**: passes all of the JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft)
  as JSON text, as a parsed document, through a results collector and as Perl data, and all of its annotation tests.
  Its regular expressions give V8's answer on every record of the
  [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data).
- **Fast**: hundreds of times faster than JSON::Schema::Modern (see [Performance](#performance)).
- **Results and annotations**: a results collector at the basic, detailed or verbose level gives the same rows
  (locations, messages, order) as the other Corvus evaluators, and the annotations grouped by location, keyword and
  schema location.
- **Bounded**: a match of a regular expression has a limit of time (one second unless set), and a regular expression
  has limits of size, so that neither a schema nor an instance from outside can make a validator hang.
- Perl 5.26 or later, on any platform Perl runs on.

## Install

```sh
cpanm JSON::Schema::Corvus
```

## Usage

```perl
use JSON::Schema::Corvus;

my $validator = JSON::Schema::Corvus->compile(<<'JSON');
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": { "id": { "type": "integer", "minimum": 1 } },
  "required": ["id"]
}
JSON

$validator->is_valid_json('{"id": 3}');   # true
$validator->is_valid_json('{"id": 0}');   # false
$validator->is_valid({ id => 3 });        # Perl data: true

my $collector = JSON::Schema::Corvus::ResultsCollector->new('detailed');
$validator->evaluate_json('{"id": "3"}', $collector);
for my $row (@{ $collector->results }) {
  print "$row->{document_evaluation_location}: $row->{message}\n" unless $row->{is_match};
}
```

JSON text is parsed by the package's own parser, which keeps numbers exactly as the text says. For an instance
validated more than once, parse it once with `JSON::Schema::Corvus::Document->parse` and validate the document. Perl
data is read by the rules JSON::PP follows (a scalar that has a number and has never been used as a string is a number;
`JSON::PP::true`, Perl's own booleans, `\1` and `\0` are booleans; `Math::BigInt` is an integer, exactly; an object
with `TO_JSON` is what that returns), and a `Math::BigFloat` with a fraction is the nearest floating point number.

`compile` takes the schema as JSON text or Perl data, and the options of
[JSON::Schema::Corvus::XS](../JSON-Schema-Corvus-XS#usage): `default_dialect`, `assert_format`,
`assert_format_in_legacy_drafts`, `assert_content`, `formats`, `resolve_document`, `base_uri`, `entry_point`,
`max_depth` and `pattern_match_limit`. Failures are thrown as `JSON::Schema::Corvus::Error` objects: `::InvalidJson`
(with the byte `offset`), `::Compilation` (and `::PatternTooLarge`, which is one), `::Depth` and `::PatternMatchLimit`.
The two packages differ only in the prefix of these names, so either can be used as the other. See the module's POD
for the rest.

## Regular expressions

`pattern`, `patternProperties` and `format: regex` are ECMA-262 regular expressions, run with ECMA-262's meaning and
Unicode 17, whatever version of Unicode the Perl has. A pattern is read with the grammar of the `u` flag, and one that
only that grammar refuses with the Annex B grammar of a pattern with no flag.

Two engines run what is read. Perl's own engine runs a pattern that is proved to match in time proportional to the
text (the proof of the Python package), written out with every literal and class as code points, so none of Perl's
own meanings of `\d`, `\w`, `\s`, the dot or its Unicode tables applies. Such a match needs no limit, which is as well,
since Perl's engine cannot be stopped once it has started. Every other pattern (a backreference, a lookbehind, or
anything the proof does not prove) runs on the package's own matcher, a port of the Go port's, with the limit of a
match. That matcher is in Perl, so a deliberately difficult pattern can reach the limit of one second where the XS
package answers; 34 of the 4.3 million texts of the ECMA-262 test data do, and all of them get V8's answer with a limit
of 30 seconds.

Perl 5.40 compiles a class whose members are one case folding set, such as `[\x{1f80}\x{1f88}]`, to a node that
compares with full case folding, which matches neither member of that class. The package never writes such a class:
a class of at most four characters is written as an alternation.

## Performance

Measured with the corpora of [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) (each
schema compiled once, a warm-up pass, then the best of three passes over every instance, parsed before), with Perl
5.40.

- Against JSON::Schema::Modern 0.651, given the schema once with `add_schema` and asked for a flag with
  `short_circuit`, it is 240 to 545 times faster (yamllint 545x, semantic-release 485x, importmap 412x, aws-cdk 364x,
  lerna 270x, stale 240x), and gives the same verdict on every instance.
- JSON::Schema::Corvus::XS is 7 to 50 times faster than this package on a parsed document (20 times on most corpora),
  and gives the same verdict on every instance of all 37 corpora. On JSON text the XS package also parses in C.

## Tests

`make test` runs the API tests, and the JSON-Schema-Test-Suite and its annotation tests when it finds the suite
(`JSON_SCHEMA_TEST_SUITE`, or the repository's submodule two directories up).

## License

Apache License 2.0. The package holds tables derived from the Unicode Character Database, whose notice is in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
