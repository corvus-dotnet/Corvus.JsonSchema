# AsyncAPI Message Resumption and Acknowledgement

When building production AsyncAPI consumers, you need to handle restarts gracefully — if your application crashes or is redeployed, it should resume processing messages from where it left off, not lose messages or reprocess everything from the beginning.

This document explains how each transport handles message resumption, acknowledgement, and offset management.

## Overview: Transport-Specific Mechanisms

Message resumption is **transport-specific** — each broker has its own model:

| Transport | Resumption Mechanism | Configuration |
|-----------|---------------------|---------------|
| **Kafka** | Consumer groups + committed offsets | `GroupId` + `AutoOffsetReset` |
| **AMQP/RabbitMQ** | Durable queues + message acknowledgement | `QueueDurable` + explicit ack/nack |
| **MQTT** | Clean session flag + QoS levels | `CleanSession` + `QualityOfServiceLevel` |
| **NATS** | Core NATS has no persistence; JetStream provides durable consumers | Not yet implemented |
| **InMemoryMessageTransport** | No persistence (testing only) | N/A |

## Kafka: Consumer Groups and Offsets

### How It Works

Kafka tracks **consumer offsets** per consumer group. When a consumer successfully processes a message, the transport commits the offset to Kafka. On restart, the consumer resumes from the last committed offset.

```
Topic: sensor-readings
Partition 0: [msg-0] [msg-1] [msg-2] [msg-3] [msg-4] ...
                                      ^
                                   Last committed offset
                                   (consumer resumes here)
```

### Configuration

```csharp
using Corvus.Text.Json.AsyncApi.Kafka;
using Confluent.Kafka;

KafkaTransportOptions options = new()
{
    BootstrapServers = "localhost:9092",
    
    // Consumer group ID — all consumers with the same GroupId share offsets
    GroupId = "sensor-processor-v1",
    
    // What to do when no offset exists (new consumer group):
    //   Earliest - Start from the beginning
    //   Latest   - Start from newest messages
    AutoOffsetReset = AutoOffsetReset.Earliest,
    
    // Fine-grained control via ConsumerConfig:
    ConsumerConfig = new ConsumerConfig
    {
        // Automatically commit offsets after successful processing
        EnableAutoCommit = true,
        AutoCommitIntervalMs = 5000,
        
        // Or disable auto-commit for manual control:
        // EnableAutoCommit = false,
    },
};

await using KafkaMessageTransport transport = new(options);
```

### Commit Behavior

The transport **automatically commits** after:
1. Handler completes successfully
2. Error policy returns `MessageErrorAction.Skip`
3. Error policy dead-letters the message

Offsets are **not committed** if:
- Handler throws and error policy returns `MessageErrorAction.Retry` (message will be redelivered)
- Transport crashes before commit (message will be redelivered on restart)

### Example: Guaranteed Processing

```csharp
KafkaTransportOptions options = new()
{
    GroupId = "order-processor",
    AutoOffsetReset = AutoOffsetReset.Earliest,
    
    ConsumerConfig = new ConsumerConfig
    {
        // Commit immediately after each message (safest, slowest)
        EnableAutoCommit = false,
    },
};

await using KafkaMessageTransport transport = new(options);

// After successful handler execution, offset is committed
// If app crashes mid-processing, message will be redelivered
ReceiveOrderConsumer consumer = new(transport, handler);
await consumer.StartAsync();
```

### Example: At-Least-Once Delivery

This is the default Kafka behavior. Messages may be processed more than once if:
- Handler completes but app crashes before offset commit
- Network partition causes duplicate delivery

**Your handler must be idempotent** — processing the same message twice should be safe.

### Example: Starting Fresh (Reset Consumer Group)

To reprocess all messages from the beginning:

