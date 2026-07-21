# UX component catalog

This is the reference for every component in the Arazzo control-plane web kit: what it is, how to use it, and
how the components interact. For how to adopt, theme, and compose the kit, see the
[web UI kit guide](web-ui-kit.md); for the decisions behind its shape, the web-kit ADRs under
[`../adr/`](../adr/README.md).

Every element is a custom element registered through `define('arazzo-...', ...)` and extends `ArazzoElement`
(`src/components/base.js`). The shared conventions below apply to all of them.

## Shared conventions (`base.js`)

- **`ArazzoElement`** (extends `HTMLElement`): an open shadow root; `$` and `$$` query helpers; `emit(type,
  detail)` for a bubbling, composed `CustomEvent`; and client injection, where `get client()` lazily builds an
  `ArazzoControlPlaneClient` from the `base-url` attribute, or you set `.client` directly (setting it
  re-renders).
- **`define(tag, ctor)`** registers an element (idempotent).
- **CSS tokens.** `SHARED_CSS`, `PAGER_CSS`, `PICKER_CSS`, and `GRANTEE_CHIP_CSS` are bundles a component
  includes in its shadow `<style>`; components read `--arazzo-*` tokens from a themed ancestor rather than
  setting their own, so a host themes the whole kit at once.
- **Helpers.** `confirmDialog(host, {...})` (themed, focus-trapped, promise-based; the native `confirm` is not
  used), `granteeChip`, `statusColor`, `escapeHtml`, `relativeTime` / `countdown` / `absoluteTime`.
- **Injection model.** Every panel exposes `.client` (or `base-url`); Layer 2 screens additionally expose
  `.authProvider` (a function returning the `Authorization` header) and `.fetch` (an interceptor). Setting any
  rebuilds the client and re-applies it to children.
- **Scope gating.** A `scopes` attribute (space-separated) shows, hides, or disables mutating controls. An
  absent attribute defers to the server.

## Runs

- **`<arazzo-control-plane>`** (`arazzo-control-plane.js`). Layer 2 run-management screen (filters plus
  master/detail). Attributes `base-url`, `scopes` (gates `runs:purge`), `theme`, `poll`. Properties `.client`,
  `.authProvider`, `.fetch`; methods `reload()` / `refresh()`. Re-emits `run-selected`, `run-changed`,
  `run-deleted`, `purge-completed`, `error`. Hosts `arazzo-runs-table`, `arazzo-run-detail`,
  `arazzo-purge-dialog`, `arazzo-workflow-id-input`.
- **`<arazzo-runs-table>`** (`runs-table.js`). Filterable, keyset-paged run list. Attributes `base-url`,
  `status`, `workflow-id`, `page-size`, `poll`, `selectable`. Emits `run-selected {run}`, `loaded`, `error`.
  Hosts `arazzo-pager`, `arazzo-status-badge`.
- **`<arazzo-run-detail>`** (`run-detail.js`). Full record plus scope-gated remediation for one run.
  Attributes `base-url`, `runid`, `poll`, `scopes`. Emits `run-changed`, `run-deleted`, `error`, `close`. Hosts
  `arazzo-cancel-button`, `arazzo-resume-dialog`, `arazzo-status-badge`.
- **`<arazzo-cancel-button>`** (`cancel-button.js`). One-click cancel of a non-terminal run. Attributes
  `base-url`, `runid`, `label`, `no-confirm`. Emits `run-cancelled`, `error`.
- **`<arazzo-resume-dialog>`** (`resume-dialog.js`). Modal for the four resume modes (retry, rewind, skip,
  state-patch, [ADR 0022](../adr/0022-resume-mode-taxonomy.md)). Methods `open(run)`, `close()`. Emits
  `resume-submitted`, `error`. Hosts `arazzo-workflow-step-picker`, `arazzo-value-editor`.
