# 035 — OpenAPI Callback Client: Sending Webhooks from Your Server

This recipe shows how to generate the **server-side sender** for OpenAPI webhooks and callbacks. The generated callback client lets your API send strongly-typed notifications to subscriber endpoints using the same OpenAPI description that defined the subscription contract.

## What This Shows

| Feature | Where |
|---------|-------|
| Generate a typed callback client from `webhooks` and `callbacks` | `corvusjson openapi-callback-client` |
| Point the transport at a subscriber callback endpoint | `HttpClientTransport` |
| Send a typed webhook body from server code | `ApiWebhooksClient.SystemAlertWebhookAsync(...)` |
| Handle the subscriber's acknowledgement | `SystemAlertWebhookResponse` |

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

## Sending a Notification to a Subscriber

A server typically stores the subscriber's callback base URL when the subscription is created. Later, it creates a transport for that subscriber and uses the generated client to send the notification:

```csharp
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using EventSubscription.CallbackClient;

HttpClient httpClient = new()
{
    BaseAddress = new Uri("https://subscriber.example.com/callbacks/")
};

await using HttpClientTransport transport = new(httpClient, disposeClient: true);
await using ApiWebhooksClient client = new(transport);

await using SystemAlertWebhookResponse response = await client.SystemAlertWebhookAsync(
    body: new PostSystemAlertBody.Source(static (ref PostSystemAlertBody.Builder b) =>
    {
        b.Create(
            alertId: "alert-001"u8,
            severity: "critical"u8,
            message: "Disk usage exceeded 90%."u8);
    }));
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
