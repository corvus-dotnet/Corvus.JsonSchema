# Version History

## V5.2.3

V5.2.3 fixes the `HttpClient`-backed OpenAPI transport so that a base URL carrying a path prefix — for example an API-gateway route — is preserved in every request URI.

### Bug fixes

- **`HttpClientTransport` now preserves a base URL's path prefix** — Generated operation paths always begin with `/`, and the transport previously sent them as relative URIs for `HttpClient` to resolve against `HttpClient.BaseAddress`. Under RFC 3986 §5.3, an absolute-path reference such as `/transactions` **replaces** the base URI's entire path, so any deployment whose base URL carries a path prefix — the Azure API Management pattern, `https://apim.example/inventory/` — silently lost the prefix: requests landed on `https://apim.example/transactions` instead of `https://apim.example/inventory/transactions`, and because generated paths always start with `/` there was no `BaseAddress` spelling that avoided it. The transport now composes the final absolute URI itself — the base address up to and including its path, with any trailing `/` trimmed, followed by the resolved operation path and query — so the prefix is preserved; this is the same composition NSwag- and Kiota-generated clients use. For a base address with no path segment the composed URI is byte-identical to the previous resolution, and a client with no `BaseAddress` at all still fails with `HttpClient`'s usual invalid-request-URI error, so no other behavior changes. This is a runtime fix in `Corvus.Text.Json.OpenApi.HttpTransport`; no regeneration of generated clients is required.

## V5.2.2

V5.2.2 lets the property-parameter `Build(...)` factory be used directly as an array element (or object-property value) inside a mutable builder callback.

### Bug fixes

- **The property-parameter `Build(...)` factory can now be passed straight to a builder's `AddItem`/`InsertItem`/`SetProperty`/`Set…` methods** — Generated mutable models expose a `T.Build(field: value, …)` factory that captures the `Create(...)` arguments into a lazy `T.Source`. Previously that `Source` could only be handed to a *direct* consumer (a `CreateBuilder(...)` call or a generated client/result factory); using it as an **array element** inside an array-builder callback — `b.AddItem(Item.Build(id: …, name: …))` — failed to compile with `CS8347`/`CS8350`/`CS8156`. The C# ref-safety analysis assumed the `Build` result (a `ref struct` constructed from `in` parameters) might escape into the wider-scoped builder, even though `AddItem`/`InsertItem` materialize it synchronously and never retain it. The generated property-parameter `Build(...)` factory parameters, the capturing `Source(...)` constructor parameters, and the array `AddItem`/`InsertItem` and object `Set…`/`SetProperty` consumers are now emitted as `scoped in`. This is both truthful (each copies or materializes its argument synchronously) and a **backward-compatible** relaxation — callers may pass narrower-scoped values, and nothing that compiled before stops compiling — so the compact factory form now works uniformly when building arrays of objects. The fix is in the shared V5 code-generation core, so it applies to models emitted by the source generator, the `corvusjson` CLI, and the OpenAPI/AsyncAPI generators alike; regenerate your models to pick it up. The delegate-form `Build(static (ref Builder b) => …)` was unaffected (it wraps a heap closure, not parameter refs).

## V5.2.1

V5.2.1 fixes several validation defects — a runtime crash in evaluation tracking, a property-dispatch hash collision, detailed-results faults and corruption on complex schemas, an `unevaluatedProperties` over-rejection through `dependentSchemas`, and a struct-layout cycle in generated recursive discriminated unions — and lets you disable `format` assertion globally to produce annotation-only output.

### Bug fixes

- **Validating an object or array with 232–255 evaluation-tracked properties or items no longer throws `ArgumentOutOfRangeException`** — The internal evaluation-tracking context records "evaluated" property/item bits in an inline 8-`int` (256-bit) buffer and flagged whether those bits had spilled to a larger rented buffer using a bit of that buffer's last `int`. The flag was `0b1000_0000` — bit 7 of the last int, i.e. the data bit for **index 231** — so marking property or item 231 as evaluated (which happens for any object or array with 232 or more tracked entries) corrupted the flag, and the buffer accessor then mis-read it as the rented layout and threw. The flag now uses the most-significant bit (index 255), which `MaxComplexValueCount` already keeps out of inline data. This is a runtime fix in `Corvus.Text.Json` shared by every generated validator, the standalone evaluator, and the dynamic validator; no regeneration is required.

- **Recursive discriminated unions no longer produce a `CS0523` ("cycle in the struct layout") compile error** — The discriminated-union wiring projects each constituent's own `Source` builder through the union's `Source` by value. For a union with a constituent that (transitively) embeds the union's own `Source`, that projection closed a value-type containment cycle, which the C# compiler rejects. The generator's cycle analysis now models the union→constituent projection edge and suppresses the by-value projection for a cyclic constituent (which keeps its builder/`JsonElement` path), so the generated types compile. Regenerate generated code to pick up the fix.

- **Property dispatch no longer mis-resolves a property whose name is a prefix of, and hash-collides with, another declared property** — The internal property-name hash (used to look up the matcher/value for a property in objects with many declared members) packs a key's first seven bytes verbatim and folds its length plus two more bytes into the top byte. For keys of eight or more bytes that top byte could come out as `0` — the value reserved as the marker for a short, fully-encoded key — so a long key whose `(length + key[7] + last byte)` is a multiple of 256 collided with a short key sharing its first seven bytes (e.g. `NewLine` versus `NewLinesForBracesInMethods`, whose top byte is `(26 + 's' + 's') % 256 = 0`). The short-key lookup fast-path trusts that marker and skips the full key comparison, so it resolved the short key to the colliding sibling's entry — validating the value against the wrong subschema and, in the document property map, returning the wrong property's value. The hash now maps that top byte into `1..255`, keeping the length in the hash while reserving `0` for short keys. The same algorithm was duplicated in five places (property and enum lookups, the document property map, and the JSON Path planner); all are fixed. This is a runtime fix; no regeneration is required.

- **Detailed (verbose) validation results no longer fault on schema locations that contain URI-unsafe characters** — A schema evaluation location — for example the subschema under a `patternProperties` key whose regex is `^x-` — is a JSON Pointer (RFC 6901), in which a reference token may legally contain characters that are unsafe in a URI fragment (such as `^`). The path-copy routine used when assembling detailed results asserted the location was a canonical URI, so `#/patternProperties/^x-` faulted. Schema locations are now kept as raw JSON Pointers throughout — percent-encoding is reserved for the point at which a location is actually rendered as a URI — and the path-copy routine no longer over-validates them. Regenerate generated code to pick up the unencoded locations.

- **Detailed validation results no longer corrupt when validating deeply-branching schemas** — When the results collector discarded a child evaluation context that had produced no results (for example a matching `if` condition, which contributes no output in detailed mode), it rolled its result buffer and committed-result stack back to `(0, 0)` instead of to the position where the context began, clobbering earlier committed results; enumerating the detailed results then read a corrupted length header and threw an out-of-range index. This surfaced validating the OpenAPI 3.1 metaschema, whose nested `oneOf`/`if`/`dependentSchemas` over a recursive `$dynamicRef` discard many such contexts. The collector now rolls back to the start-of-context marker it records when the context begins, whose buffer position and committed-result count are mutually consistent. This is a runtime fix; no regeneration is required.

