# Baseline against Blaze

`blaze-compare.py` runs [Blaze](https://github.com/sourcemeta/blaze), through the Sourcemeta `jsonschema` CLI that
embeds it, over the same 37 Sourcemeta corpora the harness uses, and sets its figures against a `quick` run.

1. Download a release of the CLI (no toolchain needed): the `jsonschema-<version>-linux-x86_64.zip` asset of
   https://github.com/sourcemeta/jsonschema/releases, unzip it and note the path of `bin/jsonschema`.
2. Build the harness in Release and run `quick 40`, pinned to the performance cores on an idle box, saving its output:
   `taskset -c 0-11 dotnet <harness>.dll quick 40 > quick.log`.
3. Compare:
   `tools/blaze-compare.py --cli <path>/bin/jsonschema --corpus <harness bin>/sourcemeta --quick quick.log --loop 20 --pin 0-11`

The CLI's `validate --benchmark --loop N --fast` prints, per instance, the mean and standard deviation of the
evaluation time over N loops with the instance already parsed; summing the means over a corpus gives the time to
evaluate every instance once, which is what the harness's runtime column measures (as a minimum over rounds rather
than a mean, which favours us slightly). Format assertion is off on both sides, as it is for the checked-in generated
models. The last column compares the shipping generated code with Blaze as well.

Paired measurements of an engine change use `blazebasis` rather than `quick`: run the baseline binary, the new one,
then the baseline again (the third run is the noise floor), all pinned and on an idle box. `quick 40`'s
generated-code column, an identical binary on both sides, moves by a median 10% between paired runs, so it cannot
resolve a 10% engine change.

## The four-axis tables

`measure.sh` produces the cold, warm, compile and memory comparison of every path (Blaze compiling and from its
template; the runtime evaluator compiling, from an image, under the JIT, ReadyToRun and native AOT; the generated
models under the JIT and native AOT), every corpus in its own process for every column, and `assemble.py` builds the
tables from its logs. `JSONSCHEMA=<blaze cli> ./measure.sh publish`, then `warm <tag>`, `cold <tag>`, `table <tag>`;
see the script header. The `Corvus.Text.Json.RuntimeEvaluator.ColdRunner` project is what gets published; it needs
clang for native AOT.

`measure.sh profile` produces a static profile (`corvus.mibc`) from an instrumented JIT run over the corpora with
dotnet-pgo, which is not shipped: build it from the runtime repository (`src/coreclr/tools/dotnet-pgo`, after
`./build.sh -restore -subset clr.tools /p:NuGetAudit=false`) and point `DOTNET_PGO` at its `dotnet-pgo.dll`. When the
profile is present, `publish` passes it to the native AOT builds through the runner's `ColdMibc` property; native AOT
without it runs 10 to 20% behind the JIT's tier-1 code, and the profile recovers most of that.

The profile the package ships (`src/Corvus.Text.Json/profiles/Corvus.Text.Json.mibc`, handed to ILC by the package's
`buildTransitive` targets) is `corvus.mibc` from that step; docs/ReleaseProcess.md has the release routine. To check a
packed library rather than the project, publish the runner with `-p:ColdPackage=<version>` and a
`-p:RestoreConfigFile=` naming a feed that has it: the runner then carries the package's profile through the package's
own targets (`-p:CorvusTextJsonUseProfile=false` for the control), and `warm 200 <corpus>` on both shows the difference.
