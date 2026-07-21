# ADR 0034. A standalone published-endpoint hosting service is not required

Date: 2026-07-21. Status: **Accepted**. Scope: whether publishing a workflow at an HTTP endpoint needs a
separate service. Builds on [ADR 0023](0023-two-process-store-as-queue.md) and
[ADR 0026](0026-triggers-async-by-default.md). This records why a dedicated hosting service for published
workflow endpoints is declined, because the existing run-start endpoint and runner already provide the
capability.

## Context

"Publishing" a workflow usually means exposing it at a configured, secured HTTP endpoint that callers invoke to
run it. A natural design is a dedicated hosting service that stands up such endpoints. The question is whether
that service adds capability the platform lacks, or only deployment topology it already has another way.

### Grounded architectural facts

- **The run-start endpoint already publishes a version at a secured endpoint.** The control plane's
  `POST …/versions/{n}/runs` operation (`startCatalogWorkflowRun`,
  [ADR 0026](0026-triggers-async-by-default.md)) serves a catalogued version at a configured, secured HTTP
  endpoint: inputs validated against the version's baked schema, gated on `runs:write`, the version pinned in
  the path, and a durable run created. The execution-host design (§6.2) names this endpoint as exactly the
  "publish the workflow at a configured, secured endpoint" goal.
- **The runner already loads and executes.** `WorkflowExecutorLoader` and `HostedWorkflowResumer` load and run
  the catalogued assembly ([ADR 0024](0024-collectible-assembly-per-version.md),
  [ADR 0023](0023-two-process-store-as-queue.md)).
- **The decision is recorded and deferred.** Catalog design and issue #878 decline a standalone hosting
  service. What a separate host would add is deployment topology (a dedicated URL and inbound auth per
  workflow, deployed independently), not new capability.

## Decision

A dedicated **standalone published-endpoint hosting service is not required, and is declined**. The control
plane's run-start endpoint already publishes a catalogued version at a configured, secured HTTP endpoint with
schema validation, capability gating, version pinning, and a durable run, and the runner already loads and
executes it. A separate host would add only deployment topology, not capability, so it is deferred until a
concrete scenario needs that topology.

## Consequences

- Publishing a workflow is running it through the existing secured run-start endpoint, so there is one HTTP
  surface to secure, validate, and audit, not two.
- The capability is not blocked on building a new service. A deployment that needs a per-workflow URL deployed
  independently is the only case that would call for more.
- If that case arrives, the leaner shape is an embeddable ASP.NET host extension that reuses the loader and
  resumer, not a bespoke service that re-implements the run-start, validation, and authorization the control
  plane already has.
- This keeps the two-process model intact ([ADR 0023](0023-two-process-store-as-queue.md)): the control plane
  exposes the HTTP surface and enqueues, the runner executes, and no third process is introduced.
