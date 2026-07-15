# Arazzo Workflow Designer — design

A **design-test-and-validate environment** for Arazzo workflows, delivered as reusable web components
in the existing kit (`web/arazzo-control-plane-ui`). An author finds or adds OpenAPI/AsyncAPI sources,
browses their operation surface to create steps, edits success/failure handling and criteria with
proper editor support, manages inputs/outputs through the schema-driven typed forms, exercises the
workflow interactively against a mock transport and virtual clock, captures that behaviour as
scenarios, and publishes to the catalog with the evidence of successful validation. Terms are as
defined in [`UBIQUITOUSLANGUAGE.md`](./UBIQUITOUSLANGUAGE.md).

Status: **in delivery**. §14 sequences the slices and carries per-slice status; slice 1
(workspace core: CRUD, validate, workspace table, designer shell) is built.

## 1. Goals & non-goals

**Goals**

- **Cover the full Arazzo 1.1 capability surface.** Everything the schema can express is editable:
  all step binding kinds (OpenAPI operation, nested workflow, AsyncAPI channel with
  `action`/`correlationId`/`timeout`), all criterion types (`xpath` round-trips but is flagged —
  the runtime does not evaluate it), workflow-level and step-level actions, reusable components,
  payload replacements, `querystring` parameters, `dependsOn`, multiple workflows per document.
- **The design surface is an instrument, not an illustration.** The same canvas that authors the
  workflow runs it: debug sessions step the workflow live, light the taken path, expose paused
  state, and let the author inject triggers and advance the virtual clock interactively.
- **Working copies keep the catalog clean.** Iterating during development saves the document (and
  its scenarios) durably without ever minting a catalog version; *publish* is a deliberate act.
- **Test scenarios are first-class and travel with the workflow.** Scenarios are edited alongside
  the working copy, carried forward when a new version of a published workflow is edited, run as a
  suite, and their successful execution is recorded as evidence at publish.
- **Zero-build kit citizenship.** Standards-only custom elements extending `ArazzoElement`; Shadow
  DOM; `--arazzo-*` token theming; a Layer-0 client extension; events over navigation; loading /
  empty / error states; scope-gated actions; mock-API demo coverage. No framework runtime.
- **Schema-typed end to end.** Value editing uses the baked-schema TypeDescriptor forms
  (`<arazzo-value-editor>`); expression editing gets completions computed from the *actual* resolved
  types of the working copy.

**Non-goals (this epic)**

- Not a replacement for the text editor: the text mode and the design surface are peers over one
  document model; neither is a downgraded mirror of the other.
- Real-time collaboration ships later, but the document model is **collaboration-ready from day
  one** (decided 2026-07-04 — this reverses an earlier non-goal): edits are identity-addressed
  operations with inverses, never snapshots, so the realtime transport is an addition, not a
  rewrite (§5.2). Working copies keep etag concurrency for whole-document saves; Git covers
  asynchronous multi-author flows.
- No arbitrary-API live calls from the designer. Simulation talks to the mock transport only;
  running against real environments stays the runs surface's job.
- No bundled IdP or GitHub credential UI beyond the control-plane-brokered flow (§12); the kit stays
  auth-agnostic.

## 2. Prior art

[Jentic's Arazzo Editor](https://jentic.com/product/arazzo-editor) (hosted product) and
[`@jentic/arazzo-ui`](https://github.com/jentic/jentic-arazzo-tools) (Apache-2.0 viewer, React +
React Flow v11 + mermaid) validate form-based editing with a live diagram. We deliberately go
beyond that model rather than copying it:

| Jentic | This design |
|--------|-------------|
| Diagram is output-only (forms in, picture out) | The surface is bidirectional and is also the debugger |
| No execution semantics in the editor | Deterministic simulation: run, step, breakpoints, time-travel |
| Untyped/JSON value entry | Baked-schema typed forms and typed expression completions |
| No testing story | Scenarios, recording, suite runs, publish-with-evidence |
| React runtime, bundler required | Zero-build custom elements in the existing kit |
| OpenAPI only, Arazzo 1.0.1 export | OpenAPI + AsyncAPI, Arazzo 1.1, catalog/governance integrated |

## 3. UX concept

### 3.1 Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│ Toolbar: workflow switcher · Save · Validate · ▶ Run … · Publish…    │
├──────────┬──────────────────────────────────────────┬────────────────┤
│ Sources  │  Design surface        ◄►  Text (CM6)    │ Inspector      │
│ &        │                                          │ (contextual:   │
│ operation│  [diagram area: steps, edges,            │  document /    │
│ browser  │   defaults layer, debug overlay]         │  workflow /    │
│          │                                          │  step / edge / │
│ (rail,   │                                          │  action /      │
│ collapsi-│                                          │  scenario)     │
│ ble)     ├──────────────────────────────────────────┤                │
│          │ Bottom tray: Problems · Scenarios ·      │                │
│          │ Debug (controls, context, trace)         │                │
└──────────┴──────────────────────────────────────────┴────────────────┘
```

- **Design surface ↔ Text** are tabs (or a split) over one shared document model (§5.3). Edits in
  either reflect in both; selection is synchronized (select a step on the canvas → cursor lands on
  it in text, and vice versa).
- **Inspector** (right bar) renders the editor for the current selection. Nothing is editable *only*
  on the canvas; the canvas is direct manipulation over the same properties the inspector shows.
- **Bottom tray** hosts Problems (validation diagnostics, click-to-navigate), Scenarios (the
  suite), and Debug (controls + context explorer + trace) — the tray expands during a debug session.

### 3.2 Authoring on the surface

- **Steps from operations.** Drag an operation from the source browser onto the surface (or click
  "+ Step") to create a step bound to it, pre-populated with required parameters from the operation
  descriptor. Steps render as cards: method/channel badge, `stepId`, source name, a one-line
  operation summary, and status chips (breakpoint, problems, outputs count).
- **Edges are semantics, not decoration — one grammar: an action is an edge to a target.**
  **Start and end render as pseudo-nodes** (projection-only, reserved ids `#start`/`#end`, never
  written into the document): the entry edge leaves start; every `end` action *and* the implicit
  fall-off-the-last-step completion land on the end terminal, so "how can this workflow finish?"
  is always visible. Sequence flow is muted; `goto` and `end` actions are explicit directional
  edges carrying their criteria labels, success/failure distinct by colour + pattern (never colour
  alone). Retry renders as a self-badge with `retryAfter`/`retryLimit`. Dragging a port onto the
  end terminal authors an `end` action; the start node is never an action target. Workflow
  `inputs` anchor to start and `outputs` to end — selecting them opens the matching inspector.
- **Inherited vs local handling is a visible layer.** Workflow-level `successActions`/
  `failureActions` render as a "defaults" layer (a halo/lane at the surface edge). A step with no
  local handlers shows ghosted inherited markers; clicking one offers **"localize here"** (copy to
  the step for editing). A step that overrides shows solid local markers with an "overrides
  defaults" affordance. This makes the Arazzo inheritance model legible at a glance.
- **Criteria on edges.** An action's `criteria` summarize on the edge label; an explicit action
  edge with no criteria is labelled *always* (ghost style) — unconditional behaviour is visible,
  not silent. Clicking the edge opens the criteria editor in the inspector.
- **Verdict vs routing — why success carries an extra layer.** `successCriteria` is the *verdict*:
  all must match for the step to succeed, and failure is defined as its complement (there is no
  `failureCriteria` — one boundary, authored once, no overlaps or gaps). `onSuccess`/`onFailure`
  are *routing* given that verdict, and at that layer the two sides are structurally identical;
  per-action criteria choose *which reaction applies*, never whether the step succeeded. The
  inspector captions each section with its role so the model is legible in-product.
- **Action order is semantics: first-match-wins.** Arazzo dispatches the *first* action in
  declaration order whose criteria all match (the runtime's `ControlFlowEmitter` emits exactly
  this; step-level actions take precedence over workflow-level defaults), so an action with no
  criteria — a **catch-all** — always matches and everything after it is dead. The designer makes
  this legible and safe: action lists reorder (▲▼) with **catch-alls pinned to the end** (no
  reorder controls, criteria'd actions cannot move below them, insertions land above them, an
  action that loses its last criterion repositions to the end); edge labels lead with their
  precedence (`1·`, `2·`) when a step has several same-kind actions; and anything that still ends
  up after a catch-all (e.g. a hand-authored document) is **flagged unreachable** — a projection
  problem, a struck-through dimmed edge, and a ⚠ row marker — never silently rewritten.
- **Drop → select → conditions.** Drawing an edge writes the action, auto-selects the new edge,
  and the inspector opens on its criteria — conditions are one keystroke away without a modal
  interrupting bulk authoring. Dropping an *identical unconditional* duplicate selects the
  existing edge instead of appending a dead action; once criteria differ, parallel edges between
  the same pair are legitimate and fan out side by side.
- **Multiple workflows** in a document appear in the toolbar switcher; a step bound to another
  workflow (`workflowId` binding) renders as a sub-workflow card that can be opened (breadcrumb
  navigation). `dependsOn` renders in a document-level overview mode.
- **Undo/redo** is document-model-level and spans both editors.

### 3.3 The debug session (the differentiator)

Deterministic simulation (compiled executor + scripted mock transport + virtual clock) makes a full
run milliseconds and exactly reproducible. The UX exploits that:

- **Run / Pause / Step / Run-to-here / Breakpoints / Stop.** Set breakpoints on steps; run a
  scenario (or an ad-hoc setup); the active step pulses; taken edges light as criteria evaluate
  (success green / failure red); untaken branches dim. While running, the run control is **Pause**
  (halts before the next step); **Stop** terminates a live session and clears the overlay. A
  *completed* run deliberately keeps its overlay for inspection — **Clear** is the explicit return
  to the clean editing surface (starting a new run also replaces it). **Step**
  executes exactly the next step and pauses again — invoked from idle it starts the session paused
  before the first step; "run to here" targets a step. *Every* pause — breakpoint, manual pause,
  or step — hands the paused context to the context explorer and expression console below for
  inspection; step and resume are both §8.2 replays, differing only in how far the stop condition
  advances (one step vs. the next breakpoint/end).
- **Virtual clock as a control.** When the run suspends on a timer, the debug controls show the
  wait and offer "advance to due" / "+1s / +1m / +1h"; retries with `retryAfter` show the same.
  Nothing waits in real time.
- **Trigger injection.** When the run suspends on a message wait (AsyncAPI receive), the debug tray
  offers an "inject message" form — typed from the channel's schema — so "what if the webhook
  arrives late / malformed / twice?" is interactive.
- **Paused-state inspection.** The context explorer shows the live execution context (`$inputs`,
  each completed step's `$steps.<id>.outputs`, the in-flight request/response) as an explorable
  tree. Hovering a completed step on the canvas shows its actual request/response and a **criterion
  truth table** (each criterion, its evaluated operands, pass/fail).
- **Expression console.** A REPL input (same highlighted editor as criteria) evaluates any runtime
  expression or JSONPath against the paused context — the fastest way to debug a criterion.
- **Time-travel.** The trace is fully recorded; a scrubber moves the canvas overlay backward and
  forward through the run. Because stepping is replay-based (§8.2), scrubbing is pure client-side
  rendering over the trace.
- **Live re-run on edit.** Editing the document (or a mock) during a session offers "re-run to the
  same point" — replay is exact, so the author iterates on a criterion against the same paused
  moment repeatedly.

### 3.4 Scenario recording

A debug session *is* scenario authoring. The session's setup (inputs, mock scripts, injected
triggers, clock advances) accumulates in the Debug tray; **"Save as scenario…"** captures it.
Expectations are promoted from observed reality: right-click a step in the trace → "expect reached"
/ "expect outputs…"; the final state offers "expect outcome Completed" and per-output criteria
pre-filled from actual values. Assertions reuse the criterion language — testing teaches the same
skills as authoring. Hand-editing scenarios in the typed forms remains available.

## 4. What the server must add (API-first)

