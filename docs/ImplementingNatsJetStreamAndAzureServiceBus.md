# Implementing NATS JetStream and Azure Service Bus Transports

This document outlines the implementation plan for two new AsyncAPI message transports:
1. **NATS JetStream** — durable message streaming for NATS
2. **Azure Service Bus** — Microsoft Azure's enterprise messaging service

## Architecture Overview

Both transports follow the same pattern as existing implementations (Kafka, AMQP, MQTT):

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

## 1. NATS JetStream Transport

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

### Package Dependencies

```xml
<PackageReference Include="NATS.Net" />
```

The `NATS.Net` client already supports JetStream. No additional packages needed.

### Project Structure

Create: `src/Corvus.Text.Json.AsyncApi.NatsJetStream/`

**Files:**
- `NatsJetStreamMessageTransport.cs` — Main transport implementation
- `NatsJetStreamTransportOptions.cs` — Configuration options
- `Corvus.Text.Json.AsyncApi.NatsJetStream.csproj` — Project file

### Key Implementation Details

#### Options Design

```csharp
public sealed class NatsJetStreamTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the NATS server URL.
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";
    
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
    
    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }
    
    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }
    
    /// <inheritdoc/>
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

JetStream subscriptions need:
1. **Create/verify stream exists** (or auto-create)
2. **Create/verify consumer exists** (durable consumer with resume capability)
3. **Subscribe to consumer** (fetch messages)
4. **Acknowledge messages** after successful processing

```csharp
public async ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
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
```

#### Publish Pattern

Publishing is simpler — just write to the stream:

```csharp
public async ValueTask PublishAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    in TPayload payload,
    in JsonElement headers,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    string subject = Encoding.UTF8.GetString(channelUtf8.Span);
    byte[] payloadBytes = SerializeToOwnedBytes(in payload);
    
    INatsJSContext js = await this.connection.CreateJetStreamContext(cancellationToken);
    
    PubAckResponse ack = await js.PublishAsync(
        subject,
        payloadBytes,
        cancellationToken: cancellationToken);
    
    // ack.Sequence tells you the stream sequence number assigned
}
```

### Testing Strategy

**Integration Tests** (`tests/Corvus.Text.Json.AsyncApi.Transport.IntegrationTests/`):

Create `NatsJetStreamTransportTests.cs` following the pattern in `KafkaTransportTests.cs`:

```csharp
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class NatsJetStreamTransportTests
{
    private static NatsContainer? container;
    private static NatsJetStreamMessageTransport? transport;
    
    [ClassInitialize]
    public static async Task SetupAsync(TestContext context)
    {
        // Use Testcontainers to spin up NATS with JetStream
        container = new NatsBuilder()
            .WithImage("nats:latest")
            .WithCommand("--js") // Enable JetStream
            .Build();
        
        await container.StartAsync();
        
        var options = new NatsJetStreamTransportOptions
        {
            Url = container.GetConnectionString(),
            DurableConsumerName = "test-consumer",
        };
        
        transport = new NatsJetStreamMessageTransport(options);
    }
    
    [TestMethod]
    public async Task PublishAndConsume_MessageDelivered()
    {
        // Test basic pub/sub
    }
    
    [TestMethod]
    public async Task ConsumerRestart_ResumesFromOffset()
    {
        // Publish 100 messages
        // Consume 50 messages
        // Stop consumer
        // Restart consumer
        // Verify remaining 50 are delivered (not all 100)
    }
    
    [TestMethod]
    public async Task HandlerException_MessageRedelivered()
    {
        // Test at-least-once delivery
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

```csharp
public async ValueTask SubscribeAsync<TPayload>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken)
    where TPayload : struct, IJsonElement<TPayload>
{
    string queueName = Encoding.UTF8.GetString(channelUtf8.Span);
    
    // Create receiver
    ServiceBusReceiver receiver = this.options.EnableSessions
        ? await this.client.AcceptNextSessionAsync(queueName, cancellationToken: cancellationToken)
        : this.client.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = this.options.ReceiveMode,
            PrefetchCount = this.options.PrefetchCount,
        });
    
    // Background task processes messages
    _ = Task.Run(async () =>
    {
        await foreach (ServiceBusReceivedMessage msg in receiver.ReceiveMessagesAsync(cancellationToken))
        {
            try
            {
                using ParsedJsonDocument<TPayload> doc = ParsedJsonDocument<TPayload>.Parse(msg.Body.ToArray());
                await handler(doc.RootElement, default, cancellationToken);
                
                // Complete message (remove from queue)
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
    public async Task PublishAndConsume_MessageDelivered()
    {
        // Test basic pub/sub
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

**Day 1-2: Project Setup**
- [ ] Create `Corvus.Text.Json.AsyncApi.NatsJetStream` project
- [ ] Add `NatsJetStreamTransportOptions` with all configuration
- [ ] Set up basic project structure matching Kafka pattern

**Day 3-5: Core Implementation**
- [ ] Implement `PublishAsync` with stream creation
- [ ] Implement `SubscribeAsync` with durable consumer
- [ ] Implement acknowledgement handling (Ack/Nak)
- [ ] Implement error policy integration

**Day 6-7: Testing**
- [ ] Add Testcontainers-based integration tests
- [ ] Test resumption behavior
- [ ] Test at-least-once delivery
- [ ] Test dead-lettering

**Day 8-10: Documentation & Polish**
- [ ] Add to `AsyncApiMessageResumption.md`
- [ ] Add example recipe (040-AsyncApiNatsJetStream)
- [ ] Update main AsyncAPI docs

### Phase 2: Azure Service Bus (Week 3-4)

**Day 1-2: Project Setup**
- [ ] Create `Corvus.Text.Json.AsyncApi.AzureServiceBus` project
- [ ] Add `AzureServiceBusTransportOptions` with auth patterns
- [ ] Set up basic project structure

**Day 3-5: Core Implementation**
- [ ] Implement `PublishAsync` with multiple auth modes
- [ ] Implement `SubscribeAsync` with PeekLock mode
- [ ] Implement Complete/Abandon/DeadLetter patterns
- [ ] Implement error policy integration

**Day 6-7: Advanced Features**
- [ ] Add session support
- [ ] Add scheduled message support
- [ ] Add duplicate detection configuration

**Day 8-9: Testing**
- [ ] Add integration tests (Azurite or real namespace)
- [ ] Test all auth modes
- [ ] Test dead-letter queue behavior
- [ ] Test session ordering

**Day 10: Documentation**
- [ ] Add to `AsyncApiMessageResumption.md`
- [ ] Add example recipe (041-AsyncApiAzureServiceBus)
- [ ] Add authentication guide for Azure scenarios

## Common Patterns to Follow

### 1. Serialization

Use the same pattern as other transports:

```csharp
private static byte[] SerializeToOwnedBytes<T>(in T value)
    where T : struct, IJsonElement<T>
{
    ArrayBufferWriter<byte> buffer = GetOrCreateBuffer();
    Utf8JsonWriter writer = GetOrCreateWriter(buffer);
    
    writer.Reset();
    value.WriteTo(writer);
    writer.Flush();
    
    return buffer.WrittenSpan.ToArray(); // Copy to owned array
}
```

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
- ✅ PeekLock mode receives and completes messages
- ✅ Handler exception triggers dead-lettering
- ✅ Managed Identity authentication works
- ✅ Session ordering works correctly
- ✅ Integration tests pass in CI (Azurite or real namespace)
- ✅ Documentation complete with auth examples

## Questions to Resolve

1. **NATS stream naming**: Should we auto-create streams per channel, or require explicit stream configuration?
   - **Recommendation**: Auto-create by default, with option to specify explicit stream name
   
2. **Azure Service Bus queue vs topic**: Should we support both, or just queues?
   - **Recommendation**: Start with queues (simpler), add topics in v2 if needed
   
3. **Azure Service Bus namespace**: Connection string or managed identity as default?
   - **Recommendation**: Support both, recommend managed identity in docs
   
4. **Testing in CI**: Use Testcontainers for NATS, but what about Azure Service Bus?
   - **Recommendation**: Use Azurite emulator if possible, or provision real namespace in CI

## Related Files

- `src/Corvus.Text.Json.AsyncApi/IMessageTransport.cs` — Interface to implement
- `src/Corvus.Text.Json.AsyncApi.Kafka/` — Reference implementation
- `tests/Corvus.Text.Json.AsyncApi.Transport.IntegrationTests/` — Test pattern
- `docs/AsyncApiMessageResumption.md` — Documentation to update