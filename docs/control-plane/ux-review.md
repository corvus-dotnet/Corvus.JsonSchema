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
| **Connections** | The source credentials a run uses | `credentials-table`, `credential-dialog`, `grantee-picker` |
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
| **Platform / connections admin** | Connections | Register the credentials a workflow's runs need; control who may use them |
| **Security admin** | Access → Rules + Grants | Define the reusable reach vocabulary and grant reach to identities |
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
3. **(Build) Select the Arazzo workflow `.json`.** The dialog reads its `sourceDescriptions` and, per source, asks for the source document **and shows the credentials that source already has** (a binding serves every workflow that references it — reuse, default off) or warns when none and nudges to set one up (default on).
4. **Seed administrators.** The workflow's own identity is pre-filled (removable); add more via the `{workflow | tenant}` dimension picker + `+ Add administrator`.
5. **Fill owner** (name + email required) and optional tags.
6. **Click `Add workflow`.** The version lands, staged administrators apply, and any ticked credential setups open in sequence — each **locked to its source with the auth kind + config derived from the uploaded source document** (§7.5); the new version's detail opens.

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
1. **Open the Connections tab.** A table of credential bindings with a colour-coded validity status, the secret reference (read-only), the resolved usage grantee, and an expiry countdown.
2. **Filter** by status (`all / valid / expiring soon / expired`) and source-name substring.
3. **`Load more`** to page (keyset).

### 5.2 Register a credential ("connection")
1. **Click `New credential`** (needs `credentials:write`). The dialog states plainly: *references and non-secret metadata only — the secret is never entered here.*
2. **Identity** — source name, environment, auth kind (`apiKey`/`bearer`/`basic`/`oauth2ClientCredentials`), optional expiry + description.
3. **Secret references** — for each secret the auth kind needs, pick a store (Key Vault / AWS Secrets Manager / Vault / env / file / raw) and fill its guided fields; a **live preview** shows the composed `scheme://locator#version`. Inline secrets are rejected.
4. **Runner access (§13.5).** Each reference now shows a per-store **access note**: registering the *pointer* is not enough — the workflow runner reads the secret at run time as its **own** least-privilege identity, which the operator must grant **read** on that exact path (Key Vault: the *Key Vault Secrets User* role / a get-secret policy on the named vault; AWS: `secretsmanager:GetSecretValue`; Vault: a `read` policy on `mount/data/path`; env/file: present for the runner). Writing the secret is a **separate, write-capable** identity (CI/IaC); the control plane never touches the store. This closes the "stored reference resolves at run time" gap — without the grant, resolution fails with a 403.
5. **Config** — the auth kind's non-secret fields (e.g. token URL, client id) plus an escape hatch for extra entries.
6. **Usage** — pick the grantee whose runs may use this binding (grantee picker; partial identities are flagged).
7. **`Create`.** The binding appears in the table.

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

### 6.2 Author a grant binding (give an identity reach)
1. **Open Access → Grants** and **search** (server-side + `Load more`).
2. **`New grant` → WHO:** use the **grantee picker** to find a team/role. *(Pick a person and the form refuses it and points you at the access-request flow; a partial identity is flagged.)*
3. **WHERE:** for each of **read / write / purge** choose `Denied`, `Unrestricted`, or **scoped reach**; for scoped reach, add one or more rules via the **server-backed typeahead** (each becomes a removable chip).
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
- **Runner-access guidance on the credential dialog (added in this review).** The dialog previously stopped at "the control plane stores only the reference" — true, but it left the operator without the *consequence*: the secret only resolves at run time if the **runner's own identity** has been granted read on that path (§13.5). Each secret reference now carries a per-store access note (Key Vault role / AWS action / Vault policy / env-or-file), and states that *writing* the secret is a separate CI/IaC identity. This keeps the "reference, never secret" boundary while making the out-of-band grant the operator must perform explicit and correct-by-construction.
- **Honest empty / loading / error states** with retry, and non-disclosing 403s throughout.

### 7.2 Friction & rough edges (prioritised)

**P1 — small fixes, clear correctness/clarity wins**
1. **Resume → Skip "record outputs" placeholder shows a JSON *Patch* example** (`[{ "op": "replace", … }]`) instead of a step-output example — it invites the wrong syntax into the skip-outputs editor. *(resume-dialog.js ~44.)*
2. **Faulted runs keep polling.** The terminal set is `{Completed, Cancelled}`, so a Faulted run is polled forever even though it can't self-progress. Add `Faulted` to the no-poll set. *(run-detail.js ~19.)*
3. **Scope typeahead silently discards a free-typed value on Enter** if it isn't already in the datalist — type-ahead-then-Enter feels broken. Either accept a server round-trip on commit, or show "no such scope".

