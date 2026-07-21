# Arazzo Workflows

## Overview

`Corvus.Text.Json.Arazzo` compiles an [OAI Arazzo](https://spec.openapis.org/arazzo/latest.html) workflow
document into a strongly-typed .NET executor and runs it over your OpenAPI and AsyncAPI sources. A workflow is a
sequence of steps that call operations, pass data between them, branch on criteria, and (optionally) send and
receive messages on a channel. Corvus generates the executor ahead of time rather than interpreting the document
at run time, so a workflow runs as plain typed code with no reflection and no per-step allocation on the happy
path.

Two layers ship together, and the engine runs without the control plane.

| Layer | What it is |
|-------|-----------|
| **The engine** | The code generator and runtime that turn an `.arazzo.json` document into a typed executor (its inputs model and the filtered OpenAPI or AsyncAPI clients it calls), plus the durability seam that lets a run checkpoint and resume. |
| **The control plane** | An optional governed HTTP surface that catalogs immutable workflow versions, runs them, promotes them across environments, and administers who may do what, with row-level security over a canonical identity. |

The deep documentation set lives under [`docs/arazzo/`](arazzo/README.md): Architecture Decision Records for the
*why*, implementation guides for the *how*, and reference material (the REST contract, the glossary, and the
use-case and observability catalogs).

## The pipeline

Authoring, generating, and running are three stages. This is the essence; the
[authoring, generating, and running guide](arazzo/guides/authoring-generating-running.md) walks each in full.

**Author** an Arazzo `1.1.0` document (JSON or YAML). It names the sources it orchestrates and one or more
workflows, each with typed `inputs` and an ordered list of steps.

```json
{
  "arazzo": "1.1.0",
  "info": { "title": "Onboard customer", "version": "1.0.0" },
  "sourceDescriptions": [
    { "name": "onboarding", "url": "./onboarding.openapi.json", "type": "openapi" }
  ],
  "workflows": [
    {
      "workflowId": "onboard-customer",
      "inputs": { "type": "object", "required": ["email"], "properties": { "email": { "type": "string" } } },
      "steps": [
        {
          "stepId": "createAccount",
          "operationId": "createAccount",
          "requestBody": { "payload": { "email": "$inputs.email" } },
          "outputs": { "accountId": "$response.body#/accountId" }
        }
      ],
      "outputs": { "accountId": "$steps.createAccount.outputs.accountId" }
    }
  ]
}
```

**Generate** a typed executor from it. The generator emits only the operations the workflow references and the
schema models those operations reach, and it records a lock file so a re-run regenerates only when something
changed.

```
corvusjson arazzo-generate ./onboard-customer.arazzo.json --rootNamespace MyApp.Workflows --outputPath ./Generated
```

**Run** the generated executor through its non-generic host adapter. The call takes the API transports (keyed by
source-description name), an optional message transport, a caller-owned `JsonWorkspace` for the built outputs, the
inputs as a `JsonElement`, and a durability seam. The result is tri-state (`Completed`, `Faulted`, or
`Suspended`).

```csharp
WorkflowRunResultKind result = await workflow.RunAsync(
    apiTransports,     // IReadOnlyDictionary<string, IApiTransport>
    messageTransport,  // IMessageTransport? (null when no messaging is used)
    workspace,         // JsonWorkspace (caller-owned)
    inputs,            // JsonElement
    run,               // IWorkflowRun (a no-op run for the in-process case)
    cancellationToken);
```

Durability is opt-in at generation. A durable executor threads an `IWorkflowRun` and checkpoints after each step,
so a crash resumes from where it stopped. The resumable state is the run's step outputs plus a cursor. The
products the executor already built are the checkpoint.

## The control plane

For a governed deployment, the control plane wraps the engine in an HTTP surface. It catalogs immutable,
content-hashed workflow versions, compiles each to a runnable executor at catalog-add, runs them (in-process or on
a separate runner that claims durable runs through the store), promotes versions across environments through an
approval flow, and enforces a two-plane access model (capability scopes for what an action is, row-level reach for
which rows it touches). See the [catalog](arazzo/guides/catalog.md),
[running a runner](arazzo/guides/running-a-runner.md),
[authorization and identity](arazzo/guides/identity-and-authorization.md), and
[REST API reference](arazzo/reference/control-plane-rest-api.md) for the detail.

## Documentation map

| Start here | For |
|------------|-----|
| [`docs/arazzo/README.md`](arazzo/README.md) | The map of the whole documentation set. |
| [Authoring, generating, and running](arazzo/guides/authoring-generating-running.md) | The end-to-end engine pipeline. |
| [Durability and state stores](arazzo/guides/durability-and-state-stores.md) | Checkpoints, resume, and writing a state-store backend. |
| [The catalog](arazzo/guides/catalog.md) | Packaging, publishing, and promoting versions. |
| [Authorization and identity](arazzo/guides/identity-and-authorization.md) | The two-plane access model, row security, and the entitlement lifecycle. |
| [Platform conventions](arazzo/guides/platform-conventions.md) | The high-performance JSON, pagination, and api-first codegen conventions the control plane follows. |
| [ADR index](arazzo/adr/README.md) | Every design decision and its rationale. |

## Samples

The runnable samples under `samples/arazzo/` are complete Arazzo documents and hosts, including retry, async
message, multi-source, and durable workflows, plus a control-plane demo. Their `specs/` folders are the best place
to see full documents that exercise every building block.
