# Arazzo control-plane REST API

[`arazzo-control-plane.openapi.json`](arazzo-control-plane.openapi.json) is an **OpenAPI 3.2** description of the
Arazzo durability control-plane REST surface, and it is the **source of truth**: the CLI and every client are
generated from it, and the server's handlers are generated from it too
([ADR 0039](../adr/0039-api-first-openapi-source-of-truth.md)). This page is the map, the operation groups, the
scope model, authentication, and the resume-mode union. For the exhaustive per-operation detail (paths, request
and response schemas, status codes), the contract and the generated `IApi*Handler` interfaces are authoritative,
so this page does not restate them and cannot drift from them.

## Operation groups

The surface is about 150 operations across **17 tag groups**. Most groups follow the same shape, a keyset-paged
`list` ([ADR 0035](../adr/0035-keyset-pagination-everywhere.md)) with a bounded `count`
([ADR 0036](../adr/0036-bounded-count-contract.md)), a `get`, and `create` / `update` / `delete`, so the table
below gives each group's job rather than its operations. Read the contract for the current operation set.

| Group (tag) | What it covers |
|-------------|----------------|
| `runs` | Workflow runs: list, get, run steps, `resume` (the mode union below), `cancel`, single delete, and a bulk `PURGE`. |
| `catalog` | Publish and read versioned, runnable workflow definitions: add, list, get, update, and delete versions; download the package and its addressable documents (workflow, sources, schemas, executor assembly and manifest); validate inputs; start a run. See [the catalog guide](../guides/catalog.md). |
| `runners` | Read the registered runners that host and execute catalog versions. |
| `runnerAuthorizations` | Authorize which runners may serve a deployment environment: register, authorize, revoke, quarantine. See [ADR 0027](../adr/0027-runner-environment-binding.md). |
| `schedules` | Durable schedules that trigger a workflow run on a cadence: list, create, get, delete, run-now. |
| `security` | Author the row-security policy, rules and claim-to-rule bindings, and read the access-grant overview. See [the identity and authorization guide](../guides/identity-and-authorization.md). |
| `credentials` | Manage source credential bindings: references and non-secret metadata only, never secret material. See [the source-credentials guide](../guides/source-credentials.md). |
| `administrators` | Manage who may publish and administer a base workflow: add, remove, transfer. |
| `environments` | Manage governed, reach-scoped deployment environments and their administrators. |
| `sources` | Manage first-class, reach-scoped source registrations, and read their operations. |
| `workspace` | Designer working copies: mutable Arazzo documents saved without minting a version, their scenarios, `publishWorkingCopy`, `simulateWorkingCopy`, and source binding. See [the workflow-designer guide](../guides/workflow-designer.md). |
| `debugRuns` | Debug a working copy: start, step and resume, inspect, inject a message to a suspended wait, cancel and delete. See [ADR 0045](../adr/0045-debug-runs-never-credentials-in-browser.md). |
| `github` | Brokered GitHub integration: the control plane holds the Git session, so pull and commit a working copy, list and create branches, browse a repo. |
| `availability` | Make a workflow version available in a deployment environment ("promotion"), directly where the caller administers the environment. |
| `availabilityRequests` | Request and approve making a version available in an environment (promotion requests). |
| `accessRequests` | Request and approve elevated, time-bound access to a workflow ([ADR 0010](../adr/0010-access-requests-ceiling-bounded.md)). |
| `identity` | Resolve real grantees (person, team, role, workflow) to their exact deployment-stamped identity, and read whoami and capabilities. See [ADR 0008](../adr/0008-resolved-grantee-resolution.md). |

## The scope model

The control plane is privileged, so its operations are **capability-scoped**
([ADR 0001](../adr/0001-two-plane-access-model.md): capability is which operations, reach is which rows). A scope
is `resource:verb`, and each is both an authorization-policy name the endpoint demands and a scope value the
principal must carry. The set is defined by `ControlPlaneScopes` in
`src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server/ControlPlaneAuthorization.cs`, which is the source of
truth; the resource families are:

