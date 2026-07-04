# Arazzo Control Plane — Web UI Kit (design)

A **reusable, zero-build kit of composable web components** over the [Arazzo Control Plane REST
API](./README.md). It is the UI half of the control-plane quick-start: anyone can `<script>`-import the
pieces they want — just the API client, a single widget, or the whole panel — into a React, Vue, Svelte,
Angular, or plain-HTML application, with no toolchain.

It is deliberately a **kit**, not a monolith. The complete panel (`<arazzo-control-plane>`) is the
reference assembly that proves the pieces compose; it doubles as the sample's run-management screen.

## Goals & non-goals

**Goals**

- **Drop-in anywhere.** Standards-only custom elements; works the same in every framework and in raw HTML.
- **Layered reuse.** A DOM-free client, standalone widgets, and a composed panel — adopt at any granularity.
- **Style-isolated but themeable.** Shadow DOM keeps host CSS out; CSS custom properties + `::part()` let
  the host theme without forking.
- **Auth-agnostic.** The host owns its OAuth2/OIDC/mTLS session and hands the kit credentials. The kit never
  embeds an IdP flow.
- **Runs with no backend.** A demo page + in-memory mock of the API so the kit is explorable immediately.

**Non-goals (v1)**

- No bundled OAuth/OIDC client (the single biggest reuse-killer — it weds the kit to one IdP).
- No charts/metrics dashboard. Telemetry lives in the `Corvus.Arazzo` OpenTelemetry sources, not this API.
- No server. This is the browser side only; it talks to a deployed control-plane host.

## The contract it targets

The kit tracks the control-plane OpenAPI document across its operator surfaces and capability-scope tiers
(from [`README.md`](./README.md)). **Runs**, the **workflow catalog**, **source credentials**, **workflow
administration**, the **§16.5 access-request** surface, the **reach vocabulary** (§14.2 scopes + grant bindings),
the **sources registry** (§7.6), **governed environments** (§7.7), and **availability/promotion** (§7.8) all ship
today.

### Runs — `runs:read` / `runs:write` / `runs:purge`

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listRuns` | `GET /runs?status&workflowId&created/updatedAfter/Before&tag&correlationId&limit&pageToken` | `runs:read` | `<arazzo-runs-table>` |
| `getRun` | `GET /runs/{runId}` | `runs:read` | `<arazzo-run-detail>` |
| `resumeRun` | `POST /runs/{runId}/resume` | `runs:write` | `<arazzo-resume-dialog>` |
| `cancelRun` | `POST /runs/{runId}/cancel` | `runs:write` | `<arazzo-cancel-button>` |
| `deleteRun` | `DELETE /runs/{runId}` | `runs:purge` | `<arazzo-run-detail>` (guarded action) |
| `purgeRuns` | `PURGE /runs?olderThan&limit` | `runs:purge` | `<arazzo-purge-dialog>` |
| `listRunners` | `GET /runners?limit&pageToken` | `runs:read` | `<arazzo-runners>` (the §5.4 registry / health view) |

### Workflow catalog — `catalog:read` / `catalog:write` / `catalog:purge`

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `searchCatalog` · `listCatalogVersions` · `getCatalogVersion` | `GET /catalog[/{base}[/versions/{n}]]` | `catalog:read` | `<arazzo-catalog-table>`, `<arazzo-catalog-detail>` |
| `getCatalogPackage`/`Workflow`/`Source`/`WorkflowSchemas` · `validateCatalogValue` | `GET …/{package,workflow,sources/{name},schemas}` · `POST …/validate` | `catalog:read` | `<arazzo-catalog-detail>`, `<arazzo-value-editor>` |
| `addCatalogVersion` | `POST /catalog` (multipart) | `catalog:write` | `<arazzo-catalog-add-dialog>` |
| `updateCatalogVersion` · `obsoleteCatalogVersion` | `PATCH …/versions/{n}` | `catalog:write` | `<arazzo-catalog-detail>` (guarded) |
| `deleteCatalogVersion` · `purgeCatalog` | `DELETE …/versions/{n}` · `PURGE /catalog` | `catalog:purge` | `<arazzo-catalog-detail>`, panel toolbar |

### Source credentials — `credentials:read` / `credentials:write`

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listCredentials` · `getCredential` | `GET /credentials[/{source}/{env}]` | `credentials:read` | `<arazzo-credentials-table>` |
| `createCredential` · `updateCredential` · `deleteCredential` | `POST /credentials` · `PUT`/`DELETE /credentials/{source}/{env}` | `credentials:write` | `<arazzo-credential-dialog>` |

### Workflow administration — `administrators:read` / `administrators:write`

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listAdministrators` | `GET /administrators/{base}` | `administrators:read` | `<arazzo-administrators-panel>` |
| `addAdministrator` · `removeAdministrator` · `transferAdministration` | `POST .../members` · `DELETE .../members/{dim}/{val}` · `PUT /administrators/{base}` | `administrators:write` | `<arazzo-administrators-panel>` |

### Access requests (§16.5) — request → approve → entitlement

| Operation | HTTP | Kit surface |
|-----------|------|-------------|
| `submitAccessRequest` | `POST /accessRequests` | `<arazzo-access-request-dialog>`, `<arazzo-access-requests>` |
| `listAccessRequests` · `getAccessRequest` | `GET /accessRequests[?baseWorkflowId&status]` · `GET .../{id}` | `<arazzo-access-requests>` |
| `approveAccessRequest` · `approveAccessRequestAsEligible` · `denyAccessRequest` | `POST .../{id}/{approve,approveAsEligible,deny}` | `<arazzo-access-requests>` (approver queue) |
| `withdrawAccessRequest` · `revokeAccessRequest` | `POST .../{id}/{withdraw,revoke}` | `<arazzo-access-requests>` |

An approval is **capped to run access** (`runs:read`/`runs:write`); the requesting subject is always the caller.

### Reach vocabulary (§14.2) — `security:read` / `security:write`

The reusable scope/grant vocabulary a deployment's row security is built from ("scope" is the user-facing term for a
named security **rule** — a row-filter expression; a **grant binding** maps a principal claim to per-verb read/write/purge
reach).

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listSecurityRules` · `getSecurityRule` | `GET /security/rules[/{ruleName}]` | `security:read` | `<arazzo-rules-panel>` |
| `createSecurityRule` · `updateSecurityRule` · `deleteSecurityRule` | `POST /security/rules` · `PUT`/`DELETE …/{ruleName}` | `security:write` | `<arazzo-rules-panel>` (modal editor) |
| `listSecurityOrderings` | `GET /security/orderings` | `security:read` | `<arazzo-rules-panel>` (ordered/classification templates) |
| `listSecurityBindings` · `getSecurityBinding` | `GET /security/bindings[/{bindingId}]` | `security:read` | `<arazzo-grants-panel>` |
| `createSecurityBinding` · `updateSecurityBinding` · `deleteSecurityBinding` | `POST /security/bindings` · `PUT`/`DELETE …/{bindingId}` | `security:write` | `<arazzo-grants-panel>` (self-elevation rejected `403`) |

### Sources registry (§7.6) — `sources:read` / `sources:write`

