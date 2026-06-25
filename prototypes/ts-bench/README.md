# ts-bench — Sourcemeta-dataset validation benchmark

Benchmarks the **corvus-ts** validators (emitted by the production CLI, `corvus jsonschema … --engine
TypeScript`) against the fastest JS/TS JSON Schema validators, over the
[Sourcemeta `jsonschema-benchmark`](https://github.com/sourcemeta-research/jsonschema-benchmark) dataset:
**37 real-world schemas** (npm/CI/cloud config schemas — 35 draft-07, 2 2020-12) and **35,407 instances**.

## Competitors

| Impl | Package | Notes |
|---|---|---|
| **corvus-ts** | (this repo, `--engine TypeScript`) | AOT-compiled validators over `JSON.parse`'d values |
| ajv | `ajv` / `ajv/dist/2020` / `ajv-draft-04` | `{strict:false}`, no `ajv-formats` (formats as annotations) |
| @exodus/schemasafe | `@exodus/schemasafe` | `{mode:'spec', isJSON:true}` |
| @hyperjump/json-schema | `@hyperjump/json-schema` | compiled validator (sync) |

## Methodology

Mirrors Sourcemeta's: per schema, every validator receives the *same* `JSON.parse`'d instances. We run one
cold pass (records the valid-count), `WARMUP` warm-up passes (default 30 — enough for V8 JIT convergence),
then time **one warm pass** over all instances. **Compile time is excluded** — this measures steady-state
validation throughput. A **correctness cross-check** records each validator's valid-count per schema; a
fast-but-wrong validator is meaningless, so disagreements are surfaced.

```
npm install
# generate validators for all 37 schemas via the production CLI, then compile:
#   for each schema: corvus jsonschema <schema> --engine TypeScript --outputPath bench-gen/<name>
#   npx tsc -p tsconfig.bench.json
node bench.mjs /path/to/jsonschema-benchmark      # WARMUP=N to override
```

## Results (WARMUP=30, ROUNDS=7 median; see `RESULTS.txt` for the full per-schema table)

Geometric mean of ns/instance (warm, compile excluded; **median of 7 passes** — single-pass runs are too
noisy to trust, see the optimization study below) — **lower is better**:

| Validator | Geomean ns/instance | Schemas handled | Fastest on |
|---|---:|:---:|:---:|
| **corvus-ts** | **2,124** | **37 / 37** | 17 |
| @exodus/schemasafe | 2,135 | 29 / 37 | 5 |
| ajv | 2,464 | 35 / 37 | 15 |
| @hyperjump/json-schema | 34,009 | 35 / 37 | 0 |

**corvus-ts has the best geometric-mean throughput *and* is the only validator that handles all 37
schemas.** It is the single fastest validator on 17 of 37 (ajv 15, schemasafe 5, hyperjump 0). schemasafe
fails to compile 8 of the 37 real-world schemas; ajv and hyperjump each fail 2; hyperjump is ~16× slower.
The wins are large on complex schemas (openapi **28.6×** vs ajv, cypress 22.9×, jsconfig 5.6×, omnisharp
4.5×, geojson 4.3×, babelrc 4.4×), which dominate the geomean; the losses are on small/flat schemas where
ajv's hand-tuned property dispatch wins, mostly by < 2× and several within ~1–6% (effectively ties). We
**never** lose to hyperjump.

## Correctness

33/37 schemas: **all four validators agree** on valid-counts. The 4 disagreements:

- **code-climate**, **helm-chart-lock**, **draft-04** — corvus-ts agrees with the **majority**; the
  outlier is ajv (stricter) or schemasafe (stricter/erroring), not corvus.
- **openapi** (2020-12 OpenAPI 3.1 metaschema, `$dynamicRef`-heavy) — a genuine 3-way split
  (corvus 9 / ajv 0 / hyperjump 107 valid of 107). No ground truth; the validators disagree wildly with
  each other. Flagged for separate study.

## Finding: the krakend `u`-flag regex bug (fixed)

krakend originally **failed in all four validators** (including corvus-ts). Its schema uses a `pattern`
with identity escapes — `[^\*\?\&\%]` — which are valid ECMA-262 *without* the Unicode (`u`) flag but a
`SyntaxError` *with* it. corvus-ts (like ajv/hyperjump/schemasafe) compiled patterns with `u` and crashed.

Fixed in the generator: the `pattern`/`patternProperties` **keyword** now compiles through a cached `__re`
helper that tries `u` first (correct Unicode semantics; the test-suite pattern cases stay green) and falls
back to non-`u` for identity-escape patterns. `format: regex` deliberately stays `u`-only (strict
ECMA-262 validity — `\a` *is* an invalid escape there, per the test suite). After the fix corvus-ts
validates krakend 47/47 and is the **only** validator in this set that compiles all 37 schemas, while the
full JSON-Schema-Test-Suite still passes 7848/7849.

## Optimization study — direct property access (NEGATIVE result; do not re-attempt without an A/B)

We lose to Ajv on a few tiny flat objects, so we tried Ajv's strategy: replace the `Object.keys` + key
loop with **direct property access** (`if (Object.hasOwn(o, "x")) …`), which required switching the
evaluation tracker's property marks from an **index bitmask** to a **name `Set<string>`**. It passed the
suite (7848/7849) but was a **33% regression** — measured by a *controlled same-run A/B*
(`bench-compare.mjs`, median of 7 passes, old vs new vs ajv adjacently, so machine-state noise hits all
impls equally):

```
Geomean NEW/OLD speedup: 0.672x   (>1 = faster)   →  refactor 33% SLOWER
Per-schema: NEW faster on 1, OLD faster on 34, ~tie on 2
Geomean ns/instance:  old 1222   new 1820   ajv 1376    →  the BASELINE already beats ajv
```

Why the baseline wins: the index bitmask `markProp(i)` = `pl |= 1<<i` is zero-allocation; a `Set<string>`
allocates + hashes (brutal for `unevaluated*`/`allOf` tracking schemas — openapi 0.32×). And in V8,
`Object.keys(o)` (a fast intrinsic over a stable hidden class) + interned-string identity compares beats
N× `Object.hasOwn` + N× property accesses. Ajv's edge is its *whole* specialised machinery, not "direct
access" in isolation.

**Method lesson:** a single noisy run (a concurrent process on the box) had shown the refactor *improving*
2,300→1,957 ns — pure machine-state noise that would have led to committing a 33% regression. Only the
same-run A/B exposed it. **Measure optimizations with `bench-compare.mjs`, never by comparing two separate
full runs.**
