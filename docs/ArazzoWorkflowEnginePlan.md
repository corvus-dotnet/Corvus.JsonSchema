# Arazzo Workflow Execution Engine — Implementation Plan

> Status: Proposal / design. Target: build an Arazzo 1.0.x / 1.1.0 workflow execution
> engine on top of the existing Corvus OpenAPI and AsyncAPI code-generation stack.

## 1. What Arazzo is

The [Arazzo Specification](https://github.com/OAI/Arazzo-Specification)
(OpenAPI Initiative, current version **1.1.0**, 2026-05-18) is a
language-agnostic format for describing **sequences of API calls** and the
data dependencies between them. An Arazzo document references one or more
**source descriptions** (OpenAPI, AsyncAPI, or another Arazzo doc) and weaves
their operations into **workflows** made of ordered **steps**. Data flows
between steps via **runtime expressions** (`$steps.x.outputs.y`, `$inputs.z`,
`$response.body#/ptr`, …); branching is driven by **success/failure criteria**
and **success/failure actions** (`end` / `goto` / `retry`).

### Object model (the pieces we must support)

| Object | Purpose | Key fields |
|---|---|---|
| Arazzo (root) | container | `arazzo`, `$self`, `info`, `sourceDescriptions`, `workflows`, `components` |
| Source Description | reference to an OpenAPI/AsyncAPI/Arazzo doc | `name`, `url`, `type` |
| Workflow | a named sequence | `workflowId`, `inputs` (JSON Schema), `steps`, `outputs`, `parameters`, `successActions`, `failureActions`, `dependsOn` |
| Step | one operation / sub-workflow call | `stepId`, `operationId` \| `operationPath` \| `workflowId`, `parameters`, `requestBody`, `successCriteria`, `onSuccess`, `onFailure`, `outputs`, `dependsOn`; AsyncAPI: `channelPath`, `correlationId`, `action` (send/receive), `timeout` |
| Parameter | input to operation/workflow | `name`, `in` (path/query/querystring/header/cookie), `value` (const \| expression \| Selector) |
| Success Action | `end` or `goto` | `name`, `type`, `stepId`/`workflowId`, `criteria`, `parameters` |
| Failure Action | `end`/`retry`/`goto` | `name`, `type`, `stepId`/`workflowId`, `retryAfter`, `retryLimit`, `criteria` |
| Criterion | assertion | `context`, `condition`, `type` (simple/regex/jsonpath/xpath) |
| Selector (1.1) | precise data extraction | `context`, `selector`, `type` (jsonpath/xpath/jsonpointer) |
| Request Body | payload | `contentType`, `payload`, `replacements[]` (Payload Replacement) |
| Components / Reusable | reuse | `inputs`, `parameters`, `successActions`, `failureActions`; `$components.<type>.<name>` |

### Runtime expressions
`$url`, `$method`, `$statusCode`, `$request.{header,query,path,body}`,
`$response.{header,body}`, `$message.{header,payload}` (AsyncAPI),
`$inputs.<n>`, `$outputs.<n>`, `$steps.<id>.outputs.<n>`,
`$workflows.<id>.{inputs,outputs}.<n>`, `$sourceDescriptions.<n>...`,
`$components.<type>.<n>`, `$self`. Each `...body`/`...payload` form may carry a
`#/json-pointer` suffix. Expressions also embed in strings via `{$...}`.

## 2. What we already have (and can reuse)

The repo is a high-performance, **zero-reflection, compile-time** code-gen
stack. The relevant assets:

- **OpenAPI codegen** — `Corvus.Text.Json.OpenApi30/31/32` (models generated
  from a checked-in `OpenApiNN.json` schema), `…OpenApi.CodeGeneration`
  (generator), `…OpenApi` (runtime contracts), `…OpenApi.HttpTransport`
  (`HttpClientTransport`, auth providers).
- **Generated client shape** — per operation, a `struct TRequest : IApiRequest<TRequest>`
  and `struct TResponse : IApiResponse<TResponse>`, invoked through
  `IApiTransport.SendAsync<TRequest,TResponse>(in req, …)` (and body overloads).
  Requests carry typed parameter fields with style/explode baked in; responses
  expose status-discriminated, lazily-parsed typed bodies.
- **AsyncAPI codegen** — `…AsyncApi26/30`, `…AsyncApi.CodeGeneration`, protocol
  bindings (Kafka, MQTT, NATS, AMQP, Azure Service Bus, WebSocket),
  `…AsyncApi.Polly` (resilience), `…AsyncApi.Testing`, plus
  `AsyncApiRuntimeExpression` (`$message.header/payload#…`).
- **Existing runtime-expression parser** — `OpenApi.CodeGeneration.RuntimeExpression`
  already parses a **subset** (`$url`, `$method`, `$request.*`, `$response.*`).
  Arazzo needs a **superset** (steps/inputs/outputs/workflows/components/message/statusCode + `{…}` interpolation).
- **Expression evaluation runtimes** — `…JsonPath` (RFC 9535 JSONPath evaluator),
  `…JMESPath`, `…Jsonata`, `…JsonLogic`, each with runtime + codegen + source-gen.
- **JSON plumbing** — `ParsedJsonDocument<T>`, `JsonDocumentBuilder<T>`, JSON
  Pointer encoding, `JsonSchemaTypeBuilder` (drives type generation from a schema).
- **CLI** — `Corvus.Json.Cli(.Core)` with `openapi-*`, `asyncapi-*`, `jsonschema`,
  `config` commands; `GeneratorConfig` generated at build time from its schema;
  per-spec lock files (`OpenApiLockFile`) for incremental regen.

**There is no workflow engine today** — but every primitive a workflow needs
(typed HTTP/messaging execution, JSON Pointer, JSONPath, schema-typed models)
already exists.

## 3. The central design decision

Arazzo references operations **dynamically** by `operationId`/`operationPath`
read from a JSON document at run time. Our generated clients are **statically
typed**: each operation is a *distinct* `TRequest`/`TResponse` struct dispatched
through generic constraints — there is no `operationId → Type` registry, by
design (no reflection, no boxing).

Two ways to bridge this:

- **(A) Code-generate the workflow executor** (recommended). At build time, the
  Arazzo generator resolves each step's `operationId` to the *specific* generated
  request/response struct and emits straight-line C# that binds parameters,
  calls `SendAsync<TRequest,TResponse>`, evaluates criteria, extracts outputs,
  and implements control flow. This matches the repo's entire philosophy:
  static typing, zero reflection, validated at compile time, fast.
- **(B) Runtime interpreter** (rejected as primary). Load Arazzo + OpenAPI at
  run time and dispatch dynamically. This fights the static transport contract
  (generic struct constraints), forces reflection/boxing, and discards
  compile-time validation. Could be offered later as a thin "dynamic mode" for
  scenarios where the workflow isn't known at build time, but it is **not** the
  default.

**Decision: build a code generator** (`Corvus.Text.Json.Arazzo.CodeGeneration`)
plus a small **runtime support library** (`Corvus.Text.Json.Arazzo`) for the
cross-cutting concerns that are genuinely dynamic (expression evaluation
against live JSON, criterion evaluation, retry/goto control-flow state, result
aggregation). The generated code is thin glue; the runtime library is reusable
and independently testable.

### 3.1 Generate only what's used (filtering)

An Arazzo document is a **precise manifest of the operations actually invoked**:
every step names an `operationId`/`operationPath` (or a `channelPath` for
AsyncAPI). We must exploit this — do **not** generate a full client for each
source description. Instead:

- Walk all workflows/steps up front and compute the exact set of referenced
  operations per source description.
- Drive the existing OpenAPI/AsyncAPI **`OperationFilter`** with that set so the
  client generators emit only the referenced operations' request/response
  structs **and (transitively) only the schema models those operations reach**.
- For Arazzo→Arazzo source descriptions, recurse into the referenced doc and
  union the operation sets before filtering.

Benefits: dramatically smaller generated code, faster builds, smaller binaries,
and a generated surface that maps 1:1 to what the workflow needs — nothing
dead. This must be a first-class concern of the generator from Phase 2, not a
later optimization, because it shapes the coordination contract between the
Arazzo generator and the OpenAPI/AsyncAPI generators.

### 3.2 Testability & mocking (first-class, from day one)

Users must be able to **exercise a workflow they have designed against mocked
responses** to produce deterministic outcomes and call paths — for automated
tests **and** for an interactive workflow-designer UI. This is a design driver,
not an afterthought, and it is cheap because the architecture already provides
the seam.

- **The single I/O seam.** All execution flows through
  `IWorkflowTransportProvider.For(source) → IApiTransport`. Swap in a mock and
  *any* workflow becomes exercisable with zero real endpoints.
- **`MockApiTransport` / `MockWorkflowTransport`** — a generic `IApiTransport`
  implementation. It inspects the typed request via the *same* `Write*` members
  the real transport uses (`Method`, resolved path, query, headers, body) and
  returns a programmed response built through the generated
  `TResponse.CreateAsync(statusCode, stream, contentType, headers, …)` factory,
  feeding canned JSON over a `MemoryStream`. **No per-operation mock code is
  required** — the static-abstract request/response contract makes this fully
  generic and reuses the exact production parsing path.
- **Response scripting.** Program responses by `(source, operationId/path +
  method)` with optional request-content predicates; queue sequences to drive
  `retry`/`goto` branches; set status codes, headers and bodies so every
  success/failure criterion path is reachable and assertable.
- **Determinism.** Inject `TimeProvider` (virtual clock for `retryAfter` /
  `timeout`) and a seeded id source (e.g. `correlationId`); no wall-clock, no
  network. Same inputs + same script ⇒ same outcome + same call path, every run.
- **Structured execution trace.** The engine records which steps ran, the
  request issued, the response used, each criterion's result, the action taken
  (`end`/`goto`/`retry`), retry counts, and outputs — the deterministic
  *call path*.
- **Designer-UI mode.** A `WorkflowSimulator` facade = executor + mock transport
  + trace, returning the structured outcome and call path for visualization. The
  same facade backs unit tests and an interactive designer, and its trace is the
  **same model the telemetry emits** (§3.3) — one source of truth.

### 3.3 Observability (first-class, from day one)

The engine emits **fully correlated OpenTelemetry** from the very first
generated executor, and telemetry correctness is a **tested conformance
contract**, not best-effort.

- **Correlated span tree.** A dedicated `ActivitySource`
  (`Corvus.Text.Json.Arazzo`) emits: workflow (root) → step (child) →
  operation/message request (child); sub-workflow steps nest their own workflow
  span. W3C trace context propagates into outgoing requests (reuse AsyncAPI's
  `TraceContextPropagator`; emit the equivalent for the OpenAPI transport) so
  spans correlate **end-to-end through the called APIs**.
- **Attributes.** `workflowId`, `stepId`, `operationId`/`operationPath`, source
  name, criterion outcomes, action taken, retry count/limit, status code,
  success/failure — with redaction hooks and bounded cardinality for sensitive
  or high-cardinality params/bodies.
- **Metrics.** A `Meter` exposes workflow/step duration histograms and
  retry/goto/success/failure counters.
- **BCL-only, exporter-agnostic.** Built on `System.Diagnostics`
  (`ActivitySource`/`Meter`) so the core runtime takes no heavy dependency; OTel
  is wired via standard registration by the host.
- **Conformance via telemetry.** The conformance suite runs reference workflows
  against scripted mock responses and asserts **both** the functional outcome
  **and** the emitted span tree — shape, parent/child correlation, and
  attributes — using an in-memory exporter. The §3.2 execution trace and the
  telemetry are the same model, so the in-memory exporter *is* the call-path
  oracle.

### 3.4 Runtime engineering principles (how the code is written)

The engine is part of a high-performance, zero-reflection library, so the
implementation must follow its house style:

- **Strongly-typed accessors first.** Consume generated model types via their
  typed getters; use `IsNotUndefined()`/`IsUndefined()` for presence and
  `GetString()`/typed getters for values — never `ValueKind`-sniffing or
  `(string)` casts where a typed accessor exists.
- **`Match()` for unions and enums.** Resolve discriminated shapes (e.g. a
  step's `operationId`/`operationPath`/`workflowId`, or a closed enum) with the
  generated `Match(...)` overloads, which hand back a view where the matched
  member is guaranteed present — not hand-rolled `if`/`IsNotUndefined` ladders.
- **Zero allocation on the per-execution hot path.** Stay in **UTF-8**
  (`ReadOnlySpan<byte>`) end-to-end; prefer UTF-8 overloads
  (`TryGetProperty(ReadOnlySpan<byte>)`, `TryResolvePointer(ReadOnlySpan<byte>)`).
  Build output with rented buffers (`ArrayPool<byte>`, `ArrayBufferWriter<byte>`,
  `Utf8JsonWriter`) and the repository's own internal `System.Text.Utf8ValueStringBuilder`
  (`Common/src/System/Text/Utf8ValueStringBuilder.cs`, *linked* into the
  consuming project via `<Compile Include="$(CommonPath)System\Text\Utf8ValueStringBuilder.cs"/>`,
  as `Corvus.Text.Json.JsonLogic`/`.Toon`/`.Jsonata` do — **not** an external
  package) — not `string` concatenation. Operate on the struct value type
  `Corvus.Text.Json.JsonElement` over pooled `ParsedJsonDocument` memory; use
  `JsonElement.WriteTo(Utf8JsonWriter)` to splice values without materializing
  strings. Build-/generation-time work (e.g. parsing a runtime expression) may
  allocate; the per-step path must not. Code-generated executors bake property
  names and JSON Pointers as `"..."u8` literals so the hot path allocates nothing.
- **Measured — 0 B/op across the per-call hot path.**
  `benchmarks/Corvus.Text.Json.Arazzo.Benchmarks` (BenchmarkDotNet
  `[MemoryDiagnoser]`, plus a fast `GC.GetAllocatedBytesForCurrentThread` probe)
  confirms **0 B/op** for expression resolution + JSON Pointer, numeric/boolean
  *and string* simple criteria, JSONPath, regex, and (compiled-template)
  interpolation. Techniques: operands stay UTF-8 (JSON via `GetUtf8String()`,
  literals/scalars baked); string equality uses an ASCII fast path
  (`Ascii.IsValid` → `Ascii.EqualsIgnoreCase`) with a full-Unicode slow path
  (transcode + `OrdinalIgnoreCase`, stack/`ArrayPool`); numeric coercion via
  `Utf8Parser`; regex transcodes the UTF-8 context to a stack/pooled `Span<char>`
  for `Regex.IsMatch(ReadOnlySpan<char>)`; interpolation parses once into a
  `CompiledInterpolationTemplate` and appends via `Utf8ValueStringBuilder`.
  Scalar context collections are built once at setup (not on the hot path). A full
  BenchmarkDotNet run (net10, i7-13800H) confirms **0 allocated bytes/op** across
  all six paths, with means ≈ 8 ns (numeric criterion), 40 ns (resolve), 46 ns
  (compiled-template interpolation), 58 ns (string criterion), 65 ns (jsonpath),
  97 ns (regex).
- **Compile criteria ahead-of-time.** Regular expressions and JSONPath queries
  must be compiled once, never per step. The generated executor (.NET 10+) emits
  criteria as ahead-of-time code: JSONPath via the Corvus JSONPath source
  generator and regular expressions via `[GeneratedRegex]` — zero per-evaluation
  overhead. The runtime library's interpreted `CompiledCriterion` is the
  dynamic-mode fallback: it still compiles once (a cached `Regex`; the
  `JsonPathEvaluator` query cache) and evaluates without re-parsing.

## 4. Proposed project layout (mirrors OpenApi/AsyncApi)

| Project | Contents |
|---|---|
| `Corvus.Text.Json.Arazzo10` / `…Arazzo11` | Arazzo document **models**, generated from checked-in `Arazzo10.json` / `Arazzo11.json` schemas (exactly the `OpenApi30.json` pattern). |
| `Corvus.Text.Json.Arazzo` | **Runtime library**: `WorkflowExecutionContext`, `ArazzoRuntimeExpression` (full parser+evaluator) + `{…}` interpolation, `CriterionEvaluator` (simple/regex/jsonpath[/xpath]), simple-condition mini-parser, `WorkflowResult`/`StepResult`, control-flow signals (`End`/`Goto`/`Retry`), `IWorkflowTransportProvider` (resolves a source name → its `IApiTransport`), the **structured execution trace**, **OpenTelemetry instrumentation** (`ActivitySource`/`Meter`, BCL-only — §3.3), and `TimeProvider`/seeded-id injection for determinism (§3.2). |
| `Corvus.Text.Json.Arazzo.CodeGeneration` | The **generator**: walks an Arazzo doc + resolved source descriptions, maps steps → generated client types, emits one executor class per workflow. Coordinates with the OpenAPI/AsyncAPI generators for operationId→type mapping. |
| `Corvus.Text.Json.Arazzo.Testing` (**from the start**) | `MockApiTransport`/`MockWorkflowTransport` + response-scripting DSL, `WorkflowSimulator` (designer-UI / dry-run mode), virtual clock, in-memory OTel exporter assertions, and the conformance harness + fixtures (Arazzo spec examples). |
| CLI additions in `Corvus.Json.Cli.Core` | `arazzo-show` (operation/step tree), `arazzo-generate` (emit executors), `GeneratorConfig` extension. |

## 5. Generated artifact shape (sketch)

For a workflow `loginThenFetch` whose `inputs` schema generates `LoginThenFetchInputs`:

```csharp
public sealed partial class LoginThenFetchWorkflow
{
    // sources resolved to their generated clients via the provider
    public async ValueTask<WorkflowResult> ExecuteAsync(
        LoginThenFetchInputs inputs,
        IWorkflowTransportProvider transports,
        WorkflowOptions? options = null,
        CancellationToken ct = default)
    {
        var ctx = new WorkflowExecutionContext(inputs.AsJsonElement, options);

        // ---- step: loginStep (operationId: login on source "auth") ----
        var loginReq = new LoginRequest(
            username: ctx.Eval("$inputs.username").AsString());     // typed binding
        await using var loginResp =
            await transports.For("auth").SendAsync<LoginRequest, LoginResponse>(loginReq, ct);
        ctx.RecordResponse("loginStep", loginResp);                  // for $statusCode/$response.*
        if (!ctx.EvaluateCriteria(LoginStepSuccessCriteria, "loginStep"))
            return ctx.HandleFailure("loginStep", LoginStepOnFailure); // retry/goto/end
        ctx.SetStepOutput("loginStep", "token", ctx.Extract(loginResp, "$response.body#/accessToken"));

        // ---- step: fetchStep (implicit dependency via $steps.loginStep.outputs.token) ----
        var fetchReq = new GetDataRequest(
            authorization: ctx.Eval("$steps.loginStep.outputs.token").AsString());
        await using var fetchResp =
            await transports.For("data").SendAsync<GetDataRequest, GetDataResponse>(fetchReq, ct);
        // … criteria, outputs …

        return ctx.Complete(/* workflow outputs map */);
    }
}
```

Key points:
- **Inputs** → strongly-typed model via the existing JSON Schema codegen.
- **Parameter binding** is generated per-field: a runtime expression is evaluated
  to a JSON value, then converted to the request struct's typed field.
- **Outputs** are stored as JSON values in the context (loosely typed, since
  they cross step boundaries); `$steps.*.outputs.*` reads them back.
- **Control flow** (`goto`/`retry`/`end`) is implemented as a small generated
  state machine / labelled loop, not straight-line, once Phase 3 lands.

## 6. Phased delivery

### Phase 0 — Spike & models (foundation)
- Vendor `Arazzo10.json` / `Arazzo11.json` schemas; wire them into the model
  build the same way `OpenApi30.json` is. Produces typed Arazzo document models.
- `arazzo-show` CLI command: parse a doc, print workflows/steps/sources.
- Decide the runtime-vs-codegen split (this doc) and stub the three projects.

### Phase 1 — Runtime library (no codegen yet, fully unit-testable)
- `ArazzoRuntimeExpression`: full parser for all sources + `#/pointer` + `{…}`
  string interpolation (RFC 8259 serialization of non-scalars). Generalize from
  the existing `OpenApi.CodeGeneration.RuntimeExpression`.
- `WorkflowExecutionContext`: holds inputs, per-step request/response snapshots,
  step outputs, workflow outputs; evaluates expressions; extracts via JSON Pointer.
- `CriterionEvaluator`: `simple` (mini-parser for `== != < > <= >= && ||` over
  literals + expressions), `regex` (.NET), `jsonpath` (reuse `…JsonPath`).
  `xpath` deferred (XML-only; low priority for JSON APIs — log as unsupported).
- Result/signal types: `WorkflowResult`, `StepResult`, `EndSignal`/`GotoSignal`/`RetrySignal`.
- **Testability (from day one, §3.2):** `MockApiTransport`/`MockWorkflowTransport`,
  response-scripting DSL, `WorkflowSimulator`, `TimeProvider`/seeded-id injection,
  and the structured execution trace — all unit-tested before any codegen exists.
- **Observability (from day one, §3.3):** `ActivitySource`/`Meter` instrumentation
  of `WorkflowExecutionContext`, plus in-memory OTel exporter test helpers so the
  span tree is assertable from the outset.

### Phase 2 — Code generator MVP (OpenAPI, happy path)
- **Compute the referenced-operation set** across all workflows/steps and drive
  the OpenAPI `OperationFilter` so only those operations (and their reachable
  schema models) are generated — see §3.1. This shapes the generator↔generator
  contract, so build it in from the start.
- Resolve `sourceDescriptions` of `type: openapi` to generated client types;
  build the `operationId → (TRequest, TResponse, source)` map by coordinating
  with the OpenAPI generator's operation model (reuse its naming/schema-pointer
  logic rather than re-deriving names).
- Emit one executor per workflow: sequential steps, parameter binding, request
  send, `successCriteria` gate, `outputs` extraction, workflow `inputs`/`outputs`.
- `IWorkflowTransportProvider` to supply an `IApiTransport` per source name.
- **Every generated executor emits spans/trace and runs green under the mock
  transport in tests from day one** (§3.2/§3.3) — testability and telemetry are
  acceptance criteria for the MVP, not a later phase.

### Phase 3 — Control flow & reuse
- `onSuccess`/`onFailure` actions: `end`, `goto` (step/workflow), `retry`
  (`retryAfter`, `retryLimit`); workflow-level `successActions`/`failureActions`
  as defaults. Generate a labelled-loop / state-machine executor.
- `dependsOn` ordering; sub-workflow steps (`workflowId`); workflow `outputs`
  surfaced via `$workflows.<id>.outputs.*`.
- `components` + `Reusable` (`$components.*` resolution, parameter overrides).

### Phase 4 — Bodies, selectors, source resolution
- `requestBody` (`contentType`, `payload`, `replacements[]` Payload Replacement
  via JSON Pointer/JSONPath/XPath targets) → map onto the transport body overloads.
- `operationPath` (JSON Pointer into a source doc) as an alternative to `operationId`.
- Selector Objects (1.1) for outputs/parameters (jsonpath/xpath/jsonpointer).
- Source fetching + caching (remote `url`), reusing the `OpenApiLockFile`
  incremental-regen pattern; an Arazzo lock file.

### Phase 5 — AsyncAPI steps
- `channelPath` + `action` (send/receive), `correlationId`, `timeout`,
  `$message.header/payload#…` expressions. Bridge to the AsyncAPI generated
  producers/consumers and protocol bindings; reuse `AsyncApiRuntimeExpression`.

### Phase 6 — Polish & productionization
- `arazzo-generate` CLI + `GeneratorConfig` integration (build-time generation).
- **Conformance suite asserts telemetry** (span tree shape, correlation,
  attributes) alongside functional outcomes via the in-memory exporter — building
  on the testability/observability foundations laid in Phases 1–2 (§3.2/§3.3).
- Resilience (`…AsyncApi.Polly`-style retry/circuit-breaker over steps),
  exporter/dashboard wiring docs, structured logging.
- Workflow-designer integration sample built on `WorkflowSimulator`.
- `docs/Arazzo.md` user guide + `docs/CodeSampleCatalog.md` entries; samples.

### Phase 7 — Fully-static executor (drop the interpreter from generated code)

A performance phase that removes the remaining interpreter indirection and per-run
structural allocation from the **generated** executor. (This is "Option B" from the
generated-executor allocation review; Phase 2 shipped "Option A", which already
resolves `$steps.<id>.outputs` statically and emits **no runtime dictionaries** — a
one-step run is ~700 B/op, all of it the per-run `WorkflowExecutionContext` object
plus the output-document builders; the per-evaluation hot path is already 0 B/op.)

Goal: the generated executor uses **no `WorkflowExecutionContext`, no
`ArazzoExpression`, and no `CompiledCriterion`** at run time — every runtime
expression and criterion is compiled to direct C# at generation time.

Scope:
- **Expression → accessor compiler.** Resolve every runtime-expression source to a
  direct local/parameter accessor, decided at generation time:
  - `$inputs.<name>#/ptr` → navigate the `inputs` parameter;
  - `$steps.<id>.outputs.<name>#/ptr` → navigate the step's outputs local (already done in Option A);
  - `$response.body#/ptr` → navigate the current step's response body local (e.g. `getPetResponse.OkBody`);
  - `$statusCode` → `getPetResponse.StatusCode` (an `int`, not routed through a `JsonElement`);
  - `$response.header.<n>` / `$request.{header,query,path}.<n>` / `$url` / `$method` → the request/response accessors directly;
  - `$message.{header,payload}` → the AsyncAPI message accessors.
- **Criteria as native code.** Emit `successCriteria` (and `onSuccess`/`onFailure`
  `criteria`) as native C# conditions rather than `CompiledCriterion.Evaluate`:
  - `simple` → native comparisons that **faithfully reproduce** the spec semantics
    already implemented and tested in `CompiledCriterion`/`Comparand`
    (ASCII/Unicode case-insensitive string compare, numeric-string coercion for
    `< <= > >=`, `null`-equals-only-`null`, lone-expression truthiness);
  - `regex` → `[GeneratedRegex]` partial methods (source-generated, AOT-friendly);
  - `jsonpath` → the JSONPath source generator / a compiled query;
  - `xpath` → deferred with the rest of XPath.
- **Drop the per-run context object** entirely (its only remaining job after Option A
  is feeding criteria and current-step value resolution; both become direct code).

Outcome: per-run allocation reduced to just the output-document builders (structural;
further reducible only by `JsonWorkspace` pooling the builder objects), and faster
execution (no virtual/delegate indirection, AOT regex/jsonpath).

Risks / why it's a separate phase:
- It **re-implements semantics that already exist and are tested** in the interpreter
  (`CompiledCriterion`, `Comparand`, `ArazzoExpression`), in a second place — so the
  full criterion/expression edge-case suite must be run against the **generated**
  output (e.g. via the end-to-end emit→compile→run harness) to guarantee no
  divergence from the interpreter.
- Larger surface (regex/jsonpath AOT integration; pointer-navigation codegen).
- Best done incrementally: `simple` criteria → native conditions first (common, easy),
  then current-step value accessors, then regex/jsonpath AOT.

Tracking: the matrix's "AOT-compiled criteria in generated executor" row is the
criteria half of this phase.

## 7. Key risks & open questions

1. **Static↔dynamic bridge** (the core risk): coordinating the Arazzo generator
   with the OpenAPI/AsyncAPI generators to get exact generated type names per
   `operationId`. Mitigation: reuse the OpenAPI generator's internal operation
   model rather than duplicating naming heuristics; generate Arazzo + clients in
   one coordinated pass.
2. **operationId uniqueness** across multiple sources — must qualify by source.
   `operationPath` avoids ambiguity but needs JSON-Pointer resolution into the
   source doc.
3. **Loosely-typed step outputs** — values crossing step boundaries are JSON, not
   schema-typed, unless we also resolve each output's schema. Start loose
   (JSON values), optionally tighten later by inferring output types from the
   operation response schema.
4. **`simple` criterion grammar** — Arazzo's "simple" condition is underspecified
   in edge cases; needs a small, well-tested expression parser with clear
   precedence/semantics. Pin behavior with conformance tests.
5. **XPath-over-JSON** — XPath/`xpath` criterion type targets XML; low priority
   for JSON APIs. Support JSON Pointer + JSONPath first; gate XPath behind XML
   content types or mark unsupported.
6. **`goto`/`retry` control flow** — straight-line emission breaks; needs a
   generated state machine. Design the executor shape for this from Phase 2 even
   though branching lands in Phase 3.
7. **Concurrency / `dependsOn`** — spec is sequential-by-default with `dependsOn`
   for async coordination; decide whether to parallelize independent steps or
   stay strictly sequential initially (recommend sequential first).
8. **Expression-eval safety** — runtime expressions and `simple` conditions are
   evaluated against untrusted response data; ensure no injection beyond JSON
   value extraction, bounded regex (timeout), bounded retries.
9. **Runtime "dynamic mode"** — is build-time codegen sufficient, or do we need
   to execute Arazzo docs not known until run time? If the latter, scope a
   separate interpreted engine (option B) as a follow-up.
10. **Telemetry correlation across the transport boundary** — *assessed; low
    risk.* End-to-end spans need W3C trace-context propagation into outgoing
    requests. Findings:
    - **AsyncAPI: no gap.** `AsyncApiTelemetry` (`ActivitySource`/`Meter`
      `Corvus.AsyncApi`), `InstrumentedMessageTransport` (span + metrics
      decorator), and `TraceContextPropagator` (W3C inject/extract via message
      headers) already exist and are reusable as-is.
    - **OpenAPI: small, bounded gap.** `HttpClientTransport` has *no* custom
      instrumentation today. But (a) propagation into called HTTP APIs is largely
      **free** — .NET's built-in `System.Net.Http` instrumentation auto-injects
      `traceparent`/`tracestate` when an `Activity` is current at send time, which
      it will be (the engine's step span), so this is mainly a registration
      concern (`AddHttpClientInstrumentation()`); and (b) a first-class,
      attribute-rich OpenAPI *operation* span needs only ~2 small files — an
      `InstrumentedApiTransport` decorator over `IApiTransport` plus an
      `OpenApiTelemetry` class — a near-mechanical copy of the AsyncAPI pattern,
      living in `…OpenApi.HttpTransport` and benefiting all OpenAPI client users.
    - The Arazzo runtime owns the workflow/step spans (`ArazzoTelemetry`
      `ActivitySource`); operation/request spans nest beneath via `Activity.Current`.
11. **Mock fidelity** — request matching in the mock transport must consume the
    *same* `Write*` serialization path as the real transport, or mocked call
    paths won't faithfully reflect production. Build the mock against the
    production request-writing contract, not a parallel implementation.
12. **Attribute cardinality / PII** — telemetry attributes drawn from params and
    bodies risk high cardinality and leaking sensitive data; provide redaction
    hooks and bounded cardinality by default.

## 8. Recommended next step

Start with **Phase 0 + Phase 1**: vendor the Arazzo schemas to generate the
document models, ship `arazzo-show`, and build the standalone, fully
unit-tested runtime library (expression evaluator + criterion evaluator +
execution context). These are low-risk, high-leverage, and de-risk the hard
parts (expression/criterion semantics) before committing to the generator
integration in Phase 2.

## 9. Durability execution model — checkpoint & resume

This is the load-bearing design the store (§10) and control plane (§11) sit on:
*how the reification-free generated executor persists its state and resumes* —
both for crash recovery (**Tier 1**) and long-running suspension (**Tier 2**).

### 9.1 Principle — the products *are* the checkpoint

The executor only ever constructs the genuine products (each step's `outputs`
`JsonElement` and the workflow `outputs`). Combined with a tiny scalar cursor,
those products are the *entire* resumable state — so a checkpoint is almost free:
serialize JSON that already exists. Nothing else (no reified expression context,
no interpreted plan) needs persisting, because the generated executor *is* the
plan.

The as-built executor (supersedes the §5 sketch) is a static method whose control
flow is a labelled `while(true){ switch(__state){ … } }` loop; per-step outputs
are `JsonElement` locals hoisted at method scope (`{step}OutputsElement`), with
hoisted retry counters and, for AsyncAPI correlation, a `correlationTokens`
register. The complete resumable state is therefore exactly:

- `__state` — the cursor (which `case` runs next);
- each hoisted step-`outputs` `JsonElement`;
- each retry counter; the correlation register;
- the workflow `inputs` (a `JsonElement`).

### 9.2 The checkpoint document (one JSON doc per run)

```
{ "runId", "workflowId", "status",          // Pending|Running|Suspended|Completed|Cancelled|Faulted
  "cursor",                                  // __state
  "retryCounters": { "<stepId>": n },
  "correlationTokens": { "<name>": "<token>" },
  "inputs":  <json>,
  "stepOutputs": { "<stepId>": <json> },     // the built outputs products
  "wait":  { "kind": "timer", "dueAt": "…" } // Tier 2: why it is Suspended
         | { "kind": "message", "channel": "…", "correlationId": "…" },
  "fault": { "stepId", "attempt", "error", "at" },
  "history": [ … ],                          // lifecycle + management audit
  "etag" }                                   // optimistic concurrency
```

The step-output `JsonElement`s and inputs serialize natively; everything else is
scalar. The whole doc is small and backend-agnostic (§10).

### 9.3 Generated-executor mechanism (opt-in codegen)

Durability is a **generation option** (`WorkflowExecutorOptions.Durable`), so the
default executor keeps its 0-alloc hot path with no checkpoint plumbing. When
enabled, the emitter generates a second shape that threads an `IWorkflowRun? run`
and returns a tri-state result (see 9.4):

- **On entry — restore.** Locals initialise from the run instead of `default`:
  `int __state = run?.Cursor ?? 0;`,
  `JsonElement getPetOutputsElement = run is not null && run.TryLoadOutputs("getPet", out var v) ? v : default;`,
  retry counters and `correlationTokens` likewise. A fresh run loads nothing and
  behaves exactly like today.
- **After each step — checkpoint.** Once a step's outputs local is assigned and
  the next cursor chosen, before the loop iterates:
  `run?.SetOutputs("getPet", getPetOutputsElement);`
  `if (run is not null) { await run.CheckpointAsync(__state, ct); }`
  The `run` object owns serialization (products → JSON), the store write, and
  optimistic-concurrency/lease — the generated code stays declarative. Every
  step-id and output local is known at gen time, so save/restore are fully static
  (no reflection, no dynamic dispatch).

**Resume is just re-calling the method** with a loaded `run`: the `while/switch`
loop jumps to the restored cursor with its locals already populated and carries
on. No separate "resume entrypoint" is generated.

> **Tier 1 — implemented.** The crash-recovery slice of 9.3/9.6 is built. The
> `Durable` option forces the labelled-loop state machine and threads an optional
> `IWorkflowRun? run` into `ExecuteAsync`; on entry `__state`, each step-outputs
> local, each retry counter, and the `correlationTokens` register restore from the
> run; after each step the run stages the products (`SetStepOutputs`/`SetRetryCount`)
> and writes a checkpoint at the chosen cursor; on completion the run records the
> terminal checkpoint with the final outputs (`CompleteAsync`). The as-built Tier-1
> seam is slightly leaner than the 9.6 sketch — it omits the Tier-2-only members
> (`TryGetCorrelationToken`, `Suspend<T>`/`WorkflowRunResult<T>`), exposes the
> correlation register directly as `CorrelationTokens` (the executor reads and
> mutates it in place), and adds `SetRetryCount` + `CompleteAsync`. The runtime
> seam lives in `Corvus.Text.Json.Arazzo`; the store (`IWorkflowStateStore`), the
> per-run `WorkflowRun`, the checkpoint serializer, and the in-memory reference
> store live in `Corvus.Text.Json.Arazzo.Durability`. A generated durable executor
> resumes after an interrupted run and finishes without re-running completed steps,
> their outputs restored into the final result (end-to-end test).

### 9.4 Tri-state result and suspension (Tier 2)

A durable executor returns `WorkflowRunResult<TOutputs>` =
`Completed(outputs) | Faulted(record) | Suspended(wait)` (the non-durable
executor still returns `ValueTask<TOutputs>` unchanged). **Suspension boundaries**
are the points where a run may wait indefinitely:

- a **timer** — a `retry` with `retryAfter`, or any future workflow-level wait:
  instead of `await Task.Delay(...)`, the executor checkpoints `status=Suspended`,
  `wait={timer, dueAt}` and **returns** `Suspended`;
- a **correlated receive** — an AsyncAPI receive (esp. `correlationId`): instead
  of blocking on `ReceiveOneAsync`, it checkpoints `wait={message, channel,
  correlationId}` and returns `Suspended`.

A **worker** (the trigger loop) resumes a suspended run when its wait is
satisfied — a due timer or a matching delivered message — by loading the
checkpoint and re-calling `ExecuteAsync(…, run: loaded)`; for a message wait the
delivered payload is handed in so the receive step completes immediately. Tier 1
ignores suspension entirely (timers stay in-process `Task.Delay`, receives block)
and only uses 9.3's checkpoint-after-step for crash recovery — so Tier 2 is a
pure superset that adds the tri-state return + the wait index (§10) + the worker.

> **Tier 2 — implemented.** The full superset is built. A durable executor returns
> `WorkflowRunResult<TOutputs>`; a step failure returns `Faulted` (recoverably
> persisted), a `retry` with a delay suspends on a durable timer (`SuspendForTimerAsync`),
> and a correlated/plain AsyncAPI receive suspends on a message wait
> (`SuspendForMessageAsync`) — all only when a run is present; with a `null` run the
> executor keeps the Tier-1 behaviour (`Task.Delay`, blocking `ReceiveOneAsync`,
> throw on fault) and only ever completes. The run owns the clock (it computes
> `dueAt` and stamps fault times) and the message handoff (`TryTakeDeliveredMessage`/
> `DeliverMessage`). The store's optional `IWorkflowWaitIndex` (`QueryDueAsync`,
> `QueryAwaitingAsync`, and the §11 visibility `QueryAsync`) is implemented on the
> in-memory store, and a `WorkflowWorker` polls it, takes a per-run lease, loads the
> checkpoint, hands in any delivered message, and re-enters the generated executor
> through a host-supplied `WorkflowResumer`. End-to-end: a run suspends on a timer or
> a receive and a worker resumes it to completion. Responders and dynamic/parameterised
> receive addresses keep the Tier-1 blocking path (no suspension) for now.

### 9.5 Delivery semantics

Checkpoint-after-step gives **at-least-once** step execution: a crash *after* a
step's side effect but *before* its checkpoint persists re-runs that whole step on
resume. Steps should therefore be idempotent, or rely on correlation /
server-side idempotency keys — the same contract every checkpointing engine
(DTFx, Temporal) carries. We document this and surface an idempotency-key hook on
operation steps as a later refinement. Optimistic concurrency (ETag) plus a
single-owner lease (§10) prevent a horizontally-scaled fleet from double-advancing
a run.

### 9.6 The `IWorkflowRun` seam

```csharp
public interface IWorkflowRun
{
    int Cursor { get; }
    bool TryLoadOutputs(string stepId, out JsonElement outputs);
    int RetryCount(string stepId);
    bool TryGetCorrelationToken(string name, out ReadOnlySpan<byte> token);

    void SetOutputs(string stepId, in JsonElement outputs);           // stage products for the next checkpoint
    ValueTask CheckpointAsync(int cursor, CancellationToken ct);       // serialize + persist (CAS/lease)
    WorkflowRunResult<T> Suspend<T>(in WorkflowWait wait);             // Tier 2: persist Suspended + return
}
```

`IWorkflowRun` is the only new runtime seam the generated code touches; it is
backed by an `IWorkflowStateStore` (§10) and constructed by the host/worker. The
in-memory implementation (shipped in the runtime/testing layer) makes the whole
mechanism unit-testable with no external store, exactly as `InMemoryMessageTransport`
does for AsyncAPI — including a crash-and-resume test that drops the run after step
N and re-enters from the checkpoint.

### 9.7 Telemetry

Checkpoint save/load emit spans under the workflow span; `suspended`/`resumed`/
`checkpoint` counters join the existing `ArazzoTelemetry` meters. The execution
trace already *is* the run history (9.2).

## 10. Phase 6 — out-of-process durability store packages

The durability layer (§9) needs a pluggable
`IWorkflowStateStore`. Beyond the in-memory default, which out-of-process
backends should we ship? This section captures the research.

### What the engine actually needs from a store

A checkpoint is a single JSON document keyed by a workflow run id, plus, for
long-running suspension (Tier 2), an index to find runs that are *due* (durable
timers / `retryAfter`) or *awaiting a correlation id* (AsyncAPI receive). So a
backend must provide:

1. **Key/value by run id** — save/load the checkpoint JSON. (All backends do this.)
2. **Optimistic concurrency** — ETag/version, so a horizontally-scaled fleet never double-advances a run.
3. **Single-owner lease** — only one worker resumes a given run.
4. **A due/correlation index** *(Tier 2 only)* — query "runs due before T" and "run awaiting correlation X".

Requirements 1–3 are easy everywhere; (4) is the discriminator, and different
backends satisfy it very differently (SQL indexed column; Cosmos query +
change-feed; Redis sorted-set scored by due-time; Azure Table partitioned by
time-bucket; or *no* store query at all — durable timers as Service Bus
scheduled messages). We therefore split the abstraction: a core
`IWorkflowStateStore` (1–3) that any backend can implement, plus an optional
`IWorkflowWaitIndex` / timer capability (4) that richer backends add. A
blob-only store can implement the core and delegate timers to scheduled messages.

### The abstraction (interfaces & seam)

The generated executor only ever sees `IWorkflowRun` (§9.6); the **store is a
host-level concern**, wired at startup and referenced by nothing generated. Two
interfaces split by capability:

```csharp
// Universal — trivial on every backend.
public interface IWorkflowStateStore
{
    // create-or-update under optimistic concurrency; returns the new etag (conflict if `expected` is stale).
    ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8,
                                      in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken ct);
    ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken ct);   // bytes + etag
    ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken ct);
    ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken ct);
    ValueTask DeleteAsync(WorkflowRunId id, CancellationToken ct);
}

// Optional — richer backends add it (or a *separate* package provides it).
public interface IWorkflowWaitIndex
{
    IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, CancellationToken ct);             // timers
    IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(ReadOnlyMemory<byte> channel,
                                                       ReadOnlyMemory<byte> correlationId, CancellationToken ct); // msg wakeups
    ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken ct);                       // §11 visibility
}
```

- **Capability negotiation** is `store is IWorkflowWaitIndex`. A blob-only store
  implements just the core and delegates timers to Service Bus scheduled messages;
  SQL/Cosmos/Redis implement both. The same index serves Tier-2 wakeups *and* the
  §11 control-plane queries — one mechanism, not two.
- **Backends never parse the checkpoint.** The runtime serialises products → JSON
  and projects a tiny `WorkflowRunIndexEntry` — `{ status, workflowId, createdAt,
  updatedAt, dueAt?, awaitingChannel?, awaitingCorrelationId?, errorType?, tags }`
  — alongside the opaque checkpoint bytes. A backend is a thin adapter: *store
  these bytes by id at this etag; index these few fields.* That is what keeps each
  backend cheap. (JSON-native stores *may* keep the checkpoint as a queryable
  document, but never need to — the index entry covers every query.)
- **Concurrency is contract, mapping is adapter.** The interface fixes the
  semantics (save fails on etag mismatch → `WorkflowConflict`; lease is advisory
  single-owner with TTL); each backend maps them to its native primitive (blob
  ETag + lease; Postgres `xmin`/version column + advisory lock; SQL Server
  `rowversion` + app lock; MySQL version column; Cosmos `_etag`; Mongo version
  field; Redis `WATCH`/Lua + sorted-set timer index).
- **Composition (DI).** `services.AddArazzoDurability().UsePostgres(conn)`; store
  and index can come from *different* packages (e.g. blob checkpoints + Redis wait
  index), composed at registration. The worker/host resolves `IWorkflowStateStore`
  (+ optional `IWorkflowWaitIndex`) and builds the per-run `IWorkflowRun`.
- **One conformance suite.** A shared store-conformance test suite (the
  AsyncAPI-transport-tests pattern) — CAS conflict, lease contention, due-query,
  correlation-query, round-trip — runs against every backend's real container
  (the WSL+Podman broker harness applies directly). The in-memory store is the
  reference implementation and runs the same suite.

### What the closest .NET analogs ship (evidence)

| Engine | Persistence backends shipped |
|---|---|
| Durable Task Framework / Durable Functions | **Azure Storage** (blobs + tables + queues, blob leases for partitions); **MSSQL** (`microsoft/durabletask-mssql`); Netherite (Event Hubs + FASTER — *being retired ~2028, do not emulate*); plus the new managed *Durable Task Scheduler* |
| Elsa Workflows 3 | **EF Core** (SQL Server, PostgreSQL, SQLite, MySQL); **MongoDB**; **Dapper** |
| MassTransit sagas | **EF Core** (SQL); **MongoDB**; **Redis**; **Marten** (PostgreSQL); NHibernate; **Azure Cosmos DB** (Mongo + Document APIs) |
| Temporal | **PostgreSQL**, **MySQL**, **Cassandra** (core); SQLite / Elasticsearch (visibility) |

The consensus is clear: **relational (PostgreSQL + SQL Server, + MySQL)**, a
**document store (Mongo / Cosmos)**, **Redis**, and — in the Azure ecosystem
specifically — **Azure Storage** are the backends customers expect. But note the
analogs reach relational via EF Core; **we deliberately do not.** The store is a
thin checkpoint table (an opaque blob column + a handful of indexed projection
columns + CAS + a lease), so an ORM adds dependency weight, a migrations
framework, and an extra mapping layer for no benefit — and it would obscure the
per-dialect concurrency primitive (`xmin` / `rowversion`) we depend on. We ship
**direct ADO.NET adapters per database** instead (the Dapper/`durabletask-mssql`
lineage), each owning its driver, its SQL dialect, and a `CREATE TABLE IF NOT
EXISTS` schema script — no EF, no migrations runtime.

### Recommended set (naming mirrors the AsyncAPI binding convention)

- `Corvus.Text.Json.Arazzo.Durability` — abstractions + **in-memory** default (in the testing/runtime layer).
- `Corvus.Text.Json.Arazzo.Durability.AzureStorage` — Blob for the checkpoint (with **blob leases** for single-owner), Table for the wait/correlation index; durable timers via **Service Bus scheduled messages** (reuses the existing ASB binding) or Storage Queue visibility delays. *Best fit for Corvus' Azure lean.* SDKs: `Azure.Storage.Blobs`, `Azure.Data.Tables`, `Azure.Messaging.ServiceBus`.
- **Relational — direct ADO.NET adapters, one package per database (no EF/ORM).** Each stores the checkpoint in a JSON/`JSONB`/`nvarchar(max)` column with the projection fields as indexed columns, maps CAS + lease to the native primitive, and ships a `CREATE TABLE IF NOT EXISTS` script (idempotent, no migrations runtime). They can share a small internal SQL helper but stay separate so each pulls only its own driver:
  - `Corvus.Text.Json.Arazzo.Durability.Postgres` — **the relational default.** `Npgsql`; checkpoint as **`JSONB`** (queryable if ever needed), wait index as indexed columns, CAS via a version column / `xmin`, single-owner via **advisory locks**. Postgres's JSON-native fit makes it the strongest on-prem/portable option.
  - `Corvus.Text.Json.Arazzo.Durability.SqlServer` — `Microsoft.Data.SqlClient`; `nvarchar(max)`/`json` column, CAS via **`rowversion`**, lease via `sp_getapplock` (the `durabletask-mssql` lineage). Covers **SQL Server *and* Azure SQL Database** (same driver/wire protocol — just a connection string) and Azure SQL Managed Instance.
  - `Corvus.Text.Json.Arazzo.Durability.MySql` — `MySqlConnector`; `JSON` column, CAS via a version column. Covers **MySQL, MariaDB, and Aurora MySQL**.
  - `Corvus.Text.Json.Arazzo.Durability.Sqlite` — `Microsoft.Data.Sqlite`; single-file, zero-setup **local-dev / embedded** option (the in-memory store is for tests; SQLite is for a real on-disk single-node run).

  **Compatibility families (a direct-driver dividend).** Because each adapter speaks its driver's wire protocol rather than an ORM dialect, one package covers an entire engine family at no extra cost: the **Postgres** adapter also serves **CockroachDB, YugabyteDB, AlloyDB, Aurora PostgreSQL, Neon, and Citus**; **SqlServer** also serves **Azure SQL Database / Managed Instance**; **MySql** also serves **MariaDB / Aurora MySQL**. So "PostgreSQL + SQL Server + MySQL" adapters already reach most of the managed-relational market.
- `Corvus.Text.Json.Arazzo.Durability.Cosmos` — JSON-native (stores the checkpoint document directly and queries it), **change feed** for timer/trigger dispatch, TTL. SDK: `Microsoft.Azure.Cosmos`.
- `Corvus.Text.Json.Arazzo.Durability.Mongo` — ubiquitous document store; optimistic concurrency via a version field (the MassTransit/Elsa pattern). SDK: `MongoDB.Driver`.
- `Corvus.Text.Json.Arazzo.Durability.Redis` — KV + **sorted-set timer index** (score = due-time) + streams for wake-ups; excellent as a *wait/timer index* even alongside another primary store. SDK: `StackExchange.Redis`.
- `Corvus.Text.Json.Arazzo.Durability.NatsJetStream` — reuses the **NATS** infrastructure the AsyncAPI side already ships: a JetStream **KV bucket** for checkpoints (native revisions = CAS), and JetStream consumers / a KV watch for timer + correlation wake-ups. Low marginal cost when NATS is already the broker, and keeps the wake-up path on the same bus as the messages. SDK: `NATS.Client` (already referenced).
- *(Optional / on demand)*: `…Durability.DynamoDb` (KV + TTL + streams) and `…Durability.Firestore` (cross-cloud); `…Durability.Cassandra` (wide-column, the Temporal-core lineage; also ScyllaDB).

**No separate search / analytics "visibility store."** Visibility queries (§11)
are served by the **authoritative store's own `IWorkflowWaitIndex`** — SQL indexed
columns, Cosmos/Mongo fields, Redis, or a blob store's companion Azure Table — so
the one index already answers timers, correlation wake-ups, *and* operator
queries. We considered a dedicated visibility layer (the Temporal
state-vs-visibility split) backed by **Elasticsearch/OpenSearch** or an analytical
column store such as **DuckDB**, and **decided against it**: it adds a second
system to operate and keep in sync for query shapes the rich primary stores
already handle. (The authoritative store is OLTP — frequent small point-writes +
CAS + fleet concurrency — which is also why analytical/columnar engines like
DuckDB are not candidates there: SQLite is the embedded authoritative choice.)

### Suggested shipping order

1. **First wave:** abstractions + in-memory; **Postgres** (relational default, JSONB); **SQLite** (local-dev/embedded); **Azure Storage** (Azure-native). Covers portable/on-prem, local, and Azure.
2. **Second wave:** **SQL Server**; **Cosmos**; **Redis**.
3. **Third wave / on demand:** **MySQL**; **MongoDB**; DynamoDB; Firestore.

Each is a thin adapter over the same JSON checkpoint, so adding a backend is
low cost; we ship the first wave with the durability feature and add the rest
based on demand.

## 11. Faulting, resume, and workflow management (control plane)

Durability is only useful if operators can see and act on stuck runs. The engine
needs a **control plane**: fault a run, query faulted/suspended runs, and resume
(or remediate, cancel) them. This is the workflow-level analogue of a
dead-letter queue — and the AsyncAPI side already models DLQ inspection and
redelivery (`RecordDeadLetter`, dead-letter channels), so the shape is familiar.

### Run lifecycle

Every run has a persisted status, stored in its checkpoint:

`Pending → Running → { Suspended (awaiting timer/message) | Completed | Cancelled | Faulted }`

A run **faults** when a step errors and the workflow's own `failureActions`
don't resolve it — `retryLimit` exhausted with no `goto`/`end`, an unhandled
operation/transport error, a failed `successCriteria` with no handler, or an
input/schema validation failure — or on an infrastructure error mid-execution.
Faulting is distinct from a *clean* failure (`failureActions: end`): a clean end
is terminal-by-design; a fault is **terminal-but-recoverable**, awaiting action.

The persisted **fault record** captures: the faulted `stepId`, attempt count,
the error (type/message, and which runtime expression or criterion failed), the
timestamp (from the injected `TimeProvider`), a `retriable` hint, and the pointer
to the last good checkpoint. Faulting emits an error span + a `faulted` metric
counter (alertable), and the fault detail is appended to the run's history (the
execution trace *is* the history).

