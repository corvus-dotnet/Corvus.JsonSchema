# corvus_json_schema

A JSON Schema evaluator for Ruby (draft 4, 6, 7, 2019-09 and 2020-12), a native extension over the
[corvus-json-schema](https://crates.io/crates/corvus-json-schema) Rust crate, the Rust port of the Corvus.Text.Json V5
standalone evaluator.

- **Conformant**: passes the JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, which the other Corvus evaluators also exclude (a JSON parser reads
  `1.0` as an integer).
- **Fast**: values are read in place from Ruby's own objects (nothing is converted or copied), and JSON text is
  validated without creating Ruby objects for it.
- **Precompiled**: native gems for Linux (x86_64 and aarch64, glibc and musl), macOS (x86_64 and arm64) and Windows
  (x64), for Ruby 3.3, 3.4 and 4.0. On other platforms the source gem builds the extension, which needs a Rust
  toolchain (1.85 or later) and libclang.

## Install

```sh
gem install corvus_json_schema
```

## Usage

```ruby
require "corvus_json_schema"

validator = CorvusJsonSchema.compile({
  "type" => "object", "required" => ["id"], "properties" => { "id" => { "type" => "integer" } }
})
validator.valid?({ "id" => 3 })           # => true: a Hash, read in place
validator.valid?({ id: "3" })             # => false: Symbol keys are read as their names
validator.valid_json?('{"id": 3}')        # => true: JSON text, parsed in Rust

collector = CorvusJsonSchema::Collector.new(:detailed)
validator.evaluate({ "id" => "3" }, collector)  # => false
collector.results
# => [..., { is_match: false, message: "The value was expected to be of type 'integer'",
#             evaluation_location: "/properties/id/type", schema_location: "/properties/id/type",
#             instance_location: "/id" }, ...]
```

`CorvusJsonSchema.compile(schema, **options)` takes the schema as a Hash, an Array, `true` or `false`, or JSON text.
Its options:

| Option | Default | Meaning |
| --- | --- | --- |
| `default_dialect` | `:draft202012` | The dialect of a schema without `$schema`: `:draft4`, `:draft6`, `:draft7`, `:draft201909` or `:draft202012`. |
| `assert_format` | `nil` | Whether `format` is asserted; `nil` follows the schema's vocabularies. |
| `assert_format_in_legacy_drafts` | `false` | Assert `format` in drafts 4 to 7 when `assert_format` is `nil`. |
| `assert_content` | `true` | Assert `contentEncoding` and `contentMediaType` in drafts 4 to 7. |
| `formats` | `nil` | A Hash of format name to a callable that takes the string and returns whether it is valid. |
| `resolver` | `nil` | A callable that takes an absolute URI and returns the document (a Hash or JSON text), or `nil` when it has none. |
| `base_uri` | `nil` | The schema's base URI, when it has no `$id`. |
| `entry_point` | `nil` | A reference to the subschema to validate against, such as `"#/$defs/item"`. |
| `max_depth` | `128` | The deepest the evaluator recurses in place before it raises `DepthError`. |

A `Validator` is immutable and can be shared between threads.

- `valid?(value)` and `valid_json?(text)` return whether the instance is valid, evaluating only as far as the answer
  needs.
- `evaluate(value, collector)` evaluates every keyword and replaces the collector's results with this evaluation's.
  `Collector.new(level)` takes `:basic` (failures only), `:detailed` (failures, with messages) or `:verbose` (every
  result, and annotations); `results` returns its rows and `annotations` the annotations of a verbose evaluation,
  grouped by instance location, keyword and schema location.

Errors are subclasses of `CorvusJsonSchema::Error`: `CompilationError` (an invalid schema, or a reference that cannot
be resolved), `DepthError` and `InvalidJsonError` (for `valid_json?`). An error raised by a format or resolver callable
propagates.

## Values

A value is read as its JSON kind: a `Hash` (String or Symbol keys) is an object, an `Array` an array, a `String` (UTF-8
or US-ASCII) or `Symbol` a string, an `Integer` or `Float` a number, and `true`, `false` and `nil` themselves. An
Integer beyond 64 bits is compared as the nearest double, as by a JSON parser without arbitrary precision.

Values are read only as the schema examines them, so a value the schema never looks at is not checked: with
`{"type" => "object"}`, `{ "a" => Object.new }` is valid. A value the schema does examine that is none of the kinds
above raises `TypeError` (`ArgumentError` for a NaN or infinite Float, `EncodingError` for a String that is not valid
UTF-8).

## How it works

The crate compiles the schema once into its node graph and fail-fast plans (see
[src-rs/corvus-json-schema](../../src-rs/corvus-json-schema/README.md)). Its evaluator is generic over an `Instance`
trait (a value shown as one of the six JSON kinds, with arrays and objects read in place), which this extension
implements over Ruby values: arrays are read by index, strings as the bytes Ruby holds, and a Hash's pairs are gathered
(with `rb_hash_foreach`) into a buffer reused across evaluations when the evaluator first reads that object.
Evaluation allocates no Ruby objects, so Ruby's garbage collector cannot run, and so cannot move a value, while it
reads them; when custom formats (Ruby code, which can allocate) are in use, the instance is first converted to the
crate's own form instead.

`valid_json?` parses into the crate's `JsonDocument`, whose buffers are reused for each thread: in the steady state a
validation of JSON text allocates nothing.

## Building from source

In this repository, with Ruby 3.3 or later, a Rust toolchain and libclang:

```sh
bundle install
bundle exec rake compile test
```

The Rakefile copies the crate from `src-rs/corvus-json-schema` into `ext/corvus_json_schema/vendor` whenever it loads,
so the source gem carries the crate's sources and builds on its own. The tests run the JSON-Schema-Test-Suite from the
repository's submodule (or `JSON_SCHEMA_TEST_SUITE`).

## License

Apache-2.0
