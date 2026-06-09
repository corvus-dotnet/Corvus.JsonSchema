# AsyncAPI Code Generation

> **[Try the AsyncAPI Playground](/playground-asyncapi/)** — generate strongly-typed producers, consumers, handlers, and request/response (request/reply) code in your browser.

## Overview

`Corvus.Text.Json` includes a code generator that produces strongly-typed **producers** and **consumers** from [AsyncAPI](https://www.asyncapi.com/) specifications (versions 2.6 and 3.0). AsyncAPI 2.6 channel-level `publish` operations generate receive-side consumers; `subscribe` operations generate send-side producers.

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
dotnet add package Corvus.Text.Json.AsyncApi.AzureServiceBus
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
using System.Text;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

// InMemoryMessageTransport for testing; in production, use NatsMessageTransport,
// KafkaMessageTransport, etc. — the API is identical.
await using InMemoryMessageTransport transport = new();

TurnOnProducer producer = new(transport);

// Publish a validated message — schema validation runs before the message leaves your process
await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "on"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-001");

// Inspect what was published
PublishedMessage msg = transport.PublishedMessages[0];
Console.WriteLine($"Channel: {msg.Channel}");
Console.WriteLine($"Payload: {Encoding.UTF8.GetString(msg.PayloadBytes)}");
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
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

LightMeasuredHandler handler = new();
ReceiveLightMeasurementConsumer consumer = new(transport, handler);

await consumer.StartAsync();

// Simulate an incoming message (in production, the broker delivers these)
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    """{"lumens":250,"sentAt":"2024-01-15T10:30:00Z"}"""u8.ToArray());

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

### Namespace Layout

The generator places request/response infrastructure (producers, consumers, handler interfaces, message types) in the **root namespace** you specify, and JSON Schema model types in a `.Models` sub-namespace:

```
Streetlights.Client/              ← root namespace (--rootNamespace)
├── TurnOnProducer                ← producer class
├── ReceiveLightMeasurementConsumer  ← consumer class
├── IReceiveLightMeasurementHandler  ← handler interface
├── TurnOnTurnOnOffMessage        ← message type
└── Models/                       ← model sub-namespace
    ├── TurnOnOffPayload          ← JSON Schema model
    └── LightMeasuredPayload      ← JSON Schema model
```

Consumer code typically imports both namespaces:

```csharp
using Streetlights.Client;         // producers, consumers, handlers
using Streetlights.Client.Models;  // payload model types
```

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

Every generated producer and consumer accepts a `ValidationMode` parameter that controls whether messages are validated against their JSON Schema, and the degree of diagnostic information provided on failure.

### Validation Modes

| Mode | Behaviour | Use for |
|------|-----------|---------|
| `ValidationMode.None` | No validation | Trusted internal services, maximum throughput |
| `ValidationMode.Basic` | Fast boolean `EvaluateSchema()` check; on failure throws with message name but no details | Production default — catches malformed messages |
| `ValidationMode.Detailed` | Full evaluation with `JsonSchemaResultsCollector`; on failure includes JSON diagnostics with evaluation paths and error messages | Development, debugging, API gateways |

### Producer-Side Validation

Validation runs **before** the message leaves your process:

```csharp
// Basic validation (default) — catches schema violations before publish
TurnOnProducer producer = new(transport, validationMode: ValidationMode.Basic);

try
{
    await producer.PublishTurnOnOffAsync(
        payload: TurnOnOffPayload.Build(command: "invalid-command"u8, sentAt: DateTimeOffset.UtcNow),
        streetlightId: "lamp-001");
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

Dead-lettered messages are published to a derived channel address (e.g., `dead-letter.smartylighting.streetlights.1.0.action.{id}.lighting.measured`). The generated consumer calls `IMessageTransport.DeadLetterAsync` when the error policy returns `MessageErrorAction.DeadLetter`, so validation failures and handler exceptions are handled consistently across transports.

The dead-letter message includes:
- The original payload bytes
- The original headers
- The exception that caused the failure
- The original channel address

Each transport maps this to the most natural broker mechanism: Kafka, NATS, MQTT, AMQP, and WebSocket publish a new message to a configured dead-letter topic/subject/channel; Azure Service Bus also uses the broker's native dead-letter settlement path for messages already being processed by its receiver loop.

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
| `Corvus.Text.Json.AsyncApi.AzureServiceBus` | Azure Service Bus | Queues and topics |
| `Corvus.Text.Json.AsyncApi.Testing` | In-memory (tests only) | None |

For durable consumption, acknowledgement, redelivery, and restart behavior, see [AsyncAPI message resumption](AsyncApiMessageResumption.md).

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

### Azure Service Bus

```csharp
using Corvus.Text.Json.AsyncApi.AzureServiceBus;

