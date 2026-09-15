# 039 — AsyncAPI Authentication

Demonstrates every **authentication provider** supported by the AsyncAPI runtime. Each section shows a different security scheme — from simple API keys to Azure AD OAuth 2.0 with token rotation.

> **Note:** The Azure Identity sections need real Azure AD configuration to authenticate. Only the client credentials section requests a token (when it publishes), so without valid credentials it fails and prints what you need to configure. The managed identity, interactive browser and device code sections only create the credential and the producer, so they print `provider configured`; they would request a token on their first publish or subscribe. All other sections run successfully with the in-memory transport.

## What This Demonstrates

| Auth Mechanism | Provider | Notes |
|---------------|----------|-------|
| OAuth 2.0 — Client Credentials | `OAuth2AuthenticationProvider` + `ClientSecretCredential` | Requires Azure AD app registration |
| OAuth 2.0 — Managed Identity | `OAuth2AuthenticationProvider` + `DefaultAzureCredential` | Azure-hosted services only |
| OAuth 2.0 — Interactive Browser | `OAuth2AuthenticationProvider` + `InteractiveBrowserCredential` | Desktop/native apps |
| OAuth 2.0 — Device Code | `OAuth2AuthenticationProvider` + `DeviceCodeCredential` | CLI tools, headless terminals |
| OAuth 2.0 — Static Token | `OAuth2AuthenticationProvider` | Short-lived scripts and testing |
| Bearer Token | `BearerTokenAuthenticationProvider` | Simple JWT or opaque token |
| Bearer Token — Dynamic Refresh | `BearerTokenAuthenticationProvider` with factory | Token rotation |
| API Key | `ApiKeyAuthenticationProvider` | Header, query, or cookie |
| Username/Password (SASL) | `UserPasswordAuthenticationProvider` | Kafka SASL, AMQP PLAIN |
| Client Certificate (mTLS) | `CertificateAuthenticationProvider` | Mutual TLS |
| Composite (Multiple Schemes) | `CompositeAuthenticationProvider` | Routes by `SecuritySchemeType` |
| Custom Provider | Implement `IMessageAuthenticationProvider` | Any non-standard auth |

## Prerequisites

```bash
dotnet tool install --global Corvus.Json.Cli
```

For the Azure Identity examples, you also need:

```bash
dotnet add package Azure.Identity
```

## Generating the Code

```bash
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Client \
    --outputPath Generated
```

## How Authentication Works

Every producer and consumer accepts an optional `IMessageAuthenticationProvider`:

```csharp
TurnOnProducer producer = new(transport, authProvider: auth);
```

The generated code calls `AuthenticateAsync` before each publish or subscribe operation. The provider populates a `MessageAuthenticationContext` with credentials, tokens, or certificates appropriate to the transport.

### Choosing a Provider

| Your scenario | Use this provider |
|--------------|-------------------|
| Azure service-to-service | `OAuth2AuthenticationProvider` + `ClientSecretCredential` |
| Azure managed workload | `OAuth2AuthenticationProvider` + `DefaultAzureCredential` |
| Desktop app with user login | `OAuth2AuthenticationProvider` + `InteractiveBrowserCredential` |
| CLI tool without browser | `OAuth2AuthenticationProvider` + `DeviceCodeCredential` |
| Pre-obtained JWT or opaque token | `BearerTokenAuthenticationProvider` |
| API key in header/query | `ApiKeyAuthenticationProvider` |
| Kafka/AMQP username+password | `UserPasswordAuthenticationProvider` |
| Mutual TLS | `CertificateAuthenticationProvider` |
| Multiple schemes required | `CompositeAuthenticationProvider` |
| Non-standard auth | Implement `IMessageAuthenticationProvider` |

## Running

```bash
dotnet run -f net10.0
```

The client credentials section prints a warning with instructions for configuring real credentials, because its publish requests a token. The managed identity, interactive browser and device code sections create their providers without requesting a token, so they print `provider configured`. All other sections run successfully.

Expected output (the exception name in brackets depends on the environment):

```text
OAuth2 (client credentials): ⚠ Requires real Azure AD credentials. Set tenantId, clientId, and clientSecret to valid values. (AuthenticationFailedException)
OAuth2 (managed identity): provider configured
OAuth2 (interactive browser): provider configured
OAuth2 (device code): provider configured
OAuth2 (static token): published to smartylighting.streetlights.1.0.action.lamp-002.turn.on
Bearer token: published to smartylighting.streetlights.1.0.action.lamp-003.turn.on
Bearer token (dynamic): consumer subscribed
API key: published to smartylighting.streetlights.1.0.action.lamp-004.turn.on
API key (named): consumer subscribed
Username/password: published to smartylighting.streetlights.1.0.action.lamp-005.turn.on
Certificate (mTLS): provider configured
Composite: published to smartylighting.streetlights.1.0.action.lamp-006.turn.on
Custom provider: published to smartylighting.streetlights.1.0.action.lamp-007.turn.on

Total messages published: 6
```

## Related Recipes

- [036 — AsyncAPI Producer](../036-AsyncApiProducer/) — producer basics and Source pattern
- [037 — AsyncAPI Consumer](../037-AsyncApiConsumer/) — consumer and error handling
- [038 — AsyncAPI End-to-End](../038-AsyncApiEndToEnd/) — full pipeline integration test