A source (`{name, type, document}` + per-environment credentials) registered once and **referenced** by workflows; the
list omits the heavy `document` (returned only on a single read). Consumed by the add-workflow wizard (resolve a
registered source, no re-upload) and the credential dialog (derive auth/server from the doc), not a standalone panel.

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listSources` · `getSource` | `GET /sources[/{name}]` | `sources:read` | add-workflow wizard, credential dialog |
| `registerSource` | `POST /sources` | `sources:write` | add-workflow wizard (commit) |

### Governed environments (§7.7) — `environments:read` / `environments:write` / `availability:read`

An environment is a first-class, reach-scoped, **governed** resource (administrator set + audit, exactly like a
workflow §15 — *creating one grants the creator administration*).

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listEnvironments` · `getEnvironment` | `GET /environments[/{name}]` | `environments:read` | `<arazzo-environments>` |
| `createEnvironment` · `updateEnvironment` · `deleteEnvironment` | `POST /environments` · `PUT`/`DELETE …/{name}` | `environments:write` | `<arazzo-environments>` |
| `listEnvironmentAdministrators` | `GET …/{name}/administrators` | `environments:read` | `<arazzo-administrators-panel environment=…>` |
| `addEnvironmentAdministrator` · `removeEnvironmentAdministrator` · `transferEnvironmentAdministration` | `POST …/members` · `DELETE …/members/{digest}` · `PUT …/administrators` | `environments:write` | `<arazzo-administrators-panel environment=…>` |
| `listEnvironmentAvailability` | `GET …/{name}/availability` | `availability:read` | `<arazzo-environments>` (available-versions list) |

### Availability / promotion (§7.8) — `availability:read` / `availability:write` + the approver inbox

"Promotion" = make a workflow **version** available in an environment (additive, many-to-many), governed by the **target
environment's administrators**; readiness-gated (every source credentialed in that environment). An administrator makes a
version available directly; everyone else raises a **request** to a per-environment approver inbox (the §16.5 shape,
parameterised by environment — auth-only, gated in-handler).

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listVersionAvailability` | `GET /catalog/{base}/versions/{n}/availability` | `availability:read` | `<arazzo-catalog-detail>` ("Available in"), `<arazzo-availability-matrix>` |
| `makeVersionAvailable` · `withdrawVersionAvailability` | `PUT`/`DELETE …/availability/{environment}` | `availability:write` | `<arazzo-availability-matrix>` (direct, env-admin) |
| `submitAvailabilityRequest` | `POST /availabilityRequests` | auth-only | `<arazzo-availability-request-dialog>` |
| `listAvailabilityRequests` · `getAvailabilityRequest` | `GET /availabilityRequests[?environment&scope&status]` · `GET .../{id}` | auth-only | `<arazzo-availability-requests>` |
| `approveAvailabilityRequest` · `denyAvailabilityRequest` · `withdrawAvailabilityRequest` | `POST .../{id}/{approve,deny,withdraw}` | auth-only | `<arazzo-availability-requests>` (approver inbox) |

Key model facts the **runs** UI renders (the catalog, credential, and administrator surfaces carry their own
summary/detail models — `CatalogVersionSummary`/`CatalogVersion`, `CredentialBindingSummary`,
`AdministratorList`):

- `WorkflowRunStatus` = `Pending | Running | Suspended | Completed | Cancelled | Faulted`.
- `WorkflowRunSummary` (list row): `id, workflowId, status, createdAt, updatedAt, dueAt?, awaitingChannel?,
  awaitingCorrelationId?, errorType?`.
- `WorkflowRunDetail` (detail): `id, workflowId, status, cursor, createdAt, wait?, fault?, etag`.
- `WorkflowWait`: `kind (Timer|Message), dueAt?, channel?, correlationId?`.
- `WorkflowFault`: `stepId, attempt, error, at`.
- `ResumeRequest` is a `oneOf` over `mode`: `RetryFaultedStep` · `Rewind {targetCursor}` ·
  `Skip {targetCursor?, skipOutputs?}` · `StatePatch {patch[]}` (RFC 6902).
- Errors are `application/problem+json` (RFC 9457): `{type, title, status, detail, instance}`.
- Pagination is **keyset**: `WorkflowRunPage {runs[], nextPageToken?}`.
- Concurrency: the detail carries an `etag`; mutations return **409** on concurrent change (no `If-Match`
  header in the contract — the kit surfaces the 409 and re-fetches).

## Architecture — three layers

```
┌────────────────────────────────────────────────────────────┐
│ Layer 2   <arazzo-control-plane> (runs) · <arazzo-catalog>   │
│           composes Layer 1; owns layout, filters, routing    │
├────────────────────────────────────────────────────────────┤
│ Layer 1   runs:    runs-table · run-detail · resume-dialog   │
│           cancel-button · purge-dialog · status-badge        │
│           value-editor · workflow-id-input · …-step-picker   │
│           catalog: catalog-table · catalog-detail · add-…    │
│           credentials: credentials-table · credential-dialog │
│           administrators-panel · access-requests · …         │
│           standalone custom elements (Shadow DOM)            │
├────────────────────────────────────────────────────────────┤
│ Layer 0   ArazzoControlPlaneClient   (no DOM)                │
│           run · catalog · credential · administrator ops     │
│           keyset paging · problem+json → errors              │
│           auth via host-supplied fetch/getToken              │
└────────────────────────────────────────────────────────────┘
                          │ uses host's
                          ▼  fetch + credentials
                  Arazzo Control Plane API
```

**Hard rule:** Layer 1 components never call `fetch` directly — they take a Layer-0 client (or are handed
data). This keeps auth, retries, base-URL, and error mapping in exactly one place, and makes every widget
unit-testable against a fake client.

---

## Layer 0 — `ArazzoControlPlaneClient`

A dependency-free ES module. The most reusable artifact in the kit: usable in Node, tests, CLIs, or any UI.

```js
import { ArazzoControlPlaneClient, ProblemError } from './arazzo-client.js';

const client = new ArazzoControlPlaneClient({
  baseUrl: '/arazzo/v1',                 // required
  // Auth: pick ONE. Both are optional; default is same-origin credentials.
  fetch: (url, init) => myFetch(url, init),        // full control (interceptors, mTLS host, retries)
  getAuthHeader: async () => `Bearer ${await app.token()}`, // simplest: just a header
  credentials: 'include',                // used when neither hook is supplied (cookie/proxy auth)
  signal,                                // optional default AbortSignal
});
```

### Methods (mirror the operationIds)

```js
// runs:read
await client.listRuns({ status, workflowId, createdAfter, createdBefore, updatedAfter, updatedBefore, tags, correlationId, limit = 100, pageToken }); // → { runs, nextPageToken }
for await (const page of client.listRunsPaged({ status, workflowId })) { … }  // async-iterator over pages
await client.getRun(runId);                                             // → WorkflowRunDetail | throws 404

// runs:write
await client.resumeRun(runId, resume);   // resume = { mode:'RetryFaultedStep' } | {mode:'Rewind',targetCursor}
                                         //        | {mode:'Skip',targetCursor?,skipOutputs?}
                                         //        | {mode:'StatePatch',patch:[...]}      → WorkflowRunDetail
