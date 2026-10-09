# CorvusJsonSchemaBench

Performance measurement for the Julia evaluator. It is a project of its own, so that the package keeps no
dependencies. Its `Project.toml` points at the package in this checkout (`[sources]`), and at the benchmark entry
below.

```sh
julia --project=. -e 'using Pkg; Pkg.develop([PackageSpec(path="../CorvusJsonSchema"), PackageSpec(path="jsonschema-benchmark")]); Pkg.instantiate()'
julia --project=. test/runtests.jl
```

Julia 1.11 and later read `[sources]`, so `Pkg.instantiate()` alone is enough there. Julia 1.10 does not, and needs
the `Pkg.develop` call. `Manifest.toml` is not committed: the project is not a release artefact, and one manifest
does not serve both Julia 1.10 and the latest.

`test/runtests.jl` runs both programs below on small corpora and checks what they print. It measures nothing.

## In process

`compare.jl` validates the [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)
corpora with CorvusJsonSchema and with [JSONSchema.jl](https://github.com/JuliaIO/JSONSchema.jl), in one process.
It interleaves the engines' timed passes so that drift in the machine's speed affects them alike.

```sh
julia --project=. compare.jl --schemas <jsonschema-benchmark>/schemas [--only a,b] [--engines corvus,jsonschema] [--budget-ms 1000]
```

Each engine is used as its documentation says. CorvusJsonSchema validates documents parsed by `parse_document`.
JSONSchema.jl validates values parsed by JSON.jl. The schema is the benchmark's `schema-noformat.json` when the
checkout has made it, and otherwise `schema.json` with its `format` keywords removed. An engine that rejects an
instance, or cannot compile the schema, is reported instead of timed. JSONSchema.jl implements draft 4, 6 and 7, so
it is reported and not timed on a corpus whose schema needs a later draft. jsonschema-benchmark has no Julia
implementation to compare with.

## The jsonschema-benchmark entry

`jsonschema-benchmark/` is the `implementations/corvus-jl` directory for jsonschema-benchmark. It is a small package,
`CorvusJsonSchemaBenchmark`, with `main.jl` to start it. The program takes the schema file and the instances file
and prints `cold,warm,compile,parse` in nanoseconds. It warms up for 2 seconds (and at least 100 passes) and reports
the last warm-up pass as the warm time, the same rule as the Go entry's `main.go`
(`src-go/corvus-json-schema-bench/jsonschema-benchmark`) and the Java entry's `Main`. It exits 1 if an instance is
invalid, and 2 for a file it cannot read, text that is not JSON or a schema that does not compile.

The program is a package, and not a script, so that Julia compiles it once. A script is compiled again by every
process that runs it. The package has a precompile workload that runs the protocol on a small corpus, and the
library has its own, so a process that runs the program compiles nothing (`julia --trace-compile=stderr` prints
nothing for a run).

Its `Project.toml` names the registered package, with no `[sources]`, as the benchmark needs. `Dockerfile` builds
the image from Julia's General registry. It cannot be built until the package's first version is registered there:
the `ADD` of the package's `Versions.toml` fails. `Dockerfile.local` builds the image from this checkout:

```sh
pwsh Build-Image.ps1
```

To run the entry outside a container, use this directory's parent as the project, which has the entry and the
library from this checkout:

```sh
julia --project=.. main.jl <schema-noformat.json> <instances.jsonl>
```

The image takes the package's latest version in the General registry when it is built (`Project.toml` names the
oldest version the entry works with), and `version.sh` reports the version it built. The image compiles the package
and the program into package images when it is built, for the processor of the machine that builds it. Build the
image on the machine that runs it, as the benchmark's Makefile does. On another kind of processor Julia finds the
images unusable and compiles them again when the container starts, which the figures do not show and the run time
does.

To add the entry to a jsonschema-benchmark checkout:
- copy the directory to `implementations/corvus-jl`, without `Dockerfile.local`, `Dockerfile.sysimage`,
  `sysimage.jl`, `Build-Image.ps1`, `Compare-Images.ps1`, `Makefile.fragment` and `memory-wrapper.sh` (the
  benchmark's Makefile copies its own);
- add the rules from `Makefile.fragment` to the Makefile;
- add `corvus-jl` to the README's list of implementations and to the names in the plot scripts.

`Compare-Images.ps1` runs images over the corpora, each corpus in a fresh container, and compares their medians with
the first image's. It is the Go entry's script with `corvus-jl` first in its default list:

```sh
pwsh Compare-Images.ps1 -Schemas <jsonschema-benchmark>/schemas -Images corvus-jl,corvus-go,corvus-rs,corvus-java,blaze `
    -Runs 3 -CpuSet 2-9 -Csv results.csv
```

`-CpuSet` pins the containers to those CPUs through `taskset` (rootless podman cannot use `--cpuset-cpus`). Images
that take the corpus directory instead of the two files are named by `-DirectoryImages` (by default `blaze` and
`go-jsonschema`).

### Package images or a system image

A Julia process can get compiled code in two ways. The default image uses the first, and `pwsh Build-Image.ps1
-SystemImage` builds `jsonschema-benchmark/corvus-jl-sysimage` with the second.

- **Package images** are what `Pkg.precompile` writes, and what anyone who installs the package has. A process
  loads the image of each package it uses when it reaches `using`. This is the image the benchmark entry builds, so
  a container measures the package as a user has it.
- **A system image** made by [PackageCompiler](https://github.com/JuliaLang/PackageCompiler.jl) holds Julia's own
  system image with the package and the program compiled into it. The process starts from it, and `using` then
  loads nothing. PackageCompiler is installed in an environment of its own while the image is built, so neither the
  package nor the program depends on it.

Both were built and run here on the same corpora (Julia 1.13.1, 2026-10-08), to see what the choice changes and not
to measure it:

- With either, Julia compiles nothing while the program runs. The four figures of the protocol came out alike from
  both images, as they should: none of the timed sections waits for a compiler in either.
- The system image removes the loading of the two package images from the start of the process. That is a small
  part of the time a Julia process takes to start, most of which is Julia starting, and it is outside what the
  protocol times.
- The system image made the container's peak memory, which `memory-wrapper.sh` reports, somewhat lower.
- The system image costs a C compiler in the image, several minutes of build, and an image about half a gigabyte
  larger, and it is not how the package is installed.

So the entry uses package images, and the system image stays an option for a comparison of the two.

No figures are published for this port yet. Timing runs need a host with its CPUs pinned and nothing else running.
