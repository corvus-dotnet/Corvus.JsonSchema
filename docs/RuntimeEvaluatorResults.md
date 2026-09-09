# Results (2026-09-09, corrected baseline)

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

| case | generated | runtime | ratio |
|---|---|---|---|
| ansible-meta | 2.26 ms | 677.47 us | 0.30 |
| aws-cdk | 480.65 us | 100.05 us | 0.21 |
| babelrc | 1.03 ms | 550.15 us | 0.53 |
| clang-format | 1.01 ms | 331.44 us | 0.33 |
| cmake-presets | 39.57 ms | 31.13 ms | 0.79 |
| code-climate | 1.88 ms | 480.82 us | 0.26 |
| cql2 | 1.01 ms | 243.06 us | 0.24 |
| cspell | 9.76 ms | 1.74 ms | 0.18 |
| cypress | 1.22 ms | 424.39 us | 0.35 |
| deno | 3.05 ms | 902.46 us | 0.30 |
| dependabot | 4.08 ms | 1.05 ms | 0.26 |
| draft-04 | 17.80 ms | 13.53 ms | 0.76 |
| fabric-mod | 3.97 ms | 1.36 ms | 0.34 |
| geojson | 56.22 ms | 33.30 ms | 0.59 |
| gitpod-configuration | 1.56 ms | 477.15 us | 0.31 |
| helm-chart-lock | 3.38 ms | 1.51 ms | 0.45 |
| importmap | 1.58 ms | 218.59 us | 0.14 |
| jasmine | 793.96 us | 309.89 us | 0.39 |
| jsconfig | 2.93 ms | 1.26 ms | 0.43 |
| jshintrc | 4.32 ms | 1.66 ms | 0.38 |
| krakend | 1.41 ms | 530.03 us | 0.38 |
| lazygit | 1.28 ms | 293.14 us | 0.23 |
| lerna | 1.40 ms | 243.79 us | 0.17 |
| nest-cli | 2.66 ms | 448.21 us | 0.17 |
| omnisharp | 3.18 ms | 778.16 us | 0.24 |
| openapi | 30.05 ms | 23.78 ms | 0.79 |
| pre-commit-hooks | 2.12 ms | 943.92 us | 0.45 |
| pulumi | 3.05 ms | 1.41 ms | 0.46 |
| semantic-release | 1.30 ms | 398.24 us | 0.31 |
| stale | 1.73 ms | 439.53 us | 0.25 |
| stylecop | 3.48 ms | 629.46 us | 0.18 |
| tmuxinator | 918.91 us | 227.55 us | 0.25 |
| ui5 | 6.38 ms | 2.05 ms | 0.32 |
| ui5-manifest | 24.66 ms | 6.39 ms | 0.26 |
| unreal-engine-uproject | 13.30 ms | 7.17 ms | 0.54 |
| vercel | 2.70 ms | 486.88 us | 0.18 |
| yamllint | 340.46 us | 72.47 us | 0.21 |

geometric mean ratio (runtime / generated): 0.32 over 37 cases

## Keyword-group micro benchmarks (one evaluation; typed model / generated standalone evaluator / runtime evaluator)

| category | typed | standalone | runtime | runtime/typed | runtime/standalone |
|---|---|---|---|---|---|
| Object | 596 ns | 881 ns | 301 ns | 0.50 | 0.34 |
| Array | 1.15 us | 1.44 us | 1.10 us | 0.96 | 0.76 |
| String | 571 ns | 437 ns | 314 ns | 0.55 | 0.72 |
| Unevaluated | 871 ns | 991 ns | 648 ns | 0.74 | 0.65 |
| DynamicRef | 706 ns | 1.03 us | 1.51 us | 2.14 | 1.47 |
| OneOf | 177 ns | 1.05 us | 95 ns | 0.53 | 0.09 |
| Verbose | 5.53 us | 5.31 us | 3.86 us | 0.70 | 0.73 |

The `DynamicRef` micro case fluctuates between 0.8 and 2.1 across runs on this loaded machine; it remains the
shape with the most interpretive overhead (see DESIGN.md, "Known gaps").

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
