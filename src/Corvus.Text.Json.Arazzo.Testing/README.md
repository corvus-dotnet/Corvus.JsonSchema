# Corvus.Text.Json.Arazzo.Testing

Testing support for [Arazzo](https://github.com/OAI/Arazzo-Specification) workflows.

- a **mock transport** with response scripting — exercise a workflow against canned responses
  with zero real endpoints
- a **workflow simulator** for deterministic dry-runs (outcome + call path), suitable for unit
  tests and for a workflow-designer UI
- an **in-memory OpenTelemetry conformance harness** — assert the emitted span tree (shape,
  correlation, attributes) alongside functional outcomes

> Phase 0 scaffold. Implemented in Phase 1 — see `docs/ArazzoWorkflowEnginePlan.md` §3.2/§3.3.

## Related Packages

- `Corvus.Text.Json.Arazzo` — workflow execution runtime
- `Corvus.Text.Json.Arazzo10` — Arazzo 1.0 document model types
