# Corvus.Text.Json.RuntimeEvaluator — design notes

A runtime (no code generation) JSON Schema evaluator built on `Corvus.Text.Json`. It is the spike for the
V5 "standalone evaluator" replacement: compile a schema once into an in-memory program, then evaluate any
`IJsonElement<T>` against it with zero allocations in flag mode, or with full results/annotations through
the existing `JsonSchemaResultsCollector` / `JsonSchemaAnnotationProducer`.

## Layout

| Path | Purpose |
|---|---|
| `src/Corvus.Text.Json/Corvus/Text/Json/RuntimeEvaluator` | Part of the `Corvus.Text.Json` assembly, namespace `Corvus.Text.Json.RuntimeEvaluator`. Public surface: `JsonSchemaEvaluator`, `JsonSchemaEvaluatorOptions`, `JsonSchemaDialect`, `JsonSchemaDocumentResolver`. |
| `src/.../Compilation` | Loader (documents, resources, `$id`/anchors), compiler (node graph), node model. |
| `src/.../Evaluation` | The engine: `Evaluator` (generic over `FastMode` / `CollectingMode`), `EvaluationState`. |
| `tests/...Tests/Suite` | JSON-Schema-Test-Suite runner (validation + annotations), one MSTest per suite file. |
| `tests/...Tests/Unit` | Focused tests for references, keywords, results collection. |
| `benchmarks/...Benchmarks` | BenchmarkDotNet: the 37 Sourcemeta cases (generated model vs runtime evaluator) and cold start. |

The library links `EcmaRegexTranslator.cs` from `Corvus.Text.Json.CodeGeneration` as shared source and
embeds the standard metaschemas from its `metaschema` folder.

## Compilation (cold start)

1. **Load**: parse the root document with `ParsedJsonDocument<JsonElement>` (a private copy of the bytes),
   walk it once, dialect-aware (`SchemaKeywords`), registering resources (`$id` / `id`), anchors
   (`$anchor`, `$dynamicAnchor`, legacy `#plain-name` ids), `$recursiveAnchor`, and the resource that owns
   every visited schema object. Draft ≤7 objects containing `$ref` ignore their siblings, including `$id`.
   `$schema` is resolved to a dialect and a vocabulary set; unknown metaschemas are loaded (via the resolver
   or the embedded set) and their `$vocabulary` read, so custom metaschemas without the validation vocabulary
   or with `format-assertion` behave correctly.
2. **Resolve**: `$ref` targets resolve at compile time to node ids (URI resolution with `System.Uri`, JSON
   pointer fragments via `TryResolvePointer`, plain-name fragments via the anchor tables). Remote documents
   come from `JsonSchemaEvaluatorOptions.DocumentResolver`.
3. **Compile**: each distinct `(document, element)` becomes one `SchemaNode` with pre-digested keyword data:
   a `TypeMask`, normalized decimal bounds (`NumberValue`), a normalized divisor (`DivisorValue`), a
   `PatternMatcher` (noop/non-empty/prefix/range fast paths or a translated `Regex`), a `Utf8NameMap`
   for `properties` (which also carries the presence bits used by `required`, `dependentRequired`,
   `dependentSchemas` and `dependencies`), and `ChildRef`s carrying the evaluation-path segment bytes.
   Annotation-only keywords keep their raw JSON bytes for verbose output.
4. **Dynamic references**: `$dynamicRef` and `$recursiveRef` are resolved statically first. Only when the
   initial target carries the matching `$dynamicAnchor` (or `$recursiveAnchor: true`) is the reference
   dynamic: a table `resourceId -> nodeId` is built over all loaded resources (iterated to a fixpoint, since
   compiling candidates may load more documents). If only one resource defines the anchor, the reference is
   demoted to a static `$ref`. So is a reference whose anchor every entry resource that can reach it defines with
   the same target: the scope is searched outermost-first and its outermost entry is always the resource evaluation
   started in, so that target is the answer on every path (the strict-tree shape). Entries that cannot reach the
   reference never evaluate it, so a program with an entry point per generated type is judged per reference, not as
   a whole. When every reaching entry resource defines the anchor but with different targets (the strict-tree shape
   with an entry point inside the inner resource too), the outermost scope still decides on every path, so the
   target is a function of the entry resource alone: the reference keeps a table indexed by entry resource
   (`DynamicRefTarget.NodeByEntryResource`) and the evaluator resolves it from the resource evaluation started in,
   with no scope maintenance. Only references that some reaching entry resource cannot decide keep the dynamic
   scope; if none remain the engine never maintains one (`UsesDynamicScope == false`).
