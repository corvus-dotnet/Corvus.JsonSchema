// <copyright file="MultipartFormReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Deserializes <c>multipart/form-data</c> bodies into JSON,
/// scanning MIME boundaries directly from raw UTF-8 bytes.
/// </summary>
/// <remarks>
/// <para>
/// This is the server-side inverse of <see cref="MultipartFormDataSerializer"/>.
/// It scans raw UTF-8 bytes for multipart boundaries, extracts part headers
/// (Content-Disposition name, Content-Type), and builds a JSON object from the
/// text and JSON parts.
/// </para>
/// <para>
/// Binary parts (identified by a non-JSON, non-text Content-Type or by matching
/// a known binary property name) are reported separately via the
/// <see cref="BinaryPart"/> callback pattern.
/// </para>
/// </remarks>
public ref struct MultipartFormReader
{
    private static ReadOnlySpan<byte> CrLf => "\r\n"u8;

    private static ReadOnlySpan<byte> DoubleCrLf => "\r\n\r\n"u8;

    private static ReadOnlySpan<byte> ContentDispositionPrefix => "content-disposition:"u8;

    private static ReadOnlySpan<byte> ContentTypePrefix => "content-type:"u8;

    private static ReadOnlySpan<byte> NameEquals => "name=\""u8;

    private static ReadOnlySpan<byte> FilenameEquals => "filename=\""u8;

    private static ReadOnlySpan<byte> ApplicationJson => "application/json"u8;

    private ReadOnlySpan<byte> remaining;
    private ReadOnlySpan<byte> boundary;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultipartFormReader"/> struct.
    /// </summary>
    /// <param name="body">The raw UTF-8 multipart body bytes.</param>
    /// <param name="boundary">The boundary string (UTF-8 encoded, without leading <c>--</c>).</param>
    public MultipartFormReader(ReadOnlySpan<byte> body, ReadOnlySpan<byte> boundary)
    {
        this.remaining = body;
        this.boundary = boundary;

        // Skip the preamble (anything before the first boundary).
        this.SkipToFirstBoundary();
    }

    /// <summary>
    /// Represents a binary part encountered during multipart deserialization.
    /// </summary>
    /// <param name="Name">The form field name (from Content-Disposition).</param>
    /// <param name="FileName">The filename, if present.</param>
    /// <param name="ContentType">The Content-Type of the part.</param>
    /// <param name="Data">The raw bytes of the part body.</param>
    public readonly ref struct BinaryPart(
        ReadOnlySpan<byte> Name,
        ReadOnlySpan<byte> FileName,
        ReadOnlySpan<byte> ContentType,
        ReadOnlySpan<byte> Data)
    {
        /// <summary>Gets the form field name.</summary>
        public ReadOnlySpan<byte> Name { get; } = Name;

        /// <summary>Gets the filename, if present.</summary>
        public ReadOnlySpan<byte> FileName { get; } = FileName;

        /// <summary>Gets the Content-Type of the part.</summary>
        public ReadOnlySpan<byte> ContentType { get; } = ContentType;

        /// <summary>Gets the raw bytes of the part body.</summary>
        public ReadOnlySpan<byte> Data { get; } = Data;
    }

    /// <summary>
    /// Advances to the next part, returning the part's name, content type, and body.
    /// </summary>
    /// <param name="name">The form field name (from Content-Disposition).</param>
    /// <param name="fileName">The filename, if present in Content-Disposition.</param>
    /// <param name="contentType">The Content-Type of the part (empty if not specified).</param>
    /// <param name="body">The raw body bytes of the part.</param>
    /// <returns><see langword="true"/> if a part was found; <see langword="false"/> if no more parts remain.</returns>
    public bool TryReadNextPart(
        out ReadOnlySpan<byte> name,
        out ReadOnlySpan<byte> fileName,
        out ReadOnlySpan<byte> contentType,
        out ReadOnlySpan<byte> body)
    {
        name = default;
        fileName = default;
        contentType = default;
        body = default;

        if (this.remaining.IsEmpty)
        {
            return false;
        }

        // We should be positioned after a boundary line.
        // Find the end of headers (double CRLF).
        int headerEnd = this.remaining.IndexOf(DoubleCrLf);
        if (headerEnd < 0)
        {
            return false;
        }

        ReadOnlySpan<byte> headers = this.remaining[..headerEnd];
        ReadOnlySpan<byte> afterHeaders = this.remaining[(headerEnd + 4)..]; // skip \r\n\r\n

        // Parse headers to extract name, filename, content-type.
        ParseHeaders(headers, out name, out fileName, out contentType);

        if (name.IsEmpty)
        {
            // Invalid part — skip to next boundary.
            this.remaining = afterHeaders;
            return this.AdvanceToNextBoundary(out body);
        }

        // Find the next boundary in the remaining body.
        // The boundary is preceded by \r\n--boundary
        this.remaining = afterHeaders;
        body = this.FindPartBody();

        return true;
    }

    /// <summary>
    /// Deserializes a <c>multipart/form-data</c> body into a JSON object
    /// written to the specified <see cref="Utf8JsonWriter"/>, reporting
    /// binary parts via a callback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Text parts with no Content-Type or <c>text/plain</c> are classified
    /// using the same heuristic as <see cref="FormFieldReader.WriteJsonValue"/>
    /// (booleans, numbers, null, JSON structures, or strings).
    /// </para>
    /// <para>
    /// Parts with <c>application/json</c> (or <c>+json</c> suffix) Content-Type
    /// are written as raw JSON values.
    /// </para>
    /// <para>
    /// Binary parts (file uploads, octet-stream, images, etc.) are not included
    /// in the JSON output. They are reported via the <paramref name="binaryPartCallback"/>.
    /// </para>
    /// </remarks>
    /// <param name="multipartBody">The raw UTF-8 multipart body bytes.</param>
    /// <param name="boundary">The boundary string (UTF-8, without leading <c>--</c>).</param>
    /// <param name="writer">The JSON writer to write the resulting JSON object to.</param>
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
        MultipartFormReader reader = new(multipartBody, boundary);
        writer.WriteStartObject();

        while (reader.TryReadNextPart(out var name, out var fileName, out var contentType, out var body))
        {
            bool isBinary = IsBinaryContentType(contentType, fileName);
            if (isBinary)
            {
                binaryPartCallback?.Invoke(new BinaryPart(name, fileName, contentType, body));
                continue;
            }

            bool isJson = IsJsonContentType(contentType);
            writer.WritePropertyName(name);

            if (isJson)
            {
                // Part body is JSON — write it as raw value.
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
                // Text part — classify heuristically (same as form-urlencoded).
                FormFieldReader.WriteJsonValue(writer, body);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Extracts the boundary parameter from a <c>Content-Type</c> header value.
    /// </summary>
    /// <param name="contentType">The full Content-Type header value
    /// (e.g., <c>multipart/form-data; boundary=abc123</c>).</param>
    /// <param name="boundary">The extracted boundary bytes, or empty if not found.</param>
    /// <returns><see langword="true"/> if the boundary was found.</returns>
    public static bool TryExtractBoundary(
        ReadOnlySpan<byte> contentType,
        out ReadOnlySpan<byte> boundary)
    {
        ReadOnlySpan<byte> boundaryToken = "boundary="u8;
        int idx = contentType.IndexOf(boundaryToken);
        if (idx < 0)
        {
            boundary = default;
            return false;
        }

        ReadOnlySpan<byte> value = contentType[(idx + boundaryToken.Length)..];

        // Strip quotes if present.
        if (value.Length >= 2 && value[0] == (byte)'"')
        {
            int closeQuote = value[1..].IndexOf((byte)'"');
            if (closeQuote >= 0)
            {
                boundary = value[1..(closeQuote + 1)];
            }
            else
            {
                boundary = value[1..];
            }
        }
        else
        {
            // Unquoted — boundary ends at semicolon, space, or end.
            int end = value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] is (byte)';' or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                {
                    end = i;
                    break;
                }
            }

            boundary = value[..end];
        }

        return !boundary.IsEmpty;
    }

    /// <summary>
    /// Delegate for handling binary parts encountered during multipart deserialization.
    /// </summary>
    /// <param name="part">The binary part data.</param>
    public delegate void BinaryPartHandler(BinaryPart part);

    private static void ParseHeaders(
        ReadOnlySpan<byte> headers,
        out ReadOnlySpan<byte> name,
        out ReadOnlySpan<byte> fileName,
        out ReadOnlySpan<byte> contentType)
    {
        name = default;
        fileName = default;
        contentType = default;

        // Split headers by CRLF and inspect each line.
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

            // Case-insensitive header comparison by lowering the line.
            if (StartsWithIgnoreCase(line, ContentDispositionPrefix))
            {
                ReadOnlySpan<byte> value = line[ContentDispositionPrefix.Length..].TrimStart((byte)' ');
                name = ExtractQuotedParam(value, NameEquals);
                fileName = ExtractQuotedParam(value, FilenameEquals);
            }
            else if (StartsWithIgnoreCase(line, ContentTypePrefix))
            {
                contentType = line[ContentTypePrefix.Length..].TrimStart((byte)' ');

                // Trim trailing whitespace/semicolons.
                while (contentType.Length > 0 &&
                       contentType[^1] is (byte)' ' or (byte)'\t' or (byte)';')
                {
                    contentType = contentType[..^1];
                }
            }
        }
    }

    private static ReadOnlySpan<byte> ExtractQuotedParam(
        ReadOnlySpan<byte> headerValue,
        ReadOnlySpan<byte> paramPrefix)
    {
        // Search case-insensitively for paramPrefix (e.g. name=")
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

            // ASCII lowercase comparison.
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

    private static bool IsJsonContentType(ReadOnlySpan<byte> contentType)
    {
        if (contentType.IsEmpty)
        {
            return false;
        }

        // Exact match: application/json
        if (contentType.SequenceEqual(ApplicationJson))
        {
            return true;
        }

        // Suffix match: +json (e.g. application/vnd.api+json)
        return contentType.EndsWith("+json"u8);
    }

    private static bool IsBinaryContentType(ReadOnlySpan<byte> contentType, ReadOnlySpan<byte> fileName)
    {
        // If a filename is present, it's a file upload → binary.
        if (!fileName.IsEmpty)
        {
            return true;
        }

        // No content type or text/plain → treat as text.
        if (contentType.IsEmpty)
        {
            return false;
        }

        // application/json or +json → not binary.
        if (IsJsonContentType(contentType))
        {
            return false;
        }

        // text/* → not binary.
        if (StartsWithIgnoreCase(contentType, "text/"u8))
        {
            return false;
        }

        // Everything else (application/octet-stream, image/*, etc.) → binary.
        return true;
    }

    private void SkipToFirstBoundary()
    {
        // The first boundary is \r\n--boundary\r\n or at the very start --boundary\r\n.
        // Build the boundary marker: --boundary
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

        // Move past the boundary line (marker + CRLF).
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
        // Find \r\n--boundary in remaining. The part body is everything before that.
        Span<byte> marker = stackalloc byte[4 + this.boundary.Length]; // \r\n--boundary
        marker[0] = (byte)'\r';
        marker[1] = (byte)'\n';
        marker[2] = (byte)'-';
        marker[3] = (byte)'-';
        this.boundary.CopyTo(marker[4..]);

        int idx = this.remaining.IndexOf(marker);
        if (idx < 0)
        {
            // Final part with no proper closing — consume everything.
            ReadOnlySpan<byte> body = this.remaining;
            this.remaining = default;
            return body;
        }

        ReadOnlySpan<byte> partBody = this.remaining[..idx];

        // Advance past the boundary. Check if this is the closing boundary (--boundary--).
        int afterMarker = idx + marker.Length;
        if (afterMarker + 2 <= this.remaining.Length &&
            this.remaining[afterMarker] == (byte)'-' &&
            this.remaining[afterMarker + 1] == (byte)'-')
        {
            // Closing boundary — no more parts.
            this.remaining = default;
        }
        else if (afterMarker < this.remaining.Length &&
                 this.remaining[afterMarker..].StartsWith(CrLf))
        {
            // Next part follows after CRLF.
            this.remaining = this.remaining[(afterMarker + 2)..];
        }
        else
        {
            this.remaining = this.remaining[afterMarker..];
        }

        return partBody;
    }

    private bool AdvanceToNextBoundary(out ReadOnlySpan<byte> skippedBody)
    {
        skippedBody = this.FindPartBody();
        return !this.remaining.IsEmpty || !skippedBody.IsEmpty;
    }
}