// <copyright file="CharUnicodeInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Globalization;

/// <summary>
/// This class implements a set of methods for retrieving character type
/// information. Character type information is independent of culture
/// and region.
/// </summary>
public static partial class CharUnicodeInfo
{
    internal const char HIGH_SURROGATE_END = '\udbff';

    internal const int HIGH_SURROGATE_RANGE = 0x3FF;

    internal const char HIGH_SURROGATE_START = '\ud800';

    internal const char LOW_SURROGATE_END = '\udfff';

    internal const char LOW_SURROGATE_START = '\udc00';

    // The starting codepoint for Unicode plane 1.  Plane 1 contains 0x010000 ~ 0x01ffff.
    internal const int UNICODE_PLANE01_START = 0x10000;

    /*
     * GetBidiCategory
     * ===============
     * Data derived from https:// www.unicode.org/reports/tr9/#Bidirectional_Character_Types. This data
     * is encoded in DerivedBidiClass.txt. We map "L" to "strong left-to-right"; and we map "R" and "AL"
     * to "strong right-to-left". All other (non-strong) code points are "other" for our purposes.
     */

    internal static StrongBidiCategory GetBidiCategory(ReadOnlySpan<char> s, int index)
    {
        if ((uint)index >= (uint)s.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
        }

        return GetBidiCategoryNoBoundsChecks((uint)GetCodePointFromString(s, index));
    }

    internal static StrongBidiCategory GetBidiCategory(ReadOnlySpan<byte> s, int index, out int consumed)
    {
        if ((uint)index >= (uint)s.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
        }

        return GetBidiCategoryNoBoundsChecks((uint)GetCodePointFromString(s, index, out consumed));
    }

    [Conditional("DEBUG")]
    private static void AssertIsValidCodePoint(uint codePoint)
    {
        if (codePoint > 0x10FFFFU)
        {
            Debug.Fail($"The value {FormattableString.Invariant($"U+{codePoint:X4}")} is not a valid Unicode code point.");
        }
    }

    private static StrongBidiCategory GetBidiCategoryNoBoundsChecks(uint codePoint)
    {
        nuint offset = GetCategoryCasingTableOffsetNoBoundsChecks(codePoint);

        // Each entry of the 'CategoryValues' table uses bits 5 - 6 to store the strong bidi information.
        var bidiCategory = (StrongBidiCategory)(Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoriesValues), offset) & 0b_0110_0000);
        Debug.Assert(bidiCategory == StrongBidiCategory.Other || bidiCategory == StrongBidiCategory.StrongLeftToRight || bidiCategory == StrongBidiCategory.StrongRightToLeft, "Unknown StrongBidiCategory value.");

        return bidiCategory;
    }

    /////*
    //// * HELPER AND TABLE LOOKUP ROUTINES
    //// */

    /// <summary>
    /// Retrieves the offset into the "CategoryCasing" arrays where this code point's
    /// information is stored. Used for getting the Unicode category, bidi information,
    /// and whitespace information.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint GetCategoryCasingTableOffsetNoBoundsChecks(uint codePoint)
    {
        AssertIsValidCodePoint(codePoint);

        // The code below is written with the assumption that the backing store is 11:5:4.
        AssertCategoryCasingTableLevels(11, 5, 4);

        // Get the level index item from the high 11 bits of the code point.
        uint index = Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoryCasingLevel1Index), codePoint >> 9);

        // Get the level 2 WORD offset from the next 5 bits of the code point.
        // This provides the base offset of the level 3 table.
        // Note that & has lower precedence than +, so remember the parens.
        ref byte level2Ref = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoryCasingLevel2Index), (index << 6) + ((codePoint >> 3) & 0b_0011_1110));

        if (BitConverter.IsLittleEndian)
        {
            index = Unsafe.ReadUnaligned<ushort>(ref level2Ref);
        }
        else
        {
            index = BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(ref level2Ref));
        }

        // Get the result from the low 4 bits of the code point.
        // This is the offset into the values table where the data is stored.
        return Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(CategoryCasingLevel3Index), (index << 4) + (codePoint & 0x0F));
    }

    /// <summary>
    /// Returns the code point pointed to by index, decoding any surrogate sequence if possible.
    /// This is similar to char.ConvertToUTF32, but the difference is that
    /// it does not throw exceptions when invalid surrogate characters are passed in.
    ///
    /// WARNING: since it doesn't throw an exception it CAN return a value
    /// in the surrogate range D800-DFFF, which is not a legal scalar value.
    /// </summary>
    private static int GetCodePointFromString(ReadOnlySpan<char> s, int index)
    {
        Debug.Assert((uint)index < (uint)s.Length, "index < s.Length");

        int codePoint = 0;

        // We know the 'if' block below will always succeed, but it allows the
        // JIT to optimize the codegen of this method.
        if ((uint)index < (uint)s.Length)
        {
            codePoint = s[index];
            int temp1 = codePoint - HIGH_SURROGATE_START;
            if ((uint)temp1 <= HIGH_SURROGATE_RANGE)
            {
                index++;
                if ((uint)index < (uint)s.Length)
                {
                    int temp2 = s[index] - LOW_SURROGATE_START;
                    if ((uint)temp2 <= HIGH_SURROGATE_RANGE)
                    {
                        // Combine these surrogate code points into a supplementary code point
                        codePoint = (temp1 << 10) + temp2 + UNICODE_PLANE01_START;
                    }
                }
            }
        }

        return codePoint;
    }

    /// <summary>
    /// Returns the code point pointed to by index, decoding any surrogate sequence if possible.
    /// This is similar to char.ConvertToUTF32, but the difference is that
    /// it does not throw exceptions when invalid surrogate characters are passed in.
    ///
    /// WARNING: since it doesn't throw an exception it CAN return a value
    /// in the surrogate range D800-DFFF, which is not a legal scalar value.
    /// </summary>
    private static int GetCodePointFromString(ReadOnlySpan<byte> s, int index, out int consumed)
    {
        Debug.Assert((uint)index < (uint)s.Length, "index < s.Length");

        int codePoint = 0;
        consumed = 0;

        // We know the 'if' block below will always succeed, but it allows the
        // JIT to optimize the codegen of this method.
        if ((uint)index < (uint)s.Length)
        {
            Rune.DecodeFromUtf8(s.Slice(index), out Rune result, out consumed);
            codePoint = result.Value;
        }

        return codePoint;
    }
}