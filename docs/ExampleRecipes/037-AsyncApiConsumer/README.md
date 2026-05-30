# 037 — AsyncAPI Consumer

Demonstrates how to **receive and handle messages** using a generated AsyncAPI 3.0 consumer. The code generator produces `ReceiveLightMeasurementConsumer` and the `IReceiveLightMeasurementHandler` interface — you implement the handler, and the consumer manages subscription, deserialization, schema validation, and error routing.

## What This Demonstrates

| Feature | Where |
|---------|-------|
| Generated consumer with typed handler | `ReceiveLightMeasurementConsumer` + `IReceiveLightMeasurementHandler` |
| Incoming payload schema validation | `ValidationMode.Basic` rejects invalid messages |
| Custom error policy | `IMessageErrorPolicy` — skip, retry, or dead-letter |
| Strongly-typed payload access | `payload.Lumens`, `payload.SentAt` |
| Auto-delivery from in-memory transport | `InMemoryMessageTransport` acts like a real broker |

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

This produces the same files as [036](../036-AsyncApiProducer/) — both producer and consumer are generated from the same spec.

## How the Generated Code Helps

### The Handler Pattern

The generator produces an `IReceiveLightMeasurementHandler` interface with a single method:

```csharp
public interface IReceiveLightMeasurementHandler
{
    ValueTask HandleLightMeasuredAsync(
        LightMeasuredPayload payload,
        CancellationToken cancellationToken = default);
}
```

Your handler receives a **strongly-typed, already-validated** payload. You never parse JSON or check schemas manually.

### Validation and Error Policy

The consumer validates incoming messages against the schema before calling your handler:

```csharp
LogAndSkipErrorPolicy errorPolicy = new();
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Basic,
    errorPolicy: errorPolicy);
```

If validation fails, the message is routed to your `IMessageErrorPolicy` — it never reaches the handler. The policy decides what to do:

| Action | Behaviour |
|--------|-----------|
| `MessageErrorAction.Skip` | Discard the message and continue |
| `MessageErrorAction.DeadLetter` | Move to dead-letter queue |

### Consumer Lifecycle

```csharp
await consumer.StartAsync();    // Subscribe to the channel
// ... messages are published and auto-delivered ...
await consumer.StopAsync();     // Unsubscribe and clean up
```

### How InMemoryMessageTransport Works

Unlike manual `DeliverAsync()` calls, `InMemoryMessageTransport.PublishAsync()` **automatically delivers** to active subscribers — just like a real broker (Kafka/NATS/MQTT). When you publish to a channel, any consumer subscribed to that channel immediately receives it.

In a production consumer, your application would not normally publish directly to the same transport it consumes from. The physical streetlight, or a telemetry gateway representing it, would publish the measurement to the broker. This recipe writes to `InMemoryMessageTransport` only to simulate that external telemetry source in a self-contained console app.

The AsyncAPI document defines the receive channel as the address template `smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured`. The `{streetlightId}` segment is a template parameter, not a literal broker channel. A real device would publish to a concrete channel such as `smartylighting.streetlights.1.0.action.lamp-42.lighting.measured`; the generated consumer subscribes using the template, and the in-memory testing transport uses exact string matching, so the sample publishes to that same template key to trigger the generated subscriber.

## Running

```bash
dotnet run -f net10.0
```

Expected output:

```text
Consumer started, waiting for messages...
Simulating telemetry from lamp-42 on smartylighting.streetlights.1.0.action.lamp-42.lighting.measured
  Received: lumens=512, sentAt=2026-05-25T10:30:00Z
Handler received 1 message(s)
Last lumens: 512
  Received: lumens=2048, sentAt=2026-05-25T10:31:00Z
Handler received 2 total message(s)
Last lumens: 2048
Consumer stopped
```

## Related Recipes

- [036 — AsyncAPI Producer](../036-AsyncApiProducer/) — publishing messages
- [038 — AsyncAPI End-to-End](../038-AsyncApiEndToEnd/) — full producer + consumer pipeline
- [039 — AsyncAPI Authentication](../039-AsyncApiAuthentication/) — all supported auth patterns
