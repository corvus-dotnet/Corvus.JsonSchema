# TypeScript Playground — Architecture

A Blazor WebAssembly playground for the **TypeScript** code-generation engine
(`TypeScriptLanguageProvider`). It is the TypeScript analogue of the C# playground in
[`docs/playground`](../playground/ARCHITECTURE.md), and shares its overall shape — a Blazor WASM app with
Monaco editors — but does something the C# one structurally cannot: it generates the code **and runs it**, in
the browser's own JavaScript engine, with no server.

## What it does

1. You edit a **JSON Schema**.
2. The schema is compiled to **TypeScript** — types + AOT validators + builders/mutators — by the real
   `TypeScriptLanguageProvider`, running in WASM. The same engine the CLI and the test suite use.
3. You write a short **TypeScript snippet** that imports the generated module and uses it.
4. **Run** transpiles your snippet + the generated module + the shared runtime into one bundle and executes
   it, capturing `console` output.
5. A **Type Map** tab shows the emitted type surface (kinds, members, composition, enums, consts).

The C# playground Roslyn-compiles and runs C# in WASM; the TypeScript playground transpiles to JavaScript and
runs it natively. That "see it run" loop is the differentiator.

## Pipeline

```
JSON Schema ─▶ CodeGenerationService (C#, in WASM)
                 • PrepopulatedDocumentResolver + embedded metaschema (no file I/O)
                 • register dialect vocabularies (2020-12 / 2019-09 / 7 / 6 / 4 / OpenApi30 / Corvus)
                 • JsonSchemaTypeBuilder.AddTypeDeclarationsAsync
                 • TypeScriptLanguageProvider.GenerateCodeUsing  ─▶ generated.ts + corvus-runtime.ts
                 • append evaluateRoot (RootEvaluatorExport)
                 • BuildTypeMap (walk the TypeDeclaration graph → the Type Map panel)
                            │
generated.ts ──────────────┘
   +  your .ts  ─▶ playgroundInterop.transpileAndRun (JS)
                 • esbuild-wasm bundles [your TS + generated TS + the pre-bundled runtime]
                   (a vfs plugin resolves "./generated.js" and the runtime import to in-memory sources)
                 • the self-contained iife bundle is executed via new Function; console output is captured
                            │
                       Console panel
```

## Projects & key files

- `src/Corvus.Text.Json.TypeScript.Playground/` — the Blazor WASM app (its own `.slnx`).
  - `Services/CodeGenerationService.cs` — the codegen pipeline (provider = `TypeScriptLanguageProvider`) +
    `BuildTypeMap`. Reads each type's assigned name from the `Ts_FinalName` metadata the provider writes.
  - `Services/Metaschema.cs` — loads the embedded metaschema documents (WASM-safe, no file I/O).
  - `Services/SampleRegistry.cs` — the built-in examples (Person, Shape, TodoList, Measurement).
  - `Components/MainLayout.razor` — the four-pane UI (schema | generated/type-map, your TS | console),
    Generate/Run, the sample picker and the theme toggle.
  - `Components/TypeMapPanel.razor` — the type-surface tree.
  - `wwwroot/js/playground-interop.js` — theme helpers + the esbuild-wasm transpile-and-run bridge.

## Vendored browser assets (`regenerate-vendored-assets.ps1`)

- `wwwroot/corvus-runtime.js` — the shared `@endjin/corvus-json-runtime` (with `lossless-json`, the Temporal
  polyfill and `tr46` inlined) bundled into one self-contained ESM. The in-browser-transpiled module imports
  it as `/corvus-runtime.js`.
- `wwwroot/lib/esbuild/` — `esbuild-wasm` (the browser ESM wrapper + the `.wasm` binary), the in-browser
  transpiler/bundler.

Regenerate both after changing the runtime package or bumping esbuild-wasm.

## Two Blazor-interop constraints (important)

The transpile-and-run runs inside an `IJSRuntime.InvokeAsync` call, and that await does not pump every async
primitive:

- esbuild-wasm is initialised with **`worker: false`** — a Web Worker's `postMessage` callbacks aren't pumped
  while the interop call is awaiting, so a worker-backed build would hang.
- the bundle is emitted as an **`iife`** and run via **`new Function`**, not `await import(blobURL)` — a
  dynamic import of a Blob hangs for the same reason.

## Running locally

```powershell
# (only after changing the runtime or esbuild-wasm)
pwsh docs/playground-typescript/regenerate-vendored-assets.ps1

$env:ASPNETCORE_URLS = "http://127.0.0.1:5282"
dotnet run --project docs/playground-typescript/src/Corvus.Text.Json.TypeScript.Playground/Corvus.Text.Json.TypeScript.Playground.csproj
```

## Verification

Browser end-to-end tests run under Playwright (chromium): boot the served app, Generate, Run, switch samples,
toggle theme, open the Type Map, and assert on the captured console + the rendered map.

## Features

Full parity with the C# playground (minus the C#-vs-TypeScript specifics — Roslyn compile vs esbuild transpile,
and the user-code editor language):

- **Multiple schema files** as tabs (add / rename / remove, each marked GENERATED or REFerenced) for cross-file
  `$ref`; the codegen registers each by `schema://playground/<name>` so relative refs resolve.
- Schema → generated TypeScript (+ a **Type Map** tab) → write a snippet with **IntelliSense** on the generated
  types → **Run** it in the browser.
- The documented example recipes (`docs/typescript/examples`, embedded) as **samples** — all 19, each a
  schema + a runnable demo exercising build / evaluate / patch / produce / match / format factories etc.;
  **New** project, auto/light/dark **theming**, **Share** (URL state) and **Save/Open** the whole project
  (all schema files + your TypeScript) as a ZIP.
- Published into the documentation website build (`docs/website/build.ps1`, Step 9j) at `/playground-typescript/`.
