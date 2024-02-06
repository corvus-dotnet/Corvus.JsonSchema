// <copyright file="JsonBase64Content.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON base64content.
/// </summary>
public readonly partial struct JsonBase64Content
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonBase64Content"/> struct.
    /// </summary>
    /// <param name="value">The json document containing the base64 content.</param>
    /// <remarks>
    /// This does not take ownership of the document. The caller should dispose of it in the usual way, once its
    /// use in this scope is complete.
    /// </remarks>
    public JsonBase64Content(JsonDocument value)
    {
        // We both serialize it on creation...
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        value.WriteTo(writer);
        this.stringBacking = Convert.ToBase64String(abw.WrittenSpan);
        this.jsonElementBacking = default;
        this.backing = Backing.String;
    }

    /// <summary>
    /// Get the base64 encoded string.
    /// </summary>
    /// <returns>The base 64 encoded string.</returns>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public ReadOnlySpan<char> GetBase64EncodedString()
    {
        if ((this.backing & Backing.String) != 0)
        {
            return this.stringBacking;
        }
        else if (this.ValueKind == JsonValueKind.String)
        {
            return this.jsonElementBacking.GetString() ?? throw new InvalidOperationException();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Get the base64 encoded string.
    /// </summary>
    /// <returns>The base 64 encoded string.</returns>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public ReadOnlySpan<byte> GetUtf8BytesBase64EncodedString()
    {
        if ((this.backing & Backing.String) != 0)
        {
                return Encoding.UTF8.GetBytes(this.stringBacking);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            if (this.jsonElementBacking.GetString() is string decoded)
            {
                return Encoding.UTF8.GetBytes(decoded);
            }
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Get the decoded base64 bytes.
    /// </summary>
    /// <returns>The base 64 bytes.</returns>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public ReadOnlySpan<byte> GetDecodedBase64Bytes()
    {
        if ((this.backing & Backing.String) != 0)
        {
            Span<byte> result = new byte[this.stringBacking.Length];
            if (!Convert.TryFromBase64String(this.stringBacking, result, out int bytesWritten))
            {
                throw new InvalidOperationException();
            }

            return result[..bytesWritten];
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            if (this.jsonElementBacking.TryGetBytesFromBase64(out byte[]? decoded))
            {
                return decoded;
            }
        }

        throw new InvalidOperationException();
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
            byte[]? rentedFromPool = null;
            Span<byte> decoded =
                this.stringBacking.Length > JsonValueHelpers.MaxStackAlloc
                ? (rentedFromPool = ArrayPool<byte>.Shared.Rent(this.stringBacking.Length))
                : stackalloc byte[JsonValueHelpers.MaxStackAlloc];

            try
            {
                if (Convert.TryFromBase64String(this.stringBacking, decoded, out int bytesWritten))
                {
                    var reader = new Utf8JsonReader(decoded[0..bytesWritten]);
                    if (JsonDocument.TryParseValue(ref reader, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
                    }
                }

                result = default;
                return EncodedContentMediaTypeParseStatus.UnableToParseToMediaType;
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
            if (this.jsonElementBacking.TryGetBytesFromBase64(out byte[]? decoded))
            {
                var reader = new Utf8JsonReader(decoded);
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                try
                {
                    if (JsonDocument.TryParseValue(ref reader, out result))
                    {
                        return EncodedContentMediaTypeParseStatus.Success;
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
        }

        result = null;
        return EncodedContentMediaTypeParseStatus.UnableToDecode;
    }
}