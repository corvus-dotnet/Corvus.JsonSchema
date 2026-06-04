# 034 — OpenAPI Callback Server: Receiving Webhooks

This recipe shows the **receiver side** of OpenAPI callbacks and webhooks: you generate ASP.NET Core minimal API endpoints that a remote service can call when it needs to notify your application.

## What This Shows

| Feature | Where |
|---|---|
| Generating callback/webhook server stubs from OpenAPI 3.2 | `corvusjson openapi-callback-server` |
| Implementing generated handler interfaces | `Program.cs` |
| Registering generated minimal API endpoints | `app.MapApiEndpoints(...)` |
| Receiving per-operation callbacks and top-level webhooks in one app | `CallbackReceiver` |
| Runtime expression route parameters | `OnEventCallbackRoute: "/callbacks/onEvent"` |
| Customizing each endpoint via the `configureEndpoint` hook | `app.MapApiEndpoints(..., configureEndpoint: ...)` |

## Generating the Callback Server

Run the generator from this directory:

```bash
corvusjson openapi-callback-server event-api.json \
    --rootNamespace EventSubscription.CallbackServer \
    --outputPath ./Generated
```

Expected generated artifacts include:

- `Generated/IApiCallbacksHandler.cs`
- `Generated/IApiWebhooksHandler.cs`
- `Generated/ApiEndpointRegistration.cs`
- `Generated/OnEventCallbackParams.cs`
- `Generated/OnEventCallbackResult.cs`
- `Generated/SystemAlertWebhookParams.cs`
- `Generated/SystemAlertWebhookResult.cs`
- `Generated/Models/` for schema-backed payload types

## How to Implement the Handler

After generation, implement the handler interfaces produced from the callback and webhook tags:

```csharp
internal sealed class CallbackReceiver : IApiCallbacksHandler, IApiWebhooksHandler
{
    public ValueTask<OnEventCallbackResult> HandleOnEventCallbackAsync(
        OnEventCallbackParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(OnEventCallbackResult.Ok());
    }

    public ValueTask<SystemAlertWebhookResult> HandleSystemAlertWebhookAsync(
        SystemAlertWebhookParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(SystemAlertWebhookResult.Ok());
    }
}
```

The generated middleware handles JSON parsing and schema validation before your method runs, so `parameters.Body` is already a strongly-typed model from the OpenAPI document.

## How to Wire Up Endpoints

Once the generated files exist, map them into ASP.NET Core minimal APIs:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

CallbackReceiver handler = new(app.Logger);
app.MapApiEndpoints(
    callbacksHandler: handler,
    webhooksHandler: handler,
    OnEventCallbackRoute: "/callbacks/onEvent");

app.Run();
```

`MapApiEndpoints()` comes from generated code. It registers one route per callback or webhook operation and dispatches the request to your handler implementation.

The `OnEventCallbackRoute` parameter is required because the OpenAPI spec defines this callback path as a runtime expression (`{$request.body#/callbackUrl}`). The actual URL is determined dynamically by the client at subscription time, so you — the server implementer — choose what route to listen on.

## Customizing the Generated Endpoints

`MapApiEndpoints` has a second overload that accepts a `configureEndpoint` callback. It runs once per generated endpoint, after the route is mapped, with a descriptor for the operation and the route's `IEndpointConventionBuilder` — the standard ASP.NET extension point for naming, tags, authorization, output caching, rate limiting, and more. The bare overload is preserved, so adopting the hook is source- and binary-compatible.

```csharp
app.MapApiEndpoints(
    callbacksHandler: handler,
    webhooksHandler: handler,
    OnEventCallbackRoute: "/callbacks/onEvent",
    configureEndpoint: static (in EndpointDescriptor endpoint, IEndpointConventionBuilder builder) =>
    {
        builder.WithName(endpoint.OperationId ?? endpoint.MethodName);
        builder.WithTags([.. endpoint.Tags]);

        // IsCallback is true for every webhook/callback operation.
        if (endpoint.IsCallback)
        {
            builder.WithMetadata(new EndpointGroupNameAttribute("event-deliveries"));
        }

        // For a spec that declares security, translate each requirement into the
        // authorization policies your app registered. event-api.json declares no
        // security, so this list is empty and the endpoints stay anonymous.
        foreach (EndpointSecurityRequirement requirement in endpoint.SecurityRequirements)
        {
            builder.RequireAuthorization($"{requirement.SchemeName}:{string.Join('+', requirement.Scopes)}");
        }
    });
```

`EndpointDescriptor` carries the `OperationId`, `MethodName`, `HttpMethod`, `RouteTemplate`, `Tags`, `IsCallback`, and `SecurityRequirements` for the operation. See the "Customizing Generated Endpoints" section of the main OpenAPI guide (`docs/OpenApi.md`) for the full descriptor reference and a spec that wires `RequireAuthorization()` from declared security.

## Running the Recipe

```bash
dotnet run --project docs/ExampleRecipes/034-OpenApiCallbackServer
```

### Registered endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/systemAlert` | Receives webhook deliveries for the `systemAlert` webhook |
| POST | `/callbacks/onEvent` | Receives callback deliveries for the `onEvent` callback |

### Testing with curl

Send a webhook (system alert):

```bash
curl -X POST http://localhost:64910/systemAlert -H "Content-Type: application/json" -d '{"alertId":"alert-001","severity":"critical","message":"Disk usage exceeded 90%."}'
```

Send a callback (event notification):

```bash
curl -X POST http://localhost:64910/callbacks/onEvent -H "Content-Type: application/json" -d '{"eventId":"evt-123","eventType":"subscription.created","timestamp":"2026-01-15T10:30:00Z"}'
```

Both should return HTTP 200.

## Key Concepts

- **Webhooks** have static paths — the route is known at build time from the webhook name (e.g. `systemAlert`).
- **Callbacks** with runtime expressions (e.g. `{$request.body#/callbackUrl}`) have dynamic paths — you provide the route at registration time since only you know what URL your clients will use.
- The generator produces strongly-typed `Params` and `Result` structs so your handler receives validated data and returns spec-compliant responses.
