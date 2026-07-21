# Arazzo access, identity, and entitlement: design detail

This spec is the detailed design behind the control-plane access model: control-plane authorization and
tag-based row security (§14), workflow administration (§15), the identity, login, and entitlement lifecycle
(§16), and the security-review remediation (§17). It is the exhaustive counterpart to the conceptual
[access-model.md](access-model.md) summary and the access-model [ADRs](../arazzo/adr/README.md) (0001 to 0016).
It was split out of the execution-host design so the access-and-identity subsystem stands on its own.

The section numbering is retained from the original execution-host design (this spec begins at §14), so existing
cross-references to these sections stay valid.

## 14. Authorization — control plane and tag-based row security

Two layers: **operation** authorization (can this principal call this endpoint at all) and **row**
authorization (which workflows/runs can it see or act on). The control plane is ASP.NET Core; the mechanism
is standard and **per-deployment configurable**, with a concrete strategy shipped in the sample.

### 14.1 Operation authorization (capability scopes)

- The control plane ships **capability scopes as authorization policy names** — the full set (as built,
  `ControlPlaneAuthorization`) is `catalog:read`/`catalog:write`/`catalog:purge`, `runs:read`/`runs:write`/`runs:purge`,
  `security:read`/`security:write`, `credentials:read`/`credentials:write`, `administrators:read`/`administrators:write`
  (§9, §13, §15, §16.5) — and each endpoint declares its requirement. **Triggering a run uses `runs:write`** (there is no
  `workflows:run` scope); the access-request endpoints are gated by the per-workflow §15 administrator membership, not a
  global scope.
- The **deployment** supplies authentication (any ASP.NET Core scheme — JWT bearer / OIDC / mTLS) and the
  claim→policy mapping (`AddAuthentication().Add…` + `AddAuthorization`). The control plane does **not**
  hard-code an identity provider; it depends only on `ClaimsPrincipal` + the named policies. This is the
  "configurable per deployment" seam.
- The **sample** implements one concrete strategy (JWT bearer with a `scope` claim mapped to the policies,
  plus a dev API-key scheme) to demonstrate end to end.
- **Ambient identity dimensions (deriving a `sys:` tag from request context, not the IdP).** Because the control
  plane keys entirely on `ClaimsPrincipal` and treats *how* claims are acquired as the host's concern, a deployment
  may synthesize claims from the request itself — a vanity host, a route prefix, an API-gateway header — through an
  `IClaimsTransformation` / middleware, so a `sys:` dimension such as `sys:tenant` need **not** come from the token.
  This is sound for the *runtime caller*, but membership matching
  ([ADR 0003](../arazzo/adr/0003-membership-matching-over-canonical-identity.md)) imposes a consistency
  requirement at *grant-authoring* time that is easy to miss: a grant's named dimension only matches a caller
  whose stamped identity will contain it, so an ambient dimension has to be authored with the same value it is
  later stamped with. The full treatment — the seam, the trap, and how it is
  built — is **§16.5.5** (now implemented via the `IAmbientIdentityDimensions` provider).

### 14.2 Row-level security — security tags + rule engine

Row authorization decides **which** workflows (catalog versions) and runs a principal may see or act on. It is
**not** the free-form user `tags` (those stay as user-facing, AND-filtered metadata). It is a separate concept:

- **Security tags** are **key/value pairs** (labels) on a row — e.g. `tenant=acme`, `team=payments`,
  `classification=restricted`. They are set when the row is created (a run **inherits** its workflow version's
  security tags; a catalog version is labelled when added) and are distinct from user tags. The **non-internal**
  (unprefixed) tags on a **catalog version** are user-owned: supplied on the add request (`addCatalogVersion`'s
  `securityTags` part) and re-tagged later by a workflow administrator via the metadata patch
  (`CatalogMetadataPatch.securityTags`) — a governed, audited edit, not immutable. **Internal** (reserved-prefix)
  tags remain deployment-owned and never user-settable (see below). A run inherits its version's security tags at trigger.
- **Tag rules** are boolean expressions over those labels, written in (a reuse of) the **Arazzo `simple`
  criterion grammar** — the same `==`/`!=`/`<`/`<=`/`>`/`>=`, `&&`/`||`/`!`/grouping engine already inlined for
  step criteria (`SimpleConditionEvaluator` runtime + `SimpleCriterionInliner` codegen, over `Comparand`).
  Example: `tenant == 'acme' && (team == 'payments' || team == 'billing')`. Real-world access is richer than
  "this tag AND that tag", which is exactly why a small expression language — not a fixed KVP match — is used.
- **A principal's claims resolve to a well-defined rule** — their effective access predicate. Rules reference
  both **literals** and **claim values** (e.g. `tenant == $claim.tenant`), so one parameterised rule serves
  many principals; the principal's claims supply the parameter values at evaluation time.
- **Rules compile to emitted evaluators.** Because the grammar is the `simple` one, a rule is compiled the same
  way step criteria are — into (a) an efficient in-memory evaluator, and (b) a per-backend **store predicate**
  (the grammar maps cleanly to SQL/NoSQL boolean `WHERE`s over the security-tag storage). So row filtering is
  **pushed into the store as an indexed query** (never scan-then-filter, per §5.4), and a single-row access
  check (get-by-id, write/trigger) runs the in-memory evaluator → `403` when the rule is unsatisfied.
- **A separate security-focused API in the control plane** authors and manages the rules and the claim→rule
  mapping (its own capability scopes, e.g. `security:read`/`security:write`), kept apart from the run/catalog
  operational surface. Rules are versioned state; changing a rule re-emits its evaluator/predicate.
- **Bootstrap rules.** The system seeds a set of common, ready-to-use rules at initialization so the model is
  usable from the start — **tenant-scoped** (one designated key must match the principal's value, e.g.
  `tenant == $claim.tenant`), **ABAC label-superset** (the principal must satisfy every label the row carries),
  and **intersection** (the principal shares at least one label with the row). These are ordinary rules, not
  hard-coded behaviour: a deployment uses them as-is, edits them, or removes them via the security API.
- The layers compose: scopes (§14.1) gate the **operation**; the resolved tag rule gates the **rows**. A
  `runs:read` principal lists runs, but only those whose security tags satisfy its rule.

**Resolved during implementation:** **deny-by-default is now the posture** — a non-null `SecurityFilter` admits
a row only if it positively grants it: an empty rule set admits nothing ("no restriction" is a `null` reach /
`AccessContext.System`, never an empty filter), and an unclassified (untagged) row is admitted to no scoped
principal (only the full-reach `null` credential sees untagged rows). The store-predicate translation is done
per backend (~18 stores, container-verified) via the reference-then-fan-out pattern, with the
`ExistsAnyTag` guard enforcing the untagged-deny at the indexed-query level.

**Still open/assumed** (revise as the security API design firms up): the rule grammar may need an `in (...)` set
operator and richer null/absent-label handling beyond the step-criterion subset; and the persistence + claim→rule
mapping model for the security API itself (below).

### 14.3 Deployment access-control shell — mandated filters + internal tags

A deployment can **wrap** the row-security model so its own constraints are inescapable — e.g. mandate that
every principal is filtered to its own tenant/customer/organization. Users author their security tags and rules
*within* that shell; they cannot reach outside it. This is what keeps one shared-hosting tenant from leaking
into another even if a user rule is misconfigured.

- **Internal (deployment) security tags** are marked by a **reserved key prefix** (deployment-configurable,
  e.g. `sys:`). They are:
  - **immutable** — set by the deployment at row creation (e.g. the tenant resolved from the principal /
    hosting context), never editable through the user-facing API;
  - **invisible to clients** — stripped from catalog/run read responses so the isolation labels are not
    disclosed;
  - **reserved on input** — the API rejects any user attempt to create or edit a security tag (or reference a
    rule operand) whose key carries the internal prefix. End-users own the unprefixed keyspace only.
- **Mandated wrapper rule (defense in depth).** The effective access decision is the deployment's mandated
  wrapper rule **AND** the principal's resolved user rule — both must hold. A user rule can therefore only
  *narrow* within the shell, never widen past it. The wrapper references internal tags
  (e.g. `sys:tenant == $claim.tenant`) and is **ANDed into the store predicate** alongside the user rule, so
  tenant isolation is enforced on every query and single-row check, pushed down to the store.
- **Hooks:** security-tag key validation (reject the reserved prefix from user input); a deployment-configured
  access-control wrapper (the mandated rule + an internal-tag injector at row creation + a response stripper);
  the compiled predicate becomes `wrapperPredicate AND userPredicate`. The wrapper is per-deployment
  configuration, like the auth scheme (§14.1) — the sample demonstrates a tenant shell.

### 14.4 Control-plane enforcement (HTTP) — the AccessContext model

Enforcement is **secure by construction, not by remembering to pass a filter**. Every control-plane client
operation (`ISecuredWorkflowManagement` / `ISecuredWorkflowCatalog`) **requires** an `AccessContext` — there is
no contextless/unscoped read on those surfaces, so an unscoped read cannot exist to be misused. The truly
unscoped reads live one layer down on the **store** (`IWorkflowStateStore` / `IWorkflowWaitIndex`), which is the
trusted system layer the dispatcher, runner, and integrity checks use and which is never handed to a handler.

- **`AccessContext` carries reach per verb.** It holds the caller's `ReadReach` / `WriteReach` / `PurgeReach`
  (each a `SecurityFilter?`; `null` = unrestricted), so read can be granted independently of write and purge —
  e.g. read across an org but write/purge only your team. `AccessContext.System` is the explicit, named,
  full-reach credential for the system path: "system" is a credential, **not the absence of one**.
