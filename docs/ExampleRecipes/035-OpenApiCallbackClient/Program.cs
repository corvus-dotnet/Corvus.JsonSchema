// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using EventSubscription.CallbackClient;
using EventSubscription.CallbackClient.Models;

// ── Sending webhooks from your server ─────────────────────────────────────────
// This example shows the SERVER-SIDE SENDER: your API delivers notifications to
// subscriber callback URLs using the generated callback client.
//
// Production code uses HttpClientTransport with a real subscriber base address.
// This recipe uses an in-memory transport so `dotnet run` is self-contained and
// does not depend on a remote server.

await using DemoTransport transport = new();
Console.WriteLine("Using in-memory demo transport. In production, replace with HttpClientTransport.");
Console.WriteLine();

// The generator creates separate client classes per tag group.
ApiWebhooksClient webhooksClient = new(transport);
ApiCallbacksClient callbacksClient = new(transport);

// ── 1. Send a top-level webhook (systemAlert) ────────────────────────────────
Console.WriteLine("1. Sending systemAlert webhook...");
await using SystemAlertWebhookResponse alertResponse = await webhooksClient.SystemAlertWebhookAsync(
    body: new Schema.Source(static (ref Schema.Builder b) =>
    {
        b.Create(
            alertId: "alert-001"u8,
            severity: "critical"u8,
            message: "Disk usage exceeded 90%."u8);
    }),
    validationMode: ValidationMode.Basic);

if (alertResponse.IsSuccess)
{
    Console.WriteLine("   Subscriber acknowledged the webhook (HTTP 200).");
}
else
{
    Console.WriteLine($"   Subscriber returned HTTP {alertResponse.StatusCode}.");
}

Console.WriteLine();

// ── 2. Send a per-subscription callback (onEventCallback) ────────────────────
Console.WriteLine("2. Sending onEventCallback...");
await using OnEventCallbackResponse eventResponse = await callbacksClient.OnEventCallbackAsync(
    body: new Schema1.Source(static (ref Schema1.Builder b) =>
    {
        b.Create(
            eventId: "evt-123"u8,
            eventType: "subscription.created"u8,
            timestamp: "2026-01-15T10:30:00Z"u8);
    }),
    validationMode: ValidationMode.Basic);

if (eventResponse.IsSuccess)
{
    Console.WriteLine("   Subscriber acknowledged the callback (HTTP 200).");
}
else
{
    Console.WriteLine($"   Subscriber returned HTTP {eventResponse.StatusCode}.");
}

Console.WriteLine();
Console.WriteLine("Done!");

// ── In-memory transport for self-contained demo ──────────────────────────────
// In production, replace with:
//   HttpClient httpClient = new() { BaseAddress = new Uri("https://subscriber.example.com/callbacks/") };
//   await using HttpClientTransport transport = new(httpClient, disposeClient: true);
internal sealed class DemoTransport : IApiTransport
{
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse> =>
        TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(
        in TRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse> =>
        TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Stream body,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse> =>
        TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Func<Stream, CancellationToken, ValueTask> bodyWriter,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse> =>
        TResponse.CreateAsync(200, Stream.Null, cancellationToken: cancellationToken);

    public ValueTask DisposeAsync() => default;
}