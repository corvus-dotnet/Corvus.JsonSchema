# Results (2026-09-09, with direct document access)

Machine shared with other work; measured with the interleaved min-of-N harness (`dotnet run -c Release -- quick 40`).

**Baseline correction.** Earlier tables in this session were measured against a `Corvus.Text.Json.dll` that turned
out to be a Debug build: the Corvus project writes Debug and Release into the same `bin/net10.0` folder, and a Debug
build had overwritten the Release one, leaving the document layer's `[Conditional("DEBUG")]` assertions compiled in on
both sides of the comparison (about a quarter of the profiled time). The tables below were taken after clearing that
folder and verifying by hash that the loaded assembly is the `obj/Release` build. The geometric mean is unchanged at
0.32 and every one of the 37 cases is now below 1.0.
All 389 tests pass (full JSON-Schema-Test-Suite incl. optional and format, annotation suite, unit tests) and
`-- diff` agrees with the generated models on every instance of all 37 Sourcemeta cases.

## Allocation

`dotnet run -c Release -- alloc` measures `GC.GetAllocatedBytesForCurrentThread` over steady-state passes with a
`GCAllocationTick` listener attached: **every warm evaluation allocates zero bytes** — all 19 asserted formats,
every keyword probe (uniqueItems, enum/const of objects, patternProperties, propertyNames, contains, escaped
names/strings, dependent schemas, if/then/else, 300-property objects, big numbers), the micro set including the
verbose collector, and full passes over all 37 corpora. The only allocation ticks in the process come from setup
(file reading, document parsing, schema compilation).

## Sourcemeta corpus, flag mode (ratio = runtime evaluator / generated model, lower is better)

Quiet machine, Release `Corvus.Text.Json` verified by hash, raw document access (`RawDocumentAccess`) in use.

| case | generated | runtime | ratio |
|---|---|---|---|
| ansible-meta | 945.46 us | 250.12 us | 0.26 |
| aws-cdk | 229.59 us | 44.30 us | 0.19 |
| babelrc | 376.18 us | 191.90 us | 0.51 |
| clang-format | 218.81 us | 146.97 us | 0.67 |
| cmake-presets | 10.17 ms | 8.40 ms | 0.83 |
| code-climate | 847.29 us | 208.54 us | 0.25 |
| cql2 | 468.72 us | 116.39 us | 0.25 |
| cspell | 5.64 ms | 1.19 ms | 0.21 |
| cypress | 583.61 us | 214.21 us | 0.37 |
| deno | 1.65 ms | 556.32 us | 0.34 |
| dependabot | 1.90 ms | 437.25 us | 0.23 |
| draft-04 | 8.64 ms | 6.85 ms | 0.79 |
| fabric-mod | 2.51 ms | 694.71 us | 0.28 |
| geojson | 28.41 ms | 11.77 ms | 0.41 |
| gitpod-configuration | 1.16 ms | 207.06 us | 0.18 |
| helm-chart-lock | 2.51 ms | 658.43 us | 0.26 |
| importmap | 746.76 us | 96.54 us | 0.13 |
| jasmine | 383.67 us | 134.38 us | 0.35 |
| jsconfig | 1.34 ms | 596.69 us | 0.44 |
| jshintrc | 2.05 ms | 766.41 us | 0.37 |
| krakend | 643.40 us | 234.63 us | 0.36 |
| lazygit | 553.80 us | 132.81 us | 0.24 |
| lerna | 596.28 us | 105.29 us | 0.18 |
| nest-cli | 1.32 ms | 199.06 us | 0.15 |
| omnisharp | 1.55 ms | 387.67 us | 0.25 |
| openapi | 12.23 ms | 9.05 ms | 0.74 |
| pre-commit-hooks | 1.52 ms | 408.55 us | 0.27 |
| pulumi | 2.02 ms | 600.35 us | 0.30 |
| semantic-release | 617.59 us | 181.19 us | 0.29 |
| stale | 811.75 us | 189.38 us | 0.23 |
| stylecop | 1.59 ms | 272.23 us | 0.17 |
| tmuxinator | 442.86 us | 101.78 us | 0.23 |
| ui5 | 3.04 ms | 1.13 ms | 0.37 |
| ui5-manifest | 12.78 ms | 3.18 ms | 0.25 |
| unreal-engine-uproject | 6.37 ms | 3.64 ms | 0.57 |
| vercel | 1.21 ms | 208.78 us | 0.17 |
| yamllint | 165.47 us | 38.92 us | 0.24 |

geometric mean ratio (runtime / generated): 0.30 over 37 cases

## Keyword-group micro benchmarks (one evaluation; typed model / generated standalone evaluator / runtime evaluator)

| category | typed | standalone | runtime | runtime/typed | runtime/standalone |
|---|---|---|---|---|---|
| Object | 310 ns | 596 ns | 124 ns | 0.40 | 0.21 |
| Array | 639 ns | 640 ns | 481 ns | 0.75 | 0.75 |
| String | 113 ns | 108 ns | 166 ns | 1.47 | 1.53 |
| Unevaluated | 427 ns | 873 ns | 272 ns | 0.64 | 0.31 |
| DynamicRef | 333 ns | 541 ns | 682 ns | 2.05 | 1.26 |
| OneOf | 85 ns | 553 ns | 50 ns | 0.59 | 0.09 |
| Verbose | 2.42 us | 2.38 us | 1.79 us | 0.74 | 0.75 |

The `String` micro case is regex-bound (the typed model uses a source-generated regex, the evaluator a compiled
one) and `DynamicRef` fluctuates between 0.8 and 2 across runs; both are in "Known gaps" in RuntimeEvaluator.md.

## Cold start (Release Corvus build, compiled regexes, machine very heavily loaded: absolute times inflated on both sides)

| schema | bytes | runtime first call | runtime warm | runtime alloc | Roslyn validator first | Roslyn validator second |
|---|---|---|---|---|---|---|
| aws-cdk | 740 | 169 ms (first compile in process) | 0.10 ms | 37 KB | 13463 ms | 6879 ms |
| cql2 | 18400 | 55 ms | 3.1 ms | 491 KB | 53662 ms | 52251 ms |
| geojson | 46177 | 13 ms | 12.3 ms | 592 KB | 79331 ms | 35334 ms |
| cmake-presets | 86084 | 17 ms | 1.7 ms | 1402 KB | 57286 ms | 59373 ms |
| ansible-meta | 37015 | 23 ms | 1.5 ms | 676 KB | 49321 ms | 38294 ms |
| openapi | 33281 | 8.7 ms | 1.2 ms | 660 KB | 32270 ms | 56131 ms |
| pulumi | 7983 | 1.2 ms | 0.10 ms | 127 KB | 15471 ms | 17897 ms |

On a quieter run earlier in the day the warm compile times were 0.06 to 3.5 ms and the Roslyn validator 0.9 to 10.6 s;
the ratio (three to four orders of magnitude) is stable across load.
