// <copyright file="JsonReaderHelper.Unescape.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers.Text;
using System.Diagnostics;
using System.Text;
#if BUILDING_SOURCE_GENERATOR
using Corvus.Json;
#endif

#if STJ
namespace Corvus.Yaml.Internal;
#else
namespace Corvus.Text.Json;
#endif

internal static partial class JsonReaderHelper
{
    internal static void Unescape(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
    {
        Debug.Assert(destination.Length >= source.Length);

        int idx = source.IndexOf(JsonConstants.BackSlash);
        Debug.Assert(idx >= 0);

        bool result = TryUnescape(source, destination, idx, out written);
        Debug.Assert(result);
    }

    internal static void Unescape(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written)
    {
        Debug.Assert(idx >= 0 && idx < source.Length);
        Debug.Assert(source[idx] == JsonConstants.BackSlash);
        Debug.Assert(destination.Length >= source.Length);

        bool result = TryUnescape(source, destination, idx, out written);
        Debug.Assert(result);
    }

    /// <summary>
    /// Used when writing to buffers not guaranteed to fit the unescaped result.
    /// </summary>
    internal static bool TryUnescape(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
    {
        int idx = source.IndexOf(JsonConstants.BackSlash);
        Debug.Assert(idx >= 0);

        return TryUnescape(source, destination, idx, out written);
    }

    /// <summary>
    /// Unescapes text that may hold an escape of half of a surrogate pair with no other half (such as <c>\ud800</c>),
    /// which the parser accepts. Such a code unit is written as its generalized UTF-8 bytes (<c>\ud800</c> as
    /// <c>ED A0 80</c>), which are not UTF-8, so the result reads as the same text with those bytes raw would. Every
    /// other escape is unescaped as <see cref="Unescape(ReadOnlySpan{byte}, Span{byte}, out int)"/> does it.
    /// </summary>
    /// <param name="source">The escaped text, with at least one escape.</param>
    /// <param name="destination">The destination, at least as long as <paramref name="source"/>.</param>
    /// <param name="written">The number of bytes written.</param>
    internal static void UnescapeToGeneralizedUtf8(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
    {
        Debug.Assert(destination.Length >= source.Length);

        int idx = source.IndexOf(JsonConstants.BackSlash);
        Debug.Assert(idx >= 0);

        bool result = TryUnescape(source, destination, idx, out written, generalizeLoneSurrogates: true);
        Debug.Assert(result);
    }

    /// <summary>
    /// <see cref="UnescapeToGeneralizedUtf8(ReadOnlySpan{byte}, Span{byte}, out int)"/>, given the index of the first
    /// escape.
    /// </summary>
    /// <param name="source">The escaped text.</param>
    /// <param name="destination">The destination, at least as long as <paramref name="source"/>.</param>
    /// <param name="idx">The index of the first backslash in <paramref name="source"/>.</param>
    /// <param name="written">The number of bytes written.</param>
    internal static void UnescapeToGeneralizedUtf8(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written)
    {
        Debug.Assert(idx >= 0 && idx < source.Length);
        Debug.Assert(source[idx] == JsonConstants.BackSlash);
        Debug.Assert(destination.Length >= source.Length);

        bool result = TryUnescape(source, destination, idx, out written, generalizeLoneSurrogates: true);
        Debug.Assert(result);
    }

    /// <summary>
    /// Used when writing to buffers not guaranteed to fit the unescaped result.
    /// </summary>
    private static bool TryUnescape(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written, bool generalizeLoneSurrogates = false)
    {
        Debug.Assert(idx >= 0 && idx < source.Length);
        Debug.Assert(source[idx] == JsonConstants.BackSlash);

        if (!source.Slice(0, idx).TryCopyTo(destination))
        {
            written = 0;
            goto DestinationTooShort;
        }

        written = idx;

        while (true)
        {
            Debug.Assert(source[idx] == JsonConstants.BackSlash);

            if (written == destination.Length)
            {
                goto DestinationTooShort;
            }

            switch (source[++idx])
            {
                case JsonConstants.Quote:
                    destination[written++] = JsonConstants.Quote;
                    break;

                case (byte)'n':
                    destination[written++] = JsonConstants.LineFeed;
                    break;

                case (byte)'r':
                    destination[written++] = JsonConstants.CarriageReturn;
                    break;

                case JsonConstants.BackSlash:
                    destination[written++] = JsonConstants.BackSlash;
                    break;

                case JsonConstants.Slash:
                    destination[written++] = JsonConstants.Slash;
                    break;

                case (byte)'t':
                    destination[written++] = JsonConstants.Tab;
                    break;

                case (byte)'b':
                    destination[written++] = JsonConstants.BackSpace;
                    break;

                case (byte)'f':
                    destination[written++] = JsonConstants.FormFeed;
                    break;

                default:
                    Debug.Assert(source[idx] == 'u', "invalid escape sequences must have already been caught by Utf8JsonReader.Read()");

                    // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                    // Otherwise, the Utf8JsonReader would have already thrown an exception.
                    Debug.Assert(source.Length >= idx + 5);

                    bool result = Utf8Parser.TryParse(source.Slice(idx + 1, 4), out int scalar, out int bytesConsumed, 'x');
                    Debug.Assert(result);
                    Debug.Assert(bytesConsumed == 4);
                    idx += 4;

                    if (JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                    {
                        // The first hex value cannot be a low surrogate.
                        if (scalar >= JsonConstants.LowSurrogateStartValue)
                        {
                            if (generalizeLoneSurrogates)
                            {
                                WriteGeneralizedUtf8(scalar, destination, ref written);
                                break;
                            }

                            ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(scalar);
                        }

                        Debug.Assert(JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.HighSurrogateEndValue));

                        // We must have a low surrogate following a high surrogate.
                        if (source.Length < idx + 7 || source[idx + 1] != '\\' || source[idx + 2] != 'u')
                        {
                            if (generalizeLoneSurrogates)
                            {
                                WriteGeneralizedUtf8(scalar, destination, ref written);
                                break;
                            }

                            ThrowHelper.ThrowInvalidOperationException_ReadIncompleteUTF16();
                        }

                        // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                        // Otherwise, the Utf8JsonReader would have already thrown an exception.
                        result = Utf8Parser.TryParse(source.Slice(idx + 3, 4), out int lowSurrogate, out bytesConsumed, 'x');
                        Debug.Assert(result);
                        Debug.Assert(bytesConsumed == 4);

                        // If the first hex value is a high surrogate, the next one must be a low surrogate.
                        if (!JsonHelpers.IsInRangeInclusive((uint)lowSurrogate, JsonConstants.LowSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                        {
                            if (generalizeLoneSurrogates)
                            {
                                // The next escape is not consumed: it is unescaped on its own (it may begin a pair).
                                WriteGeneralizedUtf8(scalar, destination, ref written);
                                break;
                            }

                            ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(lowSurrogate);
                        }

                        idx += 6;

                        // To find the unicode scalar:
                        // (0x400 * (High surrogate - 0xD800)) + Low surrogate - 0xDC00 + 0x10000
                        scalar = (JsonConstants.BitShiftBy10 * (scalar - JsonConstants.HighSurrogateStartValue))
                            + (lowSurrogate - JsonConstants.LowSurrogateStartValue)
                            + JsonConstants.UnicodePlane01StartValue;
                    }

                    var rune = new Rune(scalar);
                    bool success = rune.TryEncodeToUtf8(destination.Slice(written), out int bytesWritten);
                    if (!success)
                    {
                        goto DestinationTooShort;
                    }

                    Debug.Assert(bytesWritten <= 4);
                    written += bytesWritten;
                    break;
            }

            if (++idx == source.Length)
            {
                goto Success;
            }

            if (source[idx] != JsonConstants.BackSlash)
            {
                ReadOnlySpan<byte> remaining = source.Slice(idx);
                int nextUnescapedSegmentLength = remaining.IndexOf(JsonConstants.BackSlash);
                if (nextUnescapedSegmentLength < 0)
                {
                    nextUnescapedSegmentLength = remaining.Length;
                }

                if ((uint)(written + nextUnescapedSegmentLength) >= (uint)destination.Length)
                {
                    goto DestinationTooShort;
                }

                Debug.Assert(nextUnescapedSegmentLength > 0);
                switch (nextUnescapedSegmentLength)
                {
                    case 1:
                        destination[written++] = source[idx++];
                        break;

                    case 2:
                        destination[written++] = source[idx++];
                        destination[written++] = source[idx++];
                        break;

                    case 3:
                        destination[written++] = source[idx++];
                        destination[written++] = source[idx++];
                        destination[written++] = source[idx++];
                        break;

                    default:
                        remaining.Slice(0, nextUnescapedSegmentLength).CopyTo(destination.Slice(written));
                        written += nextUnescapedSegmentLength;
                        idx += nextUnescapedSegmentLength;
                        break;
                }

                Debug.Assert(idx == source.Length || source[idx] == JsonConstants.BackSlash);

                if (idx == source.Length)
                {
                    goto Success;
                }
            }
        }

    Success:
        return true;

    DestinationTooShort:
        return false;
    }

    /// <summary>
    /// Writes a lone surrogate code unit as the three bytes UTF-8 would give it as a scalar (generalized UTF-8). The
    /// destination always has room: the escape it replaces is six bytes.
    /// </summary>
    private static void WriteGeneralizedUtf8(int codeUnit, Span<byte> destination, ref int written)
    {
        destination[written++] = (byte)(0xE0 | (codeUnit >> 12));
        destination[written++] = (byte)(0x80 | ((codeUnit >> 6) & 0x3F));
        destination[written++] = (byte)(0x80 | (codeUnit & 0x3F));
    }
}