// <copyright file="MultipartMixedReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Deserializes <c>multipart/mixed</c> bodies into JSON arrays,
/// scanning MIME boundaries directly from raw UTF-8 bytes.
/// </summary>
/// <remarks>
/// <para>
/// This is the server-side inverse of <see cref="MultipartMixedSerializer"/>.
/// Unlike <see cref="MultipartFormReader"/> which produces a JSON <em>object</em>
/// from named form-data parts, this reader produces a JSON <em>array</em>
/// from positional unnamed parts.
/// </para>
/// <para>
/// Each part is identified by its position in the multipart stream.
/// Parts with <c>application/json</c> Content-Type are written as raw JSON values;
/// binary parts are reported via a callback and are not included in the array.
/// </para>
/// </remarks>
public ref struct MultipartMixedReader
{
    private static ReadOnlySpan<byte> CrLf => "\r\n"u8;

    private static ReadOnlySpan<byte> DoubleCrLf => "\r\n\r\n"u8;

    private static ReadOnlySpan<byte> ContentTypePrefix => "content-type:"u8;

    private static ReadOnlySpan<byte> ContentDispositionPrefix => "content-disposition:"u8;

    private static ReadOnlySpan<byte> FilenameEquals => "filename=\""u8;

    private static ReadOnlySpan<byte> ApplicationJson => "application/json"u8;

    private ReadOnlySpan<byte> remaining;
    private ReadOnlySpan<byte> boundary;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultipartMixedReader"/> struct.
    /// </summary>
    /// <param name="body">The raw UTF-8 multipart body bytes.</param>
    /// <param name="boundary">The boundary string (UTF-8 encoded, without leading <c>--</c>).</param>
    public MultipartMixedReader(ReadOnlySpan<byte> body, ReadOnlySpan<byte> boundary)
    {
        this.remaining = body;
        this.boundary = boundary;

        // Skip the preamble (anything before the first boundary).
        this.SkipToFirstBoundary();
    }

    /// <summary>
    /// Represents a binary part encountered during multipart/mixed deserialization.
    /// </summary>
    /// <param name="Index">The zero-based position of the part in the multipart message.</param>
    /// <param name="FileName">The filename, if present in Content-Disposition.</param>
    /// <param name="ContentType">The Content-Type of the part.</param>
    /// <param name="Data">The raw bytes of the part body.</param>
    public readonly ref struct BinaryPart(
        int Index,
        ReadOnlySpan<byte> FileName,
        ReadOnlySpan<byte> ContentType,
        ReadOnlySpan<byte> Data)
    {
        /// <summary>Gets the zero-based position of the part.</summary>
        public int Index { get; } = Index;

        /// <summary>Gets the filename, if present.</summary>
        public ReadOnlySpan<byte> FileName { get; } = FileName;

        /// <summary>Gets the Content-Type of the part.</summary>
        public ReadOnlySpan<byte> ContentType { get; } = ContentType;

        /// <summary>Gets the raw bytes of the part body.</summary>
        public ReadOnlySpan<byte> Data { get; } = Data;
    }

    /// <summary>
    /// Delegate for handling binary parts encountered during multipart/mixed deserialization.
    /// </summary>
    /// <param name="part">The binary part data.</param>
    public delegate void BinaryPartHandler(BinaryPart part);

    /// <summary>
    /// Advances to the next part, returning the part's content type and body.
    /// </summary>
    /// <param name="contentType">The Content-Type of the part (empty if not specified).</param>
    /// <param name="fileName">The filename from Content-Disposition, if present.</param>
    /// <param name="body">The raw body bytes of the part.</param>
    /// <returns><see langword="true"/> if a part was found; <see langword="false"/> if no more parts remain.</returns>
    public bool TryReadNextPart(
        out ReadOnlySpan<byte> contentType,
        out ReadOnlySpan<byte> fileName,
        out ReadOnlySpan<byte> body)
    {
        contentType = default;
        fileName = default;
        body = default;

        if (this.remaining.IsEmpty)
        {
            return false;
        }

        // Find the end of headers (double CRLF).
        int headerEnd = this.remaining.IndexOf(DoubleCrLf);
        if (headerEnd < 0)
        {
            return false;
        }

        ReadOnlySpan<byte> headers = this.remaining[..headerEnd];

        // Move past the headers + double CRLF.
        this.remaining = this.remaining[(headerEnd + 4)..];

        // Parse headers to extract content-type and filename.
        ParseHeaders(headers, out contentType, out fileName);

        // Find the part body (everything until next boundary).
        body = this.FindPartBody();

        return true;
    }

    /// <summary>
    /// Deserializes a <c>multipart/mixed</c> body into a JSON array
    /// written to the specified <see cref="Utf8JsonWriter"/>, reporting
    /// binary parts via a callback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parts with <c>application/json</c> (or <c>+json</c> suffix) Content-Type,
    /// or parts with no Content-Type, are written as raw JSON array elements.
    /// </para>
    /// <para>
    /// Binary parts (file uploads, octet-stream, images, etc.) are not included
    /// in the JSON output. They are reported via the <paramref name="binaryPartCallback"/>.
    /// </para>
    /// </remarks>
    /// <param name="multipartBody">The raw UTF-8 multipart body bytes.</param>
    /// <param name="boundary">The boundary string (UTF-8, without leading <c>--</c>).</param>
    /// <param name="writer">The JSON writer to write the resulting JSON array to.</param>
    /// <param name="binaryPartCallback">
    /// Optional callback invoked for each binary part. If <see langword="null"/>,
    /// binary parts are silently skipped.
    /// </param>
    public static void DeserializeToJson(
        ReadOnlySpan<byte> multipartBody,
        ReadOnlySpan<byte> boundary,
        Utf8JsonWriter writer,
        BinaryPartHandler? binaryPartCallback = null)
    {
        MultipartMixedReader reader = new(multipartBody, boundary);
        writer.WriteStartArray();

        int index = 0;
        while (reader.TryReadNextPart(out var contentType, out var fileName, out var body))
        {
            bool isBinary = IsBinaryContentType(contentType, fileName);
            if (isBinary)
            {
                binaryPartCallback?.Invoke(new BinaryPart(index, fileName, contentType, body));
                index++;
                continue;
            }

            if (IsJsonContentType(contentType) || contentType.IsEmpty)
            {
                // JSON part — write as raw value.
                if (body.IsEmpty)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteRawValue(body);
                }
            }
            else
            {
                // Text part — write as a JSON string.
                writer.WriteStringValue(body);
            }

            index++;
        }

        writer.WriteEndArray();
    }

    private static void ParseHeaders(
        ReadOnlySpan<byte> headers,
        out ReadOnlySpan<byte> contentType,
        out ReadOnlySpan<byte> fileName)
    {
        contentType = default;
        fileName = default;

        ReadOnlySpan<byte> remaining = headers;
        while (!remaining.IsEmpty)
        {
            ReadOnlySpan<byte> line;
            int crlfIdx = remaining.IndexOf(CrLf);
            if (crlfIdx < 0)
            {
                line = remaining;
                remaining = default;
            }
            else
            {
                line = remaining[..crlfIdx];
                remaining = remaining[(crlfIdx + 2)..];
            }

            if (line.IsEmpty)
            {
                continue;
            }

            if (StartsWithIgnoreCase(line, ContentTypePrefix))
            {
                contentType = line[ContentTypePrefix.Length..].TrimStart((byte)' ');

                // Strip media type parameters (e.g. "; charset=utf-8").
                int semiIdx = contentType.IndexOf((byte)';');
                if (semiIdx >= 0)
                {
                    contentType = contentType[..semiIdx];
                }

                // Trim any trailing whitespace.
                while (contentType.Length > 0 &&
                       contentType[^1] is (byte)' ' or (byte)'\t')
                {
                    contentType = contentType[..^1];
                }
            }
            else if (StartsWithIgnoreCase(line, ContentDispositionPrefix))
            {
                ReadOnlySpan<byte> value = line[ContentDispositionPrefix.Length..].TrimStart((byte)' ');
                fileName = ExtractQuotedParam(value, FilenameEquals);
            }
        }
    }

    private static ReadOnlySpan<byte> ExtractQuotedParam(
        ReadOnlySpan<byte> headerValue,
        ReadOnlySpan<byte> paramPrefix)
    {
        int idx = IndexOfIgnoreCase(headerValue, paramPrefix);
        if (idx < 0)
        {
            return default;
        }

        int start = idx + paramPrefix.Length;
        if (start >= headerValue.Length)
        {
            return default;
        }

        int closeQuote = headerValue[start..].IndexOf((byte)'"');
        if (closeQuote < 0)
        {
            return headerValue[start..];
        }

        return headerValue[start..(start + closeQuote)];
    }

    private static bool IsJsonContentType(ReadOnlySpan<byte> contentType)
    {
        if (contentType.IsEmpty)
        {
            return false;
        }

        if (contentType.SequenceEqual(ApplicationJson))
        {
            return true;
        }

        return contentType.EndsWith("+json"u8);
    }

    private static bool IsBinaryContentType(ReadOnlySpan<byte> contentType, ReadOnlySpan<byte> fileName)
    {
        if (!fileName.IsEmpty)
        {
            return true;
        }

        if (contentType.IsEmpty)
        {
            return false;
        }

        if (IsJsonContentType(contentType))
        {
            return false;
        }

        if (StartsWithIgnoreCase(contentType, "text/"u8))
        {
            return false;
        }

        return true;
    }

    private static bool StartsWithIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> prefix)
    {
        if (span.Length < prefix.Length)
        {
            return false;
        }

        for (int i = 0; i < prefix.Length; i++)
        {
            byte a = span[i];
            byte b = prefix[i];

            if (a >= (byte)'A' && a <= (byte)'Z')
            {
                a = (byte)(a + 32);
            }

            if (a != b)
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOfIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
    {
        if (value.Length > span.Length)
        {
            return -1;
        }

        for (int i = 0; i <= span.Length - value.Length; i++)
        {
            if (StartsWithIgnoreCase(span[i..], value))
            {
                return i;
            }
        }

        return -1;
    }

    private void SkipToFirstBoundary()
    {
        Span<byte> marker = stackalloc byte[2 + this.boundary.Length];
        marker[0] = (byte)'-';
        marker[1] = (byte)'-';
        this.boundary.CopyTo(marker[2..]);

        int idx = this.remaining.IndexOf(marker);
        if (idx < 0)
        {
            this.remaining = default;
            return;
        }

        int afterBoundary = idx + marker.Length;
        if (afterBoundary < this.remaining.Length &&
            this.remaining[afterBoundary..].StartsWith(CrLf))
        {
            this.remaining = this.remaining[(afterBoundary + 2)..];
        }
        else
        {
            this.remaining = this.remaining[afterBoundary..];
        }
    }

    private ReadOnlySpan<byte> FindPartBody()
    {
        Span<byte> marker = stackalloc byte[4 + this.boundary.Length];
        marker[0] = (byte)'\r';
        marker[1] = (byte)'\n';
        marker[2] = (byte)'-';
        marker[3] = (byte)'-';
        this.boundary.CopyTo(marker[4..]);

        int idx = this.remaining.IndexOf(marker);
        if (idx < 0)
        {
            ReadOnlySpan<byte> body = this.remaining;
            this.remaining = default;
            return body;
        }

        ReadOnlySpan<byte> partBody = this.remaining[..idx];

        int afterMarker = idx + marker.Length;
        if (afterMarker + 2 <= this.remaining.Length &&
            this.remaining[afterMarker] == (byte)'-' &&
            this.remaining[afterMarker + 1] == (byte)'-')
        {
            this.remaining = default;
        }
        else if (afterMarker < this.remaining.Length &&
                 this.remaining[afterMarker..].StartsWith(CrLf))
        {
            this.remaining = this.remaining[(afterMarker + 2)..];
        }
        else
        {
            this.remaining = this.remaining[afterMarker..];
        }

        return partBody;
    }
}