The kit consumes the control-plane OpenAPI contract; per the house rule, every feature below is
authored in `arazzo-control-plane.openapi.json` first, then stores/handlers/CLI, then the kit.

New resource groups (names use the ubiquitous language; scopes follow the existing tier pattern):

### 4.1 Workspace (working copies) — `workspace:read` / `workspace:write`

| Operation | HTTP | Purpose |
|-----------|------|---------|
| `createWorkingCopy` | `POST /workspace/workflows` | From scratch, from an uploaded document, **or from a catalog version** (`fromBaseWorkflowId` + `versionNumber` — copies the document, its source attachments by reference, and the version's scenarios: the carry-over). |
| `listWorkingCopies` | `GET /workspace/workflows` | Keyset-paged, reach-scoped list (id, name, baseWorkflowId?, updatedBy/At, problem count). |
| `getWorkingCopy` / `updateWorkingCopy` / `deleteWorkingCopy` | `GET`/`PUT`/`DELETE /workspace/workflows/{id}` | Document + scenario container; `PUT` is etag-guarded (409 on concurrent change). Save as often as needed; no catalog interaction. |
| `validateWorkingCopy` | `POST /workspace/workflows/{id}/validate` | Full diagnostics: JSON-Schema conformance of the document, plus semantic checks — unresolved `operationId`/`operationPath`/`channelPath`, unknown `stepId` in `goto`, expression parse errors (via `ArazzoExpression.Parse`), criterion syntax, dangling component references, unreachable steps. Returns positioned diagnostics (JSON Pointer + severity) for the Problems tray and Monaco markers. |
| `getWorkingCopySchemas` | `GET /workspace/workflows/{id}/schemas` | Baked-schema TypeDescriptors recomputed for the working copy (same shape as `getCatalogWorkflowSchemas`), powering typed forms and expression completions. |
| `attachWorkingCopySource` / `listWorkingCopySources` / … | `POST`/`GET`/`DELETE /workspace/workflows/{id}/sources[/{name}]` | Attach a source per `sourceDescriptions` name: by **registry reference**, by **upload**, or by **fetch** (§4.4). The working copy resolves like a package (self-contained input to schemas/simulation). |
| `listSourceOperations` | `GET /workspace/workflows/{id}/sources/{name}/operations` and `GET /sources/{name}/operations` | The operation surface: `OperationDescriptor` / `AsyncApiChannelDescriptor` projections (id, path/channel, method/action, summary, parameters, request/response types **including documented response codes and request/message schemas** — these power the step inspector's criteria and body templates). |

Governance: a working copy is a governed resource in the environment/workflow §15 style — creating
one grants the creator administration; reach labels apply. It is deliberately *light* (no
availability, no runs, no executor persisted).

### 4.2 Scenarios — stored on the working copy; published into the package

| Operation | HTTP | Purpose |
|-----------|------|---------|
| `listScenarios` / `putScenario` / `deleteScenario` | `GET`/`PUT`/`DELETE /workspace/workflows/{id}/scenarios[/{scenarioName}]` | CRUD on the working copy's scenario set. |
| `runScenario` / `runAllScenarios` | `POST …/scenarios/{name}/run` · `POST …/scenarios/run` | Execute against the simulator; returns per-scenario `{outcome, trace, expectationResults[]}` (suite report for run-all). |

**Scenario model** — a new JSON Schema (generated types server-side; TypeDescriptors for the UI
forms; no hand-rolled records):

```jsonc
{
  "name": "payment-declined-then-retry",
  "description": "…",
  "inputs": { /* validated against the workflow's inputs schema */ },
  "mocks": [                       // per source-operation scripting (MockApiTransport surface)
    { "source": "payments", "operationId": "authorize",
      "match": { /* optional criteria over request */ },
      "responses": [               // sequence semantics; last repeats
        { "status": 402, "body": { … }, "delay": "PT0S" },
        { "status": 200, "body": { … } } ] } ],
  "triggers": [                    // message injections for AsyncAPI waits
    { "channel": "payments.events", "correlation": "$inputs.orderId",
      "payload": { … }, "at": { "afterStep": "authorize" } } ],
  "clock": { "start": "2026-01-01T00:00:00Z", "autoAdvance": true },  // advance-to-due on timer waits
  "expect": {
    "outcome": "Completed",
    "path": ["validate", "authorize", "authorize", "capture"],        // optional exact/subsequence
    "outputs": [ { "condition": "$outputs.receiptId != null" } ],     // criterion grammar
    "steps": { "authorize": { "attempts": 2 } } }
}
```

At publish, the scenario set and the evidence are written into the package as
`metadata/scenarios.json` and `metadata/evidence.json` (the `.awp` format already reserves named
metadata entries) — immutable, content-addressed, and carried to the next working copy.

### 4.3 Simulation — `workspace:read` (it mutates nothing)

| Operation | HTTP | Purpose |
|-----------|------|---------|
| `simulateWorkingCopy` | `POST /workspace/workflows/{id}/simulate` | Body: `{scenarioName | inline scenario, until?: {stepId?, occurrence?, breakpoints?[]}, overrides?: {inputs?, mocks?, triggers?, clock?}}`. Returns the **trace** up to the stop condition. |
| `simulateCatalogVersion` | `POST /catalog/{base}/versions/{n}/simulate` | Same, for published versions (re-verify evidence, explore a regression). |

**Stateless stepping (§8.2):** there is no server-side debug-session resource. Every debug command
replays from the start to a new stop condition — determinism makes the replay exact and cheap, and
the control plane stays stateless. The response's trace is complete up to the stop point, so
time-travel scrubbing needs no further calls.

### 4.4 Source acquisition — `sources:read` / `sources:write`

| Operation | HTTP | Purpose |
|-----------|------|---------|
| `fetchSourceDocument` | `POST /sources/fetch` | `{url, credential?: {sourceName, environment} | inline authKind+secretRef}` → the fetched, validated document (+ detected type/version, content digest). **Server-side fetch**: avoids browser CORS entirely and reuses the §13 credential machinery (`SourceCredentials.Http`) for authenticated spec endpoints. Does not register; the caller attaches or registers the result. |

Upload (multipart) already exists on the wizard path; the working-copy attach (§4.1) accepts the
same. Registry registration at publish follows the existing wizard readiness rules.

### 4.5 Scenario runner CLI — the CI story

The control-plane CLI gains **`scenarios run`**, CI-native and wrappable as a GitHub Action:

```
arazzo scenarios run
  --workflow ./workflows/nightly-reconcile.arazzo.json     # or --working-copy <id> · --catalog <base> --version <n>
  --sources ./specs                                        # resolve sourceDescriptions against local files/URLs
  --scenarios "./scenarios/nightly-reconcile/**/*.scenario.json"   # globbable, repeatable
  --filter "payment-*"                                     # name filter within the matched set
  --report junit=out/scenarios.xml --report json=out/suite.json
  --github-annotations                                     # ::error annotations + job-summary markdown
```

Two execution modes:

- **Standalone (default).** No control plane required: the CLI hosts the simulator in-process —
  build + compile the workflow document with its source documents, run every matched scenario
  against the mock transport and virtual clock. Everything the designer does interactively,
  headless. This is the CI mode: workflows, specs, and scenarios live in a repo; the pipeline runs
  the suite on every push/PR.
- **Remote.** `--base-url` + host-supplied auth targets a control plane's simulate endpoints (a
  working copy or a catalog version) — e.g. re-verifying a published version's evidence from a
  pipeline.

CI grade: non-zero exit on any failed expectation (or validation/compile failure); deterministic
ordering; console, JUnit XML, and JSON reports — **the JSON report is the same suite-report shape
`publish` embeds as evidence**, so a pipeline can finish with publish-with-evidence: PR merge →
suite green → publish mints the draft version.

On-disk layout: one scenario per file (`<name>.scenario.json`, schema §4.2). The Git-bound working
copy (§4.7) commits/pulls scenarios as these individual, globbable files, so the designer and the
repo/CI layout stay isomorphic.

A thin **GitHub Action** wrapper (composite action: install the dotnet tool, map inputs to flags)
ships alongside, making a versioned `uses:` reference the one-line CI story.

### 4.6 Publish & evidence — `catalog:write`

| Operation | HTTP | Purpose |
|-----------|------|---------|
| `publishWorkingCopy` | `POST /workspace/workflows/{id}/publish` | Body: `{owner, tags, requireScenarios?: true}`. The server: (1) validates the document; (2) resolves/attaches sources (registering new ones, wizard-style readiness); (3) **re-runs the full scenario suite server-side** — evidence is server-attested, never client-submitted; (4) builds the package including `metadata/scenarios.json` + `metadata/evidence.json`; (5) `AddAsync` → the new version (a **draft** until promoted). Fails 422 with the suite report if a scenario fails and `requireScenarios` is set. |

**Evidence model** (in-package + projected onto `CatalogVersion` metadata): engine + generator
versions, package content hash, per-scenario `{name, scenarioHash, outcome, pathSummary, durationVirtual,
at}`, and the suite verdict. The catalog detail renders an evidence badge ("12/12 scenarios ✓ at
publish"); `GET …/versions/{n}/evidence` serves the document.

**Promotion readiness (implemented):** an environment can require evidence — `readiness =
credentials ∧ (evidence.suiteGreen ∨ ¬environment.requireEvidence)`. The per-environment
`requireEvidence` flag (create/update, administrator-governed like the rest of the environment's
metadata) is default-off, so existing promotion behaviour is unchanged unless an environment opts
in. Suite-green means the attested suite ran at least one scenario and none failed; no evidence, or
an empty suite, is unevidenced and refused. Both promotion paths hit the same gate — a direct
make-available and an availability-request approval — refusing with a 409 `evidence-required`
problem.

### 4.7 GitHub integration — brokered; `workspace:write` + host-configured

GitHub's OAuth token exchange has no CORS support, so the browser cannot complete an auth flow
alone; the control plane brokers a **GitHub App** (fine-grained, repo-scoped, short-lived
user-to-server tokens):

| Operation | HTTP | Purpose |
|-----------|------|---------|
| `beginGitHubAuth` / `completeGitHubAuth` | `GET /github/auth` → redirect; callback exchanges the code server-side | Standard web-application flow; the control plane holds the App credentials and the user token (server-side session or encrypted at rest with a KMS ref — never in the browser). |
| `getGitHubStatus` | `GET /github/session` | Signed-in identity + installations + accessible repos. |
| `browseRepo` | `GET /github/repos/{owner}/{repo}/contents?ref&path` | Proxied browse for the open/import dialogs. |
| `bind` (on the working copy) | part of `PUT /workspace/workflows/{id}` | `gitBinding: {owner, repo, branch, path, specPaths?, scenariosDir?}` — a working copy may be **Git-bound**; `specPaths` maps sourceDescriptions names to spec file paths, and scenarios round-trip as individual `<name>.scenario.json` files under `scenariosDir` (the §4.5 CI layout). |
| `pullWorkingCopy` / `commitWorkingCopy` | `POST /workspace/workflows/{id}/git/{pull,commit}` | Pull: refresh document (+ bound source docs + scenarios) from the branch (etag/merge guard). Commit: write document (+ scenario files) to the branch with a message; optionally open a PR (`draft` → review flow for workflow development). |

Uses: version management of workflows *and* their OpenAPI/AsyncAPI specs during development
(branch-per-working-copy is the natural multi-author flow); importing specs from repos; optionally
pushing the published package inputs + evidence to a release branch/tag at publish. GitHub Enterprise
Server is configuration (base URL), not new design. The kit never sees a GitHub credential; it calls
the control plane.

**Identity rules (ratified).** Authorship is the signed-in user's GitHub-held git identity,
stamped by GitHub rather than composed by us; token custody is server-side per control-plane
principal; unattended actions use the App's bot identity or don't happen. Concretely:

- **Attribution — the user's.** Every user-initiated pull/commit/PR runs on that user's
  user-to-server token, so commits are authored by the human (with the "via App" badge and
  GitHub's web-flow signature on contents-endpoint commits). Never a shared service account.
- **Git identity is GitHub-held, never composed.** Commit-writing API calls **omit
  `author`/`committer`** so GitHub stamps the account's display name and configured commit email —
  respecting commit-email privacy (the `noreply` address still links to the account). The control
  plane never sets an email itself (a composed address either leaks a private one or breaks
  attribution), and the designer never offers a free-form git-identity field (self-asserted
  identity is forgeable; identity comes resolved from the authenticated principal).
- **Reach is the intersection** of the user's own repo access ∧ the App installation's repo
  selection ∧ the App's fine-grained permissions (contents, pull requests — nothing more).
- **Custody — the server's, keyed by principal.** GitHub's token exchange has no CORS, so the
  exchange must happen server-side; the user token is held per control-plane principal (session,
  or encrypted at rest with a KMS ref), unreachable from any other principal's session, and never
  sent to the browser.
- **No impersonation in either direction.** Machine work never wears a human's git identity: any
  future unattended path (e.g. pushing the published package + evidence to a release branch)
  commits as the App's own bot identity via an installation token, clearly machine-attributed.

## 5. Kit architecture (Layer 0 / 0.5 / 1 / 2)

### 5.1 Layer 0 — client extensions

`ArazzoControlPlaneClient` gains the §4 methods (workspace, scenarios, simulate, fetch, publish,
github). Same conventions: `ProblemError`, keyset paging, host-supplied auth, conformance-tested
against the OpenAPI document.

### 5.2 Layer 0.5 — `WorkflowDocumentModel` (new, DOM-free, collaboration-ready)

A shared observable model over the Arazzo document, imported by both editors and the inspector.
**Every edit is a group of identity-addressed operations, never a snapshot** — the property that
cannot be retrofitted, so it is the foundation:

- **Operations, not snapshots.** `set`/`remove`/`insert`/`move` ops address the document by its
  stable Arazzo identities (`workflows[id=…].steps[id=…].description`), not array indices —
  concurrent edits to different steps merge with no transform; indices never shear under
  concurrent insertion. Editors keep a simple API (`update(mutator)`, `applyText(text)`): an
  **identity-aware structural diff** reduces whatever they did to minimal ops — a step edited in
  text mode emits the *same* op a canvas edit would, which is also what keeps canvas positions
  and selection stable across text edits.
- **Undo/redo are local and inverse-based**: they invert *this actor's* op groups (with coalescing
  for typing bursts), never a collaborator's interleaved work — snapshot undo would revert
  everyone.
- **The transport seam, not the transport**: local groups emit as an `ops` event (actor + seq +
  label); `applyRemote(group)` applies a collaborator's ops delivered in **server total order**
  (the workspace later grows an ops relay — `POST /workspace/workflows/{id}/ops` + an SSE/WebSocket
  stream — and remains etag-guarded for whole-document saves). Same-field concurrent writes
  resolve last-writer-wins in that order; ops whose target a collaborator deleted are skipped,
  not faulted. Client-side pending-op rebasing (for latency masking) layers on later without
  changing the op shapes.
- **Layout persistence**: node positions are UI state, not document content — stored in the working
  copy's `designerState` (a sibling field, never written into the Arazzo document, never packaged,
  never part of the op stream).

### 5.3 Layer 1 — new components

All extend `ArazzoElement`; `SHARED_CSS` + `--arazzo-*` tokens; events bubble composed; explicit
loading/empty/error states; scope-gated actions. (Attribute/event tables in the style of
`ui-design.md` to be finalized per-slice; the inventory and responsibilities:)

| Element | Responsibility |
|---------|----------------|
| `<arazzo-workspace-table>` | Working copies list: open, create (blank / from version / from Git), delete. Emits `working-copy-selected`. |
| `<arazzo-design-surface>` | The diagram area (§6). Renders the graph projection; direct manipulation (add/move/connect/delete, marquee select, pan/zoom, auto-layout); debug overlay (active step, taken edges, breakpoints); a `diffState` overlay channel (added/removed/changed classes + ghost mode, §6.4); emits `selection-changed`, `step-created`, `edge-created`, `breakpoint-toggled`, `view-changed`, … |
| `<arazzo-text-editor>` | CodeMirror 6 wrapper (lazy-loaded, §7); document text mode with schema validation markers, expression token highlighting, selection sync. |
| `<arazzo-operation-browser>` | Left rail: the document's sources + each one's operation surface (search/filter; drag or click-to-add). "Add source…" opens the acquisition dialog. |
| `<arazzo-source-acquisition-dialog>` | Add a source: pick from registry · fetch from URL (+ optional credential) · upload · import from GitHub. |
| `<arazzo-step-inspector>` | Selected step: binding (operation/workflow/channel pickers), parameters (typed), `requestBody` payload via `<arazzo-payload-editor>` — **schema-driven structure with expression-capable leaves**: fields from the binding's schema (nested objects as fieldsets, type chips), every scalar an `<arazzo-expression-input>` (completions included), literals coerced to the schema's type while runtime expressions stay strings — with a full-fidelity guarded JSON view (schema-less bindings get JSON only; unknown keys are always preserved) + replacements, success criteria, local actions, outputs. **Operation-derived templates** (from `listSourceOperations`): success criteria + one failure action per documented response — with a catch-all from the documented `default`, or an explicit `unexpected-failure` fallback when the operation documents none — and a request-body/message-payload **skeleton built from the binding's schema** (structure typed for you; values, usually runtime expressions, yours to fill). Templates fill empty sections and insert above any existing catch-all without duplicating; they never overwrite. *Decided (2026-07-04):* the templated fallback is an explicit `end` action, not an absent case — an unmatched failure would otherwise **fault** the run (resumable), and for designed workflows an explicit, visible, retargetable fallback beats an invisible fault path; scenarios surface the difference in testing, and authors retarget the action (goto/retry) in one click. |
| `<arazzo-workflow-inspector>` | Selected workflow: `inputs` schema editor, workflow-level actions (the defaults layer), `outputs`, `parameters`, `dependsOn`. |
| `<arazzo-schema-editor>` | Typed JSON-Schema authoring for a workflow's `inputs` and the components library's input schemas (§5.3a): property rows (type/format/required), first-class `oneOf`/`anyOf`/`allOf` combiner nodes, advanced nodes for constructs the visual tier does not render (preserved and JSON-editable), a Form \| JSON toggle over the guarded editor, a live typed preview (a `<arazzo-value-editor>` on the authored schema), and a shared-type reference picker against `components.inputs`. |
| `<arazzo-document-inspector>` | `info`, source descriptions, `components` (reusable library management). |
| `<arazzo-criteria-editor>` | An ordered list of criterion rows: type picker (`simple`/`regex`/`jsonpath`; a version select only where a real choice exists), `context` expression input, condition editor with syntax highlighting + live server validation. `xpath` is schema-valid Arazzo but the runtime does not evaluate it — never offered for new criteria; preserved and flagged ⚠ when a document carries it. |
| `<arazzo-expression-input>` | Single-line highlighted runtime-expression / JSONPath editor with completions from the schema context (§7.2). Reused by criteria, parameters, outputs, payload replacements, scenario expectations. |
| `<arazzo-action-editor>` | One success/failure action: type (`end`/`goto`/`retry`), target step/workflow picker, retry settings, criteria; "localize/revert to inherited" affordances. |
| `<arazzo-scenario-panel>` | Scenario suite: list + per-scenario status chips, run one/all, open editor, "save session as scenario". |
| `<arazzo-scenario-editor>` | Typed scenario editing: inputs (typed form from the workflow inputs schema), mocks (per-operation response scripting, response bodies typed from the *source* schemas), triggers, clock, expectations (criteria editors). |
| `<arazzo-debug-controls>` | Run/pause/step/run-to/continue/stop, breakpoint list, virtual-clock control (advance to due / +Δ), trigger injection, session status. |
| `<arazzo-context-explorer>` | Paused-context tree (`$inputs`, `$steps.*`, request/response) + the expression console. |
| `<arazzo-trace-viewer>` | The recorded trace: step timeline with per-step request/response and criterion truth tables; the time-travel scrubber; click-to-navigate to canvas/inspector. |
| `<arazzo-evidence-badge>` | Evidence summary for a catalog version (suite verdict, count, at); embedded by `<arazzo-catalog-detail>`. |
| `<arazzo-github-dialog>` | Sign-in status, repo/branch/path binding, pull/commit (+PR) actions for a Git-bound working copy. |
| `<arazzo-workflow-compare>` | Reusable side-by-side / overlay / text comparison of any two workflow versions (§6.4). Paints the visual diff via each surface's `diffState`, a grouped change list with Prev/Next, and — with `mergeTarget` on a side — Take/Keep verbs emitting `change-accepted` / `merge-text-applied` for the host to apply to its one model. Opened by the Git history browser and catalog-detail's "Compare with version…". |

Reused as-is: `<arazzo-value-editor>` (typed forms), `<arazzo-workflow-picker>`,
`<arazzo-grantee-picker>` (workspace administration), `<arazzo-catalog-add-dialog>` patterns
(publish dialog), pager, status badge, confirm dialog.

### 5.3a The schema editor (§15 item 8 — resolved 2026-07-13)

`<arazzo-schema-editor>` authors a workflow's `inputs` and the components library's input schemas
as a typed form rather than a guarded JSON textarea. It edits a **subset** of JSON Schema visually
and stays **lossless** over the rest: constructs the visual tier does not render survive untouched
and stay JSON-editable, so a round-trip never disturbs unrendered keywords or key order elsewhere.

- **Typed nodes.** Property rows carry type, format, required, enum/const, and a typed `default`
  edited by the same `<arazzo-value-editor>` machinery. Object and array nodes nest.
- **First-class combiners.** `oneOf`/`anyOf`/`allOf` are authored as combiner nodes, not dropped to
  raw JSON. The renderer (`schema-descriptor.js` `normalizeDescriptor`) and the server's baked-schema
  generator (`WorkflowSchemaMetadataGenerator`) apply the **same** normalization so both consumers
  agree: `oneOf`/`anyOf` become a union variant picker (null branch collapses to nullable, a lone
  branch unwraps, an explicit OpenAPI `discriminator` is honoured), and a **simple `allOf` merges**
  into one object descriptor. The simple-`allOf` rule: every branch is an object schema contributing
  only `properties`/`required` (plus `type: object`/`title`/`description`); the merge is the union of
  `properties` and of `required`, with a same-key overlap allowed only when the two subschemas are
  structurally equal (**order-insensitive**, so the two normalizers cannot disagree on reordered
  keys). A conflict, a non-object branch, or a branch keyword beyond that set makes it not-simple and
  falls back to a raw typeless descriptor, never a guessed last-writer-wins merge.
- **Advanced nodes.** A keyword the form does not render, `not`, `patternProperties`, `$ref` with
  siblings, and the like show as a labelled advanced row that preserves the raw subschema and opens
  the JSON tier. Nothing is destroyed.
- **JSON tier.** A Form | JSON toggle over the shared guarded editor (`wireGuardedJson`): edits apply
  only when they parse; per host, a blank commit either deletes (workflow inputs) or holds the last
  valid value (document inspector). The form tier never deletes.
- **Server validation pass.** `/workspace/workflows/{id}/validate` gains a fourth pass that
  meta-validates each embedded `inputs` schema against the JSON Schema 2020-12 meta-schema with the
  product's own validator, emitting positioned findings (`/workflows/N/inputs/…`,
  `/components/inputs/K/…`) into the Problems tray, plus a dangling-local-`$ref` finding that nothing
  else detects. This is authoritative; the client tier is authoring convenience.
