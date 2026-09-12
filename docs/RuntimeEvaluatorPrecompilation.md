# Pre-compiled schema programs (Stage 2 design note)

Status: design and measurement spike, 2026-09-10. Nothing in this note is wired into the generators yet; the
measurements come from `ProgramImage` (in `Corvus.Text.Json.RuntimeEvaluator`), the `image`/`emit` commands of the
runtime-evaluator benchmark harness, the `write`/`image` modes of the cold-start harness, and a throw-away "spike"
console project that compiled emitted C# against the evaluator.

## Where Stage 0 leaves us

After Stage 0 a generated assembly carries, per compilation, one `CorvusJsonSchemaProgram` with the schema documents
as UTF-8 data and lazily compiled entry points. At first use the runtime evaluator parses the documents, resolves
references, reads any custom metaschema's `$vocabulary`, builds the node graph, and constructs the regular expressions.
Stage 2 moves everything except regular-expression construction to generation time, and hands regular expressions to
`[GeneratedRegex]`.

What Stage 2 cannot remove is JIT of the evaluation engine itself, measured earlier at roughly 40 ms floor for the
first evaluation in a cold process and about half that with ReadyToRun. That is a consumer publish setting, not
something the package can ship.

## What a compiled program is

The compiler's output is a plain object graph, `SchemaNode[]`, with:

* per-node summary flags, the fused `NodePlan`, the dialect and resource id, and the schema location bytes;
* value keywords: a `TypeMask`, `const`/`enum` values, normalised numeric bounds and divisor, string bounds, a
  `PatternMatcher`, format and content kinds with their assertion flags;
* object keywords: a `Utf8NameMap<PropertyEntry>` (with presence bits for `required`, `dependentRequired`,
  `dependentSchemas` and `dependencies`), pattern properties, additional/unevaluated properties, property names;
* array keywords: prefix items, items, contains bounds, unevaluated items;
* in-place applicators: resolved `$ref` targets, `$dynamicRef` tables (`resourceId -> nodeId`), `allOf`/`anyOf`/`oneOf`
  with their discriminators and type unions, `not`, `if`/`then`/`else`;
* annotation keywords as raw JSON bytes, and every child edge (`ChildRef`) with its evaluation-path segment bytes.

Only one thing in that graph still points at a parsed schema document: `ConstantValue`, used for deep equality of
object and array `const`/`enum` values. Everything else is bytes, integers, and small objects.

## Two emitted shapes

### A. Program image

`ProgramImage` serialises the graph to a versioned binary image (varints, length-prefixed byte arrays, a packed bit
set per node) plus one JSON array holding every `const`/`enum` value. `JsonSchemaEvaluator.FromProgramImage` rebuilds
the graph without the loader or compiler; the constants array is parsed once, which is the only parse left.
`ToProgramImage` records every entry point compiled so far, and `ForEntryPoint` on an image resolves only those.

Conformance: the entire JSON Schema Test Suite passes with every schema round-tripped through an image (the
`ImageSuiteTests` class), and all 37 Sourcemeta corpora agree instance for instance between the compiled program and
its image.

Warm, in-process, minimum of 15 rounds, interpreted regexes (compiled regexes are cached process-wide, so warm figures
are the same either way):

| | Compile | Image load |
|---|---|---|
| All 37 corpora, total | 45.9 ms | 8.6 ms |
| Allocation, all corpora | 21.4 MB | 10.7 MB |
| Largest (ui5-manifest, 2688 nodes) | 8.2 ms | 1.4 ms |
| Median corpus (deno, 108 nodes) | 0.92 ms | 0.13 ms |

Process-cold, first schema in a fresh process (three runs each):

| | First load | Warm |
|---|---|---|
| Compile, aws-cdk (10 nodes) | 71 to 85 ms | 0.07 ms |
| Image, aws-cdk | 19 to 24 ms | 0.01 ms |
| Compile, cql2 (regex-heavy) | 35 to 39 ms | 2.5 to 2.8 ms |
| Image, cql2 | 33 to 38 ms | 0.4 ms |

The cql2 row is the regular-expression cost: with `CompileRegularExpressions` on (the default) the first construction
of each `Regex` compiles it to IL, and that dominates the image load. `[GeneratedRegex]` moves exactly that cost to
build time, so it is the case Stage 2 must handle, not a limit of the image.

Image size (version 4) is about two thirds of the schema text: 1.05 MB against 1.66 MB over the corpora, down from
2.16 MB in version 2. Three changes account for it. Byte payloads (path segments, property names, keywords, number
text, annotation JSON) are interned in one table and referenced by index. A node's schema location is stored as the
index of the earlier node whose location is its longest prefix on a segment boundary, plus the interned remainder,
so the per-node JSON pointer costs a few bytes instead of the whole string (the locations were the largest single
item: 195 KB of ui5-manifest's 525 KB). Keyword groups a node does not use (value, number, string, object, array,
in-place applicators, annotations) are skipped under a per-node section mask instead of writing every default. What
remains is dominated by annotation JSON (`title`, `description`, `default`, `examples` raw text, about 0.5 MB in
total), which verbose output needs and which compresses no further without changing what is kept. Load time is
unchanged (8.8 ms warm across the 37 corpora); the `breakdown` benchmark command reports the per-schema split.

The machine was loaded during these runs (load average 8 to 25 from concurrent builds); the minimum-of-N figures are
stable, the cold first-load figures vary by roughly a third between runs.

### B. C# static initialisers

`StaticInitEmitter` (benchmark harness, `emit` command) writes the same graph as C# object initialisers, chunked into
methods of 32 nodes, with the constants array as a UTF-8 literal, regexes constructed through `PatternMatcher.Create`
and the `Build` method returning a `CompiledSchema` through the image constructor. The spike project compiled five
corpora (babelrc, openapi, cmake-presets, krakend, ui5-manifest) and timed `Build()` against loading the equivalent
image; the emitted graph is checked by writing it back to an image and comparing bytes with the compiler's image.

Emitted source and compile cost (Release, five programs, one project):

| Corpus | Nodes | Emitted C# | Image |
|---|---|---|---|
| babelrc | 38 | 80 KB | 8 KB |
| openapi | 363 | 698 KB | 52 KB |
| cmake-presets | 677 | 1.36 MB | 146 KB |
| krakend | 1334 | 3.10 MB | 489 KB |
| ui5-manifest | 2688 | 5.52 MB | 582 KB |

Roslyn compiled the five programs in about 10 seconds of wall time; the resulting assembly is 5.4 MB. The emitted
graphs are byte-for-byte identical to the compiler's when written back as images.

Type initialisation against image load, same process, three runs (first call includes JIT of the emitted methods;
warm is the minimum of 20):

| Corpus | Build first | Build warm | Image first | Image warm |
|---|---|---|---|---|
| babelrc | 21 ms | 0.02 ms | 2.4 to 3.1 ms | 0.04 ms |
| openapi | 51 to 55 ms | 0.16 ms | 0.3 ms | 0.24 ms |
| cmake-presets | 99 to 104 ms | 0.32 ms | 0.6 ms | 0.45 ms |
| krakend | 245 to 256 ms | 0.9 ms | 1.3 to 1.7 ms | 1.1 ms |
| ui5-manifest | 421 to 427 ms | 1.5 ms | 2.1 to 2.3 ms | 2.0 ms |

The first-call column is the decisive one: JIT of the object-initialiser methods costs roughly 0.16 ms per node,
so a 2688-node program spends over 400 ms initialising the first time, two hundred times the image load. Once
JIT-compiled the static form is 25 to 35 percent faster than the image reader, which is not worth the cold-start
cost, the tenfold source size, or the public surface the emitted code would need over the node model. ReadyToRun on
the consumer would recover some of the JIT cost, but only for consumers that opt in, and the image needs no such help.

## Where pre-compilation runs

Two producers emit programs today: the CLI (`Corvus.Json.Cli`, a normal .NET application) and the Roslyn source
generator (`Corvus.Text.Json.SourceGenerator`, netstandard2.0, which by design carries no binary dependencies and
compiles the V4 analysis core and selected Corvus.Text.Json files in as sources).

Pre-compiling in the CLI is straightforward: it can reference the evaluator package and call the compiler. Pre-compiling
in the source generator needs the schema compiler and the document model it reads with (`ParsedJsonDocument`,
`JsonElement`, `JsonElementHelpers`, `UnescapedUtf8JsonString`, `EcmaRegexTranslator`) compiled into the generator.
Both `Corvus.Text.Json` and the evaluator already target netstandard2.0, so this is a matter of linking sources the
way the generator links the V4 core, and of the generator's build-time cost. The alternative is for the source
generator to keep emitting the Stage 0 form while the CLI emits pre-compiled programs; that leaves two shapes in the
field and is not recommended.

