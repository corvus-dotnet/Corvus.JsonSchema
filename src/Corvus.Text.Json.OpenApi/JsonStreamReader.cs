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
/// <para>Each yielded document or event is backed by pooled memory. The caller must
/// dispose each item when done to return memory to the pool.</para>
/// </remarks>
public static class JsonStreamReader
{
    private static ReadOnlySpan<byte> DataColonSpace => "data: "u8;

    private static ReadOnlySpan<byte> DataColon => "data:"u8;

    private static ReadOnlySpan<byte> EventColon => "event:"u8;

    private static ReadOnlySpan<byte> IdColon => "id:"u8;

    private static ReadOnlySpan<byte> RetryColon => "retry:"u8;

    /// <summary>
    /// Asynchronously reads items from a stream containing newline-delimited JSON or SSE data,
    /// yielding each as a disposable <see cref="ParsedJsonDocument{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type to parse each JSON payload into.</typeparam>
    /// <param name="stream">The source stream (NDJSON or SSE).</param>
    /// <param name="pipeReaderOptions">Optional <see cref="StreamPipeReaderOptions"/> for controlling
    /// the internal <see cref="PipeReader"/> buffer size and behavior.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of parsed documents. Each must be disposed by the caller.</returns>
    /// <remarks>
    /// <para>Supported formats:</para>
    /// <list type="bullet">
    ///   <item><description>NDJSON: each line is a complete JSON value.</description></item>
    ///   <item><description>SSE: lines prefixed with <c>data: </c> contain JSON payloads;
    ///   comment lines (starting with <c>:</c>) and metadata lines (<c>event:</c>, <c>id:</c>,
    ///   <c>retry:</c>) are skipped.</description></item>
    /// </list>
    /// <para>Empty lines are skipped. Each yielded document is backed by pooled memory
    /// and must be disposed when no longer needed.</para>
    /// </remarks>
    public static async IAsyncEnumerable<ParsedJsonDocument<T>> ReadItemsAsync<T>(
        Stream stream,
        StreamPipeReaderOptions? pipeReaderOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        PipeReader pipeReader = PipeReader.Create(stream, pipeReaderOptions ?? new StreamPipeReaderOptions(leaveOpen: true));
        byte[]? lineBuffer = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, ref lineBuffer, out ReadOnlySpan<byte> line))
                {
                    if (TryGetDataPayload(line, out ReadOnlySpan<byte> payload))
                    {
                        yield return ParseFromPayload<T>(payload);
                    }
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    // Process any trailing data without a final newline.
                    if (buffer.Length > 0)
                    {
                        ReadOnlySpan<byte> trailing = Linearize(buffer, ref lineBuffer);
                        if (TryGetDataPayload(trailing, out ReadOnlySpan<byte> trailingPayload))
                        {
                            yield return ParseFromPayload<T>(trailingPayload);
                        }
                    }

                    break;
                }
            }
        }
        finally
        {
            if (lineBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(lineBuffer);
            }

            await pipeReader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously reads Server-Sent Events from a stream, yielding each event
    /// with its associated metadata (event type, id, retry) as raw UTF-8 bytes.
    /// </summary>
    /// <typeparam name="T">The element type to parse each JSON data payload into.</typeparam>
    /// <param name="stream">The source SSE stream.</param>
    /// <param name="pipeReaderOptions">Optional <see cref="StreamPipeReaderOptions"/> for controlling
    /// the internal <see cref="PipeReader"/> buffer size and behavior.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of SSE events. Each must be disposed by the caller.</returns>
    /// <remarks>
    /// <para>Per the SSE specification, events are delimited by blank lines. Metadata fields
    /// (<c>event:</c>, <c>id:</c>, <c>retry:</c>) are accumulated until a <c>data:</c> field
    /// is encountered. Events are dispatched on blank lines or end-of-stream.</para>
    /// <para>Multi-line data fields (multiple consecutive <c>data:</c> lines before a blank line)
    /// are concatenated with U+000A per the SSE spec.</para>
    /// <para>Each yielded <see cref="SseEvent{T}"/> is backed by pooled memory
    /// and must be disposed when no longer needed.</para>
    /// </remarks>
    public static async IAsyncEnumerable<SseEvent<T>> ReadSseItemsAsync<T>(
        Stream stream,
        StreamPipeReaderOptions? pipeReaderOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        PipeReader pipeReader = PipeReader.Create(stream, pipeReaderOptions ?? new StreamPipeReaderOptions(leaveOpen: true));
        byte[]? lineBuffer = null;

        try
        {
            ReadOnlyMemory<byte> eventType = default;
            ReadOnlyMemory<byte> id = default;
            int? retry = null;
            byte[]? dataBuffer = null;
            int dataLength = 0;
            byte[]? metadataBuffer = null;
            int metadataLength = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, ref lineBuffer, out ReadOnlySpan<byte> line))
                {
                    if (TryProcessSseLine(line, ref eventType, ref id, ref retry, ref dataBuffer, ref dataLength, ref metadataBuffer, ref metadataLength, out SseEvent<T>? evt) && evt.HasValue)
                    {
                        yield return evt.Value;
                    }
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    // Process any trailing line without a final newline.
                    if (buffer.Length > 0)
                    {
                        ReadOnlySpan<byte> trailing = Linearize(buffer, ref lineBuffer);
                        TryProcessSseLine<T>(trailing, ref eventType, ref id, ref retry, ref dataBuffer, ref dataLength, ref metadataBuffer, ref metadataLength, out _);
                    }

                    // Flush any pending event at end of stream.
                    if (dataLength > 0 && dataBuffer is not null)
                    {
                        ParsedJsonDocument<T> doc = ParsedJsonDocument<T>.Parse(dataBuffer.AsMemory(0, dataLength), dataBuffer);
                        yield return new SseEvent<T>(doc, eventType, id, retry, metadataBuffer);
                        dataBuffer = null;
                        metadataBuffer = null;
                    }
                    else
                    {
                        if (dataBuffer is not null)
                        {
                            ArrayPool<byte>.Shared.Return(dataBuffer);
                        }

                        if (metadataBuffer is not null)
                        {
                            ArrayPool<byte>.Shared.Return(metadataBuffer);
                        }
                    }

                    break;
                }
            }
        }
        finally
        {
            if (lineBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(lineBuffer);
            }

            await pipeReader.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static ParsedJsonDocument<T> ParseFromPayload<T>(ReadOnlySpan<byte> payload)
        where T : struct, IJsonElement<T>
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(payload.Length);
        payload.CopyTo(rented);
        return ParsedJsonDocument<T>.Parse(rented.AsMemory(0, payload.Length), rented);
    }

    /// <summary>
    /// Linearizes a <see cref="ReadOnlySequence{T}"/> into a contiguous span,
    /// reusing a caller-owned buffer to avoid per-line allocation.
    /// </summary>
    private static ReadOnlySpan<byte> Linearize(ReadOnlySequence<byte> sequence, ref byte[]? lineBuffer)
    {
        if (sequence.IsSingleSegment)
        {
            return sequence.FirstSpan;
        }

        int length = (int)sequence.Length;
        if (lineBuffer is null || lineBuffer.Length < length)
        {
            if (lineBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(lineBuffer);
            }

            lineBuffer = ArrayPool<byte>.Shared.Rent(length);
        }

        sequence.CopyTo(lineBuffer);
        return lineBuffer.AsSpan(0, length);
    }

    /// <summary>
    /// Extracts the next complete line (up to <c>\n</c>) from the buffer, trims <c>\r</c>,
    /// and linearizes it into a contiguous span using the caller-owned buffer.
    /// </summary>
    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, ref byte[]? lineBuffer, out ReadOnlySpan<byte> line)
    {
        SequencePosition? newlinePos = buffer.PositionOf((byte)'\n');
        if (newlinePos is null)
        {
            line = default;
            return false;
        }

        ReadOnlySequence<byte> lineSequence = buffer.Slice(0, newlinePos.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, newlinePos.Value));

        // Linearize and trim trailing \r if present (CRLF).
        ReadOnlySpan<byte> span = Linearize(lineSequence, ref lineBuffer);
        if (span.Length > 0 && span[^1] == (byte)'\r')
        {
            span = span[..^1];
        }

        line = span;
        return true;
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
        ReadOnlySpan<byte> span,
        ref ReadOnlyMemory<byte> eventType,
        ref ReadOnlyMemory<byte> id,
        ref int? retry,
        ref byte[]? dataBuffer,
        ref int dataLength,
        ref byte[]? metadataBuffer,
        ref int metadataLength,
        out SseEvent<T>? result)
        where T : struct, IJsonElement<T>
    {
        // Blank line = event boundary.
        if (span.IsEmpty)
        {
            if (dataLength > 0 && dataBuffer is not null)
            {
                // Transfer ownership of the data buffer to the parsed document.
                ParsedJsonDocument<T> doc = ParsedJsonDocument<T>.Parse(dataBuffer.AsMemory(0, dataLength), dataBuffer);
                result = new SseEvent<T>(doc, eventType, id, retry, metadataBuffer);

                // Reset state for next event — buffers are now owned by the SseEvent.
                dataBuffer = null;
                metadataBuffer = null;
                eventType = default;
                id = default;
                retry = null;
                dataLength = 0;
                metadataLength = 0;
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
            eventType = AppendMetadata(value, ref metadataBuffer, ref metadataLength);
            result = null;
            return false;
        }

        // id: field.
        if (span.StartsWith(IdColon))
        {
            ReadOnlySpan<byte> value = StripLeadingSpace(span[3..]);
            id = AppendMetadata(value, ref metadataBuffer, ref metadataLength);
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

    private static ReadOnlyMemory<byte> AppendMetadata(ReadOnlySpan<byte> value, ref byte[]? buffer, ref int length)
    {
        if (value.IsEmpty)
        {
            return default;
        }

        int needed = length + value.Length;
        if (buffer is null || buffer.Length < needed)
        {
            int newSize = Math.Max(needed, 128);
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            if (buffer is not null)
            {
                buffer.AsSpan(0, length).CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(buffer);
            }

            buffer = newBuffer;
        }

        int start = length;
        value.CopyTo(buffer.AsSpan(length));
        length += value.Length;
        return buffer.AsMemory(start, value.Length);
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