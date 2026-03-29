// <copyright file="StandardContent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Standard content formatting.
/// </summary>
public static class StandardContent
{
    /// <summary>
    /// This unescapes JSON data which has been escaped into a JSON string and parses it into a JSON document.
    /// </summary>
    /// <param name="jsonElement">The JSON string containing the escaped json text.</param>
    /// <param name="base64Decode">Indicates whether to base64 decode the string before processing.</param>
    /// <param name="result">The parsed document.</param>
    /// <returns><see cref="EncodedContentMediaTypeParseStatus.Success"/> if the document was parsed successfully.</returns>
    public static EncodedContentMediaTypeParseStatus ParseEscapedJsonContentInJsonString(in JsonElement jsonElement, bool base64Decode, out JsonDocument? result)
    {
        Debug.Assert(jsonElement.ValueKind == JsonValueKind.String, "You must provide a string element.");

        (JsonDocument? Document, EncodedContentMediaTypeParseStatus Status) parseResult;

        if (base64Decode)
        {
            jsonElement.TryGetValue(DecodeUnescapeAndParseJsonDocument, default(object?), out parseResult);
        }
        else
        {
            jsonElement.TryGetValue(UnescapeAndParseJsonDocument, default(object?), out parseResult);
        }

        result = parseResult.Document;
        return parseResult.Status;

        static bool DecodeUnescapeAndParseJsonDocument(ReadOnlySpan<byte> base64Source, in object? state, out (JsonDocument? Document, EncodedContentMediaTypeParseStatus Status) result)
        {
            int length = base64Source.Length;
            byte[]? buffer = null;
            Span<byte> utf8Source = length <= JsonConstants.StackallocThreshold ?
                stackalloc byte[length] :
                (buffer = ArrayPool<byte>.Shared.Rent(length));

            try
            {
                if (Base64.DecodeFromUtf8(base64Source, utf8Source, out _, out int written) != OperationStatus.Done)
                {
                    result = (null, EncodedContentMediaTypeParseStatus.UnableToDecode);
                    return false;
                }

                return UnescapeAndParseJsonDocument(utf8Source[..written], default, out result);
            }
            finally
            {
                if (buffer is byte[] b)
                {
                    ArrayPool<byte>.Shared.Return(b);
                }
            }
        }

        static bool UnescapeAndParseJsonDocument(ReadOnlySpan<byte> utf8Source, in object? state, out (JsonDocument? Document, EncodedContentMediaTypeParseStatus Status) result)
        {
            int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

            int length = utf8Source.Length;

            scoped ReadOnlySpan<byte> unescapedResult;

            if (idx >= 0)
            {
                byte[]? buffer = null;

                Span<byte> utf8Unescaped = length <= JsonConstants.StackallocThreshold ?
                    stackalloc byte[length] :
                    (buffer = ArrayPool<byte>.Shared.Rent(length));

                try
                {
                    JsonReaderHelper.Unescape(utf8Source, utf8Unescaped, idx, out int written);
                    unescapedResult = utf8Unescaped[..written];
                }
                finally
                {
                    if (buffer is byte[] b)
                    {
                        ArrayPool<byte>.Shared.Return(b);
                    }
                }
            }
            else
            {
                unescapedResult = utf8Source;
            }

            if (unescapedResult.Length > 0)
            {
                var reader2 = new Utf8JsonReader(unescapedResult);
                try
                {
                    if (JsonDocument.TryParseValue(ref reader2, out JsonDocument? resultDocument))
                    {
                        result = (resultDocument, EncodedContentMediaTypeParseStatus.Success);
                        return true;
                    }
                }
                catch (JsonException)
                {
                }

                result = (null, EncodedContentMediaTypeParseStatus.UnableToParseToMediaType);
                return false;
            }

            result = (null, EncodedContentMediaTypeParseStatus.UnableToDecode);
            return false;
        }
    }

