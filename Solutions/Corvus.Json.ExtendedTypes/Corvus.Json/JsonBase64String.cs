// <copyright file="JsonBase64String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON base64 string.
/// </summary>
public readonly partial struct JsonBase64String
{
    /// <summary>
    /// Creates a new instance of the <see cref="JsonBase64String"/> struct from a byte arrary.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> from which to construct the Base64 content.</param>
    /// <returns>The base 64 encoded string represnetation of the byte array.</returns>
    /// <remarks>This encodes the byte array as a base 64 string.</remarks>
    public static JsonBase64String FromByteArray(ReadOnlySpan<byte> value)
    {
        return new JsonBase64String(Encoding.UTF8.GetString(value));
    }

    /// <summary>
    /// Get the base64 encoded string.
    /// </summary>
    /// <returns>The base 64 encoded string.</returns>
    public ReadOnlySpan<char> GetBase64EncodedString()
    {
        if ((this.backing & Backing.String) != 0)
        {
            return this.stringBacking;
        }
        else if (this.ValueKind == JsonValueKind.String)
        {
            string? result = this.jsonElementBacking.GetString();
            if (result is null)
            {
                throw new InvalidOperationException();
            }

            return result;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Get the decoded base64 bytes.
    /// </summary>
    /// <returns>The base 64 bytes.</returns>
    public ReadOnlySpan<byte> GetDecodedBase64Bytes()
    {
        if ((this.backing & Backing.String) != 0)
        {
            Span<byte> decoded = new byte[this.stringBacking.Length];
            if (!Convert.TryFromBase64String(this.stringBacking, decoded, out int bytesWritten))
            {
                throw new InvalidOperationException();
            }

            return decoded[..bytesWritten];
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
    /// Get a value indicating whether this instance has a Base64-encoded byte array.
    /// </summary>
    /// <returns>The base 64 bytes.</returns>
    public bool HasBase64Bytes()
    {
        if ((this.backing & Backing.String) != 0)
        {
            Span<byte> decoded = stackalloc byte[this.stringBacking.Length];
            return Convert.TryFromBase64String(this.stringBacking, decoded, out _);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return this.AsJsonElement.TryGetValue(this.HasBase64BytesCore, (object?)null, true, out bool result);
        }

        return false;
    }

    private bool HasBase64BytesCore(ReadOnlySpan<byte> span, in object? state, out bool result)
    {
        result = JsonReaderHelper.CanDecodeBase64(span);
        return result;
    }
}