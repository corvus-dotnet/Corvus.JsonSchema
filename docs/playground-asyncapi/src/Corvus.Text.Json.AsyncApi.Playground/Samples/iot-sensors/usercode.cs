// End-to-end: Producer + Consumer with InMemoryMessageTransport
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Playground;
using Playground.Models;

// This example demonstrates the full publish → transport → consume pipeline
// using InMemoryMessageTransport for testing without a real broker.

await using InMemoryMessageTransport transport = new();

// ── Set up the consumer side ─────────────────────────────────────────────────
LightCommandHandler handler = new();
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Basic);

await consumer.StartAsync();

// ── Set up the producer side ─────────────────────────────────────────────────
TurnOnProducer producer = new(transport, ValidationMode.Basic);

// ── Publish a message ────────────────────────────────────────────────────────
// The producer serializes and publishes to the channel.
// The transport captures it AND delivers it to any active subscriber.
await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "on"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-42");

Console.WriteLine($"Published to channel, consumer received: {handler.ReceivedCount}");

// ── Deliver directly to the consumer (simulating broker delivery) ────────────
// For the lightMeasured channel (receive operation), we simulate what the
// broker would do — deliver raw bytes to the subscribed consumer.
ReadOnlyMemory<byte> sensorReading = """{"lumens":1024,"sentAt":"2026-05-25T12:00:00Z"}"""u8.ToArray();
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    sensorReading);

Console.WriteLine($"Delivered sensor reading, consumer received: {handler.ReceivedCount}");
Console.WriteLine($"Last measurement: {handler.LastLumens} lumens");

// ── Validation protects the consumer ─────────────────────────────────────────
// Bad data from the broker is caught before reaching your handler.
ReadOnlyMemory<byte> badData = """{"lumens":-5}"""u8.ToArray();
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    badData);

Console.WriteLine($"After bad data: handler still has {handler.ReceivedCount} (invalid message skipped)");

// ── Inspect transport state ──────────────────────────────────────────────────
Console.WriteLine($"\nTransport state:");
Console.WriteLine($"  Published messages: {transport.PublishedMessages.Count}");
Console.WriteLine($"  Dead-lettered: {transport.DeadLetteredMessages.Count}");

foreach (PublishedMessage msg in transport.PublishedMessages)
{
    Console.WriteLine($"  → {msg.Channel}: {Encoding.UTF8.GetString(msg.PayloadBytes)}");
}

// ── Clean shutdown ───────────────────────────────────────────────────────────
await consumer.StopAsync();
Console.WriteLine("\nConsumer stopped. End-to-end demo complete.");

// ── Handler implementation ───────────────────────────────────────────────────
internal sealed class LightCommandHandler : IReceiveLightMeasurementHandler
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
