# JSON Schema for PHP

[corvus-dotnet/corvus-json-schema](https://packagist.org/packages/corvus-dotnet/corvus-json-schema) is the Corvus JSON
Schema evaluator for PHP: draft 4, 6, 7, 2019-09 and 2020-12. It is a PHP extension over the
[Rust crate](JsonSchemaForRust.md), installed with [PIE](https://github.com/php/pie), and gives the same results and
annotations as every other Corvus implementation.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft)
  except `draft4/optional/zeroTerminatedFloats.json`, read in place, as JSON text and through a collector.
- **Fast.** PHP arrays and objects are read in place, with nothing converted or copied, and JSON text is validated
  without creating PHP values for it.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- **Prebuilt.** For PHP 8.2, 8.3, 8.4 and 8.5, thread-safe and not, on Linux (x86_64 and arm64, glibc 2.17 or later and
  musl), macOS (Apple silicon) and Windows (x64).

## Install

```sh
pie install corvus-dotnet/corvus-json-schema
```

PIE downloads the build for the PHP it installs into, and enables it.

## Validate

```php
use Corvus\JsonSchema\Validator;

$validator = Validator::compile([
    'type' => 'object',
    'required' => ['id'],
    'properties' => ['id' => ['type' => 'integer']],
]);
$validator->isValid(['id' => 3]);            // true: an array, read in place
$validator->isValid((object) ['id' => '3']); // false: a stdClass, read in place
$validator->isValidJson('{"id": 3}');        // true: JSON text, parsed in Rust
```

`Validator::compile` takes the schema as an array, an object, a bool, or JSON text.

## Options

`Validator::compile($schema, $options)` takes an array of options:

| Option | Default | Meaning |
|---|---|---|
| `defaultDialect` | `Dialect::Draft202012` | The dialect of a schema without `$schema`: a `Corvus\JsonSchema\Dialect`. |
| `assertFormat` | `null` | Whether `format` is asserted; `null` follows the schema's vocabularies. |
| `assertFormatInLegacyDrafts` | `false` | Assert `format` in drafts 4 to 7 when `assertFormat` is `null`. |
| `assertContent` | `true` | Assert `contentEncoding` and `contentMediaType` in draft 7. |
| `formats` | none | An array of format name => a callable that returns whether a string is valid. |
| `resolver` | none | A callable from an absolute URI to the document (an array, an object or JSON text), or `null`. |
| `baseUri` | none | The schema's base URI, when it has no `$id`. |
| `entryPoint` | none | A subschema to validate against, such as `'#/$defs/item'`. |
| `maxDepth` | `128` | The deepest the evaluator recurses in place before it throws `DepthException`. |

Errors are exceptions extending `Corvus\JsonSchema\JsonSchemaException`: `CompilationException`, `DepthException` and
`InvalidJsonException`.

## Results and annotations

```php
use Corvus\JsonSchema\{Collector, ResultsLevel};

$collector = new Collector(ResultsLevel::Detailed);
$validator->evaluate(['id' => '3'], $collector); // false
$collector->results();
// [..., ['isMatch' => false, 'message' => "The value was expected to be of type 'integer'",
//        'evaluationLocation' => '/properties/id/type', 'schemaLocation' => '/properties/id/type',
//        'instanceLocation' => '/id'], ...]

$verbose = new Collector(ResultsLevel::Verbose);
$validator->evaluate(['id' => 3], $verbose);
$verbose->annotations(); // instance location => keyword => schema location => value
```

`Basic` records the failures without messages, `Detailed` adds the messages, and `Verbose` records every keyword,
passing ones and annotations included. At every level the root's own result is the last row.

## Values

A value is read as `json_encode` would write it: a list (`array_is_list`, the empty array included) is an array, any
other array an object, a `stdClass` an object, and strings, integers, floats, booleans and null themselves. A
`JsonSerializable` is converted to what its `jsonSerialize()` returns, and a backed enum to its value.
`json_decode($json, true)` cannot tell `{}` from `[]`: decode to objects, or validate the text with `isValidJson`, when
the difference matters.

## Links

- Package: [Packagist](https://packagist.org/packages/corvus-dotnet/corvus-json-schema), releases on
  [corvus-json-schema-php](https://github.com/corvus-dotnet/corvus-json-schema-php/releases)
- Source and README: [src-php/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-php/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
