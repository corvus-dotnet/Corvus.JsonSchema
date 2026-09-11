// <copyright file="NetStandardExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET5_0_OR_GREATER

using System.Text;

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// Span-based API shims for the netstandard targets.
/// </summary>
internal static class NetStandardExtensions
{
    public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        byte[] array = bytes.ToArray();
        return encoding.GetString(array, 0, array.Length);
    }

    public static int GetBytes(this Encoding encoding, string text, Span<byte> destination)
    {
        byte[] array = encoding.GetBytes(text);
        array.AsSpan().CopyTo(destination);
        return array.Length;
    }

    public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> text, Span<byte> destination)
    {
        return encoding.GetBytes(text.ToString(), destination);
    }

    public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        if (dictionary.ContainsKey(key))
        {
            return false;
        }

        dictionary.Add(key, value);
        return true;
    }
}

#endif