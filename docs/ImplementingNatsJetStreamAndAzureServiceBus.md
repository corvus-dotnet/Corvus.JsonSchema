# Implementing NATS JetStream and Azure Service Bus Transports

This document outlines the implementation plan for two new AsyncAPI message transports:
1. **NATS JetStream** — durable message streaming for NATS
2. **Azure Service Bus** — Microsoft Azure's enterprise messaging service

## 📦 Packages

**One new package:**

1. **NATS JetStream** — Extends existing `Corvus.Text.Json.AsyncApi.Nats` package (adds `UseJetStream` option)
2. **Azure Service Bus** — New package: `Corvus.Text.Json.AsyncApi.AzureServiceBus`

```
┌─────────────────────────────────────────────┐
│ Generated Producer/Consumer                 │
│   (from AsyncAPI spec)                      │
└────────────┬────────────────────────────────┘
             │ Depends on
             ↓
┌─────────────────────────────────────────────┐
│ IMessageTransport                           │
│   PublishAsync<T>()                         │
│   SubscribeAsync<T>()                       │
│   RequestAsync<T>()                         │
└────────────┬────────────────────────────────┘
             │ Implemented by
             ↓
┌─────────────────────────────────────────────┐
│ NatsJetStreamMessageTransport               │
│ AzureServiceBusMessageTransport             │
└─────────────────────────────────────────────┘
```

## 1. NATS JetStream Extension

### Why JetStream?

The current `NatsMessageTransport` uses **core NATS**, which has:
- ✅ Sub-millisecond latency
- ✅ Simple pub/sub
- ❌ **No message persistence** — offline consumers lose messages
- ❌ **No consumer offsets** — can't resume from where you left off

**NATS JetStream** adds:
- ✅ Durable message storage (disk/memory)
- ✅ Consumer offsets (resume on restart)
- ✅ Replay historical messages
- ✅ At-least-once and exactly-once delivery
- ✅ Horizontal scaling with consumer groups

### Architecture Decision: Unified Transport

**Extend the existing `NatsMessageTransport`** rather than creating a separate package. JetStream is a feature of the same NATS server, uses the same connection, and can be toggled via options.

### Package Dependencies

```xml
<PackageReference Include="NATS.Net" />
```

The `NATS.Net` client already supports JetStream. No additional packages needed.

### Project Structure

Extend: `src/Corvus.Text.Json.AsyncApi.Nats/`

**Modified Files:**
- `NatsMessageTransport.cs` — Add JetStream code paths
- `NatsTransportOptions.cs` — Add JetStream configuration options

### Key Implementation Details

#### Options Design

Add JetStream-specific properties to existing `NatsTransportOptions`:

```csharp
public sealed class NatsTransportOptions : ITransportOptions
{
    // Existing properties
    public string Url { get; set; } = "nats://localhost:4222";
    public string? Name { get; set; }
    public string DeadLetterSuffix { get; set; } = ".dead-letter";
    public string InboxPrefix { get; set; } = "_INBOX";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    // NEW: JetStream properties
    
    /// <summary>
    /// Gets or sets a value indicating whether to use JetStream for persistence.
    /// When false, uses core NATS (no persistence).
    /// </summary>
    public bool UseJetStream { get; set; }
    
    /// <summary>
    /// Gets or sets the JetStream stream name. If empty, uses channel name.
    /// </summary>
    public string? StreamName { get; set; }
    
    /// <summary>
    /// Gets or sets the durable consumer name for resumption.
    /// All consumers with the same name share offset state.
    /// </summary>
    public string DurableConsumerName { get; set; } = "corvus-asyncapi";
    
    /// <summary>
    /// Gets or sets the delivery policy for new consumers.
    /// </summary>
    public DeliverPolicy DeliverPolicy { get; set; } = DeliverPolicy.All;
    
    /// <summary>
    /// Gets or sets the acknowledgement wait timeout.
    /// </summary>
    public TimeSpan AckWait { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before dead-lettering.
    /// </summary>
    public int MaxDeliver { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets the stream storage type.
    /// </summary>
    public StorageType StorageType { get; set; } = StorageType.File;
    
    // Existing ITransportOptions
    public IMessageErrorPolicy? ErrorPolicy { get; set; }
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }
    public ProcessingLoopHeartbeat? Heartbeat { get; set; }
}

public enum DeliverPolicy
{
    All,              // Start from beginning
    Last,             // Start from most recent
    New,              // Start from next new message
    ByStartSequence,  // Start from specific sequence number
    ByStartTime,      // Start from specific timestamp
}

public enum StorageType
{
    File,    // Persistent to disk
    Memory,  // In-memory only (fast, volatile)
}
```

