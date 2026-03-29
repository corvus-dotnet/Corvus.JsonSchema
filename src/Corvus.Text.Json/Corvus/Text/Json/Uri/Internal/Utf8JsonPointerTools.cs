// <copyright file="Utf8JsonPointerTools.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides methods for validating JSON Pointer strings.
/// </summary>
internal static class Utf8JsonPointerTools
{
    /// <summary>
    /// Validates whether the specified byte span represents a valid JSON Pointer.
    /// </summary>
    /// <param name="span">The byte span to validate.</param>
    /// <returns><see langword="true"/> if the span is a valid JSON Pointer; otherwise, <see langword="false"/>.</returns>
    public static bool Validate(ReadOnlySpan<byte> span)
    {
        int i = 0;
        int length = span.Length;

        // An empty string is a valid JSON pointer (root)
        if (length == 0)
        {
            return true;
        }

        while (i < length)
        {
            // Each reference-token must be prefixed by '/'
            if (span[i] != (byte)'/')
            {
                return false;
            }

            i++;

            // reference-token: *( unescaped / escaped )
            while (i < length && span[i] != (byte)'/')
            {
                if (span[i] == (byte)'~')
                {
                    // escaped: "~" ("0" / "1")
                    if (i + 1 >= length)
                    {
                        return false;
                    }

                    byte next = span[i + 1];
                    if (next != (byte)'0' && next != (byte)'1')
                    {
                        return false;
                    }

                    i += 2;
                }
                else
                {
                    // unescaped: %x00-2E / %x30-7D / %x7F-10FFFF
                    byte b = span[i];
                    if ((b >= 0x00 && b <= 0x2E) ||
                        (b >= 0x30 && b <= 0x7D) ||
                        (b == 0x7F))
                    {
                        i++;
                    }
                    else if (b >= 0x80)
                    {
                        if (Rune.DecodeFromUtf8(span.Slice(i), out _, out int runeLen) != System.Buffers.OperationStatus.Done)
                        {
                            return false;
                        }

                        if (runeLen == 0 || i + runeLen > length)
                        {
                            return false;
                        }

                        i += runeLen;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Validates whether the specified byte span represents a valid relative JSON Pointer.
    /// </summary>
    /// <param name="span">The byte span to validate.</param>
    /// <returns><see langword="true"/> if the span is a valid relative JSON Pointer; otherwise, <see langword="false"/>.</returns>
    public static bool ValidateRelative(ReadOnlySpan<byte> span)
    {
        int i = 0;
        int length = span.Length;

        // Parse non-negative-integer (origin-specification)
        if (i >= length) return false;
        if (span[i] == (byte)'0')
        {
            i++;
        }
        else if (IsNonZeroDigit(span[i]))
        {
            i++;
            while (i < length && IsDigit(span[i]))
            {
                i++;
            }
        }
        else
        {
            return false;
        }

        // Optional index-manipulation
        if (i < length && (span[i] == (byte)'+' || span[i] == (byte)'-'))
        {
            i++;

            // Must be positive-integer (no leading zero)
            if (i >= length || !IsNonZeroDigit(span[i])) return false;
            i++;
            while (i < length && IsDigit(span[i]))
            {
                i++;
            }
        }

        // Must be followed by either '#' or a valid json-pointer
        if (i == length)
        {
            // No '#' or json-pointer, valid
            return true;
        }

        if (span[i] == (byte)'#')
        {
            // '#' must be the last character
            return i + 1 == length;
        }
        else if (span[i] == (byte)'/' || i == length)
        {
            // json-pointer: *( "/" reference-token )
            // Use Utf8JsonPointer.Validate for the remainder
            return Utf8JsonPointerTools.Validate(span.Slice(i));
        }
        else if (i == length)
        {
            // End of input, but no '#' or json-pointer
            return false;
        }
        else
        {
            // Invalid character after origin-specification/index-manipulation
            return false;
        }

        static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';

        static bool IsNonZeroDigit(byte b) => b >= (byte)'1' && b <= (byte)'9';
    }

    /// <summary>
    /// Decodes the ~ encoding in a reference.
    /// </summary>
    /// <param name="encodedFragment">The encoded reference.</param>
    /// <param name="fragment">The span into which to write the result.</param>
    /// <returns>The length of the decoded reference.</returns>
    public static int DecodePointer(ReadOnlySpan<byte> encodedFragment, Span<byte> fragment)
    {
        int readIndex = 0;
        int writeIndex = 0;

        while (readIndex < encodedFragment.Length)
        {
            if (encodedFragment[readIndex] != '~')
            {
                fragment[writeIndex] = encodedFragment[readIndex];
                readIndex++;
                writeIndex++;
            }
            else
            {
                if (readIndex >= encodedFragment.Length - 1)
                {
                    ThrowHelper.ThrowInvalidOperation_JsonPointer_Expected_Digit_After_Escape_Character_Found_End();
                }

                if (encodedFragment[readIndex + 1] == '0')
                {
                    fragment[writeIndex] = (byte)'~';
                }
                else if (encodedFragment[readIndex + 1] == '1')
                {
                    fragment[writeIndex] = (byte)'/';
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperation_JsonPointer_Expected_Digit_After_Escape_Character();
                }

                readIndex += 2;
                writeIndex++;
            }
        }

        return writeIndex;
    }
}