```bash
# Delete the consumer group (stops all consumers first!)
kafka-consumer-groups --bootstrap-server localhost:9092 \
    --group sensor-processor-v1 --delete

# Or reset to earliest offset
kafka-consumer-groups --bootstrap-server localhost:9092 \
    --group sensor-processor-v1 --topic sensor-readings \
    --reset-offsets --to-earliest --execute
```

## AMQP/RabbitMQ: Durable Queues and Acknowledgement

### How It Works

AMQP uses **explicit acknowledgement**. When a message is delivered to a consumer:
1. Message remains in the queue (unacknowledged)
2. Transport calls your handler
3. On success → `BasicAck` (message removed from queue)
4. On failure → `BasicNack` with requeue=false (message dead-lettered)

If your app crashes with unacknowledged messages, RabbitMQ redelivers them to another consumer (or the same consumer after restart).

```
Queue: sensor-readings
+------------------------------------------+
| [msg-1] [msg-2] [msg-3] [msg-4] ...     |
|   ACK      ?                             |
|  (done)  (unacked - will be redelivered) |
+------------------------------------------+
```

### Configuration

```csharp
using Corvus.Text.Json.AsyncApi.Amqp;

AmqpTransportOptions options = new()
{
    ConnectionUri = "amqp://guest:guest@localhost:5672/",
    
    // Durable queues survive broker restarts
    QueueDurable = true,
    ExchangeDurable = true,
    
    // Prefetch count — how many unacknowledged messages to buffer
    PrefetchCount = 10,
    
    // Dead-letter exchange for failed messages
    DeadLetterExchange = "sensor-errors",
};

await using AmqpMessageTransport transport = new(options);
```

### Acknowledgement Behavior

| Handler Result | Action | Message Fate |
|---------------|--------|--------------|
| Success | `BasicAck` | Removed from queue |
| Error policy: Skip | `BasicNack` (requeue=false) | Dead-lettered |
| Error policy: Retry | `BasicNack` (requeue=true) | Redelivered |
| Error policy: DeadLetter | `BasicNack` + publish to DLX | Moved to dead-letter exchange |
| Crash (no ack) | — | Redelivered on restart |

### Example: Guaranteed Processing with Dead-Lettering

```csharp
AmqpTransportOptions options = new()
{
    QueueDurable = true,
    PrefetchCount = 1, // Process one message at a time
    DeadLetterExchange = "orders.dead-letter",
};

await using AmqpMessageTransport transport = new(options);

// If handler fails, message is sent to dead-letter exchange
// If app crashes, unacknowledged message is redelivered
ReceiveOrderConsumer consumer = new(transport, handler);
await consumer.StartAsync();
```

### Example: Manual Queue Inspection

```bash
# List queues and message counts
rabbitmqctl list_queues name messages messages_unacknowledged

# View messages in dead-letter queue
rabbitmqadmin get queue=orders.dead-letter count=10

# Purge a queue (careful!)
rabbitmqctl purge_queue orders.dead-letter
```

## MQTT: Clean Session and QoS Levels

### How It Works

MQTT uses **QoS (Quality of Service) levels** combined with the **clean session** flag:

- `CleanSession = true` → Session state is discarded on disconnect (no resumption)
- `CleanSession = false` → Broker stores session state (subscriptions + undelivered messages)

QoS levels:
- **QoS 0** (At Most Once) — Fire and forget, no acknowledgement
- **QoS 1** (At Least Once) — Acknowledged, may duplicate
- **QoS 2** (Exactly Once) — Guaranteed delivery, no duplicates (slowest)

### Configuration

```csharp
using Corvus.Text.Json.AsyncApi.Mqtt;
using MQTTnet.Protocol;

MqttTransportOptions options = new()
{
    Host = "localhost",
    Port = 1883,
    
    // Persistent session — must use unique ClientId
    ClientId = "sensor-processor-001",
    CleanSession = false,
    
    // QoS level for all subscriptions
    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
};

await using MqttMessageTransport transport = new(options);
```

### Resumption Behavior

