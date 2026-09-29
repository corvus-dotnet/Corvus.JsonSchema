# Benchmarks

## Corpora comparison

`corpora.mjs` runs every corpus of [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)
through `jsonschema-benchmark/main.mjs` (this evaluator's implementation of the benchmark's protocol: parse all
instances, compile, validate cold, warm up, validate warm; prints `cold,warm,compile,parse` in nanoseconds) in a fresh
process per run, and optionally through jsu-js's own benchmark program, then prints a table.

```sh
git clone --depth 1 https://github.com/sourcemeta-research/jsonschema-benchmark.git ../jsb
npm run build
node bench/corpora.mjs --schemas ../jsb/schemas --runs 3
```

To include jsu-js (JSON Schema Utils compiled to JavaScript), which needs Python 3.12 or later:

```sh
python3.12 -m venv jsu && jsu/bin/pip install "git+https://github.com/clairey-zx81/json-model@main" "git+https://github.com/zx80/json-schema-utils@main"
export PATH="$PWD/jsu/bin:$PATH"
mkdir jsujs && cd jsujs && echo '{"name":"jsu-benchmark","type":"module"}' > package.json
npm install "$(jsu-compile --runtime)/js" --install-links
cp ../jsb/implementations/jsu-js/jsonschema_benchmark.js . && cd ..
node bench/corpora.mjs --schemas ../jsb/schemas --jsu ./jsujs --runs 3
```

Results (JSON and Markdown) are written to `bench/results/`. `--only a,b` restricts the corpora.

Absolute times depend on the machine; compare implementations measured in the same run. The warm figure is a single
pass after warm-up, as in the benchmark itself, so small corpora are noisy.

## Profiling one corpus

```sh
node bench/loop.mjs ../jsb/schemas/openapi 3                       # fastest pass over the corpus
node --cpu-prof bench/loop.mjs ../jsb/schemas/openapi 3             # CPU profile of the generated functions
DUMP=openapi.js node bench/loop.mjs ../jsb/schemas/openapi 0.1      # write the generated source
```

## Adding the implementation to jsonschema-benchmark

Copy `jsonschema-benchmark/` to `implementations/corvus-ts` in a jsonschema-benchmark checkout (with the
benchmark's `memory-wrapper.sh`, which its Makefile copies in), and add the rules from `Makefile.fragment` to the
Makefile. Until the package is published to npm the image builds it from this repository (`CORVUS_REF` build argument,
default `main`).
