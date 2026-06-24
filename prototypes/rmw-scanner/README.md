# Model C prototype — dual-offset RMW scanner + benchmark

Validates the **§13 / §13.6** Model C thesis from
[`../../docs/design/TypeScriptLanguageProvider.md`](../../docs/design/TypeScriptLanguageProvider.md):
a structural-sharing, byte-native, dual-offset (byte + UTF-16) RMW model for JSON.

Plain ES modules, zero dependencies (Node ≥ 18). Types are erased in real TS, so a `.mjs`
prototype measures the same V8 behaviour with no build step.

```bash
node --expose-gc bench.mjs
```

## Files
- `scanner.mjs` — the dual-offset UTF-8 scanner (`scanInto` → pooled `Int32Array` index table,
  byte + UTF-16 spans in one pass) for the editor path; the **optimised wire-path scanner**
  (`scanTargets`: byte-only, **early-stop** at the last edited field, `Uint8Array.indexOf`
  string-skipping); and the UTF-16 string scanner (`scanStringInto`, the jsonc-parser analog).
- `optbench.mjs` — current vs **optimised** Model C vs native (`node --expose-gc optbench.mjs`),
  with "early" and "late" (worst-case) edit placements. Optimised Model C wins the wire path at
  every size/content (2.4×–34× vs native, ~10–20× less GC). See design doc §13.9.
- `rmw.mjs` — the RMW variants (Model C byte splice, native parse/stringify, string-splice),
  the synthetic doc generator, and the change-set builder.
- `bench.mjs` — sustained-load harness: verifies every variant produces equivalent JSON, then
  times each over synchronous work only, capturing GC events (alloc-rate proxy) via an
  async-yielding loop so the `PerformanceObserver` actually fires.

## Variants
**Wire (bytes → bytes):** `C` Model C (scan + copy-through byte splice) · `N` native
(decode+parse+mutate+stringify+encode) · `S` string-splice (decode+UTF-16-scan+splice+encode).
**Editor (string → string):** `NS` native (parse+stringify, no transcode — native's best case) ·
`SS` string-splice (UTF-16 scan + substring splice, no transcode — the fair Model-C analog when
the doc is already an in-memory string) · `CS` Model C via encode→splice→decode.

## What it does and does not measure
- **Does:** full read→modify→write round-trip throughput, and GC-event rate per 1000 ops.
- **Does not:** (1) the **incremental-edit amortization** that is Model C's real editor value —
  scan once, apply many cheap edits, never re-parse/re-stringify — every op here is a full
  round-trip, which favours native's bulk C++; (2) **multi-tenant GC contention** — single-thread
  throughput under-states Model C's far-lower allocation advantage under concurrent load;
  (3) bytes/op (uses gc-events/1k-ops as a proxy at the default semi-space size). Numbers are one
  machine / one run — indicative, not definitive.

See design doc §13.7 for the analysed results and conclusions.