## Engine changes Stage 2 needs

1. **Regular-expression provider.** Done (image version 2). The image carries a pattern table of every pattern that
   needs a `Regex` (prefix, length-range and trivial patterns are matched without one), in first-seen node order, and
   nodes reference patterns by index. `JsonSchemaEvaluatorOptions.RegexProvider` is asked for `(index, pattern)`
   before a regular expression is constructed, with index -1 when compiling from text; a `null` answer falls back to
   construction. `JsonSchemaEvaluator.GetImagePatterns` reads the table so an emitter can write one `[GeneratedRegex]`
   method per entry, and `ToDotNetPattern` gives the translated pattern the evaluator itself would construct, so the
   generated instance matches exactly. The emitted table belongs to the image-emission step.
2. **Constants.** The image carries the values as one JSON array parsed at load. Emitting them as statically
   constructed values instead requires `const`/`enum` deep equality to work against standalone values rather than a
   document and index; the load-time parse measured under 0.1 ms for every corpus, so this is an optimisation to do
   after the shape is settled, not before.
3. **Image versioning.** The image version is tied to the evaluator assembly. The generator and the evaluator ship
   from the same repository, and a consumer referencing mismatched packages should fail at type initialisation with a
   clear message rather than silently recompiling from schema text; keeping the text as a fallback would forfeit the
   size and start-up gains.
4. **Entry points.** Every entry point a generated type needs must be compiled when the image is produced; the
   generator already knows them (the program entries of Stage 0).
5. **String table.** Done (image version 4): interned byte payloads, prefix-encoded locations and a section mask per
   node; see the size paragraph above.

## Recommendation

Emit **program images**, not static initialisers. The image loads in a fifth of the compile time warm, a quarter of
it process-cold, halves compile-time allocation, needs no schema text, keeps the node model internal, and is already
conformance-clean. The emitted C# per compilation shrinks to a shim: the image as UTF-8 data, the entry-point table,
the options, and a `[GeneratedRegex]` method per pattern wired in through the regular-expression provider.

Order of work:

1. Regular-expression provider on `FromProgramImage`, and the emitted `[GeneratedRegex]` table (the cql2 row above
   is what this buys).
2. `RuntimeProgramGenerator` emits the image and the shim instead of the schema documents; the CLI drives it first
   because it can reference the evaluator directly. Done: `CSharpLanguageProvider.Options.ProgramCompiler` takes a
   `SchemaProgramCompiler` delegate (the generator library keeps no evaluator dependency); the CLI supplies
   `RuntimeProgramCompiler.Compile`, which compiles the program exactly as the emitted code would, registers every
   entry point, and returns the image and its patterns translated to .NET syntax. The emitted class is then partial,
   carries the image as base64 UTF-8 literals decoded on first use, loads it with `FromProgramImage`, and wires one
   `[GeneratedRegex]` method per pattern through `RegexProvider` under the same framework guard the old generator
   used; the schema documents are not embedded. Without a compiler the Stage 0 form is emitted.
3. Link the compiler into the source generator, so both producers emit the same shape. Done; see the next section.
4. String table in the image (size only; done, image version 4), then constants as static values if the load-time
   parse ever shows up.

## The source generator

The source generator takes no binary dependencies: every component it uses is linked in as source files, as the
JMESPath and JSONPath generators do, and it builds against `System.Text.Json` (`STJ` and `BUILDING_SOURCE_GENERATOR`
are defined). The program compiler follows the same rule, so the compiler and the image writer are linked as files
and must compile over `System.Text.Json` in that configuration; the Corvus document model is not brought in for
their sake.

The compile path already uses an element API that `System.Text.Json` shares (`ValueKind`, `GetString`,
`TryGetProperty`, `ValueEquals`, `EnumerateObject`/`EnumerateArray`, `GetRawText`). What differs is confined to a
few places:

* **Element identity.** The loader and compiler identify a subschema by (document, element index), which the Corvus
  document exposes and `System.Text.Json` does not. Under `STJ` the loader assigns the index itself: every element it
  reaches (walking, resolving a pointer fragment, or an anchor) is interned by its JSON pointer, and the index is the
  intern ordinal. The node's schema location is then that pointer, which replaces `TryGetJsonPointer`, and pointer
  fragments are resolved by walking properties and array indices with the usual `~0`/`~1` decoding, which replaces
  `TryResolvePointer`.
* **Raw values.** `GetRawSimpleValue` (number text) becomes `GetRawText`; `GetUtf8String` becomes the UTF-8 encoding
  of `GetString`; the token type of a constant is derived from its `ValueKind`.
* **Constants.** `ConstantValue` holds the element and its (document, ordinal) identity instead of a document and
  index; the pool writes `GetRawText`.
* **Runtime-only members** are compiled out under `STJ`: number comparison and `multipleOf` (they use the Corvus
  number helpers; the writer needs only the text), regular-expression matching, the message providers, the image
  reader except the pattern table, `CompiledSchema`'s constants document, and every `Evaluate` overload.

The linked file set is the `Compilation` directory (compiler, loader, node model, fused-object analysis, name map,
keywords, URI utilities, metaschema table, image, and the `System.Text.Json` shim), the netstandard2.0 polyfills,
the public option, dialect, format-mode and exception types, the compile-side half of `JsonSchemaEvaluator`, the
ECMA regex translator (already linked), and the CLI's `RuntimeProgramCompiler`, which is the
`SchemaProgramCompiler` implementation the generator passes in its language provider options. The evaluation
directory is not linked. The generator's embedded metaschemas already use the logical names the evaluator's
metaschema table looks up. Because the generator applies the repository's analyzer set (StyleCop, Roslynator, the
analyzer-banned-API rules) to linked files, the evaluator sources follow the `src` conventions (no trailing
newline, no environment reads on the generator path: the A/B switches are constants under `STJ`).

One difference from the CLI's output: the generator's program carries no `[GeneratedRegex]` table. Roslyn does not
chain source generators, so the regular-expression generator never sees code another generator produced and the
partial methods would have no implementation. The generator's shim therefore returns no provider and the evaluator
constructs each expression from the image's pattern table on first use, as it does on runtimes without the regex
generator. The image itself is identical.

Version coupling is the same as today's source generator to `Corvus.Text.Json` pairing: the generator emits the image
version of the files it was built from, and `FromProgramImage` rejects any other version at type initialisation
with a message naming both versions. There is no schema-text fallback.

## Throughput ceiling: emitted evaluation code against the engine

The `ceiling` command of the benchmark harness times hand-written, straight-line evaluators for the micro cases (the
best a per-schema code emitter could produce: same raw document access and helpers, no node graph, no flags, no
generic dispatch) against the engine on the same instances, after checking they agree. Quiet machine, minimum of 40
rounds, three runs of all cases interleaved and two runs of each case alone:

| Case | Engine path | Engine | Straight-line | Ratio |
|---|---|---|---|---|
| Object | fused `Object` plan | 122 to 163 ns | 92 to 285 ns | 0.3 to 2.3 (noise) |
| OneOf | discriminator | 59 to 200 ns | 106 to 358 ns | 1.7 to 1.9 |
| Array | array-items plan, `uniqueItems` | 465 to 579 ns | 409 to 481 ns | 0.7 to 0.9 |
| Unevaluated | `allOf` × 3 with evaluated bits | 230 to 482 ns | 25 to 141 ns | 0.10 to 0.30 |
| DynamicRef | dynamic scope, strict tree | 246 to 873 ns | 34 to 183 ns | 0.13 to 0.21 |

Two conclusions and one caveat.

* Where the compiler already fuses the node into a plan (objects, discriminated `oneOf`, simple arrays) there is
  nothing left for emitted code to win; the discriminator beats the straight-line form because it short-circuits on
  the missing property.
* The general path (in-place applicators over the same instance with evaluated-bit tracking, and dynamic scope
  resolution) has three to ten times headroom, but the straight-line form does not win it by removing dispatch. It
  wins it by making one pass over the properties with a single merged switch, and by knowing the dynamic reference's
  target statically. Both are schema-level transformations the compiler can perform and the engine can execute as
  plans: fuse the property tables of `allOf`, `$ref`, `if`/`then`/`else` branches over the same object into one
  table with per-entry branch conditions, and demote a `$dynamicRef` whose anchor is defined in the entry point's root
  resource (always the outermost scope, so always the answer) to a static reference. That puts the gain into every
  program, image or compiled, and into the runtime Validator, with no emitted code.