### Visibility index (querying)

Following Temporal's split of authoritative state vs. *visibility*, the
checkpoint store holds the source of truth while a **queryable index** answers
management queries. This is the *same* index Tier 2 uses to find due/awaiting
runs (§10) generalized to carry: status, `workflowId`, created/updated/faulted
timestamps, error type, and user tags. Rich backends (SQL/Cosmos/Mongo) make
this columns/fields; a blob-only store gets a companion Azure Table. So one
index serves timers, correlation wake-ups, *and* operator queries.

### Management API

A control-plane client over the store + the same worker-trigger channel used for
Tier 2 resume:

```csharp
public interface IWorkflowManagementClient
{
    // Query / visibility
    ValueTask<WorkflowRunPage> ListAsync(WorkflowQuery query, CancellationToken ct);   // filter by status, workflowId, time range, error type, tags; paged
    ValueTask<WorkflowRunDetail?> GetAsync(WorkflowRunId id, CancellationToken ct);     // status + fault detail + history/trace

    // Control
    ValueTask<bool> ResumeAsync(WorkflowRunId id, ResumeOptions options, CancellationToken ct);
    ValueTask<bool> CancelAsync(WorkflowRunId id, string reason, CancellationToken ct);
    ValueTask<bool> SuspendAsync(WorkflowRunId id, CancellationToken ct);               // and UnsuspendAsync
    ValueTask<int>  PurgeAsync(WorkflowPurgeQuery query, CancellationToken ct);          // reap old completed/cancelled
}
```

