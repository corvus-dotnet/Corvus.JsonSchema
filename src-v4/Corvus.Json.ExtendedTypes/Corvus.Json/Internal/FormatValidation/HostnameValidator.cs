// <copyright file="HostnameValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Text;
#else
using Rune = Corvus.Json.Rune;
#endif
using Corvus.Globalization;

namespace Corvus.Json.Internal;

/// <summary>
/// RFC 5890/5892-compliant hostname and IDN hostname validation over UTF-8 spans.
/// Ported from V5's JsonSchemaEvaluation.String.cs.
/// </summary>
internal static class HostnameValidator
{
    private static ReadOnlySpan<int> DisallowedIdn =>
            [0x0640, 0x07FA, 0x302E, 0x302F,
        0x3031, 0x3032, 0x3033, 0x3034,
        0x3035, 0x303B];

    private static ReadOnlySpan<int> ViramaTable =>
            [0x094D, 0x09CD, 0x0A4D, 0x0ACD,
        0x0B4D, 0x0BCD, 0x0C4D, 0x0CCD,
        0x0D3B, 0x0D3C, 0x0D4D, 0x0DCA,
        0x0E3A, 0x0EBA, 0x0F84, 0x1039,
        0x103A, 0x1714, 0x1715, 0x1734,
        0x17D2, 0x1A60, 0x1B44, 0x1BAA,
        0x1BAB, 0x1BF2, 0x1BF3, 0x2D7F,
        0xA806, 0xA82C, 0xA8C4, 0xA953,
        0xA9C0, 0xAAF6, 0xABED, 0x10A3F,
        0x11046, 0x11070, 0x1107F, 0x110B9,
        0x11133, 0x11134, 0x111C0, 0x11235,
        0x112EA, 0x1134D, 0x11442, 0x114C2,
        0x115BF, 0x1163F, 0x116B6, 0x1172B,
        0x11839, 0x1193D, 0x1193E, 0x119E0,
        0x11A34, 0x11A47, 0x11A99, 0x11C3F,
        0x11D44, 0x11D45, 0x11D97];