* The absolute figures for cases under 300 ns move by two to three times between process configurations (the
  engine's DynamicRef case ran at 870 ns in the interleaved run and 250 ns alone; the straight-line Unevaluated case
  at 141 ns and 25 ns). That is JIT and dynamic PGO deciding differently about the same large generic methods, a
  known instability of this case. Ratios within a run are meaningful; absolute figures are not to two digits, and the
  variance is itself an argument for simpler fused plans over the general path.

Of the 37 corpora, only ui5-manifest and cmake-presets (heavy `allOf`), openapi (`unevaluated*`) and cql2
(`$dynamicRef`) lean on the general path; the rest sit on planned paths. The corpus comparison in the harness is still
against the checked-in models generated by the previous generator (those projects reference only
`Corvus.Text.Json`), which makes it an independent oracle for agreement but means its "generated" column is the old
validation code, not the Stage 0 shims: geometric mean ratio 0.25 over 37 cases on this run.

Decision (2026-09-10): no emitted evaluation code; do the schema-level fusion in the compiler, after the regular
expression provider.

### Fusion, done

Two compiler transformations followed, both flag-mode plans with collecting mode unchanged, and both checked by
running the whole JSON Schema Test Suite in collecting mode (the general path) as well as flag mode (the plans):

* **Static `$dynamicRef` when every entry resource defines the anchor** with the same target. The scope is searched
  outermost-first and its outermost entry is always the resource evaluation started in, so the target is the answer
  on every path; programs with no remaining dynamic references keep no scope at all. This is re-applied as entry points
  are added, and only the entries that can reach the reference count: a generated program registers an entry for every
  type, and entries whose resource never defines the anchor cannot block a reference they never evaluate. Two
  reachable entries that resolve differently make the reference entry-resolved rather than dynamic: since each
  entry's resource is the outermost scope on every path, the target is a function of the entry resource, kept as a
  table on the reference and read once at evaluation with no scope maintenance (image version 5). Only a reaching
  entry whose resource lacks the anchor keeps the scope. This is what makes the strict-tree micro case as fast
  generated (entry points in both resources) as through the API (one entry).

Generated code against the API, measured on the micro cases with the fixes in (typed generated model, generated
standalone evaluator, runtime API; quiet box, minimum of 40 rounds): object 262/258/253 ns, array 1.05/1.07/1.02 µs,
string 83/80/76, unevaluated 245/241/238, dynamic reference 273/281/263, oneOf 68/63/63, verbose 3.09/2.82/3.06 µs.
With tiered compilation disabled the three paths are identical to the nanosecond (oneOf 59/59/59, dynamic reference
241/240/242): they run the same machine code.

The harness had to be fixed to show this. Its quick timer warmed each workload with a hundred calls and then took the
minimum of forty rounds; that leaves paths at tier 0 or in on-stack-replacement code during measurement, because the
runtime's tiering delay restarts whenever new tier-0 code appears and an interleaved three-way loop keeps producing
it. Under that warmup the same process reported oneOf at 132 against 80 ns and the string case above 200 ns, and the
figures moved between runs. The timer now warms for a second, pauses past the tiering delay, warms again, and only
then measures. Two other explanations were chased and ruled out on the way: dynamic PGO (the gap persisted with it
off) and the document instance or its element type (a probe that evaluates one schema over documents of every model
type, and over twelve identical documents in either order, shows them all within 5%; `docprobe` command). None of
this can be measured on a loaded box: with a build running, the same document measured 198 ns in one pass and 434 in
the next.