await client.cancelRun(runId, { reason });                              // → WorkflowRunDetail

// runs:purge
await client.deleteRun(runId);                                         // → void (204)
await client.purgeRuns({ olderThan, limit });                         // → { purgedCount }
// runners:read — the execution-host registry (§5.4), read-only (runners self-register + heartbeat out of band)
await client.listRunners({ limit, pageToken });                      // → { runners, nextPageToken }
for await (const page of client.listRunnersPaged({ limit })) { … }

// catalog:read — search + per-version documents
await client.searchCatalog({ q, baseWorkflowId, workflowIdPrefix, tags, status, owner, limit, pageToken });
for await (const page of client.searchCatalogPaged({ … })) { … }
await client.listCatalogVersions(baseWorkflowId);                     // → { versions, nextPageToken }
await client.getCatalogVersion(baseWorkflowId, n);                    // → CatalogVersion | throws 404
await client.getCatalogPackage(baseWorkflowId, n);                    // → Blob (zip archive)
await client.getCatalogWorkflow(baseWorkflowId, n);                   // → Arazzo document
await client.getCatalogWorkflowSchemas(baseWorkflowId, n);           // → baked schema metadata
await client.getCatalogSource(baseWorkflowId, n, sourceName);
await client.validateCatalogValue(baseWorkflowId, n, target, value); // → { valid, errors? }
// catalog:write / catalog:purge
await client.addCatalogVersion({ package, owner, tags });  // multipart/form-data POST /catalog; version assigned server-side
await client.updateCatalogVersion(baseWorkflowId, n, patch);
await client.obsoleteCatalogVersion(baseWorkflowId, n);
await client.deleteCatalogVersion(baseWorkflowId, n);                 // → void (204)
await client.purgeCatalog();                                          // → { purgedCount }

// credentials:read / credentials:write — references + non-secret metadata only (never secret material)
await client.listCredentials();                                      // → { credentials: [CredentialBindingSummary] }
await client.getCredential(sourceName, environment);                 // → CredentialBindingSummary | throws 404
await client.createCredential(body);                                 // → CredentialBindingSummary (201) | 400 | 409
await client.updateCredential(sourceName, environment, body);       // → CredentialBindingSummary (PUT) | 400 | 404
await client.deleteCredential(sourceName, environment);             // → void (204) | 404

// administrators:read / administrators:write — resolved identities (§16.5.4): a stable digest (the removal key),
// the deployment-mapped {dimension,value} grants the identity resolves from, and an optional kind/label
await client.listAdministrators(baseWorkflowId);                          // → { administrators: [{digest, identity, kind?, label?}] }
await client.addAdministrator(baseWorkflowId, member);                    // member = a resolved grantee {value, dimension?, kind?, identity?, label?} → AdministratorList | 400 | 403 | 409
await client.removeAdministrator(baseWorkflowId, digest);                 // → AdministratorList | 403 | 409
await client.transferAdministration(baseWorkflowId, { administrators }); // → AdministratorList | 400 | 403 | 409

// access requests (§16.5) — request → approve → entitlement-write; an approval is capped to run access
await client.submitAccessRequest({ baseWorkflowId, requestedScopes, reason?, requestedDurationSeconds? }); // → AccessRequestView
await client.listAccessRequests({ status?, baseWorkflowId? });           // → { accessRequests } (own, or a workflow's queue)
await client.getAccessRequest(requestId);                                // → AccessRequestView | 403 | 404
await client.approveAccessRequest(requestId, { reason? });               // → AccessRequestView (administrator only)
await client.approveAccessRequestAsEligible(requestId, { reason?, eligibilityWindowSeconds? }); // → AccessRequestView
await client.denyAccessRequest(requestId, { reason? });                  // → AccessRequestView
await client.withdrawAccessRequest(requestId, { reason? });              // → AccessRequestView (requester only)
await client.revokeAccessRequest(requestId, { reason? });                // → AccessRequestView (administrator only)

// reach vocabulary (§14.2) — security:read / security:write
await client.listSecurityRules({ q?, limit?, pageToken? });              // → { rules, nextPageToken }   ("scopes")
await client.getSecurityRule(name); await client.createSecurityRule({ name, expression, description? });
await client.updateSecurityRule(name, { expression, description? }); await client.deleteSecurityRule(name);
await client.listSecurityOrderings();                                    // → { orderings: [{dimension, labels}] }
await client.listSecurityBindings({ q?, limit?, pageToken? });           // → { bindings, nextPageToken }   (grant bindings)
await client.getSecurityBinding(id); await client.createSecurityBinding(body); // self-elevation rejected 403
await client.updateSecurityBinding(id, body); await client.deleteSecurityBinding(id);

// sources registry (§7.6) — sources:read / sources:write — list omits the document
await client.listSources({ limit?, pageToken? });                        // → { sources, nextPageToken }
await client.getSource(name);                                            // → Source WITH document | 404
await client.registerSource({ name, type, document, displayName?, description?, managementTags? }); // → 201 | 409

// governed environments (§7.7) — environments:read / environments:write; availability list is availability:read
await client.listEnvironments({ limit?, pageToken? });                   // → { environments, nextPageToken }
await client.getEnvironment(name);                                       // → EnvironmentSummary | 404
await client.createEnvironment({ name, displayName?, description?, managementTags? }); // → 201 (grants creator admin) | 409
await client.updateEnvironment(name, { displayName?, description? }); await client.deleteEnvironment(name);
await client.listEnvironmentAdministrators(name);                        // → { administrators } (resolved identities)
await client.addEnvironmentAdministrator(name, member); await client.removeEnvironmentAdministrator(name, digest);
await client.transferEnvironmentAdministration(name, { administrators });
await client.listEnvironmentAvailability(name, { limit?, pageToken? });  // → { availability, nextPageToken }

