// <copyright file="CosmosJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// Corvus.Text.Json helpers for talking to Azure Cosmos DB through its <em>stream</em> item and query APIs. The
/// Cosmos stream surface bypasses the SDK's own (reflection) serializer entirely: the store writes a document's
/// canonical JSON bytes (produced by a generated schema type) and reads responses by parsing the raw payload with
/// the Corvus.Text.Json reader — so no <c>System.Text.Json.JsonSerializer</c> or hand-rolled POCO is ever involved.
/// </summary>
internal static class CosmosJson
{
    private const int DefaultBufferSize = 512;

    private static readonly byte[] DocumentsPropertyUtf8 = Encoding.UTF8.GetBytes("Documents");
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>
    /// Writes a generated document's JSON into a readable stream for a Cosmos stream write, allocation-leanly: the JSON
    /// is serialized through the thread-local pooled writer cache and the bytes handed to the SDK via an
    /// <see cref="ArrayPool{T}"/>-backed, non-growing stream — so the only per-write GC allocation is the small stream
    /// wrapper (the SDK requires a <see cref="Stream"/>). The caller owns and disposes the stream (which returns its
    /// rented buffer to the pool); it is positioned at the start.
    /// </summary>
    /// <typeparam name="T">The Corvus.Text.Json document type.</typeparam>
    /// <param name="value">The document to serialize.</param>
    /// <returns>A readable stream over the document's UTF-8 JSON, positioned at the start.</returns>
    public static MemoryStream WriteToStream<T>(in T value)
        where T : IJsonElement
        => WriteToStream(value, static (Utf8JsonWriter writer, in T v) => v.WriteTo(writer));

    /// <summary>
    /// Serializes JSON (written by <paramref name="write"/>) into a readable stream for a Cosmos stream write,
    /// allocation-leanly. The JSON is built through the thread-local pooled writer cache, which is rented, used, and
    /// returned <strong>synchronously here</strong> (the cache is thread-affine and must not cross an <c>await</c>); the
    /// resulting bytes are copied into an <see cref="ArrayPool{T}"/>-rented buffer wrapped by a non-growing stream that
    /// returns the buffer to the pool on <see cref="IDisposable.Dispose"/> (safe to dispose on any thread, after the
    /// Cosmos call). The only per-write GC allocation is the stream wrapper the SDK's <see cref="Stream"/> contract
    /// forces; the writer, scratch buffer, and payload bytes are all pooled.
    /// </summary>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>A readable stream over the written UTF-8 JSON, positioned at the start.</returns>
    public static MemoryStream WriteToStream<TContext>(in TContext context, PersistedJson.WriteCallback<TContext> write)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        byte[]? payload = null;
        try
        {
            write(writer, in context);
            writer.Flush();
            ReadOnlySpan<byte> written = buffer.WrittenSpan;
            int length = written.Length;
            payload = ArrayPool<byte>.Shared.Rent(length);
            written.CopyTo(payload);
            var stream = new PooledWriteStream(payload, length);
            payload = null; // ownership transferred to the stream
            return stream;
        }
        catch
        {
            if (payload is not null)
            {
                ArrayPool<byte>.Shared.Return(payload);
            }

            throw;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Serializes JSON (written by <paramref name="write"/>) into an <see cref="ArrayPool{T}"/>-rented buffer, returned
    /// as a disposable <see cref="RentedJson"/>. Use it to embed a sub-document as base64 inside an envelope without a
    /// nested writer rent or an owned <see cref="byte"/> array: serialize it once here, then <c>WriteBase64String</c> its
    /// <see cref="RentedJson.Span"/> inside a <see cref="WriteToStream{TContext}(in TContext, PersistedJson.WriteCallback{TContext})"/>
    /// envelope callback, and dispose it once the envelope stream has been built. The pooled writer cache is rented and
    /// returned synchronously here, so it never crosses an <c>await</c>.
    /// </summary>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>The serialized JSON over a pooled buffer; dispose to return the buffer to the pool.</returns>
    public static RentedJson RentJson<TContext>(in TContext context, PersistedJson.WriteCallback<TContext> write)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            write(writer, in context);
            writer.Flush();
            ReadOnlySpan<byte> written = buffer.WrittenSpan;
            byte[] rented = ArrayPool<byte>.Shared.Rent(written.Length);
            written.CopyTo(rented);
            return new RentedJson(rented, written.Length);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Reads a Cosmos response content stream fully into an owned UTF-8 buffer.</summary>
    /// <param name="stream">The response content stream (may be <see langword="null"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The UTF-8 payload (empty if the stream is <see langword="null"/> or empty).</returns>
    public static async ValueTask<ReadOnlyMemory<byte>> ReadAllAsync(Stream? stream, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// Returns the raw JSON of each element of a Cosmos query page's top-level <c>Documents</c> array. Each slice is
    /// itself a complete JSON value (object or scalar), ready to hand to a generated type's <c>FromJson</c> or to one
    /// of the projection readers below.
    /// </summary>
    /// <param name="page">The query page payload (<c>{"Documents":[ ... ],"_count": n}</c>).</param>
    /// <returns>The element payloads, in document order.</returns>
    public static List<ReadOnlyMemory<byte>> ReadDocuments(ReadOnlyMemory<byte> page)
    {
        var elements = new List<ReadOnlyMemory<byte>>();
        if (page.IsEmpty)
        {
            return elements;
        }

        var reader = new Utf8JsonReader(page.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return elements;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isDocuments = reader.ValueTextEquals(DocumentsPropertyUtf8);
            reader.Read();
            if (isDocuments)
            {
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        int start = (int)reader.TokenStartIndex;
                        reader.Skip();
                        int end = (int)reader.BytesConsumed;
                        elements.Add(page.Slice(start, end - start));
                    }
                }

                return elements;
            }

            reader.Skip();
        }

        return elements;
    }

