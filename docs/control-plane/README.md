# Arazzo Control Plane — REST API

[`arazzo-control-plane.openapi.json`](arazzo-control-plane.openapi.json) is an **OpenAPI 3.2** description of a
REST surface over the Arazzo durability control plane (`IWorkflowManagementClient`, plan §11). It is the
contract a CLI (`arazzo-runs`) and other clients are generated against; a server implementation maps each
operation onto the corresponding `IWorkflowManagementClient` method over a chosen durability store.

## Operations

| HTTP | operationId | `IWorkflowManagementClient` | Notes |
|------|-------------|-----------------------------|-------|
| `GET /runs` | `listRuns` | `ListAsync` | Visibility query (`status`, `workflowId`, `limit`, `pageToken`) over the wait/visibility index. |
| `GET /runs/{runId}` | `getRun` | `GetAsync` | Status, cursor, wait/fault detail, etag. `404` if unknown. |
| `POST /runs/{runId}/resume` | `resumeRun` | `ResumeAsync` | Retry a faulted run from its last checkpoint. `409` if not faulted / held / changed concurrently. |
| `POST /runs/{runId}/cancel` | `cancelRun` | `CancelAsync` | Mark a non-terminal run `Cancelled`. `409` if already terminal / held. |
| `PURGE /runs` | `purgeRuns` | `PurgeAsync` | Reap completed/cancelled runs older than `olderThan`. |

Successful `resume`/`cancel` return the run's new `WorkflowRunDetail`. Errors use `application/problem+json`
(RFC 9457).

## Security model

The control plane is privileged, and its operations fall into capability tiers, so authorization is **scoped**:

| Scope | Grants | Operations |
|-------|--------|------------|
| `runs:read` | Visibility | `listRuns`, `getRun` |
| `runs:write` | Remediation | `resumeRun`, `cancelRun` |
| `runs:purge` | Destructive (permanent delete) | `purgeRuns` |

Authentication is one of (any satisfies a request):

- **OAuth2** (`oauth2`) — bearer access tokens; authorization-code (PKCE/device-code) for interactive operators,
  client-credentials for services and the CLI. Scopes as above.
- **OpenID Connect** (`openIdConnect`) — the same tokens/scopes via IdP discovery.
- **Mutual TLS** (`mtls`) — client certificates for zero-trust callers. Mutual TLS carries no scopes, so the
  host maps the certificate identity to a tier out of band.

The authorization server / discovery / issuer is **deployment-chosen** (the URLs in the document are
placeholders). Transport is **HTTPS** (the `http` server variant is for local development only). The
authenticated principal is what the control plane records in a run's audit history for each action.

## OpenAPI 3.2 features used

- `openapi: 3.2.0` with a top-level `$self` document identity.
- `additionalOperations` to express the non-standard **`PURGE`** method on the `/runs` collection (the bulk reap).
- `info.summary` and per-tag `summary`.
- JSON Schema 2020-12 throughout (nullable via `type: [..., "null"]`, `anyOf` with `{"type":"null"}` for nullable `$ref`s).

## Validation

The document is validated by generating a client from it with this repo's OpenAPI generator — which is also how
it becomes the CLI's basis:

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-client docs/control-plane/arazzo-control-plane.openapi.json \
  --rootNamespace Corvus.Arazzo.ControlPlane.Client --outputPath ./Generated
```

This emits an `IApiRunsClient`/`ApiRunsClient` with `listRuns`/`getRun`/`resumeRun`/`cancelRun`/`purgeRuns`
methods and strongly-typed models (`WorkflowRunDetail`, `WorkflowRunPage`, `WorkflowFault`, …).

## Scope

This describes the implemented control-plane surface. Reserved for later iterations (see plan §11): richer
resume modes (`Rewind`/`Skip`/`StatePatch`), real pagination tokens, single-run delete, a run history/trace
resource, and the hosting service + `arazzo-runs` CLI that consume this contract.
