// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;

// ── End-to-end: Producer + Consumer with InMemoryMessageTransport ────────────
// This example demonstrates the full publish -> transport -> consume pipeline
// using InMemoryMessageTransport for testing without a real broker.

await using InMemoryMessageTransport transport = new();

// ── Set up the consumer side ─────────────────────────────────────────────────
LightMeasurementHandler handler = new();
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Basic);

await consumer.StartAsync();

// ── Set up the producer side ─────────────────────────────────────────────────
TurnOnProducer producer = new(transport, ValidationMode.Basic);

// ── Publish a command ────────────────────────────────────────────────────────
// The generated producer serializes and publishes to the concrete command
// channel for lamp-42. There is no command consumer in this recipe, so the
// transport captures the outbound command but does not invoke the measurement
// handler.
await producer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-42");

Console.WriteLine($"Published command; measurement handler received: {handler.ReceivedCount}");

// ── Deliver telemetry to the consumer ────────────────────────────────────────
// The measurement is not produced by the command producer. In a real system, a
// streetlight or telemetry gateway would publish this to the broker, and the
// broker would deliver the raw bytes to the generated consumer. DeliverAsync
// simulates that broker delivery path.
const string MeasuredChannelTemplate = "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured";
ReadOnlyMemory<byte> sensorReading = """{"lumens":1024,"sentAt":"2026-05-25T12:00:00Z"}"""u8.ToArray();
await transport.DeliverAsync<LightMeasuredPayload>(
    MeasuredChannelTemplate,
    sensorReading);

Console.WriteLine($"Delivered sensor reading; measurement handler received: {handler.ReceivedCount}");
Console.WriteLine($"Last measurement: {handler.LastLumens} lumens");

// ── Validation protects the consumer ─────────────────────────────────────────
// Bad data from the broker is caught before reaching your handler. The default
// error policy dead-letters invalid messages.
ReadOnlyMemory<byte> badData = """{"lumens":-5,"sentAt":"2026-05-25T12:01:00Z"}"""u8.ToArray();
await transport.DeliverAsync<LightMeasuredPayload>(
    MeasuredChannelTemplate,
    badData);

Console.WriteLine($"After bad data: handler still has {handler.ReceivedCount} valid message(s)");

// ── Inspect transport state ──────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("Transport state:");
Console.WriteLine($"  Published messages: {transport.PublishedMessages.Count}");
Console.WriteLine($"  Dead-lettered: {transport.DeadLetteredMessages.Count}");

foreach (PublishedMessage msg in transport.PublishedMessages)
{
    Console.WriteLine($"  -> {msg.Channel}: {Encoding.UTF8.GetString(msg.PayloadBytes)}");
}

// ── Clean shutdown ───────────────────────────────────────────────────────────
await consumer.StopAsync();
Console.WriteLine();
Console.WriteLine("Consumer stopped. End-to-end demo complete.");

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
        Console.WriteLine($"  [Handler] lumens={payload.Lumens}, sentAt={payload.SentAt}");
        return ValueTask.CompletedTask;
    }
}