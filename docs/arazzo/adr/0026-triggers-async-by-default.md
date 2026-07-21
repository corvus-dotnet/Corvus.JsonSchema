# ADR 0026. Triggers are async by default: HTTP always on, an enqueued run the runner claims

Date: 2026-07-21. Status: **Accepted**. Scope: how a run is started. Builds on
[ADR 0023](0023-two-process-store-as-queue.md). This records why starting a run enqueues it and returns
`202`, why HTTP is the always-on trigger, and why message and schedule triggers are host-configured rather
than a package concern.

## Context

A run has to be startable, and the natural expectation is a synchronous call that runs the workflow and
returns its result. But the control plane never executes runs ([ADR 0023](0023-two-process-store-as-queue.md));
a runner does, on its own schedule, after claiming the run. So a start cannot block on the result without the
control plane either executing the run itself or holding a request open across the runner's work. The start
has to be asynchronous. Separately, a workflow might be triggered by an HTTP call, a message on a channel, or
a schedule, and where that trigger configuration lives (in the package, or in the host) affects whether a
package couples to a transport.

### Grounded architectural facts

- **Starting a run is async, returning `202`.** `HandleStartCatalogWorkflowRunAsync`
  (`ControlPlane.Server/ArazzoControlPlaneCatalogHandler.cs`) enqueues a `Pending` run and returns
  `202 Accepted`. The operation is `POST /catalog/{baseWorkflowId}/versions/{versionNumber}/runs`
  (`startCatalogWorkflowRun`), taking an `environment` and an idempotency-key header, with responses
  `202/404/409/422`.
- **HTTP is the always-on trigger.** The run-start endpoint is part of the control-plane surface and needs no
  transport configuration, so every deployment can start a run by HTTP.
- **Message and schedule triggers are host-configured, not a package extension.** The design (execution-host
  §6) declines an `x-arazzo-triggers` package extension: message triggers are handled by an in-band dispatcher
  workflow (receive a message, then start the target's HTTP trigger), and durable cron is a scheduler service,
  so neither couples a package to a transport.

## Decision

Starting a run is **asynchronous by default**. The run-start endpoint enqueues a `Pending` run and returns
`202 Accepted`; the runner claims and advances it ([ADR 0023](0023-two-process-store-as-queue.md)). HTTP is the
always-on trigger, part of the control-plane surface with no transport configuration. Message and schedule
triggers are host-configured (a dispatcher workflow, a scheduler service) rather than declared in the package,
so a package never couples to a transport through a trigger declaration.

## Consequences

- A start returns immediately with a run to poll or subscribe to, rather than holding a connection open across
  the runner's work. The run's progress is read from the run resource.
- Every deployment can start a run by HTTP with no broker, because the HTTP trigger needs no transport.
- A package does not declare its triggers, so the same catalogued package runs under HTTP, message, or
  schedule triggering depending only on how the host is wired, not on the package.
- Idempotency is carried on the start (the idempotency-key header), so a retried start does not create a
  duplicate run.
