# 038-AsyncApiEndToEnd - Producer + Consumer Integration

Demonstrates a complete AsyncAPI workflow with generated producer and consumer components using `InMemoryMessageTransport` for testing.

## What You'll Learn

| Pattern | Generated Code | Benefit |
|---------|---------------|---------|
| Publishing messages | `TurnOnProducer` | Schema-validated, type-safe publishing |
| Consuming messages | `ReceiveLightMeasurementConsumer` + handler | Automatic deserialization and validation |
| Testing without a broker | `InMemoryMessageTransport` | Fast, reliable tests for producer capture and broker-style delivery |
| Schema validation | Built into producers/consumers | Invalid data caught before reaching handlers |

## The Pattern

`InMemoryMessageTransport` lets a test exercise both sides of an AsyncAPI application without Kafka, NATS, MQTT, or another broker:

1. **Start the consumer** - the generated consumer subscribes and forwards valid messages to your handler.
2. **Publish with the generated producer** - the generated producer validates, serializes, resolves channel parameters, and writes to the transport.
3. **Deliver broker input** - `DeliverAsync<T>()` simulates raw bytes arriving from a real broker for a subscribed consumer channel.
4. **Validate before handling** - schema validation runs before your handler sees the typed payload.

The command producer and measurement consumer are intentionally different operations. The producer sends a command to a streetlight (`...lamp-42.turn.on`). The consumer receives telemetry from a streetlight (`...{streetlightId}.lighting.measured`). In a real deployment, the telemetry would come from the streetlight or a gateway, not from the command producer. The recipe uses `DeliverAsync` to simulate the broker delivering that incoming telemetry.

## Quick Start

```bash
cd docs/ExampleRecipes/038-AsyncApiEndToEnd
dotnet run
```

**Output:**
```
Published command; measurement handler received: 0
  [Handler] lumens=1024, sentAt=2026-05-25T12:00:00+00:00
Delivered sensor reading; measurement handler received: 1
Last measurement: 1024 lumens
After bad data: handler still has 1 valid message(s)

Transport state:
  Published messages: 1
  Dead-lettered: 1
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
TurnOnProducer producer = new(transport, ValidationMode.Basic);

await producer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-42");
```

The generated producer:
1. Serializes the payload to JSON
2. Validates the payload before publishing
3. Resolves `{streetlightId}` to the concrete command channel
4. Adds the message to `PublishedMessages` for assertions

This command is outbound. It is not delivered to the measurement consumer because that consumer is subscribed to the telemetry channel, not the command channel.

### 4. Deliver telemetry from the broker

```csharp
ReadOnlyMemory<byte> sensorReading =
    """{"lumens":1024,"sentAt":"2026-05-25T12:00:00Z"}"""u8.ToArray();

await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    sensorReading);
```

`DeliverAsync<T>()` simulates the broker delivering raw JSON bytes to the subscribed consumer. This is the right model for incoming telemetry: the consumer did not create the message, it receives bytes from the broker and lets generated code parse, validate, and dispatch to the handler.

The generated consumer currently subscribes using the AsyncAPI channel template key. A real broker topic would normally contain a concrete value such as `smartylighting.streetlights.1.0.action.lamp-42.lighting.measured`; this in-memory sample uses the template string because the testing transport matches subscriptions by exact string.

### 5. Schema validation protects consumers

```csharp
ReadOnlyMemory<byte> badData =
    """{"lumens":-5,"sentAt":"2026-05-25T12:01:00Z"}"""u8.ToArray();

await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    badData);
```

Invalid data is parsed and routed to the generated consumer, but schema validation fails before your handler is called. The default error policy dead-letters the message, so `handler.ReceivedCount` stays unchanged and `transport.DeadLetteredMessages.Count` increases.

## Testing Patterns

### Pattern 1: Verify message delivery

```csharp
int beforeCount = handler.ReceivedCount;

await transport.DeliverAsync<LightMeasuredPayload>(...);

Assert.AreEqual(beforeCount + 1, handler.ReceivedCount);
Assert.AreEqual(1024, handler.LastLumens);
```

### Pattern 2: Verify published message capture

```csharp
await producer.PublishTurnOnOffAsync(...);

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
corvusjson asyncapi-generate streetlights.json --rootNamespace GeneratedAsyncApi --outputPath Generated
```

**Files created:**

- `TurnOnProducer.cs` — Producer for the `turnOn` operation
- `ReceiveLightMeasurementConsumer.cs` — Consumer for the `receiveLightMeasurement` operation
- `IReceiveLightMeasurementHandler.cs` — Handler interface you implement
- `Models/TurnOnOffPayload.cs` — Message payload types
- `Models/LightMeasuredPayload.cs` — Message payload types

The producer and consumer are **transport-agnostic** — they work with any `IMessageTransport` implementation.