// availability / promotion (§7.8) — version availability + direct make/withdraw + the per-environment approver inbox
await client.listVersionAvailability(baseWorkflowId, n);                 // → { availability, nextPageToken }
await client.makeVersionAvailable(baseWorkflowId, n, environment);       // env-admin only; readiness-gated 409 → AvailabilityEntry (201/200)
await client.withdrawVersionAvailability(baseWorkflowId, n, environment); // → void (204) | 403 | 404
await client.submitAvailabilityRequest({ baseWorkflowId, versionNumber, environment, reason? });
await client.listAvailabilityRequests({ status?, environment?, scope? }); // scope = 'mine' | 'queue' (approver inbox)
await client.getAvailabilityRequest(requestId);
await client.approveAvailabilityRequest(requestId, { reason? });         // environment administrator only; readiness-gated 409
await client.denyAvailabilityRequest(requestId, { reason? }); await client.withdrawAvailabilityRequest(requestId, { reason? });
```

### Behaviours baked in

- **Error mapping.** Any non-2xx with `application/problem+json` becomes a thrown `ProblemError` exposing
  `{ status, title, detail, type, instance }`. `404`/`409` are normal control-flow signals, so widgets
  `catch` and branch on `err.status` rather than parsing strings.
- **Auth precedence.** `fetch` hook → `getAuthHeader` → `credentials`. Whichever is set wins, in that order.
- **No retries by default** (the host's `fetch` hook is the place to add them); cancellation via `AbortSignal`.
- **Zero global state.** Construct one client per host; pass it down. (Layer-1 elements also accept a
  `base-url` attribute and build their own client when not handed one — convenient for plain HTML.)

---

## Layer 1 — composable components

All are custom elements with Shadow DOM. Each: works standalone, takes a `.client` property (or `base-url`
attribute), emits bubbling `CustomEvent`s, exposes `::part()`s and `--arazzo-*` variables, and renders
explicit **loading / empty / error** states. None of them navigate — they emit; the host (or Layer 2) decides.

### `<arazzo-status-badge status="Faulted">`
The shared status pill (and the canonical status→colour mapping). Pure presentational; no client.

### `<arazzo-runs-table>`
Lists runs with filters and keyset paging.

| | |
|---|---|
| **Attributes** | `base-url`, `status` (filter), `workflow-id` (filter), `created-after`/`created-before`/`updated-after`/`updated-before` (time-window filters), `tags` (AND-matched), `correlation-id` (exact match), `page-size` (default 100), `poll` (ms; auto-refresh off by default), `selectable` |
| **Properties** | `.client`, `.filters = { status, workflowId }` |
| **Events** | `run-selected {detail:{run}}`, `error {detail:{problem}}`, `loaded {detail:{count, hasMore}}` |
| **Parts** | `table`, `row`, `cell`, `status`, `pager`, `filters` |
| **States** | skeleton rows while loading · "No runs match" empty · inline error banner with retry |

Columns: status badge · `workflowId` · `id` (truncated, copyable) · age (`createdAt`) · "waiting on"
(`dueAt` for timers / `awaitingChannel`+`awaitingCorrelationId` for messages) · `errorType`. Sort is
client-side within a page; paging is keyset via `nextPageToken` (Prev keeps a small token stack).

### `<arazzo-run-detail runid="…">`
The full record for one run, plus its available actions.

| | |
|---|---|
| **Attributes** | `base-url`, `runid`, `poll` (ms), `scopes` (space-separated, gates actions) |
| **Properties** | `.client`, `.run` (inject to skip the fetch) |
| **Events** | `run-changed {detail:{run}}` (after an action), `run-deleted {detail:{runId}}`, `error`, `close` |
| **Parts** | `header`, `status`, `cursor`, `wait`, `fault`, `actions` |

Renders status, `cursor`, `createdAt`, the **wait** block (timer due-time with a live countdown, or
channel/correlation for message waits), and the **fault** block (`stepId`, `attempt`, `error`, `at`).
Surfaces actions **gated by `scopes` and status**: Resume + Cancel (`runs:write`, non-terminal/faulted),
Delete (`runs:purge`, behind a confirm). An action the token can't perform is hidden, not shown-disabled,
to keep the surface honest — unless `show-forbidden` is set (then disabled with a tooltip).

### `<arazzo-resume-dialog>`
A modal for the four resume modes. The most involved widget.

- Mode picker: **Retry faulted step** (default) · **Rewind** · **Skip** · **State patch**.
- Retry → no inputs. Rewind → `targetCursor` number. Skip → optional `targetCursor` + optional
  `skipOutputs` JSON. State patch → an **RFC 6902 JSON Patch editor** (add/remove/replace rows over the run
  context `{ inputs, stepOutputs }`, with a raw-JSON escape hatch and validation before submit).
- Emits `resume-submitted {detail:{run}}` on success; renders the `409` ("not faulted / changed
  concurrently / patch failed") inline and offers re-fetch.

### `<arazzo-cancel-button runid="…">`
One-click cancel with an optional `reason` prompt and a confirm. Emits `run-cancelled` / surfaces `409`
(already terminal). The lowest-risk mutation, deliberately its own tiny element so a host can embed *just*
cancel without the rest.

### `<arazzo-purge-dialog>`
Bulk reap: `olderThan` (date/relative picker) + optional `limit`, a strong confirm ("permanently delete N+
completed/cancelled runs"), and a result toast (`purgedCount`). `runs:purge` only.

### Run authoring helpers

Three smaller elements the run widgets compose (and a host can reuse standalone):

- **`<arazzo-workflow-id-input>`** — a `workflowId` text filter with **catalog autocomplete** (type-ahead over
  catalogued base ids); emits `workflow-id-changed`.
- **`<arazzo-workflow-step-picker>`** — choose a resume **target step by name** (for Rewind/Skip) from the
  workflow's steps, resolving the cursor; direction-constrained so you can't pick an unreachable step.
- **`<arazzo-value-editor>`** — a **strongly-typed form built from a step's precomputed schema metadata**
  (unions/tuples/maps/`const`, inline booleans, live per-field validation). It is the editor for Skip's
  `skipOutputs` and for typed inputs; validates against the baked schema before emitting `value-changed`.

### Runners (`/runners`, §5.4)

#### `<arazzo-runners>`
The runner registry / health view — a **read-only** observability surface (runners self-register and heartbeat out of
band; the control plane only observes, so there are no mutating controls). Lists each execution host with its liveness
(**Online** / **Stale**, derived from the most recent `lastSeenAt` against `stale-after`, default 90s — past the TTL a
runner is pruned server-side), uptime, advertised `maxConcurrency`, transports, and the workflow versions it hosts
(loaded / loading). Keyset-paged (Load more); an optional `poll` (ms) keeps the heartbeat ages + health current.
`runs:read`. Emits `loaded`, `error`.

### Catalog (`/catalog`)

The workflow catalog browse/govern surface — one row per workflow with a version switcher.

#### `<arazzo-catalog-table>`
Searches catalogued versions (free-text `q` · base id · status · owner · tags). Shows **one row per base
workflow** — its versions collapse together, the latest is the representative; the detail view switches versions.

| | |
|---|---|
| **Attributes** | `base-url`, `q`, `base-workflow-id`, `status`, `owner`, `tags`, `page-size` (default 50), `selectable` |
| **Properties** | `.client`, `.filters = { q, baseWorkflowId, status, owner, tags }` |
| **Events** | `version-selected {detail:{version, baseWorkflowId, versions}}`, `loaded {detail:{count, hasMore}}`, `error {detail:{problem}}` |

Columns: Workflow (title + `baseWorkflowId`) · latest version · status · owner · updated · tags. Because a base
workflow has many immutable versions, the table fetches every matching version (walking the server's keyset
pages) and groups them **client-side**; paging the table is over the resulting groups.

#### `<arazzo-catalog-detail>`
One workflow's versions and documents, plus governance actions — the **per-workflow governance hub**. A version
switcher selects a version; renders its metadata (`owner`, tags, status), and offers the document downloads
(`package`/`workflow`/`sources`/`schemas`) and a typed-value **validate** (via `<arazzo-value-editor>` against
the baked schemas). Guarded actions: **update** governance metadata + **obsolete** (`catalog:write`),
**delete** a version (`catalog:purge`, confirmed). Embeds the authz-gated **Security — administrators** section
(`<arazzo-administrators-panel>`, keyed by the version's `baseWorkflowId`) and a self-service **Request access…**
action (opens `<arazzo-access-request-dialog>` locked to this workflow, §16.5). Emits `version-changed` /
`version-deleted` / `access-requested`.

#### `<arazzo-catalog-add-dialog>`
Upload a pre-built package **or build one in-browser** from a workflow document + its source files
(`./workflow-package`), set `owner`/tags, and submit — the catalog assigns the version number server-side.
`catalog:write`. Emits `version-added`.

### Credentials & administration (`/credentials`, `/administrators`)

Two operator surfaces that manage **references and identity metadata only — never secret material** (client methods
in [Layer 0](#layer-0--arazzocontrolplaneclient)). The UI mirrors that invariant: no field ever accepts or displays a
secret; a credential is a `secretRef` (`scheme://locator[#version]`) plus non-secret config, and rotation is
*re-pointing the reference*, not entering a secret.

