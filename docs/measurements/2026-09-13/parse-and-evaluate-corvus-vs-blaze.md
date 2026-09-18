# Parse and evaluate once, warm: Blaze against Corvus (2026-09-13)

One pass over each Sourcemeta corpus, every instance parsed and evaluated once (the shape of a service validating each request), at steady state. Per corpus: the sum over instances of the per-instance mean. Blaze 16.10.0 (`jsonschema validate --fast`), Corvus runtime evaluator on .NET 10 (JIT). Same machine, same instances, each corpus in its own process.

| corpus | instances | Blaze via CLI | Blaze less CLI bookkeeping | Corvus | Corvus / Blaze |
|---|---:|---:|---:|---:|---:|
| ansible-meta | 333 | 1.44 ms | 1.03 ms | 213.3 µs | 0.21 |
| aws-cdk | 483 | 3.11 ms | 2.50 ms | 358.2 µs | 0.14 |
| babelrc | 794 | 2.18 ms | 1.19 ms | 279.2 µs | 0.24 |
| clang-format | 133 | 508.4 µs | 342.1 µs | 59.1 µs | 0.17 |
| cmake-presets | 967 | 20.01 ms | 18.80 ms | 3.63 ms | 0.19 |
| code-climate | 2484 | 10.73 ms | 7.62 ms | 1.08 ms | 0.14 |
| cql2 | 109 | 487.4 µs | 351.2 µs | 95.7 µs | 0.27 |
| cspell | 981 | 6.83 ms | 5.61 ms | 897.5 µs | 0.16 |
| cypress | 981 | 4.39 ms | 3.17 ms | 542.1 µs | 0.17 |
| deno | 987 | 6.42 ms | 5.19 ms | 904.8 µs | 0.17 |
| dependabot | 967 | 4.30 ms | 3.09 ms | 591.5 µs | 0.19 |
| draft-04 | 563 | 41.74 ms | 41.04 ms | 5.60 ms | 0.14 |
| fabric-mod | 911 | 5.62 ms | 4.48 ms | 729.3 µs | 0.16 |
| geojson | 500 | 191.61 ms | 190.98 ms | 51.78 ms | 0.27 |
| gitpod-configuration | 986 | 3.79 ms | 2.55 ms | 484.4 µs | 0.19 |
| helm-chart-lock | 3888 | 12.79 ms | 7.93 ms | 1.27 ms | 0.16 |
| importmap | 964 | 3.52 ms | 2.32 ms | 328.2 µs | 0.14 |
| jasmine | 980 | 2.40 ms | 1.18 ms | 289.1 µs | 0.25 |
| jsconfig | 981 | 3.37 ms | 2.14 ms | 464.8 µs | 0.22 |
| jshintrc | 966 | 4.90 ms | 3.69 ms | 624.8 µs | 0.17 |
| krakend | 47 | 989.1 µs | 930.4 µs | 155.1 µs | 0.17 |
| lazygit | 280 | 989.4 µs | 639.4 µs | 146.7 µs | 0.23 |
| lerna | 985 | 3.11 ms | 1.87 ms | 319.9 µs | 0.17 |
| nest-cli | 1025 | 3.64 ms | 2.36 ms | 389.4 µs | 0.16 |
| omnisharp | 987 | 4.37 ms | 3.14 ms | 465.5 µs | 0.15 |
| openapi | 107 | 77.69 ms | 77.55 ms | 13.04 ms | 0.17 |
| pre-commit-hooks | 985 | 4.84 ms | 3.61 ms | 547.1 µs | 0.15 |
| pulumi | 3807 | 12.88 ms | 8.12 ms | 1.14 ms | 0.14 |
| semantic-release | 794 | 3.74 ms | 2.74 ms | 415.5 µs | 0.15 |
| stale | 961 | 3.04 ms | 1.84 ms | 402.8 µs | 0.22 |
| stylecop | 983 | 3.96 ms | 2.73 ms | 566.2 µs | 0.21 |
| tmuxinator | 382 | 1.91 ms | 1.43 ms | 231.7 µs | 0.16 |
| ui5 | 942 | 5.04 ms | 3.86 ms | 864.7 µs | 0.22 |
| ui5-manifest | 611 | 13.84 ms | 13.07 ms | 2.52 ms | 0.19 |
| unreal-engine-uproject | 859 | 3.86 ms | 2.79 ms | 570.1 µs | 0.20 |
| vercel | 710 | 2.76 ms | 1.87 ms | 358.9 µs | 0.19 |
| yamllint | 984 | 3.81 ms | 2.58 ms | 424.1 µs | 0.16 |
| **median** | | **3.86 ms** | **2.74 ms** | **484.4 µs** | **0.17** |

Corvus is faster on 37 of 37 corpora; geometric mean of Corvus over Blaze (less bookkeeping) 0.18.

How the two sides were timed. Corvus: in process, a timer around parse, evaluate and dispose per instance, 100 loops, clock overhead subtracted. Blaze: its benchmark mode times evaluation of an already parsed instance only, so the parse-inclusive figure is taken by difference: the CLI validating ten copies of the corpus (`validate --fast`, output discarded) minus the same over one instance, per instance, median of three. That difference carries the CLI's per-instance bookkeeping (reading the line, result handling, output), measured at 1.25 µs per instance with trivial instances (`{}`, `1`) against the empty schema; the fourth column subtracts it, and the ratio uses that column. What remains in Blaze's figure is its JSON parse and its evaluation; an in-process harness would remove the last of the doubt.

