// <copyright file="Utf8UriUncNameHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods for validating UNC (Universal Naming Convention) names.
/// </summary>
internal static class Utf8UriUncNameHelper
{
    /// <summary>
    /// The maximum length for an internet name.
    /// </summary>
    public const int MaximumInternetNameLength = 256;

    // IsValid
    // ATTN: This class has been re-designed as to conform to XP+ UNC hostname format
    // It is now similar to DNS name but can contain Unicode characters as well
    // This class will be removed and replaced by IDN specification later,
    // but for now we violate URI RFC cause we never escape Unicode characters on the wire
    // For the same reason we never unescape UNC host names since we never accept
    // them in escaped format.
    // Valid UNC server name chars:
    // a Unicode Letter    (not allowed as the only in a segment)
    // a Latin-1 digit
    // '-'    45 0x2D
    // '.'    46 0x2E    (only as a host domain delimiter)
    // '_'    95 0x5F
    // Assumption is the caller will check on the resulting name length
    // Remarks:  MUST NOT be used unless all input indexes are verified and trusted.

    /// <summary>
    /// Determines whether the specified UNC name is valid.
    /// </summary>
    /// <param name="name">Pointer to the name to validate.</param>
    /// <param name="start">The start index of the name.</param>
    /// <param name="returnedEnd">A reference to the end index; updated with the actual end of the valid name.</param>
    /// <param name="notImplicitFile">A value indicating whether this is not an implicit file URI.</param>
    /// <returns><see langword="true"/> if the UNC name is valid; otherwise, <see langword="false"/>.</returns>
    /// <remarks>MUST NOT be used unless all input indexes are verified and trusted.</remarks>
    public static unsafe bool IsValid(byte* name, int start, ref int returnedEnd, bool notImplicitFile)
    {
        int end = returnedEnd;

        if (start == end)
            return false;

        // First segment could consist of only '_' or '-' but it cannot be all digits or empty
        bool validShortName = false;
        int i = start;
        for (; i < end; ++i)
        {
            if (name[i] == (byte)'/' || name[i] == (byte)'\\' || (notImplicitFile && (name[i] == (byte)':' || name[i] == (byte)'?' || name[i] == (byte)'#')))
            {
                end = i;
                break;
            }
            else if (name[i] == (byte)'.')
            {
                ++i;
                break;
            }

            if (Rune.DecodeFromUtf8(new ReadOnlySpan<byte>(name + i, end - i), out Rune currentRune, out int bytesConsumed) != OperationStatus.Done)
            {
                return false;
            }

            if (Rune.IsLetter(currentRune) || name[i] == (byte)'-' || name[i] == (byte)'_')
            {
                validShortName = true;
            }
            else if (!Utf8UriTools.IsAsciiDigit(name[i]))
            {
                return false;
            }

            // Skip over the multibyte element
            i += bytesConsumed - 1;
        }

        if (!validShortName)
            return false;

        // Subsequent segments must start with a letter or a digit
        for (; i < end; ++i)
        {
            if (Rune.DecodeFromUtf8(new ReadOnlySpan<byte>(name + i, end - i), out Rune currentRune, out int bytesConsumed) != OperationStatus.Done)
            {
                return false;
            }

            if (name[i] == (byte)'/' || name[i] == (byte)'\\' || (notImplicitFile && (name[i] == (byte)':' || name[i] == (byte)'?' || name[i] == (byte)'#')))
            {
                end = i;
                break;
            }
            else if (name[i] == (byte)'.')
            {
                if (!validShortName || ((i - 1) >= start && name[i - 1] == (byte)'.'))
                    return false;

                validShortName = false;
            }
            else if (name[i] == (byte)'-' || name[i] == (byte)'_')
            {
                if (!validShortName)
                    return false;
            }
            else if (Rune.IsLetter(currentRune) || Utf8UriTools.IsAsciiDigit(name[i]))
            {
                if (!validShortName)
                    validShortName = true;
            }
            else
                return false;
        }

        // last segment can end with the dot
        if (((i - 1) >= start && name[i - 1] == (byte)'.'))
            validShortName = true;

        if (!validShortName)
            return false;

        // caller must check for (end - start <= MaximumInternetNameLength)
        returnedEnd = end;
        return true;
    }
}