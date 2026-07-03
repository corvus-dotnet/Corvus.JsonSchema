# Security UI design — a coherent, goal-oriented surface for the access model

**Status:** substantially built — this began design-first, but most slices have since shipped. See
**§0 Current state** for the live, code-verified per-slice status; keep that section current as work lands.
**Scope:** the operator-facing UI for the whole §13–§16.5 access model — administrators, credential
usage, row-security rules + claim→rule bindings, access requests, and a "who-can-do-what" overview —
unified around one mental model and one identity primitive. Builds on the §16.5.4 resolved-grantee
picker and the §15 admin = resolved-identity work already shipped.

---

## 0. Current state (living — last verified 2026-07-03 against branch `worktree-arazzo-workflow-engine-plan`)

This section is the ground truth; keep it current as slices land (memory: *keep-control-plane-living-docs-current*).
The rest of this doc is the original design narrative and lags the code in places — this section reconciles it.

**§7 build slices**

- **Slice 1 — Shared grantee chip + kind/label parity — DONE.**
- **Slice 2 — Credential usage → picker + parity (kind/label stored on the binding) — DONE.**
- **Slice 3 — Rule grammar (`in (…)` set-membership, ordered `classification <=`) — DONE functionally, via a
  different architecture than §7.3/§8.5 propose.** Implemented as a **standalone `SecurityRule` engine** in the
  durability layer (its own parser/AST + bytes-to-bytes evaluator + per-backend SQL emitter + a `/security/orderings`
  config resource), **not** by extending the shared `SimpleConditionEvaluator` / `SimpleCriterionInliner`
  step-criterion grammar — those (and the codegen inliner) were deliberately left untouched, so security rules are
  runtime-compiled, never codegen-inlined. Ordering is real and threaded (configured `SecurityLabelOrderings` →
  store predicate; fail-closed on unranked labels). Per-backend pushdown: SQL + Cosmos native; InMemory/Mongo/
  Redis/Nats evaluate in memory (pre-existing pattern). Unit + container-conformance pushdown-oracle + end-to-end tests.
- **Slice 4 — Reach/Rules UI (`scopes-panel`) — DONE.** Template-first builder including the set-membership and
  classification-ordering templates (the latter gated on `/security/orderings`), CRUD, reach-scoped, 13 tests.
- **Slice 5 — Bindings UI (`grants-panel`) — DONE**, including the server-side **self-elevation guard** (create +
  update) with a dedicated test; person elevation routed to the request flow.
- **Slice 6 — Access overview (`GET /access/grants?grantee=…` server aggregation + Overview screen) — SERVER SIDE DONE;
  UI + mock remaining.** The OpenAPI path + schemas (`AccessGrantsOverview` = grantee echo + matching bindings +
  administered workflows + usable credentials), regen (server + CLI), and the bytes-native handler
  (`HandleGetAccessGrantsAsync`: opaque base64url grantee token → owned `ResolvedGrantee` doc echoed whole; bindings
  scanned + claim-matched **string-free** via `ValueEquals`/`GetUtf8String().Span`; `ListAdministeredWorkflowsAsync`
  reverse lookup; credentials scanned + `IsUsableBy`; closure-free context-threaded `From()` projection with
  transfer-matched-pages ownership) are **built + green + tested** (2 server tests: aggregation + malformed-token 400).
  §803 proof landed: the fair projection benchmark shows the bytes-native projection at **256 B vs the naive 1376 B
  (0.19×)**; the full end-to-end handler is 4601 B (token decode + 3 store scans + serialize). **Remaining:** the
  `<arazzo-access-overview>` web component + its mock `/access/grants` + web client method + inline revoke, then the
  slice-7 slotting. The client encodes the token as base64url of a `ResolvedGrantee` JSON (from `GET /identity/grantees`).
