// <copyright file="JsonStringUnescaper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json;

/// <summary>
/// Provides a public API for unescaping JSON-encoded UTF-8 byte sequences.
/// </summary>
/// <remarks>
/// <para>
/// This class exposes the battle-tested unescaping logic for use by companion packages
/// (e.g. Corvus.Text.Json.Yaml) that need to decode JSON escape sequences without
/// duplicating the implementation.
/// </para>
/// <para>
/// All standard JSON escape sequences are handled: <c>\"</c>, <c>\\</c>, <c>\/</c>,
/// <c>\n</c>, <c>\r</c>, <c>\t</c>, <c>\b</c>, <c>\f</c>, and <c>\uXXXX</c>
/// (including surrogate pairs for characters outside the Basic Multilingual Plane).
/// </para>
/// </remarks>
public static class JsonStringUnescaper
{
    /// <summary>
    /// Unescapes a JSON-encoded UTF-8 byte sequence, writing the result to <paramref name="destination"/>.
    /// The first backslash is located automatically.
    /// </summary>
    /// <param name="source">The escaped UTF-8 source bytes. Must contain at least one backslash.</param>
    /// <param name="destination">The destination buffer. Must be at least as long as <paramref name="source"/>.</param>
    /// <param name="written">The number of bytes written to <paramref name="destination"/>.</param>
    public static void Unescape(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
    {
        JsonReaderHelper.Unescape(source, destination, out written);
    }

    /// <summary>
    /// Unescapes a JSON-encoded UTF-8 byte sequence, writing the result to <paramref name="destination"/>.
    /// The caller provides the index of the first backslash to avoid a redundant scan.
    /// </summary>
    /// <param name="source">The escaped UTF-8 source bytes.</param>
    /// <param name="destination">The destination buffer. Must be at least as long as <paramref name="source"/>.</param>
    /// <param name="idx">The index of the first backslash in <paramref name="source"/>.</param>
    /// <param name="written">The number of bytes written to <paramref name="destination"/>.</param>
    public static void Unescape(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written)
    {
        JsonReaderHelper.Unescape(source, destination, idx, out written);
    }

    /// <summary>
    /// Attempts to unescape a JSON-encoded UTF-8 byte sequence into <paramref name="destination"/>.
    /// Returns <see langword="false"/> if the destination buffer is too small.
    /// The first backslash is located automatically.
    /// </summary>
    /// <param name="source">The escaped UTF-8 source bytes. Must contain at least one backslash.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="written">The number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if the unescaped output fit in <paramref name="destination"/>; otherwise <see langword="false"/>.</returns>
    public static bool TryUnescape(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
    {
        return JsonReaderHelper.TryUnescape(source, destination, out written);
    }
}