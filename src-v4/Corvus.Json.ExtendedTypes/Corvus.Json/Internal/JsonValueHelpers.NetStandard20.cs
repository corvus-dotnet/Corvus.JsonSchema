// <copyright file="JsonValueHelpers.NetStandard20.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// JsonValueHelpers for netstandard2.0.
/// </summary>
public static partial class JsonValueHelpers
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
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(buffer.Length);
        byte[] utf8Buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        char[] sourceBuffer = ArrayPool<char>.Shared.Rent(buffer.Length);

        buffer.CopyTo(sourceBuffer);
        try
        {
            int written = Encoding.UTF8.GetBytes(sourceBuffer, 0, buffer.Length, utf8Buffer, 0);
            Utf8JsonReader reader = new(utf8Buffer.AsSpan(0, written));
            return ParseValue<T>(ref reader);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(utf8Buffer, true);
            ArrayPool<char>.Shared.Return(sourceBuffer, true);
        }
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
        return JsonValueNetStandard20Extensions.FromJsonElement<T>(JsonElement.ParseValue(ref reader));
    }
}

#endif