#### Subscription Pattern

The implementation branches based on `UseJetStream`:

**Core NATS** (UseJetStream = false, current behavior):
```csharp
public async ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    if (!this.options.UseJetStream)
    {
        // Existing core NATS subscription (no changes)
        string subject = Encoding.UTF8.GetString(channelUtf8.Span);
        await this.connection.SubscribeAsync(subject, handler, cancellationToken);
        return;
    }
    
    // JetStream path below...
}
```

**JetStream** (UseJetStream = true, new code):
```csharp
public async ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    if (this.options.UseJetStream)
    {
        string subject = Encoding.UTF8.GetString(channelUtf8.Span);
        
        // Get JetStream context
        INatsJSContext js = await this.connection.CreateJetStreamContext(cancellationToken);
        
        // Ensure stream exists
        await js.CreateStreamAsync(new StreamConfig
        {
            Name = this.options.StreamName ?? subject,
            Subjects = [subject],
            Storage = this.options.StorageType,
        }, cancellationToken);
        
        // Create durable consumer
        await js.CreateOrUpdateConsumerAsync(
            this.options.StreamName ?? subject,
            new ConsumerConfig
            {
                Name = this.options.DurableConsumerName,
                DurableConsumer = true,
                DeliverPolicy = this.options.DeliverPolicy,
                AckWait = this.options.AckWait,
                MaxDeliver = this.options.MaxDeliver,
            },
            cancellationToken);
        
        // Subscribe with consumer
        var consumer = await js.GetConsumerAsync(
            this.options.StreamName ?? subject,
            this.options.DurableConsumerName,
            cancellationToken);
        
        // Background task processes messages
        _ = Task.Run(async () =>
        {
            await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken))
            {
                try
                {
                    using ParsedJsonDocument<TPayload> doc = ParsedJsonDocument<TPayload>.Parse(msg.Data);
                    await handler(doc.RootElement, default, cancellationToken);
                    await msg.AckAsync(cancellationToken); // Commit offset
                }
                catch (Exception ex)
                {
                    // Error policy determines: retry, skip, dead-letter
                    // If skip: await msg.AckAsync()
                    // If retry: await msg.NakAsync(delay)
                }
            }
        }, cancellationToken);
    }
}
```

#### Publish Pattern

Publishing also branches based on `UseJetStream`:

**Core NATS** (existing behavior, unchanged):
```csharp
public async ValueTask PublishAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    in TPayload payload,
    in JsonElement headers,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    if (!this.options.UseJetStream)
    {
        // Existing core NATS publish (no changes)
        // Uses JsonElementSerializer<TPayload>.Instance for zero-allocation serialization
        string subject = Encoding.UTF8.GetString(channelUtf8.Span);
        await this.connection.PublishAsync(
            subject: subject,
            data: payload,
            serializer: JsonElementSerializer<TPayload>.Instance,
            cancellationToken: cancellationToken);
        return;
    }
    
    // JetStream path below...
}
```

**JetStream** (new code):
```csharp
public async ValueTask PublishAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    in TPayload payload,
    in JsonElement headers,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    if (this.options.UseJetStream)
    {
        string subject = Encoding.UTF8.GetString(channelUtf8.Span);
        
        INatsJSContext js = await this.connection.CreateJetStreamContext(cancellationToken);
        
        // JetStream also supports IBufferWriter-based serialization
        PubAckResponse ack = await js.PublishAsync(
            subject,
            payload,
            serializer: JsonElementSerializer<TPayload>.Instance,  // Zero-allocation
            cancellationToken: cancellationToken);
        
        // ack.Sequence tells you the stream sequence number assigned
    }
}
```

### Testing Strategy

**Integration Tests** (`tests/Corvus.Text.Json.AsyncApi.Transport.IntegrationTests/`):

Add JetStream tests to existing `NatsTransportTests.cs`:

