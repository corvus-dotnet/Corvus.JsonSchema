# ADR 0050. A durable per-step run journal

Date: 2026-07-21. Status: **Accepted**. Scope: how a durable run records what each step did, so an operator can
diagnose it. Builds on [ADR 0019](0019-products-are-the-checkpoint.md), and touches
[ADR 0013](0013-step-output-disclosure-tier.md) and [ADR 0038](0038-payload-safe-governance-audit.md). This
records why a run persists a metadata-only per-step journal inside its checkpoint, and why that gives operators a
step-by-step trace that survives resume.

## Context

An operator diagnosing a durable run needs to see what it actually did: which steps ran, in what order, with what
outcome, after how many retries, and roughly when. Today the control plane cannot answer that. A run's detail shows
metadata, a cursor, and (for a faulted run) the terminal fault message, but there is no list of executed steps, no
per-step outcome, and no timing anywhere. The current step-journal read is a shallow projection of the step outputs
and attests no status and no timing, because the data to attest them is not retained. The rich per-step trace that
exists is produced only for design-time debug runs (the designer's debug dock), assembled live from a recording
transport, and is never persisted for a catalogued durable run.

The checkpoint model ([ADR 0019](0019-products-are-the-checkpoint.md)) is that the products are the state: the
resumable state is the step outputs plus a cursor, one small JSON document per run. So a per-step trace is not a
byproduct of that model; it is additional metadata a run would have to record on purpose.

### Grounded architectural facts

- **Per step, the checkpoint retains only outputs and a retry count.** `WorkflowCheckpointState`
  (`src/Corvus.Text.Json.Arazzo.Durability/WorkflowCheckpointState.cs`) holds `StepOutputs` (stepId to outputs) and
  `RetryCounters` (stepId to attempt count). Everything else is run-level: `Status`, `Cursor`,
  `CreatedAt`/`UpdatedAt`, and a single terminal `Fault` (stepId, attempt, error). There is no per-step status and
  no per-step timing.
- **The executor emits no per-step timing.** The executor code generation
  (`src/Corvus.Text.Json.Arazzo.CodeGeneration`) records a per-step retry counter and the outputs, but no start or
  end timestamp per step. This is also why the workflow-duration and step-duration histograms were retired: an
  in-process timer cannot measure across a durable re-entry.
- **The checkpoint is an opaque blob to the backends.** `WorkflowCheckpoint` is
  `(ReadOnlyMemory<byte> Utf8, WorkflowEtag Etag)` and `IWorkflowStateStore.SaveAsync` stores the bytes. The
  structured contents are written by the hand-rolled `WorkflowCheckpointSerializer` (a `Utf8JsonWriter`, not a
  generated schema). So a field added inside the checkpoint document is invisible to every backend; only the
  serializer and the reader see it.
- **The journal read is already a sensitive read.** `SecuredWorkflowManagement.GetStepJournalAsync` loads the
  checkpoint state and projects `StepOutputs`; it is gated behind the disclosure tier
  ([ADR 0013](0013-step-output-disclosure-tier.md)) and emits the `workflow.journal.read` audit span, because a
  journal discloses strictly more than the run detail.
- **A trace shape already exists.** Debug runs assemble a trace via `MetadataTraceAssembler` in the shape
  `{ stepId, status, attempt, outputs?, requests[] }` with `outcome`, `pausedBefore`, `fault`, and `wait`, which
  the designer's debug dock renders.

## Options

The decision is where per-step status and timing live, and how much is recorded.

1. **Project it from the current checkpoint.** Rejected. The timing data does not exist to project, and per-step
   status is only weakly inferable (from the cursor, presence in outputs, and the single terminal fault), so the
   read would guess. The current shallow journal avoids that by attesting nothing.
2. **Record it in a separate, queryable journal store.** Rejected. A per-step journal is read as a whole with its
   run, not queried across runs, so a dedicated store adds a persistence surface and a cross-store consistency
   problem for no query benefit.
3. **Add per-step columns to each backend's run schema.** Rejected. The journal is not something a backend filters
   or sorts on, and indexed columns would thread a field through every backend (the concern that deferred the
   store-schema follow-ups), whereas the journal only needs to be stored and returned whole.
4. **Record a per-step journal inside the checkpoint blob (chosen).** The executor appends a small metadata entry
   per step as it advances, the serializer persists an array in the checkpoint document, and the read enriches the
   existing journal projection. Because the blob is opaque to the backends, no backend changes and no index column
   are needed.

### Antagonistic review

- **Per-step timing across a durable re-entry.** The retired duration histograms failed because an in-process
  timer measures a single advance, not the whole run. This is different: the journal persists per-step `startedAt`
  and `endedAt` timestamps at the moment each step runs, sourced from the `TimeProvider` the executor already
  threads. A completed step is never re-run, and its entry is restored from the checkpoint on resume, so the
  journal accumulates correctly across advances and the timestamps are the true per-step windows. The
  persisted-timestamp form is sound where an in-process timer was not.
- **Unbounded growth on a loop.** A `goto` loop re-executing a step could append entries without bound. So the
  journal appends one entry per step execution (a true history, which shows retries and loop passes) but is capped
  at the most recent N entries with a `truncated` marker, so a pathological run cannot bloat the checkpoint.
- **Payload leakage.** A journal that carried request and response exchanges would enlarge the checkpoint and widen
  disclosure. So the durable journal is metadata only (stepId, status, attempt, timestamps); the exchange-level
  trace stays a debug-run concern. This keeps the checkpoint small and the journal payload-safe, consistent with
  the disclosure tier ([ADR 0013](0013-step-output-disclosure-tier.md)) and the payload-safe posture
  ([ADR 0038](0038-payload-safe-governance-audit.md)).
- **Runs that predate the journal.** The serializer reads the array when present and tolerates its absence, the
  same way as the nullable `updatedAt` seam. A run that started before the journal existed carries entries only for
  steps run afterwards, and the read notes that the journal begins at the current cursor rather than fabricating
  history.
- **A step that suspends.** A step that parks on a receive or timer records one entry with a `startedAt` and no
  `endedAt` until it resumes and completes, when `endedAt` is stamped. It does not record a fresh entry per wake.

## Decision

A durable run records a **metadata-only per-step journal inside its checkpoint document**. As the executor
advances, it appends one entry per step execution, `{ stepId, status, attempt, startedAt, endedAt }`, where
`status` is `succeeded`, `faulted`, `skipped`, or `suspended`, `attempt` comes from the existing retry counter, and
the timestamps come from the executor's `TimeProvider`. The `WorkflowCheckpointSerializer` writes and reads the
entries as a `stepJournal` array in the checkpoint document, so the journal rides in the opaque blob and needs no
backend change and no index column. The journal is capped at the most recent N entries with a `truncated` marker.
The entry shape aligns with the debug-run trace, minus the request and response exchanges, so a durable run and a
debug run render the same trace shape. The read path enriches the existing `GetStepJournalAsync` projection and
stays gated as a sensitive read.

The four scoping decisions are ratified: per-step timestamps for timing, append-with-cap for loops,
journal-begins-at-cursor for runs that predate the feature, and one entry per step (stamped on completion) for a
suspended step.

## Consequences

- The operator's core diagnostic question, "what did this run actually do", is answerable from the run detail: the
  executed steps, their outcomes, retry attempts, and timing, step by step. This closes the strongest single gap
  the antagonistic UI review recorded.
- Durable runs and debug runs share a trace shape, so the same UI component renders both, and an author's debug
  view and an operator's run view agree.
- The change is contained to the serializer, `IWorkflowRun`, the executor code generation (a per-step record at
  each step boundary plus a threaded start timestamp), and the read projection with its contract. No backend, no
  index column, and no new store.
- The checkpoint grows by a small metadata entry per step, bounded by the cap. Because the entries carry no
  payloads, the growth is small and the disclosure surface is unchanged.
- The journal exists only where a run checkpoints, so it is opt-in with durability, consistent with durability
  being opt-in ([ADR 0020](0020-durability-is-opt-in-codegen.md)).
