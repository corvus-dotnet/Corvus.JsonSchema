# OpenAPI client generation for the other languages

Status: approved plan, 2026-10-16. Revised after an antagonistic review (findings at the end), then the eight open
decisions and the handling of each improvement opportunity were settled by the user the same day (recorded under
Decisions), with consolidation of the four C# generators pulled into phase 0 at the user's request, and a second
antagonistic review of that form applied (also at the end). Not yet an issue. Companion tables: [.NET parity checklist](OpenApiClientsForOtherLanguages-DotNetParity.md) (95 capabilities,
each with the generator method and the covspec operation that exercises it) and
[language-port facts](OpenApiClientsForOtherLanguages-LanguageFacts.md) (13 ports, their evaluator APIs and type
systems).

## Goal

`corvusjson openapi-client` generates a strongly typed, validating, allocation-disciplined HTTP client for .NET. The
JSON Schema evaluator has since been ported to TypeScript, Rust, Go, Java and Kotlin, Julia, Python, Object Pascal,
PHP, Ruby, R, Swift and C. This plan gives each of those runtimes the same client generator, at the same capability,
from the same CLI, on the same type model, under the same performance discipline as the evaluator ports.

The brief, verbatim, and how each line is met:

1. Mirror the .NET client generator completely. The parity checklist is the definition of done per language.
2. Use the existing CLI with language options. `--language` on `openapi-client`, `openapi-callback-client` and
   `jsonschema`; everything before emission (spec reading, YAML, version detection, filters, lock file, reference
   resolution, model type building) is shared, not reimplemented.
3. Use the existing `TypeDeclaration` infrastructure, emitting idiomatic types. One `ILanguageProvider` per language
   plugs into the language-neutral codegen core exactly as `CSharpLanguageProvider` does.
4. Evaluator-grade performance and allocation. The generated client is specialised per operation at generation time
   (no runtime interpretation of the spec), validation uses each port's own fast path, and every language gets the
   allocation tests and benchmarks its evaluator port already has.
5. No scope creep. Server generation for other languages and new evaluator features beyond the two named
   dependencies are reviewed later, not built here. Consolidating the four C# client generators is in phase 0
   because the user asked for it, and it is held to byte-identical output.

## What main already provides

These are the facts the design rests on. All verified on `main` at `60bab583211`.

- **Each spec-version generator is already two-stage.** `OpenApi20/30/31/32CodeGenerator.Generate` walks the typed
  OpenAPI document into private `OperationInfo` records (`ParameterInfo`, `RequestBodyInfo`, `ContentInfo`,
  `EncodingInfo`, `ResponseInfo`, `HeaderInfo`, `LinkInfo`, `ServerInfo`, `OperationSecurityRequirementSet`), then
  `Emit*` methods write C#. The records are commented "emit-boundary record types" and hold schema pointers,
  serialisation kinds, styles, media types and security sets. They contain no C# type names. Two fields are
  C#-derived identifiers: `OperationInfo.MethodName` and `BinaryPropertyInfo.ParameterName`. (3.2's
  `CustomMethodName` is the custom HTTP method of an additional operation, as written in the document, and is
  neutral.)
- **The records differ only by version features.** 3.0 and 3.1 differ in the `OperationRef` document type alone. 2.0
  adds `ParameterInfo.DegradedCollectionFormat`. 3.2 adds `ParameterInfo.AllowReserved`, `MixedPartInfo` prefix and
  item parts on `RequestBodyInfo`, `Summary`, `Description` and `BinaryParts` on `ResponseInfo`,
  `ContentInfo.ItemSchemaPointer`, and `SecuritySchemeInfo` with available scopes. The 3.2 generator is the feature
  superset (70 emit methods against 55; it alone emits SSE, NDJSON, multipart responses and cookie key-name
  constants). Text responses are emitted from the shared `CodeEmitHelpers`.
- **Model types come from the shared codegen core.** `OpenApiGenerateCommand.GenerateSchemaTypesAsync` builds a
  `JsonSchemaTypeBuilder` over the spec (with the OpenAPI 2.0 and 3.0 dialect vocabularies), collects the schema
  pointers the operations reference, calls `GenerateCodeUsing(CSharpLanguageProvider, ...)`, then walks the
  `TypeDeclaration`s to build the pointer-to-type-name map and the set of pointers that got a context-threaded
  `Source<TContext>` builder.
- **Schema programs are a provider concern, driven by the core.** `JsonSchemaTypeBuilder` checks for
  `ISchemaProgramLanguageProvider` on any provider and calls `SetProgramRootTypes` and `SetSchemaDocuments`
  (UTF-8 when `ISchemaProgramUtf8LanguageProvider`). The C# provider compiles them into `CorvusJsonSchemaProgram.cs`.
- **The evaluator ports split into three validation shapes** (facts table, column 3). Text-native: Rust, Go, Java,
  Julia, Pascal, Swift, C and the Rust-backed Python, Ruby, PHP and R bindings validate UTF-8 JSON text without
  materialising a host value. Value-native: TypeScript and pure Python validate the language's own values and generate
  specialised code (`generateModule`, `generate_module`; one entry point per module). The bindings over the Rust crate
  also read host values in place.
- **The ports' parsed documents are not navigable from outside.** Go's `Document` exports only `String`; Java's
  `JsonDocument` navigation is package-private; Rust exposes `JsonDocumentValue` through the public `Instance` trait
  (property and item iteration). None of them offers a public typed accessor surface a generated model could be a
  view over today.
- **The ports do not yet implement the OpenAPI 3.0 and 2.0 dialect semantics** (`nullable`, `x-nullable`, every
  format under Draft 4 rules) that PR 1024 gave the .NET evaluator. The agreed follow-up is one PR across every port.