The oneOf profile did show one engine cost worth taking: the discriminator reads its property through the document's
by-name lookup (`JsonDocument.TryGetNamedPropertyValueIndexUnsafe`, 30% of the evaluation) rather than the raw-access
scan the rest of the engine uses.
* **Fused object plan** (`FusedObjects`, `NodePlan.FusedObject`) for a node with `unevaluatedProperties` whose object
  semantics spread over `allOf`, `$ref` and `if`/`then`/`else` with required-only conditions: every property name any
  branch knows is resolved at compile time to the child schemas that apply (own property, matching pattern
  properties, or the branch's `additionalProperties`), unknown names resolve per branch at evaluation, conditional
  branches are deferred to a second step over the properties they touched, and the coverage bits feed the unevaluated
  check directly. It is restricted to nodes with `unevaluatedProperties` because there the pass over every instance
  property is unavoidable; fusing branches without it doubled cmake-presets (large instances, small branches, where
  the general path's schema-driven unrolled lookups win). A fused plan applies its contributors' keywords without
  entering them as nodes, so it is withheld only from nodes that can reach a live dynamic reference; the guard was
  program-wide at first, which cost every schema in a generated program its fused plans as soon as one schema in the
  compilation used `$dynamicRef` (the unevaluated micro case ran four times slower generated than through the API).

Micro cases after fusion (quiet-ish machine, ratio is engine after / engine before):

| Case | Engine before | Engine after | Straight-line |
|---|---|---|---|
| Unevaluated | 589 ns | 142 to 170 ns | 151 to 181 ns |
| DynamicRef (strict tree) | 878 ns | 226 to 242 ns | 192 to 200 ns |
| Object, Array, OneOf | unchanged | unchanged | |

Corpora: openapi (27 `unevaluated*`) 8.1 ms to 7.2 ms; cmake-presets unchanged after the restriction; the rest within
noise. The general path remains the fallback for every shape the plan does not take, and `CORVUS_RT_NO_FUSE=1`
disables it for A/B runs.

## Compile-step optimisations after the merge (2026-09-11)

Six changes, profiled on the corpora with the least gain over the shipping generated code (sampled with
`dotnet-trace` through the harness's `profile` command), all in the compiler and its plans: discriminators keyed on
integer and boolean constants (cmake-presets' root is a `oneOf` of eight branches on an integer `version`);
`uniqueItems` through a structural set that every array plan accepts (unreal-engine-uproject spent 55% of its time
renting, zeroing and transcoding for the shared hash set); the object plan taking `patternProperties` and
`dependencies` (draft-04 and clang-format roots were on the general path); a length-then-byte name map in place of a
hash per instance name (17 to 28% of cypress, cmake-presets and pre-commit-hooks); one-pass property matching for
unrolled objects and discriminators instead of by-name lookups through the document; and regex-free matchers for
anchored ASCII class sequences and literal alternations.

Measured as a pair, the commit before the six against the commit after, both harnesses run back to back on an idle
box pinned to the performance cores (`taskset -c 0-11`; this is a hybrid i7-13800H and unpinned runs land on
efficiency cores at random, which was the source of the bimodal noise seen earlier):

| Corpus | Runtime before | Runtime after | Speed-up | Ratio to generated, before to after |
|---|---|---|---|---|
| unreal-engine-uproject | 3.75 ms | 611 µs | 6.1 | 0.54 to 0.07 |
| jshintrc | 1.18 ms | 342 µs | 3.5 | 1.02 to 0.41 |
| omnisharp | 566 µs | 224 µs | 2.5 | 0.45 to 0.37 |
| ansible-meta | 331 µs | 133 µs | 2.5 | 0.50 to 0.41 |
| clang-format | 84 µs | 34 µs | 2.5 | 0.50 to 0.41 |
| babelrc, jsconfig, cypress, jasmine | | | 1.8 to 1.9 | |
| cmake-presets | 9.58 ms | 5.52 ms | 1.7 | 0.72 to 0.57 |
| draft-04 | 5.88 ms | 3.38 ms | 1.7 | 0.60 to 0.34 |
| krakend | 346 µs | 245 µs | 1.4 | |
| openapi | 8.94 ms | 7.61 ms | 1.2 | 0.59 to 0.53 |
| geometric mean over 37 | | | | 0.47 to 0.39 |

Corpora that read slower in the single paired run (vercel, ui5-manifest, pulumi, semantic-release) were repeated three
times on each binary and are equal within run-to-run noise, which is up to 25% on the small ones even pinned and
idle.

### Fused plan: conditions on values, nested conditions, dependencies, required-only alternatives

openapi barely moved under the six items because its object schemas were still on the general path: its `if`
keywords test property values (`{"properties": {"in": {"const": "query"}}, "required": ["in"]}`, one with a
`pattern`), `parameter` and `header` carry a `oneOf` of two required-only branches (schema or content) and a
`dependentSchemas` whose schema holds further conditions, and `security-scheme` has six conditional `allOf`
branches. The fused plan now takes all of these. A condition carries value tests keyed like discriminator values
(tagged string, canonical integer and boolean constants; a `pattern` test for strings), checked as each property is
passed. Conditions nest: each records the condition and polarity it is reached through, and a contributor applies
only when its own condition matches and the chain above does; `dependentSchemas` and `dependentRequired` become
presence-gated contributors the same way. A `oneOf`/`anyOf` whose branches are plain `required` lists is decided
after the pass by counting the branches whose names were all seen. When a condition holds, the `if` schema's own
properties count as evaluated, as the general path's annotations do. Fused plans are rebuilt on load, so the image
format is unchanged.

openapi: 24 fused object schemas instead of 19; 7.61 ms to 5.62 ms (1.59 against the pre-optimisation 8.94 ms).
cmake-presets took a further 1.2 (5.52 to 4.52 ms) from its required-only alternatives; omnisharp and nest-cli
gained as well. Geometric mean over the 37 corpora: 0.38 against the shipping generated code, from 0.47 before this
round of work. Pinned, back to back with the same baseline binary as above.

## Engine mechanics after the schema-shape work (2026-09-11, second round)

With the fused plan covering openapi's shapes, the profiles of the least-improved corpora (jasmine, stale, jsconfig,
pulumi, yamllint) pointed at the engine's per-property and per-node mechanics rather than any keyword. Six changes,
each its own commit:

1. **Property names from the row spans.** The object loops (object plan, fused plan, unrolled objects, discriminator
   lookups) read a name's location and length from the metadata rows held as spans in the evaluation state, one
   bounds check per field, instead of slicing a `ReadOnlyMemory` and converting it to a span per property; the
   fused loop's per-name step is split out so the raw span feeds it directly, and `Utf8NameMap` keeps its per-length
   position and table in one bucket array. Alone this was worth 3% on the geometric mean: the loop was already
   tight, and the disassembly (`DOTNET_JitDisasm`) shows the remaining per-property cost is the `SequenceEqual`
   call and the child's own entry, not the access.
2. **Fused plan without `unevaluatedProperties`; forward plan.** The fused plan now fuses whenever its one pass
   replaces several: two or more contributors with object keywords, an `if` the seen bits decide, or required-only
   alternatives. jasmine's root (`allOf` of three object branches) and stale's (`type`, two properties, `allOf` of a
   twelve-property `$ref`) each went from three passes to one. A node whose object keywords are all its own keeps the
   object plan, dependencies included: fusing draft-04's root for its two `dependencies` alone cost 40% on that
   corpus, because the fused routine's fixed per-object cost outweighs a second pass over small objects. A node
   whose only assertion is a lone `allOf` branch or a kept `$ref` gets `NodePlan.Forward` and dispatches straight
   to the child's plan (yamllint's root). Both are derived from the graph on image load.
3. **Type-dispatched `anyOf`/`oneOf`.** When every branch asserts a `type` and no two accept the same token type,
   the token type selects the one branch that can match (pulumi's `runtime`: a string or an object). The branch
   keeps its own type test, so `integer` and draft 4 lexical integers need nothing special, and it marks straight
   into the parent's evaluated bits.
4. **String leaves.** An unescaped string is evaluated from its raw text with no wrapper to dispose, and since a
   rune is one to four bytes, `minLength` passes and `maxLength` fails from the byte length alone outside the band
   where the rune count matters. This exposed a quirk: a fixed-string document (property names) keeps the quotes on
   the one-argument raw-value overload, so the interface access path asks for them stripped explicitly.
5. **Flag-mode entry.** Without a collector and without a dynamic scope the scope buffer never grows, so the entry
   has no try/finally and dispatches the root (or its elided `$ref` target) on its plan. This exposed a gap: the
   array-items plan returned early for a node without `items`, skipping `uniqueItems`, which the general path had
   always covered at the root. Arrays of up to eight items are now compared pairwise, renting nothing.
6. **Leftovers.** The class-sequence parser flattens groups of alternatives (`^[Ee][Ss]2022(\.(A|B))?$`, jsconfig's
   case-insensitive literals with optional suffixes; `^[1-5](?:[0-9]{2}|XX)$`) into whole alternatives, a dot is a
   one-rune class, and top-level alternations stay with the regex since `^a|b$` anchors only its ends. The
   unique-items hash mixes eight bytes at a time.

Measured on the like-for-like basis (`blazebasis 200`, pinned, idle box), baseline binary, new binary, baseline
again, so the third run gives the noise floor (0.99 on the geometric mean, up to 8% on a single corpus, and yamllint,
whose whole corpus is 20 µs, swings by half):

| Corpus | Before | After | After / before |
|---|---|---|---|
| jasmine | 107 µs | 68 µs | 0.63 |
| aws-cdk | 36 µs | 24 µs | 0.67 |
| lazygit | 88 µs | 59 µs | 0.68 |
| unreal-engine-uproject | 444 µs | 300 µs | 0.68 |
| pulumi | 417 µs | 309 µs | 0.74 |
| jsconfig | 352 µs | 268 µs | 0.76 |
| cmake-presets | 3.23 ms | 2.55 ms | 0.79 |
| cypress, fabric-mod, openapi, vercel | | | 0.80 to 0.83 |
| clang-format, ansible-meta, draft-04, stylecop, ui5 | | | 0.84 to 0.86 |
| stale, helm-chart-lock, cql2 | | | within noise |
| geometric mean over 37 | | | 0.85 |

Blaze, re-run the same afternoon (its own figures were 8% faster than in the earlier session, so both sides are
taken from the same hour): geometric mean of our runtime over Blaze 1.06, from 1.23 for the baseline binary the same
day. We lead on 17 corpora (ui5-manifest 0.25, jshintrc 0.40, clang-format 0.50, omnisharp 0.55, openapi 0.56, cql2
0.64, draft-04 0.76, cypress 0.78, pre-commit-hooks 0.79, ansible-meta and tmuxinator 0.82, deno and fabric-mod
0.85, lerna and aws-cdk 0.87, geojson 0.88, lazygit 0.92); within 20% on eleven; and still behind on the
small-instance corpora: helm-chart-lock (5.2, unchanged), importmap (3.9), yamllint and ui5 (2.4), semantic-release
and krakend (1.7). What remains there is the per-evaluation and per-object fixed cost, which the entry change only
dented.

Two measurement notes. The `quick 40` protocol is too noisy for changes of this size: its generated-code column, an
identical binary on both sides, moved by a median 10% and up to 2.7x between paired runs, so a paired `quick`
comparison cannot see a 10% change; `blazebasis` with the baseline run twice is the protocol now. And a method with
`stackalloc` cannot use on-stack replacement, so the JIT compiles the object loops straight to full opts without
dynamic PGO; nothing in them is virtual, so this costs little, but it is why `EvalObjectPlanCore` shows one tier in a
disassembly.

## Strict object plan and per-property mechanics (2026-09-11, third round)

helm-chart-lock is the simplest shape in the corpus (an object of three required properties with
`additionalProperties: false`, one of them an array of such objects) and our plan for it was already the best we
had, so what was left was the cost per property: about 13 ns against Blaze's 2.5. Three commits:

1. **Name map keys as word loads; scratch context per call.** Keys of up to sixteen bytes (the bucket guarantees
   equal length) compare as one or two overlapping word loads instead of a `SequenceEqual` call. The
   `JsonSchemaContext` the shared format helpers need is a local at its three call sites instead of a field the
   evaluation state zeroed on every entry.
2. **Type-only children tested in place; `required` as a mask.** A property entry, and a fused application, whose
   child is a type-only leaf carries the child's type mask, so the object plan, the fused plan and the strict loop
   test the token type without a call (and skip a child that is `true`); with at most 64 seen bits, `required` is
   one mask test. Derived from the graph at compile time and on image load.
3. **`NodePlan.StrictObject`.** An object node with named properties only (no pattern properties, dependencies or
   additional-properties schema; `additionalProperties` absent, `true` or `false`) and at most 64 seen bits gets one
   loop: look the name up, reject an unknown name when additional properties are false, test the token type for a
   type-only child and dispatch the rest on their plan, then one mask test for `required`.

Measured on the like-for-like basis after the box had recovered (the timer-overhead column read 13.5 ns in all
three runs; noise floor 1.03 on the geometric mean):

| Corpus | Before | After | After / before |
|---|---|---|---|
| helm-chart-lock | 445 µs | 270 µs | 0.61 |
| gitpod-configuration | 125 µs | 77 µs | 0.62 |
| jshintrc | 187 µs | 131 µs | 0.70 |
| nest-cli | 109 µs | 76 µs | 0.70 |
| dependabot | 282 µs | 199 µs | 0.71 |
| omnisharp | 134 µs | 99 µs | 0.74 |
| lerna, deno, pre-commit-hooks, cmake-presets, cypress, cspell, importmap | | | 0.77 to 0.82 |
| ui5, semantic-release, tmuxinator, openapi | | | 1.04 to 1.12, inside that run's noise |
| geometric mean over 37 | | | 0.86 |

Against the same-day Blaze run the geometric mean of our runtime over Blaze is now 0.89; we lead on 22 corpora.
helm-chart-lock is at 70 ns per instance against Blaze's 24, importmap 3.1 times Blaze, yamllint 2.1, ui5 2.7. The
remaining gap on those is the per-evaluation entry (state setup and the root dispatch, about 15 ns) and the
per-object prologue of the plans; the property loop itself is now a lookup, a word compare and a token-type test.

A measurement note for the record: from mid-afternoon the host ran the VM at a third of its speed (a throttled
clock and runaway network traffic on the host), which the overhead column showed as 30 to 1,200 ns while guest load
and memory looked normal. Every run in that window was discarded; the column is the health check before trusting
any run.

## In-place plans, maps and inline enums (2026-09-11, fourth round)

Profiles of the corpora still behind Blaze after the strict plan pointed at three more mechanics, each its own
commit:

1. **Strict plan for maps and any `additionalProperties`.** The unknown-name decision is precomputed on the node:
   rejected (`false`), tested in place (a type-only leaf), dispatched on its plan (any other schema) or allowed. An
   object whose only keyword is `additionalProperties` (importmap's maps of strings) skips the name lookup
   altogether.
2. **Plans for nodes whose only keyword is a typed `anyOf`/`oneOf`.** `NodePlan.TypeUnion` and
   `NodePlan.TypeDispatch`: the child entry tests the mask or reads the table and jumps to the branch's plan without
   the general node prologue (semantic-release's `plugins` items are a string or an array, its `branches` a string,
   an object or an array). A branch on an in-place cycle keeps the guarded general edge.
3. **String enums in place; lookups without copying.** A property entry or fused application whose child is a leaf
   with only an `enum` of strings carries the string set and is tested in the loops without a call, from the raw
   text when unescaped. `Utf8NameMap` gained a tagged lookup, so discriminator values and fused value tests no longer
   copy the value behind a tag byte.

A fourth change was tried and reverted: allocating the dynamic-scope stack only for programs that need it made the
`stackalloc` conditional, which turns it into a variable-size `localloc` and changed the entry method's frame setup
on every call; the small-instance corpora (cypress, aws-cdk, nest-cli, stale, yamllint) lost 7 to 16% from that
alone. The scope stack is back to a fixed frame slot.

Two lessons from measuring this round. First, the box's page-reporting order had been raised from 5 to 10 during
the afternoon, which halved guest throughput for everything (identical binaries ran 1.7 to 2.0 times slower on the
timer-free `quick` mode, and the overhead column read 19 ns instead of 13.5); every measurement taken in that state
was discarded once the cause was found, and the setting is back at the default. Second, the first cool-box run
showed a group of small corpora 10 to 30% slower: the child dispatch is inlined into every loop, and the two new
plan cases plus the inlined string-set test had doubled the strict loop's tier-1 code (3,136 to 6,042 bytes). The
loop now resolves a property to one mask, set or child and runs one type test and one dispatch, with the new plans
and the set test out of line (3,259 bytes).

Measured three-way on the cool box (overhead column 13.4 to 13.9 ns), twice; noise floor 1.02 on the geometric mean
and up to 10% on a single small corpus:

| Corpus | Before | After | After / before (two runs) |
|---|---|---|---|
| semantic-release | 74 µs | 42 µs | 0.56, 0.57 |
| importmap | 45 µs | 27 µs | 0.65, 0.54 |
| fabric-mod | 253 µs | 195 µs | 0.78, 0.74 |
| dependabot | 210 µs | 165 µs | 0.82, 0.74 |
| tmuxinator | 36 µs | 28 µs | 0.70, 0.85 |
| pre-commit-hooks, cql2, stylecop, draft-04, cmake-presets, deno, krakend, pulumi | | | 0.85 to 0.94 |
| the rest | | | within the noise |
| geometric mean over 37 | | | 0.927, 0.917 |

Against the same-day Blaze run the geometric mean of our runtime over Blaze is 0.85; semantic-release is level with
Blaze (0.93) and importmap at 1.9 from 2.9. helm-chart-lock did not move: its cost is the per-object prologue and
the per-evaluation entry. A flag-mode entry without the scope buffer and the try/finally followed as its own
commit and measured neutral to a few percent on the smallest corpora in an alternating A/B (five runs of each
binary on seven corpora, medians: yamllint 1.02, cypress 0.98, aws-cdk 0.97, cspell 0.92), so the entry is not
where the remaining fixed cost lives; the per-object prologue is the open item.

A third measurement lesson, and the cause of every "idle guest but slow" episode of the day: Windows core parking.
The host's power scheme had `CPMINCORES` at 4, which parked sixteen of the twenty logical processors, all six
P-cores among them, and left the VM and everything else on four E-core threads; from inside the guest that showed
as a run queue in the fifties with 96% idle, the same loop taking 0.12 to 0.89 s depending on which guest CPU it
landed on, and pinning inside the guest changing nothing, since guest CPU numbers have no fixed relation to host
cores. Setting `CPMINCORES` to 100 (`powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100`, the
same for DC, then `/setactive`) is the fix; pinning the `vmmemWSL` process to the P-cores is a useful belt and
braces. The harness's overhead column (13.5 ns healthy) is the check to run before trusting any measurement, and
alternating the two binaries in short runs and comparing medians is the protocol for changes of a few percent.

## The strict loop's instruction budget (2026-09-11, late)

With the entry shown not to be the fixed cost, the tier-1 disassembly of the strict loop was read instruction by
instruction: about 150 per property with some twenty bounds checks, against the thirty or forty Blaze needs. Four
of the sources were avoidable without giving up the checks that matter: the value row's header (type and row count)
was read once for the type test and again to step to the next row; the name row's location and length were two
checked reads where one 8-byte read serves; the key compare did six checked slice reads on spans already known to
hold `n` bytes; and each property's entry was a class reached through a checked array load, then five field loads.
One commit takes all four: `IDocumentAccess.TokenTypeAndNext`, a single read for the name row, the key words read
through refs, and a value-type `StrictEntry` table parallel to the name map (`Utf8NameMap.TryGetIndex`).

Alternating A/B (five runs of each binary, medians): helm-chart-lock 0.85, ui5 and krakend 0.89, vercel 0.91,
aws-cdk 0.92, cypress, cspell, jsconfig and stale 0.95; yamllint, whose root has one named property and whose
instances have none of it, unchanged within noise. The same recipe then went into the object plan's loop and the
fused loop (one header read, the type handed to the inline tests, the object plan resolving through the value table
by index): against the round's baseline, draft-04 0.77 (its root sits on the object plan for its dependencies),
helm-chart-lock and ui5 0.88, aws-cdk 0.91, cypress 0.94, krakend 0.95. yamllint alone reads about 1.10 in both
A/B runs, some two nanoseconds per instance on a corpus whose properties are all unknown names; the loop's
prologue for that case is the one place left to look at there.

The bounds checks were then sorted by what makes them unnecessary. Five per property are guaranteed by the name
map's construction (the position read is within a key of the bucket's length, the table has 256 entries indexed by
a byte, and the key, chain and entry indices came out of that table) and now go through refs; the two row reads are
covered by one check per object that the container's rows exist; the slice into the text keeps its check, since its
location and length come from the rows. The type test became a bit test: a strict entry carries the token types it
accepts as one bit per token type, and only the integer case still calls out. The escaped-name lookup moved out of
line to relieve register pressure. Against the round baseline, medians of five: helm-chart-lock 0.83 (from 0.88),
importmap 0.55 (0.59), krakend 0.89 (0.95), stale 0.90 (0.95), vercel 0.93 (0.98), with the rest unchanged. The
residual against Blaze on these corpora is now structural (span headers, the per-child plan switch, the JIT's
register allocation across a large method) rather than incidental. The object plan's loop and the fused loop got
the same treatment afterwards, worth cypress 0.84 (from 0.93) and draft-04 0.75 (0.76), the rest within noise.

Two more from the profiles taken after that. The name map's lookup had stopped inlining: the bucket struct was
nested in the generic map and the unchecked accessor was a generic method on it, so the shared instantiation needed
a runtime type lookup and both showed up as frames. With the bucket and the accessor moved to non-generic types the
loops inline them again, which was yamllint's unexplained 10% (0.98 from 1.10) and a further 4 to 8% on
helm-chart-lock, stale, aws-cdk and jsconfig. And krakend spent a quarter of its time in a regex: it applies
`^[@$_#]` as a pattern property on 128 objects, so every property of every instance went through a UTF-16 transcode
and a regex scan. A start-anchored class sequence without a closing anchor now matches as a prefix (fixed counts
before the last atom, the last atom's minimum count), and krakend halved (0.50 from 0.88).

A last pass on the call chain between the entry and the loops: forward chains collapse to their final target at
compile time and the entry skips a forwarding root; the strict object plan is entered in one call with its prologue
and loop in one method; the array-items loop steps siblings unchecked after one range check. Inlining the flag-mode
entry into `Evaluate` was tried and cost yamllint 15%, so the entry stays a call. importmap 0.49 (from 0.54), the
rest within noise.

The table taken on the healthy box before that last pass (both engines back to back, overhead column 14.6 ns): our
runtime is fastest on 27 of 37 corpora, geometric mean 0.80 against Blaze; krakend, cypress and dependabot moved to
our side, and only ui5-manifest (1.08), dependabot (1.11), cspell (1.13) and jsconfig (1.17) sit above parity
besides the four structural ones: importmap 2.0, ui5 2.2, yamllint 2.3, helm-chart-lock 2.9.

Two more after that table. An object with no named properties takes a map loop that reads no names: every value
takes the additional-properties resolution (importmap 0.39 from 0.49). And on the fused plan an entry's keyed value
tests merge into one map from tagged value to the mask of tests that allow it, so a property under several
conditions is looked up once, while the string-set membership test takes the token type its caller already has and
reads the value's row once (ui5 0.84 from 0.88, cspell 0.92 from 1.05, draft-04 0.74 from 0.79, jsconfig 0.88 from
0.92).

Two from the shapes of individual corpora. cspell tests its word lists with `^(?=[^SET]+$)(?=(.*\\w)).+$` (and the
`!+` variant): a lookahead that excludes a set and one that demands a word character. That form is recognised and
matched as one scan of the bytes, none in the set or a line terminator, at least one ASCII word character; cspell
0.73 (from 0.92). And jsconfig's root is a oneOf of three object branches over disjoint property names, which the
general path applied one branch at a time, each with its own pass over the instance. An unconditional oneOf or
anyOf whose branches carry object keywords now fuses into the object pass as a contributor group: each branch is a
contributor tagged with its group and branch index; a failed application marks the branch in a per-group bitmask
instead of failing the object; and the group is decided after the pass, any survivor for anyOf, exactly one for
oneOf. The group is refused when the keyword has a discriminator or type union (the general path decides those from
one property), when the node tracks coverage, and when a property name would receive an expensive child, anything
but a leaf, a simple array or a boolean schema, from more than one branch. That last guard came from cmake-presets,
whose eight discriminated branches share names with large children and went 5.8 times slower on an unguarded
version: applying every branch to every property is only cheaper than one branch at a time when the branches
mostly reject by name. jsconfig 0.68 (from 0.88), cmake-presets unchanged.

Three more tries on ui5 before the one that paid. Its root and the levels of its type-and-version decision tree
were on the general path. Their conditions key on `null` in places, which is now a keyable value for fused
conditions and discriminators, but that moved only two nodes: the real blocker is size, since fusing the root would
take about 80 contributors against the limit of 64. Raising the limit fused the root and made ui5 1.5 times slower
than the baseline: the fused pass applies every property to every gated application, and against a tree that
mostly rejects by one property's value that costs more than walking one branch. A shallow variant, folding a
branch whose subtree does not fit as an opaque child evaluated after the pass, was 1.17: the pass itself, with its
deferrals and condition tables, costs more per level than the general path's one-property `if` scan. What was
expensive was not the scans but the general path's in-place bookkeeping around each level, about a fifth of ui5's
time in `EvalInPlace`, `EvalIf`, `EvalCore` and their callees. A node whose keywords are type and object keywords
plus if/then/else, and which does not fuse, now takes a conditional plan: the strict or object plan for its own
keywords, then the `if` and the branch it selects dispatched as plain children. ui5 0.72 of the round baseline
(from 0.87), openapi 0.82 (from 0.93). A pattern of the form `^ES5|ES6|ES7$`, whose anchors bind to the first and
last alternatives only, also matches without the regex now; jsconfig did not move, so that pattern is not on its
hot path.

The entry's fixed cost, read from the tier-1 listing of the public `Evaluate`: it inlined the whole flag-mode
entry and with it the general entry's stack scope buffer, which the frame then zeroed on every call (the public
class lacked `SkipLocalsInit`), and the evaluation state was built as a zeroed temporary and copied over. The
general entry is no longer inlined, the class skips locals init, and the state is written field by field in place:
frame 376 to 232 bytes, entry code 3061 to 2827 bytes, yamllint 0.91 of the round baseline. What remains at the
entry is the document type check, the raw-access construction and the memory-to-span conversion for the UTF-8
text, about twenty instructions. And ui5's bundle arrays carry `additionalProperties: false`, which applies to
nothing on an array; plan selection now treats object keywords under a type that excludes objects, and array
keywords under a type that excludes arrays, as absent, so those nodes take the array-items plan (ui5's general-path
nodes 7 to 4, ui5-manifest 0.85 from 0.88).

The table at the end of the day, on a healthy box (overhead 14.0 ns, both engines back to back): fastest on 30 of
37, geometric mean 0.77. Since the morning table jsconfig (1.17 to 0.82), cspell (1.13 to 0.84) and dependabot
(1.11 to 0.94) crossed to our side; importmap 2.02 to 1.57, ui5 2.15 to 1.72 and helm-chart-lock 2.86 to 2.38 on
the structural gaps; yamllint 2.33. One correction to the morning table: its babelrc row (0.46) was an artefact of
the harness's per-corpus overhead probe spiking during that run, so too much was subtracted; babelrc is about 52
µs on every build including the round baseline, 1.13 against Blaze, and the morning's honest count was 26 of 37.
Two lessons about measurement from the day. The host, a laptop, drops to a lower performance mode when its screen
sleeps even while work continues over a remote session; both engines run about 35% slower and stay there, and
nothing shows from inside the VM (no steal, same clocksource, same speed on every vCPU). Only alternating A/B pairs
are trustworthy in that state; the overhead column of the harness's output (13 to 15 ns healthy) is the tell. And a
single spike of that probe zeroes or inflates one corpus's row, so every row's overhead column is checked before
a table is read.

Two more on the fixed costs, taken on the healthy box. The parsed document now keeps the array behind its UTF-8
memory when there is one and hands the evaluator its rows and text as spans in one call, so the entry no longer
converts a memory to a span per evaluation; and the node a root's flag-mode evaluation enters is computed with the
forwards instead of resolved on every call. And a strict-object child whose type admits objects, reached from a
loop that already holds the value's token type, is entered at its loop directly: no second token-type read, no type
test, no plan dispatch. yamllint 0.79 of the round baseline (from 0.91), helm-chart-lock 0.72 (from 0.78), the rest
within noise. What remains at the entry is the document type test and the raw-access construction; what remains per
object is the loop itself, whose per-property step is about the same work as Blaze's. The remaining gap on the
small-instance corpora is the difference between a generic loop and a per-schema compiled closure, and the two
core-library options that would narrow it further (a pre-classified name word per row, or short keys inline in the
row) were judged not worth their parse-time cost, which every instance pays.

A measurement correction that moves the table more than any single optimisation did. The Blaze CLI is run once
per corpus; our harness ran every corpus in one process, and the JIT's dynamic profile (block layout, guarded
devirtualisation) is shaped by whichever corpora ran first, so a small corpus late in the order reads 10 to 30%
slower than on its own: yamllint 2.69 in the one-process take against 1.90 in its own process on the same build
and box, jshintrc 0.28 against 0.22, importmap 1.63 against 1.40, code-climate 0.90 against 0.75. Blaze's figures
do not move between the two. The table is now taken one process per corpus on both sides: at commit 4e98a9c on
the healthy box (overhead 14.6 ns), fastest on 31 of 37, geometric mean 0.73; the remaining losses are
helm-chart-lock 2.32, yamllint 1.90, ui5 1.69, importmap 1.40, ui5-manifest 1.14 and stale 1.02, with babelrc at
0.97.

## Cold start against Blaze (2026-09-12)

Wall time of one fresh process per corpus (median of three, pinned, healthy box): read the schema, compile it or
load a program image, read the instances file, validate every instance once, which is what the Blaze CLI's
`validate --fast` does. The harness's `cold` command and the `Corvus.Text.Json.RuntimeEvaluator.ColdRunner` project
run it; the runner publishes as ReadyToRun or native AOT (`-p:ColdAot=true`). Under the JIT the run is dominated by
start-up and JIT time: about 55 ms to reach `main` and 60 to 140 ms of JIT for the compiler before any validation,
so every corpus takes 120 to 320 ms against Blaze's 6 to 250 ms (median ratio 14.6, geojson and openapi the only
ones within 3x). ReadyToRun removes most of the JIT but keeps the runtime start-up: 42 to 330 ms (median ratio
4.4). Native AOT removes both: 3.6 to 155 ms, faster than Blaze on all 37 corpora, median ratio 0.50, totals 445 ms
against 1024 ms. Under AOT the schema compile is 0.1 to 3 ms for all but two corpora (krakend 6 ms and ui5-manifest
10 ms, their regular expressions), and loading a program image instead brings ui5-manifest from 26 to 15 ms; the
validation pass, parse included, is 0.5 to 12 ms for all but geojson (108 ms, large instances) and openapi (28 ms).
Blaze's own compile is what costs it on the large schemas: ui5-manifest 165 ms and openapi 100 ms end to end.
Table: `cold-start-2026-09-12.md` in the session notes.