    /// <summary>
    /// This unescapes JSON data which has been escaped into a JSON string and parses it into a JSON document.
    /// </summary>
    /// <param name="source">The JSON string containing the escaped json text.</param>
    /// <param name="base64Decode">Indicates whether to base64 decode the string before processing.</param>
    /// <param name="result">The parsed document.</param>
    /// <returns><see cref="EncodedContentMediaTypeParseStatus.Success"/> if the document was parsed successfully.</returns>
    public static EncodedContentMediaTypeParseStatus ParseEscapedJsonContentInJsonString(ReadOnlySpan<char> source, bool base64Decode, out JsonDocument? result)
    {
        int idx = source.IndexOf('\\');

        int length = source.Length;
        byte[]? buffer = null;

        Span<byte> utf8Unescaped = length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[length] :
            (buffer = ArrayPool<byte>.Shared.Rent(length));

        try
        {
            scoped ReadOnlySpan<byte> unescapedResult;

            if (idx >= 0)
            {
                JsonReaderHelper.Unescape(source, utf8Unescaped, idx, out int written);
                unescapedResult = utf8Unescaped[..written];
            }
            else
            {
                int written = JsonReaderHelper.TranscodeHelper(source, utf8Unescaped);
                unescapedResult = utf8Unescaped[..written];
            }

            if (unescapedResult.Length > 0)
            {
                var reader2 = new Utf8JsonReader(unescapedResult);

                try
                {
                    if (JsonDocument.TryParseValue(ref reader2, out JsonDocument? resultDocument))
                    {
                        result = resultDocument;
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }
                catch (JsonException)
                {
                }

                result = null;
                return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
            }

            result = null;
            return EncodedContentMediaTypeParseStatus.UnableToDecode;
        }
        finally
        {
            if (buffer is byte[] b)
            {
                ArrayPool<byte>.Shared.Return(b);
            }
        }
    }

    /// <summary>
    /// Unescape escaped JSON data stored in a JSON string.
    /// </summary>
    /// <param name="source">The source text to unescape.</param>
    /// <param name="unescaped">The unescaped text.</param>
    /// <param name="written">The number of characters written into the target buffer.</param>
    /// <returns><see langword="true"/>if the string was successfully unescaped.</returns>
    public static bool Unescape(in JsonElement source, Memory<char> unescaped, out int written)
    {
        Debug.Assert(source.ValueKind == JsonValueKind.String, "You must provide a string element.");

        return source.TryGetValue(UnescapeJsonValue, unescaped, out written);

        static bool UnescapeJsonValue(ReadOnlySpan<byte> source, in Memory<char> buffer, out int written)
        {
            return Unescape(source, buffer.Span, out written);
        }
    }

    /// <summary>
    /// Unescape escaped JSON data stored in a JSON string.
    /// </summary>
    /// <param name="source">The source text to unescape.</param>
    /// <param name="unescaped">The unescaped text.</param>
    /// <param name="written">The number of characters written into the target buffer.</param>
    /// <returns><see langword="true"/>if the string was successfully unescaped.</returns>
    public static bool Unescape(ReadOnlySpan<char> source, Span<char> unescaped, out int written)
    {
        int idx = source.IndexOf('\\');
        int length = source.Length;

        if (idx >= 0)
        {
            byte[]? buffer = null;

            Span<byte> utf8Unescaped = length <= JsonConstants.StackallocThreshold ?
                stackalloc byte[length] :
                (buffer = ArrayPool<byte>.Shared.Rent(length));

            try
            {
                JsonReaderHelper.Unescape(source, utf8Unescaped, idx, out written);
                ReadOnlySpan<byte> unescapedResult = utf8Unescaped[..written];
                written = JsonReaderHelper.TranscodeHelper(unescapedResult, unescaped);
                return true;
            }
            finally
            {
                if (buffer is byte[] b)
                {
                    ArrayPool<byte>.Shared.Return(b);
                }
            }
        }
        else
        {
            source.CopyTo(unescaped);
            written = length;
            return true;
        }
    }

    /// <summary>
    /// Unescape the JSON string.
    /// </summary>
    /// <param name="source">The source text to unescape.</param>
    /// <param name="unescaped">The unescaped text.</param>
    /// <param name="written">The number of characters written into the target buffer.</param>
    /// <returns><see langword="true"/>if the string was successfully unescaped.</returns>
    public static bool Unescape(ReadOnlySpan<byte> source, Span<char> unescaped, out int written)
    {
        int idx = source.IndexOf(JsonConstants.BackSlash);
        int length = source.Length;

        if (idx >= 0)
        {
            byte[]? buffer = null;

            Span<byte> utf8Unescaped = length <= JsonConstants.StackallocThreshold ?
                stackalloc byte[length] :
                (buffer = ArrayPool<byte>.Shared.Rent(length));

            try
            {
                JsonReaderHelper.Unescape(source, utf8Unescaped, idx, out written);
                ReadOnlySpan<byte> unescapedResult = utf8Unescaped[..written];
                written = JsonReaderHelper.TranscodeHelper(unescapedResult, unescaped);
                return true;
            }
            finally
            {
                if (buffer is byte[] b)
                {
                    ArrayPool<byte>.Shared.Return(b);
                }
            }
        }
        else
        {
            written = JsonReaderHelper.TranscodeHelper(source, unescaped);
            return true;
        }
    }

    /// <summary>
    /// Gets the length of buffer suitable for the unescaped string.
    /// </summary>
    /// <param name="stringBacking">The string for which to get the buffer size.</param>
    /// <returns>A value which is at least long enough to contain the decoded bytes.</returns>
    public static int GetUnescapedBufferSize(string stringBacking)
    {
        return stringBacking.Length;
    }

    /// <summary>
    /// Gets the length of buffer suitable for the unescaped string.
    /// </summary>
    /// <param name="jsonElementBacking">The JsonElement containin the string for which to get the buffer size.</param>
    /// <returns>A value which is at least long enough to contain the decoded bytes.</returns>
    public static int GetUnescapedBufferSize(JsonElement jsonElementBacking)
    {
        Debug.Assert(jsonElementBacking.ValueKind == JsonValueKind.String, "You must provide a string element.");

        if (jsonElementBacking.TryGetValue(GetStringLength, default(object?), out int length))
        {
            return length;
        }

        return -1;

        static bool GetStringLength(ReadOnlySpan<byte> span, in object? state, out int value)
        {
            value = span.Length;
            return true;
        }
    }
}