```csharp
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class NatsTransportTests
{
    // Existing core NATS tests remain unchanged
    
    // NEW: JetStream-specific tests
    
    [TestMethod]
    public async Task JetStream_PublishAndConsume_MessageDelivered()
    {
        var options = new NatsTransportOptions
        {
            Url = container.GetConnectionString(),
            UseJetStream = true,
            DurableConsumerName = "test-consumer",
        };
        
        // Test basic pub/sub with persistence
    }
    
    [TestMethod]
    public async Task JetStream_ConsumerRestart_ResumesFromOffset()
    {
        // Publish 100 messages
        // Consume 50 messages
        // Stop consumer
        // Restart consumer
        // Verify remaining 50 are delivered (not all 100)
    }
    
    [TestMethod]
    public async Task JetStream_HandlerException_MessageRedelivered()
    {
        // Test at-least-once delivery
    }
    
    [TestMethod]
    public async Task CoreNats_NoJetStream_BehaviorUnchanged()
    {
        var options = new NatsTransportOptions
        {
            Url = container.GetConnectionString(),
            UseJetStream = false, // Core NATS
        };
        
        // Verify existing behavior still works
    }
}
```

## 2. Azure Service Bus Transport

### Why Azure Service Bus?

Azure Service Bus is Microsoft's enterprise messaging service with:
- ✅ Guaranteed message delivery
- ✅ At-least-once and at-most-once delivery
- ✅ Dead-letter queues
- ✅ Message sessions (ordering)
- ✅ Scheduled messages
- ✅ Duplicate detection
- ✅ Integrated with Azure ecosystem (Key Vault, Managed Identity)

### Package Dependencies

```xml
<PackageReference Include="Azure.Messaging.ServiceBus" />
<PackageReference Include="Azure.Identity" /> <!-- For managed identity auth -->
```

### Project Structure

Create: `src/Corvus.Text.Json.AsyncApi.AzureServiceBus/`

**Files:**
- `AzureServiceBusMessageTransport.cs` — Main transport implementation
- `AzureServiceBusTransportOptions.cs` — Configuration options
- `Corvus.Text.Json.AsyncApi.AzureServiceBus.csproj` — Project file

### Key Implementation Details

#### Options Design

```csharp
public sealed class AzureServiceBusTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the Service Bus namespace connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
    
    /// <summary>
    /// Gets or sets the fully qualified namespace (e.g., "myns.servicebus.windows.net").
    /// Used with managed identity or TokenCredential.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }
    
    /// <summary>
    /// Gets or sets the Azure token credential for authentication.
    /// </summary>
    public TokenCredential? Credential { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to use topics (true) or queues (false).
    /// Topics enable pub/sub with multiple subscriptions per channel.
    /// </summary>
    public bool UseTopic { get; set; }
    
    /// <summary>
    /// Gets or sets the subscription name when using topics.
    /// Multiple consumers with the same subscription name share messages (competing consumers).
    /// </summary>
    public string? SubscriptionName { get; set; }
    
    /// <summary>
    /// Gets or sets the default receive mode.
    /// </summary>
    public ServiceBusReceiveMode ReceiveMode { get; set; } = ServiceBusReceiveMode.PeekLock;
    
    /// <summary>
    /// Gets or sets the maximum auto-lock renewal duration.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets the prefetch count.
    /// </summary>
    public int PrefetchCount { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets a value indicating whether to use sessions.
    /// </summary>
    public bool EnableSessions { get; set; }
    
    /// <summary>
    /// Gets or sets the dead-letter queue suffix.
    /// </summary>
    public string DeadLetterSuffix { get; set; } = "/$DeadLetterQueue";
    
    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }
    
    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }
    
    /// <inheritdoc/>
    public ProcessingLoopHeartbeat? Heartbeat { get; set; }
}
```

#### Authentication Patterns

Support three auth modes:

```csharp
// 1. Connection string (simplest, not recommended for production)
var options = new AzureServiceBusTransportOptions
{
    ConnectionString = "Endpoint=sb://myns.servicebus.windows.net/;...",
};

// 2. Managed Identity (recommended for Azure-hosted workloads)
var options = new AzureServiceBusTransportOptions
{
    FullyQualifiedNamespace = "myns.servicebus.windows.net",
    Credential = new DefaultAzureCredential(),
};

// 3. Service Principal
var options = new AzureServiceBusTransportOptions
{
    FullyQualifiedNamespace = "myns.servicebus.windows.net",
    Credential = new ClientSecretCredential(tenantId, clientId, secret),
};
```

#### Subscription Pattern

Queue mode and topic mode differ in how they create receivers:

**Queue Mode** (UseTopic = false):
```csharp
public async ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    string queueName = Encoding.UTF8.GetString(channelUtf8.Span);
    
    // Create receiver for queue
    ServiceBusReceiver receiver = this.options.EnableSessions
        ? await this.client.AcceptNextSessionAsync(queueName, cancellationToken: cancellationToken)
        : this.client.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = this.options.ReceiveMode,
            PrefetchCount = this.options.PrefetchCount,
        });
    
    await ProcessMessagesAsync(receiver, handler, cancellationToken);
}
```

**Topic Mode** (UseTopic = true):
```csharp
public async ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    string topicName = Encoding.UTF8.GetString(channelUtf8.Span);
    string subscriptionName = this.options.SubscriptionName ?? "corvus-default";
    
    // Create receiver for topic subscription
    ServiceBusReceiver receiver = this.options.EnableSessions
        ? await this.client.AcceptNextSessionAsync(topicName, subscriptionName, cancellationToken: cancellationToken)
        : this.client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
        {
            ReceiveMode = this.options.ReceiveMode,
            PrefetchCount = this.options.PrefetchCount,
        });
    
    await ProcessMessagesAsync(receiver, handler, cancellationToken);
}

// Shared processing logic
private async Task ProcessMessagesAsync<TPayload>(
    ServiceBusReceiver receiver,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    // Background task processes messages
    _ = Task.Run(async () =>
    {
        await foreach (ServiceBusReceivedMessage msg in receiver.ReceiveMessagesAsync(cancellationToken))
        {
            try
            {
                using ParsedJsonDocument<TPayload> doc = ParsedJsonDocument<TPayload>.Parse(msg.Body.ToArray());
                await handler(doc.RootElement, default, cancellationToken);
                
                // Complete message (remove from queue/subscription)
                await receiver.CompleteMessageAsync(msg, cancellationToken);
            }
            catch (Exception ex)
            {
                // Error policy determines action
                // Skip: await receiver.CompleteMessageAsync(msg)
                // DeadLetter: await receiver.DeadLetterMessageAsync(msg)
                // Retry: await receiver.AbandonMessageAsync(msg)
            }
        }
    }, cancellationToken);
}
```

#### Publish Pattern

Publishing differs slightly between queue and topic mode:

**Queue Mode:**
```csharp
public async ValueTask PublishAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    in TPayload payload,
    in JsonElement headers,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    string queueName = Encoding.UTF8.GetString(channelUtf8.Span);
    byte[] payloadBytes = SerializeToOwnedBytes(in payload);
    
    ServiceBusSender sender = this.client.CreateSender(queueName);
    
    var message = new ServiceBusMessage(payloadBytes)
    {
        ContentType = "application/json",
    };
    
    // Add headers as application properties
    if (headers.ValueKind != JsonValueKind.Undefined)
    {
        foreach (JsonProperty prop in headers.EnumerateObject())
        {
            message.ApplicationProperties[prop.Name] = prop.Value.ToString();
        }
    }
    
    await sender.SendMessageAsync(message, cancellationToken);
}
```

**Topic Mode:**
Same implementation, but `channelUtf8` is interpreted as topic name instead of queue name. The sender creation is identical — `CreateSender()` works for both queues and topics.

### Testing Strategy

**Integration Tests** (`tests/Corvus.Text.Json.AsyncApi.Transport.IntegrationTests/`):

Create `AzureServiceBusTransportTests.cs`:

```csharp
[TestClass]
[TestCategory("integration")]
[TestCategory("azure")]
public class AzureServiceBusTransportTests
{
    // Use Azurite for local testing, or real Service Bus namespace in CI
    
    [TestMethod]
    public async Task QueueMode_PublishAndConsume_MessageDelivered()
    {
        // Test basic queue pub/sub
    }
    
    [TestMethod]
    public async Task TopicMode_PublishAndConsume_AllSubscriptionsReceive()
    {
        // Test topic with multiple subscriptions
        // Verify each subscription receives the message
    }
    
    [TestMethod]
    public async Task TopicMode_CompetingConsumers_MessageReceivedOnce()
    {
        // Two consumers with same subscription name
        // Verify only one receives each message
    }
    
    [TestMethod]
    public async Task HandlerException_MessageDeadLettered()
    {
        // Test dead-letter queue behavior
    }
    
    [TestMethod]
    public async Task SessionEnabled_MessagesOrderedBySessionId()
    {
        // Test message sessions
    }
}
```

## Implementation Roadmap

### Phase 1: NATS JetStream (Week 1-2)

