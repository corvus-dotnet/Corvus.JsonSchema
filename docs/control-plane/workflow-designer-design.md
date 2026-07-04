# Arazzo Workflow Designer — design

A **design-test-and-validate environment** for Arazzo workflows, delivered as reusable web components
in the existing kit (`web/arazzo-control-plane-ui`). An author finds or adds OpenAPI/AsyncAPI sources,
browses their operation surface to create steps, edits success/failure handling and criteria with
proper editor support, manages inputs/outputs through the schema-driven typed forms, exercises the
workflow interactively against a mock transport and virtual clock, captures that behaviour as
scenarios, and publishes to the catalog with the evidence of successful validation. Terms are as
defined in [`UBIQUITOUSLANGUAGE.md`](./UBIQUITOUSLANGUAGE.md).

Status: **design**. Nothing in this document is built; §14 sequences the delivery.

## 1. Goals & non-goals

**Goals**

- **Cover the full Arazzo 1.1 capability surface.** Everything the schema can express is editable:
  all step binding kinds (OpenAPI operation, nested workflow, AsyncAPI channel with
  `action`/`correlationId`/`timeout`), all criterion types, workflow-level and step-level actions,
  reusable components, payload replacements, `querystring` parameters, `dependsOn`, multiple
  workflows per document.
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
- No collaborative real-time editing (CRDT/multi-cursor). Working copies use etag concurrency; Git
  integration covers multi-author flows.
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
  (halts before the next step); **Stop** terminates the session and clears the overlay. "Step"
  advances one step; "run to here" targets a step. *Every* pause — breakpoint, manual pause, or
  step — hands the paused context to the context explorer and expression console below for
  inspection; resuming is a §8.2 replay with the next stop condition.
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
| `listSourceOperations` | `GET /workspace/workflows/{id}/sources/{name}/operations` and `GET /sources/{name}/operations` | The operation surface: `OperationDescriptor` / `AsyncApiChannelDescriptor` projections (id, path/channel, method/action, summary, parameters, request/response types) from the existing `DescribeOperations` machinery. |

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

**Promotion readiness (proposed extension):** an environment can require evidence — `readiness =
credentials ∧ (evidence.suiteGreen ∨ environment.allowsUnevidenced)`. Deployment-configurable so
existing promotion behaviour is unchanged by default.

### 4.7 GitHub integration — brokered; `workspace:write` + host-configured

GitHub's OAuth token exchange has no CORS support, so the browser cannot complete an auth flow
alone; the control plane brokers a **GitHub App** (fine-grained, repo-scoped, short-lived
user-to-server tokens):

| Operation | HTTP | Purpose |
|-----------|------|---------|
| `beginGitHubAuth` / `completeGitHubAuth` | `GET /github/auth` → redirect; callback exchanges the code server-side | Standard web-application flow; the control plane holds the App credentials and the user token (server-side session or encrypted at rest with a KMS ref — never in the browser). |
| `getGitHubStatus` | `GET /github/session` | Signed-in identity + installations + accessible repos. |
| `browseRepo` | `GET /github/repos/{owner}/{repo}/contents?ref&path` | Proxied browse for the open/import dialogs. |
| `bind` (on the working copy) | part of `PUT /workspace/workflows/{id}` | `gitBinding: {owner, repo, branch, path, specPaths?, scenarioPaths?}` — a working copy may be **Git-bound**; scenarios round-trip as individual `<name>.scenario.json` files (the §4.5 CI layout). |
| `pullWorkingCopy` / `commitWorkingCopy` | `POST /workspace/workflows/{id}/git/{pull,commit}` | Pull: refresh document (+ bound source docs + scenarios) from the branch (etag/merge guard). Commit: write document (+ scenario files) to the branch with a message; optionally open a PR (`draft` → review flow for workflow development). |

Uses: version management of workflows *and* their OpenAPI/AsyncAPI specs during development
(branch-per-working-copy is the natural multi-author flow); importing specs from repos; optionally
pushing the published package inputs + evidence to a release branch/tag at publish. GitHub Enterprise
Server is configuration (base URL), not new design. The kit never sees a GitHub credential; it calls
the control plane.

## 5. Kit architecture (Layer 0 / 0.5 / 1 / 2)

### 5.1 Layer 0 — client extensions

`ArazzoControlPlaneClient` gains the §4 methods (workspace, scenarios, simulate, fetch, publish,
github). Same conventions: `ProblemError`, keyset paging, host-supplied auth, conformance-tested
against the OpenAPI document.

### 5.2 Layer 0.5 — `WorkflowDocumentModel` (new, DOM-free)

A shared observable model over the Arazzo document, imported by both editors and the inspector:

- Holds the parsed document + a change-event stream; all edits go through it (canvas, inspector,
  text) so the editors never talk to each other.
- **Undo/redo** stack (grouped operations, e.g. "add step + bind operation" is one undo unit).
- Two-way text sync: serializes deterministically for Monaco; on text edits, reparses and diffs
  (structural diff keyed by `stepId`/`workflowId` so canvas positions and selection survive edits).
- Derived views: the **graph projection** (nodes/edges incl. the inherited-defaults layer — §6.2),
  the diagnostics overlay, and the expression-completion context (from the working-copy schemas).
- Layout persistence: node positions are UI state, not document content — stored in the working
  copy's `designerState` (a sibling field, never written into the Arazzo document, never packaged).

