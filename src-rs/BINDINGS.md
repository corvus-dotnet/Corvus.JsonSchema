# corvus-json-schema bindings: design

The crate (`src-rs/corvus-json-schema`) is the one evaluator; every other language reaches it one of two ways:

- **through the C library** (`src-rs/corvus-json-schema-capi`, released as `capi-v<version>`): C and C++, and Swift;
- **directly from Rust**, with an `Instance` implementation that reads the language's own values in place: Python
  (`src-py/corvus-json-schema-rs`, PyO3), Ruby and PHP. Through the C library they would have to serialise every value
  to JSON text first, or pay a callback per value.

Every binding offers the same operations, named in the language's idiom: compile a schema (from text, or from the
language's own values), with the options of the crate's `CompileOptions` (dialect, format and content assertion, base
URI, entry point, depth, custom formats, a document resolver); validate a value read in place, or JSON text (the
crate's allocation-free `Validator::validate_json`); and evaluate into a results collector (Basic, Detailed, Verbose)
with annotations. Each runs the JSON-Schema-Test-Suite in its CI, and each is measured against its language's
incumbent in the jsonschema-benchmark harness.

Decisions (2026-10-02): the names are `corvus_json_schema` throughout; the minimum versions Ruby 3.3, PHP 8.2 and Swift
5.9 (the oldest still supported upstream); Ruby publishes to RubyGems and PHP to Packagist (installed with PIE); Swift
lives in a repository of its own. PHP's Packagist package also needs a repository of its own (Packagist reads a
package from a repository's root): its sources stay here, and each release is pushed there.

## Ruby (`src-rb/corvus-json-schema`)

- **Shape:** a gem, `corvus_json_schema` (module `CorvusJsonSchema`), whose native extension is a Rust crate built with
  rb-sys and magnus (`ext/corvus_json_schema`), with a thin Ruby layer (`lib/`).
- **API:** `CorvusJsonSchema.compile(schema, **options)` returns a `Validator` (`schema` a Hash or JSON text);
  `validator.valid?(value)`, `validator.valid_json?(text)`, `validator.evaluate(value, collector)`;
  `CorvusJsonSchema::Collector.new(:basic | :detailed | :verbose)` with `results` and `annotations`. Errors are
  `CorvusJsonSchema::CompilationError`, `DepthError` and `JSON::ParserError`-like `InvalidJsonError`.
- **Values read in place:** `Hash` (String or Symbol keys), `Array`, `String` (UTF-8 or ASCII), `Integer` (Bignums
  beyond 64 bits as doubles), `Float`, `true`, `false`, `nil`. Ruby has no public lazy Hash iterator, so an object's
  pairs are gathered (`rb_hash_foreach`) into a buffer when the evaluator iterates it, and a name is found by scanning
  them (objects are small; the evaluator's plans look up few names).
- **GC safety:** the evaluator holds Ruby values only while it runs, and they stay reachable from the instance passed
  in (on the stack), so the collector keeps them. Ruby's compaction could still move them if a GC ran during an
  evaluation: only Ruby code run from the evaluation can trigger one, which means custom format validators written in
  Ruby, so with those the instance is converted (to a `JsonDocument`) before evaluation instead.
- **Release:** precompiled native gems (x86_64 and aarch64 Linux, glibc and musl; x86_64 and arm64 macOS; x64
  Windows), built with rb-sys's cross-gem tooling, and the source gem; published by trusted publishing on a version
  change, honouring `no_release`, tagged `rb-v<version>`.

## PHP (`src-php/corvus-json-schema`)

- **Shape:** an extension, `corvus_json_schema`, written with ext-php-rs; namespace `Corvus\JsonSchema`.
- **API:** `Validator::compile(array|object|string $schema, array $options = [])`, `$validator->isValid(mixed $value)`,
  `$validator->isValidJson(string $json)`, `$validator->evaluate(mixed $value, Collector $collector)`;
  `new Collector(ResultsLevel::Basic)` with `results()` and `annotations()`. Errors are exceptions
  (`CompilationException`, `DepthException`, `InvalidJsonException`).
- **Values read in place:** PHP arrays (a list, `array_is_list`, is a JSON array, with the empty array an array as
  `json_encode` writes it; any other array is an object, integer keys read as their decimal text), `stdClass` (an
  object), strings, integers, floats, booleans and null. ext-php-rs iterates hash tables lazily, and PHP frees by
  reference counting and never moves values, so nothing is gathered or converted. Other objects (`JsonSerializable`
  and the like) are converted through their JSON form.
- **Release:** a Packagist package (`corvus-dotnet/corvus-json-schema`, type `php-ext`) for PIE, with prebuilt
  extensions for each supported PHP minor version, thread-safe and not, on Linux (x86_64 and arm64, glibc and musl),
  macOS (arm64) and Windows (x64), named as PIE looks for them. Packagist reads a package from a
  repository's root and its versions from that repository's tags, so the package's repository is a separate one,
  `corvus-dotnet/corvus-json-schema-php`, to which the publish workflow pushes this directory (taking the crate from
  crates.io) and whose releases hold the builds; this repository's commit is tagged `php-v<version>`.

## Swift (repository `corvus-dotnet/corvus-json-schema-swift`)

- **Shape:** a SwiftPM package (module `CorvusJsonSchema`) over the C library's Clang module `CCorvusJsonSchema`: on
  Apple platforms a binary target, the XCFramework built from the C library for macOS, iOS and the iOS simulator and
  attached to each `capi-v<version>` release; on Linux a system library target, the C library's release package found
  through its pkg-config file (at the minimum Swift version, 5.9, SwiftPM's binary targets serve libraries only on
  Apple platforms).
- **API:** `let validator = try Validator(schema: String, options: Options = .init())` (or `Data`, or
  `Validator(schemaURI:options:)`); `validator.isValid(json:)` for `String` and `Data` (the main path, JSON text
  validated in place), `validator.isValid(_ document: Document)`, `validator.evaluate(json:collector:)`;
  `Collector(level:)` with `results` and `annotationsJSON()`. Errors are the `JSONSchemaError` enum, mirroring
  `cjs_status`, with the message and byte offset. Classes own their handles and free them in `deinit`; `Validator`
  and `Document` are `Sendable` (the C validator and document are thread-safe).
- **Release:** the C library's publish job builds the XCFramework and attaches it with its SwiftPM checksum; the Swift
  repository's `Package.swift` names that URL and checksum, updated when it moves to a new C library release, and
  the Swift package is versioned and tagged on its own.

## Benchmarks

When the bindings exist, one pull request to the jsonschema-benchmark fork adds `corvus-cpp` (the C++ wrapper),
`corvus-rb`, `corvus-php` and `corvus-swift`, beside Blaze, json_schemer, Opis and the existing Corvus entries, each
built from its latest release as the others are.
