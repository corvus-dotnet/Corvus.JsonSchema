// <copyright file="MultipartMixedSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
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
/// <para>
/// All output is written directly as UTF-8 bytes to avoid <see cref="System.IO.StreamWriter"/>
/// and intermediate string allocations on the serialization path.
/// </para>
/// </remarks>
public static class MultipartMixedSerializer
{
    private static ReadOnlySpan<byte> BoundaryPrefix => "----CorvusBoundary"u8;

    private static ReadOnlySpan<byte> DashDash => "--"u8;

    private static ReadOnlySpan<byte> Crlf => "\r\n"u8;

    private static ReadOnlySpan<byte> DoubleCrlf => "\r\n\r\n"u8;

    private static ReadOnlySpan<byte> ContentTypeHeader => "\r\nContent-Type: "u8;

    private static ReadOnlySpan<byte> ContentDispositionHeader => "\r\nContent-Disposition: attachment; filename=\""u8;

    private static ReadOnlySpan<byte> Quote => "\""u8;

    /// <summary>
    /// Writes a JSON part to the multipart message using <see cref="Utf8JsonWriter"/>
    /// for zero-copy JSON serialization.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="output">The stream to write to.</param>
    /// <param name="guid">A <see cref="Guid"/> that uniquely identifies this multipart message.
    /// The MIME boundary is reconstructed from a fixed prefix and this value at each write.</param>
    /// <param name="value">The JSON value to serialize.</param>
    /// <param name="contentType">
    /// The Content-Type for this part. Defaults to <c>"application/json"</c>.
    /// </param>
    public static void WriteJsonPart<T>(
        Stream output,
        Guid guid,
        in T value,
        ReadOnlySpan<byte> contentType = default)
        where T : struct, IJsonElement<T>
    {
        if (contentType.IsEmpty)
        {
            contentType = "application/json"u8;
        }

        WriteBoundaryLine(output, guid, contentType);
        output.Write(DoubleCrlf);

        using Utf8JsonWriter jsonWriter = new(output, new JsonWriterOptions { SkipValidation = true });
        value.WriteTo(jsonWriter);
        jsonWriter.Flush();

        output.Write(Crlf);
    }

    /// <summary>
    /// Writes a binary part to the multipart message.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="guid">A <see cref="Guid"/> that uniquely identifies this multipart message.</param>
    /// <param name="binaryPart">The binary part data to write.</param>
    public static void WriteBinaryPart(
        Stream output,
        Guid guid,
        BinaryPartData binaryPart)
    {
        WriteBoundaryLine(output, guid, binaryPart.ContentType);

        if (binaryPart.FileName is not null)
        {
            output.Write(ContentDispositionHeader);
            WriteAsciiString(output, binaryPart.FileName);
            output.Write(Quote);
        }

        output.Write(DoubleCrlf);
        binaryPart.WriteContent(output);
        output.Write(Crlf);
    }

    /// <summary>
    /// Writes a text/plain part to the multipart message.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="guid">A <see cref="Guid"/> that uniquely identifies this multipart message.</param>
    /// <param name="text">The text content as a UTF-8 byte span.</param>
    /// <param name="contentType">
    /// The Content-Type for this part. Defaults to <c>"text/plain"</c>.
    /// </param>
    public static void WriteTextPart(
        Stream output,
        Guid guid,
        ReadOnlySpan<byte> text,
        ReadOnlySpan<byte> contentType = default)
    {
        if (contentType.IsEmpty)
        {
            contentType = "text/plain"u8;
        }

        WriteBoundaryLine(output, guid, contentType);
        output.Write(DoubleCrlf);
        output.Write(text);
        output.Write(Crlf);
    }

    /// <summary>
    /// Writes the closing boundary marker for the multipart message.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="guid">A <see cref="Guid"/> that uniquely identifies this multipart message.</param>
    public static void WriteClosingBoundary(Stream output, Guid guid)
    {
        output.Write(DashDash);
        WriteBoundary(output, guid);
        output.Write(DashDash);
        output.Write(Crlf);
    }

    /// <summary>
    /// Builds the <c>Content-Type</c> header value for the multipart message
    /// including the boundary parameter.
    /// </summary>
    /// <param name="guid">A <see cref="Guid"/> that uniquely identifies this multipart message.</param>
    /// <returns>A string suitable for the HTTP <c>Content-Type</c> header.</returns>
    public static string GetContentType(Guid guid)
    {
        return $"multipart/mixed; boundary=----CorvusBoundary{guid:N}";
    }

    /// <summary>
    /// Writes the full boundary token (<c>----CorvusBoundary{guid:N}</c>) to the stream
    /// by formatting the <see cref="Guid"/> directly into a stack-allocated buffer.
    /// </summary>
    private static void WriteBoundary(Stream output, Guid guid)
    {
        // "----CorvusBoundary" (18) + 32 hex digits = 50 bytes
        Span<byte> buffer = stackalloc byte[50];
        BoundaryPrefix.CopyTo(buffer);
        guid.TryFormat(buffer.Slice(18), out _, "N");
        output.Write(buffer);
    }

    /// <summary>
    /// Writes <c>--{boundary}\r\nContent-Type: {contentType}</c> to the stream.
    /// </summary>
    private static void WriteBoundaryLine(Stream output, Guid guid, ReadOnlySpan<byte> contentType)
    {
        output.Write(DashDash);
        WriteBoundary(output, guid);
        output.Write(ContentTypeHeader);
        output.Write(contentType);
    }

    /// <summary>
    /// Writes <c>--{boundary}\r\nContent-Type: {contentType}</c> to the stream,
    /// accepting the content type as a string for interop with <see cref="BinaryPartData"/>.
    /// </summary>
    private static void WriteBoundaryLine(Stream output, Guid guid, string contentType)
    {
        output.Write(DashDash);
        WriteBoundary(output, guid);
        output.Write(ContentTypeHeader);
        WriteAsciiString(output, contentType);
    }

    /// <summary>
    /// Writes a short ASCII string directly to the stream using a stack-allocated buffer.
    /// </summary>
    private static void WriteAsciiString(Stream output, string value)
    {
        Span<byte> buffer = stackalloc byte[256];
        int written = Encoding.ASCII.GetBytes(value.AsSpan(), buffer);
        output.Write(buffer.Slice(0, written));
    }
}