#### `<arazzo-credentials-table>`
The rotation worklist. Status-first, like the CLI's `credentials list`.

| | |
|---|---|
| **Attributes** | `base-url`, `status` (filter: `valid`/`expiring`/`expired`), `source` (filter), `selectable`, `scopes` (gates the New button) |
| **Properties** | `.client`, `.filters = { status, source }` |
| **Events** | `credential-selected {detail:{binding}}`, `credential-new {}`, `loaded {detail:{count, expiring, expired}}`, `error {detail:{problem}}` |
| **Parts** | `table`, `row`, `cell`, `status`, `toolbar` |

Columns: source · environment · auth kind · **`credentialStatus`** (a status badge — `valid`/`expiringSoon`/`expired`)
· expires · usage grants. The store pages server-side; a footer counts expiring/expired and offers **Load more** to
append the next keyset page. `status`/`source` filter the loaded rows client-side. No secret column exists.

#### `<arazzo-credential-dialog>`
A single create/edit/rotate dialog (`open()` to create, `open(binding)` to edit) — there is no separate detail
element. It edits **references and metadata** — auth-kind-driven `secretRef` slots, config key/values, auth kind,
optional `expiresAt`, usage grants — and rejects a value that isn't a well-formed `secretRef` *before* submit (the same
boundary the server enforces, so a secret can't be smuggled in). The `secretRef` slots and the non-secret config fields
are **driven by the auth kind**; unrecognised config keys are preserved verbatim. The kinds are `apiKey`/`bearer`/
`basic`/`oauth2ClientCredentials` (one slot each) and **`mtls`** (§13.1: a `certificate` slot plus optional
`privateKey`/`passphrase`) — for `mtls` the dialog forces "Shared" and hides the usage "Restrict" option, since a client
certificate is connection-level (the TLS handshake) and cannot be usage-scoped. **Edit is a merge** mirroring the CLI:
re-pointing a reference is a rotation and stamps `rotatedAt`; unspecified fields are preserved (management tags and usage
grants are shown read-only when editing). Emits `credential-saved`. `credentials:write` gates create/edit/delete.

#### `<arazzo-administrators-panel>` (`base-workflow-id="…"` **or** `environment="…"`)
The administrator set for one subject. **Subject-agnostic**: set `base-workflow-id` for a workflow's administrators
(§15) or `environment` for an environment's (§7.7) — `_subject()` binds the matching client operations, and `canWrite`
gates on `administrators:write` (workflow) or `environments:write` (environment). Lists the resolved identities
(digest + the `{dimension, value}` grants + kind/label); **add**, **remove** (per-row, refused for the last —
surfaces `409`), and **transfer** (replace the whole set). Members are named with the resolved-grantee
`<arazzo-grantee-picker>` (which resolves a real person/team/role to its exact `sys:` identity), not a hand-typed
tuple. Non-disclosing: a non-administrator caller's mutation is a `403` shown as a plain banner, never a leak of who
is. The panel renders as a per-workflow **Security** section on the catalog detail and as the administrators section
of `<arazzo-environments>`, not a standalone screen.

### Access requests (`/accessRequests`, §16.5)

The request → approve → entitlement-write surface. An approval is capped to **run access** (`runs:read`/`runs:write`);
the requesting subject is always the caller (a request can never target a third party).

