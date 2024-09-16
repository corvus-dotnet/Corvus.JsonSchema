﻿// <copyright file="JsonBase64StringPre201909.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON base64 string.
/// </summary>
public readonly partial struct JsonBase64StringPre201909
{
    /// <summary>
    /// Creates a new instance of the <see cref="JsonBase64StringPre201909"/> struct from a byte arrary.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> from which to construct the Base64 content.</param>
    /// <returns>The base 64 encoded string represnetation of the byte array.</returns>
    /// <remarks>This encodes the byte array as a base 64 string.</remarks>
    public static JsonBase64StringPre201909 FromByteArray(ReadOnlySpan<byte> value)
    {
#if NET8_0_OR_GREATER
        return new JsonBase64StringPre201909(Encoding.UTF8.GetString(value));
#else
        byte[] bytes = ArrayPool<byte>.Shared.Rent(value.Length);
        try
        {
            value.CopyTo(bytes);
            return new JsonBase64StringPre201909(Encoding.UTF8.GetString(bytes));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
#endif
    }

    /// <summary>
    /// Get the base64 encoded string.
    /// </summary>
    /// <returns>The base 64 encoded string.</returns>
    public ReadOnlySpan<char> GetBase64EncodedString()
    {
        if ((this.backing & Backing.String) != 0)
        {
#if NET8_0_OR_GREATER
            return this.stringBacking;
#else
            return this.stringBacking.AsSpan();
#endif
        }
        else if (this.ValueKind == JsonValueKind.String)
        {
            string? result = this.jsonElementBacking.GetString();
            if (result is null)
            {
                throw new InvalidOperationException();
            }

#if NET8_0_OR_GREATER
            return result;
#else
            return result.AsSpan();
#endif
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
#if NET8_0_OR_GREATER
            Span<byte> decoded = new byte[this.stringBacking.Length];
            if (!Convert.TryFromBase64String(this.stringBacking, decoded, out int bytesWritten))
            {
                throw new InvalidOperationException();
            }

            return decoded[..bytesWritten];
#else
            return Convert.FromBase64String(this.stringBacking).AsSpan();
#endif
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
#if NET8_0_OR_GREATER
            Span<byte> decoded = stackalloc byte[this.stringBacking.Length];
            return Convert.TryFromBase64String(this.stringBacking, decoded, out _);
#else
            try
            {
                Convert.FromBase64String(this.stringBacking);
                return true;
            }
            catch
            {
                return false;
            }
#endif
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return this.jsonElementBacking.TryGetBytesFromBase64(out byte[]? _);
        }

        return false;
    }
}