- **Test and benchmark assets exist.** `tests/Corvus.Text.Json.OpenApi.CodeGeneration.Tests/TestData/covspec-{2.0,3.0,3.1,3.2}.json`
  cover the capability matrix; `tests/Corvus.Text.Json.OpenApi31.Runtime.Tests` has 289 end-to-end tests over a
  capturing mock transport; `benchmarks/Corvus.Text.Json.OpenApi.Benchmarks` measures the full pipeline against Kiota.

## Dependencies outside this plan

1. **Ports OpenAPI-dialect follow-up.** Until it lands, generated clients for 2.0 and 3.0 specs would validate
   under the wrong rules in every language. 3.1 and 3.2 specs (Draft 2020-12) are unaffected. Until that PR is
   released in a port, the CLI refuses a 2.0 or 3.0 spec for that language with a message naming the dependency,
   so no shipped client validates wrongly; each language's 2.0 and 3.0 fixtures are enabled when the refusal is
   lifted. Nothing else in this plan waits for it.
2. **Model input from the parsed document** (next section) needs a small public navigation surface on the Go and
   Java documents. That is the one piece of evaluator API this plan adds, and it is scoped to accessors the
   evaluators already have internally. Its shape is a value handle, not the index-based internals: a small struct
   of document pointer and node index (Rust's `JsonDocumentValue<'d>` is the model) with kind, property lookup by
   UTF-8 name, item iteration, string bytes and number text, allocating nothing per navigation. Each port's
   allocation tests cover it.

## The failed attempt, as a constraint

The branches `mwadams-multi-language-openapi-clients` and `mwadams-production-openapi-clients` are not a base. Three
of their choices are the shapes this plan forbids: a second spec walker on `System.Text.Json.Nodes` beside the four
that exist; a data-driven `OperationSpec` interpreted per call by a runtime core; and validation on a deep-copied,
double-converted value. Each costs allocation and dispatch the .NET client does not pay. Their useful residue is the
reminder that TypeScript models can be rendered from `TypeDeclaration` (reduction, composition, tuples, required-ness)
without a hand-rolled schema walker, which this plan does through a proper provider instead.

## Architecture

```
OpenAPI 2.0 / 3.0 / 3.1 / 3.2 (JSON or YAML)
   |  existing CLI front end: read, detect version, filter, lock, resolve $ref   (shared, unchanged)
   |
   |-- JsonSchemaTypeBuilder over the referenced schemas                        (shared, unchanged)
   |      GenerateCodeUsing(<Language>Provider)  ->  Models/ + schema embedding  (new provider per language)
   |      CLI walks the TypeDeclarations with the provider -> IModelTypeMap
   |
   |-- OpenApiNNCodeGenerator.PrepareOperations  ->  OpenApiClientModel         (promoted from private records)
   |
   '-- IOpenApiClientEmitter.Emit(model, typeMap)  ->  client files              (C# today; one per language)
            |
            v
      per-language runtime package (transport, wire helpers, bodies, auth hooks)
            |
            v
      the language's existing evaluator (request and response validation)
```

### Seam 1. A public operation model promoted from the generators

Move the emit-boundary record family into `Corvus.Text.Json.OpenApi.CodeGeneration` as public types (namespace
`Corvus.Text.Json.OpenApi.CodeGeneration.Operations`), one definition that is the union of the four versions' fields
(the 3.2 set plus 2.0's `DegradedCollectionFormat`). Each version generator keeps its own `Prepare*` methods (the
typed documents differ per version, and that code is where the 2.0 lowering and the 3.0 dialect handling live) but
returns the shared records, and exposes

```csharp
public OpenApiClientModel PrepareOperations(JsonElement specRoot, OperationScope scope, OperationFilter? filter = null, IOpenApiReferenceResolver? resolver = null);
```

where `OperationScope` is `Paths` or `WebhooksAndCallbacks` (the two walks the C# client and callback-client
generation already use). In step 1 the method is an instance method, so what preparation reads from the
generator's constructor today (`IgnoreEmptyFormUrlEncodedBody`, which `PrepareRequestBody` consumes) stays there;
when preparation and emission split in step 3 it moves to a `PrepareOptions` argument. And `OpenApiClientModel` is
`(SpecVersion, Title, ApiVersion, ServerInfo? RootServer, SecuritySchemeInfo[] SecuritySchemes,
OperationInfo[] Operations)`. `SpecVersion` is the document's OpenAPI version, distinct from the API's own version,
and is what the drift branches below test. `SecuritySchemeInfo` is populated by all four versions (today only 3.2
prepares it; 3.1 reads a scheme-type lookup instead), which is a preparation addition with no C# emission change.

Server emission consumes the same records: `GenerateServer` and `GenerateCallbackServer` call `PrepareOperation`
and read `OperationInfo` exactly as the client path does, including the two identifier fields (14 uses in the 3.1
server path alone). Promotion therefore changes the types the server emitters read, and the identifier-field change
in PR 0b reaches server emission too. The gate covers both.

The promotion itself is pure and is phase 0's first step. The two C#-derived identifier fields come off the records
in PR 0b: `MethodName` is derived by the C# client and server emitters from `OperationId`, `Method`,
`PathTemplate` and `CustomMethodName` exactly as `GetMethodName` derives it today, through one shared helper, and `BinaryPropertyInfo.ParameterName` from `PropertyName` through `SanitizeParameterName`. Other emitters
derive their own names from the same neutral fields.

### Seam 2. One client emitter interface

```csharp
public interface IOpenApiClientEmitter
{
    string LanguageId { get; }
    IReadOnlyList<GeneratedFile> EmitClient(OpenApiClientModel model, IModelTypeMap types, ClientEmitOptions options);
    IReadOnlyList<GeneratedFile> EmitCallbackClient(OpenApiClientModel model, IModelTypeMap types, ClientEmitOptions options);
}
```

`IModelTypeMap` answers: the type reference for a schema pointer, whether the pointer has a builder shape, and the
fallback type for an unmapped pointer (`JsonElement` in C#, the language's dynamic JSON value elsewhere). The CLI
builds it the way `GenerateSchemaTypesAsync` does today, walking the `TypeDeclaration`s, but asking the provider
(each provider exposes `TypeReferenceFor(TypeDeclaration)` and `HasBuilder(TypeDeclaration)` on its own class) instead
of the C# extension methods. The CLI selects the emitter by `--language`.

C# goes through this interface in phase 0 and ends as one emitter. Today the client emission code is duplicated
across the four version assemblies (`Corvus.Text.Json.OpenApi20/30/31/32`, roughly 50 to 70 `Emit*` methods each,
about 11k of the 30k lines in the four generator files), beside the server emission those assemblies also hold. Phase 0 takes it in two moves, each
gated on byte-identical output:

1. **Route.** Each version generator implements `IOpenApiClientEmitter` in place: `EmitClient` is today's `Generate`
   body minus the preparation loop, `EmitCallbackClient` is today's `GenerateCallbackClient` likewise (the 2.0
   generator keeps rejecting that call, as the CLI does today), the constructor-injected `schemaTypeMap` and
   `contextSourceBodyPointers` become the `IModelTypeMap` argument (`ResolveSchemaTypeName` and
   `HasContextThreadedBody` read it), and `rootNamespace` and `clientName` arrive in `ClientEmitOptions`. The CLI's
   `csharp` path calls `PrepareOperations` then the interface. This proves the interface carries everything an
   emitter needs, and it is what makes the drift catalogue possible: a model prepared by one version can be handed
   to another version's emitter.
2. **Consolidate.** A single `CSharpClientEmitter` in the shared `Corvus.Text.Json.OpenApi.CodeGeneration` project
   (beside `CodeEmitHelpers` and `IndentedWriter`, which it already owns) consumes only the promoted records, so the
   reference direction is natural and the version assemblies keep preparation, the document models and server
   emission. The move is done one emit-method family at a time (request struct, response struct, match result,
   links, interface, implementation, callback client), replacing each version's copy with the shared one and
   regenerating after each family. Server emission keeps working throughout; private helpers it shares with client
   emission are moved to `CodeEmitHelpers` rather than duplicated.

One emitter cannot be byte-identical to four if the four already disagree for the same record content, so
consolidation starts with a **drift catalogue**. A spec can only be prepared by its own version's generator, so the
catalogue is built after routing, by cross-feeding: every covspec, callbacks and recipe model is prepared once by
its own version and emitted by all four emitters (a 3.1 model through the 3.2 emitter sees its 3.2-only fields
empty), and the outputs are diffed pairwise. Each difference is classified as feature-driven (present only because
the record carries a 3.2 or 2.0 field, such as `ItemSchemaPointer`, `BinaryParts` or `DegradedCollectionFormat`) or
as drift (the same record content emitted differently, such as the cookie key-name constants only 3.2 emits, the
multipart response accessors only 3.2 emits for a media type 3.0 and 3.1 also allow, or 3.2's response summary
comments). Feature-driven differences fall out of the records. Each drift item is carried through consolidation as an explicit,
commented branch on `OpenApiClientModel.SpecVersion`, so the consolidated emitter is byte-identical to all four on
day one. Each branch is then removed in its own commit that upgrades the older versions to the superset output, with
the regenerated diff reviewed and the recipes and runtime-test `Generated/` trees updated deliberately. That is the
only point in phase 0 where generated C# changes, and it changes by decision, not by accident.

### Seam 3. One language provider per language

Project `src/Corvus.Text.Json.CodeGeneration.<Lang>` containing `<Lang>LanguageProvider : IHierarchicalLanguageProvider,
ISchemaProgramUtf8LanguageProvider` (plain `ILanguageProvider` where the language has no nested types, such as Go;
streaming where the file layout warrants it), its `Options`, its name heuristics
(the shared `INameHeuristic` set plus a casing policy), its code-file builders and keyword handlers. It also reuses
the OpenAPI schema-name heuristic that the C# path already shares.

What "idiomatic" means per language is fixed up front in a type-mapping table (one row per schema shape) in each
provider's README, written and reviewed before the provider, in the style of the evaluator ports' OPTIMIZATIONS.md
checklists. The shapes every provider must cover: object (required and optional properties, additional and pattern
properties), array and tuple, string with format (date-time, uuid, uri, binary and byte content encodings, 64-bit
and big integers as text), integer and number widths, boolean, null, enum and const of any JSON kind, `oneOf`/`anyOf`
as a tagged union with an exhaustive `match`, `allOf` as composition, `if`/`then`/`else`, nullability from the 3.0
dialect, `$ref` to shared types, and the "any JSON" fallback.

Validation on models is the evaluator, never a re-implementation. Each generated type exposes `validate`/`evaluate`
entry points that call the port over the embedded schema. The embedding is the provider's schema-program decision:

| Port shape | Embedding emitted by the provider | At runtime |
|---|---|---|
| TypeScript, pure Python | the schema bundle as a constant plus a lazy holder calling the port's `compile`; and a build script that runs the port's own generator (`npx corvus-json-schema generate`, `generate_module`) once per root type to swap in precompiled modules | compile once per process by default (specialised code in memory); no compile at all after the build script |
| Java | the schema bundle as a resource; `Validator.compile` once in a static holder | bytecode generated once per process |
| Rust, Go, Julia, Pascal, Swift, Rust-backed Python, Ruby, PHP, R | the schema bundle embedded as a UTF-8 constant; compiled once lazily | one compile per process |

The TypeScript and Python module generators are functions of those ports, so the C# CLI cannot run them at
generation time without shelling out to Node or Python. The provider therefore emits what works with no extra
tooling (the bundle and a lazy compile, which in those ports already produces specialised code in memory) and the
script that upgrades a shipped package to precompiled modules. Porting those generators to C# is not in this plan.

### Where response bytes become a model

This is the design fork the first draft glossed over, and it decides whether contract item 3 (one parse per body)
is achievable per language.

| Languages | Port's instance input | Model construction | Passes over the bytes |
|---|---|---|---|
| TypeScript, pure Python | host value | `JSON.parse` / `json.loads` once, validate the value, the value is the model | one |
| Rust-backed Python, Ruby, PHP, R | host value, read in place | host JSON decode once, validate in place, the value is the model | one |
| Rust | `JsonDocument` via the public `Instance` trait | generated model = typed accessors over `JsonDocumentValue` (a view, like the C# model) or a decode from it; validate the document | one |
| Go, Java | `Document` / `JsonDocument`, navigation not public | needs a public accessor surface (kind, property lookup, items, string bytes, number text), then as Rust | one, after dependency 2 |
| Julia, Pascal, Swift | parsed document, navigation not public | same choice as Go and Java, or validate text then decode with the host library | one, or two meanwhile |

Recommendation: models in the compiled languages are views over the port's document, as in C#, with a decode-to-struct
convenience for callers who want plain values. Dependency 2 exports the accessors the evaluators already use
internally; the generated code touches only that public surface. Until a port has it, that language runs validate-text
then host-decode (two passes) and the contract records the exception with a measurement.

Builders mirror the C# `Source<TContext>` idea in each language's own terms: TypeScript object literals typed by the
model, Go struct literals, Rust struct initialisers, Java records, Python dataclasses, Julia keyword constructors,
serialised once into the transport's buffer and validated from those bytes on the text-native ports. No closures
are generated where the language has a value literal.

### Seam 4. The CLI

- `openapi-client`, `openapi-callback-client`: `--language csharp|typescript|go|java|rust|python|julia|pascal|php|ruby|r|swift`
  (default `csharp`). The command's existing options are classified once: shared (`--outputPath`, `--clientName`,
  `--force`, `--spec-url`, `--include-path`, `--exclude-path`, `--filter`, `--specVersion`, `--yaml`,
  `--ignoreEmptyFormUrlEncodedBody`), C#-only (`--rootNamespace`), server-only (`--serverBinaryParts`), and a
  per-language namespace equivalent of `--rootNamespace` (`--package` for Go and Java, `--module` for Python and
  Julia, `--crate` for Rust). A C#-only option with a non-C# language is an error, not ignored. No option is added
  to `openapi-client` beyond `--language` and the namespace equivalents; the model-shaping options that exist only
  on `jsonschema` (`--assertFormat`, `--formatMode`, `--nativeEnums`, `--unions`, `--optionalAsNullable`, and the
  rest) stay there, where the per-language classification applies the same way.
- `jsonschema --language <id>`: the same providers through `GenerationDriverV5`, so model-only generation works for
  every language and the provider is testable without OpenAPI. The option is added to the generator-config schema
  (`src/Corvus.Json.Cli.Core/generator-config.json`), which is where CLI options are defined, and each provider gets
  its own options mapping beside `MapGeneratorConfigToOptions`.
- The lock file gains `language` (schema in `Schemas/corvusjson-openapi-lock.json`, model regenerated, write path
  plumbed). Absent `language` means `csharp`, and a lock written by one language does not report another as up to
  date. The lock file already records `generatorVersion`, and `IsUpToDate` already treats a different generator
  version as stale, so a generator upgrade regenerates without `--force` today; nothing is added for that.
- `openapi-show` is unchanged.

### Seam 5. Per-language runtime package

The analogue of `Corvus.Text.Json.OpenApi` plus `.HttpTransport`, published beside the evaluator (`src-ts/corvus-openapi-client`,
`src-go/corvus-openapi-client`, and so on), depending on the evaluator package. It is written in the host language
in every case; the bindings over the Rust crate get no new FFI surface for it. It holds only what cannot be
specialised at generation time:

- a transport interface over the platform HTTP client, with the request shape being bytes-native (path and query
  written into the language's byte buffer type, headers as a callback or a pre-sized list);
- percent-encoding, header-value parsing and style splitting for the array and object parameter kinds;
- form-urlencoded and multipart (form-data and mixed) writers and readers, SSE and NDJSON readers, text and binary
  bodies, the retry-safe body contract (re-invoke a write callback, rewind a seekable stream, fail a non-seekable one);
- `ValidationMode` (None, Basic, Detailed), the validation and unexpected-response error types, and result
  formatting over the port's results collector;
- authentication hooks: API key (header, query, cookie), HTTP basic and bearer, a token-provider interface for
  OAuth2 and OpenID Connect, cookie sessions;
- a resilience seam: a transport decorator taking a retry policy function, carrying the retry-safe body semantics.

Entra ID (MSAL) and Polly are .NET integration packages. Their mapping is a token-provider hook and a retry seam
plus one documented native example per language (decision 6).

### The generated client, per language

Specialised per operation, like the C# request struct:

- a request type per operation with pre-encoded path segment literals, the exact Accept header value, and
  per-parameter writers chosen at generation time by `ParameterSerializationKind` (no style switch at runtime);
- a response type per operation with one member per declared status and media type, typed headers read lazily, and
  `match` over a tagged union (TypeScript discriminated union, Rust enum, Java sealed interface with a generated
  match method since Java 17 has no pattern-matching switch, Go interface with a generated `Match` function, Python
  `match`, Julia dispatch, Swift enum, Ruby `case/in`, PHP `match`, R `switch`, Pascal `case` over a variant
  record);
- a client per tag plus a default client, an interface where the language has one, server URI construction with
  variables, scope constants, links as follow-on methods with runtime-expression resolution, callback clients;
- validation calls placed exactly where the C# client places them, with Basic as the default and the response
  default None;
- a test-only fixture adapter per operation, emitted on request through a hidden CLI option used only by the
  regeneration scripts (`--emit-fixture-adapter`, not documented), that maps a fixture
  case's JSON inputs onto the typed request and the typed outcome back to the fixture's JSON. A language-neutral
  fixture set cannot otherwise drive a strongly typed client without a hand-written adapter per operation per
  language. The C# emitter emits one too, so the C# fixture runner is built the same way.

## The performance and allocation contract

This is the gate, per language, not a goal. It is stated in the port's own units because the ports already measure
themselves this way.

1. No runtime interpretation of the operation: no dictionary of parameter values, no style dispatch, no media-type
   classification at call time. Everything decidable from the spec is decided in the emitter.
2. Validation uses the port's fastest entry. Text-native ports validate the parsed document once and the model is a
   view over it (or, until dependency 2, validate the text and decode once). Value-native ports validate the value
   once, with no copy.
3. One parse per response body and one serialise per request body, into the buffer the transport sends. No interim
   strings where the language has a byte type. Any language running two passes meanwhile says so in its README with
   the measured cost.
4. Allocation tests in the port's existing harness (Go `testing.AllocsPerRun`, Java and Julia allocation tests, Rust
   allocation counter, Pascal `TestAllocations`, Node `--expose-gc` event counts) assert the per-call allocation of
   the generated client over a mock transport. The budget is not guessed: it is measured on the pilot, decomposed
   (transport, parse, model, validation), and then fixed as a regression ceiling in the test.
5. A mock-transport end-to-end benchmark mirroring `EndToEndBenchmarks` (list, get by id, POST JSON, PUT form,
   response header; None, Basic and Detailed validation) in each `src-<lang>/corvus-openapi-client-bench`.
   Comparison against the language's incumbent generator is reported when cheap, and is not a gate.
6. Generated code uses only the port's public API. Java generated code stays under the HotSpot 8000-bytecode-byte
   method limit; the emitter splits large matchers.

## Parity

The checklist file is the matrix. Grouped, with the phase in which each group lands for a language (every language
goes through all phases; phases are per language, not global):

| Group | Checklist rows | Phase |
|---|---|---|
| Client and interface per tag, request and response types, operation naming, docs, deprecation, cancellation | 1-6, 50, 54-56, 95 | A |
| Path, query, header, cookie parameters in every style, defaults, path-level inheritance, Accept | 7-21 | A |
| JSON bodies, optional bodies, closure-free builders, scalar bodies | 22-25 | A |
| JSON responses, content-type branching, `match`, try-get accessors, typed headers | 33-39 | A |
| Client and response validation, modes, failure surfacing | 46-49 | A |
| Servers, variables, path and operation overrides | 51-53 | A |
| Lock file, spec URL, filters, show, version detection, YAML, `$ref` | 82-94 | A (shared front end) |
| Form-urlencoded with encodings, multipart form-data, binary ordering, octet-stream bodies, text and binary responses, wildcard media | 26-32, 40-42 | B |
| Links and runtime expressions, callback client, webhooks | 57-67 | B |
| Security schemes, scope and key-name constants, auth hooks, per-request override | 68-76 | B |
| Multipart responses, SSE, NDJSON | 43-45 | C |
| Resilience seam and retry-safe bodies, telemetry hook, transport factory | 77-81 | C |

Phase A is the cut that proves the seams. B and C complete parity. A language is not announced until C.

## Language rollout

| Order | Language | Why here | Models idiom | Validation input | Transport |
|---|---|---|---|---|---|
| 1 | Go (1.25) | text-native, so the model-input fork and dependency 2 are exercised first; no sum types, so the `match` design is stress-tested; allocation tests and module publishing exist; fastest build | view structs over the document handle with accessor methods, a decode-to-plain-struct convenience, a generated `Match` per response; the port's value handle as the fallback type | document | `net/http` + `context` |
| 2 | TypeScript (Node 20) | the port's own module generator and lazy compile both give specialised code; largest audience; npm publishing and CI exist; proves the value-native path | `interface` + discriminated unions, `bigint` for wide integers | value, once | `fetch` |
| 3 | Java 17 (Kotlin smoke) | bytecode validators, Maven publishing; sealed interfaces and records are in the baseline, pattern-matching `switch` is not (Java 21), so `match` is a generated method taking one function per case, as in C# | view classes over the document handle, records as the decoded convenience, sealed interfaces for unions | document | `java.net.http` |
| 4 | Rust (1.89) | the crate underlies six other packages; `Instance` already public | view structs over `JsonDocumentValue`, enums for unions, serde structs as the decoded convenience | document | trait over `reqwest`/`ureq` |
| 5 | Python (3.10) | pure (`generate_module`) and Rust-backed (in-place values) share one generated surface | dataclasses + `match` | value | `httpx` sync and async |
| 6 | Julia (1.10) | text-native, type-stability tests | view structs + `Union` dispatch | document or text | `HTTP.jl` |
| 7 | Ruby (3.3), PHP (8.2), R | bindings over the crate; thin generated clients over host values | `Data` + `case/in`; classes, enums, `match`; lists | value, in place | `Net::HTTP`, PSR-18, `httr2` |
| 8 | Swift | package over the C library; enum `match` is native | view structs + enums | document or text | `URLSession` |
| 9 | Object Pascal | source package; variant records | records | document or text | `fphttpclient` |

Baselines are the ports' declared minimums (`pom.xml`, `go.mod`, `pyproject.toml`, the gemspec, `composer.json`,
`package.json`, `Cargo.toml`, `Project.toml`). The generated code and runtime packages target the same minimums;
no language is raised for this work.

The first two languages are the pilot pair: Go for the bytes-native path, TypeScript for the value-native path. The
runtime-package shape (seam 5) and the contract budgets are not frozen until both are green. The first version ships
with languages 1 to 5; languages 6 to 9 are the long tail, worked through steadily in that order afterwards
(decision 7), behind the review gate below. C and C++ are excluded for now; an idiomatic wrapper over the C library
is wanted eventually (decision 2).

## Phases of work

### Phase 0. Seams (.NET only, two PRs, generated C# unchanged except by catalogued decision)

The idempotency gate for every step: full solution builds warning-free; every OpenAPI test project passes; the
`Generated/` trees regenerate byte-identical in all eight runtime test projects (`OpenApi20/30/31/32.Runtime.Tests`
and `OpenApi20/30/31/32.Server.Runtime.Tests`, 658 files today) and in the five client recipes (029, 031, 033
client, 035 callback client, 042), via `regenerate-and-diff.ps1` and the recipe regeneration script. The server
trees are in the gate because server emission reads the same records.

PR 0a, seams:

1. Promote the record family to public `Operations` types; each version generator returns them; add
   `PrepareOperations` with both scopes; prepare `SecuritySchemeInfo` in every version. Gate as above.
2. Add `IOpenApiClientEmitter`, `IModelTypeMap`, `ClientEmitOptions`, and `--language` plumbing in
   `OpenApiGenerateCommand`, `OpenApiCallbackClientCommand` and `GenerateCommand`/`GenerationDriverV5`, with the
   option classification and the lock-file `language` field. With only `csharp` registered, behaviour is
   unchanged.
3. Route C# through the interface in place (seam 2, move 1) for both the client and the callback client. Gate as
   above, now through the new path.
4. A language-neutral conformance fixture set, `tests/OpenApi-Client-Fixtures/`: hand-authored, one or more cases per
   checklist row over the covspec operations (about 100 cases, not a transcription of the 289 tests). Each case
   holds the inputs, the expected request line, headers and body bytes, the canned response, and the expected typed
   outcome, with stated normalisation rules (multipart boundaries, header order, chunking of streamed bodies). A C#
   runner, driving the generated C# fixture adapters over a capturing mock transport, proves the fixtures against
   the existing .NET client before any other language consumes them. This is the "same results everywhere"
   instrument the evaluator ports get from the JSON Schema Test Suite. It is the largest item in PR 0a and is a
   different kind of change from the seams, so it may be split out as PR 0a2 if review size demands.

PR 0b, one C# emitter:

5. The drift catalogue (seam 2), committed as a document beside this one, every difference classified and the
   drift items each given a decision (upgrade the older versions, or keep the version branch with a reason).
6. Consolidation family by family into `CSharpClientEmitter`, drift carried as explicit `SpecVersion` branches, a
   regeneration and the gate after each family. The four version generators lose their client `Emit*` methods and
   keep preparation, the document models and server emission.
7. The C#-derived identifier fields come off the records; the client emitter and the four server emitters derive
   them through one shared helper. Gate as above, server trees included.
8. One commit per drift item removing its branch, with the regenerated diff reviewed and the `Generated/` trees
   updated deliberately. The gate for these commits is the reviewed diff plus the full OpenAPI test suites and the
   fixture runner, since the output is meant to change. Every upgrade is additive to the generated public surface
   (new members on older versions' output); a drift item whose resolution would remove or rename a member is kept
   as a branch instead, because that is a breaking change and this phase ships as a patch (GitVersion: features are
   patch, breaking is minor). If review size demands, steps 5 to 8 split as PR 0b (consolidation, identical output)
   and PR 0c (drift upgrades, changed output).

### Phase 1. The pilot pair, Go then TypeScript, each to full parity

- Go: dependency 2 (public document accessors) as its own small PR on the Go port first, released as a module
  version before the client package exists, since the runtime package depends on the evaluator by version (the
  same sequencing applies to every language whose evaluator changes for this work, including the dependency 1
  release); then the provider
  (type-mapping table first), the client emitter, `src-go/corvus-openapi-client`, fixtures runner, allocation and
  benchmark harness, golden emitter tests in `Corvus.Text.Json.OpenApi.CodeGeneration.Tests`, a `go.yml` job, docs.
- TypeScript: the same unit, with one validator module per root type.
- Exit for each: parity groups A, B and C green; contract items 1 to 6 measured and recorded in the port's README;
  the C# client's fixture results and the language's are the same rows; 2.0 and 3.0 fixtures enabled once
  dependency 1 lands.
- Fixture-runner review (decision on opportunity 5): after the pilot pair, map every one of the 289 bespoke C#
  end-to-end tests to the fixture rows that cover it, and report the gaps. If coverage is equivalent and the runner
  has proved itself on two languages, the bespoke tests are replaced by the C# fixture runner so every language,
  C# included, is tested the same way. Until that review passes, both run.

### Phase 2. Java, Rust, Python

Same work unit each, one language in flight at a time. Java starts with its dependency 2 PR. This completes the first
version.

### Review gate before the long tail

Before Julia, the opportunities the user asked to revisit at this point are reviewed and either scheduled or left:
program-image loading in the ports (2), multi-entry-point validator modules (3), server generation for other
languages (4) and incumbent-generator comparisons (6).

### Phase 3. Julia, Ruby, PHP, R, Swift, Pascal

Same work unit, in that order, steadily. Ruby, PHP and R clients are thin: generated host-value models plus a client
over host-language wire helpers.

### The per-language work unit

Three PRs per language, each with the gates of the pre-commit checklist:

1. **Models.** Type-mapping table and client surface sketch in the provider README, reviewed before code; the
   provider; `jsonschema --language` golden tests over the existing codegen test schemas.
2. **Client.** Emitter with golden tests over covspec-2.0/3.0/3.1/3.2 and callbacks-3.x; the runtime package with
   unit tests for wire helpers; the fixture runner; allocation tests; CI job.
3. **Ship.** Benchmark and recorded contract measurements; `docs/OpenApiFor<Lang>.md` linked from
   `docs/OtherLanguages.md`; an example beside the port; version history; publish workflow.

## Testing and CI

- .NET side: generator golden tests per language in `Corvus.Text.Json.OpenApi.CodeGeneration.Tests`; the promoted
  operation model gets its own tests (`PrepareOperations` over the covspec files for all four versions and both
  scopes).
- Language side: the fixture runner, the port's own test harness, allocation tests, the benchmark, all in the
  existing per-language workflow (`go.yml`, `typescript.yml`, ...), gated on generated output checked in beside the
  runtime package (regenerated by a script in the same style as `regenerate-examples.ps1`).
- The documentation code-sample catalog applies to every new `docs/` file, including these proposals once committed.

## Decisions (taken 2026-10-16)

1. **Model input in the compiled languages.** Views over the port's document; dependency 2 on Go and Java.
2. **C and C++.** Excluded for now. An idiomatic client wrapper over the C library is wanted eventually (residual
   opportunity).
3. **Kotlin.** A Kotlin-smoke test over the Java client.
4. **Example location.** Beside the port.
5. **The June 2026 TypeScript language-provider work.** Not used; the TypeScript provider is written fresh to its
   type-mapping table.
6. **Entra ID and Polly.** Hooks plus a native example per language.
7. **The long tail.** Worked through steadily, in order, after a first version ships with Go, TypeScript, Java, Rust
   and Python.
8. **Pilot order.** Go then TypeScript.

How the improvement opportunities of the first draft were handled:

| Opportunity | Handling |
|---|---|
| 1. Route C# through `IOpenApiClientEmitter`, neutral identifier fields, one C# emitter across the four spec versions | All of it in phase 0: routing in PR 0a (step 3), consolidation and the neutral fields in PR 0b (steps 5 to 8), byte-identical output as the gate throughout, drift catalogued and resolved by explicit decision |
| 2. Program-image loading in the ports | Review gate before the long tail |
| 3. Multi-entry-point validator modules in TypeScript and Python | Review gate before the long tail |
| 4. Server generation for other languages | Review gate before the long tail |
| 5. Replace the bespoke C# end-to-end tests with the fixture runner | A phase-1 exit review: coverage mapping of the 289 tests to fixture rows, then replacement once equivalent, so every language is tested the same way |
| 6. Incumbent-generator comparisons | Review gate before the long tail |
| 7. Lock file records the generator version | Already on main: `OpenApiLockFile.IsUpToDate` compares `generatorVersion` and regenerates on a mismatch. Nothing to do |

## Residual opportunities, not scheduled

- An idiomatic client wrapper over the C library for C and C++ consumers (decision 2).
- Consolidating the four generators' server emission the same way, once a second language wants servers
  (opportunity 4 at the review gate).

## Delegation pattern for implementation

Each work-unit step is a narrow brief for a lower-cost agent: the files to read (never a generator whole; the
promoted records plus one emit method as the pattern), the output files, the gate command, and the contract items
it must measure. Two agents at most in flight, each writing to its own files. Review and the gates stay with the
orchestrator.

## Antagonistic review, 2026-10-16

What the first draft claimed, what the code says, and what changed.

| # | Claim attacked | Finding | Change |
|---|---|---|---|
| 1 | "The only C#-ism before the emit boundary is `MethodName`" | `BinaryPropertyInfo.ParameterName` is also a sanitised C# identifier (3.2's `CustomMethodName` turned out to be the neutral custom HTTP method) | Both fields come off the records in PR 0b, derived by the consolidated C# emitter with the same functions, so output stays identical |
| 2 | "Records are near-identical across versions" | Six field differences (2.0: 1; 3.2: 5), all additive | Shared record = union of fields, listed |
| 3 | "One parse per response, model built from the port's document" | Go and Java documents expose no public navigation; Rust does via `Instance`; the others not checked beyond the docs | New section on the model-input fork; dependency 2; contract items 2 and 3 reworded; decision 1 |
| 4 | Ports validate 2.0 and 3.0 models correctly | They lack the OpenAPI dialect semantics PR 1024 gave .NET | Dependency 1; 2.0/3.0 fixtures gated per language |
| 5 | TypeScript embeds "one specialised validator module" | `generateModule` takes a single entry point | One module per root type; multi-entry moved to opportunity 3 |
| 6 | TypeScript as the pilot proves the seams | It cannot surface finding 3 or the bytes-native runtime shape | Pilot pair, Go first; decision 8 |
| 7 | Fixtures "derived from the 289 tests" | Those are hand-written C# tests; derivation is manual either way | About 100 hand-authored cases per checklist row, normalisation rules stated, proved by a C# runner first |
| 8 | Bindings reuse "the crate's wire helpers exposed through each binding" | That is new FFI surface in three bindings | Wire helpers written in the host language; no FFI change |
| 9 | "Each language is its own PR" | A full language is provider + emitter + runtime + tests + docs; one PR is unreviewable and one CI round per fix | Three PRs per language |
| 10 | `PrepareOperations` covers the client | The callback client walks webhooks and callbacks separately | `OperationScope` parameter |
| 11 | Scope constants emitted from `SecuritySchemeInfo` | Only 3.2 prepares it; 3.1 uses a scheme-type lookup | Prepare it in all versions in phase 0 (preparation only) |
| 12 | Lock-file `language` field is a one-liner | The lock model is schema-generated; absent must read as `csharp` | Stated in seam 4 |
| 13 | Entra ID and Polly "mirrored" | They are .NET integration packages with no language-neutral equivalent | Mapping made explicit as decision 6 |
| 14 | Nine languages scheduled | No demand signal for the long tail; scope risk | First version = languages 1 to 5, long tail steadily after; decision 7 |
| 15 | Allocation "budget stated in the test" | No way to set a budget before measuring | Measured on the pilot, decomposed, then fixed as a ceiling |

## Second antagonistic review, 2026-10-16, after consolidation moved into phase 0

| # | Claim attacked | Finding | Change |
|---|---|---|---|
| 16 | "Generate every spec through all four generators" to build the drift catalogue | A spec is only accepted by its own version's generator; the four cannot be compared on the same input that way | Catalogue built after routing by cross-feeding prepared models between emitters; stated in seam 2 |
| 17 | The gate is "the three runtime test projects and the recipes" | There are eight runtime test projects with `Generated/` trees (four client, four server, 658 files) and five client recipes | Gate lists all of them |
| 18 | Promotion and the identifier-field change touch only client emission | `GenerateServer` and `GenerateCallbackServer` call `PrepareOperation` and read the same records, with 14 uses of the identifier fields in the 3.1 server path | Server emission named in seam 1 and step 7; server trees in the gate; one shared naming helper |
| 19 | `PrepareOperations` needs only the spec, scope, filter and resolver | `ignoreEmptyFormUrlEncodedBody` is read by `PrepareRequestBody`, so it is a preparation option, not an emit option; `rootNamespace` and `clientName` are emit options | `PrepareOptions` added to the signature; `ClientEmitOptions` carries the emit-side values |
| 20 | `OpenApiClientModel` carries `Version` | That is the API's version; the drift branches need the OpenAPI document version | `SpecVersion` added as a separate field |
| 21 | A language-neutral fixture set can drive a typed client | It cannot without a per-operation adapter, and hand-writing one per operation per language is the scope creep the fixtures were meant to avoid | Every emitter emits a test-only fixture adapter on request; the C# runner uses the C# ones |
| 22 | Dependency 2 is "export the accessors the evaluators already use" | Go's and Java's internals are index-based private methods; exporting them as-is is not a usable or safe API | Shape fixed as a zero-allocation value handle modelled on Rust's `JsonDocumentValue`, covered by the ports' allocation tests |
| 23 | `jsonschema --language` is a flag on `GenerateCommand` | CLI options live in the generator-config schema, and the C# options mapping is C#-specific | Option added to `generator-config.json`; per-provider options mapping |
| 24 | Goal item 5 still said consolidation was out of scope | Contradicted phase 0 after the user's decision | Reworded |
| 25 | PR 0a is "seams, no behaviour change" | It also carries about 100 hand-authored fixtures and a runner, a different kind of change | Flagged for a split into PR 0a2 if review size demands |

## Third antagonistic review, 2026-10-16, final pass

| # | Claim attacked | Finding | Change |
|---|---|---|---|
| 26 | TypeScript and Python providers emit "the port's `generateModule` output" at generation time | Those generators are functions of the TypeScript and Python ports; the C# CLI cannot call them without shelling out to Node or Python | Provider emits the bundle plus a lazy `compile` holder (specialised code in memory, once per process) and a build script that runs the port's own CLI to swap in precompiled modules; porting the generators to C# is out of scope |
| 27 | The lock file "gains `generatorVersion`" (opportunity 7) | `OpenApiLockFile.IsUpToDate` already compares the recorded generator version and regenerates on mismatch | Removed from phase 0; only `language` is added; opportunity 7 marked already done |
| 28 | `openapi-client` options classified as shared include `--assertFormat`, `--formatMode`, `--nativeEnums`, `--unions` | Those exist only on `jsonschema`; the client command has 13 options and none of these. Listing them implied adding options, which is scope creep | Classification restated over the real option set; nothing added beyond `--language` and the namespace equivalents |
| 29 | Consolidation covers "about 18k lines" | The client emission regions measure 2028, 2645, 2653 and 3442 lines, about 11k of the 30k in the four files | Number corrected |
| 30 | Drift upgrades are safe to ship inside the consolidation PR | They change the generated public surface; an upgrade that removed a member would be breaking under this repo's versioning | Upgrades must be additive; breaking resolutions stay as branches; PR 0c split if review size demands |
| 31 | Dependency 2 is "a small PR on the Go port first" | The client package depends on the evaluator by released version, so the port must release before the client can build | Release sequencing stated for Go and Java |
| 32 | `--emit-fixture-adapter` as a product option | A test-only concern on the public CLI surface | Hidden option used only by the regeneration scripts |

## Fourth antagonistic review, 2026-10-16, language baselines and consistency

| # | Claim attacked | Finding | Change |
|---|---|---|---|
| 33 | Java `match` over a sealed interface | The Java port's baseline is 17 (`maven.compiler.release`); sealed interfaces and records are in, pattern-matching `switch` is Java 21 | `match` is a generated method taking one function per case, as in C#; baseline stated |
| 34 | Rollout "models idiom" column said structs, records and serde | Decision 1 makes models views over the port's document in Go, Java, Rust, Julia, Swift and Pascal; plain structs are the decoded convenience | Column restated per language; Go's fallback type is the port's value handle, not `json.RawMessage` |
| 35 | Python `match`, Ruby `case/in` and `Data`, PHP `match` and enums are available | Confirmed: Python 3.10, Ruby 3.3, PHP 8.2, Go 1.25, Node 20, Rust 1.89, Julia 1.10 declared minimums | Baselines recorded; no language raised |
| 36 | The first version can ship before dependency 1 | A 2.0 or 3.0 spec through a language without the dialect follow-up yields a client that validates wrongly | The CLI refuses 2.0 and 3.0 for that language until the dependency is released there |
| 37 | Release sequencing stated for Go and Java only | Every runtime package depends on its evaluator by released version, and dependency 1 changes every port | Generalised |
