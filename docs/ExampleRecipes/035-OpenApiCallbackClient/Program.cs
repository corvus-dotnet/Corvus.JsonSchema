// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using EventSubscription.CallbackClient;
using EventSubscription.CallbackClient.Models;

// The callback-client generator also emits ApiCallbacksClient for the per-subscription
// callback operation. This sample uses the top-level webhook sender to demonstrate the
// server-side delivery flow.
HttpClient httpClient = new()
{
    BaseAddress = new Uri("https://subscriber.example.com/callbacks/")
};

await using HttpClientTransport transport = new(httpClient, disposeClient: true);
await using ApiWebhooksClient client = new(transport);

await using SystemAlertWebhookResponse response = await client.SystemAlertWebhookAsync(
    body: new Schema.Source(static (ref Schema.Builder b) =>
    {
        b.Create(
            alertId: "alert-001"u8,
            severity: "critical"u8,
            message: "Disk usage exceeded 90%."u8);
    }),
    validationMode: ValidationMode.Basic);

if (response.IsSuccess)
{
    Console.WriteLine("Subscriber acknowledged the webhook.");
}
else
{
    Console.WriteLine($"Subscriber returned HTTP {response.StatusCode}.");
}