// <copyright file="FormatNumberPicture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using Corvus.Text;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Pre-parsed XPath F&amp;O 4.7 format-number picture descriptor.
/// Parsed once at compile time; <see cref="Format"/> produces zero managed allocations.
/// </summary>
internal readonly struct FormatNumberPicture
{
    private readonly byte[] _decSep;
    private readonly byte[] _grpSep;
    private readonly byte[] _expSep;
    private readonly byte[] _zeroDigitUtf8;
    private readonly int _digitByteLen;
    private readonly byte[] _minusSign;
    private readonly byte[] _percent;
    private readonly byte[] _perMille;
    private readonly SubPic _pos;
    private readonly SubPic _neg;

    private FormatNumberPicture(
        byte[] decSep,
        byte[] grpSep,
        byte[] expSep,
        byte[] zeroDigitUtf8,
        byte[] minusSign,
        byte[] percent,
        byte[] perMille,
        in SubPic pos,
        in SubPic neg)
    {
        _decSep = decSep;
        _grpSep = grpSep;
        _expSep = expSep;
        _zeroDigitUtf8 = zeroDigitUtf8;
        _digitByteLen = zeroDigitUtf8.Length;
        _minusSign = minusSign;
        _percent = percent;
        _perMille = perMille;
        _pos = pos;
        _neg = neg;
    }

    /// <summary>
    /// Analysed sub-picture (positive or negative).
    /// </summary>
    internal readonly struct SubPic
    {
        public readonly byte[] Prefix;
        public readonly byte[] Suffix;
        public readonly int RegularGrouping;
        public readonly int[] IntGroupPositions;
        public readonly int[] FracGroupPositions;
        public readonly int MinInt;
        public readonly int ScalingFactor;
        public readonly int MinFrac;
        public readonly int MaxFrac;
        public readonly int MinExp;
        public readonly bool HasPercent;
        public readonly bool HasPerMille;
        public readonly bool HasDecSep;

        public SubPic(
            byte[] prefix,
            byte[] suffix,
            int regularGrouping,
            int[] intGroupPositions,
            int[] fracGroupPositions,
            int minInt,
            int scalingFactor,
            int minFrac,
            int maxFrac,
            int minExp,
            bool hasPercent,
            bool hasPerMille,
            bool hasDecSep)
        {
            Prefix = prefix;
            Suffix = suffix;
            RegularGrouping = regularGrouping;
            IntGroupPositions = intGroupPositions;
            FracGroupPositions = fracGroupPositions;
            MinInt = minInt;
            ScalingFactor = scalingFactor;
            MinFrac = minFrac;
            MaxFrac = maxFrac;
            MinExp = minExp;
            HasPercent = hasPercent;
            HasPerMille = hasPerMille;
            HasDecSep = hasDecSep;
        }
    }

    /// <summary>
    /// Parses a format-number picture string and optional formatting options into
    /// a reusable <see cref="FormatNumberPicture"/>.
    /// </summary>
    /// <remarks>This is called once at compile time; allocations here are acceptable.</remarks>
    public static FormatNumberPicture Parse(string picture, JsonElement options)
    {
        // Build formatting properties (defaults overridden by options).
        char decSep = '.';
        char grpSep = ',';
        char expSep = 'e';
        string minusSign = "-";
        string pct = "%";
        string perMille = "\u2030";
        char zeroDigit = '0';
        char optDigit = '#';
        char patSep = ';';

        if (options.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in options.EnumerateObject())
            {
                string v = prop.Value.GetString() ?? string.Empty;
                switch (prop.Name)
                {
                    case "decimal-separator": if (v.Length > 0) decSep = v[0]; break;
                    case "grouping-separator": if (v.Length > 0) grpSep = v[0]; break;
                    case "exponent-separator": if (v.Length > 0) expSep = v[0]; break;
                    case "minus-sign": if (v.Length > 0) minusSign = v; break;
                    case "percent": pct = v; break;
                    case "per-mille": perMille = v; break;
                    case "zero-digit": if (v.Length > 0) zeroDigit = v[0]; break;
                    case "digit": if (v.Length > 0) optDigit = v[0]; break;
                    case "pattern-separator": if (v.Length > 0) patSep = v[0]; break;
                }
            }
        }

        // Local helpers matching the existing logic.
        bool IsInDigitFamily(char c) => c >= zeroDigit && c < (char)(zeroDigit + 10);
        bool IsActiveNotExp(char c) => (IsInDigitFamily(c) || c == optDigit || c == decSep || c == grpSep) && c != expSep;
        bool IsActive(char c) => IsInDigitFamily(c) || c == optDigit || c == decSep || c == grpSep || c == expSep;
        bool IsDigitOrOpt(char c) => IsInDigitFamily(c) || c == optDigit;

        // Split on pattern separator.
        string[] subPics = picture.Split(patSep);
        if (subPics.Length > 2)
        {
            throw new JsonataException("D3080", SR.D3080_ThePictureStringMustNotContainMoreThanOnePatternSeparator, 0);
        }

        int numPics = subPics.Length;

        // Parse and analyse each sub-picture.
        SubPic[] analysed = new SubPic[numPics];

        for (int p = 0; p < numPics; p++)
        {
            string sub = subPics[p];

            // Find prefix (before first active char, excluding expSep).
            int prefixEnd = sub.Length;
            for (int i = 0; i < sub.Length; i++)
            {
                if (IsActiveNotExp(sub[i]))
                {
                    prefixEnd = i;
                    break;
                }
            }

            // Find suffix (after last active char, excluding expSep).
            int suffixStart = 0;
            for (int i = sub.Length - 1; i >= 0; i--)
            {
                if (IsActiveNotExp(sub[i]))
                {
                    suffixStart = i + 1;
                    break;
                }
            }

            string prefix = sub.Substring(0, prefixEnd);
            string suffix = sub.Substring(suffixStart);
            string active = (prefixEnd <= suffixStart)
                ? sub.Substring(prefixEnd, suffixStart - prefixEnd)
                : string.Empty;

            // Find exponent separator.
            int expPos = sub.IndexOf(expSep, prefixEnd);
            string mantissa;
            string? exponent;
            if (expPos == -1 || expPos >= suffixStart)
            {
                mantissa = active;
                exponent = null;
            }
            else
            {
                int expPosInActive = expPos - prefixEnd;
                mantissa = active.Substring(0, expPosInActive);
                exponent = active.Substring(expPosInActive + 1);
            }

            // Split mantissa on decimal separator.
            int decIdx = mantissa.IndexOf(decSep);
            string intPart;
            string fracPart;
            bool hasDecSepInPicture;
            if (decIdx == -1)
            {
                intPart = mantissa;
                fracPart = suffix;
                hasDecSepInPicture = false;
            }
            else
            {
                intPart = mantissa.Substring(0, decIdx);
                fracPart = mantissa.Substring(decIdx + 1);
                hasDecSepInPicture = true;
            }

            // ── Validation (F&O 4.7.3) ─────────────────────────
            if (sub.IndexOf(decSep) != sub.LastIndexOf(decSep))
            {
                throw new JsonataException("D3081", SR.D3081_ThePictureStringMustNotContainMoreThanOneDecimalSeparator, 0);
            }

            if (pct.Length > 0)
            {
                int firstPct = sub.IndexOf(pct, StringComparison.Ordinal);
                if (firstPct != -1 && sub.IndexOf(pct, firstPct + pct.Length, StringComparison.Ordinal) != -1)
                {
                    throw new JsonataException("D3082", SR.D3082_ThePictureStringMustNotContainMoreThanOnePercentCharacter, 0);
                }
            }

            if (perMille.Length > 0)
            {
                int firstPm = sub.IndexOf(perMille, StringComparison.Ordinal);
                if (firstPm != -1 && sub.IndexOf(perMille, firstPm + perMille.Length, StringComparison.Ordinal) != -1)
                {
                    throw new JsonataException("D3083", SR.D3083_ThePictureStringMustNotContainMoreThanOnePerMilleCharacter, 0);
                }
            }

            if (pct.Length > 0 && perMille.Length > 0 &&
                sub.IndexOf(pct, StringComparison.Ordinal) != -1 &&
                sub.IndexOf(perMille, StringComparison.Ordinal) != -1)
            {
                throw new JsonataException("D3084", SR.D3084_PictureStringBothPercentAndPerMille, 0);
            }

            bool hasDigit = false;
            for (int i = 0; i < mantissa.Length; i++)
            {
                if (IsInDigitFamily(mantissa[i]) || mantissa[i] == optDigit)
                {
                    hasDigit = true;
                    break;
                }
            }

            if (!hasDigit)
            {
                throw new JsonataException("D3085", SR.D3085_MantissaMustContainDigit, 0);
            }

            for (int i = 0; i < active.Length; i++)
            {
                if (!IsActive(active[i]))
                {
                    throw new JsonataException("D3086", SR.D3086_PassiveCharacterBetweenActive, 0);
                }
            }

            int decPosInSub = sub.IndexOf(decSep);
            if (decPosInSub != -1)
            {
                if ((decPosInSub > 0 && sub[decPosInSub - 1] == grpSep) ||
                    (decPosInSub < sub.Length - 1 && sub[decPosInSub + 1] == grpSep))
                {
                    throw new JsonataException("D3087", SR.D3087_GroupingSeparatorAdjacentToDecimal, 0);
                }
            }
            else
            {
                if (intPart.Length > 0 && intPart[intPart.Length - 1] == grpSep)
                {
                    throw new JsonataException("D3088", SR.D3088_IntegerPartEndsWithGroupingSeparator, 0);
                }
            }

            string doubleGrp = new string(grpSep, 2);
            if (sub.IndexOf(doubleGrp, StringComparison.Ordinal) != -1)
            {
                throw new JsonataException("D3089", SR.D3089_ThePictureStringMustNotContainAdjacentGroupingSeparators, 0);
            }

            int optPos = intPart.IndexOf(optDigit);
            if (optPos != -1)
            {
                for (int i = 0; i < optPos; i++)
                {
                    if (IsInDigitFamily(intPart[i]))
                    {
                        throw new JsonataException("D3090", SR.D3090_MandatoryDigitBeforeOptional, 0);
                    }
                }
            }

            int lastOptPos = fracPart.LastIndexOf(optDigit);
            if (lastOptPos != -1)
            {
                for (int i = lastOptPos; i < fracPart.Length; i++)
                {
                    if (IsInDigitFamily(fracPart[i]))
                    {
                        throw new JsonataException("D3091", SR.D3091_MandatoryDigitAfterOptional, 0);
                    }
                }
            }

            bool expPresent = exponent is not null;
            if (expPresent && exponent!.Length > 0 &&
                (sub.IndexOf(pct, StringComparison.Ordinal) != -1 ||
                 sub.IndexOf(perMille, StringComparison.Ordinal) != -1))
            {
                throw new JsonataException("D3092", SR.D3092_ExponentWithPercentOrPerMille, 0);
            }

            if (expPresent)
            {
                if (exponent!.Length == 0)
                {
                    throw new JsonataException("D3093", SR.D3093_TheExponentPartOfThePictureStringMustContainAtLeastOneDigit, 0);
                }

                for (int i = 0; i < exponent!.Length; i++)
                {
                    if (!IsInDigitFamily(exponent![i]))
                    {
                        throw new JsonataException("D3093", SR.D3093_TheExponentPartOfThePictureStringMustContainOnlyDigitCharact, 0);
                    }
                }
            }

            // ── Analysis (F&O 4.7.4) ───────────────────────────
            List<int> intGrpPositions = GetGroupingPositions(intPart, false, intPart, grpSep, IsDigitOrOpt);
            int regularGrouping = ComputeRegularGrouping(intGrpPositions);
            List<int> fracGrpPositions = GetGroupingPositions(fracPart, true, intPart, grpSep, IsDigitOrOpt);

            int minInt = 0;
            for (int i = 0; i < intPart.Length; i++)
            {
                if (IsInDigitFamily(intPart[i]))
                {
                    minInt++;
                }
            }

            int sf = minInt;
            int minFrac = 0;
            int maxFrac = 0;
            for (int i = 0; i < fracPart.Length; i++)
            {
                if (IsInDigitFamily(fracPart[i]))
                {
                    minFrac++;
                }

                if (IsDigitOrOpt(fracPart[i]))
                {
                    maxFrac++;
                }
            }

            if (minInt == 0 && maxFrac == 0)
            {
                if (expPresent)
                {
                    minFrac = 1;
                    maxFrac = 1;
                }
                else
                {
                    minInt = 1;
                }
            }

            if (expPresent && minInt == 0 && intPart.IndexOf(optDigit) != -1)
            {
                minInt = 1;
            }

            if (minInt == 0 && minFrac == 0)
            {
                minFrac = 1;
            }

            int minExp = 0;
            if (expPresent)
            {
                for (int i = 0; i < exponent!.Length; i++)
                {
                    if (IsInDigitFamily(exponent![i]))
                    {
                        minExp++;
                    }
                }
            }

            bool hasPctInSub = pct.Length > 0 && sub.IndexOf(pct, StringComparison.Ordinal) != -1;
            bool hasPmInSub = perMille.Length > 0 && sub.IndexOf(perMille, StringComparison.Ordinal) != -1;

            analysed[p] = new SubPic(
                System.Text.Encoding.UTF8.GetBytes(prefix),
                System.Text.Encoding.UTF8.GetBytes(suffix),
                regularGrouping,
                intGrpPositions.ToArray(),
                fracGrpPositions.ToArray(),
                minInt,
                sf,
                minFrac,
                maxFrac,
                minExp,
                hasPctInSub,
                hasPmInSub,
                hasDecSepInPicture);
        }

        // Derive negative sub-picture if only one provided.
        SubPic negPic;
        byte[] minusBytes = System.Text.Encoding.UTF8.GetBytes(minusSign);
        if (numPics == 1)
        {
            byte[] negPrefix = new byte[minusBytes.Length + analysed[0].Prefix.Length];
            minusBytes.CopyTo(negPrefix, 0);
            analysed[0].Prefix.CopyTo(negPrefix, minusBytes.Length);
            negPic = new SubPic(
                negPrefix,
                analysed[0].Suffix,
                analysed[0].RegularGrouping,
                analysed[0].IntGroupPositions,
                analysed[0].FracGroupPositions,
                analysed[0].MinInt,
                analysed[0].ScalingFactor,
                analysed[0].MinFrac,
                analysed[0].MaxFrac,
                analysed[0].MinExp,
                analysed[0].HasPercent,
                analysed[0].HasPerMille,
                analysed[0].HasDecSep);
        }
        else
        {
            negPic = analysed[1];
        }

        byte[] zeroDigitUtf8 = EncodeCharToUtf8(zeroDigit);

        return new FormatNumberPicture(
            EncodeCharToUtf8(decSep),
            EncodeCharToUtf8(grpSep),
            EncodeCharToUtf8(expSep),
            zeroDigitUtf8,
            minusBytes,
            System.Text.Encoding.UTF8.GetBytes(pct),
            System.Text.Encoding.UTF8.GetBytes(perMille),
            analysed[0],
            negPic);
    }

    /// <summary>
    /// Formats a <see cref="double"/> value into <paramref name="output"/> according to
    /// this pre-parsed picture. Zero managed allocations on the formatting path.
    /// </summary>
    public void Format(double value, ref Utf8ValueStringBuilder output)
    {
        // Select sub-picture based on sign.
        ref readonly SubPic pic = ref (value >= 0 ? ref _pos : ref _neg);

        // Apply percent/per-mille scaling.
        double adjusted = value;
        if (pic.HasPercent)
        {
            adjusted *= 100;
        }
        else if (pic.HasPerMille)
        {
            adjusted *= 1000;
        }

        // Handle exponent normalization.
        double fmtMantissa;
        int exponentValue = 0;
        bool hasExponent = pic.MinExp > 0;

        if (!hasExponent)
        {
            fmtMantissa = adjusted;
        }
        else
        {
            double maxMantissa = Math.Pow(10, pic.ScalingFactor);
            double minMantissa = Math.Pow(10, pic.ScalingFactor - 1);
            fmtMantissa = adjusted;
            double absMant = Math.Abs(fmtMantissa);

            if (absMant != 0)
            {
                while (absMant < minMantissa)
                {
                    fmtMantissa *= 10;
                    absMant *= 10;
                    exponentValue--;
                }

                while (absMant > maxMantissa)
                {
                    fmtMantissa /= 10;
                    absMant /= 10;
                    exponentValue++;
                }
            }
        }

        // Format the absolute mantissa as fixed-point UTF-8 bytes.
        Span<byte> mantissaBuf = stackalloc byte[64];
        if (!Utf8Formatter.TryFormat(Math.Abs(fmtMantissa), mantissaBuf, out int mantissaLen, new StandardFormat('F', (byte)pic.MaxFrac)))
        {
            throw new InvalidOperationException("Failed to format mantissa as UTF-8");
        }

        ReadOnlySpan<byte> mantissa = mantissaBuf.Slice(0, mantissaLen);

        // Find the decimal point in the formatted mantissa.
        int dotPos = mantissa.IndexOf((byte)'.');
        if (dotPos == -1)
        {
            dotPos = mantissaLen;
        }

        // Digit offset for custom digit family remapping.
        // For single-byte digits (ASCII), this is a simple byte offset.
        // For multi-byte digits (e.g., circled numbers), we use AppendDigit.
        bool isMultiByte = _digitByteLen > 1;

        // Identify the range of significant integer digits (strip leading zeros).
        int intStart = 0;
        while (intStart < dotPos && mantissa[intStart] == (byte)'0')
        {
            intStart++;
        }

        // Identify the range of significant fractional digits (strip trailing zeros).
        int fracEnd = mantissaLen;
        if (dotPos < mantissaLen)
        {
            while (fracEnd > dotPos + 1 && mantissa[fracEnd - 1] == (byte)'0')
            {
                fracEnd--;
            }
        }

        int actualIntDigits = dotPos - intStart;
        int actualFracDigits = (dotPos < mantissaLen) ? fracEnd - dotPos - 1 : 0;

        // Calculate padding.
        int padLeft = pic.MinInt - actualIntDigits;
        if (padLeft < 0)
        {
            padLeft = 0;
        }

        int padRight = pic.MinFrac - actualFracDigits;
        if (padRight < 0)
        {
            padRight = 0;
        }

        int totalIntDigits = padLeft + actualIntDigits;
        int totalFracDigits = actualFracDigits + padRight;

        // ── Write prefix ────────────────────────────────────
        output.Append(pic.Prefix);

        // ── Write integer part with grouping separators ─────
        WriteIntegerPart(
            ref output,
            mantissa.Slice(intStart, actualIntDigits),
            totalIntDigits,
            padLeft,
            pic.RegularGrouping,
            pic.IntGroupPositions,
            _grpSep,
            _zeroDigitUtf8,
            _digitByteLen,
            isMultiByte);

        // ── Write decimal separator + fractional part ───────
        if (pic.HasDecSep && totalFracDigits > 0)
        {
            output.Append(_decSep);
            WriteFractionalPart(
                ref output,
                mantissa,
                dotPos,
                fracEnd,
                actualFracDigits,
                padRight,
                pic.FracGroupPositions,
                _grpSep,
                _zeroDigitUtf8,
                _digitByteLen,
                isMultiByte);
        }

        // ── Write exponent ──────────────────────────────────
        if (hasExponent)
        {
            output.Append(_expSep);
            if (exponentValue < 0)
            {
                output.Append(_minusSign);
            }

            // Format exponent value.
            Span<byte> expBuf = stackalloc byte[16];
            if (!Utf8Formatter.TryFormat(Math.Abs(exponentValue), expBuf, out int expLen))
            {
                throw new InvalidOperationException("Failed to format exponent as UTF-8");
            }

            // Pad exponent to minimum size.
            int expPad = pic.MinExp - expLen;
            if (expPad > 0)
            {
                AppendZeroPad(ref output, expPad, _zeroDigitUtf8);
            }

            // Remap exponent digits.
            for (int i = 0; i < expLen; i++)
            {
                AppendDigit(ref output, expBuf[i] - (byte)'0', _zeroDigitUtf8, _digitByteLen, isMultiByte);
            }
        }

        // ── Write suffix ────────────────────────────────────
        output.Append(pic.Suffix);
    }

    private static void WriteIntegerPart(
        ref Utf8ValueStringBuilder output,
        scoped ReadOnlySpan<byte> intDigits,
        int totalIntDigits,
        int padLeft,
        int regularGrouping,
        int[] irregularPositions,
        byte[] grpSep,
        byte[] zeroDigitUtf8,
        int digitByteLen,
        bool isMultiByte)
    {
        int written = 0;

        // Write padding zeros.
        for (int i = 0; i < padLeft; i++)
        {
            if (ShouldInsertGroupSep(written, totalIntDigits, regularGrouping, irregularPositions))
            {
                output.Append(grpSep);
            }

            output.Append(zeroDigitUtf8);
            written++;
        }

        // Write actual digits.
        for (int i = 0; i < intDigits.Length; i++)
        {
            if (ShouldInsertGroupSep(written, totalIntDigits, regularGrouping, irregularPositions))
            {
                output.Append(grpSep);
            }

            AppendDigit(ref output, intDigits[i] - (byte)'0', zeroDigitUtf8, digitByteLen, isMultiByte);
            written++;
        }
    }

    private static bool ShouldInsertGroupSep(
        int written,
        int totalDigits,
        int regularGrouping,
        int[] irregularPositions)
    {
        if (written == 0)
        {
            return false;
        }

        int remaining = totalDigits - written;

        if (regularGrouping > 0)
        {
            return remaining > 0 && remaining % regularGrouping == 0;
        }

        for (int i = 0; i < irregularPositions.Length; i++)
        {
            if (irregularPositions[i] == remaining)
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteFractionalPart(
        ref Utf8ValueStringBuilder output,
        scoped ReadOnlySpan<byte> mantissa,
        int dotPos,
        int fracEnd,
        int actualFracDigits,
        int padRight,
        int[] fracGroupPositions,
        byte[] grpSep,
        byte[] zeroDigitUtf8,
        int digitByteLen,
        bool isMultiByte)
    {
        int written = 0;

        // Write actual fractional digits.
        if (dotPos < mantissa.Length)
        {
            for (int i = dotPos + 1; i < fracEnd; i++)
            {
                if (ShouldInsertFracGroupSep(written, fracGroupPositions))
                {
                    output.Append(grpSep);
                }

                AppendDigit(ref output, mantissa[i] - (byte)'0', zeroDigitUtf8, digitByteLen, isMultiByte);
                written++;
            }
        }

        // Write padding zeros.
        for (int i = 0; i < padRight; i++)
        {
            if (ShouldInsertFracGroupSep(written, fracGroupPositions))
            {
                output.Append(grpSep);
            }

            output.Append(zeroDigitUtf8);
            written++;
        }
    }

    private static bool ShouldInsertFracGroupSep(int written, int[] fracGroupPositions)
    {
        if (written == 0)
        {
            return false;
        }

        for (int i = 0; i < fracGroupPositions.Length; i++)
        {
            if (fracGroupPositions[i] == written)
            {
                return true;
            }
        }

        return false;
    }

    // ── Digit output helpers ────────────────────────────────

    /// <summary>
    /// Appends a single digit (0-9) using the configured digit family encoding.
    /// For single-byte families (ASCII), writes one byte. For multi-byte families
    /// (e.g., circled numbers), writes the zero-digit UTF-8 bytes with the last byte
    /// incremented by the digit value.
    /// </summary>
    private static void AppendDigit(
        ref Utf8ValueStringBuilder output,
        int digitValue,
        byte[] zeroDigitUtf8,
        int digitByteLen,
        bool isMultiByte)
    {
        if (!isMultiByte)
        {
            output.Append((byte)(zeroDigitUtf8[0] + digitValue));
        }
        else
        {
            Span<byte> buf = stackalloc byte[4];
            zeroDigitUtf8.AsSpan().CopyTo(buf);
            buf[digitByteLen - 1] = (byte)(buf[digitByteLen - 1] + digitValue);
            output.Append(buf.Slice(0, digitByteLen));
        }
    }

    /// <summary>
    /// Appends <paramref name="count"/> zero-digit characters.
    /// </summary>
    private static void AppendZeroPad(
        ref Utf8ValueStringBuilder output,
        int count,
        byte[] zeroDigitUtf8)
    {
        for (int i = 0; i < count; i++)
        {
            output.Append(zeroDigitUtf8);
        }
    }

    /// <summary>
    /// Encodes a single <see cref="char"/> to its UTF-8 byte representation.
    /// </summary>
    private static byte[] EncodeCharToUtf8(char c)
    {
        Span<char> chars = stackalloc char[1];
        chars[0] = c;
        return System.Text.Encoding.UTF8.GetBytes(chars.ToArray());
    }

    // ── Picture parsing helpers ─────────────────────────────
    private static List<int> GetGroupingPositions(
        string part,
        bool toLeft,
        string searchPart,
        char grpSep,
        Func<char, bool> isDigitOrOpt)
    {
        var positions = new List<int>();
        int grpPos = part.IndexOf(grpSep);
        while (grpPos != -1)
        {
            string segment = toLeft ? part.Substring(0, grpPos) : part.Substring(grpPos);
            int count = 0;
            for (int i = 0; i < segment.Length; i++)
            {
                if (isDigitOrOpt(segment[i]))
                {
                    count++;
                }
            }

            positions.Add(count);
            grpPos = searchPart.IndexOf(grpSep, grpPos + 1);
        }

        return positions;
    }

    private static int ComputeRegularGrouping(List<int> positions)
    {
        if (positions.Count == 0)
        {
            return 0;
        }

        static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
        int factor = positions[0];
        for (int i = 1; i < positions.Count; i++)
        {
            factor = Gcd(factor, positions[i]);
        }

        for (int idx = 1; idx <= positions.Count; idx++)
        {
            if (!positions.Contains(idx * factor))
            {
                return 0;
            }
        }

        return factor;
    }
}