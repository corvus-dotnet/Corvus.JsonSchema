# ADR 0049. CodeMirror 6, vendored as a single bundle

Date: 2026-07-21. Status: **Accepted**. Scope: the code-editor technology in the web kit and designer. Builds on
[ADR 0041](0041-standards-only-zero-build-elements.md). This records why the kit's editing surfaces use
CodeMirror 6, vendored as one self-contained module, rather than a heavier editor or a hand-rolled one.

## Context

The designer needs a real code editor in a few places: the runtime-expression input (with highlighting and
schema-aware completions), the JSON view, and the side-by-side merge view behind the workflow diff. A `textarea`
does not give highlighting, completions, or a diff view; building those from scratch is a large, error-prone
effort. The constraint is the kit's zero-build promise ([ADR 0041](0041-standards-only-zero-build-elements.md)):
a consumer drops in plain ESM with no toolchain, so the editor must not push a package graph or a build step
onto them.

### Grounded architectural facts

- **CodeMirror 6 backs the editing surfaces.** `expression-input.js` (the runtime-expression editor) and
  `json-view.js` use it, and the merge view backs the workflow diff.
- **It is vendored as one self-contained ESM module.** `src/vendor/codemirror.mjs` is built by
  `npm run build:vendor` (esbuild), which bundles `@codemirror/{state,view,language,autocomplete,commands,merge,lang-json}`
  and `@lezer/highlight` into a single minified module. The build entry is `vendor/codemirror-entry.js`.
- **The consumer imports the pre-built bundle.** The kit ships the vendored module, so a consumer installs no
  CodeMirror packages and runs no bundler; the module is loaded like any other kit file.

## Options

**A hand-rolled editor.** No dependency, full control. Rejected: highlighting, completions, and a merge view
are substantial to build and maintain, and would be worse than a mature editor.

**Monaco.** The richest editor. Rejected: it is heavy and worker-based, which does not fit a zero-build,
drop-in-ESM kit, and it dwarfs the kit's footprint.

**CodeMirror 6, unbundled.** Rejected: importing the CodeMirror package graph directly would push a dozen
`@codemirror/*` dependencies and a bundler onto every consumer, breaking the zero-build promise.

**CodeMirror 6, vendored as a single bundle (chosen).** A modern, modular editor pre-bundled into one ESM
module, so the consumer gets the editor with no install and no build.

## Decision

The kit uses **CodeMirror 6, vendored as a single self-contained ESM bundle** (`src/vendor/codemirror.mjs`,
built by `npm run build:vendor`). The editing surfaces import the bundle; a consumer installs no CodeMirror
packages and runs no build step.

## Consequences

- A consumer gets a real editor (syntax highlighting, schema-aware completions, and the merge and diff view)
  with no toolchain, upholding the zero-build promise.
- Upgrading CodeMirror is a deliberate step: bump the dev dependencies and re-run `npm run build:vendor`; the
  vendored artifact is checked in, so it is reviewed as a build output.
- The editor is confined to the surfaces that need it (the expression input, the JSON view, the diff), not the
  whole kit, so a screen that needs no editor pays nothing for it.
- The vendored bundle carries CodeMirror's MIT licence banner, so the licence travels with the artifact.
