# corvus-json-schema-bench

Performance measurement for the Go evaluator. It is a module of its own, so that the library keeps no dependencies.
A `replace` directive in `go.mod` points it at the library in this checkout.

## In process

The program at the root of the module validates the
[jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) corpora with corvus-json-schema
and with [santhosh-tekuri/jsonschema](https://github.com/santhosh-tekuri/jsonschema) v6, the Go validator the
benchmark runs (`implementations/go-jsonschema`), in one process. It interleaves the engines' timed passes so that
drift in the machine's speed affects them alike.

```sh
go run . --schemas <jsonschema-benchmark>/schemas [--only a,b] [--engines corvus,santhosh] [--budget-ms 1000]
```

Each engine is used as the benchmark uses it. Corvus validates documents parsed by `ParseDocument`. The other engine
validates values decoded by `encoding/json`, at the version the benchmark pins (6.0.1). The schema is the benchmark's
`schema-noformat.json` when the checkout has made it, and otherwise `schema.json` with its `format` keywords removed.
An engine that rejects an instance, or cannot compile the schema, is reported instead of timed.

## The jsonschema-benchmark entry

`jsonschema-benchmark/` is the `implementations/corvus-go` directory for jsonschema-benchmark, and a module of its
own. `main.go` takes the schema file and the instances file and prints `cold,warm,compile,parse` in nanoseconds. It
warms up for 2 seconds (and at least 100 passes) and reports the last warm-up pass as the warm time, the same rule as
the Java entry's `Main` (`src-java/corvus-json-schema-bench/jsonschema-benchmark`). It exits 1 if an instance is
invalid.

Its `go.mod` requires the published module by path and version, with no `replace`, as the benchmark needs.
`Dockerfile` builds the image from the Go module proxy. `Dockerfile.local` builds it from this checkout, with a
workspace standing in for the published module:

```sh
pwsh Build-Image.ps1
```

To build the entry outside a container, make the same workspace (`go.work` is ignored by git):

```sh
cd jsonschema-benchmark
go work init . ../../corvus-json-schema
go build -o corvus_go_benchmark .
./corvus_go_benchmark <schema-noformat.json> <instances.jsonl>
```

To add the entry to a jsonschema-benchmark checkout once the module's first version is tagged:
- copy the directory to `implementations/corvus-go`, without `Dockerfile.local`, `Build-Image.ps1`,
  `Compare-Images.ps1`, `Makefile.fragment` and `memory-wrapper.sh` (the benchmark's Makefile copies its own);
- run `go mod tidy` there, which writes the `go.sum` the image build needs (it cannot exist before the version does);
- add the rules from `Makefile.fragment` to the Makefile;
- add `corvus-go` to the README's list of implementations and to the names in the plot scripts.

`Compare-Images.ps1` runs images over the corpora, each corpus in a fresh container, and compares their medians with
the first image's:

```sh
pwsh Compare-Images.ps1 -Schemas <jsonschema-benchmark>/schemas -Images corvus-go,go-jsonschema,corvus-rs,corvus-java,blaze `
    -Runs 3 -CpuSet 2-9 -Csv results.csv
```

`-CpuSet` pins the containers to those CPUs through `taskset` (rootless podman cannot use `--cpuset-cpus`). Images
that take the corpus directory instead of the two files are named by `-DirectoryImages` (by default `blaze` and
`go-jsonschema`).

The benchmark's own `go-jsonschema` image warms up by a number of passes and times one more pass after the loop. Go
compiles ahead of time, so the two rules measure the same code, but a comparison is only like for like when both
images follow one rule. The in-process program above applies one rule to both engines.

No figures are published for this port yet. Timing runs need a host with its CPUs pinned and nothing else running.
