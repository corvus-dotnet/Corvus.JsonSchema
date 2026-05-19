// <copyright file="JsonStreamReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Reads a stream of newline-delimited JSON (NDJSON) or Server-Sent Events (SSE)
/// and yields strongly-typed items parsed from each JSON payload.
/// </summary>
/// <remarks>
/// <para>All processing is performed directly on UTF-8 bytes using <see cref="PipeReader"/>.
/// No transcoding to UTF-16 occurs.</para>
/// </remarks>
public static class JsonStreamReader
{
    private static ReadOnlySpan<byte> DataColonSpace => "data: "u8;

    private static ReadOnlySpan<byte> DataColon => "data:"u8;

    private static ReadOnlySpan<byte> EventColon => "event:"u8;

    private static ReadOnlySpan<byte> IdColon => "id:"u8;

    private static ReadOnlySpan<byte> RetryColon => "retry:"u8;

    /// <summary>
    /// Asynchronously reads items from a stream containing newline-delimited JSON or SSE data.
    /// </summary>
    /// <typeparam name="T">The element type to parse each JSON payload into.</typeparam>
    /// <param name="stream">The source stream (NDJSON or SSE).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of parsed items.</returns>
    /// <remarks>
    /// <para>Supported formats:</para>
    /// <list type="bullet">
    ///   <item><description>NDJSON: each line is a complete JSON value.</description></item>
    ///   <item><description>SSE: lines prefixed with <c>data: </c> contain JSON payloads;
    ///   comment lines (starting with <c>:</c>) and metadata lines (<c>event:</c>, <c>id:</c>,
    ///   <c>retry:</c>) are skipped.</description></item>
    /// </list>
    /// <para>Empty lines are skipped. Each yielded item is a standalone value with no
    /// shared disposal requirements.</para>
    /// </remarks>
    public static async IAsyncEnumerable<T> ReadItemsAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        PipeReader pipeReader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    if (TryGetDataPayload(line, out ReadOnlySpan<byte> payload))
                    {
                        yield return JsonElementHelpers.ParseValue<T>(payload);
                    }
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    // Process any trailing data without a final newline.
                    if (buffer.Length > 0 && TryGetDataPayload(buffer, out ReadOnlySpan<byte> trailing))
                    {
                        yield return JsonElementHelpers.ParseValue<T>(trailing);
                    }

