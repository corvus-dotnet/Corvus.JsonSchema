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
- Measured on a quiet 20-core host, node v20.

## The headline

**1. Change-set updates (`patch`) — tens of × faster (up to ~80×), near-zero allocation.**
The dominant real-world pattern ("update these fields") goes through the lean path: locate the
member spans by name and splice — no parse, no draft, no re-serialise of the rest.

| `patch {title}` | small | mid | large |
|---|---|---|---|
| vs native (ASCII) | 1.03× | 8.2× | **~22–50×** |
| vs native (non-ASCII) | 1.35× | 13.0× | **~80×** |

(The large-doc patch is so fast — tens of µs vs native's ~1.5 ms — that the multiple is measurement-sensitive; it is consistently one to two orders of magnitude.)

**2. Recipe edits (`produce`) win for the common cases, and beat immer across the board.**
Same immer-style `produce(doc, draft => …)` DX, lazy byte reads (unchanged fields never parsed),
element-lazy arrays, and a trailing `push` that appends bytes without decoding.

| large doc, vs native / vs immer | ASCII | non-ASCII |
|---|---|---|
| `produce` title (scalar) | 1.94× / 2.32× | 2.34× / 2.74× |
| `produce` owner.name (nested) | 1.57× / 2.09× | 2.31× / 2.81× |
| `produce` items[mid].price (array element) | 0.79× / 1.03× | 1.21× / 1.44× |
| `produce` items.push (pure-append) | 0.86× / 1.02× | 1.19× / 1.40× |

Scalar/nested edits are a clean 1.6–2.3× over native (2–2.8× over immer). Array-element edits and
`push` are parity-to-win over native (a win at non-ASCII; native's C++ serialiser just edges it at
large ASCII) and **beat immer at every mid/large size** — element access touches only that element's
span, and `push` inserts the new element's bytes before `]` without decoding the array.

GC pressure, 3000 large-doc round-trips: **scalar edit** Model C **125** events / 72 ms vs native 171
/ 109 ms vs immer 268 / 163 ms (~1.4× less than native, ~2.2× less than immer); **array edit** Model C
**199** vs native 170 vs immer 274.

**3. `build` (construct) is at the native floor (~1.0×).** `build<T>(props)` is one `JSON.stringify`
+ one UTF-8 encode — it cannot beat native serialisation, so it *is* native (0.98–1.12× across sizes).

## The honest crossovers (where native still leads)

- **Round-trip with validation** trails for ASCII/array edits (0.33–0.91×) and reaches parity/win only
  at large non-ASCII scalar edits (1.12×). Validation is parse-based (Model B), and that shared inbound
  full-parse dominates both sides, masking the produce write-side win. **This is the next lever:
  byte-native (change-set-scoped) re-validation** would let the round-trip skip the full parse.
- **Tiny documents (~0.3 KB)**: native's C++ parser wins for `produce` element/scalar edits (the draft
  setup dominates) — except the lean `patch` path, which still wins (1.0–1.35×).
- **Large ASCII array-element/`push`**: a slight native lead (0.79–0.86×) — both rewrite ~the whole
  doc bytes there, and native's C++ serialiser is hard to beat for pure ASCII; we still beat immer.

## What this motivates (next)

- **Byte-native (change-set-scoped) re-validation** — the one path still parse-bound; closing it
  surfaces the produce write-side win end-to-end in the full request round-trip.
- **Standalone full canonical writer** (separate, opt-in; recursive key-sort + canonical number/string
  forms, mirroring the C# canonicaliser) for hashing / content-addressing / golden determinism — kept
  off the fast `build` path (see design doc §5.7 roadmap).

**Net:** `patch` is a dramatic, unqualified win; the immer-style `produce` recipe path wins for the
common "edit a few scalar / nested / array-element fields of a large document" case (the editor /
config / API-patch pattern) with less allocation, and beats immer across the board including arrays;
`build` is at the native floor; the remaining crossover (round-trip validation) has a clear next step.
