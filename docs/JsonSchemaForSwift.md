# JSON Schema for Swift

[CorvusJsonSchema](https://github.com/corvus-dotnet/corvus-json-schema-swift) is the Corvus JSON Schema evaluator for
Swift: draft 4, 6, 7, 2019-09 and 2020-12. It is a Swift API over the [C library](JsonSchemaForC.md), and gives the
same results and annotations as every other Corvus implementation.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, as JSON text, as parsed documents and through a collector.
- **Fast.** JSON text is parsed into per-thread buffers and validated in place; in the steady state a validation
  allocates nothing.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- **Platforms.** macOS 10.15 and iOS 13 or later (the C library comes as an XCFramework), and Linux (with the C library
  installed). Swift 5.9 or later.

## Install

Add the package to your `Package.swift`:

```swift
dependencies: [
    .package(url: "https://github.com/corvus-dotnet/corvus-json-schema-swift", from: "0.1.0"),
],
targets: [
    .target(name: "MyApp", dependencies: [.product(name: "CorvusJsonSchema", package: "corvus-json-schema-swift")]),
]
```

On Apple platforms that is all: SwiftPM downloads the C library's XCFramework. On Linux, install the
[C library's package](JsonSchemaForC.md#install) for your platform first, and make its pkg-config file and shared
library findable (see the package's
[README](https://github.com/corvus-dotnet/corvus-json-schema-swift#install)).

## Validate

```swift
import CorvusJsonSchema

let validator = try Validator(schema: #"""
    {"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}}
    """#)
try validator.isValid(json: #"{"id": 3}"#)    // true
try validator.isValid(json: #"{"id": "3"}"#)  // false
```

`Validator(schema:options:)` compiles a schema from its JSON text (a `String` or UTF-8 `Data`). A `Validator` is
immutable and `Sendable`. To validate the same JSON more than once, parse it into a `Document` and call
`isValid(_:)`.

## Options

| Option | Default | Meaning |
|---|---|---|
| `defaultDialect` | `.draft202012` | The dialect of a schema without `$schema`. |
| `assertFormat` | `nil` | Whether `format` is asserted; `nil` follows the schema's vocabularies. |
| `assertFormatInLegacyDrafts` | `false` | Assert `format` in drafts 4 to 7 when `assertFormat` is `nil`. |
| `assertContent` | `true` | Assert `contentEncoding` and `contentMediaType` in draft 7. |
| `baseURI` | `nil` | The schema's base URI, when it has no `$id`. |
| `entryPoint` | `nil` | A subschema to validate against, such as `"#/$defs/item"`. |
| `maxDepth` | `128` | The deepest the evaluator recurses in place before it throws `depthExceeded`. |
| `formats` | `[:]` | Custom formats: name to a closure that returns whether the string is valid. |
| `resolver` | `nil` | A closure from an absolute URI to the document's JSON text, or `nil` when it has none. |

Errors are `JSONSchemaError`: `invalidJSON` and `invalidUTF8` (with the byte offset), `compilationFailed`,
`depthExceeded`, `invalidArgument` and `internalError`. An error a resolver throws is thrown by the compilation.

## Results and annotations

```swift
let collector = Collector(level: .detailed)
try validator.evaluate(json: #"{"id": "3"}"#, collector: collector)  // false
for result in collector.results where !result.isMatch {
    print(result.instanceLocation, result.message)
}

let verbose = Collector(level: .verbose)
try validator.evaluate(json: #"{"id": 3}"#, collector: verbose)
let annotations = try verbose.annotationsJSON()  // instance location -> keyword -> schema location -> value
```

`.basic` records the failures without messages, `.detailed` adds the messages, and `.verbose` records every keyword,
passing ones and annotations included. At every level the root's own result is the last row.

## Links

- Package: [corvus-json-schema-swift](https://github.com/corvus-dotnet/corvus-json-schema-swift)
- The C library it uses: [JSON Schema for C](JsonSchemaForC.md)
- The other languages: see [Other languages](OtherLanguages.md)
