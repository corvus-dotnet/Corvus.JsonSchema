# Antagonistic UI review. Use cases and evaluation

Date: 2026-07-16. Scope: the sample shell (nine tabs at `/`) and the designer, reviewed against the
running composition (real Keycloak sign-in, real stores, real runner) plus the seeded designer demo.
Method: enumerate the jobs each persona brings to each surface, then walk the rendered UI and judge
three things per job. Is the information clear and uncluttered. Are the actions discoverable and close
to the data they act on. Does the flow follow logically from the job.

This complements `ux-review.md` (which predates the shell, designer, environments, promotion,
runners and approvals surfaces). Findings carry IDs and severity (BLOCKER, MAJOR, MINOR, POLISH) and
cite the use case they fail. Strengths are recorded too. An antagonistic review that only lists
faults misreports the product.

---

## 1. Use-case inventory

Format: *as a / when I am / I want to / so I can*.

### Runs (U-RUN)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-RUN-1 | operator | on call | see at a glance which runs are unhealthy | triage before anything escalates |
| U-RUN-2 | operator | triaging a faulted run | see which step failed, with the fault detail | decide whether to resume, cancel, or escalate |
| U-RUN-3 | operator | diagnosing | see what a run actually did, step by step | explain an outcome without guessing |
| U-RUN-4 | operator | recovering | resume a faulted run with the right mode | restore service without re-running everything |
| U-RUN-5 | operator | investigating a stuck run | see what it is waiting on (timer or message, correlation) | unblock it or explain the delay |
| U-RUN-6 | operator | cancelling | mark a run cancelled with a reason | stop pointless work and leave an audit trail |
| U-RUN-7 | operator | housekeeping | purge old terminal runs by rule | keep the store lean without hand-picking rows |
| U-RUN-8 | operator | correlating with telemetry | copy a run's correlation id | pivot to traces and logs |
| U-RUN-9 | anyone | filtering | narrow by status, workflow, tags, time | find the runs I mean quickly |

### Catalog (U-CAT)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-CAT-1 | owner | curating | see every workflow, its latest version and status | know what the estate contains |
| U-CAT-2 | owner | inspecting a version | see its identity, description, owner and audit trail | trust what I am about to promote |
| U-CAT-3 | owner | comparing | diff two versions of a workflow | understand what changed before rollout |
| U-CAT-4 | owner | promoting | see where a version is available and where it is ready | move it toward production deliberately |
| U-CAT-5 | owner | governing | see and edit the version's administrator set | hand off or share control safely |
| U-CAT-6 | owner | scoping visibility | manage the version's management tags | control who may see and manage it |
| U-CAT-7 | owner | wiring | see the version's sources and their credential readiness per environment | know why promotion is or is not offered |
| U-CAT-8 | publisher | adding | register a new workflow version | make it runnable and governable |
| U-CAT-9 | owner | retiring | obsolete and purge dead versions | keep the catalog trustworthy |

### Environments and promotion (U-ENV)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-ENV-1 | environment admin | operating | see the environments I administer | know my remit at a glance |
| U-ENV-2 | environment admin | governing | edit metadata, evidence policy, draft-run posture | keep each environment's rules explicit |
| U-ENV-3 | environment admin | governing | manage the environment's administrator set | share operation without oversharing |
| U-ENV-4 | environment admin | auditing | see which workflow versions are available here | know what can run in my environment |
| U-ENV-5 | workflow owner | delivering | request promotion of a version into an environment I cannot write | move toward production through governance |
| U-ENV-6 | environment admin | deciding | approve or deny promotion requests for my environments | control what lands where, with reasons recorded |

### Sources and credentials (U-SRC)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-SRC-1 | connections admin | surveying | list the registered API surfaces | know what workflows may bind to |
| U-SRC-2 | connections admin | wiring an environment | create a credential binding for a source in an environment | make workflows runnable there |
| U-SRC-3 | connections admin | auditing | see every binding, its status, expiry and usage scope | catch expiring or over-shared credentials early |
| U-SRC-4 | connections admin | rotating | edit or duplicate a binding without touching secret material | rotate safely, reference-only |
| U-SRC-5 | connections admin | revoking | revoke a binding | cut access fast when something leaks |
| U-SRC-6 | security admin | scoping | restrict who may use a credential | keep production credentials off shared paths |

