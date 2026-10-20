# JSON::Schema::Corvus::XS

A JSON Schema validator for Perl (draft 4, 6, 7, 2019-09 and 2020-12), a binding to the
[corvus-json-schema C library](../../src-rs/corvus-json-schema-capi), which
[Alien::CorvusJsonSchema](../Alien-CorvusJsonSchema) finds or installs. [JSON::Schema::Corvus](../JSON-Schema-Corvus)
is the same evaluator in pure Perl, with the same API.

- **Conformant**: passes all of the JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft)
  as JSON text, as a parsed document, through a results collector and as Perl data, and all of its annotation tests.
- **Results and annotations**: a results collector at the basic, detailed or verbose level gives the same rows
  (locations, messages, order) as the other Corvus evaluators, and the annotations grouped by location, keyword and
  schema location.
- **Bounded**: a match of a regular expression has a limit of time (one second unless set), and a regular expression
  has limits of size, so that neither a schema nor an instance from outside can make a validator hang.
- Perl 5.26 or later, on Linux (x86_64 and aarch64, glibc and musl), macOS and Windows (Strawberry Perl).

## Install

```sh
cpanm JSON::Schema::Corvus::XS
```

This installs Alien::CorvusJsonSchema first, which uses the C library if `pkg-config` finds it, and otherwise
downloads the C library's release package for the platform and links it statically. Building needs a C compiler.

## Usage

```perl
use JSON::Schema::Corvus::XS;

my $validator = JSON::Schema::Corvus::XS->compile(<<'JSON');
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

my $collector = JSON::Schema::Corvus::XS::ResultsCollector->new('detailed');
$validator->evaluate_json('{"id": "3"}', $collector);
for my $row (@{ $collector->results }) {
  print "$row->{document_evaluation_location}: $row->{message}\n" unless $row->{is_match};
}
```

JSON text is the main input: the C library parses it, so numbers, strings and booleans are exactly what the text
says, and validating it creates no Perl values. Perl data is encoded to JSON text first, by the rules JSON::PP
follows (a scalar that has a number and has never been used as a string is a number; `JSON::PP::true`, Perl's own
booleans, `\1` and `\0` are booleans; `Math::BigInt` and `Math::BigFloat` are numbers, written exactly; an object
with `TO_JSON` is what that returns).

`compile` takes the schema as JSON text or Perl data, and these options:

| Option | Meaning |
|---|---|
| `default_dialect` | Dialect for schemas without `$schema`: `draft4`, `draft6`, `draft7`, `draft2019-09` or `draft2020-12` (the default). |
| `assert_format` | True asserts `format`, false never does; left out, it follows the vocabularies (2020-12 `format-assertion`). |
| `assert_format_in_legacy_drafts` | With `assert_format` left out, also assert `format` in drafts 4 to 7. |
| `assert_content` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default true). |
| `formats` | Custom format assertions by name, such as `{ even => sub { length($_[0]) % 2 == 0 } }`. |
| `resolve_document` | `sub ($uri)` returning JSON text, Perl data or `undef`, for remote `$ref`s. The standard metaschemas are built in. |
| `base_uri` | Base URI of the root document. |
| `entry_point` | Evaluate from a subschema, such as `#/$defs/item`. |
| `max_depth` | Depth limit for in-place recursion on a cycle (default 128). |
| `pattern_match_limit` | The seconds one match of a regular expression against one string may take (default 1); `undef` or 0 turns the limit off. |

Failures are thrown as `JSON::Schema::Corvus::XS::Error` objects: `::InvalidJson` (with the byte `offset`),
`::Compilation` (and `::PatternTooLarge`, which is one), `::Depth` and `::PatternMatchLimit`. An exception that a
custom format or the resolver throws is thrown again by the call that ran it. See the module's POD for the rest:
`compile_uri`, parsed documents, and the results collector.

## Tests

`make test` runs the API tests, and the JSON-Schema-Test-Suite and its annotation tests when it finds the suite
(`JSON_SCHEMA_TEST_SUITE`, or the repository's submodule two directories up).

## License

Apache License 2.0.