- **`<arazzo-purge-dialog>`** (`purge-dialog.js`). Bulk reap of completed/cancelled runs (destructive,
  `runs:purge`). Methods `open()`, `close()`. Emits `purge-completed`.
- **`<arazzo-workflow-step-picker>`** (`workflow-step-picker.js`). Choose a run's target step by name for
  rewind/skip. Attributes `workflow-id`, `cursor`, `direction`. Emits `change {index, stepId}`.
- **`<arazzo-schedules>`** (`schedules-panel.js`). Durable-schedule management (list, run-now, delete).
  Attributes `base-url`, `poll`, `page-size`. Emits `loaded`, `error`. Hosts `arazzo-pager`.

## Runners

- **`<arazzo-runners>`** (`runners-panel.js`). Read-only runner registry and health (online vs stale by
  heartbeat). Attributes `base-url`, `stale-after`, `poll`. Emits `loaded`, `error`. Hosts `arazzo-pager`.
- **`<arazzo-runner-authorizations>`** (`runner-authorizations-panel.js`). The approver inbox for authorizing
  or revoking which runners may serve an environment ([ADR 0027](../adr/0027-runner-environment-binding.md)).
  Attributes `base-url`, `environment`. Emits `runner-authorization-decided`, `error`, `loaded`. Hosts
  `arazzo-environment-picker`, `arazzo-pager`.

## Catalog

- **`<arazzo-catalog>`** (`arazzo-catalog.js`). Layer 2 catalog browse/govern screen. Attributes `base-url`,
  `scopes`, `theme`. Methods `openWorkflow(baseId)` (the deep-link entry), `reload()`. Re-emits
  `version-selected`, `version-changed`, `version-deleted`, `workflow-added`, `error`. Hosts
  `arazzo-catalog-table`, `arazzo-catalog-detail`, `arazzo-catalog-add-dialog`, `arazzo-splitbar`.
- **`<arazzo-catalog-table>`** (`catalog-table.js`). Catalog list, one row per base workflow. Attributes
  `base-url`, `q`, `status`, `owner`, `tags`, `page-size`, `selectable`. Emits `version-selected`, `loaded`,
  `error`. Hosts `arazzo-pager`.
- **`<arazzo-catalog-detail>`** (`catalog-detail.js`). Full record plus governance for one version. Attributes
  `base-url`, `base-workflow-id`, `version-number`, `scopes`. Emits `version-changed`, `version-deleted`,
  `access-requested`, `promotion-requested`, `close`, `error`. The richest host: nests
  `arazzo-administrators-panel`, `arazzo-availability-matrix`, `arazzo-availability-request-dialog`,
  `arazzo-access-request-dialog` (locked to the workflow), `arazzo-workflow-compare`, `arazzo-tag-editor`,
  `arazzo-credential-dialog`.
- **`<arazzo-catalog-add-dialog>`** (`catalog-add-dialog.js`). Guided wizard to add a workflow and its sources.
  Methods `open()`, `close()`. Emits `workflow-added`. Hosts `arazzo-grantee-picker`, `arazzo-credential-dialog`.
- **`<arazzo-availability-matrix>`** (`availability-matrix.js`). The version-by-environment promotion grid
  (§7.8). Attributes `base-url`, `base-workflow-id`, `selected-version`, `scopes`. Emits `availability-changed`,
  `promotion-requested`, `loaded`, `error`. Hosts `arazzo-availability-request-dialog`.
- **`<arazzo-availability-request-dialog>`** (`availability-request-dialog.js`). "Request promotion" form.
  Methods `open({...})`, `close()`. Emits `availability-request-submitted`. Hosts `arazzo-workflow-picker`.
- **`<arazzo-availability-requests>`** (`availability-requests-panel.js`). Promotion request and approval
  surface (My requests, Approver inbox). Attributes `base-url`, `view`, `environment`. Emits
  `availability-request-submitted`, `availability-request-decided`, `error`, `loaded`. Hosts the dialog and
  `arazzo-pager`.
