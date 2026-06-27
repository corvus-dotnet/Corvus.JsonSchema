# corvus-ts Bowtie harness

[`bowtie-harness.mjs`](bowtie-harness.mjs) is a [Bowtie](https://bowtie.report) test harness for **corvus-ts**
— the AOT TypeScript JSON Schema engine. It lets the engine run under the standard Bowtie tooling against the
official test suite, alongside the other implementations.

## How it works

corvus-ts is a code **generator**, not a runtime interpreter, so each schema is compiled on the fly. Rather
than spawn the CLI per schema, the harness drives a **long-running in-process codegen worker**
(`Corvus.Json.Cli codegen-worker`) that uses the generator directly. Per Bowtie `run` command:

1. the worker generates the module in-process, registering Bowtie's per-case `registry` of remote schemas in
   the document resolver via `AddDocument` (the v5-harness approach — no HTTP/file remote resolution, **no
   server**), and writes `generated.ts` + `corvus-runtime.ts`;
2. the harness `tsc`-compiles them and dynamic-imports the default `evaluateRoot`;
3. it validates each instance — parsed with the source-preserving `parseInstance` (numbers → `LosslessNumber`
   for exact `multipleOf`/big-int, objects → null-prototype so `"__proto__"` is an ordinary own key).

Protocol: newline-delimited JSON on stdin → NDJSON responses on stdout (`start` / `dialect` / `run` / `stop`).

## Run

```
node bowtie-harness.mjs                       # then feed it Bowtie commands on stdin
node bowtie-harness.mjs < bowtie-selftest.ndjson | grep '"seq"'   # self-test (3 cases incl. $ref+multipleOf:0.1)
```

The self-test must print, for each `seq`, the expected validity list (e.g. `seq 1` → `[true,false,false]`,
`seq 3` → `[true,false]` for `0.3`/`0.31` against `multipleOf: 0.1`).

## Notes / follow-ups

- Lives in `ts-bench` to reuse its `node_modules` (the generated-code runtime deps + `tsc`) — a standalone
  npm package + Docker image (for registering with Bowtie proper) is a follow-up.
- Per-schema codegen + `tsc` is the cost of an AOT engine under Bowtie; correctness is unaffected, throughput
  is bounded by the .NET CLI start + compile per schema.
- Compliance itself is already proven by the in-repo suite-runner (7848/7849 over 5 dialects); this harness is
  about standardized Bowtie tooling integration.

## Running under real Bowtie (Podman)

corvus-ts is a code generator, so the harness is packaged as an OCI image bundling a self-contained linux-x64
CLI + Node + tsc; per Bowtie `run` it codegens, compiles, and runs. Build it and run the official suite with
the real Bowtie tooling:

```
./bowtie-image/build-image.sh                          # publish CLI + build localhost/corvus-ts (podman.exe)
podman.exe run -i --rm localhost/corvus-ts < bowtie-selftest.ndjson | grep seq   # in-container smoke
bowtie.exe smoke -i image:localhost/corvus-ts          # success: true across all 5 dialects
./run-bowtie-suite.sh draft2020-12 draft2019-09 draft7 draft6 draft4             # official suite, tallied
```

Verified with the Windows `bowtie.exe` (2026.4.16) + `podman.exe` (5.8.2), reachable from WSL (`podman.exe`
builds from a `\\wsl.localhost\...` path). `bowtie summary` has an id-keying bug for a `localhost/` image
(keys results by `image:localhost/corvus-ts` but the header by `localhost/corvus-ts`), so
`run-bowtie-suite.sh` tallies the raw report (`expected` vs `results`) directly.

### Limitations

- **Registry supported** — the `codegen-worker` registers Bowtie's per-case remote-schema registry in the
  document resolver via `AddDocument` (no server), so `bowtie smoke` no longer reports `"registry": false` and
  `refRemote` passes (31/31 on draft2020-12).
- Per-schema codegen happens in the long-running worker (no per-schema process); `tsc` inside the container is
  the remaining throughput cost of an AOT engine under Bowtie.
