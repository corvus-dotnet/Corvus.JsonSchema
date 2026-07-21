# Generating a client and using the CLI

The [OpenAPI contract](../reference/arazzo-control-plane.openapi.json) is the source of truth for the
control-plane REST surface ([ADR 0039](../adr/0039-api-first-openapi-source-of-truth.md)): the server handlers,
every client, and the `arazzo-runs` CLI are all generated from it. This guide covers generating a typed client
and using the CLI. For the surface itself (operation groups, the scope model, resume modes), see the
[REST API reference](../reference/control-plane-rest-api.md).

## Generating a typed client

Run this repo's OpenAPI generator over the contract to emit a strongly-typed client:

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-client docs/arazzo/reference/arazzo-control-plane.openapi.json \
  --rootNamespace MyApp.Arazzo.Client \
  --outputPath src/MyApp/Generated
```

This emits an `IApi<Group>Client` and `Api<Group>Client` per operation group (for example `IApiRunsClient` with
`listRuns` / `getRun` / `resumeRun` / `cancelRun` / `purgeRuns`) plus the strongly-typed CTJ models
(`WorkflowRunDetail`, `WorkflowRunPage`, `WorkflowFault`, and the rest). Because the client is generated from the
contract, it cannot drift from it, and the web kit's `conformance.test.mjs` checks its Layer-0 client's emitted
requests against the same contract, so a divergence fails the test.

## The CLI

`arazzo-runs` is the CLI (`src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli`, a Spectre.Console.Cli
`CommandApp`). It drives the control plane over HTTP through the same generated client, with subcommands for the
operation groups (runs, catalog, credentials, administrators, access requests, schedules, and the designer's
scenarios and debug runs).

Authentication is OIDC for a native app: `arazzo-runs login` runs Authorization Code with PKCE on a loopback
redirect (opening the system browser), or `--use-device-code` for a headless or over-SSH session; the token is
cached and silently refreshed, so subsequent commands are non-interactive. `--server`, `--authority`, and
`--client-id` (or the `ARAZZO_RUNS_*` environment variables) select the control plane and the identity provider.

## See also

- The [REST API reference](../reference/control-plane-rest-api.md) for the operation groups, the scope model,
  authentication, and the resume modes.
- The [authoring, generating, and running guide](authoring-generating-running.md) for generating a workflow's
  executor from its package.