    /// <summary>Reads a string property from a projection element, or <see langword="null"/> if absent/null.</summary>
    /// <param name="element">The element JSON.</param>
    /// <param name="propertyUtf8">The UTF-8 property name.</param>
    /// <returns>The string value, or <see langword="null"/>.</returns>
    public static string? GetString(ReadOnlyMemory<byte> element, ReadOnlySpan<byte> propertyUtf8)
    {
        var reader = new Utf8JsonReader(element.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool match = reader.ValueTextEquals(propertyUtf8);
            reader.Read();
            if (match)
            {
                return reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            }

            reader.Skip();
        }

        return null;
    }

    /// <summary>Reads an integer property from a projection element, or <see langword="null"/> if absent/not a number.</summary>
    /// <param name="element">The element JSON.</param>
    /// <param name="propertyUtf8">The UTF-8 property name.</param>
    /// <returns>The numeric value, or <see langword="null"/>.</returns>
    public static long? GetInt64(ReadOnlyMemory<byte> element, ReadOnlySpan<byte> propertyUtf8)
    {
        var reader = new Utf8JsonReader(element.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool match = reader.ValueTextEquals(propertyUtf8);
            reader.Read();
            if (match)
            {
                return reader.TokenType == JsonTokenType.Number ? reader.GetInt64() : null;
            }

            reader.Skip();
        }

        return null;
    }

    /// <summary>Reads a bare scalar element (a <c>SELECT VALUE</c> projection) as an <see cref="long"/>.</summary>
    /// <param name="element">The element JSON.</param>
    /// <returns>The numeric value, or <see langword="null"/> if the element is null or not a number.</returns>
    public static long? AsInt64OrNull(ReadOnlyMemory<byte> element)
    {
        var reader = new Utf8JsonReader(element.Span);
        return reader.Read() && reader.TokenType == JsonTokenType.Number ? reader.GetInt64() : null;
    }

    /// <summary>
    /// JSON serialized into an <see cref="ArrayPool{T}"/>-rented buffer (see <see cref="RentJson{TContext}"/>). The only
    /// allocation is the rented buffer, which <see cref="Dispose"/> returns to the pool; the struct itself is a thin
    /// view. Read the bytes via <see cref="Span"/>; do not use after disposal.
    /// </summary>
    public readonly struct RentedJson : IDisposable
    {
        private readonly byte[] rented;
        private readonly int length;

        /// <summary>Initializes a new instance of the <see cref="RentedJson"/> struct, taking ownership of the rented buffer.</summary>
        /// <param name="rented">The <see cref="ArrayPool{T}"/>-rented buffer (may be larger than <paramref name="length"/>).</param>
        /// <param name="length">The number of written bytes.</param>
        public RentedJson(byte[] rented, int length)
        {
            this.rented = rented;
            this.length = length;
        }

        /// <summary>Gets the written UTF-8 JSON; valid only until <see cref="Dispose"/>.</summary>
        public ReadOnlySpan<byte> Span => this.rented.AsSpan(0, this.length);

        /// <summary>Returns the rented buffer to the pool.</summary>
        public void Dispose() => ArrayPool<byte>.Shared.Return(this.rented);
    }

    // A read-only, non-growing MemoryStream over an ArrayPool-rented buffer that returns the buffer to the pool on
    // Dispose. The buffer is rented synchronously when the stream is built and returned when the caller disposes the
    // stream after the Cosmos call (ArrayPool.Return is thread-safe, so disposing on a continuation thread is fine).
    private sealed class PooledWriteStream : MemoryStream
    {
        private byte[]? rented;

        public PooledWriteStream(byte[] rented, int length)
            : base(rented, 0, length, writable: false, publiclyVisible: false)
            => this.rented = rented;

        protected override void Dispose(bool disposing)
        {
            byte[]? toReturn = this.rented;
            this.rented = null;
            base.Dispose(disposing);
            if (toReturn is not null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }
    }
}