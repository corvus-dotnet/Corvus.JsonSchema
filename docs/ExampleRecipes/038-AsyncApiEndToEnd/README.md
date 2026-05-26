# 038 — AsyncAPI End-to-End: Full Producer → Consumer Pipeline

Demonstrates the **complete publish → transport → consume lifecycle** using both the generated producer and consumer on a shared `InMemoryMessageTransport`. This is the integration test pattern for AsyncAPI-based messaging without a real broker.

## What This Demonstrates

| Feature | Where | Why It Matters |
|---------|-------|---------------|
| Producer publishes typed messages | `TurnOnProducer.PublishTurnOnOffAsync` | No manual JSON serialization |
| Consumer receives via handler | `ReceiveLightMeasurementConsumer` + handler | Typed payload access, no parsing |
| Shared in-memory transport | `InMemoryMessageTransport` | Test without broker infrastructure |
| Schema validation rejects bad data | Invalid payload skipped before handler | Handler only sees valid data |
| Transport state inspection | `PublishedMessages`, `DeadLetteredMessages` | Verify message flow in tests |
| Simulated broker delivery | `transport.DeliverAsync<T>()` | Control timing and content |

## The Pattern: Testing Without a Broker

Real broker integration tests are expensive — they require Docker containers, network IO, and sometimes minutes of setup. The `InMemoryMessageTransport` pattern lets you verify your messaging logic in milliseconds:

1. **Producer sends** — message is captured in `PublishedMessages`
2. **You control delivery** — call `DeliverAsync` to simulate broker delivery at the exact moment you want
3. **Consumer reacts** — your handler runs with typed, validated payloads
4. **Assert state** — inspect counts, dead-letters, and handler side effects

This is **not** a mock — it's a real transport implementation. Your producer and consumer code runs identically to production; only the network layer is replaced.

## Prerequisites

```bash
dotnet tool install --global Corvus.Json.Cli
```

## Generating the Code

```bash
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Client \
    --outputPath Generated
```

This produces both producer and consumer classes from the same spec, plus:
- **16 model types** in `Models/` — strongly-typed schema-validated payloads
- **Message wrappers** — `TurnOnTurnOnOffMessage`, `ReceiveLightMeasurementLightMeasuredMessage`
- **Handler interface** — `IReceiveLightMeasurementHandler` with `HandleLightMeasuredAsync`

## Full Walkthrough

[Example code](./Program.cs)

### Step 1: Create the in-memory transport

The transport acts as both publisher and subscriber:

```csharp
await using InMemoryMessageTransport transport = new();
```

### Step 2: Set up the consumer

Create a handler that implements the generated interface:

```csharp
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
```

Wire it to the transport:

```csharp
LightCommandHandler handler = new();
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Basic);  // Validates before calling handler

await consumer.StartAsync();  // Subscribes to the channel
```

### Step 3: Set up the producer

```csharp
TurnOnProducer producer = new(transport, ValidationMode.Basic);
```

### Step 4: Publish a message

The producer validates, serializes, and sends the message. The transport captures it **and** immediately delivers it to the subscribed consumer:

```csharp
await producer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-42");

Console.WriteLine($"Published to channel, consumer received: {handler.ReceivedCount}");
// Output: Published to channel, consumer received: 0
```

**Why zero?** The producer's `Publish` call is asynchronous in the API sense, but `InMemoryMessageTransport` only *queues* the message — it doesn't deliver to subscribers until you explicitly call `DeliverAsync` (shown next). This gives you full control over timing in tests.

### Step 5: Simulate broker delivery

