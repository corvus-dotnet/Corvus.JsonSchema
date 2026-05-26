// IoT Sensors — Request/Reply with InMemoryMessageTransport
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Playground;

// ── Set up the in-memory transport ──────────────────────────────────────────
InMemoryMessageTransport transport = new();

// ── 1. Publish temperature readings ─────────────────────────────────────────
Console.WriteLine("1. Publishing temperature readings...");
PublishTemperatureProducer tempProducer = new(transport);

await tempProducer.PublishAsync(
    sensorId: "sensor-42"u8,
    payload: new TemperaturePayload.Source(static (ref TemperaturePayload.Builder b) =>
    {
        b.Create(celsius: 23.5);
    }));

await tempProducer.PublishAsync(
    sensorId: "sensor-42"u8,
    payload: new TemperaturePayload.Source(static (ref TemperaturePayload.Builder b) =>
    {
        b.Create(celsius: 24.1);
    }));

Console.WriteLine($"   Published {transport.PublishedMessages.Count} reading(s)");
foreach (PublishedMessage msg in transport.PublishedMessages)
{
    Console.WriteLine($"   [{msg.Channel}] {System.Text.Encoding.UTF8.GetString(msg.PayloadBytes)}");
}

Console.WriteLine();

// ── 2. Inspect all messages on the transport ─────────────────────────────────
Console.WriteLine("2. Transport state:");
Console.WriteLine($"   Total published: {transport.PublishedMessages.Count}");
Console.WriteLine($"   Dead-lettered:   {transport.DeadLetteredMessages.Count}");
Console.WriteLine();

// ── 3. Reset and verify ──────────────────────────────────────────────────────
Console.WriteLine("3. Resetting transport...");
transport.Reset();
Console.WriteLine($"   Messages after reset: {transport.PublishedMessages.Count}");

Console.WriteLine();
Console.WriteLine("Done!");
