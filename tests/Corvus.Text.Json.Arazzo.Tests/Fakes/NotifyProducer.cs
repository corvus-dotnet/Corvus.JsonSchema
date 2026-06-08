// <copyright file="NotifyProducer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace Acme.Notifications;

/// <summary>
/// A generated-style AsyncAPI producer (mirrors the real generated producer's shape) used by the
/// channel-step end-to-end test: it materialises the payload <c>Source</c> into a workspace and
/// publishes it on a fixed channel through the <see cref="IMessageTransport"/>.
/// </summary>
public sealed class NotifyProducer(IMessageTransport transport)
{
    private readonly IMessageTransport transport = transport;

    /// <summary>Publishes a <c>notify</c> message on the <c>notifications</c> channel.</summary>
    /// <param name="payload">The message payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the message has been published.</returns>
    public ValueTask PublishNotifyAsync(JsonElement.Source payload, CancellationToken cancellationToken = default)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        JsonElement built = JsonElement.CreateBuilder(workspace, payload).RootElement;
        return this.PublishCoreAsync(workspace, built, cancellationToken);
    }

    private async ValueTask PublishCoreAsync(JsonWorkspace workspace, JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            await this.transport.PublishAsync("notifications"u8.ToArray(), payload, default, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            workspace.Dispose();
        }
    }
}