- **Slice 7 — Access-area reorg — IN PROGRESS** (decision taken). Today an **Access** tab holds Requests and a
  **Permissions** tab holds Bindings + Reach (split codified in `smoke.spec.js`). Remaining: fold
  Overview | Bindings | Reach | Requests under a single Access sub-nav, move Bindings+Reach out of Permissions,
  slot in Overview (needs slice 6), cross-link Administrators from Catalog, retire the Permissions tab.

**Security-tags / management-tags thread** — settable + admin-editable-on-update `{key,value}` reach labels
(catalog `securityTags`; environment/source/credential `managementTags`). **DONE end-to-end** for all four
entities: OpenAPI / server handler / durability + store-conformance / mock; the web edit UI via the shared
`<arazzo-tag-editor>` (catalog + environment + credential); and the CLI `--manage`-on-update flag for
environment/source/credential (parity with catalog's `--security-tag`, with integration tests). **Only remaining
item:** the registered-**source** has no standalone UI edit panel — **parked** (the sources registry has no
dedicated panel by design, §7.6); `updateSource` exists server- and mock-side for when a panel lands.

**Actual app-shell tabs today** (`demo/index.html`): Runs · Runners · Catalog · Sources · Environments · Access ·
Permissions · Promotions · Runner auth. (The §3 snapshot below predates several of these.)

---

## 1. The reframe: every access decision is **WHO can do WHAT, WHERE**

> For the conceptual model on its own — capability (scopes) vs reach (grants + rules), why a grant's
> verbs are `read`/`write`/`purge` and not scopes, and how the two planes compose — see
> [`access-model.md`](./access-model.md).

The model is clean; the UI today is not. Each access decision has three axes:

| Axis | Meaning | Mechanism (design ref) |
|------|---------|------------------------|
| **WHO** | the principal/grantee | a resolved `sys:` **identity** (admin, credential usage) **or** a principal **claim** (a binding's `claimType`/`claimValue`) |
| **WHAT** | the capability / verb | a **scope** (`catalog:read`, `runs:write`, …, §14.1) and/or a **verb** (read / write / purge, §14.4) |
| **WHERE** | which rows | **reach**: a security **rule** over security tags, inside the deployment's mandated tenant shell (§14.2/§14.3) |

Two sub-mechanisms name "WHO", and that is the crux of today's inconsistency:

- **Identity grants** name a principal by their unforgeable `sys:` identity tags, matched against a row/run:
  - **administrators** (§15) — set-equality against the caller's stamped identity;
  - **credential usage** (§13) — label-**superset** (`IsUsableBy`: a run usable only if it carries *all* the
    binding's usage tags).
  Both are exactly what the **resolved-grantee picker** (§16.5.4) produces — name a real person/team/role/
  workflow, resolve to its `sys:` identity. Administrators already use it; credential usage still hand-types
  `{dimension,value}` tuples (the §16.5.4 guessing hazard).
- **Claim→rule bindings** (§14.2) name a principal by an inbound **claim** (`claimType`/`claimValue`, e.g.
  `team=payments`) and grant per-verb **reach** (a set of rule names, AND-ed, or `unrestricted`). This is the
  *reach* plane. *(Originally CLI/API-only with no UI — since delivered: the Rules UI (`scopes-panel`, slice 4)
  and the Bindings UI (`grants-panel`, slice 5) now exist under the Permissions tab. See §0.)*

So the same question — *"what can Alice do, and where?"* — is unanswerable in the UI, and "who" is authored
in three different mental models. **The overhaul makes WHO one consistent, correct-by-construction act, makes
WHERE visible and goal-oriented, and gives a single "who-can-do-what" overview.**

---

## 2. The model, precisely (what the UI must express)

The two planes (§16.1): **identity** (who you are — IdP/claims) vs **authorization** (what you may do). A
principal needs two authorization things, sourced differently (§16.5):

- **Capability** — a scope, usually from a role claim → ASP.NET policy (§14.1). Coarse, standing.
- **Reach** — which rows, via `sys:` tags → a security rule (§14.2). Coarse standing reach falls out of team
  membership; fine/elevated reach is granted by an access request → admin approval → an entitlement write.

The managed objects:

1. **Security rule** (`/security/rules`) — `{ name, expression, description }`. `expression` is the Arazzo
   `simple` grammar over security tags (e.g. `tenant == $claim.tenant`, `domain == 'payments'`). Compiles to
   an in-memory evaluator **and** an indexed per-backend store predicate. Seeded **bootstrap rules**:
   tenant-scoped, ABAC label-superset, intersection (§14.2). Rules are the reusable **WHERE** vocabulary.
2. **Claim→rule binding** (`/security/bindings`) — `{ claimType, claimValue, read, write, purge }` where each
   verb is a `VerbGrant` = `unrestricted` **or** a conjunction of `ruleNames`. "A principal with this claim
   gets, per verb, the reach of these rules." This is **WHO (claim) → WHAT (verb) → WHERE (rules)**.
3. **Credential usage grant** (§13, on a credential binding) — identity grants scoping which runs may **use**
   the binding. Superset-matched; the picker's resolved identity scopes precisely.
4. **Workflow administrators** (§15, per base workflow id) — resolved identities that govern a workflow
   (publish versions, approve requests). Done — digest-keyed, picker-driven, kind/label shown.
5. **Access request + approval** (§16.5) — the elevate flow: request → route to the §15 admin → approval
   writes the entitlement (a binding). Done (submit/queue/approve/deny/withdraw/revoke).

The **tenant shell** (§14.3) ANDs a mandated wrapper rule into every decision and stamps immutable,
client-invisible `sys:` internal tags. Users author within the shell; the UI must never invite editing
internal (`sys:`-prefixed) tags directly — which is exactly why the grantee picker (intent → resolved
identity) is the right primitive, not a raw tag editor (see memory: *security-ui-correct-by-construction*).

---

## 3. Current UI state + gaps (grounded)

App shell today (`demo/index.html`): top-level tabs **Runs · Runners · Catalog · Sources · Environments ·
Access · Permissions · Promotions · Runner auth** (see §0 for the live list). Rules + Bindings now live under
**Permissions**; the Access tab holds Requests — the §7 item 7 reorg to fold them under one Access area is in progress.
(The table below is the original "before" snapshot; §0 has the current per-surface status.)

| Surface | Today | Verdict |
|---------|-------|---------|
| Administrators (§15) | `administrators-panel` under Catalog detail; resolved-grantee picker; kind/label shown | ✅ the reference pattern |
| Grantee picker (§16.5.4) | `grantee-picker` → `{kind, value, identity[], label, complete}` | ✅ the universal WHO primitive |
| Access requests (§16.5) | `access-requests-panel` under the **Access** tab | ✅ exists |
| Credential usage (§13) | `credential-dialog` hand-rolled `{dimension,value}` rows; table shows `dim=value` | ⚠️ inconsistent WHO; no kind/label |
| Security rules (§14.2) | **none** (CLI/API only) | ❌ missing — the reach model is invisible |
| Claim→rule bindings (§14.2) | **none** (CLI/API only) | ❌ missing |
| "Who can do what" overview (§16.5 grants view) | **none** | ❌ missing |

The **Access** tab currently holds only requests. It becomes the **home of the whole access model**.

---

## 4. End-user goals (jobs-to-be-done) → model mapping

| # | Goal (operator's words) | Model action | Surface |
|---|--------------------------|--------------|---------|
| G1 | "Let the payments team read payments workflows" | binding: claim `team=payments` → read = rule `domain==payments` | Access bindings |
| G2 | "Let Alice run payments workflows" | access request → admin approve → entitlement binding (write reach, scoped to her) | Requests (✅) / direct grant |
| G3 | "Only nightly-reconcile runs may use this DB credential" | credential usage grant = resolved `workflow` grantee | Sources → credential |
| G4 | "Who administers nightly-reconcile? add/remove" | administrators (resolved identities) | Catalog detail (✅) |
| G5 | "What can Alice do, and where? revoke X" | read across bindings/admin/usage; delete a binding/grant | Access overview |
| G6 | "Define a reusable reach (e.g. the finance domain)" | create/edit a rule (template or expression) | Reach / Rules |
| G7 | "Approve/deny pending access requests" | decide a request | Requests (✅) |

The design's job: make G1, G5, G6 (today impossible in the UI) and G3 (today error-prone) **a few clicks**,
without making the operator learn the rule grammar for the common cases.

---

## 5. The universal primitives

### 5.1 The grantee = the universal WHO

One `grantee-picker` everywhere a principal is named (administrators ✅, credential usage, and — for
well-known kinds — the binding principal). One shared **grantee chip** renderer shows a resolved grantee
consistently wherever displayed:

```
[👤 person] Ada Lovelace   ·   sys:sub=u-1042
[👥 team]   Payments       ·   team=payments
[⚙ workflow] nightly-reconcile · workflow=nightly-reconcile
```

— kind icon + label foremost, the resolved identity grants secondary/dimmed, the opaque digest behind a
"copy id" affordance (never the headline). This is the **kind/label parity** applied uniformly (admin panel,
credential usage, access overview, requests).

> **Precision:** the picker produces a `sys:` **identity** (the WHO for identity grants: admin, usage). A
> *binding* keys on a **claim** — and the identity→claim mapping is lossy and issuer-sensitive across multiple
> IdPs. See **§6.5** for the full treatment (direct group bindings vs request-gated person elevation, and the
> picker→claim / `sys:iss` story). The binding form is explicitly **WHO → WHAT → WHERE**, not an identity grant.

### 5.2 Goal templates hide the grammar

Rules are authored from **templates** (the seeded bootstrap rules) for the common cases — *"rows in domain
___"*, *"rows matching the caller's tenant"*, *"rows sharing any label with the caller"* — with a single
**Advanced (expression)** escape hatch exposing the raw `simple` grammar for power users. The operator picks
a goal; the UI writes the rule.

---

## 6. Proposed information architecture — the **Access** tab

The Access tab becomes a small left-nav of the access model; per-workflow Administrators stay in catalog
detail (they are workflow-scoped) but are cross-linked from the overview.

```
Access
├─ Overview        "who can do what, where" — search a grantee → all their access; revoke inline
├─ Bindings        claim → (read/write/purge) reach — the WHO→WHAT→WHERE editor (G1, G2-direct)
├─ Reach (Rules)   the reusable WHERE vocabulary — template-first, expression-advanced (G6)
└─ Requests        the approval queue + my requests (✅ exists, G7)
   (Administrators are per-workflow, in Catalog → version detail — cross-linked here, G4)
```

### 6.1 Overview (G5) — the headline new screen

```
Access ▸ Overview
┌───────────────────────────────────────────────────────────────────────────┐
│  Find a grantee:  [ 👤 Ada Lovelace ▾ ]   (grantee-picker)                   │
├───────────────────────────────────────────────────────────────────────────┤
│  Ada Lovelace  (person · sys:sub=u-1042)                                     │
│                                                                             │
│  Reach (bindings)                                                            │
│   • read   domain==payments            via binding claim sub=u-1042   [Revoke]│
│   • write  domain==payments            via binding claim sub=u-1042   [Revoke]│
│                                                                             │
│  Administers                                                                 │
│   • nightly-reconcile                                                  [Open] │
│                                                                             │
│  Credential usage                                                           │
│   • db-main / prod   (run identity carries sys:sub=u-1042)            [Open] │
└───────────────────────────────────────────────────────────────────────────┘
```

Aggregates across bindings + administrators + credential usage for a chosen grantee, with inline revoke.
(See §8 for the open question on whether this needs a server aggregation endpoint or composes client-side.)

### 6.2 Bindings (G1) — WHO → WHAT → WHERE

```
Access ▸ Bindings ▸ New
┌───────────────────────────────────────────────────────────────────────────┐
│ WHO    grantee  [ 👥 Payments team ▾ ]   →  claim  team = payments           │
│ WHAT/WHERE                                                                   │
│   read   ( ) unrestricted   (•) rules: [domain==payments ✕] [+ add reach]    │
│   write  (•) unrestricted   ( ) rules: …                                     │
│   purge  ( ) unrestricted   (•) rules: (none — denied)                       │
│                                                          [Cancel]  [Create]  │
└───────────────────────────────────────────────────────────────────────────┘
```

"add reach" opens the Reach picker (existing rules) or "+ new reach" (template). The binding stores
`{claimType, claimValue, read, write, purge}` verbatim against `/security/bindings`.

### 6.3 Reach / Rules (G6) — template-first

```
Access ▸ Reach ▸ New
┌───────────────────────────────────────────────────────────────────────────┐
│ Goal:  (•) Rows in a domain        domain = [ payments ]                     │
│        ( ) Rows matching caller's tenant   (tenant == $claim.tenant)         │
│        ( ) Rows sharing any label with the caller                            │
│        ( ) Advanced — write an expression                                    │
│ Name:  [ reach-payments ]   (auto-suggested)                                 │
│ Preview:  domain == 'payments'                                               │
│                                                          [Cancel]  [Create]  │
└───────────────────────────────────────────────────────────────────────────┘
```

The template writes the `expression`; Advanced reveals the raw grammar with the live preview as validation.

### 6.4 Credential usage (G3) — picker + parity (within Sources)

The `credential-dialog`'s hand-rolled grant rows → the grantee picker: name the grantee that may use the
binding; its resolved identity becomes the usage tags (superset-matched = precise). The credentials table +
dialog show the grantee **chip** (kind/label), not `sys:sub=…`. (Full parity, per your call — so the
credential binding *stores* kind/label, mirroring `AdministratorGrant`.)

### 6.5 Binding authoring — direct vs request-gated, and the picker→claim / multi-IdP story

This is the safety-critical heart of the Bindings surface. Two kinds of binding, **two authoring paths**,
chosen for separation of duties + no-ambient-privilege (§16.5.3):

- **Standing group/policy bindings** — key on a *group/role* claim (e.g. `team=payments`) and grant typically
  *read* reach. They define what membership *confers*; a new team member inherits them automatically via
  their claim. This is **policy authoring**, not a per-person ambient grant, so an admin authors them
  **directly** in the Bindings editor (G1).
- **Per-principal elevation** — a binding granting *write/purge/run* reach scoped to a *specific person*
  (`sub`). The escalation-sensitive case. It stays **request→approve only** (the §16.5 flow): requester ≠ the
  approving §15 admin (separation of duties), the request carries intent+justification and the approval the
  approver+reason (the audit chain), and §16.5.3's "no ambient privilege" holds. The Bindings editor does
  **not** offer ad-hoc "grant person X write". **Self-elevation is rejected server-side** as defense in depth.

> Rule the UI encodes: **direct = group + read (policy); request-gated = person + elevated.**

**The picker→claim mapping, and multiple semi-trusted IdPs.** Subtle and safety-critical:

- An **identity grant** (admin, credential usage) binds on the **post-shell `sys:` identity** the picker
  resolves — which **includes `sys:iss`** (the cross-provider uniqueness key, see memory
  *arazzo-directory-adapters-identity-uniqueness*). So identity grants are **issuer-safe by construction**:
  `sub=u-1042` from IdP-A and IdP-B resolve to *different* identities and are never conflated. ✓
- A **claim→rule binding** keys on an **inbound claim** (`claimType`/`claimValue`) — **pre-shell**, before
  issuer resolution. A naive binding on `claimType=sub, claimValue=u-1042` matches that subject **from any
  issuer** → with multiple semi-trusted IdPs an **impersonation / identity-collision risk**.
- The picker→claim derivation is therefore **lossy**: `grantee.identity` (sys: tags) is the *output* of the
  deployment's claim→`sys:` map; recovering a single binding *claim* needs the *inverse*, which is not unique.
  Consequences the design adopts:
  - **Person grantees → the request flow**, not direct bindings: the server, holding the full resolved
    identity (iss+sub), writes the correct **issuer-qualified** entitlement. (Reinforces the split above.)
  - **Group/role bindings** authored directly key on a claim the deployment treats as **canonical / issuer-
    unique**; the UI **surfaces the caveat** ("a claim is only as trustworthy as the issuer asserting it; with
    multiple semi-trusted IdPs prefer issuer-qualified claims") and offers the **raw-claim fallback** for
    deployments whose claims already carry issuer namespacing.
  - **Model follow-up (flagged, out of v1):** first-class person-scoped *direct* bindings would need the
    binding key to carry the **issuer** (a `{claimType, claimValue, issuer?}` / composite claim). The request
    flow covers the v1 need; noted for the security API.

---

## 7. Build slices (incremental, each shippable; #803 discipline applies)

Each slice keeps the warning-free build + tests + (for any server/wire change) the per-row ownership ledger +
MemoryDiagnoser benchmark + container conformance gates. Web-only slices still get node/component/smoke tests.

1. **[DONE] Shared grantee chip + kind/label parity.** Extract one renderer; apply to admin panel + (next slices)
   credential usage + overview + bindings. *Web only.*
2. **[DONE] Credential usage → picker + parity.** Web swap in `credential-dialog`/`credentials-table`; the wire
   already carries `{dimension,value}` — **but** full parity (display kind/label) needs the binding to *store*
   the grantee kind/label, so this slice includes the credential-binding model + summary projection + mock +
   the OpenAPI shape (ledger + benchmark per the §803 protocol, mirroring the admin `AdministratorGrant` work).
3. **[DONE — built as a separate engine, not as framed below] Rule grammar extension.** Adds **set membership**
   `in (…)` and ordered **classification `<=`** (with absent-label semantics + a **defined ordering** of labels —
   a configured `SecurityLabelOrderings`, surfaced as the read-only `/security/orderings` resource). **As built,
   this is a standalone `SecurityRule` engine in the durability layer** (its own parser/AST + bytes-to-bytes
   evaluator + per-backend SQL emitter). The originally-planned route — extend the shared `SimpleConditionEvaluator`
   (runtime) + `SimpleCriterionInliner` (codegen) step-criterion grammar — was **not** taken; those were left
   untouched, so security rules are runtime-compiled, never codegen-inlined. Per-backend store-predicate pushdown
   (SQL + Cosmos native; InMemory/Mongo/Redis/Nats in-memory). Unit + container-conformance pushdown-oracle +
   end-to-end tests. Landed before the Reach UI shipped those templates.
4. **[DONE] Reach / Rules UI.** New web surface over `/security/rules` (template-first incl. the new set-membership +
   classification templates, raw-expression advanced). Mostly web + mock; server/OpenAPI already expose the ops.
5. **[DONE] Bindings UI.** New web surface over `/security/bindings` with the WHO→WHAT→WHERE editor: **direct** for
   group/role policy bindings (picker for well-known kinds + raw-claim fallback + the multi-IdP caveat banner,
   §6.5); per-person elevation routed to the request flow, not authored here. Web + mock; the server adds a
   **self-elevation guard** (defense in depth, built + tested — create and update). Server/OpenAPI already expose the binding ops.
6. **[SERVER SIDE DONE — UI + mock remaining] Access overview** (server-side aggregation, per your call). The reach-scoped endpoint
   `GET /access/grants?grantee=…` aggregates a grantee's bindings + administered workflows + credential
   usage server-side (client stays thin). Handler + 2 tests built + green; §803 ledger + MemoryDiagnoser proof landed
   (bytes-native projection 256 B vs naive 1376 B; end-to-end 4601 B). Container-conformance and the Web surface (the
   Overview screen + inline revoke — delete the underlying binding/grant) remain.
7. **[IN PROGRESS] Access-area reorg.** Fold Overview/Bindings/Reach/Requests under the Access tab nav; cross-link
   per-workflow Administrators from Catalog. *Started (decision taken): today an **Access** tab holds Requests and a
   **Permissions** tab holds Bindings + Reach (split codified in `smoke.spec.js`). Remaining: one Access sub-nav
   over Overview | Bindings | Reach | Requests, move Bindings+Reach out of Permissions, slot in Overview (needs
   slice 6), cross-link Administrators, retire the Permissions tab.*

Recommended order: 1 → 2 → 3 → 4 → 5 → 6 → 7 (grantee primitive, then credentials, then the grammar the rules
need, then the two missing editors, then the server-aggregated overview, then the reorg). Each is
independently demoable. **Status (2026-07-02): 1–5 DONE, 6 not started, 7 in progress — see §0.** The recommended
order also predates a parallel thread — the **security-tags / management-tags** work (settable + editable reach
labels on catalog/environment/source/credential) — which is now **done end-to-end** (API/store/mock/web/CLI),
bar the parked registered-source UI edit panel (§0).

---

## 8. Decisions (resolved with the reviewer)

1. **Direct grant vs request-only** → **split by binding type** (§6.5): standing **group/policy** bindings
   (group/role claim, typically read) are **directly** admin-authored; **per-person elevation** (write/run,
   `sub`-scoped) stays **request→approve only**, with a server **self-elevation guard**. Rationale: separation
   of duties, the audit chain, and §16.5.3 no-ambient-privilege.
2. **Overview aggregation** → **server-side** endpoint `GET /access/grants?grantee=…` (reach-scoped); keep the
   client thin (slice 6, full §803 ledger/benchmark/conformance).
3. **Binding principal via picker** → **picker + raw-claim fallback** (§6.5). The picker→claim mapping is
   lossy/issuer-sensitive: identity grants are issuer-safe (post-shell `sys:` incl. `sys:iss`); claim bindings
   are pre-shell, so person grantees route to the request flow (server writes the issuer-qualified
   entitlement), group/role bindings key on an issuer-canonical claim with a multi-IdP caveat banner, and
   raw-claim entry covers issuer-namespaced deployments. First-class person-scoped *direct* bindings (issuer in
   the binding key) are a flagged model follow-up, out of v1.
4. **Credential usage parity** → **full parity**: store kind/label on the credential binding, mirroring
   `AdministratorGrant` (slice 2).
5. **Rule templates** → **add classification `<=` and `in (…)` set membership** (common tag-based constructs).
   *Built (slice 3) — but as a standalone `SecurityRule` engine in the durability layer with per-backend predicate
   pushdown, NOT by changing `SimpleConditionEvaluator` / `SimpleCriterionInliner` (those were left untouched).
   Classification ordering is a configured `SecurityLabelOrderings` surfaced as `/security/orderings`. See §0 / §7.3.*

**Residual sub-questions:**
- ~~Classification ordering: numeric levels vs a deployment-configured label order~~ → **resolved**: a
  deployment-configured ascending label order (`SecurityLabelOrderings` / `/security/orderings`), fail-closed on
  unranked labels.
- ~~`in (…)` semantics for absent/null labels + per-backend store-predicate translation~~ → **resolved**: absent
  dimension → empty set → `false`; per-backend pushdown implemented (SQL + Cosmos native; InMemory/Mongo/Redis/Nats
  in-memory).
- The exact claim a `person` grantee implies for the raw-claim fallback (deployment claim-map dependent). *(still open)*

---

## 9. References

- Design: `execution-host-design.md` §13 (credentials), §14 (authorization / row security / shell), §15
  (administration), §16.1 (two planes), §16.5 (entitlement lifecycle), §16.5.4 (grantee resolution),
  §16.5.5 (ambient dimensions).
- Shipped: `arazzo-admin-resolved-identity` (the pattern to mirror), `security-ui-correct-by-construction`
  (intent → resolved identity, never raw tuples).
- API: `/security/rules`, `/security/bindings`, `/credentials`, `/administrators`, `/identity/grantees`,
  `/accessRequests` (`arazzo-control-plane.openapi.json`).