- **The policy resolves it.** `ControlPlaneRowSecurityPolicy.Resolve(principal) -> AccessContext` (plus
  `GetInternalTags`, `ValidateUserTags`), bound to the request principal through `IHttpContextAccessor` and
  passed via the optional `rowSecurity` argument to `MapArazzoControlPlane`. With no policy the binding yields
  `AccessContext.System` throughout — fully unrestricted, behaviour unchanged. A deployment typically implements
  the policy over a `SecurityShell`.
- **Reads are scoped; single-row access is gated.** List/search apply `ReadReach` in the store query; get and
  every catalog document endpoint return `null` (→ **404**, non-disclosing) for a row outside `ReadReach`.
- **Writes gate write reach, with 403 vs 404.** Resume/cancel/delete/update gate `WriteReach` *before* acting. A
  row outside **read** reach is **404** (non-disclosing — you cannot tell it exists). A row you *can* read but
  cannot write is **403 Forbidden** (its existence is already disclosed by the read, so masking it as 404 would
  be dishonest). The OpenAPI contract declares 403 (a `Forbidden` response) on resumeRun/cancelRun/deleteRun/
  updateCatalogVersion/deleteCatalogVersion, and the handlers return it via the generated result type.
- **Creation stamps internal tags.** Adding a catalog version stamps the deployment's internal tags (e.g. the
  principal's tenant, §14.3) onto it; triggered runs inherit the version's labels.
- **Purge is row-scoped by `PurgeReach`, orthogonal to the purge capability.** The `runs:purge` *scope* (§14.1)
  grants the *capability*; `PurgeReach` bounds *which rows*. A **tenant admin** purges only their tenant; a
  **service operator** (`AccessContext.System`) purges across tenants. Run purge enumerates through the *same*
  reach-filtered query path `ListAsync` uses (so it is subsumed by query correctness); catalog purge filters its
  `ListObsoleteAsync` candidates by `PurgeReach`.
- **Backend honoring landed with §17.6/§17.7.** Enforcement is correct across all backends: the relational
  stores + Cosmos push the reach predicate server-side (`SqlSecurityRuleEmitter`/`CosmosSecurityRuleEmitter`);
  InMemory/Mongo/Redis/NATS/AzureStorage stream-and-filter per row. The interim fail-loud fallback
  (`NotSupportedException` on a non-null filter) this bullet originally mandated is retired.

**Decision (§14):** operation authz = ASP.NET Core policies named after capability scopes, with the scheme +
claim mapping supplied per deployment (sample-implemented). Row authz = **security tags (KVP labels) + tag
rules in the `simple`-criterion grammar**; claims resolve to a rule, rules compile to an in-memory evaluator
**and** an indexed per-backend store predicate, and a separate security API in the control plane manages the
rules. A deployment may **wrap** the model (§14.3) with a mandated filter + reserved-prefix internal tags
(immutable, client-invisible) that AND into every decision, so multi-tenant isolation is inescapable. Applied
uniformly to workflows and runs.

## 15. Workflow administration — identity, entitlement, and management

A base workflow id needs an **authority**: the identity entitled to publish further versions of it and to anchor
the source-credential grants its runs use (§13). This is distinct from the catalog's governance **`owner`**
(`CatalogOwner { name, email, team?, url? }`, §catalog-design) — that is the accountable *contact*, descriptive
and freely editable. The authority is the **administrator**, and it is a *security identity*, not a contact.

### 15.1 What an administrator is

- An **administrator** is a **deployment-stamped internal (`sys:`) security identity** — the set of internal tags
  `ControlPlaneRowSecurityPolicy.GetInternalTags(principal)` yields for a principal (§14.3/§14.4). It is the same
  unforgeable identity the deployment stamps onto a catalogued version (`securityTags`) and that the credential
  **usage-grants** name (§13, `{dimension, value}` → `sys:{dimension}={value}`). There is **one** identity concept
  across §13–§15, not a parallel "administrator" entity.
- **Granularity is the deployment's choice.** Whatever `GetInternalTags` stamps *is* the administration grain — a
  shell that stamps only `sys:tenant` gives tenant-level administration; one that also stamps `sys:sub` allows
  per-principal co-administration. The model hard-codes neither.
- **Unforgeable by construction.** Only the deployment shell stamps `sys:` tags (reserved-prefix, immutable, rejected
  on user input, §14.3), so a principal cannot self-assert administration by supplying tags — the core security
  invariant that motivated the whole §13 usage-grant design ("no free-for-all by matching tags").

### 15.2 Establishment and the immutable workflow identity

- **Version 1 establishes administration — by materializing an explicit record.** A base id's first version is stamped
  (`WorkflowIdentity`) with the submitter's identity, and with the **immutable workflow identity**
  `sys:workflow=<baseWorkflowId>` that its runs inherit — the identity a credential grant names. The administrator
  identity is the stamped set with `sys:workflow` removed (`WorkflowIdentity.AdministratorIdentity`). When an
  administrator store is configured, publishing version 1 **eagerly writes** the per-base-id administrator record with
  that identity as the sole, **explicit** administrator (`SecuredWorkflowCatalog.AddAsync` →
  `IWorkflowAdministratorStore.PutAsync`). Administration is therefore **only ever the explicit store record** — never an
  implicit version-1 derivation — so the creator is a *normal, removable* administrator (a co-administrator can later
  remove them, e.g. when they leave the organisation), and the reverse administration index (§15.4) has an entry for
  every workflow from birth. (With **no** administrator store configured, administration is the single, immutable
  version-1 identity — surfaced as a synthetic display-only record — and the management operations are unavailable.)
- **Publishing a further version requires being an administrator.** `SecuredWorkflowCatalog.AddAsync` refuses
  (`WorkflowAdministrationException` → 409) a submitter whose stamped identity is not a member of the base id's
  administrator set, so `sys:workflow` cannot be squatted. Membership is the subset test of
  [ADR 0003](../arazzo/adr/0003-membership-matching-over-canonical-identity.md): a stored administrator identity
  administers a submitter whose stamped identity contains it (`WorkflowIdentity.SameAdministrator`, founder ⊆
  candidate). This supersedes the earlier set-equality rule. The catalog Add path carries the stamped identity,
  not an `AccessContext`.

### 15.3 Managing administrators (reassignment and co-administration)

Administration is **mutable** — teams hand workflows off and share them — but a version's `sys:` tags are immutable
(§14.3), so administration is *not* re-stamped onto versions. It is held in an **explicit, per-base-id administrator
record** materialized at creation (§15.2) and the authoritative source thereafter:

