---
name: corvus-benchmark-results-page
description: >
  The standard presentation of a benchmark run: one self-contained, interactive HTML page (ratio leaderboard,
  per-workload matrix, one-workload bars, head to head, method) generated from a JSON data file by a stdlib Python
  script, with the vocabulary and the measures taken from the data so it fits any benchmark. First used for the
  sourcemeta jsonschema-benchmark results site, then for Corvus.Regex.Ecma262's Industry benchmarks.
  USE FOR: publishing or updating benchmark results, adding a results page to a repository or site, converting a
  BenchmarkDotNet or other run into the page.
  DO NOT USE FOR: running benchmarks or deciding what to measure.
---

# Benchmark results page

## The standard

A benchmark's results are published as **one HTML page, generated from committed data**, never as a hand-written
table alone and never as screenshots:

1. **The data file** (`<run>.json`, in the shape `site/generate_site.py` documents) is committed beside the run's
   markdown record. It names the run (commit, date, host, probe readings), the implementations or engines
   (`impls`, with a `group` such as a language or a vendor), the workloads (`schemas`), the measures, and every
   result: the runs of each measurement, or its summary (`median`, `min`, `max`). A failure is `null`; a missing
   measurement is absent.
2. **The page** is `site/generate_site.py --data <run>.json --out <dir>/index.html`, built by the site build (not
   committed) and served next to the documentation. It has, in this order: a dark run header with the facts of the
   run; a sticky control row (measure, "compare with", the implementation picker grouped by `group`); **Overall**,
   a leaderboard of geometric-mean ratios with "fastest on N" labels; **By workload**, a colour matrix of ratios
   or values; **One workload**, bars with whiskers on a log or linear scale; **Head to head**, every workload
   for a pair; **How to read this**, the method as the data supplies it. State lives in the address hash so a view
   can be linked.
3. **The words** are the data's: `vocab` names the two axes (implementation/workload, engine/schema) and the
   failure, so the page never says "schema" about a regex workload.
4. **The markdown record** stays: the same numbers as a table in `docs/benchmarks/<name>.md`, with the host, the
   commit, the probe readings and the rows above 1.0 explained, for readers of the repository and for the diff.

Conventions the page keeps (do not re-decide them): the default comparison is "fastest on each workload"; the
leader's label says "1.07×, fastest on 21" so a leader above 1× is explained; a lower memory figure is "lower",
never "faster"; every list follows the filters; the geometric mean is over the workloads both sides complete.


**Compliance beside each implementation.** An implementation may carry `compliance`: an object of counts
(`{"right": N, "wrong": N, "refused": N, "acceptedInvalid": N}`) from a conformance suite, or a string such as
`"not ECMA-262"` or `"the oracle"`. The page shows it as a badge beside the name in the leaderboard and the picker
(the counts in the tooltip); an implementation without the field shows nothing. Publish it whenever the benchmark
compares implementations of a standard, so a reader sees what a fast implementation is fast at.

**Facts come from the run.** `meta.facts`, `meta.date` and `meta.runUrl` are read from the harness's own record of
the run (the benchmark tool's report header, the run's end time, the CI run's URL), never typed in.

## Files

- `site/template.html` — the page: styles, markup and the chart code (hand-rolled SVG, no dependencies but the
  Google Fonts stylesheet). Change it here and copy it to the repositories that carry one.
- `site/generate_site.py` — substitutes the data into the template. Stdlib only.

Repositories that publish a page keep a copy of both under `tools/benchmarks/results-page/` with an adapter that
writes the data file from their runner's output (Corvus.Regex.Ecma262: `tools/benchmarks/results-data.py` from
BenchmarkDotNet's CSV). Keep the copies identical to the skill's; a change is made here first.

## Adapting a run

- BenchmarkDotNet: the `*-report.csv` has `Method` (the engine), the parameter column (the workload), `Mean`,
  `Error`, `StdDev` and `Allocated`. The summary for a measurement is `{median: Mean, min: Mean - Error,
  max: Mean + Error}` in nanoseconds; `Allocated` becomes a measure with `"bytes": true`. Say so in `vocab`
  (`centre: "Mean"`, `spread: "Error"`, the whisker legend).
- A runner with repeated runs (the jsonschema-benchmark): the parts are the arrays of runs, the page takes the
  median and the whiskers are the fastest and slowest run.
- Name the implementations as the reader knows them (".NET generated", "Corvus generated UTF-8"), not by the
  method name in the code.

## Checking it

Open the page from a file URL or the site: every section renders with no console error, the picker lists every
implementation under its group, "Only those that complete every workload" keeps the ones with no `null`, the
address hash changes with the controls, and the page works at phone width (the matrix scrolls inside its box).
