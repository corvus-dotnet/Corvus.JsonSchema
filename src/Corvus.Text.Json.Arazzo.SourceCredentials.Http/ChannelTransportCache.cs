// <copyright file="ChannelTransportCache.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// The runner's channel-transport binder (ADR 0051): one shared, lazily-connected <see cref="IMessageTransport"/>
/// per channel source, built from that source's §13 channel credential in this runner's environment. The binding
/// carries the environment's broker endpoint (<c>serverUrl</c> config) and the broker credential; the transport
/// is built by the <see cref="IChannelTransportFactory"/> matching the protocol the source document declares
/// (baked into the workflow descriptor's <see cref="MessageSourceDescriptor"/>).
/// </summary>
/// <remarks>
/// <para>Broker auth is connection-time, so a channel credential is connection-scoped — never usage-scoped to a
/// run — and the built transport is shared across every run that binds the source. The transport connects on
/// first use (single-flight), keeping the bind path synchronous; resolution failures surface on the first
/// channel operation as a <see cref="WorkflowTransportBindingException"/>.</para>
/// <para>Rotation: the built connection holds the credential it connected with. A rotated binding is picked up
/// when the process restarts (or a new cache is constructed); live reconnect-on-rotation is deliberately not
/// attempted here because in-flight runs hold the shared transport.</para>
/// </remarks>
public sealed class ChannelTransportCache : IAsyncDisposable
{
    private readonly ISourceCredentialStore store;
    private readonly ISecretResolver resolver;
    private readonly string environment;
    private readonly IReadOnlyList<IChannelTransportFactory> factories;
    private readonly ConcurrentDictionary<string, LazyChannelTransport> transports = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ChannelTransportCache"/> class.</summary>
    /// <param name="store">The §13 credential store the (source, environment) channel bindings are read from.</param>
    /// <param name="resolver">The runner's read-only secret resolver (dereferences the binding's secret references).</param>
    /// <param name="environment">The deployment environment this runner serves; bindings resolve for it.</param>
    /// <param name="factories">The protocol-dispatched transport factories the host registers (one per broker
    /// protocol it serves, e.g. <see cref="IChannelTransportFactory.Protocol"/> <c>nats</c>).</param>
    public ChannelTransportCache(ISourceCredentialStore store, ISecretResolver resolver, string environment, IReadOnlyList<IChannelTransportFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(factories);
        this.store = store;
        this.resolver = resolver;
        this.environment = environment;
        this.factories = factories;
    }

