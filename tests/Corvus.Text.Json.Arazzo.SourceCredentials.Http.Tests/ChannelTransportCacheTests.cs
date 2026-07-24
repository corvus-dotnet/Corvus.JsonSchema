// <copyright file="ChannelTransportCacheTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.AsyncApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http.Tests;

/// <summary>
/// Coverage of <see cref="ChannelTransportCache"/> (ADR 0051): one shared, lazily-connected transport per channel
/// source, built from the source's §13 channel credential through the protocol's factory — resolution failures
/// surface as <see cref="WorkflowTransportBindingException"/> on first use, and the connection is built once.
/// </summary>
[TestClass]
public sealed class ChannelTransportCacheTests
{
    [TestMethod]
    public async Task The_transport_connects_on_first_use_from_the_binding_and_is_built_once()
    {
        var store = new InMemorySourceCredentialStore();
        (await store.AddAsync(
            new SourceCredentialDefinition(
                "events",
                "production",
                SourceCredentialKind.Bearer,
                [new SecretReferenceDefinition("value", "vault://secret/arazzo/events#token")],
                Config: [new CredentialConfigDefinition("serverUrl", "nats://broker:4222")]),
            "test",
            default)).Dispose();
        var resolver = new FakeSecretResolver(new Dictionary<string, string> { ["vault://secret/arazzo/events#token"] = "connect-token" });
        var factory = new RecordingChannelTransportFactory();
        await using var cache = new ChannelTransportCache(store, resolver, "production", [factory]);

        IMessageTransport transport = cache.GetTransport("events", "nats");
        factory.Created.ShouldBe(0, "the bind path must not wait on a broker handshake");
        cache.GetTransport("events", "nats").ShouldBeSameAs(transport, "one shared transport per channel source");

        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("""{"hello":"world"}"""u8.ToArray());
        await transport.PublishAsync("events.hello"u8.ToArray().AsMemory(), payload.RootElement);
        await transport.PublishAsync("events.hello"u8.ToArray().AsMemory(), payload.RootElement);

        factory.Created.ShouldBe(1, "the connection is built once and shared");
        factory.LastSettings!.ServerUrl.ShouldBe("nats://broker:4222");
        factory.LastSettings.AuthKind.ShouldBe("bearer");
        factory.LastSettings.Secrets["value"].ShouldBe("connect-token");
        factory.Inner!.Published.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_missing_binding_faults_on_first_use_naming_the_source_and_environment()
    {
        var store = new InMemorySourceCredentialStore();
        var resolver = new FakeSecretResolver([]);
        await using var cache = new ChannelTransportCache(store, resolver, "staging", [new RecordingChannelTransportFactory()]);

        IMessageTransport transport = cache.GetTransport("events", "nats");
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        WorkflowTransportBindingException fault = await Should.ThrowAsync<WorkflowTransportBindingException>(
            async () => await transport.PublishAsync("events.hello"u8.ToArray().AsMemory(), payload.RootElement));
        fault.Message.ShouldContain("events");
        fault.Message.ShouldContain("staging");
        fault.Message.ShouldContain("serverUrl");
    }

    [TestMethod]
    public async Task A_binding_without_a_server_url_faults_on_first_use()
    {
        var store = new InMemorySourceCredentialStore();
        (await store.AddAsync(
            new SourceCredentialDefinition(
                "events",
                "production",
                SourceCredentialKind.Bearer,
                [new SecretReferenceDefinition("value", "vault://secret/arazzo/events#token")]),
            "test",
            default)).Dispose();
        await using var cache = new ChannelTransportCache(store, new FakeSecretResolver([]), "production", [new RecordingChannelTransportFactory()]);

        IMessageTransport transport = cache.GetTransport("events", "nats");
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        (await Should.ThrowAsync<WorkflowTransportBindingException>(
            async () => await transport.PublishAsync("events.hello"u8.ToArray().AsMemory(), payload.RootElement)))
            .Message.ShouldContain("serverUrl");
    }

    [TestMethod]
    public async Task An_unregistered_protocol_faults_on_first_use()
    {
        var store = new InMemorySourceCredentialStore();
        (await store.AddAsync(
            new SourceCredentialDefinition(
                "events",
                "production",
                SourceCredentialKind.Bearer,
                [new SecretReferenceDefinition("value", "vault://secret/arazzo/events#token")],
                Config: [new CredentialConfigDefinition("serverUrl", "kafka-1:9092")]),
            "test",
            default)).Dispose();
        var resolver = new FakeSecretResolver(new Dictionary<string, string> { ["vault://secret/arazzo/events#token"] = "connect-token" });
        await using var cache = new ChannelTransportCache(store, resolver, "production", [new RecordingChannelTransportFactory()]);

        IMessageTransport transport = cache.GetTransport("events", "kafka");
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        (await Should.ThrowAsync<WorkflowTransportBindingException>(
            async () => await transport.PublishAsync("events.hello"u8.ToArray().AsMemory(), payload.RootElement)))
            .Message.ShouldContain("kafka");
    }

    /// <summary>A factory that records the settings it was handed and serves an in-memory transport.</summary>
    private sealed class RecordingChannelTransportFactory : IChannelTransportFactory
    {
        public int Created { get; private set; }

        public ChannelTransportSettings? LastSettings { get; private set; }

        public RecordingMessageTransport? Inner { get; private set; }

        public string Protocol => "nats";

        public ValueTask<IMessageTransport> CreateTransportAsync(ChannelTransportSettings settings, CancellationToken cancellationToken)
        {
            this.Created++;
            this.LastSettings = settings;
            this.Inner = new RecordingMessageTransport();
            return new ValueTask<IMessageTransport>(this.Inner);
        }
    }

    /// <summary>A minimal transport that counts publishes.</summary>
    private sealed class RecordingMessageTransport : IMessageTransport
    {
        public List<string> Published { get; } = [];

        public ValueTask DisposeAsync() => default;

        public ValueTask PublishAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, in TPayload payload, in JsonElement headers = default, CancellationToken cancellationToken = default)
            where TPayload : struct, Corvus.Text.Json.Internal.IJsonElement<TPayload>
        {
            this.Published.Add(System.Text.Encoding.UTF8.GetString(channelUtf8.Span));
            return default;
        }

        public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(ReadOnlyMemory<byte> requestChannelUtf8, ReadOnlyMemory<byte> replyChannelUtf8, TRequest request, ReadOnlyMemory<byte> correlationIdUtf8, JsonElement headers = default, CancellationToken cancellationToken = default)
            where TRequest : struct, Corvus.Text.Json.Internal.IJsonElement<TRequest>
            where TReply : struct, Corvus.Text.Json.Internal.IJsonElement<TReply>
            => throw new NotSupportedException();

        public ValueTask SubscribeAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, Func<TPayload, JsonElement, CancellationToken, ValueTask> handler, CancellationToken cancellationToken = default)
            where TPayload : struct, Corvus.Text.Json.Internal.IJsonElement<TPayload>
            => default;

        public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default) => default;

        public ValueTask DeadLetterAsync(ReadOnlyMemory<byte> deadLetterChannelUtf8, ReadOnlyMemory<byte> originalChannelUtf8, in JsonElement payload, in JsonElement headers, Exception exception, CancellationToken cancellationToken = default) => default;
    }
}