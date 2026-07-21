# ADR 0028. Pluggable execution backends and isolation models

Date: 2026-07-21. Status: **Proposed**. Scope: how a runner isolates the execution of a run. Builds on
[ADR 0024](0024-collectible-assembly-per-version.md). This records the intended seam for dispatching a run to a
pluggable execution backend with a chosen isolation model, and is explicit that only the in-process backend
ships today.

## Context

Today a runner executes a run in-process, loading the version's assembly into a collectible load context
([ADR 0024](0024-collectible-assembly-per-version.md)). In-process is efficient and enough when the runner
trusts the workflow code it hosts. Some deployments will want stronger isolation: a per-run micro-guest, a
serverless function, or a container, so a run cannot affect the runner or its neighbours. That isolation should
be a choice a deployment makes, not a rewrite of the runner, which means dispatch has to go through a seam that
an isolation model plugs into.

### Grounded architectural facts

- **The in-process collectible-ALC backend ships.** A run executes in-process through
  `WorkflowExecutorLoader` and a collectible `AssemblyLoadContext` ([ADR 0024](0024-collectible-assembly-per-version.md)).
- **The pluggable-backend seam is a design draft.** Execution-host §5.6 ("Execution backends and isolation
  models") is marked DRAFT: it specifies a seam that dispatches a run to a pluggable execution backend with a
  chosen isolation model, in-process today, and equally a per-run micro-guest, a serverless function, or a
  container, with resume using the same seam. The out-of-process backends are not built.
- **The tracking issue is deferred.** The alternative execution backends are the subject of an open,
  deferred issue (#876), so this ADR records the intended direction rather than a shipped seam.

## Decision (proposed)

Execution is to be a **pluggable backend seam**. A runner dispatches a run to an execution backend that
applies a chosen isolation model, and a resume goes through the same seam. The in-process collectible-load-
context backend is the shipping default. Out-of-process backends (per-run micro-guest, serverless, container)
are a declared future the seam is shaped to admit, not yet implemented.

This ADR is **Proposed**. The in-process backend is the current reality; the pluggable seam and out-of-process
backends are a design direction pending the deferred work.

## Consequences

- The runner's execution path is expected to move behind a backend seam, so a deployment can select an
  isolation model without changing the runner.
- Until the seam ships, execution is in-process only, and a deployment that needs stronger isolation runs
  runners in separately-isolated hosts at the process boundary rather than per-run.
- Resume is to use the same backend seam as a fresh run, so an isolation model applies uniformly to starting
  and resuming.
- Because this is Proposed, an implementation should revisit this ADR and move it to Accepted (or supersede
  it) once the seam and at least one out-of-process backend land.
