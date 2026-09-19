# ADR 0072. Re-running a run is a server-side operation through the one start admission

Date: 2026-09-19. Status: **Accepted**. Implementation: REST API, server and CLI landed; the console follows. Scope: how an operator starts a run again from the beginning. Follows [ADR 0068](0068-execution-budget-fuel-wall-clock-depth.md), whose re-budget keeps a faulted run's work; this is the remedy when that is not possible or not wanted.

## Context

A run that cannot be resumed (it made as many attempts as the journal holds, or it is older than the deployment's ceiling allows), or that should not be (its completed work is not to be trusted, or it exhausted its budget for reasons outside the workflow), had one remedy: start a new run by hand, with the same inputs. Nothing offered that. The only start operation takes the inputs in its body, and a run's inputs are not returned by any read, so an operator had to have kept them.

## Options

1. **Return a run's inputs on its detail, and let the client post them to the existing start.** No new operation. But inputs can carry personal and sensitive data, and every reader of a run would gain them: a new disclosure on an existing read scope, to every client, for the sake of one action.
2. **A server-side re-run operation.** The server reads the original's inputs from its checkpoint and starts the new run. Inputs never leave the server.

## Decision

**`POST /runs/{runId}/rerun` starts a new run of the same workflow version, in the same environment, with the same inputs and tags.** The server reads them from the original run's checkpoint, within the caller's read reach (a run they cannot see is answered as not found). It requires the scope a start requires and accepts an `Idempotency-Key`, so a repeated request starts one run. It answers as a start answers.

**A re-run goes through the one start admission.** The chain a start owes (the version and the environment in the caller's reach, the tenancy agreement between them, a runnable version, inputs that validate, availability in the environment, a hosting runner at the environment's isolation, a live deployment where one is required, and the tenant's capacity) was inline in the catalog's start handler. It is now one seam, `IRunStartAdmission`, that the catalog's start, the re-run and a schedule's run-now all call, returning an outcome no one operation owns so that each answers in its own response type. A second copy of the chain is how a start path comes to skip a gate. So a re-run is refused exactly where a start would be: a version withdrawn from the environment, no hosting runner, inputs that no longer validate, a tenant at capacity.

**The new run is a run of its own.** It gets a fresh execution budget resolved from the environment as it is now, a new correlation id, and records the original as `rerunOf` on its checkpoint, returned on its detail. The re-run is audited as `run.rerun` with both ids. `rerunOf` is a field of the checkpoint body and is not indexed, so no store backend changes, and a run cannot list its re-runs.

**What cannot be re-run is refused as `not-rerunnable`:** a draft run, which is a debug session over a working copy, the scheduler's own run, and a run whose workflow version is no longer in the catalog or is outside the caller's reach.

## Consequences

- A run's inputs stay undisclosed. No read returns them and no client handles them to re-run.
- Schedule run-now comes through the same admission (threat model H45, closed). It had its own shorter copy of the chain, which counted no capacity, validated no inputs and assumed in-process isolation. It is refused now exactly where a start would be, including 422 for stored inputs the target would refuse and 429 at the tenant's capacity.
- A versioned workflow id has one reading, `WorkflowVersionId.TryParse`: digits only after the last `-v`, in the invariant culture. There had been four private copies, three of which read `flow-v-3` as version minus three.
- Listing the re-runs of a run needs an indexed column on every run store. It is not provided.
- A re-run repeats the workflow's effects on its sources. It is an operator's decision and is audited as one.