- **Record.** A `WorkflowAdministrators` document per base id — the set of administrator identities, with audit and
  an etag — **materialized eagerly when version 1 is published** (seeded with the creator's stamped identity) and the
  sole authority for administration thereafter. Held in an `IWorkflowAdministratorStore` (per-backend, like the
  run/catalog/credential stores — **shipped across all nine backends**, container-verified on a shared conformance
  suite). Never empty: the last administrator cannot be removed (no orphaning). The creator is a normal entry, so a
  co-administrator can remove them.
- **Operations** (on `ISecuredWorkflowCatalog`, authorized by **current-administrator membership**, not row reach):
  `GetAdministrators`, `AddAdministrator` (idempotent), `RemoveAdministrator` (refuses the last), and
  `TransferAdministration` (replace the set — hand-off; the caller need not remain). Each names administrators with
  the **usage-grant vocabulary** (`{dimension, value}` the policy maps to `sys:` tags), never free-form tags; changes
  are etag-CAS with bounded retry.
- **Surfaces (✅ implemented).** A `/administrators/{baseWorkflowId}` control-plane API (`GET` list, `PUT` transfer,
  `POST .../members` add, `DELETE .../members/{dimension}/{value}` remove), gated by `administrators:read`/
  `administrators:write` scopes — non-disclosing (unknown base id and not-an-administrator both `403`), `409` on the
  optimistic-concurrency race or a last-administrator removal. An `arazzo-runs administrators` CLI
  (`list`/`add`/`remove`/`transfer`) drives it, as does the catalog-detail `<arazzo-administrators-panel>` web UI
  (grantee-picker-driven). Administrators are named by the deployment-mapped grant on the wire, never raw `sys:` tags.
- **Trust boundary.** The record holds only `sys:` identity tags — authorization metadata, never secrets — so it
  persists as plain JSON like every other entity. Administration is over unforgeable stamped identity end to end;
  the management surface cannot widen entitlement past what the shell stamps.

### 15.4 The reverse administration index (approver inbox)

> **Status.** The index itself is **implemented across all ten backends** (InMemory + the nine persistent stores),
> container-verified on the shared conformance suite. The approver-inbox endpoint that consumes it (§16.5) is the next step.

The forward record answers *"who administers base id X?"*. The **approver inbox** (§16.5) needs the reverse: *"which
workflows does this caller administer?"* — so an approver sees every pending access request they can act on, without
having to name a workflow first. Administration membership is the subset test of
[ADR 0003](../arazzo/adr/0003-membership-matching-over-canonical-identity.md) (`WorkflowAdministrators.IsAdministeredBy`
→ `WorkflowIdentity.SameAdministrator`, founder ⊆ candidate). The stored side stays keyed on the founder's canonical
digest (`SecurityIdentityDigest`), so the reverse lookup is still indexed and never a scan; only the read side fans out,
looking up every non-empty subset-digest of the caller's identity (`SecurityIdentityDigest.SubsetDigests`):

- **Index.** Each `IWorkflowAdministratorStore` maintains a reverse map `adminDigest → {baseWorkflowId}` (the in-memory
  analogue of a backend's indexed digest column, the `InMemoryObservedIdentityStore.byDigest` collision-probe pattern,
  §16.5.4), refreshed on every `PutAsync` (diff the base id's old vs new administrator digests; add/remove its id per
  digest). Because administration is now materialized **at creation** (§15.2), the index has an entry for every workflow
  from birth, with no implicit-version-1 blind spot.
- **Query.** `ListAdministeredAsync(adminDigest, limit, pageToken)` returns the caller's administered base ids, keyset-
  paged (bytes-native continuation token, like every other §803 list). The inbox resolves the caller's digest
  (`SecurityIdentityDigest.Compute(CallerIdentity())`) and lists access requests across that set (§16.5).
- **Authority.** The index reflects the same digests the forward `IsAdministeredBy` compares, so the inbox can only ever
  surface workflows the caller genuinely administers — no over-grant, no missed match.

**Decision (§15):** a base workflow id is governed by an **administrator** — a deployment-stamped `sys:` security
identity (the same identity used for version stamping and credential grants), distinct from the descriptive
governance `owner` contact. Version 1 establishes it; publishing further versions requires membership; administration
is reassignable / shareable via a per-base-id administrator record (last-administrator-protected, etag-CAS), with
administrators named in the usage-grant `{dimension, value}` vocabulary — never forgeable user tags.

## 16. Identity, login, and the entitlement lifecycle

§14/§15 decide *what a principal may do once authenticated*. This section decides *how a principal comes to
exist, logs in, and is granted access* — the layer an identity provider (the demo runs **Keycloak**, the
locally-runnable stand-in, §13.5-style) forces us to make concrete.

### 16.1 Two planes — identity vs. authorization

The governing invariant: **the Arazzo control plane has no user registry and issues no credentials.** It
authorizes *claims*. Two cleanly separated planes:

- **Identity plane (the IdP — Keycloak).** *Who you are.* Owns registration (human + machine), credentials, MFA,
  the login flows, and **coarse org/team membership** (groups/roles/attributes). The single source of truth for
  identity.
- **Authorization plane (Arazzo).** *What you may do here.* The deployment shell resolves the authenticated
  principal → **capability scopes** (§14.1) + **`sys:` internal tags** (§14.3); the **security-policy store**
  (§14.2) holds the claim→entitlement rules; §15 administrators and §13 credential grants key off the *same*
  `sys:` identity. Arazzo holds no passwords and no user table — only grant *bindings* keyed to claims.

This is the §13.5 secure-introduction / separation principle at the API edge: identity provisioning is a
separate, declarative concern; Arazzo never holds identity or credentials.

### 16.2 Bootstrapping the first admin — declarative, in three tiers

The "first admin into an empty system" problem (the identity analogue of §13.5's secret-zero) is solved with
**configuration / IaC, not an interactive "create first user" screen**:

1. **IdP super-admin** — Keycloak's own `KC_BOOTSTRAP_ADMIN_USERNAME`/`PASSWORD` at first boot (the master-realm
   admin). Dev: fixed; prod: secret-managed + temporary, recoverable via Keycloak break-glass.
2. **Arazzo realm + seed admin principal** — a **declarative realm import** (realm JSON) seeds an `arazzo` realm,
   an `arazzo-admins` group, and a seed admin user (and a seed admin client for automation). This is the
   deploy-time identity-provisioning step — the exact shape as the §13.5 `vault-init` provisioner.
3. **Arazzo authz grant** — the deployment policy maps the `arazzo-admins` group claim → **all capability scopes
   + unrestricted reach** (the "service operator", i.e. `AccessContext.System`-equivalent). Config-as-code,
   per §14.1's "scheme + claim mapping supplied per deployment".

The first admin then logs in via OIDC and *already holds* admin — they exist because the realm import + the
policy config say so. A **break-glass** path (the dev API-key scheme, or a one-time bootstrap token disabled
after first use) remains for recovery when the IdP or its config is unavailable.

### 16.3 Login UX

- **Web UI — OIDC Authorization Code + PKCE via a BFF.** The host runs the OIDC dance and holds tokens in a
  secure **HttpOnly cookie session**; the (zero-build, web-component) SPA calls the API same-origin with **no
  tokens in JavaScript**. Safer than a browser-held token for a no-build SPA, and it keeps the SPA trivial.
- **CLI — OIDC for native apps.** **Auth Code + PKCE on a loopback redirect** (RFC 8252) is the default — it
  opens the system browser, the smoothest experience on a desktop (and the Azure CLI default) — with the
  **Device Authorization Flow** (RFC 8628; `--use-device-code`) for headless / over-SSH use, printing a
  verification URL + code. Access + refresh tokens are cached (a file under the user's app-data, overridable;
  `offline_access` is requested for the refresh token) and silently refreshed, so subsequent commands are
  non-interactive. Built on `Duende.IdentityModel.OidcClient` (the `arazzo-runs login [--use-device-code]` /
  `logout` commands; `--authority`/`--client-id`/`--server` or `ARAZZO_RUNS_*` env select the IdP + control plane).

**BFF session-cookie security (CSRF).** A cookie is *ambient* — the browser attaches it to cross-site requests —
so the BFF session needs CSRF defence in depth (the bearer/API-key paths are immune; they carry no ambient
credential). Layers, matching the canonical .NET BFF (Duende) pattern:

- **`HttpOnly`** — the session cookie is invisible to JavaScript (XSS cannot read it).
- **`SameSite=Lax`** — the cookie is not sent on cross-site state-changing requests (the classic CSRF vector).
  It stays `Lax`, not `Strict`, so the top-level redirect back from the IdP carries it. SameSite alone is not
  sufficient: *all subdomains are same-site*, and older browsers vary.
- **Required anti-forgery header (`X-CSRF`)** — the robust layer. The server **rejects (403)** any request that
  carries the session cookie, uses an unsafe method, and lacks the `X-CSRF` header; the SPA sends it on every
  call. A custom header forces a **CORS preflight** for any cross-origin caller, which is **denied by default**
  (no CORS policy is configured), isolating the cookie-authenticated API to the same origin. This is *not* the
  classic synchronizer-token antiforgery (which is for server-rendered forms); the required-header form is the
  idiomatic SPA/JSON-BFF choice.
- **OIDC `state` + `nonce` + correlation cookie** protect the login flow itself; **`/logout` is POST-only**
  (not GET-forgeable, and SameSite blocks a cross-site logout-POST from carrying the cookie).

### 16.4 Principals — humans and machines

Both are *principals with claims*; only the authN flow and IdP-side registration differ — Arazzo's authz is
identical for both.

- **Humans** → Keycloak **users** (self-service registration where the realm enables it, else admin-invite),
  placed in **groups/roles** → claims. Arazzo never registers a human.
- **Machines** → Keycloak **clients**, in two tiers (matching current best practice):
  - **Client credentials** with **private-key-JWT or mTLS** (not a shared secret) for simple cases — static
    secrets are the weak spot and must be rotated.
  - **Workload identity federation** (the target): the workload presents platform attestation (Kubernetes
    ServiceAccount, cloud IAM, SPIFFE/SVID) and exchanges it for an Arazzo-scoped token — **no stored secret**.
    The §13.5 secure-introduction principle, now for the API caller (the same idea the runner uses toward Vault).

### 16.5 The entitlement lifecycle — invite, grant, request, approve

This is the operational flow §14/§15 left implicit. The **division of responsibility**:

- The **IdP owns identity + coarse, slow-changing org/team membership** (groups → claims). Adding/removing a
  person *from the org or a team* is an IdP operation.
- The **Arazzo security-policy store owns the claim→entitlement bindings**, including **per-principal grants**
  (the deployment shell can stamp `sys:sub`, §15.1, so a rule may key to one principal). Granting access in
  Arazzo means *writing a rule/binding* (§14.2 grammar) — **never mutating the IdP**. Fast-changing, in-domain
  entitlement lives here, next to the §15 administrators who approve it.

A principal needs two things to act, sourced differently:

| Need | What | Source |
|------|------|--------|
| **Capability** | a scope (`catalog:read`, `runs:write`, …, §14.1) | usually a **role claim** from the IdP → policy |
| **Reach** | which domain's rows (§14.2/§14.3) | a **team/domain claim** → `sys:` tag → a security rule |

Two flows, by privilege level:

- **Onboard (coarse, standing — no request).** Invite the user in the IdP and add them to their team group →
  on login their claims yield `sys:team=payments` → the **standing bootstrap rule** grants `catalog:read` +
  **read-reach to `domain=payments`**. They can **list/read payments workflows immediately**. Read/list access
  is membership-driven: it falls out of org/team membership, no request needed.
- **Elevate (run/admin — request → approve).** Running needs `runs:write` + **write-reach** to the domain; this
  is *not* granted by mere membership. The user issues an **access request** ("run access to `payments`") →
  Arazzo **routes it to the domain administrator** (§15 — Arazzo already knows who holds the `sys:team=payments`
  admin grant) → on approval Arazzo **writes an entitlement** (a security-policy binding granting `runs:write`
  reach `domain=payments`, scoped to the requester's `sys:sub`, or by elevating them into an operator role) →
  effective on their next request, **revocable and audited** in the grants view.

**Effective access at request time** = the deployment shell resolves the principal's claims → `sys:` tags →
capability scopes + the security-policy store's rules (standing rules **∪** per-principal entitlements) →
the `AccessContext` (`ReadReach`/`WriteReach`/`PurgeReach` + scopes) the handlers already enforce.

> **Worked example — Alice (Payments).** (1) An admin invites Alice in Keycloak and adds her to the `payments`
> group. (2) Alice logs in (OIDC); her `team=payments` claim → `sys:team=payments`; the standing rule lets her
> **list payments workflows** right away. (3) Alice needs to run `nightly-reconcile`; she has no run access, so
> she clicks **Request run access — payments**. (4) The request lands in the **payments domain administrator's**
> approval queue. (5) The admin approves; Arazzo writes a `runs:write` entitlement scoped to Alice + reach
> `domain=payments`. (6) Alice can now trigger payments runs — and only payments runs. The admin can revoke it,
> and the grant is auditable.

**What this means we must build** (the new surface — the rest already exists): an **access-request + approval
workflow** — a request resource, the routing to the relevant §15 administrator, the approver queue, and the
entitlement write on approval — exposed in the API, CLI, and UI. The grant store, the rule grammar/engine, and
the §15 administrator lookup are already in place; the request/approval workflow layers on top of them.

### 16.5.1 The approval process — a strategy seam, and the workflow-backed target

The approval *decision process* is a **pluggable strategy**, not hard-wired — the same shape ServiceNow Flow
Designer and Camunda/BPMN use for self-service access requests (request → route to approver → grant + audit):

- **Default (built-in, ships with the access-request surface).** A single-approver flow: the request routes to
  the §15 domain administrator, who approves, and the control plane performs the bounded grant write. No engine
  involved — so the onboarding/entitlement surface is **decoupled from the (paused) live-execution work** and
  ships with the Keycloak slice.
- **Workflow-backed (the target — Arazzo governing Arazzo).** A **system-provided Arazzo approval workflow**,
  bootstrapped declaratively at deploy (catalogued + §15-administered by the bootstrap system admin), behind the
  same strategy seam, for orgs needing multi-stage / conditional / notified / time-boxed approvals. It is the
  natural composition of primitives that already exist: a human approval = a **suspended-for-message / human-task
  run**; customization = **publishing a new catalog version** (governed by §15 + catalog versioning); the grant =
  a **typed OpenAPI operation** on the security API called with a §13 system credential.

Three guardrails make a *user-editable* approval safe (without them it is a privilege-escalation engine):

1. **The requester needs no access (circularity break).** "Request access" is a first-class control-plane
   endpoint any authenticated principal may call — it is *not* a general workflow trigger. The approval workflow
   is **system-bootstrapped, system-administered, and runs as `AccessContext.System`**, seeded before any user
   exists (the §16.2 bootstrap pattern). The requester triggers it but never needs rights on it.
2. **The grant authority is capped by the platform, not the workflow.** The editable workflow decides *who/
   whether/how many approvers, conditions, escalations, timeouts* — but the security API's grant operation
   enforces the ceiling: an approval may grant **at most the requested scope to the requesting subject** — never
   `security:write`, never system reach, never a third party, never escalation. So the decision process can be
   liberalized freely; the **privilege ceiling is fixed** (the same bounded-authority idea as the §13.5 Vault
   provisioner).
3. **Editing the approval workflow is a top-tier, audited, system-admin-only operation** — separation of duties
   at the meta-level: who can be *approved by* it ≠ who can *edit* it.

**Sequencing.** The built-in default lands with the Keycloak/OIDC + access-request surface (this slice). The
workflow-backed approval is the **capstone of the slice**, implemented once the rest of Keycloak is proven — and
it doubles as the **ideal first live-executed workflow** (internal, controlled, high-value, exercising human-task
suspend/resume + a typed privileged action end to end), so it is the concrete motivation to unpause live
execution, dogfooded on the system's own governance rather than a demo toy.

### 16.5.2 Built-in access-request + approval — build plan

The built-in (default-strategy) surface is built first. Two design decisions were taken (recorded here as the
plan of record):

- **Decision A — the grant lives in the Arazzo authorization plane (per-principal capability *and* reach).** An
  approval writes a **single entitlement** binding the requester's subject claim to *both* a capability scope
  (`runs:write`) and a row reach (`domain=payments`). Arazzo never mutates the IdP; capability and reach are
  *both* resolved from claims **∪** stored per-principal entitlements. Rejected: "membership confers capability,
  gate reach only" (weaker — everyone in a domain would hold run capability) and "reach-only now" (leaves the
  grant semantics incomplete).
- **Decision B — the request targets a workflow (per-workflow), routed to that workflow's §15 administrators.** A
  request is *"run access to `nightly-reconcile`"*; it routes to the existing `IWorkflowAdministratorStore`
  administrators of that base id (no new domain-admin registry). The granted reach may still be the workflow's
  domain. Rejected: per-domain routing (would add a parallel admin registry; the design's "domain administrator"
  language is a future generalization).

**The Decision-A crux (two enforcement layers).** Reach is resolved *in* the handler
(`PersistentRowSecurityPolicy.Resolve` → `AccessContext`); a scope is enforced *before* the handler by the
authorization policy reading the `scope` claim. So a stored scope grant is only effective if it is **unioned into
the principal's effective scopes at authentication time**, not merely in row security. The shape: extend the
security-policy binding with an optional granted-`scopes[]`, and introduce **one entitlement resolver**
(*claims ∪ stored per-principal grants → effective scopes + `AccessContext`*) that **both** the scope-authorization
layer and the row-security layer consult (the demo `KeycloakClaimsTransformer` and the durable server both call
it). One entitlement object carries scope + reach — exactly the §16.5 wording.

**Performance invariant.** The entitlement resolver is a **per-request warm path**, so it is held to the
security layer's low/zero-allocation bar: generated CTJ types (no hand-rolled records), ledger-then-code per op,
and a `MemoryDiagnoser` benchmark proving the resolve path's allocation floor. The added
scope-union must not regress the existing `Resolve`/`HasScope` warm path.

**Platform cap (guardrail 2), enforced in the approval handler** (there is no write-time cap in the policy store
today — the right place for it is the approval layer): an approval grants **at most** `requested ∩ a fixed
grantable allowlist` (§16.5.3) — run capability + reach only (`runs:read`/`runs:write`, plus the `catalog:read` view grant,
§17.3), **never** `security:write`/system reach/a third party/
escalation; the subject is fixed to the requester and the reach to the target workflow's domain.

**Time-bound grants (PIM / just-in-time).** Approved elevation is **time-boxed, not standing** — the privileged-
identity-management model. Two decisions: (a) **lazy, fail-safe expiry** — the entitlement carries an optional
`expiresAt` and the resolver **excludes any binding past it at resolution time**, so an expired grant stops working
even if cleanup is down; a background **reaper** deletes expired rows as housekeeping only (correctness never
depends on it — the same fail-safe shape as a token/lease). The warm path stays zero-allocation on the common
non-expiring case via a precomputed `AnyExpiringBindings` flag; the resolver gains a `TimeProvider`. (b)
**deployment max-TTL + requester-proposed duration** — the deployment configures a maximum TTL; the request may
propose a duration ≤ max (defaulting to it); approval stamps `expiresAt = now + min(proposed, max)`. Standing
grants (the bootstrap admin, standing read rules) carry no expiry. Modelled as **optional** fields — the binding's
`expiresAt`, the request's `requestedDurationSeconds` (proposed) + `grantedUntil` (audit) — added up front so there
is no later migration; the lazy-expiry resolver + reaper land as a focused sub-step (1.5) between the store and the
approval service.

**Sequenced build (each its own gated, tested commit):**

1. **Entitlement resolver** (Decision-A foundation) — extend the binding schema with optional `scopes[]`; the
   unified resolver; wire granted scopes into the authorization scope source; unit-test claims ∪ grants; benchmark
   the warm path. *Hardest/most core — first, in isolation.*
2. **Access-request store + model** — a generated `AccessRequest` schema type (requester identity, target base id,
   requested scopes, requested reach, status, decision audit, resulting-binding ref) + `IAccessRequestStore` +
   `SqliteAccessRequestStore`.
3. **Approval service** — over (1)+(2): gate approve on §15 admin-of-target-workflow; enforce the platform cap;
   write the entitlement; mark Approved + audit; deny/withdraw paths.
4. **API** — the `accessRequests` OpenAPI resource (`POST` create = any authenticated principal [guardrail 1];
   `GET` list with mine/approvable filters; `GET {id}`; `POST {id}/approve|deny|withdraw`); regen server; handler;
   wire into `MapArazzoControlPlane`.
5. **CLI** — an `access-requests` command branch; regen client; integration tests.
6. **Demo E2E** — wire the store into the demo host; prove the §16.5 worked example against real Keycloak (alice
   requests → payments admin approves → alice may trigger that workflow, and only it).
7. **UI** — the request button + approver queue in the live app shell.

**Sub-decisions (settled).** **Grantable-scope allowlist = `runs:read` + `runs:write`** (as built —
`AccessRequestApprovalService.GrantableScopes`). An approval grants at most run access to the requester's own subject,
reach-scoped to the target workflow; the hard never-list is enforced **by construction** — granted scopes are the
intersection `requested ∩ allowlist`, so anything outside the allowlist (`security:*`, `administrators:write`, any
`*:purge`, etc.) can never be granted, and purge reach is hard-wired to `None`. The subject is fixed to the requester
and the reach to the target workflow. **The `catalog:read` "view" grant is now built (§17.3)** — letting a reviewer see
one workflow without joining its domain or administering it (administration §15 is orthogonal to visibility):
`catalog:read` is in `AccessRequestApprovalService.GrantableScopes`, and `WriteBindingAsync` maps a granted
`catalog:read` → a *read reach* over the workflow's rows, so the grant carries reach (it is no longer inert).
**Early revoke is in this slice** — a revoke deletes the granted security-policy binding (the grant stops at the next
resolution, fail-safe) and marks the request `Revoked`, audited; so a grant is both time-boxed *and* cuttable short.

### 16.5.3 Eligible vs active — self-elevation (no ambient privilege)

Elevated capability is **never ambient** — not for delegated administrators, and **not for the bootstrap system
admin**. The Azure-PIM *eligible vs active* model: a principal may be **eligible** (permitted to self-elevate a
capability) without it being active; wielding it requires an explicit, **audited activation** (justification +
time-box ≤ max TTL) that turns eligible → active. Decisions:

- **Scope — everyone.** No standing god-mode for any identity. Even the bootstrap system admin logs in with
  standing read + eligibility only and must activate to act. A **break-glass** path (the dev API-key scheme / a
  one-time bootstrap token) remains for recovery when the elevation path itself is unavailable — the §16.2
  secret-zero analogue.
- **Eligibility source — the IdP (coarse) now.** Eligibility is a group/role claim (e.g. `arazzo-admins` ⇒
  eligible-for-all), checked at activation; finer Arazzo-stored eligible-assignments are a later refinement —
  consistent with the §16.1 plane split (the IdP owns coarse membership).

**Composition (no rework).** A self-elevation reuses the whole access-request machinery: it is an `AccessRequest`
(subject = the activator, requested scopes = the eligible capability, duration ≤ max) **auto-approved by an
eligibility strategy** behind the §16.5.1 seam — the same time-boxed, platform-capped, audited entitlement the
route-to-admin default writes, minus the human approver. The **resolver is unchanged** (capability already comes
only from *active* stored entitlements); what changes is the **claims mapping** — a group confers *eligibility* (a
marker), not standing elevated scopes (the demo `KeycloakClaimsTransformer` moves from `arazzo-admins → All` to
`arazzo-admins → eligible-for-All`). Each activation is **audited by telemetry**: the request/grant trail plus a
span/event on the `Corvus.Arazzo` ActivitySource/Meter (already wired to OTel / the Aspire dashboard via
ServiceDefaults). Lands in step 3 (the eligibility strategy + an `activate` convenience) and the demo claims-mapping
change; the step-2 store needs no changes.

**Approver-granted eligibility (the PIM "eligible assignment").** Beyond the coarse IdP-claim eligibility, a §15
administrator may **grant eligibility** to a principal — the durable "you may self-elevate this" assignment, distinct
from a one-time active grant. The flow: a request → the approver chooses **approve-as-eligible** (writes an
eligibility assignment: subject + scope ∩ allowlist + reach = the workflow + an eligibility window + a per-activation
TTL cap), optionally **also activating once now** ("make eligible, then grant"); thereafter the principal
**self-elevates JIT** without re-approval — each self-elevation auto-approved because the stored eligibility matches,
minting a fresh time-boxed active grant, audited. Eligibility itself confers **no ambient capability** — it only
gates activation. An admin can revoke eligibility (future activations denied) and/or an active grant (cut current
access short). This is the symmetric counterpart of the capability resolution: **capability = claims ∪ stored
entitlements**; **eligibility = claims ∪ stored eligibility**. Decisions: **(a) model — reuse the security-policy
binding with an optional `eligibleOnly` flag**: the resolver *ignores* eligible-only bindings (they grant nothing
active — preserving no-ambient-elevation), and the self-elevation strategy *reads* them as eligibility; one store,
one model, riding the shared serialization (all 10 backends free, like `scopes`/`expiresAt`). **(b) sequencing —
active grants land in step 3** (the §15-admin gate + platform cap + revoke + IdP-claim self-elevation); **approver-
granted stored eligibility + self-elevation-against-it is 3c**. The `eligibleOnly` binding field is added up front
(in step 3) with the resolver already excluding such bindings — so the field is sound and inert until 3c writes/reads
it (no later migration).

**3c — as built (settled).** (a) A new terminal request status **`Eligible`**, distinct from `Approved` (a live
grant): the request records that it was satisfied by an *eligibility assignment*, not an active grant. (b)
**Approve-as-eligible is a dedicated operation** — `POST /accessRequests/{id}/approveAsEligible` (optional
`AccessRequestEligibilityNote` = note + eligibility window), symmetric with approve/deny/withdraw/revoke — writing
the `eligibleOnly` binding (subject + the per-workflow reach rule + scopes capped to the run-access allowlist + an
optional eligibility window); the resolver ignores it, so it confers nothing active and no in-process refresh is
needed. (c) **Self-elevation resolves eligibility in `SubmitAsync`** (eligibility = claims ∪ stored): the deployment's
self-elevation predicate over the requester's principal is resolved in the service (not handed in by the caller) and
unioned with a **by-subject store query** that returns only the requester's bindings and matches an `eligibleOnly` one
on the workflow rule + scope-cover + not-lapsed, auto-approving into a fresh active grant via the existing grant path.
The by-subject query is the reverse index (§16.5.4): a native equality lookup on the claim columns where the backend
can filter server-side (Sqlite, Postgres, MySql, SqlServer, Cosmos, Mongo), and a string-free in-memory filter over the
full read where it cannot (Redis, NATS, Azure Table Storage, in-memory) — retiring the earlier cold scan. The
workflow-rule, scope-cover, and not-lapsed filters run in-service on the (few) narrowed rows, because the reach rule
lives inside the binding document rather than a queryable column; the claim columns carry the whole selectivity a
requester's small binding set needs. (d) The
**per-activation cap is the deployment max TTL** — the eligibility window bounds the *eligibility*; each *activation*
is independently capped at the max TTL (no new binding field). (e) **Revoke accepts an `Eligible` request**, deleting
the assignment so future activations are denied. The "make eligible *and* activate once now" convenience is deferred
(it is eligibility followed by an immediate self-elevation); the demo claims-mapping change (`arazzo-admins →
eligible-for-All`) remains step 6.

### 16.5.4 Naming a grantee — identity resolution (no guessing)

> **Status (as built, June 2026) — this section is the *target* design; a large slice is implemented and committed.**
> BUILT: `IObservedIdentityStore` + the in-memory reference store **and all nine durability backends**
> (Sqlite/Postgres/MySql/SqlServer/Cosmos/Mongo/Redis/AzureStorage/NATS, reach-filtered, conformance-locked); the
> `ControlPlaneRowSecurityPolicy` grantee seam (`SupportedGranteeKinds` / `ResolveGranteeIdentity`) and `whoami`; the
> `IPrincipalDirectory` seam **with an LDAP/AD adapter** (`Corvus.Text.Json.Arazzo.Directories.Ldap`, Novell async,
> conformance-verified against a self-built OpenLDAP container), **a Keycloak adapter**
> (`Corvus.Text.Json.Arazzo.Directories.Keycloak`, Admin REST over the Corvus reader, single-flight token cache,
> conformance-verified against a real `keycloak:26.0` container), **and a SCIM 2.0 adapter**
> (`Corvus.Text.Json.Arazzo.Directories.Scim`, RFC 7644 filter/bearer over the Corvus reader, conformance-verified against
> a mock SCIM provider via the shared `StubHttpMessageHandler`), **a Microsoft Entra ID (Graph) adapter**
> (`Corvus.Text.Json.Arazzo.Directories.EntraId`, OAuth2 client-credentials + single-flight token cache, Graph
> `$filter=startsWith`/`$select` over the Corvus reader, conformance-verified against a mock Graph endpoint), **and an Okta
> adapter** (`Corvus.Text.Json.Arazzo.Directories.Okta`, SSWS API token, Okta `search sw` over the Corvus reader with
> parse-side projection, conformance-verified against a mock Okta endpoint), **and a Google Workspace adapter**
> (`Corvus.Text.Json.Arazzo.Directories.Google`, service-account JWT / domain-wide delegation + single-flight token cache,
> Admin SDK Directory `query` over the Corvus reader, conformance-verified against a mock Directory endpoint); the
> **bytes-to-bytes identity path** — when the deployment supplies a span mapper (`IDirectoryIdentitySpanMapper`), each HTTP
> adapter captures only the value/label + declared attributes as UTF-8 and builds the identity straight into a pooled
> buffer via `SecurityTagSet.Build`/`IdentityBuilder` (no managed string per attribute or tag — a measured 77–90% drop in
> identity-construction allocation across Graph/SCIM/Okta/Keycloak/Google), with the string `FromTags` path unchanged for
> LDAP and string mappers; the
> **attribute-projection seam** — a mapper may declare
> the provider attributes it reads (`IDirectoryIdentityMapper.RequiredAttributes`, opt-in; empty = surface everything, the
> safe default) and an adapter fetches only those plus the value/label attributes it needs (LDAP's native search list,
> SCIM `attributes`, Graph `$select`, Google `fields`), so a directory search neither over-fetches over the wire nor
> over-materialises on parse; the **issuer dimension** — every adapter funnels records
> through `DirectoryPrincipalProjector`, which stamps a configured, mapper-immutable `sys:iss` so identities are disjoint
> across providers by construction (`DirectoryIssuer`); the **identity-collision probe** (`FindIdentityConflictAsync`,
> digest-indexed, full-reach) across every store; **collision enforcement** — admin-add/transfer refuse (409) a grant
> whose resolved identity already belongs to a different grantee; and `GET /identity/{whoami,capabilities,grantees}` with
> the admin-add recording hook. The `IPrincipalDirectory` family is now complete — LDAP, Keycloak, SCIM 2.0, Entra ID,
> Okta, and Google Workspace all ship. `catalog:read` **is now a grantable "view" scope** (§17.3) —
> `AccessRequestApprovalService.GrantableScopes` defaults to `runs:read`/`runs:write`/`catalog:read`, mapping a granted
> `catalog:read` to read-reach over the workflow's rows — so a reviewer can see one workflow without operating or
> administering it. The §17.1 reach-scoping (the observed-identity store, and the `/identity/grantees` search, are now
> reach-filtered like every other list surface) and §17.2 honest `complete` are **implemented**, as is §16.5.5 ambient
> identity dimensions. The **by-subject entitlement index** is now built (§16.5.3): self-elevation resolves eligibility
> in the service and queries the requester's bindings by subject — a native claim-column lookup where the backend can
> filter server-side (Sqlite/Postgres/MySql/SqlServer/Cosmos/Mongo), a string-free in-memory filter elsewhere — instead
> of cold-scanning the whole binding store (the workflow-rule/scope filters run in-service on the narrowed set, as the
> reach rule lives in the binding document, not a column). The **resolved-grantee UI** shipped: `<arazzo-grantee-picker>` drives
> `GET /identity/grantees` across the admin, bindings, credential-usage, overview, and catalog surfaces (retiring the
> hand-assembled `{dimension, value}` tuples). Multi-tag **person** resolution also landed — the directory adapters
> resolve a person to their full membership-expanded identity, and the grantee picker pins that full resolved identity.

> **Superseded (2026-07, ratified membership model).** The exact set-equality rule below has been superseded by a
> **membership (subset)** model: a principal administers / reaches / may-use a target iff the principal's whole stamped
> `sys:` identity **contains** the target's named identity. See
> [ADR 0003](../arazzo/adr/0003-membership-matching-over-canonical-identity.md) for the decision and the
> authorization-versus-identity-operation classification (the identity operations, add-idempotency, digest removal,
> and the collision probe, stay exact by design). The paragraph below is retained as the original resolved-grantee
> rationale; read "set-equality" as "subset" for the authorization surfaces.

Administration (§15) and entitlement (§16.5) both **name a security identity**, and membership is **exact set-equality**
on the caller's whole stamped `sys:` identity (`WorkflowAdministrators.IsAdministeredBy` — *"a superset or partial match
is not an administrator"*). A hand-typed `{dimension, value}` is therefore a footgun: a value the deployment never stamps
matches **no one** (an inert rule that looks authoritative); a coarse one **over-grants** (`tenant=acme` = the whole
tenant); a bad transfer **locks the caller out**. The UI must let an operator name a **real grantee** and have the system
resolve it to the **exact identity** — never assemble tag tuples by hand, never guess the deployment's stamping grain.

- **Grantee kinds — `person`, `team`, `role`, `workflow`.** Each resolves to the `sys:` identity the deployment stamps
  for it (a person → its full per-principal identity, e.g. `{sys:tenant=acme, sys:sub=alice}`; a workflow →
  `sys:workflow=<id>`). The low-level `{dimension, value}` becomes a derived detail the operator never types.
- **Resolution sources** (a grantee is resolved by one of, richest first): **(1) Pluggable directory/IdP search** — an
  `IPrincipalDirectory` seam the deployment plugs, with adapters for the popular protocols (LDAP/AD, SCIM 2.0, OIDC
  UserInfo / Microsoft Entra (Graph), SAML); it searches people / teams / roles and returns each as a resolved identity.
  The directory does its own indexing; Arazzo keeps no shadow copy. **(2) Observed-identity typeahead** (always
  available, no directory) — Arazzo already records every subject it has seen (access-request `subjectClaimValue`s,
  version `createdBy`, current administrators, existing grant subjects); a **store-indexed prefix query** over these is a
  searchable identity list with zero directory dependency (the common "promote someone who already interacts with this
  workflow" case). **(3) Free-typed well-known subject id** — the escape hatch for an identity known out-of-band,
  validated/resolved through the policy so it still maps to something the deployment would stamp (an unmappable grant is
  rejected, as `IdentityFromGrant` does today).
- **Server seam.** The grantee→identity mapping and the caller's own identity ("whoami", for *add-me* and lockout
  prevention) extend the existing `ControlPlaneRowSecurityPolicy` (already the home of `GetInternalTags` /
  `ResolveUsageGrants` / `DescribeUsageScope`); the directory search is the separate injectable `IPrincipalDirectory`.
  New control-plane endpoints expose **the grantee kinds the deployment supports** (capabilities — so the UI offers
  exactly the resolvable grain, no guessing), **resolve/search** (a grantee query → resolved identities), and **whoami**
  (the caller's resolved identity). The default unscoped policy supports nothing (resolves to empty) — identity features
  light up only where the deployment provides a policy + directory.
- **Indexed-store invariant.** Every Arazzo-owned lookup here is an **indexed query pushed down to the store**, never an
  in-memory scan — the same discipline as the keyset-paged stores: the observed-identity typeahead (prefix-indexed on
  subject), the entitlement/grant lookups (*"what does subject X hold"* — the by-subject binding query, a native
  claim-column lookup where the backend can filter server-side and a string-free in-memory filter elsewhere, retiring
  the §16.5.3 "cold store scan" for self-elevation; the by-workflow-rule filter runs in-service on the subject-narrowed
  set, as the reach rule is stored in the binding document, not a queryable column), and
  the reach-filtered catalog/run listings (the reach predicate pushed to the backend, like the credential keyset pages).
  Directory search is the external system's responsibility; Arazzo's own reads stay indexed and paged across all backends.

**Decision (§16.5.4):** operators name grantees as `person`/`team`/`role`/`workflow`, resolved to an exact `sys:`
identity by a pluggable directory seam **∪** a store-indexed observed-identity typeahead **∪** a validated free-typed
subject id; the `{dimension, value}` tuple is never hand-assembled. The control plane separates **three surfaces** over
one resolved identity — **operate** (`runs:read`/`runs:write`, reach-scoped — *built*), **administer** (§15 governance —
*built*), and **view** (`catalog:read`, reach-scoped — *grantable server-side*, §17.3; the UI picker that drives it is
design-intent) — so granting sight or operation of a workflow never implies administering it. Arazzo-owned
identity/entitlement/reach queries are to be indexed and store-pushed-down; as built, the observed-identity typeahead and
the `/identity/grantees` search are reach-filtered, and the by-subject binding query (self-elevation eligibility) is
pushed down natively where the backend supports it. The directory adapters and backend stores **ship**; the
resolved-grantee UI remains design-intent.

### 16.5.5 Ambient identity dimensions — deriving a `sys:` tag from request context (not the IdP)

> **Status: BUILT (2026-06).** The capability is implemented; this section is retained as the rationale and the
> correctness trap it guards against. The seam is the `IAmbientIdentityDimensions` provider (one source of truth funnelled
> into both stamping moments); see **"What was built"** at the end of the section for the concrete types and tests.

**The scenario.** A multi-tenanted host where a `sys:` identity dimension — typically `sys:tenant`, but the argument
generalises to any context-derived dimension — comes from somewhere *other* than the external identity provider: a
**vanity URL / host** (`acme.host.example` → `sys:tenant=acme`), a **route prefix** (`/t/acme/...`), or an **API-gateway
front end** that injects a header (`X-Tenant: acme`) after its own resolution. The IdP token says *who* the principal is
(`sys:iss`, `sys:sub`); the *tenant* is a property of the request path, not the token.

**The seam — everything keys on `ClaimsPrincipal`.** The control plane is deliberately ignorant of how a principal
acquired its claims (§14.1); the single identity seam is `ControlPlaneRowSecurityPolicy`, which only ever sees a
`ClaimsPrincipal` (read through `IHttpContextAccessor`). So "augment the identity from a non-IdP source" reduces to "get
the dimension onto the principal as a claim before the policy reads it." Concretely, identity is stamped at **two**
moments, and both must agree:

1. **Runtime caller stamping (the straightforward half).** `PersistentRowSecurityPolicy.Resolve(principal)` derives the
   caller's per-verb reach, and `GetInternalTags(principal)` / the injected `internalTagResolver` derives the `sys:` tags
   stamped onto rows the caller creates. Both read claims. To source the tenant from the request, add an
   `IClaimsTransformation` (or auth middleware) that reads `IHttpContextAccessor` (`Host` / route / gateway header) and
   adds a `tenant` claim — *exactly* the shape of the demo's `KeycloakClaimsTransformer`, which already synthesizes
   `scope` claims from `groups`. The policy then maps `tenant` uniformly with token-sourced claims; **no control-plane
   change**, and the §14.2 bootstrap rules that already parameterise on a claim (`tenant == $claim.tenant`) work unchanged.

2. **Grant-authoring stamping (the trap).** Administration (§15) and entitlement (§16.5) membership and reach are
   **exact set-equality on the principal's whole stamped `sys:` identity** (§16.5.4). So the *same* dimension must be
   stamped when a **grantee** is resolved — `ControlPlaneRowSecurityPolicy.ResolveGranteeIdentity(kind, value)` and the
   directory `IDirectoryIdentityMapper` / `DirectoryPrincipalProjector`. Here is the failure: an administrator on
   `acme.host.example` resolving a person against Keycloak gets `{sys:iss, sys:sub}` back — the directory adapter queries
   the **IdP, which has no knowledge of the vanity-URL tenant** — so the grant's identity omits `sys:tenant=acme`. The
   runtime caller (also via `acme.host.example`) carries `sys:tenant=acme`. The two never set-equal, and the grant is
   **silently inert** (it looks authoritative but matches no one — the exact §16.5.4 footgun the resolved-grantee design
   set out to remove, re-introduced through a back door). The reach path fails the same way: a stored rule
   `tenant == $claim.tenant` is fine, but a grant *enumerating* a resolved identity (administrator entry, entitlement
   grant) carries the wrong tag set.

**The fix is a pattern we have already shipped: `DirectoryIssuer` / `sys:iss`.** The issuer dimension is a
deployment-controlled value that every adapter funnels through **one** projector (`DirectoryPrincipalProjector`), which
strips any mapper-supplied value and stamps the configured one — mapper-immutable, correct-by-construction, so identities
are consistent across providers without trusting each mapper to remember. A context-derived `sys:tenant` is the *same
shape*: a deployment-controlled dimension that must be funnelled through one projector and stamped at **both** the runtime
and the authoring moment, from **one source**, so the two can never drift.

**What must be built (the actionable checklist):**

- **An ambient-dimension provider, request-scoped.** A small deployment-supplied abstraction — e.g.
  `IAmbientIdentityDimensions.Resolve() → IReadOnlyList<SecurityTag>` — backed by `IHttpContextAccessor`, that maps the
  current request's context (host / route / validated gateway header) to its `sys:` dimensions. It is the **single source
  of truth** consulted by both stamping moments below. It must **fail closed**: an unresolvable context yields *no*
  tenant and therefore *no* access, never a blank/wildcard that would cross tenants.
- **Generalise `DirectoryIssuer` from one hard-coded `sys:iss` to a set of mapper-immutable ambient dimensions.**
  `DirectoryPrincipalProjector` already strips-and-restamps the issuer; extend it to strip-and-restamp every ambient
  dimension the provider yields (issuer becomes one such dimension). The grantee resolved inside an `acme` request thus
  carries `sys:tenant=acme` by construction, no mapper cooperation required. `ResolveGranteeIdentity` (the non-directory
  resolution path) must consult the same provider.
- **Wire the runtime path from the same provider.** Either a deployment `IClaimsTransformation` that emits the ambient
  dimensions as claims, **or** have the policy read the provider directly in `Resolve` / `GetInternalTags`. Prefer one
  provider injected into both the projector and the policy so a single configuration governs both ends — drift between
  "how the caller is stamped" and "how a grantee is stamped" is the whole bug class this section exists to prevent.
- **A round-trip conformance test (the regression lock).** Prove that a grantee resolved within tenant-context `T`
  set-equals a runtime caller in tenant-context `T` (membership via `WorkflowAdministrators.IsAdministeredBy`, reach via
  the resolved `AccessContext`), **and** does *not* match a caller in tenant-context `T'`. This is the test that would
  have caught the inert-grant trap; it must exist before the feature is trusted.
- **Collision probe / digest — verify, don't assume.** `SecurityIdentityDigest` already hashes the *whole* canonical tag
  set, so `FindIdentityConflictAsync` picks up the ambient dimension automatically — but add an explicit case so a future
  change to the digest can't silently drop it (two principals identical but for `sys:tenant` must not collide).
- **`whoami` / add-me / lockout (§15).** The caller's own resolved identity (used for *add-me* and transfer-lockout
  prevention) composes `GetInternalTags`, so it inherits the ambient dimension automatically once the runtime path stamps
  it — but a deployment that sources tenant from context **must** keep whoami on the same provider, or an admin can fail
  *add-me* (their authored entry lacks the tenant the runtime stamps) or lock themselves out on transfer.

**Trust boundary (this is an isolation primitive — getting it wrong is a cross-tenant breach).** A context-derived
dimension is only as trustworthy as the path that sets it. An `X-Tenant` gateway header is safe **only** if the
application cannot be reached bypassing the gateway, or strips/ignores any client-supplied copy and trusts only the
gateway-inserted value. A vanity-host → tenant mapping must be an **authoritative, validated allow-list**, never the raw
`Host` header taken at face value. The ambient-dimension provider is the right place to enforce this (validate the
context against configured mappings, fail closed on a miss) so the trust decision lives in one auditable component rather
than scattered across handlers. None of `sys:`'s correct-by-construction guarantees hold if the dimension's *source* is
spoofable.

**Reach-rule evaluation reads the claim map — stamp every reach-relevant dimension as ambient.** Binding *selection*
(which claim/identity a binding keys on, §16.5.4) is on the forgery-resistant stamped identity, but a matched binding's
*rule* — e.g. `sys:tenant == $claim.tenant` — is evaluated against the request's **claim map**, resolved from the token
except where an ambient dimension (or an internal-tag resolver) overrides the same name. Ambient closes the tenant case
(the provider's `sys:tenant` shadows any token `tenant` claim). The general case is a **deployment responsibility**: any
dimension a reach rule compares must be sourced from the ambient provider or the policy's internal-tag resolver, **not**
taken from the raw token, or a forged claim for a non-ambient dimension flows straight into the reach predicate. Concretely:
every dimension named on the right of a reach rule must be one the ambient provider governs (`GovernedKeys`) or one the
policy stamps in `GetInternalTags`; a deployment that authors a reach rule over a token-only claim has re-opened the
forgery surface `sys:` exists to close. The round-trip conformance test above is the regression lock for the dimensions
that *are* stamped; a reach rule over an unstamped dimension is outside that guarantee by construction.

**What was built (2026-06).** The checklist above, realised:

- **The provider.** `IAmbientIdentityDimensions` (`Corvus.Text.Json.Arazzo.Durability`) — `GovernedKeys` (the `sys:`
  dimensions it owns) + `Resolve() → AmbientDimensionSet`. `AmbientDimensionSet` carries the dimensions in both the
  managed-`SecurityTag` form (string paths) and pre-encoded UTF-8 (the bytes-to-bytes span path), built once per context
  and returned cached, so resolution allocates nothing. Two impls: `StaticAmbientIdentityDimensions` (fixed/single-tenant)
  and `HttpRequestAmbientIdentityDimensions` (`…ControlPlane.Server`) which maps the request's vanity host (`ByHost`) or a
  gateway header (`ByHeader`) to its tenant against an **authoritative allow-list**, failing closed on anything
  unconfigured — the trust boundary in one component.
- **Generalised `DirectoryIssuer`.** `AmbientIdentityStamp` (the string-path strip-and-restamp, generalising
  `DirectoryIssuer.Stamp` to the governed set, mapper-immutable and fail-closed) + `DirectoryPrincipalProjector`'s optional
  `ambient` constructor argument, which stamps on **both** the string path (`AmbientIdentityStamp`) and the bytes-to-bytes
  span path (`AmbientDimensionSet.WriteTo(ref IdentityBuilder)` — no string u-turn, proven by `GoogleResponseParseBenchmarks`
  at 1.95 KB vs the 1.77 KB no-ambient span). The issuer itself is now a *static governed dimension*
  (`StaticAmbientIdentityDimensions([sys:iss=…])`), so the projector funnels the issuer and any ambient dimension through
  one uniform stamp — no issuer-specific path, and one `FromTags` pass over the union on the string side.
  `ResolveGranteeIdentity` (the non-directory path) consults the same provider.
- **Runtime from the same provider (drift-proof).** `PersistentRowSecurityPolicy` takes the **same** `ambient` provider and
  reads it directly (not via a separate claims transformer, so the two ends cannot drift): `GetInternalTags` strip-and-restamps
  the caller's ambient `sys:` tags; `Resolve` injects the ambient dimensions into the reach claim map, prefix-stripped to
  claim space and **authoritative** (a token-supplied `tenant` claim cannot widen the context-derived reach).
- **Regression locks.** `AmbientIdentityDimensionsRoundTripTests` proves a grantee resolved in context `T` set-equals the
  caller in `T` (membership via `WorkflowIdentity.SameAdministrator`, reach via the resolved `AccessContext`) and not in
  `T'`, plus the forged-claim and fail-closed cases; `SecurityIdentityDigestTests` locks that two principals differing only
  by `sys:tenant` do not collide; the caller's whoami identity composes `GetInternalTags`, so it inherits the dimension from
  the same provider.

### 16.6 Decisions (§16)

- **Identity lives in the IdP; Arazzo authorizes claims** — no user table, no credential issuance.
- **Bootstrap is declarative** — realm import + claim→capability policy config; a break-glass token covers IdP/
  config-unavailable recovery.
- **UI login = Authorization Code + PKCE via a BFF** (HttpOnly cookie session; no token in the SPA).
- **CLI login = loopback Auth Code + PKCE (default; opens the browser) + Device Authorization Flow
  (`--use-device-code`, headless/SSH)**, with a cached, silently-refreshed token.
- **Machine identity = client-credentials (private-key-JWT/mTLS) now, workload-identity-federation as the target.**
- **Entitlement = IdP coarse membership (claims) + Arazzo fine-grained grants** (security-policy store, incl.
  per-principal via `sys:sub`). **Read/list is membership-driven** (standing rules); **elevated run capability goes
  through an in-app access request → §15 domain-administrator approval → entitlement write** (capped to `runs:read`/
  `runs:write`). **Built (§17.3):** a per-subject, reach-scoped `catalog:read` "view" grant so a reviewer can be granted
  sight of one workflow without joining its domain. **Server-resolved grantees are built** — an operator-facing query
  (`GET /identity/grantees`) resolves a `person`/`team`/`role`/`workflow` to its exact `sys:` identity via the directory
  seam (six adapters) ∪ the reach-filtered store-indexed observed-identity typeahead ∪ a validated subject id, rather than
  hand-typing tuples; the **resolved-grantee UI picker** that drives it is the remaining design-intent piece (with
  multi-tag **person** resolution).
- A consolidated "principals & grants" admin view is **deferred**, but the **access-request/approval surface is
  in scope** for the Keycloak slice (it is the missing piece, not a nicety).
- **Approval is a strategy seam (§16.5.1).** A **built-in single-approver default** (route to the §15 domain
  admin → bounded grant write, no engine) ships with the access-request surface, decoupled from live execution.
  A **system-bootstrapped, customizable workflow-backed approval** is the documented target — implemented as the
  **capstone of this slice** once the rest of Keycloak is proven, and serving as the first live-executed workflow.
  Three guardrails are mandatory: requester-needs-no-access, platform-capped grant authority, and edit-as-system-
  admin-only (separation of duties).

## 17. Security-review remediation (design)

> Status: **design, agreed June 2026** — the plan from the adversarial security review (§13–§16.5.4). The review's
> verdict was *iterate, not redesign*: the core (forgery prevention, exact-equality §15 membership, fail-closed
> §16.5 grant caps, the §13 secret boundary, non-disclosure) is sound and conformance-locked. The findings cluster
> at two roots — **capability ≠ reach** (a capability honoured without a matching reach leaks) and
> **secure-by-construction but not secure-by-default**. Findings referenced as F1–F10; implementation follows this.

### 17.1 Reach-scope the identity store (F1) — restore the one consistent idiom

The observed-identity store is the **only** list surface that omits `AccessContext`; catalog/runs/credentials all
reach-filter. That inconsistency *is* the cross-tenant disclosure (F1): a holder of `administrators:read` (an admin
of *any one* workflow) can enumerate every observed identity across all tenants (empty prefix = list-all). Fix:

- **`IObservedIdentityStore.SearchAsync` gains an `AccessContext context`** (first parameter, as every other store).
  A candidate is admitted iff `context.Admits(AccessVerb.Read, candidate.IdentityTags)` — the caller discovers an
  identity **only when their read-reach already admits its domain tags**. `tenant=acme`'s admin discovers acme's
  people/teams/roles/workflows; `globex`'s are invisible (non-disclosing). The predicate is **pushed down to the
  backend**, not applied after a global fetch.
- **Why this predicate is right.** An identity carries its own `sys:` tags (a person `{sys:tenant=acme, sys:sub=
  alice}`); a read-reach rule (`sys:tenant == 'acme'`) admits any tag-set containing `sys:tenant=acme`. So the picker
  still finds *people in the caller's own domain* (the real grantee use case) while a workflow-only admin discovers
  only what they can already see. Reach scopes discovery to exactly the caller's existing visibility — no new
  disclosure axis is invented. `/identity/grantees` stays gated by `administrators:read` **and** is now reach-filtered;
  `whoami`/`capabilities` are unchanged.

### 17.2 Honest `complete` (F3)

`complete` must mean "the principal's *whole* stamped identity, so an exact-equality grant will match." The handler
hardcodes `true`, over-asserting for the single-tag mapping the admin-add hook records. Design: **the recording path
stamps completeness; the handler reports it.** `ObservedIdentity` gains a `complete` flag — `true` for a full
resolution (a directory adapter, or a principal's own `GetInternalTags`), `false` for the policy's best-effort
single-tag `ResolveGranteeIdentity` default (a `{sub, alice}` grant that is *not* alice's full `{tenant, sub}`
identity). The policy decides whole-grain vs partial per `{kind, identity}`. The UI surfaces `complete: false` as
"may be partial — confirm before granting," restoring the one warning against an inert/over-broad rule.

### 17.3 Finish the view surface — `catalog:read` as a grantable, reach-scoped entitlement (F4)

Decision: **finish it** (not cut). Make the promised "view" grant real:

- **Allowlist:** `AccessRequestApprovalService.GrantableScopes` → `[runs:read, runs:write, catalog:read]`. The
  fail-closed intersection and the never-list are unchanged.
- **Reach mapping:** `WriteBindingAsync` maps `catalog:read` → **read-reach** over the workflow rule
  (`sys:workflow == '<id>'`) — the same rule as `runs:read`, distinguished by the granted **scope** at the authz
  layer. A `catalog:read` grant = scope `catalog:read` + read-reach → the grantee `GET`s that one workflow's catalog
  entry without joining the domain or administering it; a bare `catalog:read` grant is no longer inert.
- **Surfaces:** the access-request dialog gains a **"View (catalog:read)"** option; the resolved grantee picker's
  three surfaces (view / operate / administer) become fully real.

### 17.4 Secure-by-default (F2) — a closed default and an explicit mode — **IMPLEMENTED**

The old `MapArazzoControlPlane` defaulted to unauthenticated + `AccessContext.System` (`= (null,null,null)` = omnipotent),
and `requireAuthorization`/`rowSecurity` were *independently* optional (so a deployment could get auth **without** reach).
Fix the defaults, not just the docs:

- **No all-null default context.** `AccessContext` is a sealed **class** with a mandatory 3-arg constructor — there is
  no `default`/parameterless instance that is silently omnipotent (a forgotten context is `null`, which fails closed).
  `AccessContext.System` stays the **explicit, named** value for the genuine trusted-system path (it is a credential,
  not the absence of one).
- **An explicit, required `ControlPlaneSecurityMode`** (no default) replaces the two independently-defaulting flags, so a
  deployment must *name* its posture and can never get an open plane, or scopes without reach, by omission:
  - **`Open`** — unauthenticated + System reach (today's dev behaviour, but only as a *named, deliberate* choice, logged
    loudly at startup); a row-security policy must **not** be supplied (it would be ignored).
  - **`Scoped`** — auth + scope gating + a **required** `ControlPlaneRowSecurityPolicy` (the production posture; you
    cannot get scopes-without-reach by omission, the F2 footgun).
  - **`ScopesOnly`** — auth + scope gating + System reach, only as an *explicit* single-tenant choice; a policy must
    **not** be supplied.
  - **`RowSecurityOnly`** — auth + per-row reach with **no** scope gating; a policy is **required**.

  The mapping **validates the mode/policy pairing at startup** (`ArgumentException` on a required policy omitted, or a
  policy supplied where it would be ignored), so an insecure-by-omission combination cannot be constructed. Verified by
  `ControlPlaneAuthorizationTests.The_security_mode_forbids_insecure_by_omission_combinations`; all callers migrated.

### 17.5 Hardening (F5/F6/F7) — **IMPLEMENTED**

- **OAuth2 token endpoint (F5):** `SourceCredentialProviderFactory` rejects a non-`https` `tokenUrl` (a cleartext
  client-secret POST) with an `InvalidOperationException` *before* the secret is resolved. A deployment opts into an
  `http` endpoint for local development with `allowInsecureOAuthTokenEndpoint: true`.
- **`FileSecretResolver` (F6):** an optional confinement root. When configured, every locator is resolved *relative to
  the root* and the canonicalised path must stay within it — an absolute locator or a `..` traversal that would escape
  is rejected before any I/O. With no root the locator is the exact path (the trusted-operator k8s-projection case,
  unchanged). Threaded through `SecretResolverBuilder.AddFile(secretRoot)`.
- **Wildcard binding (F7):** a `claimType == "*"` binding matches every authenticated principal, so an `Unrestricted`
  grant on it would make everyone an operator and collapse tenant isolation. At snapshot-compile the policy **demotes**
  a wildcard `Unrestricted` verb grant to no reach (a wildcard binding's *rule-bounded* grants are still honoured);
  `allowWildcardUnrestrictedReach: true` on `PersistentRowSecurityPolicy` opts a genuinely single-tenant deployment back
  in. Verified by `PersistentRowSecurityPolicyTests`; the demotion is done once per generation, off the hot path.

### 17.6 API behavioural mismatches + consistent idioms — **IMPLEMENTED**

The OpenAPI document is the generated server's source of truth; it must declare exactly what handlers return, and
handlers must map errors consistently. Re-grounding the review's list against the *current* document found several
items already correct from later work; the remaining deltas were applied and **both the server and the CLI client were
regenerated** from the corrected document.

- **Status-code truth (audited).** The consistent rule is **invalid input → 400, optimistic/state conflict → 409,
  non-disclosable → 403/404**. `approveAccessRequest`/`approveAccessRequestAsEligible` already declare *both* `400`
  (scopes not grantable) and `409` (not pending), and the handler matches. `startCatalogWorkflowRun` already declares
  the `409` its handler returns for both "not runnable (no executor)" and "no hosting runner" — its description now
  names both causes. `resumeRun` declared no `202` yet its description claimed the code "MAY return 202"; the code never
  emits it, so the misleading sentence was removed (200/403/404/409 is the true set). `cancelRun.reason` gained
  `minLength: 1` so an empty audit reason is rejected at validation (the declared `400`) rather than silently accepted —
  the generator now emits a constrained `ReasonEntity`.
- **Tag + scheme hygiene.** The `catalog` and `runners` tags used by operations are now declared at the top level, and
  `security:read`/`security:write` were added to both OAuth2 flows' `scopes` blocks (they were referenced by the
  security operations but absent from the scheme).
- **Consistent idioms throughout (the standing rule).** One error→status mapping across all handlers; **keyset
  pagination** (opaque backend-scoped token) on every list surface; **`AccessContext` threaded through every store
  read** (17.1 closes the last gap); reads non-disclosing (404 out-of-reach, 403 readable-not-writable); CTJ generated
  types end to end (no hand-rolled records, no STJ); pooled documents the caller owns. The remediation makes the
  identity layer conform to the idioms the rest of the surface already follows.

### 17.7 Sequencing — **COMPLETE**

The planned order was (1) **17.4 secure-by-default** + **17.6 API truth**; (2) **17.1 reach-scoped identity store** (the
interface change that **gates the §16.5.4 backend fan-out** — done *before* the fan-out); (3) **17.2 `complete`** +
**17.3 the view surface**; (4) **17.5 hardening**. All are now implemented and re-validated against the conformance
suite, the HTTP API tests, and the `MemoryDiagnoser` floors, with a warning-free `slnx` build throughout.

The per-backend **`IObservedIdentityStore` fan-out** across the nine durability backends is now **DONE** (it was gated on
the reach-aware `SearchAsync` interface from 17.1): Sqlite, Postgres, MySQL, SQL Server, Cosmos, MongoDB, Redis, Azure
Storage, and NATS JetStream each implement the store and pass the shared conformance suite on real infrastructure. The
backends follow the catalog store's optimised reach idiom: the four relational backends + Cosmos **push the reach
predicate server-side** (a denormalised child `ObservedIdentitySecurityTags` table via the shared `SqlSecurityRuleEmitter`,
or an embedded `securityTags` array via `CosmosSecurityRuleEmitter`), with the SQL key columns declared with a binary/
ordinal collation (`COLLATE "C"` / `utf8mb4_bin` / `Latin1_General_BIN2`) so the keyset is byte-ordinal; MongoDB, Redis,
Azure Storage, and NATS apply reach in memory over their native ordering (matching their own catalog stores). The
sighting upsert and search share one `ObservedIdentitySerialization` merge across every backend (including the in-memory
reference).

The `IPrincipalDirectory` seam ships with **six concrete adapters** — **LDAP/AD**
(`Corvus.Text.Json.Arazzo.Directories.Ldap`) and **Keycloak** (`Corvus.Text.Json.Arazzo.Directories.Keycloak`),
conformance-verified against real containers, plus **SCIM 2.0** (`…Directories.Scim`), **Microsoft Entra ID / Graph**
(`…Directories.EntraId`), **Okta** (`…Directories.Okta`), and **Google Workspace** (`…Directories.Google`), each
conformance-verified against a mock provider via the shared `StubHttpMessageHandler`. The family is complete; the five
HTTP adapters share the bytes-to-bytes identity path and the attribute-projection seam.
