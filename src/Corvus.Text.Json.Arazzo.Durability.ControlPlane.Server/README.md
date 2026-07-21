# Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server

An ASP.NET Core server for the [Arazzo control-plane REST API](../../docs/arazzo/reference/control-plane-rest-api.md), generated
from its OpenAPI 3.2 description and wired to `ISecuredWorkflowManagement`.

The generated endpoints (under `Generated/`, produced by `corvusjson openapi-server`) handle routing, parameter
and body deserialization, schema validation, and typed response serialization. A handler per resource group
(e.g. `ArazzoControlPlaneHandler` implements the generated `IApiRunsHandler`) delegates each operation to the
matching client and projects its result DTOs into the generated response models.

```csharp
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

WebApplication app = WebApplication.CreateBuilder(args).Build();

// management: an ISecuredWorkflowManagement over your chosen durability store (with a resumer for ResumeAsync).
// catalog:    an ISecuredWorkflowCatalog wrapping a catalog store + the run store (for referential integrity).
// runners:    an IRunnerRegistry the runners endpoint reads and the trigger gate consults.
// Optional args follow (authorization, row security, the security/credential/access-request stores, identity);
// see MapArazzoControlPlane's parameter docs.
app.MapArazzoControlPlane(management, catalog, runners);

app.Run();
```

This maps every resource group the OpenAPI description declares — runs, runners, catalog, security,
credentials, administrators, environments, sources, workspace (designer working copies), debugRuns
(§18 debug runs, including `inject-message`), github, availability, availability-requests,
runner-authorizations, access-requests, and identity — each onto its handler. The run operations
(`GET /runs`, `GET /runs/{runId}`, `POST /runs/{runId}/resume`, `POST /runs/{runId}/cancel`, `PURGE /runs`)
delegate to the management client; the catalog operations (`/catalog…`, including
`GET /catalog/{baseWorkflowId}/versions/{versionNumber}/schemas` and
`POST /catalog/{baseWorkflowId}/versions/{versionNumber}/validate`) delegate to the catalog client; the runners
endpoint reads the runner registry; and the remaining groups (security, credentials, administrators,
access-requests, identity) are backed by the optional stores described under `MapArazzoControlPlane`'s
parameters (an in-memory store by default so the endpoints function in development).

## Catalog schema metadata

`GET …/versions/{n}/schemas` serves the precomputed typed-shape metadata (each workflow's inputs and each
step's resolved output types) that UIs use to render strongly-typed forms (the typed patch/output builder, and
in future a workflow editor) without re-parsing the OpenAPI/AsyncAPI sources. That metadata is **baked into the
package at add time** by an `IWorkflowMetadataProvider`. To enable it, construct the catalog store with the
code-generation-backed provider — every backend's `ConnectAsync` accepts one (after the time provider):

```csharp
using Corvus.Text.Json.Arazzo;                  // IWorkflowMetadataProvider
using Corvus.Text.Json.Arazzo.CodeGeneration;   // WorkflowSchemaMetadataProvider

IWorkflowMetadataProvider metadata = new WorkflowSchemaMetadataProvider();

// e.g. Postgres — any backend's ConnectAsync takes the provider after the time provider:
var catalogStore = await PostgresWorkflowCatalogStore.ConnectAsync(
    connectionString, timeProvider: null, metadataProvider: metadata);

var catalog = new SecuredWorkflowCatalog(catalogStore, runStore, "ops");
app.MapArazzoControlPlane(management, catalog, runners);
```

Omit the provider and versions are stored without baked metadata — the `schemas` endpoint then returns `404`
and clients fall back to untyped editing. The provider pulls in `Corvus.Text.Json.Arazzo.CodeGeneration` (the
OpenAPI/AsyncAPI classifier), which runs only at add time.

## Schema validation (`POST …/versions/{n}/validate`)

`POST …/versions/{n}/validate` validates a JSON value against the **true JSON Schema** of a target within the
version's package — a workflow's `inputs`, a step's request or response body, or a step's `outputs` object —
returning `{ "valid": …, "errors": [ … ] }` (a malformed value still returns `200` with `valid: false`; an
unresolvable version or target returns `404`). Unlike the precomputed `schemas` metadata (which is a lossy,
render-oriented shape), this resolves the real schema from the package and runs the full
`Corvus.Text.Json.Validator`. The compiled schema is cached, keyed by the (immutable) version + target, so it is
bounded by distinct catalogued schemas rather than request volume.

> **Hosting requirement.** The validator compiles generated model types at runtime, which needs the **host
> application's** compilation context (preprocessor defines + reference assemblies) in its `deps.json`. Set
> `<PreserveCompilationContext>true</PreserveCompilationContext>` in the project that hosts this server,
> otherwise the first `validate` call throws `Unable to compile generated code`. (The other endpoints don't
> require this.)

## Security

The OpenAPI document declares scoped OAuth2/OIDC + mutual-TLS security, and the generator emits the
scheme/requirement metadata (`ApiEndpointRegistration.SecuritySchemes` / `SecurityRequirements`) plus a
`RequireDeclaredAuthorization` endpoint convention. The endpoints demand capability scopes per operation —
the full set (`ControlPlaneScopes.All`) is `catalog:read` / `catalog:write` / `catalog:purge`, `runs:read` /
`runs:write` / `runs:purge`, `security:read` / `security:write`, `credentials:read` / `credentials:write`,
and `administrators:read` / `administrators:write`. Enforcing them is the host's responsibility — register the
matching authentication (your IdP / client-certificate validation) and a policy per scope (the shipped
`AddArazzoControlPlaneAuthorization` registers defaults), then pass the **required** `ControlPlaneSecurityMode`
to `MapArazzoControlPlane` (design §17.4 — there is no insecure default):

- **`Open`** — unauthenticated, full reach; development / trusted-network only (logged loudly at startup). A
  row-security policy must **not** be supplied.
- **`Scoped`** — authentication + capability-scope gating + a **required** `ControlPlaneRowSecurityPolicy` for
  per-row reach (the production posture; you cannot get scopes without reach by omission).
- **`ScopesOnly`** — authentication + capability-scope gating, with full (System) reach; an explicit single-tenant
  choice. A policy must **not** be supplied.
- **`RowSecurityOnly`** — authentication + per-row reach with **no** capability-scope gating; a policy is
  **required**.

The mapping validates the mode/policy pairing at startup (a required policy omitted, or a policy supplied where it
would be ignored, throws `ArgumentException`), so an insecure-by-omission combination cannot be constructed.

## Regenerating

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-server docs/arazzo/reference/arazzo-control-plane.openapi.json \
  --rootNamespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server \
  --outputPath src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server/Generated
```