### 5.3 Layer 1 — new components

All extend `ArazzoElement`; `SHARED_CSS` + `--arazzo-*` tokens; events bubble composed; explicit
loading/empty/error states; scope-gated actions. (Attribute/event tables in the style of
`ui-design.md` to be finalized per-slice; the inventory and responsibilities:)

| Element | Responsibility |
|---------|----------------|
| `<arazzo-workspace-table>` | Working copies list: open, create (blank / from version / from Git), delete. Emits `working-copy-selected`. |
| `<arazzo-design-surface>` | The diagram area (§6). Renders the graph projection; direct manipulation (add/move/connect/delete, marquee select, pan/zoom, auto-layout); debug overlay (active step, taken edges, breakpoints); emits `selection-changed`, `step-created`, `edge-created`, `breakpoint-toggled`, … |
| `<arazzo-text-editor>` | CodeMirror 6 wrapper (lazy-loaded, §7); document text mode with schema validation markers, expression token highlighting, selection sync. |
| `<arazzo-operation-browser>` | Left rail: the document's sources + each one's operation surface (search/filter; drag or click-to-add). "Add source…" opens the acquisition dialog. |
| `<arazzo-source-acquisition-dialog>` | Add a source: pick from registry · fetch from URL (+ optional credential) · upload · import from GitHub. |
| `<arazzo-step-inspector>` | Selected step: binding (operation/workflow/channel pickers), parameters (typed), `requestBody` payload (typed via descriptor) + replacements, success criteria, local actions, outputs. |
| `<arazzo-workflow-inspector>` | Selected workflow: `inputs` schema editor, workflow-level actions (the defaults layer), `outputs`, `parameters`, `dependsOn`. |
| `<arazzo-document-inspector>` | `info`, source descriptions, `components` (reusable library management). |
| `<arazzo-criteria-editor>` | An ordered list of criterion rows: type picker (`simple`/`regex`/`jsonpath`/`xpath` + version), `context` expression input, condition editor with syntax highlighting + live server validation. |
| `<arazzo-expression-input>` | Single-line highlighted runtime-expression / JSONPath editor with completions from the schema context (§7.2). Reused by criteria, parameters, outputs, payload replacements, scenario expectations. |
| `<arazzo-action-editor>` | One success/failure action: type (`end`/`goto`/`retry`), target step/workflow picker, retry settings, criteria; "localize/revert to inherited" affordances. |
| `<arazzo-scenario-panel>` | Scenario suite: list + per-scenario status chips, run one/all, open editor, "save session as scenario". |
| `<arazzo-scenario-editor>` | Typed scenario editing: inputs (typed form from the workflow inputs schema), mocks (per-operation response scripting, response bodies typed from the *source* schemas), triggers, clock, expectations (criteria editors). |
| `<arazzo-debug-controls>` | Run/pause/step/run-to/continue/stop, breakpoint list, virtual-clock control (advance to due / +Δ), trigger injection, session status. |
| `<arazzo-context-explorer>` | Paused-context tree (`$inputs`, `$steps.*`, request/response) + the expression console. |
| `<arazzo-trace-viewer>` | The recorded trace: step timeline with per-step request/response and criterion truth tables; the time-travel scrubber; click-to-navigate to canvas/inspector. |
| `<arazzo-evidence-badge>` | Evidence summary for a catalog version (suite verdict, count, at); embedded by `<arazzo-catalog-detail>`. |
| `<arazzo-github-dialog>` | Sign-in status, repo/branch/path binding, pull/commit (+PR) actions for a Git-bound working copy. |

Reused as-is: `<arazzo-value-editor>` (typed forms), `<arazzo-workflow-picker>`,
`<arazzo-grantee-picker>` (workspace administration), `<arazzo-catalog-add-dialog>` patterns
(publish dialog), pager, status badge, confirm dialog.

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
2. **Operation surface & sources.** `listSourceOperations` (+ registry variant), attach/upload,
   `fetchSourceDocument` (authenticated URL); operation browser + acquisition dialog; step
   creation via inspector (form-first, still no canvas).
3. **Design surface v1.** Graph projection + chosen canvas technology: render, select, inspector
   wiring, add/move/connect/delete, defaults layer, auto-layout, `designerState` persistence.
4. **Inspectors complete.** Step/workflow/document inspectors, criteria/action editors,
   expression inputs with highlighting + completions; full Arazzo 1.1 editing parity (the §1
   coverage checklist holds from this slice on).
5. **Simulator + debug.** Server `WorkflowSimulator` + trace + stateless stepping; debug controls,
   context explorer, trace viewer, canvas overlay, expression console.
6. **Scenarios.** Scenario schema + CRUD + run endpoints (run-one and run-all suite report);
   scenario panel/editor; session recording; carry-over on create-from-version; **the `scenarios
   run` CLI** (§4.5: standalone in-process + remote modes, globbing, JUnit/JSON reports, CI exit
   codes).
7. **Publish with evidence.** Publish endpoint (server-attested suite), package entries, evidence
   badge on catalog detail; optional promotion-readiness extension.
8. **GitHub.** App broker + session endpoints; bind/pull/commit(+PR) incl. scenario files;
   import-from-repo in the acquisition dialog; **the GitHub Action wrapper** for the scenario
   runner (§4.5).

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
