# JSON Schema for Perl

Two CPAN distributions bring the Corvus JSON Schema evaluator to Perl, for draft 4, 6, 7, 2019-09 and 2020-12, with
one API between them:

- **[JSON::Schema::Corvus](https://metacpan.org/dist/JSON-Schema-Corvus)**, in pure Perl, with no dependency beyond
  Perl's core. A schema is compiled into Perl, which validates with exactly the checks the schema needs (a port of
  the [pure-Python package](JsonSchemaForPython.md)'s code generator). It is hundreds of times faster than
  JSON::Schema::Modern.
- **[JSON::Schema::Corvus::XS](https://metacpan.org/dist/JSON-Schema-Corvus-XS)**, a binding to the
  [C library](JsonSchemaForC.md), which
  [Alien::CorvusJsonSchema](https://metacpan.org/dist/Alien-CorvusJsonSchema) finds or installs. It is faster still,
  and parses JSON text in C.

Both pass the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) as JSON text, as
parsed documents, through a results collector and as Perl data, and the suite's annotation tests, and give the same
results and annotations as every other Corvus implementation. They need Perl 5.26 or later. The XS package is tested on
Linux (x86_64 and aarch64, glibc and musl), macOS and Windows with Strawberry Perl.

## Install

```sh
cpanm JSON::Schema::Corvus        # pure Perl
cpanm JSON::Schema::Corvus::XS    # on the C library
```

Installing the XS package installs Alien::CorvusJsonSchema first. That uses the C library if `pkg-config` finds it
(version 0.1.4 or later), and otherwise downloads the C library's release package for the platform, checks it against
the SHA-256 digest the distribution records, and links it statically. Building needs a C compiler.

## Validate

```perl
use JSON::Schema::Corvus;   # or JSON::Schema::Corvus::XS, with the same calls

my $validator = JSON::Schema::Corvus->compile(
  '{"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}}');

$validator->is_valid_json('{"id": 3}');     # true
$validator->is_valid_json('{"id": "3"}');   # false
$validator->is_valid({ id => 3 });          # Perl data: true
```

`compile` takes the schema as JSON text or as Perl data, and `compile_uri` an absolute URI, fetched through the
resolver or one of the standard metaschemas. A validator may be used any number of times.

JSON text is the main input: numbers, strings and booleans are exactly what the text says. Perl data is read by the
rules JSON::PP follows: a scalar that has a number and has never been used as a string is a number, and any other
defined scalar is a string; `undef` is null; `JSON::PP::true`, Perl's own booleans (5.36 and later), `\1` and `\0` are
booleans; a `Math::BigInt` is an integer, exactly; an object with a `TO_JSON` method is what that returns. To validate
the same JSON more than once, parse it once with `...::Document->parse($text)` and call `is_valid_document`.

## Errors

Failures are thrown as objects, which stringify to their message: `JSON::Schema::Corvus::Error` (or
`JSON::Schema::Corvus::XS::Error`) for a bad argument, and these classes under it:

| Class | Meaning |
|---|---|
| `::InvalidJson` | Text that is not JSON, or not UTF-8; `offset` is the byte offset at which it was found to be invalid. |
| `::Compilation` | A schema that does not compile: a reference that does not resolve, or a regular expression that is not valid. |
| `::PatternTooLarge` | A `::Compilation` error for a regular expression over a limit of size. |
| `::Depth` | Evaluation recursed in place beyond `max_depth`. |
| `::PatternMatchLimit` | A match of a regular expression reached `pattern_match_limit`: the instance is neither valid nor not valid. |

An exception that a custom format or the resolver throws goes up through the call that ran it.

## Options

| Option of `compile` | Default | Meaning |
|---|---|---|
| `default_dialect` | `draft2020-12` | The dialect of a schema without `$schema`: `draft4`, `draft6`, `draft7`, `draft2019-09` or `draft2020-12`. |
| `assert_format` | as the vocabularies say | True asserts `format`, false never does; left out, it follows the vocabularies. |
| `assert_format_in_legacy_drafts` | false | Assert `format` in drafts 4 to 7 when it follows the vocabularies. |
| `assert_content` | true | Assert `contentEncoding` and `contentMediaType` in draft 7. |
| `formats` | none | Custom formats by name: a sub of the string that returns whether it is valid. |
| `resolve_document` | none | A sub from an absolute URI to the document, as JSON text or Perl data, or `undef` for one it does not know. |
| `base_uri` | none | The schema's base URI, when it has no `$id`. |
| `entry_point` | the root | A subschema to validate against, such as `#/$defs/item`. |
| `max_depth` | 128 | The deepest the evaluator recurses in place before it fails with `::Depth`. |
| `pattern_match_limit` | 1 | The seconds one match of a regular expression may take; `undef` or 0 turns the limit off. |

A schema and an instance may both come from outside, so a regular expression is bounded in what it costs. A match that
takes longer than the pattern match limit is abandoned, and a schema with a regular expression over a limit of size
does not compile. The XS package matches every pattern with the C library's engine. The pure package runs a pattern
that it proves to match in time proportional to the text on Perl's own engine, written out with ECMA-262's meaning,
and every other pattern on an ECMA-262 engine of its own, in Perl: a deliberately difficult pattern can reach the
limit there where the XS package answers. See [Patterns across the ports](PatternConformance.md).

## Results and annotations

```perl
my $collector = JSON::Schema::Corvus::ResultsCollector->new('detailed');
$validator->evaluate_json('{"id": "3"}', $collector);
for my $row (@{ $collector->results }) {
  print "$row->{document_evaluation_location}: $row->{message}\n" unless $row->{is_match};
}
```

`basic` records the failures without messages, `detailed` adds the messages, and `verbose` records every keyword,
passing ones and annotations included. A row is a hash with `is_match`, `message`, `evaluation_location`,
`schema_evaluation_location` and `document_evaluation_location`, and at every level the root's own result is the last
row. `$collector->annotations` gives the annotations of a verbose evaluation as Perl data, grouped by instance location,
keyword and schema location, and `annotations_json` as JSON text.

## Performance

On the corpora of [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark), with Perl 5.40
and the instances parsed before they are timed, the pure package is 240 to 545 times faster than JSON::Schema::Modern
0.651 (given the schema once, and asked for a flag), and the XS package 7 to 50 times faster than the pure one. All
three give the same verdict on every instance.

## Links

- The pure package: [JSON-Schema-Corvus](https://metacpan.org/dist/JSON-Schema-Corvus), and its
  [source](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-perl/JSON-Schema-Corvus)
- The XS package: [JSON-Schema-Corvus-XS](https://metacpan.org/dist/JSON-Schema-Corvus-XS), and its
  [source](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-perl/JSON-Schema-Corvus-XS)
- The C library it uses: [JSON Schema for C](JsonSchemaForC.md)
- The other languages: see [Other languages](OtherLanguages.md)
