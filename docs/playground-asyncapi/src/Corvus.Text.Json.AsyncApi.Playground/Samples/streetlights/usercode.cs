// Streetlights — Publish/Subscribe with InMemoryMessageTransport
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Playground;

// ── Set up the in-memory transport ──────────────────────────────────────────
InMemoryMessageTransport transport = new();

// ── 1. Create a producer and send a "turn on" command ────────────────────────
Console.WriteLine("1. Sending 'turn on' command...");
TurnOnProducer producer = new(transport);
await producer.PublishAsync(
    streetlightId: "lamp-001"u8,
    payload: new TurnOnOffPayload.Source(static (ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8);
    }));

Console.WriteLine($"   Published {transport.PublishedMessages.Count} message(s)");
Console.WriteLine($"   Channel: {transport.PublishedMessages[0].Channel}");
Console.WriteLine();

// ── 2. Deliver a light measurement event to the handler ──────────────────────
Console.WriteLine("2. Delivering light measurement event...");
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting/streetlights/1/0/event/lamp-001/lighting/measured"u8,
    new LightMeasuredPayload.Source(static (ref LightMeasuredPayload.Builder b) =>
    {
        b.Create(lumens: 350);
    }));

Console.WriteLine("   Measurement delivered to handler.");
Console.WriteLine();

// ── 3. Inspect published messages ────────────────────────────────────────────
Console.WriteLine("3. All published messages:");
foreach (PublishedMessage msg in transport.PublishedMessages)
{
    Console.WriteLine($"   [{msg.Channel}] {System.Text.Encoding.UTF8.GetString(msg.PayloadBytes)}");
}

Console.WriteLine();
Console.WriteLine("Done!");
