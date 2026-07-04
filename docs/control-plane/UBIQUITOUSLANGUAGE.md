# Ubiquitous language

A living glossary of the terms this project uses, in the Domain-Driven Design sense: one name per
concept, used consistently in code, documents, APIs, UI copy, and conversation. **Maintained at the
root of the Arazzo design-docs folder; update it in the same change that introduces or refines a
term.** If a term here conflicts with usage elsewhere, this document wins and the other usage should
be fixed. Known conflicts and their resolutions are recorded in
[Normalization decisions](#normalization-decisions) at the end.

Terms marked **(proposed)** are being introduced by in-flight design work and settle when that
design lands.

## Arazzo document model

| Term | Definition |
|------|------------|
| **Arazzo document** | A document conforming to the Arazzo specification (1.0/1.1): `info`, `sourceDescriptions`, one or more `workflows`, optional `components`. The unit a designer edits and the catalog stores. |
| **Workflow** | A named sequence of steps within an Arazzo document (`workflowId`, `inputs` schema, `steps`, workflow-level `successActions`/`failureActions`, `outputs`, `parameters`, `dependsOn`). |
| **Step** | One unit of work in a workflow. Binds to exactly one of: an OpenAPI operation (`operationId` or `operationPath`), another workflow (`workflowId`), or an AsyncAPI channel (`channelPath`, 1.1). Carries `parameters`, `requestBody`, `successCriteria`, `onSuccess`/`onFailure` actions, and `outputs`. |
| **Source description** | An entry in the document's `sourceDescriptions`: a named reference (`name`, `url`, `type` = `openapi` \| `asyncapi` \| `arazzo`) to a source document. In-document; distinct from a *source document* (the file itself) and a *registered source* (a `/sources` registry entry). |
| **Source document** | The actual OpenAPI/AsyncAPI/Arazzo file a workflow's steps bind against; stored inside the package as `sources/<name>.json`. |
| **Operation surface** | The set of operations (OpenAPI) or channel operations (AsyncAPI) a source document exposes; what a user browses to create steps. Described by `OperationDescriptor` / `AsyncApiChannelDescriptor`. |
| **Runtime expression** | An Arazzo `$…` expression (`$inputs`, `$steps`, `$outputs`, `$response`, `$statusCode`, …) evaluated during execution; parsed by `ArazzoExpression`. |
| **Criterion** | A success/assertion condition (`simple`, `regex`, `jsonpath`, `xpath`) with optional `context` runtime expression. Used in step `successCriteria`, in action `criteria`, and (proposed) in scenario expectations. |
| **Action** | A success action (`end`, `goto`) or failure action (`end`, `goto`, `retry` with `retryAfter`/`retryLimit`), attached to a step or inherited from the workflow level. |
| **Components** | The document's reusable library (`inputs`, `parameters`, `successActions`, `failureActions`) referenced via reusable objects. |

## Catalog and lifecycle

| Term | Definition |
|------|------------|
| **Catalog** | The control plane's immutable, content-hashed, versioned store of workflow packages, with governance metadata and search. |
| **Base workflow id** | The unversioned workflow identity (`nightly-reconcile`) under which versions group. |
| **Version** (catalog version) | One immutable release of a base workflow: the package plus governance metadata. The store assigns `versionNumber` and rewrites the workflow id to `{base}-v{n}` (the **workflow-id rewrite**). Content never changes after add; only governance metadata is mutable. |
| **Package** (`.awp`) | The self-contained, deterministic, content-hashed container for a version: the Arazzo document, every referenced source document, and named metadata entries. A complete input to code generation. |
| **Content hash** | SHA-256 over the RFC 8785 canonical form of the logical `{workflow, sources}` content — framing-independent, stable across repacks. The version's identity and the executor's integrity binding. |
| **Baked schemas** | The precomputed `metadata/schemas.json` package entry: per-step schema metadata (typed inputs, resolved output types) that drives `/validate` and the UI's typed forms without re-parsing sources. |
| **Executor manifest** | The `metadata/executor-manifest.json` package entry: target framework, assembly digest, package-hash binding, entry type. Verified before an executor loads. |
| **Draft** | **A version not yet made available in any environment.** A derived state (its availability set is empty), not a stored flag. A draft is exercised by scenarios in the designer, then promoted through environments. Once available anywhere it is no longer a draft. |
| **Working copy** (proposed) | The mutable Arazzo document under edit in the workflow designer, with its scenarios. Durably saveable on its own, as many times as needed, without ever minting a catalog version — so development iteration does not pollute the catalog. Persisted in the designer workspace (and optionally a Git branch); *publish* mints a catalog version from it. Prefer this term over "draft save": **draft** is reserved for an unpromoted catalog version. |
| **Publish** (proposed) | The act of minting a catalog version from a working copy, together with its scenarios and the evidence of their successful execution. A published-but-unpromoted version is a draft. (Serving a version at an endpoint is *hosting*, not publishing — see normalization E.) |
| **Runnable** | A version that carries a compiled executor (code generation and compile succeeded at add). Only runnable versions can be triggered. |
| **Obsolete** | The retired governance status of a version (`Active` → `Obsolete`); still addressable until purged, never deletable while runs reference it. |
| **Purge** | The destructive bulk-reap of obsolete versions, or of old completed/cancelled runs, that nothing references. Gated by a `:purge` capability scope and bounded by purge reach. |

## Environments and promotion

| Term | Definition |
|------|------------|
| **Environment** | A first-class governed deployment target (administrator set, audit) that versions are made available in and credentials are bound per. |
| **Environment administrator** | A member of an environment's governing set; approves availability and runner-authorization requests for that environment. The environment-scoped counterpart of a workflow administrator. |
| **Availability** | The many-to-many relation "version V is available in environment E". Additive; governed by the target environment's administrators. |
| **Promotion** | Making a version available in an environment, either directly (environment administrator) or via an availability request through the approver inbox. Readiness-gated. |
| **Availability request** | A request that a version be made available in an environment, routed to that environment's administrators' approver inbox (approve/deny/withdraw). The environment-scoped sibling of an access request. |
| **Readiness** | The gate on promotion: every source the version references must have a usable credential in the target environment. (Proposed extension: validation evidence as an additional readiness dimension.) |
| **Availability matrix** | The version × environment grid showing each pair as available, promotable, or not ready, with direct make/withdraw or request-promotion actions. |

## Design, test, and validation (proposed)

| Term | Definition |
|------|------------|
| **Workflow designer** | The UI for authoring an Arazzo document: a visual design surface (diagram area + context inspector) and a text editor over the same working copy. The design surface doubles as the interactive debugger for simulations — it is an instrument, not an illustration. |
| **Scenario** | A named, declarative test for a workflow: inputs, mocked source behaviour, triggers, virtual-clock advances, and expected outcomes/paths/outputs. Associated with the version under design; carried forward when a new version of a published workflow is edited. |
| **Mock transport** | The simulated transport (`MockApiTransport`) that answers a workflow's source operations during a simulation with scripted responses instead of real endpoints. (Not a *runner* — the executor is real; its transport and clock are simulated.) |
| **Virtual clock** | The simulated `TimeProvider` a simulation runs against, so timer waits and retries elapse deterministically without real time passing. |
| **Simulation** | Executing a workflow's compiled executor against the mock transport and virtual clock, producing a trace. Deterministic and replayable. |
| **Debug session** | An interactive simulation in the designer: run/pause/step a workflow on the design surface against a scenario's mock transport and virtual clock, with breakpoints on steps, live state inspection, expression evaluation against the paused context, trigger injection, and time-travel over the recorded trace. |
| **Trace** | The structured record of one simulation: the path taken, per-step requests/responses, criterion results, retries, waits, outputs, and final outcome. |
| **Evidence** | The durable record that a version's scenarios executed successfully: the scenario set (by content hash), per-scenario outcomes, and the engine/version identity, attached at publish and surfaced by the catalog. |
| **Scenario runner** | The CLI (`scenarios run`) that executes a globbable set of scenarios against a workflow — in-process via the simulator (standalone, no control plane) or against a control plane — with CI-grade reports and exit codes; wrappable as a GitHub Action. |

## Execution

| Term | Definition |
|------|------------|
| **Executor** | The compiled assembly generated from a package that runs the workflow; loaded per version with digest verification against its manifest. |
| **Run** | One execution of a workflow version (`Pending` → `Running` → `Suspended`/`Completed`/`Cancelled`/`Faulted`), durable and resumable. |
| **Trigger** | How a fresh run is started (as opposed to resuming a suspended one): the HTTP, message, or schedule trigger surface. |
| **Checkpoint** | The single small JSON document per run that *is* the entire resumable state: cursor, step outputs, retry counters, correlation tokens, inputs, wait, fault, history, etag. |
| **Cursor** | The scalar state-machine position in a run's checkpoint identifying which step runs next; the core of resume. |
| **Wait** | Why a run is `Suspended`: a timer (`dueAt`) or a correlated message (`channel` + `correlationId`). |
| **Correlation id** | The key a message wait matches on, so an inbound message resumes exactly the run that awaits it. |
| **Fault** | A run is *faulted* when a step errors and `failureActions` do not resolve it: terminal-but-recoverable. The fault record captures step id, attempt, error, time, and retriability. |
| **Resume** | Continuing a suspended/faulted run by mode: retry faulted step, rewind, skip, or **state patch** (an RFC 6902 patch to the run context, then retry). |
| **Lease** | The advisory single-owner, TTL-bounded claim that stops a horizontally-scaled runner fleet double-advancing one run. |
| **Runner** | An execution host that claims and executes runs; self-registers and heartbeats to the runner registry. |
| **Runner registry** | The store-backed table of runners and the versions they host; a runner missing heartbeats goes **Stale**, past TTL it is pruned and its runs become claimable. |
| **Runner ↔ environment binding** | One environment per runner (many runners per environment): a runner is provisioned for exactly one environment's credentials/network and must be authorized by that environment's administrators before it is dispatchable. |
| **Execution backend** | The pluggable isolation model a runner dispatches a run to: in-process collectible load context, per-run micro-guest, serverless function, or container — an isolation ↔ latency ↔ cost choice. |

## Security and governance

Every access decision has **two planes**: WHO can do WHAT (capability scopes) and WHERE (reach).
Both must pass.

| Term | Definition |
|------|------------|
| **Capability scope** | A token capability tier (`catalog:read`, `runs:write`, `credentials:write`, …) gating API operations. Say "capability scope", not bare "scope", where ambiguity is possible. |
| **Reach** | Row-level visibility: which records a caller can see/affect per verb (read/write/purge), defined by security rules and grant bindings. |
| **Security rule** | A named, reusable boolean row-filter expression over reach labels, written in the `simple`-criterion grammar (e.g. `tenant == $claim.tenant`). The reusable WHERE vocabulary a grant binding points at. |
| **Grant binding** | A `{claimType, claimValue}` → per-verb reach mapping (each verb `Denied`, `Unrestricted`, or a set of rule names). The API/wire name is *security binding*; docs and UI say **grant binding**. |
| **Reach label** | The umbrella term for the `{key, value}` labels reach rules match against: **security tags** on catalog versions and runs (inherited by runs from their version), **management tags** on environments, registered sources, and credentials. Never say bare "tags" — *user tags* are the separate free-form display/filter metadata. |
| **Internal (`sys:`) tag** | A reserved-prefix reach label stamped by the deployment shell: immutable, client-invisible, rejected on user input; the unforgeable key administration and grants rely on. |
| **Tenant shell** | A deployment wrapper that ANDs a mandated rule (e.g. `sys:tenant == $claim.tenant`) and internal-tag stamping into every decision, so multi-tenant isolation is inescapable and users can only narrow within it. |
| **Entitlement** | A per-principal stored grant (never in the IdP) binding a subject claim to a capability scope and a reach, optionally time-boxed. Capability = token claims ∪ stored entitlements. |
| **Access request** | A control-plane resource any principal submits to ask for elevated access to a specific workflow; routed to that workflow's administrators; approval writes an entitlement. A request can never target a third party. |
| **Approver inbox** | The queue showing an approver every pending access/availability/runner request across everything they administer, without naming a subject first. |
| **Eligibility** | The PIM-style "you may self-elevate this" grant (*approve-as-eligible*): durable permission to activate access JIT without re-approval. Eligible is not active — wielding requires an explicit, audited, time-boxed activation. |
| **Grantee** | A real person/team/role/workflow named by governance surfaces, resolved (via directory search, observed-identity typeahead, or a validated well-known subject id) to the exact **resolved identity** the deployment stamps. |
| **Identity dimension** | One `{dimension, value}` key of a stamped `sys:` identity (e.g. `sys:tenant`, `sys:sub`, `sys:iss`). An **ambient identity dimension** is derived from request context (host, route, gateway header) rather than the token, stamped identically at runtime and grant-authoring moments. |
| **Partial identity** | A grantee resolution that yields only some of the dimensions the deployment stamps; flagged because membership matches by exact set-equality, so a partial identity may match no one. |
| **Digest** (identity digest) | The canonical digest over a stamped identity set — equal iff set-equal. The stable key administrators are indexed by and removed by. |
| **Principal directory** | The pluggable directory seam (LDAP/AD, Keycloak, SCIM, Entra ID, Okta, Google) that searches people/teams/roles and returns resolved identities. It resolves *who*, never *what they may do*. |
| **Administrator** | A member of a workflow's or environment's governing set; the only third-party grant in the access model. |
| **Registered source** | An entry in the control plane's `/sources` registry: a named source document registered once and referenced by workflows, with per-environment credentials. |
| **Credential** | A per-source, per-environment binding of a **secretRef** plus non-secret configuration. Its **status** (`Valid`/`ExpiringSoon`/`Expired`) is derived on read; rotation is re-pointing the reference. |
| **secretRef** | A scheme'd *reference* to secret material in an external store (`keyvault://`, `vault://`, `env://`, …) — never the material itself. Only runners resolve it, at bind time. |
| **Usability** | The label-superset test (`IsUsableBy`) governing which runs may use a credential: the run must carry all the binding's usage tags. Distinct from reach (superset test, not rule match). |
| **Persona** | A named operator archetype the UX targets (operator/SRE, publisher/owner, connections admin, security admin, approver, requester); the demo's persona selector drives scope gating from one source of truth. |

## API mechanics

| Term | Definition |
|------|------------|
| **Keyset pagination** | The project-wide list idiom: `limit` + opaque `pageToken` in, `nextPageToken` out, ordered by a stable per-store key. Every list endpoint *and its store* is paged from the start. |
| **Problem details** | Errors are RFC 9457 `application/problem+json` (`type`, `title`, `status`, `detail`, `instance`); clients branch on status, never parse strings. |

## Normalization decisions

Conflicts found by the 2026-07 docs sweep, and the canonical resolutions. Renames marked ⚠ are
pending fixes in the named surfaces.

| # | Conflict | Resolution |
|---|----------|------------|
| A | "Scope" means (1) capability tier, (2) the per-verb grant value `Scoped`, (3) the UI's "Scopes" panel which actually edits security *rules* (`ux-review.md` §"Scopes = security rules"). | (1) is canonical: **capability scope**. Say **scoped reach** for (2). The UI surface is **Rules**: primary tag `arazzo-rules-panel` with `arazzo-scopes-panel` kept as a deprecated alias; `ui-design.md`, `security-ui-design.md`, `ux-review.md` copy updated. |
| B | The grant object has four names: grant / security binding / grant binding / claim→rule binding. | **Grant binding** is canonical in docs/UI; **security binding** is recorded as the API/wire synonym. |
| C | "Source" is overloaded: in-document description, the file, the registry entry, and the UI "Sources" tab that actually manages credentials. | Three precise terms — **source description**, **source document**, **registered source**. The credential-management tab is now **Connections** (was "Sources"); demo, `ui-design.md`, `ux-review.md` updated. |
| D | "Draft" is also allocation-campaign jargon for a pooled document being built at a store's write leaf (`allocation-matrix.md`, `ValidateDraft`). | **Draft** is the lifecycle term only. Scope-qualify the allocation jargon as **write draft** (or rename to *builder document*) in campaign docs/code as touched. |
| E | "Publish" as mint-a-version vs the `catalog-design.md` "publish/hosting service" that serves a version at an endpoint. | **Publish** = mint a catalog version. The endpoint-serving service is the **hosting service** (`catalog-design.md` future-phase section updated). |
| F | Bare "tags" ambiguity (user tags vs security tags vs management tags). | Always qualify: **user tags** / **security tags** / **management tags**; **reach labels** is the umbrella. |
| G | `ux-review.md` labels the Access area "Reach scopes", re-fusing the two planes. | Say **reach rules**; avoid "reach scopes". |
| H | "Readiness" described as a soft check in one place and a hard gate in another (`ux-review.md`). | Readiness is a **hard gate** on promotion and the add-workflow wizard; views may *display* readiness, but promotion never bypasses it. |