- **`<arazzo-workflow-compare>`** (`workflow-compare.js`). Side-by-side / overlay / text diff of two versions.
  Properties `.left`, `.right`, `.diff`. Hosts two read-only `arazzo-design-surface` instances.

## Environments and sources

- **`<arazzo-environments>`** (`environments-panel.js`). Master-detail over the environment registry (§7.7).
  Attributes `base-url`, `scopes`. Emits `environment-selected/created/changed/deleted`, `loaded`, `error`.
  Hosts `arazzo-administrators-panel`, `arazzo-tag-editor`, `arazzo-splitbar`, `arazzo-pager`.
- **`<arazzo-environment-input>`** (`environment-input.js`). Text input with a filtered environment dropdown,
  accepts a free-typed value. Attributes `placeholder`, `value`, `base-url`. Retargets `input` / `change`.
- **`<arazzo-environment-picker>`** (`environment-picker.js`). Type-to-search picker resolving to a real
  environment. Attributes `placeholder`, `value`, `base-url`. Emits `environment-picked`.
- **`<arazzo-sources>`** (`sources-panel.js`). Master-detail over the source registry (§7.6). Attributes
  `base-url`, `scopes`. Emits `source-selected/created/changed/deleted`, `credential-saved`, `loaded`, `error`.
  Hosts `arazzo-source-operations`, `arazzo-json-view`, `arazzo-credential-dialog`, `arazzo-tag-editor`,
  `arazzo-splitbar`, `arazzo-pager`.
- **`<arazzo-source-operations>`** (`source-operations.js`). Read-only filterable operations/messages view over
  a source. Property `.source`. Emits `loaded`, `error`.
- **`<arazzo-source-acquisition-dialog>`** (`source-acquisition-dialog.js`). Attach a source to a designer
  working copy (pick registered, fetch a URL, upload JSON). Methods `open({...})`, `close()`. Emits
  `source-attached`. Hosts `arazzo-catalog-table`, `arazzo-github-connect`, `arazzo-text-editor`.

## Credentials

- **`<arazzo-credentials>`** (`credentials.js`). Credential-rotation worklist as master-detail. Attributes
  `base-url`, `scopes`. Re-emits `credential-selected/saved/deleted`, `error`. Hosts `arazzo-credentials-table`,
  `arazzo-credential-detail`, `arazzo-credential-dialog`, `arazzo-splitbar`.
- **`<arazzo-credentials-table>`** (`credentials-table.js`). Status-first rotation worklist (valid, expiring,
  expired). Attributes `base-url`, `status`, `source`, `page-size`, `selectable`. Emits `credential-selected`,
  `loaded`, `error`. Hosts `arazzo-environment-picker`, `arazzo-pager`.
- **`<arazzo-credential-detail>`** (`credential-detail.js`). Full record plus scope-gated actions
  (edit/duplicate/revoke) for one binding; references and metadata only, never secrets. Attributes `base-url`,
  `scopes`. Emits `credential-edit`, `credential-duplicate`, `credential-deleted`, `close`, `error`.
- **`<arazzo-credential-dialog>`** (`credential-dialog.js`). Create/edit/rotate a source credential binding
  (references and non-secret metadata; refuses secret material). Methods `open(binding?)`, `close()`. Emits
  `credential-saved`. Hosts `arazzo-environment-input`, `arazzo-grantee-picker`, `arazzo-tag-editor`.

## Security and access

- **`<arazzo-access-overview>`** (`access-overview-panel.js`). The who-can-do-what overview for one grantee
  (reach grants, capabilities, administered workflows and environments, credential usage,
  [ADR 0015](../adr/0015-access-overview-server-aggregated.md)). Attributes `base-url`, `scopes` (gates
  Revoke). Emits `grantee-selected`, `revoked`, `open-workflow`, `open-environment`, `open-credential`,
  `error`. Hosts `arazzo-grantee-picker` and three `arazzo-pager`s (reach, administered, credentials).
