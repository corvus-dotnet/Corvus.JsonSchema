// <copyright file="SimulationMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// The simulator's <see cref="IMessageTransport"/>: a channel SEND records the published message —
/// serialized exactly as a real transport would send it — into the owning <see cref="MockApiTransport"/>'s
/// exchange stream (as an <see cref="Corvus.Text.Json.OpenApi.OperationMethod.Publish"/> exchange on the
/// channel address), so the debugger's "exchanges — as sent" shows what each send carried. Nothing is
/// delivered anywhere: a simulation's receive steps suspend on the durable wait protocol and are released
/// by scenario triggers through the run (<c>DeliverMessage</c>), never through a broker.
/// </summary>
/// <remarks>
/// Request/reply sends are not yet simulated (<see cref="NotSupportedException"/> names the gap);
/// subscriptions are accepted as no-ops because the durable executor's receives never poll the
/// transport — the wait rides the run.
/// </remarks>
/// <param name="recorder">The transport whose exchange stream receives the publish records.</param>
/// <param name="triggers">The scenario's trigger queue — a request/reply send takes its scripted
/// reply from the next trigger on the REPLY channel (cursor shared with the wait loop).</param>
public sealed class SimulationMessageTransport(MockApiTransport recorder, SimulationTriggerQueue? triggers = null) : IMessageTransport
{
    /// <inheritdoc/>
    public ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            payload.WriteTo(writer);
        }

        // The string conversion sits at the outermost boundary, per the transport contract; the
        // payload bytes are the exchange's own copy (the trace outlives this call).
        recorder.RecordMessagePublish(Encoding.UTF8.GetString(channelUtf8.Span), buffer.WrittenSpan.ToArray());
        return default;
    }

    /// <inheritdoc/>
    public ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        // The reply comes from the scenario: the next trigger scripted on the REPLY channel. The
        // string conversions sit at the outermost boundary, per the transport contract.
        string requestChannel = Encoding.UTF8.GetString(requestChannelUtf8.Span);
        string replyChannel = Encoding.UTF8.GetString(replyChannelUtf8.Span);
        if (triggers is null || !triggers.TryTake(replyChannel, correlationId: null, out SimulationTrigger reply))
        {
            throw new InvalidOperationException($"The request/reply send on '{requestChannel}' has no scripted reply — add a scenario trigger for its reply channel '{replyChannel}' carrying the reply payload.");
        }

        var requestBuffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(requestBuffer))
        {
            request.WriteTo(writer);
        }

        var replyBuffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(replyBuffer))
        {
            reply.Payload.WriteTo(writer);
        }

        recorder.RecordMessagePublish(requestChannel, requestBuffer.WrittenSpan.ToArray(), replyBuffer.WrittenSpan.ToArray());

        // A free re-wrap of the trigger's own backing document (no parse, no copy); the scenario
        // outlives the simulation, and the executor clones the reply into the run workspace.
        return new((Rewrap<JsonElement, TReply>(reply.Payload), default));
    }

    /// <summary>Re-wraps an element as another type over the SAME backing document — the constrained-generic
    /// route to <see cref="IJsonElement.ParentDocument"/> (the members are interface-visible only).</summary>
    private static TTarget Rewrap<TSource, TTarget>(TSource source)
        where TSource : struct, IJsonElement<TSource>
        where TTarget : struct, IJsonElement<TTarget>
        => TTarget.CreateInstance(source.ParentDocument, source.ParentDocumentIndex);

    /// <inheritdoc/>
    public ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
        => default; // receives suspend on the durable wait protocol; triggers deliver through the run

    /// <inheritdoc/>
    public ValueTask UnsubscribeAsync(ReadOnlyMemory<byte> channelUtf8, CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc/>
    public ValueTask DeadLetterAsync(
        ReadOnlyMemory<byte> deadLetterChannelUtf8,
        ReadOnlyMemory<byte> originalChannelUtf8,
        in JsonElement payload,
        in JsonElement headers,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            payload.WriteTo(writer);
        }

        recorder.RecordMessagePublish(Encoding.UTF8.GetString(deadLetterChannelUtf8.Span), buffer.WrittenSpan.ToArray());
        return default;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;
}