// Build entry for the vendored CodeMirror 6 bundle (src/vendor/codemirror.mjs).
//
// Why vendored: the kit is zero-build ESM at consumption time, and CM6 requires every consumer to
// share ONE instance of each core package — CDN per-package bundles (jsDelivr +esm) are built and
// cached independently, so their internal exact-version pins drift apart (observed: autocomplete
// pinning state@6.6.0 while view/language/commands pin 6.7.0), which breaks CM6's instanceof-based
// extension checks at runtime. A single self-contained bundle is the only dependable shape.
//
// Rebuild with: npm run build:vendor  (dev-time only; the artifact ships in src/ and is imported
// lazily by <arazzo-expression-input> — nothing loads it until an editor mounts.)

export * as state from '@codemirror/state';
export * as view from '@codemirror/view';
export * as language from '@codemirror/language';
export * as autocomplete from '@codemirror/autocomplete';
export * as commands from '@codemirror/commands';
export * as lezerHighlight from '@lezer/highlight';