5. **Unevaluated tracking**: nodes with `unevaluatedProperties`/`unevaluatedItems` are marked, and the mark
   is propagated to every in-place applicator descendant (excluding `not`) so that only those nodes pay for
   evaluated-bit bookkeeping.

No delegates or closures are allocated at compile time for paths: the results collector is driven through its
generic `TProviderContext` overloads with static lambdas and an `EdgeContext` struct.

## Evaluation

`Evaluator.Eval<TMode>` is one implementation JIT-specialised twice: `FastMode` (collector absent) has every
reporting call folded away and fails fast at the first violation; `CollectingMode` is exhaustive and pushes
results into `IJsonSchemaResultsCollector` with evaluation path, schema location and instance location.

* Objects and arrays are evaluated in a **single pass** over the instance: property lookup in the name map,
  pattern properties, additionalProperties, propertyNames, presence bits for required/dependencies; prefix
  items, items, contains, uniqueItems (`UniqueItemsHashSet` on stack buffers).
* Evaluated properties/items are tracked in stack-allocated `ulong` bitsets (renting above 256 entries).
  In-place applicators receive a cleared scratch bitset; a successful child's bits are OR-ed into the parent,
  so annotations flow up but never down (this is what makes `$ref` open a new unevaluated scope).
* The dynamic scope is a stack-allocated span of resource ids, pushed only when evaluation crosses a
  resource boundary and only if the program has dynamic references. `$dynamicRef` scans it outermost-first.
* Numbers are compared in normalized decimal form (`JsonElementHelpers.ParseNumber` /
  `CompareNormalizedJsonNumbers` / `IsMultipleOf`) so big numbers and small divisors are exact.
* Formats, content encodings, regex matching, rune counting and deep equality all reuse the public
  `JsonSchemaEvaluation` / `JsonElementHelpers` helpers from `Corvus.Text.Json`.

## Conformance

`tests/.../Suite/JsonSchemaTestSuiteTests.cs` runs every file under `tests/draft4`, `draft6`, `draft7`,
`draft2019-09`, `draft2020-12` (required, `optional/` and `optional/format/` with format assertion on).
The only exclusions are `draft4/optional/zeroTerminatedFloats.json` and the leap-second cases in the
`time`/`date-time` format files, matching Corvus.JsonSchema. `AnnotationSuiteTests.cs` runs the annotation
suite through a verbose collector and `JsonSchemaAnnotationProducer`.

## Flag-mode optimisations (after reading Blaze's compiler)