For operations where the consumer **receives** (not the producer's outbound channel), use `DeliverAsync` to simulate what the broker would do:

```csharp
ReadOnlyMemory<byte> sensorReading = """{"lumens":1024,"sentAt":"2026-05-25T12:00:00Z"}"""u8.ToArray();

await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    sensorReading);

Console.WriteLine($"Delivered sensor reading, consumer received: {handler.ReceivedCount}");
// Output: Delivered sensor reading, consumer received: 1
Console.WriteLine($"Last measurement: {handler.LastLumens} lumens");
// Output: Last measurement: 1024 lumens
```

The transport:
1. Deserializes the byte array into a `LightMeasuredPayload`
2. Validates it against the schema
3. Calls `handler.HandleLightMeasuredAsync(payload, ...)`

### Step 6: Validation protects the consumer

Send invalid data to verify schema validation works:

```csharp
ReadOnlyMemory<byte> badData = """{"lumens":-5}"""u8.ToArray();
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    badData);

Console.WriteLine($"After bad data: handler still has {handler.ReceivedCount}");
// Output: After bad data: handler still has 1 (invalid message skipped)
```

The schema defines `lumens` as a non-negative number. The generated code catches the violation and dead-letters the message — your handler never sees it.

### Step 7: Inspect transport state

The transport exposes collections for test assertions:

```csharp
Console.WriteLine($"Transport state:");
Console.WriteLine($"  Published messages: {transport.PublishedMessages.Count}");
// Output:   Published messages: 1

Console.WriteLine($"  Dead-lettered: {transport.DeadLetteredMessages.Count}");
// Output:   Dead-lettered: 1

foreach (PublishedMessage msg in transport.PublishedMessages)
{
    Console.WriteLine($"  → {msg.Channel}: {Encoding.UTF8.GetString(msg.PayloadBytes)}");
}
// Output:   → smartylighting.streetlights.1.0.action.lamp-42.turn.on: {"command":"on","sentAt":"2026-05-26T..."}
```

### Step 8: Clean shutdown

```csharp
await consumer.StopAsync();
Console.WriteLine("\nConsumer stopped. End-to-end demo complete.");
```

## Integration Testing Patterns

### Pattern 1: Arrange-Act-Assert with controlled delivery

```csharp
// Arrange
var handler = new TestHandler();
await using var transport = new InMemoryMessageTransport();
var consumer = new MyConsumer(transport, handler);
await consumer.StartAsync();

// Act
await transport.DeliverAsync<MyPayload>(
    "my.channel",
    """{"value": 42}"""u8.ToArray());

// Assert
Assert.AreEqual(1, handler.ReceivedCount);
Assert.AreEqual(42, handler.LastValue);
```

### Pattern 2: Verify producer output without consumer

```csharp
await using var transport = new InMemoryMessageTransport();
var producer = new MyProducer(transport);

await producer.PublishAsync(new MyPayload.Source(...));

Assert.AreEqual(1, transport.PublishedMessages.Count);
PublishedMessage msg = transport.PublishedMessages[0];
Assert.AreEqual("expected.channel.name", msg.Channel);

// Deserialize and verify payload structure
using var doc = ParsedJsonDocument<JsonElement>.Parse(msg.PayloadBytes.Span);
Assert.AreEqual("expected-value", (string)doc.RootElement.GetProperty("field"u8));
```

### Pattern 3: Test error policy behavior

```csharp
var errorPolicy = new TestErrorPolicy();
var consumer = new MyConsumer(transport, handler, errorPolicy: errorPolicy);
await consumer.StartAsync();

// Send invalid data
await transport.DeliverAsync<MyPayload>("channel", """{"invalid": true}"""u8.ToArray());

// Verify error policy was invoked
Assert.AreEqual(1, errorPolicy.ErrorCount);
Assert.AreEqual(MessageErrorAction.DeadLetter, errorPolicy.LastAction);
```

## Common Pitfalls

### Pitfall 1: Expecting immediate delivery after `PublishAsync`

**Problem:**

```csharp
await producer.PublishAsync(...);
Assert.AreEqual(1, handler.ReceivedCount);  // FAILS: handler.ReceivedCount is still 0
```

**Why:** `InMemoryMessageTransport` queues published messages but doesn't auto-deliver to consumers. Use `DeliverAsync` explicitly.

**Fix:** Use the correct delivery API:

```csharp
await transport.DeliverAsync<MyPayload>("channel", messageBytes);
```

### Pitfall 2: Not disposing ParsedJsonDocument in assertions

**Problem:**

```csharp
foreach (var msg in transport.PublishedMessages)
{
    var doc = ParsedJsonDocument<JsonElement>.Parse(msg.PayloadBytes.Span);
    // Forgot 'using' — memory leak!
}
```

**Fix:**

```csharp
foreach (var msg in transport.PublishedMessages)
{
    using var doc = ParsedJsonDocument<JsonElement>.Parse(msg.PayloadBytes.Span);
    // Memory returned after using block
}
```

### Pitfall 3: Checking consumer state before StartAsync completes

**Problem:**

```csharp
var consumer = new MyConsumer(transport, handler);
consumer.StartAsync();  // Missing await!
await transport.DeliverAsync(...);  // Delivery happens before subscription completes
```

**Fix:**

```csharp
await consumer.StartAsync();  // Wait for subscription
await transport.DeliverAsync(...);  // Now delivery works
```

## Troubleshooting

### "Consumer received 0 messages even though I called DeliverAsync"

**Check:**
1. Did you `await consumer.StartAsync()` before delivery?
2. Does the channel name in `DeliverAsync` match the consumer's subscribed channel?
3. Is the payload valid against the schema? Check `transport.DeadLetteredMessages`.

### "Handler never called despite valid payload"

**Check:**
1. Did you pass `validationMode: ValidationMode.None` when you meant `Basic`?
2. Is your handler method `async` but you forgot to `await` internally? This can cause silent swallowing of exceptions.
3. Check `transport.DeadLetteredMessages` — if count > 0, validation is failing.

### "Memory usage grows during test"

**Cause:** Not disposing `ParsedJsonDocument` instances from transport inspection.

**Fix:** Always use `using` when parsing `msg.PayloadBytes`:

```csharp
using var doc = ParsedJsonDocument<MyPayload>.Parse(msg.PayloadBytes.Span);
```

## Performance Tips

1. **Reuse workspaces** — when building many payloads in a loop, create one `JsonWorkspace` and reuse it:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
for (int i = 0; i < 1000; i++)
{
    await producer.PublishAsync(
        new MyPayload.Source((ref MyPayload.Builder b) => { b.Create(...); }),
        workspace: workspace);  // Reuse pooled memory
}
```

2. **Use UTF-8 literals** — `"value"u8` avoids string → UTF-8 conversion overhead.

3. **ValidationMode.None in hot paths** — once your data model is stable, disable validation for maximum throughput:

```csharp
var producer = new MyProducer(transport, ValidationMode.None);
```

## Running the Example

```bash
cd docs/ExampleRecipes/038-AsyncApiEndToEnd
dotnet run
```

Expected output:

```text
Published to channel, consumer received: 0
  [Handler] lumens=1024, sentAt=2026-05-25T12:00:00Z
Delivered sensor reading, consumer received: 1
Last measurement: 1024 lumens
After bad data: handler still has 1 (invalid message skipped)

Transport state:
  Published messages: 1
  Dead-lettered: 1
  → smartylighting.streetlights.1.0.action.lamp-42.turn.on: {"command":"on","sentAt":"2026-05-26T..."}

Consumer stopped. End-to-end demo complete.
```

## Related Recipes

- [036 — AsyncAPI Producer](../036-AsyncApiProducer/) — producer details and Source pattern
- [037 — AsyncAPI Consumer](../037-AsyncApiConsumer/) — consumer details and error policy
- [039 — AsyncAPI Authentication](../039-AsyncApiAuthentication/) — all supported auth patterns
