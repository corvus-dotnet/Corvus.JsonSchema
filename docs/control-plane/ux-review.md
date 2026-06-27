# Arazzo Control Plane — UX review & task catalogue

A fresh-eyes review of the whole control-plane web UI (`web/arazzo-control-plane-ui`): the information
architecture, the people who use it and what they are trying to do, a step-by-step catalogue of every common
task, and a prioritised set of findings. The task flows are written as **captioned storyboards** — each step is
one caption over one screen state — so they double as a shot list for short screen-capture videos (see §6).

> Method: read every component and the demo composition with fresh eyes, traced each task end to end against the
> actual controls, and judged it against common best practice for operator consoles and approval/permission UIs.
> Nothing here changes behaviour — it is a review.

---

## 1. Information architecture

The kit composes into a four-tab console (`demo/index.html`):

| Tab | What it is | Components |
|---|---|---|
| **Runs** | Live run management — monitor, diagnose, recover | `arazzo-control-plane` (runs-table, run-detail, resume/cancel/purge dialogs, status-badge) |
| **Catalog** | The workflow registry + per-workflow administration | `arazzo-catalog` (catalog-table, catalog-detail, add-dialog, administrators-panel, workflow-id / step pickers) |
| **Sources** | The source credentials ("connections") a run uses | `credentials-table`, `credential-dialog`, `grantee-picker` |
| **Access** | Reach scopes, grants, and the approval inbox | `grants-panel`, `scopes-panel`, `access-requests-panel`, `grantee-picker`, `access-request-dialog` |

Each tab is a self-contained web component that takes a `base-url` + `scopes` and is permission-gated: a control
the caller's scopes don't allow is hidden (or shown disabled with a `Requires …` tooltip when `show-forbidden`
is set). The whole kit is theme-token driven (light/dark/auto).

---

## 2. Personas

| Persona | Lives in | Core job |
|---|---|---|
| **Operator / SRE** | Runs | Keep workflow runs healthy; find and recover faulted runs fast |
| **Workflow publisher / owner** | Catalog | Publish and curate workflow versions; hand off administration |
| **Platform / connections admin** | Sources | Register the credentials a workflow's runs need; control who may use them |
| **Security admin** | Access → Scopes + Grants | Define the reusable reach vocabulary and grant reach to identities |
| **Workflow administrator (approver)** | Access → Approver queue | Clear the inbox of access requests for the workflows they administer |
| **Requester (any authenticated user)** | Access → My requests | Ask for run access to a workflow and track the outcome |

**Cross-cutting design ethos — "correct by construction".** The UI repeatedly steers users to safe choices
rather than warning them after the fact: identities are *picked and resolved*, never hand-typed (grantee picker);
a per-person grant is *refused at the UI* and steered to the access-request flow; a partial identity is flagged
because grants match by exact identity; `runs:write` auto-selects-and-locks `runs:read`; the credential dialog
accepts a *secret reference*, never a secret. These are the strongest parts of the experience and the review's
recommendations try hard not to erode them.

---

## 3. Runs — task catalogue

**Goals.** See run health at a glance; slice the run set down to the few that need attention; understand *why* a
run faulted; recover it with the least-destructive surgery; tidy up old runs.

### 3.1 Monitor & triage
1. **Open the Runs tab.** A table loads the most recent runs; a colour-coded status pill leads each row (Pending/Running blue, Suspended amber, Completed green, Cancelled grey, Faulted red).
2. **Turn on `auto-refresh`** (toolbar checkbox) to poll every few seconds, or hit `↻` to refresh once.
3. **Read each row** — status, workflow id, short run id (with a copy button), age, what it's *waiting on*, the error type if faulted, and tags.

### 3.2 Narrow to what matters
1. **Click a status chip** (`All`, `Faulted`, `Suspended`, …) to filter; the table resets to the first page.
2. **Type a workflow id, tags, or correlation id** (debounced) to filter further; tags are AND-ed.
3. **Set a created/updated time window**, or `Clear dates` to reset.
4. **Page** with `‹ Prev` / `Next ›` (keyset).

### 3.3 Diagnose a fault
1. **Click a faulted run.** The detail pane opens beside the table.
2. **Read the red Fault block** — the step that faulted, the attempt number, the timestamp, and the error message in monospace.
3. **Check the definition list** — cursor (which step), inputs, correlation id (copyable) — to decide the recovery.

