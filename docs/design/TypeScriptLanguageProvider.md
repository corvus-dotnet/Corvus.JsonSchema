# Design: A High-Performance, Idiomatic TypeScript Language Provider

> **Status:** Draft for review · **Author:** generated research + design · **Scope:** a new
> `ILanguageProvider` (written in C#) that plugs into the existing language‑neutral code‑generation
> core and emits idiomatic, high‑performance TypeScript with the same *kind* of performance
> characteristics and the same JSON Schema compliance as the V5 C# language provider.

> **Note on code samples.** The C# and TypeScript blocks in this document are *illustrative design
> sketches* of code that does not exist yet. They are deliberately excluded from the documentation
> code‑sample catalog gate (they are not compile‑verifiable against the current tree). Mark them
> `category: fragment` if/when this file is committed.

---

## 1. Goal and the central reconciliation

We want a generator that, given a JSON Schema, produces **strongly‑typed, validated TypeScript**
that is both *idiomatic* (what a TS engineer would hand‑write in 2026) and *high‑performance* (the
same engineering philosophy that makes the V5 C# output fast), with **full JSON Schema compliance**
(drafts 4/6/7/2019‑09/2020‑12 plus OpenAPI 3.0/3.1), verified against the official test suite.

The generator itself is **written in C#**, as a new `TypeScriptLanguageProvider :
IHierarchicalLanguageProvider` that reuses the entire language‑neutral core in
`src-v4/Corvus.Json.CodeGeneration` — exactly parallel to today's
`src/Corvus.Text.Json.CodeGeneration/CSharpLanguageProvider.cs`.

### 1.1 The one decision that shapes everything: the generated runtime model

V5's defining performance trick in .NET is the **bytes‑backed, zero‑allocation document**: each
generated type is a two‑field `readonly struct (IJsonDocument _parent, int _idx)` that decodes
values lazily from a pooled UTF‑8 buffer, with `static abstract` CRTP factories for
virtual‑call‑free generic dispatch.

**The bytes‑backed model transfers to V8 — but conditionally, and the conditions are exactly the
ones Corvus already enforces.** An earlier draft of this section claimed "V8's generational GC
neutralises the rationale." **That was wrong, and is corrected here.** The honest, evidence‑backed
position (see §4 for citations):

* **The .NET allocation‑rate → GC‑frequency mechanism holds in V8 too.** Per‑scavenge cost is
  survivor‑bound, but scavenge *frequency* is allocation‑rate‑bound (a minor GC fires whenever the
  New Space bump pointer hits the end), and a high allocation rate causes **premature promotion**
  (objects copied to Old Space, escalating cheap minor GC into expensive mark‑sweep). Under
  *sustained throughput* this drives main‑thread pause time and tail latency just as it does in
  .NET — the same reason Corvus pools in the first place. Production write‑ups (Plaid 30→2% scavenge
  CPU; Node core +18% from semi‑space tuning) and V8's own team chasing allocation/copy reduction
  inside native C++ (`JSON.stringify` >2× from a non‑reallocating output buffer) confirm minor‑GC
  cost is *not* negligible under load.
* **A bytes‑native, lazy, parse‑over‑spans model genuinely wins — measured, not just theorised.**
  simdjson's Node bindings beat native `JSON.parse` by **~2.1× on large documents when access stays
  lazy/zero‑copy** (e.g. 189 MB payloads). This is the direct JS analog of Corvus's
  `Utf8JsonReader`‑over‑pooled‑bytes thesis, and it works in V8.
* **But the win is narrower and more conditional than in .NET, for three JS‑specific reasons.**
  (1) The baseline is lower — bump‑pointer young‑gen allocation is cheaper than .NET gen‑0, and V8's
  native `JSON.parse` (tuned C++, faster than the equivalent JS literal) is hard to beat on raw
  parse cost. (2) **Escape analysis can make a short‑lived wrapper's allocation vanish entirely** in
  warm, monomorphic JIT code — which is *why microbenchmarks systematically under‑report* the
  allocation cost you are eliminating. (3) A deopt hazard with no .NET equivalent: **reusing mutable
  pooled objects churns hidden classes and can tip call sites megamorphic/dictionary‑mode**, making
  naïve pooling net‑negative.
* **The crossover is sharp.** For **small/medium, fully‑materialised** documents, native
  `JSON.parse` + the C++→JS realisation tax beats a JS‑side reader (simdjson‑Node *loses* on
  citm_catalog/twitter). The bytes model wins when documents are **large or partially accessed**,
  access stays **lazy** (you don't materialise the whole object graph), the hot path is
  **shape‑stable**, and you're **past JIT warm‑up** — and any shared/generic `getField(name)` reader
  that sees every schema's shape will go **megamorphic** and erase the win.

So a faithful port of V5's bytes‑native *lazy‑read* model is defensible only in the
large‑payload/partial‑read corner, and is net worse for the small whole‑document case. But the
stated target workload here is the **read‑modify‑write (RMW)** cycle (`read bytes → validate/access
some fields → modify a few → write bytes back`), and **RMW is not that corner** — it reads *and*
writes the whole document, so lazy‑read buys nothing on the write side. The part of the Corvus model
that *actually* maps onto JSON RMW is not the immutable‑struct reader; it is the **mutable‑builder
partial‑update** pattern ("patch a builder, `Set` only changed fields, never read‑all + rebuild" —
cf. the existing `JsonWorkspace`/`JsonDocumentBuilder` design). Its JS analog is a **structural‑sharing
patch model over the original bytes** (Model C, §1.2/§4.2): keep the `Uint8Array`, index only what
you need, leave unchanged members as byte‑slices, and on write copy untouched ranges through verbatim
while re‑serialising only the fields that changed. That is where a bytes‑backed design genuinely
earns its keep in V8, because RMW's mandatory whole‑document *write* is exactly the operation where
"copy unchanged bytes through" beats "realise → re‑serialise everything".

Independently of which document model wins, V5's *performance philosophy* transfers perfectly and is
exactly what the **fastest** JS schema tools already do:

| V5 philosophy pillar | .NET mechanism | Idiomatic JS realisation |
|---|---|---|
| Codegen‑time specialisation, **no runtime schema interpretation** | emitted `Evaluate` per type | AOT‑emitted monomorphic `evaluate{Type}()` functions (cf. `ajv/standalone`, typia) |
| Kind‑first dispatch, fail‑fast | `switch (JsonTokenType)` + `if (!ctx.HasCollector) return` | `switch (typeof v)` / `Array.isArray` first; early‑return without a collector |
| Avoid allocation / re‑decoding | structs + UTF‑8 + pooling | monomorphic generated code; avoid string materialisation on the hot path *where free*; don't fight the native parser |
| Static dispatch (CRTP) | `static abstract CreateInstance` | per‑schema functions with statically‑named call sites; **never** a generic key reader |

The common ground, whatever the document model, is: **AOT‑compiled, monomorphic, specialised
validation + access code with zero runtime schema interpretation.** That is the engine of the
performance, and it is shared by both models below.

### 1.2 Recommended runtime model (the headline decision)

**These are not three rival runtimes you pick between by speed — they are complementary layers for
different jobs.** The idiomatic types + AOT validators (the §5 surface) are the *baseline every consumer
uses* to **read / validate / consume** parsed JSON — that is what "Model B" names, and it is not a slower
competitor to C for the same task. Our type model is **mutable** (the V5 `JsonDocumentBuilder` analog): reads use the readonly view, and **mutations go through Model C** — the structural-sharing byte-patch engine (§5.7), proven best for read-modify-write / live editing (§13.7-13.9). **Model A** is a niche
lazy-read fast path. So: everyone gets the typed surface + validators; consumers that mutate/edit also
use Model C; almost nobody needs A. The benchmark "Model C wins" is specifically the *RMW round-trip* —
for pure consumption there is nothing to beat, you just read a validated plain object.

All models share one AOT validator skeleton; they differ only in the *value‑access / write* layer
(`emitModel: B | C | A | combinations`). Given the stated target workload is **read‑modify‑write**,
the engineering priority is B (default) and **C** (the RMW path), with A reserved for a separate
lazy‑read need.

1. **Model B — parse‑then‑validate over plain values (the default, and possibly all you need).**
   Consumers `JSON.parse` (or pass an already‑parsed value); we validate with AOT‑generated
   monomorphic code and expose a **type‑only** surface (`readonly` interfaces, discriminated unions,
   branded formats). **Wins the common case** — small/medium, fully‑read bodies/config/messages —
   where native `JSON.parse` + stable monomorphic shapes beat any JS‑side byte reader, and the
   round‑trip is `parse → mutate object → JSON.stringify`. Most idiomatic, most tree‑shakeable.
2. **Model C — structural‑sharing patch model over the original bytes (the RMW‑optimised path; the
   one to engineer carefully).** The JS port of Corvus's *mutable‑builder partial‑update*, not its
   immutable‑struct reader. Keep the original `Uint8Array`; parse only enough to locate top‑level
   (and on‑demand nested) members into a fixed‑shape **index/token table**; unchanged members stay as
   **byte‑slices into the original buffer**; changed members hold new values. On write, **stream the
   output**: copy untouched member byte‑ranges directly buffer→buffer (no decode, no JS‑string, no
   re‑serialise) and serialise only the fields that actually changed; re‑validate only the change
   set. This is the one place a bytes‑backed design genuinely earns its keep in V8 — RMW's mandatory
   whole‑document write is exactly where "copy unchanged bytes through" beats "realise →
   re‑serialise". **Measured (§13.7/§13.8): wins for large or non‑ASCII payloads and for incremental editing;
   the ASCII full‑round‑trip crossover is non‑monotonic** — Model C wins small (~360 B) and large
   (≥256 KB) ASCII but native's C++ parser leads the mid‑range (~4–64 KB) — and native's small, confined mid-range lead does not justify a runtime model-switching fallback, so Model C is used across the board (the ~1.6x ASCII mid-range dip is accepted).
3. **Model A — bytes‑native *lazy‑read* views (a separate opt‑in, only for large/partial *read*).**
   The faithful immutable‑struct port; genuinely wins (~2.1×, §4) **only** when you touch a few
   fields of a large/streamed document and never materialise the rest. Net worse for
   full‑materialisation and irrelevant to RMW's write side. Build only if lazy partial *read* over
   large payloads is its own named target.
4. **The transferable V5 lesson is the *discipline*, not the bytes.** Ports to all models:
   codegen‑time specialisation, **don't materialise/re‑serialise what you don't touch**, kind‑first
   dispatch, **monomorphic accessors**. Do **not** port a bytes document merely because it is
   "faithful to V5" — in .NET it wins broadly (higher GC floor, no escape analysis); in V8 the floor
   is lower and escape analysis erases temporaries, so a bytes design pays off only in the RMW‑write
   (C) and large/lazy‑read (A) corners.

> **⛳ Decisions to confirm.**
> • **Default model:** I recommend **Model B** as the default emit.
> • **The RMW path:** the **stated killer app — editing a live JSON document in a VS Code extension —
>   is an RMW workload**, so this is now a *named, concrete* requirement, not a hypothetical. I
>   recommend **engineering Model C** (the structural‑sharing patch model, §4.2) as the
>   high‑performance path — it maps onto the part of the Corvus C# design
>   (`JsonWorkspace`/`JsonDocumentBuilder` partial update) that actually fits JSON RMW, and it emits
>   exactly the `{offset,length,content}`/`TextEdit` shape VS Code consumes.
> • **Dual‑offset is required, not optional:** because the editor boundary (LSP) is **UTF‑16** while
>   persistence/wire is **UTF‑8**, Model C's index table must carry both offset spaces from one scan
>   (§4.2). "Faithful UTF‑8 bytes everywhere" is the wrong call *specifically at the editor boundary*.
> • **Model A:** defer unless lazy partial *read* over large payloads is independently a target.
> • **Acceptance gate for C and A:** a **sustained‑throughput** benchmark (allocation rate / GC
>   frequency / p99), never a microbenchmark — V8's escape analysis flatters low‑load microbenchmarks
>   into reporting allocation as free.

---

## 2. Background: the engine we are extending

The code‑generation stack is two assemblies joined by one project reference
(`src/Corvus.Text.Json.CodeGeneration.csproj` → `src-v4/.../Corvus.Json.CodeGeneration.csproj`).

```
                         LANGUAGE‑NEUTRAL CORE                     LANGUAGE PROVIDER
            (src-v4/Corvus.Json.CodeGeneration, reuse 100%)   (src/Corvus.Text.Json.CodeGeneration)
 schema ──► JsonSchemaTypeBuilder.AddTypeDeclarationsAsync ──► (graph of TypeDeclaration)
            ├─ IDocumentResolver  (file/http/compound)              CSharpLanguageProvider
            ├─ VocabularyRegistry (draft 4/6/7/2019/2020, OAS)      ├─ ICodeFileBuilder × 3
            ├─ JsonSchemaRegistry (load, $id/$anchor/$ref/scope)    ├─ IKeywordValidationHandler × N  (emit C#)
            ├─ TypeDeclaration graph + reduction/dedup              ├─ INameHeuristic × ~15
            └─ CodeGenerator (indent/line‑ending text buffer)       ├─ INameCollisionResolver
 GenerateCodeUsing(provider, roots) ──► provider hooks ──►          ├─ FormatHandlerRegistry
            IReadOnlyCollection<GeneratedCodeFile> {Name, Content}  ├─ CSharpMemberName : MemberName
                                                                    └─ CodeGeneratorExtensions.*  (C# syntax)
```

**Verified reusable, unchanged, by a TS provider:** schema loading; `$ref`/`$dynamicRef`/
`$recursiveRef`/anchor/scope resolution; the `TypeDeclaration` graph; **structural reduction/dedup**
(N schemas with the same effective shape collapse to one type); the keyword + vocabulary model (94
keyword interfaces, all *data/analysis*, with **zero** code‑emission coupling — grep‑verified); the
`JsonSchemaTypeBuilder` orchestration; the seam interfaces (`ILanguageProvider`,
`IHierarchicalLanguageProvider`, the `IKeywordValidationHandler`/`IChildValidationHandler` family,
`ICodeFileBuilder`, `INameHeuristic`); the registries; and **`CodeGenerator`** — a genuinely
language‑agnostic indented text buffer (`indentSequence`, `instancesPerIndent`, `lineEndSequence`
are constructor params; no braces or C# syntax baked in).

**What is C#‑specific and must be re‑implemented for TS (all already isolated in
`src/Corvus.Text.Json.CodeGeneration`):** the `CSharpLanguageProvider`; the metadata‑bag extension
methods that store `CSharp_DotnetTypeName`/`DotnetNamespace`/accessibility on `TypeDeclaration`; the
concrete validation handlers (they emit literal C#); the code‑file builders (`CorePartial`,
`MutableCorePartial`, `JsonSchemaPartial`); `CodeGeneratorExtensions.*` (C# syntax); name
heuristics' built‑in‑type mapping; collision resolver; format handlers; `CSharpMemberName`;
`EcmaRegexTranslator`; `StandaloneEvaluatorGenerator`.

There is **no neutral intermediate representation** between keyword data and emitted text — handlers
go straight from `IKeyword`/`TypeDeclaration` to language strings. So the TS provider does not
*translate an IR*; it re‑implements the "what to write for keyword X" mapping against a TS runtime
of our design. The core hands us a fully resolved, deduplicated, on‑demand‑named type graph plus a
priority‑ordered handler‑dispatch framework and a text buffer.

> The **Roslyn source generator** (`IncrementalSourceGenerator`, `[Generator(LanguageNames.CSharp)]`)
> is **not** an attach point — it emits into a C# compilation, where TS output is meaningless. The TS
> provider is a **CLI / standalone** concern only.

---

## 3. Capability parity target

“Equivalent capability to V5” means matching this matrix (all confirmed present in V5):

* **Dialects:** Draft 2020‑12 (decomposed into its 8 sub‑vocabularies), 2019‑09 (6 vocabularies),
  7, 6, 4; OpenAPI 3.0 (Draft‑4‑based) and 3.1 (→ 2020‑12); the Corvus extension vocabulary.
  Fallback for schema‑less documents defaults to **2020‑12** (configurable).
* **Keywords:** `type`/`enum`/`const`; `properties`/`patternProperties`/`additionalProperties`/
  `propertyNames`/`required`/`min`‑`maxProperties`; `dependentRequired`/`dependentSchemas`/
  `dependencies`; `allOf`/`anyOf`/`oneOf`/`not`; `if`/`then`/`else`; `items`/`prefixItems`/
  `additionalItems`/`contains`/`min`‑`maxContains`/`min`‑`maxItems`/`uniqueItems`; numeric
  `minimum`/`maximum`/`exclusive*`/`multipleOf`; string `minLength`/`maxLength`/`pattern`;
  `unevaluatedProperties`/`unevaluatedItems`; `$ref`/`$dynamicRef`/`$dynamicAnchor`/`$recursiveRef`/
  `$recursiveAnchor`/`$id`/`$anchor`/`$defs`. Annotation keywords: `title`/`description`/`default`/
  `examples`/`deprecated`/`readOnly`/`writeOnly`/`format` (annotation‑only by default in 2020‑12)/
  content keywords. OpenAPI `nullable`/`discriminator`/`example`.
* **Formats** with dedicated typed treatment: `date`, `date-time`, `time`, `duration`, `email`,
  `hostname`, `ipv4`, `ipv6`, `uuid`, `uri`(+`-reference`/`-template`), `iri`, `json-pointer`,
  `regex`; numeric formats (`int16/32/64/128`, `uint*`, `half/single/double/decimal`, `byte`).
  Assertion modes **assert / disable / warning** resolved at **codegen time** (no runtime branch).
* **Codegen modes:** `TypeGeneration`, `SchemaEvaluationOnly` (standalone evaluator), `Both`;
  annotation collection in verbose mode.
* **Regex:** ECMA‑262 patterns; V5 *translates* ECMA→.NET. TS is native ECMA, so we **drop the
  translator** but **keep the classifier** (`Noop`/`NonEmpty`/`Prefix`/`Range`/`FullRegex`
  short‑circuits).
* **Determinism:** type/file names deterministic across OSes (issue #825). Inherited free **iff** TS
  naming uses the same heuristic ordering (`OrderBy(LocatedSchema.Location)`).

---

## 4. The runtime model in depth (evidence)

Why both models are first‑class, when each wins, and the honest crossover. *(This section was
revised after a follow‑up research pass corrected an earlier over‑strong "the GC neutralises it"
verdict.)*

**The allocation‑rate → GC‑frequency mechanism that justifies pooling in .NET holds in V8 under
sustained load.** Per‑scavenge cost is survivor‑bound, but a minor GC *fires* whenever the New Space
bump pointer fills, so allocation **rate** sets GC **frequency**; a high rate also causes **premature
promotion** (survivors copied to Old Space → cheap minor GC escalates into expensive mark‑sweep).
Evidence it bites under throughput: Plaid cut scavenge **30%→2% of CPU**; Node core measured
**+18%** throughput from semi‑space tuning (Nearform +20–27%); reducing per‑op allocation directly
(fast‑json‑stringify ~2×, JSONStream ~5×, buffer reuse +278% at 1 KB); and V8's own team made
`JSON.stringify` **>2× faster** by replacing a reallocating output buffer, and *halved* scavenge
main‑thread time in Orinoco — neither worth doing if minor‑GC cost were negligible. *(v8.dev
trash‑talk / orinoco‑parallel‑scavenger / json‑stringify; v8‑perf/gc.md; engineering.plaid.com;
nodejs/node#42511; nearform semi‑space study; fastify/fast‑json‑stringify; puzpuzpuz/nbufpool.)*

**A bytes‑native, lazy, zero‑copy model genuinely wins — measured.** simdjson's Node bindings beat
native `JSON.parse` by **~2.1× on large documents when access stays lazy/zero‑copy** (e.g. 189 MB
cityLots, github_events). This is the direct JS analog of Corvus's `Utf8JsonReader`‑over‑pooled‑bytes
design, and it wins in V8 — not only in .NET. Decoding UTF‑8→string is itself a real, measured cost
(`Buffer.toString` ~2.9× faster than `TextDecoder.decode`, independently re‑confirmed), so a view
that compares **raw bytes without materialising strings** (key matching, enum/format checks, ASCII
content) avoids a cost the parse‑then‑validate path always pays. `Uint8Array`/`DataView` byte access
is fully optimised today (DataView ≈ TypedArray after TurboFan inlining). *(luizperes/simdjson_nodejs;
nodejs/performance#18; node#39879; v8.dev/blog/dataview.)*

**But the win is narrower and more conditional than in .NET — three JS‑specific reasons.**
(1) **Lower floor:** bump‑pointer young‑gen allocation is cheaper than .NET gen‑0, and V8's native
`JSON.parse` is *faster than the equivalent JS literal* (~1.7×) and unbeatable in JS (only native
SIMD rewrites beat it). (2) **Escape analysis can erase a wrapper's allocation entirely** in warm,
monomorphic JIT code — which is *why microbenchmarks systematically under‑report* the allocation cost
you are eliminating, and why the proof must be a **sustained‑throughput** benchmark. (3) **A deopt
hazard with no .NET equivalent:** reusing *mutable* pooled objects churns hidden classes and can tip
call sites megamorphic/dictionary‑mode — so naïve pooling is more often an anti‑pattern in JS.
*(v8.dev cost‑of‑javascript‑2019 / v8‑release‑71 / fast‑properties; kipp.ly escape‑analysis;
mrale.ph monomorphism.)*

**The crossover is sharp, and it sets the decision rule.** For **small/medium, fully‑materialised**
documents, native `JSON.parse` + the C++→JS realisation tax beats a JS‑side reader (simdjson‑Node
*loses* on citm_catalog/twitter). The bytes model (A) wins when documents are **large or partially
accessed**, access stays **lazy** (no full object‑graph materialisation), the hot path is
**shape‑stable**, you're **past warm‑up**, and accessors are **per‑schema monomorphic** (a shared
`getField(name)` reader goes megamorphic and erases the win). The parse‑then‑validate model (B) wins
for the small whole‑document case and is the more idiomatic surface. A pragmatic Node‑only lever
worth documenting: `--max-semi-space-size` captures much of the allocation‑rate win without code
changes.

**WASM verdict:** emit JavaScript, not WASM. WASM can't touch JS strings/objects directly; every
string crossing costs a copy + O(n) UTF‑16↔UTF‑8 transcode that repeatedly erases any parser win.
The whole JS perf frontier (Ajv, typia, simdjson‑in‑JS) competes by emitting *better JS* that V8 JITs
to monomorphic machine code. *(Mozilla Hacks JS↔WASM; dev.to “16 patterns”; simdjson.org.)*

**Honest caveats carried into the plan:** the strongest direct evidence (simdjson‑Node ~2.1×) is one
project's benchmarks; there is no clean V8‑specific write‑barrier overhead figure; and the nbufpool
278% number is self‑described as "really‑really unfair." The directional conclusion — the
allocation‑rate mechanism is real and a bytes‑native *lazy* model wins for large/partial/high‑throughput
workloads, but loses for small full‑materialisation — is consistent across Node core, V8's own blog,
and production write‑ups. **Model A must still be validated with our own sustained‑throughput +
allocation benchmark before its perf claims are published**, precisely because escape analysis makes
microbenchmarks lie in its favour at low load and against it is irrelevant at high load.

### 4.1 Numeric precision — a compliance subtlety that touches the model

JS `number` is IEEE-754 double — lossy beyond 2^53 and for most decimals. V5 validates numeric
keywords against the number's **ASCII/text representation** (its `BigNumber`/`BigInteger`), never a
binary double, which is why it gets `multipleOf`, large-integer `const`/`enum`, and high-precision
bounds exactly. **The TS provider mirrors this: numeric validation operates on the number token's
source text, not the parsed JS double.**

* **Validation — exact, zero-dependency, on the token text.** Parse the decimal literal to a scaled
  integer (sign, `BigInt` mantissa, base-10 exponent) and do exact arithmetic with native `BigInt`:
  `multipleOf` = scale both to a common exponent and test `vInt % dInt === 0n`; bounds = cross-scaled
  `BigInt` compare; numeric `const`/`enum` equality = compare the mathematical value (so `1.0` === `1`,
  and integers beyond 2^53 compare correctly). This is the C# ASCII-number approach with **no
  third-party dependency** — exactly what the suite's `multipleOf`/large-integer cases require.
  (Proven runnable: `prototypes/number-exact.mjs`.)
* **Where the text comes from.** The bytes models (A/C) retain the token bytes natively (§4.2). Model B
  parses to a lossy double, so exact numeric validation there needs the **source literal** via
  `JSON.parse(text, reviver)` source-text access (TC39 Stage-4, V8) — so full numeric compliance pushes
  even Model B to keep a number's source text. A fast double path is used only where the schema's
  numeric constraints are exactly representable in a double.
* **Value accessor — `number` default + a pluggable exact fallback.** The generated accessor returns
  `number` by default (fast, possibly lossy); for exact reads it offers a fallback returning an
  arbitrary-precision value from a **third-party big-number library** via a small adapter seam
  (`BigNumberAdapter`). JS has no native arbitrary-precision decimal, so the runtime does not hard-depend
  on one — the default adapter targets a well-known lib (`bignumber.js`/`decimal.js`-style) and consumers
  can swap it. Integer-only formats beyond the safe range return native `bigint`.

This is the second place (with the `Undefined` sentinel, §5.1) where faithful C# behaviour and full
compliance pull the design toward retaining source text rather than the convenient JS value.

### 4.2 Model C — the structural‑sharing patch model for RMW (concrete shape)

The RMW‑optimised path. Engineered to make the *write* side cheap by never realising or
re‑serialising the unchanged majority of the document. This is the JS realisation of Corvus's
mutable‑builder partial‑update.

* **Index/token table (the only mandatory parse work) — dual‑offset.** On `read`, scan the
  `Uint8Array` *once* to record, per object, each member's name/value spans in **both byte offsets
  *and* UTF‑16 code‑unit offsets**. Store as a **fixed‑shape, per‑schema structure** — e.g. parallel
  typed arrays of `(nameByteStart, nameByteEnd, valByteStart, valByteEnd, nameU16Start, …)` indexed by
  a statically‑known member slot — *not* a `Map<string, …>` or a mutated bag (which would churn hidden
  classes / go megamorphic). For schema‑shaped JSON the member set is known at codegen time, so each
  member gets a fixed slot and a statically‑named accessor. **The dual offset is not optional for the
  killer app** (see the VS Code bullet): byte offsets drive the server‑side/wire/persistence RMW path;
  UTF‑16 code‑unit offsets drive the editor path — computing both in one scan avoids an O(scan)
  conversion on every edit.
* **VS Code / LSP projection (the stated killer app: editing a live JSON document in an extension).**
  VS Code's `TextDocument`/`TextEdit` model is **UTF‑16, not UTF‑8** — LSP positions are UTF‑16
  code‑unit offsets (e.g. in `"a𐐀b"`, `b` is at offset 3, because `𐐀` is two code units). A
  byte‑native core therefore needs a **UTF‑16 projection**: emit edits as `TextEdit{range, newText}`
  using the table's UTF‑16 offsets. This is a *lookup*, not a rescan, precisely because the table is
  dual‑offset — and the `{offset, length, content}` edit shape is exactly what VS Code consumes (it is
  why `jsonc-parser`, our patch‑write template, was built for VS Code). One byte‑native scan thus
  serves both the wire RMW path and live‑document editing.
* **Read accessors (per‑schema, monomorphic).** `getName()` returns either the decoded value
  (cached) or decodes its byte range on demand; format/enum checks compare raw bytes (no string).
  Never a generic `get(name)`.
* **Change set (fixed‑shape overlay).** A modification records `(slot → newValue)` in a fixed‑shape
  overlay (parallel array / struct‑of‑slices keyed by slot), never via `delete`/property addition on
  a pooled object. "Unchanged" = slot absent from the overlay → its original byte range.
* **Write (stream, copy‑through).** Walk the member slots in canonical order; for each: if unchanged,
  `output.set(original.subarray(valueStart, valueEnd))` — a raw buffer→buffer copy, no decode/encode;
  if changed, serialise the new value. Added members are appended; removed members are skipped.
  Object framing (`{`, `,`, `:`, `}`) is emitted by the generated writer.
* **Validation scoping.** Unchanged members were proven valid on read; re‑validate only the change
  set (+ any cross‑field constraints the change can affect). The RMW analog of "validate only the
  patched fields".
* **No runtime model-switching fallback — Model C uses its byte path for all sizes.** A *runtime* size/content heuristic that switched to native `JSON.parse`+`JSON.stringify` for some inputs would add per-call branching plus a second code path, to reclaim native's only edge: the mid-range ASCII (~4-64 KB) full-round-trip band, where it leads by just ~1.2-1.6x and at ~10x the allocation (§13.7/§13.8). That dip is not worth the overhead, so no fallback is emitted.

Open sub‑questions to settle when specifying C in detail: the exact index‑table encoding (parallel
typed arrays vs packed `Int32Array`), nested‑descent laziness policy, canonicalisation/key‑order
handling on write (Corvus already has add‑order + on‑the‑fly sort precedent), and whether to lean on
existing JS edit‑range libraries (`jsonc-parser` edit APIs, `json-source-map`) for the index layer or
generate it. These are deliberately deferred until the RMW target is confirmed. **§12 has the full
build‑vs‑reuse breakdown with licenses** — note in particular that the unchanged‑*subtree* passthrough
is our own byte‑range copy, because the native `JSON.rawJSON` primitive is **primitives‑only** and
cannot carry an object/array subtree as one raw blob.

---

## 5. Generated TypeScript: the output contract

Side‑by‑side with V5 C#. **TS surface is type‑only where possible (erased at runtime), with
co‑located AOT validators that are tree‑shakeable.**

> **Validated.** The output shapes in §5.1-§5.3 are hand-written to match the proposed generated code and **type-checked under `tsc --strict --exactOptionalPropertyTypes`** (`prototypes/ts-output-shape/*.ts` compiles clean; `@ts-expect-error` markers prove the compiler rejects wrong-branch access, explicit `undefined` on optional props, missing required props, bad enum values, and spoofed format brands).

### 5.1 Objects

**V5 C#:** `readonly partial struct Person : IJsonElement<Person>` with `_parent/_idx`, UTF‑8 keyed
getters, `Undefined` for absence, `Source`/`Build` factories.

**TS (Model B):** an idiomatic `readonly` interface + an AOT validator + (optional) a typed parse
entry point.

```ts
// person.ts  — generated
export interface Person {
  readonly name: PersonName;          // required → non-optional
  readonly age?: number;              // optional → `?:`  (NOT a custom Undefined sentinel)
  readonly competedIn?: readonly Year[];
}

// AOT validator: monomorphic, kind-first, fail-fast (verbose mode adds a collector arg)
export function isPerson(v: unknown): v is Person { return validatePerson(v) === undefined; }
export function validatePerson(v: unknown, ctx?: EvalContext): Failure | undefined {
  if (typeof v !== "object" || v === null || Array.isArray(v)) return fail(ctx, "type", "object");
  const o = v as Record<string, unknown>;
  // required-set tracked with a small bitmask; property dispatch: ≤3 inline ifs, ≥4 via a static lookup
  if (!("name" in o)) return fail(ctx, "required", "name");
  const e0 = validatePersonName(o.name, ctx); if (e0) return e0;
  if ("age" in o) { const e1 = validateInteger(o.age, ctx); if (e1) return e1; }
  return undefined;
}
```

Key choices, justified:

* **`Undefined` sentinel is dropped.** TS idiom for “absent” is the `?:` optional modifier +
  `in`/`hasOwnProperty`, ideally with `exactOptionalPropertyTypes` so `age?: number` means exactly
  “number or absent”. A dedicated `Undefined` JSON‑value‑kind would fight the language. *(This is the
  single biggest, deliberate divergence from the C# model.)* The mapping is mechanical:
  not‑in‑`required` → `prop?: T`; in‑`required` → `prop: T`.
* **`readonly` interfaces, not classes**, for the default surface — zero runtime cost, fully
  tree‑shakeable, idiomatic. Classes appear only for Model A views (which *need* getters) and for the
  `match()` helper objects on non‑discriminated unions.
* **Construction:** no `Source`/`Build`/`TContext` ref‑struct machinery (a .NET zero‑alloc concern).
  Idiomatic TS construction is an object literal typed as the interface; for *writing* large payloads
  efficiently, offer a builder that streams directly into a `Utf8` output buffer (the spiritual
  analog of `Build<TContext>`), as a runtime‑library helper — not per‑type generated noise. Type-checked: `prototypes/ts-output-shape/object-model.ts`.

### 5.2 Unions (`oneOf` / `anyOf`)

**V5 C#:** `Match(...)` overloads + `TryGetAs{Branch}` + `Branch.From(this)`; discriminator
fast‑path.

**TS — two cases (type-checked: `prototypes/ts-output-shape/union-model.ts`):**

*Discriminated* (shared literal `const` discriminant, incl. OpenAPI `discriminator`): emit a genuine
**discriminated union** — the type system narrows for free. The `Match()` analog dispatches on the
discriminant (the discriminator fast-path), one typed handler per branch, exhaustive via `assertNever`;
consumers can equally just `switch (s.kind)`. *(Closes the `json-schema-to-typescript` gap, which
collapses `oneOf`->`anyOf` and loses discriminated-union semantics.)*

```ts
export interface Circle    { readonly kind: "circle";    readonly radius: number; }
export interface Rectangle { readonly kind: "rectangle"; readonly width: number; readonly height: number; }
export type Shape = Circle | Rectangle;

export function matchShape<R>(value: Shape, cases: {
  circle: (v: Circle) => R;
  rectangle: (v: Rectangle) => R;
}): R {
  switch (value.kind) {
    case "circle":    return cases.circle(value);
    case "rectangle": return cases.rectangle(value);
    default:          return assertNever(value);   // add a variant -> build breaks here
  }
}
```

*Non-discriminated* `oneOf`/`anyOf`: TS can't express XOR in a type, so the **type** is a plain union
`A | B` and runtime **guards** (the generated branch validators) narrow it — the literal analog of V5's
`Match()`. `oneOf` (exactly-one) / `anyOf` (>=one) cardinality is enforced by the **validator**, not the
type (a deliberate, documented limitation).

```ts
export type FullName = string | PersonName;

export function isStringName(v: FullName): v is string { return typeof v === "string"; }       // = V5 TryGetAs{Branch}
export function isStructuredName(v: FullName): v is PersonName { return typeof v === "object" && v !== null; }

export function matchFullName<R>(value: FullName, cases: {
  string: (v: string) => R; name: (v: PersonName) => R; fallback?: (v: FullName) => R;
}): R {
  if (isStringName(value))     return cases.string(value);
  if (isStructuredName(value)) return cases.name(value);
  if (cases.fallback)          return cases.fallback(value);
  throw new Error("no oneOf branch matched");
}
```

**V5 -> TS mapping:** `Match(...matchers, defaultMatch)` -> `matchX(value, { branch..., fallback? })`;
`TryGetAs{Branch}(out T)` -> `isX(v): v is X`; `Branch.From(this)` -> free (the value already *is* the
branch after narrowing); discriminator fast-path -> `switch` on the discriminant.

### 5.3 The full recognised‑pattern catalogue → idiomatic TS

*(every shape in this catalogue is type-checked under `tsc --strict`: `prototypes/ts-output-shape/{object,union,enum-const,format-brand,array-tuple,map-object,conditional}-model.ts`.)*

The core already *classifies* each schema into a recognised shape (the language‑neutral side is the
same one V5 consumes). The TS provider's job is to give each of those shapes its **idiomatic TS
rendering**, mechanically. The authoritative enumeration is
[`docs/CodeGenerationPatternDiscovery.md`](../CodeGenerationPatternDiscovery.md); the table below maps
every shape it lists to the TS we emit, with the core symbol that triggers it. The shapes in §5.1/§5.2
(objects, discriminated/non‑discriminated unions) are not repeated here.

| Recognised shape (core symbol / trigger) | Idiomatic TS surface | Notes |
|---|---|---|
| **Pure tuple** (`TupleTypeDeclaration`; `prefixItems`, or draft‑7 array‑form `items`, with no extra items) | **fixed‑length readonly tuple type** `readonly [A, B, C]` | TS tuples are a first‑class, zero‑runtime type — a *much* better fit than C#'s `Item0/Item1` struct. Element access is `t[0]` (typed per position). |
| **Array with prefix tuple** (prefix `prefixItems` + an `items`/`additionalItems` tail schema) | **labelled head + rest** `readonly [A, B, ...Rest[]]` | TS variadic‑tuple types express "first N are typed, the rest are `Rest`" natively. |
| **Plain (homogeneous) array** (`ArrayItemsTypeDeclaration`; single‑schema `items`) | `readonly T[]` (`ReadonlyArray<T>`) | Validator runs one monomorphic element loop + `contains`/length/`uniqueItems`. |
| **Fixed‑size / numeric array (tensor)** (`IsFixedSizeArray()`/`ArrayDimension()`/`IsNumericArray()`) | `readonly [number, number, number]` for a known length; nested tuples for multi‑dimension; optionally a `Float64Array`/typed‑array view in Model A/C for numeric hot paths | Fixed length → fixed‑length tuple; numeric leaf → consider a typed array for byte‑level work. |
| **Map / dictionary object** (`FallbackObjectPropertyType` / `additionalProperties` / `patternProperties` / `unevaluatedProperties`) | `interface` with declared `properties` **plus** an index signature `readonly [key: string]: T` (when a single fallback type), or a `Readonly<Record<string, T>>` for a pure map | `patternProperties` → keep the index signature broad in the *type* and enforce the regex‑keyed value schema in the *validator*; expose a typed `entries()`/`get(key)` runtime helper rather than C#'s `TryGetProperty`. |
| **Single core‑type wrapper** (`ImpliedCoreTypes().CountTypes() == 1` + constraints/format) | a **branded alias** of the base primitive — `type AccountId = Brand<string, "AccountId">` / `type Port = Brand<number, "Port">` | The JS analog of V5's single‑value struct‑with‑conversions. No wrapper object; the brand carries the constraint identity, the validator/factory enforces it. |
| **Const** (`SingleConstantValue()`; `const`) | the **literal type** of the value — any JSON type: `"create"`, `42`, `true`, `null`; an object/array const → a literal object/tuple type (`{readonly x:0}`, `readonly [1]`) | Validator **deep-equals** the exact value: exact numeric compare (§4.1) for numbers, deep structural equality for object/array. The TYPE is best-effort; the VALIDATOR is authoritative (rejects extra props structural typing would allow). |
| **Enum / `anyOf` of consts** (any JSON values — *not just strings*) | **union of literal types of whatever JSON types the values hold** — `"a" \| "b"`, `1 \| 2 \| 3`, `"auto" \| 0 \| false \| null` (mixed); object/array values → literal object/tuple types | **Never** a TS `enum`. Validation = JSON **deep-equality** vs the allowed values (exact numeric §4.1 for numbers, deep structural for object/array); all-string enums keep a `Set`/byte-membership fast path. Type-checked: `enum-const-model.ts`. |
| **String formats** (`WellKnownStringFormatHandler`) | **branded string** + optional richer parse helper | `uuid`/`email`/`hostname`/`ipv4`/`ipv6`/`json-pointer`/`regex`/`uri*`/`iri*` → `Brand<string, "Uuid">` etc., minted only in validating factories; `date`/`date-time`/`time`/`duration` additionally offer `toDate(s): Date` (the JS analog of V5's NodaTime conversions). |
| **Numeric formats** (`WellKnownNumericFormatHandler`) | `number`, **branded** where range matters, and **`bigint`** for 64‑bit+ | `int16/int32`, `uint16/…`, `half/single/double/decimal`, `byte` → `number` (with a range‑checking validator and an optional brand); **`int64/uint64/int128/uint128`** → `bigint` (exceeds JS safe‑integer range — see §4.1). |
| **Type reduction** (`ReducedTypeDeclaration`; annotation‑only schema) | **no type emitted** — it aliases the reduced target | Annotation‑only schemas (`description` with no constraints) and structurally‑identical schemas collapse to one type *in the core* — the TS provider inherits this for free (the dedup that makes the output small). Emit a `type Alias = Target` only when a distinct name is wanted. |
| **`if`/`then`/`else`** (conditional) | type stays the base shape; **validator** applies the conditional | TS can't express "if matches I then must match T"; encode it in the AOT validator (§5.4), not the type. |

#### 5.3.1 Shape‑based conversions (the `From`/`As` model) — the one V5 feature with a real TS analog

V5 generates implicit/explicit conversion operators and `From<T>()` factories so a value can be viewed
**as** any structurally‑compatible composition member (driven by `AllOfCompositionTypes()` /
`AnyOfCompositionTypes()` / `OneOfCompositionTypes()` in
`CodeGeneratorExtensions.Conversions.cs`). TS is **structurally typed**, so much of this is *free*: a
value that satisfies an `allOf` member's shape **already is** that type — no conversion call needed,
which is strictly more ergonomic than C#. The provider should therefore emit **conversions only where
structural typing doesn't already give them**:

* **`allOf` (intersection).** Emit `type T = A & B & C`. A `T` is assignable to `A`/`B`/`C`
  automatically (structural) — the V5 `implicit operator A(T)` is unnecessary. Provide typed
  *narrowing* helpers (`asA(t: T): A` is just `t`) only as documentation/ergonomic sugar, zero runtime.
* **`anyOf`/`oneOf` (union).** Emit `type T = A | B`. Going *from* a `T` to a branch needs a guard, not a
  cast — emit `isA(v): v is A` / `matchT(...)` (§5.2). Going *to* `T` from an `A` is free (widening).
* **Brand/format conversions** are the genuine V5 conversions worth porting: `asUuid`, `toDate`,
  `toBigInt` — validating factories that mint the branded/parsed form (§5.3 table). These are the TS
  equivalent of V5's `implicit operator Guid(Uuid)`.

The rule: **lean on structural typing; emit a runtime conversion only when a brand, a parse, or a
union‑narrowing guard is actually required.** This is both more idiomatic and more tree‑shakeable than
mechanically porting every C# operator.

### 5.4 Validation emission (the high‑performance core)

Mirror V5's `Evaluate` architecture as straight‑line, monomorphic JS — this *is* the performance
story, and it is the same approach as `ajv/standalone` and typia.

* **One `evaluate{Type}(value, ctx?)` function per subschema**, chained by composition — directly
  modelled on `StandaloneEvaluatorGenerator` (subschema discovery → property‑matcher infra →
  per‑subschema methods).
* **Kind‑first dispatch:** `switch (typeof v)` / `Array.isArray` before any keyword work; skip
  keywords irrelevant to the actual kind (report `ignoredKeyword` only when a collector is attached).
* **Fail‑fast without a collector** (`if (!ctx?.collector) return failure`), full annotation walk in
  verbose/collector mode — the two‑mode design V5 uses.
* **Property dispatch:** ≤3 properties → `if/else` chain; ≥4 → a static lookup (object literal /
  `Map`) for O(1) dispatch (the V5 hash‑matcher analog).
* **Required‑set tracking** via a small integer bitmask (`Span<uint>` analog).
* **Array‑shape dispatch (mirrors the §5.3 catalogue):** *pure tuple* → fixed‑count positional
  checks (`v.length === N` then `evaluateA(v[0])`, `evaluateB(v[1])`, … — fully unrolled, monomorphic,
  no loop); *prefix tuple + tail* → positional checks for the head then one element loop over the rest;
  *plain array* → a single element loop. `contains`/`minContains`/`maxContains`, length, and
  `uniqueItems` are layered on; `uniqueItems` uses a `Set` of canonicalised values.
* **`if`/`then`/`else`:** evaluate the `if` schema collecting *no* failures (it's a predicate); branch
  to `then` or `else` accordingly — the type surface stays the base shape (§5.3), the constraint lives
  here.
* **`unevaluatedProperties`/`unevaluatedItems`:** thread an evaluated‑keys/indices set through the
  context, populated by applicators, consumed last — same priority ordering as V5
  (`First → CoreType → Default → Composition → AfterComposition → Last`).
* **Discriminator fast‑path** for OpenAPI‑style `oneOf`: switch on the discriminant before evaluating
  branches.
* **Regex:** classify at codegen time; emit `startsWith`/range/length checks or a hoisted, lazily
  compiled `RegExp` (module‑scope `const`, built once). No ECMA→.NET translation needed.
* **Annotation collection** (`SchemaEvaluationOnly`/`Both` + verbose): an `EvalContext` carrying a
  results collector, evaluated‑props/items, and path tracking — the TS analog of `JsonSchemaContext`.

### 5.5 Format validation (must pass the suite)

`format` is annotation-only by default in 2020-12; when assertion is enabled (the per-format
assert/disable/warning mode is resolved at **codegen time**, §3), the emitted checks must be
**RFC-accurate and pass the JSON-Schema-Test-Suite `optional/format/` cases** — the bar the C# library
clears via Bowtie.

* **Runtime format validators, zero-dependency, RFC-accurate.** Port the C# format semantics into
  `@corvus/json-runtime`: `date`/`date-time`/`time`/`duration` (RFC 3339), `email`/`idn-email`
  (RFC 5321/5322 + IDN), `hostname`/`idn-hostname` (RFC 1123 + IDNA/UTS-46), `ipv4`/`ipv6` (incl. zone),
  `uri`/`uri-reference`/`iri`/`iri-reference` (RFC 3986/3987), `uri-template` (RFC 6570),
  `json-pointer`/`relative-json-pointer` (RFC 6901), `uuid` (RFC 4122), `regex` (ECMA-262).
* **Regex is native** — `regex` format and the `pattern` keyword compile straight to JS `RegExp`, no
  ECMA->.NET translation (unlike C#); keep only the classifier (Noop/Prefix/Range short-circuits).
* **The hard ones, flagged honestly.** `idn-hostname`/`idn-email`/`iri`/`iri-reference` need Unicode
  normalisation + IDNA (UTS-46) and are where format compliance usually breaks; budget for careful
  Unicode handling (the C# library carries IDN/Unicode polyfills for exactly this). `email` quoted local
  parts and `ipv6` edge cases are similarly fiddly.
* **Date/time/duration — the highest-risk format family, and it needs a NodaTime-grade value model.**
  Two concerns: (a) **validation** must be RFC 3339-exact and pass the suite's `date`/`date-time`/`time`/
  `duration` cases (date validity, leap-second `:60`, offsets, fractional seconds, `T`/`Z` casing) —
  zero-dependency validators verified against the suite; (b) the **rich value accessor / parse + emission**
  wants offset/zone-preserving, sub-millisecond, `Period`/`Duration`-aware types — `Date` is too lossy (a
  UTC instant only; drops the original offset; no `LocalDate`/`Period`). The NodaTime analog in JS is
  **`Temporal`** (`PlainDate`≈`LocalDate`, `PlainTime`, `ZonedDateTime`/`Instant`+offset≈`OffsetDateTime`,
  `Duration`≈`Period`), with `Intl` for localisation and the runtime IANA tz database for time zones. The
  accessor returns `Temporal` types via a **pluggable adapter** (same seam as the big-number adapter,
  §4.1): native `Temporal` where present, the official `@js-temporal/polyfill` otherwise, `Date` as a
  last-resort fallback. Model C bonus: an *unchanged* date-time is copied through verbatim as bytes,
  sidestepping any re-emission / round-trip canonicalisation.
* **Verification is the gate** — each validator is checked against `optional/format/<name>.json` through
  the codegen-aware harness (§8). "Passes the suite", not "looks right", is the acceptance criterion,
  with the same documented exclusions the C# engine uses.

### 5.6 Schema location & evaluation path ($dynamicRef/$recursiveRef + spec output)

The core engine already resolves `$ref`/`$dynamicRef`/`$dynamicAnchor`/`$recursiveRef`/`$recursiveAnchor`
structurally (scope stack + dynamic-scope graph, §2) — the TS provider does **not** re-implement dynamic
resolution. What it must do is **generate the same location metadata the C# library emits**, so output
and dynamic scope are correct:

* **Per-subschema location constants** — emit, per subschema, the **schema location** (absolute keyword
  URI) and the **evaluation path** (JSON-Pointer keyword path) as module-scope constants: the TS analog
  of the C# `{Name}SchemaPath`/`{Name}EvaluationPath` statics (`EmitPathProviderFields` in
  `StandaloneEvaluatorGenerator`).
* **Thread them through `EvalContext`** — as each `evaluate{Type}` descends it pushes the current keyword
  location (the analog of `JsonSchemaContext`), so errors/annotations carry the spec output format's
  **`instanceLocation` / `keywordLocation` / `absoluteKeywordLocation`** — what the suite's output tests
  and Bowtie check.
* **$dynamicRef/$recursiveRef** — the engine resolves the dynamic anchor against the dynamic scope into
  the type graph; the generated code carries the location identifiers and threads the dynamic scope
  through the context exactly as C# does (location-keyed dispatch, not a new scheme), keeping recursive/
  dynamic schemas (e.g. a meta-schema's `$dynamicAnchor: meta`) resolving and reporting identically.

### 5.7 The mutable type model — mutation via Model C (the V5 `JsonDocumentBuilder` analog)

The type model is **mutable**, mirroring V5 (a readonly element + a `Mutable` partial +
`JsonDocumentBuilder`; "patch a builder, `Set` only changed fields, never read-all + rebuild"). The TS
split is the same:

* **Read view** — the `readonly` interface (§5.1). Pure-consumption consumers parse + validate + read
  plain objects and never instantiate any mutation machinery (zero overhead — this is the only thing
  "Model B" ever was).
* **Mutation** — a generated mutation API whose edits are applied as a **Model C structural-sharing
  byte patch**: set only the changed fields, copy unchanged bytes through verbatim, never rebuild or
  re-`stringify` the whole document. This is *the* place Model C lives — it is the **engine of the
  mutable model**, not an optional bolt-on, and it is the proven-best path for mutation (wire RMW
  2.4-34x §13.9; incremental editing 100x-5900x/edit §13.8).
* **API shape (decided): idiomatic immer-style `produce`.** `produce(doc, d => { d.age = 31; d.address.city = "X" })`
  returns a *new* immutable document — you write plain "mutations" on a typed `Draft<T>` (immer's `Draft`)
  and get an immutable result, the established TS pattern. The recorded change-set is the **universal
  currency**: it lowers to a Model C structural-sharing **byte patch** (set only changed fields, unchanged
  bytes copied verbatim), and the *same* change-set is **RFC 6902 JSON Patch** and the basis for minimal
  LSP `TextEdit`s (§5.6). **Zero-dependency:** we don't need immer (its value is object-level structural
  sharing; ours is byte-level via Model C), so a tiny `Proxy` change-recorder suffices; a per-type
  convenience method `doc.produce(recipe)` lowers to the same thing. Proven: typed surface + safety in
  `prototypes/ts-output-shape/mutation-model.ts` (type-checked), and mechanism -> RFC 6902 -> byte patch in
  `prototypes/rmw-scanner/produce.mjs` (runnable, 11/11).

So **"native parse beats the byte path" does not apply to the mutable model**: pure reads are just plain
reads (no Model C in play), and *mutation* is Model C, which wins. The only result where native led was
the **un-optimised, full-round-trip-per-edit** RMW — eliminated by the early-stop optimisation (§13.9)
and irrelevant to the realistic incremental-edit pattern (§13.8), both of which put Model C ahead.

---

## 6. The runtime support library (`@corvus/json-runtime`, working name)

A small, **zero‑dependency, ESM‑first, stateless** package that generated code imports. Tree‑shake
to near‑zero for type‑only consumers.

Contents:

* `EvalContext`, `Failure`/error types, `FormatError`, `assertNever`.
* Format predicates/parsers (uuid/date‑time/email/ipv4/ipv6/hostname/uri/json‑pointer…), each
  independently importable (tree‑shakeable).
* Numeric helpers: exact `multipleOf`/bounds via `bigint`/source‑text (for §4.1 compliance), plus the
  fast double path; base64/base64url via feature‑detected TC39 `Uint8Array.fromBase64` with a
  `Buffer` (Node) / `atob` (browser) fallback.
* Brand helper types.
* (Model C — RMW patch) shared bytes infrastructure in a **separate entry point** (so Model B
  consumers never pull it in): a tokenizer/index‑table scanner, the fixed‑shape change‑set overlay
  primitives, and a streaming copy‑through writer (`writeUnchanged(out, src, start, end)` +
  per‑value serialisers). The generated per‑schema view/patch classes build on these.
* (Model A — lazy read) the same bytes/index infrastructure plus lazy per‑schema view base classes;
  shares the tokenizer with C.

Packaging (research‑backed):

* `"type": "module"`, `"sideEffects": false`, ship compiled `.js` + `.d.ts` (never raw `.ts`).
* `exports` with `"types"` first in each entry; ESM‑only is viable (stateless + zero‑dep ⇒ no
  dual‑package hazard; `require(esm)` covers CJS consumers on Node ≥20.19/22).
* tsconfig: `module: nodenext`, `declaration`, `declarationMap`, `verbatimModuleSyntax`,
  `exactOptionalPropertyTypes`.
* Validate with `@arethetypeswrong/cli` + `publint` in CI.
* Web‑standard `TextEncoder`/`TextDecoder` unconditionally; Node `Buffer` fast paths behind a `node`
  export condition.

---

## 7. The provider implementation (C#)

A new project **`Corvus.Text.Json.TypeScript.CodeGeneration`** (mirroring
`Corvus.Text.Json.CodeGeneration`), referencing the same `src-v4/Corvus.Json.CodeGeneration` core.

### 7.1 `TypeScriptLanguageProvider : IHierarchicalLanguageProvider`

Implements the pipeline callbacks the core invokes (in order): `IdentifyNonGeneratedType` →
(`SetParent` for nesting) → `SetNamesBeforeSubschema` / `SetNamesAfterSubschema` → `ShouldGenerate`
→ `GenerateCodeFor`, plus the registration pass‑throughs and `TryGetValidationHandlersFor`. Owns four
registries (validation handlers, code‑file builders, name heuristics, collision resolvers), exactly
like `CSharpLanguageProvider`. Implement `IHierarchicalLanguageProvider` because TS modules/types
nest (and to inherit the core's parent‑setting pass).

```csharp
// illustrative
public sealed class TypeScriptLanguageProvider : IHierarchicalLanguageProvider
{
    public static TypeScriptLanguageProvider DefaultWithOptions(Options options) => CreateDefault(options);

    private static TypeScriptLanguageProvider CreateDefault(Options o)
    {
        var p = new TypeScriptLanguageProvider(o);
        p.RegisterCodeFileBuilders(new TypeScriptModuleBuilder());        // see §7.4
        p.RegisterValidationHandlers(/* TS-emitting handler set, §7.5 */);
        p.RegisterNameHeuristics(/* reuse structural heuristics; TS built-in mapping */);
        p.RegisterNameCollisionResolvers(new TypeScriptNameCollisionResolver());
        return p;
    }
}
```

### 7.2 `Options` (mirror, pruned/reinterpreted)

Keep: `defaultNamespace` (→ module/package root), `namedTypes`, `namespaces`,
`useOptionalNameHeuristics`/`disabledNamingHeuristics`, `alwaysAssertFormat`/`formatModeOverrides`,
`codeGenerationMode`, `fileExtension = ".ts"`, `lineEndSequence`. **Drop** C#‑runtime‑specific
options: `useImplicitOperatorString`, `addExplicitUsings`, `buildParametersThreshold`,
`optionalAsNullable`/`excludeNonNullDefaulted` (re‑interpret as the §5.1 optional mapping).
**Reinterpret** `defaultAccessibility` as TS export visibility (`export` vs module‑private). Add TS
knobs: `runtimeModuleSpecifier` (import path for `@corvus/json-runtime`), `emitModel`
(`B` default | `C` RMW patch | `A` lazy‑read | combinations), `indentWidth` (default 2), `moduleStyle`.

### 7.3 Naming: `TypeScriptMemberName : MemberName` + collision resolver

Override `BuildName()` for TS identifier rules: **PascalCase** types/interfaces, **camelCase**
members, reserved‑word avoidance, leading‑digit handling, sanitisation. Reuse the *structural* name
heuristics from the core ordering (path/title/required‑property/const/etc.) so cross‑OS determinism
(#825) is inherited; supply only the TS built‑in‑type mapping (`string`/`number`/`boolean`/`unknown`/
`bigint`) and a TS collision resolver (suffix by kind, as C# does).

### 7.4 Emission: `TypeScriptCodeGeneratorExtensions` + code‑file builders

Construct the core `CodeGenerator` with `indentSequence: " "`, `instancesPerIndent: 2`, configured
`lineEndSequence`. Write a parallel set of extension methods that emit TS syntax via the neutral
primitives (`AppendLineIndent`, `PushIndent`/`PopIndent`, `PushMemberScope`/`PopMemberScope`,
`BeginFile`/`EndFile`, `GetOrAddMemberName`): `BeginExportInterface`, `BeginExportClass`,
`AppendImportType`, `AppendUnionType`, `AppendJsDoc`, brace open/close, etc.

**File‑set per type.** TS has no `partial` types, so V5's three partials (`CorePartial`,
`MutableCorePartial`, `JsonSchemaPartial`) collapse. Recommended: **one `.ts` module per generated
type**, containing the type/interface, its validator, and its helpers, plus a generated **barrel
`index.ts`** that re‑exports. (A second module per type — e.g. `{type}.schema.ts` for the
evaluator — is an option for `SchemaEvaluationOnly`, but co‑location + tree‑shaking makes one module
preferable.) Choose `FileExtension = ".ts"`; reuse the global/shared‑helpers file concept for
once‑emitted named types.

### 7.5 Validation handlers (the bulk of the work)

Re‑implement the handler families against TS, reusing the core's keyword classification, priority
ordering, and child‑handler composition. Each handler still self‑selects via `HandlesKeyword(keyword
is I…ValidationKeyword)` and emits TS instead of C#:

`Type`, `Format`, `Number`, `String`, `Const/Enum`, `Composition{AllOf,AnyOf,OneOf,Not}`,
`TernaryIf`, `Object` (+ child handlers: properties, patternProperties, additionalProperties,
required, propertyNames, dependent*), `Array` (+ child handlers: items, prefixItems, contains,
counts, uniqueItems), and the `unevaluated*` "Last" handlers. The composition model (priority‑ordered
prepend/append of children around a parent body; emit‑then‑trim for optional children) carries over
unchanged — only the emitted text differs.

### 7.6 Driver wiring (where it attaches)

Add a `--language csharp|typescript` option to `GenerateCommand` (preferred over a new `Engine`
member, since “engine” currently means V4‑vs‑V5 *runtime/core*, not language). Branch at
`GenerationDriverV5.cs:203‑211`:

```csharp
ILanguageProvider provider = generatorConfig.Language switch
{
    Language.TypeScript => TypeScriptLanguageProvider.DefaultWithOptions(MapToTsOptions(generatorConfig, namedTypes, mode)),
    _                   => CSharpLanguageProvider.DefaultWithOptions(MapGeneratorConfigToOptions(generatorConfig, namedTypes, mode)),
};
var generatedCode = typeBuilder.GenerateCodeUsing(provider, typesToGenerate, CancellationToken.None);
```

Everything **before** line 203 (resolver, `VocabularyRegistry`, `JsonSchemaTypeBuilder`,
`AddTypeDeclarationsAsync`) and **after** it (`WriteFiles` + `PathTruncator` + collision counter +
#825 determinism + `--outputMapFile`) is reused verbatim — `GeneratedCodeFile` is just name +
content. **Not** wired into the Roslyn source generator.

---

## 8. Compliance & testing strategy

Match V5's two independent gates, both driven by the official `JSON-Schema-Test-Suite` submodule.

1. **Codegen‑aware suite tests (primary, strongest).** A TS analog of
   `Common/tests/TestUtilities/TestJsonSchemaCodeGenerator`: for each suite schema, run
   `TypeScriptLanguageProvider`, emit `.ts`, then **compile and run it** — the analog of the Roslyn
   `DynamicCompiler` — via `tsc`/`esbuild` + Node, asserting each case's boolean expectation (and
   annotation output for the evaluator/annotation suites). Reuse the existing `appsettings.json`
   collection + hierarchical exclusion model and the suite's `remotes/` resolution; wire into (or
   alongside) `update-json-schema-test-suite.ps1`, e.g. a `--language ts` mode of the existing
   test‑class generator. **This proves the *generator* is correct, not a generic runtime validator.**
2. **Bowtie cross‑implementation conformance.** Bowtie is language‑agnostic and already has JS/TS
   harnesses (ajv, hyperjump). Add a Node/TS harness that imports our generated‑then‑compiled
   validators and speaks Bowtie's JSON‑over‑stdio (IHOP) protocol, plus a sibling patcher in
   `configure-bowtie-for-local-development.ps1` that `npm`/`pnpm install`s the local TS package
   (instead of injecting a local NuGet feed). Run `bowtie suite -i … 2020-12 | bowtie summary`.
3. **Performance gate (sustained‑throughput, not microbenchmark).** A throughput + allocation +
   tail‑latency benchmark under *sustained load* (validate + access, varied payload sizes/shapes,
   varied fraction‑of‑fields‑accessed, **and an explicit read‑modify‑write axis**), comparing
   Model B, Model C (RMW patch), Model A (if built), Ajv‑standalone and TypeBox. This benchmark
   **substantiates the perf claims and sets the crossover thresholds** — and it must run under load
   precisely because escape analysis flatters low‑load microbenchmarks into reporting allocation as
   free (§4). Expected/measured shape (the run is in §13.7/§13.8): Model C ahead for non-ASCII at every size and for ASCII RMW of large / low-change-ratio documents (skips realise+re-serialise of unchanged fields); native ahead only for the mid-range ASCII (~4-64 KB) full-round-trip band (at ~10x the allocation); Model A relevant only for large/partial pure-read. Native leads only the mid-range ASCII band, by ~1.2-1.6x at ~10x allocation — too small to justify a runtime model-switching fallback (§13.8), so Model C is used across the board.
4. **Type‑level tests** (`tsd`/`expect-type`) asserting the generated types narrow correctly
   (discriminated unions, brands, optionality), and `@arethetypeswrong/cli` + `publint` on the
   runtime package.

---

## 9. Phasing

| Phase | Deliverable |
|---|---|
| **0 — Scaffold** | New project + `TypeScriptLanguageProvider` skeleton, `Options`, `TypeScriptMemberName`, `TypeScriptCodeGeneratorExtensions`; `--language ts` wired into the CLI; emit interfaces for simple objects. End‑to‑end "schema → `.ts`" smoke test. |
| **1 — Type surface** | Objects (optional/required mapping), arrays, enums/consts (literal unions), formats (brands), `$ref`, nested types, barrel `index.ts`. Idiomatic, no validators yet. |
| **2 — AOT validators** | Per‑type `evaluate` functions; kind‑first dispatch; property‑matcher dispatch; required bitmask; numeric/string/array/object keyword handlers; format modes; regex classification. Draft 2020‑12 core. |
| **3 — Full keyword + dialect coverage** | Composition (`allOf`/`anyOf`/`oneOf`/`not`/`if`‑`then`‑`else`), discriminated‑union + `match`, `unevaluated*`, `$dynamicRef`/`$recursiveRef`; drafts 4/6/7/2019‑09 + OpenAPI 3.0/3.1; numeric‑precision compliance (§4.1). |
| **4 — Evaluator modes** | `SchemaEvaluationOnly` + annotation collection (verbose/collector). |
| **5 — Compliance** | Codegen‑aware TS suite harness + Bowtie TS harness; drive to full pass (matching V5's exclusion set, with reasons). |
| **6 — RMW path (Model C)** | RMW is the confirmed target (VS Code killer app). Per the **§13 source‑grounded plan**: dual‑offset (byte+UTF‑16) scanner ported from jsonc-parser; packed‑`Int32Array` index table + monomorphic per‑member accessors; fixed‑shape change‑set overlay; streaming copy‑through writer; `applyEdits` byte port; LSP `TextEdit` projection; change-set-scoped re-validation (no runtime model-switching fallback — §13.8). Start with the §13.6 prototype. |
| **7 — Perf gate + (optional) Model A** | The §13.6 sustained‑load RMW benchmark — **prototyped & run (§13.7, §13.8)**: Model C wins large/non‑ASCII wire RMW (≤5.6×), incremental editing 100×–5900×/edit zero‑alloc, 4–13× less GC; loses **mid‑range ASCII (~4–64 KB)** full‑round‑trip (native's C++ parser, at ~10× the allocation) → native leads only that small band (~1.2-1.6x), not worth a runtime model-switching fallback, so Model C is used across the board. Remaining: multi‑tenant concurrent GC contention + a live LSP round‑trip; then runtime‑library hardening; Model A lazy‑read *only if* large‑payload partial read is independently a target. |
| **Cross‑cutting** | Package `@corvus/json-runtime` (ESM, tree‑shakeable); docs + a playground. |

---

## 10. Risks & open questions

* **Runtime‑model decisions (§1.2)** — the load‑bearing choices: confirm **Model B** default; confirm
  **RMW** (not lazy partial read) is the workload to optimise → engineer **Model C** (structural‑sharing
  patch, §4.2); defer **Model A** unless lazy large‑payload read is its own target.
* **Model C correctness/perf risks** — the change‑set carrier and index table must be **fixed‑shape**
  (no `delete`/property churn) and accessors **per‑schema monomorphic**, or the deopt erases the win;
  the size/content crossover was measured (§13.8) — native leads only a small mid-range-ASCII band, not worth a runtime fallback; write‑side
  canonicalisation/key‑order must be defined; perf proven under **sustained load**, not microbench.
* **Numeric precision (§4.1)** — full compliance requires exact numeric validation; design the
  runtime numeric helpers (source‑text/`bigint`) early; decide a default policy for `number` vs exact.
* **`oneOf` XOR in a structural type system** — the *type* is a union; XOR is enforced only by the
  validator. Acceptable and standard, but document it.
* **Recursive / `$dynamicRef` types** — express as named interfaces referencing themselves; ensure the
  emitter handles cycles (the core graph already resolves them; emission must not infinite‑loop).
* **Megamorphism discipline (Model A)** — enforce per‑schema monomorphic accessors; lint/benchmark.
* **`exactOptionalPropertyTypes`** — recommend it for consumers; the generated types assume it for the
  cleanest optional semantics.
* **Determinism** — verify cross‑OS file/name stability uses the same ordering as #825.
* **Source generator** — out of scope (C#‑only); the TS provider is CLI/standalone. If a "watch /
  build‑time" TS experience is wanted later, that's an esbuild/tsc plugin or CLI watch, not Roslyn.

---

## 11. Appendix — reuse map

| Concern | Source | TS provider action |
|---|---|---|
| Schema load, `$id`/`$anchor`/`$ref`/`$dynamicRef`/scope | `src-v4` core | **reuse unchanged** |
| `TypeDeclaration` graph + reduction/dedup | `src-v4` core | **reuse unchanged** |
| Keyword + vocabulary model (94 ifaces, all dialects) | `src-v4` core + dialect assemblies | **reuse unchanged** |
| `JsonSchemaTypeBuilder` orchestration | `src-v4` core | **reuse unchanged** |
| `CodeGenerator` text buffer | `src-v4` core | **reuse** (indent=2 spaces, TS line endings) |
| Seam interfaces + registries | `src-v4` core | **reuse unchanged** |
| Document resolvers, `PathTruncator`, file writing, `--outputMapFile`, #825 determinism | `src-v4` core + `GenerationDriverV5` | **reuse unchanged** |
| Name heuristics (structural ordering) | `src/...NameHeuristics` | **reuse ordering**; new built‑in‑type mapping |
| `CSharpLanguageProvider` | `src/...CSharpLanguageProvider.cs` | **new** `TypeScriptLanguageProvider` (template) |
| Metadata‑bag extensions (`CSharp_*`) | `src/...TypeDeclarationExtensions.cs` | **new** `Ts_*` extensions |
| Validation handlers | `src/...ValidationHandlers` | **new** TS‑emitting set (reuse classification/priority/composition) |
| Code‑file builders (3 partials) | `src/...CodeFileBuilders` | **new** 1‑module‑per‑type builder |
| `CodeGeneratorExtensions.*` (C# syntax) | `src/...` | **new** `TypeScriptCodeGeneratorExtensions` |
| `CSharpMemberName` | `src/...` | **new** `TypeScriptMemberName` |
| Format handlers | `src/...FormatHandlers` | **new** TS format handlers (brands + predicates) |
| `EcmaRegexTranslator` | `src/...` | **drop** (TS is native ECMA); keep the **classifier** |
| `StandaloneEvaluatorGenerator` | `src/...` | **port** the architecture to TS emission |
| Roslyn source generator | `src/...SourceGenerator` | **not applicable** |
| Conformance (test‑suite harness + Bowtie) | `Common/tests/...`, Bowtie harness, `*.ps1` | **new** TS analogs over the same submodule |

---

## 12. OSS foundations & build‑vs‑reuse (RMW + validation pipeline)

**Strategic finding:** the byte‑range structural‑sharing RMW model (§4.2) **does not exist as a
shipping JS/Node library** — the niche is genuinely unoccupied (even at the C++ layer: all simdjson
bindings inherit a read‑only On‑Demand path). Existing RMW tools are either UTF‑16 string/CST‑based
or share at the parsed‑object level (immer/Immutable.js). So Model C is net‑new, but it can be
*assembled* from proven, permissively‑licensed pieces rather than built from zero. *(License/maint.
facts below were research‑verified; re‑check at integration time — "no library exists" is
absence‑of‑evidence, not proof.)*

| Pipeline stage | Best existing art | License | Decision |
|---|---|---|---|
| **Patch‑write algorithm** (the core of C) | **jsonc‑parser** `modify`→`Edit{offset,length,content}`→`applyEdits` (VS Code settings editor) | MIT | **Reimplement the design over UTF‑8 byte offsets** (it's UTF‑16/string‑bound; too widely depended‑on to retrofit). This *is* the proven copy‑through splice. |
| **Pointer→source‑range locator** | **json‑cst** (CST with `range{start,end}`) + **jsonpos** (RFC 6901 pointer→range), same author | MIT | **Fork/build on** (both dormant since 2022‑23 → you'd own them; char offsets need byte conversion). |
| **Lossless RMW CST reference** | **@croct/json5‑parser** — only *actively maintained* pure‑TS lossless RMW CST | MIT | **Study / possibly contribute** a byte‑output mode (pre‑1.0, tiny team → adoption risk). |
| **Verbatim leaf emission** | **`JSON.rawJSON`** (TC39 Stage 4, Node 22+) | native | **Use for unchanged leaf *primitives* only** — it is **primitives‑only** (objects/arrays throw), so unchanged *subtree* passthrough is our own byte‑range copy, not `rawJSON`. TS lib types not yet shipped → local `.d.ts`. |
| **`Uint8Array`‑native codecs / sidecar ideas** | **json‑joy** `@jsonjoy.com/json-pack`,`/json-pointer`,`/util` | **Apache‑2.0** (sub‑pkgs) | **Reuse the Apache‑2.0 sub‑packages + design ideas only.** ⚠️ Umbrella `json-joy` + CRDT core are **AGPL‑3.0‑only** — do not fork those. |
| **Codegen‑baked parse+validate seam** | **@exodus/schemasafe** — generates standalone validator *and parser* modules from schema | MIT, zero‑dep | **Top study target** (closest existing "validate‑during‑parse" seam; small community = room to contribute). |
| **Changed‑field serializer codegen** | **fast‑json-stringify** (Fastify) | MIT | **Mirror the per‑schema stringifier codegen.** Its weak spot is large arrays/payloads (native `JSON.stringify` wins there) — exactly where C's write‑through‑unchanged‑bytes beats it. |
| **Validation‑codegen architecture + dual‑mode** | **TypeBox `TypeCompiler`** (JIT + interpreter fallback) | MIT | **Architecture reference** for the validator emitter. |
| **AOT/types‑as‑source analog** | **typia** | MIT | Validator output liftable; stringifier output runtime‑coupled (re‑implement, don't embed). |
| **Lazy read‑only front end** | **everything‑json** (simdjson tape, lazy `.get()`/`.path()`) | ISC | Only relevant if Model A (lazy read) is built; no byte offsets / no write side. |

**Two consequences for the design:**

* **We are inherently CSP / edge‑runtime safe.** We emit *static `.ts` source compiled ahead of
  time* — no runtime `new Function`/`eval` (unlike Ajv's default runtime codegen). This is a genuine
  advantage in CSP contexts and Cloudflare Workers/Deno Deploy, matching `ajv/standalone` and typia.
  (Keep this property: never emit code that needs runtime `Function` construction.)
* **The fastest validators can't be out‑validated on their own turf** (Ajv/typia/TypeBox JIT
  object‑walks at 100M+ ops/s). The only structural way to win is the seam none of them occupy —
  **validate during parse / over bytes**, and for RMW, **write unchanged bytes through**. That seam,
  not raw validator micro‑perf, is the design's performance thesis.

**Recommended permissive‑license assembly for Model C:** port jsonc‑parser's `applyEdits` to UTF‑8
byte offsets (patch‑write core) · borrow json‑cst/jsonpos's pointer→range locator · `JSON.rawJSON`
for verbatim leaf primitives + our own byte‑range pass for subtree passthrough · mirror
fast‑json-stringify for the changed‑field serialiser (and beat it on large output) · study
@exodus/schemasafe for the codegen‑baked parse+validate seam. **One trap to avoid: json‑joy's
AGPL‑licensed core.**

### 12.1 Source‑level port effort & the VS Code/LSP boundary

Pulling the actual source of the top candidates tightens the estimates and confirms the seam:

* **jsonc‑parser — moderate, well‑bounded port (not a research project).** The edit core is literally
  `text.slice(0, off) + content + text.slice(off + len)` over an `Edit{offset,length,content}[]` — **encoding‑agnostic, carries over near‑verbatim.** The real work is the **scanner** (~35–40%
  encoding‑specific: `charCodeAt`→`Uint8Array` indexing, UTF‑8 multi‑byte decode in `scanString`,
  `\uXXXX` escapes, line/char→byte tracking); ~60–65% (bracket matching, state) is structural and
  ports unchanged. Estimate ~1–2k LOC concentrated in the scanner. **Clean‑room port, dual‑offset,
  MIT‑clean — do not fork** (UTF‑16‑bound; 47M weekly downloads of dependents make upstream retrofit
  impossible).
* **json‑cst + jsonpos — low effort to adopt, but subsumed by the jsonc‑parser port.** Clean CST with
  per‑node `range` + raw‑token retention, but **no write/serialise API (confirmed absent)** and
  char‑based offsets. Use only as a quick RFC 6901 pointer→range accelerator; otherwise the scanner
  port gives the same ranges plus the edit model json‑cst lacks.
* **@exodus/schemasafe `parser()` — study, don't build RMW on it.** Generates a self‑contained,
  zero‑dep module that parses a **string** and validates in **one pass** → `{valid, value}`. But it
  **materialises a full POJO** and is **string‑only (no `Buffer`/bytes)** — i.e. fused
  parse‑then‑wrap, not byte‑slice retention. Ideal template for the **validation leaf and the
  small‑doc Model‑B default**; not the RMW model.
* **fast‑json-stringify — mirror the codegen; it confirms the seam.** Per‑schema `new Function`
  serialiser, but **always re‑encodes — "input JS objects are never emitted as raw bytes."** Our RMW
  writer = its codegen for *changed* fields **+ raw byte‑range copy for unchanged** ones (the thing it
  structurally cannot do, and where it's weakest — large arrays/payloads).
* **The VS Code/LSP boundary is UTF‑16 — this is the single most important architectural
  consequence.** LSP `TextEdit`/`TextDocument` positions are UTF‑16 code‑unit offsets. So Model C is a
  **byte‑native core with a UTF‑16 projection**: the dual‑offset table (§4.2) lets one scan serve both
  the server‑side/wire byte path and the editor path, and edits project to LSP `TextEdit` by lookup,
  not rescan. (A naïve byte‑only model would pay an O(scan) byte→UTF‑16 conversion per edit, defeating
  the point.)

**Per‑stage port‑effort summary:** scanner (dual‑offset) = *moderate*; member‑location / pointer→range
= *low* (rides the scan); edit/patch model + `applyEdits` = *low* (near‑verbatim); changed‑field
serialiser = *moderate* (our codegen, fast‑json-stringify‑style); unchanged‑subtree passthrough =
*moderate, net‑new* (the differentiator); validation leaf / small‑doc default = *low* (embed/template
schemasafe‑style output); VS Code projection = *low* (lookup on the dual‑offset table). License path is
clean (all MIT; only json‑joy's AGPL core is off‑limits). **§13 is the source‑grounded implementation
plan that validates this estimate against the real `microsoft/node-jsonc-parser` source.**

---

## 13. Model C implementation plan — the dual‑offset scanner (source‑grounded)

Verified against the live `microsoft/node-jsonc-parser` `main` source (`src/impl/scanner.ts`,
`src/impl/edit.ts`, `src/main.ts`). **Headline: the "moderate effort" estimate holds.** ~40% of
`createScanner` (~80 of ~200 lines) is encoding‑specific; the structural ~60% ports unchanged because
**every JSON structural character (`{ } [ ] : , "`, digits, keyword letters) is ASCII**, so a byte‑level
state machine is identical to jsonc-parser's char‑code one. `applyEdit`/`applyEdits` port near‑verbatim.
The only genuinely subtle code (~10 lines) is the dual‑offset lockstep across 4‑byte/surrogate content.

### 13.1 Scanner anatomy (what changes)

`createScanner` is a closure over UTF‑16‑offset state (`pos`, `tokenOffset`, `lineNumber`,
`lineStartOffset`, …). Encoding‑specific operations to rewrite for `Uint8Array`: `charCodeAt(pos)` →
byte indexing (~40 call sites repo‑wide); `text.substring(start,pos)` → byte‑span capture;
`String.fromCharCode` escape decoding; `scanString`'s `\u` handling; line/column tracking → dual
counters. Structural (ports unchanged): the `scanNext` dispatch `switch`, string/number/keyword state
machines, `scanNumber` leading‑zero→fraction→exponent logic, `scanHexDigits` accumulation, comment/
trivia handling, error states. **Free correctness upgrade:** jsonc-parser's `scanString` decodes `\uXXXX`
as a single `String.fromCharCode` with *no surrogate‑pair combining* (BMP‑only — a known source
limitation). We never decode escapes for the index table (we retain byte spans) and decode raw UTF‑8 via
`TextDecoder` only at materialisation, which handles non‑BMP correctly — so we sidestep the bug.

### 13.2 The dual‑offset lockstep (the crux)

Maintain two counters over the UTF‑8 `Uint8Array`: `byteOff` (array index) and `u16Off` (the UTF‑16
code‑unit offset an equivalent JS string would have — the **LSP coordinate**). Advance per lead byte:

| Lead byte | Codepoint range | Δbyte | Δu16 |
|---|---|---|---|
| `0xxxxxxx` (`<0x80`) ASCII | U+0000–007F | +1 | +1 |
| `110xxxxx` (`0xC0–DF`) | U+0080–07FF | +2 | +1 |
| `1110xxxx` (`0xE0–EF`) | U+0800–FFFF | +3 | +1 |
| `11110xxx` (`0xF0–F7`) | U+10000–10FFFF | +4 | **+2** (surrogate pair) |
| `10xxxxxx` (`0x80–BF`) | continuation | — | malformed if seen as lead |

**Key insight that makes this cheap and correct:** the LSP `TextEdit` coordinate is an offset into the
**source text**, not into the decoded string value. JSON escapes (`\n`, `\uXXXX`) are ASCII *in the
source*, so they advance **both** counters by their source width (`\n` = +2/+2, `\uXXXX` = +6/+6).
Therefore `byteOff` and `u16Off` **stay equal across all‑ASCII regions (including all escapes) and
diverge only on raw non‑ASCII content bytes.** For a pure‑ASCII document `byteOff === u16Off`
everywhere → the dual‑offset machinery costs nothing on the common case; only the 4‑byte→+2 astral case
is non‑trivial (exactly the `"a𐐀b"` surrogate case LSP calls out).

```ts
// advance one codepoint, updating both offsets; returns the lead byte for the state machine.
// the dispatcher works on bytes — every JSON structural char is ASCII (<0x80).
function advance(): number {
  const b = buf[byteOff];
  if (b < 0x80) { byteOff += 1; u16Off += 1; return b; } // ASCII (incl. structure + escapes)
  if (b < 0xE0) { byteOff += 2; u16Off += 1; return b; } // 2-byte
  if (b < 0xF0) { byteOff += 3; u16Off += 1; return b; } // 3-byte
                  byteOff += 4; u16Off += 2; return b;    // 4-byte → surrogate pair
}
// scanString records name/value spans in BOTH offset spaces; escapes are consumed, not decoded:
//   '\\' then letter → advance() twice (ASCII); '\u' → advance() ×4 more (hex digits, all ASCII)
//   ordinary char → advance() (table handles multibyte u16).  No String materialisation.
```

### 13.3 Index table — packed `Int32Array`, monomorphic accessors

Per member, store 8 `Int32` in one contiguous `Int32Array` indexed `slot*8 + field`:
`[nameByteStart, nameByteEnd, valByteStart, valByteEnd, nameU16Start, nameU16End, valU16Start, valU16End]`
= **32 bytes/member, one allocation, cache‑local, no objects → no hidden‑class/megamorphism risk.**
(`Int32` caps docs at 2 GiB; use `Float64Array` only if >2 GiB is a target.) The u16 ends are stored
flat rather than recomputed (the delta needs the non‑ASCII content count anyway; the branch costs more
than 8 bytes). **The generator emits a fixed, statically‑named accessor per member** —
`getName() { return decode(buf, tbl[K_NAME_VS], tbl[K_NAME_VE]); }` with codegen‑baked literal indices.
**No generic `get(key)` reader** — that is the single make‑or‑break monomorphism rule. **Lazy nested
descent:** the top scan records only top‑level member spans and skips object/array values by
bracket‑depth counting (ASCII, cheap); a nested member's sub‑table is built on first access and cached.

### 13.4 Patch / write / LSP projection

* **`applyEdit` / `applyEdits` port verbatim** (confirmed from source). `applyEdit` = `prefix + content
  + suffix`; the byte form uses `Uint8Array.subarray`/`set`. `applyEdits` = **sort by offset asc then
  length asc; apply right‑to‑left so earlier offsets stay valid; throw on overlap**
  (`e.offset + e.length <= lastModifiedOffset`). Pure `number→number` arithmetic — encoding‑agnostic.
* **Streaming copy‑through writer (the differentiator, not in jsonc-parser):** walk members in canonical
  order; unchanged member → `out.set(buf.subarray(valByteStart, valByteEnd))` (raw memcpy, no
  decode/encode); changed → serialise the new value; framing (`{ , : }`) emitted by the generated
  writer. This is why C beats parse‑then‑`stringify`: the unchanged majority is a `Uint8Array.set`.
* **LSP `TextEdit` projection = lookup, not rescan:** read the affected member's stored u16 offsets and
  convert u16→`Position{line,character}` via a per‑line u16 start table (O(log lines) binary search).
* **`JSON.rawJSON` (leaf‑only):** useful only in a hybrid where changed fields are built with native
  `JSON.stringify` and unchanged *leaves* are spliced in; unchanged *subtree* passthrough is our byte
  copy (rawJSON throws on objects/arrays).

### 13.5 Edge cases & guardrails

Malformed UTF‑8 / continuation‑byte‑as‑lead / invalid leads (`0xF5–0xFF`) → `ScanError`, stop the scan
(don't desync counters); strict reject for wire, lenient `TextDecoder({fatal:false})` only at
materialisation. Skip a leading UTF‑8 BOM but **count its bytes** so offsets stay document‑absolute and
agree with VS Code's view. **Exact numeric validation (§4.1) is free here:** the table already retains
the number token's `valByteStart/End`, so `multipleOf`/bounds read the original token bytes — strictly
better than parse‑then‑validate (which already lost precision to a JS `number`). Default write order =
**preserve original document order** (trivially correct copy‑through, minimal editor diff); canonical
sort opt‑in. Megamorphism guardrails (make‑or‑break): no generic reader; typed‑array index table;
fixed‑shape change‑set overlay (no `delete`/dynamic keys); fixed‑method per‑schema view classes.

### 13.6 Prototype + sustained‑load benchmark (the acceptance gate)

**Prototype (~250–400 LOC for the scanner):** dual‑offset `scanDocument(buf) → Int32Array` top‑level
index + one‑level lazy descent; a single fixed schema (~12–20 fields, mixed string/number/nested);
hand‑written per‑member monomorphic accessors + fixed‑shape change‑set overlay + streaming copy‑through
writer; `byteEditToTextEdit` LSP projection; change‑set‑scoped re‑validation.

**Benchmark (sustained load, NOT microbench — escape analysis makes microbenchmarks lie):** RMW loop
(read → modify `k` of `N` → write) for 30–60 s to steady state. Axes: doc size {~300 B, 4 KB, 64 KB,
1 MB} × change ratio {1 field, 10%, 50%, 100%} × content {ASCII‑only, non‑ASCII‑heavy}. Metrics:
throughput, **allocation rate** (bytes/op via `--trace-gc`/`memoryUsage` deltas), **minor‑GC
frequency**, **p99** per op. Baselines: (a) native `JSON.parse`→mutate→`JSON.stringify` (the one to
beat); (b) jsonc-parser `modify`+`applyEdits` (UTF‑16 string reference — isolates the byte‑vs‑string
win). Run at default and tuned `--max-semi-space-size`.

**Falsifiable thesis.** *Predicted:* C **loses** for small docs / high change ratio (bookkeeping
unamortised → must fall back to B below a size/change‑ratio threshold) and **wins** for medium/large
docs at low change ratio, margin ∝ `(1 − k/N) × size`. *Confirmed if:* at medium+ size and low change
ratio, C shows higher throughput **and** lower allocation rate **and** lower p99 than both baselines
under sustained load, **and** the advantage vanishes/reverses for small/high‑change‑ratio (proving the
crossover and the necessity of the B fallback). *Refuted if:* native parse+stringify matches/beats C
across all axes (native parser+GC dominate even copy‑through), **or** C's allocation rate isn't lower
(an accidental‑materialisation or megamorphism bug to hunt).

### 13.7 Prototype benchmark results (measured)

A working prototype of §13.6 was built and run — the dual‑offset scanner (`scanInto` →
pooled `Int32Array`), the copy‑through `applyEditsBytes`, the synthetic doc generator, and a
sustained‑load harness that **verifies every variant produces equivalent JSON before timing**.
(Prototype: `prototypes/rmw-scanner/`, plain ES modules, zero‑dependency, `node --expose-gc
bench.mjs`. Node 20, WSL2, one run — indicative, not definitive.) Variants: **C** Model C
(scan + copy‑through byte splice), **N** native (decode+`JSON.parse`+mutate+`JSON.stringify`+encode),
**S** string‑splice over bytes; editor (string→string): **NS** native, **SS** string‑splice
(no transcode), **CS** Model C via transcode. Metric `g` = GC events / 1000 ops (allocation‑rate
proxy). RMW = read → modify k fields → write.

**Headline result — allocation/GC pressure (the core thesis), Model C vs native:**

| doc | C `g` | N `g` | native allocates |
|---|---|---|---|
| ASCII 64 KB | 2.3 | 30.3 | **~13× more** |
| ASCII 256 KB | 8.5 | 46.9 | ~5.5× more |
| Unicode 64 KB | 2.1 | 13.4 | ~6.5× more |
| Unicode 256 KB | 7.8 | 35.2 | ~4.5× more |

Model C generates **5–13× fewer GC events** across all mid/large docs, ASCII and Unicode alike —
*even in the cases where native is faster on single‑thread throughput*. This is the
sustained‑load argument made concrete: native buys its raw speed with allocation the prototype's
single‑threaded loop doesn't punish, but a multi‑tenant server would (GC contention → p99). The
benchmark **under‑states** Model C's advantage because it measures isolated throughput, not
concurrent heap pressure.

**Throughput — wire (bytes→bytes), Model C / native (C/N):**

| content | ~360 B | ~4 KB | ~64 KB | ~256 KB |
|---|---|---|---|---|
| ASCII | 1.6× | 0.8× | **0.5×** | 1.1× |
| Unicode | 2.0× | 4.1× | 3.9× | **5.6×** |

* **Non‑ASCII content: Model C wins everywhere (2–5.6×), margin growing with size** — native pays a
  heavy UTF‑8↔UTF‑16 transcode (`TextDecoder`/`TextEncoder`) on top of parse/stringify; Model C never
  decodes the unchanged bytes.
* **ASCII: native's tuned C++ parse+stringify wins the mid‑range (4–64 KB)**; Model C wins at small
  (~360 B) and very large (256 KB, where the copy‑through `memcpy` of the huge unchanged region
  dominates). The predicted size/content crossover is real, but native's lead there is small (~1.2-1.6x) and confined to one band — **not worth a runtime model-switching fallback** (§13.8).

**Throughput — editor (string→string), the VS Code in‑memory case:**

* When the document is already a JS string, the byte core via transcode (**CS**) loses everywhere
  (0.14–0.86× of native) — **forcing bytes in the editor is the wrong move**.
* The fair analog is the **no‑transcode string‑splice (SS)**: it beats native (NS) only for small
  docs (and a few Unicode mid cases); native parse+stringify wins for mid/large **full round‑trips**.

**Verdict against the §13.6 thesis:**
* **Confirmed (allocation):** Model C's allocation/GC pressure is dramatically lower (5–13×) across
  all mid/large docs in every content type — the strongest, most consistent result, and the one that
  matters under concurrent load.
* **Confirmed (throughput) for the headline regime:** bytes→bytes RMW of **large and/or non‑ASCII**
  documents — Model C wins, up to 5.6×.
* **Refined (throughput):** for **ASCII mid-size bytes (~4-64 KB)**, native parse+stringify wins on raw throughput — but only by ~1.2-1.6x, while allocating ~13x more. The deciding axes are content ASCII-ness and the bytes-vs-string contract, not just size; and the margin is small enough that a runtime model-switching fallback is not worth its overhead.
* **New finding — the editor (string) contract is where the throughput win is weakest.** For the VS
  Code killer app specifically, Model C's value is **not** full‑round‑trip throughput; it is (a) the
  5–13× lower GC pressure, (b) **incremental‑edit amortisation** — scan once, apply many cheap
  `applyEdits` producing minimal LSP `TextEdit`s, never re‑parsing/re‑stringifying the whole document
  (the pattern this full‑round‑trip benchmark does **not** measure, and the one editors actually use),
  and (c) the dual‑offset LSP projection. A follow‑up benchmark should measure the incremental‑edit
  loop and concurrent‑load GC contention, where the prototype says the real wins live.

**Net:** Model C is **strongly justified** for wire/persistence RMW of large or non-ASCII payloads and anywhere sustained-load allocation pressure matters; for the editor it is used in its **incremental** mode (scan-once, many edits), which §13.8 measures. Native leads only the mid-range ASCII full-round-trip band by a small margin — not enough to justify a runtime fallback, so Model C is used across the board.

### 13.8 Round 2 — incremental editing & tail latency (measured)

The two patterns §13.7 said it under‑measured were then built and benchmarked
(`prototypes/rmw-scanner/bench2.mjs` + `editor.mjs`; the incremental session verifies its final
document equals native applying the same edits). **Both confirm the thesis decisively.**

**Part A — incremental editor session (open once, then M edits; the real VS Code pattern).**
Per‑edit cost (µs) and GC events / 1000 edits; native re‑stringifies the whole doc per edit because
`JSON.parse` keeps no source positions:

| doc | C emit (VS Code owns text) | C splice (materialise bytes) | native (re‑stringify) | native ÷ emit |
|---|---|---|---|---|
| 4 KB | **0.03 µs, 0 GC** | 1.5 µs | 3.5 µs | **101×** |
| 64 KB | **0.03 µs, 0 GC** | 7.4 µs | 45 µs | **1380×** |
| 256 KB | **0.03 µs, 0 GC** | 28 µs | 187 µs | **5879×** |

Emit‑only incremental editing is **constant‑time (~0.03 µs) regardless of document size and allocates
nothing** — it is an O(members) offset fixup plus a minimal `TextEdit`, never touching the unchanged
bulk. Native scales O(n) per edit with heavy GC. **For the killer app, Model C's incremental mode is
100×–5900× cheaper per edit and allocation‑free.** This is the result that matters for VS Code, and it
*reverses* §13.7's full‑round‑trip editor finding — because the real editor pattern is incremental, not
re‑serialise‑per‑edit (§13.7 measured the wrong pattern, as flagged).

**Part B — tail latency under sustained load (20 000 full RMW ops, per‑op latency, 64 KB).**

| | p50 µs | p99 µs | max µs | GC pause | GC count |
|---|---|---|---|---|---|
| ASCII — Model C | 130 | 219 | 915 | **21 ms** | **40** |
| ASCII — native | 78 | 311 | **2911** | 89 ms | 393 |
| Unicode — Model C | 52 | 99 | 2152 | **20 ms** | **40** |
| Unicode — native | 246 | 572 | 1874 | 113 ms | 254 |

Native's allocation rate drives **4.3–5.7× more total GC pause time and 6–10× more collections**. That
shows up in the tail: ASCII native has a *better median* (its C++ parser is fast on ASCII) but a **3.2×
worse max** (914 µs → 2911 µs — GC‑pause spikes land in the tail); Unicode Model C wins outright (p99
5.8× better). **Honest caveat:** in this single‑threaded, low‑retained‑heap microbench the tail gap is
modest and native's median sometimes wins; the GC‑pause gap would *amplify* under real concurrency and a
larger retained working set (more to scan per GC), so this still **under‑states** the production tail
impact — but the allocation→GC→tail mechanism is now measured, not asserted.

**Combined verdict (rounds 1 + 2).** The Model C thesis holds where the design claims it: (i) wire RMW
of **large or non‑ASCII** payloads — up to 5.6× throughput (§13.7); (ii) **incremental editing** — the
killer‑app pattern — 100×–5900× per edit, zero‑alloc (§13.8 Part A); (iii) **sustained‑load allocation
pressure** — 4–13× less GC across the board (§13.7, §13.8 Part B). It is **not** a throughput win for **mid-range ASCII (~4-64 KB)** full-round-trip work, where native's C++ parser leads — but only by ~1.2-1.6x and at ~10x the allocation (a lead that erodes under concurrency). The ASCII wire crossover is **non-monotonic**: Model C wins small (~360 B, ~1.5-1.7x) *and* large (>=256 KB, ~1.1-1.2x); native wins only the 4-64 KB middle band (C/N ~0.6-0.85). Because native's sole advantage is this small, confined band, **a runtime model-switching heuristic is not worth its overhead — don't ship one; standardize on Model C.** Net: **build Model C for the editor (incremental mode) and wire RMW generally; use Model B (parse-then-validate) where only the idiomatic type-only surface is needed; no runtime model-switching — the small mid-range-ASCII dip is accepted.** Remaining unmeasured: true multi‑tenant
concurrent GC contention (expected to widen Model C's lead), and a real LSP `TextEdit` round‑trip in a
live extension. **Update: §13.9 optimises the scanner and removes the mid‑range‑ASCII native‑win band
entirely — after optimisation native wins nowhere on the wire path, which makes "no runtime fallback"
not just acceptable but obvious.**

### 13.9 Model C optimisation — early‑stop + `indexOf` + byte‑only (measured)

The §13.7/§13.8 prototype had one self‑inflicted inefficiency: `scanInto` scanned the *whole* document
(including the large unedited trailing field) and tracked UTF‑16 offsets even on the wire path where
they're unused. Three changes (`scanner.mjs scanTargets` / `rmw.mjs rmwModelCOpt`) fix it, exactly
along Model C's own "don't touch what you don't read" principle:

1. **Early‑stop targeted scan** — locate *only* the edited fields (match member names against the
   codegen‑known set) and **stop once all are found**; the unscanned tail is copied verbatim by
   `applyEditsBytes` (one native memcpy), never parsed. `JSON.parse` structurally cannot do this.
2. **`Uint8Array.indexOf` string‑skipping** — jump to a string value's closing quote in native code
   (honouring escapes by counting preceding backslashes) instead of a JS per‑byte loop. This also
   speeds the *worst* case (an edit in the big trailing field), where the scan can't stop early.
3. **Byte‑only on the wire path** — drop the UTF‑16 counter (needed only for the editor/LSP
   projection). Two scan modes result: **targeted byte‑only** (wire RMW) and **full dual‑offset**
   (editor incremental, which must track every member to emit subsequent `TextEdit`s).

**Measured (optimised Copt vs native, wire bytes→bytes; "early" = edits before the big field,
"late" = rewrite the big trailing field — Copt's worst case):**

| | ASCII 4 KB | ASCII 64 KB | ASCII 256 KB | Unicode 64 KB |
|---|---|---|---|---|
| **Copt/N, early** | **2.9×** | **6.5×** | **8.9×** | **24×** |
| **Copt/N, late** | 2.4× | 3.8× | 6.9× | 11× |
| was (unoptimised C/N) | 0.80× | 0.61× | 1.13× | 3.9× |

`Copt` also speeds Model C itself by up to **9×** (`Copt/C`) and keeps GC ~10–20× below native (64 KB:
gc/1k Copt 2.0 vs native 39). **The two ASCII mid‑range cases native used to win (0.80×, 0.61×) flip to
Model C winning 2.9× and 6.5×; native wins at no size or content on the wire path, and even Copt's
worst case (rewrite the big field) wins 2.4–6.9× because `indexOf` skips the interior natively.**

**Revised conclusion (supersedes the §13.7/§13.8 native‑win finding).** With the optimised scanner,
Model C wins the wire RMW path **across the board** (1.7×–34× faster, ~10–20× less GC) on top of the
editor incremental win (100×–5900×/edit, §13.8). The fallback‑to‑native question is moot. Honest
caveats: the early‑stop win is largest when edits precede a large trailing blob (common for
append‑style payloads), but `indexOf` keeps even the worst case ahead; changed‑field content is
pre‑encoded once; single machine / single run — indicative. True unknowns unchanged: multi‑tenant
concurrent GC contention and a live LSP round‑trip.

**Future optimisation path (not pursued — already well ahead).** Two further levers remain if a
workload ever needs them, both widening Model C's lead rather than changing the conclusion:
* **Output‑buffer pooling.** `applyEditsBytes` still allocates one output `Uint8Array` per op — the
  last meaningful allocation, and what keeps `Copt`'s gc/1k at ~2–8 on large docs. Pooling a reusable
  output and returning a view (caller consumes before the next op, e.g. writes to a socket) would cut
  it further. Notably this is something Model C *can* do and native largely *cannot* (native must
  allocate a fresh string), so it only widens the gap. Left out here because it changes the
  benchmark's fairness contract.
* **Single‑edit `applyEdits` specialisation** — skip the sort/loop for the common k=1 case (micro).
These are recorded as a deliberate future path; the current optimised Model C (across‑the‑board wire
win + 100×–5900×/edit incremental + ~10–20× less GC) is the point at which this comparison wraps.

### 13.10 Provider integration spike (validated with running code)

The runtime model (§13.1–13.9) was proven by benchmark; the *provider‑side* premise of §7 — that a new
C# `ILanguageProvider` plugs into the existing language‑neutral core and emits TypeScript end‑to‑end —
was only asserted from code‑reading. It is now **validated with running code**:
`prototypes/ts-provider-spike/` is a minimal real `TypeScriptLanguageProviderSpike : ILanguageProvider`
that references *only* the core (`src-v4/Corvus.Json.CodeGeneration`) + the 2020‑12 dialect, bootstraps
`JsonSchemaTypeBuilder` exactly as `GenerationDriverV5` does, and calls `GenerateCodeUsing(provider, …)`
on a real schema. It compiles and runs, emitting an `export interface` per type. Confirmed against the
design's claims:

* **The seam compiles from a fresh external project** — `ILanguageProvider`, `TypeDeclaration`,
  `GeneratedCodeFile`, `PropertyDeclaration`, `VocabularyRegistry`, the document resolvers, and
  `JsonReference` are all consumable with two project references; no core change needed.
* **The full pipeline runs** — parse → build graph → **reduce** → name → the provider's hooks
  (`SetNamesBeforeSubschema`, `ShouldGenerate`, `GenerateCodeFor`) are invoked in order, as §2/§7
  describe.
* **The real type model is readable** — walking `PropertyDeclarations` yielded the actual property
  names, and the nested object subschema was generated as its own type (the graph + reduction work).
* **`AddMetaschema` is not required** for a self‑contained schema (the analyser matches `$schema` by
  URI string) — one fewer dependency than the CLI path uses.

Scope (deliberately minimal — this validates *integration*, not feature completeness): names default to
`Entity/Entity2…` because the spike registers no name heuristics; scalar leaf types emit as empty
interfaces because it implements no `IdentifyNonGeneratedType`/built‑in mapping; property types are
`unknown` (no type‑reference resolution); no validation. Each of those is a known §5/§7 work item, not a
surprise — the seam itself is proven. This is the concrete starting point for Phase 0 (§9).

### 13.11 Validator emission spike (validated end-to-end)

The biggest pre-implementation unknown — *can a TS provider emit a working validator from the core,
and does it compile + run correctly?* — is now **proven end-to-end** (`prototypes/ts-provider-spike/`,
extended; `TypeScriptLanguageProviderSpike` + `validate-test.mjs`). The provider walks the core type
graph `StandaloneEvaluator`-style (one `evaluate{Type}` per subschema, recursing via
`PropertyDeclaration.ReducedPropertyType`), reads the constraint model, and emits real AOT TypeScript
validators. For a schema exercising `type` / `required` / nested `properties` / `minLength` / `pattern`
/ `minimum` / `integer` / `enum`, the emitted code:

* **compiles under `tsc --strict`** (`--noEmit` exit 0) and to ESM JS;
* **runs and validates correctly** — `12/12` valid/invalid instances (missing-required, wrong-type,
  too-short, negative, non-integer, bad-enum, pattern-fail all rejected; valid + minimal-valid accepted).

Confirmed by this spike:
* **The constraint model is drivable** — `type`, `required` (`RequiredOrOptional`), per-property value
  types (`ReducedPropertyType`), and leaf constraints are all readable to drive emission.
* **Emission path — corrected (per review).** Walk-the-model proved the mechanism *fast*, but the
  **production validator wires the `IKeywordValidationHandler` composition framework** (§7.5), because
  that registry — `RegisterValidationHandlers` + keyword->handler dispatch + `IChildValidationHandler`
  composition — is the **extensibility seam**: user/third-party **custom keywords and vocabularies** plug
  in there (exactly how the OpenApi/AsyncApi providers extend the C# engine). A monolithic walk-the-model
  emitter cannot accept extensions. (The `SchemaEvaluationOnly` standalone-evaluator *mode* may still walk
  the model, as C# does.)
* **Native ECMA regex confirmed** — `pattern` emits `new RegExp(<json-string>, "u")` and runs; no
  ECMA→.NET translation (production hoists the `RegExp`).
* **Code-point length** — `minLength`/`maxLength` use `[...value].length` (Unicode code points), not
  UTF-16 `.length`.

Scope/known gaps (the spike proves the *mechanism*, full compliance is the implementation): the spike
uses JS numeric compare (production uses exact §4.1), `JSON.stringify` enum/const equality (production
uses exact numeric + deep structural §5.3), and no `allOf`/`anyOf`/`oneOf`/`unevaluated*`/`$dynamicRef`
yet — all designed (§5.4/§5.6), not yet emitted. **Net: the validation engine is demonstrated, not
assumed; the three pre-implementation spikes (integration §13.10, mutation API §5.7, validator §13.11)
are all green.**

### 13.12 Mutation API benchmark (measured)

Benchmarked the `produce` mutation API itself (`prototypes/rmw-scanner/produce-bench.mjs`), not just the
engine — with a **non-cloning** recorder (the §5.7 spike's `structuredClone` was a correctness shortcut
that would defeat Model C; the production recorder must capture the change-set without materialising the
document). `produce` vs raw Model C vs native (ops/s):

| | ASCII 360 B | 4 KB | 64 KB | 256 KB | uni 64 KB |
|---|---|---|---|---|---|
| produce / native | 0.5x | 1.9x | 7.4x | 5.8x | 14.5x |
| produce / raw Model C | 0.24x | 0.40x | 0.56x | 0.75x | 0.84x |

Honest read: the API layer is **not free** — the `Proxy` recorder + per-op change-set allocation costs
0.24-0.84x of the raw engine, worst at small docs (fixed overhead dominates), amortising as size grows.
The full `produce` path still **beats native for medium/large docs and all non-ASCII** (the
Model-C-favourable regime), losing only at the smallest ASCII doc. Clear optimisation headroom (pool the
target/edit arrays, avoid per-op re-allocation, skip the `Proxy` for shallow recipes) to close the
produce<->raw gap — a Phase-0 task, flagged not hand-waved.
