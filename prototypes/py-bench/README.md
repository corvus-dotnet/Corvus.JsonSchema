# py-bench — Sourcemeta-dataset validation benchmark

Benchmarks the **corvus-py** validators (emitted by the production CLI, `corvus jsonschema … --engine
Python`) against the main Python JSON Schema validators, over the
[Sourcemeta `jsonschema-benchmark`](https://github.com/sourcemeta-research/jsonschema-benchmark) dataset:
**37 real-world schemas** (npm/CI/cloud config schemas — 35 draft-07, 2 2020-12) and **~35,000 instances**.
This is the Python peer of `../ts-bench`, with the same methodology.

## Competitors

| Impl | Package | Kind | Notes |
|---|---|---|---|
| **corvus-py** | (this repo, `--engine Python`) | generated Python | AOT validators over `json.loads`'d values |
| jsonschema | `jsonschema` | pure Python | the reference implementation; format annotation-only (no `format_checker`) |
| fastjsonschema | `fastjsonschema` | generated Python | compiles the schema to Python |
| jsonschema-rs | `jsonschema-rs` | Rust (PyO3) | `validate_formats=False` |

## Methodology

Mirrors ts-bench (and Sourcemeta's): per schema, every validator receives the *same* `json.loads`'d
instances. One cold pass (records the valid-count, catches a throwing validator), `WARMUP` warm-up passes,
then `ROUNDS` timed warm passes over all instances, median. **Compile time is excluded** — steady-state
validation throughput only. A **correctness cross-check** records each validator's valid-count per schema; a
fast-but-wrong validator is meaningless, so the headline metric is **correctness-gated** (only a validator
that produced the consensus valid-count earns credit on that schema).

```
./generate.sh /path/to/jsonschema-benchmark      # regenerates bench-gen/ via the production CLI
WARMUP=8 ROUNDS=9 <gate-venv>/python bench.py /path/to/jsonschema-benchmark
```

> **`generate.sh` passes `--assertFormat false`, and must.** The dataset's expected valid-counts (and the
> other validators) treat `format` as annotation-only (the 2020-12 default). The Python CLI asserts formats
> by default (matching the TS/C# CLIs), so the benchmark must opt out — otherwise corvus-py would reject
> `format`-violating instances the consensus accepts, producing spurious disagreements and an unfair
> comparison.
>
> The generated output directory name is the schema name with `-` → `_` so it is an importable package.
> Timings need a **quiet box** — a concurrent process turns the ns numbers into noise (see ts-bench).

## Results (WARMUP=8, ROUNDS=9 median; CPython 3.12; corvus-py with the leaf-inline / dict-dispatch / native-numeric optimizations)

| Validator | Kind | Correct on | Geomean ns/instance | Fastest-*correct* wins |
|---|---|:---:|---:|:---:|
| jsonschema-rs | Rust | 36 / 37 (cspell fails) | 3,172 ns | 29 |
| **corvus-py** | generated Python | **37 / 37** | 10,774 ns | 2 |
| fastjsonschema | generated Python | 30 / 37 | 7,556 ns | 6 |
| jsonschema | pure Python | 37 / 37 | 181,854 ns | 0 |

**jsonschema-rs (native Rust) is the clear leader** on speed and correctness. **corvus-py is the only
validator correct on all 37 schemas** (jsonschema-rs fails cspell), and is ~17× faster than the pure-Python
reference. **fastjsonschema** fails to compile 6 (cql2, krakend, pulumi, ui5, ui5-manifest, vercel) and is
stricter than the consensus on helm-chart-lock.

**corvus-py vs fastjsonschema** (the two generated-Python engines), geomean over the same fair instances
(`benchfj.py`): **0.938×** — competitive, and it *wins* on every composition-heavy schema (geojson ~5×,
tmuxinator ~4×, fabric-mod ~4×, cmake-presets ~3.6×, draft-04 ~2×), losing only on the sub-µs flat schemas
where fixed per-property loop overhead dominates.

**The Python speed reality differs from the JS one.** In JS, corvus-ts *wins* (it competes with other JS
validators). In Python the fastest option is native **Rust** (`jsonschema-rs`), which generated Python can't
beat on raw throughput. corvus-py's position is: **correct (uniquely all-37) + typed (`TypedDict`) +
byte-native + pure-Python-installable (no Rust toolchain), at fastjsonschema-class speed.**

## Correctness cross-check

**36/37 schemas: all four validators agree on valid-counts.** The only disagreement is `fastjsonschema` on
helm-chart-lock (stricter: 3760 vs 3888). corvus-py agrees with the reference `jsonschema` on every schema.

> **fastjsonschema mutates its input** (it fills `default` values — a draft-04 instance grows 6740 → 21127
> chars). The harnesses give fastjsonschema a private `copy.deepcopy` (untimed) so it cannot corrupt the
> pristine list the other validators read. Without this, whichever validator ran after fastjsonschema on the
> shared list (jsonschema-rs, 4th) validated defaulted garbage and appeared to "wrongly reject" many schemas —
> an artifact, not a real disagreement. A default-heavy schema also makes fastjsonschema's own per-fresh-instance
> cost higher than a warm re-validation of already-defaulted data suggests.

## Module-per-type porting bugs found and fixed (via this benchmark)

The benchmark surfaced three real defects unique to the module-per-type Python shape (which the single-file
TS engine never hits), all now fixed:

- **Union runtime alias → circular import.** `X = A | B` is a *runtime* expression that eagerly evaluates the
  member references at import and deadlocks on a recursive/circular schema. Fixed to PEP 695 `type X = A | B`
  (lazily evaluated, type-only — the analog of TypeScript's erased `type X = …`).
- **Functional `TypedDict` eager evaluation.** A `$`-prefixed key (e.g. `$ref`) forces the functional
  `TypedDict("X", {"$ref": Sequence[schema.Schema]})` form, which eagerly evaluates the member types →
  circular. Fixed to string forward references.
- **Module / local name collision.** A property named `value` produced a module `value` that shadowed the
  validator's `value` parameter (and modules could shadow runtime imports like `fresh`/`eq`). Fixed by
  importing every sibling module under a collision-free `_m_`-prefixed alias.