#### `<arazzo-access-request-dialog>`
The §16.5 "request access" submit form, reusable as a standalone dialog. `open()` to pick any workflow (autocomplete),
or `open({ baseWorkflowId, lockWorkflow: true })` to fix it to one workflow. The form offers exactly the run verbs (an
approval is capped to run access). Emits `access-request-submitted`. Shared by `<arazzo-access-requests>` and by the
catalog entry (`<arazzo-catalog-detail>`'s **Request access…** action, locked to that workflow).

#### `<arazzo-access-requests>`
Two views over the same identity-gated API (`view="mine|queue"`): **My requests** (`GET /accessRequests` — the caller's
own; submit a new request or withdraw a pending one) and the **Approver queue** (`GET /accessRequests?baseWorkflowId=…`
— that workflow's queue; the caller must administer it, `403` shown as a plain banner). Queue actions: approve /
approve-as-eligible / deny a pending request, and revoke an approved grant. Emits `access-request-submitted`,
`access-request-decided {request, action}`, `error`.

**Three surfaces over one resolved identity — the resolved-grantee picker is the design-intent UI (§16.5.4; the server identity layer ships).**
Membership matches a caller's *whole* stamped `sys:` identity by **exact equality**, so making an operator hand-assemble
a `{dimension, value}` tuple or guess the deployment's grain is a hazard — a wrong value silently matches no one,
over-grants a whole tenant, or locks the caller out. The **target design** is a single **grantee picker** that lets the
operator name a **real** `person` / `team` / `role` / `workflow`, resolved to the exact identity by a **pluggable
directory/IdP search** (LDAP/SCIM/OIDC/Entra/SAML), a **store-indexed typeahead over identities Arazzo has already
seen**, or a **validated well-known subject id**; it would drive three choices — **View** (`catalog:read`, reach-scoped),
**Operate** (`runs:read`/`runs:write`, reach-scoped, via the §16.5 request path), and **Administer** (§15 governance).

> **Status — what is built vs. design-intent** (consistent with `execution-host-design.md` §16.5.4). Built **on the
> server**: the whole identity layer — `IPrincipalDirectory` with six adapters (LDAP, Keycloak, SCIM 2.0, Entra ID, Okta,
> Google Workspace), the `GET /identity/{whoami,capabilities,grantees}` endpoints (reach-filtered, `complete`-reported,
> with `observed` and `directory` sources), the `SupportedGranteeKinds` / `ResolveGranteeIdentity` grantee seam, and
> §16.5.5 ambient identity dimensions; and `catalog:read` **is** a grantable "view" scope
> (`AccessRequestApprovalService.GrantableScopes` defaults to `runs:read`/`runs:write`/`catalog:read`). Built **in the
> UI**: **Operate** (run access through the §16.5 request → approve → entitlement-write path); **Administer** (the §15
> workflow administrator set **and** the §7.7 environment administrator set, via the subject-agnostic
> `<arazzo-administrators-panel>`); and — now built — the **resolved-grantee `<arazzo-grantee-picker>`** itself (a
> `GET /identity/grantees` typeahead → resolved `sys:` identity, with a `kinds` allow-list so a surface admits only the
> grantee kinds it should), wired into the administrator panels and the add-workflow wizard. It **replaces** the earlier
> interim `<arazzo-admin-grant-input>` tuple builder. The **"view" grant** (`catalog:read`) **is** offered — as the
> default, least-privilege option in `<arazzo-access-request-dialog>` (§17.3: View / Read runs / Operate), self-service
> via the request → approve path, with `catalog:read` in the server's `AccessRequestApprovalService.GrantableScopes`.
> **Deliberate non-goal:** a *unified picker* by which an administrator grants View (or Operate) to a **named third
> party** in one step. The access model intentionally holds the invariant that **a request can never target a third
> party** (only §15 administration names others); a direct third-party scope grant would be a new grant primitive that
> relaxes that invariant, and is **out of scope** — View/Operate are self-service (request → approve), Administer is the
> only third-party grant.

### Reach vocabulary (`/security`, §14.2)

The reusable scope/grant vocabulary the deployment's row security is built from. Both page + search server-side and gate
mutations on `security:write`.

#### `<arazzo-rules-panel>`
Manages the named **rules** (row-filter expressions — the API calls them security *rules*). A searchable, keyset-paged
list; authoring is **template-first in a modal editor** (pick a goal — label match, set membership, caller-claim, ABAC
superset/intersect, ordered classification, or raw advanced — and the template writes the expression with a live preview;
`listSecurityOrderings` supplies the classification labels). Emits `scopes-changed` (event name kept for compatibility),
`loaded`, `error`. Registered as `arazzo-rules-panel`, with `arazzo-scopes-panel` kept as a deprecated alias.

#### `<arazzo-grants-panel>`
Manages the **grant bindings** — a principal claim → per-verb (read/write/purge) reach, each verb `unrestricted` or a set
of scope names. Searchable, keyset-paged, modal editor with a scope typeahead. The server **rejects self-elevation**
(`403`) — granting a claim the caller itself holds write/purge reach goes through the access-request flow. Emits
`grants-changed`, `loaded`, `error`.

### Sources, environments & promotion (`/sources`, `/environments`, `/availabilityRequests`, §7.6–7.8)

#### Sources registry (§7.6) — no standalone panel
The `/sources` registry (list omits the heavy `document`; a single read returns it) is consumed by the **add-workflow
wizard** (`<arazzo-catalog-add-dialog>` resolves a workflow's declared `sourceDescriptions` against the registry — a
registered source needs no re-upload — and registers genuinely-new ones at commit) and the **credential dialog** (derives
the auth kind + per-environment server base URL from the source document). There is no dedicated sources panel.

#### `<arazzo-environments>`
The governed-environment management surface (§7.7) — a self-contained **master-detail** panel. Left: a keyset-paged list
(**Load more**) + a **New environment** modal (`environments:write`; creating one grants the creator administration).
Right (per selection): editable metadata (display name / description → `updateEnvironment`), the environment's
administrator set via `<arazzo-administrators-panel environment=…>`, the **workflow versions available in it**
(`listEnvironmentAvailability`, `availability:read`), and **Delete** (confirmed). Emits `environment-selected`,
`environment-created`, `environment-changed`, `environment-deleted`, `loaded`, `error`.

#### `<arazzo-availability-request-dialog>`
The §7.8 "request promotion" submit form (`open({ baseWorkflowId?, versionNumber?, environment?, lockWorkflow? })`):
choose a workflow + version + target environment. Reused standalone and by `<arazzo-catalog-detail>`'s **Request
promotion…** action (locked to a version, offering only environments where it is *ready* and not already available).
Emits `availability-request-submitted`.

#### `<arazzo-availability-requests>`
Two views over the identity-gated API (`view="mine|queue"`): **My requests** (the caller's own; submit / withdraw) and
the per-environment **Approver inbox** (every request across the environments the caller administers; approve / deny). An
approval is **readiness-gated** (`409` when a source is uncredentialed in the target environment). Columns: workflow ·
version · environment · requester. Emits `availability-request-submitted`, `availability-request-decided {request,
action}`, `loaded`, `error`.

#### `<arazzo-availability-matrix base-workflow-id="…">`
The **promotion matrix** for one base workflow — rows = its versions (newest first), columns = the deployment
environments, each cell the availability of that `(version, environment)` pair. A cell is **available** (with a
**Withdraw** action), **promotable** (ready — every source the version references has a usable credential in the
environment, §7.7/§13 — offering a direct **Make available** with `availability:write`, or **Request promotion** through
the approver inbox otherwise), or **not ready** (no action). `selected-version` highlights a row. Embedded by
`<arazzo-catalog-detail>` (the version's base workflow) below the per-version "Available in" line. Emits
`availability-changed {versionNumber, environment, available}`, `promotion-requested`, `loaded`, `error`.

---

## Layer 2 — reference panels

Each panel composes Layer 1 into a master/detail screen, owns **one** Layer-0 client (built from `base-url` +
`authProvider`) shared with every child, and gates actions by `scopes`. Two packaged panels ship —
`<arazzo-control-plane>` (`arazzo-control-plane.js`) and `<arazzo-catalog>` (`arazzo-catalog.js`). The
administrators and access-request surfaces fold into the catalog panel (they are embedded by
`<arazzo-catalog-detail>`, the per-workflow governance hub); the **credentials** surface ships as Layer-1
components (`<arazzo-credentials-table>` + `<arazzo-credential-dialog>` + `<arazzo-access-requests>`) that the host
composes directly (as the demo page does) — there is no separate packaged credentials/administration panel element.

```html
<arazzo-control-plane base-url="/arazzo/v1" scopes="runs:read runs:write"></arazzo-control-plane>
<script type="module">
  import './arazzo-control-plane.js';                 // registers all elements
  document.querySelector('arazzo-control-plane')
    .authProvider = async () => `Bearer ${await app.getAccessToken()}`;
</script>
```

### `<arazzo-control-plane>` — run management (`arazzo-control-plane.js`)
- Left: `<arazzo-runs-table>` with a filter bar (status chips + workflowId search) and auto-refresh toggle.
- Right: `<arazzo-run-detail>` for the selected run, wiring its `Resume`/`Cancel`/`Delete` to the dialogs.
- Toolbar: a guarded **Purge** entry (only if `scopes` includes `runs:purge`).

### `<arazzo-catalog>` — catalog browse/govern (`arazzo-catalog.js`)
- Left: `<arazzo-catalog-table>` (search + filters); Right: `<arazzo-catalog-detail>` with the version
  switcher, document downloads, typed-value validate, and the guarded update/obsolete/delete actions.
- The detail is the **per-workflow governance hub**: it embeds the authz-gated **Security — administrators**
  section (`<arazzo-administrators-panel>`) and a self-service **Request access…** action
  (`<arazzo-access-request-dialog>`, locked to the workflow).
- Toolbar: a guarded **Add version** entry (`<arazzo-catalog-add-dialog>`, `catalog:write`); the Add-workflow
  flow can stage administration (the interim `<arazzo-admin-grant-input>`) and set up per-source credential
  bindings after the version lands (the guided `<arazzo-credential-dialog>` locked to each declared source).

### Credentials, access, permissions, environments & promotion surfaces (Layer-1, host-composed)
These ship as Layer-1 components rather than packaged Layer-2 panels — the host (or the demo page) composes them, one
per tab:
- **Sources** — `<arazzo-credentials-table>` (status worklist) paired with `<arazzo-credential-dialog>` for
  create/edit/rotate (`credentials:read`/`write`).
- **Access** — `<arazzo-access-requests>` for the §16.5 request/approval surface (My requests + the approver queue).
- **Permissions** — `<arazzo-grants-panel>` + `<arazzo-rules-panel>` for the §14.2 reach vocabulary
  (`security:read`/`write`).
- **Environments** — `<arazzo-environments>` for the governed §7.7 registry (it embeds `<arazzo-administrators-panel>`
  in `environment` mode).
- **Promotions** — `<arazzo-availability-requests>` (+ `<arazzo-availability-request-dialog>`) for the §7.8 approver
  inbox.
- `<arazzo-administrators-panel>` is embedded by the catalog detail and by `<arazzo-environments>` (above), not a
  standalone screen.

---

## Cross-cutting contracts

### Theming
Every component reads a small token set; the host overrides any of them (defaults give a clean neutral look,
light/dark via `prefers-color-scheme` unless `theme="light|dark"` is set):

```
--arazzo-font, --arazzo-radius, --arazzo-bg, --arazzo-surface, --arazzo-border,
--arazzo-text, --arazzo-muted, --arazzo-accent,
--arazzo-status-running, --arazzo-status-suspended, --arazzo-status-faulted,
--arazzo-status-completed, --arazzo-status-cancelled, --arazzo-status-pending
```

Plus `::part()`s on every structural node for deeper restyling without forking.

### States, a11y, and safety
- **Loading / empty / error** are first-class in every data component (skeletons, empty copy, an error
  banner that shows the problem `title`/`detail` and a retry). Silence is never a state.
- **Accessibility:** semantic table, dialogs use the native `<dialog>` element (`showModal()` — built-in focus
  trap) with `Esc`/backdrop close, status conveyed by text + colour (not colour alone), full keyboard paths.
- **Destructive guards:** Delete and Purge require explicit confirmation; Purge also echoes the match count.
- **Scope honesty:** actions absent unless `scopes` grants them — the UI never offers what the token will
  `403`.

### Events over navigation
Components emit; they don't route. `run-selected`, `run-changed`, `resume-submitted`, `run-cancelled`,
`error`, `close`. This is what lets a host wire them into its own layout/router, and lets Layer 2 orchestrate
without the children knowing about each other.

---

## File layout (zero-build)

The kit is an **npm package** (`@corvus-dotnet/arazzo-control-plane-ui`) — what web developers expect — at
`web/arazzo-control-plane-ui/`. The **deliverable** is `src/` (the only thing
`package.json` `files` publishes); the **demo** and **tests** are dev-only siblings — a clean separation
between what ships and what doesn't. There is no .NET/RCL packaging; any host (including the ASP.NET
control-plane server) serves the static ESM directly or pulls it from a CDN.

```
web/arazzo-control-plane-ui/
├─ package.json                        npm package: exports (., /client, /components/*), files: ["src"]
├─ README.md                           package readme
├─ src/                               ← DELIVERABLE (published to npm)
│  ├─ arazzo-client.js                 Layer 0 — ArazzoControlPlaneClient, ProblemError (run·catalog·credential·administrator·access-request ops)
│  ├─ arazzo-control-plane.js          Layer 2 — run-management panel (registers everything)
│  ├─ arazzo-catalog.js                Layer 2 — catalog browse/govern panel
│  ├─ workflow-package.js              build/inspect a package archive in-browser (./workflow-package)
│  ├─ arazzo-kit.css                   optional shared theme tokens
│  └─ components/                      Layer 1 — base.js + status-badge; runs: runs-table, run-detail,
│                                        resume-dialog, cancel-button, purge-dialog, value-editor,
│                                        workflow-id-input, workflow-step-picker; catalog: catalog-table,
│                                        catalog-detail, catalog-add-dialog; credentials: credentials-table,
│                                        credential-dialog; administrators-panel, admin-grant-input (uncommitted
│                                        interim); access-requests-panel (<arazzo-access-requests>),
│                                        access-request-dialog
├─ demo/                              ← DEV-ONLY sample (not published)
│  ├─ index.html                       live demo wired to the mock
│  ├─ mock-api.js                      in-memory control plane (seeded runs + catalog, problem+json)
│  └─ favicon.svg                      the Corvus mark
└─ test/                             ← DEV-ONLY (not published): node:test + @web/test-runner + Playwright
```

Each deliverable file is a standalone ES module importing only its siblings — no bundler, no transpile. A
consumer can `import` one component, or the whole panel, from npm, a CDN, or their own static host.

## Dev / mock harness
`demo/mock-api.js` implements the run, catalog, credential, administrator, and access-request operations in
memory (seeded with runs in every status, including a faulted run with a fault record and a suspended run with
timer/message waits, plus catalogued workflow versions, seeded credential bindings, administrators, and access
requests) and returns RFC 9457 errors so the error/empty/loading paths and the resume/conflict `409`s are all
exercisable with **no server**. `demo/index.html` mounts the panels against it. This is the "open it and it
works" quick-start entry point.

The demo also carries a **persona** selector (Administrator / Operator / Read-only viewer) that drives the whole
gated-elevation model from one source of truth: it sets each component's capability `scopes` (so the real components
hide the write controls they lack) **and** tells the mock which scopes + administration the caller has, so the mock
returns the same `401`/`403`/elevation-required responses a real control plane would (`mock.setPersona(...)`). Switching
to **Operator** turns the promotion matrix's direct **Make available** into **Request promotion**, empties the approver
inbox (an operator administers nothing), and `403`s a direct make/approve — so the request → approve loop is visible
end-to-end; switching back to **Administrator** approves it. This keeps the demo honest: the gates the server enforces
(and the server/CLI test suites cover) are *shown*, not bypassed.

## Validation against the real contract
The kit's `ArazzoControlPlaneClient` is checked against the same OpenAPI document the server/CLI are
generated from (`arazzo-control-plane.openapi.json`): a small conformance test asserts every request shape
(paths, query params, the `ResumeRequest` `oneOf` bodies, `PURGE /runs`) and response model the client
builds matches the spec, so the JS client and the generated .NET client can't drift.

## Open questions (for review)

1. **Where it lives / how it ships** — *Resolved:* an npm package
   (`@corvus-dotnet/arazzo-control-plane-ui`) at `web/arazzo-control-plane-ui/`,
   publishing `src/` (its `package.json` `files`), with `demo/` and `test/` as dev-only siblings. npm is what
   web developers expect; there is no .NET/RCL package — any host serves the static ESM or pulls it from a CDN.
2. **Resume `StatePatch` editor depth** — full visual RFC 6902 builder vs. a validated raw-JSON textarea for
   v1. *Recommendation: raw-JSON-with-validation now, visual builder later.*
3. **Polling vs. push** — the API is poll-only; `poll` attributes cover it. If a future events/SSE resource
   lands (plan §11), the table/detail can subscribe instead with no API change to the components.
4. **Packaging step (optional, later)** — ship as loose ESM only, or *also* publish a single concatenated
   `arazzo-kit.min.js` for one-tag adoption. *Recommendation: loose ESM now; add the concat build only if
   asked.*

## Planned work — review feedback (agreed order: #2 → #1 → #5, plus #6)

**Status:** ALL DONE — #2 · #6 · #1 (store-level keyset pagination across all 10 backends + handler + spec + regen
+ JS client/table/mock + conformance; **every backend validated** — InMemory/SQLite in-process and all 8 container
backends 13/13 each via the podman tunnel) · #5 (the New Workflow dialog can set up a credential binding per
declared source after the version lands, reusing the guided credential dialog locked to each source). Per-backend tie-breaker: the tag *discriminator* column
for SQLite/Postgres/Mongo(`_id.t`)/Redis/NATS/AzureStorage; the indexed `TagsHash` for SqlServer/MySql (Tags is
unindexable there); Cosmos orders `(sourceName, environment)` server-side and resolves the discriminator tie-break
in-memory (no stored discriminator property). The continuation token is opaque + backend-scoped, so each backend
keysets on whatever it can order.