    /// <summary>
    /// Gets the shared transport for a channel source — a lazily-connecting handle, so the bind path stays
    /// synchronous and the broker connection is established (single-flight) on the first channel operation.
    /// </summary>
    /// <param name="sourceName">The channel source's <c>sourceDescriptions</c> name.</param>
    /// <param name="protocol">The transport protocol the source document declares (from the descriptor).</param>
    /// <returns>The shared transport handle for (source, environment).</returns>
    public IMessageTransport GetTransport(string sourceName, string protocol)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(protocol);
        return this.transports.GetOrAdd(sourceName, static (name, state) => new LazyChannelTransport(state.Cache, name, state.Protocol), (Cache: this, Protocol: protocol));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (LazyChannelTransport transport in this.transports.Values)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }

        this.transports.Clear();
    }

    // Resolves the source's channel binding in this environment, dereferences its secrets, and builds the
    // connected transport through the protocol's factory. Connect-time only — never on the publish hot path.
    private async ValueTask<IMessageTransport> ConnectAsync(string sourceName, string protocol, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<SourceCredentialBinding>? document = await this.store.GetAsync(sourceName, this.environment, AccessContext.System, cancellationToken).ConfigureAwait(false);
        if (document is not { } doc)
        {
            throw new WorkflowTransportBindingException(
                $"Channel source '{sourceName}' has no credential binding in '{this.environment}' (ADR 0051); bind the environment's broker URL ('serverUrl' config) and broker credential.");
        }

        SourceCredentialBinding binding = doc.RootElement;
        if (!binding.TryGetConfigValue("serverUrl", out string? serverUrl) || string.IsNullOrEmpty(serverUrl))
        {
            throw new WorkflowTransportBindingException(
                $"The channel credential for source '{sourceName}' in '{this.environment}' carries no 'serverUrl' config entry (ADR 0051); the binding must name the environment's broker endpoint.");
        }

        IChannelTransportFactory? factory = null;
        foreach (IChannelTransportFactory candidate in this.factories)
        {
            if (string.Equals(candidate.Protocol, protocol, StringComparison.Ordinal))
            {
                factory = candidate;
                break;
            }
        }

        if (factory is null)
        {
            throw new WorkflowTransportBindingException(
                $"Channel source '{sourceName}' declares protocol '{protocol}', but this host registers no transport factory for it (ADR 0051).");
        }

        // The revealed secret values live only in the settings handed to the factory and the connection it
        // builds — the OAuth client-credential posture; the scrubable material is disposed as it is revealed.
        // One pass over the references: each role and ref is read exactly once (no per-role re-scan).
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        if (binding.SecretRefs.IsNotUndefined())
        {
            foreach (SourceCredentialBinding.SecretReference reference in binding.SecretRefs.EnumerateArray())
            {
                if (!SecretRef.TryParse((string)reference.Ref, out SecretRef secretRef))
                {
                    continue;
                }

                using SecretMaterial material = await this.resolver.ResolveAsync(secretRef, cancellationToken).ConfigureAwait(false);
                secrets[(string)reference.Name] = material.Reveal();
            }
        }

        if (binding.Config.IsNotUndefined())
        {
            foreach (SourceCredentialBinding.CredentialConfigEntry entry in binding.Config.EnumerateArray())
            {
                config[(string)entry.Key] = (string)entry.Value;
            }
        }

        try
        {
            return await factory.CreateTransportAsync(
                new ChannelTransportSettings(sourceName, this.environment, serverUrl, binding.AuthKindValue.ToJsonToken(), secrets, config),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            throw new WorkflowTransportBindingException(
                $"The channel credential for source '{sourceName}' in '{this.environment}' could not build a '{protocol}' transport: {ex.Message}");
        }
    }

    /// <summary>
    /// The shared per-source transport handle: connects on first use (single-flight) and delegates every
    /// operation to the connected transport, so the synchronous bind seam never waits on a broker handshake.
    /// </summary>
    private sealed class LazyChannelTransport(ChannelTransportCache cache, string sourceName, string protocol) : IMessageTransport
    {
        private readonly SemaphoreSlim connectGate = new(1, 1);
        private IMessageTransport? inner;

        public async ValueTask DisposeAsync()
        {
            // Take the gate so a shutdown never disposes the semaphore out from under an in-flight first-use
            // connect; the inner transport is detached and disposed under it.
            await this.connectGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.inner is { } transport)
                {
                    this.inner = null;
                    await transport.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                this.connectGate.Release();
                this.connectGate.Dispose();
            }
        }

        public ValueTask PublishAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, in TPayload payload, in JsonElement headers = default, CancellationToken cancellationToken = default)
            where TPayload : struct, IJsonElement<TPayload>
            => this.inner is { } transport
                ? transport.PublishAsync(channelUtf8, in payload, in headers, cancellationToken)
                : this.ConnectThenPublishAsync(channelUtf8, payload, headers, cancellationToken);

        public ValueTask PublishAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, in TPayload payload, in MessageContext context, in JsonElement headers = default, CancellationToken cancellationToken = default)
            where TPayload : struct, IJsonElement<TPayload>
            => this.inner is { } transport
                ? transport.PublishAsync(channelUtf8, in payload, in context, in headers, cancellationToken)
                : this.ConnectThenPublishAsync(channelUtf8, payload, context, headers, cancellationToken);

        public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(ReadOnlyMemory<byte> requestChannelUtf8, ReadOnlyMemory<byte> replyChannelUtf8, TRequest request, ReadOnlyMemory<byte> correlationIdUtf8, JsonElement headers = default, CancellationToken cancellationToken = default)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
            => this.inner is { } transport
                ? transport.RequestAsync<TRequest, TReply>(requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, headers, cancellationToken)
                : this.ConnectThenRequestAsync<TRequest, TReply>(requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, headers, cancellationToken);

        public ValueTask SubscribeAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, Func<TPayload, JsonElement, CancellationToken, ValueTask> handler, CancellationToken cancellationToken = default)
            where TPayload : struct, IJsonElement<TPayload>
            => this.inner is { } transport
                ? transport.SubscribeAsync(channelUtf8, handler, cancellationToken)
                : this.ConnectThenSubscribeAsync(channelUtf8, handler, cancellationToken);

        public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
            => this.inner is { } transport
                ? transport.UnsubscribeAsync(channelUtf8, cancellationToken)
                : this.ConnectThenUnsubscribeAsync(channelUtf8, cancellationToken);

        public ValueTask DeadLetterAsync(ReadOnlyMemory<byte> deadLetterChannelUtf8, ReadOnlyMemory<byte> originalChannelUtf8, in JsonElement payload, in JsonElement headers, Exception exception, CancellationToken cancellationToken = default)
            => this.inner is { } transport
                ? transport.DeadLetterAsync(deadLetterChannelUtf8, originalChannelUtf8, in payload, in headers, exception, cancellationToken)
                : this.ConnectThenDeadLetterAsync(deadLetterChannelUtf8, originalChannelUtf8, payload, headers, exception, cancellationToken);

        private async ValueTask<IMessageTransport> ConnectedAsync(CancellationToken cancellationToken)
        {
            if (this.inner is { } transport)
            {
                return transport;
            }

            await this.connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return this.inner ??= await cache.ConnectAsync(sourceName, protocol, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                this.connectGate.Release();
            }
        }

        private async ValueTask ConnectThenPublishAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, TPayload payload, JsonElement headers, CancellationToken cancellationToken)
            where TPayload : struct, IJsonElement<TPayload>
        {
            IMessageTransport transport = await this.ConnectedAsync(cancellationToken).ConfigureAwait(false);
            await transport.PublishAsync(channelUtf8, in payload, in headers, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask ConnectThenPublishAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, TPayload payload, MessageContext context, JsonElement headers, CancellationToken cancellationToken)
            where TPayload : struct, IJsonElement<TPayload>
        {
            IMessageTransport transport = await this.ConnectedAsync(cancellationToken).ConfigureAwait(false);
            await transport.PublishAsync(channelUtf8, in payload, in context, in headers, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<(TReply Payload, JsonElement Headers)> ConnectThenRequestAsync<TRequest, TReply>(ReadOnlyMemory<byte> requestChannelUtf8, ReadOnlyMemory<byte> replyChannelUtf8, TRequest request, ReadOnlyMemory<byte> correlationIdUtf8, JsonElement headers, CancellationToken cancellationToken)
            where TRequest : struct, IJsonElement<TRequest>
            where TReply : struct, IJsonElement<TReply>
        {
            IMessageTransport transport = await this.ConnectedAsync(cancellationToken).ConfigureAwait(false);
            return await transport.RequestAsync<TRequest, TReply>(requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, headers, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask ConnectThenSubscribeAsync<TPayload>(ReadOnlyMemory<byte> channelUtf8, Func<TPayload, JsonElement, CancellationToken, ValueTask> handler, CancellationToken cancellationToken)
            where TPayload : struct, IJsonElement<TPayload>
        {
            IMessageTransport transport = await this.ConnectedAsync(cancellationToken).ConfigureAwait(false);
            await transport.SubscribeAsync(channelUtf8, handler, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask ConnectThenUnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken)
        {
            IMessageTransport transport = await this.ConnectedAsync(cancellationToken).ConfigureAwait(false);
            await transport.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask ConnectThenDeadLetterAsync(ReadOnlyMemory<byte> deadLetterChannelUtf8, ReadOnlyMemory<byte> originalChannelUtf8, JsonElement payload, JsonElement headers, Exception exception, CancellationToken cancellationToken)
        {
            IMessageTransport transport = await this.ConnectedAsync(cancellationToken).ConfigureAwait(false);
            await transport.DeadLetterAsync(deadLetterChannelUtf8, originalChannelUtf8, in payload, in headers, exception, cancellationToken).ConfigureAwait(false);
        }
    }
}