**P2 — consistency & scale**
4. **Two paging idioms.** Runs and Catalog use `‹ Prev / Next ›`; Connections, Access, Rules, Grants use `Load more`. Pick one model for the console (Load-more reads better for action lists; Prev/Next for dense tables — but be deliberate, not accidental).
5. **Catalog list groups client-side.** It fetches versions and groups by base id in the browser, so its search/filter runs over the fetched slice rather than server-side like the rest of the kit (which the paging campaign just made server-paged). At many workflows this is the odd one out; consider a server-side base-workflow search to match.
6. **Purge dialog forgets the last cutoff** (resets to "30 days ago" each open) — minor repeated tax for routine reaping.

**P3 — capability gaps / nice-to-haves**
7. **Credential usage grant is immutable after create** — changing who may use a source means delete + recreate. Likewise a **grant binding's claim *type*** is read-only in edit. Both force destroy-and-recreate for a small change.
8. **No delete affordance for a source credential in the table/dialog** (the API supports it) — the lifecycle is create/rotate only in the UI.
9. **Durations are hours-only** (request duration; eligibility window). Day/week shortcuts would cut arithmetic.
10. **Untyped "extra config" rows** on credentials aren't validated against what the runner expects — easy to mistype a key.

### 7.3 Terminology (a cross-cutting note)
The UI uses the canonical vocabulary (see [`UBIQUITOUSLANGUAGE.md`](./UBIQUITOUSLANGUAGE.md)) where it reads more human than the API/wire name — **Rules** (the API calls them security *rules*), **Connections** (the tab that manages source *credentials*), **grant bindings** (the API calls them *security bindings*). A one-line in-product glossary or tooltip still helps operators who also read the API/CLI, where the wire names surface.

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
- **Multi-tenant membership is a tenant switcher, not a grant binding.** An identity that belongs to more than one tenant needs
  a dedicated **tenant switcher** in the console chrome that sets the active tenant for the whole session (re-scoping
  everything), distinct from anything in the grant/admin surfaces. This is new UI, not a tweak.

**Demo data (done):** the example seed is now separate from the test fixtures (`demo/demo-seed.js`, passed to
`createMockControlPlane`) and is tenant-free — within-tenant reach vocabulary, team/role grants, team administrators,
and run labels by domain rather than tenant — so the walkthroughs show the product as a tenant actually experiences it.

### 7.5 Credential creation is rooted in the wrong place (P1, conceptual)
A standalone **Connections → New credential** that asks the operator to free-type a **source name** and **auth kind** is
back-to-front. The source name *must* match a `sourceDescriptions` entry in a workflow — un-guessable from the Sources
tab — and the auth kind plus where the secret goes (header/query name, token URL, scopes) are not guesses either: they
are declared in the source's **OpenAPI/AsyncAPI** document. A credential is, in effect, *a named secret bound to a
specific source* — so the act of binding belongs where the source (and its auth shape) is known.

**Status: built.** The dialog now derives `{ authKind, config }` from a source document's `components.securitySchemes`
(apiKey/httpApiKey, http bearer/basic, userPassword, oauth2 client-credentials) and locks the auth kind with a
"derived from …" note; the catalog detail's per-workflow Sources panel lists each source's existing bindings and a
"Set up credential…" that fetches the source doc and opens the derived dialog; the **add-workflow** dialog surfaces
each source's existing bindings (reuse, default off) or nudges when none (default on) and derives auth from the
uploaded source document; the Connections tab dropped free-typed create and gained **Duplicate to environment**. *(Demo
sources carry real `securitySchemes`; clip 3 is recorded from the catalog-detail flow.)*

**Corrected model (as built):**
- **Create a credential only where the source is known and its auth is derivable.** From the workflow add/edit flow and
  from a per-workflow **Sources** panel on the catalog detail: list the workflow's declared sources and, for each, open
  the dialog with **source name, auth kind, and config pre-derived from the source document's `securitySchemes`**
  (apiKey → parameter name + location; HTTP bearer/basic; oauth2 client-credentials → token URL + scopes). The operator
  then supplies only the **secret reference** (+ the §13.5 runner-access grant) and the usage grantee — the dialog stops
  asking for what the document already knows.
