// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;

// ── Setting up the transport ─────────────────────────────────────────────────
await using InMemoryMessageTransport transport = new();

// ── Implementing the handler ─────────────────────────────────────────────────
// The generator produces an IReceiveLightMeasurementHandler interface.
// Your implementation receives strongly-typed, validated payloads.
LightMeasurementHandler handler = new();

// ── Creating the consumer ────────────────────────────────────────────────────
// The consumer takes a transport, handler, validation mode, and error policy.
// ValidationMode.Basic enables schema validation on incoming messages — if a
// message fails validation, the error policy determines what happens.
LogAndSkipErrorPolicy errorPolicy = new();
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Basic,
    errorPolicy: errorPolicy);

// ── Starting the consumer ────────────────────────────────────────────────────
// StartAsync subscribes to the channel. If an auth provider is configured,
// it authenticates before subscribing.
await consumer.StartAsync();
Console.WriteLine("Consumer started, waiting for messages...");

// ── Simulating message delivery ──────────────────────────────────────────────
// In production, the transport delivers messages from the broker.
// For testing, we use DeliverAsync to simulate incoming messages.
ReadOnlyMemory<byte> validPayload = """{"lumens":512,"sentAt":"2026-05-25T10:30:00Z"}"""u8.ToArray();
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    validPayload);

Console.WriteLine($"Handler received {handler.ReceivedCount} message(s)");
Console.WriteLine($"Last lumens: {handler.LastLumens}");

// ── Validation failure handling ──────────────────────────────────────────────
// Invalid messages are caught by the validation layer and routed through
// the error policy — they never reach your handler.
ReadOnlyMemory<byte> invalidPayload = """{"lumens":"not-a-number"}"""u8.ToArray();
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    invalidPayload);

Console.WriteLine($"After invalid: handler still has {handler.ReceivedCount} message(s)");
Console.WriteLine($"Errors logged: {errorPolicy.ErrorCount}");

// ── Stopping the consumer ────────────────────────────────────────────────────
await consumer.StopAsync();
Console.WriteLine("Consumer stopped");

// ── Handler implementation ───────────────────────────────────────────────────
internal sealed class LightMeasurementHandler : IReceiveLightMeasurementHandler
{
    public int ReceivedCount { get; private set; }

    public int LastLumens { get; private set; }

    public ValueTask HandleLightMeasuredAsync(
        LightMeasuredPayload payload,
        CancellationToken cancellationToken = default)
    {
        this.ReceivedCount++;
        this.LastLumens = (int)payload.Lumens;
        Console.WriteLine($"  Received: lumens={payload.Lumens}, sentAt={payload.SentAt}");
        return ValueTask.CompletedTask;
    }
}

// ── Custom error policy ──────────────────────────────────────────────────────
// The error policy decides what happens when message handling fails
// (validation error, handler exception, etc.)
internal sealed class LogAndSkipErrorPolicy : IMessageErrorPolicy
{
    public int ErrorCount { get; private set; }

    public ValueTask<MessageErrorAction> HandleErrorAsync(
        Exception exception,
        MessageErrorContext context,
        CancellationToken cancellationToken = default)
    {
        this.ErrorCount++;
        Console.WriteLine($"  Error on {context.Channel}: {exception.Message}");
        return new ValueTask<MessageErrorAction>(MessageErrorAction.Skip);
    }
}