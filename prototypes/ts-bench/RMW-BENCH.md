# Model C RMW — e2e benchmark (the elevator pitch, measured)

The generated TypeScript engine's mutation surface vs the two things a TS app does today:
**native** (`JSON.parse` → mutate the object → `JSON.stringify`) and **immer** (`parse` →
`produce(recipe)` → `stringify`). Bytes-in → bytes-out, the real wire/persistence shape.

- Harness: `rmw-e2e.bench.mjs` (run `node --expose-gc rmw-e2e.bench.mjs`); raw numbers in `RESULTS-rmw.txt`.
- Methodology: median-of-7 ns/op, auto-calibrated iterations, **correctness-gated** (every variant's
  output must be structurally equal to native before its timing counts). GC pressure = GC events +
  pause over 3000 large-doc round-trips. Drives the **shipping generated code** (`produceCatalog` /
  `patchCatalog` / `buildCatalog` / `evaluateRoot`), not a hand-written prototype.
- Fixture: a `Catalog` (nested `owner`, string `tags`, and an `items` array of records). Sizes
  small (~0.3 KB) / mid (~8 KB) / large (~201 KB); ASCII and non-ASCII (multibyte) content.
- Measured on a quiet 20-core host (load ~0.3), node v20.

## The headline

**1. Change-set updates (`patch`) — up to ~66× faster, near-zero allocation.**
The dominant real-world pattern ("update these fields") goes through the lean path: locate the
member spans by name and splice — no parse, no draft, no re-serialise of the rest.

| `patch {title}` | small | mid | large |
|---|---|---|---|
| vs native (ASCII) | 1.10× | 9.66× | **48.6×** |
| vs native (non-ASCII) | 1.18× | 11.07× | **65.9×** |

**2. Recipe edits (`produce`) of scalar/nested fields on large/non-ASCII docs — 1.7–2.5× vs native,
2–3× vs immer, and less GC.** Same immer-style `produce(doc, draft => …)` DX, but lazy byte reads:
unchanged fields are never parsed, unchanged bytes are copied through verbatim.

| large doc | vs native | vs immer |
|---|---|---|
| `produce` title (scalar), ASCII | 2.06× | 2.45× |
| `produce` title (scalar), non-ASCII | 2.41× | 2.96× |
| `produce` owner.name (nested), ASCII | 1.70× | 2.17× |
| `produce` owner.name (nested), non-ASCII | 2.51× | 3.07× |

GC pressure, 3000 large-doc round-trips, **scalar edit**: Model C **126** GC events / 78 ms vs
native 170 / 110 ms vs immer 268 / 166 ms — **~1.4× less than native, ~2.1× less than immer.**

## The honest crossovers (where native/immer win today)

- **Editing an element of a large array** (`items[mid].price = …`, `items.push(…)`): **0.3–0.5× vs
  native** and ~0.4–0.6× vs immer. The lazy draft decodes-on-touch, then `clone + diff`s that array;
  in this fixture `items` *is* the bulk of the doc, so that's ≈ a full decode + a clone + a diff
  (GC: 967 events vs native 170). This is exactly where immer's optimized array methods lead.
- **`build` (construct from a full object)**: ~0.85× vs native `JSON.stringify` at large sizes —
  native's C++ serialiser is hard to beat; `build`'s value is canonical ordering, not raw speed.
- **Tiny documents**: native's C++ parser wins at ~0.3 KB for `produce`/round-trip (the draft setup
  dominates) — except the lean `patch` path, which still wins (1.1–1.2×).
- **Round-trip with validation** is ~parity for scalar edits (0.6–1.0×, slight win at large
  non-ASCII) because validation is parse-based (Model B) and that shared full-parse dominates both
  sides; the produce write-side win is real but partly masked.

## What this motivates (next)

- **Lazy array descent + no-clone**: make array-element access lazy (locate the element span, edit
  in place) and drop the `clone + diff` for the whole array — turns the array-edit loss into a win
  and closes the immer gap on arrays. Pair with the **pure-append** optimization (route a detected
  tail-append through the byte append instead of re-serialising the array).
- **Byte-native (change-set-scoped) re-validation**: so the round-trip can skip the full inbound
  parse and surface the produce write-side win end-to-end.

**Net:** the change-set path is a dramatic, unqualified win; the immer-style recipe path wins for the
common "edit a few scalar/nested fields of a large document" case (the editor / config / API-patch
pattern) with less allocation; array-structural edits and full reconstruction are the honest
crossovers with a clear optimization path.
