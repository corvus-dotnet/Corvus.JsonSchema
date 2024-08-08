// <copyright file="JsonReaderHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from code:
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable

using System;
using System.Buffers;
using System.Buffers.Text;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace Corvus.Json
{
    internal static partial class JsonReaderHelper
    {
        public static readonly UTF8Encoding s_utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        public static bool CanDecodeBase64(ReadOnlySpan<byte> utf8Unescaped)
        {
            byte[]? pooledArray = null;

            Span<byte> byteSpan = utf8Unescaped.Length <= JsonConstants.StackallocThreshold ?
                stackalloc byte[JsonConstants.StackallocThreshold] :
                (pooledArray = ArrayPool<byte>.Shared.Rent(utf8Unescaped.Length));

            OperationStatus status = Base64.DecodeFromUtf8(utf8Unescaped, byteSpan, out int bytesConsumed, out int bytesWritten);

            if (status != OperationStatus.Done)
            {
                if (pooledArray != null)
                {
                    ArrayPool<byte>.Shared.Return(pooledArray, true);
                }

                return false;
            }
            Debug.Assert(bytesConsumed == utf8Unescaped.Length);

            if (pooledArray != null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray, true);
            }

            return true;
        }

        public static void Unescape(ReadOnlySpan<char> source, Span<byte> destination, int idx, out int written)
        {
            Debug.Assert(idx >= 0 && idx < source.Length);
            Debug.Assert(source[idx] == JsonConstants.BackSlash);
            Debug.Assert(destination.Length >= source.Length);

            TranscodeHelper(source.Slice(0, idx), destination);
            written = idx;
            int lastWritten = -1;

            for (; idx < source.Length; idx++)
            {
                char currentChar = source[idx];
                if (currentChar == '\\')
                {
                    if (lastWritten != -1)
                    {
                        written += TranscodeHelper(source[lastWritten..idx], destination[written..]);
                        lastWritten = -1;
                    }

                    idx++;
                    currentChar = source[idx];

                    if (currentChar == '"')
                    {
                        destination[written++] = JsonConstants.Quote;
                    }
                    else if (currentChar == 'n')
                    {
                        destination[written++] = JsonConstants.LineFeed;
                    }
                    else if (currentChar == 'r')
                    {
                        destination[written++] = JsonConstants.CarriageReturn;
                    }
                    else if (currentChar == '\\')
                    {
                        destination[written++] = JsonConstants.BackSlash;
                    }
                    else if (currentChar == '/')
                    {
                        destination[written++] = JsonConstants.Slash;
                    }
                    else if (currentChar == 't')
                    {
                        destination[written++] = JsonConstants.Tab;
                    }
                    else if (currentChar == 'b')
                    {
                        destination[written++] = JsonConstants.BackSpace;
                    }
                    else if (currentChar == 'f')
                    {
                        destination[written++] = JsonConstants.FormFeed;
                    }
                    else if (currentChar == 'u')
                    {
                        // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                        // Otherwise, the Utf8JsonReader would have already thrown an exception.
                        Debug.Assert(source.Length >= idx + 5);

                        bool result = TryParse4DigitHex(source.Slice(idx + 1, 4), out int scalar, out int bytesConsumed);
                        Debug.Assert(result);
                        Debug.Assert(bytesConsumed == 4);
                        idx += bytesConsumed;     // The loop iteration will increment idx past the last hex digit

                        if (JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                        {
                            // The first hex value cannot be a low surrogate.
                            if (scalar >= JsonConstants.LowSurrogateStartValue)
                            {
                                throw new InvalidOperationException($"Read Invalid UTF16: {scalar}");
                            }

                            Debug.Assert(JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.HighSurrogateEndValue));

                            idx += 3;   // Skip the last hex digit and the next \u

                            // We must have a low surrogate following a high surrogate.
                            if (source.Length < idx + 4 || source[idx - 2] != '\\' || source[idx - 1] != 'u')
                            {
                                throw new InvalidOperationException("Read Invalid UTF16");
                            }

                            // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                            // Otherwise, the Utf8JsonReader would have already thrown an exception.
                            result = TryParse4DigitHex(source.Slice(idx, 4), out int lowSurrogate, out bytesConsumed);
                            Debug.Assert(result);
                            Debug.Assert(bytesConsumed == 4);

                            // If the first hex value is a high surrogate, the next one must be a low surrogate.
                            if (!JsonHelpers.IsInRangeInclusive((uint)lowSurrogate, JsonConstants.LowSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                            {
                                throw new InvalidOperationException($"Read Invalid UTF16: {lowSurrogate}");
                            }

                            idx += bytesConsumed - 1;  // The loop iteration will increment idx past the last hex digit

                            // To find the unicode scalar:
                            // (0x400 * (High surrogate - 0xD800)) + Low surrogate - 0xDC00 + 0x10000
                            scalar = (JsonConstants.BitShiftBy10 * (scalar - JsonConstants.HighSurrogateStartValue))
                                + (lowSurrogate - JsonConstants.LowSurrogateStartValue)
                                + JsonConstants.UnicodePlane01StartValue;
                        }

                        var rune = new Rune(scalar);
                        int bytesWritten = rune.EncodeToUtf8(destination.Slice(written));
                        Debug.Assert(bytesWritten <= 4);
                        written += bytesWritten;
                    }
                }
                else
                {
                    if (lastWritten == -1)
                    {
                        lastWritten = idx;
                    }

                    //// Instead of writing these in character by character, accumulate
                    /// a contiguous range and write them together
                    ////Span<char> buffer = stackalloc char[1];
                    ////buffer[0] = currentChar;
                    ////written += TranscodeHelper(buffer, destination[written..]);
                }
            }

            if (lastWritten != -1)
            {
                written += TranscodeHelper(source[lastWritten..], destination[written..]);
            }
        }

        public static void Unescape(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written)
        {
            Debug.Assert(idx >= 0 && idx < source.Length);
            Debug.Assert(source[idx] == JsonConstants.BackSlash);
            Debug.Assert(destination.Length >= source.Length);

            source.Slice(0, idx).CopyTo(destination);
            written = idx;

            for (; idx < source.Length; idx++)
            {
                byte currentByte = source[idx];
                if (currentByte == JsonConstants.BackSlash)
                {
                    idx++;
                    currentByte = source[idx];

                    if (currentByte == JsonConstants.Quote)
                    {
                        destination[written++] = JsonConstants.Quote;
                    }
                    else if (currentByte == 'n')
                    {
                        destination[written++] = JsonConstants.LineFeed;
                    }
                    else if (currentByte == 'r')
                    {
                        destination[written++] = JsonConstants.CarriageReturn;
                    }
                    else if (currentByte == JsonConstants.BackSlash)
                    {
                        destination[written++] = JsonConstants.BackSlash;
                    }
                    else if (currentByte == JsonConstants.Slash)
                    {
                        destination[written++] = JsonConstants.Slash;
                    }
                    else if (currentByte == 't')
                    {
                        destination[written++] = JsonConstants.Tab;
                    }
                    else if (currentByte == 'b')
                    {
                        destination[written++] = JsonConstants.BackSpace;
                    }
                    else if (currentByte == 'f')
                    {
                        destination[written++] = JsonConstants.FormFeed;
                    }
                    else if (currentByte == 'u')
                    {
                        // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                        // Otherwise, the Utf8JsonReader would have already thrown an exception.
                        Debug.Assert(source.Length >= idx + 5);

                        bool result = Utf8Parser.TryParse(source.Slice(idx + 1, 4), out int scalar, out int bytesConsumed, 'x');
                        Debug.Assert(result);
                        Debug.Assert(bytesConsumed == 4);
                        idx += bytesConsumed;     // The loop iteration will increment idx past the last hex digit

                        if (JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                        {
                            // The first hex value cannot be a low surrogate.
                            if (scalar >= JsonConstants.LowSurrogateStartValue)
                            {
                                throw new InvalidOperationException($"Read Invalid UTF16: {scalar}");
                            }

                            Debug.Assert(JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.HighSurrogateEndValue));

                            idx += 3;   // Skip the last hex digit and the next \u

                            // We must have a low surrogate following a high surrogate.
                            if (source.Length < idx + 4 || source[idx - 2] != '\\' || source[idx - 1] != 'u')
                            {
                                throw new InvalidOperationException("Read Invalid UTF16");
                            }

                            // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                            // Otherwise, the Utf8JsonReader would have already thrown an exception.
                            result = Utf8Parser.TryParse(source.Slice(idx, 4), out int lowSurrogate, out bytesConsumed, 'x');
                            Debug.Assert(result);
                            Debug.Assert(bytesConsumed == 4);

                            // If the first hex value is a high surrogate, the next one must be a low surrogate.
                            if (!JsonHelpers.IsInRangeInclusive((uint)lowSurrogate, JsonConstants.LowSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                            {
                                throw new InvalidOperationException($"Read Invalid UTF16: {lowSurrogate}");
                            }

                            idx += bytesConsumed - 1;  // The loop iteration will increment idx past the last hex digit

                            // To find the unicode scalar:
                            // (0x400 * (High surrogate - 0xD800)) + Low surrogate - 0xDC00 + 0x10000
                            scalar = (JsonConstants.BitShiftBy10 * (scalar - JsonConstants.HighSurrogateStartValue))
                                + (lowSurrogate - JsonConstants.LowSurrogateStartValue)
                                + JsonConstants.UnicodePlane01StartValue;
                        }

                        var rune = new Rune(scalar);
                        int bytesWritten = rune.EncodeToUtf8(destination.Slice(written));
                        Debug.Assert(bytesWritten <= 4);
                        written += bytesWritten;
                    }
                }
                else
                {
                    destination[written++] = currentByte;
                }
            }
        }

        public static int TranscodeHelper(ReadOnlySpan<char> unescaped, Span<byte> destination)
        {
            try
            {
#if NET8_0_OR_GREATER
                return s_utf8Encoding.GetBytes(unescaped, destination);
#else
                char[] chars = ArrayPool<char>.Shared.Rent(destination.Length);
                byte[] bytes = ArrayPool<byte>.Shared.Rent(unescaped.Length);

                unescaped.CopyTo(chars);

                try
                {
                    int written = s_utf8Encoding.GetBytes(chars, 0, unescaped.Length, bytes, 0);
                    bytes.AsSpan(0, written).CopyTo(destination);
                    return written;
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(chars);
                    ArrayPool<byte>.Shared.Return(bytes);
                }
#endif
            }
            catch (DecoderFallbackException dfe)
            {
                // We want to be consistent with the exception being thrown
                // so the user only has to catch a single exception.
                // Since we already throw InvalidOperationException for mismatch token type,
                // and while unescaping, using that exception for failure to decode invalid UTF-8 bytes as well.
                // Therefore, wrapping the DecoderFallbackException around an InvalidOperationException.
                throw new InvalidOperationException("Cannot transcode invalid UTF8 bytes.", dfe);
            }
            catch (ArgumentException)
            {
                // Destination buffer was too small; clear it up since the encoder might have not.
                destination.Clear();
                throw;
            }
        }

        public static int TranscodeHelper(ReadOnlySpan<byte> utf8Unescaped, Span<char> destination)
        {
            try
            {
#if NET8_0_OR_GREATER
                return s_utf8Encoding.GetChars(utf8Unescaped, destination);
#else
                char[] chars = ArrayPool<char>.Shared.Rent(destination.Length);
                byte[] bytes = ArrayPool<byte>.Shared.Rent(utf8Unescaped.Length);

                utf8Unescaped.CopyTo(bytes);

                try
                {
                    int written = s_utf8Encoding.GetChars(bytes, 0, utf8Unescaped.Length, chars, 0);
                    chars.AsSpan(0, written).CopyTo(destination);
                    return written;
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(chars);
                    ArrayPool<byte>.Shared.Return(bytes);
                }
#endif
            }
            catch (DecoderFallbackException dfe)
            {
                // We want to be consistent with the exception being thrown
                // so the user only has to catch a single exception.
                // Since we already throw InvalidOperationException for mismatch token type,
                // and while unescaping, using that exception for failure to decode invalid UTF-8 bytes as well.
                // Therefore, wrapping the DecoderFallbackException around an InvalidOperationException.
                throw new InvalidOperationException("Cannot transcode invalid UTF8 bytes.", dfe);
            }
            catch (ArgumentException)
            {
                // Destination buffer was too small; clear it up since the encoder might have not.
                destination.Clear();
                throw;
            }
        }

        public static bool TryParse4DigitHex(ReadOnlySpan<char> hexSpan, out int result, out int charsRead)
        {
            result = 0;
            charsRead = 0;

            if (hexSpan.Length != 4)
            {
                return false;
            }

            for (int i = 0; i < hexSpan.Length; i++)
            {
                result <<= 4; // Shift result left by 4 bits to make room for the next hex digit
                char c = hexSpan[i];
                if (c >= '0' && c <= '9')
                {
                    result += c - '0';
                }
                else if (c >= 'A' && c <= 'F')
                {
                    result += c - 'A' + 10;
                }
                else if (c >= 'a' && c <= 'f')
                {
                    result += c - 'a' + 10;
                }
                else
                {
                    result = 0;
                    charsRead = i;
                    return false;
                }

                charsRead++;
            }

            return true;
        }
    }
}