| Clean Session | Client ID | Resumption |
|--------------|-----------|------------|
| `true` | Any | No resumption — messages during downtime are lost |
| `false` | Unique | Broker queues messages during downtime |
| `false` | Random/empty | **Won't work** — different ID on restart = new session |

### Example: Durable Consumer

```csharp
MqttTransportOptions options = new()
{
    // CRITICAL: Must be stable across restarts
    ClientId = "order-processor-prod-01",
    CleanSession = false,
    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
};

await using MqttMessageTransport transport = new(options);

// Messages published while offline are delivered after restart
ReceiveOrderConsumer consumer = new(transport, handler);
await consumer.StartAsync();
```

### Example: Ephemeral Consumer (No Resumption)

```csharp
MqttTransportOptions options = new()
{
    CleanSession = true, // Discard session on disconnect
    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
};

await using MqttMessageTransport transport = new(options);

// Messages during downtime are lost — use for telemetry, logging
ReceiveTelemetryConsumer consumer = new(transport, handler);
await consumer.StartAsync();
```

## NATS: No Built-In Persistence (Core NATS)

### Current Status

The `NatsMessageTransport` currently uses **core NATS**, which has **no message persistence**. If a consumer is offline, messages are lost.

For durable consumers with resumption, you need **NATS JetStream** (not yet implemented in this library).

### Configuration

```csharp
using Corvus.Text.Json.AsyncApi.Nats;

NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    Name = "sensor-processor",
};

await using NatsMessageTransport transport = new(options);

// Core NATS: messages are delivered to active subscribers only
// No resumption support — offline = messages lost
```

### Future: JetStream Durable Consumers

When JetStream support is added, it will provide:
- **Stream storage** — messages persisted to disk
- **Consumer offsets** — resume from last acknowledged message
- **Replay** — reprocess historical messages

Expected configuration:

```csharp
// NOT YET IMPLEMENTED
NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    UseJetStream = true,
    
    // Durable consumer name — stable across restarts
    ConsumerName = "order-processor",
    
    // Where to start if no offset exists
    DeliverPolicy = DeliverPolicy.All, // or .New, .Last, .ByStartSequence
};
```

## InMemoryMessageTransport: Testing Only

The `InMemoryMessageTransport` has **no persistence** — it's designed for testing, not production. All messages are lost when the process exits.

Use it for:
- Unit tests
- Integration tests
- Local development
- Example code

**Never use in production.**

## Architecture Decision: Interface Design

### Current Design: Implicit Resumption

The current `IMessageTransport.SubscribeAsync()` signature has **no explicit resumption point parameter**:

```csharp
ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken = default);
```

Resumption is configured **implicitly** via transport options:
- Kafka: `GroupId` + `ConsumerConfig`
- AMQP: `QueueDurable` + acknowledgement
- MQTT: `ClientId` + `CleanSession`

### Alternative: Explicit Resumption Parameter

An alternative design would add an explicit resumption context:

```csharp
// NOT IMPLEMENTED — design consideration only
ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    SubscriptionContext context,  // <-- New parameter
    CancellationToken cancellationToken = default);

public sealed class SubscriptionContext
{
    // Kafka: consumer group ID
    public string? GroupId { get; set; }
    
    // MQTT: client ID for persistent session
    public string? ClientId { get; set; }
    
    // AMQP: queue name (instead of auto-generated)
    public string? QueueName { get; set; }
    
    // JetStream: durable consumer name
    public string? DurableConsumerName { get; set; }
    
    // Where to start if no offset exists
    public OffsetResetBehavior ResetBehavior { get; set; }
}
```

### Trade-Offs

| Approach | Pros | Cons |
|----------|------|------|
| **Implicit (current)** | Simple interface, transport-specific tuning via options | Resumption behavior not obvious at call site, can't override per subscription |
| **Explicit** | Clear resumption semantics at call site, per-subscription control | More complex API, duplicates transport options, breaks existing code |

