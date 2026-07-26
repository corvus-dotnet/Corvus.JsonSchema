# ADR 0057. The serverless workflow executor is compiled against reference assemblies

Date: 2026-07-26. Status: **Accepted**. Scope: how the workflow executor IL is compiled at catalog-add time so it can be native-AOT compiled in a serverless build container. Refines [ADR 0055](0055-serverless-backend-aot-from-signed-executor.md).

ADR 0055 established that the serverless backend native-AOT compiles a version's binary from the version's signed executor IL, not from re-generated source. The end-to-end container proof then exposed that the executor IL, as compiled, could not be recompiled in the build container at all. This record settles how the executor is compiled so it can be. It refines 0055 and does not change its decisions.

## Context

The executor IL is compiled at catalog-add time by `WorkflowExecutorProvider`, which compiles the generated executor source with `DynamicCompiler` and stores the resulting assembly in the version's package as `metadata/executor.dll`.

`DynamicCompiler` was built for a different job: the validator compiles JSON Schema types dynamically and loads them into the running process. For that job it supplements its compilation references with the AppDomain's loaded **implementation** assemblies (for example `System.Private.CoreLib`), which is correct, because an assembly that will only ever be loaded in this process is best compiled against the exact assemblies it will bind to, and this also avoids reference-assembly facade version mismatches.

An executor destined for a serverless build is different: it is a stored artifact that is recompiled elsewhere. The serverless build assembles a thin host-app that references the stored `executor.dll` and compiles that host-app in a container against the .NET **reference** assemblies. A reference-assembly compile cannot consume an assembly whose types resolve their base (`System.Object`) to `System.Private.CoreLib`, because the reference-assembly BCL surfaces those types through `System.Runtime`, not the implementation corlib. So the container compile failed with `CS0012` ("the type 'Object' is defined in an assembly that is not referenced … `System.Private.CoreLib`"). The executor compiled for in-process loading was not portable.

## Decision

**The stored workflow executor is compiled against reference assemblies (portable).** `DynamicCompiler` gains an opt-in portable compile mode that builds its references from the compilation context's reference libraries (the reference-assembly BCL, `System.Runtime` rather than `System.Private.CoreLib`, plus the package and project references) and supplements only with output-directory project assemblies, never the AppDomain's loaded implementation assemblies. `WorkflowExecutorProvider` compiles the executor in this mode.

A portable executor references `System.Runtime`, so the serverless host-app compiles against it in the container, and it still loads in-process without change, because reference assemblies forward to the implementation at load. So one stored executor serves both backends: the in-process collectible-load-context backend and the serverless native-AOT backend. The validator's in-process dynamic compilation keeps the implementation-assembly references it needs, since the portable mode is opt-in and off by default.

The host that compiles the executor must preserve its compilation context (`PreserveCompilationContext`), so the reference libraries are resolvable. That is already required of a control-plane host for the `validate` endpoint, so it is not a new obligation.

This ADR is **Accepted**. The container proof now builds a real, signed native binary from a runtime-generated executor end to end.

## Consequences

- The stored executor is a portable artifact that both backends use unchanged. The serverless build compiles against it; the in-process backend loads it as before.
- A general rule this records: a build-time-generated assembly that is meant to be recompiled downstream must be compiled against reference assemblies, not the loaded implementation assemblies. An assembly compiled against implementation assemblies is loadable in-process but not recompilable elsewhere.
- The control-plane host that compiles executors must preserve its compilation context. This was already required for the `validate` endpoint.
- The change is confined to how the executor is compiled. The signing chain, the package layout, and the build and deploy flow of 0055 are unchanged: the portable executor is still signed, stored as `metadata/executor.dll`, verified before the build, and native-AOT compiled from that signed IL.
