# Antagonistic OpenTelemetry review. Operator actions and observability coverage

Date: 2026-07-17. Scope: every action a person can take against the control plane, judged against
the telemetry it emits — a distributed-tracing span, a metric where a rate or latency matters, and an
audit-grade structured log where governance changed. This mirrors the method of
`ui-antagonistic-review.md`: enumerate the actions across every surface, then judge each against a
fixed bar, record strengths as well as gaps, and rank the gaps into a worklist.

The bar, per action:

- **Span** — an activity on the `Corvus.Arazzo` `ActivitySource` naming the action, carrying the
  actor, the target (run / version / environment / grant / request / runner), and an outcome tag. The
  span is zero-cost when no listener is attached, so presence costs nothing at rest.
- **Metric** — a counter where a rate matters (decisions per minute, rotations, revocations) or a
  histogram where a latency matters. Not every action needs one; a rare, individually-audited
  governance change is served by its span and log, not a counter.
- **Audit-grade log** — a structured `LogInformation` record naming *who* did *what* to *which*
  target, with the outcome, for every action that changes the security posture or the governed estate.
  This is the record a security audit reads; a span alone is sampled and short-lived, an audit log is
  retained.

Payload hygiene is a first-class part of the sweep: step outputs and secret references must never
reach any of the three channels.

Findings carry a severity — **BLOCKER**, **MAJOR**, **MINOR**, **POLISH** — and cite the action they
concern. IDs are `O-<surface>-<n>`.

---

## 1. What exists today

Only two domain services emit telemetry, and one read path:

| Emitter | Actions | Span | Metric | Audit log |
|---|---|---|---|---|
| `SecuredWorkflowManagement` (runs) | resume, resume-request, cancel, purge, delete | ✅ rich | ✅ counters | ❌ |
| `SecuredWorkflowCatalog` (catalog) | add, update, delete, purge, administration ops | ✅ good | ❌ | ❌ |
| `SensitiveReadAudit` (#860) | read step journal | ✅ | ❌ | ✅ |
| Runtime executor (generated) | workflow/step execution | ✅ | ✅ counters + histograms | n/a |

The runs spans are the gold standard the rest of the surface should be held to: `workflow.resume`
tags the run id, the actor, the resume mode, the resolved workflow id, the run's correlation id, and a
precise outcome (`resumed`, `not-faulted`, `leased-by-other`). The catalog spans tag the actor, the
base workflow id, the version number, and an outcome (`versioned-id-rejected`,
`workflow-not-administered`, and the success path).

Every other control-plane surface emits **nothing**: no span, no metric, no audit log. A grep of the
server project confirms only three files touch `System.Diagnostics.Activity` or `ILogger` —
`ArazzoControlPlaneHandler` (which delegates runs telemetry to the domain service and hosts the #860
journal audit), `ControlPlaneEndpointExtensions` (the open-mode warning and the audit-logger wiring),
and `SensitiveReadAudit`. The 100-plus handler methods on the other surfaces are dark.

## 2. Payload hygiene (strength)

**Clean.** Every `SetTag` argument across the codebase is an identifier, a count, an enum, a status,
or a cursor — run id, workflow id, actor, resume mode, outcome, base workflow id, version number,
purged count, older-than, disclosure tier. None carries a step output or secret material. The one
free-text tag is `corvus.arazzo.cancel_reason`, which is operator-authored prose on the cancel action,
not workflow payload. The #860 audit log records only ids, the actor, and the disclosure tier — never
the payload it gated. This property must be **preserved by construction** as the dark surfaces gain
telemetry: the reusable emitter (worklist item 1) takes only actor / action / target-id / outcome, so
a caller cannot pass a payload through it.

The disclosure-tier design (#859/#860) already keeps step outputs out of telemetry: the audit records
that a journal was read and at which tier, never the outputs themselves.

## 3. Action inventory and coverage

Format: action → span / metric / log, then the finding. ✅ present and adequate, ⚠️ present but weak,
❌ absent.

### 3.1 Runs lifecycle (O-RUN)

| Action | Span | Metric | Log |
|---|---|---|---|
| Resume (all modes) | ✅ | ✅ `workflows.resumed` | ❌ |
| Cancel (with reason) | ✅ | ✅ `workflows.cancelled` | ❌ |
| Purge by rule | ✅ | ✅ `workflows.purged` | ❌ |
| Delete one | ✅ | ✅ `workflows.deleted` | ❌ |
| Suspend (timer/message) | ✅ (checkpoint) | ✅ `workflows.suspended` | n/a |

- **O-RUN-1 (MINOR)** — resume / cancel / purge / delete change run state on an operator's authority
  but emit no audit-grade log. The span carries the record, but spans are sampled and short-lived; a
  retained "who cancelled run X, with what reason" line is missing. U-RUN-6 in the UI review explicitly
  frames cancel as leaving an audit trail; the trail is a sampled span today.
- **O-RUN-2 (MINOR)** — the actor tag is the domain service's fixed owner identity (`ops`), not the
  authenticated principal who issued the request. For traceability the span attributes a service, not a
  person. The #860 journal audit resolves the real principal (`PrincipalDisplayName`); the run
  mutations should attribute the same way.

### 3.2 Catalog publish / retag / purge (O-CAT)

| Action | Span | Metric | Log |
|---|---|---|---|
| Add version (publish) | ✅ | ❌ | ❌ |
| Update version (retag, classify, describe) | ✅ | ❌ | ❌ |
| Delete / obsolete version | ✅ | ❌ | ❌ |
| Purge catalog | ✅ | ❌ | ❌ |
| Administration ops (admins, transfer) | ✅ | ❌ | ❌ |

- **O-CAT-1 (MINOR)** — publish/retag/purge have good spans but no audit-grade log, so retagging a
  version's management tags or classifying it `outputsSensitivity: sensitive` (a security-posture change,
  #859) leaves only a sampled span. This is the highest-value catalog gap given #859.
- **O-CAT-2 (POLISH)** — no publish/obsolete rate metric. Low value; catalog change is deliberate and
  individually spanned, not a rate to chart.

### 3.3 Environments and promotion (O-ENV)

| Action | Span | Metric | Log |
|---|---|---|---|
| Create / update / delete environment | ❌ | ❌ | ❌ |
| Add / remove / transfer environment administrator | ❌ | ❌ | ❌ |
| Make a version available (promote) | ❌ | ❌ | ❌ |
| Delete a version's availability (demote) | ❌ | ❌ | ❌ |
| Approve / deny an availability (promotion) request | ❌ | ❌ | ❌ |

- **O-ENV-1 (MAJOR)** — promotion is the act of letting a workflow version run in an environment
  (production included). Making a version available, and approving a promotion request, change what can
  execute where, and neither leaves a trace of who decided it. This is squarely governance and is dark.
- **O-ENV-2 (MAJOR)** — environment administrator changes (add / remove / transfer) reassign who
  governs an environment. No audit record of the reassignment.

### 3.4 Sources and credentials (O-CRED)

| Action | Span | Metric | Log |
|---|---|---|---|
| Create credential | ❌ | ❌ | ❌ |
| Update / rotate credential | ❌ | ❌ | ❌ |
| Delete credential | ❌ | ❌ | ❌ |
| Author / edit a usage grant | ❌ | ❌ | ❌ |
| Create / update / delete a source | ❌ | ❌ | ❌ |

- **O-CRED-1 (MAJOR)** — credential creation, rotation, and deletion are secret-custody events. Who
  rotated the production credential, and when, is exactly what a security audit asks first, and there is
  no record. (The secret value itself is correctly never logged — the gap is the *event*, not the
  material.) Rotation additionally warrants a metric: a rotation rate that drops to zero is a signal.
- **O-CRED-2 (MAJOR)** — usage-grant authoring (which identities a credential may act as) changes
  reach and is dark.
- **O-CRED-3 (MINOR)** — source create/update/delete is estate change, lower stakes than the
  credential itself, but still ungoverned by telemetry.

### 3.5 Grants and rules authoring (O-SEC)

| Action | Span | Metric | Log |
|---|---|---|---|
| Create / update / delete a security binding (grant) | ❌ | ❌ | ❌ |
| Create / update / delete a security rule | ❌ | ❌ | ❌ |
| Reorder security rules | ❌ | ❌ | ❌ |

- **O-SEC-1 (MAJOR)** — security bindings and rules *are* the authorization model. Authoring a grant
  or a rule changes who can reach what, and the entire surface is dark. A rule reorder can change the
  effective decision (first-match ordering) with no trace at all.

### 3.6 Access-request lifecycle (O-ACC)

| Action | Span | Metric | Log |
|---|---|---|---|
| Submit access request | ❌ | ❌ | ❌ |
| Approve (grant) | ❌ | ❌ | ❌ |
| Approve as eligible (PIM-style) | ❌ | ❌ | ❌ |
| Deny | ❌ | ❌ | ❌ |
| Withdraw (by requester) | ❌ | ❌ | ❌ |
| Revoke a granted access | ❌ | ❌ | ❌ |
| **Independent-decision refusal (403 own-request)** | ❌ | ❌ | ❌ |

- **O-ACC-1 (MAJOR)** — the access-request lifecycle decides who is granted reach into the estate.
  Every approve / deny / revoke is a governance decision with a named decider (`decidedBy`), and none is
  audited. This is the surface where an audit log matters most: it is the paper trail for elevation.
- **O-ACC-2 (MAJOR)** — the **independent-decision refusal** (a requester's attempt to decide their
  own request, refused with `403 own-request`) is a security control firing. That it fired — who tried
  to self-approve what — is precisely a signal a security team wants, and it is invisible. The issue
  singles this out; it is the top of the worklist alongside the decision audit itself.

### 3.7 Runner authorization (O-RNR)

| Action | Span | Metric | Log |
|---|---|---|---|
| Authorize a runner | ❌ | ❌ | ❌ |
| Quarantine (drain) a runner (#846) | ❌ | ❌ | ❌ |
| Revoke a runner (+ lease fence) (#846) | ❌ | ❌ | ❌ |
| Reinstate / re-authorize | ❌ | ❌ | ❌ |

- **O-RNR-1 (MAJOR)** — runner authorization decides which compute may claim and execute work.
  Revoke additionally fences the runner's in-flight leases (#846) — a containment action — and none of
  authorize / quarantine / revoke / reinstate is traced. Revoking a compromised runner is an incident
  action that should be the most-audited event on the system, and it is dark.

### 3.8 Administrators (O-ADM)

| Action | Span | Metric | Log |
|---|---|---|---|
| Add / remove a workflow administrator | ❌ | ❌ | ❌ |
| Transfer workflow administration | ❌ | ❌ | ❌ |
| Add / remove an environment administrator | ❌ | ❌ | ❌ |
| Transfer environment administration | ❌ | ❌ | ❌ |

- **O-ADM-1 (MAJOR)** — administrator-set changes reassign governance authority. Transfer is a
  hand-off of control; it is dark. (Some of these route through the catalog administration span for
  workflow versions — O-CAT — but the environment and transfer paths do not.)

### 3.9 Debug / draft runs (O-DBG)

| Action | Span | Metric | Log |
|---|---|---|---|
| Start / resume / cancel a debug run | ❌ | ❌ | ❌ |
| Inject a message into a debug run | ❌ | ❌ | ❌ |
| Delete a debug run / scenario | ❌ | ❌ | ❌ |

- **O-DBG-1 (MINOR)** — debug runs are a development affordance, lower stakes than governed
  execution, but a debug run still executes real operations against real sources. A span per debug run
  (mirroring the runtime executor's workflow span) would let a developer pivot a debug session into
  traces; today the designer's step-into is untraced. Lower priority than the governance surfaces.

### 3.10 Reads (O-RD)

Reads are not audited in general, by design — listing runs or the catalog is ordinary observability
traffic. The one exception is the step journal (#860), audited because its payload is sensitive. This
is the correct line: audit the sensitive read, not every list. No finding.

## 4. Ranked worklist

Ranked by security value first, then by breadth of the surface it closes.

1. **Reusable governance-audit emitter** (foundation). A `GovernanceAudit.Mutation(...)` helper on the
   `Corvus.Arazzo` source: a span (actor / action / target-kind / target-id / outcome) plus an
   audit-grade log, payload-safe by construction (it accepts only ids and a controlled outcome label, never a
   value). Generalizes
   `SensitiveReadAudit`. Everything below wires into it. **Landed as slice 2.**
2. **Access-request + availability-request decisions, including the independent-decision refusal**
   (O-ACC, O-ENV promotion). The paper trail for elevation and promotion, and the security control that
   refuses self-decision. **Landed as slice 3.**
3. **Credential create / rotate / delete + usage-grant authoring** (O-CRED). Secret custody. Add a
   rotation counter. **Landed:** `credential.create` (usage-grant authoring is audited here, since a
   binding's usage scope is set at creation and immutable after), `credential.update` (distinguishing a
   `rotated` — the secret reference changed — from a metadata `updated`), `credential.delete`, and the
   `refused-out-of-reach` privilege-escalation guard; rotations feed the `corvus.arazzo.credentials.rotated`
   counter.
4. **Grants and rules authoring** (O-SEC). The authorization model itself. **Landed:**
   `security-rule.create/update/delete` and `security-binding.create/update/delete`, plus the
   self-elevation guard's refusal (`refused-self-elevation`) on binding create/update. (Rule ordering is
   deployment configuration, not a mutation endpoint, so there is nothing to audit there.)
5. **Runner authorization: authorize / quarantine / revoke / reinstate** (O-RNR). Containment actions.
   **Landed:** `runner.authorize` (the prior state names the act — `authorized` from Pending, `reinstated`
   from Quarantined, `re-authorized` from Revoked), `runner.quarantine`, and `runner.revoke` (the
   containment action, recorded once the removal is durable and the in-flight leases are fenced), plus the
   `refused-not-administrator` gate on each.
6. **Environment create / update / delete, promotion (make-available / demote), admin transfers**
   (O-ENV, O-ADM). Who governs what runs where.
7. **Audit-grade log for the existing run and catalog spans; attribute the authenticated principal**
   (O-RUN-1, O-RUN-2, O-CAT-1). Upgrade the two surfaces that already have spans.
8. **Debug-run and source/workspace spans** (O-DBG, O-CRED-3). Development-time traceability.
9. **Governance rate metrics** (POLISH): counters per decision outcome (approved / denied / revoked),
   credential rotations, runner revocations — the rates worth charting.

Items 3–9 are follow-up slices, deliberately not rushed in one blind pass; each touches a distinct
handler family and wants its own contract tests. This document is the standing worklist for them.

## 5. Verdict

Payload hygiene is clean and the runtime + runs telemetry is genuinely good. The gap is breadth: the
governance surfaces that change the system's security posture — credentials, grants, rules, runner
authorization, access and promotion decisions, administrator transfers — emit no telemetry at all, so
the control plane cannot answer "who changed this, and when" for its most sensitive actions. The fix is
not per-surface cleverness but one reusable, payload-safe audit primitive applied uniformly, started in
slices 2 and 3 and ranked for completion above.
