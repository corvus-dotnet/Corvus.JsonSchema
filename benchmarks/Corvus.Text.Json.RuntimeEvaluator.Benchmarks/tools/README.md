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