- **`<arazzo-access-requests>`** (`access-requests-panel.js`). Access-request and approval surface (My
  requests, Approver queue, §16.5). Attributes `base-url`, `view`, `base-workflow-id`. Emits
  `access-request-submitted`, `access-request-decided`, `error`, `loaded`. Hosts `arazzo-access-request-dialog`,
  `arazzo-workflow-id-input`, `arazzo-pager`.
- **`<arazzo-access-request-dialog>`** (`access-request-dialog.js`). "Request access" form. Methods `open({...})`,
  `close()`. Emits `access-request-submitted`. Hosts `arazzo-workflow-picker`.
- **`<arazzo-grants-panel>`** (`grants-panel.js`). Author access grants: a claim mapped to per-verb reach
  ([ADR 0002](../adr/0002-grant-verbs-are-reach-not-scopes.md)). Attributes `base-url`, `scopes`. Emits
  `grants-changed`, `loaded`, `error`. Hosts `arazzo-grantee-picker`, `arazzo-pager`.
- **`<arazzo-rules-panel>`** (`scopes-panel.js`). Manage reusable reach rules (named row-filter expressions).
  Attributes `base-url`, `scopes`. Emits `scopes-changed`, `loaded`, `error`. Hosts `arazzo-pager`. Primary
  tag is `arazzo-rules-panel`; `arazzo-scopes-panel` is a kept-for-compatibility alias (see Deprecations).
- **`<arazzo-administrators-panel>`** (`administrators-panel.js`). Manage the administrator set of a workflow
  (§15) or an environment (§7.7). Attributes `base-url`, `base-workflow-id` XOR `environment`, `scopes`. Emits
  `administrators-changed`, `error`, `loaded`. Hosts `arazzo-grantee-picker`.
- **`<arazzo-grantee-picker>`** (`grantee-picker.js`). Resolve a real grantee to its exact identity
  ([ADR 0008](../adr/0008-resolved-grantee-resolution.md)). Attributes `placeholder`, `kind`, `kinds`,
  `source`, `base-url`. Emits `grantee-selected`, `grantee-cleared`, `error`. Falls back to hosting
  `arazzo-admin-grant-input`.
- **`<arazzo-admin-grant-input>`** (`admin-grant-input.js`). Interim `{dimension, value}` admin-identity input,
  superseded by `arazzo-grantee-picker` (see Deprecations).
- **`<arazzo-auth-status>`** (`auth-status.js`). Optional BFF sign-in / sign-out chrome; self-discovers via
  `/me`, invisible when auth is disabled ([ADR 0042](../adr/0042-auth-agnostic-host-owns-session.md)).

## Designer

The designer is composed by the host (`demo/designer.html`); there is no single designer shell element.

- **`<arazzo-design-surface>`** (`design-surface.js`). Editable, debuggable SVG diagram of a projected workflow
  graph ([ADR 0043](../adr/0043-first-party-svg-design-surface.md)). Properties `.graph`, `.layoutEngine`,
  `.debugState`, `.diffState`, `.selection`. Emits `selection-changed`, `node-activated`, `edge-created`,
  `edge-retargeted`, `operation-dropped`, `delete-requested`, `breakpoint-toggled`, `workflow-open`, and more.
- **`<arazzo-text-editor>`** (`text-editor.js`). Full-document CodeMirror text view over the document model.
  Property `.value`. Emits `text-changed`.
- **`<arazzo-workflow-inspector>`** (`workflow-inspector.js`). Workflow-level editor (summary, typed inputs,
  default actions, outputs). Property `.value`. Emits `workflow-changed`. Hosts `arazzo-schema-editor`,
  `arazzo-outputs-editor`, `arazzo-action-editor`.