- **Reuse, don't recreate.** A credential is bound to a *source*, not a workflow, so the same binding serves every
  workflow that references that source. The per-source step therefore offers **select an existing binding for this
  source that you have access to** *or* set up a new one — you don't rebuild credentials per workflow.
- **The Connections tab is management, not creation.** Remove the free-typed **New credential** entirely; the tab is
  rotate (re-point the reference), revoke (delete — currently missing, finding #8), grant who-may-use, and watch
  validity/expiry across all bindings. The one create-shaped action that belongs here is **Duplicate to another
  environment** — clone an existing binding's source name + derived auth/config into a new `environment`, pointing at
  that environment's secret. It's legitimate precisely because it's rooted in an existing source (name + auth carried
  over), not free-typed.

Keeps every strength (reference-not-secret, resolved-identity usage, the runner-access note) while removing the
guessing. *(Demo: the mock's source stubs gain `components.securitySchemes` so the derivation is demonstrable;
clip 3 — currently shot against the standalone path — is re-recorded from the workflow-rooted flow.)*

### 7.6 Sources as first-class entities + the Add-workflow wizard (P1, proposed)
The Add-workflow dialog exposed a deeper modelling gap: **credentials are global per source** (`sourceName@environment`,
shared by every workflow that references the source) but **source documents are packaged per workflow-version**. So the
dialog can say a source "already has credentials" yet still demand its document — and a re-uploaded document could
disagree with the auth those credentials assume. The fix is to make a **source first-class**.

- **A source is registered once.** A source = `{ name, type, document (OpenAPI/AsyncAPI) }` plus its per-environment
  credentials. Workflows **reference** it by name; the catalog resolves a workflow's `sourceDescriptions` against the
  registry. A source already in the registry is shown **resolved and immutable** in Add-workflow (no re-upload); only a
  genuinely new source asks for a document. (Server/durability impact is real; the demo models a `/sources` registry in
  the mock to prove the UX.)
- **Reach-scoped discovery.** The sources and credentials the wizard surfaces are only those **in the creating user's
  reach** (the same reach model as `searchGrantees` / row security) — never the whole deployment's.
- **Authoring access vs runtime use are decoupled (the divergence question).** Three resources with distinct policies:
  the source *document* (referenceable?), *managing* a credential (`credentials:read/write` + reach), and a run *using*
  a credential (the run identity matches the binding's `usageGrantee`; the runner resolves the secret as its own
  identity, §13.5). The rule:
  - **Hard gate:** a workflow may reference only sources in the author's reach (so the wizard lists only those).
  - **Credential presence is a readiness check, not a hard gate.** Per source/environment the wizard shows whether a
    usable credential exists *where the author can see it*; missing → "needs a credential before runs succeed" (create
    now if you have `credentials:write` + reach, or defer to whoever owns it).
  - **Divergence is about visibility, never about whether existing runs work.** Source-in-reach + credentials you can't
    manage → "available; credentials managed elsewhere" (proceed). Credentials you can see + source not in reach → the
    source isn't offered for new workflows (the orphaned binding shows only under Sources management). Revoking an
    author's access later changes what they can *see/edit*, not whether existing runs resolve secrets (that follows the
    runner identity + `usageGrantee`, not the author).
- **Gather-then-commit wizard (not inline post-add).** The dialog becomes staged — **Workflow → Sources (resolve known,
  register new, credentials where needed) → Administrators (grantee-picker, workflow-self default, always skippable) →
  Review & commit**. Everything is gathered first; **one commit** at the end performs register-sources + add-version +
  set-credentials + apply-admins (ordered, with a clear per-step result; a server-side batch is the eventual home). The
  current "set up credentials after adding" checkbox + sequential post-add dialogs are removed. The admin step replaces
  the interim `{workflow|tenant}` `admin-grant-input` with the resolved-identity grantee-picker (no `tenant`).

> **Delivered.** The `/sources` registry ships full-stack (OpenAPI → 10 durability backends + conformance → handler →
> CLI → mock); the **stepped add-workflow wizard** (`<arazzo-catalog-add-dialog>`: Details → Sources & credentials →
> Administrators → Review) resolves registered sources with no re-upload, registers new ones, sets credentials
> **inline**, and names administrators with the resolved-grantee picker. Sources are reach-scoped; readiness is a
> wizard hard-gate (see §7.7). The server still reads source names from the uploaded package (`CatalogPackage.ReadSourceNames`)
> — "registered ⇒ no re-upload" is delivered by the wizard packing the registered document client-side at commit.

