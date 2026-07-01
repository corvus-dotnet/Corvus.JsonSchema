# 035 — OpenAPI Callback Client: Sending Webhooks from Your Server

This recipe shows how to generate the **server-side sender** for OpenAPI webhooks and callbacks. The generated callback client lets your API send strongly-typed notifications to subscriber endpoints using the same OpenAPI description that defined the subscription contract.

> **Paired with [034-OpenApiCallbackServer](../034-OpenApiCallbackServer/):** That recipe is the *receiver*; this one is the *sender*. Together they demonstrate the full callback/webhook round-trip.

## What This Shows

| Feature | Where |
|---------|-------|
| Generate a typed callback client from `webhooks` and `callbacks` | `corvusjson openapi-callback-client` |
| Send a typed webhook body from server code | `ApiWebhooksClient.SystemAlertWebhookAsync(...)` |
| Send a per-subscription callback | `ApiCallbacksClient.OnEventCallbackAsync(...)` |
| Handle the subscriber's acknowledgement | `SystemAlertWebhookResponse.IsSuccess` |
| In-memory transport for self-contained demo | `DemoTransport` |

## Generate the Callback Client

Run the generator from this recipe directory:

```bash
corvusjson openapi-callback-client event-api.json \
    --rootNamespace EventSubscription.CallbackClient \
    --outputPath ./Generated
```

The callback-client generator reads only the OpenAPI `webhooks` section and per-operation `callbacks`. It does **not** generate a client for the main `/subscriptions` path.

For this spec, the generated output includes:

- `ApiWebhooksClient` / `IApiWebhooksClient` for the `systemAlert` webhook
- `ApiCallbacksClient` / `IApiCallbacksClient` for the `onEventCallback` callback operation
- typed request/response models under `Generated/Models/`

## Running the Example

```bash
dotnet run --project docs/ExampleRecipes/035-OpenApiCallbackClient
```

The example uses an in-memory `DemoTransport` (like [031-OpenApiAdvancedClient](../031-OpenApiAdvancedClient/)) so it runs self-contained with no external dependencies. Output:

```
Using in-memory demo transport. In production, replace with HttpClientTransport.

1. Sending systemAlert webhook...
   Subscriber acknowledged the webhook (HTTP 200).

2. Sending onEventCallback...
   Subscriber acknowledged the callback (HTTP 200).

Done!
```

## Production Usage

In production, replace `DemoTransport` with `HttpClientTransport` pointing at the subscriber's callback URL:

```csharp
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using EventSubscription.CallbackClient;
using EventSubscription.CallbackClient.Models;

HttpClient httpClient = new()
{
    BaseAddress = new Uri("https://subscriber.example.com/callbacks/")
};

await using HttpClientTransport transport = new(httpClient, disposeClient: true);
await using ApiWebhooksClient client = new(transport);

await using SystemAlertWebhookResponse response = await client.SystemAlertWebhookAsync(
    body: Schema.Build(
        alertId: "alert-001"u8,
        severity: "critical"u8,
        message: "Disk usage exceeded 90%."u8),
    validationMode: ValidationMode.Basic);
```

## Handling the Subscriber Response

The generated response type exposes the HTTP status code and success flag, so your server can decide whether to retry, dead-letter, or mark delivery as complete:

```csharp
if (response.IsSuccess)
{
    Console.WriteLine("Subscriber acknowledged the webhook.");
}
else
{
    Console.WriteLine($"Subscriber returned HTTP {response.StatusCode}.");
}
```

If you want to invoke the per-subscription callback operation instead of the top-level webhook, use the generated `ApiCallbacksClient` in the same way.
