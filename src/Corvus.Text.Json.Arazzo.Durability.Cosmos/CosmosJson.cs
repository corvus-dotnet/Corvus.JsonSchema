// <copyright file="CosmosJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
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
    public static Stream WriteToStream<T>(in T value)
        where T : IJsonElement
        => WriteToStream(value, static (Utf8JsonWriter writer, in T v) => v.WriteTo(writer));

    /// <summary>
    /// Serializes JSON (written by <paramref name="write"/>) into a readable stream for a Cosmos stream write,
    /// allocation-leanly. The JSON is built through the thread-local pooled writer cache, which is rented, used, and
    /// returned <strong>synchronously here</strong> (the cache is thread-affine and must not cross an <c>await</c>); the
    /// resulting bytes are copied into an <see cref="ArrayPool{T}"/>-rented buffer wrapped by a non-growing stream that
    /// returns the buffer to the pool on <see cref="IDisposable.Dispose"/> (safe to dispose on any thread, after the
    /// Cosmos call). Nothing is GC-allocated per write: the writer, scratch buffer, and payload bytes are pooled, and the
    /// stream wrapper itself is a pooled <see cref="ReadOnlyMemoryStream"/> instance.
    /// </summary>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>A readable stream over the written UTF-8 JSON, positioned at the start.</returns>
    public static Stream WriteToStream<TContext>(in TContext context, PersistedJson.WriteCallback<TContext> write)
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
            ReadOnlyMemoryStream stream = ReadOnlyMemoryStream.RentOwned(payload, length);
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

    /// <summary>
    /// Reads a Cosmos response content stream fully into an <see cref="ArrayPool{T}"/>-rented buffer, returned as a
    /// disposable <see cref="RentedResponse"/> — no intermediate <see cref="MemoryStream"/> and no owned <see cref="byte"/>
    /// array. Every caller consumes the payload transiently (parse → <c>Clone</c>, or slice → parse-each), so the buffer's
    /// lifetime is the <c>using</c> scope; dispose returns it to the pool. The only per-read GC allocation is whatever the
    /// caller's <c>FromJson</c> clone genuinely retains.
    /// </summary>
    /// <param name="stream">The response content stream (may be <see langword="null"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The pooled UTF-8 payload (empty if the stream is <see langword="null"/> or empty); dispose to return the buffer.</returns>
    public static async ValueTask<RentedResponse> ReadAllAsync(Stream? stream, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            return default;
        }

        // Size from the stream length when seekable (the Cosmos SDK hands back a seekable MemoryStream), else grow the
        // rented buffer by doubling. Either way the scratch is pooled, not a MemoryStream + ToArray.
        int capacity = stream.CanSeek && stream.Length > 0 ? (int)stream.Length : DefaultBufferSize;
        byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);
        int total = 0;
        try
        {
            while (true)
            {
                if (total == rented.Length)
                {
                    byte[] larger = ArrayPool<byte>.Shared.Rent(rented.Length * 2);
                    Array.Copy(rented, larger, total);
                    ArrayPool<byte>.Shared.Return(rented);
                    rented = larger;
                }

                int read = await stream.ReadAsync(rented.AsMemory(total, rented.Length - total), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (total == 0)
            {
                ArrayPool<byte>.Shared.Return(rented);
                return default;
            }

            RentedResponse response = new(rented, total);
            rented = null!; // ownership transferred to the RentedResponse
            return response;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
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

    /// <summary>Decodes a base64 value held as raw UTF-8 (e.g. a generated base64-string property's
    /// <c>GetUtf8String().Span</c>) straight to an owned <see cref="byte"/> array via the UTF-8 buffer decoder — no
    /// intermediate managed base64 string. The decode runs through pooled scratch; the returned array is sized to the
    /// exact decoded length (the form a caller that keeps the bytes, e.g. a package/checkpoint, demands).</summary>
    /// <param name="utf8Base64">The base64 text as UTF-8 bytes.</param>
    /// <returns>The decoded bytes (one owned array).</returns>
    public static byte[] DecodeBase64Utf8(ReadOnlySpan<byte> utf8Base64)
    {
        if (utf8Base64.IsEmpty)
        {
            return [];
        }

        byte[] scratch = ArrayPool<byte>.Shared.Rent(Base64.GetMaxDecodedFromUtf8Length(utf8Base64.Length));
        try
        {
            Base64.DecodeFromUtf8(utf8Base64, scratch, out _, out int written);
            return scratch.AsSpan(0, written).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    /// <summary>Returns the <strong>raw UTF-8 bytes</strong> of a property's value (any JSON value — object, array,
    /// scalar) as a slice of <paramref name="element"/> — no copy, no decode. Used to read a document that was embedded
    /// verbatim as a nested JSON value (via <see cref="Utf8JsonWriter.WriteRawValue(ReadOnlySpan{byte}, bool)"/>) rather
    /// than base64-wrapped, so a JSON payload round-trips with no spurious encode/decode. Empty if the property is
    /// absent. The returned memory is a view into <paramref name="element"/>; copy it (e.g. parse it) before that
    /// backing buffer is reused.</summary>
    /// <param name="element">The element JSON.</param>
    /// <param name="propertyUtf8">The UTF-8 property name.</param>
    /// <returns>The value's raw UTF-8 bytes, or empty if absent.</returns>
    public static ReadOnlyMemory<byte> GetRawValue(ReadOnlyMemory<byte> element, ReadOnlySpan<byte> propertyUtf8)
    {
        var reader = new Utf8JsonReader(element.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return default;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool match = reader.ValueTextEquals(propertyUtf8);
            reader.Read();
            if (match)
            {
                int start = (int)reader.TokenStartIndex;
                reader.Skip();
                return element[start..(int)reader.BytesConsumed];
            }

            reader.Skip();
        }

        return default;
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

    /// <summary>
    /// A Cosmos response payload read into an <see cref="ArrayPool{T}"/>-rented buffer (see <see cref="ReadAllAsync"/>).
    /// The only allocation is the rented buffer, which <see cref="Dispose"/> returns to the pool; <c>default</c> is the
    /// empty payload (nothing rented). Read the bytes via <see cref="Memory"/>/<see cref="Span"/>; do not use after disposal.
    /// </summary>
    public readonly struct RentedResponse : IDisposable
    {
        private readonly byte[]? rented;
        private readonly int length;

        /// <summary>Initializes a new instance of the <see cref="RentedResponse"/> struct, taking ownership of the rented buffer.</summary>
        /// <param name="rented">The <see cref="ArrayPool{T}"/>-rented buffer (may be larger than <paramref name="length"/>).</param>
        /// <param name="length">The number of bytes read.</param>
        public RentedResponse(byte[] rented, int length)
        {
            this.rented = rented;
            this.length = length;
        }

        /// <summary>Gets the response payload; valid only until <see cref="Dispose"/>.</summary>
        public ReadOnlyMemory<byte> Memory => this.rented is null ? ReadOnlyMemory<byte>.Empty : this.rented.AsMemory(0, this.length);

        /// <summary>Gets the response payload; valid only until <see cref="Dispose"/>.</summary>
        public ReadOnlySpan<byte> Span => this.rented is null ? default : this.rented.AsSpan(0, this.length);

        /// <summary>Returns the rented buffer to the pool, if any.</summary>
        public void Dispose()
        {
            if (this.rented is not null)
            {
                ArrayPool<byte>.Shared.Return(this.rented);
            }
        }
    }
}