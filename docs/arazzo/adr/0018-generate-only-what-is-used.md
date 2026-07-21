# ADR 0018. Generate only the operations a workflow uses

Date: 2026-07-21. Status: **Accepted**. Scope: how much client and model code the generator emits for a
workflow's source descriptions. Builds on [ADR 0017](0017-code-generate-the-executor.md). This records why the
generator emits only the operations a workflow actually references, and the schema models those operations
reach, rather than a full client for each source.

## Context

A workflow's source descriptions can be large. A referenced OpenAPI document may describe hundreds of
operations and thousands of schema models, while the workflow calls a handful. Generating a full client for
each source would produce a large amount of code the workflow never uses, slowing compilation and bloating the
executor package for no benefit.

An Arazzo document is precise about what it uses. Every step names an `operationId` or `operationPath` (or a
`channelPath` for an AsyncAPI source), so the exact set of referenced operations is knowable up front.

### Grounded architectural facts

- **The referenced set is collected up front.** `ArazzoReferences`
  (`src/Corvus.Text.Json.Arazzo.CodeGeneration/ArazzoReferences.cs`) holds the distinct referenced
  `operationId`s, `operationPath`s, and sub-workflow `workflowId`s, in first-seen order, computed by walking
  every workflow and step.
- **That set drives the existing operation filter.** The referenced set is passed to the OpenAPI and AsyncAPI
  generators' `OperationFilter` (`src/Corvus.Text.Json.OpenApi.CodeGeneration/OperationFilter.cs`, and the
  AsyncAPI equivalent), so a client emits only the referenced operations' request and response structs, and
  transitively only the schema models those operations reach.
- **Arazzo-to-Arazzo sources recurse.** A source that is itself an Arazzo document is recursed into and its
  referenced operation set unioned before filtering, so a sub-workflow's sources are filtered the same way.

## Decision

The generator emits **only the operations a workflow references**, and the schema models those operations
reach. It walks the workflows to collect the referenced set (`ArazzoReferences`) and drives the OpenAPI and
AsyncAPI `OperationFilter` with it, rather than generating a full client per source description. An
Arazzo-to-Arazzo source is recursed into and its references unioned first.

## Consequences

- The generated executor package carries only the client and model code the workflow needs, so it compiles
  faster and is smaller.
- A source document can be arbitrarily large without inflating the workflow that uses a few of its operations.
- The reachable-model filtering is transitive, so a referenced operation pulls in exactly the schema models it
  uses and no more, which keeps the emitted model set minimal.
- The filter is the OpenAPI and AsyncAPI generators' own, so Arazzo generation reuses the same filtering the
  standalone client generators use rather than a bespoke one.
