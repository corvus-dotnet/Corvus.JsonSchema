# ADR 0020. Durability is opt-in code generation

Date: 2026-07-21. Status: **Accepted**. Scope: how a workflow becomes durable. Builds on
[ADR 0017](0017-code-generate-the-executor.md) and [ADR 0019](0019-products-are-the-checkpoint.md). This
records why durability is a generation-time choice that changes the emitted executor, so a non-durable
workflow pays nothing for a durability path it does not use.

## Context

Some workflows need to survive a crash and resume; some are short, in-process, and do not. Durability has a
cost: a checkpoint write after each step, and the plumbing to thread a per-run durability seam. That cost
should fall only on workflows that opt in. The question is where the choice is made. At run time (one executor
that branches on a durability flag per step), or at generation time (two shapes of executor, one that
checkpoints and one that does not).

### Grounded architectural facts

- **`durable` is a generation flag.** The `arazzo-generate` command takes `--durable`
  (`src/Corvus.Json.Cli.Core/ArazzoGenerateCommand.cs`), and `ArazzoGenerationDriver.GenerateAsync` and
  `WorkflowExecutorProvider.BuildExecutor` carry a `durable` parameter. At catalog-add the executor is built
  durable by default.
- **The emitted control flow differs.** `ControlFlowEmitter`
  (`src/Corvus.Text.Json.Arazzo.CodeGeneration/ControlFlowEmitter.cs`) seeds the state from the run in durable
  mode (`int __state = run?.Cursor ?? 0;`) and from zero otherwise (`int __state = 0;`), and emits a
  `CheckpointAsync` after each step only when durable. The non-durable executor has no checkpoint calls.
- **The executor is compiled and bound at catalog-add.** `WorkflowExecutorProvider.BuildExecutor` generates
  the clients and executor in memory, compiles them into a single assembly
  (`DynamicCompiler.CompileToAssemblyBytes`), and emits a manifest binding the assembly digest and package
  hash to the version, so the runnable artifact is produced when the package is catalogued.

## Decision

Durability is an **opt-in generation-time choice**. The generator emits a checkpointing executor when durable
is requested and a plain executor otherwise. A non-durable workflow's executor contains no checkpoint calls
and threads no durability seam, so it pays nothing for durability. The choice is made once, at generation, not
per step at run time.

## Consequences

- A non-durable workflow runs as plain typed code with no per-step write, so the in-process case is not taxed
  by a durability path it does not use.
- A durable workflow's executor checkpoints after each step and resumes from the cursor, using the same
  products-as-state model ([ADR 0019](0019-products-are-the-checkpoint.md)), so the durable and non-durable
  forms produce the same results.
- The durable executor is compiled into a single assembly bound to the package version by digest, which is
  what the loader verifies and unloads (the runner and execution-host domain covers loading).
- Because durability is a generation flag rather than a run-time branch, there is no per-step conditional in
  the hot path deciding whether to checkpoint.