### Runners (U-RNR)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-RNR-1 | SRE | operating | see each runner's health, environment and load posture | know execution capacity is alive |
| U-RNR-2 | SRE | rolling out | see which workflow versions a runner has loaded | confirm a deploy actually landed |
| U-RNR-3 | environment admin | gating | authorize or revoke a runner for my environment | control who executes in my environment |
| U-RNR-4 | SRE | diagnosing | get from a stale runner to a next action | recover capacity, not just observe its absence |

### Security (U-SEC)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-SEC-1 | security admin | authoring vocabulary | create and edit named reach rules | reuse one WHERE vocabulary across grants |
| U-SEC-2 | security admin | granting | key a claim to per-verb reach through the picker | grant without hand-typing identities |
| U-SEC-3 | security admin | narrowing | attach several rules to one grant knowingly | intersect constraints without surprises |
| U-SEC-4 | security admin | widening | add a second grant for the same claim | give either/or reach the safe way |
| U-SEC-5 | security admin | auditing one principal | resolve everything a grantee can do and why | answer "what can X touch" in one place |
| U-SEC-6 | security admin | revoking | remove a grant from wherever I see it | cut access at the moment of discovery |
| U-SEC-7 | security admin | maintaining | know which rules are load-bearing before editing or deleting | avoid silently stranding grants |
| U-SEC-8 | auditor | reviewing | read the grant list and understand each row unaided | review policy without the API docs |
| U-SEC-9 | anyone | protecting the floor | be unable to casually break the baseline and founder bindings | avoid locking the deployment out |

### Approvals and requests (U-APR)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-APR-1 | approver | anywhere in the shell | see whether I have work waiting | clear my queue without polling |
| U-APR-2 | approver | deciding access | approve, make eligible, or deny with a recorded reason | elevate people deliberately |
| U-APR-3 | approver | deciding promotion | approve or deny availability requests for my environments | gate delivery with an audit trail |
| U-APR-4 | approver | post-decision | revisit past decisions and revoke an approval | undo a grant that outlived its purpose |
| U-APR-5 | requester | needing access | request scoped, time-boxed access to a workflow I can see | do my job without a standing grant |
| U-APR-6 | requester | waiting | track my requests and their outcomes | know where I stand without asking |

### Designer (U-DSN)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-DSN-1 | author | opening the designer | pick a working copy or start one | get to authoring in one step |
| U-DSN-2 | author | reading a flow | understand the graph, edge kinds and step states at a glance | reason about control flow visually |
| U-DSN-3 | author | editing a step | change bindings, parameters, criteria, outputs next to the step | author without leaving the canvas |
| U-DSN-4 | author | wiring surfaces | attach sources and drag operations onto the canvas | bind steps to real APIs |
| U-DSN-5 | author | validating | see problems with positions and jump to them | fix issues before publish refuses them |
| U-DSN-6 | author | verifying | run against mocks, step, set breakpoints, inspect exchanges | debug the flow like code |
| U-DSN-7 | author | verifying durably | run a draft in a development environment | prove it against real infrastructure |
| U-DSN-8 | author | regression-proofing | save a trace as a scenario and run the suite | keep proven behavior pinned |
| U-DSN-9 | author | collaborating | bind to a branch, commit, pull, compare, roll back | keep the document in version control |
| U-DSN-10 | author | shipping | publish to the catalog with evidence attached | hand the version to governance |

### Shell (U-SH)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-SH-1 | anyone | arriving | sign in with my real identity and see who I am | trust the session I am acting in |
| U-SH-2 | anyone | navigating | find each job's surface from the tab names alone | not memorize a site map |
| U-SH-3 | anyone | reading any list | see real counts, paging and explicit empty states | trust what the list claims |
| U-SH-4 | anyone | acting | find the action on or next to the data it affects | act without hunting |

---