### 3.4 Recover a faulted run
1. **Click `Resume…`** in the detail pane.
2. **Pick a mode** (each a labelled radio card): **Retry the faulted step** (default, the common case); **Rewind** to an earlier step (pick it from the step picker); **Skip** past it (optionally record outputs for the skipped step, validated against the step's schema); **State patch** (an RFC-6902 patch over `{ inputs, stepOutputs }`, then retry).
3. **Fill the mode's fields** and **click `Resume`.** The detail and table refresh.

### 3.5 Cancel a run
1. **Click `Cancel`** (non-terminal runs only) → confirm dialog.
2. **Optionally give a reason**, then **`Cancel run`.** Status becomes Cancelled.

### 3.6 Bulk purge
1. **Click `Purge…`** (needs `runs:purge`).
2. **Choose a cutoff** — type a date or click a `7 / 30 / 90 days` preset; optionally cap the count.
3. **`Purge` → confirm.** A green "Purged N runs" result appears; the table reloads. (Only Completed/Cancelled runs are reaped.)

---

## 4. Catalog & administrators — task catalogue

**Goals.** Find a workflow and inspect exactly what a version contains; publish a new version (or a brand-new
workflow) safely; retire or delete versions; control *who* may publish further versions.

### 4.1 Browse & inspect
1. **Open the Catalog tab.** One row per base workflow (latest version as the representative).
2. **Filter** by status chip (`All`/`Active`/`Obsolete`) and the title/owner/tags search inputs.
3. **Click a row.** The detail pane shows the version's description, content hash (copyable), tags, owner (name/email/team/url), full audit trail, the source descriptions (each downloadable), and `Package (.awp)` / `Workflow (.json)` downloads.
4. **Switch versions** with the header version dropdown.

### 4.2 Publish a new workflow / version
1. **Click `Add workflow…`** (needs `catalog:write`).
2. **Choose `Build from documents`** (default) or `Upload package (.awp)`.
3. **(Build) Select the Arazzo workflow `.json`.** The dialog reads its `sourceDescriptions` and asks for each source document, each with a *"set up a credential binding after adding"* checkbox.
4. **Seed administrators.** The workflow's own identity is pre-filled (removable); add more via the `{workflow | tenant}` dimension picker + `+ Add administrator`.
5. **Fill owner** (name + email required) and optional tags.
6. **Click `Add workflow`.** The version lands, staged administrators apply, and any checked credential bindings open in sequence; the new version's detail opens.

### 4.3 Retire / delete a version
1. **`Obsolete…`** (needs `catalog:write`) → confirm. The badge flips to Obsolete and the audit records who/when. It stays in the catalog.
2. **`Delete…`** (needs `catalog:purge`) → confirm (danger). Refused while runs still reference it.

### 4.4 Administer a workflow
1. **Open a workflow's detail** and scroll to **Security — Administrators**.
2. **`Add`** (needs `administrators:write`) → the **grantee picker** resolves a team/role to its exact identity → `Add`.
3. **`✕ Remove`** an administrator → confirm. The last administrator cannot be removed.
   *(Non-disclosing: without permission you simply can't act; the set is never leaked.)*

---

## 5. Sources / connections — task catalogue

**Goals.** Register the credentials a workflow's runs use, as *references into a secret store* (never raw
secrets); keep them rotated and visible (expiry); say which identity's runs may use each one.

### 5.1 Browse & monitor
1. **Open the Sources tab.** A table of credential bindings with a colour-coded validity status, the secret reference (read-only), the resolved usage grantee, and an expiry countdown.
2. **Filter** by status (`all / valid / expiring soon / expired`) and source-name substring.
3. **`Load more`** to page (keyset).

### 5.2 Register a credential ("connection")
1. **Click `New credential`** (needs `credentials:write`). The dialog states plainly: *references and non-secret metadata only — the secret is never entered here.*
2. **Identity** — source name, environment, auth kind (`apiKey`/`bearer`/`basic`/`oauth2ClientCredentials`), optional expiry + description.
3. **Secret references** — for each secret the auth kind needs, pick a store (Key Vault / AWS Secrets Manager / Vault / env / file / raw) and fill its guided fields; a **live preview** shows the composed `scheme://locator#version`. Inline secrets are rejected.
4. **Config** — the auth kind's non-secret fields (e.g. token URL, client id) plus an escape hatch for extra entries.
5. **Usage** — pick the grantee whose runs may use this binding (grantee picker; partial identities are flagged).
6. **`Create`.** The binding appears in the table.

### 5.3 Rotate / edit
1. **Click a row → edit.** Source name + environment are read-only; secret refs and config pre-fill.
2. **Change a secret reference** → on save the binding is auto-stamped `rotatedAt` (no separate "rotate" action).

---

## 6. Access — scopes, grants & the approval inbox

**Goals.** *Security admin:* maintain a reusable reach vocabulary and grant reach to identities. *Requester:*
ask for run access. *Approver:* clear the inbox of requests they can act on.

### 6.1 Author a scope (reusable reach rule)
1. **Open Access → Scopes** and **search** (debounced, server-side — searches the whole vocabulary, then `Load more`).
2. **`New scope`** → **pick a goal template**: a label value, one-of a set, matches the caller's claim, shares-any-label, ABAC superset, a classification level, or *Advanced* (raw expression).
3. **Fill the template's fields** — a **live preview** shows the expression and the name auto-suggests.
4. **`Create`.** (`Edit`/`Delete` from each row; deleting warns that grants referencing it lose that reach.)

### 6.2 Author a grant (give an identity reach)
1. **Open Access → Grants** and **search** (server-side + `Load more`).
2. **`New grant` → WHO:** use the **grantee picker** to find a team/role. *(Pick a person and the form refuses it and points you at the access-request flow; a partial identity is flagged.)*
3. **WHERE:** for each of **read / write / purge** choose `Denied`, `Unrestricted`, or `Scoped`; for `Scoped`, add one or more scopes via the **server-backed typeahead** (each becomes a removable chip).
4. **`Create`.** (Editing locks the claim *type*; the value stays editable.)

### 6.3 Request access (requester)
1. **Access → My requests → `Request access…`.**
2. **Pick the workflow**, choose scopes (*View*, *Read runs*, *Trigger/resume/cancel runs* — checking write auto-locks read), optionally a reason and a duration.
3. **`Submit request`.** It appears under *My requests* as Pending; **`Withdraw`** while pending.

### 6.4 Clear the approver inbox (approver)
1. **Access → Approver queue.** It opens straight to **every Pending request across all workflows you administer**, oldest-first — no workflow to choose first. Each row shows the workflow, requester, scopes, age.
2. **Optionally filter** by status or a single workflow; **`Load more`** to page.
3. **Act on a row:** **`Approve`** / **`Make eligible`** (with an optional eligibility window) / **`Deny`** — each captures an optional note; **`Revoke`** an approved grant.
4. **Empty inbox:** *"🎉 You're all caught up — no pending requests across the workflows you administer."*

---

## 7. Fresh-eyes findings

### 7.1 What's strong (keep / lean into)
- **Correct-by-construction security UX.** Resolved-identity pickers, person-grant steering, partial-identity flags, reference-not-secret, the `write ⇒ read` lock, the template-first scope editor with live preview. This is well above the bar for permission UIs and is the product's differentiator.
- **The approver inbox** (just shipped) — actionable-default (Pending), context-per-row (workflow + requester), filters-not-gates, inbox-zero. This is the right shape.
- **Recovery surgery** (the four resume modes, schema-validated skip outputs) is genuinely powerful and well-explained per mode.
- **Honest empty / loading / error states** with retry, and non-disclosing 403s throughout.

### 7.2 Friction & rough edges (prioritised)

**P1 — small fixes, clear correctness/clarity wins**
1. **Resume → Skip "record outputs" placeholder shows a JSON *Patch* example** (`[{ "op": "replace", … }]`) instead of a step-output example — it invites the wrong syntax into the skip-outputs editor. *(resume-dialog.js ~44.)*
2. **Faulted runs keep polling.** The terminal set is `{Completed, Cancelled}`, so a Faulted run is polled forever even though it can't self-progress. Add `Faulted` to the no-poll set. *(run-detail.js ~19.)*
3. **Scope typeahead silently discards a free-typed value on Enter** if it isn't already in the datalist — type-ahead-then-Enter feels broken. Either accept a server round-trip on commit, or show "no such scope".

**P2 — consistency & scale**
4. **Two paging idioms.** Runs and Catalog use `‹ Prev / Next ›`; Sources, Access, Scopes, Grants use `Load more`. Pick one model for the console (Load-more reads better for action lists; Prev/Next for dense tables — but be deliberate, not accidental).
5. **Catalog list groups client-side.** It fetches versions and groups by base id in the browser, so its search/filter runs over the fetched slice rather than server-side like the rest of the kit (which the paging campaign just made server-paged). At many workflows this is the odd one out; consider a server-side base-workflow search to match.
6. **Purge dialog forgets the last cutoff** (resets to "30 days ago" each open) — minor repeated tax for routine reaping.

**P3 — capability gaps / nice-to-haves**
7. **Credential usage grant is immutable after create** — changing who may use a source means delete + recreate. Likewise **grant claim *type*** is read-only in edit. Both force destroy-and-recreate for a small change.
8. **No delete affordance for a source credential in the table/dialog** (the API supports it) — the lifecycle is create/rotate only in the UI.
9. **Durations are hours-only** (request duration; eligibility window). Day/week shortcuts would cut arithmetic.
10. **Untyped "extra config" rows** on credentials aren't validated against what the runner expects — easy to mistype a key.

### 7.3 Terminology (a cross-cutting note)
The UI deliberately renames API concepts for users — **Scopes** = security *rules*, **Sources** = source *credentials* / connections, **Grants** = *bindings*. The renames are reasonable (more human), but a one-line glossary somewhere in-product (or a tooltip) would help operators who also read the API/CLI, where the original names surface. Worth a deliberate decision rather than drift.

### 7.4 Tenant is the ambient boundary, not a user-facing dimension (P1, conceptual)
Tenant isolation is **system-enforced and ambient**: the deployment's row-security policy stamps the caller's
`sys:tenant` automatically (§14.3/§14.4) and scopes every list/read/write to the caller's *current* tenant. It
follows that a user **never authors or sees a `tenant` constraint** — within their tenant they reason about
**domains, teams, roles, and classifications**, never the tenant itself. Surfacing `tenant` as a user choice leaks a
system concept into the user surface and is wrong for multi-tenant SaaS. Two concrete corrections:

- **Drop `tenant` from the authoring UIs.** The administrator grant input offers a `{ workflow | tenant }` dimension,
  and the scope library shipped tenant-isolation rules (`tenant == $claim.tenant`, `tenant in (…)`) as if they were
  optional reach vocabulary. Neither belongs in a single-tenant view — administration and reach should be expressed in
  team/role/domain/classification terms. *(The demo's example data has been corrected to reflect this — see below;
  removing the `tenant` option from the components themselves is the matching code change.)*
- **Multi-tenant membership is a tenant switcher, not a grant.** An identity that belongs to more than one tenant needs
  a dedicated **tenant switcher** in the console chrome that sets the active tenant for the whole session (re-scoping
  everything), distinct from anything in the grant/admin surfaces. This is new UI, not a tweak.

**Demo data (done):** the example seed is now separate from the test fixtures (`demo/demo-seed.js`, passed to
`createMockControlPlane`) and is tenant-free — within-tenant reach vocabulary, team/role grants, team administrators,
and run labels by domain rather than tenant — so the walkthroughs show the product as a tenant actually experiences it.

---

## 8. Video shot-list (ready to record)

The §3–§6 storyboards are the scripts. The highest-value short clips (≈20–40s each), with the caption track:

| # | Clip | Captions (per shot) |
|---|---|---|
| 1 | **Diagnose & retry a faulted run** | "Runs open with health at a glance" → "Filter to Faulted" → "Open the run — the fault, step, and error" → "Resume → Retry the faulted step" → "Recovered" |
| 2 | **Surgical recovery (skip / patch)** | "A bad step output" → "Resume → Skip, record corrected outputs (schema-checked)" → "…or State-patch the context and retry" |
| 3 | **Publish a workflow** | "Add workflow → choose the document" → "Its sources are detected" → "Seed administrators" → "Owner + tags → Add" → "Published; bind its credentials" |
| 4 | **Register a connection safely** | "New credential — references, never secrets" → "Pick the secret store; the reference is previewed" → "Choose whose runs may use it" → "Created" |
| 5 | **Author a scope, then grant it** | "New scope → pick a goal; the expression writes itself" → "New grant → pick a team (not a person)" → "Scoped read via that scope" |
| 6 | **The approver inbox** | "Approver queue opens on what you can act on" → "Pending across every workflow you administer" → "Approve with a note / make eligible / deny" → "Inbox zero" |
| 7 | **Request access (requester)** | "Request access — pick a workflow + scopes" → "write auto-includes read" → "Submitted; track or withdraw" |

Each clip is a Playwright run against the in-browser mock (no server), with a caption banner injected per step and
the page's own video recording on. (See the note in the PR / chat about producing these — it needs the browser
toolchain installed in the worktree.)
