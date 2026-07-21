# ADR 0022. The resume-mode taxonomy

Date: 2026-07-21. Status: **Accepted**. Scope: the ways a faulted run may be resumed. Builds on
[ADR 0019](0019-products-are-the-checkpoint.md) and [ADR 0021](0021-state-store-abstraction.md). This records
the fixed set of resume modes, and why each is a checkpoint mutation under optimistic concurrency followed by
re-entering the executor.

## Context

A durable run can fault: a step's operation returned an error, or a bad input produced a bad output downstream.
An operator needs ways to recover a faulted run, and those ways are not all the same. Retrying the failed step
is the common case, but sometimes the fix is to go back and re-run from an earlier step, to skip a step that
cannot succeed, or to correct the persisted state and try again. The set of recovery actions has to be fixed
and named, so an operator (and the audit trail) knows exactly what each one does to the run.

Because the run's state is its checkpoint ([ADR 0019](0019-products-are-the-checkpoint.md)), every recovery is
a mutation of that checkpoint followed by re-entering the executor. What differs is which part of the
checkpoint each mode mutates.

### Grounded architectural facts

- **Four resume modes.** `ResumeMode` (`src/Corvus.Text.Json.Arazzo.Durability/ISecuredWorkflowManagement.cs`,
  plan §11) is `RetryFaultedStep`, `Rewind`, `Skip`, and `StatePatch`.
  - `RetryFaultedStep` re-executes from the last checkpoint, the faulted step. The common case.
  - `Rewind` moves the cursor back to an earlier step (`ResumeOptions.TargetCursor`) and re-runs forward,
    overwriting the re-executed steps' outputs.
  - `Skip` advances the cursor past the faulted step (to `TargetCursor`, or the next index), optionally
    recording operator-supplied outputs (`ResumeOptions.SkipOutputs`) so downstream references resolve. Only
    safe when downstream does not need the skipped step's real outputs.
  - `StatePatch` applies an RFC 6902 JSON Patch (`ResumeOptions.Patch`) to the run's persisted context (an
    object of `inputs` and `stepOutputs`) to fix a bad input or output, then retries the faulted step.
- **Each is a checkpoint mutation under optimistic concurrency.** Every mode loads the checkpoint, mutates
  status, cursor, or state, then re-enters the executor, all under the store's optimistic concurrency and
  lease ([ADR 0021](0021-state-store-abstraction.md)).
- **Cancel is not a resume mode.** Cancelling a run is a distinct operation, not a way of resuming it, so it is
  not in the `ResumeMode` set.

## Decision

A faulted run is resumed by one of a **fixed set of four modes**: `RetryFaultedStep`, `Rewind`, `Skip`, and
`StatePatch`. Each is a named mutation of the run's checkpoint (cursor, status, or persisted state) followed by
re-entering the executor, performed under optimistic concurrency and the run lease. Cancelling is a separate
operation, not a resume mode.

## Consequences

- An operator has a small, named vocabulary of recoveries, each with a defined effect on the run, so a
  recovery action is auditable and predictable.
- Every mode works through the checkpoint model ([ADR 0019](0019-products-are-the-checkpoint.md)), so resuming
  is loading a document, mutating it, and re-entering the executor, not a special execution path.
- Optimistic concurrency on the checkpoint means two operators, or an operator and the runner, cannot resume
  the same run into conflicting states: the second write fails and retries against the current state.
- The modes map directly to the resume UI, where the operator picks retry, rewind, skip, or state-patch, and
  supplies the target cursor, skip outputs, or patch the chosen mode needs.
