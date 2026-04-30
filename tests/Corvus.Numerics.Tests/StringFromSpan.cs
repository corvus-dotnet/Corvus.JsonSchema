// <copyright file="StringFromSpan.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Helper class to create strings from spans in a cross-platform way.
/// </summary>
internal static class StringFromSpan
{
    /// <summary>
    /// Creates a string from a ReadOnlySpan of characters.
    /// </summary>
    /// <param name="chars">The span of characters.</param>
    /// <returns>A new string.</returns>
    public static string Create(ReadOnlySpan<char> chars)
    {
#if NET
        return new string(chars);
#else
        return chars.ToString();
#endif
    }

    /// <summary>
    /// Creates a string from a Span of characters.
    /// </summary>
    /// <param name="chars">The span of characters.</param>
    /// <returns>A new string.</returns>
    public static string Create(Span<char> chars)
    {
#if NET
        return new string(chars);
#else
        return chars.ToString();
#endif
    }

    /// <summary>
    /// Creates a UTF-8 string from a ReadOnlySpan of bytes.
    /// </summary>
    /// <param name="utf8Bytes">The span of UTF-8 encoded bytes.</param>
    /// <returns>A new string.</returns>
    public static string CreateFromUtf8(ReadOnlySpan<byte> utf8Bytes)
    {
#if NET
        return System.Text.Encoding.UTF8.GetString(utf8Bytes);
#else
        return System.Text.Encoding.UTF8.GetString(utf8Bytes.ToArray());
#endif
    }

    /// <summary>
    /// Creates a UTF-8 string from a Span of bytes.
    /// </summary>
    /// <param name="utf8Bytes">The span of UTF-8 encoded bytes.</param>
    /// <returns>A new string.</returns>
    public static string CreateFromUtf8(Span<byte> utf8Bytes)
    {
#if NET
        return System.Text.Encoding.UTF8.GetString(utf8Bytes);
#else
        return System.Text.Encoding.UTF8.GetString(utf8Bytes.ToArray());
#endif
    }
}