// <copyright file="MultipartFormDataSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Serializes a JSON object to <c>multipart/form-data</c> format.
/// </summary>
/// <remarks>
/// <para>
/// Per RFC 7578, each property of the JSON object becomes a separate part
/// with a <c>Content-Disposition: form-data; name="..."</c> header.
/// </para>
/// <para>
/// Default behavior (no Encoding Object overrides):
/// </para>
/// <list type="bullet">
/// <item><description>Primitive properties (<c>string</c>, <c>number</c>, <c>integer</c>,
/// <c>boolean</c>): written as plain text.</description></item>
/// <item><description>Complex properties (<c>object</c>, <c>array</c>): JSON-stringified
/// with <c>Content-Type: application/json</c>.</description></item>
/// <item><description>Null properties: written as empty parts.</description></item>
/// </list>
/// <para>
/// When an Encoding Object specifies <c>contentType</c> per property, the overload
/// accepting a <see cref="IReadOnlyDictionary{TKey, TValue}"/> of
/// <see cref="PropertyEncoding"/> applies those overrides.
/// </para>
/// </remarks>
public static class MultipartFormDataSerializer
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Generates a unique MIME multipart boundary string.
    /// </summary>
    /// <returns>A boundary string safe for use in multipart messages.</returns>
    public static string GenerateBoundary()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>multipart/form-data</c> format using default encoding.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the multipart body to.</param>
    /// <param name="boundary">The boundary string (must match the one in the Content-Type header).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static void Serialize<T>(in T value, Stream output, string boundary)
        where T : struct, IJsonElement<T>
    {
        Serialize(value, output, boundary, null);
    }

    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>multipart/form-data</c> format, applying per-property encoding
    /// overrides from the OpenAPI Encoding Object.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the multipart body to.</param>
    /// <param name="boundary">The boundary string (must match the one in the Content-Type header).</param>
    /// <param name="encodings">
    /// Per-property encoding overrides keyed by property name, or <see langword="null"/>
    /// to use default encoding for all properties.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static void Serialize<T>(
        in T value,
        Stream output,
        string boundary,
        IReadOnlyDictionary<string, PropertyEncoding>? encodings)
        where T : struct, IJsonElement<T>
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 256, leaveOpen: true);

        foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
        {
            string name = property.Name;

            writer.Write("--");
            writer.Write(boundary);
            writer.Write("\r\n");

            writer.Write("Content-Disposition: form-data; name=\"");
            writer.Write(name);
            writer.Write("\"\r\n");

            JsonElement propValue = property.Value;

            PropertyEncoding enc = default;
            encodings?.TryGetValue(name, out enc);

            string? contentTypeOverride = enc.ContentType;

            switch (propValue.ValueKind)
            {
                case JsonValueKind.String:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    writer.Write(propValue.GetString());
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    writer.Write(propValue.ToString());
                    break;

                case JsonValueKind.Null:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    break;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    writer.Write("Content-Type: ");
                    writer.Write(contentTypeOverride ?? "application/json");
                    writer.Write("\r\n\r\n");
                    writer.Write(propValue.ToString());
                    break;
            }

            writer.Write("\r\n");
        }

        // Final boundary with closing "--".
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("--\r\n");
    }

    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>multipart/form-data</c> format, applying per-property encoding
    /// overrides and substituting binary parts for specified properties.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the multipart body to.</param>
    /// <param name="boundary">The boundary string (must match the one in the Content-Type header).</param>
    /// <param name="encodings">
    /// Per-property encoding overrides keyed by property name, or <see langword="null"/>
    /// to use default encoding for all properties.
    /// </param>
    /// <param name="binaryParts">
    /// Binary part data keyed by property name, or <see langword="null"/> if there
    /// are no binary parts. Properties that appear here are written as raw binary
    /// content instead of their JSON representation.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static async ValueTask SerializeAsync<T>(
        T value,
        Stream output,
        string boundary,
        IReadOnlyDictionary<string, PropertyEncoding>? encodings,
        IReadOnlyDictionary<string, BinaryPartData>? binaryParts,
        CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 256, leaveOpen: true);

        foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
        {
            string name = property.Name;

            // Check if this property is a binary part.
            if (binaryParts is not null && binaryParts.TryGetValue(name, out BinaryPartData binaryPart))
            {
                await WriteBinaryPartAsync(writer, output, boundary, name, binaryPart, cancellationToken).ConfigureAwait(false);
                continue;
            }

            writer.Write("--");
            writer.Write(boundary);
            writer.Write("\r\n");

            writer.Write("Content-Disposition: form-data; name=\"");
            writer.Write(name);
            writer.Write("\"\r\n");

            JsonElement propValue = property.Value;

            PropertyEncoding enc = default;
            encodings?.TryGetValue(name, out enc);

            string? contentTypeOverride = enc.ContentType;

            switch (propValue.ValueKind)
            {
                case JsonValueKind.String:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    writer.Write(propValue.GetString());
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    writer.Write(propValue.ToString());
                    break;

                case JsonValueKind.Null:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    break;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    writer.Write("Content-Type: ");
                    writer.Write(contentTypeOverride ?? "application/json");
                    writer.Write("\r\n\r\n");
                    writer.Write(propValue.ToString());
                    break;
            }

            writer.Write("\r\n");
        }

        // Write any binary parts that don't correspond to JSON properties
        // (e.g. the JSON body used a placeholder or omitted the field).
        if (binaryParts is not null)
        {
            HashSet<string> seen = [];
            foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
            {
                seen.Add(property.Name);
            }

            foreach (KeyValuePair<string, BinaryPartData> kvp in binaryParts)
            {
                if (!seen.Contains(kvp.Key))
                {
                    await WriteBinaryPartAsync(writer, output, boundary, kvp.Key, kvp.Value, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // Final boundary with closing "--".
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("--\r\n");
    }

    /// <summary>
    /// Deserializes a <c>multipart/form-data</c> body into a
    /// <see cref="ParsedJsonDocument{T}"/>, building a JSON object from
    /// the text and JSON parts.
    /// </summary>
    /// <typeparam name="T">The JSON element type to parse into.</typeparam>
    /// <param name="multipartBody">The raw UTF-8 multipart body bytes.</param>
    /// <param name="boundary">The boundary string (UTF-8, without leading <c>--</c>).</param>
    /// <param name="binaryPartCallback">
    /// Optional callback invoked for each binary part. If <see langword="null"/>,
    /// binary parts are silently skipped.
    /// </param>
    /// <returns>A parsed JSON document backed by pooled memory. The caller must dispose it.</returns>
    public static ParsedJsonDocument<T> Deserialize<T>(
        ReadOnlyMemory<byte> multipartBody,
        ReadOnlySpan<byte> boundary,
        MultipartFormReader.BinaryPartHandler? binaryPartCallback = null)
        where T : struct, IJsonElement<T>
    {
        using PooledBufferWriter jsonBuffer = new(multipartBody.Length);
        using Utf8JsonWriter jsonWriter = new(jsonBuffer);

        MultipartFormReader.DeserializeToJson(multipartBody.Span, boundary, jsonWriter, binaryPartCallback);
        jsonWriter.Flush();

        return ParsedJsonDocument<T>.Parse(jsonBuffer.WrittenMemory);
    }

    /// <summary>
    /// Deserializes a <c>multipart/form-data</c> body from a stream into a
    /// <see cref="ParsedJsonDocument{T}"/>.
    /// </summary>
    /// <typeparam name="T">The JSON element type to parse into.</typeparam>
    /// <param name="stream">The request body stream.</param>
    /// <param name="contentType">The Content-Type header value (used to extract the boundary).</param>
    /// <param name="binaryPartCallback">
    /// Optional callback invoked for each binary part. If <see langword="null"/>,
    /// binary parts are silently skipped.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A parsed JSON document backed by pooled memory. The caller must dispose it.</returns>
    public static async ValueTask<ParsedJsonDocument<T>> DeserializeAsync<T>(
        Stream stream,
        ReadOnlyMemory<byte> contentType,
        MultipartFormReader.BinaryPartHandler? binaryPartCallback = null,
        CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        if (!MultipartFormReader.TryExtractBoundary(contentType.Span, out ReadOnlySpan<byte> boundarySpan))
        {
            ThrowHelper.ThrowMultipartBoundaryNotFound();
        }

        // Copy boundary to a rented array — we need it to survive the async state machine.
        byte[] boundaryBuffer = FormFieldReader.Rent(boundarySpan.Length);
        boundarySpan.CopyTo(boundaryBuffer);
        ReadOnlyMemory<byte> boundaryMemory = boundaryBuffer.AsMemory(0, boundarySpan.Length);

        return await DeserializeWithRentedBoundaryAsync<T>(stream, boundaryBuffer, boundaryMemory, binaryPartCallback, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes a <c>multipart/form-data</c> body from a stream into a
    /// <see cref="ParsedJsonDocument{T}"/>, extracting the boundary from the
    /// Content-Type header string without allocating.
    /// </summary>
    /// <typeparam name="T">The JSON element type to parse into.</typeparam>
    /// <param name="stream">The request body stream.</param>
    /// <param name="contentType">The Content-Type header string (e.g., <c>multipart/form-data; boundary=abc</c>).</param>
    /// <param name="binaryPartCallback">
    /// Optional callback invoked for each binary part. If <see langword="null"/>,
    /// binary parts are silently skipped.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A parsed JSON document backed by pooled memory. The caller must dispose it.</returns>
    public static ValueTask<ParsedJsonDocument<T>> DeserializeAsync<T>(
        Stream stream,
        string? contentType,
        MultipartFormReader.BinaryPartHandler? binaryPartCallback = null,
        CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        // Encode Content-Type string to UTF-8 using a rented buffer.
        int byteCount = contentType is not null
            ? System.Text.Encoding.UTF8.GetByteCount(contentType)
            : 0;

        byte[] ctBuffer = FormFieldReader.Rent(Math.Max(byteCount, 1));
        try
        {
            if (contentType is not null)
            {
                System.Text.Encoding.UTF8.GetBytes(contentType, 0, contentType.Length, ctBuffer, 0);
            }

            if (!MultipartFormReader.TryExtractBoundary(ctBuffer.AsSpan(0, byteCount), out ReadOnlySpan<byte> boundarySpan))
            {
                ThrowHelper.ThrowMultipartBoundaryNotFound();
            }

            // Copy boundary to a rented array that survives the async state machine.
            byte[] boundaryBuffer = FormFieldReader.Rent(boundarySpan.Length);
            boundarySpan.CopyTo(boundaryBuffer);
            ReadOnlyMemory<byte> boundaryMemory = boundaryBuffer.AsMemory(0, boundarySpan.Length);

            // We can now return the content-type buffer before going async.
            FormFieldReader.Return(ctBuffer);
            ctBuffer = null!;

            return DeserializeWithRentedBoundaryAsync<T>(stream, boundaryBuffer, boundaryMemory, binaryPartCallback, cancellationToken);
        }
        finally
        {
            if (ctBuffer is not null)
            {
                FormFieldReader.Return(ctBuffer);
            }
        }
    }

    private static async ValueTask<ParsedJsonDocument<T>> DeserializeWithRentedBoundaryAsync<T>(
        Stream stream,
        byte[] boundaryBuffer,
        ReadOnlyMemory<byte> boundaryMemory,
        MultipartFormReader.BinaryPartHandler? binaryPartCallback,
        CancellationToken cancellationToken)
        where T : struct, IJsonElement<T>
    {
        try
        {
            (byte[] buffer, int length) = await FormFieldReader.RentBodyAsync(stream, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                return Deserialize<T>(
                    buffer.AsMemory(0, length),
                    boundaryMemory.Span,
                    binaryPartCallback);
            }
            finally
            {
                FormFieldReader.Return(buffer);
            }
        }
        finally
        {
            FormFieldReader.Return(boundaryBuffer);
        }
    }

    private static async ValueTask WriteBinaryPartAsync(
        StreamWriter writer,
        Stream output,
        string boundary,
        string name,
        BinaryPartData binaryPart,
        CancellationToken cancellationToken)
    {
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("\r\n");

        writer.Write("Content-Disposition: form-data; name=\"");
        writer.Write(name);
        writer.Write("\"");

        if (binaryPart.FileName is not null)
        {
            writer.Write("; filename=\"");
            writer.Write(binaryPart.FileName);
            writer.Write("\"");
        }

        writer.Write("\r\n");
        writer.Write("Content-Type: ");
        writer.Write(binaryPart.ContentType);
        writer.Write("\r\n\r\n");

        // Flush text writer before writing raw bytes via async callback.
        writer.Flush();
        await binaryPart.WriteContentAsync(output, cancellationToken).ConfigureAwait(false);

        writer.Write("\r\n");
    }
}