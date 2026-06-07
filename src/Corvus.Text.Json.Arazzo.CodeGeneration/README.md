# Corvus.Text.Json.Arazzo.CodeGeneration

Code generator that emits strongly-typed workflow executors from
[Arazzo](https://github.com/OAI/Arazzo-Specification) documents.

The generator bridges Arazzo's dynamic `operationId`/`operationPath` references to the
statically-typed request/response structs produced by the Corvus OpenAPI and AsyncAPI
generators, emitting one executor class per workflow. It computes the exact set of
referenced operations and drives the OpenAPI/AsyncAPI `OperationFilter` so only the
operations the workflow uses (and the schema models they reach) are generated.

> Phase 0 scaffold. Generation is implemented from Phase 2 onwards — see
> `docs/ArazzoWorkflowEnginePlan.md`.

## Related Packages

- `Corvus.Text.Json.Arazzo` — workflow execution runtime
- `Corvus.Text.Json.Arazzo10` — Arazzo 1.0 document model types
