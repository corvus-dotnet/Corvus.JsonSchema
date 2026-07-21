# CLAUDE.md

This repository's agent guidance is written in GitHub Copilot's format. **Read it before
doing any work — do not infer conventions from the code.** The notes below are only a
pointer; the linked files are the source of truth.

## Start here (required reading)

1. **[`.github/copilot-instructions.md`](.github/copilot-instructions.md)** — the authoritative
   project instructions: build/test commands, coding conventions, the documentation
   code-sample catalog gate, code generation, benchmarks, the docs website, and the
   playgrounds.
2. **[`.github/skills/`](.github/skills/)** — task-specific skills (e.g. `corvus-build-and-test`,
   `corvus-codegen`, `corvus-docs-website`, `corvus-test-suite-regeneration`). Open the
   relevant skill's `SKILL.md` for the area you're working in.

## Active campaign — control-plane allocation work (issue #803)

If you are working on the bytes-to-bytes allocation campaign (control-plane API → durable store
allocation reduction), the durable principle is
[ADR 0037, bytes-native seams](docs/arazzo/adr/0037-bytes-native-seams.md) (the campaign's binding
protocol and row-by-row matrix have been folded into it). Hard rules:

- **Clean slate** — prior commits/summaries/memory are *current code shape only*, never proof of
  completion. A row is done **only** when a measured before→after appears in the matrix's Part D.
- **Derive, don't anchor** — build each change from `.github/skills/` + the relevant spec under
  `docs/arazzo/guides/` (credentials §13/§13.4.1, access §14.2); the existing code is the corpus being replaced.
- **Per-row gates, in order** — ground → run baseline benchmark (paste the number) → post ownership
  ledger → **STOP for go-ahead** → change → re-run benchmark (paste before→after) → update the
  matrix → commit only when asked. One row at a time; no batching; stop and ask when unsure.

## Pre-commit gates (full detail in `.github/copilot-instructions.md`)

Run these before **every** commit:

1. **Warning-free build** — `dotnet build Corvus.Text.Json.slnx` must report `0 Warning(s)`
   (`TreatWarningsAsErrors=true` across all projects).
2. **Run affected tests** —
   `dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop&TestCategory!=integration"`.
   Use `FullyQualifiedName~` for filters; Microsoft Testing Platform does not accept
   `--nologo` or `-v q`.
3. **Code-sample catalog** — if any file under `.github/`, `docs/`, or a skill/instruction
   file changed, run `pwsh docs/update-code-sample-catalog.ps1 -UpdateFile <path>` for each
   changed file, then `pwsh docs/update-code-sample-catalog.ps1 -Check` (must exit 0). CI
   fails on a stale catalog.

## After changing a code generator

- Regenerate the example recipes: `pwsh docs/ExampleRecipes/regenerate-examples.ps1 -Force`
  (build `src/Corvus.Json.Cli` in Release first), then build **and run** the affected
  recipes to confirm their output.
- If the JSON Schema model generator changed, also regenerate the benchmark `C/`
  directories — never `B/`.

> If anything here conflicts with `.github/copilot-instructions.md`, that file wins.
