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
- Measured on a quiet 20-core host (load < 1), node v20.

## The headline

**1. Change-set updates (`patch`) — up to ~72× faster, near-zero allocation.**
The dominant real-world pattern ("update these fields") goes through the lean path: locate the
member spans by name and splice — no parse, no draft, no re-serialise of the rest.

| `patch {title}` | small | mid | large |
|---|---|---|---|
| vs native (ASCII) | 1.21× | 10.27× | **43.0×** |
| vs native (non-ASCII) | 1.45× | 14.06× | **71.9×** |

**2. Recipe edits (`produce`) of scalar/nested fields on large/non-ASCII docs — 1.6–2.5× vs native,
2–3× vs immer, and less GC.** Same immer-style `produce(doc, draft => …)` DX, but lazy byte reads:
unchanged fields are never parsed, unchanged bytes are copied through verbatim.

| large doc | vs native | vs immer |
|---|---|---|
| `produce` title (scalar), ASCII | 2.00× | 2.58× |
| `produce` title (scalar), non-ASCII | 2.51× | 3.06× |
| `produce` owner.name (nested), ASCII | 1.65× | 2.07× |
| `produce` owner.name (nested), non-ASCII | 2.44× | 2.97× |

GC pressure, 3000 large-doc round-trips, **scalar edit**: Model C **125** GC events / 75 ms vs
native 171 / 109 ms vs immer 268 / 164 ms — **~1.4× less than native, ~2.1× less than immer.**

**3. Editing one field of one element of a large array (`items[mid].field = v`) — parity-to-win, and
beats immer.** The draft is *element-lazy*: it locates only that element's byte span and splices the
one field — the rest of the array is never decoded.

| `items[mid].price` | mid | large |
|---|---|---|
| vs native (ASCII) | 0.68× | 0.81× |
| vs native (non-ASCII) | 0.98× | **1.25×** |
| vs immer (ASCII) | 1.05× | 1.05× |
| vs immer (non-ASCII) | 1.35× | **1.48×** |

GC pressure, large-doc array-element round-trip: Model C **199** events vs native 172 vs immer 274
(down from 967 before the element-lazy rewrite — ~5× less churn).

## The honest crossovers (where native/immer win today)

- **Array *structural* edits** (`items.push(…)`, `splice`): **0.4–0.55× vs native**. A length change
  materialises that array (decode + re-serialise); in this fixture `items` *is* the bulk of the doc,
  so that ≈ native's whole-doc round-trip. The fix is **pure-append** (append the new element's bytes
  before `]` instead of re-serialising) — not yet implemented.
- **`build` (construct from a full object)**: ~0.85× vs native `JSON.stringify` at large sizes —
  native's C++ serialiser is hard to beat; `build`'s value is canonical ordering, not raw speed.
- **Tiny documents**: native's C++ parser wins at ~0.3 KB for `produce`/round-trip (the draft setup
  dominates) — except the lean `patch` path, which still wins (1.2–1.45×).
- **Round-trip with validation** trails for ASCII (0.5–0.9×) and reaches parity/slight win only at
  large non-ASCII (1.05–1.09×), because validation is parse-based (Model B) and that shared full-parse
  dominates both sides; the produce write-side win is real but partly masked.

## What this motivates (next)

- **Pure-append** for `push`/tail-inserts: route a detected append through a byte insert before `]`
  (the existing `rmwArrayBytes` append) instead of materialise + whole-replace — turns the structural
  array rows from loss to win and preserves the original element bytes.
- **Byte-native (change-set-scoped) re-validation**: so the round-trip can skip the full inbound
  parse and surface the produce write-side win end-to-end.

**Net:** the change-set path is a dramatic, unqualified win; the immer-style recipe path wins for the
common "edit a few scalar/nested/array-element fields of a large document" case (the editor / config /
API-patch pattern) with less allocation and beating immer; array-structural inserts and full
reconstruction are the honest crossovers with a clear optimization path (pure-append).
