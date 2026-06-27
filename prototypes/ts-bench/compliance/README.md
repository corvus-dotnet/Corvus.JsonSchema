# corvus-ts compliance harness

Self-contained, CI-runnable [JSON-Schema-Test-Suite](https://github.com/json-schema-org/JSON-Schema-Test-Suite)
compliance runner for the **corvus-ts** TypeScript JSON Schema engine (the AOT TypeScript code generator
in `src/Corvus.Text.Json.TypeScript.CodeGeneration`).

This is the full-suite compliance runner **relocated out of the feasibility spike**
(`prototypes/ts-provider-spike/SuiteHarness.cs` + `suite-runner.mjs`) into a standalone form so the spike can
be deleted. It depends only on the **public** codegen libraries — nothing in the spike, nothing in the
production CLI.

## What it does

1. **`ComplianceGenerator`** (.NET console tool) walks each dialect (`draft4`, `draft6`, `draft7`,
   `draft2019-09`, `draft2020-12`) under the suite's `tests/<dialect>/` — the top-level `*.json`,
   `optional/*.json`, and `optional/format/*.json`. For **each test-group** it runs the TypeScript code
   generator and emits one TS module (`g_<dialect>_<file>_<i>.ts`, exporting `const evaluateRoot`) plus a
   `manifest.json` recording `{ module, dialect, file, group, error, tests:[{ data, valid, desc }] }`, where
   `data` is the instance's raw JSON **source text** (Model C / source-preserving). Groups whose schema fails
   to generate record `error` + a `null` module. `optional/format/*`, `optional/content`, and
   `optional/format-assertion` use `CreateWithFormatAssertion`; everything else uses `CreateDefault` — exactly
   as the spike's `SuiteHarness`.

2. **`suite-runner.mjs`** transpiles every emitted `.ts` (the shared `corvus-runtime.ts` + every module) to
   `.js` with **esbuild `transformSync`** (`loader:'ts'`, `format:'esm'`) — fast type-stripping, no per-module
   `tsc` type-check — then imports each module's `evaluateRoot`, parses each instance once with the
   source-preserving `parseInstance` (numbers → `LosslessNumber`, objects → null-prototype), and tallies
   pass/fail per dialect. It prints a machine-readable summary line and exits non-zero on any NEW failure.

## Run it

```sh
./run-compliance.sh
```

That builds the generator (Release), generates the modules + manifest into `out-suite/`, and runs the Node
runner. Optional args:

```sh
./run-compliance.sh [testsBaseDir] [outDir]
#   testsBaseDir  default: <repo>/JSON-Schema-Test-Suite/tests
#   outDir        default: ./out-suite  (kept under prototypes/ts-bench, see "node_modules" below)
```

Final line, e.g.:

```
PASS=7848 FAIL=0 KNOWN_FAIL=1 ERRORED_GROUPS=0 EXCLUDED_CASES=0
```

`FAIL` counts only **new / unexpected** failures and is the CI gate (the runner `process.exit(1)`s when
`FAIL>0`). `KNOWN_FAIL` counts allow-listed, pre-existing engine gaps (see below); they are always printed,
never hidden.

## node_modules

The generated `corvus-runtime.ts` imports three packages by bare specifier (`lossless-json`,
`@js-temporal/polyfill`, `tr46`), and the runner itself needs `esbuild` + `lossless-json`. All five live in
`prototypes/ts-bench/node_modules`. The runner is invoked from `prototypes/ts-bench`, and the default out dir
(`compliance/out-suite`) sits under that tree, so every bare import resolves via Node's upward `node_modules`
walk. **If you pass a custom `outDir` outside `prototypes/ts-bench`, those imports will not resolve and every
module will fail to load** — keep the out dir under `prototypes/ts-bench`, or run `npm install` in the out
dir's tree.

## Known failures (allow-list)

The harness reproduces the spike's known-good baseline exactly: **7848/7849** cases pass. The single
remaining case is a documented engine limitation that the spike exhibits identically (verified by running the
spike's own runner against its own prebuilt output):

- `draft4 optional/zeroTerminatedFloats` — `1.0` is accepted as an `integer` (the engine does not distinguish
  `1.0` from `1` for `type: integer` in draft4).

It is allow-listed in `suite-runner.mjs` (`KNOWN_FAILURES`) so the CI gate stays green on the known-good
baseline while still catching any new regression. To treat the allow-list as empty (every failure trips the
gate), run with `COMPLIANCE_NO_ALLOWLIST=1`.

## Files

- `ComplianceGenerator.csproj` — the generator project (references the public `src-v4` codegen libs + the
  `src` TypeScript provider; `Content`-bundles the standard metaschemas from
  `src-v4/Corvus.Json.Validator/metaschema`). Non-shipping tooling (empty `Directory.Build.props`).
- `Program.cs` — `SuiteHarness.Run`, faithfully relocated from the spike.
- `FakeWebDocumentResolver.cs` — offline resolver for the suite's `localhost:1234` remotes (self-contained).
- `Directory.Build.props` — empty, to keep this out of the repo's strict StyleCop/warnings-as-errors props.
- `suite-runner.mjs` — esbuild-transpile + evaluate + tally.
- `run-compliance.sh` — build → generate → run.