### Recommendation

**Keep the current implicit design** for now. Reasons:
1. Resumption semantics vary wildly across transports — a unified API would be either too abstract or too leaky
2. Most apps use one transport with global config (e.g., all Kafka consumers share the same GroupId)
3. Per-subscription overrides can be added later via an optional context parameter without breaking changes

If per-subscription control becomes necessary, consider:

```csharp
// Additive change — keeps existing signature
ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    SubscriptionOptions? options = null,  // <-- Optional overrides
    CancellationToken cancellationToken = default);
```

## Best Practices

### 1. Choose Appropriate Delivery Guarantees

| Use Case | Recommended Transport + Config |
|----------|-------------------------------|
| Financial transactions | Kafka + `EnableAutoCommit = false` + idempotent handler |
| Order processing | AMQP + `QueueDurable = true` + dead-letter exchange |
| IoT telemetry | MQTT QoS 0 or core NATS (loss acceptable) |
| System logs | MQTT QoS 1 or Kafka |
| Exactly-once semantics | Kafka with transactional producer + idempotent consumer |

### 2. Always Make Handlers Idempotent

At-least-once delivery means messages may be processed multiple times. Your handler must handle duplicates:

```csharp
public ValueTask HandleOrderAsync(Order order, CancellationToken ct)
{
    // Check if already processed
    if (await _db.Orders.AnyAsync(o => o.Id == order.Id, ct))
    {
        return ValueTask.CompletedTask; // Already processed
    }
    
    // Process order
    await _db.Orders.AddAsync(order, ct);
    await _db.SaveChangesAsync(ct);
    
    return ValueTask.CompletedTask;
}
```

### 3. Use Stable Consumer Identifiers

For resumption to work, consumer identifiers must be **stable across restarts**:

- Kafka: Use a consistent `GroupId` (not random)
- MQTT: Use a stable `ClientId` (not auto-generated)
- AMQP: Use durable queues with predictable names

### 4. Monitor Dead-Letter Queues

Set up alerts for dead-letter queue depth:

```csharp
// Example: Log dead-letter messages for alerting
public sealed class AlertOnDeadLetterPolicy : IMessageErrorPolicy
{
    private readonly ILogger _logger;
    
    public async ValueTask<MessageErrorAction> HandleErrorAsync(
        Exception exception,
        MessageErrorContext context,
        CancellationToken ct)
    {
        _logger.LogError(exception,
            "Message dead-lettered from channel {Channel}. Payload: {Payload}",
            context.Channel, context.Payload);
        
        // Trigger alert to ops team
        await _metrics.IncrementCounterAsync("dead_letters_total", ct);
        
        return MessageErrorAction.DeadLetter;
    }
}
```

### 5. Test Crash Recovery

Always test that your consumer resumes correctly after a crash:

```csharp
[TestMethod]
public async Task Consumer_ResumesAfterCrash()
{
    // Publish 100 messages
    for (int i = 0; i < 100; i++)
    {
        await producer.PublishAsync(...);
    }
    
    // Start consumer, process 50 messages
    await consumer.StartAsync();
    await WaitForMessageCount(handler, 50);
    
    // Simulate crash
    await consumer.StopAsync();
    
    // Restart consumer
    await consumer.StartAsync();
    await WaitForMessageCount(handler, 100);
    
    // All 100 messages should be processed (no loss, possible duplicates)
    Assert.AreEqual(100, handler.UniqueMessagesProcessed);
}
```

## Related Documentation

- [Example 037 — AsyncAPI Consumer](ExampleRecipes/037-AsyncApiConsumer/README.md) — Basic consumer setup
- [Example 038 — AsyncAPI End-to-End](ExampleRecipes/038-AsyncApiEndToEnd/README.md) — Producer + consumer integration
- [Example 039 — AsyncAPI Authentication](ExampleRecipes/039-AsyncApiAuthentication/README.md) — Auth for all transports