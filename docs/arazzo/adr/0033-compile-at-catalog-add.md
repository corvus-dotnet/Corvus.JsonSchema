# ADR 0033. Compile at catalog-add; the package is a complete code-generation input

Date: 2026-07-21. Status: **Accepted**. Scope: when a workflow's executor is compiled. Builds on
[ADR 0017](0017-code-generate-the-executor.md), [ADR 0020](0020-durability-is-opt-in-codegen.md), and
[ADR 0030](0030-immutable-content-hashed-versioned-packages.md). This records why the executor is generated and
compiled when a version is added to the catalog, and baked into the package.

## Context

A code-generated executor ([ADR 0017](0017-code-generate-the-executor.md)) has to be compiled at some point
before it runs. Compiling it on first run would put a compile on the run path and would need the toolchain
present wherever runs happen. Compiling it when the version is catalogued instead means the version is
runnable the moment it is published, and the runner needs only to load an assembly, not to generate and
compile one. For that to work, the package must be a complete, self-contained input to generation, so nothing
has to be fetched at compile time.

### Grounded architectural facts

- **The package is a complete generation input.** A package bundles the workflow and every source document it
  references ([ADR 0030](0030-immutable-content-hashed-versioned-packages.md)), so generation resolves every
  reference from within the package, in memory, with nothing external.
- **The executor is built and baked in at add.** `WorkflowExecutorProvider.BuildExecutor`
  (`src/Corvus.Text.Json.Arazzo.Generation/WorkflowExecutorProvider.cs`) generates the clients and executor in
  memory and compiles them into a single assembly (`DynamicCompiler.CompileToAssemblyBytes`);
  `CatalogPackage.Process` bakes the assembly and its manifest into the canonical package
  (`WorkflowPackage.ExecutorEntryName` plus the manifest, and an optional detached signature).
- **A non-compilable package is still catalogued.** A package that cannot be compiled is catalogued and marked
  not-runnable, rather than rejected, so the catalogue records it while the runtime declines to run it.
- **The assembly is integrity-bound to the version.** The manifest carries the assembly digest and the
  package's content hash ([ADR 0025](0025-integrity-binding-optional-signature.md),
  [ADR 0031](0031-content-hash-over-rfc8785-canonical.md)).

## Decision

A version's executor is **generated and compiled when the version is added to the catalog**, and the compiled
assembly plus its manifest are baked into the package. The package is a complete, self-contained input to
generation, so compiling resolves everything from within the package in memory. A version is runnable the
moment it is catalogued, and a runner loads the baked assembly rather than compiling one. A package that cannot
compile is catalogued as not-runnable.

## Consequences

- There is no compile on the run path, and no toolchain requirement on the runner. The runner loads an
  assembly ([ADR 0024](0024-collectible-assembly-per-version.md)).
- Compile errors surface at publish, not at first run, so a broken package is caught when it is added.
- The compiled assembly travels inside the immutable package, bound to the version by digest and content hash,
  so what the runner loads is exactly what was compiled for that version.
- Durability is chosen at this compile ([ADR 0020](0020-durability-is-opt-in-codegen.md)): the executor baked
  in is the durable or non-durable form as the deployment configured.
