// <copyright file="JsonContentPre201909.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON content.
/// </summary>
public readonly partial struct JsonContentPre201909
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonContentPre201909"/> struct.
    /// </summary>
    /// <param name="value">The <see cref="JsonDocument"/> from which to construct the Base64 content.</param>
    /// <remarks>
    /// This does not take ownership of the document. The caller should dispose of it in the usual way, once its
    /// use in this scope is complete.
    /// </remarks>
    public JsonContentPre201909(JsonDocument value)
    {
        // We both serialize it on creation...
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        value.WriteTo(writer);
        this.stringBacking = abw.WrittenSpan.ToString();
        this.jsonElementBacking = default;
        this.backing = Backing.String;
    }

    /// <summary>
    /// Try to get the JSON document from the content.
    /// </summary>
    /// <param name="result">A JSON document produced from the content, or null if the content did not represent a Base64 encoded JSON document.</param>
    /// <returns><c>True</c> if the document was parsed successfully.</returns>
    public EncodedContentMediaTypeParseStatus TryGetJsonDocument(out JsonDocument? result)
    {
        if ((this.backing & Backing.String) != 0)
        {
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
            try
            {
                byte[]? rentedFromPool = null;
                int required = Encoding.UTF8.GetMaxByteCount(this.stringBacking.Length);
                Span<byte> utf8SourceBuffer =
                    required > JsonValueHelpers.MaxStackAlloc
                    ? (rentedFromPool = ArrayPool<byte>.Shared.Rent(required))
                    : stackalloc byte[JsonValueHelpers.MaxStackAlloc];

                try
                {
                    int written = Encoding.UTF8.GetBytes(this.stringBacking, utf8SourceBuffer);
                    ReadOnlySpan<byte> utf8Source = utf8SourceBuffer[..written];

                    int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                    ReadOnlySpan<byte> utf8Unescaped =
                        idx >= 0 ? JsonReaderHelper.GetUnescapedSpan(utf8Source, idx)
                        : utf8Source;

                    var reader2 = new Utf8JsonReader(utf8Unescaped);
                    if (JsonDocument.TryParseValue(ref reader2, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }
                finally
                {
                    if (rentedFromPool is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedFromPool, true);
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the return...
            }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.

            result = null;
            return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
            try
            {
                string sourceString = this.jsonElementBacking.GetString()!;
                byte[]? rentedFromPool = null;
                int required = Encoding.UTF8.GetMaxByteCount(sourceString.Length);
                Span<byte> utf8SourceBuffer =
                    required > JsonValueHelpers.MaxStackAlloc
                    ? (rentedFromPool = ArrayPool<byte>.Shared.Rent(required))
                    : stackalloc byte[JsonValueHelpers.MaxStackAlloc];

                try
                {
                    int written = Encoding.UTF8.GetBytes(sourceString, utf8SourceBuffer);
                    ReadOnlySpan<byte> utf8Source = utf8SourceBuffer[..written];

                    int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                    ReadOnlySpan<byte> utf8Unescaped =
                        idx >= 0 ? JsonReaderHelper.GetUnescapedSpan(utf8Source, idx)
                        : utf8Source;

                    var reader2 = new Utf8JsonReader(utf8Unescaped);
                    if (JsonDocument.TryParseValue(ref reader2, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }
                finally
                {
                    if (rentedFromPool is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedFromPool, true);
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the return...
            }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.

            result = default;
            return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
        }

        result = null;
        return EncodedContentMediaTypeParseStatus.UnableToDecode;
    }

    /// <summary>
    /// Gets the value as an unescaped string.
    /// </summary>
    /// <param name="result">The value as a string.</param>
    /// <returns><c>True</c> if the value could be retrieved.</returns>
    public bool TryGetUnescapedString(out string result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            string sourceString = this.stringBacking;
            byte[]? rentedFromPool = null;
            int required = Encoding.UTF8.GetMaxByteCount(sourceString.Length);
            Span<byte> utf8SourceBuffer =
                required > JsonValueHelpers.MaxStackAlloc
                ? (rentedFromPool = ArrayPool<byte>.Shared.Rent(required))
                : stackalloc byte[JsonValueHelpers.MaxStackAlloc];

            try
            {
                int written = Encoding.UTF8.GetBytes(sourceString, utf8SourceBuffer);
                ReadOnlySpan<byte> utf8Source = utf8SourceBuffer[..written];

                int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                ReadOnlySpan<byte> utf8Unescaped =
                    idx >= 0 ? JsonReaderHelper.GetUnescapedSpan(utf8Source, idx)
                    : utf8Source;

                result = Encoding.UTF8.GetString(utf8Unescaped);
                return true;
            }
            finally
            {
                if (rentedFromPool is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedFromPool, true);
                }
            }
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            if (str is not null)
            {
                result = str;
                return true;
            }
        }

        result = string.Empty;
        return false;
    }

    /// <summary>
    /// Gets the value as an unescaped span.
    /// </summary>
    /// <returns>The unescaped value as a span of char.</returns>
    public ReadOnlySpan<char> AsUnescapedSpan()
    {
        if ((this.backing & Backing.String) != 0)
        {
            string sourceString = this.stringBacking;
            byte[]? rentedFromPool = null;
            int required = Encoding.UTF8.GetMaxByteCount(sourceString.Length);
            Span<byte> utf8SourceBuffer =
                required > JsonValueHelpers.MaxStackAlloc
                ? (rentedFromPool = ArrayPool<byte>.Shared.Rent(required))
                : stackalloc byte[JsonValueHelpers.MaxStackAlloc];

            try
            {
                int written = Encoding.UTF8.GetBytes(sourceString, utf8SourceBuffer);
                ReadOnlySpan<byte> utf8Source = utf8SourceBuffer[..written];

                int idx = utf8Source.IndexOf(JsonConstants.BackSlash);

                ReadOnlySpan<byte> utf8Unescaped =
                    idx >= 0 ? JsonReaderHelper.GetUnescapedSpan(utf8Source, idx)
                    : utf8Source;

                Span<char> result = new char[Encoding.UTF8.GetMaxCharCount(utf8Unescaped.Length)];
                written = Encoding.UTF8.GetChars(utf8Unescaped, result);
                return result[..written];
            }
            finally
            {
                if (rentedFromPool is not null)
                {
                    ArrayPool<byte>.Shared.Return(rentedFromPool, true);
                }
            }
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            return str!.AsSpan();
        }

        return ReadOnlySpan<char>.Empty;
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <typeparam name="T">The type of the item with which to compare.</typeparam>
    /// <param name="other">The item with which to compare.</param>
    /// <returns><c>True</c> if the items are equal.</returns>
    public bool Equals<T>(T other)
        where T : struct, IJsonValue<T>
    {
        if (this.IsNull() && other.IsNull())
        {
            return true;
        }

        if (other.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return this.EqualsCore((JsonContentPre201909)other.AsString);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The item with which to compare.</param>
    /// <returns><c>True</c> if the items are equal.</returns>
    public bool Equals(JsonContentPre201909 other)
    {
        if (this.IsNull() && other.IsNull())
        {
            return true;
        }

        return this.EqualsCore(other);
    }

    private bool EqualsCore(JsonContentPre201909 other)
    {
        JsonValueKind valueKind = this.ValueKind;
        if (valueKind != JsonValueKind.String || other.ValueKind != valueKind)
        {
            return false;
        }

        return this.AsUnescapedSpan().Equals(other.AsUnescapedSpan(), StringComparison.Ordinal);
    }
}