- **`<arazzo-step-inspector>`** (`step-inspector.js`). Editor for one step (binding, parameters, request body,
  criteria, actions, outputs). Property `.value`. Emits `step-changed`. Hosts `arazzo-criteria-editor`,
  `arazzo-payload-editor`, `arazzo-outputs-editor`, `arazzo-expression-input`, `arazzo-action-editor`.
- **`<arazzo-document-inspector>`** (`document-inspector.js`). Document-level editor (info, source
  descriptions, the reusable components library). Property `.value`. Emits `document-changed`. Hosts
  `arazzo-schema-editor`, `arazzo-action-editor`.
- **`<arazzo-criteria-editor>`** (`criteria-editor.js`). Ordered list of Arazzo criterion rows. Property
  `.value`. Emits `criteria-changed`. Hosts `arazzo-expression-input`.
- **`<arazzo-action-editor>`** (`action-editor.js`). One success/failure action (end, goto, retry). Property
  `.value`. Emits `action-changed`. Hosts `arazzo-criteria-editor`.
- **`<arazzo-outputs-editor>`** (`outputs-editor.js`). A name to runtime-expression map editor. Property
  `.value`. Emits `outputs-changed`. Hosts `arazzo-expression-input`.
- **`<arazzo-payload-editor>`** (`payload-editor.js`). Schema-driven request-body / message-payload editor.
  Properties `.schema`, `.value`. Emits `payload-changed`. Hosts `arazzo-expression-input`.
- **`<arazzo-expression-input>`** (`expression-input.js`). One-line runtime-expression editor with
  highlighting and schema completions (CodeMirror). Attributes `value`, `placeholder`, `readonly`. Emits
  `value-changed`, `commit`, `validated`. The shared editing control for criteria, parameters, and outputs.
- **`<arazzo-schema-editor>`** (`schema-editor.js`). Typed JSON Schema authoring form for inputs and library
  components. Property `.value`. Emits `schema-changed`, `library-create`, `library-open`. Hosts
  `arazzo-value-editor`.
- **`<arazzo-value-editor>`** (`value-editor.js`). Typed form that builds a JSON value from a type descriptor.
  Properties `.descriptor`, `.value`. Used by the resume dialog (state-patch), schema editor, and debug tray.
- **`<arazzo-scenario-panel>`** (`scenario-panel.js`). A working copy's scenario suite (list, run, verdicts).
  Properties `.client`, `.workingCopyId`. Emits `run-trace`, `scenarios-changed`, `error`. Hosts
  `arazzo-scenario-editor`.
- **`<arazzo-scenario-editor>`** (`scenario-editor.js`). Typed scenario form (inputs, mocks, expectations).
  Property `.scenario`. Emits `scenario-changed`, `cancel`. Hosts `arazzo-value-editor`.
- **`<arazzo-debug-tray>`** (`debug-tray.js`). Debug session tray: trace viewer, paused-context explorer,
  time-travel scrubber ([ADR 0045](../adr/0045-debug-runs-never-credentials-in-browser.md)). Properties
  `.trace`, `.cursor`. Emits `cursor-changed`, `step-requested`, `output-override`, `trigger-injected`. Hosts
  a read-only `arazzo-design-surface`, `arazzo-json-view`, `arazzo-value-editor`.
- **`<arazzo-operation-browser>`** (`operation-browser.js`). Designer left rail: attached sources and a
  searchable operation surface; click, keyboard, or drag to add a step. Emits `operation-selected`,
  `add-source-requested`, `source-detached`.
- **`<arazzo-workspace-table>`** (`workspace-table.js`). Lists the designer's working copies, keyset-paged.
  Attributes `base-url`, `page-size`, `selectable`, `can-write`. Emits `working-copy-selected/created/deleted`,
  `loaded`, `error`.
