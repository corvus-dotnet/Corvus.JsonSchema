# ADR 0024. One collectible assembly per version, loaded and unloaded on demand

Date: 2026-07-21. Status: **Accepted**. Scope: how a runner loads and evicts workflow executors. Builds on
[ADR 0020](0020-durability-is-opt-in-codegen.md). This records why each workflow version is one assembly loaded
into a collectible load context, cached on first use, and unloaded when the version is obsoleted.

## Context

A runner hosts many workflow versions, and the set changes over time as versions are published and obsoleted.
Loading every version at startup would waste memory on versions that never run, and would not handle versions
published after startup. Loading a version and never unloading it would leak memory as versions accumulate.
The runner needs to load a version on first use and reclaim it when it is no longer needed, without disturbing
runs that are still using it.

### Grounded architectural facts

- **One assembly per version.** A catalogued version compiles to a single assembly bound to the version by
  digest ([ADR 0020](0020-durability-is-opt-in-codegen.md), `WorkflowExecutorProvider`).
- **Loaded into a collectible context, keyed by version, cached.** `WorkflowExecutorLoader.Load`
  (`src/Corvus.Text.Json.Arazzo.Execution/WorkflowExecutorLoader.cs`) loads the assembly into a collectible
  `AssemblyLoadContext` keyed by `(baseWorkflowId, versionNumber)`, activates the manifest entry type to an
  `IHostedWorkflow`, and caches it for reuse.
- **Unloaded on demand, with in-flight runs kept alive.** `WorkflowExecutorLoader.Unload` removes the version
  from the cache and disposes its collectible load context, so an obsoleted version evicts. In-flight runs
  hold a reference that keeps the context alive until they finish.

## Decision

Each workflow version is **one assembly loaded into a collectible `AssemblyLoadContext`**, keyed by
`(baseWorkflowId, versionNumber)`, loaded on first use and cached. Unloading a version disposes its load
context so the memory is reclaimed, while in-flight runs keep their context alive until they complete.

## Consequences

- The runner pays memory only for versions it actually runs, and only from first use, not from startup.
- A version published after the runner started is loaded on its first run, so the runner does not need
  restarting to pick up new versions.
- Obsoleting a version reclaims its memory, and does so safely: a run mid-flight on that version is not pulled
  out from under it, because its reference holds the context alive.
- The load path verifies the assembly's integrity before activating it
  ([ADR 0025](0025-integrity-binding-optional-signature.md)), so a tampered or mismatched assembly does not
  load.
