# CLAUDE.md

Authoritative instructions live in [`.github/copilot-instructions.md`](.github/copilot-instructions.md)
(build/test commands, conventions, the docs code-sample catalog gate, codegen, benchmarks, the docs
website, the playgrounds) and the task skills in [`.github/skills/`](.github/skills/). Read the relevant
ones before working. Do not infer conventions from surrounding code. If anything here conflicts,
`copilot-instructions.md` wins.

## Pre-commit gates (every commit)

1. `dotnet build Corvus.Text.Json.slnx` must report `0 Warning(s)` (warnings are errors everywhere).
2. `dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop&TestCategory!=integration"`.
   Use `FullyQualifiedName~` filters; Microsoft Testing Platform rejects `--nologo` and `-v q`.
3. If any file under `.github/`, `docs/`, or a skill/instruction file changed, run
   `pwsh docs/update-code-sample-catalog.ps1 -UpdateFile <path>` for each changed file, then
   `-Check` (must exit 0). CI fails on a stale catalog.

## After changing a code generator

- Build `src/Corvus.Json.Cli` in Release, run `pwsh docs/ExampleRecipes/regenerate-examples.ps1 -Force`,
  then build and run the affected recipes to confirm their output.
- If the JSON Schema model generator changed, also regenerate the benchmark `C/` directories, never `B/`.
- Generator output is also checked in under `benchmarks/*/Generated` (regenerate it; unlike the test
  projects it is not rebuilt at compile time), and the AsyncAPI playground
  (`docs/playground-asyncapi/`, its own solution, outside the main slnx and every gate) compiles user
  code against generated output at runtime in the browser. Verify both after generator changes.