- **`<arazzo-git-dialog>`** (`git-dialog.js`). Bind a working copy to a branch and round-trip (pull, commit,
  draft PR) as the user's GitHub identity (§4.7). Methods `open({...})`, `close()`. Emits `binding-saved`,
  `pulled`, `committed`, `error`. Hosts `arazzo-git-tree`, `arazzo-github-connect`, `arazzo-workflow-compare`.
- **`<arazzo-git-tree>`** (`git-tree.js`). Reusable lazy tree browser. Property `.loader`. Emits `picked`.
- **`<arazzo-github-connect>`** (`github-connect.js`). Brokered GitHub session control (popup OAuth, polls to
  connected). Emits `github-connected`, `github-disconnected`, `error`.

## Shared primitives

- **`<arazzo-pager>`** (`pager.js`). The universal list footer: Prev / Next over a keyset cursor plus an info
  area ([ADR 0035](../adr/0035-keyset-pagination-everywhere.md)). Renders into light DOM on purpose, so its
  buttons stay queryable from a host's shadow root and inherit the theme. Emits `prev`, `next`; method
  `update({hasPrev, hasNext, loading, info})`. Used by nearly every list.
- **`<arazzo-status-badge>`** (`status-badge.js`). Presentational status pill, coloured by the canonical
  `statusColor` map. Reflects a `status` attribute.
- **`<arazzo-splitbar>`** (`splitbar.js`). Draggable resize bar driving one CSS custom property. Attributes
  `orientation`, `target`, `prop`, `min`, `max`, `storage-key`. Emits `split-changed`.
- **`<arazzo-input-dialog>`** (`input-dialog.js`). The kit's standard ask: `ask({title, fields, confirmLabel,
  danger})` returns a Promise. The native `prompt` / `confirm` / `alert` are not used.
- **`<arazzo-json-view>`** (`json-view.js`). Read-only syntax-highlighted JSON. Property `.value`.
- **`<arazzo-tag-editor>`** (`tag-editor.js`). Reusable key/value tag editor for the §14.2 security and
  management tags. Property `.tags`. Emits `tags-changed`.
- **The picker family** (share `PICKER_CSS`, retarget `input` / `change`, resolve to real server objects):
  `arazzo-workflow-picker`, `arazzo-workflow-id-input`, `arazzo-environment-picker`, `arazzo-environment-input`,
  `arazzo-grantee-picker`.

## Interaction map

- **Master to detail.** A table emits a selection that bubbles to its Layer 2 screen, which mounts the detail
  pane; a change in the detail bubbles back and refreshes the table. This is the shape for runs, catalog,
  credentials, environments, and sources.
- **The access overview is the deep-link hub.** Its `open-workflow`, `open-environment`, and `open-credential`
  events are routed by the host to the catalog (`openWorkflow(baseId)`), environments, and credentials
  surfaces.
- **Governance dialogs from records.** `arazzo-catalog-detail` raises access and promotion requests through the
  embedded request dialogs, which also appear standalone in the request inboxes.
- **Designer flow.** The operation browser requests a source acquisition; a drag drops onto the design surface;
  the surface's selection drives the step, workflow, and document inspectors, whose changes update the document
  model and the text editor; the scenario panel hands a run trace to the debug tray; the git dialog opens a
  compare and drives the GitHub connect.

## Deprecations

- **`arazzo-scopes-panel` is an alias of `arazzo-rules-panel`** (`scopes-panel.js`). `arazzo-rules-panel` is
  the primary registration; `arazzo-scopes-panel` is a kept-for-compatibility alias subclass. "Rule" is the
  user-facing term; the `scopes-changed` event and the `scopes` file name persist only for back-compatibility.
- **`arazzo-admin-grant-input` is superseded by `arazzo-grantee-picker`** (`admin-grant-input.js`). It is the
  interim free-form `{dimension, value}` input; the picker resolves a real grantee to its exact identity
  ([ADR 0008](../adr/0008-resolved-grantee-resolution.md)) and is the correct-by-construction successor (it
  still falls back to hosting the interim input).