Blaze's compile can be measured on its own through its `compile --fast --minify` command (wall time in a fresh
process less the 2.3 ms of a bare `--version`; it includes writing the template as JSON, so it slightly overstates).
Medians over the corpora: Blaze 2.9 ms, ours under native AOT 0.34 ms, a ratio of 0.11; the large schemas are where
it tells, ui5-manifest 207 ms against 9.9, krakend 64 against 6.3, ui5 39 against 3.0, cmake-presets 24 against 1.7.
Blaze's templates are big (ui5-manifest's is 41 MB of JSON, cql2's 5.5 MB), so its `validate --template`, the
analogue of our program image, is no faster than compiling on those: parsing the template costs what the compile
did. Precompiled cold start like for like (Blaze `validate --template` against our AOT runner loading an image):
medians 9.8 ms against 4.7 ms, a ratio of 0.49, ours faster on all 37. Table: `compile-2026-09-12.md`.

The warm measurement under the same three builds (the runner's `warm` command, one process per corpus, fresh Blaze
run): the JIT's tier-1 code is fastest on 30 of 37 at a geometric mean of 0.75 against Blaze; ReadyToRun 23 of 37
at 0.87; native AOT 23 of 37 at 0.88, about 17% behind the JIT at the median. The machine's instruction set instead
of the x64 baseline moves AOT by 2 to 4%, so the difference is dynamic PGO (guarded devirtualisation and
profile-driven layout), which native AOT lacks. The trade is therefore start-up against steady state: native AOT
starts in 4 to 25 ms where the JIT takes 120 to 320 ms, and settles 15 to 20% slower per evaluation. Table:
`warm-2026-09-12.md`.

