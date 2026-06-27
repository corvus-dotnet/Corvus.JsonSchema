# corvus-ts Bowtie harness

[`bowtie-harness.mjs`](bowtie-harness.mjs) is a [Bowtie](https://bowtie.report) test harness for **corvus-ts**
— the AOT TypeScript JSON Schema engine. It lets the engine run under the standard Bowtie tooling against the
official test suite, alongside the other implementations.

## How it works

corvus-ts is a code **generator**, not a runtime interpreter, so each schema is compiled on the fly. Per
Bowtie `run` command the harness:

1. writes the schema to a temp dir (source-preserving, via `lossless-json`);
2. runs the production CLI — `Corvus.Json.Cli … jsonschema <schema> --engine TypeScript --outputPath <dir>`;
3. `tsc`-compiles the emitted `generated.ts` + `corvus-runtime.ts`;
4. dynamic-imports the default `evaluateRoot`;
5. validates each instance — parsed with the source-preserving `parseInstance` (numbers → `LosslessNumber`
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
