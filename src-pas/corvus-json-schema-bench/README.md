# corvus-json-schema-bench (Object Pascal)

The program that runs the Object Pascal evaluator under the
[jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) protocol.

`jsonschema-benchmark/corvus_pascal_benchmark.pas` takes a schema and a file of instances, one to a line. It parses
every instance, compiles the schema, validates every instance once cold, warms up for 2 seconds (at least 100
passes), and prints `cold,warm,compile,parse` in nanoseconds for the last warm-up pass. It exits 1 if an instance is
not valid. It reads the clock through the `Linux` unit, so it builds for Linux only.

```sh
mkdir -p build/units
fpc -O3 -Fu../corvus-json-schema/src -Fi../corvus-json-schema/src -FUbuild/units -FEbuild \
    jsonschema-benchmark/corvus_pascal_benchmark.pas
build/corvus_pascal_benchmark schema.json instances.jsonl
```

The figures in the package's [README](../corvus-json-schema/README.md#performance) come from this program in a
container, beside the other implementations' containers, with `Compare-Images.ps1` of the Go module's
[benchmark directory](../../src-go/corvus-json-schema-bench/jsonschema-benchmark).