## Regular expressions without a regex (2026-09-12, late)

Native AOT has no compiled regular expressions, so every `pattern` the matcher kinds did not cover ran in the
interpreter there, and the harness's `patterns` command counted 68 such pattern nodes across the corpora (335 in
all). The matcher's special kinds are replaced by one general form. A pattern is alternatives of atom sequences,
each anchored where its own `^` and `$` say, so `^a|b|c$` anchors only its first alternative at the start and its
last at the end, and unanchored ones search (with a vectorised start when the first atom is one to three ASCII
bytes). A sequence qualifies when every variable atom but the last is followed by atoms that admit none of its
characters, up to the first that must consume something, so its greedy run ends exactly where the regex's would;
the atoms after the last variable one are fixed and are matched from the end. Groups of alternatives, optional
groups and small fixed repeats flatten into whole alternatives. Classes gained negation, `\d \w \s` and their
negations with ECMA-262's ASCII semantics (plus the non-ASCII members of `\s`, kept exactly), and literals outside
ASCII such as `µ`. A separated-list kind takes `^item(sep item)*$` and `^(item sep)*item$` when the separator
starts with a character the item's variable atom does not admit, with a different first or last item allowed. The
agreement test checks every pattern, the corpus ones included, against the translated regex on every input.
Regex-backed nodes fall from 68 to 8, the eight being backtracking shapes (`[a-z]+[a-z0-9]+`, a dot after a
variable atom, semver) and a lookahead. Under AOT cspell, krakend, ui5-manifest and unreal-engine-uproject are 20
to 25% faster; under the JIT, where the regex was compiled, they are 3 to 10% faster; jsconfig, whose one
alternation was a compiled literal search, sits 10% slower under AOT than the interpreted regex did and unchanged
under the JIT.

