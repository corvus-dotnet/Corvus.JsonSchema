# Arazzo Control Plane — REST API

[`arazzo-control-plane.openapi.json`](arazzo-control-plane.openapi.json) is an **OpenAPI 3.2** description of a
REST surface over the Arazzo durability control plane (`IWorkflowManagementClient`, plan §11). It is the
contract a CLI (`arazzo-runs`) and other clients are generated against; a server implementation maps each
operation onto the corresponding `IWorkflowManagementClient` method over a chosen durability store.

## Operations

| HTTP | operationId | `IWorkflowManagementClient` | Notes |
|------|-------------|-----------------------------|-------|
| `GET /runs` | `listRuns` | `ListAsync` | Visibility query (`status`, `workflowId`, `limit`, `pageToken`) over the wait/visibility index. Keyset pagination: a non-empty `nextPageToken` (and a `next` link) is returned when more pages remain. |
| `GET /runs/{runId}` | `getRun` | `GetAsync` | Status, cursor, wait/fault detail, etag. `404` if unknown. |
| `DELETE /runs/{runId}` | `deleteRun` | `DeleteAsync` | Permanently delete one run by id (any status). `204` on success, `404` if unknown, `409` if held. Destructive — `runs:purge` scope. |
| `POST /runs/{runId}/resume` | `resumeRun` | `ResumeAsync` | Resume a faulted run. The request body is a `oneOf` union on `mode`: `RetryFaultedStep`, `Rewind` (`targetCursor`), `Skip` (`targetCursor?`, `skipOutputs?`), `StatePatch` (`patch`). `409` if not faulted / held / changed concurrently / patch failed. |
| `POST /runs/{runId}/cancel` | `cancelRun` | `CancelAsync` | Mark a non-terminal run `Cancelled`. `409` if already terminal / held. |
| `PURGE /runs` | `purgeRuns` | `PurgeAsync` | Reap completed/cancelled runs older than `olderThan` (in bulk). |

Successful `resume`/`cancel` return the run's new `WorkflowRunDetail`. Errors use `application/problem+json`
(RFC 9457).

### Resume modes

`resumeRun`'s body is a discriminated union (`ResumeRequest`, a JSON Schema 2020-12 `oneOf` whose `mode`
`const` selects the variant); the generated server `Match`es it onto the engine's `ResumeOptions`:

- **`RetryFaultedStep`** — re-run the faulted step (the common case).
- **`Rewind`** — reset the cursor to `targetCursor` and re-run forward, overwriting the re-executed steps' outputs.
- **`Skip`** — advance past the faulted step (to `targetCursor`, default faulted + 1), optionally recording `skipOutputs` for it.
- **`StatePatch`** — apply an RFC 6902 JSON Patch (`patch`) to the run context `{ "inputs": …, "stepOutputs": { … } }`, then retry.

### Observability

The control plane and run lifecycle are observable through the `Corvus.Arazzo` OpenTelemetry sources (no
separate history/trace resource): management actions emit audit spans (`workflow.resume` / `cancel` / `delete`
/ `purge`, tagged with actor, run/workflow id, resume mode and outcome) and counters
(`corvus.arazzo.workflows.{resumed,cancelled,suspended,purged,deleted}`); each checkpoint emits a
`workflow.checkpoint` span and a `corvus.arazzo.checkpoint.duration` measurement.

## Security model

The control plane is privileged, and its operations fall into capability tiers, so authorization is **scoped**:

| Scope | Grants | Operations |
|-------|--------|------------|
| `runs:read` | Visibility | `listRuns`, `getRun` |
| `runs:write` | Remediation | `resumeRun`, `cancelRun` |
| `runs:purge` | Destructive (permanent delete) | `purgeRuns`, `deleteRun` |

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

This emits an `IApiRunsClient`/`ApiRunsClient` with
`listRuns`/`getRun`/`deleteRun`/`resumeRun`/`cancelRun`/`purgeRuns` methods and strongly-typed models
(`WorkflowRunDetail`, `WorkflowRunPage`, `WorkflowFault`, …).

## Scope

This describes the implemented control-plane surface: visibility (list/get, paged), the full set of resume
modes (`RetryFaultedStep`/`Rewind`/`Skip`/`StatePatch`), cancel, single-run delete, and bulk purge, with
audit observability via OpenTelemetry. The run history/trace is delivered through the `Corvus.Arazzo` telemetry
sources rather than a dedicated REST resource. Reserved for later iterations (see plan §11): a run
history/trace *resource* (should one be wanted beyond telemetry), and the hosting service that runs this
contract in production.