AzureServiceBusTransportOptions options = new()
{
    ConnectionString = "<service-bus-connection-string>",
    QueueName = "streetlights",
    RequestTimeout = TimeSpan.FromSeconds(30),
};

await using AzureServiceBusMessageTransport transport =
    await AzureServiceBusMessageTransport.CreateAsync(options);
```

Use `UseTopic = true` with `TopicName` and `SubscriptionName` for pub/sub:

```csharp
AzureServiceBusTransportOptions options = new()
{
    ConnectionString = "<service-bus-connection-string>",
    UseTopic = true,
    TopicName = "streetlights",
    SubscriptionName = "processors",
};
```

For Microsoft Entra ID authentication, set `FullyQualifiedNamespace` and `Credential` instead of `ConnectionString`.

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

await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "on"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-001");

// Assert published messages
Assert.AreEqual(1, transport.PublishedMessages.Count);

// Deliver messages to consumers for testing
await transport.DeliverAsync<LightMeasuredPayload>(
    "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
    """{"lumens":250,"sentAt":"2024-01-15T10:30:00Z"}"""u8.ToArray());
```

## Local Integration Test Troubleshooting

The transport integration tests use Testcontainers and need either Docker or Podman running locally. When running targeted transport tests, include the `integration` category intentionally; for normal test runs, continue to exclude it.

```powershell
dotnet test --project tests\Corvus.Text.Json.AsyncApi.Transport.IntegrationTests\Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.csproj -c Debug -f net10.0 --filter "FullyQualifiedName~KafkaTransportTests&TestCategory=integration&TestCategory!=failing&TestCategory!=outerloop"
```

Common issues:

| Symptom | Check |
|---------|-------|
| Testcontainers cannot connect | Start Docker Desktop or Podman and ensure the `DOCKER_HOST` environment variable points at the active socket when Podman is not the default. |
| Kafka consumers time out | Use the Testcontainers-provided bootstrap address and create topics before subscribing; do not hard-code `localhost:9092`. |
| Azure Service Bus emulator exits during startup | The bind-mounted emulator config file must be readable inside the Linux container. On Docker, this requires owner read/write and group/world read permissions. |
| RabbitMQ/MQTT/NATS tests fail after a previous interrupted run | Remove stale containers and volumes with the container engine's cleanup command, then rerun the targeted test class. |

## Authentication

Generated producers and consumers accept an optional `IMessageAuthenticationProvider`. The provider supplies credentials to the transport **before** messages are published or subscriptions are established.

The generated code determines which security scheme to use from the AsyncAPI spec's `securitySchemes`. It constructs a `MessageAuthenticationContext` with the scheme type and name, passes it to your provider, then the transport reads the populated credentials. This is called once per publish (producers) or once at subscription time (consumers).

### How It Works

```
Your code constructs a producer/consumer with an IMessageAuthenticationProvider
  → Generated code creates MessageAuthenticationContext(schemeType, schemeName)
  → Calls provider.AuthenticateAsync(context, ct)
  → Provider populates context.Credentials dictionary
  → Transport reads credentials to configure connection/message
```

The `MessageAuthenticationContext.Credentials` dictionary is transport-agnostic — each transport reads the keys it understands:

| Credential key | Used by |
|----------------|---------|
| `token` | Bearer token transports (NATS auth token, Kafka OAUTHBEARER) |
| `username` / `password` | SASL PLAIN, AMQP, MQTT username/password |
| `key` | API key (transport-specific header or connection param) |
| `access_token` / `token_type` / `scopes` | OAuth2 flows |
| `certificate` | TLS client certificate (base64 PFX) |