### Resume / remediation options for a faulted run

`ResumeOptions` covers the operator intents (each is: load checkpoint → mutate
status/cursor/state under optimistic concurrency → enqueue for a worker):

- **Retry the faulted step** — reset the cursor to the faulted step, clear its
  partial state, re-execute. (The common case.)
- **Resume from an earlier step (rewind)** — set the cursor back, discard outputs
  after it, re-run. (cf. Temporal *reset* / Durable Functions *rewind*.)
- **Skip the faulted step** — mark it skipped (empty or operator-supplied
  outputs), advance. (Only safe when downstream doesn't need its outputs.)
- **Resume with a state patch** — apply a **JSON Patch** (reuse
  `Corvus.Text.Json.Patch`) to the persisted context to fix a bad input/output,
  then retry. Powerful for manual remediation.
- **Cancel** — mark `Cancelled`.

All mutations use the store's optimistic concurrency (ETag/version) and take a
lease, so concurrent operators — or an operator and a worker — can't conflict.
Every management action is appended to the run history for **audit** (who, when,
what patch/reason).

### Phasing

- **Phase 3** (with Tier 1 durability + the state machine): introduce the run
  lifecycle, the `Faulted` state + fault record, and basic `ResumeAsync`
  (retry-faulted-step) and `CancelAsync`.
- **Phase 6** (productionization): the full `IWorkflowManagementClient` with the
  visibility index, rich queries, rewind/skip/state-patch resume, purge, audit
  trail, and a CLI surface (e.g. `arazzo-runs list --status faulted`,
  `arazzo-runs resume <id>`). Conformance tests assert the fault → query →
  resume → complete cycle, including the emitted telemetry.
