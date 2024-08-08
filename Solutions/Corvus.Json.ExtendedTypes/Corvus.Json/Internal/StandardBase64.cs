// <copyright file="StandardBase64.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Standard format a base64 string.
/// </summary>
public static class StandardBase64
{
    /// <summary>
    /// Encode a <see cref="ReadOnlySpan{Byte}"/> to a base64 string.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>The base64 encoded string representing the bytes.</returns>
    public static string EncodeToString(ReadOnlySpan<byte> value)
    {
#if NET8_0_OR_GREATER
        return Encoding.UTF8.GetString(value);
#else
        byte[] bytes = ArrayPool<byte>.Shared.Rent(value.Length);
        try
        {
            value.CopyTo(bytes);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
#endif
    }

    /// <summary>
    /// Gets a value indcating whether the string has valid base64 encoded content.
    /// </summary>
    /// <param name="jsonElementBacking">The value to test.</param>
    /// <returns><see langword="true"/> if the value has valid base64 encoded bytes.</returns>
    public static bool HasBase64Bytes(in JsonElement jsonElementBacking)
    {
        Debug.Assert(jsonElementBacking.ValueKind == JsonValueKind.String, "You must provide a string element.");

        return jsonElementBacking.TryGetValue(HasBase64BytesCore, (object?)null, true, out bool result);

        static bool HasBase64BytesCore(ReadOnlySpan<byte> span, in object? state, out bool result)
        {
#if NET8_0_OR_GREATER
            result = Base64.IsValid(span, out _);
#else
            result = JsonReaderHelper.CanDecodeBase64(span);
#endif
            return result;
        }
    }

    /// <summary>
    /// Gets the length of buffer suitable for the decoded base64 bytes.
    /// </summary>
    /// <param name="stringBacking">The string for which to get the buffer size.</param>
    /// <returns>A value which is at least long enough to contain the decoded bytes.</returns>
    public static int GetDecodedBufferSize(string stringBacking)
    {
        return stringBacking.Length;
    }

    /// <summary>
    /// Gets the length of buffer suitable for the decoded base64 bytes.
    /// </summary>
    /// <param name="jsonElementBacking">The JsonElement containin the string for which to get the buffer size.</param>
    /// <returns>A value which is at least long enough to contain the decoded bytes.</returns>
    public static int GetDecodedBufferSize(JsonElement jsonElementBacking)
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

#if NET8_0_OR_GREATER
    /// <summary>
    /// Gets a value indcating whether the string has valid base64 encoded content.
    /// </summary>
    /// <param name="stringBacking">The value to test.</param>
    /// <returns><see langword="true"/> if the value has valid base64 encoded bytes.</returns>
    public static bool HasBase64Bytes(string? stringBacking)
    {
        if (stringBacking is null)
        {
            return false;
        }

        return Base64.IsValid(stringBacking, out _);
    }

    /// <summary>
    /// Decode a base64 string.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element containing the base64 string.</param>
    /// <param name="result">A buffer for the decoded bytes to be written.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the buffer was decoded.</returns>
    public static bool Decode(in JsonElement jsonElementBacking, Span<byte> result, out int written)
    {
        Debug.Assert(jsonElementBacking.ValueKind == JsonValueKind.String, "You must provide a string element.");

        if (jsonElementBacking.TryGetValue(RentBufferAndDecode, jsonElementBacking, out (byte[] RentedBuffer, int Written) rentedResult))
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

        written = rentedResult.Written;
        return false;
    }

    /// <summary>
    /// Decode a base64 string.
    /// </summary>
    /// <param name="stringBacking">The base64 string.</param>
    /// <param name="result">A buffer for the decoded bytes to be written.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the buffer was decoded.</returns>
    public static bool Decode(string? stringBacking, Span<byte> result, out int written)
    {
        if (stringBacking is null)
        {
            written = 0;
            return false;
        }

        if (Convert.TryFromBase64String(stringBacking, result, out written))
        {
            return true;
        }
        else
        {
            if (result.Length >= stringBacking.Length)
            {
                written = 0;
            }
            else
            {
                written = stringBacking.Length;
            }

            return false;
        }
    }

    private static bool RentBufferAndDecode(ReadOnlySpan<char> span, in JsonElement state, out (byte[] RentedBuffer, int Written) result)
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
    /// <summary>
    /// Gets a value indcating whether the string has valid base64 encoded content.
    /// </summary>
    /// <param name="stringBacking">The value to test.</param>
    /// <returns><see langword="true"/> if the value has valid base64 encoded bytes.</returns>
    public static bool HasBase64Bytes(string? stringBacking)
    {
        try
        {
            Convert.FromBase64String(stringBacking);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Decode a base64 string.
    /// </summary>
    /// <param name="jsonElementBacking">The JSON element containing the base64 string.</param>
    /// <param name="result">A buffer for the decoded bytes to be written.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the buffer was decoded.</returns>
    public static bool Decode(in JsonElement jsonElementBacking, Span<byte> result, out int written)
    {
        return Decode(jsonElementBacking.GetString(), result, out written);
    }

    /// <summary>
    /// Decode a base64 string.
    /// </summary>
    /// <param name="stringBacking">The base64 string.</param>
    /// <param name="result">A buffer for the decoded bytes to be written.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the buffer was decoded.</returns>
    public static bool Decode(string? stringBacking, Span<byte> result, out int written)
    {
        if (stringBacking is null)
        {
            written = 0;
            return false;
        }

        try
        {
            byte[] writtenBytes = Convert.FromBase64String(stringBacking);
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