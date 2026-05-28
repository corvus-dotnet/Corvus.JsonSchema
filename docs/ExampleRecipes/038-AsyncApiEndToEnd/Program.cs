// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;

// ── End-to-end: Producer + Consumer with InMemoryMessageTransport ────────────
// This example demonstrates a complete AsyncAPI workflow using InMemoryMessageTransport.
// The transport automatically delivers published messages to matching subscribers,
// simulating a real broker's behavior.

await using InMemoryMessageTransport transport = new();

// ── Set up the consumer (receives measurements from streetlights) ────────────
LightMeasurementHandler handler = new();
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Basic);

await consumer.StartAsync();

// ── Set up the producer (sends commands to streetlights) ─────────────────────
TurnOnProducer commandProducer = new(transport, ValidationMode.Basic);

// ── Example 1: Publish a command ─────────────────────────────────────────────
// In a real system, this would tell a streetlight to turn on.
await commandProducer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-42");

Console.WriteLine($"Command published. Transport captured: {transport.PublishedMessages.Count} messages");

// ── Example 2: Simulate a streetlight publishing measurements ────────────────
// In a real system, sensor hardware would do this. Here we manually publish
// to the consumer's channel to demonstrate the complete flow.
using ParsedJsonDocument<LightMeasuredPayload> measurementDoc = ParsedJsonDocument<LightMeasuredPayload>.Parse(
    """{"lumens":1024,"sentAt":"2026-05-25T12:00:00Z"}"""u8.ToArray());

await transport.PublishAsync(
    channelUtf8: Encoding.UTF8.GetBytes("smartylighting.streetlights.1.0.action.lamp-42.lighting.measured"),
    payload: measurementDoc.RootElement);

Console.WriteLine($"Measurement published and delivered, handler received: {handler.ReceivedCount}");
Console.WriteLine($"Last measurement: {handler.LastLumens} lumens");

// ── Example 3: Schema validation protects consumers ──────────────────────────
// Invalid data is caught before reaching your handler (lumens must be >= 0).
try
{
    using ParsedJsonDocument<LightMeasuredPayload> badDoc = ParsedJsonDocument<LightMeasuredPayload>.Parse(
        """{"lumens":-5,"sentAt":"2026-05-25T12:00:00Z"}"""u8.ToArray());

    await transport.PublishAsync(
        channelUtf8: Encoding.UTF8.GetBytes("smartylighting.streetlights.1.0.action.lamp-42.lighting.measured"),
        payload: badDoc.RootElement);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Validation prevented invalid publish: {ex.Message.Split('\n')[0]}");
}

Console.WriteLine($"After invalid attempt: handler still has {handler.ReceivedCount} valid messages");

// ── Inspect transport state ──────────────────────────────────────────────────
Console.WriteLine($"\nTransport state:");
Console.WriteLine($"  Published messages: {transport.PublishedMessages.Count}");

foreach (PublishedMessage msg in transport.PublishedMessages)
{
    string preview = Encoding.UTF8.GetString(msg.PayloadBytes[..Math.Min(60, msg.PayloadBytes.Length)]);
    Console.WriteLine($"  → {msg.Channel[..Math.Min(60, msg.Channel.Length)]}: {preview}...");
}

// ── Clean shutdown ───────────────────────────────────────────────────────────
await consumer.StopAsync();
Console.WriteLine("\nConsumer stopped. End-to-end demo complete.");

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
        Console.WriteLine($"  [Handler] Received: lumens={payload.Lumens}, sentAt={payload.SentAt}");
        return ValueTask.CompletedTask;
    }
}