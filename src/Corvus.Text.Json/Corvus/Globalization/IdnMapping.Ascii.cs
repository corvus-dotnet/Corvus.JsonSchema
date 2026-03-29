// <copyright file="IdnMapping.Ascii.cs" company="Endjin Limited">
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Corvus.Globalization;

// IdnMapping class used to map names to Punycode
public sealed partial class IdnMapping
{
    public static IdnMapping Default { get; } = new IdnMapping { AllowUnassigned = true, UseStd3AsciiRules = true };

    private bool _allowUnassigned;

    private bool _useStd3AsciiRules;

    public IdnMapping()
    {
    }

    public bool AllowUnassigned
    {
        get => _allowUnassigned;
        set => _allowUnassigned = value;
    }

    public bool UseStd3AsciiRules
    {
        get => _useStd3AsciiRules;
        set => _useStd3AsciiRules = value;
    }

    // Gets ASCII (Punycode) version of the string
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetAscii(ReadOnlySpan<char> unicode, Span<char> outputBuffer, out int written) =>
        GetAscii(unicode, outputBuffer, 0, out written);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetAscii(ReadOnlySpan<char> unicode, Span<char> outputBuffer, int index, out int written)
    {
        return GetAscii(unicode, outputBuffer, index, unicode.Length - index, out written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetAscii(ReadOnlySpan<char> unicode, Span<char> outputBuffer, int index, int count, out int written)
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

        if (index > unicode.Length)
        {
            written = 0;
            return false;
        }

        if (index > unicode.Length - count)
        {
            written = 0;
            return false;
        }

        if (count == 0)
        {
            written = 0;
            return false;
        }

        if (unicode[index + count - 1] == 0)
        {
            written = 0;
            return false;
        }

        return GetAsciiInvariant(unicode, outputBuffer, index, count, out written);
    }

    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is IdnMapping that &&
        _allowUnassigned == that._allowUnassigned &&
        _useStd3AsciiRules == that._useStd3AsciiRules;

    public override int GetHashCode() =>
        (_allowUnassigned ? 100 : 200) + (_useStd3AsciiRules ? 1000 : 2000);

    // Invariant implementation
    private const char c_delimiter = '-';

    private static ReadOnlySpan<char> c_strAcePrefix => "xn--";

    private const int c_labelLimit = 63;          // Not including dots
    private const int c_defaultNameLimit = 255;   // Including dots
    private const int c_initialN = 0x80;

    private const int c_maxint = 0x7ffffff;

    private const int c_initialBias = 72;

    private const int c_punycodeBase = 36;

    private const int c_tmin = 1;

    private const int c_tmax = 26;

    private const int c_skew = 38;

    private const int c_damp = 700;

    private bool GetAsciiInvariant(ReadOnlySpan<char> unicode, Span<char> outputBuffer, int index, int count, out int written)
    {
        if (index > 0 || count < unicode.Length)
        {
            unicode = unicode.Slice(index, count);
        }

        // Check for ASCII only string, which will be unchanged
        if (ValidateStd3AndAscii(unicode, UseStd3AsciiRules, true))
        {
            bool result = unicode.TryCopyTo(outputBuffer);
            written = result ? unicode.Length : 0;
            return result;
        }

        // Cannot be null terminated (normalization won't help us with this one, and
        // may have returned false before checking the whole string above)
        Debug.Assert(count >= 1, "[IdnMapping.GetAscii] Expected 0 length strings to fail before now.");
        if (unicode[^1] <= 0x1f)
        {
            written = 0;
            return false;
        }

        // May need to check Std3 rules again for non-ascii
        if (UseStd3AsciiRules)
        {
            if (!ValidateStd3AndAscii(unicode, true, false))
            {
                written = 0;
                return false;
            }
        }

        // Go ahead and encode it
        return PunycodeEncode(unicode, outputBuffer, out written);
    }

    // See if we're only ASCII
    private static bool ValidateStd3AndAscii(ReadOnlySpan<char> unicode, bool bUseStd3, bool bCheckAscii)
    {
        // If its empty, then its too small
        if (unicode.Length == 0)
        {
            return false;
        }

        int iLastDot = -1;

        // Loop the whole string
        for (int i = 0; i < unicode.Length; i++)
        {
            // Aren't allowing control chars (or 7f, but idn tables catch that, they don't catch \0 at end though)
            if (unicode[i] <= 0x1f)
            {
                return false;
            }

            // If its Unicode or a control character, return false (non-ascii)
            if (bCheckAscii && unicode[i] >= 0x7f)
                return false;

            // Check for dots
            if (IsDot(unicode[i]))
            {
                // Can't have 2 dots in a row
                if (i == iLastDot + 1)
                    return false;

                // If its too far between dots then fail
                if (i - iLastDot > c_labelLimit + 1)
                    return false;

                // If validating Std3, then char before dot can't be - char
                if (bUseStd3 && i > 0)
                {
                    if (!ValidateStd3(unicode[i - 1], true))
                    {
                        return false;
                    }
                }

                // Remember where the last dot is
                iLastDot = i;
                continue;
            }

            // If necessary, make sure its a valid std3 character
            if (bUseStd3)
            {
                if (!ValidateStd3(unicode[i], i == iLastDot + 1))
                {
                    return false;
                }
            }
        }

        // If we never had a dot, then we need to be shorter than the label limit
        if (iLastDot == -1 && unicode.Length > c_labelLimit)
            return false;

        // Need to validate entire string length, 1 shorter if last char wasn't a dot
        if (unicode.Length > c_defaultNameLimit - (IsDot(unicode[^1]) ? 0 : 1))
            return false;

        // If last char wasn't a dot we need to check for trailing -
        if (bUseStd3 && !IsDot(unicode[^1]))
        {
            if (!ValidateStd3(unicode[^1], true))
            {
                return false;
            }
        }

        return true;
    }

    /* PunycodeEncode() converts Unicode to Punycode.  The input     */
    /* is represented as an array of Unicode code points (not code    */
    /* units; surrogate pairs are not allowed), and the output        */
    /* will be represented as an array of ASCII code points.  The     */
    /* output string is *not* null-terminated; it will contain        */
    /* zeros if and only if the input contains zeros.  (Of course     */
    /* the caller can leave room for a terminator and add one if      */
    /* needed.)  The input_length is the number of code points in     */
    /* the input.  The output_length is an in/out argument: the       */
    /* caller passes in the maximum number of code points that it     */

    /* can receive, and on successful return it will contain the      */
    /* number of code points actually output.  The case_flags array   */
    /* holds input_length boolean values, where nonzero suggests that */
    /* the corresponding Unicode character be forced to uppercase     */
    /* after being decoded (if possible), and zero suggests that      */
    /* it be forced to lowercase (if possible).  ASCII code points    */
    /* are encoded literally, except that ASCII letters are forced    */
    /* to uppercase or lowercase according to the corresponding       */
    /* uppercase flags.  If case_flags is a null pointer then ASCII   */
    /* letters are left as they are, and other code points are        */
    /* treated as if their uppercase flags were zero.  The return     */
    /* value can be any of the punycode_status values defined above   */
    /* except punycode_bad_input; if not punycode_success, then       */
    /* output_size and output might contain garbage.                  */

    private static bool PunycodeEncode(ReadOnlySpan<char> unicode, Span<char> buffer, out int written)
    {
        // 0 length strings aren't allowed
        if (unicode.Length == 0)
        {
            written = 0;
            return false;
        }

        int iNextDot = 0;
        int iAfterLastDot = 0;
        int iOutputAfterLastDot = 0;
        int bufferIndex = 0;

        // Find the next dot
        while (iNextDot < unicode.Length)
        {
            // Legal "dot" separators (i.e: . in www.microsoft.com)
            const string DotSeparators = ".\u3002\uFF0E\uFF61";

            // Find end of this segment
            iNextDot = unicode.Slice(iAfterLastDot).IndexOfAny(DotSeparators);
            iNextDot = iNextDot < 0 ? unicode.Length : iNextDot + iAfterLastDot;

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

            // We'll need an Ace prefix
            if (!c_strAcePrefix.TryCopyTo(buffer.Slice(bufferIndex)))
            {
                written = 0;
                return false;
            }
            else
            {
                bufferIndex += c_strAcePrefix.Length;
            }

            // Everything resets every segment.
            bool bRightToLeft = false;

            // Check for RTL.  If right-to-left, then 1st & last chars must be RTL
            StrongBidiCategory eBidi = CharUnicodeInfo.GetBidiCategory(unicode, iAfterLastDot);
            if (eBidi == StrongBidiCategory.StrongRightToLeft)
            {
                // It has to be right to left.
                bRightToLeft = true;

                // Check last char
                int iTest = iNextDot - 1;
                if (char.IsLowSurrogate(unicode[iTest]))
                {
                    iTest--;
                }

                eBidi = CharUnicodeInfo.GetBidiCategory(unicode, iTest);
                if (eBidi != StrongBidiCategory.StrongRightToLeft)
                {
                    // Oops, last wasn't RTL, last should be RTL if first is RTL
                    written = 0;
                    return false;
                }
            }

            // Handle the basic code points
            int basicCount;
            int numProcessed = 0;           // Num code points that have been processed so far (this segment)
            for (basicCount = iAfterLastDot; basicCount < iNextDot; basicCount++)
            {
                // Can't be lonely surrogate because it would've given up in normalization
                Debug.Assert(!char.IsLowSurrogate(unicode[basicCount]), "[IdnMapping.punycode_encode]Unexpected low surrogate");

                // Double check our bidi rules
                StrongBidiCategory testBidi = CharUnicodeInfo.GetBidiCategory(unicode, basicCount);

                // If we're RTL, we can't have LTR chars
                if (bRightToLeft && testBidi == StrongBidiCategory.StrongLeftToRight)
                {
                    // Oops, error
                    written = 0;
                    return false;
                }

                // If we're not RTL we can't have RTL chars
                if (!bRightToLeft && testBidi == StrongBidiCategory.StrongRightToLeft)
                {
                    // Oops, error
                    written = 0;
                    return false;
                }

                // If its basic then add it
                if (Basic(unicode[basicCount]))
                {
                    if (bufferIndex >= buffer.Length)
                    {
                        written = 0;
                        return false;
                    }

                    buffer[bufferIndex++] = EncodeBasic(unicode[basicCount]);
                    numProcessed++;
                }

                // If its a surrogate, skip the next since our bidi category tester doesn't handle it.
                else if (IsSurrogatePair(unicode.Slice(basicCount)))
                    basicCount++;
            }

            int numBasicCodePoints = numProcessed;     // number of basic code points

            // Stop if we ONLY had basic code points
            if (numBasicCodePoints == iNextDot - iAfterLastDot)
            {
                // Get rid of xn-- and this segment is done
                int start = iOutputAfterLastDot + c_strAcePrefix.Length;
                int length = bufferIndex - start;
                buffer.Slice(start, length).CopyTo(buffer.Slice(iOutputAfterLastDot));
                bufferIndex -= length;
            }
            else
            {
                // If it has some non-basic code points the input cannot start with xn--
                if (unicode.Slice(iAfterLastDot).StartsWith(c_strAcePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    written = 0;
                    return false;
                }

                // Need to do ACE encoding
                int numSurrogatePairs = 0;            // number of surrogate pairs so far

                // Add a delimiter (-) if we had any basic code points (between basic and encoded pieces)
                if (numBasicCodePoints > 0)
                {
                    if (bufferIndex >= buffer.Length)
                    {
                        written = 0;
                        return false;
                    }

                    buffer[bufferIndex++] = c_delimiter;
                }

                // Initialize the state
                int n = c_initialN;
                int delta = 0;
                int bias = c_initialBias;

                // Main loop
                while (numProcessed < (iNextDot - iAfterLastDot))
                {
                    /* All non-basic code points < n have been     */
                    /* handled already.  Find the next larger one: */
                    int j;
                    int m;
                    int test;
                    for (m = c_maxint, j = iAfterLastDot;
                         j < iNextDot;
                         j += IsSupplementary(test) ? 2 : 1)
                    {
                        if (!ConvertToUtf32(unicode.Slice(j), out test))
                        {
                            written = 0;
                            return false;
                        }

                        if (test >= n && test < m) m = test;
                    }

                    /* Increase delta enough to advance the decoder's    */
                    /* <n,i> state to <m,0>, but guard against overflow: */
                    delta += (int)((m - n) * ((numProcessed - numSurrogatePairs) + 1));
                    Debug.Assert(delta > 0, "[IdnMapping.cs]1 punycode_encode - delta overflowed int");
                    n = m;

                    for (j = iAfterLastDot; j < iNextDot; j += IsSupplementary(test) ? 2 : 1)
                    {
                        // Make sure we're aware of surrogates
                        if (!ConvertToUtf32(unicode.Slice(j), out test))
                        {
                            written = 0;
                            return false;
                        }

                        // Adjust for character position (only the chars in our string already, some
                        // haven't been processed.
                        if (test < n)
                        {
                            delta++;
                            Debug.Assert(delta > 0, "[IdnMapping.cs]2 punycode_encode - delta overflowed int");
                        }

                        if (test == n)
                        {
                            // Represent delta as a generalized variable-length integer:
                            int q, k;
                            for (q = delta, k = c_punycodeBase; ; k += c_punycodeBase)
                            {
                                int t = k <= bias ? c_tmin : k >= bias + c_tmax ? c_tmax : k - bias;
                                if (q < t) break;
                                Debug.Assert(c_punycodeBase != t, "[IdnMapping.punycode_encode]Expected c_punycodeBase (36) to be != t");

                                if (bufferIndex >= buffer.Length)
                                {
                                    written = 0;
                                    return false;
                                }

                                buffer[bufferIndex++] = EncodeDigit(t + (q - t) % (c_punycodeBase - t));
                                q = (q - t) / (c_punycodeBase - t);
                            }

                            if (bufferIndex >= buffer.Length)
                            {
                                written = 0;
                                return false;
                            }

                            buffer[bufferIndex++] = EncodeDigit(q);
                            bias = Adapt(delta, (numProcessed - numSurrogatePairs) + 1, numProcessed == numBasicCodePoints);
                            delta = 0;
                            numProcessed++;

                            if (IsSupplementary(m))
                            {
                                numProcessed++;
                                numSurrogatePairs++;
                            }
                        }
                    }

                    ++delta;
                    ++n;
                    Debug.Assert(delta > 0, "[IdnMapping.cs]3 punycode_encode - delta overflowed int");
                }
            }

            // Make sure its not too big
            if (bufferIndex - iOutputAfterLastDot > c_labelLimit)
            {
                written = 0;
                return false;
            }

            // Done with this segment, add dot if necessary
            if (iNextDot != unicode.Length)
            {
                if (bufferIndex >= buffer.Length)
                {
                    written = 0;
                    return false;
                }

                buffer[bufferIndex++] = '.';
            }

            iAfterLastDot = iNextDot + 1;
            iOutputAfterLastDot = bufferIndex;
        }

        // Give up if we're too long
        if (bufferIndex > c_defaultNameLimit - (IsDot(unicode[^1]) ? 0 : 1))
        {
            written = 0;
            return false;
        }

        // Return our output string
        written = bufferIndex;
        return true;
    }

    private static bool IsSurrogatePair(ReadOnlySpan<char> s)
    {
        if (1 < s.Length)
        {
            return char.IsSurrogatePair(s[0], s[1]);
        }

        return false;
    }

    private static bool ConvertToUtf32(ReadOnlySpan<char> s, out int utf32)
    {
        // Check if the character at index is a high surrogate.
        int temp1 = s[0] - CharUnicodeInfo.HIGH_SURROGATE_START;
        if ((uint)temp1 <= 0x7ff)
        {
            // Found a surrogate char.
            if (temp1 <= 0x3ff)
            {
                // Found a high surrogate.
                if (1 < s.Length)
                {
                    int temp2 = s[1] - CharUnicodeInfo.LOW_SURROGATE_START;
                    if ((uint)temp2 <= 0x3ff)
                    {
                        // Found a low surrogate.
                        utf32 = (temp1 * 0x400) + temp2 + CharUnicodeInfo.UNICODE_PLANE01_START;
                        return true;
                    }
                }
            }

            utf32 = 0;
            return false;
        }

        // Not a high-surrogate or low-surrogate. Generate the UTF32 value for the BMP characters.
        utf32 = s[0];
        return true;
    }

    // Is it a dot?
    // are we U+002E (., full stop), U+3002 (ideographic full stop), U+FF0E (fullwidth full stop), or
    // U+FF61 (halfwidth ideographic full stop).
    // Note: IDNA Normalization gets rid of dots now, but testing for last dot is before normalization
    internal static bool IsDot(char c) =>
        c == '.' || c == '\u3002' || c == '\uFF0E' || c == '\uFF61';

    internal static int FindDot(ReadOnlySpan<char> value)
    {
        return value.IndexOfAny(".\u3002\uFF0E\uFF61".AsSpan());
    }

    private static bool IsSupplementary(int cTest) =>
        cTest >= 0x10000;

    private static bool Basic(uint cp) =>

        // Is it in ASCII range?
        cp < 0x80;

    // Validate Std3 rules for a character
    private static bool ValidateStd3(char c, bool bNextToDot)
    {
        // Check for illegal characters
        return !(c <= ',' || c == '/' || (c >= ':' && c <= '@') ||      // Lots of characters not allowed
            (c >= '[' && c <= '`') || (c >= '{' && c <= (char)0x7F) ||
            (c == '-' && bNextToDot));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterUpper(char v)
    {
#if NET
        return char.IsAsciiLetterUpper(v);
#else
        return IsBetween(v, 'A', 'Z');
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiDigit(char v)
    {
#if NET
        return char.IsAsciiDigit(v);
#else
        return IsBetween(v, '0', '9');
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetterLower(char v)
    {
#if NET
        return char.IsAsciiLetterLower(v);
#else
        return IsBetween(v, 'a', 'z');
#endif
    }

#if !NET
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
    private static bool IsBetween(char c, char minInclusive, char maxInclusive) =>
        (uint)(c - minInclusive) <= (uint)(maxInclusive - minInclusive);
#endif

    private static int Adapt(int delta, int numpoints, bool firsttime)
    {
        uint k;

        delta = firsttime ? delta / c_damp : delta / 2;
        Debug.Assert(numpoints != 0, "[IdnMapping.adapt]Expected non-zero numpoints.");
        delta += delta / numpoints;

        for (k = 0; delta > ((c_punycodeBase - c_tmin) * c_tmax) / 2; k += c_punycodeBase)
        {
            delta /= c_punycodeBase - c_tmin;
        }

        Debug.Assert(delta + c_skew != 0, "[IdnMapping.adapt]Expected non-zero delta+skew.");
        return (int)(k + (c_punycodeBase - c_tmin + 1) * delta / (delta + c_skew));
    }

    /* EncodeBasic(bcp,flag) forces a basic code point to lowercase */
    /* if flag is false, uppercase if flag is true, and returns    */
    /* the resulting code point.  The code point is unchanged if it  */
    /* is caseless.  The behavior is undefined if bcp is not a basic */
    /* code point.                                                   */

    private static char EncodeBasic(char bcp)
    {
        if (IsAsciiLetterUpper(bcp))
            bcp += (char)('a' - 'A');

        return bcp;
    }

    /* EncodeDigit(d,flag) returns the basic code point whose value      */
    /* (when used for representing integers) is d, which needs to be in   */
    /* the range 0 to punycodeBase-1.  The lowercase form is used unless flag is  */
    /* true, in which case the uppercase form is used. */

    private static char EncodeDigit(int d)
    {
        Debug.Assert(d >= 0 && d < c_punycodeBase, "[IdnMapping.encode_digit]Expected 0 <= d < punycodeBase");

        // 26-35 map to ASCII 0-9
        if (d > 25) return (char)(d - 26 + '0');

        // 0-25 map to a-z or A-Z
        return (char)(d + 'a');
    }
}