// <copyright file="Utf8Uri.EscapeDataString.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// A UTF-8 URI.
/// </summary>
public readonly ref partial struct Utf8Uri
{
    /// <summary>
    /// Escapes a UTF-8 string per RFC 3986 <c>EscapeDataString</c> semantics.
    /// Unreserved characters (A-Z, a-z, 0-9, '-', '_', '.', '~') pass through;
    /// all other bytes are percent-encoded as <c>%XX</c> with uppercase hex digits.
    /// </summary>
    /// <param name="utf8String">The unescaped UTF-8 bytes to encode.</param>
    /// <param name="destination">The buffer into which to write the escaped result.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if the entire source was escaped into the destination;
    /// <see langword="false"/> if the destination was too small.</returns>
    public static bool TryEscapeDataString(ReadOnlySpan<byte> utf8String, Span<byte> destination, out int bytesWritten)
    {
        return Utf8UriTools.TryEscapeDataString(utf8String, destination, out bytesWritten);
    }

    /// <summary>
    /// Unescapes a percent-encoded UTF-8 string, matching <c>Uri.UnescapeDataString</c> semantics.
    /// Valid <c>%XX</c> sequences are decoded. Non-ASCII decoded bytes are only emitted when
    /// consecutive <c>%XX</c> sequences form a valid UTF-8 multi-byte sequence; invalid
    /// multi-byte sequences remain in their encoded form.
    /// Malformed sequences (invalid hex digits, truncated <c>%</c> at end of input) are
    /// copied through literally.
    /// </summary>
    /// <param name="utf8String">The percent-encoded UTF-8 bytes to decode.</param>
    /// <param name="destination">The buffer into which to write the unescaped result.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if the entire source was unescaped into the destination;
    /// <see langword="false"/> if the destination was too small.</returns>
    public static bool TryUnescapeDataString(ReadOnlySpan<byte> utf8String, Span<byte> destination, out int bytesWritten)
    {
        return Utf8UriTools.TryUnescapeDataString(utf8String, destination, out bytesWritten);
    }

    /// <summary>
    /// Escapes a UTF-8 string with URI semantics (matching JavaScript's <c>encodeURI</c>).
    /// RFC 3986 unreserved characters plus reserved characters
    /// (<c>: / ? # [ ] @ ! $ &amp; ' ( ) * + , ; =</c>) pass through unescaped;
    /// all other bytes are percent-encoded as <c>%XX</c> with uppercase hex digits.
    /// </summary>
    /// <param name="utf8String">The UTF-8 bytes to encode.</param>
    /// <param name="destination">The buffer into which to write the escaped result.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if the entire source was escaped into the destination;
    /// <see langword="false"/> if the destination was too small.</returns>
    public static bool TryEscapeUri(ReadOnlySpan<byte> utf8String, Span<byte> destination, out int bytesWritten)
    {
        return Utf8UriTools.TryEscapeUri(utf8String, destination, out bytesWritten);
    }
}