// <copyright file="Utf8UriHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods for UTF-8 URI processing.
/// </summary>
internal static class Utf8UriHelper
{
#if NET

    /// <summary>
    /// SearchValues for all ASCII letters and digits, as well as the RFC3986 unreserved marks '-', '_', '.', and '~'.
    /// </summary>
    public static readonly SearchValues<char> Unreserved =
        SearchValues.Create("-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~");

    /// <summary>
    /// SearchValues for all ASCII letters and digits, as well as the RFC3986 unreserved marks '-', '_', '.', and '~' (byte version).
    /// </summary>
    public static readonly SearchValues<byte> UnreservedBytes =
        SearchValues.Create("-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~"u8);

#else
    /// <summary>
    /// Span of all ASCII letters and digits, as well as the RFC3986 unreserved marks '-', '_', '.', and '~'.
    /// </summary>
    public static ReadOnlySpan<char> Unreserved => "-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~";

    /// <summary>
    /// Span of all ASCII letters and digits, as well as the RFC3986 unreserved marks '-', '_', '.', and '~' (byte version).
    /// </summary>
    public static ReadOnlySpan<byte> UnreservedBytes => "-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~"u8;
#endif

    /// <summary>
    /// Determines whether the specified byte represents a linear white space character.
    /// </summary>
    /// <param name="b">The byte to check.</param>
    /// <returns><see langword="true"/> if the byte is a linear white space character; otherwise, <see langword="false"/>.</returns>
    internal static bool IsLWS(byte b)
    {
        return (b <= (byte)' ') && (b == (byte)' ' || b == (byte)'\n' || b == (byte)'\r' || b == (byte)'\t');
    }

    /// <summary>
    /// Converts an ASCII scheme byte span to a lowercase invariant string.
    /// </summary>
    /// <param name="asciiSpan">The ASCII span to convert.</param>
    /// <returns>A lowercase invariant string representation of the scheme.</returns>
    internal static string AsciiSchemeToLowerInvariantString(ReadOnlySpan<byte> asciiSpan)
    {
        Debug.Assert(asciiSpan.Length < Utf8UriTools.c_MaxUriSchemeName);

        Span<char> buffer = stackalloc char[asciiSpan.Length];
        Span<char> buffer2 = stackalloc char[asciiSpan.Length];
        int charsWritten = JsonReaderHelper.TranscodeHelper(asciiSpan, buffer);
        Debug.Assert(charsWritten == buffer.Length);
        charsWritten = buffer.ToLowerInvariant(buffer2);
        Debug.Assert(charsWritten == buffer2.Length);
        return buffer2.ToString();
    }

    /// <summary>
    /// Converts 2 hex chars to a byte (returned in a char), e.g, "0a" becomes (char)0x0A.
    /// <para>If either char is not hex, returns <see cref="Uri.c_DummyChar"/>.</para>
    /// </summary>
    internal static char DecodeHexChars(int first, int second)
    {
        int a = HexConverter.FromChar(first);
        int b = HexConverter.FromChar(second);

        if ((a | b) == 0xFF)
        {
            // either a or b is 0xFF (invalid)
            return Utf8UriTools.c_DummyChar;
        }

        return (char)((a << 4) | b);
    }
}