    /// <summary>
    /// Validates an ASCII hostname per RFC 952/1123.
    /// </summary>
    internal static bool MatchHostname(ReadOnlySpan<byte> value)
    {
        if (value.Length > 253)
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[256];
        int i = 0;
        int characterCount = 0;
        byte lastAscii = 0;
        bool decodePunicode = false;
        while (i < value.Length)
        {
            if (value[i] > 0x7F)
            {
                return false;
            }

            if (lastAscii == (byte)'-' && value[i] == (byte)'-')
            {
                if (characterCount != 3 ||
                    !((value[i - 3] == (byte)'x' || value[i - 3] == (byte)'X') &&
                      (value[i - 2] == (byte)'n' || value[i - 2] == (byte)'N')))
                {
                    return false;
                }

                decodePunicode = true;
                break;
            }

            lastAscii = value[i];

            if (lastAscii == (byte)'.')
            {
                if (characterCount > 63)
                {
                    return false;
                }

                characterCount = 0;
                i++;
                continue;
            }

            if (!char.IsLetterOrDigit((char)lastAscii) && !(characterCount != 0 && lastAscii == (byte)'-'))
            {
                return false;
            }

            characterCount++;
            i++;
        }

        if (decodePunicode)
        {
            if (!IdnMapping.Default.GetUnicode(value, decoded, out int written))
            {
                return false;
            }

            scoped ReadOnlySpan<byte> segment = decoded.Slice(0, written);

            return MatchDecodedHostname(segment);
        }

        if (characterCount == 0 || characterCount > 63)
        {
            return false;
        }

        if (lastAscii == '-' || lastAscii == '.')
        {
            return false;
        }

        if (value[0] == '.')
        {
            return false;
        }

        if (value.Length > 3 &&
            value[2] == '-' &&
            value[3] == '-')
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates an internationalized domain name (IDN) hostname per RFC 5890/5892.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIdnHostname(ReadOnlySpan<byte> value)
    {
        if (value.Length > 254)
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[256];

        try
        {
            if (!IdnMapping.Default.GetUnicode(value, decoded, out int written))
            {
                return false;
            }

            scoped ReadOnlySpan<byte> segment = decoded.Slice(0, written);

            return MatchDecodedHostname(segment);
        }
        catch (ArgumentException)
        {
            // Invalid IDN input — treat as not matching
            return false;
        }
    }

    /// <summary>
    /// Validates a decoded (Unicode) hostname per RFC 5892 PVALID/DISALLOWED classification.
    /// </summary>
    internal static bool MatchDecodedHostname(ReadOnlySpan<byte> value)
    {
        bool wasLastDot = false;
        int i = 0;
        Rune previousRune = default;
        bool hasHiraganaKatakanaOrHan = false;
        bool hasKatakankaMiddleDot = false;
        bool hasArabicIndicDigits = false;
        bool hasExtendedArabicIndicDigits = false;
        int runeCount = 0;
        while (i < value.Length)
        {
            runeCount++;

            byte byteValue = value[i];
            Rune.DecodeFromUtf8(value.Slice(i), out Rune rune, out int bytesConsumed);
            int runeValue = (int)rune.Value;

            if (i == 0)
            {
                System.Globalization.UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                if (category == System.Globalization.UnicodeCategory.SpacingCombiningMark ||
                    category == System.Globalization.UnicodeCategory.EnclosingMark ||
                    category == System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    return false;
                }
            }

            i += bytesConsumed;

            hasHiraganaKatakanaOrHan |= IsHiraganaKatakanaOrHanNotMiddleDot(runeValue);
            hasArabicIndicDigits |= IsArabicIndicDigit(runeValue);
            hasExtendedArabicIndicDigits |= IsExtendedArabicIndicDigit(runeValue);

            if (wasLastDot)
            {
                if (!Rune.IsLetter(rune))
                {
                    return false;
                }
            }

            if (DisallowedIdn.IndexOf(runeValue) >= 0)
            {
                return false;
            }

            if (!Rune.IsLetterOrDigit(rune))
            {
                if (byteValue == (byte)'.' || runeValue == 0x3002 || runeValue == 0xFF0E || runeValue == 0xFF61)
                {
                    if (hasKatakankaMiddleDot && !hasHiraganaKatakanaOrHan)
                    {
                        return false;
                    }

                    if (hasArabicIndicDigits && hasExtendedArabicIndicDigits)
                    {
                        return false;
                    }

                    hasHiraganaKatakanaOrHan = false;
                    hasKatakankaMiddleDot = false;
                    hasArabicIndicDigits = false;
                    hasExtendedArabicIndicDigits = false;
                    wasLastDot = true;
                    previousRune = rune;
                    continue;
                }

                hasKatakankaMiddleDot |= (runeValue == 0x30FB);

                Rune lookahead = default;

                if (runeValue == 0x0375)
                {
                    if (i >= value.Length)
                    {
                        return false;
                    }

                    Rune.DecodeFromUtf8(value.Slice(i), out lookahead, out _);

                    if (!IsGreek((int)lookahead.Value))
                    {
                        return false;
                    }

                    wasLastDot = false;
                    previousRune = rune;
                    continue;
                }

                // Middle dot
                if (runeValue == 0x00B7)
                {
                    if ((int)previousRune.Value != 0x006C)
                    {
                        return false;
                    }

                    if ((int)lookahead.Value == 0)
                    {
                        Rune.DecodeFromUtf8(value.Slice(i), out lookahead, out _);
                        if ((int)lookahead.Value != 0x006C)
                        {
                            return false;
                        }
                    }
                }

                if (byteValue == (byte)'-')
                {
                    if (i == bytesConsumed || i >= value.Length)
                    {
                        return false;
                    }

                    if (runeCount == 4 && (int)previousRune.Value == 0x002d)
                    {
                        return false;
                    }

                    wasLastDot = false;
                    previousRune = rune;
                    continue;
                }

                // ZERO WIDTH JOINER not preceded by Virama
                if (runeValue == 0x200D && !IsVirama((int)previousRune.Value))
                {
                    return false;
                }

                // Geresh or Gershayim must be preceded by Hebrew
                if ((runeValue == 0x05F3 || runeValue == 0x05F4) && !IsHebrew((int)previousRune.Value))
                {
                    return false;
                }
            }

            wasLastDot = false;
            previousRune = rune;
        }

        if (hasKatakankaMiddleDot && !hasHiraganaKatakanaOrHan)
        {
            return false;
        }

        if (hasArabicIndicDigits && hasExtendedArabicIndicDigits)
        {
            return false;
        }

        return true;
    }

    private static bool IsArabicIndicDigit(int value) => (value >= 0x0660 && value <= 0x0669);

    private static bool IsExtendedArabicIndicDigit(int value) => (value >= 0x06F0 && value <= 0x06F9);

    private static bool IsGreek(int value) => (value >= 0x0370 && value <= 0x03FF) || (value >= 0x1F00 && value <= 0x1FFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHebrew(int value) => (value >= 0x0590 && value <= 0x05FF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHiraganaKatakanaOrHanNotMiddleDot(int value)
    {
        if (value == 0x30FB)
        {
            return false;
        }

        return
            (value >= 0x30A0 && value <= 0x30FF) ||
            (value >= 0x3040 && value <= 0x309F) ||
            (value >= 0x3400 && value <= 0x4DB5) ||
            (value >= 0x4E00 && value <= 0x9FCB) ||
            (value >= 0xF900 && value <= 0xFA6A);
    }

    private static bool IsVirama(int value) => ViramaTable.IndexOf(value) >= 0;
}