- **`runs`**: `read`, `outputs:read` (the step-output disclosure tier, [ADR 0013](../adr/0013-step-output-disclosure-tier.md)), `write`, `purge`.
- **`catalog`**: `read`, `write`, `purge`.
- **`security`**, **`credentials`**, **`administrators`**, **`environments`**, **`sources`**, **`workspace`**, **`availability`**: `read` and `write`.
- **`runners:register`** (a runner registering itself) and **`accessRequests:grant`** (an approver granting a request).

Only the `runs` and `catalog` families have a `:purge` verb, because only they have a destructive bulk reap. The
`identity` operations and the self-service half of `accessRequests` are not scope-gated, they require an
authenticated principal with eligibility enforced per operation. Enforcement is fail-closed and non-disclosing
([ADR 0004](../adr/0004-fail-closed-non-disclosing-enforcement.md)); the wiring is in the
[authentication and authorization guide](../guides/auth-and-authorization.md).

## Resume modes

`resumeRun`'s body is a discriminated union (`ResumeRequest`, a JSON Schema 2020-12 `oneOf` whose `mode` `const`
selects the variant); the generated server `Match`es it onto the engine's `ResumeOptions`
([ADR 0022](../adr/0022-resume-mode-taxonomy.md)):

- **`RetryFaultedStep`**: re-run the faulted step (the common case).
- **`Rewind`**: reset the cursor to `targetCursor` and re-run forward, overwriting the re-executed steps' outputs.
- **`Skip`**: advance past the faulted step (to `targetCursor`, default faulted + 1), optionally recording `skipOutputs`.
- **`StatePatch`**: apply an RFC 6902 JSON Patch (`patch`) to the run context `{ "inputs": …, "stepOutputs": { … } }`, then retry.

There is deliberately no `Cancel` resume mode; cancellation is the separate `cancelRun` operation.

## Authentication

A request is satisfied by any one scheme:

- **OAuth2** (`oauth2`): bearer access tokens, authorization-code (PKCE or device-code) for interactive
  operators, client-credentials for services and the CLI.
- **OpenID Connect** (`openIdConnect`): the same tokens and scopes via IdP discovery.
- **Mutual TLS** (`mtls`): client certificates for zero-trust callers. Mutual TLS carries no scopes, so the host
  maps the certificate identity to a tier out of band.

The authorization server, discovery, and issuer are **deployment-chosen** (the URLs in the document are
placeholders). Transport is **HTTPS**; the `http` server variant is for local development only. The
authenticated principal is what the control plane records in each governed action's audit
([ADR 0038](../adr/0038-payload-safe-governance-audit.md)).

## Observability

The control plane and run lifecycle are observable through the `Corvus.Arazzo` OpenTelemetry sources, not a
separate history resource: governed actions emit a payload-safe audit span and log plus a governance-decision
counter ([ADR 0038](../adr/0038-payload-safe-governance-audit.md)), and each checkpoint emits a
`workflow.checkpoint` span and a duration measurement.

## OpenAPI 3.2 features used

- `openapi: 3.2.0` with a top-level `$self` document identity.
- `additionalOperations` to express the non-standard **`PURGE`** method on the `/runs` and `/catalog`
  collections (the bulk reaps).
- `info.summary` and per-tag `summary`.
- JSON Schema 2020-12 throughout (nullable via `type: [..., "null"]`, and `anyOf` with `{"type":"null"}` for a
  nullable `$ref`).

## Generating a client

The contract is validated the same way it becomes the CLI's basis, by generating a client from it with this
repo's OpenAPI generator:

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-client docs/arazzo/reference/arazzo-control-plane.openapi.json \
  --rootNamespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client \
  --outputPath src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli/Generated
```

The web kit's `conformance.test.mjs` also checks its Layer 0 client's emitted requests against this contract, so
a drift between the client and the contract fails the test.