### Microsoft Entra ID (Azure AD / MSAL)

For Azure-hosted messaging (Event Hubs via Kafka protocol, Azure Service Bus via AMQP), use `Azure.Identity` with the `OAuth2AuthenticationProvider`'s token factory.

Requires: `dotnet add package Azure.Identity`

```csharp
using Azure.Core;
using Azure.Identity;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

// Client credentials flow (service-to-service)
string tenantId = "your-tenant-id";
string clientId = "your-client-id";
string clientSecret = "your-client-secret";

TokenCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
    accessTokenFactory: async ct =>
    {
        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://eventhubs.azure.net/.default"]),
            ct);
        return token.Token;
    },
    tokenType: "Bearer");

TurnOnProducer producer = new(transport, authProvider: auth);

await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "on"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-001");
```

For different Azure Identity flows, substitute the credential type:

```csharp
using Azure.Identity;

// Managed identity (Azure-hosted services — no secrets needed)
TokenCredential managedIdentity = new DefaultAzureCredential();

// Interactive browser login (desktop/native apps)
TokenCredential browser = new InteractiveBrowserCredential(
    new InteractiveBrowserCredentialOptions
    {
        ClientId = "your-client-id",
        TenantId = "your-tenant-id",
    });

// Device code flow (CLI tools, headless terminals)
TokenCredential deviceCode = new DeviceCodeCredential(
    new DeviceCodeCredentialOptions
    {
        ClientId = "your-client-id",
        TenantId = "your-tenant-id",
        DeviceCodeCallback = (info, cancel) =>
        {
            Console.WriteLine(info.Message);
            return Task.CompletedTask;
        },
    });
```

All credential types work with the same `OAuth2AuthenticationProvider` factory pattern — the factory acquires tokens using whichever `TokenCredential` you supply.

### OAuth 2.0 (Static Token)

For short-lived scripts, testing, or pre-acquired tokens:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
    accessToken: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
    tokenType: "Bearer",
    scopes: "read:messages write:messages");

TurnOnProducer producer = new(transport, authProvider: auth);

await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "off"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-002");
```

The `accessTokenFactory` is called on **every publish** (producers) and **once at subscribe time** (consumers). For caching and refresh logic, wrap your token client with `Microsoft.Extensions.Caching.Memory` or use your SDK's built-in token cache.

### Bearer Token (Simple JWT)

For brokers that accept a static JWT or pre-acquired bearer token:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

IMessageAuthenticationProvider auth = new BearerTokenAuthenticationProvider("my-jwt-token");

TurnOnProducer producer = new(transport, authProvider: auth);

await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "on"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-003");
```

With dynamic token refresh:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();
LightMeasuredHandler handler = new();

// In production, this factory would call your token endpoint
IMessageAuthenticationProvider auth = new BearerTokenAuthenticationProvider(
    tokenFactory: ct => new ValueTask<string>("refreshed-token-value"));

ReceiveLightMeasurementConsumer consumer = new(transport, handler, authProvider: auth);
await consumer.StartAsync();
await consumer.StopAsync();

internal sealed class LightMeasuredHandler : IReceiveLightMeasurementHandler
{
    public ValueTask HandleLightMeasuredAsync(
        LightMeasuredPayload payload, CancellationToken cancellationToken = default) => default;
}
```

### API Key

For brokers or gateways that authenticate via API keys:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();
LightMeasuredHandler handler = new();

// Simple key — transport uses it as-is
IMessageAuthenticationProvider simpleAuth = new ApiKeyAuthenticationProvider(
    apiKey: "sk-live-abc123");

// HTTP API key style — with name and location metadata
IMessageAuthenticationProvider namedAuth = new ApiKeyAuthenticationProvider(
    apiKey: "sk-live-abc123",
    name: "X-API-Key",
    location: "header");

ReceiveLightMeasurementConsumer consumer = new(transport, handler, authProvider: namedAuth);
await consumer.StartAsync();
await consumer.StopAsync();

internal sealed class LightMeasuredHandler : IReceiveLightMeasurementHandler
{
    public ValueTask HandleLightMeasuredAsync(
        LightMeasuredPayload payload, CancellationToken cancellationToken = default) => default;
}
```

