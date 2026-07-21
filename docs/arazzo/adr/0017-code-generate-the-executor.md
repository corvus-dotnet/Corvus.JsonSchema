# ADR 0017. Code-generate the executor, not a runtime interpreter

Date: 2026-07-21. Status: **Accepted**. Scope: how an Arazzo workflow is executed. This records why a workflow
is compiled to a strongly-typed executor ahead of time rather than interpreted from the document at run time.

## Context

An Arazzo document describes a workflow: ordered steps that call operations, pass data between them, and
branch on criteria. There are two ways to run it. Interpret the document at run time, walking the steps and
resolving each operation, parameter, and expression dynamically. Or compile the document ahead of time into a
typed executor that runs as ordinary code. The choice sets the performance floor and the failure surface for
every run.

### Grounded architectural facts

- **The decision is a code generator.** The engine plan (`docs/ArazzoWorkflowEnginePlan.md` §3) weighs a
  runtime interpreter and rejects it as the primary mode, and builds a code generator instead
  (`Corvus.Text.Json.Arazzo.CodeGeneration`). The generated executor is a static `ExecuteAsync`, generic over
  the workflow's inputs and outputs, with no reflection and no boxing on the run path.
- **The executor is the generated artifact.** `WorkflowExecutorEmitter` (and `ControlFlowEmitter` for the
  branching form) emit the executor; a run enters it through the non-generic `IHostedWorkflow` adapter
  (`src/Corvus.Text.Json.Arazzo/IHostedWorkflow.cs`), so a host runs a workflow without referencing the
  generated types.
- **The interpreter was removed from the generated code too.** A later phase (plan §7) dropped the remaining
  interpreter indirection from the generated executor, so the run path is fully static.

## Options

**Runtime interpreter (rejected as primary).** Load the Arazzo and its sources at run time and walk them.
Rejected: it pays reflection and dynamic dispatch on every step of every run, and moves errors that a compiler
would catch (a mistyped output reference, a missing operation) to run time. It is retained only as a possible
later dynamic mode, not the default.

**Ahead-of-time code generation (chosen).** Compile the document to a typed executor.

## Decision

An Arazzo workflow is **compiled ahead of time into a strongly-typed executor**, not interpreted at run time.
The generator emits a static `ExecuteAsync` and a non-generic `IHostedWorkflow` adapter. The run path has no
reflection and no boxing, and a whole class of authoring errors is caught at generation time rather than at
run time.

## Consequences

- Runs are fast and allocation-lean, because the executor is ordinary typed code, not an interpreter loop.
- Authoring errors surface at generation. A step that references a missing operation, or an output that does
  not exist, fails to generate rather than failing mid-run.
- The generated executor is the unit that is versioned, compiled, and loaded
  ([ADR 0020](0020-durability-is-opt-in-codegen.md)), which is what makes integrity binding and collectible
  unload possible.
- Because the executor only ever constructs genuine values, its state is exactly those values, which is what
  makes the reification-free checkpoint model possible
  ([ADR 0019](0019-products-are-the-checkpoint.md)).
