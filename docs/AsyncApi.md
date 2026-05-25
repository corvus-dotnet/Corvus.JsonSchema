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

For Azure-hosted messaging (Event Hubs via Kafka protocol, Azure Service Bus via AMQP), use `Azure.Identity` with the `OAuth2AuthenticationProvider`'s token factory:

```csharp
using Azure.Identity;
using Azure.Core;
using Corvus.Text.Json.AsyncApi;

// Client credentials flow (service-to-service)
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
```

For different Azure Identity flows, substitute the credential type:

```csharp
// Managed identity (Azure-hosted services — no secrets needed)
TokenCredential credential = new DefaultAzureCredential();

// Interactive browser login (desktop/native apps)
TokenCredential credential = new InteractiveBrowserCredential(
    new InteractiveBrowserCredentialOptions
    {
        ClientId = clientId,
        TenantId = tenantId,
    });

// Device code flow (CLI tools, headless terminals)
TokenCredential credential = new DeviceCodeCredential(
    new DeviceCodeCredentialOptions
    {
        ClientId = clientId,
        TenantId = tenantId,
        DeviceCodeCallback = (info, cancel) =>
        {
            Console.WriteLine(info.Message);
            return Task.CompletedTask;
        },
    });
```

All credential types work with the same `OAuth2AuthenticationProvider` factory pattern — the factory acquires tokens using whichever `TokenCredential` you supply.

### OAuth 2.0 (Generic / Custom Identity Provider)

For non-Azure OAuth2 providers (Auth0, Okta, Keycloak, custom):

```csharp
using Corvus.Text.Json.AsyncApi;

// Static token (short-lived scripts, testing)
IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
    accessToken: "eyJhbGciOi...",
    tokenType: "Bearer",
    scopes: "read:messages write:messages");

// Dynamic token acquisition with refresh (production)
IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
    accessTokenFactory: async ct =>
    {
        // Your OAuth2 client credentials / token exchange implementation
        TokenResponse response = await oidcClient.GetClientCredentialsTokenAsync(
            "https://auth.example.com/token",
            clientId,
            clientSecret,
            ["messaging.publish"],
            ct);
        return response.AccessToken;
    },
    tokenType: "Bearer",
    scopes: "messaging.publish");

TurnOnProducer producer = new(transport, authProvider: auth);
```

The `accessTokenFactory` is called on **every publish** (producers) and **once at subscribe time** (consumers). For caching and refresh logic, wrap your token client with `Microsoft.Extensions.Caching.Memory` or use your SDK's built-in token cache.

### Bearer Token (Simple JWT)

For brokers that accept a static JWT or pre-acquired bearer token:

```csharp
using Corvus.Text.Json.AsyncApi;

IMessageAuthenticationProvider auth = new BearerTokenAuthenticationProvider("my-jwt-token");

TurnOnProducer producer = new(transport, authProvider: auth);
```

With dynamic token refresh:

```csharp
IMessageAuthenticationProvider auth = new BearerTokenAuthenticationProvider(
    tokenFactory: async ct =>
    {
        // Refresh the token before it expires
        return await tokenService.GetOrRefreshTokenAsync(ct);
    });
```

### API Key

For brokers or gateways that authenticate via API keys:

```csharp
using Corvus.Text.Json.AsyncApi;

// Simple key — transport uses it as-is
IMessageAuthenticationProvider auth = new ApiKeyAuthenticationProvider(
    apiKey: "sk-live-abc123");

// HTTP API key style — with name and location metadata
IMessageAuthenticationProvider auth = new ApiKeyAuthenticationProvider(
    apiKey: "sk-live-abc123",
    name: "X-API-Key",
    location: "header");

ReceiveLightMeasurementConsumer consumer = new(
    transport, handler, authProvider: auth);
```

### Username/Password (SASL)

For Kafka SASL PLAIN, AMQP PLAIN, or MQTT username/password authentication:

```csharp
using Corvus.Text.Json.AsyncApi;

IMessageAuthenticationProvider auth = new UserPasswordAuthenticationProvider(
    username: "service-account",
    password: "secret-from-keyvault");

// Kafka with SASL PLAIN
await using KafkaMessageTransport transport = new(new KafkaTransportOptions
{
    BootstrapServers = "kafka.example.com:9092",
});

TurnOnProducer producer = new(transport, authProvider: auth);
```

### Client Certificate (mTLS)

For brokers requiring mutual TLS (Kafka SSL, RabbitMQ peer verification):

