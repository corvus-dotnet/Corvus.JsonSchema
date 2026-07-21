# ADR 0019. The products are the checkpoint

Date: 2026-07-21. Status: **Accepted**. Scope: what a durable run persists to be able to resume. Builds on
[ADR 0017](0017-code-generate-the-executor.md). This records why a run's resumable state is exactly the step
outputs it has already produced plus a scalar cursor, with no separate reified state machine.

## Context

A durable workflow has to survive a crash and resume where it left off. The usual way is to reify a state
machine: maintain an explicit representation of the run's position and variables, separate from the values the
workflow computes, and persist that. This second representation is a source of bugs, because it can drift from
what the executor actually did, and it is work the executor does purely for durability.

The code-generated executor ([ADR 0017](0017-code-generate-the-executor.md)) offers a simpler option. It only
ever constructs genuine products: a step's output is a real value the next step reads. If those values are
persisted, together with which step is next, the run can resume by re-entering the executor at that step with
those values already in hand. There is nothing else to reify.

### Grounded architectural facts

- **The state is step outputs plus a cursor.** The engine plan (`docs/ArazzoWorkflowEnginePlan.md` §9) states
  the durability model: the step-output values and a scalar cursor are the entire resumable state, one small
  JSON document per run. There is no separate state-machine representation.
- **A fresh run is the empty case of the same shape.** `IWorkflowRun`
  (`src/Corvus.Text.Json.Arazzo/IWorkflowRun.cs`) exposes `Cursor` (0 for a fresh run) and per-step output
  accessors that return their empty default before the step has run. The generated `while`/`switch` executor
  jumps straight to `Cursor`, so a fresh run and a resumed run enter the same code the same way.
- **A checkpoint serialises what already exists.** Because the executor constructs the products regardless of
  durability, checkpointing is serialising the values already built plus the cursor
  (`IWorkflowRun.CheckpointAsync(cursor)`), not building a separate durable representation.

## Decision

A durable run's resumable state is **the step outputs it has produced plus a scalar cursor**. The run persists
one small JSON document holding those values and the next step index. To resume, the executor is re-entered at
the cursor with the persisted outputs in hand. There is no separate reified state machine, because the
executor only constructs genuine products and those products, plus the cursor, are the entire state.

## Consequences

- There is no second representation to drift from the truth. What is persisted is exactly what the executor
  produced, so a resumed run cannot disagree with what a non-resumed run would have done.
- A checkpoint is cheap: it serialises values that already exist. Durability adds a write per step, not a
  parallel state machine.
- A fresh run and a resumed run are the same code path, differing only in the cursor and whether the outputs
  are empty, which is why the durable and non-durable executors behave identically
  ([ADR 0020](0020-durability-is-opt-in-codegen.md)).
- The checkpoint document is small and per-run, which is what makes the store abstraction
  ([ADR 0021](0021-state-store-abstraction.md)) simple: persist and load one document under optimistic
  concurrency.