| Technique | Where | Notes |
|---|---|---|
| Discriminator selection for `oneOf`/`anyOf` | `SchemaCompiler.BuildDiscriminator`, `Evaluator.TrySelectBranches` | Branches are classified by the constraint their `properties[X]` places on values: positive (`const` or `enum` of strings, canonical integers or booleans, keys tagged by kind so `"1"` and `1` differ; a non-canonical number such as `2.0` may equal any keyed integer and selects every branch), negative (`type: string` + `not: {enum}`), or wildcard. The instance's `X` value selects the candidate branch list; absent/non-object instances fall back to full evaluation. Flag mode only. |
| Unrolled small objects | `SchemaNode.UnrolledProperties`, `Evaluator.EvalObjectUnrolled` | Up to six properties, mostly required, and no pattern/additional/propertyNames/dependencies: look each name up directly (required first) instead of enumerating the instance and hashing every name. Only when no evaluated-bit tracking is active. Blaze's `properties_as_loop` heuristic. |
| Pure `$ref` elision | `SchemaCompiler.ElidePureRefs`, `ChildRef.FastNode` | A node that is nothing but `$ref` is skipped in flag mode (Blaze's jump-target inlining). Hops that cross a schema resource are kept when the program uses dynamic scope, because entering that resource must still push it. Collecting mode keeps the original node so evaluation paths are unchanged. |
| Type-only leaves | `SchemaNode.IsTypeOnly` | `{ "type": ... }` children are a token-type test at the call site (Blaze's `LoopItemsTypeStrict` / `AssertionPropertyTypeStrict`). |
| `SkipLocalsInit` | `Evaluator` | Stack bitsets are cleared explicitly to the used length only. |
| Leaf fast path | `SchemaNode.IsLeaf`, `Evaluator.EvalLeafFast` | Nodes with only local keywords (type/const/enum/number/string) skip the depth, scope and tracking bookkeeping of node entry. Flags are computed after dynamic references are finalised (a `$dynamicRef`-only node is not a leaf). |
| Fused simple arrays | `SchemaNode.IsSimpleArray`, `Evaluator.EvalSimpleArrayFast` | `{type: array, minItems, maxItems, items: <leaf>}` runs one tight loop; the general array pass also inlines type-only/leaf item loops (Blaze's `LoopItemsTypeStrict`). This took geojson from 1.3× to 0.5×. |
| Plain-integer bounds | `Evaluator.EvalNumber` | Integer literal against integer bounds compares as `long` without decimal normalisation. |
| Absent-discriminator fast fail; type unions | `Discriminator.AllRequire`, `SchemaNode.OneOfTypeUnion`/`AnyOfTypeUnion` | If every branch requires the discriminator property, its absence fails the keyword at once; `oneOf`/`anyOf` over type-only branches is a single mask test (`AssertionTypeStrictAny`). |
| Structural `uniqueItems` set | `Evaluator.UniqueItemSet`, `Evaluator.HashValue` | An open-addressing set of item indices keyed by a hash over the raw UTF-8 that agrees with JSON equality (unescaped strings, numbers by value, objects order-independently), rented and sized to the array. Replaced the shared hash set, which rented and zeroed a fixed table per array and transcoded every string to UTF-16; `uniqueItems` no longer costs an array its simple-array or array-items plan. Took unreal-engine-uproject from 0.65 to 0.21 of the generated code. |
| No scratch for non-marking children | `SchemaNode.MarksProperties/MarksItems`, `Evaluator.CanMark` | In-place children that cannot mark properties/items (computed transitively at compile time) are evaluated without a scratch bitset or merge. Took the strict-tree `$dynamicRef` micro case from 2.9× to 0.8. |
| Process-wide regex cache | `PatternMatcher.RegexCache` | Identical patterns compile once per process (regexes are immutable); helps cold start with compiled regexes. |
| Cheapest-first unrolled order | `SchemaCompiler.OrderUnrolledProperties` | Required before optional, leaves before applicators. The unroll heuristic itself is "all required, or at most two entries": a looser rule regressed jasmine/cypress. |
| Object plan with `patternProperties` and `dependencies` | `SchemaCompiler.SelectPlan`, `Evaluator.EvalObjectPlanCore` | The object plan applies pattern properties after the named property (additional only when neither matched) and checks dependent required lists and schemas over the seen bits, so roots with either keyword (draft-04, clang-format) keep the planned path. |
| One-pass property matching | `Evaluator.EvalObjectUnrolled`, `Evaluator.TryFindProperty` | Unrolled objects match instance names against their few entries in one pass over the object, and discriminators find their property the same way, instead of by-name lookups through the document (a scan or a map build per property). |
| Length-then-byte name map | `Utf8NameMap` | A lookup is a length test, one byte at the position chosen per length to separate that length's keys, and a comparison against the one or two candidates sharing it; no hash over the instance name. String-enum matching uses the same map. |
| Regex-free class sequences and literal alternations | `PatternMatcher.ClassAtom`, `PatternMatcher.TryParseClassSequence`, `PatternMatcher.TryParseLiterals` | `^[a-zA-Z0-9_.-|@#]*$`, `^\d{4}-\d{2}-\d{2}$`, `^(schemas|responses)$` and the like match on UTF-8 directly: ASCII classes with quantifiers where every atom but the last has a fixed count, or a set of literals. Anything else still goes to the (translated) regular expression, and such patterns no longer appear in a program image's pattern table. |

| Fused evaluation plans | `NodePlan`, `SchemaNode.Plan`, `SchemaCompiler.SelectPlan`, `Evaluator.EvalChildFast` | Every node gets one flag-mode routine at compile time and child entry sites dispatch on it once: `Leaf`, `SimpleArray`, `Object` (type + properties/required/additionalProperties/count bounds in one loop, raw property-name spans when unescaped), `ArrayItems` (type + items/length bounds), `DynamicRef` (a bare `$dynamicRef` resolved at the entry site and dispatched straight to its target), or `General`. In-place entry with a live evaluated bitset and collecting mode stay on the general path. The strict-tree `$dynamicRef` shape drops from about a dozen calls per tree node to about half that. `CORVUS_RT_NO_PLANS=1` keeps only the pre-plan routines for A/B runs. |
| Tracking only where consumed | `SchemaCompiler.ComputeTracking` | A node allocates an evaluated bitset on entry only when it has `unevaluatedProperties`/`unevaluatedItems` itself; in-place children receive the parent's bitset through the call, so the earlier downward propagation of the tracking flag (which made every `$ref` target of a tracking node allocate and mark for nothing when reached from a consuming keyword) is gone. |
| Packed node flags; compile-time cycle detection | `NodeFlags`, `SchemaNode.Flags`, `SchemaCompiler.ComputeInPlaceCycles` | Node entry reads one flags word instead of a dozen booleans, and nodes without type/const/enum skip that block with a single test. The runaway guard for in-place recursion (`MaxDepth`) applies only to nodes the compiler finds on a cycle of in-place applicators (Tarjan SCC over `$ref`/`$dynamicRef`/allOf/anyOf/oneOf/not/if/dependent-schema edges), so ordinary in-place edges carry no counter and no try/finally. |
| Type-union and type-dispatch plans | `NodePlan.TypeUnion`, `NodePlan.TypeDispatch`, `SchemaNode.InPlaceUnionMask`/`InPlaceDispatch` | A node whose only keyword is an `anyOf`/`oneOf` decided by type union or type dispatch is entered with one mask test or one table read and a jump to the branch's plan. |
| Maps and typed additionalProperties on the strict plan; inline string enums | `SchemaNode.AdditionalRejects`/`AdditionalInlineType`/`AdditionalFastNode`, `PropertyEntry.InlineEnum`, `Utf8NameMap.TryGetValue(tag, value)` | Unknown names are rejected, type-tested in place, dispatched or allowed by precomputed node fields; an object with only `additionalProperties` skips the lookup; string-enum children are tested in place; tagged discriminator and value-test lookups do not copy the value. |
| Strict object plan; inline type-only children; required mask | `NodePlan.StrictObject`, `PropertyEntry.InlineType`, `SchemaNode.RequiredMask`, `SchemaCompiler.ComputeObjectDetails` | An object node with named properties only (no pattern properties, dependencies or additional-properties schema) runs one loop of name lookup plus token-type test for type-only children, dispatching other children on their plan, then one mask test for `required`. The object and fused plans test type-only children in place too. Name-map keys of up to sixteen bytes compare as word loads. |
| Forward plan | `NodePlan.Forward`, `SchemaNode.ForwardNode`, `SchemaCompiler.ComputeForwards` | A node whose only assertion is a lone `allOf` branch or a `$ref` the elision kept dispatches flag mode straight to the child's plan; not across a resource boundary when the program keeps a dynamic scope, and not onto an in-place cycle. Derived from the graph on image load. |
| Fused plan without `unevaluatedProperties` | `FusedObjects.TryFuse` | Fuses whenever one pass replaces several (two or more contributors with object keywords, an `if` condition the seen bits decide, required-only alternatives); a node with only its own object keywords, dependencies included, keeps the object plan. |
| Type dispatch for `anyOf`/`oneOf` | `SchemaNode.AnyOfTypeDispatch`/`OneOfTypeDispatch`, `SchemaCompiler.ComputeTypeDispatch` | When every branch asserts a type and no two accept the same token type, the token type selects the only branch that can match; branches keep their own type test. |
| Raw property names; string leaves over the raw span | `IDocumentAccess.PropertyNameRaw`, `Evaluator.EvalStringCore` | Object loops read a name's location and length from the row spans in the state; unescaped strings are evaluated from the raw text; `minLength`/`maxLength` are decided from the byte length where a rune count cannot change the answer. |
| Flag-mode entry on the plan | `Evaluator.Evaluate` | Without a collector or a dynamic scope the root goes straight to `EvalChildFast` with no try/finally. |
| Conditional plan | `NodePlan.Conditional`, `SchemaNode.ConditionalOwnPlan`, `SchemaCompiler.IsConditionalOnly`, `Evaluator.EvalConditionalPlan` | A node whose keywords are type and object keywords plus if/then/else, and which does not fuse, runs its own keywords through the strict or object plan and then dispatches the `if` and the selected branch as plain children, with none of the general path's in-place bookkeeping. |
| Grouped alternatives and dots in patterns | `PatternMatcher.TryParseAlternatives`, `ClassAtom.TryConsume` | Groups of alternatives (optionally `?`) are flattened into whole class sequences; `.` is a one-rune class excluding line terminators; top-level alternations stay with the regex. |
| Direct document access | `RawDocumentAccess` (Corvus.Text.Json), `IDocumentAccess`/`RawAccess`/`InterfaceAccess` | The evaluator is generic over an access type as well as the evaluation mode. For parsed documents (`JsonDocument.TryGetRawAccess`) it reads metadata rows and text directly: token type, size, sibling stepping and raw values are a couple of loads each, with no per-call disposal check, and the object/array loops step siblings by row arithmetic instead of enumerators. Every other document type uses the `IJsonDocument` interface path (`propertyNames` always does, for its fixed string document). Strings and names that contain escapes still unescape through the document. |

Experiment switches (environment variables, read once): `CORVUS_RT_NO_PLANS`, `CORVUS_RT_NO_UNROLL`, `CORVUS_RT_NO_ELIDE`,
`CORVUS_RT_NO_DISCRIMINATOR`, `CORVUS_RT_NO_LEAF`, `CORVUS_RT_NO_ORDER`, `CORVUS_RT_NO_INTFAST`,
`CORVUS_RT_REGEX_INTERPRETED`.

### Fused object plan

A node with `unevaluatedProperties` whose object semantics are spread over `allOf`, `$ref` and `if`/`then`/`else`
(with `if` a pure `required` list) gets a fused flag-mode plan: one pass over the instance's properties applying,
for each name, the child schemas every branch resolves it to (precomputed for known names, per-branch pattern and
additional resolution for unknown ones), a second step for the branches whose condition the seen properties decide,
then required names and the unevaluated check from the coverage bits. See `FusedObjects` and the Stage 2 design note.
Collecting mode keeps the general path; `CORVUS_RT_NO_FUSE=1` disables the plan.

## Allocation and JIT tiering

Steady-state evaluation allocates nothing (verified with `GC.GetAllocatedBytesForCurrentThread` and a
`GCAllocationTick` type listener over all formats, keyword probes, the verbose collector and all 37 corpora). One
non-steady-state effect is worth knowing: while a method is still tier-0 JIT code, every `"..."u8` or static
`ReadOnlySpan` access goes through `RuntimeHelpers.CreateSpan`, which allocates a 72-byte `RuntimeFieldInfoStub`
per access; tier-1 code resolves these statically. Tier-up is postponed while other code keeps being jitted, so a
process that continuously compiles new schemas (or a benchmark harness) can observe these allocations from the
shared Corvus helpers (IDN formats, the prime table used by `UniqueItemsHashSet`). The evaluator's own flag-mode
paths use static arrays for the keyword names it passes to those helpers so they allocate nothing at any tier;
the remaining tier-0 stubs come from `Corvus.Text.Json` and would be removed by `AggressiveOptimization` on its
hot format helpers, or by ReadyToRun.

## Benchmarking notes

* `benchmarks/.../Benchmarks` uses the in-process toolchain: BenchmarkDotNet's separate host build re-compiles the
  37 model projects and raced on file locks. Ratios are measured within one process, so they stay meaningful on a
  loaded machine; absolute times do not.
* `dotnet run -c Release -- diff [name...]` cross-checks every Sourcemeta instance between the generated model and
  the runtime evaluator and explains disagreements with basic results. The checked-in generated models do not assert
  `format`, so the Sourcemeta comparison runs with format as an annotation on both sides.
* `MicroBenchmarks` compares, per keyword group, the generated typed model, the generated standalone evaluator and
  the runtime evaluator over identical schemas (source-generated in `Corvus.Text.Json.RuntimeEvaluator.MicroModels`).

## Results (2026-09-09, loaded machine, in-process interleaved min-of-N)

The harness also has `dump <case|schema-file>` (prints every node with its plan, flags and fast-path markers) and
`CORVUS_RT_QUICK_CPUTIME=1` (times `quick` rounds by thread CPU time; pair with `DOTNET_TieredCompilation=0` for
same-binary A/B runs on a loaded host).

Measured with `dotnet run -c Release -- quick 40` in `benchmarks/Corvus.Text.Json.RuntimeEvaluator.Benchmarks`
(alternating the generated model and the runtime evaluator, keeping the fastest round of each). Ratios are
runtime evaluator time divided by generated-model time for the whole instance corpus of each case, flag mode,
format as annotation on both sides (matching the checked-in generated models). See `RESULTS.md` for the table
from the most recent run.

Cold start (`benchmarks/Corvus.Text.Json.RuntimeEvaluator.ColdStart`): compiling a Sourcemeta schema with the
runtime evaluator takes 0.07 to 4 ms warm (about 80 ms for the very first compile in a process, which JIT-compiles
the compiler itself), against 1 to 17 seconds per schema for the Roslyn-based `Corvus.Text.Json.Validator` it
replaced; the Validator is now a facade over this evaluator (see `docs/Validator.md`).

## Known gaps and next steps

* Leap seconds in `time`/`date-time` (73 suite cases) are rejected by the shared Corvus parsers.
* Evaluation paths in collecting mode use plain keyword segments (`properties/foo`, `$ref`, `allOf/0`) rather than
  the generated evaluator's `#/$ref/0/...` convention; annotation output (schema location, instance location,
  keyword, value) is identical.
* Regexes are compiled to IL by default (decision 2026-09-09: evaluation throughput over cold start);
  `CompileRegularExpressions = false` (or `CORVUS_RT_REGEX_INTERPRETED=1` for experiments) restores interpreted
  regexes for faster compilation.
* The document layer no longer dominates profiles of property-heavy cases (raw access took cmake-presets, draft-04
  and openapi from 0.76 to 0.79 down to 0.68 to 0.81 on a loaded machine, and the object micro case from 0.50 to
  0.26). Remaining per-node overhead is interpretive dispatch in `EvalCore`/`Eval` (a CPU profile of the strict-tree
  case is 90% in the evaluator's own frames). The next structural step would be precomputed per-node evaluation plans
  (a small set of fused instruction kinds selected at compile time, closer to Blaze's instruction model) instead of
  the flag-by-flag keyword dispatch. Perfect-hash string sets for large enums remain untried.
* The public `Corvus.Text.Json` helpers used here (`JsonSchemaEvaluation.Match*`, `JsonElementHelpers.*`) take a
  `ref JsonSchemaContext` for reporting; the evaluator passes a scratch context. When merged into Corvus V5 the
  pure predicates (`MatchEmail(ReadOnlySpan<byte>)` etc.) should be made public instead.

## Pre-compilation

Program images (`JsonSchemaEvaluator.ToProgramImage`/`FromProgramImage`) and the Stage 2 design and measurements are in
[RuntimeEvaluatorPrecompilation.md](RuntimeEvaluatorPrecompilation.md).

## Publishing an application

Nothing is required of a consumer. The recommendation, from the measurements below: **native AOT for one-shot or
short-lived processes** (first validation in 4.5 ms, half of Blaze's, no JIT ramp, and within 6% of the JIT per
document once warm, on the profile the package carries); **framework-dependent, the JIT, for long-running
processes** (the fastest steady state, because dynamic PGO tunes the evaluation loops to the application's own
schemas and instances, and every runtime update improves the deployed binary without a rebuild; its 150 ms of
startup is behind Blaze's after the first million or so documents of a process); and **ReadyToRun with the
library excluded or partially precompiled** when cold start matters in a process that lives long enough to want
the JIT's steady state. Parsing included, a document validated once at steady state costs about a fifth of what
it costs Blaze (the parse-and-evaluate column of the four-axis table), on every corpus, under all three modes.

The paragraphs below say what each publish mode gives and where the choices are, measured over the 37 Sourcemeta
corpora (medians; the full table is in [RuntimeEvaluatorPrecompilation.md](RuntimeEvaluatorPrecompilation.md),
"The four-axis table").

**Framework-dependent (the default).** Cold start of a process that compiles a schema and validates one document
is about 150 ms, most of it the JIT compiling the schema compiler; steady state is the fastest of the three modes
(76 µs per corpus pass at the median, ahead of Blaze on 31 of 37 corpora). Loading a program image instead of
compiling (`JsonSchemaEvaluator.FromProgramImage`) takes cold start to 125 ms; source-generated types, which embed
their image, to 119 ms.

**Native AOT (`PublishAot`).** Cold start is 4.5 to 5 ms, half of Blaze's, and there is no JIT. Steady state would
be about 15% behind the JIT's, because the JIT's dynamic profile drives the inlining of the evaluation loops and
native AOT has none; so the package carries a static profile of the library's evaluation paths
(`profiles/Corvus.Text.Json.mibc`, recorded from an instrumented run over the corpora) and its
`buildTransitive/Corvus.Text.Json.targets` hands it to the AOT compiler whenever `PublishAot` is set. With it the
steady state is within about 10% of the JIT's (90 against 76 µs), ahead of Blaze on 29 of 37; without it, about
15% behind the JIT. Checked against a packed library: the runner published against the package reads the same as
the project build with the profile (1.00 at the median over 14 corpora), and with the profile opted out 5% slower
at the median, up to 15% (krakend, draft-04). The profile
describes the library's code, not the application's schemas, so it applies to any application. To leave it out set
`CorvusTextJsonUseProfile` to `false`; to use a profile of your own workload (dotnet-pgo `create-mibc` over an
instrumented trace) point `CorvusTextJsonProfile` at it.

**ReadyToRun (`PublishReadyToRun`).** Precompiling the library halves cold start (150 to 60 ms) but costs steady
state on small documents: the runtime re-jits the hot loops from the precompiled code without the profile-driven
inlining, 1.2 to 1.5x slower on the smallest corpora (yamllint, helm-chart-lock). The partial mode that avoids
it (precompile the schema compiler only, from a profile of the compile phase) is a global switch of the publish
(`--partial` to crossgen2 with `PublishReadyToRunPgoFiles`), which would also strip the application's own
assemblies of everything not in the profile, so the package does not set it. The choice is the application's:
keep the default when cold start matters more, or exclude the library from precompilation when steady state
does:

```xml
<ItemGroup>
  <PublishReadyToRunExclude Include="Corvus.Text.Json.dll" />
</ItemGroup>
```

**Source-generated types** need none of this for their schemas: they embed the program image and validate
through the evaluator, so the compile-phase question does not arise; the native AOT profile still applies to
their evaluation.
