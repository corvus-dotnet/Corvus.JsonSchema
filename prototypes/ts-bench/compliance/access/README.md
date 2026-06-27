# Generated-accessor "access" suites

End-to-end tests for the **generated API surface** of the corvus-ts TypeScript engine —
the code the generator emits for each type: the typed interface, `produce` / byte-RMW
(`patch*`, `produce*`), `build*`, union `match*` + per-branch guards, format brands +
validating factories, and the results collector. This is **distinct from validation
compliance** (`../`, driven off the JSON-Schema-Test-Suite): these suites exercise *how you
consume a value through the emitted types*, not whether validation is correct.

They were relocated here from the (now removable) feasibility spike
`prototypes/ts-provider-spike/` so they run in CI.

## Layout

- `schemas/` — the 9 feature schemas (one per suite).
- `suites/` — the 9 `*.test.ts` suites, copied verbatim from the spike. Each prints its own
  `... N passed, M failed` tally.
- `run-access.mjs` — the runner: drives the codegen worker, esbuild-transpiles each module +
  test, runs it, and gates on the tally.
- `run-access.sh` — builds the worker, then invokes the runner from `prototypes/ts-bench`.

| suite (`suites/`)          | schema (`schemas/`)     | what it covers                                  |
| -------------------------- | ----------------------- | ----------------------------------------------- |
| `arrays-access.test.ts`    | `arrays.json`           | array / tuple / prefix+tail / tensor / map      |
| `formats-access.test.ts`   | `formats.json`          | format brands + validating factory, int64→bigint |
| `mutation-access.test.ts`  | `mutation.json`         | generic `produce` + `recordChanges` (JSON Patch) |
| `produce-access.test.ts`   | `profile.json`          | per-type `produce*` immer-style draft            |
| `rmw-access.test.ts`       | `rmw.json`              | byte-level Model C read-modify-write (`patch*`)  |
| `shapes-access.test.ts`    | `shapes.json`           | reshaped typed array-ops on `patch*`             |
| `union-access.test.ts`     | `union.json`            | union alias, per-branch guards, `match*`         |
| `collector.test.ts`        | `person-collector.json` | detailed/verbose results collector + `toOutput`  |
| `provider-access.test.ts`  | `person.json`           | the emitted typed interface + enum literal-union |

## Codegen

All 9 suites use the **base** `TypeScriptLanguageProvider.CreateDefault()` codegen — the
same default the spike's `Program.cs` used. None of them needs the `TsFormatExtensionHandler`
extension seam (no suite imports a `generated-ext` module), so no dedicated generator is
required: the runner reuses the existing `../../bowtie-worker` (public codegen libs only,
empty `Directory.Build.props`), driving it over its NDJSON `{ schema, out }` protocol to
emit `generated.ts` + `corvus-runtime.ts` per schema.

## Run

```bash
./run-access.sh
```

The runner **must** execute with cwd `prototypes/ts-bench` (the script handles this) so the
generated runtime's bare imports (`lossless-json`, `@js-temporal/polyfill`, `tr46`) and
`esbuild` resolve via `ts-bench/node_modules`. For each suite it:

1. codegens the module via the worker;
2. esbuild-transpiles `generated.ts` + `corvus-runtime.ts` into the relative directory name
   the test imports from (e.g. `./out-arrays/`), and transpiles the `.test.ts` alongside —
   exactly the `transformSync(..., { loader: 'ts', format: 'esm' })` strip-only transpile
   `../../bowtie-harness.mjs` uses (no per-case `tsc`);
3. runs the transpiled test in a child `node` and parses its `N passed, M failed` tally.

The script **exits non-zero if any suite reports a failure or error**. Note `collector.test.ts`
prints its tally *without* throwing, so the parsed `failed` count — not the exit code — is the
gate for that suite (the runner checks both).

Scratch output lands in `access-work/` (gitignored).

Override the worker binary with `CORVUS_WORKER=/path/to/corvus-ts-bowtie-worker`, or the
build configuration with `ACCESS_CONFIG=Debug`.
