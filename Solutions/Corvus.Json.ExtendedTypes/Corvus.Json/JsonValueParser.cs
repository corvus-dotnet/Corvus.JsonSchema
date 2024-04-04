// <copyright file="JsonValueParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Parses JSON values.
/// </summary>
public static class JsonValueParser
{
    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to parse.</typeparam>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static T ParseValue<T>(ReadOnlySpan<char> buffer)
    where T : struct, IJsonValue<T>
    {
#if NET8_0_OR_GREATER
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(buffer.Length);
        byte[]? pooledBytes = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocThreshold ?
            stackalloc byte[maxByteCount] :
            (pooledBytes = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(buffer, utf8Buffer);
            Utf8JsonReader reader = new(utf8Buffer[..written]);
            return ParseValue<T>(ref reader);
        }
        finally
        {
            if (pooledBytes is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledBytes, true);
            }
        }
#else
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(buffer.Length);
        byte[] utf8Buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        char[] charBuffer = ArrayPool<char>.Shared.Rent(buffer.Length);
        buffer.CopyTo(charBuffer);

        try
        {
            int written = Encoding.UTF8.GetBytes(charBuffer, 0, buffer.Length, utf8Buffer, 0);
            Utf8JsonReader reader = new(utf8Buffer.AsSpan(0, written));
            return ParseValue<T>(ref reader);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(utf8Buffer, true);
            ArrayPool<char>.Shared.Return(charBuffer, true);
        }
#endif
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to parse.</typeparam>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static T ParseValue<T>(ReadOnlySpan<byte> buffer)
        where T : struct, IJsonValue<T>
    {
        Utf8JsonReader reader = new(buffer);
        return ParseValue<T>(ref reader);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to parse.</typeparam>
    /// <param name="reader">The reader from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static T ParseValue<T>(ref Utf8JsonReader reader)
        where T : struct, IJsonValue<T>
    {
#if NET8_0_OR_GREATER
        return T.FromJson(JsonElement.ParseValue(ref reader));
#else
        return JsonValueNetStandard20Extensions.FromJsonElement<T>(JsonElement.ParseValue(ref reader));
#endif
    }
}