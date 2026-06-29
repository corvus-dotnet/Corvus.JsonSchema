---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "The runtime and options"
---
## The runtime

The generated code imports a small runtime library for the parts TypeScript cannot express on its own — format checks, exact numeric comparison, and the byte-level edit engine. You supply it in one of two ways:

- **As a package** (used above): pass `--tsRuntimeModule @endjin/corvus-json-runtime` and `npm install @endjin/corvus-json-runtime`. This is the right choice when you generate many modules, because they all share one installed copy.
- **Self-contained**: omit the option, and the generator writes a `corvus-runtime.ts` next to each generated module. You then install its three dependencies yourself: `npm install lossless-json @js-temporal/polyfill tr46`.

Either way the runtime is ESM-only, so your `tsconfig.json` should target ES modules — for example `"module": "ESNext"` with `"moduleResolution": "Bundler"` or `"NodeNext"`.

## Code-generation options

| Option | Effect |
|--------|--------|
| `--tsRuntimeModule <specifier>` | import the runtime from this module specifier (such as the npm package) instead of emitting `corvus-runtime.ts` alongside the module |
| `--tsModulePerType` | emit one module per type plus a barrel `index.ts` (for tree-shaking and IDE navigation) instead of a single `generated.ts` |
| `--codeGenerationMode SchemaEvaluationOnly` | emit validators only — no interfaces, builders, or mutators |
| `--assertFormat false` | treat `format` as an annotation: recorded, but not enforced by `evaluate` |
