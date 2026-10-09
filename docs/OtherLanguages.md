# JSON Schema in other languages

The Corvus JSON Schema evaluator is not only for .NET. The same evaluator, with the same behaviour, is available for
JavaScript and TypeScript, Rust, Java and Kotlin, Go, Julia, Python, C, C++, Swift, Ruby, PHP and R.

| Language | Package | Install |
|---|---|---|
| [.NET (C#)](Validator.md) | [Corvus.Text.Json.Validator](https://www.nuget.org/packages/Corvus.Text.Json.Validator) (see also the [runtime evaluator](RuntimeEvaluator.md)) | `dotnet add package Corvus.Text.Json.Validator` |
| [JavaScript and TypeScript](JsonSchemaForJavascript.md) | [@corvus-dotnet/json-schema](https://www.npmjs.com/package/@corvus-dotnet/json-schema) | `npm install @corvus-dotnet/json-schema` |
| [Rust](JsonSchemaForRust.md) | [corvus-json-schema](https://crates.io/crates/corvus-json-schema) | `cargo add corvus-json-schema` |
| [Java and Kotlin](JsonSchemaForJava.md) | [io.github.corvus-dotnet:corvus-json-schema](https://central.sonatype.com/artifact/io.github.corvus-dotnet/corvus-json-schema) | Maven or Gradle |
| [Go](JsonSchemaForGo.md) | [github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema](https://pkg.go.dev/github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema) | `go get github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema` |
| [Julia](JsonSchemaForJulia.md) | [CorvusJsonSchema](https://github.com/JuliaRegistries/General/tree/master/C/CorvusJsonSchema) | `pkg> add CorvusJsonSchema` |
| [Python](JsonSchemaForPython.md) | [corvus-json-schema](https://pypi.org/project/corvus-json-schema/), [corvus-json-schema-rs](https://pypi.org/project/corvus-json-schema-rs/) | `pip install corvus-json-schema-rs` |
| [C](JsonSchemaForC.md) | The corvus-json-schema C library | [Release packages](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=capi-v) |
| [C++](JsonSchemaForCpp.md) | The C library's header-only C++17 wrapper | [Release packages](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=capi-v) |
| [Swift](JsonSchemaForSwift.md) | [CorvusJsonSchema](https://github.com/corvus-dotnet/corvus-json-schema-swift) | Swift Package Manager |
| [Ruby](JsonSchemaForRuby.md) | [corvus_json_schema](https://rubygems.org/gems/corvus_json_schema) | `gem install corvus_json_schema` |
| [PHP](JsonSchemaForPhp.md) | [corvus-dotnet/corvus-json-schema](https://packagist.org/packages/corvus-dotnet/corvus-json-schema) | `pie install corvus-dotnet/corvus-json-schema` |
| [R](JsonSchemaForR.md) | [corvusjsonschema](https://corvus-dotnet.r-universe.dev/corvusjsonschema) | `install.packages("corvusjsonschema", repos = "https://corvus-dotnet.r-universe.dev")` |

## What they share

- **The same evaluator.** Each is a port of, or built on a port of, the .NET runtime evaluator. The Rust crate is the
  engine behind the Python (Rust-backed), Ruby, PHP and R packages and the C library, and the C library is the engine
  behind the C++ wrapper and the Swift package. The JavaScript and pure-Python packages compile schemas into their own
  languages' code, and the Java library compiles them into JVM bytecode. The Go module is a port of the Rust crate's
  plans to Go, with no native code, and the Julia package is a port of the Go module to Julia.
- **The same results.** Every implementation evaluates drafts 4, 6, 7, 2019-09 and 2020-12, passes the
  JSON-Schema-Test-Suite, and reports results at the Basic, Detailed and Verbose levels, with the same rows (evaluation
  path, schema location, instance location and message, in the same order). Annotations come grouped the same way
  everywhere: instance location, then keyword, then schema location, then value (in R, as the columns of a data
  frame). Results from any of them can be consumed the same way.
- **The same options.** The default dialect, format and content assertion, custom formats, a document resolver for
  remote references, a base URI, an entry point and a recursion limit, each named in its language's idiom.
- **Fast validation.** Each validates without collecting anything unless asked; results collection is a separate path
  that costs nothing when it is not used. The native implementations read their languages' own values in place, and
  validate JSON text without creating values for it.
