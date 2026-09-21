# ADR 0039. API-first: the OpenAPI surface is the source of truth

Date: 2026-07-21. Status: **Accepted**. Implementation: **partly, with divergences**. Verified against the code 2026-09-21. Built: the control plane's OpenAPI document drives the generated routing, parsing and validation, the handlers implement the generated interfaces, the CLI client is generated, and `openapi-client` and `openapi-server` exist as commands. Divergences: the run-scoped checkpoint surface (`GET` and `POST` on `/environments/{environment}/runs/{runId}/checkpoint`) is mapped by hand inside `MapArazzoControlPlane` and is absent from the contract, so "the published contract and the code cannot disagree" is false for it, and the runner contract describes a generated surface for the same concept; the web client `arazzo-client.js` is written by hand and held to the contract by a test and not by generation; and nothing regenerates `Generated/` and diffs it in CI. Scope: how the control-plane API and its clients are built and kept in
sync. This records why the OpenAPI document is the single source of truth, regenerated before the
implementation, rather than hand-written and documented after.

## Context

The control plane has a REST surface, a server that implements it, a client, a CLI, and a web UI. If the API
shape lived in the server code, the client, CLI, and UI would each need to be kept in sync by hand, and the
published contract could drift from what the server actually does. The shape should live in one place that
everything else is generated from, so the contract and the code cannot disagree.

### Grounded architectural facts

- **The OpenAPI document is the source of truth.** `docs/arazzo/reference/arazzo-control-plane.openapi.json` is
  the contract. The server stubs, the client, and the CLI client are generated from it.
- **Generation is a build step with dedicated commands.** `openapi-server` (`OpenApiServerCommand`) generates
  the server stubs and `openapi-client` (`OpenApiGenerateCommand`) generates the client, registered in the
  CLI (`src/Corvus.Json.Cli.Core/CliAppFactory.cs`). The generated server handlers route, parse, and validate;
  the hand-written handlers implement the generated `IApi*Handler` interfaces.
- **The flow is author, regenerate, then implement.** A change starts in the OpenAPI document. The `Generated/`
  code is regenerated, and only then are the store, handler, CLI, and UI implemented against the new shape. A
  regeneration that produces a drifting diff means the generator version has skewed, and it is reconciled
  before commit.

## Decision

The control plane is **API-first**: the OpenAPI document is the single source of truth, and the server stubs,
client, and CLI are generated from it. A change is authored in the OpenAPI first, the `Generated/` code is
regenerated, and the store, handler, CLI, and UI are implemented against the regenerated shape. The published
contract and the code cannot disagree, because the code that routes, parses, and validates is generated from
the contract.

## Consequences

- The contract and the server cannot drift, because the server's routing, parsing, and validation are
  generated from the contract, and a hand-written handler only fills in the generated interface.
- The client, CLI, and UI all derive from one shape, so a change to the surface propagates by regeneration
  rather than by hand-editing each consumer.
- The discipline is a real constraint: an API change must go through the OpenAPI and a regeneration, never a
  direct edit of generated code, and a drifting regen diff is a signal to reconcile, not to hand-fix.
- Generated types carry their own serialisation, which is what lets the handlers stay bytes-native
  ([ADR 0037](0037-bytes-native-seams.md)) instead of hand-rolling records.
