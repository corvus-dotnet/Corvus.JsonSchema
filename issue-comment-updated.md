# Implementation Plan: NATS JetStream and Azure Service Bus Transports

This plan outlines the implementation of two new AsyncAPI message transport capabilities with persistence and resumption.

## 📋 Full Plan Document

[ImplementingNatsJetStreamAndAzureServiceBus.md](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/feature/750-net-api-client-generator-from-openapi-corvus-alternative-to-kiota/docs/ImplementingNatsJetStreamAndAzureServiceBus.md)

## 🎯 Overview

### NATS JetStream Extension
Adds durable message storage and consumer offset tracking to the existing NATS transport:
- ✅ Persistent message storage (disk/memory)
- ✅ Consumer offsets (resume from last position)
- ✅ Replay historical messages
- ✅ At-least-once and exactly-once delivery
- ✅ Horizontal scaling with consumer groups

**Architecture Decision:** Extend existing `NatsMessageTransport` with `UseJetStream` flag rather than creating a separate package. Same server, same connection, just a feature toggle.

### Azure Service Bus (New Transport)
Microsoft's enterprise messaging with built-in enterprise features:
- ✅ Guaranteed message delivery with PeekLock
- ✅ **Queues** (point-to-point) and **Topics** (pub/sub)
- ✅ Dead-letter queues
- ✅ Message sessions (ordering)
- ✅ Three authentication modes (connection string, managed identity, service principal)

## 📦 Packages

**No new NATS package needed:**
- ✅ NATS JetStream → Extends `Corvus.Text.Json.AsyncApi.Nats` (add `UseJetStream` option)

**New package for Azure:**
- ✅ `Corvus.Text.Json.AsyncApi.AzureServiceBus`

## 🗓️ Implementation Roadmap

### Phase 1: NATS JetStream (2 weeks)

**Week 1: Core Implementation**
- Days 1-2: Add JetStream options to existing `NatsTransportOptions`
- Days 3-5: Implement JetStream code paths in PublishAsync/SubscribeAsync (branching on `UseJetStream`)
- Days 6-7: Add JetStream tests to existing `NatsTransportTests.cs` + regression tests

**Week 2: Polish & Documentation**
- Days 8-10: Documentation, example recipe, update AsyncApiMessageResumption.md

**Key Features:**
- Durable consumer with configurable `DeliverPolicy` (All, Last, New, ByStartSequence, ByStartTime)
- Auto-create streams per channel (configurable)
- File or memory storage
- Max delivery attempts before dead-lettering
- **Core NATS behavior unchanged when `UseJetStream = false`**

### Phase 2: Azure Service Bus (2 weeks)

**Week 1: Core Implementation**
- Days 1-2: Project setup + options with queue/topic mode
- Days 3-5: PublishAsync (unified), SubscribeAsync (queue + topic modes), Complete/Abandon/DeadLetter
- Days 6-7: Session support, scheduled messages, duplicate detection

**Week 2: Testing & Documentation**
- Days 8-9: Integration tests (queue mode, topic mode, competing consumers, auth modes)
- Day 10: Documentation + example recipe

**Key Features:**
- **Queue mode** (`UseTopic = false`): Point-to-point delivery
- **Topic mode** (`UseTopic = true`): Pub/sub with subscriptions
  - Multiple subscriptions → each receives all messages
  - Same subscription name → competing consumers (load balancing)
- PeekLock with auto-renewal
- Three auth patterns: connection string, `DefaultAzureCredential`, `TokenCredential`

## ✅ Success Criteria

### NATS JetStream
- ✅ Publish to stream succeeds
- ✅ Durable consumer receives messages
- ✅ Consumer restart resumes from last offset
- ✅ Handler exception triggers redelivery
- ✅ Error policy dead-lettering works
- ✅ Integration tests pass (Testcontainers)
- ✅ **Core NATS tests still pass (no regression)**
- ✅ Documentation complete

### Azure Service Bus
- ✅ Queue mode: point-to-point delivery
- ✅ Topic mode: multiple subscriptions receive messages
- ✅ Topic mode: competing consumers (same subscription)
- ✅ Handler exception triggers dead-lettering
- ✅ All three auth modes work
- ✅ Session ordering works
- ✅ Integration tests pass
- ✅ Documentation complete

## 🧪 Testing Strategy

- NATS: Extend existing `NatsTransportTests.cs` with JetStream tests + `nats:latest --js` container
- Azure Service Bus: New `AzureServiceBusTransportTests.cs` with Azurite emulator or real namespace in CI
- Test categories: `[TestCategory("integration")]`, `[TestCategory("docker")]`, `[TestCategory("azure")]`

## 📝 Architecture Notes

### NATS (Unified Transport)
- Extend existing `NatsMessageTransport.cs`
- Branch on `UseJetStream` in PublishAsync/SubscribeAsync
- When false: existing core NATS behavior (unchanged)
- When true: JetStream code path (new)
- Same `CreateAsync()` factory method

### Azure Service Bus (New Transport)
- Follow existing Kafka/AMQP patterns
- Use `CreateAsync()` factory method (async connection setup)
- Implement `IMessageTransport` and `IHealthCheckableTransport`
- Integrate with `IMessageErrorPolicy`
- Use thread-static buffer pooling for serialization
- Background task pattern for message processing loop

## 💡 Example Usage

### NATS Core (Existing, Unchanged)
```csharp
NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    UseJetStream = false,  // Default
};
await using var transport = await NatsMessageTransport.CreateAsync(options);
```

### NATS JetStream (New)
```csharp
NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    UseJetStream = true,  // Enable persistence
    DurableConsumerName = "my-consumer",
    DeliverPolicy = DeliverPolicy.All,
    StorageType = StorageType.File,
};
await using var transport = await NatsMessageTransport.CreateAsync(options);
```

### Azure Service Bus Queue
```csharp
AzureServiceBusTransportOptions options = new()
{
    FullyQualifiedNamespace = "myns.servicebus.windows.net",
    Credential = new DefaultAzureCredential(),
    UseTopic = false,  // Queue mode
};
await using var transport = await AzureServiceBusMessageTransport.CreateAsync(options);
```

### Azure Service Bus Topic
```csharp
AzureServiceBusTransportOptions options = new()
{
    FullyQualifiedNamespace = "myns.servicebus.windows.net",
    Credential = new DefaultAzureCredential(),
    UseTopic = true,  // Topic mode
    SubscriptionName = "my-subscription",
};
await using var transport = await AzureServiceBusMessageTransport.CreateAsync(options);
```

---

See the [full plan document](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/feature/750-net-api-client-generator-from-openapi-corvus-alternative-to-kiota/docs/ImplementingNatsJetStreamAndAzureServiceBus.md) for detailed code examples, options design, subscription patterns, and implementation notes.