### 7.7 Environment is a first-class, system-wide axis (P1, cross-cutting)
The `environment` we added to a credential is not a per-binding detail — it is a **deployment axis** the whole system
must honour consistently. The governing rule: **a workflow is runnable in environment E only if it has a *full set* of
source credentials for E** — every source it references resolves a valid credential for E. A missing or expired
credential for even one source makes the workflow **not-ready in that environment**, independent of every other
environment.

It must follow through end to end:
- **Sources / credentials:** a source carries credentials *per environment*; "duplicate to another environment" exists
  precisely to complete a set. Readiness is computed **per environment** (`ready / missing <source>… / expired …`).
- **Catalog (workflow detail + Add-workflow review):** show per-environment readiness for the workflow's declared
  sources — "production: ready (3/3) · staging: missing `events`". The wizard's Review step is the natural home; the
  workflow detail shows it standing.
- **Runs:** a run targets an environment and uses *that* environment's credential set; the runs UI should surface the
  run's environment (and a pre-run readiness check belongs here).
- **Runners (not yet designed):** runners execute in/for an environment and must have the environment's credential set
  reachable — the Runners UX, when built, is environment-scoped and consumes the same readiness model.

**Every environment is a governed resource — creating one grants you its administration.** There is no "open"
self-service tier. An environment has a **governance owner / administrator set and an audit trail, exactly like a
workflow (§15)**. The only thing that makes `DEV-MWA-1` feel self-service is that **the creator is explicitly granted
administration of the environment they create** (and wires their own credentials into it). `Production` differs only in
*who administers it* — a user may have **no access to `Production`** yet full administration of `DEV-MWA-1` they own.
Same governance shape, different membership.

This threads everywhere:
- **Environments carry administrators + audit** (created/updated by-and-when), managed with the **same resolved-identity
  grantee-picker** as workflow administrators — never a `tenant` tuple. The same access-request shape (§16.5) applies.
- **Every environment-scoped action is gated by environment access**: setting up or using a credential, *viewing* the
  sources/credentials in it, reading readiness, targeting a run. No access ⇒ you don't see it and can't act in it.
