# Terminology normalization pass

A one-time docs/UI-copy pass applying the canonical vocabulary in
[`UBIQUITOUSLANGUAGE.md`](./UBIQUITOUSLANGUAGE.md) (new in this change set — **commit it as part of
this pass** and maintain it from then on: update it in the same change that introduces or refines a
term; it wins conflicts). The items below come from the 2026-07 sweep recorded in that document's
"Normalization decisions" table (A–H). Tick each item as it lands.

**Rules of engagement**

- Docs and user-facing UI copy only. Do **not** rename API operationIds, wire fields, store
  schemas, or C# symbols (the one sanctioned exception is the element-tag alias in item 1).
- No behavioural changes in this pass.
- Gates per commit as usual: warning-free build where code is touched; affected component tests +
  demo smoke for UI-copy changes; and for every docs file changed, run
  `pwsh docs/update-code-sample-catalog.ps1 -UpdateFile <path>` then `-Check` (must exit 0).
- If you disagree with a resolution, stop and flag it rather than silently diverging.
- One commit per item is fine; small batches are fine too.

## The items

- [x] **1. "Scopes" panel → "Rules"** (decision A). The `<arazzo-scopes-panel>` edits security
  *rules*; "scope" canonically means a capability scope (`catalog:read`, …). Rename user-facing
  copy: the panel heading, the demo tab label, and the wording in `ui-design.md`,
  `security-ui-design.md`, `ux-review.md` (the "Scopes = security rules" gloss becomes just
  "Rules"). Element tag: register `<arazzo-rules-panel>` as the primary tag and keep
  `<arazzo-scopes-panel>` as a deprecated alias (`define()` both against the same class) so kit
  consumers don't break. Use "scoped reach" for the per-verb grant value formerly written "Scoped".

- [x] **2. Grant object naming** (decision B). Canonical term in docs/UI: **grant binding**. Where
  the API/wire name appears (`listSecurityBindings`, …), keep it but introduce it once as "the API
  calls it a *security binding*". Sweep `access-model.md`, `ui-design.md`, `ux-review.md` for stray
  "grant" / "claim→rule binding" variants.

- [ ] **3. "Sources" tab → "Connections"** (decision C). The demo tab named "Sources" manages
  source *credentials* (the rotation worklist), not the `/sources` registry. Rename the tab in the
  demo and its references in `ui-design.md` / `ux-review.md`. Elsewhere use the three precise
  terms: *source description* (in-document), *source document* (the file), *registered source*
  (a `/sources` entry) — never bare "source" where ambiguous.

- [ ] **4. Allocation-campaign "draft" jargon** (decision D). "Draft" is now a lifecycle term (a
  catalog version not yet available in any environment). In `allocation-protocol.md` /
  `allocation-matrix.md` prose, qualify the pooled write-leaf document as **write draft** going
  forward; historical matrix rows need not be mass-edited. Leave code symbols (e.g.
  `ValidateDraft`) alone unless touched anyway.

- [ ] **5. "publish/hosting service" → "hosting service"** (decision E). In `catalog-design.md`'s
  future-phase section: *publish* is reserved for minting a catalog version from a working copy;
  the endpoint-serving service is the *hosting service*.

- [ ] **6. Qualify bare "tags"** (decision F). Always **user tags** (free-form metadata) /
  **security tags** (versions, runs) / **management tags** (environments, sources, credentials);
  **reach labels** is the umbrella. Fix the ambiguous spots in `execution-host-design.md` §14.2,
  `ui-design.md`, `ux-review.md` — prioritize any sentence where a reader could mistake which kind
  is meant.

- [ ] **7. "Reach scopes" → "reach rules"** (decision G). The Access-area label in `ux-review.md`
  re-fuses the two planes; fix it.

- [ ] **8. Readiness is a hard gate** (decision H). Fix the `ux-review.md` spot (~line 278) that
  calls readiness "a readiness check, not a hard gate": views may *display* readiness, but
  promotion and the add-workflow wizard never bypass it.

## When complete

All boxes ticked, gates green, `UBIQUITOUSLANGUAGE.md` committed and its ⚠ markers in the
"Normalization decisions" table cleared (the renames are then done). Note any term refinements you
made in your commit messages — the `arazzo-workflow-designer` child branch re-syncs onto this
branch afterwards and needs to pick them up.
