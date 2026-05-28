// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;

// All examples use InMemoryMessageTransport for testability.
// In production, substitute any IMessageTransport (NATS, Kafka, AMQP, etc.).
await using InMemoryMessageTransport transport = new();
LightMeasuredHandler handler = new();

// ── OAuth 2.0 with Azure Identity (Client Credentials) ─────────────────────
// Requires: dotnet add package Azure.Identity
// ⚠ Replace the placeholder values below with real Azure AD credentials.
try
{
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
        payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
        {
            b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
        }),
        streetlightId: "lamp-001");

    Console.WriteLine($"OAuth2 (client credentials): published to {transport.PublishedMessages[^1].Channel}");
}
catch (Exception ex)
{
    Console.WriteLine($"OAuth2 (client credentials): \u26a0 Requires real Azure AD credentials. " +
        $"Set tenantId, clientId, and clientSecret to valid values. ({ex.GetType().Name})");
}

// ── OAuth 2.0 with Managed Identity ────────────────────────────────────────
// No secrets needed — works automatically on Azure-hosted services.
// ⚠ This will fail outside of an Azure environment (VM, App Service, AKS, etc.).
try
{
    TokenCredential credential = new DefaultAzureCredential();

    IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
        accessTokenFactory: async ct =>
        {
            AccessToken token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://servicebus.azure.net/.default"]),
                ct);
            return token.Token;
        },
        tokenType: "Bearer");

    TurnOnProducer producer = new(transport, authProvider: auth);
    Console.WriteLine("OAuth2 (managed identity): provider configured");
}
catch (Exception ex)
{
    Console.WriteLine($"OAuth2 (managed identity): \u26a0 Requires an Azure-hosted environment " +
        $"(VM, App Service, AKS) with managed identity enabled. ({ex.GetType().Name})");
}

// ── OAuth 2.0 with Interactive Browser Login ───────────────────────────────
// Desktop/native apps — opens a browser for user authentication.
// ⚠ Replace the placeholder values below with real Azure AD app registration.
try
{
    string clientId = "your-client-id";
    string tenantId = "your-tenant-id";

    TokenCredential credential = new InteractiveBrowserCredential(
        new InteractiveBrowserCredentialOptions
        {
            ClientId = clientId,
            TenantId = tenantId,
        });

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
    Console.WriteLine("OAuth2 (interactive browser): provider configured");
}
catch (Exception ex)
{
    Console.WriteLine($"OAuth2 (interactive browser): \u26a0 Requires real Azure AD credentials. " +
        $"Set clientId and tenantId to valid values. ({ex.GetType().Name})");
}

// ── OAuth 2.0 with Device Code Flow ───────────────────────────────────────
// CLI tools and headless terminals.
// ⚠ Replace the placeholder values below with real Azure AD app registration.
try
{
    string clientId = "your-client-id";
    string tenantId = "your-tenant-id";

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
    Console.WriteLine("OAuth2 (device code): provider configured");
}
catch (Exception ex)
{
    Console.WriteLine($"OAuth2 (device code): \u26a0 Requires real Azure AD credentials. " +
        $"Set clientId and tenantId to valid values. ({ex.GetType().Name})");
}

// ── OAuth 2.0 with Static Token ────────────────────────────────────────────
// Short-lived scripts and testing.
{
    IMessageAuthenticationProvider auth = new OAuth2AuthenticationProvider(
        accessToken: "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
        tokenType: "Bearer",
        scopes: "read:messages write:messages");

    TurnOnProducer producer = new(transport, authProvider: auth);
    await producer.PublishTurnOnOffAsync(
        payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
        {
            b.Create(command: "off"u8, sentAt: DateTimeOffset.UtcNow);
        }),
        streetlightId: "lamp-002");

    Console.WriteLine($"OAuth2 (static token): published to {transport.PublishedMessages[^1].Channel}");
}

// ── Bearer Token (Simple JWT) ──────────────────────────────────────────────
{
    IMessageAuthenticationProvider auth = new BearerTokenAuthenticationProvider("my-jwt-token");

    TurnOnProducer producer = new(transport, authProvider: auth);
    await producer.PublishTurnOnOffAsync(
        payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
        {
            b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
        }),
        streetlightId: "lamp-003");

    Console.WriteLine($"Bearer token: published to {transport.PublishedMessages[^1].Channel}");
}

// ── Bearer Token with Dynamic Refresh ──────────────────────────────────────
{
    // In production, this factory would call your token endpoint
    IMessageAuthenticationProvider auth = new BearerTokenAuthenticationProvider(
        tokenFactory: ct => new ValueTask<string>("refreshed-token-value"));

    ReceiveLightMeasurementConsumer consumer = new(transport, handler, authProvider: auth);
    await consumer.StartAsync();
    Console.WriteLine("Bearer token (dynamic): consumer subscribed");
    await consumer.StopAsync();
}

