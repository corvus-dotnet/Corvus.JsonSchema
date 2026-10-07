# corvus-json-schema-bench

Performance measurement for the Java evaluator.

## In process

`Compare` validates the [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) corpora
with corvus-json-schema and the other Java validators the benchmark runs (networknt json-schema-validator and kmp), in
one JVM, interleaving the engines' timed passes so that drift in the machine's speed affects them alike. It uses the
library installed in the local Maven repository, so run `./mvnw install -DskipTests` in `../corvus-json-schema` first.

```sh
./mvnw -B -ntp package
java -cp "target/classes:$(cat target/classpath.txt)" io.github.corvusdotnet.jsonschema.bench.Compare \
    --schemas <jsonschema-benchmark>/schemas [--only a,b] [--engines corvus,networknt,kmp] [--budget-ms 1000]
```

## The jsonschema-benchmark entry

`jsonschema-benchmark/` is the `implementations/corvus-java` directory for jsonschema-benchmark. `Main` prints
`cold,warm,compile,parse` in nanoseconds. It warms up for 2 seconds (and at least 100 passes) and reports the last
warm-up pass as the warm time; the comparison harnesses for the other implementations follow the same rule, so that
every runtime is measured after its JIT has settled. `Dockerfile` builds the image from Maven Central, with an AOT cache
trained on the metaschemas; `Dockerfile.local` builds it from this checkout (`pwsh Build-Image.ps1`).

`Compare-Images.ps1` runs images over the corpora, each corpus in a fresh container, and compares their medians with
the first image's:

```sh
pwsh Compare-Images.ps1 -Schemas <jsonschema-benchmark>/schemas -Images corvus-java,blaze,corvus-rs,corvus \
    -Runs 3 -CpuSet 2-9 -Csv results.csv
```

`-CpuSet` pins the containers to those CPUs through `taskset` (rootless podman cannot use `--cpuset-cpus`).
