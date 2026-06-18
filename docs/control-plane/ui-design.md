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
(from [`README.md`](./README.md)). **Runs** and the **workflow catalog** ship today; **source credentials**
and **workflow administration** are the surfaces being added (their components are designed below).

### Runs — `runs:read` / `runs:write` / `runs:purge`

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listRuns` | `GET /runs?status&workflowId&limit&pageToken` | `runs:read` | `<arazzo-runs-table>` |
| `getRun` | `GET /runs/{runId}` | `runs:read` | `<arazzo-run-detail>` |
| `resumeRun` | `POST /runs/{runId}/resume` | `runs:write` | `<arazzo-resume-dialog>` |
| `cancelRun` | `POST /runs/{runId}/cancel` | `runs:write` | `<arazzo-cancel-button>` |
| `deleteRun` | `DELETE /runs/{runId}` | `runs:purge` | `<arazzo-run-detail>` (guarded action) |
| `purgeRuns` | `PURGE /runs?olderThan&limit` | `runs:purge` | `<arazzo-purge-dialog>` |

### Workflow catalog — `catalog:read` / `catalog:write` / `catalog:purge`

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `searchCatalog` · `listCatalogVersions` · `getCatalogVersion` | `GET /catalog[/{base}[/versions/{n}]]` | `catalog:read` | `<arazzo-catalog-table>`, `<arazzo-catalog-detail>` |
| `getCatalogPackage`/`Workflow`/`Source`/`WorkflowSchemas` · `validateCatalogValue` | `GET …/{package,workflow,sources/{name},schemas}` · `POST …/validate` | `catalog:read` | `<arazzo-catalog-detail>`, `<arazzo-value-editor>` |
| `addCatalogVersion` | `POST /catalog/{base}/versions` | `catalog:write` | `<arazzo-catalog-add-dialog>` |
| `updateCatalogVersion` · `obsoleteCatalogVersion` | `PATCH …/versions/{n}` | `catalog:write` | `<arazzo-catalog-detail>` (guarded) |
| `deleteCatalogVersion` · `purgeCatalog` | `DELETE …/versions/{n}` · `PURGE /catalog` | `catalog:purge` | `<arazzo-catalog-detail>`, panel toolbar |

### Source credentials — `credentials:read` / `credentials:write` *(in progress)*

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listCredentials` · `getCredential` | `GET /credentials[/{source}/{env}]` | `credentials:read` | `<arazzo-credentials-table>`, `<arazzo-credential-detail>` |
| `createCredential` · `updateCredential` · `deleteCredential` | `POST /credentials` · `PUT`/`DELETE /credentials/{source}/{env}` | `credentials:write` | `<arazzo-credential-dialog>` |