- **A property evaluated only through `dependentSchemas` is now credited for `unevaluatedProperties` regardless of object property order** — `dependentSchemas` is an applicator over the whole object, so a dependent subschema can evaluate — and so credit for `unevaluatedProperties` — any property, including one positioned before the property that triggers the dependency. The object validator emitted the dependent subschema lazily, inside the trigger property's named-property validator (run at that property's position in the single forward property loop), while `unevaluatedProperties` is checked inline in the same loop, so a property positioned before its trigger failed its `unevaluatedProperties` check before the dependent subschema credited it. The most common victim is an OpenAPI 3.1 parameter's `example` — reachable only through `dependentSchemas.schema`, and placed before `schema` by real specifications — which made the OpenAPI 3.1 metaschema over-reject valid documents. `dependentSchemas` is now evaluated as a whole-object pre-pass before the property loop (detecting each dependency directly), so its crediting always precedes the inline `unevaluatedProperties` checks; the extra pass exists only when `dependentSchemas` are present. Regenerate generated code to pick up the fix.

### New features

- **`format` assertion can be disabled globally to generate annotation-only output** — Corvus.Text.Json asserts `format` by default on every draft. `--assertFormat false` — which previously had no effect, because the CLI option was a value-less flag stuck on its `true` default — now binds a value and disables assertion for drafts where `format` is an annotation by vocabulary (e.g. 2020-12). For drafts whose vocabulary asserts `format` (draft-04/06/07), the new global form of `--formatMode` — a bare `--formatMode disable` (equivalently `--formatMode *=disable`) — disables assertion for **every** format on **every** draft, so `format` is recorded as an annotation but never fails validation. Per-format overrides (e.g. `--formatMode date-time=disable`) continue to take precedence over both the global default and the vocabulary.

## V5.2.0

V5.2.0 makes generated JSON Schema type names deterministic across operating systems, fixing a rare case where the same schema produced a different generated type name on Windows than on Linux or macOS.

### Breaking changes