From a UI review of the source-credential + governance surfaces. The governing IA decision: **the per-workflow
detail page is the governance hub** — a workflow's administrators (§15) and its source credential bindings (§13)
live *on the workflow*, not in standalone, deployment-wide tabs.

1. **#2 — Administrators move onto the workflow detail (DONE first).** The standalone "Administrators" tab (with
   a workflow dropdown that does not scale) is removed; the §15 administrator set is an authz-gated **Security**
   section on `<arazzo-catalog-detail>`, keyed by the version's `baseWorkflowId`, editable only with
   `administrators:write` (read-only / `403` otherwise — the panel already degrades).
2. **#1 — Pagination for the Sources (`/credentials`) list — IN THE STORES (keyset).** Unlike Runs/Catalog (which
   keyset-page in the store), `/credentials` returns everything in one call; at the thousands-to-millions of
   bindings a deployment accrues over time that does not scale. **Design decision (corrected) — pagination is
   pushed into every backend, not layered in the handler.** Reach is evaluated in-memory, but it is a *per-row
   predicate*, so each backend does the standard keyset **scan-and-filter-until-the-page-is-full**: seek past the
   cursor (`WHERE (sourceName, environment, discriminator) > @cursor ORDER BY … LIMIT batch`, an indexed range
   seek), stream rows applying the reach predicate, stop once `limit` pass, and emit a `nextPageToken` = the last
   included row's key. This reads ≈ `limit / selectivity` rows per page, not the whole table; handler-level
   load-all-then-slice would be O(N) per page / O(N²) to traverse. Changes: `ISourceCredentialStore` list becomes
   paged (`limit` + `pageToken` → page + `nextPageToken`; recommend changing `ListAsync` — the list endpoint is
   its only consumer); **all 10 backends** (InMemory, SQLite, Postgres, SqlServer, MySql, Mongo, Cosmos, Redis,
   NATS, AzureStorage) order by the **logical** key `(sourceName, environment, discriminator)` — for the hash-PK
   backends (SqlServer/MySql), order on the real columns/discriminator, **not** the tag hash, so order + token are
   consistent; `SourceCredentialStoreConformance` gains pagination cases (boundaries, stable order, reach
   interaction, token round-trip); OpenAPI (`limit` + `pageToken` query, `nextPageToken` on `CredentialBindingList`)
   + regenerate stubs + handler threads it through + JS client + `<arazzo-credentials-table>` + mock.
