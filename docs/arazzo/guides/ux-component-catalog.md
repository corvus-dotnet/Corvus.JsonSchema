# UX component catalog

This is the reference for every component in the Arazzo control-plane web kit: what it is, how to use it, and
how the components interact. For how to adopt, theme, and compose the kit, see the
[web UI kit guide](web-ui-kit.md); for the decisions behind its shape, the web-kit ADRs under
[`../adr/`](../adr/README.md).

Every element is a custom element registered through `define('arazzo-...', ...)` and extends `ArazzoElement`
(`src/components/base.js`). Each entry below lists the facets that apply to it. An omitted facet does not apply.

## At a glance

| Area | Components |
|------|-----------|
| [Runs](#runs) | `control-plane`, `runs-table`, `run-detail`, `cancel-button`, `resume-dialog`, `purge-dialog`, `workflow-step-picker`, `schedules` |
| [Runners](#runners) | `runners`, `runner-authorizations` |
| [Catalog](#catalog) | `catalog`, `catalog-table`, `catalog-detail`, `catalog-add-dialog`, `availability-matrix`, `availability-request-dialog`, `availability-requests`, `workflow-compare` |
| [Environments and sources](#environments-and-sources) | `environments`, `environment-input`, `environment-picker`, `filter-input`, `sources`, `source-operations`, `source-acquisition-dialog` |
| [Credentials](#credentials) | `credentials`, `credentials-table`, `credential-detail`, `credential-dialog` |
| [Security and access](#security-and-access) | `access-overview`, `access-requests`, `access-request-dialog`, `grants-panel`, `rules-panel`, `administrators-panel`, `grantee-picker`, `auth-status` |
| [Designer](#designer) | `design-surface`, `text-editor`, `workflow-inspector`, `step-inspector`, `document-inspector`, `criteria-editor`, `action-editor`, `outputs-editor`, `payload-editor`, `expression-input`, `schema-editor`, `value-editor`, `scenario-panel`, `scenario-editor`, `debug-tray`, `operation-browser`, `workspace-table`, `git-dialog`, `git-tree`, `github-connect` |
| [Shared primitives](#shared-primitives) | `pager`, `status-badge`, `splitbar`, `input-dialog`, `json-view`, `tag-editor`, and the picker family |

## Shared conventions

Every element extends `ArazzoElement` (`src/components/base.js`) and follows these conventions, so they are
not repeated per entry.

- **Client injection.** Set `.client` (a `ArazzoControlPlaneClient`), or a `base-url` attribute the element
  builds one from. Setting `.client` re-renders. A Layer 2 screen additionally exposes `.authProvider` (a
  function returning the `Authorization` header) and `.fetch`, shared with its children.
- **Events.** `emit(type, detail)` dispatches a bubbling, composed `CustomEvent`, so a host hears it across
  shadow roots.
- **Scope gating.** A `scopes` attribute (space-separated) shows, hides, or disables mutating controls. An
  absent attribute defers to the server.
- **Theming.** Components read `--arazzo-*` tokens from a themed ancestor (via `SHARED_CSS`, `PAGER_CSS`,
  `PICKER_CSS`), so a host themes the whole kit at once.
- **Dialogs.** `confirmDialog(host, {...})` shows a themed, focus-trapped confirmation inside the host's shadow
  root. The native `prompt` / `confirm` / `alert` are not used.

## Design conventions

Beyond the shared mechanics above, the components hold to these cross-cutting UX principles. A new component
follows them so the kit stays coherent.

- **Operator-facing copy carries nothing the operator cannot act on.** Product copy names things in the
  operator's terms and never leaks an internal token: no spec-section references, no ETags, cursors, raw
  identity tuples, or actor ids in a label, and the same holds for seeded descriptions.
- **Identity always renders as a resolved label.** A person, team, role, or workflow shows through one shared
  grantee chip as its resolved label ([ADR 0008](../adr/0008-resolved-grantee-resolution.md)); a raw `sys:`
  tuple, an issuer pin, or an actor string never reaches a list row or an audit line, and a system actor
  renders as a glyph rather than a raw service id.
- **Friction is proportional to severity.** A system-critical or genesis object (the baseline `*` binding, the
  founder binding, a load-bearing rule) carries a distinct badge, a typed-challenge confirmation to delete or
  degrade, and referential-integrity signalling ("in use by N grants", the strand warning), so no principal can
  casually lock the deployment out.
- **Permission gating is server-authoritative.** A component gates a privileged control from a `scopes`
  attribute whose absence defers to the server, never a hard-coded partial list
  ([ADR 0047](../adr/0047-web-kit-permission-gating-server-authoritative.md)). The server's fail-closed
  enforcement is the real control; the UI gating is a courtesy.
- **Actions live next to their data, and surfaces link to their counterparts.** A control sits on the record it
  acts on, and across a surface boundary a record links to its counterpart surface (a source to its
  credentials, a runner to its authorization, a run to its diagnosis) rather than making the operator navigate
  from scratch.

---

## Runs

### `<arazzo-control-plane>`

Layer 2 run-management screen: filters plus master/detail. Source: `arazzo-control-plane.js`.

- **Attributes:** `base-url`, `scopes` (gates `runs:purge`), `theme`, `poll`
- **Properties:** `.client`, `.authProvider`, `.fetch`; methods `reload()`, `refresh()`
- **Events:** re-emits `run-selected`, `run-changed`, `run-deleted`, `purge-completed`, `error`
- **Hosts:** `arazzo-runs-table`, `arazzo-run-detail`, `arazzo-purge-dialog`, `arazzo-workflow-id-input`

### `<arazzo-runs-table>`

Filterable, keyset-paged run list. Source: `runs-table.js`.

- **Attributes:** `base-url`, `status`, `workflow-id`, `page-size`, `poll`, `selectable`
- **Events:** `run-selected {run}`, `loaded`, `error`
- **Hosts:** `arazzo-pager`, `arazzo-status-badge`

### `<arazzo-run-detail>`

Full record plus scope-gated remediation for one run. Source: `run-detail.js`.

- **Attributes:** `base-url`, `runid`, `poll`, `scopes`
- **Events:** `run-changed`, `run-deleted`, `error`, `close`
- **Hosts:** `arazzo-cancel-button`, `arazzo-resume-dialog`, `arazzo-status-badge`

### `<arazzo-cancel-button>`

One-click cancel of a non-terminal run. Source: `cancel-button.js`.

- **Attributes:** `base-url`, `runid`, `label`, `no-confirm`
- **Events:** `run-cancelled`, `error`

### `<arazzo-resume-dialog>`

Modal for the four resume modes (retry, rewind, skip, state-patch,
[ADR 0022](../adr/0022-resume-mode-taxonomy.md)). Source: `resume-dialog.js`.

- **Methods:** `open(run)`, `close()`
- **Events:** `resume-submitted`, `error`
- **Hosts:** `arazzo-workflow-step-picker`, `arazzo-value-editor`

### `<arazzo-purge-dialog>`

Bulk reap of completed and cancelled runs (destructive, `runs:purge`). Source: `purge-dialog.js`.

- **Methods:** `open()`, `close()`
- **Events:** `purge-completed`

### `<arazzo-workflow-step-picker>`

Choose a run's target step by name for rewind or skip. Source: `workflow-step-picker.js`.

- **Attributes:** `workflow-id`, `cursor`, `direction`
- **Events:** `change {index, stepId}`

### `<arazzo-schedules>`

Durable-schedule management: list, run-now, delete. Source: `schedules-panel.js`.

- **Attributes:** `base-url`, `poll`, `page-size`
- **Events:** `loaded`, `error`
- **Hosts:** `arazzo-pager`

---

## Runners

### `<arazzo-runners>`

Read-only runner registry and health (online versus stale by heartbeat). Source: `runners-panel.js`.

- **Attributes:** `base-url`, `stale-after`, `poll`
- **Events:** `loaded`, `error`
- **Hosts:** `arazzo-pager`

### `<arazzo-runner-authorizations>`

The approver inbox for authorizing or revoking which runners may serve an environment
([ADR 0027](../adr/0027-runner-environment-binding.md)). Source: `runner-authorizations-panel.js`.

- **Attributes:** `base-url`, `environment`
- **Events:** `runner-authorization-decided`, `error`, `loaded`
- **Hosts:** `arazzo-environment-picker`, `arazzo-pager`

---

## Catalog

### `<arazzo-catalog>`

Layer 2 catalog browse and govern screen. Source: `arazzo-catalog.js`.

- **Attributes:** `base-url`, `scopes`, `theme`
- **Properties:** methods `openWorkflow(baseId)` (the deep-link entry), `reload()`
- **Events:** re-emits `version-selected`, `version-changed`, `version-deleted`, `workflow-added`, `error`
- **Hosts:** `arazzo-catalog-table`, `arazzo-catalog-detail`, `arazzo-catalog-add-dialog`, `arazzo-splitbar`

### `<arazzo-catalog-table>`

Catalog list, one row per base workflow. Source: `catalog-table.js`.

- **Attributes:** `base-url`, `q`, `status`, `owner`, `tags`, `page-size`, `selectable`
- **Events:** `version-selected`, `loaded`, `error`
- **Hosts:** `arazzo-pager`

### `<arazzo-catalog-detail>`

Full record plus governance for one version. The richest host in the kit. Source: `catalog-detail.js`.

- **Attributes:** `base-url`, `base-workflow-id`, `version-number`, `scopes`
- **Events:** `version-changed`, `version-deleted`, `access-requested`, `promotion-requested`, `close`, `error`
- **Hosts:** `arazzo-administrators-panel`, `arazzo-availability-matrix`, `arazzo-availability-request-dialog`,
  `arazzo-access-request-dialog` (locked to the workflow), `arazzo-workflow-compare`, `arazzo-tag-editor`,
  `arazzo-credential-dialog`

### `<arazzo-catalog-add-dialog>`

Guided wizard to add a workflow and its sources. Source: `catalog-add-dialog.js`.

- **Methods:** `open()`, `close()`
- **Events:** `workflow-added`
- **Hosts:** `arazzo-grantee-picker`, `arazzo-credential-dialog`

### `<arazzo-availability-matrix>`

The version-by-environment promotion grid (§7.8). Source: `availability-matrix.js`.

- **Attributes:** `base-url`, `base-workflow-id`, `selected-version`, `scopes`
- **Events:** `availability-changed`, `promotion-requested`, `loaded`, `error`
- **Hosts:** `arazzo-availability-request-dialog`

### `<arazzo-availability-request-dialog>`

"Request promotion" form. Source: `availability-request-dialog.js`.

- **Methods:** `open({...})`, `close()`
- **Events:** `availability-request-submitted`
- **Hosts:** `arazzo-workflow-picker`

### `<arazzo-availability-requests>`

Promotion request and approval surface (My requests, Approver inbox). Source: `availability-requests-panel.js`.

- **Attributes:** `base-url`, `view`, `environment`
- **Events:** `availability-request-submitted`, `availability-request-decided`, `error`, `loaded`
- **Hosts:** `arazzo-availability-request-dialog`, `arazzo-pager`

### `<arazzo-workflow-compare>`

Side-by-side, overlay, or text diff of two versions. Source: `workflow-compare.js`.

- **Properties:** `.left`, `.right`, `.diff`
- **Hosts:** two read-only `arazzo-design-surface` instances

---

## Environments and sources

### `<arazzo-environments>`

Master-detail over the environment registry (§7.7). Source: `environments-panel.js`.

- **Attributes:** `base-url`, `scopes`
- **Events:** `environment-selected/created/changed/deleted`, `loaded`, `error`
- **Hosts:** `arazzo-administrators-panel`, `arazzo-tag-editor`, `arazzo-splitbar`, `arazzo-pager`

### `<arazzo-filter-input>`

Text input with the kit's themed, filtered dropdown over caller-supplied items — the generic combo behind
the GitHub repository and branch pickers, whose lists can be very long. Properties: `items`
(`[{value, label?, sub?}]`), an optional async `lookup` that deepens the list while typing (debounced,
stale-guarded — the repo pickers use it so an owner-qualified query reaches repositories the session's seed
never contains), `value`, `readOnly`, `disabled`. Focus shows the full list; typing filters; a value outside
the list stays free-typable. Source: `filter-input.js`.

### `<arazzo-environment-input>`

Text input with a filtered environment dropdown; accepts a free-typed value. Source: `environment-input.js`.

- **Attributes:** `placeholder`, `value`, `base-url`
- **Events:** retargeted `input` / `change`

### `<arazzo-environment-picker>`

Type-to-search picker resolving to a real environment. Source: `environment-picker.js`.

- **Attributes:** `placeholder`, `value`, `base-url`
- **Events:** `environment-picked`

### `<arazzo-sources>`

Master-detail over the source registry (§7.6). Source: `sources-panel.js`.

- **Attributes:** `base-url`, `scopes`
- **Events:** `source-selected/created/changed/deleted`, `credential-saved`, `loaded`, `error`
- **Hosts:** `arazzo-source-operations`, `arazzo-json-view`, `arazzo-credential-dialog`, `arazzo-tag-editor`,
  `arazzo-splitbar`, `arazzo-pager`

### `<arazzo-source-operations>`

Read-only filterable operations and messages view over a source. Source: `source-operations.js`.

- **Properties:** `.source`
- **Events:** `loaded`, `error`

### `<arazzo-source-acquisition-dialog>`

Attach a source to a designer working copy (pick registered, fetch a URL, upload JSON).
Source: `source-acquisition-dialog.js`.

- **Methods:** `open({...})`, `close()`
- **Events:** `source-attached`
- **Hosts:** `arazzo-catalog-table`, `arazzo-github-connect`, `arazzo-text-editor`

---

## Credentials

### `<arazzo-credentials>`

Credential-rotation worklist as master-detail. Source: `credentials.js`.

- **Attributes:** `base-url`, `scopes`
- **Events:** re-emits `credential-selected/saved/deleted`, `error`
- **Hosts:** `arazzo-credentials-table`, `arazzo-credential-detail`, `arazzo-credential-dialog`, `arazzo-splitbar`

### `<arazzo-credentials-table>`

Status-first rotation worklist (valid, expiring, expired). Source: `credentials-table.js`.

- **Attributes:** `base-url`, `status`, `source`, `page-size`, `selectable`
- **Events:** `credential-selected`, `loaded`, `error`
- **Hosts:** `arazzo-environment-picker`, `arazzo-pager`

### `<arazzo-credential-detail>`

Full record plus scope-gated actions for one binding; references and metadata only, never secrets.
Source: `credential-detail.js`.

- **Attributes:** `base-url`, `scopes`
- **Events:** `credential-edit`, `credential-duplicate`, `credential-deleted`, `close`, `error`

### `<arazzo-credential-dialog>`

Create, edit, or rotate a source credential binding (references and non-secret metadata; refuses secret
material). Source: `credential-dialog.js`.

- **Methods:** `open(binding?)`, `close()`
- **Events:** `credential-saved`
- **Hosts:** `arazzo-environment-input`, `arazzo-grantee-picker`, `arazzo-tag-editor`

---

## Security and access

### `<arazzo-access-overview>`

The who-can-do-what overview for one grantee: reach grants, capabilities, administered workflows and
environments, credential usage ([ADR 0015](../adr/0015-access-overview-server-aggregated.md)).
Source: `access-overview-panel.js`.

- **Attributes:** `base-url`, `scopes` (gates Revoke)
- **Events:** `grantee-selected`, `revoked`, `open-workflow`, `open-environment`, `open-credential`, `error`
- **Hosts:** `arazzo-grantee-picker`, three `arazzo-pager`s (reach, administered, credentials)

### `<arazzo-access-requests>`

Access-request and approval surface (My requests, Approver queue, §16.5). Source: `access-requests-panel.js`.

- **Attributes:** `base-url`, `view`, `base-workflow-id`
- **Events:** `access-request-submitted`, `access-request-decided`, `error`, `loaded`
- **Hosts:** `arazzo-access-request-dialog`, `arazzo-workflow-id-input`, `arazzo-pager`

### `<arazzo-access-request-dialog>`

"Request access" form. Source: `access-request-dialog.js`.

- **Methods:** `open({...})`, `close()`
- **Events:** `access-request-submitted`
- **Hosts:** `arazzo-workflow-picker`

### `<arazzo-grants-panel>`

Author access grants: a claim mapped to per-verb reach
([ADR 0002](../adr/0002-grant-verbs-are-reach-not-scopes.md)). Source: `grants-panel.js`.

- **Attributes:** `base-url`, `scopes`
- **Events:** `grants-changed`, `loaded`, `error`
- **Hosts:** `arazzo-grantee-picker`, `arazzo-pager`

### `<arazzo-rules-panel>`

Manage reusable reach rules (named row-filter expressions). Source: `rules-panel.js`.

- **Attributes:** `base-url`, `scopes`
- **Events:** `rules-changed`, `loaded`, `error`
- **Hosts:** `arazzo-pager`

### `<arazzo-administrators-panel>`

Manage the administrator set of a workflow (§15) or an environment (§7.7). Source: `administrators-panel.js`.

- **Attributes:** `base-url`, `base-workflow-id` XOR `environment`, `scopes`
- **Events:** `administrators-changed`, `error`, `loaded`
- **Hosts:** `arazzo-grantee-picker`

### `<arazzo-grantee-picker>`

Resolve a real grantee to its exact identity ([ADR 0008](../adr/0008-resolved-grantee-resolution.md)).
Source: `grantee-picker.js`.

- **Attributes:** `placeholder`, `kind`, `kinds`, `source`, `base-url`
- **Events:** `grantee-selected`, `grantee-cleared`, `error`

### `<arazzo-auth-status>`

Optional BFF sign-in and sign-out chrome; self-discovers via `/me`, invisible when auth is disabled
([ADR 0042](../adr/0042-auth-agnostic-host-owns-session.md)). Source: `auth-status.js`.

---

## Designer

The designer is composed by the host (`demo/designer.html`); there is no single designer shell element.

### `<arazzo-design-surface>`

Editable, debuggable SVG diagram of a projected workflow graph
([ADR 0043](../adr/0043-first-party-svg-design-surface.md)). Source: `design-surface.js`.

- **Properties:** `.graph`, `.layoutEngine`, `.debugState`, `.diffState`, `.selection`
- **Events:** `selection-changed`, `node-activated`, `edge-created`, `edge-retargeted`, `operation-dropped`,
  `delete-requested`, `breakpoint-toggled`, `workflow-open`, and more

### `<arazzo-text-editor>`

Full-document CodeMirror text view over the document model. Source: `text-editor.js`.

- **Properties:** `.value`
- **Events:** `text-changed`

### `<arazzo-workflow-inspector>`

Workflow-level editor (summary, typed inputs, default actions, outputs). Source: `workflow-inspector.js`.

- **Properties:** `.value`
- **Events:** `workflow-changed`
- **Hosts:** `arazzo-schema-editor`, `arazzo-outputs-editor`, `arazzo-action-editor`

### `<arazzo-step-inspector>`

Editor for one step (binding, parameters, request body, criteria, actions, outputs).
Source: `step-inspector.js`.

- **Properties:** `.value`
- **Events:** `step-changed`
- **Hosts:** `arazzo-criteria-editor`, `arazzo-payload-editor`, `arazzo-outputs-editor`,
  `arazzo-expression-input`, `arazzo-action-editor`

### `<arazzo-document-inspector>`

Document-level editor (info, source descriptions, the reusable components library).
Source: `document-inspector.js`.

- **Properties:** `.value`
- **Events:** `document-changed`
- **Hosts:** `arazzo-schema-editor`, `arazzo-action-editor`

### `<arazzo-criteria-editor>`

Ordered list of Arazzo criterion rows. Source: `criteria-editor.js`.

- **Properties:** `.value`
- **Events:** `criteria-changed`
- **Hosts:** `arazzo-expression-input`

### `<arazzo-action-editor>`

One success or failure action (end, goto, retry). Source: `action-editor.js`.

- **Properties:** `.value`
- **Events:** `action-changed`
- **Hosts:** `arazzo-criteria-editor`

### `<arazzo-outputs-editor>`

A name to runtime-expression map editor. Source: `outputs-editor.js`.

- **Properties:** `.value`
- **Events:** `outputs-changed`
- **Hosts:** `arazzo-expression-input`

### `<arazzo-payload-editor>`

Schema-driven request-body and message-payload editor. Source: `payload-editor.js`.

- **Properties:** `.schema`, `.value`
- **Events:** `payload-changed`
- **Hosts:** `arazzo-expression-input`

### `<arazzo-expression-input>`

One-line runtime-expression editor with highlighting and schema completions (CodeMirror). The shared editing
control for criteria, parameters, and outputs. Source: `expression-input.js`.

- **Attributes:** `value`, `placeholder`, `readonly`
- **Events:** `value-changed`, `commit`, `validated`

### `<arazzo-schema-editor>`

Typed JSON Schema authoring form for inputs and library components. Source: `schema-editor.js`.

- **Properties:** `.value`
- **Events:** `schema-changed`, `library-create`, `library-open`
- **Hosts:** `arazzo-value-editor`

### `<arazzo-value-editor>`

Typed form that builds a JSON value from a type descriptor. Source: `value-editor.js`. Used by the resume
dialog (state-patch), the schema editor, and the debug tray.

- **Properties:** `.descriptor`, `.value`

### `<arazzo-scenario-panel>`

A working copy's scenario suite (list, run, verdicts). Source: `scenario-panel.js`.

- **Properties:** `.client`, `.workingCopyId`
- **Events:** `run-trace`, `scenarios-changed`, `error`
- **Hosts:** `arazzo-scenario-editor`

### `<arazzo-scenario-editor>`

Typed scenario form (inputs, mocks, expectations). Source: `scenario-editor.js`.

- **Properties:** `.scenario`
- **Events:** `scenario-changed`, `cancel`
- **Hosts:** `arazzo-value-editor`

### `<arazzo-debug-tray>`

Debug session tray: trace viewer, paused-context explorer, time-travel scrubber
([ADR 0045](../adr/0045-debug-runs-never-credentials-in-browser.md)). Source: `debug-tray.js`.

- **Properties:** `.trace`, `.cursor`
- **Events:** `cursor-changed`, `step-requested`, `output-override`, `trigger-injected`
- **Hosts:** a read-only `arazzo-design-surface`, `arazzo-json-view`, `arazzo-value-editor`

### `<arazzo-operation-browser>`

Designer left rail: attached sources and a searchable operation surface; click, keyboard, or drag to add a
step. Source: `operation-browser.js`.

- **Events:** `operation-selected`, `add-source-requested`, `source-detached`

### `<arazzo-workspace-table>`

Lists the designer's working copies, keyset-paged. Source: `workspace-table.js`.

- **Attributes:** `base-url`, `page-size`, `selectable`, `can-write`
- **Events:** `working-copy-selected/created/deleted`, `loaded`, `error`

### `<arazzo-git-dialog>`

Bind a working copy to a branch and round-trip (pull, commit, draft PR) as the user's GitHub identity (§4.7).
Source: `git-dialog.js`.

- **Methods:** `open({...})`, `close()`
- **Events:** `binding-saved`, `pulled`, `committed`, `error`
- **Hosts:** `arazzo-git-tree`, `arazzo-github-connect`, `arazzo-workflow-compare`

### `<arazzo-git-tree>`

Reusable lazy tree browser. Source: `git-tree.js`.

- **Properties:** `.loader`
- **Events:** `picked`

### `<arazzo-github-connect>`

Brokered GitHub session control (popup OAuth, polls to connected). Source: `github-connect.js`.

- **Events:** `github-connected`, `github-disconnected`, `error`

---

## Shared primitives

### `<arazzo-pager>`

The universal list footer: Prev / Next over a keyset cursor plus an info area
([ADR 0035](../adr/0035-keyset-pagination-everywhere.md)). Renders into light DOM on purpose, so its buttons
stay queryable from a host's shadow root and inherit the theme. Used by nearly every list.
Source: `pager.js`.

- **Events:** `prev`, `next`
- **Methods:** `update({hasPrev, hasNext, loading, info})`

### `<arazzo-status-badge>`

Presentational status pill, coloured by the canonical `statusColor` map. Source: `status-badge.js`.

- **Attributes:** `status`

### `<arazzo-splitbar>`

Draggable resize bar driving one CSS custom property. Source: `splitbar.js`.

- **Attributes:** `orientation`, `target`, `prop`, `min`, `max`, `storage-key`
- **Events:** `split-changed`

### `<arazzo-input-dialog>`

The kit's standard ask; the native `prompt` / `confirm` / `alert` are not used. Source: `input-dialog.js`.

- **Methods:** `ask({title, fields, confirmLabel, danger})` returns a Promise

### `<arazzo-json-view>`

Read-only syntax-highlighted JSON. Source: `json-view.js`.

- **Properties:** `.value`

### `<arazzo-tag-editor>`

Reusable key/value tag editor for the §14.2 security and management tags. Source: `tag-editor.js`.

- **Properties:** `.tags`
- **Events:** `tags-changed`

### The picker family

`arazzo-workflow-picker`, `arazzo-workflow-id-input`, `arazzo-environment-picker`, `arazzo-environment-input`,
and `arazzo-grantee-picker` share `PICKER_CSS`, retarget `input` / `change`, and resolve to real server
objects.

---

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
