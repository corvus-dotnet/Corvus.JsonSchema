// <copyright file="JsonBase64String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
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
#if NET8_0_OR_GREATER
        return new JsonBase64String(Encoding.UTF8.GetString(value));
#else
        byte[] bytes = ArrayPool<byte>.Shared.Rent(value.Length);
        try
        {
            value.CopyTo(bytes);
            return new JsonBase64String(Encoding.UTF8.GetString(bytes));
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
    [Obsolete("Use the standard GetString() method.")]
    public ReadOnlySpan<char> GetBase64EncodedString()
    {
        return this.GetString().AsSpan();
    }

    /// <summary>
    /// Gets the minimum size for a buffer to
    /// pass to <see cref="TryGetDecodedBase64Bytes(Span{byte}, out int)"/>.
    /// </summary>
    /// <returns>A buffer that will be of a suitable length decoding the base64 content.</returns>
    /// <remarks>This is not a zero-cost operation. If you know the expected maximum buffer size in advance,
    /// you can improve performance by pre-allocating a reasonable buffer size and calling <see cref="TryGetDecodedBase64Bytes(Span{byte}, out int)"/>.
    /// If the buffer was too small, the <c>written</c> value will be the desired buffer size.</remarks>
    public int GetDecodedBufferSize()
    {
        if ((this.backing & Backing.String) != 0)
        {
            return this.stringBacking.Length;
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            if (this.jsonElementBacking.TryGetValue(GetStringLength, default(object?), out int length))
            {
                return length;
            }
        }

        throw new InvalidOperationException();

        static bool GetStringLength(ReadOnlySpan<byte> span, in object? state, out int value)
        {
            value = span.Length;
            return true;
        }
    }

    /// <summary>
    /// Try to get the decoded base64 bytes.
    /// </summary>
    /// <param name="result">The span into which to write the bytes.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the bytes were successfully decoded.</returns>
    /// <remarks>
    /// If the <paramref name="result"/> buffer was too short for the decoded bytes, the method will return <see langword="false"/>,
    /// and <paramref name="written"/> will be a number representing a buffer of sufficient size to decode the value. Otherwise it will be <c>0</c>.
    /// </remarks>
    public bool TryGetDecodedBase64Bytes(Span<byte> result, out int written)
    {
        if ((this.backing & Backing.String) != 0)
        {
#if NET8_0_OR_GREATER
            if (Convert.TryFromBase64String(this.stringBacking, result, out written))
            {
                return true;
            }
            else
            {
                if (result.Length >= this.stringBacking.Length)
                {
                    written = 0;
                }
                else
                {
                    written = this.stringBacking.Length;
                }

                return false;
            }
#else
            return Decode(this.stringBacking, result, out written);
#endif
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
#if NET8_0_OR_GREATER
            if (this.jsonElementBacking.TryGetValue(RentBufferAndDecode, this.jsonElementBacking, out (byte[] RentedBuffer, int Written) rentedResult))
            {
                if (rentedResult.Written > result.Length)
                {
                    written = rentedResult.Written;
                    ArrayPool<byte>.Shared.Return(rentedResult.RentedBuffer);
                    return false;
                }

                written = rentedResult.Written;
                rentedResult.RentedBuffer.AsSpan(0, rentedResult.Written).CopyTo(result);
                ArrayPool<byte>.Shared.Return(rentedResult.RentedBuffer);
                return true;
            }
#else
            string? value = this.jsonElementBacking.GetString();
            if (value is null)
            {
                written = 0;
                return false;
            }

            return Decode(value, result, out written);
#endif
        }

        written = 0;
        return false;

#if NET8_0_OR_GREATER
        static bool RentBufferAndDecode(ReadOnlySpan<char> span, in JsonElement state, out (byte[] RentedBuffer, int Written) result)
        {
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(span.Length);
            if (Convert.TryFromBase64Chars(span, rentedBuffer.AsSpan(), out int written))
            {
                result = (rentedBuffer, written);
                return true;
            }

            ArrayPool<byte>.Shared.Return(rentedBuffer);
            result = ([], span.Length);
            return false;
        }
#else
        static bool Decode(string value, Span<byte> result, out int written)
        {
            try
            {
                byte[] writtenBytes = Convert.FromBase64String(value);
                written = writtenBytes.Length;
                if (written > result.Length)
                {
                    return false;
                }

                writtenBytes.AsSpan().CopyTo(result);
                return true;
            }
            catch (FormatException)
            {
                written = 0;
                return false;
            }
        }
#endif
    }

    /// <summary>
    /// Get the decoded base64 bytes.
    /// </summary>
    /// <returns>The base 64 bytes.</returns>
    [Obsolete("Use the TryDecodeBase64Bytes() method.")]
    public ReadOnlySpan<byte> GetDecodedBase64Bytes()
    {
        if ((this.backing & Backing.String) != 0)
        {
            Span<byte> decoded = new byte[this.stringBacking.Length];
            if (this.TryGetDecodedBase64Bytes(decoded, out int written))
            {
                return decoded[..written];
            }
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
            return Base64.IsValid(this.stringBacking, out _);
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
            return this.AsJsonElement.TryGetValue(this.HasBase64BytesCore, (object?)null, true, out bool result);
        }

        return false;
    }

    private bool HasBase64BytesCore(ReadOnlySpan<byte> span, in object? state, out bool result)
    {
#if NET8_0_OR_GREATER
        result = Base64.IsValid(span, out _);
#else
        result = JsonReaderHelper.CanDecodeBase64(span);
#endif
        return result;
    }
}