6. **#6 — Request access from the catalog entry (new).** On the workflow's catalog detail (the governance hub),
   add a **Request access** action opening the §16.5 submit flow pre-filled with that `baseWorkflowId`, so an
   operator can request run access to a workflow directly where they are looking at it.
3. **#5 — Source bindings in the New Workflow dialog (DONE).** When a version is added, its package declares named
   sources; each source row has a "Set up a credential binding" checkbox, and after the version lands the guided
   credential dialog (auth-kind-driven slots) opens locked to each flagged source in turn, creating them via
   `/credentials` — completing the per-workflow governance hub (Security + Sources together).

4. **Per-kind guided Config (DONE).** The dialog now drives the **non-secret** config fields from the auth kind,
   the analogue of the guided `secretRef` slots: `apiKey` → header/parameter-name + location; `basic` → required
   username; `oauth2ClientCredentials` → required tokenUrl/clientId + optional scope/clientAuthentication;
   `bearer` → none. Known fields render as labelled inputs/selects (validated on submit), unrecognised keys are
   preserved verbatim in a free-form extra-config list so nothing is silently dropped on edit.

Still captured as a work item: **mTLS source credentials** (design §13.1) — the only auth kind the
`SourceCredentialProviderFactory` does not yet implement; required before this security epic closes.

### Subsequent epic — sources / environments / promotion (§7.6–7.8) — DELIVERED

The review feedback above led into the larger **governed-deployment** epic (design `ux-review.md` §7.6–7.8), now
delivered full-stack (API-first → 10 durability backends + conformance → handler → CLI → mock + UI):

- **§7.6 sources registry** — a first-class `/sources` registry consumed by the **stepped add-workflow wizard**
  (`<arazzo-catalog-add-dialog>`: Details → Sources & credentials → Administrators → Review; resolves registered
  sources with no re-upload, registers new ones, sets credentials **inline** during the wizard) and the credential
  dialog (auth + per-environment **server base URL** override, §8, derived from the source document and applied at the
  transport seam).
- **§7.7 governed environments** — `<arazzo-environments>` (list / create / administer + available-versions), with the
  subject-agnostic `<arazzo-administrators-panel environment=…>` for per-environment administration.
- **§7.8 promotion** — version availability on the catalog detail ("Available in" + **Request promotion…**), and the
  per-environment approver inbox (`<arazzo-availability-requests>`), readiness-gated.
- **Readiness as a hard gate** — the wizard refuses to register a build-from-docs workflow unless every source has a
  usable credential in some environment (usability per §13 `IsUsableBy`, not mere presence); the upload path defers to
  the **CLI** `catalog add`, which runs the same gate.
- The demo composes these as the **Runners / Sources / Environments / Access / Permissions / Promotions** tabs (plus Runs and Catalog).
- **Promotion matrix** — `<arazzo-availability-matrix>` (the `(version × environment)` rollout grid, with direct
  make/withdraw + request-promotion) is embedded in the catalog version detail.

The **"view" grant** (`catalog:read`) ships as the default option in `<arazzo-access-request-dialog>` (§17.3,
self-service request → approve). A *unified picker for an administrator to grant View / Operate to a **named third
party*** is a **deliberate non-goal**: the access model holds the invariant that a request can never target a third
party (only §15 administration names others), so View/Operate stay self-service and only Administer grants to others.
