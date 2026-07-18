// <copyright file="HeaderNotifyProducer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace Acme.Rpc;

/// <summary>
/// A generated-style AsyncAPI producer for a message that declares typed <c>headers</c> (mirrors the real
/// generated producer's shape): both the fire-and-forget publish and the request/reply method take a
/// <c>headers</c> argument immediately after the payload. Used by the send-side message-header end-to-end
/// tests. The request/reply method echoes the headers it was sent back as the reply so a test can assert
/// the header object the Arazzo step assembled from its <c>in: header</c> parameters.
/// </summary>
public sealed class HeaderNotifyProducer(IMessageTransport transport)
{
    private readonly IMessageTransport transport = transport;

    /// <summary>Publishes a <c>notify</c> message with headers (fire-and-forget).</summary>
    /// <param name="payload">The message payload.</param>
    /// <param name="headers">The message headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the message is published.</returns>
    public ValueTask PublishNotifyAsync(JsonElement.Source payload, JsonElement.Source headers, CancellationToken cancellationToken = default)
    {
        _ = this.transport;

        // Materialise both the way the real producer does (proving the step bound them).
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        _ = JsonElement.CreateBuilder(workspace, payload).RootElement;
        _ = JsonElement.CreateBuilder(workspace, headers).RootElement;
        workspace.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Sends a <c>notify</c> request with headers and returns a reply that echoes those headers.</summary>
    /// <param name="payload">The request payload.</param>
    /// <param name="headers">The message headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply payload, of the form <c>{ "sentHeaders": &lt;headers&gt; }</c>.</returns>
    public ValueTask<JsonElement> SendAndReceiveNotifyAsync(JsonElement.Source payload, JsonElement.Source headers, CancellationToken cancellationToken = default)
    {
        _ = this.transport;

        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        JsonElement headersElement = JsonElement.CreateBuilder(workspace, headers).RootElement;
        string headersJson = headersElement.GetRawText();
        workspace.Dispose();

        // Return a reply echoing the headers. The parsed document is left to the GC, matching the other
        // producer fakes' semantics (the reply must outlive this call).
        JsonElement reply = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes($$"""{"sentHeaders":{{headersJson}}}""")).RootElement;
        return new ValueTask<JsonElement>(reply);
    }
}
