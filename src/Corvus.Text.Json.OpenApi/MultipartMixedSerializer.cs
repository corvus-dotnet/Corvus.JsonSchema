// <copyright file="MultipartMixedSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Serializes positional or homogeneous parts to <c>multipart/mixed</c> format.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="MultipartFormDataSerializer"/> which uses named form-data parts
/// (with <c>Content-Disposition: form-data; name="..."</c>), this serializer writes
/// ordered unnamed parts separated by a MIME boundary, suitable for <c>multipart/mixed</c>.
/// </para>
/// <para>
/// This supports the OAS 3.2 <c>prefixEncoding</c> (positional encoding) and
/// <c>itemEncoding</c> (uniform encoding) fields on the Media Type Object.
/// </para>
/// </remarks>
public static class MultipartMixedSerializer
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Writes a JSON part at the specified position in the multipart message.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="output">The stream to write to.</param>
    /// <param name="boundary">The multipart boundary string.</param>
    /// <param name="value">The JSON value to serialize.</param>
    /// <param name="contentType">
    /// The Content-Type for this part. Defaults to <c>"application/json"</c>.
    /// </param>
    public static void WriteJsonPart<T>(
        Stream output,
        string boundary,
        in T value,
        string contentType = "application/json")
        where T : struct, IJsonElement<T>
    {
        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 256, leaveOpen: true);
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("\r\nContent-Type: ");
        writer.Write(contentType);
        writer.Write("\r\n\r\n");
        writer.Write(value.ToString());
        writer.Write("\r\n");
    }

    /// <summary>
    /// Writes a binary part at the specified position in the multipart message.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="boundary">The multipart boundary string.</param>
    /// <param name="binaryPart">The binary part data to write.</param>
    public static void WriteBinaryPart(
        Stream output,
        string boundary,
        BinaryPartData binaryPart)
    {
        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 256, leaveOpen: true);
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("\r\nContent-Type: ");
        writer.Write(binaryPart.ContentType);

        if (binaryPart.FileName is not null)
        {
            writer.Write("\r\nContent-Disposition: attachment; filename=\"");
            writer.Write(binaryPart.FileName);
            writer.Write("\"");
        }

        writer.Write("\r\n\r\n");
        writer.Flush();
        binaryPart.WriteContent(output);

        // We need a new writer after flushing raw binary to write the trailing CRLF.
        using StreamWriter trailer = new(output, Utf8NoBom, bufferSize: 4, leaveOpen: true);
        trailer.Write("\r\n");
    }

    /// <summary>
    /// Writes a text/plain part at the specified position in the multipart message.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="boundary">The multipart boundary string.</param>
    /// <param name="text">The text content.</param>
    /// <param name="contentType">
    /// The Content-Type for this part. Defaults to <c>"text/plain"</c>.
    /// </param>
    public static void WriteTextPart(
        Stream output,
        string boundary,
        string text,
        string contentType = "text/plain")
    {
        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 256, leaveOpen: true);
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("\r\nContent-Type: ");
        writer.Write(contentType);
        writer.Write("\r\n\r\n");
        writer.Write(text);
        writer.Write("\r\n");
    }

    /// <summary>
    /// Writes the closing boundary marker for the multipart message.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="boundary">The multipart boundary string.</param>
    public static void WriteClosingBoundary(Stream output, string boundary)
    {
        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 64, leaveOpen: true);
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("--\r\n");
    }

    /// <summary>
    /// Generates a unique boundary string for a multipart message.
    /// </summary>
    /// <returns>A boundary string safe for use in MIME multipart messages.</returns>
    public static string GenerateBoundary()
    {
        return $"----CorvusBoundary{Guid.NewGuid():N}";
    }
}