### Workflow administration — `administrators:read` / `administrators:write` *(in progress)*

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listAdministrators` | `GET /administrators/{base}` | `administrators:read` | `<arazzo-administrators-panel>` |
| `addAdministrator` · `removeAdministrator` · `transferAdministration` | `POST .../members` · `DELETE .../members/{dim}/{val}` · `PUT /administrators/{base}` | `administrators:write` | `<arazzo-administrators-panel>` |

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
│           credentials/administrators (in progress)           │
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
await client.listRuns({ status, workflowId, limit = 100, pageToken });   // → { runs, nextPageToken }
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

// catalog:read — search + per-version documents
await client.searchCatalog({ text, baseWorkflowId, workflowIdPrefix, tags, status, owner, pageToken });
for await (const page of client.searchCatalogPaged({ … })) { … }
await client.listCatalogVersions(baseWorkflowId);                     // → { versions, nextPageToken }
await client.getCatalogVersion(baseWorkflowId, n);                    // → CatalogVersion | throws 404
await client.getCatalogPackage(baseWorkflowId, n);                    // → ArrayBuffer (zip)
await client.getCatalogWorkflow(baseWorkflowId, n);                   // → Arazzo document
await client.getCatalogWorkflowSchemas(baseWorkflowId, n);           // → baked schema metadata
await client.getCatalogSource(baseWorkflowId, n, sourceName);
await client.validateCatalogValue(baseWorkflowId, n, target, value); // → { valid, errors? }
// catalog:write / catalog:purge
await client.addCatalogVersion({ baseWorkflowId, package | workflow+sources, owner, tags });
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

// administrators:read / administrators:write — identities named by the {dimension,value} grant
await client.listAdministrators(baseWorkflowId);                          // → { administrators: [{dimension,value}] }
await client.addAdministrator(baseWorkflowId, { dimension, value });      // → AdministratorList | 400 | 403 | 409
await client.removeAdministrator(baseWorkflowId, dimension, value);       // → AdministratorList | 403 | 409
await client.transferAdministration(baseWorkflowId, { administrators }); // → AdministratorList | 400 | 403 | 409
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
| **Attributes** | `base-url`, `status` (filter), `workflow-id` (filter), `page-size` (default 100), `poll` (ms; auto-refresh off by default), `selectable` |
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
| **Events** | `run-changed {detail:{run}}` (after an action), `error`, `close` |
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

### Catalog (`/catalog`)

The workflow catalog browse/govern surface — one row per workflow with a version switcher.

#### `<arazzo-catalog-table>`
Searches catalogued versions (text · base id · `workflowIdPrefix` · tags · status · owner), keyset-paged.

| | |
|---|---|
| **Attributes** | `base-url`, `text`, `workflow-id-prefix`, `status`, `page-size`, `poll`, `selectable` |
| **Properties** | `.client`, `.filters` |
| **Events** | `version-selected {detail:{version}}`, `loaded {detail:{count, hasMore}}`, `error` |

Columns: `baseWorkflowId` · latest version · status · `runnable` · owner · tags. Server-side `workflowId`
prefix search is index-backed.

#### `<arazzo-catalog-detail>`
One workflow's versions and documents, plus governance actions. A version switcher selects a version; renders
its metadata (`owner`, tags, status, `runnable`, baked `credentialStatus`), and offers the document downloads
(`package`/`workflow`/`sources`/`schemas`) and a typed-value **validate** (via `<arazzo-value-editor>` against
the baked schemas). Guarded actions: **update** governance metadata + **obsolete** (`catalog:write`),
**delete** a version (`catalog:purge`, confirmed). Emits `version-changed` / `version-deleted`.

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
| **Attributes** | `base-url`, `status` (filter: `valid`/`expiring`/`expired`), `source` (filter), `poll` (ms) |
| **Properties** | `.client`, `.filters = { status, source }` |
| **Events** | `credential-selected {detail:{binding}}`, `loaded {detail:{count, expiring, expired}}`, `error` |
| **Parts** | `table`, `row`, `status`, `expires`, `filters` |

Columns: source · environment · authKind · **`credentialStatus`** (a status badge — `valid`/`expiringSoon`/`expired`)
· `expiresAt` · usage grants. A footer counts expiring/expired; `status`/`source` filter client-side. No secret column
exists.

#### `<arazzo-credential-detail>` / `<arazzo-credential-dialog>`
Detail shows the binding's references, config, lifecycle (`expiresAt`/`rotatedAt`/status), and management/usage scopes.
The create/edit dialog edits **references and metadata** — role-named `secretRef` rows, config key/values, auth kind,
optional `expiresAt`, usage grants — and rejects a value that isn't a well-formed `secretRef` *before* submit (the same
boundary the server enforces, so a secret can't be smuggled in). **Edit is a merge** mirroring the CLI: re-pointing a
reference is a rotation and stamps `rotatedAt`; unspecified fields are preserved. `credentials:write` gates create/edit/
delete; delete is behind a confirm.

#### `<arazzo-administrators-panel baseworkflowid="…">`
The administrator set for one base id. Lists the `{dimension, value}` identities; **add** (a `{dimension,value}` form),
**remove** (per-row, refused for the last — surfaces `409`), and **transfer** (replace the whole set). Non-disclosing:
a non-administrator caller's mutation is a `403` shown as a plain "you are not an administrator of this workflow"
banner, never a leak of who is. `administrators:write` gates mutations.

---

## Layer 2 — reference panels

Each panel composes Layer 1 into a master/detail screen, owns **one** Layer-0 client (built from `base-url` +
`authProvider`) shared with every child, and gates actions by `scopes`. Two ship today; the credential and
administration panels are added with their surfaces.

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
- Toolbar: a guarded **Add version** entry (`<arazzo-catalog-add-dialog>`, `catalog:write`).

### Credentials & administration panels *(in progress)*
- A credentials panel pairs `<arazzo-credentials-table>` (status worklist) with `<arazzo-credential-detail>` /
  `<arazzo-credential-dialog>` for create/edit/rotate (`credentials:read`/`write`).
- An administration view embeds `<arazzo-administrators-panel>` for a selected base workflow id
  (`administrators:read`/`write`).

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
- **Accessibility:** semantic table, dialogs are focus-trapped `role="dialog"` with `Esc`/backdrop close,
  status conveyed by text + colour (not colour alone), full keyboard paths.
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
│  ├─ arazzo-client.js                 Layer 0 — ArazzoControlPlaneClient, ProblemError (run·catalog·credential·administrator ops)
│  ├─ arazzo-control-plane.js          Layer 2 — run-management panel (registers everything)
│  ├─ arazzo-catalog.js                Layer 2 — catalog browse/govern panel
│  ├─ workflow-package.js              build/inspect a package archive in-browser (./workflow-package)
│  ├─ arazzo-kit.css                   optional shared theme tokens
│  └─ components/                      Layer 1 — base.js + status-badge; runs: runs-table, run-detail,
│                                        resume-dialog, cancel-button, purge-dialog, value-editor,
│                                        workflow-id-input, workflow-step-picker; catalog: catalog-table,
│                                        catalog-detail, catalog-add-dialog; (credentials/administrators in progress)
├─ demo/                              ← DEV-ONLY sample (not published)
│  ├─ index.html                       live demo wired to the mock
│  ├─ mock-api.js                      in-memory control plane (seeded runs + catalog, problem+json)
│  └─ favicon.svg                      the Corvus mark
└─ test/                             ← DEV-ONLY (not published): node:test + @web/test-runner + Playwright
```