```csharp
using System.Security.Cryptography.X509Certificates;
using Corvus.Text.Json.AsyncApi;

// From PEM files
X509Certificate2 cert = X509Certificate2.CreateFromPemFile(
    "client-cert.pem", "client-key.pem");

// From PFX/PKCS12
X509Certificate2 cert = new("client.pfx", "pfx-password");

// From certificate store (Windows)
using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);
X509Certificate2 cert = store.Certificates.Find(
    X509FindType.FindByThumbprint, thumbprint, validOnly: false)[0];

IMessageAuthenticationProvider auth = new CertificateAuthenticationProvider(cert);
```

### Composite (Multiple Security Schemes)

When your AsyncAPI spec declares multiple security schemes on a server (e.g., both SASL and API key), the generated code calls `AuthenticateAsync` with different `SchemeType` values. Use `CompositeAuthenticationProvider` to route to the correct provider:

```csharp
using Corvus.Text.Json.AsyncApi;

IMessageAuthenticationProvider auth = new CompositeAuthenticationProvider(
    new KeyValuePair<SecuritySchemeType, IMessageAuthenticationProvider>[]
    {
        new(SecuritySchemeType.Plain, new UserPasswordAuthenticationProvider("user", "pass")),
        new(SecuritySchemeType.OAuth2, new OAuth2AuthenticationProvider(
            accessTokenFactory: async ct =>
            {
                AccessToken token = await credential.GetTokenAsync(
                    new TokenRequestContext(["https://kafka.azure.net/.default"]), ct);
                return token.Token;
            })),
    });

// The generated code emits the correct SchemeType per operation/server —
// the composite automatically selects the matching inner provider.
TurnOnProducer producer = new(transport, authProvider: auth);
```

### Custom Authentication Provider

For protocols with non-standard auth requirements, implement `IMessageAuthenticationProvider` directly:

