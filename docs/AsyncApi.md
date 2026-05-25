# AsyncAPI Code Generation

## Overview

`Corvus.Text.Json` includes a code generator that produces strongly-typed **producers** and **consumers** from [AsyncAPI](https://www.asyncapi.com/) specifications (versions 2.6 and 3.0).

Both sides are generated from the same spec. The generated code handles all the messaging plumbing — payload serialization, JSON Schema validation, header encoding, channel address construction, error handling, and dead-letter routing — so you focus purely on business logic.

The generator leverages the Corvus.JsonSchema V5 engine for model generation, producing zero-allocation, pooled-memory types with full JSON Schema validation built in.

## Installation

```bash
# Install the CLI tool globally
dotnet tool install --global Corvus.Json.Cli

# Or as a local tool
dotnet tool install Corvus.Json.Cli
```

For consumer/producer projects, add the runtime package:

```bash
dotnet add package Corvus.Text.Json.AsyncApi
```

Add a transport implementation:

```bash
# Choose one (or more) for your broker:
dotnet add package Corvus.Text.Json.AsyncApi.Nats
dotnet add package Corvus.Text.Json.AsyncApi.Kafka
dotnet add package Corvus.Text.Json.AsyncApi.Amqp
dotnet add package Corvus.Text.Json.AsyncApi.Mqtt
dotnet add package Corvus.Text.Json.AsyncApi.WebSocket
```

Both also need the core library:

```bash
dotnet add package Corvus.Text.Json
```

## Quick Start — Producer

Generate a typed producer from any AsyncAPI spec:

```bash
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Client \
    --outputPath ./Generated \
    --mode producer
```

Use it with just a few lines:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Nats;
using Streetlights.Client;

await using NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(
    new NatsTransportOptions { Url = "nats://localhost:4222" });

TurnOnProducer producer = new(transport);

// Publish a validated message — schema validation runs before the message leaves your process
TurnOnOffPayload payload = TurnOnOffPayload.ParseValue(
    """{"command":"on","sentAt":"2024-01-15T10:30:00Z"}"""u8);

await producer.PublishTurnOnOffAsync(payload, streetlightId: "lamp-001");
```

## Quick Start — Consumer

Generate a typed consumer:

```bash
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Client \
    --outputPath ./Generated \
    --mode consumer
```

Implement your message handler and start consuming:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Nats;
using Streetlights.Client;

await using NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(
    new NatsTransportOptions { Url = "nats://localhost:4222" });

LightMeasuredHandler handler = new();
await using ReceiveLightMeasurementConsumer consumer = new(transport, handler);

await consumer.StartAsync();
// Consumer is now running — messages arrive at handler.HandleLightMeasuredAsync
// Press Ctrl+C or dispose to stop
await consumer.StopAsync();
```

Implement your business logic:

```csharp
internal sealed class LightMeasuredHandler : IReceiveLightMeasurementHandler
{
    public ValueTask HandleLightMeasuredAsync(
        LightMeasuredPayload payload,
        CancellationToken cancellationToken = default)
    {
        // Payload is already validated — lumens >= 0 is guaranteed
        int lumens = (int)payload.Lumens;
        Console.WriteLine($"Light measured: {lumens} lumens");
        return default;
    }
}
```

## What the Generator Produces

### Producer Generation (`--mode producer`)

| Generated artifact | What it handles |
|---|---|
| **Producer class** (e.g., `TurnOnProducer`) | Orchestrates publish lifecycle: builds payload, validates against JSON Schema, constructs channel address, publishes via transport |
| **Message types** (e.g., `TurnOnTurnOnOffMessage`) | Message-level metadata and content type constants |
| **Model types** (e.g., `TurnOnOffPayload`) | Strongly-typed JSON Schema models with validation, zero-allocation access, and builder patterns |
| **Lock file** (`corvusjson-asyncapi.lock`) | Tracks spec hash for incremental regeneration |

### Consumer Generation (`--mode consumer`)

| Generated artifact | What it handles |
|---|---|
| **Consumer class** (e.g., `ReceiveLightMeasurementConsumer`) | Manages subscription lifecycle: subscribes to channel, deserializes payloads, validates, dispatches to handler, handles errors |
| **Handler interface** (e.g., `IReceiveLightMeasurementHandler`) | One async method per message — you implement business logic here |
| **Model types** | Same models as producer generation |
| **Lock file** | Tracks spec hash for incremental regeneration |

### Both (`--mode both`, the default)

Generates producers for `send` operations and consumers for `receive` operations in a single pass.

## Generated Code Architecture

### Producer Flow

```
Your Code → Producer.PublishAsync(payload, channelParams...)
  → Generated Workspace: build typed payload from Source
  → Generated Validation: validate payload against JSON Schema
  → Channel Address: construct from template + parameters (UTF-8)
  → Authentication: call IMessageAuthenticationProvider
  → IMessageTransport.PublishAsync: serialize and send
  → Cleanup: return workspace + channel rental to pool
```

### Consumer Flow

```
Message arrives on transport
  → IMessageTransport: parse bytes into ParsedJsonDocument<TPayload>
  → Generated Consumer.HandleMessageAsync: receive typed payload
  → Generated Validation: validate payload (and optionally headers)
  → Your Handler: receives strongly-typed, validated payload
  → On error: IMessageErrorPolicy decides Skip/DeadLetter/Abort
```

## Validation

Every generated producer and consumer accepts a `ValidationMode` parameter that controls how strictly messages are validated against their JSON Schema.

### Validation Modes

| Mode | Behaviour | Overhead | Use for |
|------|-----------|----------|---------|
| `ValidationMode.None` | No validation | Zero | Trusted internal services, maximum throughput |
| `ValidationMode.Basic` | Fast boolean `EvaluateSchema()` check; on failure throws with message name but no details | ~300ns | Production default — catches malformed messages |
| `ValidationMode.Detailed` | Full evaluation with `JsonSchemaResultsCollector`; on failure includes JSON diagnostics with evaluation paths and error messages | ~600ns | Development, debugging, API gateways |

### Producer-Side Validation

Validation runs **before** the message leaves your process:

```csharp
// Basic validation (default) — catches schema violations before publish
TurnOnProducer producer = new(transport, validationMode: ValidationMode.Basic);

try
{
    TurnOnOffPayload payload = TurnOnOffPayload.ParseValue(
        """{"command":"invalid-command","sentAt":"2024-01-15T10:30:00Z"}"""u8);

    await producer.PublishTurnOnOffAsync(payload, streetlightId: "lamp-001");
}
catch (ArgumentException ex)
{
    // "Message payload validation failed for 'payload'."
    Console.WriteLine(ex.Message);
}
```

### Consumer-Side Validation

Validation runs after deserialization, before your handler is called:

```csharp
// Detailed validation — full diagnostics on failure
ReceiveLightMeasurementConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Detailed);
```

If validation fails, the error policy determines what happens (skip, dead-letter, or abort). Your handler is never called with invalid data.

## Error Handling

### Error Policies

When message processing fails (validation error, handler exception, transport issue), the `IMessageErrorPolicy` decides the terminal action:

| Action | Behaviour |
|--------|-----------|
| `MessageErrorAction.Skip` | Discard the message and continue processing the next one |
| `MessageErrorAction.DeadLetter` | Publish the failed message to a dead-letter channel for later inspection |
| `MessageErrorAction.Abort` | Stop the consumer subscription entirely |

### Default Policy

The `DefaultMessageErrorPolicy` applies sensible defaults:

| Error kind | Default action |
|---|---|
| Deserialization failure | Dead-letter |
| Handler exception | Dead-letter |
| Transport connectivity error | Abort |

```csharp
// Customise: skip deserialization errors, dead-letter handler errors, abort on transport issues
IMessageErrorPolicy policy = new DefaultMessageErrorPolicy(
    deserializationAction: MessageErrorAction.Skip,
    handlerAction: MessageErrorAction.DeadLetter,
    transportAction: MessageErrorAction.Abort);

ReceiveLightMeasurementConsumer consumer = new(
    transport, handler,
    errorPolicy: policy);
```

### Custom Error Policies

Implement `IMessageErrorPolicy` for complex routing logic:

```csharp
internal sealed class RetryThenDeadLetterPolicy : IMessageErrorPolicy
{
    private readonly int maxAttempts;
    private int attempts;

    public RetryThenDeadLetterPolicy(int maxAttempts = 3)
    {
        this.maxAttempts = maxAttempts;
    }

    public ValueTask<MessageErrorAction> HandleErrorAsync(
        Exception exception,
        MessageErrorContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ErrorKind == MessageErrorKind.Transport)
        {
            return new(MessageErrorAction.Abort);
        }

        this.attempts++;
        MessageErrorAction action = this.attempts >= this.maxAttempts
            ? MessageErrorAction.DeadLetter
            : MessageErrorAction.Skip;

        return new(action);
    }
}
```

### Dead-Letter Channels

Dead-lettered messages are published to a derived channel address (e.g., `dead-letter.smartylighting.streetlights.1.0.action.{id}.lighting.measured`). The dead-letter message includes:
- The original payload bytes
- The original headers
- The exception that caused the failure
- The original channel address

## Resilience (Polly Integration)

The `Corvus.Text.Json.AsyncApi.Polly` package provides handler middleware backed by [Polly](https://github.com/App-vNext/Polly) resilience pipelines.

```bash
dotnet add package Corvus.Text.Json.AsyncApi.Polly
```

### Configuring Retry with Circuit Breaker

```csharp
using Corvus.Text.Json.AsyncApi.Polly;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;

ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromMilliseconds(200),
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30),
    })
    .Build();

NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    HandlerMiddleware = PollyResilienceMiddleware.Create(pipeline),
};

await using NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(options);
```

The middleware wraps every handler invocation. If all retries are exhausted and the operation still fails, the exception propagates to the `IMessageErrorPolicy` for terminal action (dead-letter, skip, or abort).

## Transport Implementations

All transports implement `IMessageTransport` and can be swapped without changing your producer/consumer code.

| Package | Broker | Protocol |
|---------|--------|----------|
| `Corvus.Text.Json.AsyncApi.Nats` | NATS | NATS (JetStream compatible) |
| `Corvus.Text.Json.AsyncApi.Kafka` | Apache Kafka | Kafka |
| `Corvus.Text.Json.AsyncApi.Amqp` | RabbitMQ | AMQP 0-9-1 |
| `Corvus.Text.Json.AsyncApi.Mqtt` | Mosquitto, HiveMQ, etc. | MQTT 3.1.1 / 5.0 |
| `Corvus.Text.Json.AsyncApi.WebSocket` | Any WebSocket server | WebSocket |
| `Corvus.Text.Json.AsyncApi.Testing` | In-memory (tests only) | None |

### NATS

```csharp
using Corvus.Text.Json.AsyncApi.Nats;

NatsTransportOptions options = new()
{
    Url = "nats://broker.example.com:4222",
    Name = "streetlights-service",
    RequestTimeout = TimeSpan.FromSeconds(10),
};

await using NatsMessageTransport transport = await NatsMessageTransport.CreateAsync(options);
```

### Kafka

```csharp
using Corvus.Text.Json.AsyncApi.Kafka;

KafkaTransportOptions options = new()
{
    BootstrapServers = "kafka.example.com:9092",
    GroupId = "streetlights-consumer-group",
    AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest,
};

await using KafkaMessageTransport transport = new(options);
```

### AMQP (RabbitMQ)

```csharp
using Corvus.Text.Json.AsyncApi.Amqp;

AmqpTransportOptions options = new()
{
    ConnectionUri = "amqp://guest:guest@rabbitmq.example.com:5672/",
    ExchangeName = "streetlights",
    ExchangeType = "topic",
};

await using AmqpMessageTransport transport = await AmqpMessageTransport.CreateAsync(options);
```

### MQTT

```csharp
using Corvus.Text.Json.AsyncApi.Mqtt;

MqttTransportOptions options = new()
{
    Host = "mqtt.example.com",
    Port = 1883,
    ClientId = "streetlights-publisher",
    QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
};

await using MqttMessageTransport transport = await MqttMessageTransport.CreateAsync(options);
```

### WebSocket

```csharp
using Corvus.Text.Json.AsyncApi.WebSocket;

WebSocketTransportOptions options = new()
{
    ServerUri = "wss://ws.example.com/events",
};

await using WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(options);
```

### In-Memory (Testing)

The `Corvus.Text.Json.AsyncApi.Testing` package provides an in-memory transport for unit and integration tests:

```bash
dotnet add package Corvus.Text.Json.AsyncApi.Testing
```

```csharp
using Corvus.Text.Json.AsyncApi.Testing;

InMemoryMessageTransport transport = new();

// Use with producers — messages are captured for assertions
TurnOnProducer producer = new(transport, ValidationMode.None);

TurnOnOffPayload payload = TurnOnOffPayload.ParseValue(
    """{"command":"on","sentAt":"2024-01-15T10:30:00Z"}"""u8);

await producer.PublishTurnOnOffAsync(payload, streetlightId: "lamp-001");

// Assert published messages
Assert.AreEqual(1, transport.PublishedMessages.Count);

// Deliver messages to consumers for testing
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    """{"lumens":250,"sentAt":"2024-01-15T10:30:00Z"}"""u8.ToArray());
```

## Authentication

Generated producers and consumers accept an optional `IMessageAuthenticationProvider`. The provider supplies credentials to the transport before messages are published or subscriptions are established.

The spec's `securitySchemes` determine which provider to use:

### Bearer Token

```csharp
using Corvus.Text.Json.AsyncApi;

IMessageAuthenticationProvider auth = new BearerTokenAuthenticationProvider("my-jwt-token");

TurnOnProducer producer = new(transport, authProvider: auth);
```

### API Key

```csharp
IMessageAuthenticationProvider auth = new ApiKeyAuthenticationProvider(
    key: "my-api-key",
    name: "X-API-Key",
    location: "header");

ReceiveLightMeasurementConsumer consumer = new(
    transport, handler, authProvider: auth);
```

### OAuth 2.0

For static tokens:

```csharp
IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
    accessToken: "eyJhbGciOi...",
    tokenType: "Bearer",
    scopes: "read:messages write:messages");
```

For dynamic token acquisition (client credentials flow, token refresh):

```csharp
IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
    accessTokenFactory: async ct =>
    {
        // Acquire token from your identity provider
        TokenResponse token = await identityClient.GetClientCredentialsTokenAsync(ct);
        return token.AccessToken;
    },
    tokenType: "Bearer",
    scopes: "read:messages");
```

### Username/Password (SASL)

```csharp
IMessageAuthenticationProvider auth = new UserPasswordAuthenticationProvider(
    username: "service-account",
    password: "secret");
```

### Client Certificate

```csharp
using System.Security.Cryptography.X509Certificates;

X509Certificate2 cert = X509Certificate2.CreateFromPemFile("client.pem", "client-key.pem");
IMessageAuthenticationProvider auth = new CertificateAuthenticationProvider(cert);
```

### Composite (Multiple Schemes)

When your spec declares multiple security schemes, use `CompositeAuthenticationProvider` to delegate to the correct provider based on the scheme type:

```csharp
IMessageAuthenticationProvider auth = new CompositeAuthenticationProvider(
    new BearerTokenAuthenticationProvider("my-token"),
    new ApiKeyAuthenticationProvider("key-123"));

// The generated code calls AuthenticateAsync with a context describing
// which security scheme is required — the composite provider routes
// to the correct inner provider automatically.
```

## Channel Parameters

AsyncAPI channels can contain parameters (e.g., `smartylighting.streetlights.1.0.action.{streetlightId}.turn.on`). The generated producer exposes these as method parameters:

```csharp
// The streetlightId parameter is part of the publish method signature
await producer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-42");
// Wire: publishes to "smartylighting.streetlights.1.0.action.lamp-42.turn.on"
```

The generated code constructs the channel address from the template using zero-allocation UTF-8 byte manipulation with pooled buffers — no string concatenation or allocation on the hot path.

## Message Headers

When an AsyncAPI message defines a `headers` schema, the generated code produces typed header structures:

```json
{
  "messages": {
    "lightMeasured": {
      "headers": {
        "type": "object",
        "properties": {
          "correlationId": { "type": "string" },
          "version": { "type": "integer" }
        },
        "required": ["correlationId"]
      },
      "payload": { "$ref": "#/components/schemas/LightMeasuredPayload" }
    }
  }
}
```

Headers are validated alongside the payload when `ValidationMode` is `Basic` or `Detailed`. The transport encodes headers as a JSON object using a pooled `Utf8JsonWriter`, keeping allocation constant regardless of header count.

## Request/Reply

AsyncAPI operations can model request/reply patterns. The generator produces methods that send a request and await a correlated response:

```csharp
// Generated request/reply method
(QueryResponse reply, JsonElement replyHeaders) = await queryProducer.RequestQueryAsync(
    request: new QueryPayload.Source((ref QueryPayload.Builder b) =>
    {
        b.Create(filter: "status=active"u8);
    }),
    cancellationToken: ct);

// reply is already deserialized and validated
foreach (var item in reply.Results.EnumerateArray())
{
    Console.WriteLine($"Found: {item.Name}");
}
```

The generated code handles correlation ID generation (GUID formatted directly to a `byte[36]` — no string allocation), request/reply channel pairing, and timeout management.

## Bindings

AsyncAPI bindings provide protocol-specific configuration. The generator captures bindings at three levels and passes them to the transport via `MessageContext`:

| Level | Example | What it configures |
|-------|---------|-------------------|
| Channel | Kafka topic configuration | Partition count, retention |
| Operation | Kafka consumer group | Group ID, offset reset |
| Message | Kafka message key | Partition key, headers |

```json
{
  "channels": {
    "events": {
      "bindings": {
        "kafka": {
          "topic": "streetlights.events",
          "partitions": 12,
          "replicas": 3
        }
      }
    }
  }
}
```

The generated code emits bindings as static `ReadOnlyMemory<byte>` constants in the producer/consumer, passing them to the transport in the `MessageContext`:

```csharp
// Transport receives bindings in the context — parse as needed
MessageContext context = new()
{
    ContentType = "application/json",
    ChannelBindingsJson = /* static UTF-8 JSON bytes from the spec */,
    OperationBindingsJson = /* ... */,
    MessageBindingsJson = /* ... */,
};
```

Transport implementations inspect the relevant bindings to configure delivery semantics (partition keys, exchanges, QoS levels, delivery modes, etc.).

## Telemetry and Observability

### OpenTelemetry Distributed Tracing

Wrap any transport with `InstrumentedMessageTransport` to gain automatic OpenTelemetry instrumentation:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Nats;

NatsMessageTransport raw = await NatsMessageTransport.CreateAsync(
    new NatsTransportOptions { Url = "nats://localhost:4222" });

InstrumentedMessageTransport transport = new(raw, "nats");
```

This provides:
- **Distributed trace spans** (Activities) for publish, subscribe, request, and dead-letter operations
- **Metrics** — counters and histograms for message throughput, processing duration, and errors
- **W3C trace context propagation** — `traceparent`/`tracestate` headers are injected on publish and extracted on consume, linking producer and consumer traces automatically

All instrumentation is zero-cost when no listener (e.g., OTLP exporter) is attached.

### Processing Loop Heartbeat

The `ProcessingLoopHeartbeat` monitors consumer liveness by tracking heartbeat ticks from each subscription's processing loop:

```csharp
ProcessingLoopHeartbeat heartbeat = new();

NatsTransportOptions options = new()
{
    Url = "nats://localhost:4222",
    Heartbeat = heartbeat,
};

// After starting consumers, check liveness:
bool alive = heartbeat.IsAlive("smartylighting.streetlights.1.0.action.*.lighting.measured");

// Get all subscription statuses:
foreach (var status in heartbeat.GetSubscriptionStatuses())
{
    Console.WriteLine($"{status.Channel}: {(status.IsAlive ? "alive" : "STALE")}");
}
```

If a loop has not ticked for longer than the staleness threshold (default: 30 seconds), it is considered dead. This catches loops that exit silently due to unhandled exceptions or unexpected cancellation.

### Health Checks

The `Corvus.Text.Json.AsyncApi.HealthChecks` package integrates with ASP.NET Core health checks:

```bash
dotnet add package Corvus.Text.Json.AsyncApi.HealthChecks
```

```csharp
using Corvus.Text.Json.AsyncApi.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    .AddAsyncApiTransport("nats-transport", transport);

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
```

The health check monitors:
- Transport connectivity
- Processing loop heartbeat (staleness detection)
- Dead-letter accumulation

## CLI Reference

### Generate Code (`asyncapi-generate`)

```bash
corvusjson asyncapi-generate <specFile> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<specFile>` | Path to the AsyncAPI specification (JSON or YAML) | Required |
| `--rootNamespace` | Root namespace for generated types | Required |
| `--outputPath` | Directory to write generated code | Required |
| `--mode` | Generation mode: `producer`, `consumer`, or `both` | `both` |
| `--force` | Regenerate even if lock file indicates no changes | `false` |
| `--spec-url` | URL to fetch the spec from (recorded in lock file) | — |
| `--include-channel` | Glob patterns for channels to include | All |
| `--exclude-channel` | Glob patterns for channels to exclude | None |
| `--tag` | Filter by operation tags | All |
| `--specVersion` | Force spec version (`2.6` or `3.0`); auto-detected if omitted | Auto |

Examples:

```bash
# Generate everything from a local spec
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Client \
    --outputPath ./Generated

# Generate only producers for specific channels
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Publisher \
    --outputPath ./Generated \
    --mode producer \
    --include-channel "smartylighting.streetlights.*.turn.*"

# Fetch a remote spec and generate
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Client \
    --outputPath ./Generated \
    --spec-url https://api.example.com/asyncapi.json
```

### Inspect Operations (`asyncapi-show`)

```bash
corvusjson asyncapi-show <specFile> [options]
```

Displays the channel/operation tree of an AsyncAPI specification:

```
Streetlights Kafka API v1.0.0 (AsyncAPI 3.0)

Operations (2)
├── smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured
│   └── RECV receiveLightMeasurement — Inform about environmental lighting conditions (1 msg, kafka-secure)
└── smartylighting.streetlights.1.0.action.{streetlightId}.turn.on
    └── SEND turnOn (1 msg, kafka-secure)

2 operations across 2 channels (1 send, 1 receive)
```

Supports the same filter options: `--include-channel`, `--exclude-channel`, `--tag`.

## Lock File and Regeneration

The generator produces a `corvusjson-asyncapi.lock` file alongside the generated code. This file records:
- The SHA-256 hash of the spec file
- The generation options used
- The timestamp of generation

On subsequent runs, the generator compares the current spec hash against the lock file. If the spec hasn't changed and `--force` is not specified, generation is skipped — enabling fast incremental builds.

```bash
# Normal run — skips if spec unchanged
corvusjson asyncapi-generate streetlights.json --rootNamespace Streetlights --outputPath ./Generated

# Force regeneration (e.g., after upgrading the tool)
corvusjson asyncapi-generate streetlights.json --rootNamespace Streetlights --outputPath ./Generated --force
```

When using `--spec-url`, the lock file also stores the URL. This enables update-style workflows where the spec is re-fetched from the original source on regeneration.

## Version Support

| AsyncAPI Version | Support |
|---|---|
| 3.0.x | Full — recommended |
| 2.6.x | Full — auto-detected and handled |
| 2.5 and earlier | Not supported |

The generator auto-detects the spec version from the `asyncapi` field. Use `--specVersion` to override if auto-detection fails.

### Key Differences Between 2.6 and 3.0

| Feature | 2.6 | 3.0 |
|---------|-----|-----|
| Operations | Defined on channels (`publish`/`subscribe`) | Top-level with `action: send/receive` |
| Channel parameters | On channel object | On channel object |
| Security | On server | On server |
| Traits | Supported | Supported |

The generator normalizes both formats into the same generated code structure — your producers and consumers look identical regardless of spec version.

## Best Practices

### Use `ValidationMode.Basic` in Production

The basic mode adds ~300ns per message — negligible overhead for catching malformed data that would otherwise cause runtime failures deep in your business logic. Reserve `None` for benchmarks or trusted internal pipelines.

### Configure Dead-Letter Channels

Always configure an error policy that dead-letters unprocessable messages rather than silently dropping them. This gives you an audit trail and enables replay:

```csharp
IMessageErrorPolicy policy = new DefaultMessageErrorPolicy(
    deserializationAction: MessageErrorAction.DeadLetter,
    handlerAction: MessageErrorAction.DeadLetter,
    transportAction: MessageErrorAction.Abort);
```

### Use the In-Memory Transport for Tests

Don't mock `IMessageTransport` — use `InMemoryMessageTransport`. It provides message capture, delivery simulation, and dead-letter inspection without any broker infrastructure.

### Wrap Transports with Instrumentation

Always use `InstrumentedMessageTransport` in production. The zero-cost-when-idle design means no overhead until you attach an OpenTelemetry exporter.

### Combine Polly with Error Policies

The resilience middleware (Polly) handles transient failures (retries, circuit-breaker). The error policy handles permanent failures (dead-letter, abort). They compose naturally:

```
Message arrives
  → Handler middleware (Polly): retry transient failures
  → If still failing: exception propagates
  → Error policy: dead-letter the message
```

### Generate Both Sides from the Same Spec

When you control both publisher and subscriber, generate both from the same AsyncAPI spec. This guarantees:
- Payload schemas match exactly
- Channel addresses are consistent
- Header contracts are synchronized
- Validation catches drift at development time, not in production

## Performance Characteristics

The generated code is designed for high-throughput messaging:

| Metric | Value | Notes |
|--------|-------|-------|
| Subscribe (no validation) | ~263ns | 2.4× faster than Wolverine STJ POCO dispatch |
| Subscribe (basic validation) | ~530ns | Includes full JSON Schema check |
| Publish (no validation) | ~298ns | Includes workspace + channel construction |
| Publish (basic validation) | ~621ns | Schema validates before send |
| Header encode/decode | 152B constant | Regardless of header count |
| Memory per message (subscribe) | 152B | ParsedJsonDocument pooled envelope |
| Memory per message (publish) | 200B | Workspace + builder (pooled) |

All memory is returned to pools after processing — no GC pressure under steady-state load.

## Example Recipes

- [AsyncAPI Producer](../docs/ExampleRecipes/036-AsyncApiProducer/README.md) — Basic producer generation and publishing
- [AsyncAPI Consumer](../docs/ExampleRecipes/037-AsyncApiConsumer/README.md) — Consumer with validation and error handling
- [AsyncAPI End-to-End](../docs/ExampleRecipes/038-AsyncApiEndToEnd/README.md) — Producer + consumer with in-memory transport testing