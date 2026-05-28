# 034 — OpenAPI Callback Server: Receiving Webhooks

This recipe shows the **receiver side** of OpenAPI callbacks and webhooks: you generate ASP.NET Core minimal API endpoints that a remote service can call when it needs to notify your application.

> **Important:** This recipe is intentionally **not** added to `docs/ExampleRecipes/ExampleRecipes.slnx` yet. The checked-in `Generated/` folder is only a placeholder, so you must regenerate the callback server before this project will compile.

## What This Shows

| Feature | Where |
|---|---|
| Generating callback/webhook server stubs from OpenAPI 3.2 | `corvusjson openapi-callback-server` |
| Implementing generated handler interfaces | `Program.cs` |
| Registering generated minimal API endpoints | `app.MapApiEndpoints(...)` |
| Receiving per-operation callbacks and top-level webhooks in one app | `CallbackReceiver` |
| Keeping generated output separate from hand-written code | `Generated/` |

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
    webhooksHandler: handler);

app.Run();
```

`MapApiEndpoints()` comes from generated code. It registers one route per callback or webhook operation and dispatches the request to your handler implementation.

## Running the Recipe

After regenerating `Generated/`, you can run the sample with:

```bash
dotnet run --project docs/ExampleRecipes/034-OpenApiCallbackServer
```

Use this pattern when your application needs to **receive** asynchronous notifications such as subscription events, partner callbacks, or infrastructure alerts.
