# Corvus runtime evaluator (JIT) against Blaze, warm, one process per corpus

| Corpus | Instances | Corvus runtime (JIT) | Blaze | Corvus / Blaze | Corvus fastest |
|---|---:|---:|---:|---:|:---:|
| ansible-meta | 333 | 77.7 µs | 111.9 µs | 0.69 | x |
| aws-cdk | 483 | 16.6 µs | 24.6 µs | 0.67 | x |
| babelrc | 794 | 50.7 µs | 48.0 µs | 1.06 |  |
| clang-format | 133 | 16.6 µs | 47.2 µs | 0.35 | x |
| cmake-presets | 967 | 1.62 ms | 1.82 ms | 0.89 | x |
| code-climate | 2484 | 61.6 µs | 81.8 µs | 0.75 | x |
| cql2 | 109 | 53.0 µs | 105.8 µs | 0.50 | x |
| cspell | 981 | 168.6 µs | 250.9 µs | 0.67 | x |
| cypress | 981 | 51.3 µs | 77.7 µs | 0.66 | x |
| deno | 987 | 81.1 µs | 117.5 µs | 0.69 | x |
| dependabot | 967 | 127.7 µs | 146.0 µs | 0.87 | x |
| draft-04 | 563 | 1.70 ms | 3.15 ms | 0.54 | x |
| fabric-mod | 911 | 168.7 µs | 332.9 µs | 0.51 | x |
| geojson | 500 | 5.17 ms | 6.41 ms | 0.81 | x |
| gitpod-configuration | 986 | 70.7 µs | 101.9 µs | 0.69 | x |
| helm-chart-lock | 3888 | 186.8 µs | 81.3 µs | 2.30 |  |
| importmap | 964 | 20.3 µs | 15.7 µs | 1.29 |  |
| jasmine | 980 | 52.1 µs | 53.3 µs | 0.98 | x |
| jsconfig | 981 | 145.7 µs | 179.7 µs | 0.81 | x |
| jshintrc | 966 | 107.5 µs | 466.2 µs | 0.23 | x |
| krakend | 47 | 66.3 µs | 85.3 µs | 0.78 | x |
| lazygit | 280 | 38.2 µs | 56.7 µs | 0.67 | x |
| lerna | 985 | 34.9 µs | 60.5 µs | 0.58 | x |
| nest-cli | 1025 | 63.3 µs | 90.6 µs | 0.70 | x |
| omnisharp | 987 | 67.5 µs | 239.0 µs | 0.28 | x |
| openapi | 107 | 2.99 ms | 5.82 ms | 0.51 | x |
| pre-commit-hooks | 985 | 95.5 µs | 218.5 µs | 0.44 | x |
| pulumi | 3807 | 181.4 µs | 257.0 µs | 0.71 | x |
| semantic-release | 794 | 37.5 µs | 43.0 µs | 0.87 | x |
| stale | 961 | 70.2 µs | 72.0 µs | 0.97 | x |
| stylecop | 983 | 103.9 µs | 121.1 µs | 0.86 | x |
| tmuxinator | 382 | 23.1 µs | 52.5 µs | 0.44 | x |
| ui5 | 942 | 373.4 µs | 236.2 µs | 1.58 |  |
| ui5-manifest | 611 | 1.28 ms | 1.18 ms | 1.08 |  |
| unreal-engine-uproject | 859 | 160.6 µs | 217.5 µs | 0.74 | x |
| vercel | 710 | 87.7 µs | 114.7 µs | 0.76 | x |
| yamllint | 984 | 11.7 µs | 6.3 µs | 1.85 |  |

Corvus is fastest on 31 of 37 corpora. Geometric mean of Corvus over Blaze: 0.73.