Each deliverable file is a standalone ES module importing only its siblings — no bundler, no transpile. A
consumer can `import` one component, or the whole panel, from npm, a CDN, or their own static host.

## Dev / mock harness
`demo/mock-api.js` implements the run and catalog operations in memory (seeded with runs in every status,
including a faulted run with a fault record and a suspended run with timer/message waits, plus catalogued
workflow versions) and returns RFC 9457 errors so the error/empty/loading paths and the resume/conflict `409`s
are all exercisable with **no server**. `demo/index.html` mounts the panels against it. This is the "open it
and it works" quick-start entry point. (The credential and administration surfaces extend the mock as they
land.)

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

**Status:** #2 done · #6 done · #1 done (store-level keyset pagination across all 10 backends + handler + spec +
regen + JS client/table/mock + conformance; in-process backends InMemory/SQLite validated, container backends
compile and await the podman conformance pass) · #5 next. Per-backend tie-breaker: the tag *discriminator* column
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
3. **#5 — Source bindings in the New Workflow dialog.** When a version is added, its package declares named
   sources; the add dialog lets the operator set up a credential binding per source (reusing the auth-kind-driven
   slot UI) and creates them via `/credentials` after the version lands — completing the per-workflow governance
   hub (Security + Sources together).

Also captured as work items: **mTLS source credentials** (design §13.1) and the **per-kind guided Config** fields
(the non-secret config each auth kind reads — `apiKey` header-name/location, `basic` username, `oauth2`
tokenUrl/clientId/scope), the analogue of the guided `secretRef` slots.
