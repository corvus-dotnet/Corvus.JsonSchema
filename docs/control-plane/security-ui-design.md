# Security UI design — a coherent, goal-oriented surface for the access model

**Status:** draft for review (design-first; no code until the model + slices are agreed).
**Scope:** the operator-facing UI for the whole §13–§16.5 access model — administrators, credential
usage, row-security rules + claim→rule bindings, access requests, and a "who-can-do-what" overview —
unified around one mental model and one identity primitive. Builds on the §16.5.4 resolved-grantee
picker and the §15 admin = resolved-identity work already shipped.

---

## 1. The reframe: every access decision is **WHO can do WHAT, WHERE**

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
  *reach* plane and has **no UI at all** today — rules and bindings are CLI/API only.

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

App shell today (`demo/index.html`): top-level tabs **Runs**, **Catalog**, **Sources**, **Access**.

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

1. **Shared grantee chip + kind/label parity.** Extract one renderer; apply to admin panel + (next slices)
   credential usage + overview + bindings. *Web only.*
2. **Credential usage → picker + parity.** Web swap in `credential-dialog`/`credentials-table`; the wire
   already carries `{dimension,value}` — **but** full parity (display kind/label) needs the binding to *store*
   the grantee kind/label, so this slice includes the credential-binding model + summary projection + mock +
   the OpenAPI shape (ledger + benchmark per the §803 protocol, mirroring the admin `AdministratorGrant` work).
3. **Rule grammar extension** (engine + codegen, cross-cutting). Add **set membership** `in (…)` and the
   ordered **classification `<=`** handling to the `simple`-criterion grammar (§14.2 "still-open" note) — these
   touch `SimpleConditionEvaluator` (runtime) + `SimpleCriterionInliner` (codegen) **and** the per-backend
   store-predicate translation, since the grammar is shared with step criteria. Bigger than a UI slice; needs
   its own ground→tests→(codegen regen)→container-conformance pass. Classification `<=` also needs a **defined
   ordering** of classification labels (numeric levels or a configured order) — labels aren't naturally
   ordered. Land this **before** the Reach UI offers those templates.
4. **Reach / Rules UI.** New web surface over `/security/rules` (template-first incl. the new set-membership +
   classification templates, raw-expression advanced). Mostly web + mock; server/OpenAPI already expose the ops.
5. **Bindings UI.** New web surface over `/security/bindings` with the WHO→WHAT→WHERE editor: **direct** for
   group/role policy bindings (picker for well-known kinds + raw-claim fallback + the multi-IdP caveat banner,
   §6.5); per-person elevation routed to the request flow, not authored here. Web + mock; the server adds a
   **self-elevation guard** (defense in depth). Server/OpenAPI already expose the binding ops.
6. **Access overview** (server-side aggregation, per your call). New reach-scoped endpoint
   `GET /access/grants?grantee=…` that aggregates a grantee's bindings + administered workflows + credential
   usage server-side (client stays thin) → full ground→ownership-ledger→MemoryDiagnoser→container-conformance
   per the §803 protocol. Web surface = the Overview screen + inline revoke (delete the underlying binding/grant).
7. **Access-area reorg.** Fold Overview/Bindings/Reach/Requests under the Access tab nav; cross-link
   per-workflow Administrators from Catalog.

Recommended order: 1 → 2 → 3 → 4 → 5 → 6 → 7 (grantee primitive, then credentials, then the grammar the rules
need, then the two missing editors, then the server-aggregated overview, then the reorg). Each is
independently demoable.

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
   This requires the grammar/engine extension (slice 3) — a cross-cutting `SimpleConditionEvaluator` /
   `SimpleCriterionInliner` / per-backend-predicate change, plus a defined classification ordering.

**Residual sub-questions to settle as we build:**
- Classification ordering: numeric levels vs a deployment-configured label order (slice 3).
- `in (…)` semantics for absent/null labels and the per-backend store-predicate translation (slice 3).
- The exact claim a `person` grantee implies for the raw-claim fallback (deployment claim-map dependent).

---

## 9. References

- Design: `execution-host-design.md` §13 (credentials), §14 (authorization / row security / shell), §15
  (administration), §16.1 (two planes), §16.5 (entitlement lifecycle), §16.5.4 (grantee resolution),
  §16.5.5 (ambient dimensions).
- Shipped: `arazzo-admin-resolved-identity` (the pattern to mirror), `security-ui-correct-by-construction`
  (intent → resolved identity, never raw tuples).
- API: `/security/rules`, `/security/bindings`, `/credentials`, `/administrators`, `/identity/grantees`,
  `/accessRequests` (`arazzo-control-plane.openapi.json`).
