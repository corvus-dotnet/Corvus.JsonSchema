# 038-AsyncApiEndToEnd — Producer + Consumer Integration

Demonstrates a complete AsyncAPI workflow with producer and consumer components using `InMemoryMessageTransport` for testing.

## What You'll Learn

| Pattern | Generated Code | Benefit |
|---------|---------------|---------|
| Publishing messages | `TurnOnProducer` | Schema-validated, type-safe publishing |
| Consuming messages | `ReceiveLightMeasurementConsumer` + handler | Automatic deserialization and validation |
| Testing without a broker | `InMemoryMessageTransport` | Fast, reliable tests with automatic message routing |
| Schema validation | Built into producers/consumers | Invalid data caught before reaching handlers |

## The Pattern

`InMemoryMessageTransport` simulates a real message broker:

1. **Subscribe** — consumer registers a handler for a channel
2. **Publish** — producer sends a message to a channel
3. **Auto-deliver** — transport automatically delivers to matching subscribers
4. **Validate** — schema validation happens before delivery

This matches how real brokers (Kafka, NATS, MQTT) work: publish to a channel, and all subscribers receive it.

## Quick Start

```bash
cd docs/ExampleRecipes/038-AsyncApiEndToEnd
dotnet run
```

**Output:**
```
Command published. Transport captured: 1 messages
  [Handler] Received: lumens=1024, sentAt=2026-05-25T12:00:00+00:00
Measurement published and delivered, handler received: 1
Last measurement: 1024 lumens
Validation prevented invalid publish: ...
After invalid attempt: handler still has 1 valid messages

Transport state:
  Published messages: 2
Consumer stopped. End-to-end demo complete.
```

## Step-by-Step Walkthrough

### 1. Create the transport

```csharp
await using InMemoryMessageTransport transport = new();
```

The in-memory transport captures all published messages and automatically delivers them to active subscribers on matching channels.

### 2. Set up the consumer

```csharp
LightMeasurementHandler handler = new();
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Basic);

await consumer.StartAsync();
```

The consumer subscribes to the `lightingMeasured` channel. When messages arrive, they're validated against the schema, then passed to your handler.

**Your handler implementation:**

```csharp
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
        Console.WriteLine($"Received: lumens={payload.Lumens}");
        return ValueTask.CompletedTask;
    }
}
```

### 3. Publish messages

```csharp
// Publish to the subscribed channel
await transport.PublishAsync(
    channelUtf8: Encoding.UTF8.GetBytes("smartylighting.streetlights.1.0.action.lamp-42.lighting.measured"),
    payload: new LightMeasuredPayload.Source((ref LightMeasuredPayload.Builder b) =>
    {
        b.Create(lumens: 1024, sentAt: DateTimeOffset.UtcNow);
    }));
```

The transport:
1. Serializes the payload to JSON
2. Adds it to `PublishedMessages` (for test assertions)
3. **Automatically delivers** to the consumer's subscription
4. Consumer validates the schema
5. Handler receives the typed payload

### 4. Schema validation protects you

```csharp
// This throws InvalidOperationException before publishing
await transport.PublishAsync(
    channelUtf8: Encoding.UTF8.GetBytes("smartylighting.streetlights.1.0.action.lamp-42.lighting.measured"),
    payload: new LightMeasuredPayload.Source((ref LightMeasuredPayload.Builder b) =>
    {
        b.Create(lumens: -5, sentAt: DateTimeOffset.UtcNow); // ❌ lumens must be >= 0
    }));
```

Invalid data never reaches the transport or your handler — it's caught at build time.

## Testing Patterns

### Pattern 1: Verify message delivery

```csharp
int beforeCount = handler.ReceivedCount;

await transport.PublishAsync(...);

Assert.AreEqual(beforeCount + 1, handler.ReceivedCount);
Assert.AreEqual(1024, handler.LastLumens);
```

### Pattern 2: Verify published message capture

```csharp
int beforeCount = transport.PublishedMessages.Count;

await producer.PublishTurnOnOffAsync(...);

Assert.AreEqual(beforeCount + 1, transport.PublishedMessages.Count);
PublishedMessage msg = transport.PublishedMessages[^1];
Assert.AreEqual("smartylighting.streetlights.1.0.action.lamp-42.turn.on", msg.Channel);
```

### Pattern 3: Test without consumers

```csharp
// No consumer started — message is captured but not delivered
await transport.PublishAsync(...);

Assert.AreEqual(1, transport.PublishedMessages.Count);
// No handler invoked, no errors
```

## Switching to Real Brokers

Replace `InMemoryMessageTransport` with a real transport:

```csharp
// Development/Testing
await using InMemoryMessageTransport transport = new();

// Production with Kafka
await using KafkaMessageTransport transport = new(new KafkaTransportOptions
{
    BootstrapServers = "localhost:9092",
    GroupId = "my-consumer-group"
});

// Production with NATS
await using NatsMessageTransport transport = new(new NatsTransportOptions
{
    Servers = ["nats://localhost:4222"]
});
```

**No other code changes needed.** Producers and consumers work with any `IMessageTransport`.

## Common Pitfalls

### 1. **Messages not delivered** — Consumer not started before publishing

```csharp
// ❌ Wrong: publish before consumer starts
await transport.PublishAsync(...);
await consumer.StartAsync(); // Too late!

// ✅ Correct: start consumer first
await consumer.StopAsync();
await transport.PublishAsync(...); // Now delivered
```

### 2. **Wrong channel name**

```csharp
// ❌ Channel name doesn't match consumer subscription
await transport.PublishAsync(
    channelUtf8: Encoding.UTF8.GetBytes("wrong.channel.name"),
    ...);

// ✅ Use the exact channel from the AsyncAPI spec
await transport.PublishAsync(
    channelUtf8: Encoding.UTF8.GetBytes("smartylighting.streetlights.1.0.action.lamp-42.lighting.measured"),
    ...);
```

### 3. **Assuming validation happens after publish**

```csharp
// Schema validation happens BEFORE PublishAsync returns
try
{
    await transport.PublishAsync(...); // ← Throws here if invalid
}
catch (InvalidOperationException)
{
    // Message never entered the transport
}
```

## Performance Tips

- **Reuse transport instances** — creating one per message is expensive
- **Use `ValidationMode.None` in hot paths** — after initial testing
- **Batch assertions** — check `PublishedMessages.Count` once, not per message
- **Avoid `ToString()` in loops** — use `Span<byte>` comparisons for channel names

## Related Patterns

- **036-AsyncApiProducer** — Producer-only patterns
- **037-AsyncApiConsumer** — Consumer-only patterns
- **039-AsyncApiAuthentication** — Adding auth to transports

## What Gets Generated

```bash
corvusjson asyncapi --input streetlights.json --output Generated
```

**Files created:**

- `TurnOnProducer.cs` — Producer for the `turnOn` operation
- `ReceiveLightMeasurementConsumer.cs` — Consumer for the `receiveLightMeasurement` operation
- `IReceiveLightMeasurementHandler.cs` — Handler interface you implement
- `Models/TurnOnOffPayload.cs` — Message payload types
- `Models/LightMeasuredPayload.cs` — Message payload types

The producer and consumer are **transport-agnostic** — they work with any `IMessageTransport` implementation.