## 2. Evaluation by area

### 2.1 Runs

**Works well.** Status chips filter in one click and read cleanly. Faulted rows carry the fault
message in the list itself (U-RUN-1, U-RUN-2 at list level). The wait row on a suspended run names
the channel and correlation id (U-RUN-5). Correlation id has copy-on-row (U-RUN-8). Purge is
preset-driven rather than free-form (U-RUN-7).

**Findings.**

- **RUN-1 (MAJOR, U-RUN-3).** A run's detail has no step trace. It shows metadata plus `Cursor 4
  (next step index)` and, for a faulted run, the fault message. There is no list of executed steps,
  no timings, no per-step outcome anywhere in the control plane. The operator's core diagnostic
  question, "what did this run actually do", dead-ends; the debug tray exists only in the designer
  and only for design-time sessions. The strongest single gap in the product.
- **RUN-2 (MINOR, U-RUN-2).** The detail leaks internals: an `ETag 5` row, and `Cursor 4 (next step
  index)` where an operator needs "completed 4 of 6 steps" or the next step's name. The detail's
  `Updated` row rendered blank for a completed run while the list said `1h ago`, which reads as a
  display bug.
- **RUN-3 (MINOR, U-RUN-9).** The two date-range fieldsets (four datetime inputs plus Clear dates)
  are permanently expanded across a full row of prime space. Status, workflow and tags are the
  everyday filters; dates deserve a collapsed disclosure.
- **RUN-4 (POLISH, U-RUN-7).** The red Purge button sits in the header beside refresh at all times,
  including when the filtered list is empty. Danger placement next to a routine action invites
  misclicks.
- **RUN-5 (POLISH, U-RUN-1).** `Age` and `Updated` columns duplicate each other for most rows (both
  "1h ago"), and opening the detail squeezes the table until workflow names wrap three lines. One
  of the two columns should yield under a narrow layout.

### 2.2 Catalog

**Works well.** The list is calm and scannable, title plus id plus latest plus owner (U-CAT-1). The
detail aggregates governance in one pane: administrators, promotion matrix, sources with per
environment credential readiness (U-CAT-4, U-CAT-5, U-CAT-7). The promotion matrix's three cell
states (Available, Ready with action, not ready with the reason on hover) are the right
correct-by-construction shape.

**Findings.**

- **CAT-1 (MAJOR, U-CAT-3).** The detail header crams badge, title, id, a version select and a
  "Compare with version" select into one row; the compare select clips at the pane edge at 1440px.
  The compare affordance, the entry point for U-CAT-3, is the thing that gets truncated.
- **CAT-2 (MINOR, U-CAT-2).** Section order buries identity. Management tags is the first card in
  the detail, above owner, audit and availability. Re-tagging is an occasional governance act, not
  the top job, and each card carrying its own Save multiplies commit points.
- **CAT-3 (MINOR, U-CAT-2).** The full 64-character content hash renders wrapped over two lines.
  Truncate with copy, like run ids.
- **CAT-4 (POLISH, U-CAT-9).** "Purge obsolete…" renders enabled and red when the list contains
  zero Obsolete versions. A destructive control with no applicable target should disable itself.

### 2.3 Environments and promotion

**Works well.** Reach-scoped listing is real and demonstrable (admin sees three, erin exactly her
zone). The detail carries the §4.6 evidence policy and §18 draft posture as explicit, explained
switches (U-ENV-2). Availability lists what is actually deployed (U-ENV-4). The promotion request
and decision loop closes end to end with reasons recorded (U-ENV-5, U-ENV-6).

**Findings.**

- **ENV-1 (MINOR, U-ENV-2).** The two governance checkboxes interleave with their hint paragraphs
  into one undifferentiated block; the second checkbox sits hard against the first one's hint text.
  Each toggle needs its own visual group.
- **ENV-2 (MINOR, U-ENV-3).** Administrator rows render raw identity tuples,
  `group=env-admins · iss=arazzo-keycloak`, where every picker in the product shows resolved labels.
  The issuer pin repeats on every row as pure noise. Same defect as SEC-6.
- **ENV-3 (POLISH, U-ENV-1).** The header line "Created 1h ago by demo · updated 16m ago by
  control-plane" surfaces seed and service actor strings as if they were people. Same defect as
  SH-2.

### 2.4 Sources and credentials

**Works well.** The credentials detail states the reference-only invariant in-line ("stores
references only, never secret material") exactly where a security reviewer looks (U-SRC-4). Edit,
Duplicate and Revoke sit on the detail, next to the data (U-SRC-5). Usage scope is worded in intent
terms ("Shared, any run that uses this source") (U-SRC-6).

**Findings.**

- **SRC-1 (MAJOR, U-SRC-2).** Neither the Sources page nor the Credentials page can create a
  credential binding. Creation is rooted in Catalog, inside a version's sources section, and
  nothing on either page says so or links there. The two pages named after the job silently lack
  the job; a connections admin's most likely first journey ends in a hunt. Duplicate existing is
  offered where create is not, which makes the absence read like an accident rather than a design.
- **CRED-1 (MINOR, U-SRC-3).** The list repeats the vault reference under every row (three
  identical strings per source), wraps raw grant tuples three lines deep in the Grants column, and
  fills Status and Expires with identical `valid` badges and dashes. High ink, low signal.
  No environment filter exists although environment is the natural grouping axis.

### 2.5 Runners

**Works well.** The card states health from a live heartbeat with a stale threshold, names the
served environment, and lists loaded versions (U-RNR-1, U-RNR-2). Real data confirmed against the
composition's actual runner.

**Findings.**

- **RNR-1 (MINOR, U-RNR-3, U-RNR-4).** The runner card shows no authorization state and offers no
  action. The §5.5 roster lives under Approvals → Runners, and neither surface links the other. An
  SRE staring at a stale or unauthorized runner is given observability and no verbs; an
  environment admin cannot get from the runner to its authorization row without knowing the site
  map.

### 2.6 Security

**Works well.** The grant editor's WHO and WHERE framing, with the capability versus reach
distinction stated in-line, is the best explanatory copy in the product (U-SEC-2). Person grantees
are steered to the request flow; identities are picked, never typed. The new conjunction rendering
(`+` separators, "All rules must match", the provably-empty-pair advisory) makes narrowing versus
widening teachable at the point of authoring (U-SEC-3, U-SEC-4). The rules list pairs each name
with its expression in code font (U-SEC-1, U-SEC-8).

**Findings.**

- **SEC-1 (MAJOR, live defect, U-SEC-6).** The Access overview hides its inline Revoke in the
  sample shell. `access-overview-panel.hasScope` treats an absent `scopes` attribute as holding
  nothing, while the grants, rules and environments panels treat absent as everything; the live
  shell sets no scopes attributes by design, so the admin sees no Revoke buttons (screenshot
  confirms, code confirms). The mock demo page sets explicit scopes, so every mock test passes.
  The gating conventions must be unified, and the live suite should pin the overview Revoke.
- **SEC-2 (MAJOR, U-SEC-9).** The baseline `*` binding and the founder `arazzo-admins` binding are
  editable and deletable exactly like any other grant, behind the same generic confirm. Deleting or
  degrading either is a step toward locking out every principal or every administrator, and the UI
  gives them no system treatment, no distinct badge, no strengthened confirm, no soft-guard.
- **SEC-3 (MAJOR, U-SEC-7).** Rules carry no usage indication. Nothing says
  "referenced by 4 grants" and deleting a load-bearing rule strands those grants pointing at a
  missing name, silently, fail-closed. The consequence of the most destructive action on the panel
  is invisible before and after.
- **SEC-4 (MINOR, U-SEC-5).** An eligible-only binding renders in the overview's reach section as a
  card of `read Denied / write Denied / purge Denied`, indistinguishable from an explicit deny
  record. It is a PIM eligibility that confers nothing yet; the card misstates its nature, while
  the capabilities section below renders it correctly as a dashed eligible chip.
- **SEC-5 (MINOR, U-SEC-8).** The founder row's "confers …" line is eighteen scope names in one
  italic run-on. Summarize ("confers all 18 capability scopes") or chip them.
- **SEC-6 (MINOR, U-SEC-8).** Raw tuples with the repeated issuer pin
  (`+ iss=arazzo-keycloak` on six of eight rows) dominate the claim column. The issuer belongs in
  the detail, not on every list row.

### 2.7 Approvals and requests

**Works well.** This is the strongest area. Badges on the tab and each sub-tab surface outstanding
work from anywhere (U-APR-1). Rows carry requester, resolved label, subject claim, reason and the
three decision actions directly on the row (U-APR-2, U-APR-3); decisions capture reasons; the
follow-up states (revoke an approval, track outcomes) are one filter away (U-APR-4, U-APR-6). The
empty states speak ("You're all caught up", "Use Request access… to ask"). The request dialog is
correct by construction end to end: picked workflows, forced scope coherence, readiness-constrained
environment choices (U-APR-5, U-ENV-5).

**Findings.**

- **APR-1 (MINOR, U-APR-2).** The availability queue shows the requester as the raw username
  (`alice`) where the access queue shows a resolved label ("Oscar (Observer)"). Same list, two
  identity presentations.
- **APR-2 (POLISH, U-APR-2).** Under the default Pending filter the Decision column is always
  a dash by definition. Drop the column in Pending view and reclaim the width for Reason.

### 2.8 Designer

**Works well.** The canvas communicates a lot correctly: edge kinds, fault glyph prefixes for
color-blind users, breakpoint dots, the floating workflow-defaults chip (U-DSN-2). The step
inspector binds editing to the selection with strong explanatory copy (DEPENDS ON) (U-DSN-3). The
debug dock is genuinely excellent: per-step trace, exchanges as sent, a success-criteria truth
table, outputs, waiting-on with an inject affordance, an expression console evaluated at the scrub
cursor (U-DSN-6). Scenarios keep run, edit and delete on the row (U-DSN-8). Git starts from a clean
single-affordance empty state (U-DSN-9).

**Findings.**

- **DSN-1 (MAJOR, U-DSN-2).** The node micro-notation has no key. `✓ 1 → 1 ⌁ defaults`,
  `↻ ×3/5s`, the OP and CH badges, the port dots: none of it is explained anywhere in the UI, and
  the reading only exists in the design doc. A first-session author faces glyph soup on every node
  of the canvas. A hover legend or a one-line key under the canvas would resolve it.
- **DSN-2 (MINOR, U-DSN-6).** The toolbar's run controls are icon-only (simulate, step,
  stop), and Publish is the sole labeled action. The difference between the two play-style icons
  is load-bearing (simulate versus step-into-new-session) and invisible without hovering.
- **DSN-3 (MINOR, U-DSN-3).** Stale copy: the binding field hint reads "the operation browser will
  fill this in in the full designer" while you are in the full designer. Leftover from the
  embedded-inspector era.
- **DSN-4 (POLISH, U-DSN-6).** During a live debug session the right pane happily continues
  offering document editing with no indication of whether edits affect the running session. A
  session-active hint on the inspector would remove the ambiguity.

### 2.9 Shell

**Works well.** Real sign-in with the identity displayed; tab names map to jobs; approval badges
carry real bounded counts; every list has explicit counts, paging and written empty states
(U-SH-1, U-SH-3).

**Findings.**

- **SH-1 (MAJOR, systemic, U-SH-2).** Design-document section references leak into product copy on
  at least five surfaces: "(§14.2)" on tag editors, "(§18)" on the draft-runs toggle,
  "workflow-designer §4.6" on evidence, "§16.2 tier 3" in a grant description, "§3.3" in the
  expression console hint. Operators do not have the design doc, and each reference is a sentence
  that stops explaining just when it should explain. Every one should become a plain-language
  clause.
- **SH-2 (MINOR, systemic, U-SH-1).** Seed and service actor strings render as people: "Created by
  demo", "updated by control-plane", requester "alice" unresolved. Audit lines need the same
  resolved-label treatment as pickers.
- **SH-3 (POLISH, U-SH-1).** "Signed in as **arazzo-admin**Sign out" renders with no separator
  between the name and the action link.
- **SH-4 (POLISH).** Timestamp formats mix styles in one view: relative everywhere, then
  second-precision locale strings ("7/17/2026, 6:46:58 AM") inside capability chips. Chips need at
  most minute precision.

---

## 3. Systemic observations

1. **The product's explanatory voice is its best feature and its leaks are the flip side.** The
   WHO/WHERE editor, the DEPENDS ON note, the reference-only credential note: this voice should be
   the template. But the same instinct pastes spec section numbers (SH-1) and internal identifiers
   (ETag, cursor, raw tuples, actor ids) into copy meant for operators. One editorial pass with the
   rule "no token an operator cannot act on" would lift every surface at once.
2. **Actions live close to data everywhere except across surface boundaries.** Within a panel the
   product is disciplined (decisions on rows, revoke on detail). Between panels it relies on the
   user knowing the site map: source → its credentials (SRC-1), runner → its authorization (RNR-1),
   run → its diagnosis (RUN-1). The missing affordance is always a link from the thing to its
   counterpart surface.
3. **Identity rendering is split-brained.** Pickers and approvals resolve identities to labels;
   lists and audit lines print raw tuples and actor strings (SEC-6, ENV-2, APR-1, SH-2). One
   grantee-chip component used everywhere would close it.
4. **The two demo/live gating conventions diverged** (SEC-1). Absent `scopes` means everything in
   some panels and nothing in others; the live shell deliberately sets none. Pick one convention,
   document it in the kit README, and add a live-suite pin for each privileged affordance.
5. **Guard-rails end one step before the cliff.** Confirm dialogs exist everywhere, but the
   system-critical objects (baseline and founder bindings, load-bearing rules) get the same
   treatment as a throwaway test grant (SEC-2, SEC-3). Severity-proportional friction is missing.

## 4. Ranked worklist

| Rank | Finding | Severity | One-line fix direction |
|---|---|---|---|
| 1 | RUN-1 no step trace on a run | MAJOR | run detail gains an executed-steps section (server exposes the journal it already stores) |
| 2 | SEC-1 overview Revoke invisible live | MAJOR | unify absent-scopes convention across panels; pin with a live test |
| 3 | SEC-2 baseline/founder bindings unguarded | MAJOR | system badge + strengthened confirm on genesis bindings |
| 4 | SEC-3 rule usage invisible | MAJOR | "in use by N grants" on rules; block or warn on deleting in-use rules |
| 5 | SRC-1 credential creation undiscoverable | MAJOR | create-binding entry on Sources and Credentials, or explicit links to the catalog flow |
| 6 | SH-1 spec references in product copy | MAJOR | editorial sweep replacing §refs with plain clauses |
| 7 | DSN-1 canvas notation has no key | MAJOR | hover legend / canvas key |
| 8 | CAT-1 compare select clipped | MAJOR | detail header reflow (compare moves below the title row) |
| 9 | SEC-4 eligibility renders as denial | MINOR | render eligible-only bindings as eligibility cards |
| 10 | RNR-1 runner ↔ authorization unlinked | MINOR | cross-links both ways; authorization state on the runner card |
| 11 | SEC-6/ENV-2/APR-1/SH-2 identity split-brain | MINOR | one grantee-chip renderer everywhere |
| 12 | RUN-2/RUN-3/CRED-1/CAT-2/CAT-3/ENV-1 | MINOR | clutter pass per area as listed |
| 13 | RUN-4/CAT-4/APR-2/SH-3/SH-4/DSN-2/DSN-3/DSN-4 | POLISH | batched copy and layout tweaks |

Verification notes. SEC-1 was verified in source (`access-overview-panel.js hasScope` versus
`grants-panel.js canWrite`) and in the live screenshot. RUN-2's blank Updated row is from the live
screenshot and needs a reproduction before fixing. All screenshots were taken at 1440x900, light
theme, from the running composition (control plane tabs) and the seeded designer demo (designer),
2026-07-16.