## What a ReadyToRun library and a static profile would give (2026-09-12, late)

Two experiments with the cold runner, one process per corpus, medians over the corpora. Published framework-
dependent with `PublishReadyToRun` (the runner and the library precompiled; the shared framework already is) the
cold run drops from 107 ms to 53, from an image 79 to 41, the compile step 56 to 24 ms and the first validation
pass 20 to 3.9 ms: shipping the library precompiled would halve cold start for every consumer under the JIT. The
steady-state measurement under the precompiled library read slower on the smallest corpora (yamllint 1.49x,
helm-chart-lock 1.20x), which says the precompiled code had not been replaced by tier-1 code with a profile within
the warm-up; to be settled before it is recommended.

And the AOT gap is dynamic PGO: with `DOTNET_TieredPGO=0` the JIT's steady state is 1.14x slower at the median and
native AOT is 1.12x, matching corpus by corpus (cspell 1.33 against 1.31, draft-04 1.27 against 1.33, helm-chart-lock
1.16 against 1.16, yamllint 0.98 against 1.06). ILC takes a static profile (`--mibc`, the `MibcFile` item), which
is how the framework itself is profile-optimised under AOT, so a profile of the corpus run through dotnet-pgo, built
from the runtime repository since it is not shipped, should recover most of the gap; the listings with and without
PGO (`DOTNET_JitDisasm` on the hot loops) are the map for the alternative of making the devirtualised sites direct
in source.