- **Reference picker.** The type menu leads with the shared library: reference an existing
  `components.inputs` type, extract the current node into a new shared type, or author inline. A
  reference renders as a reference row (open in library, or detach to an inline copy); a dangling
  target is a problem row. Referencing shared types is the simple default for nested schemas. The
  client renderer does not resolve `$ref`s (no document root at its seam), so the run dialog and
  expression completions consume the server's **baked** ref-resolved schemas.
- **External schema references (#94, resolved 2026-07-15).** An inputs schema may reference an
  external JSON Schema document as `schemas/<name>#<pointer>`. The document is a `/sources` registry
  entry of the new `jsonschema` type, attached to the working copy under `<name>` — deliberately
  **never** a `sourceDescription` (the Arazzo spec pins that enum to arazzo/openapi/asyncapi), so
  the integrity pass exempts it from the declared-source cross-check and it carries no operation
  surface. The validate pass's dangling-`$ref` walk errors on a `schemas/<name>` reference whose
  named document is not attached. The type menu gains an "External schemas" group (one option per
  `$defs` entry, or the document root); an external reference renders its own row, flagged when
  unattached, with no detach (the target lives outside the document). At publish, the attachment
  rides the package like every source (inside the content hash); the executor build threads the
  undeclared package entries to the generator, which registers each with the inputs-model resolver
  as a sibling of the Arazzo document (`…/arazzo/schemas/<name>`) so the **relative** reference
  resolves with no rewriting. The baked path leaves an external `$ref` node raw (same degradation
  as an unresolved library ref), so typed forms fall back to the JSON tier for that node.

### 5.4 Layer 2 — `<arazzo-workflow-designer>`

The composed panel: owns one client, the document model, and the layout (§3.1); wires selection →
inspector, debug state → surface overlay + tray; hosts the toolbar (save/validate/run/publish,
dirty + etag-conflict indicators). Ships in the package exports and gets a demo tab with the mock
API extended to cover the workspace/simulate surface (persona-gated like everything else).

## 6. The design surface

### 6.1 Technology decision: a first-party SVG surface (+ dagre for auto-layout)

**Decision: build the design surface ourselves** — hand-authored SVG inside the component's shadow
root, with `@dagrejs/dagre` (MIT, pure JS, vendored/lazy-loaded ESM) for layered-DAG auto-layout
(ELK.js only if edge-routing needs outgrow it). Estimated 1.5–3k LOC for the §6.2 scope.

Requirements it satisfies: runs inside an open Shadow DOM without event/measurement breakage; no
framework runtime (the kit is zero-build loose ESM); editable node-and-edge graph
(create/move/connect/delete, ports, selection, marquee, pan/zoom); debug overlays cheaply
re-styleable per frame; `--arazzo-*` CSS-token theming; permissive licensing.

Why not a library — an eleven-library comparative survey (React Flow/xyflow, Rete.js v2, JointJS,
maxGraph, Sprotty, bpmn-js/diagram-js, GoJS, Drawflow, LiteGraph.js, Cytoscape.js, @antv/x6) found
that the **only editors with positive, code-level shadow-DOM evidence and a full editing feature
set are GoJS and React Flow — both disqualified** (proprietary $4k+/dev canvas renderer that fights
CSS-token theming; React runtime):

- **@antv/x6** (the strongest MIT feature match) is a maintainer-confirmed non-goal: shadow-DOM
  support declined, event hit-testing uses bare `document.elementFromPoint` in the current bundle
  ([antvis/X6#1082](https://github.com/antvis/X6/issues/1082), closed Aug 2025 as out of scope).
- **GoJS** is the only fully shadow-DOM-safe complete editor (bundle-verified shadow-piercing
  hit-testing) but is commercial and canvas-rendered
  ([forum confirmation](https://forum.nwoods.com/t/drag-from-palette-to-diagram-is-not-showing-the-shape/16856),
  [pricing](https://nwoods.com/sales)).
- **Drawflow** is architecturally right (container-scoped events) but dormant since 2024, no
  undo/validation/layout — adopting it means owning a fork, at which point first-party code
  designed for this kit is strictly better.
- **LiteGraph.js** appends its menus/dialogs to `document.body` — outside the shadow root.
- **Cytoscape.js** has exemplary shadow-DOM stewardship
  ([cytoscape#3273](https://github.com/cytoscape/cytoscape.js/issues/3273), fixed in core) but is a
  visualization/analysis library: no ports, no undo, canvas-only theming; its edge-editing
  extension is dormant. Right choice for a read-only *viewer*, not the designer.
- **JointJS `@joint/core`** (zero-dep ESM) is the honorable library mention, spike-gated on its
  `document.elementFromPoint` touch/snap paths.

Positive reasons, beyond elimination: the kit's established ethos is first-party, zero-dependency
code (hand-rolled REST client, `.awp` container, schema-form generator); the graph is a **modest
layered DAG** (steps + success/failure/goto edges + defaults layer), not a free-form diagram; and
the debugger requirement (§3.3) inverts the usual trade — overlay states become CSS classes on SVG
elements we own (`pulse` animation, edge lighting, badges, breakpoint markers, all themed by
`--arazzo-*` tokens natively), which is exactly where third-party abstractions leak.

**Recorded fallback:** if editing scope outgrows the bespoke surface (free-form diagramming,
nested containers, exotic routing), **Rete.js v2 + `@retejs/lit-plugin`** is the best open-source
path (merged shadow-DOM fixes, shadow-native Lit rendering, no framework compiler); accept the Lit
runtime and single-maintainer risk consciously at that point.

### 6.2 Graph projection rules (library-independent)

- Node per step, in declared order, bracketed by the **start/end pseudo-nodes** (reserved ids
  `#start`/`#end`; projection artifacts only — never written into the Arazzo document). Start
  anchors the workflow `inputs`, end anchors its `outputs`.
- Implicit sequence edges (muted): start → first step, step → next step, last step → end; elided
  after a step whose unconditional success action ends or gotos.
- Explicit action edges: `goto` to a step, `end` to the end terminal — success/failure distinct by
  colour *and* line pattern, criteria summarized on the label; `retry` a self-badge. One end
  terminal, not a success/failure pair: the edge's kind already carries that context without
  inventing outcome semantics Arazzo does not define.
- The workflow-defaults layer renders inherited actions once (edge halo) + ghosted per-step markers.
- Sub-workflow steps (workflowId binding) render as openable composite nodes.
- Debug overlay states: `idle | active(pulse) | done-success | done-failure | skipped | breakpoint`;
  the end terminal lights with the run outcome.
- Selection model: node, edge, defaults-layer, start (→ inputs), end (→ outputs), or background
  (→ workflow inspector).

### 6.3 Surface architecture (why bespoke stays clean)

The discipline that keeps a first-party canvas from becoming an accidental framework: the surface
is **five small, separately testable layers with one data contract between them** — the §6.2 graph
projection. Nothing outside `<arazzo-design-surface>` knows SVG exists; nothing inside it knows
Arazzo exists.

| Layer | Nature | Notes |
|-------|--------|-------|
| **Projection** | Pure function: workflow → `{nodes, edges, defaultsLayer, diagnostics}` | DOM-free, lives with the document model (§5.2); unit-tested exhaustively against Arazzo fixtures. |
| **Layout** | Pure data: dagre positions ⊕ `designerState` manual overrides | dagre lazy-loaded only when auto-layout is invoked; output is plain `{x,y}` per node + edge points. |
| **Renderer** | Keyed reconciliation of the projection onto SVG groups | Every visual state — selection, problems, debug overlay — is a CSS class on an owned element, themed by `--arazzo-*` tokens. No imperative styling. |
| **Interaction** | A pointer state machine: `idle → pan · drag-node · draw-edge · marquee` | `setPointerCapture` + listeners on the component's own shadow root only. Coordinate math via the surface's own viewBox transform. **`document.elementFromPoint` and document-level listeners are banned** — the two APIs behind every shadow-DOM failure in the library survey simply do not appear. |
| **Events out** | The kit contract | `selection-changed`, `step-created`, `edge-created`, `breakpoint-toggled`, … — the same events a library adapter would emit, so the recorded Rete fallback (§6.1) would replace one component's internals, not ripple. |

Scope guard: the surface implements the §6.2 vocabulary and nothing else — no generic shapes, no
free-form containers, no plugin system. Precedent for this size and style already in-house: the
773-line schema-form generator (`value-editor.js`), the hand-rolled `.awp` container, and the
playground's bespoke SVG block renderer. Debug overlays are the projection re-rendered with trace
decorations — there is no second rendering path to keep in sync.

### 6.4 Comparison & the diff overlay (§15-8d — resolved 2026-07-13)

`<arazzo-workflow-compare>` compares any two workflow versions. It opens from the Git pane's history
browser (working copy vs a commit), from catalog-detail's "Compare with version…" affordance (two
catalog versions, §9.11 below), and from any host that supplies a document pair. On top of the plain
side-by-side it paints a **visual diff** and, when a side is the working copy, drives an **interactive
merge**. It is Layer 1: ids in, classes and events out, and it never mutates a document.

**The diff model — `workflow-diff.js` (Layer 0.5, DOM-free).** `diffWorkflowPair(left, right, {ids})`
classifies the pair on top of the §6.2 projection and the document model's identity-aware structural
`diff()` — it never writes a second structural differ. Matching rules:

- **Steps.** Identity by `stepId` first; then binding-gated **rename** detection over the residue — two
  steps pair only when they share a binding key (`operationId` / `operationPath` / `channelPath`+action
  / `workflowId`), scored by field-group similarity, assigned greedily with deterministic tie-breaks.
  Content is read from the rename-normalized identity-list `diff`, so a **reorder is a `move`, never a
  `changed`** (its seq edges and change-list entry carry it), and a renamed step presents as one entry
  (changed + a rename note), never remove+add.
- **Edges.** Semantic keys `kind|from|to|actionName`, left endpoints mapped through the step id-map,
  duplicates paired in two passes (equal attribute tuples first). Action-order changes classify as
  `changed` — first-match-wins makes precedence semantic. Each action edge also resolves its **raw list
  index** and reusable component name from the raw document (the resolved list drops unresolvable refs,
  so a resolved index is not a raw index — merge payloads address the raw document).
- **Workflow surfaces.** `inputs`⇒`#start`, `outputs`⇒`#end`, `successActions`/`failureActions`⇒the
  defaults card, plus a **components area**: a shared `$components` action body change classifies no
  step, only its referee edges — the components entry carries it.

**The `diffState` channel on `<arazzo-design-surface>`.** A named channel parallel to `debugState`
(explicit channels, class-application without rebuilds): `{nodes:{id:'added'|'removed'|'changed'},
edges:{id:same}, defaults?:'changed', notes?:{id:text}, ghosts?:{nodes,edges}, overlay?:true} | null`.
`_applyDiff()` mirrors `_applyDebug()` — the classification classes, dynamic adornments (a ＋/−/Δ corner
badge, a rename note chip, an edge halo beneath the line, all `pointer-events:none`), and a dim on
unclassified elements. Debug wins order-independently (either setter reconciles). Adornments do not
track a node drag (readonly hosts only). `diffState` and `debugState` are independent; compare never
sets `debugState`, the designer never sets `diffState`.

**Visual language — colour and a non-colour channel, never colour alone:**

| class | token (with its default chain) | non-colour channel |
|---|---|---|
| `df-added` | `--arazzo-diff-added` → `--arazzo-status-completed` → `#2a8a4a` | solid stroke + ＋ badge; solid halo |
| `df-removed` | `--arazzo-diff-removed` → `--arazzo-status-faulted` → `#d4351c` | **dashed** stroke + − badge; dashed halo |
| `df-changed` | `--arazzo-diff-changed` → `--arazzo-status-suspended` → `#b07d18` | solid stroke + Δ badge + note chip; dotted halo |

**The `--arazzo-diff-*` token contract.** Diff appearance gets its own custom properties, separately
stylable from the status palette but defaulting to it through a **nested fallback at every point of use**
(`var(--arazzo-diff-added, var(--arazzo-status-completed, #2a8a4a))`). We deliberately do **not** declare
`--arazzo-diff-*: var(--arazzo-status-*)` aliases in `arazzo-kit.css`: an unregistered custom property
substitutes its `var()` references at the element where it is declared and inherits the resolved value,
so a `:root` alias would freeze the root's status colours and stop tracking subtree theme overrides (the
kit deliberately supports these — `SHARED_CSS` never sets `--arazzo-*` on `:host` for exactly this
reason). With nested fallbacks a host-set `--arazzo-diff-*` up-tree wins; otherwise the status token
resolves at the consuming element (light/dark and subtree themes flow through); otherwise the hex.

**Shared union layout.** Both surfaces read one layout computed over the union of the two graphs (right
id-space, left-only nodes spliced after their matched predecessor), split per side through the id-map, so
matched steps sit level across the split. The compare host runs one fit over the union extent and assigns
the identical `view` to both equal-width columns; with union coordinates, syncing one side's pan/zoom
scrolls both (default on, with an unlock toggle).

**Three modes** (a segmented switch; legend, "Highlight changes" toggle, and change list are common
chrome across all three):

- **Side by side** — the two surfaces with the overlay painted.
- **Overlay (ghost)** — one union surface from `buildGhostProjection(result, base)`: the base version
  solid, the other side's exclusive elements appended as translucent ghosts (`df-ghost` composed with the
  classification, `svg.diff-overlay .df-ghost { opacity:~0.5 }`), in the base side's id-space with
  `ghost:`-prefixed exclusive edges. Base = the merge target when one is set, else the right side.
- **Text** — a CodeMirror **MergeView** over the two documents, serialized with the one deterministic
  `serializeDocument` the text editor uses (`highlightChanges` + `collapseUnchanged`; shadow root passed
  as `root`). Read-only without a merge target. If CM cannot load, Text is disabled with a title — there
  is no textarea fallback for a merge editor. (Vendored via `@codemirror/merge` in the single-instance
  `src/vendor/codemirror.mjs` bundle.)

**Interactive merge (§6.4 of the pack).** When a side is opened with `mergeTarget: true` (the Git panel
sets it on the working-copy side, sourced from the LIVE model via a `documentSource` callback so autosave
cannot stale the merge), the change list gains **Take / Keep**. *Take* adopts the other side's state for
that one entry; *Keep* marks it reviewed (session-scoped, keyed by a stable `group|kind|semantic-ids`
key that survives `refresh`, since the working copy IS the merge state — a change not taken is kept by
definition). The component emits, never mutating a document:

- `change-accepted { entry, apply, workflowId }` where `apply` is one of `insert-step` / `remove-step` /
  `replace-step` (rename accepts a wholesale replace — the model's identity ops handle the id change) /
  `move-step` / `insert-action` / `remove-action` / `replace-action` (action payloads carry **raw list
  indices**, freshly derived every Take) / `set-area` (`inputs`/`outputs`/`summary`/`description`/
  `defaults`) / `set-component`. **Seq-edge and component-sourced flow entries expose no Take** — a route
  hint points at their carrier (the moved step entry / the components entry).
- `merge-text-applied { text }` from the Text mode's editable merge-target pane (chunk `revertControls`
  from the other side, one **Apply** button).

The Layer-2 host (the designer, which owns the one `WorkflowDocumentModel`) applies each event via
`model.update(mutator, {label})` / `model.applyText(text, {label})` — the identity-aware differ reduces
it to minimal ops, so a merge accept is the same op a canvas edit emits: undoable, labelled, and
collaboration-safe. The host then hands the fresh document back via `compare.refresh({left})`; the diff
recomputes, resolved entries disappear, all modes repaint. Verb serialization (a Take/Apply locks every
verb until `refresh`) and text-buffer exclusivity (a dirty MergeView disables list Takes) keep indices
freshly derived and a half-finished merge from clobbering a Take. Because every accept is an ordinary
edit, the §3.1 single-model invariant holds — the canvas and text editor behind the dialog update live.

**Recorded alternatives.** (1) A git-style content-similarity rename fallback over the residue the binding
gate leaves behind (git pairs delete+add at ≥50% similarity; GumTree's bottom-up matcher uses dice ≥0.5)
— started without it because every fallback pairing is harder to explain in the UI than a plain removal
plus addition. (2) A step-scoped merge popover (one step's JSON in a small MergeView from a changed node)
— cheap now the merge package is vendored, but a separate increment; the `changedGroups` chips cover the
need meanwhile. (3) This delivers 8c's useful core (cherry-pick adoption into the working copy) without
three-way base tracking; divergence detection and conflict presentation remain 8c's open scope, and the
§4.2 matcher stays a pure module so that work can reuse it.

## 7. Text mode and expression editing

### 7.1 Editor technology: CodeMirror 6 for both tiers (decided)

The original ask named Monaco for the full-document text mode. The comparative research recommended
**CodeMirror 6 for both the full-document editor and the inline expression fields** for its better
shadow-DOM support, and that is the **agreed decision** (2026-07-04):

- **Shadow DOM is first-class in CM6** (`new EditorView({root: shadowRoot, …})`); Monaco has
  documented shadow-DOM gotchas requiring workarounds.
- **Zero-build fit:** CM6 is modular ESM (MIT); Monaco's only no-bundler consumption path (AMD
  loader) is deprecated, and the kit ships loose ESM with no bundle step.
  *Spike finding (2026-07-04):* CM6 must be **vendored as one bundle**
  (`src/vendor/codemirror.mjs`, `npm run build:vendor`), not CDN-imported per package — CM6
  requires a single shared instance of each core package, and per-package CDN bundles pin their
  internal dependencies independently (observed on jsDelivr: autocomplete pinned `state@6.6.0`
  while view/language/commands pinned `6.7.0`), breaking CM6's instanceof-based extension checks.
  The bundle is lazily imported only when an editor mounts; hosts can substitute import-map-managed
  modules via the `cmLoader` hook, and the component falls back to a themed plain `<input>` with
  the same value/event contract if the modules never load.
- **One grammar stack:** the same JSONPath/runtime-expression/`simple`-grammar tokenizer and the
  same schema-driven completion source serve the full document editor *and* every one-line
  criterion/expression field — consistency Monaco-plus-something-else cannot give.
- The repo already has an RFC 9535 JSONPath Monarch grammar
  (`docs/playground-jsonpath/jsonpath-language.js`) to port to a CM6 stream/Lezer grammar.

If Monaco is mandated (e.g. for consistency with the Blazor playgrounds), it remains feasible for
the *full-document* editor only (lazy-loaded from a host-configurable `monaco-base` URL, shadow-DOM
workarounds applied), with CM6 or a bespoke highlighter still needed for the inline fields — two
stacks instead of one.

### 7.2 Full-document text mode

- Lazy-loaded on first open; JSON with the Arazzo 1.1 schema wired for validation/completions;
  server diagnostics from `validateWorkingCopy` merged as positioned markers (the server checks
  semantics the schema cannot).
- Runtime expressions and JSONPath inside string literals get token highlighting, with hover
  showing the parsed expression parts.

### 7.3 Inline expression editors

Criteria conditions, `context` expressions, parameter values, outputs, and scenario expectations
are *one-line* CM6 editors (`<arazzo-expression-input>`): JSONPath + runtime-expression +
`simple`-grammar tokenization; completions from the schema context
(`$steps.<id>.outputs.<name>`, `$inputs.<name>`, operators); server-validated on debounce (parse
errors underlined with the position from `ArazzoExpression`).

## 8. Simulation architecture (server)

### 8.1 `WorkflowSimulator` (realizes the planned §3.2 facade)

`Corvus.Text.Json.Arazzo.Testing` grows the planned simulator: build/compile the working copy in
memory (the existing `IWorkflowExecutorProvider` — same path as catalog add), run the executor
against `MockApiTransport` (scenario mocks compiled to its scripting surface) and a
`FakeTimeProvider` driven by the scenario clock program, capture a structured **trace** (step
enter/exit, request/response pairs, per-criterion evaluations with operand values, retries, waits +
clock advances, output extraction, fault/outcome). Compilation caching keyed by document content
hash keeps repeated debug commands cheap (compile once per document state, replay many).

### 8.2 Stateless interactive stepping

Debug commands never hold server state: `simulate(until)` replays from the start each time.
Determinism (mock + virtual clock + fixed inputs) guarantees identical prefixes, so "step" = replay
with `until` one step further; "inject then continue" = replay with the trigger added to the
scenario delta. The trace returned is complete up to the stop point — scrubbing backward is
client-side. This keeps the control plane stateless (no session affinity, no cleanup), makes
"re-run to the same point after an edit" trivial, and the cost is milliseconds per command.

### 8.3 Safety

Simulation compiles and runs user-authored workflow code server-side — the same trust decision the
catalog already makes at add-time (compile) and run-time (execute), gated the same way: capability
scopes + reach on the working copy, a step budget, a wall-clock timeout, and the mock transport as
the *only* I/O surface (no real credentials are ever resolved in simulation).

## 9. Scenario carry-over & lifecycle

- Working copy created `fromCatalogVersion` copies that version's `metadata/scenarios.json` into
  the editable scenario set (and its document, source attachments by reference).
- Publish embeds the (possibly edited) scenarios + fresh server-attested evidence into the new
  version's package.
- Scenarios are versioned *with* the workflow they test — no separate scenario store to drift.
- A catalog version's scenarios can be simulated read-only (`simulateCatalogVersion`) — regression
  triage against published behaviour without a working copy.

## 10. Validation & Problems

Three tiers, all surfaced in the Problems tray + inline:

1. **Structural (client, instant):** JSON parse, schema-shape basics via the text editor's
   schema-aware linting.
2. **Document (server, debounced):** `validateWorkingCopy` — full JSON-Schema conformance +
   semantic rules (unresolved bindings, unknown goto targets, expression/criterion syntax with
   positions, unreachable steps, missing source attachments).
3. **Behavioural (explicit):** scenario runs; failed expectations render like diagnostics
   (click → the step/criterion involved, with the truth table).

## 11. Security & governance

- Working copies are reach-scoped, administrator-governed resources; scenarios inherit the working
  copy's governance. Capability scopes: `workspace:read`/`workspace:write` tiers mirroring runs.
- Publish requires `catalog:write` (+ registry writes per wizard rules); evidence generation runs
  under the server's authority — a client cannot fabricate evidence.
- Simulation never touches real credentials or endpoints (§8.3). The credential dialog / registry
  surfaces stay the only places credentials appear, and only as references.
- GitHub tokens live server-side only (§4.7); the kit is auth-agnostic throughout.
- Scope honesty: designer actions absent unless granted (`show-forbidden` opt-in), as everywhere.

## 12. Testing strategy

- **Client conformance:** the new Layer-0 methods assert against the OpenAPI document (existing
  conformance harness).
- **Component tests** (@web/test-runner) per element against a fake client; **Playwright** smokes
  for the composed designer: author a two-step workflow from the mock operation surface, run a
  debug session, save a scenario, publish, see the evidence badge (all against the demo mock).
- **Server:** simulator unit tests over `MockApiTransport`/`FakeTimeProvider` (the Testing
  package's own suite); workspace/scenario store conformance across all durability backends
  (existing conformance-suite pattern); publish-evidence round-trip (scenarios in → evidence out →
  package entries verifiable).
- **Determinism lock:** a repeated-simulation test asserting byte-identical traces — the §8.2
  contract.

## 13. Demo & mock

`demo/mock-api.js` grows the workspace/scenario/simulate/publish surface with seeded content: a
sample OpenAPI + AsyncAPI source pair, a seeded working copy mid-edit, seeded scenarios (one green,
one failing), so the whole design-test-validate-publish loop — including a debug session — runs
with no server, persona-gated as today. The simulation mock replays canned traces (the mock cannot
compile; it serves recorded fixtures, clearly marked).

## 14. Delivery slices (each API-first: OpenAPI → stores/conformance → handler → CLI → kit → demo)

1. **Workspace core.** Working-copy CRUD + validate + schemas; `<arazzo-workspace-table>`; designer
   shell with **text mode only** (CM6, markers, save/dirty/etag). Create-from-version (documents
   only). *The designer is useful from slice 1.*
   *Status: CRUD and validate are BUILT end-to-end — the `/workspace/workflows` contract group
   (`workspace:read`/`workspace:write`, `expectedEtag`-guarded saves → 409), the id-keyed
   `IWorkspaceWorkflowStore` (+ in-memory reference and conformance suite), the
   `ArazzoControlPlaneWorkspaceHandler` (create from document / catalog version / blank skeleton,
   name derivation, reach stamping + escalation guard), server API tests, and the JS client/mock
   parity. `validateWorkspaceWorkflow` merges two passes into positioned diagnostics
   (`WorkingCopyDiagnostics`): JSON-Schema conformance against the embedded Arazzo 1.0/1.1
   meta-schema picked by the document's declared version, plus `WorkflowDocumentAnalyzer`
   (CodeGeneration) collecting every document-local semantic finding — goto/retry targets,
   `dependsOn`, criterion syntax via the runtime's own `CompiledCriterion`, runtime-expression
   syntax, reusable component references, duplicate ids, first-match-wins reachability, and
   implicit `$steps` dependencies (§5.8.5.2.4: an unknown `$steps` target or a combined
   explicit+implicit dependency cycle is an error; a dependency that reorders a no-`dependsOn`
   workflow is a warning).
   Source-dependent checks (operationId/channelPath resolution) arrive with attached sources
   (slice 2), the schemas endpoint alongside them. `<arazzo-workspace-table>` and the designer
   shell are BUILT: the demo boots into the workspace view, opens a working copy through
   `model.reset()` (in-place load — no broadcast, history cleared), tracks dirty state, saves with
   the `expectedEtag` it read (409 surfaces as "a collaborator saved first"), and Validate
   (saving first when dirty — validation runs on the stored document) renders the Problems tray;
   clicking a finding reveals the pointed workflow/step on the canvas and in the text tab.
   Create-from-version currently requires an explicit `fromVersionNumber` (latest-defaulting
   arrives with scenario carry-over).*
2. **Operation surface & sources.** `listSourceOperations` (+ registry variant), attach/upload,
   `fetchSourceDocument` (authenticated URL); operation browser + acquisition dialog; step
   creation via inspector (form-first, still no canvas).
   *Status: the API half is BUILT — working-copy attachments
   (`PUT`/`GET`/`DELETE /workspace/workflows/{id}/sources[/{name}]`: registry reference resolved
   reach-checked at read time, or inline document stored so the working copy resolves like a
   package; attach/detach save under the etag they read, and the attach response carries the
   working copy's NEW etag so the designer's next save doesn't false-conflict), plus
   `listWorkingCopySourceOperations` and `listRegisteredSourceOperations` projecting the
   operation surface via `SourceOperationSurface` (CodeGeneration): raw-JSON-Schema
   request/parameter/response shapes with local `$ref`s inlined to a bounded depth, OpenAPI
   paths×methods and AsyncAPI 2.x/3.0 (publish/subscribe → receive/send exactly as the code
   generator maps them). JS client + mock parity done. The workspace seams passed the
   allocation-campaign protocol (ownership ledger + measured before→after, handler→InMemory
   benchmarks in `WorkspaceApiBenchmarks`): attach RMW 12.93→4.86 KB, operation surface
   16.26→3.77 KB (single-pass write-through, no descriptor records), blank create 2.21→1.77 KB
   (pooled create draft, no name string / skeleton buffer), validate 5.27→3.88 KB (pointer
   strings only at finding sites), CRUD paths already conformant (2.2–3.7 KB). `fetchSourceDocument` (§4.4) is BUILT:
   server-side fetch (no browser CORS) with an optional registered-credential reference resolved
   through the §13 machinery (secret material never rides the API), JSON and YAML payloads (YAML
   parses to the returned JSON form — same canonical digest), type/version detection, the repo's
   canonical SHA-256 digest, https-only unless the deployment opts in, a pooled 16 MiB-capped
   download, and fails-closed 400 when no fetcher is wired (measured: 1.88 KB per fetch over a
   stub endpoint). The UI half is BUILT: `<arazzo-operation-browser>`
   (the left rail — attachments + their live operation surfaces, filter, click-to-add, detach) and
   `<arazzo-source-acquisition-dialog>` (registry pick · credentialed URL fetch with server-detected
   preview · JSON upload — YAML endpoints go through Fetch), wired into the designer shell: step
   creation from descriptors (required parameters pre-populated, model-routed and undoable), the
   inspector's criteria/body templates fed from the LIVE surfaces instead of a fixture, and the
   save token refreshed from the attach response's etag (detach re-fetches) so acquisition never
   false-conflicts a save. Slice 2 is complete except GitHub import (§4.7, its own slice).*
3. **Design surface v1.** Graph projection + chosen canvas technology: render, select, inspector
   wiring, add/move/connect/delete, defaults layer, auto-layout, `designerState` persistence.
4. **Inspectors complete.** Step/workflow/document inspectors, criteria/action editors,
   expression inputs with highlighting + completions; full Arazzo 1.1 editing parity (the §1
   coverage checklist holds from this slice on).
   *Status: BUILT. 4a added the missing editing paths against a gap analysis of Arazzo11.json —
   `requestBody.replacements`, `dependsOn` on both levels (execution-order chips), workflow-level
   `parameters`, and cross-workflow `goto`/`retry` targets (retry also regained its optional
   retry-from step, which the old editor silently pruned). 4b added
   `<arazzo-document-inspector>` (info · sourceDescriptions · workflows add/remove · the
   components reusable library with embedded per-kind editors) and reusable-reference authoring:
   step parameters and both action lists offer "+ Add reference…" against the library, and
   reference rows localize (materialise the component inline) or detach. The demo's ⚙ Document
   mode routes document edits through the model (undoable, coalesced); duplicate component names
   flag visibly. Editing-parity exception now closed (2026-07-13, pack 2): workflow `inputs` and
   component input schemas author with the typed `<arazzo-schema-editor>` (§5.3a) — combiner nodes
   and merged `allOf` forms included, unrendered constructs preserved as advanced nodes; `$ref`s
   resolve on the baked path, so the run dialog and completions consume the server's baked schemas.*
5. **Simulator + debug.** Server `WorkflowSimulator` + trace + stateless stepping; debug controls,
   context explorer, trace viewer, canvas overlay, expression console.
   *Status: server half BUILT. `WorkflowSimulator` lives in `Corvus.Text.Json.Arazzo.Testing` (the
   planned §3.2 facade): compiles through the catalog's own `IWorkflowExecutorProvider` path in
   DURABLE mode — the generated executor's per-step checkpoint (whose state indices are the
   step-array indices) is the trace seam, so `TracingWorkflowRun` records step boundaries, outputs,
   retries, waits, and faults, enforces the step budget, and throws the pause signal for
   "before step X" stops with nothing half-executed. ("No code-generator changes" held only until
   §15-8a: the sub-workflow trace pack changed the emitters — the durable sub-workflow call had
   never compiled, and the corrected emission threads the child scope and time provider and
   unwraps the child's result.) The §8.1
   content-hash compile cache now exists (bounded LRU over `WorkflowExecutorLoader`). Criterion
   truth tables and the action-taken inference are computed POST-HOC via `CompiledCriterion` over
   the captured deterministic context (operand-level values remain a deferral). Timer waits
   auto-advance a bespoke `ManualTimeProvider` (fixed 2020-01-01 epoch for reproducible traces);
   message waits deliver the scenario's triggers through the run's own delivered-message seam.
   `POST /workspace/workflows/{id}/simulate` (workspace:read) with the optional `workflowId`
   selector (the handler reorders the workflows array — the provider compiles the first);
   fails closed 400 unwired, 422 not-executable. Measured (in-process ShortRun): one cached-compile
   replay = 34 µs / 14.7 KB — the §8.2 "milliseconds per command" budget holds with three orders of margin. The debug UI (5b) is BUILT:
   `<arazzo-debug-tray>` renders one complete trace — the trace viewer (per-step status, attempts,
   action taken, click-to-inspect), the paused-context explorer (exchanges, criterion truth table,
   step and workflow outputs, fault/wait/clock records), and the time-travel scrubber, all pure
   cursor movement over the recorded payload (§8.2 exactly: no further calls). `frameAt` projects
   frames onto the canvas overlay (done/failed steps, taken edges lit, active step pulsing).
   ▶ Run auto-scripts mocks from the attached surfaces' first documented success statuses;
   breakpoints ride `until.breakpoints`; double-activating a node is run-to-here
   (`until.beforeStepId`); one-more-step past a pause replays with the budget one step further.
   `simulateCatalogVersion` BUILT: the same request/trace shapes over a published version's
   IMMUTABLE package (unpacked document + embedded sources; the workflowId selector reorders like
   the working-copy path), `catalog:read`, fails closed without a simulator — re-verify evidence
   or explore a regression without a working copy. The typed inputs form + mock editor landed with
   the slice-6 typed scenario forms. Trigger injection BUILT: a suspended message wait offers the
   tray's ⚡ form (channel/correlationId prefilled from the wait); the injected trigger joins the
   session scenario and the stateless replay delivers it at the wait — Save-as-scenario captures
   the session's triggers too, and the demo simulator now suspends on unmatched message waits and
   consumes staged triggers (it silently ignored `triggers` before). Paused-context expression
   console BUILT: ⏎ resolves `$inputs`/`$outputs`/`$steps.<id>.outputs`/`$statusCode`/
   `$response.body` (dotted descent + `#/pointer`) against the recorded trace at the scrub cursor
   — client-side, stateless like all stepping (§8.2). SLICE COMPLETE.*
6. **Scenarios.** Scenario schema + CRUD + run endpoints (run-one and run-all suite report);
   scenario panel/editor; session recording; carry-over on create-from-version; **the `scenarios
   run` CLI** (§4.5: standalone in-process + remote modes, globbing, JUnit/JSON reports, CI exit
   codes).
   *Status: 6a (API) and 6b (panel + recording) BUILT. Scenarios store WITH the working copy and
   edit as etag-guarded RMW; runs resolve mocks by (source, operationId) through the attached
   surfaces, execute the §8 simulator, and judge expectations (outcome · exact/subsequence path ·
   $outputs criteria through the real criterion compiler · per-step reached/attempts); the suite
   report counts failures with per-verdict detail. The Scenarios sidebar tab lists/runs/edits
   (guarded JSON v1) and hands any run's trace to the debug tray; "Save as scenario…" captures a
   debug session with expectations promoted from the observed trace (§3.4). Carry-over on
   create-from-version: the new working copy inherits the version's scenario set (§9). The
   `scenarios run` CLI (§4.5): the headless engine is `ScenarioSuite` in the Testing assembly —
   extracted from the server, which now delegates to it, so an interactive run, a publish
   attestation, and a CI suite produce the same report shape. Standalone (default) hosts the
   simulator in-process (workflow + sources from disk/urls, `**`-globbed scenario files, `--filter`
   wildcards, deterministic ordering); remote (`--working-copy` + `--server`) executes the stored
   suite server-side. Console/JUnit/JSON reports (`--report junit=…/json=…`; the JSON report is the
   suite-report shape publish embeds), `--github-annotations` (::error + job summary), and CI exit
   codes (1 = failed expectation, 2 = suite could not run). Typed scenario forms BUILT — they
   CONSUME schemas (the §15 item-8 authoring deferral stands): `getWorkingCopySchemas` recomputes
   the baked-schema TypeDescriptors for the working copy's current document + attachments (the
   §4.1 row, same generator and shape as the catalog's), and `<arazzo-scenario-editor>` renders
   the form — inputs typed by the workflow's inputs descriptor, mocks addressed by the document's
   own operations with statuses constrained to the DECLARED responses (correct by construction:
   the simulator faults on undeclared statuses) and bodies typed per status, expectations picked
   from the workflow's steps, triggers/clock — with JSON one toggle away and unknown fields
   surviving a form round-trip (edits overlay the original object). `<arazzo-value-editor>`
   gained `seed` (prefill from an existing value), so editing is seeding + reading. SLICE
   COMPLETE; catalog-version scenario targets follow simulateCatalogVersion (§14 slice-5
   remainder).*
7. **Publish with evidence.** Publish endpoint (server-attested suite), package entries, evidence
   badge on catalog detail; optional promotion-readiness extension.
   *Status: server half BUILT. The `.awp` format's reserved `metadata/scenarios.json` +
   `metadata/evidence.json` entries exist (deterministic packing, canonical-repack survival via
   OpenPooled→PackPooled pass-through, zero-copy `TryReadEntry`, hash-invariant — evidence is
   metadata, not content). `publishWorkingCopy` (catalog:write): the validation gate (all three
   passes, shared with validate), the server-attested suite (a client cannot submit evidence;
   fails closed when scenarios exist and no simulator is wired), the canonical-hash-first evidence
   record (engine version, per-scenario outcome/pathSummary, suite totals), the package embedding
   both entries, and the catalog add stamping internal security tags with the response stripped
   through the shared `ControlPlaneAccess.PublicView`. 422 refusals carry the diagnostics or the
   suite report; `requireScenarios:false` overrides. `getCatalogEvidence` serves the entry from
   the stored package. The designer's Publish button lands refusals where they are actionable
   (validation → Problems, a failing suite → Scenarios). Scenario carry-over on
   create-from-version: the new working copy inherits the version's scenario set (§9), both
   entries sliced from one owned package read. The evidence badge: `getCatalogVersion` — the
   detail, never the index — projects `{at, suite}` from the package's evidence entry onto the
   summary (`PublishEvidenceSummary`; an empty suite attests nothing and is omitted, so promotion
   readiness can read absence as unevidenced), and catalog detail renders it green/red with the
   publish instant. Promotion readiness: environments gained the default-off `requireEvidence`
   flag (persisted schema + contract create/update/summary, editable in the environments panel);
   both promotion paths — direct make-available and availability-request approval — refuse a
   non-green (or unevidenced) version with 409 `evidence-required`, keeping the §7.7 behaviour
   exactly where the flag is unset. SLICE COMPLETE.*
8. **GitHub.** App broker + session endpoints; bind/pull/commit(+PR) incl. scenario files;
   import-from-repo in the acquisition dialog; **the GitHub Action wrapper** for the scenario
   runner (§4.5).
   *Status: 8a (the broker) BUILT. `GitHubBroker` (deployment-wired via `MapArazzoControlPlane`,
   fails closed like the source fetcher; GHES = base-URL configuration): the user-to-server flow
   with single-use principal-bound state — begin returns `{authorizeUrl, state}` JSON for a POPUP
   (a bearer SPA cannot carry its token on a top-level navigation; the opener polls the session
   and closes the popup), the anonymous callback authenticates BY the state and exchanges the code
   server-side (App secret resolved per exchange via the deployment's secret resolver, never
   held), tokens refresh on demand and live in a per-principal `IGitHubTokenStore` (in-memory
   default; encrypted-at-rest substitutable). Endpoints: getGitHubStatus (identity + installations
   + user ∩ installation repositories), deleteGitHubSession, browseRepo (proxied contents,
   non-disclosing 404). JS client + mock parity (the mock's authorizeUrl points at its own
   callback, so the demo popup self-completes). 8b (the round-trip) BUILT: the working copy's
   `gitBinding` (save replaces it, omission keeps it; persisted schema + contract + client);
   pullWorkingCopy = fetch-everything-first (document, specPaths as inline attachments,
   scenariosDir's `<name>.scenario.json` set), then ONE etag-guarded save — nothing partially
   applies; commitWorkingCopy = read-sha-then-write contents PUTs in deterministic order
   (document, bound specs — inline or registry-resolved — then scenario files), each carrying
   message/branch/content and NO author/committer (the §4.7 identity rule, asserted on the wire
   in tests), with an optional draft pull request FROM the bound branch. 8c (the UI) BUILT:
   `<arazzo-github-connect>` (the popup flow — the opener polls the session and closes the popup;
   injectable for tests), the acquisition dialog's fourth mode (GitHub: repo picker from the
   user ∩ installation intersection, contents browser, picked spec attaches through the same
   inline seam), and `<arazzo-git-dialog>` on the designer toolbar (⎇): binding form, and
   Commit + optional draft PR, the result naming the signed-in identity. (UI framing: the
   `pullWorkingCopy` action is surfaced as **Load** inside the *binding* section — loading the
   document from the branch, not a round-trip verb — kept apart from the roll-back-oriented history
   browser; only Commit lives in the round-trip UI. See open question 8c.) The binding form browses
   the repo's REAL branches (`listRepoBranches` — default branch marked) with in-dialog branch
   creation (`createRepoBranch` — a ref from a base head, no commit, no composed identity), and
   spec paths are one row per ATTACHED source (names from the working copy, only the path typed;
   blank = not tracked; stale entries marked "not attached"). Remaining: the GitHub
   Action wrapper — blocked on publishing the CLI as a dotnet tool (release engineering, not this
   branch).*

Slices 2↔3 and 5↔6 can swap/overlap; each slice lands green (build, tests, catalog gate, demo).

## 15. Open questions

1. **Workspace store shape** — a new `IWorkflowWorkspaceStore` across all 10 durability backends
   from slice 1, or in-memory + SQLite first while the contract settles? (Recommend: full
   conformance-suite fan-out from the start, per the pagination lesson.)
2. **YAML text mode** — the catalog/package canon is JSON; do we offer YAML as an editor-side
   convenience (round-tripped), or JSON only in v1? (Recommend: JSON only in v1.)
3. **Ad-hoc "run against real environment" from the designer** — deliberately out (§1 non-goals);
   confirm that triggering a *catalogued draft* version's run from the designer (existing
   `POST …/runs`) is the sanctioned bridge.
4. **Scenario portability** — `metadata/scenarios.json` schema is ours; do we also emit a
   human-readable scenario report entry for governance review at publish?
5. **Working-copy sharing model** — administrator-set governance per working copy (as designed), or
   simpler owner-only in slice 1 with governance added when Git flows land?
6. **CM6 instead of Monaco** (§7.1) — *Resolved:* CodeMirror 6 for both tiers, for its first-class
   shadow-DOM support (agreed 2026-07-04). The bespoke-SVG design surface (§6.1) was ratified the
   same day, conditional on the §6.3 layering discipline.
7. **Step `timeout` vs the virtual clock** — a step's Arazzo `timeout` compiles to a real
   `CancellationTokenSource.CancelAfter`, not a `TimeProvider`-driven delay, so the simulator's
   virtual clock cannot deterministically fast-forward a step timeout (a mock `SetResponseDelay`
   against real time is the only trigger today). Options: teach the emitter to time out via the
   executor's `TimeProvider`, or accept wall-clock step timeouts in simulation (they still bound
   runaway steps under the §8.3 wall-clock cap). (Recommend: emitter change, scheduled with the
   engine's own backlog — it affects production runs too, where a virtual-clock-driven timeout is
   equally desirable for determinism in tests.)
8c. **Pull as merge** — pull today REPLACES the working copy from the branch (etag-guarded, and
   the UI confirms with a danger dialog). A true three-way merge needs the binding to record the
   pulled commit sha (the base), divergence detection, and a conflict presentation — design work,
   not a quick add.
8d. **Visual diff overlay on the comparison visualizer** — *Resolved 2026-07-13; see §6.4.* Shipped:
   binding-gated matching (renames paired, reorders `move`d not `changed`), the `diffState` overlay
   channel with dedicated `--arazzo-diff-*` tokens, a shared union layout, three comparison modes
   (side-by-side, overlay/ghost, CodeMirror MergeView text), a grouped change list, the catalog-detail
   compare host (§9.11), and an interactive Take/Keep merge that emits `change-accepted` /
   `merge-text-applied` for the host to apply to its one model. The Git pane's history browser ships
   commit browsing (`GET /github/repos/{owner}/{repo}/commits`, scoped to the bound branch and
   document) and rollback (a danger-confirmed pull carrying the commit's `ref` — the binding never
   changes, so the next commit records the rollback on the branch); Compare opens the overlay by
   default. Remaining out of scope: the similarity rename fallback, a step-scoped merge popover, and
   8c's three-way base tracking / conflict presentation (§6.4 recorded alternatives).
8b. **Step-output overrides (engine)** — BUILT for debug runs (§18 slice 3c).
   `SimulationScenario.StepOutputOverrides` (stepId → provided outputs) is the real seam: at the
   checkpoint boundary the replay unwinds the executor, records the overridden step as skipped
   (`skipped: true`, attempt 0, no exchange, the PROVIDED outputs on the record), stages those
   outputs on the run so the re-entered executor restores them for downstream references and
   workflow outputs, and resumes past the step — the durable engine's Skip protocol (record
   outputs, move the cursor, re-enter at the cursor) replayed, with no emitter change. Only a
   criteria-less success action routes after a skipped step (no `$statusCode` exists to judge);
   end/goto are honoured; a breakpoint on the step still pauses first, and a skipped step counts
   against the budget (the mock's ordering). Debug-run resume actions ride the seam verbatim:
   Skip resolves its step from `targetCursor − 1` (else the current position, else the last
   traced step) and accumulates an override; Rewind resets the cursor (the forward replay
   re-executes — deliberate per §18); RetryFaultedStep re-advances; StatePatch applies RFC 6902
   over `{ inputs, stepOutputs }` — patched inputs replace the captured inputs, changed/added
   `stepOutputs` entries become overrides, removed entries clear theirs, untouched entries
   re-derive identically under the deterministic replay. Remaining: the ratified
   `SimulateRequest` carries no `overrides`, so the simulate endpoints deliberately do not accept
   them yet (the mock's `simulateDocument` honours them only as shared plumbing for its debug
   runs); adding that is an additive contract rev for when the what-if debugger needs server-side
   simulate overrides. The trace record's `skipped` marker was declared in `SimulationTrace` by
   8a's contract rev (pack 3 slice A, 2026-07-15) — only the `overrides` half remains open.
8a. **Sub-workflow trace records (engine)** — **resolved 2026-07-15 (pack 3).** The original
   premise here ("the REAL simulator inlines sub-workflow execution without recording nested
   records") was wrong in a sharper way the pack's antagonistic review proved by executing the
   path: DURABLE sub-workflow codegen had never compiled at all (the emitted call dropped the
   cancellation token into the `IWorkflowRun?` slot at both `ControlFlowEmitter` call sites; the
   provider swallowed the CS1503 into a silent `NotExecutable`) — only the straight-line
   non-durable form ran, as an untracked plain function. The pack fixed that first (repro-first),
   then built the ratified design: a workflowId-bound step executes through a CHILD RECORDER SCOPE
   (`IWorkflowRun.BeginSubWorkflow`) sharing ONE global step budget (sub-steps count against
   `MaxSteps`; an explicit nesting depth cap, `IWorkflowRun.MaxSubWorkflowDepth = 8`, exhausts
   runaway recursion predictably), ONE virtual clock (child timer waits auto-advance; suspensions
   bubble and the child replays fresh per invocation), and per-record exchange attribution against
   the one global exchange list. The child's records attach to the parent step's record as
   `subTrace` — the same trace shape recursively, `workflowId` on every sub-trace and never on the
   root (the tray's ascent depends on that) — with `AnalyzeTrace` recursing against each
   sub-workflow's own compiled criteria/actions, and the §12 determinism byte-lock now pinning
   repeated nested simulations byte-identical. Stops are SCOPED (`parent/child[/…]`; a bare id
   addresses root steps only; occurrence counters are root-owned and cumulative across child
   invocations); a stop inside a child leaves each ancestor a partial `subTrace` (`paused` /
   `suspended` parent statuses — never a false green completed) with the root's `pausedBefore`
   carrying the full scoped path. Durable debug runs capture the same records AT SOURCE through a
   recording sink on the run (`WorkflowRun.Recorder`): recording-only child scopes never checkpoint
   (cursor and resume verbs stay top-level — a deliberate invariant), the assembler prefers
   captured records with per-step checkpoint merge (`skipped` derived from the Skip protocol's
   checkpoint delta, never transported), and a run without a sink is untouched by construction.
   Cross-source sub-workflows (`$sourceDescriptions.x.y` targets) are recorded but not analyzed or
   scope-validated in v1 (their shape lives in another document); child-level `$inputs` criteria
   evaluate against an undefined context (the child's built inputs document does not survive the
   invocation) — both declared limits. (The §6.2 HTTP-trigger source — the Sources panel's Catalog
   mode — needs nothing: a trigger step is a plain OpenAPI operation.)
8. **Typed schema-authoring form** — **resolved 2026-07-13 (pack 2).** Built as
   `<arazzo-schema-editor>` (§5.3a), option (a): typed property rows, first-class
   `oneOf`/`anyOf`/`allOf` combiner nodes normalized identically by the client renderer and the
   server's baked-schema generator, advanced nodes that preserve unrendered constructs, a Form | JSON
   toggle over the guarded editor, a server meta-validation pass with positioned Problems findings, a
   shared-type reference picker, and a live typed preview. The original analysis: workflow `inputs`
   and the components library's input schemas edited as guarded JSON textareas (parse-gated,
   last-valid-wins), the one deliberate exception to the slice-4 "full editing parity" bar. The
   options weighed were (a) a purpose-built schema form (property rows, type/format/required toggles,
   nested objects) — the full answer, but a sizeable component in its own right; (b) reuse the
   payload-editor's schema-driven form machinery pointed at the JSON-Schema *meta*-schema — cheaper,
   but meta-schema forms are clunky; (c) keep guarded JSON and invest instead in inline validation +
   completions from the CM6 tier. Option (a) was chosen, with the guarded JSON retained as the
   lossless JSON tier underneath.

## 18. Remote dev-environment debug runs (ratified 2026-07-06)

**Ruling.** "Run/debug against real endpoints" (the live-debugging ask) is answered by running the
*draft* in a development-class environment — never by credentials in the browser. The platform's
credential posture stays absolute: the control plane holds *references into a secret store*, the
environment's runner resolves secrets **as its own identity** at run time (§13.5), and no secret
ever reaches the designer, the developer, or the working copy. Browser-local credential storage was
considered and rejected (localStorage/sessionStorage are script-readable; the "developer's own
creds" case is served better by the capture below, and the no-dev-environment case is an adoption
gap, not a designer feature).

**Model.** A **debug run** is a durable run of the *working copy's stored document* (not a
published version), started from the designer against a named environment. It rides the runs
machinery, not the simulator:

- **Forward-only.** The simulation debugger's stateless replay (§8.2) re-executes from step 0 on
  every interaction — free against mocks, unthinkable against real systems. A debug run instead
  advances persisted state exactly like a production run: *step* = resume with
  `pause.afterEachStep`, *step over — provide outputs* = the runs `Skip` resume (verbatim shape),
  *breakpoints* = `pause.beforeSteps`, *retry/rewind/state-patch* = the same `ResumeRequest` union
  the runs view uses, with rewind's side-effect re-execution being the developer's deliberate act.
- **Real waits.** A message step is a genuine suspension. Trigger *injection* on a debug run delivers
  the message STRAIGHT to the suspended run — the debug stand-in for the real publisher — and it
  advances (the `inject-message` endpoint below; the runner's `DeliverMessageAsync`, matched by
  channel + optional correlation). Nothing is staged into a scenario and nothing is published to a
  broker; a production run's message arrives from its real source.
- **Capture, then time-travel.** A debug run records its exchanges in the same trace shape the
  simulator emits (`SimulationTrace`, request bodies as sent). "Save as scenario" over that trace
  yields a mock scenario carrying *real recorded reality* — and the full replay debugger (rewind,
  what-if, step-over, inject) then works against it offline, side-effect-free. Live run for truth;
  replayed capture for archaeology.

**Governance.** Three gates, all deliberate:

1. `allowsDraftRuns` on the environment (default **false**; meaningful only on development-class
   environments) — today only published, availability-granted versions execute in an environment,
   and crossing that line is an explicit per-environment decision by its administrators.
2. The caller needs reach to the working copy AND entitlement to run in that environment (the same
   §17 resolution as catalog runs).
3. Every debug-run start is an audited event (who, which working copy, which document etag, which
   environment).

Credential *readiness* is surfaced before the attempt: the run dialog reuses the per-environment
credential-coverage machinery (§7.5–7.8) to show "development: payments ✓ · order-events ✗ (no
credential bound)" per source; starting with gaps is a 409 carrying that detail.

**Contract.** `Environment{Create,Update,Summary}.allowsDraftRuns`. The debug-run operations form
their own OpenAPI group — the **`debugRuns`** tag — so the surface reads unmistakably as debugging,
not part of the general workspace API. Under the working copy:
`POST …/{id}/debug-runs` (`{workflowId, environment, inputs?, pause?}` → 201, 403 on the flag or
entitlement, 409 on readiness), `GET …/debug-runs/{debugRunId}` (status
`running·paused·suspended·completed·faulted·cancelled`, cursor, `SimulationTrace`-shaped trace,
wait), `POST …/debug-runs/{debugRunId}/resume` (`{action?: ResumeRequest, pause?}`),
`POST …/debug-runs/{debugRunId}/inject-message` (`{channel, payload, correlationId?}` — debug-only:
delivers a message to a run suspended on an AsyncAPI receive; 409 when it awaits no such message),
`POST …/debug-runs/{debugRunId}/cancel`, `DELETE …/debug-runs/{debugRunId}`. Capture-to-scenario is
client-side over the trace — no endpoint.

**Staging.** (1) Contract + server lifecycle with the simulator as interim executor (validation,
gates, pause/resume semantics real; transport still mock) + mock parity — this gives the dock its
live-attach mode to build against. (2) The dock's attached mode: forward-only controls
(resume/step/skip/cancel), readiness in the run dialog, capture-to-scenario. (3) The engine seam:
the execution host runs the draft with the environment's credential bindings and real transport —
the only slice that touches the host. §15 item 3 is superseded by this section.

**Delivery (stage 3, 2026-07-07).** Slices 1–3c are built (contract + server lifecycle with the
interim simulator executor; the dock's attached mode; `allowsDraftRuns`; the real resume-action
seam, §15 item 8b). Stage 3 — the engine seam — is split three ways:

- **3d (built): durable draft runs, forward-only.** A debug run is an ordinary durable run of the
  working copy's captured document, riding the runs machinery. `DraftRunManagement` writes the
  capture to a sibling `IDraftRunStore` (the catalog/run split — the audited record beside the
  packed `{document, sources}` blob, content-hash-cached on the runner) then enqueues a Pending run
  carrying the reserved `$draft` id, environment-pinned and row-scoped to its working copy
  (`sys:workingCopy`, §14.2); `DraftWorkflowResumer` compiles the captured bytes rather than a
  catalog `executor.dll` and runs it forward-only, durable waits resuming through the existing
  `WorkflowWorker`; the dispatcher claims `$draft` runs under `runnerEnvironment` pinning.
  `IDraftRunStore` fans out across every durability backend, each bind held to the #803 bytes-native
  bar — the package is streamed copy-free (`ReadOnlyMemoryStream.Rent`, native `ReadOnlyMemory`
  parameters, or base64 written straight into the item writer), never a per-put `ToArray`; the
  measured floor and the bind mandate live in `DraftRunStoreBenchmarks`. The workspace handler stays
  on the interim simulator through 3d, so nothing user-visible changes until 3e.
- **3e: the pause seam + handler swap.** `afterEachStep`/`beforeSteps` are honoured at the run's step
  boundary (3e-1, the simulator's `CheckStop` precedent, no emitter change): a durable run pauses as
  `Suspended` carrying a `WorkflowWaitKind.Pause` with no wake trigger, so a worker never resumes it —
  only an explicit step/resume does. The workspace handler then routes debug runs through the
  in-process host (3e-2): a debug run *is* a durable `$draft` run; the handler captures + enqueues it,
  drives each advance through the in-process runner's recording+tracing resumer (which produces the
  `SimulationTrace`-shaped metadata trace the dock renders — 3e-2a/b), and the interim simulator
  executor retires here. Because a paused (`Suspended`) run is **not** dispatch-claimable, interactive
  debug runs require the in-process runner; a deployment wiring none fails closed
  (`debug-runs-not-offered`). **Refinement of the §18 model.** The resume verbs on a *live* debug run
  are the durable engine's native fault-remediation (retry / skip-past-the-faulted-step / rewind /
  state-patch), which act only on a **faulted** run. "Step over — provide outputs" of a *non-faulted*
  paused step is therefore not a live-run operation: it is a **replay-debugger** action (the
  `SimulateRequest.overrides` rev, 3e-3), consistent with *live run for truth; replayed capture for
  archaeology*. So the live-run dock controls are step (`afterEachStep`), continue (breakpoints), and
  fault remediation; the dock's "step over" against a live run routes to replay.
- **3f: body-capture opt-in.** The metadata trace (no bodies) lands with the 3e-2 swap. 3f narrows to
  the per-environment body-capture opt-in (default off, development-class only, audited,
  pre-authentication recording invariant, caps + `truncated` markers, purged with the run) — the
  second ruling below.

**Ruling — the simulator's role (2026-07-07).** The simulator-backed debug-run path is *interim*: it
retires at the 3e handler swap. The simulator remains permanent for the simulate/replay endpoints,
scenario suites, and failed-test debugging — deterministic, side-effect-free re-execution from step 0
is the right tool there, and a real debug-run capture saved via capture-to-scenario becomes a mock
scenario the simulator replays with full time travel. The additive `SimulateRequest.overrides` +
declared `SimulationTrace.skipped` contract rev (server-side step-over / what-if for the replay
debugger) is promoted into stage-3 scope alongside the swap.

**Ruling — trace body posture (2026-07-07).** A debug-run trace records **no request or response
bodies by default.** The baseline is metadata only: method, pre-authentication operation path,
status, byte counts, timing, step records, outputs, and wait state, with success-criteria verdicts
recorded *at source* by the runner (never re-derived post-hoc, which would need the response bodies).
Body capture is a per-environment administrative opt-in — default off, development-class only,
audited, the same shape and ceremony as `allowsDraftRuns`. Recording happens **pre-authentication**
as an invariant, so no credential material the auth provider adds can reach the trace (§13.5,
absolute). Bodies are size-capped with an explicit `truncated` marker, traces are purged with the
run, and capture-to-scenario shows the developer exactly which recorded bodies enter the
(git-reachable) working copy before they confirm. Any later expansion of body or PII persistence is a
fresh design decision, not an implementation detail.

**Built — mock runs + trigger injection wired (2026-07-10).** Both designer run modes are live.
*Mock:* the control plane wires the deterministic simulator (the simulate endpoint had been failing
closed), and the auto-mock answers each operation with a **schema-shaped** body (from the response's
JSON Schema), so `$response.body#/…` output pointers resolve and the trace shows values flowing.
*Remote debug run:* a message wait now advances via `POST …/debug-runs/{debugRunId}/inject-message`
(`{channel, payload, correlationId?}`, the `debugRuns` tag) — the payload is delivered **straight to
the suspended run** (the runner's `DeliverMessageAsync`, matched by channel + optional correlation),
the debug stand-in for the real publisher; nothing is published to a broker. A guard sits under both:
an Arazzo output (or workflow output, sub-workflow argument, request-body template value) whose
expression does not resolve is now **omitted**, never added as a `None`-valued property/item — the
generator would otherwise emit a builder call that trips a `Debug.Assert` and terminates the host on
the first such run. Verified live: a debug run of `onboard-customer-async` suspends on the
`kyc.verdict` receive and an injected `{verified, score}` resumes it to completion.
