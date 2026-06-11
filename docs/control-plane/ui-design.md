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

Six operations, three scope tiers (from [`README.md`](./README.md) / the OpenAPI document):

| Operation | HTTP | Scope | Kit surface |
|-----------|------|-------|-------------|
| `listRuns` | `GET /runs?status&workflowId&limit&pageToken` | `runs:read` | `<arazzo-runs-table>` |
| `getRun` | `GET /runs/{runId}` | `runs:read` | `<arazzo-run-detail>` |
| `resumeRun` | `POST /runs/{runId}/resume` | `runs:write` | `<arazzo-resume-dialog>` |
| `cancelRun` | `POST /runs/{runId}/cancel` | `runs:write` | `<arazzo-cancel-button>` |
| `deleteRun` | `DELETE /runs/{runId}` | `runs:purge` | `<arazzo-run-detail>` (guarded action) |
| `purgeRuns` | `PURGE /runs?olderThan&limit` | `runs:purge` | `<arazzo-purge-dialog>` |

Key model facts the UI renders:

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
│ Layer 2   <arazzo-control-plane>   reference panel / sample  │
│           composes Layer 1; owns layout, filters, routing    │
├────────────────────────────────────────────────────────────┤
│ Layer 1   <arazzo-runs-table> <arazzo-run-detail>            │
│           <arazzo-resume-dialog> <arazzo-cancel-button>      │
│           <arazzo-purge-dialog>  <arazzo-status-badge>       │
│           standalone custom elements (Shadow DOM)            │
├────────────────────────────────────────────────────────────┤
│ Layer 0   ArazzoControlPlaneClient   (no DOM)                │
│           6 ops · keyset paging · problem+json → errors      │
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

---

## Layer 2 — `<arazzo-control-plane>`

The reference panel and the sample's run screen. Composes Layer 1 into a master/detail layout:

```html
<arazzo-control-plane base-url="/arazzo/v1" scopes="runs:read runs:write"></arazzo-control-plane>
<script type="module">
  import './arazzo-control-plane.js';                 // registers all elements
  document.querySelector('arazzo-control-plane')
    .authProvider = async () => `Bearer ${await app.getAccessToken()}`;
</script>
```

- Left: `<arazzo-runs-table>` with a filter bar (status chips + workflowId search) and auto-refresh toggle.
- Right: `<arazzo-run-detail>` for the selected run, wiring its `Resume`/`Cancel`/`Delete` to the dialogs.
- Toolbar: a guarded **Purge** entry (only if `scopes` includes `runs:purge`).
- Owns one Layer-0 client, builds it from `base-url` + `authProvider`, and passes it to every child — so
  auth and base-URL are configured exactly once.

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
│  ├─ arazzo-client.js                 Layer 0 — ArazzoControlPlaneClient, ProblemError
│  ├─ arazzo-control-plane.js          Layer 2 — registers everything; the panel
│  ├─ arazzo-kit.css                   optional shared theme tokens
│  └─ components/                      Layer 1 — status-badge, runs-table, run-detail,
│                                        resume-dialog, cancel-button, purge-dialog, base.js
├─ demo/                              ← DEV-ONLY sample (not published)
│  ├─ index.html                       live demo wired to the mock
│  ├─ mock-api.js                      in-memory control plane (seeded runs, all 6 ops, problem+json)
│  └─ favicon.svg                      the Corvus mark
└─ test/                             ← DEV-ONLY (not published): node:test + @web/test-runner + Playwright
```

Each deliverable file is a standalone ES module importing only its siblings — no bundler, no transpile. A
consumer can `import` one component, or the whole panel, from npm, a CDN, or their own static host.

## Dev / mock harness
`demo/mock-api.js` implements the six operations in memory (seeded with runs in every status, including a
faulted run with a fault record and a suspended run with timer/message waits) and returns RFC 9457 errors so
the error/empty/loading paths and the resume `409` are all exercisable with **no server**. `demo/index.html`
mounts `<arazzo-control-plane>` against it. This is the "open it and it works" quick-start entry point.

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