dotnet-pgo, built from the runtime repository (`src/coreclr/tools/dotnet-pgo`; the `clr.tools` restore needs the
NuGet audit off for an unrelated project), turns an instrumented trace of the warm run over every corpus
(`DOTNET_TieredPGO=1`, a call-count threshold of 10,000 so methods stay instrumented long enough, `ReadyToRun=0`,
the runtime provider at keyword 0x1E000080018 level 5) into a MIBC of 2,656 methods of ours. Published with it
(`MibcFile`, the runner's `ColdMibc` property; `measure.sh profile`), native AOT goes from 1.15x the JIT's steady
state to 1.08x at the median: importmap 1.18 to 0.93, openapi 1.10 to 0.98, yamllint 1.09 to 1.01, jsconfig 1.27
to 1.07, cspell 1.30 to 1.11, draft-04 1.33 to 1.16. Half the gap, from a four-second trace; the rest is what the
static profile cannot express or ILC does not use.

The ReadyToRun anomaly is real and has a different cause than expected. Forcing the JIT on the precompiled build
(`DOTNET_ReadyToRun=0`) restores parity (yamllint 20.7 to 14.9 µs, helm-chart-lock 247 to 205). The tier listing
(`DOTNET_JitDisasmSummary`) shows the hot methods are re-jitted from the precompiled code, through an instrumented
tier 1 to tier 1, but the tier-1 code they end with is a quarter of the size the pure-JIT process produces
(EvaluateFlagRaw 585 bytes against 2,420): on that path the JIT does not inline the loops into the entry, and a
profile given to crossgen (`PublishReadyToRunPgoFiles`) changes nothing since the runtime re-jits anyway. What
works is partial precompilation: a profile of the compile phase alone (a trace of `prepare`, which compiles every
schema and writes its image: 1,679 of our methods, none of the evaluation loops) and crossgen's `--partial`
(the runner's `ColdPartial` property), so the compiler is precompiled and the evaluation loops stay with the JIT.
Cold start keeps most of the full-R2R gain (yamllint 50 ms against 49 and 84 for the JIT, helm-chart-lock 55
against 54 and 89, ui5 113 against 88 and 159) and steady state is at JIT parity on every corpus (yamllint 14.2
against 14.0 µs, ui5 421 against 423). That is the form to ship for JIT consumers; `measure.sh profile` produces
both profiles.

What the profile buys, in source terms, was tried and rejected. Without a profile the public `Evaluate` is a
37-byte stub: it calls `Evaluator.Evaluate`, which calls the flag-mode entry, which dispatches the root plan, three
calls per evaluation; with the profile the JIT inlines the chain into a 2.9 KB entry. Marking the first two links
`AggressiveInlining` reproduces that chain everywhere, and native AOT gains 4% from it at the median (yamllint 8%),
less than the MIBC gives (1.08 to 1.04 against the JIT). But the JIT with the attribute loses what it had gained on
yamllint (0.79 of the pre-round baseline to 1.01): forcing the inline at the entry changes what the JIT then
inlines inside it, the same lesson as an earlier attempt to inline the flag-mode entry. The attributes are not in
the tree; the static profile is the route for AOT and the JIT keeps its own judgement.

## The four-axis table (2026-09-12, evening)

One table, eight implementations by 37 corpora, four measures each: `summary2-2026-09-12.md` in the session notes.
The rows pair Blaze's two paths (compile at run time; validate from its precompiled template) with ours: the runtime
evaluator compiling at run time, the runtime evaluator loading a program image, and the shipping generator's
strongly typed models, each under the JIT and as native AOT. The measures: cold (one fresh process, prepare, read
the instances, validate each once), warm (per evaluation at steady state), compile (the preparation step alone;
for generated code, its first evaluation), and memory as bytes allocated per evaluation at steady state (the
harness's `GC.GetAllocatedBytesForCurrentThread` over the timed loop; Blaze's binary carries its own allocator, so
its column is not measurable from outside, though its heap is flat across benchmark loops). Medians over the corpora:

| implementation | cold | warm | compile | memory | warm faster than Blaze on |
|---|---:|---:|---:|---:|---:|
| blaze-compile | 10.5 ms | 118 µs | 2.9 ms | n/a | |
| blaze-template | 9.8 ms | 118 µs | 0 | n/a | |
| corvus-runtime-jit | 154 ms | 80 µs | 77 ms | 0 B | 31 of 37 |
| corvus-runtime-aot | 5.2 ms | 108 µs | 0.34 ms | 0 B | 22 of 37 |
| corvus-image-jit | 108 ms | 80 µs | 26 ms | 0 B | 31 of 37 |
| corvus-image-aot | 4.7 ms | 108 µs | 0.16 ms | 0 B | 22 of 37 |
| corvus-generated-jit | 124 ms | 82 µs | 38 ms | 0 B | 30 of 37 |
| corvus-generated-aot | 5.2 ms | 91 µs | 0.17 ms | 0 B | 25 of 37 |
| corvus-generated-previous-jit | 132 ms | 352 µs | 26 ms | 0 B | 1 of 37 |
| corvus-generated-previous-aot | 6.7 ms | 513 µs | 0.07 ms | 0 B | 0 of 37 |

The generated rows needed a correction on the way. The benchmark model projects' C/ directories dated from August,
before Stage 0, so their first measurement was of the previous generator's per-keyword validation code: 4.4 times
slower than the runtime evaluator at steady state (352 against 80 µs at the median), behind Blaze on 36 of 37,
and a profile of helm-chart-lock showed why: document access through the `IJsonDocument` interface per row, a
generic property-matcher map running as the shared instantiation and returning a delegate per property, a
`JsonSchemaContext` pushed and committed per child, and type-only leaves evaluated through a call. Those rows are
kept as `corvus-generated-previous`. Regenerated with the current generator (`Regenerate-CurrentBenchmarks.ps1`),
every model embeds a precompiled program image and validates through the runtime evaluator, and measures as the
runtime rows plus a thin call: 82 µs under the JIT, 91 µs under AOT, ahead of Blaze on 30 and 25 of 37, agreeing
with the runtime evaluator on every instance. What the table settles: the runtime evaluator, and now the generated
code, allocate nothing per evaluation on any corpus; and under AOT every Corvus row starts in 5 ms against Blaze's
10.

A measurement lesson from the generated rows: their first take subtracted a clock overhead read at 140 to 480 ns
instead of 14 on twelve corpora, a tier-0 loop in a method run once, which took a 100 µs corpus down to 25. The
clock's cost is now taken from a warmed non-generic loop as the minimum over several batches, in both measurements.

The table taken again at the end of the day with everything above in place (`tools/measure.sh profile`, `publish`,
`warm`, `cold`, `table`; commit c44ee2b's matcher, the partial ReadyToRun with the compile-phase profile as the
R2R row, the static profile in the native AOT rows; overhead 12.6 to 14.4 ns on every row). Medians over the corpora:

| implementation | cold | warm | compile | memory | warm faster than Blaze on |
|---|---:|---:|---:|---:|---:|
| blaze-compile | 10.2 ms | 115 µs | 2.9 ms | n/a | |
| blaze-template | 10.2 ms | 115 µs | 0 | n/a | |
| corvus-runtime-jit | 150 ms | 76 µs | 77 ms | 0 B | 31 of 37 |
| corvus-runtime-r2r (partial, profiled) | 60 ms | 79 µs | | 0 B | 30 of 37 |
| corvus-runtime-aot (profiled) | 4.7 ms | 90 µs | 0.36 ms | 0 B | 29 of 37 |
| corvus-image-jit | 125 ms | 76 µs | 25 ms | 0 B | 31 of 37 |
| corvus-image-aot (profiled) | 4.4 ms | 90 µs | 0.15 ms | 0 B | 29 of 37 |
| corvus-generated-jit | 119 ms | 80 µs | 42 ms | 0 B | 31 of 37 |
| corvus-generated-aot (profiled) | 5.1 ms | 84 µs | 0.18 ms | 0 B | 28 of 37 |

Against the morning's table the native AOT rows went from 108 to 90 µs (the matcher and the profile; 1.09 of the
JIT at the median, from 1.35) and from 22 to 29 corpora ahead of Blaze, the generated AOT row from 91 to 84 and
25 to 28; the JIT rows gained the matcher's share (80 to 76 µs) and the partial ReadyToRun row is new: cold start
at 60 ms against the JIT's 150, steady state at 1.00 of the JIT's. The runtime evaluator under the JIT is fastest
on 31 of 37 at a geometric mean of 0.75 of Blaze; the four structural losses remain (helm-chart-lock 2.4,
yamllint 2.2, ui5 1.75, importmap 1.4), and the two others (ui5-manifest, stale) are within a few percent.

## Against Blaze (2026-09-11)

Blaze is run through the Sourcemeta `jsonschema` CLI release binary (`benchmarks/.../tools/blaze-compare.py`, see
its README) over the same 37 corpora with `--fast`. The CLI's benchmark mode times every evaluation of every
instance on its own, subtracts the clock's overhead, and reports the mean over the loop with no warm-up; the
harness's `blazebasis <loop>` command does exactly the same after a JIT warm-up, so the two are compared like for
like: 200 loops each, pinned, idle box. Geometric mean of our runtime over Blaze: 1.32. We are faster on jshintrc
(0.48), clang-format (0.53), omnisharp (0.59), cql2 (0.65), openapi (0.72), draft-04 (0.80), ansible-meta (0.85),
fabric-mod, pre-commit-hooks, geojson and deno (0.90 to 0.96); within 40% on sixteen more; and behind by two to
five times on helm-chart-lock (5.2), importmap (4.8), yamllint (4.1), ui5 (2.7), semantic-release (2.2) and krakend
(2.0). At 20 loops Blaze's means on the small corpora carry its cold first iterations, so more loops favour it there:
yamllint fell from 14 to 7.6 µs between 20 and 200 loops while ours held at 31.

The corpora where Blaze leads most are the ones with the smallest instances: yamllint is 984 instances at 8 ns each
in Blaze against 32 ns in ours, helm-chart-lock 3,888 at 26 ns against 136 ns. That is fixed cost per evaluation
and per object, not any keyword, which is consistent with the profiles and puts the object-plan inner loop and the
per-evaluation entry path first among the remaining optimisations. The shipping generated code is 3.9 behind Blaze on
the same geometric mean.

## Open decisions

* Resolved: the evaluator is part of `Corvus.Text.Json` (namespace `Corvus.Text.Json.RuntimeEvaluator`, sources under
  `Corvus/Text/Json/RuntimeEvaluator`). Generated code needs no reference beyond `Corvus.Text.Json`, and code generated
  before the merge keeps compiling because the namespace and public API are unchanged; only `internal` details moved.
