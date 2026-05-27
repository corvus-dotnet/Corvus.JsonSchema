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
| **NATS** | Core NATS has no persistence; JetStream provides durable consumers | `UseJetStream` + `ConsumerName` |
| **Azure Service Bus** | PeekLock settlement + queues/topic subscriptions | `ReceiveMode` + queue/topic options |
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
        // The transport disables auto-commit and commits after processing.
        // Additional consumer options can be supplied here.
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
- Handler throws and the error policy returns `MessageErrorAction.Abort` (message will be redelivered)
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

1. **Message delivered** → Message stays in queue (unacknowledged)
2. **Handler processes** → Transport calls your handler code
3. **Success** → `BasicAck` sent → Message removed from queue permanently
4. **Failure** → `BasicNack` sent → Message requeued or dead-lettered

**Key point**: If your app crashes before sending `BasicAck`, RabbitMQ assumes the message wasn't processed and redelivers it to another consumer (or the same consumer after restart).

**Timeline of message processing (left to right):**
```
Queue: sensor-readings

[msg-1] [msg-2] [msg-3] [msg-4] ...
   ✓       ✗
  Acked  Unacked (will be redelivered)
```
- **msg-1**: Processed successfully, `BasicAck` sent → removed from queue
- **msg-2**: Currently being processed, not yet acknowledged
- **msg-3, msg-4**: Waiting to be delivered

If the consumer crashes now, msg-2 through msg-4 will be redelivered (msg-1 is gone forever).

This is **at-least-once delivery** — messages may be processed more than once if your app crashes between processing and acknowledgement.

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

await using AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(options);
```

### Acknowledgement Behavior

| Handler Result | Action | Message Fate |
|---------------|--------|--------------|
| Success | `BasicAck` | Removed from queue |
| Error policy: Skip | `BasicNack` (requeue=false) | Dead-lettered |
| Error policy: Abort | Consumer stops before successful acknowledgement | Redelivered |
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

await using AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(options);

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

await using MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(options);
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

await using MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(options);

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

await using MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(options);

// Messages during downtime are lost — use for telemetry, logging
ReceiveTelemetryConsumer consumer = new(transport, handler);
await consumer.StartAsync();
```

## NATS: Core NATS and JetStream

### How It Works

`NatsMessageTransport` supports two modes:

- **Core NATS** (`UseJetStream = false`, the default) — no persistence. Messages are delivered only to active subscribers.
- **JetStream** (`UseJetStream = true`) — persistent streams and durable consumers. Messages can be resumed after restart.

### Core NATS Configuration

```csharp
using Corvus.Text.Json.AsyncApi.Nats;

NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    Name = "sensor-processor",
};

await using NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(options);

// Core NATS: messages are delivered to active subscribers only
// No resumption support — offline = messages lost
```

### JetStream Configuration

```csharp
using Corvus.Text.Json.AsyncApi.Nats;

NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    Name = "sensor-processor",

    // Enable durable messaging
    UseJetStream = true,
    StreamName = "sensor-readings",
    ConsumerName = "sensor-processor-v1",

    // Redeliver if not acknowledged within this window
    AckWait = TimeSpan.FromSeconds(30),
    MaxDeliver = 5,

    // File storage survives broker restarts
    StorageType = StorageType.File,
    DeliverPolicy = DeliverPolicy.All,
};

await using NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(options);
```

### JetStream Acknowledgement Behavior

| Handler Result | Action | Message Fate |
|---------------|--------|--------------|
| Success | `Ack` | Marked processed for the durable consumer |
| Error policy: Skip | `Ack` | Skipped and not redelivered |
| Error policy: DeadLetter | Publish to dead-letter subject, then `Ack` | Available in DLQ subject |
| Error policy: Abort | Consumer stops without acknowledgement | Redelivered after `AckWait` |
| Crash (no ack) | — | Redelivered after `AckWait` |

Use a stable `ConsumerName` for durable resumption. Changing the consumer name creates a new durable consumer position.

## Azure Service Bus: PeekLock Settlement

### How It Works

Azure Service Bus supports durable queues and topic subscriptions. In the default `PeekLock` receive mode, the broker locks a message for a consumer. The transport completes the message after successful handling, dead-letters it when the error policy returns `DeadLetter`, dead-letters the current failed message and stops when the policy returns `Abort`, and leaves unsettled messages for redelivery if the process crashes before settlement.

### Configuration

```csharp
using Corvus.Text.Json.AsyncApi.AzureServiceBus;

AzureServiceBusTransportOptions queueOptions = new()
{
    ConnectionString = "<service-bus-connection-string>",
    QueueName = "sensor-readings",
    ReceiveMode = Azure.Messaging.ServiceBus.ServiceBusReceiveMode.PeekLock,
    MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
};

await using AzureServiceBusMessageTransport transport =
    await AzureServiceBusMessageTransport.CreateAsync(queueOptions);
```

For topic subscriptions:

```csharp
AzureServiceBusTransportOptions topicOptions = new()
{
    ConnectionString = "<service-bus-connection-string>",
    UseTopic = true,
    TopicName = "sensor-readings",
    SubscriptionName = "sensor-processor-v1",
};
```

### Settlement Behavior

| Handler Result | Action | Message Fate |
|---------------|--------|--------------|
| Success | Complete | Removed from queue/subscription |
| Error policy: Skip | Complete | Skipped and not redelivered |
| Error policy: DeadLetter | DeadLetter | Moved to the broker's dead-letter subqueue |
| Error policy: Abort | DeadLetter for current failure, then consumer stops | Available in dead-letter subqueue |
| Crash before settlement | — | Lock expires and message is redelivered |

Use stable queue names or topic subscription names for resumption. Enable sessions (`EnableSessions = true`) only when you need ordered session-aware processing and your Service Bus entity is configured for sessions.

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
- NATS JetStream: `UseJetStream` + `ConsumerName`
- Azure Service Bus: queue or topic subscription settlement

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

- [Example 037 — AsyncAPI Consumer](../ExampleRecipes/037-AsyncApiConsumer/) — Basic consumer setup
- [Example 038 — AsyncAPI End-to-End](../ExampleRecipes/038-AsyncApiEndToEnd/) — Producer + consumer integration
- [Example 039 — AsyncAPI Authentication](../ExampleRecipes/039-AsyncApiAuthentication/) — Auth for all transports