### Username/Password (SASL)

For Kafka SASL PLAIN, AMQP PLAIN, or MQTT username/password authentication:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

IMessageAuthenticationProvider auth = new UserPasswordAuthenticationProvider(
    username: "service-account",
    password: "secret-from-keyvault");

TurnOnProducer producer = new(transport, authProvider: auth);

await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "off"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-005");
```

### Client Certificate (mTLS)

For brokers requiring mutual TLS (Kafka SSL, RabbitMQ peer verification):

```csharp
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

// In production, load from PEM, PFX, or certificate store:
//   X509Certificate2 cert = X509Certificate2.CreateFromPemFile("client-cert.pem", "client-key.pem");
//   X509Certificate2 cert = new("client.pfx", "pfx-password");

// Self-signed cert for demonstration:
using RSA rsa = RSA.Create(2048);
CertificateRequest req = new("CN=example-client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using X509Certificate2 cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

IMessageAuthenticationProvider auth = new CertificateAuthenticationProvider(cert);

TurnOnProducer producer = new(transport, authProvider: auth);
```

### Composite (Multiple Security Schemes)

When your AsyncAPI spec declares multiple security schemes on a server (e.g., both SASL and API key), the generated code calls `AuthenticateAsync` with different `SchemeType` values. Use `CompositeAuthenticationProvider` to route to the correct provider:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

IMessageAuthenticationProvider auth = new CompositeAuthenticationProvider(
    new KeyValuePair<SecuritySchemeType, IMessageAuthenticationProvider>[]
    {
        new(SecuritySchemeType.Plain, new UserPasswordAuthenticationProvider("user", "pass")),
        new(SecuritySchemeType.Http, new BearerTokenAuthenticationProvider("token-value")),
        new(SecuritySchemeType.HttpApiKey, new ApiKeyAuthenticationProvider("api-key-123")),
    });

// The generated code emits the correct SchemeType per operation/server —
// the composite automatically selects the matching inner provider.
TurnOnProducer producer = new(transport, authProvider: auth);

await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "on"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-006");
```

### Custom Authentication Provider

For protocols with non-standard auth requirements, implement `IMessageAuthenticationProvider` directly:

```csharp
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

await using InMemoryMessageTransport transport = new();

// Custom provider that acquires credentials from a secret rotation service
IMessageAuthenticationProvider auth = new RotatingSecretProvider(
    secretFactory: ct => new ValueTask<(string User, string Pass)>(("svc-acct", "rotated-secret")));

TurnOnProducer producer = new(transport, authProvider: auth);

await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "off"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-007");
```

The implementation:

```csharp
using Corvus.Text.Json.AsyncApi;

internal sealed class RotatingSecretProvider : IMessageAuthenticationProvider
{
    private readonly Func<CancellationToken, ValueTask<(string User, string Pass)>> secretFactory;

    public RotatingSecretProvider(
        Func<CancellationToken, ValueTask<(string User, string Pass)>> secretFactory)
    {
        this.secretFactory = secretFactory;
    }

    public async ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        (string user, string pass) = await this.secretFactory(cancellationToken).ConfigureAwait(false);
        context.Credentials["username"] = user;
        context.Credentials["password"] = pass;
    }
}
```

> **See also:** [Example Recipe 039 — AsyncAPI Authentication](../ExampleRecipes/039-AsyncApiAuthentication/) for a fully compilable project demonstrating all authentication patterns.

## Channel Parameters

AsyncAPI channels can contain parameters (e.g., `smartylighting.streetlights.1.0.action.{streetlightId}.turn.on`). The generated producer exposes these as method parameters:

```csharp
// The streetlightId parameter is part of the publish method signature
await producer.PublishTurnOnOffAsync(
    payload: TurnOnOffPayload.Build(command: "on"u8, sentAt: DateTimeOffset.UtcNow),
    streetlightId: "lamp-42");
// Wire: publishes to "smartylighting.streetlights.1.0.action.lamp-42.turn.on"
```

The generated code constructs the channel address from the template using zero-allocation UTF-8 byte manipulation with pooled buffers — no string concatenation or allocation on the hot path.

## Message Headers

When an AsyncAPI message defines a `headers` schema (directly or via message traits), the generated code produces typed header structures. Headers provide metadata about the message — correlation IDs, trace context, content versioning — separate from the payload.

### Defining Headers in the Spec

Headers are typically defined via message traits (shared across multiple messages):

```json
{
  "components": {
    "schemas": {
      "CommonHeaders": {
        "type": "object",
        "properties": {
          "correlationId": { "type": "string", "format": "uuid" },
          "timestamp": { "type": "string", "format": "date-time" }
        },
        "required": ["correlationId"]
      }
    },
    "messageTraits": {
      "commonHeaders": {
        "headers": { "$ref": "#/components/schemas/CommonHeaders" }
      }
    },
    "messages": {
      "userSignedUp": {
        "payload": { "$ref": "#/components/schemas/UserSignedUpPayload" },
        "traits": [{ "$ref": "#/components/messageTraits/commonHeaders" }]
      }
    }
  }
}
```

### Producing Messages with Headers

The generated producer accepts a typed `CommonHeaders.Source` parameter alongside the payload:

```csharp
using Corvus.Text.Json.AsyncApi;

await producer.PublishUserSignedUpAsync(
    payload: UserSignedUpPayload.Build(email: "alice@example.com"u8, userId: "user-123"u8),
    headers: CommonHeaders.Build(
        correlationId: Guid.NewGuid().ToString("D"),
        timestamp: DateTimeOffset.UtcNow.ToString("O")));
```

The transport serializes headers to a JSON object using a thread-static pooled `Utf8JsonWriter`, keeping allocation constant (152 bytes) regardless of header count. For transports that don't support native headers (MQTT, NATS), headers are base64-encoded into a protocol-level property.

### Consuming Messages with Headers

The generated consumer's handler interface includes the typed headers parameter:

```csharp
using Corvus.Text.Json.AsyncApi;

// Generated handler interface includes typed headers
public interface IConsumeUserSignedUpHandler
{
    ValueTask HandleUserSignedUpAsync(
        UserSignedUpPayload payload,
        CommonHeaders headers,
        CancellationToken cancellationToken);
}

// Your handler implementation with typed header access
public class UserSignedUpHandler : IConsumeUserSignedUpHandler
{
    public ValueTask HandleUserSignedUpAsync(
        UserSignedUpPayload payload,
        CommonHeaders headers,
        CancellationToken cancellationToken)
    {
        // Typed access — no string parsing, no dictionary lookup
        string correlationId = (string)headers.CorrelationId;
        DateTimeOffset timestamp = DateTimeOffset.Parse((string)headers.Timestamp);

        Console.WriteLine($"[{correlationId}] User {payload.UserId} signed up at {timestamp}");
        return ValueTask.CompletedTask;
    }
}
```

### Messages without Headers

When the spec does not define headers for a message, the generated handler interface omits the headers parameter entirely:

```csharp
// Generated handler — no headers parameter since the spec defines none
public interface IReceiveLightMeasurementHandler
{
    ValueTask HandleLightMeasuredAsync(
        LightMeasuredPayload payload,
        CancellationToken cancellationToken = default);
}
```

The transport still delivers headers internally (for error policy context and dead-lettering), but your handler code never sees them. If you later add a `headers` schema to the message in your spec and regenerate, the handler interface gains the typed headers parameter automatically.

### Header Validation

Headers are validated alongside the payload when `ValidationMode` is `Basic` or `Detailed`. Validation uses the same compiled-schema approach as payload validation:

```csharp
using Corvus.Text.Json.AsyncApi;

// Validation mode applies to both payload AND headers
ReceiveUserSignedUpConsumer consumer = new(
    transport,
    handler,
    validationMode: ValidationMode.Detailed);

// If a message arrives with an invalid correlationId (e.g., not a UUID),
// the consumer catches the validation failure and routes through the error policy.
// With Detailed mode, the ArgumentException includes full schema evaluation results:
//   "Message headers validation failed: /correlationId: format 'uuid' validation failed"
```

The generated `ValidateHeaders<THeaders>` method mirrors `ValidatePayload<TPayload>`:

```csharp
// Generated validation (internal to the consumer)
private static void ValidateHeaders<THeaders>(THeaders headers, ValidationMode mode)
    where THeaders : struct, IJsonElement<THeaders>
{
    if (mode == ValidationMode.Basic)
    {
        if (!headers.EvaluateSchema())
        {
            ThrowHelper.ThrowMessageHeadersValidationFailed("headers");
        }
    }
    else if (mode == ValidationMode.Detailed)
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        if (!headers.EvaluateSchema(collector))
        {
            ThrowHelper.ThrowMessageHeadersValidationFailed(
                "headers", SchemaValidationDetail.FormatResults(collector));
        }
    }
}
```

### Transport-Level Header Encoding

Different transports handle header serialization differently:

| Transport | Header mechanism | Notes |
|-----------|-----------------|-------|
| Kafka | Native message headers (`Message.Headers`) | Key-value byte pairs |
| AMQP | Application properties | Native key-value map |
| Azure Service Bus | Application properties (`ServiceBusMessage.ApplicationProperties`) | User metadata map; values are encoded as strings and reconstructed as typed JSON headers for generated handlers |
| NATS | Base64-encoded JSON in message headers | Protocol has limited header support |
| MQTT | User properties (MQTT 5) or base64 in topic | MQTT 3.1 has no header concept |
| WebSocket | JSON envelope field | Framed alongside payload |

The transport layer handles encoding/decoding transparently — your handler always receives the typed struct regardless of the wire format.

Azure Service Bus deserves one extra note because it has both broker system properties and user application properties. Corvus writes AsyncAPI message headers to `ApplicationProperties`; it does not use them for broker control fields such as `CorrelationId`, `ReplyTo`, or `SessionId`. Request/reply support uses those native Service Bus fields separately, while your AsyncAPI header schema remains in the application-property map and is passed to generated handlers as the same typed header struct used by other transports.

## Request/Reply

AsyncAPI operations can model request/reply patterns. The generator produces methods that send a request and await a correlated response:

```csharp
// Generated request/reply method
(QueryResponse reply, JsonElement replyHeaders) = await queryProducer.RequestQueryAsync(
    request: QueryPayload.Build(filter: "status=active"u8),
    cancellationToken: ct);

// reply is already deserialized and validated
foreach (var item in reply.Results.EnumerateArray())
{
    Console.WriteLine($"Found: {item.Name}");
}
```

The generated code handles correlation ID generation (GUID formatted directly to a `byte[36]` — no string allocation), request/reply channel pairing, and timeout management.

AsyncAPI 3.0 uses the standard operation `reply` object. AsyncAPI 2.6 has `correlationId` but no standard `reply` object, so Corvus supports an explicit `x-corvus-reply` extension on a 2.6 operation. The extension mirrors the 3.0 shape closely enough for the generated request/reply method to use the same runtime path:

```json
{
  "subscribe": {
    "operationId": "calculate",
    "message": {
      "$ref": "#/components/messages/CalculateRequest"
    },
    "x-corvus-reply": {
      "channel": {
        "$ref": "#/channels/rpc~1calculate~1replies"
      },
      "address": {
        "location": "$message.header#/replyTo"
      },
      "message": {
        "$ref": "#/components/messages/CalculateResponse"
      }
    }
  }
}
```

The extension is intentionally explicit. The generator does not infer request/reply pairs from matching `correlationId` values because that is ambiguous in real-world 2.6 documents.

### Responder (request/reply receive)

The methods above are the *requester* half of request/reply (send a request, await the reply). The *responder* half — receive a request and send back a correlated reply — is modelled by a **receive** operation that declares a `reply`. For these operations the generator produces a reply-returning handler and a consumer that publishes the reply for you.

The handler returns the reply payload instead of `void`:

```csharp
public sealed class CalculateHandler : ICalculateHandler
{
    // A receive operation with a reply: return the reply payload; the consumer publishes it.
    public ValueTask<CalculateResponse> HandleCalculateRequestAsync(
        CalculateRequest payload,
        CancellationToken cancellationToken = default)
    {
        int sum = payload.A + payload.B;
        return ValueTask.FromResult(new CalculateResponse.Source((ref CalculateResponse.Builder b) =>
        {
            b.Create(result: sum);
        }));
    }
}
```

The generated consumer subscribes through the transport's responder primitive:

```csharp
ValueTask SubscribeReplyAsync<TRequest, TReply>(
    ReadOnlyMemory<byte> channelUtf8,
    Func<TRequest, JsonElement, CancellationToken, ValueTask<TReply>> handler,
    CancellationToken cancellationToken = default)
    where TRequest : struct, IJsonElement<TRequest>
    where TReply : struct, IJsonElement<TReply>;
```

The transport owns correlation: for each delivered request it reads the request's reply-to address and correlation id (native broker fields — the same `CorrelationId`/`ReplyTo` the requester sets), invokes the handler, and publishes the returned reply to the reply-to address correlated to the request. The handler never sees the correlation plumbing.

**Implementation status.** `SubscribeReplyAsync` is a default interface member that throws `NotSupportedException`, so a transport opts in by overriding it. The in-memory testing transport implements a full in-process round-trip: a `RequestAsync` call delivers the request to a registered responder, whose reply completes the requester's pending call (with no responder registered, `RequestAsync` parks the request for the test helper `CompleteRequest`, as before). The broker transports (NATS, Kafka, AMQP, MQTT, WebSocket, Azure Service Bus) inherit the default until responder support is implemented for each.

This responder foundation is what the Arazzo workflow engine's request/reply *receive* step builds on.

## Bindings

AsyncAPI bindings provide protocol-specific configuration. The generator captures bindings at three levels and makes them available to the transport via `MessageContext`:

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

Current transports primarily use their strongly-typed transport options for delivery semantics. `MessageContext` is the extension point for transports that need to inspect binding JSON directly; if you rely on a specific binding, verify that the selected transport implements it rather than assuming every binding affects runtime behavior automatically.

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

The health check extension works with transports that implement `IHealthCheckableTransport` (`KafkaMessageTransport`, `NatsMessageTransport`, `AmqpMessageTransport`, and `InMemoryMessageTransport`). It monitors transport connectivity by checking `IsConnected` and calling `PingAsync`.

Use `ProcessingLoopHeartbeat` separately when you need per-subscription liveness/staleness detection.

## CLI Reference

### Generate Code (`asyncapi-generate`)

```bash
corvusjson asyncapi-generate <specFile> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<specFile>` | Path to the AsyncAPI specification (JSON or YAML) | Required |
| `--rootNamespace` | Root namespace for generated types | `GeneratedAsyncApi` |
| `--outputPath` | Directory to write generated code | `./Generated` |
| `--mode` | Generation mode: `producer`, `consumer`, or `both` | `both` |
| `--force` | Regenerate even if lock file indicates no changes | `false` |
| `--spec-url` | URL to fetch the spec from (recorded in lock file) | — |
| `--yaml` | Enable YAML support explicitly; otherwise `.yaml`/`.yml` is auto-detected | Auto |
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
| Request/reply | `x-corvus-reply` extension | Standard operation `reply` object |

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

## Performance

All benchmarks use a zero-overhead stub transport (no real I/O) to isolate the pipeline cost — what you pay for serialization, validation, and dispatch logic above the transport layer. Since all approaches use the same underlying transport (NATS, Kafka, etc.), the transport cost is identical and cancels out; what differs is the per-message framework overhead.

**Comparison:**

- **Wolverine (baseline)** — a popular .NET message bus framework (Mediator mode). Measures its full pipeline: STJ serialization/deserialization + handler chain resolution + compiled invoker dispatch + context pooling + handler property access. This represents what a real framework costs today.
- **Corvus (generated)** — the generated AsyncAPI consumer/producer. Measures: pooled-memory parse → optional schema validation → typed dispatch through generated handler interface → error policy.

Both baselines include the serialization cost that a real transport would incur — Wolverine uses `System.Text.Json` POCO serialization, Corvus uses its generated workspace-based serialization.

### Subscribe (Consumer) Pipeline

| Method | Mean | Allocated | Notes |
|--------|-----:|----------:|-------|
| Wolverine (baseline) | 535 ns | 104 B | STJ deserialize + handler chain + invoker |
| Corvus consumer (no validation) | 160 ns | 152 B | Pooled parse → typed dispatch → handler |
| Corvus consumer (basic validation) | 320 ns | 152 B | + compiled schema check (boolean pass/fail) |
| Corvus consumer (detailed validation) | 485 ns | 152 B | + full diagnostics with error locations |
| Corvus consumer with headers | 464 ns | 304 B | + header parse & validate |

Corvus without validation is **3.3× faster** than Wolverine. Even with basic schema validation enabled, Corvus is **40% faster** than Wolverine — which provides no validation at all.

### Publish (Producer) Pipeline

| Method | Mean | Allocated | Notes |
|--------|-----:|----------:|-------|
| Wolverine (baseline) | 557 ns | 80 B | STJ serialize + handler chain + compiled invoker + context pooling |
| Corvus producer (no validation) | 293 ns | 200 B | Workspace + channel build + serialize |
| Corvus producer (basic validation) | 610 ns | 200 B | + compiled schema check |
| Corvus producer (detailed validation) | 834 ns | 200 B | + full diagnostics collector |

Corvus without validation is **1.9× faster** than Wolverine. With basic validation enabled, Corvus is at parity with Wolverine's no-validation baseline. The 200B allocation is the pooled `JsonWorkspace` envelope — returned to pools after the call, producing zero GC pressure under steady-state load.

### Request/Reply

| Method | Mean | Allocated | Notes |
|--------|-----:|----------:|-------|
| Wolverine + correlation header (baseline) | 521 ns | 968 B | STJ serialize + dispatch request with correlation, handler returns typed reply |
| Corvus req/reply (no validation) | 377 ns | 336 B | Generated producer: workspace + correlate + reply parse |
| Corvus req/reply (basic validation) | 651 ns | 336 B | + schema validation both directions |

Corvus without validation is **28% faster** and allocates **65% less** than the Wolverine baseline (336B vs 968B). The Corvus pipeline includes typed channel construction, schema-aware serialization, correlation ID matching (via pooled buffers), and reply parsing — all with aggressive allocation avoidance: the reply channel address is hoisted to a static field, the correlation ID is rented from `ArrayPool<byte>`, and the reply is parsed into pooled memory. With basic validation enabled, Corvus is 1.25× the baseline in time but still allocates 65% less — you get full schema conformance checking on both request and reply payloads for that cost.

### Validation Cost Summary

| Mode | Overhead | Additional allocation |
|------|:--------:|:---------------------:|
| None | — | 0 B |
| Basic (boolean pass/fail) | ~300 ns | 0 B |
| Detailed (error locations) | ~540 ns | 0 B |

All validation modes produce **zero additional allocation** — the schema evaluator operates entirely on the already-parsed document. This means you can enable validation in production without increasing GC pressure.

> *BenchmarkDotNet v0.15.8, .NET 10.0.8, 13th Gen Intel Core i7-13800H, Windows 11. OutlierMode=RemoveAll, RunStrategy=Throughput.*

## Example Recipes

- [AsyncAPI Producer](../ExampleRecipes/036-AsyncApiProducer/) — Basic producer generation and publishing
- [AsyncAPI Consumer](../ExampleRecipes/037-AsyncApiConsumer/) — Consumer with validation and error handling
- [AsyncAPI End-to-End](../ExampleRecipes/038-AsyncApiEndToEnd/) — Producer + consumer with in-memory transport testing
- [AsyncAPI Authentication](../ExampleRecipes/039-AsyncApiAuthentication/) — All auth patterns (Azure Identity, OAuth2, API Key, mTLS, composite)