                    break;
                }
            }
        }
        finally
        {
            await pipeReader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously reads Server-Sent Events from a stream, yielding each event
    /// with its associated metadata (event type, id, retry) as raw UTF-8 bytes.
    /// </summary>
    /// <typeparam name="T">The element type to parse each JSON data payload into.</typeparam>
    /// <param name="stream">The source SSE stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of SSE events containing parsed data and metadata.</returns>
    /// <remarks>
    /// <para>Per the SSE specification, events are delimited by blank lines. Metadata fields
    /// (<c>event:</c>, <c>id:</c>, <c>retry:</c>) are accumulated until a <c>data:</c> field
    /// is encountered. Events are dispatched on blank lines or end-of-stream.</para>
    /// <para>Multi-line data fields (multiple consecutive <c>data:</c> lines before a blank line)
    /// are concatenated with U+000A per the SSE spec.</para>
    /// </remarks>
    public static async IAsyncEnumerable<SseEvent<T>> ReadSseItemsAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        PipeReader pipeReader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));

        try
        {
            ReadOnlyMemory<byte> eventType = default;
            ReadOnlyMemory<byte> id = default;
            int? retry = null;
            byte[]? dataBuffer = null;
            int dataLength = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    if (TryProcessSseLine(line, ref eventType, ref id, ref retry, ref dataBuffer, ref dataLength, out SseEvent<T>? evt) && evt.HasValue)
                    {
                        yield return evt.Value;
                    }
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    // Flush any pending event at end of stream.
                    if (dataLength > 0 && dataBuffer is not null)
                    {
                        T parsed = JsonElementHelpers.ParseValue<T>(dataBuffer.AsSpan(0, dataLength));
                        yield return new SseEvent<T>(parsed, eventType, id, retry);
                    }

                    if (dataBuffer is not null)
                    {
                        ArrayPool<byte>.Shared.Return(dataBuffer);
                    }

                    break;
                }
            }
        }
        finally
        {
            await pipeReader.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        SequencePosition? newlinePos = buffer.PositionOf((byte)'\n');
        if (newlinePos is null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, newlinePos.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, newlinePos.Value));

        // Trim trailing \r if present (CRLF).
        if (line.Length > 0)
        {
            ReadOnlySpan<byte> lastSegment = line.IsSingleSegment
                ? line.FirstSpan
                : line.ToArray();
            if (lastSegment.Length > 0 && lastSegment[^1] == (byte)'\r')
            {
                line = line.Slice(0, line.Length - 1);
            }
        }

        return true;
    }

    private static bool TryGetDataPayload(ReadOnlySequence<byte> line, out ReadOnlySpan<byte> payload)
    {
        ReadOnlySpan<byte> span = line.IsSingleSegment ? line.FirstSpan : line.ToArray();
        return TryGetDataPayload(span, out payload);
    }

    private static bool TryGetDataPayload(ReadOnlySpan<byte> span, out ReadOnlySpan<byte> payload)
    {
        // Skip empty lines.
        if (span.IsEmpty)
        {
            payload = default;
            return false;
        }

        // SSE comment lines start with ':'.
        if (span[0] == (byte)':')
        {
            payload = default;
            return false;
        }

        // SSE data lines: strip "data: " or "data:" prefix.
        if (span.StartsWith(DataColonSpace))
        {
            payload = span[6..];
            return !payload.IsEmpty;
        }

        if (span.StartsWith(DataColon))
        {
            payload = span[5..];
            return !payload.IsEmpty;
        }

        // Skip SSE metadata lines (event:, id:, retry:).
        if (span.StartsWith(EventColon) || span.StartsWith(IdColon) || span.StartsWith(RetryColon))
        {
            payload = default;
            return false;
        }

        // For NDJSON, the entire line is the payload if it starts with a JSON character.
        if (IsJsonStart(span[0]))
        {
            payload = span;
            return true;
        }

        payload = default;
        return false;
    }

    private static bool TryProcessSseLine<T>(
        ReadOnlySequence<byte> line,
        ref ReadOnlyMemory<byte> eventType,
        ref ReadOnlyMemory<byte> id,
        ref int? retry,
        ref byte[]? dataBuffer,
        ref int dataLength,
        out SseEvent<T>? result)
        where T : struct, IJsonElement<T>
    {
        ReadOnlySpan<byte> span = line.IsSingleSegment ? line.FirstSpan : line.ToArray();

        // Blank line = event boundary.
        if (span.IsEmpty)
        {
            if (dataLength > 0 && dataBuffer is not null)
            {
                T parsed = JsonElementHelpers.ParseValue<T>(dataBuffer.AsSpan(0, dataLength));
                result = new SseEvent<T>(parsed, eventType, id, retry);

                // Reset state for next event.
                eventType = default;
                id = default;
                retry = null;
                dataLength = 0;
                return true;
            }

            // Reset even if no data was accumulated.
            eventType = default;
            id = default;
            retry = null;
            result = null;
            return false;
        }

        // Comment lines.
        if (span[0] == (byte)':')
        {
            result = null;
            return false;
        }

        // data: field — accumulate payload bytes.
        if (span.StartsWith(DataColonSpace))
        {
            AppendDataBytes(span[6..], ref dataBuffer, ref dataLength);
            result = null;
            return false;
        }

        if (span.StartsWith(DataColon))
        {
            AppendDataBytes(span[5..], ref dataBuffer, ref dataLength);
            result = null;
            return false;
        }

        // event: field.
        if (span.StartsWith(EventColon))
        {
            ReadOnlySpan<byte> value = StripLeadingSpace(span[6..]);
            eventType = value.ToArray();
            result = null;
            return false;
        }

        // id: field.
        if (span.StartsWith(IdColon))
        {
            ReadOnlySpan<byte> value = StripLeadingSpace(span[3..]);
            id = value.ToArray();
            result = null;
            return false;
        }

        // retry: field.
        if (span.StartsWith(RetryColon))
        {
            ReadOnlySpan<byte> value = StripLeadingSpace(span[6..]);
            if (TryParseInt(value, out int retryMs))
            {
                retry = retryMs;
            }

            result = null;
            return false;
        }

        // Unknown field — skip per SSE spec.
        result = null;
        return false;
    }

    private static void AppendDataBytes(ReadOnlySpan<byte> data, ref byte[]? buffer, ref int length)
    {
        // Per SSE spec, multiple data: lines are concatenated with \n.
        int needed = length > 0 ? length + 1 + data.Length : data.Length;

        if (buffer is null || buffer.Length < needed)
        {
            int newSize = Math.Max(needed, 256);
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            if (buffer is not null)
            {
                buffer.AsSpan(0, length).CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(buffer);
            }

            buffer = newBuffer;
        }

        if (length > 0)
        {
            buffer[length] = (byte)'\n';
            length++;
        }

        data.CopyTo(buffer.AsSpan(length));
        length += data.Length;
    }

    private static ReadOnlySpan<byte> StripLeadingSpace(ReadOnlySpan<byte> value)
    {
        return value.Length > 0 && value[0] == (byte)' ' ? value[1..] : value;
    }

    private static bool TryParseInt(ReadOnlySpan<byte> utf8, out int value)
    {
        return System.Buffers.Text.Utf8Parser.TryParse(utf8, out value, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsJsonStart(byte b)
    {
        return b is (byte)'{' or (byte)'[' or (byte)'"' or (byte)'-'
            or (byte)'t' or (byte)'f' or (byte)'n'
            or >= (byte)'0' and <= (byte)'9';
    }
}