- **The environment picker is reach-scoped, with "create" (which makes you its administrator)** — never a free-typed
  governed name. *(The credential dialog's current free-text `environment` field becomes this picker.)*
- **Sources/credentials are set up per environment, explicitly** (UI + CLI over the back-end API), visible to anyone
  with reach into that environment. *This revises §7.5's "no standalone create": explicit per-environment source setup
  is legitimate because it is rooted in a **registered source** × a **governed environment you can act in** — not a
  free-typed tuple. The Connections surface becomes "set up / manage a source's credential in an environment you administer".*
- **Readiness is computed only for environments you can act in** — a full source-credential set per such environment.

Implication for the model: an **environment is a first-class, reach-scoped, governed resource** (administrators + audit,
created-grants-admin) — not a free-text field. Readiness is a reach-scoped query over (workflow sources × *reachable*
environment credentials), reused by the catalog detail, the Add-workflow review step, promotion (§7.8), and (later)
runs and runners. The demo models an environments registry (reach-scoped list + administrators + audit + create).

> **Delivered.** Environments are first-class governed resources full-stack (`/environments` CRUD + per-environment
> administrator set with a reverse digest index, across 10 durability backends + conformance → handler → CLI →
> mock). Creating one **grants the creator administration**. The UI is `<arazzo-environments>` (list / create /
> administer + the versions available in each), administered by the subject-agnostic `<arazzo-administrators-panel
> environment=…>`. **Readiness is a wizard hard-gate**: the add-workflow wizard refuses a build-from-docs workflow
> unless every source has a *usable* credential (§13 `IsUsableBy`, not mere presence) in some environment; the
> upload path defers to the CLI `catalog add`, which runs the same gate. A source's per-environment **server base URL**
> override (§8) rides on the credential binding's `config.baseUrl` and is applied at the runtime transport seam.

### 7.8 Making a workflow version available in an environment ("promotion") (P1, new surface)
"Promotion" is shorthand for **"make this version of this workflow available in this environment."** It is **additive
and many-to-many**, NOT a supersede:
- A version can be available in **many environments**; an environment can have **many versions of the same workflow
  available at once** (a long-running workflow keeps executing on V1 in Production long after V2 arrives; a staged
  rollout phases callers V1 → V2). Making V2 available does **not** retire V1, and does **not** remove the version from
  any other environment.
- The state is simply an **availability matrix `(workflowVersion × environment)`** — available or not — governed by the
  **target environment's administrators**:
  - Administer the environment → make a version available directly.
  - Otherwise → raise a **request** to that environment's administrators via a **new approver inbox**, mirroring the
    §16.5 access-request/approval pattern (actionable-default Pending, context-per-row, decision + audit note), with
    *environment* administrators as approvers and *(workflow version → environment)* as the subject.
- A version can be made available in an environment only where its sources have a **full credential set** (§7.7) —
  readiness gates availability.
- A workflow version's detail shows the environments it is available in; an environment shows the (workflow, version)
  pairs available in it; a run in environment E may target any version available in E.

**Bootstrapping (environment provisioning).** A deploy-time concern, not a special UI path: an operator provisions the
standing environments via the **CLI with secrets generated in CI** (the same secure-introduction pattern as §13.5's
secret-zero and §15's first-admin) — granting the initial environment administrators as code. The UI *can* do it all by
hand, but that is the less-secure fallback; the secure path is CLI + CI.

Model: an availability matrix `(workflowVersion × environment) → available` + an availability-request resource, the
environment's administrator set as approval authority — the access-request inbox shape, parameterised by environment.

> **Delivered.** Both slices ship full-stack across 10 durability backends + conformance → handler → CLI → mock + UI.
> Slice 1 = the availability **matrix** (admins `makeVersionAvailable`/`withdraw` directly, two-layer env-reach→env-admin
> gate + readiness `409`); slice 2 = the **availability-request approver inbox** (`/availabilityRequests` submit / list
> [mine|environment-queue|inbox] / approve / deny / withdraw, the §16.5 machinery parameterised by environment, using the
> env-admin reverse index). UI: the catalog version detail shows "Available in" + a **Request promotion…** action
> (offering only ready, not-yet-available environments), and `<arazzo-availability-requests>` is the Promotions inbox.
> Bootstrapping via CLI + CI is supported (`environments`/`availability` command trees).

---

## 8. Video shot-list (ready to record)

The §3–§6 storyboards are the scripts. The highest-value short clips (≈20–40s each), with the caption track:

Recording rhythm (applies to all clips): each caption explains *what is about to happen and why*, leads the UI
change by a couple hundred ms, then holds so the caption and its result are read together — never a long pre-action
wait. Form fields are scrolled to the centre of the dialog *before* being described, and named one by one (what each
is, where its value comes from).

| # | Clip | Status | Captions (per shot) |
|---|---|---|---|
| 1 | **Approve an access request** | ✅ recorded (`ux-clips/1-approve-access-request.webm`) | "Approvals live under Access — your inbox spans every workflow you administer" → "Each row: who asked, the workflow, the scopes" → "Approve with a note for the audit trail" → "Inbox-zero" |
| 2 | **Recover faulted runs — one realistic case per mode** | ✅ recorded (`ux-clips/2-recover-faulted-run.webm`) | Four *different* faulted runs, each restarted in turn: **Retry** (502 transient blip) → **Rewind** (region quota — region fixed upstream, rewind to `createAccount`) → **Skip + recorded outputs** (KYC unreadable, verified by hand — record schema-typed outputs) → **State-patch** (missing `adopter.email` — patch the context, retry) |
| 3 | **Register a connection safely** | ✅ recorded (`ux-clips/3-register-connection.webm`) | "References, never secrets" → field-by-field identity → walk the Key Vault reference (vault / secret / version → composed `keyvault://…`) → **"grant the runner's own identity read on the secret (§13.5); writing it is a separate CI/IaC identity; the control plane never reads the store"** → config → whose runs may use it → "Created" |
| 4 | **Publish a workflow** | ☐ to record | "Add workflow → choose the document" → "Its sources are detected" → "Seed administrators" → "Owner + tags → Add" → "Published; bind its credentials" |
| 5 | **Author a rule, then grant it** | ☐ to record | "New rule → pick a goal; the expression writes itself" → "New grant → pick a team (not a person)" → "scoped-reach read via that rule" |
| 6 | **Request access (requester)** | ☐ to record | "Request access — pick a workflow + scopes" → "write auto-includes read" → "Submitted; track or withdraw" |

Each clip is a Playwright run against the in-browser mock (no server), with a caption banner injected per step and
the page's own video recording on (`web/arazzo-control-plane-ui/test/record.spec.js` + `record.config.mjs`; output
under `web/ux-clips/`). It needs the browser toolchain installed in the worktree.
