# Corvus.Text.Json.Arazzo

Runtime support for executing [Arazzo](https://github.com/OAI/Arazzo-Specification) workflows.

This package provides the cross-cutting, genuinely-dynamic concerns of workflow execution
that are shared by the generated executors:

- runtime-expression evaluation (`$inputs`, `$steps.*.outputs.*`, `$response.*`, `{…}` interpolation)
- criterion evaluation (simple / regex / jsonpath)
- the workflow execution context and structured execution trace
- control-flow signals (`end` / `goto` / `retry`)
- OpenTelemetry instrumentation (`ActivitySource` / `Meter`)
- `TimeProvider` / seeded-id injection for deterministic, mockable execution

> Phase 0 scaffold. The runtime API is implemented in Phase 1 — see
> `docs/ArazzoWorkflowEnginePlan.md`.

## Related Packages

- `Corvus.Text.Json.Arazzo10` — Arazzo 1.0 document model types
- `Corvus.Text.Json.Arazzo.CodeGeneration` — workflow executor code generator
- `Corvus.Text.Json.Arazzo.Testing` — mock transport and conformance harness