- **Generated type names no longer depend on the host operating system** — In a **rare** case, the documentation-based type-name heuristic could derive a different name for the same anonymous (inline) subschema depending on which OS the generator ran on. When such a subschema had a `description` and no `title`, the heuristic only named the type from that description when its length was under a 64-character cap — but the length it measured included a trailing line break assembled with `Environment.NewLine`, which is `"\r\n"` on Windows and `"\n"` on Linux and macOS. A `description` whose length landed exactly on that boundary (or one carrying trailing whitespace) therefore passed the cap on Linux/macOS — yielding a name derived from the description — yet failed it on Windows by a single character, where the generator fell back to the next heuristic (typically the required-property name, e.g. `TheIdentifierOfTheAssociatedRequiredDocument` on Linux/macOS versus `RequiredDocumentId` on Windows). The assembled documentation is now joined with a fixed `'\n'` separator and the length is measured after trimming, so the heuristic reaches the same decision on every platform. This affects only the **narrow** set of schemas whose documentation-derived name sat exactly on the boundary; for those, regenerating may change a generated type name (and any hand-written code that referenced it). Because the fix is in the shared code-generation core, it applies to the V4 and V5 engines, the `corvusjson` CLI, and the source generators. See [#825](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/825).

## V5.1.19

V5.1.19 adds a KYAML output mode to the JSON→YAML writer.

### New features

- **JSON→YAML conversion can now emit [KYAML](https://github.com/kubernetes/enhancements/tree/master/keps/sig-cli/5295-kyaml)** — KYAML (Kubernetes KEP-5295) is a strict subset of YAML 1.2 designed to be unambiguous: every mapping uses explicit `{ }` braces and every sequence explicit `[ ]` brackets, laid out across indented lines with a trailing comma after each element; string values (and ambiguous keys) are always double-quoted while numbers, booleans, and `null` are written bare. This eliminates the "Norway problem" and indentation-sensitivity pitfalls, and because KYAML is valid YAML 1.2 the output round-trips through any conforming parser (including this library's reader, which already accepts it with no configuration). Enable it with the new `YamlWriterOptions.Kyaml` preset (or `YamlWriterFormat.Kyaml`) on any `ConvertToYaml`/`ConvertToYamlString` overload or directly on `Utf8YamlWriter`; the default output remains canonical block-style YAML. The feature lives entirely in `Utf8YamlWriter`, so both the `Corvus.Text.Json.Yaml` and `Corvus.Yaml.SystemTextJson` packages gain it. See [#823](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/823).

## V5.1.18

V5.1.18 fixes a numeric validation bug where a zero-valued bound mis-validated `number` values whose magnitude was just below `0.1`.

### Bug fixes

- **A zero-valued numeric bound (`minimum`/`exclusiveMinimum`/`maximum`/`exclusiveMaximum` = `0`) no longer mis-validates `number` values in the open interval `(0, 0.1)`** — The shared numeric comparator `JsonElementHelpers.CompareNormalizedJsonNumbers` orders two normalized numbers by their *effective length* (significand length + exponent), the position of the most significant digit. Zero normalizes to an **empty significand** with exponent `0`, giving it effective length `0` — the same band as values in `[0.1, 1)`. As a result any value whose absolute magnitude was less than `0.1` (effective length `< 0`, e.g. `0.05`, `0.083`) was ordered as if it were *smaller* than zero: `minimum: 0` and `exclusiveMinimum: 0` wrongly **rejected** such values, while `maximum: 0` and `exclusiveMaximum: 0` wrongly **accepted** them. Values of `0`, values `>= 0.1`, negative values (handled by the sign comparison), and the same keywords with non-zero bounds were all unaffected, as were `integer`-typed schemas (only integer magnitudes reach the comparator). The comparator now detects an empty significand and orders zero explicitly — less than every positive value, greater than every negative value — before the effective-length comparison. This is a runtime fix in the shared comparator used by generated models, the standalone evaluator, the dynamic `Validator`, and JsonLogic; no regeneration of generated code is required. See [#819](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/819).

## V5.1.17

V5.1.17 fixes an RFC 7396 JSON Merge Patch bug where merging a nested object corrupted the parent element handle.

### Bug fixes

- **`ApplyMergePatch` no longer leaves the parent element stale when merging a nested object** — When a merge patch recursed into a nested object (`JsonMergePatchExtensions.ApplyMergePatch`), the recursion mutated the document but the parent `JsonElement.Mutable` it was iterating kept its now-stale cached document version. Processing any further property of that same (non-root) parent then failed its staleness check, and a frozen patch document copied into the merge could appear disposed on a subsequent read — surfacing as `InvalidOperationException` ("Operation is not valid due to the current state of the object") during the merge or `ObjectDisposedException: 'JsonDocument'` when the patch was read afterwards. Because a recursive merge only mutates the child's own subtree, the parent element's start index is still valid; the merge now re-mints the parent handle with the current version after each nested merge so subsequent siblings and reads succeed. Simple single-property-per-level merges were unaffected, which is why the existing RFC 7396 suite did not catch it. See [#820](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/820).

### New features

- **`JsonMarshal.RefreshUnsafe<T>(in T)`** — Returns a fresh handle to a mutable JSON element whose cached document version is brought up to date with its parent document's current version, without re-validating the element's position. This is a deliberately **dangerous** marshalling helper: it must only be called when the caller knows the element's start index is still valid — i.e. the document has been mutated only *within that element's own subtree* (descendant nodes). It backs the RFC 7396 merge-patch fix above, where a recursive merge into a child object leaves the (structurally still valid) parent element version-stale. See [#820](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/820).

## V5.1.16

V5.1.16 fixes schema reference resolution for `$ref`s expressed as `file://` URIs.

### Bug fixes

- **Schema references expressed as `file://` URIs now resolve correctly** — A `$ref` (or top-level schema reference) given as an absolute `file://` URI — for example `file:///C:/schemas/foo.json` or `file:///home/me/schemas/foo.json` — failed to resolve during code generation and runtime validation. `SchemaReferenceNormalization.TryNormalizeSchemaReference` passed the raw URI string to `Path.GetFullPath`, which treated the `file://` scheme as part of a relative path and produced a nonsensical location, so the document resolver could not find the schema. The normalizer now converts an absolute `file://` reference to its local filesystem path via `Uri.LocalPath` (handling percent-decoding such as `%20`, and the platform-specific path shape) before resolving it. Relative references and non-`file` URIs (for example `http(s)://`) are unaffected. This shared normalizer is used by the V4 and V5 code generators, the CLI, the source generators, and the dynamic `Validator`. Reported via [#724](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/724); see [#817](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/817).

## V5.1.15

V5.1.15 fixes the analyzer diagnostic documentation links, which pointed at a non-existent domain.

### Bug fixes

- **Analyzer `helpLinkUri` values now resolve to the live documentation site** — The `HelpLinkUri` for every diagnostic in both analyzer packages pointed at `https://corvus-text-json.dev/docs/...`, a domain that does not exist, so clicking "Show documentation" on a diagnostic (or following the link in build output) reached a dead link. Both packages now point at the real site, `https://corvus-oss.org/Corvus.JsonSchema/docs/...`. For the production analyzers (`CTJ001`–`CTJ010`) the per-diagnostic anchors on `analyzers.html` already matched. For the migration analyzers (`CVJ001`–`CVJ025`) the base page was additionally retargeted from the prose migration guide to the dedicated `migration-analyzers.html`, and every diagnostic now deep-links to its own `#cvjNNN-…` section anchor (previously several fragments such as `#namespace-changes`, `#core-types`, and `#mutation` did not match any anchor on the target page). See [#766](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/766).

## V5.1.14

V5.1.14 fixes a concurrency bug in OpenAPI 3.2 generated streaming server endpoints.

### Bug fixes

- **OpenAPI 3.2 streaming endpoints no longer hold a thread-local rented `Utf8JsonWriter` across an `await`** — Generated server endpoints for streaming (`itemSchema`/NDJSON/SSE) responses rented a `Utf8JsonWriter` from the thread-local writer cache (`workspace.RentWriter(...)`) and held it across the `await` on `WriteStreamAsync`. Because the cache is backed by `[ThreadStatic]` state, the writer is bound to the renting thread; when the streaming continuation resumed on a different thread pool thread (the normal case under Kestrel, which has no `SynchronizationContext`), the matching `ReturnWriter` ran on the wrong thread and corrupted the cache — throwing a `NullReferenceException` (or, in debug builds, failing fast) when the continuation thread had no cache state, and otherwise silently poisoning another thread's cache. Streaming endpoints now use a new `JsonWorkspace.CreateWriter(IBufferWriter<byte>)` that returns a dedicated, non-pooled writer with no thread affinity, disposed via `await writer.DisposeAsync()` — which is safe to release on any thread. The non-streaming response path was unaffected (it rents and returns the writer synchronously with no intervening `await`) and is unchanged, as are the OpenAPI 3.0 and 3.1 generators (which have no streaming path). See [#814](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/814).

### New features

- **`JsonWorkspace.CreateWriter(IBufferWriter<byte>)`** — Creates a dedicated `Utf8JsonWriter` that is **not** drawn from the thread-local writer cache, so it carries no thread affinity and is safe to hold across an `await` boundary (for example while streaming a response). The caller owns the writer and disposes it; it must not be passed to `ReturnWriter`. Use `RentWriter`/`RentWriterAndBuffer` for the common synchronous case where the writer is rented and returned on the same thread without an intervening `await`. See [#814](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/814).

## V5.1.13

V5.1.13 fixes three V5 bugs — `CreatePatch` failing on frozen `JsonDocumentBuilder` elements, schema `default` values being invisible through the mutable view, and circular schemas producing non-terminating generated code — and adds the ability to construct a discriminated union directly from one of its branches.

### New features

- **Construct a discriminated union from a constituent branch** — A well-formed discriminated union (a `oneOf` whose branches carry a required `const` discriminator, with no further required structure on the base) can now be built directly from one of its branches. A branch's mutable view converts to the union in a single implicit hop (`Circle.Mutable` → `Shape`), and the union's builder `Source` accepts a branch by value, so a built branch flows straight into a containing type's `CreateBuilder` for a union-typed property — for example `ShapeHolder.CreateBuilder(ws, ShapeHolder.Circle.Build(...))` — with no intermediate union document. Detection is precise: extra required properties on the base, a missing or non-required `const`, or an `anyOf` (rather than `oneOf`) do not enable the wiring, while additional non-structural keywords (`title`, `description`, and so on) on the base still do. See [#812](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/812).

### Bug fixes

- **Circular same-instance composition now fails generation with a diagnostic** — A `oneOf`/`allOf`/`anyOf`/`$ref` cycle that evaluates against the *same instance* previously produced infinitely-recursive `Match()`/`TryGetAs` code (or silently-omitted code). Code generation now detects the non-terminating composition cycle and fails with a `CircularSchemaReferenceException` naming the referencing and referenced schema locations, surfaced by the V4 and V5 source generators as diagnostic `CRV1002`. Data-driven recursion through properties or items is correctly not flagged. Applies to both the V4 and V5 engines. See [#810](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/810).
- **Schema `default` values are surfaced through the mutable view** — A non-null `default` declared on a property was invisible through a type's mutable view, and reading it could corrupt the pooled workspace. The mutable getter now returns a zero-copy frozen facade over the immutable default: reads forward to the underlying value, child elements rebase onto the facade, and any attempt to mutate the default throws an `InvalidOperationException` instructing the caller to set the value on its parent first. See [#811](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/811).
- **`CreatePatch` works with frozen `JsonDocumentBuilder` elements** — Generating a JSON Patch from a frozen `JsonDocumentBuilder` element threw because the read-only metadata copy-out paths performed an unnecessary immutability check. The check has been removed from the copy-out paths (which only read data), so `CreatePatch` round-trips correctly when either side is a frozen builder element. See [#809](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/809).

## V5.1.12

V5.1.12 fixes two V5 bugs: `Corvus.Text.Json.Period` could emit `duration` strings that are invalid under the JSON Schema `duration` format, and calling `Freeze()` on a `JsonDocumentBuilder` created via `Parse` threw an exception.

### Bug fixes

- **`Period` no longer emits invalid `duration` strings** — The V5 `Corvus.Text.Json.Period` formatter (used by `Period.ToString()` and when writing a period to a JSON document, including the OpenAPI server `Build` methods) could emit a fractional seconds value, for example `P0Y0M0DT0H0M6.220724100S` for a period built from a sub-second `System.TimeSpan`. The JSON Schema `duration` format (RFC 3339 Appendix A) does not permit fractional seconds, so such values failed to round-trip through `Period.TryParse`. The formatter now rounds any sub-second component (milliseconds, ticks, nanoseconds) to the nearest whole second — rounding halves away from zero — producing a valid, round-trippable duration. As part of the fix the formatter also omits unnecessary zero-valued units where the RFC 3339 grammar allows, so output is now compact (for example `PT6S` and `P7D` rather than `P0Y0M0DT0H0M6S` and `P0Y0M7D`). Consumers that compared the exact formatted string should note the more compact output. See [#805](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/805).
- **`Freeze()` on a parsed `JsonDocumentBuilder` no longer throws** — Calling `Freeze()` on a document produced by `JsonDocumentBuilder<T>.Parse(...)` threw `ArgumentException` (`Offset and length were out of bounds`) from `Buffer.BlockCopy`. The freeze copy assumed every value was stored as a length-prefixed *DynamicValue* blob, but values from a parsed document point directly into the original UTF-8 JSON backing region with no such header, so the copy read a bogus length. Freezing now compacts the value backing into a raw-JSON region and a DynamicValue region and propagates the raw-region length to the frozen document, so parsed, mutated, and from-scratch documents all freeze correctly. See [#808](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/808).

## V5.1.11

V5.1.11 adds per-format assertion mode configuration to the V4 and V5 code generation pipelines, so consumers can selectively disable a single `format` assertion or downgrade it to a warning without weakening all format validation.

### New features

- **Per-format assertion modes** — A new `FormatAssertionMode` (`Assert` / `Disable` / `Warning`) can be configured per format, letting you keep strict format validation while relaxing an individual format that real-world data violates (for example a `date-time` missing its timezone offset). The effective mode is resolved per format as override > vocabulary > the global `assertFormat` default > disable. Configurable via the `corvusjson` CLI `--formatMode` flag, a `formatMode` object in `generator-config.json`, the `CorvusTextJsonFormatMode` / `CorvusJsonSchemaFormatMode` MSBuild properties, and the `CSharpLanguageProvider.Options` API. `Warning` applies to string formats (a non-conformant value is reported rather than rejected); numeric formats fall back to `Assert`. Mode selection happens at generation time, so the default assert path carries no extra runtime branches. Supported by both engines. See [#749](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/749).

## V5.1.10

V5.1.10 extends OpenAPI server generation to extract an operation's declared `security` for OpenAPI 3.0 and 3.1 (not just 3.2), models it with correct OR/AND semantics, and adds a one-line authorization helper.

### New features

- **Declared security extraction and authorization helper** — Generated servers now populate each endpoint's security requirements for OpenAPI 3.0 and 3.1 (previously only 3.2 did so; 3.0/3.1 always emitted an empty array despite the spec supporting `security`). `EndpointSecurityRequirement` carries the resolved `SchemeType` (oauth2/apiKey/http/openIdConnect) and a canonical `PolicyName`, and a generated `EndpointSecurityConventions.RequireDeclaredAuthorization` extension implements the default mapping (`AllowAnonymous` when there is no security, `RequireAuthorization(PolicyName)` per requirement) so the common case is a one-line hook. The generated registration takes no dependency on `Microsoft.AspNetCore.Authorization`, and the custom `configureEndpoint` escape hatch is unchanged. See [#791](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/791).
- **OR/AND security semantics** — `EndpointDescriptor.SecurityRequirements` is now an `IReadOnlyList<EndpointSecurityRequirementSet>`, one element per OR alternative, each exposing its AND-group of requirements, an `IsOptional` marker for the anonymous (`{}`) requirement, and a canonical `PolicyName`. `RequireDeclaredAuthorization` honours the structure (AND within an alternative; a single combined policy for multiple OR alternatives), correcting the previous behaviour that flattened the array and enforced the alternatives as if all were required. See [#791](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/791).

## V5.1.9

V5.1.9 emits `Build(...)` overloads on V5 object types that take the `Create(...)` property parameters directly, removing the nested builder lambda at call sites.

### New features

- **Property-parameter `Build(...)` overloads** — V5 object types now emit `Build(...)` / `Build<TContext>(...)` factories that capture the `Create(...)` arguments directly, so `quaternion: Quaternion.Build(w: r.W, x: r.X, y: r.Y, z: r.Z)` replaces the previous `(ref b) => b.Create(...)` lambda form. The captured arguments materialise through the existing static `Create` with no closure allocation. A type emits the overload only when it has at least one `Create` parameter, is not part of a reference cycle (keeping a `Source` from ever transitively containing itself), and its captured-slot weight is within the configured threshold (default 32); recursive or over-threshold types keep the delegate/context `Build` form with a `<remarks>` explaining the omission. The threshold is configurable via the `CSharpLanguageProvider.Options` API, the `CorvusTextJsonBuildParametersThreshold` MSBuild property, and the `corvusjson` CLI `--buildParametersThreshold` flag. See [#789](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/789).

## V5.1.8

V5.1.8 adds an opt-in `NullOrUndefinedExceptNonNullDefaulted` value for the `OptionalAsNullable` generation option — generating optional properties that declare a non-null `default` as non-nullable `T` — and fixes a V5 bug where an explicit JSON `null` was not mapped to C# `null` under `OptionalAsNullable=NullOrUndefined`.

### New features

- **`OptionalAsNullable=NullOrUndefinedExceptNonNullDefaulted`** — A new opt-in value for the `OptionalAsNullable` generation option (CLI `--optionalAsNullable`, MSBuild `CorvusTextJsonOptionalAsNullable` / `CorvusJsonSchemaOptionalAsNullable`), supported by both the V4 and V5 engines. It behaves like `NullOrUndefined`, except that an optional property which declares a **non-null** `default` is generated as a non-nullable `T` (the default is always materialised when the property is absent), rather than `T?`. Optional properties without a `default`, or whose `default` is JSON `null`, remain nullable `T?`. The new value is additive — existing `None` and `NullOrUndefined` consumers are unaffected. See [#787](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/787).

### Breaking changes

- **V5 `OptionalAsNullable=NullOrUndefined` now maps an explicit JSON `null` to C# `null`** — Previously the V5 engine returned a `Null`-kind value (rather than C# `null`) when an optional property was explicitly present as JSON `null`, implementing only the "Undefined" half of the documented "JSON `null` or missing values map to C# `null`" contract. It now returns C# `null` for an explicit JSON `null`, matching both the documentation and the V4 engine. Only generated getters for optional properties under `NullOrUndefined` are affected; absent properties, and properties with a non-null `default`, are unchanged. See [#787](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/787).

## V5.1.7

V5.1.7 adds an optional per-endpoint configuration callback to the generated OpenAPI server registration, enabling consumers to apply ASP.NET endpoint conventions — including wiring the OpenAPI security specification onto endpoints — without editing generated code.

### New features

- **Per-endpoint configuration callback for generated servers** — The generated `MapApiEndpoints` extension gains an additive overload that accepts a `ConfigureEndpoint` callback. It is invoked once per generated endpoint (including webhook/callback endpoints) with an `EndpointDescriptor` (operation id, generated method name, HTTP verb, route template, tags, callback origin, and the operation's security requirements) and the route's `IEndpointConventionBuilder`. This lets consumers apply per-endpoint conventions — authorization, naming, tags, output caching, rate limiting — and wire the OpenAPI security specification onto endpoints without editing generated code. The original overload is preserved, so the change is source- and binary-compatible. Implemented for OpenAPI 3.0/3.1/3.2 across both the regular and callback/webhook server paths, with no new package dependency on the generated server. See [#783](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/783).

## V5.1.5

V5.1.5 adds context-flowing source overloads to generated V5 mutable builders.

### New features

- **Context source builder overloads** — Generated V5 mutable object and array builders now expose `AddProperty<TContext>(propertyName, in PropertyType.Source<TContext> value)` and `AddItem<TContext>(in ItemType.Source<TContext> value)` overloads (for the `ReadOnlySpan<byte>`, `ReadOnlySpan<char>`, and `string` property-name forms, with `where TContext : allows ref struct` on .NET 9 and later). This lets a context-bearing `.Source<TContext>` value be added directly to a builder without first materialising it. See [#780](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/780).

## V5.1.4

V5.1.4 changes generated OpenAPI server result factories to take `.Source` types for response headers, deprecates `ParseValue()`, adds `JsonWorkspace.TakeOwnership()`, and fixes webhook schema pointer resolution.

### Breaking changes

- **Server result factory methods use `.Source` for response headers** — Generated server result factory methods (e.g., `ListPetsResult.Ok(...)`) now accept `.Source` types for response header parameters instead of realized types. For example, a header parameter changes from `JsonString xNext = default` to `JsonString.Source xNext = default`. Existing code that passes realized types (e.g., `JsonString.ParseValue(...)`) continues to compile via implicit conversion. Factories with headers but no body now also require a `JsonWorkspace workspace` parameter.
- **`ParseValue()` deprecated** — All `ParseValue()` overloads on `JsonElement` and generated types are now marked `[Obsolete]`. Use `ParsedJsonDocument<T>.Parse()` for pooled-memory parsing (returns a disposable document that recycles memory), or `Clone()` when you genuinely need a standalone copy. `ParseValue()` allocates backing memory that becomes GC garbage — the deprecation makes the performance tradeoff explicit. See [#772](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/772).

### New features

- **`JsonWorkspace.TakeOwnership()`** — Transfers lifetime ownership of any `IJsonDocument` (including `ParsedJsonDocument<T>`) to a workspace. The document is disposed when the workspace is disposed or reset. This enables handler patterns where parsed response data must outlive the handler method but should be cleaned up with the workspace, without forcing consumers into mutable builders for read-only scenarios. See [#777](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/777).

### Bug fixes

- **Webhook schema pointer resolution** — `openapi-callback-server` previously constructed JSON pointers using `#/paths/{name}` instead of `#/webhooks/{name}` when processing webhook schemas, causing resolution failures. `SchemaPointerBuilder` full-pointer methods now accept a `rootSegmentUtf8` parameter to correctly distinguish between paths and webhooks. See [#773](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/773).

## V5.1.2

V5.1.2 is a breaking change that moves generated JSON Schema model types into a `.Models` sub-namespace for OpenAPI and AsyncAPI code generation, preventing name collisions with request/response infrastructure types.

### Breaking changes

- **OpenAPI/AsyncAPI model types now in `.Models` sub-namespace** — JSON Schema model types generated by `corvusjson openapi-client`, `openapi-server`, `openapi-callback-client`, `openapi-callback-server`, and `asyncapi-generate` are now placed in a `.Models` sub-namespace (e.g., `Petstore.Client.Models` instead of `Petstore.Client`). Consumer code must add a `using` directive for the new namespace (e.g., `using Petstore.Client.Models;`). Request/response infrastructure types (producers, consumers, handler interfaces, request/response classes) remain in the root namespace.

## V5.1.1

V5.1.1 adds TOON conversion support and fixes OpenAPI 3.2 server streaming responses generated from `itemSchema` response content.

### New features

- **TOON conversion** — Added bidirectional TOON (Token-Oriented Object Notation) conversion for JSON-shaped data, with `Corvus.Text.Json.Toon` for the Corvus pooled document model and `Corvus.Toon.SystemTextJson` for `System.Text.Json`-only applications. The converters support TOON to JSON, JSON to TOON, parsed documents, UTF-8 buffer APIs, tabular object arrays, dotted-path expansion, key folding, documentation, examples, benchmarks, and a browser playground.

### Bug fixes

- **OpenAPI 3.2 server streaming responses** — Generated ASP.NET Core server stubs now emit response bodies for `itemSchema` streaming responses. Streaming result factories use generated push-writer callbacks, frame `text/event-stream` responses as SSE (`data: ...\n\n`), frame `application/x-ndjson` and other streaming media as newline-delimited JSON, and complete the HTTP stream when the callback returns.

## V5.1

V5.1 expands Corvus.Text.Json beyond JSON Schema model generation into strongly-typed API generation, and closes a set of V4/V5 parity gaps that were identified during the 5.1 release wrap-up.

### New features

- **OpenAPI client generation** — `corvusjson openapi-client` generates strongly-typed HTTP clients for OpenAPI 3.0, 3.1, and 3.2 specifications, including typed parameters, request validation, response result types, headers, streaming, multipart forms, binary payloads, and `MatchResult()` response dispatch.
- **OpenAPI server generation** — `corvusjson openapi-server` generates ASP.NET Core handler interfaces and endpoint registration for minimal APIs, using the same typed request/response models and schema validation as generated clients.
- **AsyncAPI producer/consumer generation** — `corvusjson asyncapi-generate` generates strongly-typed producers, consumers, handlers, and request/reply flows for AsyncAPI 2.6 and 3.0 specifications.
- **AsyncAPI transports** — Generated AsyncAPI applications can use runtime transport packages for NATS, Kafka, AMQP, MQTT, WebSocket, Azure Service Bus, and in-memory testing.
- **OpenAPI and AsyncAPI document models** — Added strongly-typed V5 models for OpenAPI 3.0/3.1/3.2 and AsyncAPI 2.6/3.0 specifications.
- **Pattern property helper APIs** — V5 generated types with `patternProperties` now include generated `MatchesPattern*`, `TryAsPattern*`, and `MatchPatternProperties()` visitor dispatch helpers. V4 now has the visitor dispatch helper alongside its existing per-pattern helpers.
- **CLI accessibility configuration** — The `corvusjson` CLI and config file now support default and per-named-type generated accessibility (`Public`/`Internal`) for both V4 and V5 engines.
- **V4 generated formatting APIs** — V4 generated types now implement `IFormattable`, and on supported TFMs `ISpanFormattable` and `IUtf8SpanFormattable`, including format-aware numeric and string-format paths.
- **V4 functional composition apply** — V4 generated object composition types now emit `WithApplied(in ComposedType value)` methods for functional merging of `allOf`/`anyOf`/`oneOf` composition type properties.
- **V4 generated debugger display** — V4 generated types now include `[DebuggerDisplay]` with a hidden debugger display property.

### Breaking changes

- **V4 multi-core union conversions are now explicit** — Generated V4 conversions from a multi-core union type to `bool` or the preferred numeric .NET type are now `explicit` instead of `implicit`. This fixes unsafe implicit conversions that could throw at runtime when the instance held a different branch. Code that relied on these implicit conversions must now use an explicit cast and handle invalid branch values appropriately.
- **V5 mutable composition conversions now return mutable results** — Generated `TryGetAs___` methods on mutable V5 composition types now use `out Component.Mutable` instead of `out Component`. Existing callers that declare the old immutable out-variable type, rely on `out var` inferring the immutable type, or depend on generic/overload inference from the old result type must update the declaration or assign the returned mutable value to an immutable variable after the call.

### Bug fixes

- **V4 escaped property-name handling** — `JsonPropertyName`, `JsonObjectProperty`, and `JsonObjectProperty<T>` now use decoded/unescaped property names when backed by `JsonProperty`. This fixes comparisons and regex matching for escaped JSON property names while preserving zero-allocation callback paths.
- **V4 WASM trim warnings** — Removed trim-unsafe `System.Text.Json.JsonSerializer` usage from V4 value serialization and Int128/UInt128 numeric fallbacks. WASM/trimming builds no longer report the related IL2026 warnings.
- **API documentation signatures** — API documentation generation now preserves `in`/`out`/`ref` modifiers and nullable annotations when generating method signatures on Ubuntu.
- **JsonElement API example** — The V5 `JsonElement` API example now demonstrates `JsonElement` directly instead of showing generated-model APIs.
- **CLI package README** — The `Corvus.Json.Cli` NuGet package now has its own README, and the V4 engine is described as the immutable `Corvus.Json.ExtendedTypes` model rather than as legacy.

## V5.0

V5 introduces the new **Corvus.Text.Json** engine — a brand new code generator and runtime library that uses the existing Corvus.Json.CodeGeneration framework, and builds on the patterns of `System.Text.Json` with pooled-memory parsing, mutable document building via `JsonWorkspace`, and familiar strongly-typed `readonly struct` wrappers generated from JSON Schema, with a streamlined API and substantial performance improvements.

What we now call the V4 Engine continues to be maintained in this library - and with the same command line tool - and provides our solution for a side-effect-free mutation model.

### New features

- **Pooled-memory parsing** — `ParsedJsonDocument<T>` backed by `ArrayPool<byte>`. Just 136 bytes per document.
- **Mutable documents** — `JsonDocumentBuilder<T>` and `JsonWorkspace` provide a builder pattern for creating and modifying JSON 'in place', with versioned elements that detect stale references.
- **Extended numeric types** — `BigNumber` for arbitrary-precision decimals, `BigInteger` for large integers, plus `Int128`, `UInt128`, and `Half`.

### Breaking changes

- The CLI tool has been renamed from `generatejsonschematypes` (package: `Corvus.Json.CodeGenerator`) to `corvusjson` (package: `Corvus.Json.Cli`). Schema generation is now the `jsonschema` subcommand: `corvusjson jsonschema schema.json ...`. The legacy `generatejsonschematypes` command still works as a shim but displays a deprecation warning.
- The `corvusjson` CLI tool defaults to the V5 engine. The legacy `generatejsonschematypes` shim defaults to V4. To explicitly select an engine, use `--engine V4` or `--engine V5`.
- V5 generated types use the `Corvus.Text.Json` namespace and require the `Corvus.Text.Json` NuGet package at runtime, rather than `Corvus.Json.ExtendedTypes`.
- The immutable functional API from V4 (`WithProperty()`, `SetItem()`, etc.) is replaced by the mutable builder pattern (`CreateBuilder()`, `SetProperty()`, etc.).
- We now support the syntax of ECMAScript Regular Expressions (with the /u Unicode option) by translating them to .NET Regular Expressions during code generation.

### Bug fixes

- **Duration format validation** — Now strictly validates against RFC 3339 Appendix A grammar. Previously accepted fractional values (`PT0.5S`), non-contiguous designators (`P1Y2D` skipping Months), and other ISO 8601 extensions that are not part of RFC 3339. Both V5 and V4 engines are fixed.
- **URI percent-encoding validation** — Now rejects invalid percent-encoded sequences (`%`, `%A`, `%6G`). Previously these were accepted by the URI validator. Both V5 and V4 engines are fixed.
- **Hostname validation** — Now correctly allows consecutive hyphens in hostnames per RFC 1123 (`ab--cd.example`). Previously all `--` sequences were rejected due to overly strict IDNA/punycode detection. Both V5 and V4 engines are fixed.

### V4 test infrastructure

- **Name-based test exclusions** — The V4 spec generator now uses name-based test exclusions (matching by scenario and test description) instead of brittle index-based exclusions. This makes the V4 test suite stable across JSON Schema Test Suite updates that reorder tests.
- **OpenAPI 3.0 patternProperties exclusions** — Tests for `patternProperties` in the OpenAPI 3.0 test suite are now excluded, as `patternProperties` is not a supported keyword in the [OpenAPI 3.0 Schema Object](https://spec.openapis.org/oas/v3.0.4.html#schema-object).

## V4.6 Updates

### Breaking changes

We had a long-standing bug with the pseudo-generic type pattern and `$dynamicRef` where you would get an extra level of indirection because the anchoring `$ref` was not reducible. This is now fixed, but any code using `$dynamicRef` will need to be simplified to remove the redundant indirection.

## V4.5 Updates

### Breaking changes (Language Provider Implementers only)

The `IKeywordValidationHandler` interface contains a number of APIs that are, with hindsight, specific to the particular implementation in the `CSharpLanguageProvider` implementation.

It forces you into a method definition / method call pattern, and also implementing the "child handler" pattern.

While the child-handler pattern is still likely useful, it may have a completely different implementation in other providers.

The method definition / method call pattern is very much an "implementer's choice" and should not be imposed on all future implementations.

This breaking change applies to *language provider implementers* only, and it splits `IKeywordValidationHandler` into `IKeywordValidationHandler` and `IMethodBasedKeywordValidationHandlerWithChildren`

If you have any existing code that depends on `IKeywordValidationHandler` you will need to update it to use `IMethodBasedKeywordValidationHandlerWithChildren` instead. This can be done with a global search and replace.

You will likely also need to use the new overload of the `TypeDeclaration` extension method `OrderedValidationHandlers<T>()` to retrieve the handlers using the correct interface.

For example, the CSharpLanguageProvider has been updated in three places to use the new overload, so we can access the handler via the new interface. Unsurprisingly, these are the three places that make use of the method based/child handler pattern. Here's one of those.

```csharp
    private static CodeGenerator AppendValidationHandlerSetup(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator.AppendUsingEvaluatedItems(typeDeclaration);
        generator.AppendUsingEvaluatedProperties(typeDeclaration);

        foreach (IMethodBasedKeywordValidationHandlerWithChildren handler in typeDeclaration.OrderedValidationHandlers<IMethodBasedKeywordValidationHandlerWithChildren>(generator.LanguageProvider))
        {
            handler.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }
```

## V4.4.3 Updates

Added <CorvusJsonSchemaFallbackVocabulary>Corvus202012</CorvusJsonSchemaFallbackVocabulary> to support the `$corvusTypeName` keyword without requiring you to specify an explicit `$schema` for the vocabulary.

## V4.4 Updates

### Breaking changes

The property accessor mechanism now respects the default value of the property type.

If the schema for the property defines a `default` value, then the property accessor will return this value if and only if the value actual value is `ValueKind.Undefined`.

This does *not* affect equality or other comparisons for the object as a whole - if one value has the property *explicitly* set to the default value, and the other is relying on the "default" value, then the instances *will not* be equal.

If you use the `TryGetProperty()` mechanism to get the property value, this *will not* return the default from its `JsonObjectProperty.Value`. However, if you have access to a `JsonObjectProperty<TValue>` where the type of the property is known, then the `Value` will respect the default in the same way as the property itself.

### Fixes

In the latest version of the .NET SourceGenerator codebase, the behaviour when properties are missing has changed (we would suggest "is broken"). It no longer returns false and a null value for a missing property, but instead provides an empty string. This broke our default-value logic for settings - the net effect of which is that forcing format validation by default is accidentally switched off.

We have restored the previous behaviour, regardless of which version of the source generator infrastructure is in use.

## V4.3.17 Updates

Added netstandard2.1 packages for `Corvus.Json.ExtendedTypes` and `Corvus.Json.JsonReference` in order to support Unity builds.

## V4.3.16 Updates

### Use of IndexRange package is deprecated.

As of V1.1 of IndexRange, it now type-forwards to the recently shipped `Microsoft.Bcl.Memory` library. We will be removing the dependency on IndexRange in the V4.4 release cycle (some time after .NET 10 ships), and replacing it directly with `Microsoft.Bcl.Memory`. You should make the changes in your own code base if you have a direct dependency on `IndexRange` with this releasee in order to prepare for that change.

## V4.3.10 Updates

Added `<CorvusJsonSchemaUseImplicitOperatorString>true</CorvusJsonSchemaUseImplicitOperatorString>` to enable implicit conversion to `string`.

WARNING: Although this is very convenient for string-heavy code, it may cause unintended allocations if used without care.

## V4.3.0 Updates

### Type Accessibility using the Source Generator

The source generator now respects the accessibility of the model type.

For example

```csharp
[JsonSchemaTypeGenerator("../test.json#/$defs/FlimFlam")]
internal readonly partial struct FlimFlam
{
}
```

Any nested types will be generated with `public` accessibility.

Only `internal` and `public` are supported. The source generator will fail for an unsupported accessiblity declaration.

You can override the default accessibility for all generated types with a build property:

`<CorvusJsonSchemaDefaultAccessibility>Internal</CorvusJsonSchemaDefaultAccessibility>`

Note that you can still generate code that will not compile if you incorrectly mix-and-match `public` and `internal`. It is your responsibility to ensure that your types have compatible accessibility.

## V4.2.0 Updates

### Breaking change

The heuristic for naming (but not ordering) parameters to the `JsonObject.Create()` function has changed, to fix an issue with a parameter naming where properties differ only by case.

This could affect code that is using explicit named parameters with `Create()`, if your parameter changes its name.

If this causes a significant problem in your codebase, please raise an issue here and we will work with you to resolve the problem.

## V4.1.2 Updates

Added the `--addExplicitUsings` switch to the code generator (and a corresponding property to the `generator-config.json` schema). If `true`, then
the source generator will emit the standard global usings explicitly into the generated source files. You can then use the generated code in a project that does not have `<ImplicitUsings>enable</ImplicitUsings>`.

```csharp
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using global::System.Net.Http;
using global::System.Threading;
using global::System.Threading.Tasks;
```

## V4.1.1 Updates

## Help for people building analyzers and source generators with JSON Schema code generation

We have built a self-contained package called Corvus.Json.SourceGeneratorTools for people looking to build .NET Analyzers or Source Generators that take advantage of JSON Schema code generation.

See the [README](./Solutions/Corvus.Json.SourceGeneratorTools/README.md) for details.

## V4.1 Updates

### YAML support

We now support YAML documents for the CLI tool.

You can mix-and-match YAML and JSON documents in the same schema set, and the tool will generate code for either.

Your JSON schema can be embedded in a YAML document (such as a YAML-based OpenAPI or AsyncAPI document), and you can resolve internal references just as with a JSON document.

Add the `--yaml` command line option to enable YAML support, or set the `supportYaml: true` property in a generator config file

#### Example

*schema.yaml*
```yaml
type: array
prefixItems:
  - $ref: ./positiveInt32.yaml
  - type: string
  - type: string
    format: date-time
unevaluatedItems: false
```

*positiveInt32.yaml*
```yaml
type: integer
format: int32
minimum: 0
```

```
generatejsonschematypes --rootNamespace TestYaml --outputPath .\Model --yaml schema.yaml
```

## V4.0 Updates

There are a number of significant changes in this release

### Support for cross-vocabulary schema generation.

  So if you are upgrading a draft6 or draft7 schema set to 2020-12, for example, you can do it piecemeal and reference a schema with one dialect from a schema with another.

### Opt-in support for .NET nullable properties

  Where JSON Schema object properties are optional or nullable, use the `--optionalAsNullable` command line switch to emit nullable properties.

### Opt-in support for implicit conversions to `string` from JSON `string` types

If you have a JSON `string` type, we currently emit an `explicit` operator to convert to a .NET `string` (the counterpart of the `implicit` conversion operator *from* a .NET `string`).

We do this because conversion to string causes an allocation, and it is very easy to inadvertently do this when working with APIs that offer `string`-based overloads, in addition to e.g.
`ReadOnlySpan<char>` overloads. When passing an instance of the generated type directly to the API, the implicit conversion would kick in, allocating a string, with no warning that this
is what you have done. In a high-performance/low-allocation scenario this would be undesirable, and you would prefer to use the `GetValue()` method on the instance,
and pass the `ReadOnlySpan<char>` provided to the callback for that method.

However, sometimes you just want the convenience of being able to behave as if your JSON value is a `string`.

If so, you can now use the `--useImplicitOperatorString` command line switch to emit an implicit conversion operator to `string` for JSON `string` types.

Note: this means you will never use the built-in `Corvus.Json` types for your string-like types. This could increase the amount of code generated for your schema.

### New Source Generator

We now have a source generator that can generate types at compile time, rather than using the `generatejsonschematypes` tool.

### Using the source generator

Add a reference to the `Corvus.Json.SourceGenerator` nuget package in addition to `Corvus.Json.ExtendedTypes`. [Note, you may need to restart Visual Studio once you have done this.]
Add your JSON schema file(s), and set the Build Action to _C# analyzer additional file_.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.3.9" />
    <PackageReference Include="Corvus.Json.SourceGenerator" Version="4.3.9">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="test.json" />
  </ItemGroup>

</Project>
```

Now, create a `readonly partial struct` as a placeholder for your root generated type, and attribute it with
`[JsonSchemaTypeGenerator]`. The path to the schema file is relative to the file containing the attribute. You can
provide a pointer fragment in the usual way, if you need to e.g. `"./somefile.json#/components/schema/mySchema"`

```csharp
namespace SourceGenTest2.Model;

using Corvus.Json;

[JsonSchemaTypeGenerator("../test.json")]
public readonly partial struct FlimFlam
{
}
```

The source generator will now automatically emit code for your schema, and you can use the generated types in your code.

```
using Corvus.Json;
using SourceGenTest2.Model;

FlimFlam flimFlam = JsonAny.ParseValue("[1,2,3]"u8);
Console.WriteLine(flimFlam);
JsonArray array = flimFlam.As<JsonArray>();
Console.WriteLine(array);
```

You can find an example project here: [Sandbox.SourceGenerator](./Solutions/Sandbox.SourceGenerator)

We'd like to credit our Google Summer of Code 2024 contributor, [Pranay Joshi](https://github.com/pranayjoshi) and mentor [Greg Dennis](https://github.com/gregsdennis) for their work on this tool.

#### Configuring the source generator

There are a number of global configuration options for the source generator. These can be added to a `PropertyGroup` in your `.csproj` file.

e.g.

```xml
<PropertyGroup>
   <CorvusJsonSchemaOptionalAsNullable>None</CorvusJsonSchemaOptionalAsNullable>
</PropertyGroup>
```

`CorvusJsonSchemaOptionalAsNullable`
  - `None` - Do not emit nullable properties for optional properties
  - `NullOrUndefined` - Emit nullable properties for optional properties

`CorvusJsonSchemaDisableOptionalNamingHeuristics`
  - `False` - Enable optional naming heuristics [default]
  - `True` - Disable optional naming heuristics

`CorvusJsonSchemaDisabledNamingHeuristics`
  - Semi-colon separated list of naming heuristics to disable. You can list the available name heuristics with the `generatejsonschematypes listNameHeuristics` command in the CLI.

`CorvusJsonSchemaAlwaysAssertFormat`
  - `False` - Respect the vocabulary's format assertion
  - `True` - Always assert format assertions [default]

### New dynamic schema validation

There is a new `Corvus.Json.Validator` assembly, containing a `JsonSchema` type.

This is a new *dynamic* JSON Schema validator that can validate JSON data against a JSON Schema document, without the need to generate code ahead-of-time.

This is useful for scenarios where you have a JSON Schema document that is not known at compile time, and you only require validation, not deserialization.

You can load the schema with

```csharp
var corvusSchema = CorvusValidator.JsonSchema.FromFile("./person-array-schema.json");
```

This builds and caches a schema object from the file, and you can then validate JSON data against it with

```csharp
JsonElement elementToValidate = ...
ValidationContext result = this.corvusSchema.Validate(elementToValidate);
```

Note that this uses dynamic code generation under the hood, with Roslyn, so there is an appreciable cold-start cost for the very first schema you validate in this way
while the Roslyn components are jitted. Subsequent schema are much faster, and reused schema come from the cache.

If you reference the `Corvus.Validator` package directly in your executing assembly, it will include a target that ensures `<PreserveCompilationContext>true</PreserveCompilationContext>`
is added to a `<PropertyGroup>` in your project.

If you are using the `Corvus.Json.Validator` package in a library, you should ensure that the consuming project has this property set, to avoid issues with dynamic code generation.

You will have to do this manually if it is consumed via a Project Reference.

### New `generatejsonschematypes config` command

  Supply a json config file to the generate command, to configure and generate 1 or many schema in a single command.

  The configuration file also allows you to explicitly name arbitrary types, and optionally map them in to a specific .NET namespace.

  You can also map json schema base file URIs to specific .NET namespaces, and pre-load known-good versions of file reference dependencies.

  The [schema for the configuration file is here](./Corvus.Json.CodeGenerator/generator-config.json).

### New command line validator with `generatejsonschematypes validateDocument`

This command will validate a JSON document against a JSON schema, and output the results to the console.

For example, given schema `schema.json`

```json
{
    "$schema": "https://corvus-oss.org/json-schema/2020-12/schema",
    "type": "array",
    "prefixItems": [
        {
            "$corvusTypeName": "PositiveInt32",
            "type": "integer",
            "format": "int32",
            "minimum": 0
        },
        { "type": "string" },
        {
            "type": "string",
            "format": "date-time"
        }
    ],
    "unevaluatedItems": false
}
```

and the document `document_to_validate.json`

```json
[
    -1,
    "Hello",
    "Goodbye"
]
```

If we run:

```
generatejsonschematypes validateDocument ./schema.json ./document_to_validate.json`
```

We see the output:

```
Validation minimum - -1 is less than 0 (#/prefixItems/0/minimum, #/prefixItems/0, #/0, ./testdoc.json#1:4)
Validation type - should have been 'string' with format 'datetime' but was 'Goodbye'. (#/prefixItems/2, , #/2, ./testdoc.json#3:4)
```
### Multi-language code generator engine

- Brand new JSON Schema analyser engine, which is now language independent.
- Brand new code generation engine, which is more flexible and extensible, and uses the result of the schema analyser.
- An extensible C# language provider which generates code-using-code. No more T4 templates in the language engine.

### Additional features

- Opt-out of optional naming heuristics introduced in V3.0 with the `--disableOptionalNamingHeuristics` command line switch.
- Opt-out of specific naming heuristics by specifying `--disableNamingHeuristic`. You can list the available name heuristics with the new `generatejsonschematypes listNameHeuristics` command
- Safe truncation for extremely long file names
- Access to all JSON schema validation constants via the `CorvusValidation` nested static class.
- All formatted types (e.g. string or number formats) are now convertible to the equivalent core types (e.g. your custom `"format": "date"` type is freely convertible to and from `JsonDate`) and offer the same accessors and conversions as the core types.

### Upgrading to V4
- Code generated using V3.1 of the generator can still be built against V4 of Corvus.Json.ExtendedTypes, and used interoperably.

  This allows you to upgrade your code piecemeal to the new version of the generator. You do not need to update everything all at once.

- For the vast majority of schema, the new naming heuristics will continue to work as they did in V3.
  However, if you have a schema that is not generating the names you expect, you can inject `$corvusTypeName` into the schema to provide a hint to the generator.
  If you  hit one of these cases, please [open an issue in github](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues).

- We now generate fewer files for each type. You should delete your previous generated files before running the new version of the generator, to avoid leaving duplicate partial definitions.

### Breaking changes
- .NET 6 and .NET 7 are now out-of-support. We no longer support these versions. The `netstandard2.0` builds will fail at runtime.

- - We no longer generate the property 'default' accessors.

  Prior to V4 we emitted methods like `TryGetDefault(in JsonPropertyName name, out JsonAny value)` on objects whose properties had types with default values.

  This was somewhat redundant code, as
  a) it lacked strong typing and
  b) it had unncessary overhead - you could go directly to the property type to get the default value, rather than doing a lookup by the property name.

  If you want to discover the default value for a property, you must now do so by inspecting the `Default` static property of its type.
  If you are affected by this change, you can copy the `[typename].Default.cs` file for the relevant type from your V3 code base to provide the capability.

  However, we recommend refactoring to use the static `Default` property on the property type instead.

## V3.0 Updates

The big change with v3.0 is support for older (supported) versions of .NET, including the .NET Framework, through netstandard2.0.

As of v3.0.23 we also support draft4 and OpenAPI3.0 schema.

Additional changes include:

- Pattern matching methods for anyOf, oneOf and enum types.
- Implicit cast to bool for boolean types
- Specify an explicit type name hint for a schema with the $corvusTypeName keyword
- Improved heuristic for type naming based on `title` and `documentation` as fallbacks if no better name can be dervied.

## V2.0 Updates

There have been considerable breaking changes with V2.0 of the generator. This section will help you understand what has changed, and how to update your code.

### Json Schema Models

The JSON Schema Models have been broken out into separate projects.

  - Corvus.Json.JsonSchema.Draft6
  - Corvus.Json.JsonSchema.Draft7
  - Corvus.Json.JsonSchema.Draft201909
  - Corvus.Json.JsonSchema.Draft202012

### Code Generation

### Property Names

The static values for JSON Property Names have been moved from the root type, to a nested subtype called `PropertyNamesEntity`

### Conversions and operators

The implicit/explicit conversions and operators have been rationalised. More explicit conversions are required, at the expense of the implicit conversions.

However, most implicit conversions from/to intrinsic types are still supported.

One significant change is that there is *no* implicit conversion to `string` - this must be done explicitly, or directly through one of the comparison functions like `EqualsString()` or `EqualsUtf8String()`. This is to prevent a common source of accidental allocations and the corresponding performance hit.

## System.Text.Json support by other projects

There is a thriving ecosystem of System.Text.Json-based projects out there.

In particular I would point you at

[JsonEverything](https://github.com/gregsdennis/json-everything) by [@gregsdennis](https://github.com/gregsdennis)

- JSON Schema, drafts 6 and higher ([Specification](https://json-schema.org))
- JSON Path ([RFC in progress](https://github.com/ietf-wg-jsonpath/draft-ietf-jsonpath-jsonpath)) (.NET Standard 2.1)
- JSON Patch ([RFC 6902](https://tools.ietf.org/html/rfc6902))
- JsonLogic ([Website](https://jsonlogic.com)) (.NET Standard 2.1)
- JSON Pointer ([RFC 6901](https://tools.ietf.org/html/rfc6901))
- Relative JSON Pointer ([Specification](https://tools.ietf.org/id/draft-handrews-relative-json-pointer-00.html))
- Json.More.Net (Useful System.Text.Json extensions)
- Yaml2JsonNode

[JsonCons.Net](https://github.com/danielaparker/JsonCons.Net) by [@danielParker](https://github.com/danielaparker)

- JSON Pointer
- JSON Patch
- JSON Merge Patch
- JSON Path
- JMES Path
