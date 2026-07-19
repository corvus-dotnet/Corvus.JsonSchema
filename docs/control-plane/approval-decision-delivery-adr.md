# ADR. Delivering an approver's decision to a suspended approval-workflow run

Date: 2026-07-19. Status: **Accepted**. Scope: the
approval capstone (#880, design §16.5.1), piece 3 (decision correlation). This records why the
approver's decision reaches the suspended approval workflow by being **published to a channel**
rather than handed to the run by a bespoke control-plane call.

## Context

The approval capstone makes the access-approval decision an ordinary, editable Arazzo workflow.
Its spine is: notify the approver (editable steps), **suspend on a `receive` step awaiting the
decision**, then on approval call `grantAccessRequest` (the bounded grant operation, piece 1).
A human approver makes the call through the existing `approve` / `reject` touchpoint, and that
action has to resume the suspended run with the decision.

The governing constraint is **no special magic**: the approval workflow must do only what any
workflow can already do. A privileged mechanism that exists solely to feed the approval workflow
would make it a special case and undermine the whole premise that Arazzo governs approvals with
ordinary workflow mechanisms. A secondary concern is not coupling the approval flow to a specific
transport configuration.

### Grounded architectural facts

These are established in the code, not assumed.

- **The control plane never executes production runs.** Stated verbatim three times in
  `ArazzoControlPlaneWorkspaceHandler.cs` ("The control plane never executes.", lines 1419-1420,
  1525, 1538). The control plane enqueues a `Pending` run and marks a run resume-claimable in the
  shared store (`WorkflowRun.SetResumeRequested` / `MarkResumeClaimable`); a **separate runner
  process** claims and advances it (`WorkflowDispatchService`, `Runner.Demo/Program.cs:261`). The
  only in-process worker the control plane hosts is the `$draft` **debug** runner
  (`InProcessDraftRunner`), which is explicitly documented as having "no production analogue".
- **There is exactly one transport-agnostic publish seam**: `IMessageTransport.PublishAsync`
  (`src/Corvus.Text.Json.AsyncApi/IMessageTransport.cs:54`), with eight implementations (NATS,
  Kafka, Azure Service Bus, AMQP, MQTT, WebSocket, an in-memory transport, and an instrumenting
  decorator). No Arazzo-specific publisher interface exists; callers use generated per-channel
  producers that wrap `PublishAsync`.
- **There is a complete shipping precedent**: the KYC verdict flow. An operator's out-of-band
  decision is published (`PublishKycVerdictProducer.PublishKycVerdictAsync` ->
  `transport.PublishAsync("kyc.verdict", ...)`) and consumed on the runner
  (`ReceiveKycVerdictConsumer` -> `KycVerdictResumeHandler` ->
  `WorkflowWorker.DeliverMessageAsync("kyc.verdict", accountId, ...)`), which resumes the suspended
  KYC run. This is the same shape as an approval decision resuming a suspended approval run.
- **The control plane owns dedicated system runner(s)** that execute its internal workflows (the
  approval workflow, and later the dispatcher and cron services). The approval run therefore executes
  on a control-plane-owned system runner, not on a tenant's runner, which is where the elevated
  `accessRequests:grant` system credential and `AccessContext.System` authority belong. Being a
  runner, that system runner is still a separate execution context fed by the transport, so the
  decision reaches it by a published channel message, not by an in-process hand-in. It also settles
  the open implementation question below: the system runner hosts the decision-channel consumer,
  exactly as the KYC runner hosts `ReceiveKycVerdictConsumer`.

## Options

### Option A. Scoped server-side delivery seam

Add a delivery method (for example on `ISecuredWorkflowManagement`) that calls
`WorkflowWorker.DeliverMessageAsync` in-process. The `approve` / `reject` handler calls it to hand
the decision directly to the suspended run, reusing the existing resume primitive but bypassing the
transport.

### Option B. Publish the decision to a channel (recommended)

The `approve` / `reject` handler publishes a decision message, correlated by request id, to the
channel the approval workflow's `receive` step awaits, through `IMessageTransport.PublishAsync`
(via a generated decision producer). The runner hosting the run consumes it and resumes, exactly as
the KYC verdict flow does. The touchpoint is simply one message producer; an external approver
surface (a Slack action, a mobile app) could publish the same decision.

## Antagonistic review

### Option A. Scoped server-side seam

| Pros (steelman) | Cons |
|---|---|
| Synchronous. The `approve` call can confirm the grant inline and return the updated request, matching the built-in strategy's response shape. | **It does not work for production runs.** The control plane never executes runs; a real suspended approval run lives in a runner process. `DeliverMessageAsync` in the control plane reaches only `$draft` debug runs in its own process, which is "no production analogue". There is no in-process worker holding the real run to deliver to. |
| Self-contained. No transport or broker dependency; needs only the control plane and the store. | **It is the special magic the constraint forbids.** A bespoke primitive feeds the approval workflow's `receive` in a way no other workflow's `receive` can be fed. It makes the approval workflow a privileged special case, defeating the "just a workflow" thesis of #880. |
| Clean authority story. The touchpoint's own authentication gates the decision, with no channel-provenance question. | **Generalising it forces a role violation.** To deliver to a runner-hosted run, the control plane would have to execute runs itself (become a runner) or reach into another process. That contradicts the explicit control-plane / runner separation. |
| No at-least-once or duplicate-delivery concern. | **Reinvents transport routing.** Delivering to the right run on the right runner is precisely what the transport already does; a server-side seam re-implements a narrower, worse version of it. |
| Simple in a single-process deployment where control plane and runner are co-hosted. | New bespoke security and audit surface (a "deliver an arbitrary message to a run" capability) that has to be scoped, gated, and audited on its own. |

### Option B. Publish the decision to a channel

| Pros | Cons (steelman the objections) |
|---|---|
| **No special magic.** It reuses the exact mechanism an ordinary workflow already uses (the KYC verdict flow). The touchpoint is one producer among possible many. Fully aligned with the editability vision (approvals admins add notify steps; external surfaces can produce decisions). | **Transport coupling** (the stated concern). *Mitigation:* the coupling is to `IMessageTransport`, the abstraction with eight implementations plus the in-memory transport, not to a concrete broker. Every message-driven deployment already configures a transport, and the control plane already holds one (it publishes `kyc.requests`). This adds no new coupling that the platform does not already have. |
| **Distributed-correct.** The message reaches whichever runner hosts the run, through the transport. The control plane stays a control plane. | **The demo needs a transport.** *Mitigation:* the AppHost already runs NATS JetStream, and the test suite uses `InMemoryMessageTransport`. Both paths already work; nothing new is required to run it. |
| **Maximal production fidelity.** The decision travels the same path every real message travels (transport -> consumer -> `DeliverMessageAsync`), so what is demoed and tested is what runs in production. | **Asynchronous.** The `approve` endpoint returns before the grant is written; the request reaches `Approved` when the workflow calls `grantAccessRequest`. This changes the touchpoint's response versus the built-in's synchronous grant. *Consequence, not a blocker:* the touchpoint records "decision submitted" and the request status transitions asynchronously, which the UI already reflects by status. |
| **A shipping precedent to copy** (KYC verdict), which lowers novelty risk and gives a tested shape for producer, channel, correlation, and consumer. | **Decision provenance.** Any channel producer could publish a decision, so the channel must be secured and the decision must carry the authenticated approver identity. *Mitigation:* the `approve` / `reject` endpoint authenticates the approver (the existing touchpoint gate) and stamps identity into the message; the channel is ACL-secured per deployment. |
| Extensible. Other decision producers (ChatOps, email action links, a mobile client) publish the same message with no server change. | **At-least-once delivery.** *Mitigation:* the grant is idempotent at the request state machine. `grantAccessRequest` on an already-`Approved` request returns 409, and the decision is applied under the request etag, so a duplicate decision is safe. |
| | **A runner must host a consumer on the decision channel** that forwards to `DeliverMessageAsync` (as the KYC demo wires `ReceiveKycVerdictConsumer`). *Consequence:* the runner setup gains a decision-channel consumer, or the engine auto-subscribes `receive` steps. This is a piece 2 / piece 3 implementation detail, not a design blocker. |

### Rejected sub-variant. Store resume-claimable with the decision payload

The control plane could mark the run resume-claimable in the shared store and attach the decision,
for the runner's dispatch loop to pick up. This avoids the transport, but it bypasses the
`receive` / channel model entirely (a `receive` step is satisfied by a channel message, not a store
poke), so the approval workflow would need a bespoke suspend-and-resume shape rather than an ordinary
`receive`. That is a different flavour of the same special magic Option A carries, so it is rejected
for the same reason.

## Decision drivers, scored

| Driver | Option A | Option B |
|---|---|---|
| No special magic (the governing constraint) | ✗ bespoke, privileged | ✓ ordinary workflow mechanism |
| Works for production (runner-hosted) runs | ✗ debug-only, no production analogue | ✓ distributed-correct |
| Avoids transport-config coupling | ✓ none | ~ couples to the transport abstraction, not a concrete broker |
| Production fidelity (demo == production path) | ✗ | ✓ |
| Precedent to copy | ✗ novel | ✓ KYC verdict flow |
| Synchronous touchpoint response | ✓ | ✗ async (acceptable) |

Option A wins only on transport-independence and synchronous response. Its transport-independence is
moot because it does not work for production runs at all, and the synchronous response is a
convenience the asynchronous status transition replaces. Every other driver, including the governing
one, favours Option B.

## Decision

**Option B. Publish the decision to a channel through `IMessageTransport`, following the KYC verdict
precedent.** The `approve` / `reject` touchpoint authenticates the approver and publishes a
decision message correlated by request id; the runner hosting the approval run consumes it and
resumes; the workflow then calls the bounded `grantAccessRequest` operation. The approval workflow
uses an ordinary `receive` step, with no privileged delivery path.

## Consequences

- **Piece 3 (decision correlation)** becomes: define the decision channel and a generated producer,
  publish from the `approve` / `reject` handlers (authenticated, identity-stamped), and correlate by
  request id. The built-in synchronous strategy is unchanged; only the workflow-backed strategy
  routes decisions this way.
- **Piece 2 (workflow-backed strategy)** starts the approval run on submit and relies on the
  transport for the decision, with no in-process delivery.
- The `approve` / `reject` response under the workflow-backed strategy is "decision submitted"; the
  request reaches `Approved` asynchronously when the workflow completes the grant. Idempotency is
  guaranteed by the request state machine and the bounded grant's 409 on a non-pending request.
- The decision-channel consumer is hosted on the control plane's **system runner** (as
  `ReceiveKycVerdictConsumer` is hosted on the KYC runner), forwarding to `DeliverMessageAsync`.
- **Piece 4 (deploy bootstrap)** provisions the control plane's internal environment and system
  runner(s) alongside the approval workflow, the system credential holding `accessRequests:grant`,
  and the workflow's §15 administrators. The system runner claims the internal environment, executes
  the internal workflows, and carries the system credential that authorises the bounded grant call.
- No new "deliver a message to a run" capability is added to the control plane, so no new bespoke
  security surface is introduced.