```csharp
using Corvus.Text.Json.AsyncApi;

/// <summary>
/// Custom provider that acquires SASL SCRAM-SHA-256 credentials from HashiCorp Vault.
/// </summary>
internal sealed class VaultScramProvider : IMessageAuthenticationProvider
{
    private readonly IVaultClient vault;
    private readonly string secretPath;

    public VaultScramProvider(IVaultClient vault, string secretPath)
    {
        this.vault = vault;
        this.secretPath = secretPath;
    }

    public async ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        Secret<SecretData> secret = await this.vault.V1.Secrets.KeyValue.V2
            .ReadSecretAsync(this.secretPath, cancellationToken: cancellationToken);

        context.Credentials["username"] = secret.Data.Data["username"].ToString()!;
        context.Credentials["password"] = secret.Data.Data["password"].ToString()!;
    }
}
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
    payload: new UserSignedUpPayload.Source((ref UserSignedUpPayload.Builder b) =>
    {
        b.Create(email: "alice@example.com"u8, userId: "user-123"u8);
    }),
    headers: new CommonHeaders.Source((ref CommonHeaders.Builder b) =>
    {
        b.Create(
            correlationId: Guid.NewGuid().ToString("D"),
            timestamp: DateTimeOffset.UtcNow.ToString("O"));
    }));
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
| NATS | Base64-encoded JSON in message headers | Protocol has limited header support |
| MQTT | User properties (MQTT 5) or base64 in topic | MQTT 3.1 has no header concept |
| WebSocket | JSON envelope field | Framed alongside payload |

The transport layer handles encoding/decoding transparently — your handler always receives the typed struct regardless of the wire format.

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

## Performance

All benchmarks use a zero-overhead stub transport (no real I/O) to isolate the pipeline cost — what you pay for serialization, validation, and dispatch logic above the transport layer. Since all approaches use the same underlying transport (NATS, Kafka, etc.), the transport cost is identical and cancels out; what differs is the per-message framework overhead.

**Three comparison tiers:**

- **Raw STJ** — the absolute floor: `System.Text.Json` `JsonSerializer.Serialize` / `JsonSerializer.Deserialize` to plain C# POCOs with `[JsonPropertyName]` attributes. This is what you would write by hand with zero framework help — no validation, no typed access beyond deserialization, no error handling, no schema conformance.
- **Wolverine** — a popular .NET message bus framework (Mediator mode). Measures its full pipeline: STJ POCO deserialization + handler chain resolution + compiled invoker dispatch + context pooling + handler property access. Wolverine represents the "real framework" comparison point.
- **Corvus** — the generated AsyncAPI consumer/producer. Measures: pooled-memory parse → optional schema validation → typed dispatch through generated handler interface → error policy.

### Subscribe (Consumer) Pipeline

| Method | Mean | Allocated | Notes |
|--------|-----:|----------:|-------|
| Raw STJ deserialize (baseline) | 232 ns | 104 B | `JsonSerializer.Deserialize<T>` to POCO + access properties |
| Corvus consumer (no validation) | 263 ns | 152 B | Pooled parse → typed dispatch → handler |
| Corvus consumer (basic validation) | 530 ns | 152 B | + compiled schema check (boolean pass/fail) |
| Corvus consumer (detailed validation) | 809 ns | 152 B | + full diagnostics with error locations |
| Corvus consumer with headers | 767 ns | 304 B | + header parse & validate |
| Wolverine STJ framework dispatch | 642 ns | 104 B | STJ deserialize + handler chain + invoker |

The "Raw STJ" baseline is the minimum cost any application must pay: deserializing bytes into a usable object. Corvus adds only **31 ns** (13%) over this floor for its full generated pipeline — typed dispatch, error policy, pooled memory management — **without** any validation. With basic validation enabled (compiled JSON Schema check), Corvus is still **17% faster** than Wolverine, which provides no validation at all.

### Publish (Producer) Pipeline

| Method | Mean | Allocated | Notes |
|--------|-----:|----------:|-------|
| Raw STJ serialize (baseline) | 176 ns | 136 B | `JsonSerializer.Serialize<T>` POCO to bytes |
| Corvus producer (no validation) | 298 ns | 200 B | Workspace + channel build + serialize |
| Corvus producer (basic validation) | 621 ns | 200 B | + compiled schema check |
| Corvus producer (detailed validation) | 936 ns | 200 B | + full diagnostics collector |

The Raw STJ baseline here is just `JsonSerializer.Serialize` writing a POCO to a pooled buffer — what you'd do manually before publishing. The Corvus pipeline adds workspace management, zero-allocation channel address construction (pooled byte buffer from template + parameters), authentication callout, and serialization. The 200B allocation is the pooled `JsonWorkspace` envelope and channel rental — both returned to pools after the call, producing zero GC pressure under steady-state load.

### Header Encoding

Headers are encoded as a single JSON object using a thread-static pooled `Utf8JsonWriter`. Allocation is **constant regardless of header count**:

| Header count | Base64 encode | Corvus encode | Base64 decode | Corvus decode |
|:------------:|--------------:|--------------:|--------------:|--------------:|
| 1 | 88 B | **152 B** | 136 B | **152 B** |
| 5 | 336 B | **152 B** | 480 B | **152 B** |
| 10 | 640 B | **152 B** | 896 B | **152 B** |

Base64-encoded headers (the approach used by most messaging frameworks) grow linearly with header count. The Corvus JSON-object approach pays a fixed 152B (the `ParsedJsonDocument` envelope) regardless of how many headers are present.

### Request/Reply

| Method | Mean | Allocated | Notes |
|--------|-----:|----------:|-------|
| Raw STJ (baseline) | 287 ns | 280 B | Serialize request POCO + deserialize reply POCO |
| Corvus req/reply (no validation) | 293 ns | 352 B | Typed request + correlation + typed reply |
| Corvus req/reply (basic validation) | 578 ns | 352 B | + schema validation both directions |

The Raw STJ baseline simulates the manual implementation: `JsonSerializer.Serialize` the request POCO to bytes (publish side), then `JsonSerializer.Deserialize` the reply bytes to a POCO (subscribe side). The Corvus pipeline adds request/reply correlation (matching correlation IDs), typed channel construction, and optionally validates both request and reply against their schemas. The overhead of the full strongly-typed request/reply pipeline is just **6 ns** (2%) over raw serialization — negligible.

### Validation Cost Summary

| Mode | Overhead | Additional allocation |
|------|:--------:|:---------------------:|
| None | — | 0 B |
| Basic (boolean pass/fail) | ~270 ns | 0 B |
| Detailed (error locations) | ~550 ns | 0 B |

All validation modes produce **zero additional allocation** — the schema evaluator operates entirely on the already-parsed document. This means you can enable validation in production without increasing GC pressure.

> *BenchmarkDotNet v0.15.8, .NET 10.0.8, 13th Gen Intel Core i7-13800H, Windows 11. OutlierMode=RemoveAll, RunStrategy=Throughput.*

## Example Recipes

- [AsyncAPI Producer](../docs/ExampleRecipes/036-AsyncApiProducer/README.md) — Basic producer generation and publishing
- [AsyncAPI Consumer](../docs/ExampleRecipes/037-AsyncApiConsumer/README.md) — Consumer with validation and error handling
- [AsyncAPI End-to-End](../docs/ExampleRecipes/038-AsyncApiEndToEnd/README.md) — Producer + consumer with in-memory transport testing