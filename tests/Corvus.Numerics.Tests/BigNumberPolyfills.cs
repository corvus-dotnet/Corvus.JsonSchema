// <copyright file="BigNumberPolyfills.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Corvus.Numerics;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Polyfill extension methods for BigNumber to support Span-based APIs on .NET Framework 4.8.1.
/// </summary>
internal static class BigNumberPolyfills
{
    /// <summary>
    /// Creates a string from a Span of characters (polyfill for string constructor).
    /// </summary>
    public static unsafe string CreateString(ReadOnlySpan<char> chars)
    {
        if (chars.IsEmpty)
        {
            return string.Empty;
        }

        fixed (char* ptr = &MemoryMarshal.GetReference(chars))
        {
            return new string(ptr, 0, chars.Length);
        }
    }

    /// <summary>
    /// Polyfill for TryFormat with Span&lt;char&gt; that delegates to array-based implementation.
    /// </summary>
    public static bool TryFormat(
        this BigNumber value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        string formatString = format.IsEmpty ? string.Empty : CreateString(format);
        string result = value.ToString(formatString, provider);
        
        if (result.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        
        result.AsSpan().CopyTo(destination);
        charsWritten = result.Length;
        return true;
    }

    /// <summary>
    /// Polyfill for TryFormat with Span&lt;byte&gt; (UTF-8) that delegates to array-based implementation.
    /// </summary>
    public static bool TryFormat(
        this BigNumber value,
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        string formatString = format.IsEmpty ? string.Empty : CreateString(format);
        string result = value.ToString(formatString, provider);
        
        byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(result);
        
        if (utf8Bytes.Length > utf8Destination.Length)
        {
            bytesWritten = 0;
            return false;
        }
        
        utf8Bytes.AsSpan().CopyTo(utf8Destination);
        bytesWritten = utf8Bytes.Length;
        return true;
    }

    /// <summary>
    /// Polyfill for TryFormatOptimized with Span&lt;char&gt;.
    /// </summary>
    public static bool TryFormatOptimized(
        this BigNumber value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        // For .NET Framework, just delegate to the regular TryFormat polyfill
        return value.TryFormat(destination, out charsWritten, format, provider);
    }

    /// <summary>
    /// Polyfill for TryFormatUtf8Optimized with Span&lt;byte&gt;.
    /// </summary>
    public static bool TryFormatUtf8Optimized(
        this BigNumber value,
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        // For .NET Framework, just delegate to the regular TryFormat polyfill
        return value.TryFormat(utf8Destination, out bytesWritten, format, provider);
    }
}
#endif