**Day 1-2: Options & Design**
- [ ] Add JetStream properties to existing `NatsTransportOptions`
- [ ] Add `DeliverPolicy` and `StorageType` enums
- [ ] Design branching logic (if UseJetStream else core NATS)

**Day 3-5: Core Implementation**
- [ ] Implement JetStream path in `PublishAsync` (stream creation + publish)
- [ ] Implement JetStream path in `SubscribeAsync` (durable consumer + fetch)
- [ ] Implement acknowledgement handling (Ack/Nak)
- [ ] Ensure core NATS behavior unchanged when `UseJetStream = false`

**Day 6-7: Testing**
- [ ] Add JetStream tests to existing `NatsTransportTests.cs`
- [ ] Test resumption behavior
- [ ] Test at-least-once delivery
- [ ] Test dead-lettering
- [ ] Regression test: core NATS still works

**Day 8-10: Documentation & Polish**
- [ ] Update `AsyncApiMessageResumption.md` with JetStream section
- [ ] Add example recipe (040-AsyncApiNatsJetStream)
- [ ] Update main AsyncAPI docs with UseJetStream option

### Phase 2: Azure Service Bus (Week 3-4)

**Day 1-2: Project Setup**
- [ ] Create `Corvus.Text.Json.AsyncApi.AzureServiceBus` project
- [ ] Add `AzureServiceBusTransportOptions` with auth patterns
- [ ] Set up basic project structure

**Day 3-5: Core Implementation**
- [ ] Implement `PublishAsync` (works for both queues and topics)
- [ ] Implement `SubscribeAsync` with queue mode (PeekLock)
- [ ] Implement `SubscribeAsync` with topic mode (topic + subscription)
- [ ] Implement Complete/Abandon/DeadLetter patterns
- [ ] Implement error policy integration

**Day 6-7: Advanced Features**
- [ ] Add session support
- [ ] Add scheduled message support
- [ ] Add duplicate detection configuration

**Day 8-9: Testing**
- [ ] Add integration tests (Azurite or real namespace)
- [ ] Test all auth modes (connection string, managed identity, service principal)
- [ ] Test queue mode (point-to-point)
- [ ] Test topic mode (pub/sub with multiple subscriptions)
- [ ] Test competing consumers (same subscription name)
- [ ] Test dead-letter queue behavior
- [ ] Test session ordering

**Day 10: Documentation**
- [ ] Add to `AsyncApiMessageResumption.md`
- [ ] Add example recipe (041-AsyncApiAzureServiceBus)
- [ ] Add authentication guide for Azure scenarios

## Common Patterns to Follow

### 1. Serialization

**NATS** (use existing custom serializer, writes directly to buffer):
```csharp
// No serialization needed — NATS uses JsonElementSerializer<TPayload>.Instance
// which writes directly to IBufferWriter<byte> (zero allocation)

await jsContext.PublishAsync(
    subject,
    payload,  // Pass the payload directly, let serializer handle it
    serializer: JsonElementSerializer<TPayload>.Instance,
    cancellationToken: cancellationToken);
```

**Azure Service Bus** (requires owned byte array):
```csharp
private static byte[] SerializeToOwnedBytes<T>(in T value)
    where T : struct, IJsonElement<T>
{
    ArrayBufferWriter<byte> buffer = GetOrCreateThreadStaticBuffer();
    Utf8JsonWriter writer = GetOrCreateThreadStaticWriter(buffer);
    
    writer.Reset();
    value.WriteTo(writer);
    writer.Flush();
    
    return buffer.WrittenSpan.ToArray(); // Azure SDK requires owned array
}
```

**Note:** Azure Service Bus's `ServiceBusMessage` constructor requires an owned `byte[]` or `BinaryData`. Unlike NATS which provides `IBufferWriter<byte>`, we must allocate. The `ToArray()` copy is unavoidable with the current Azure SDK API.

### 2. Background Processing Loop

Follow the Kafka pattern for background message processing:

```csharp
private sealed class SubscriptionState
{
    public Task ProcessingTask { get; init; } = null!;
    public CancellationTokenSource CancellationSource { get; init; } = null!;
    public Delegate Handler { get; init; } = null!;
}

private readonly ConcurrentDictionary<string, SubscriptionState> subscriptions = new();
```

### 3. Error Policy Integration

Always integrate with `IMessageErrorPolicy`:

```csharp
try
{
    await handler(payload, headers, cancellationToken);
    // Acknowledge success
}
catch (Exception ex)
{
    MessageErrorContext context = new(channelUtf8, MessageErrorKind.Handler, payload, headers);
    MessageErrorAction action = await this.errorPolicy.HandleErrorAsync(ex, context, cancellationToken);
    
    switch (action)
    {
        case MessageErrorAction.Skip:
            // Acknowledge to remove from queue
            break;
        case MessageErrorAction.Retry:
            // Nack/Abandon for redelivery
            break;
        case MessageErrorAction.DeadLetter:
            // Send to dead-letter queue
            break;
    }
}
```

### 4. Health Checks

Implement `IHealthCheckableTransport`:

```csharp
public interface IHealthCheckableTransport
{
    bool IsConnected { get; }
    string MessagingSystem { get; }
    ValueTask<bool> PingAsync(CancellationToken cancellationToken = default);
}
```

### 5. Polly Resilience Integration

Both transports support resilience strategies via the existing `Corvus.Text.Json.AsyncApi.Polly` package:

```csharp
using Corvus.Text.Json.AsyncApi.Polly;
using Polly;

// Configure retry + circuit breaker + timeout
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        BreakDuration = TimeSpan.FromSeconds(30),
    })
    .AddTimeout(TimeSpan.FromSeconds(10))
    .Build();

// Apply to NATS JetStream
var natsOptions = new NatsTransportOptions
{
    UseJetStream = true,
    HandlerMiddleware = PollyResilienceMiddleware.Create(pipeline),
};

// Apply to Azure Service Bus
var asbOptions = new AzureServiceBusTransportOptions
{
    UseTopic = true,
    HandlerMiddleware = PollyResilienceMiddleware.Create(pipeline),
};
```

**Key resilience patterns:**
- **Retry** — Transient failures (network glitches, temporary unavailability)
- **Circuit Breaker** — Prevent cascading failures when downstream services fail
- **Timeout** — Prevent indefinite hangs on slow operations
- **Rate Limiter** — Control message processing throughput
- **Hedging** — Send duplicate requests after a delay to reduce tail latency

The `HandlerMiddleware` wraps the user's message handler, applying resilience strategies before the transport's acknowledgement logic.

## Success Criteria

### NATS JetStream
- ✅ Publish to stream succeeds
- ✅ Durable consumer receives messages
- ✅ Consumer restart resumes from last offset (no message loss or duplication)
- ✅ Handler exception triggers redelivery
- ✅ Error policy dead-lettering works
- ✅ Integration tests pass in CI (Testcontainers)
- ✅ Documentation complete with examples

### Azure Service Bus
- ✅ Publish to queue succeeds
- ✅ Publish to topic succeeds
- ✅ Queue mode: PeekLock receives and completes messages
- ✅ Topic mode: Multiple subscriptions each receive published messages
- ✅ Topic mode: Competing consumers (same subscription) receive each message once
- ✅ Handler exception triggers dead-lettering
- ✅ Managed Identity authentication works
- ✅ Session ordering works correctly
- ✅ Integration tests pass in CI (Azurite or real namespace)
- ✅ Documentation complete with auth examples and queue/topic patterns

## Questions to Resolve

1. **NATS stream naming**: Should we auto-create streams per channel, or require explicit stream configuration?
   - **Recommendation**: Auto-create by default, with option to specify explicit stream name via `StreamName` property
   
2. **NATS JetStream as separate package or unified?**
   - **Decision**: Unified transport with `UseJetStream` flag (same server, same connection, just a feature toggle)
   
3. **Azure Service Bus queues and topics**: Both are required. How to determine which to use?
   - **Recommendation**: Add `UseTopic` boolean option. When true, use topics with subscription name; when false, use queues
   
4. **Azure Service Bus namespace**: Connection string or managed identity as default?
   - **Recommendation**: Support both, recommend managed identity in docs
   
5. **Testing in CI**: Use Testcontainers for NATS, but what about Azure Service Bus?
   - **Recommendation**: Use Azurite emulator if possible, or provision real namespace in CI

## Related Files

- `src/Corvus.Text.Json.AsyncApi/IMessageTransport.cs` — Interface to implement
- `src/Corvus.Text.Json.AsyncApi.Nats/` — Existing NATS implementation to extend
- `src/Corvus.Text.Json.AsyncApi.Kafka/` — Reference implementation for Azure Service Bus
- `tests/Corvus.Text.Json.AsyncApi.Transport.IntegrationTests/` — Test pattern
- `docs/AsyncApiMessageResumption.md` — Documentation to update