// <copyright file="QueryProducer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace Acme.Rpc;

/// <summary>
/// A generated-style AsyncAPI request/reply producer (mirrors the real generated producer's shape) used
/// by the request/reply channel-step end-to-end test: it materialises the request payload (as the real
/// producer would before sending) and returns a fixed reply payload. A real producer would round-trip
/// the request through <see cref="IMessageTransport.RequestAsync{TRequest, TReply}"/>.
/// </summary>
public sealed class QueryProducer(IMessageTransport transport)
{
    private readonly IMessageTransport transport = transport;

    /// <summary>Sends a <c>query</c> request and returns the reply payload.</summary>
    /// <param name="payload">The request payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply payload.</returns>
    public ValueTask<JsonElement> SendAndReceiveQueryAsync(JsonElement.Source payload, CancellationToken cancellationToken = default)
    {
        _ = this.transport;

        // Materialise the request payload the way the real producer does (proving the step bound it).
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        _ = JsonElement.CreateBuilder(workspace, payload).RootElement;
        workspace.Dispose();

        // Return a fixed reply. The parsed document is left to the GC, matching RequestAsync's
        // testing-transport semantics (the reply must outlive this call).
        JsonElement reply = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"answer":42,"status":"ok"}""")).RootElement;
        return new ValueTask<JsonElement>(reply);
    }
}
