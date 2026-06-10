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
app.MapArazzoControlPlane(management);

app.Run();
```

This maps `GET /runs`, `GET /runs/{runId}`, `POST /runs/{runId}/resume`, `POST /runs/{runId}/cancel`, and
`PURGE /runs` onto the management client.

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
