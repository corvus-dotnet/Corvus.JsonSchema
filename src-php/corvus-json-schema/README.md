# corvus_json_schema

A JSON Schema evaluator for PHP (draft 4, 6, 7, 2019-09 and 2020-12), an extension over the
[corvus-json-schema](https://crates.io/crates/corvus-json-schema) Rust crate, the Rust port of the Corvus.Text.Json V5
standalone evaluator.

- **Conformant**: passes the JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, which the other Corvus evaluators also exclude (a JSON parser reads
  `1.0` as an integer).
- **Fast**: arrays and objects are read in place (nothing is converted or copied), and JSON text is validated without
  creating PHP values for it.
- **Prebuilt**: for PHP 8.2, 8.3, 8.4 and 8.5, thread-safe and not, on Linux (x86_64 and arm64, glibc 2.17 or later
  and musl), macOS (Apple silicon) and Windows (x64).

## Install

With [PIE](https://github.com/php/pie), which downloads the build for the PHP it installs into and enables it:

```sh
pie install corvus-dotnet/corvus-json-schema
```

Or download the archive for your PHP from the
[releases](https://github.com/corvus-dotnet/corvus-json-schema-php/releases), put the library it holds in PHP's
extension directory (`php-config --extension-dir`), and add `extension=corvus_json_schema` to `php.ini`.

## Usage

```php
use Corvus\JsonSchema\{Collector, ResultsLevel, Validator};

$validator = Validator::compile([
    'type' => 'object',
    'required' => ['id'],
    'properties' => ['id' => ['type' => 'integer']],
]);
$validator->isValid(['id' => 3]);           // true: an array, read in place
$validator->isValid((object) ['id' => '3']); // false: a stdClass, read in place
$validator->isValidJson('{"id": 3}');       // true: JSON text, parsed in Rust

$collector = new Collector(ResultsLevel::Detailed);
$validator->evaluate(['id' => '3'], $collector);  // false
$collector->results();
// [..., ['isMatch' => false, 'message' => "The value was expected to be of type 'integer'",
//        'evaluationLocation' => '/properties/id/type', 'schemaLocation' => '/properties/id/type',
//        'instanceLocation' => '/id'], ...]
```

`Validator::compile($schema, $options = [])` takes the schema as an array, an object, a bool, or JSON text. Its
options:

| Option | Default | Meaning |
| --- | --- | --- |
| `defaultDialect` | `Dialect::Draft202012` | The dialect of a schema without `$schema`: a `Corvus\JsonSchema\Dialect` (`Draft4`, `Draft6`, `Draft7`, `Draft201909`, `Draft202012`). |
| `assertFormat` | `null` | Whether `format` is asserted; `null` follows the schema's vocabularies. |
| `assertFormatInLegacyDrafts` | `false` | Assert `format` in drafts 4 to 7 when `assertFormat` is `null`. |
| `assertContent` | `true` | Assert `contentEncoding` and `contentMediaType` in drafts 4 to 7. |
| `formats` | none | An array of format name => a callable that takes the string and returns whether it is valid. |
| `resolver` | none | A callable that takes an absolute URI and returns the document (an array, an object or JSON text), or `null` when it has none. |
| `baseUri` | none | The schema's base URI, when it has no `$id`. |
| `entryPoint` | none | A reference to the subschema to validate against, such as `'#/$defs/item'`. |
| `maxDepth` | `128` | The deepest the evaluator recurses in place before it throws `DepthException`. |

A `Validator` is immutable.

- `isValid($value)` and `isValidJson($text)` return whether the instance is valid, evaluating only as far as the answer
  needs.
- `evaluate($value, $collector)` evaluates every keyword and replaces the collector's results with this evaluation's.
  `new Collector($level)` takes `ResultsLevel::Basic` (the default: the failures, without messages), `Detailed`
  (the failures, with messages) or `Verbose` (every result, and annotations); at every level the root's own result is
  the last row. `results()` returns its rows, `annotations()` the annotations of a verbose evaluation, grouped by
  instance location, keyword and schema location, and `clear()` empties it.

Errors are exceptions extending `Corvus\JsonSchema\JsonSchemaException`: `CompilationException` (an invalid schema, or
a reference that cannot be resolved), `DepthException` and `InvalidJsonException` (invalid JSON text). An exception
thrown by a format validator or a resolver propagates. `Corvus\JsonSchema\crate_version()` returns the version of the
crate the extension was built from.

## Values

A value is read as `json_encode` would write it: a list (`array_is_list`, the empty array included) is an array, any
other array an object (integer keys read as their decimal text), a `stdClass` an object, and strings, integers,
floats, booleans and null themselves. Other objects are converted first: a `JsonSerializable` to what its
`jsonSerialize()` returns, a backed enum to its value, and any other object to its public properties.

`json_decode($json, true)` cannot tell `{}` from `[]`: decode to objects (`json_decode($json)`), or validate the text
with `isValidJson`, when the difference matters.

Values are read only as the schema examines them, so a value the schema never looks at is not checked: with
`['type' => 'object']`, `['a' => fopen('php://memory', 'r')]` is valid. A value the schema does examine that is not
JSON throws `TypeError` (a resource, a pure enum) or `ValueError` (NaN or an infinity, a string that is not valid
UTF-8).

## How it works

The crate compiles the schema once into its node graph and fail-fast plans (see
[src-rs/corvus-json-schema](../../src-rs/corvus-json-schema/README.md)). Its evaluator is generic over an `Instance`
trait (a value shown as one of the six JSON kinds, with arrays and objects read in place), which this extension
implements over PHP's values, reading PHP's hash tables directly: a packed array's slots, or a hash's buckets, by
index; a property by scanning a small table or by PHP's hash lookup in a large one; a string as the bytes PHP holds.
PHP never moves values, and arrays are copied on write, so the evaluator reads them as they are. With format
validators written in PHP (which could change an object while the evaluator reads it) the instance is converted to the
crate's own form first.

`isValidJson` parses into the crate's `JsonDocument`, whose buffers are reused for each thread: in the steady state a
validation of JSON text allocates nothing.

## Building from source

In [Corvus.JsonSchema](https://github.com/corvus-dotnet/Corvus.JsonSchema), with PHP 8.2 or later (and its development
headers, `php-config`), a Rust toolchain and libclang:

```sh
cd src-php/corvus-json-schema
cargo build --release
php -d extension=$PWD/target/release/libcorvus_json_schema.so tests/api.php
php -d extension=$PWD/target/release/libcorvus_json_schema.so tests/suite.php
```

`pwsh package.ps1` builds and packages the extension as PIE expects. On macOS the library is
`libcorvus_json_schema.dylib`; on Windows, building needs a nightly Rust (ext-php-rs uses the vectorcall calling
convention, which is unstable). The tests run the JSON-Schema-Test-Suite from the repository's submodule (or
`JSON_SCHEMA_TEST_SUITE`).

The [corvus-json-schema-php](https://github.com/corvus-dotnet/corvus-json-schema-php) repository, which Packagist reads,
holds a copy of this directory for each release; changes are made here.

## License

Apache-2.0
