# corvus-json-schema-bench (OCaml)

The program that runs the OCaml evaluator under the
[jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) protocol.

`jsonschema-benchmark/corvus_ocaml_benchmark.ml` takes a schema and a file of instances, one to a line. It parses
every instance, compiles the schema, validates every instance once cold, warms up for 2 seconds (at least 100
passes), and prints `cold,warm,compile,parse` in nanoseconds for the last warm-up pass. It exits 1 if an instance is
not valid. That is the protocol, the warm-up rule and the command line of the Object Pascal, Go and Julia harnesses.

The OCaml standard library has no monotonic clock, so the program reads `clock_gettime` through a C stub
(`clock_stubs.c`). It builds where that function is, which is Linux and macOS. The library itself has no C code.

The program is a dune executable that takes the library from the workspace it is built in, so it is built from a
root that holds both directories. From `src-ml`:

```sh
dune build --root . --profile release ./corvus-json-schema-bench/jsonschema-benchmark/corvus_ocaml_benchmark.exe
_build/default/corvus-json-schema-bench/jsonschema-benchmark/corvus_ocaml_benchmark.exe schema.json instances.jsonl
```

The compiler's flags are dune's defaults for the release profile. `-O3` and `-unboxed-types` are for a compiler built
with flambda, which the compilers of the opam images are not, so the dune file gives neither.

`jsonschema-benchmark/Build-Image.ps1` builds the image `jsonschema-benchmark/corvus-ml` from this checkout with
OCaml 5.5 (`Dockerfile.local`), for use beside the other implementations' images with `Compare-Images.ps1` of the Go
module's [benchmark directory](../../src-go/corvus-json-schema-bench/jsonschema-benchmark):

```sh
pwsh jsonschema-benchmark/Build-Image.ps1
podman run --rm -v ~/src/jsonschema-benchmark/schemas:/workspace:ro jsonschema-benchmark/corvus-ml \
    /workspace/geojson/schema.json /workspace/geojson/instances.jsonl
```

Through `memory-wrapper.sh` the image prints the peak memory in kilobytes after the four figures.
