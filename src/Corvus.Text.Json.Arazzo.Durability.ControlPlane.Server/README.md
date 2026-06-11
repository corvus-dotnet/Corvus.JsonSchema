# Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server

An ASP.NET Core server for the [Arazzo control-plane REST API](../../docs/control-plane/README.md), generated
from its OpenAPI 3.2 description and wired to `IWorkflowManagementClient`.

The generated endpoints (under `Generated/`, produced by `corvusjson openapi-server`) handle routing, parameter
and body deserialization, schema validation, and typed response serialization. `ArazzoControlPlaneHandler`
implements the generated `IApiRunsHandler` by delegating each operation to an `IWorkflowManagementClient` and
projecting its result DTOs into the generated response models.

```csharp
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

WebApplication app = WebApplication.CreateBuilder(args).Build();

// management: an IWorkflowManagementClient over your chosen durability store (with a resumer for ResumeAsync).
// catalog:    an IWorkflowCatalogClient wrapping a catalog store + the run store (for referential integrity).
app.MapArazzoControlPlane(management, catalog);

app.Run();
```

This maps the run operations (`GET /runs`, `GET /runs/{runId}`, `POST /runs/{runId}/resume`,
`POST /runs/{runId}/cancel`, `PURGE /runs`) onto the management client, and the catalog operations
(`/catalog…`, including `GET /catalog/{baseWorkflowId}/versions/{versionNumber}/schemas`) onto the catalog client.

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

var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");
app.MapArazzoControlPlane(management, catalog);
```

Omit the provider and versions are stored without baked metadata — the `schemas` endpoint then returns `404`
and clients fall back to untyped editing. The provider pulls in `Corvus.Text.Json.Arazzo.CodeGeneration` (the
OpenAPI/AsyncAPI classifier), which runs only at add time.

## Security

The OpenAPI document declares scoped OAuth2/OIDC + mutual-TLS security (`runs:read` / `runs:write` /
`runs:purge`), and the generator emits the scheme/requirement metadata
(`ApiEndpointRegistration.SecuritySchemes` / `SecurityRequirements`) plus a
`RequireDeclaredAuthorization` endpoint convention. Enforcing it is the host's responsibility — register the
matching authentication (your IdP / client-certificate validation) and authorization policies, then apply the
convention when mapping the endpoints. `MapArazzoControlPlane` maps the routes without imposing an auth scheme,
so a deployment chooses its own.

## Regenerating

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-server docs/control-plane/arazzo-control-plane.openapi.json \
  --rootNamespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server \
  --outputPath src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server/Generated
```
