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
  are added, and two entries that resolve differently keep the reference dynamic, which is why a generated program
  that registers an entry for every type, including the sub-resource, does not get it for the strict-tree micro case
  (its typed column stays at the general-path figure).
* **Fused object plan** (`FusedObjects`, `NodePlan.FusedObject`) for a node with `unevaluatedProperties` whose object
  semantics spread over `allOf`, `$ref` and `if`/`then`/`else` with required-only conditions: every property name any
  branch knows is resolved at compile time to the child schemas that apply (own property, matching pattern
  properties, or the branch's `additionalProperties`), unknown names resolve per branch at evaluation, conditional
  branches are deferred to a second step over the properties they touched, and the coverage bits feed the unevaluated
  check directly. It is restricted to nodes with `unevaluatedProperties` because there the pass over every instance
  property is unavoidable; fusing branches without it doubled cmake-presets (large instances, small branches, where
  the general path's schema-driven unrolled lookups win).

Micro cases after fusion (quiet-ish machine, ratio is engine after / engine before):

| Case | Engine before | Engine after | Straight-line |
|---|---|---|---|
| Unevaluated | 589 ns | 142 to 170 ns | 151 to 181 ns |
| DynamicRef (strict tree) | 878 ns | 226 to 242 ns | 192 to 200 ns |
| Object, Array, OneOf | unchanged | unchanged | |

Corpora: openapi (27 `unevaluated*`) 8.1 ms to 7.2 ms; cmake-presets unchanged after the restriction; the rest within
noise. The general path remains the fallback for every shape the plan does not take, and `CORVUS_RT_NO_FUSE=1`
disables it for A/B runs.

## Open decisions

* Whether the evaluator package merges into `Corvus.Text.Json`, which decides the assembly the emitted program targets.
