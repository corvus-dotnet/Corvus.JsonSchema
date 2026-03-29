// <copyright file="IdnMapping.Unicode.UTF8.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
// This file contains the IDN functions and implementation.
// This allows encoding of non-ASCII domain names in a "punycode" form,
// for example:
// \u5B89\u5BA4\u5948\u7F8E\u6075-with-SUPER-MONKEYS
// is encoded as:
// xn---with-SUPER-MONKEYS-pc58ag80a8qai00g7n9n
// Additional options are provided to allow unassigned IDN characters and
// to validate according to the Std3ASCII Rules (like DNS names).
// There are also rules regarding bidirectionality of text and the length
// of segments.
// For additional rules see also:
// RFC 3490 - Internationalizing Domain Names in Applications (IDNA)
// RFC 3491 - Nameprep: A Stringprep Profile for Internationalized Domain Names (IDN)
// RFC 3492 - Punycode: A Bootstring encoding of Unicode for Internationalized Domain Names in Applications (IDNA)
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Corvus.Globalization;

// IdnMapping class used to map names to Punycode
public sealed partial class IdnMapping
{
    private static ReadOnlySpan<byte> c_strAcePrefixUtf8 => "xn--"u8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetUnicode(ReadOnlySpan<byte> ascii, Span<byte> outputBuffer, out int written) =>
       GetUnicode(ascii, outputBuffer, 0, out written);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetUnicode(ReadOnlySpan<byte> ascii, Span<byte> outputBuffer, int index, out int written)
    {
        return GetUnicode(ascii, outputBuffer, index, ascii.Length - index, out written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable CA1822 // Mark members as static - public API
    public bool GetUnicode(ReadOnlySpan<byte> ascii, Span<byte> outputBuffer, int index, int count, out int written)
#pragma warning restore CA1822
    {
        if (index < 0)
        {
            written = 0;
            return false;
        }

        if (count < 0)
        {
            written = 0;
            return false;
        }

        if (index > ascii.Length)
        {
            written = 0;
            return false;
        }

        if (index > ascii.Length - count)
        {
            written = 0;
            return false;
        }

        if (count == 0)
        {
            written = 0;
            return false;
        }

        if (ascii[index + count - 1] == 0)
        {
            written = 0;
            return false;
        }

        return IdnMapping.GetUnicodeInvariant(ascii, outputBuffer, index, count, out written);
    }

    internal static bool EqualAcePrefix(ReadOnlySpan<byte> readOnlySpan)
    {
        Debug.Assert(readOnlySpan.Length >= c_strAcePrefixUtf8.Length, "[IdnMapping.EqualAcePrefix]Expected readOnlySpan to be at least as long as c_strAcePrefixUtf8.");
        return (readOnlySpan[0] == (byte)'x' || readOnlySpan[0] == (byte)'X') &&
               (readOnlySpan[1] == (byte)'n' || readOnlySpan[1] == (byte)'N') &&
               readOnlySpan[2] == (byte)'-' &&
               readOnlySpan[3] == (byte)'-';
    }

    private static bool ConvertFromUtf32AndInsert(Span<byte> outputBuffer, int index, int utf32, ref int written)
    {
        Debug.Assert(index >= 0 && index <= outputBuffer.Length, "[IdnMapping.ConvertFromUtf32AndInsert]Expected index to be within bounds of outputBuffer.");
        if (!Rune.TryCreate(utf32, out Rune rune))
        {
            written = 0;
            return false;
        }

        Span<byte> buffer = stackalloc byte[4]; // Max UTF8 bytes per rune
        int localWritten = rune.EncodeToUtf8(buffer);
        if (outputBuffer.Length < written + localWritten)
        {
            written = 0;
            return false;
        }

        // Copy up
        outputBuffer.Slice(index, written - index).CopyTo(outputBuffer.Slice(index + localWritten));

        // Insert in the space
        buffer.Slice(0, localWritten).CopyTo(outputBuffer.Slice(index));
        written += localWritten;
        return true;
    }

    private static bool DecodeDigit(byte cp, out int decoded)
    {
        if (IsAsciiDigit(cp))
        {
            decoded = cp - (byte)'0' + 26;
            return true;
        }

        // Two flavors for case differences
        if (IsAsciiLetterLower(cp))
        {
            decoded = cp - (byte)'a';
            return true;
        }

        if (IsAsciiLetterUpper(cp))
        {
            decoded = cp - (byte)'A';
            return true;
        }

        decoded = -1;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiDigit(byte v)
    {
        return IsBetween(v, (byte)'0', (byte)'9');
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterLower(byte v)
    {
        return IsBetween(v, (byte)'a', (byte)'z');
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterUpper(byte v)
    {
        return IsBetween(v, (byte)'A', (byte)'Z');
    }

    /// <summary>Indicates whether a character is within the specified inclusive range.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <param name="minInclusive">The lower bound, inclusive.</param>
    /// <param name="maxInclusive">The upper bound, inclusive.</param>
    /// <returns>true if <paramref name="c"/> is within the specified range; otherwise, false.</returns>
    /// <remarks>
    /// The method does not validate that <paramref name="maxInclusive"/> is greater than or equal
    /// to <paramref name="minInclusive"/>.  If <paramref name="maxInclusive"/> is less than
    /// <paramref name="minInclusive"/>, the behavior is undefined.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBetween(byte c, byte minInclusive, byte maxInclusive) =>
        (uint)(c - minInclusive) <= (uint)(maxInclusive - minInclusive);

    // Is it a dot?
    // are we U+002E (., full stop), U+3002 (ideographic full stop), U+FF0E (fullwidth full stop), or
    // U+FF61 (halfwidth ideographic full stop).
    // Note: IDNA Normalization gets rid of dots now, but testing for last dot is before normalization
    private static bool IsDot(ReadOnlySpan<byte> ascii)
    {
        if (ascii[0] == (byte)'.')
        {
            return true;
        }

        if (ascii.Length > 1)
        {
            Rune.DecodeLastFromUtf8(ascii, out Rune rune, out _);
            return rune.Value == 0x3002 || rune.Value == 0xFF0E || rune.Value == 0xFF61;
        }

        return false;
    }

    // Is it a dot?
    // are we U+002E (., full stop), U+3002 (ideographic full stop), U+FF0E (fullwidth full stop), or
    // U+FF61 (halfwidth ideographic full stop).
    // Note: IDNA Normalization gets rid of dots now, but testing for last dot is before normalization
    private static bool LastCharacterIsDot(ReadOnlySpan<byte> ascii)
    {
        if (ascii[^1] == (byte)'.')
        {
            return true;
        }

        if (Rune.DecodeLastFromUtf8(ascii.Slice(ascii.Length - 2), out Rune rune, out _) == System.Buffers.OperationStatus.Done)
        {
            return rune.Value == 0x3002 || rune.Value == 0xFF0E || rune.Value == 0xFF61;
        }

        return false;
    }

    // Is it a dot?
    // are we U+002E (., full stop), U+3002 (ideographic full stop), U+FF0E (fullwidth full stop), or
    // U+FF61 (halfwidth ideographic full stop).
    // Note: IDNA Normalization gets rid of dots now, but testing for last dot is before normalization
    private static (int Index, int RuneCount, int dotLength) FindDot(ReadOnlySpan<byte> utf8)
    {
        int runeCount = 0;
        for (int i = 0; i < utf8.Length; i++)
        {
            if (utf8[i] == (byte)'.')
            {
                return (i, runeCount, 1);
            }

            if (i < utf8.Length - 1)
            {
                if (Rune.DecodeFromUtf8(utf8.Slice(i), out Rune rune, out int read) == System.Buffers.OperationStatus.Done)
                {
                    if (rune.Value == 0x3002 || rune.Value == 0xFF0E || rune.Value == 0xFF61)
                    {
                        return (i, runeCount, read);
                    }

                    i += read - 1;
                }
            }

            runeCount++;
        }

        return (-1, runeCount, 1);
    }

    /// <summary>
    /// Gets the length of a UTF-8 encoded string in characters (not bytes).
    /// </summary>
    /// <param name="span">The UTF-8 encoded byte span.</param>
    /// <returns>The number of Unicode characters in the string.</returns>
    /// <exception cref="ArgumentException">Thrown when the span contains invalid UTF-8 sequences.</exception>
    private static int GetUtf8StringLength(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
        {
            return 0;
        }

        int length = 0;
        ReadOnlySpan<byte> currentSpan = span;
        do
        {
            OperationStatus status = Rune.DecodeFromUtf8(currentSpan, out _, out int bytesConsumed);
            Debug.Assert(status != OperationStatus.Done);

            currentSpan = currentSpan.Slice(bytesConsumed);
            length++;
        }
        while (currentSpan.Length > 0);

        return length;
    }

    private static bool PunycodeDecode(ReadOnlySpan<byte> ascii, Span<byte> outputBuffer, out int written)
    {
        written = 0;

        // 0 length strings aren't allowed
        if (ascii.Length == 0)
        {
            written = 0;
            return false;
        }

        // Throw if we're too long
        if (ascii.Length > c_defaultNameLimit - (LastCharacterIsDot(ascii) ? 0 : 1))
        {
            written = 0;
            return false;
        }

        // Dot searching
        int iNextDot = 0;
        int iAfterLastDot = 0;
        int iOutputAfterLastDot = 0;
        while (iNextDot < ascii.Length)
        {
            // Find end of this segment
            (iNextDot, int runeCount, int dotLength) = FindDot(ascii.Slice(iAfterLastDot));
            if (iNextDot < 0 || iNextDot > ascii.Length)
            {
                iNextDot = ascii.Length;
            }
            else
            {
                iNextDot += iAfterLastDot;
            }

            // Only allowed to have empty . section at end (www.microsoft.com.)
            if (iNextDot == iAfterLastDot)
            {
                // This form DOES NOT support an FQDN, which supports a trailing dot on the hostname.
                written = 0;
                return false;

                // (unlike this code below)

                //// if (iNextDot != ascii.Length)
                //// {
                //// written = 0;

                //// return false;

                //// }

                ////// Last dot, stop
                //// break;
            }

            // In either case it can't be bigger than segment size
            if (runeCount > c_labelLimit)
            {
                written = 0;
                return false;
            }

            // See if this section's ASCII or ACE
            if (ascii.Length < c_strAcePrefixUtf8.Length + iAfterLastDot ||
                !EqualAcePrefix(ascii.Slice(iAfterLastDot, c_strAcePrefixUtf8.Length)))
            {
                // Its ASCII, copy it
                int length = iNextDot - iAfterLastDot;
                if (outputBuffer.Length < written + length)
                {
                    // Not enough space in output buffer
                    written = 0;
                    return false;
                }

                ascii.Slice(iAfterLastDot, length).CopyTo(outputBuffer.Slice(written));
                written += length;
            }
            else
            {
                // Not ASCII, bump up iAfterLastDot to be after ACE Prefix
                iAfterLastDot += c_strAcePrefixUtf8.Length;

                // Get number of basic code points (where delimiter is)
                // numBasicCodePoints < 0 if there're no basic code points
                int iTemp = ascii.Slice(0, iNextDot - 1).LastIndexOf((byte)'-');

                // Trailing - not allowed
                if (iTemp == iNextDot - 1)
                {
                    written = 0;
                    return false;
                }

                int numBasicCodePoints;
                if (iTemp <= iAfterLastDot)
                    numBasicCodePoints = 0;
                else
                {
                    numBasicCodePoints = iTemp - iAfterLastDot;

                    // Copy all the basic code points, making sure they're all in the allowed range,
                    // and losing the casing for all of them.
                    for (int copyAscii = iAfterLastDot; copyAscii < iAfterLastDot + numBasicCodePoints; copyAscii++)
                    {
                        // Make sure we don't allow unicode in the ascii part
                        if (ascii[copyAscii] > 0x7f)
                        {
                            written = 0;
                            return false;
                        }

                        if (outputBuffer.Length <= written)
                        {
                            // Not enough space in output buffer
                            written = 0;
                            return false;
                        }

                        // When appending make sure they get lower cased
                        outputBuffer[written++] = (byte)(IsAsciiLetterUpper(ascii[copyAscii]) ? ascii[copyAscii] - 'A' + 'a' : ascii[copyAscii]);
                    }
                }

                // Get ready for main loop.  Start at beginning if we didn't have any
                // basic code points, otherwise start after the -.
                // asciiIndex will be next character to read from ascii
                int asciiIndex = iAfterLastDot + (numBasicCodePoints > 0 ? numBasicCodePoints + 1 : 0);

                runeCount = numBasicCodePoints;

                // initialize our state
                int n = c_initialN;
                int bias = c_initialBias;
                int i = 0;

                int w, k;

                // no Supplementary characters yet
                int surrogatePairLength = 0;

                // Main loop, read rest of ascii
                while (asciiIndex < iNextDot)
                {
                    /* Decode a generalized variable-length integer into delta,  */
                    /* which gets added to i.  The overflow checking is easier   */
                    /* if we increase i as we go, then subtract off its starting */
                    /* value at the end to obtain delta.                         */
                    int oldi = i;

                    for (w = 1, k = c_punycodeBase; ; k += c_punycodeBase)
                    {
                        // Check to make sure we aren't overrunning our ascii string
                        if (asciiIndex >= iNextDot)
                        {
                            written = 0;
                            return false;
                        }

                        // decode the digit from the next byte
                        if (!DecodeDigit(ascii[asciiIndex++], out int digit))
                        {
                            written = 0;
                            return false;
                        }

                        Debug.Assert(w > 0, "[IdnMapping.punycode_decode]Expected w > 0");
                        if (digit > (c_maxint - i) / w)
                        {
                            written = 0;
                            return false;
                        }

                        i += (int)(digit * w);
                        int t = k <= bias ? c_tmin : k >= bias + c_tmax ? c_tmax : k - bias;
                        if (digit < t)
                            break;
                        Debug.Assert(c_punycodeBase != t, "[IdnMapping.punycode_decode]Expected t != c_punycodeBase (36)");
                        if (w > c_maxint / (c_punycodeBase - t))
                        {
                            written = 0;
                            return false;
                        }

                        w *= (c_punycodeBase - t);
                    }

                    bias = Adapt(i - oldi, (written - iOutputAfterLastDot - surrogatePairLength) + 1, oldi == 0);

                    /* i was supposed to wrap around from output.Length to 0,   */
                    /* incrementing n each time, so we'll fix that now: */
                    Debug.Assert((written - iOutputAfterLastDot - surrogatePairLength) + 1 > 0,
                        "[IdnMapping.punycode_decode]Expected to have added > 0 characters this segment");
                    if (i / ((written - iOutputAfterLastDot - surrogatePairLength) + 1) > c_maxint - n)
                    {
                        written = 0;
                        return false;
                    }

                    n += (int)(i / (written - iOutputAfterLastDot - surrogatePairLength + 1));
                    i %= (written - iOutputAfterLastDot - surrogatePairLength + 1);

                    // Make sure n is legal
                    if (n < 0 || n > 0x10ffff || (n >= 0xD800 && n <= 0xDFFF))
                    {
                        written = 0;
                        return false;
                    }

                    // insert n at position i of the output:  Really tricky if we have surrogates
                    int iUseInsertLocation;

                    // If we have supplementary characters
                    if (surrogatePairLength > 0)
                    {
                        // Hard way, we have supplementary characters
                        int iCount;
                        for (iCount = i, iUseInsertLocation = iOutputAfterLastDot; iCount > 0; iCount--)
                        {
                            // If its a surrogate, we have to go one more
                            // (We are guaranteed to be inside the outBuffer range so we don't
                            // need to test the index here);
                            Rune.DecodeFromUtf8(outputBuffer.Slice(iUseInsertLocation), out _, out int bytesConsumed);
                            iUseInsertLocation += bytesConsumed;
                        }
                    }
                    else
                    {
                        // No Supplementary bytes yet, just add i
                        iUseInsertLocation = iOutputAfterLastDot + i;
                    }

                    int prevWritten = written;
                    if (!ConvertFromUtf32AndInsert(outputBuffer, iUseInsertLocation, n, ref written))
                    {
                        return false;
                    }

                    // If it was a surrogate increment our counter
                    surrogatePairLength += (written - prevWritten) - 1;

                    // Index gets updated
                    i++;
                    runeCount++;
                }

                // Do BIDI testing
                bool bRightToLeft = false;

                // Check for RTL.  If right-to-left, then 1st & last bytes must be RTL
                StrongBidiCategory eBidi = CharUnicodeInfo.GetBidiCategory(outputBuffer.Slice(0, written), iOutputAfterLastDot, out _);
                if (eBidi == StrongBidiCategory.StrongRightToLeft)
                {
                    // It has to be right to left.
                    bRightToLeft = true;
                }

                // Check the rest of them to make sure RTL/LTR is consistent
                for (int iTest = iOutputAfterLastDot; iTest < written; iTest++)
                {
                    // Check to see if its LTR
                    eBidi = CharUnicodeInfo.GetBidiCategory(outputBuffer, iTest, out int localConsumed);
                    if ((bRightToLeft && eBidi == StrongBidiCategory.StrongLeftToRight) ||
                        (!bRightToLeft && eBidi == StrongBidiCategory.StrongRightToLeft))
                    {
                        written = 0;
                        return false;
                    }

                    // Skip to
                    iTest += localConsumed - 1;
                }

                // Its also a requirement that the last one be RTL if 1st is RTL
                if (bRightToLeft && eBidi != StrongBidiCategory.StrongRightToLeft)
                {
                    // Oops, last wasn't RTL, last should be RTL if first is RTL
                    written = 0;
                    return false;
                }
            }

            // See if this label was too long
            if (runeCount > c_labelLimit)
            {
                written = 0;
                return false;
            }

            // Done with this segment, add dot if necessary
            if (iNextDot != ascii.Length)
                outputBuffer[written++] = (byte)'.';

            iAfterLastDot = iNextDot + dotLength;
            iOutputAfterLastDot = written;
        }

        // Throw if we're too long
        if (written > c_defaultNameLimit - (IsDot(outputBuffer.Slice(written - 1)) ? 0 : 1))
        {
            written = 0;
            return false;
        }

        // Return success!
        return true;
    }

    // DecodeDigit(cp) returns the numeric value of a basic code */
    // point (for use in representing integers) in the range 0 to */
    // c_punycodeBase-1, or <0 if cp is does not represent a value. */
    private static bool ValidateStd3(byte c, bool bNextToDot)
    {
        // Check for illegal characters
        return !(c <= (byte)',' || c == (byte)'/' || (c >= (byte)':' && c <= (byte)'@') ||      // Lots of characters not allowed
            (c >= (byte)'[' && c <= (byte)'`') || (c >= (byte)'{' && c <= (byte)0x7F) ||
            (c == (byte)'-' && bNextToDot));
    }

    private static bool GetUnicodeInvariant(ReadOnlySpan<byte> ascii, Span<byte> outputBuffer, int index, int count, out int written)
    {
        if (index > 0 || count < ascii.Length)
        {
            // We're only using part of the string
            ascii = ascii.Slice(index, count);
        }

        // Convert Punycode to Unicode
        if (!PunycodeDecode(ascii, outputBuffer, out written))
        {
            written = 0;
            return false;
        }

        // We should not need to assert the round trip rule here

        //// GetAscii(strUnicode)
        ////// Output name MUST obey IDNA rules & round trip (casing differences are allowed)
        //// if (!ascii.Equals(, StringComparison.OrdinalIgnoreCase))
        //// throw new ArgumentException(SR.Argument_IdnIllegalName, nameof(ascii));

        return true;
    }

    /* PunycodeDecode() converts Punycode to Unicode.  The input is  */
    /* represented as an array of ASCII code points, and the output   */
    /* will be represented as an array of Unicode code points.  The   */
    /* input_length is the number of code points in the input.  The   */
    /* output_length is an in/out argument: the caller passes in      */
    /* the maximum number of code points that it can receive, and     */
    /* on successful return it will contain the actual number of      */
    /* code points output.  The case_flags array needs room for at    */
    /* least output_length values, or it can be a null pointer if the */
    /* case information is not needed.  A nonzero flag suggests that  */
    /* the corresponding Unicode character be forced to uppercase     */
    /* by the caller (if possible), while zero suggests that it be    */
    /* forced to lowercase (if possible).  ASCII code points are      */
    /* output already in the proper case, but their flags will be set */
    /* appropriately so that applying the flags would be harmless.    */
}