// ── API Key ────────────────────────────────────────────────────────────────
{
    // Simple key
    IMessageAuthenticationProvider auth = new ApiKeyAuthenticationProvider(
        apiKey: "sk-live-abc123");

    TurnOnProducer producer = new(transport, authProvider: auth);
    await producer.PublishTurnOnOffAsync(
        payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
        {
            b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
        }),
        streetlightId: "lamp-004");

    Console.WriteLine($"API key: published to {transport.PublishedMessages[^1].Channel}");
}

// ── API Key with Name and Location ─────────────────────────────────────────
{
    IMessageAuthenticationProvider auth = new ApiKeyAuthenticationProvider(
        apiKey: "sk-live-abc123",
        name: "X-API-Key",
        location: "header");

    ReceiveLightMeasurementConsumer consumer = new(transport, handler, authProvider: auth);
    await consumer.StartAsync();
    Console.WriteLine("API key (named): consumer subscribed");
    await consumer.StopAsync();
}

// ── Username/Password (SASL) ───────────────────────────────────────────────
{
    IMessageAuthenticationProvider auth = new UserPasswordAuthenticationProvider(
        username: "service-account",
        password: "secret-from-keyvault");

    TurnOnProducer producer = new(transport, authProvider: auth);
    await producer.PublishTurnOnOffAsync(
        payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
        {
            b.Create(command: "off"u8, sentAt: DateTimeOffset.UtcNow);
        }),
        streetlightId: "lamp-005");

    Console.WriteLine($"Username/password: published to {transport.PublishedMessages[^1].Channel}");
}

// ── Client Certificate (mTLS) ──────────────────────────────────────────────
{
    // Load a certificate from PEM files (production pattern):
    //   X509Certificate2 cert = X509Certificate2.CreateFromPemFile("client-cert.pem", "client-key.pem");
    //
    // Or from a PFX file:
    //   X509Certificate2 cert = new("client.pfx", "pfx-password");
    //
    // Or from the Windows certificate store:
    //   using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
    //   store.Open(OpenFlags.ReadOnly);
    //   X509Certificate2 cert = store.Certificates.Find(
    //       X509FindType.FindByThumbprint, thumbprint, validOnly: false)[0];

    // For this example we create a self-signed cert:
    using RSA rsa = RSA.Create(2048);
    CertificateRequest req = new("CN=example-client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using X509Certificate2 cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

    IMessageAuthenticationProvider auth = new CertificateAuthenticationProvider(cert);

    TurnOnProducer producer = new(transport, authProvider: auth);
    Console.WriteLine("Certificate (mTLS): provider configured");
}

// ── Composite (Multiple Security Schemes) ──────────────────────────────────
// When a server requires multiple auth mechanisms, CompositeAuthenticationProvider
// routes to the correct provider based on the scheme type.
{
    IMessageAuthenticationProvider auth = new CompositeAuthenticationProvider(
        new KeyValuePair<SecuritySchemeType, IMessageAuthenticationProvider>[]
        {
            new(SecuritySchemeType.Plain, new UserPasswordAuthenticationProvider("user", "pass")),
            new(SecuritySchemeType.Http, new BearerTokenAuthenticationProvider("token-value")),
            new(SecuritySchemeType.HttpApiKey, new ApiKeyAuthenticationProvider("api-key-123")),
        });

    TurnOnProducer producer = new(transport, authProvider: auth);
    await producer.PublishTurnOnOffAsync(
        payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
        {
            b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
        }),
        streetlightId: "lamp-006");

    Console.WriteLine($"Composite: published to {transport.PublishedMessages[^1].Channel}");
}

// ── Custom Authentication Provider ─────────────────────────────────────────
// Implement IMessageAuthenticationProvider for non-standard auth requirements.
{
    IMessageAuthenticationProvider auth = new RotatingSecretProvider(
        secretFactory: ct => new ValueTask<(string User, string Pass)>(("svc-acct", "rotated-secret")));

    TurnOnProducer producer = new(transport, authProvider: auth);
    await producer.PublishTurnOnOffAsync(
        payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
        {
            b.Create(command: "off"u8, sentAt: DateTimeOffset.UtcNow);
        }),
        streetlightId: "lamp-007");

    Console.WriteLine($"Custom provider: published to {transport.PublishedMessages[^1].Channel}");
}

Console.WriteLine($"\nTotal messages published: {transport.PublishedMessages.Count}");

// ── Handler implementation ───────────────────────────────────────────────────
internal sealed class LightMeasuredHandler : IReceiveLightMeasurementHandler
{
    public ValueTask HandleLightMeasuredAsync(
        LightMeasuredPayload payload,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"  Received: lumens={payload.Lumens}");
        return ValueTask.CompletedTask;
    }
}

// ── Custom auth provider that rotates credentials from a secret store ────────
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