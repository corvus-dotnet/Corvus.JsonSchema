# JSON Schema for Ruby

[corvus_json_schema](https://rubygems.org/gems/corvus_json_schema) is the Corvus JSON Schema evaluator for Ruby: draft
4, 6, 7, 2019-09 and 2020-12. It is a native extension over the [Rust crate](JsonSchemaForRust.md), and gives the same
results and annotations as every other Corvus implementation.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft)
  except `draft4/optional/zeroTerminatedFloats.json`, read in place, as JSON text and through a collector.
- **Fast.** Ruby values are read in place, with nothing converted or copied, and JSON text is validated without
  creating Ruby objects for it.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- **Precompiled.** Native gems for Linux (x86_64 and aarch64, glibc and musl), macOS (x86_64 and arm64) and Windows
  (x64), for Ruby 3.3, 3.4 and 4.0. Elsewhere the source gem builds the extension, which needs a Rust toolchain.

## Install

```sh
gem install corvus_json_schema
```

## Validate

```ruby
require "corvus_json_schema"

validator = CorvusJsonSchema.compile({
  "type" => "object", "required" => ["id"], "properties" => { "id" => { "type" => "integer" } }
})
validator.valid?({ "id" => 3 })      # => true: a Hash, read in place
validator.valid?({ id: "3" })        # => false: Symbol keys are read as their names
validator.valid_json?('{"id": 3}')   # => true: JSON text, parsed in Rust
```

`CorvusJsonSchema.compile` takes the schema as a Hash, an Array, `true` or `false`, or JSON text. A `Validator` is
immutable and can be shared between threads.

## Options

| Option | Default | Meaning |
|---|---|---|
| `default_dialect` | `:draft202012` | The dialect of a schema without `$schema`: `:draft4`, `:draft6`, `:draft7`, `:draft201909` or `:draft202012`. |
| `assert_format` | `nil` | Whether `format` is asserted; `nil` follows the schema's vocabularies. |
| `assert_format_in_legacy_drafts` | `false` | Assert `format` in drafts 4 to 7 when `assert_format` is `nil`. |
| `assert_content` | `true` | Assert `contentEncoding` and `contentMediaType` in draft 7. |
| `formats` | `nil` | A Hash of format name to a callable that returns whether a string is valid. |
| `resolver` | `nil` | A callable from an absolute URI to the document (a Hash or JSON text), or `nil` when it has none. |
| `base_uri` | `nil` | The schema's base URI, when it has no `$id`. |
| `entry_point` | `nil` | A subschema to validate against, such as `"#/$defs/item"`. |
| `max_depth` | `128` | The deepest the evaluator recurses in place before it raises `DepthError`. |
| `pattern_match_limit` | `1.0` | The seconds one match of a regular expression against one string may take before the validation raises `PatternMatchLimitError`. `nil` or `0` turns the limit off. |

Errors are subclasses of `CorvusJsonSchema::Error`: `CompilationError`, `DepthError`, `InvalidJsonError`,
`PatternMatchLimitError` and `PatternTooLargeError`.

A schema and an instance may both come from outside, so a regular expression is bounded in what it costs. A match of
a `pattern` or `patternProperties` regular expression that takes longer than `pattern_match_limit` is abandoned, and
`valid?`, `valid_json?` and `evaluate` raise `PatternMatchLimitError`: the instance is then neither valid nor not
valid. A schema with a regular expression longer than 1,048,576 bytes, nested more than 1,000 groups deep or of more
than about 16,000 terms does not compile, and `compile` raises `PatternTooLargeError`, which is a `CompilationError`.

## Results and annotations

```ruby
collector = CorvusJsonSchema::Collector.new(:detailed)
validator.evaluate({ "id" => "3" }, collector)  # => false
collector.results
# => [..., { is_match: false, message: "The value was expected to be of type 'integer'",
#             evaluation_location: "/properties/id/type", schema_location: "/properties/id/type",
#             instance_location: "/id" }, ...]

verbose = CorvusJsonSchema::Collector.new(:verbose)
validator.evaluate({ "id" => 3 }, verbose)
verbose.annotations  # instance location => keyword => schema location => value
```

`:basic` records the failures without messages, `:detailed` adds the messages, and `:verbose` records every keyword,
passing ones and annotations included. At every level the root's own result is the last row.

## Values

A Hash (String or Symbol keys) is an object, an Array an array, a String or Symbol a string, an Integer or Float a
number, and `true`, `false` and `nil` themselves. Values are read only as the schema examines them. A value the schema
does examine that is not JSON raises `TypeError` (`ArgumentError` for NaN and infinities, `EncodingError` for a String
that is not valid UTF-8).

## Links

- Gem: [RubyGems](https://rubygems.org/gems/corvus_json_schema)
- Source and README: [src-rb/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rb/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
