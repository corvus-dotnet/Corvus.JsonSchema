// <copyright file="JsonHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
#if !NET8_0_OR_GREATER
using System.Collections;
#endif

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json;

/// <summary>
/// Provides helper methods for JSON processing operations.
/// </summary>
internal static partial class JsonHelpers
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is between
    /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound)
        => (value - lowerBound) <= (upperBound - lowerBound);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is between
    /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRangeInclusive(int value, int lowerBound, int upperBound)
        => (uint)(value - lowerBound) <= (uint)(upperBound - lowerBound);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is between
    /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRangeInclusive(long value, long lowerBound, long upperBound)
        => (ulong)(value - lowerBound) <= (ulong)(upperBound - lowerBound);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is in the range [0..9].
    /// Otherwise, returns <see langword="false"/>.
    /// </summary>
    public static bool IsDigit(byte value) => (uint)(value - '0') <= '9' - '0';

    /// <summary>
    /// Calls Encoding.UTF8.GetString that supports netstandard.
    /// </summary>
    /// <param name="bytes">The utf8 bytes to convert.</param>
    /// <returns>A string representation of the UTF-8 encoded bytes.</returns>
    public static string Utf8GetString(ReadOnlySpan<byte> bytes)
    {
#if NET
        return Encoding.UTF8.GetString(bytes);
#else
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        unsafe
        {
            fixed (byte* bytesPtr = bytes)
            {
                return Encoding.UTF8.GetString(bytesPtr, bytes.Length);
            }
        }
#endif
    }

    /// <summary>
    /// Determines whether the specified double-precision floating-point number is finite (not infinite or NaN).
    /// </summary>
    /// <param name="value">A double-precision floating-point number.</param>
    /// <returns><c>true</c> if <paramref name="value"/> is finite; otherwise, <c>false</c>.</returns>
    public static bool IsFinite(double value)
    {
#if NET
        return double.IsFinite(value);
#else
        return !(double.IsNaN(value) || double.IsInfinity(value));
#endif
    }

    /// <summary>
    /// Determines whether the specified single-precision floating-point number is finite (not infinite or NaN).
    /// </summary>
    /// <param name="value">A single-precision floating-point number.</param>
    /// <returns><c>true</c> if <paramref name="value"/> is finite; otherwise, <c>false</c>.</returns>
    public static bool IsFinite(float value)
    {
#if NET
        return float.IsFinite(value);
#else
        return !(float.IsNaN(value) || float.IsInfinity(value));
#endif
    }

    /// <summary>
    /// Validates that the specified length does not exceed the maximum array length for 32-bit integers.
    /// </summary>
    /// <param name="length">The length to validate.</param>
    /// <exception cref="OutOfMemoryException">Thrown when <paramref name="length"/> exceeds the maximum allowed array length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateInt32MaxArrayLength(uint length)
    {
        if (length > 0X7FEFFFFF) // prior to .NET 6, max array length for sizeof(T) != 1 (size == 1 is larger)
        {
            ThrowHelper.ThrowOutOfMemoryException(length);
        }
    }

#if !NET8_0_OR_GREATER
    /// <summary>
    /// Determines whether all bits in the <see cref="BitArray"/> are set to <c>true</c>.
    /// </summary>
    /// <param name="bitArray">The <see cref="BitArray"/> to check.</param>
    /// <returns><c>true</c> if all bits are set; otherwise, <c>false</c>.</returns>
    public static bool HasAllSet(this BitArray bitArray)
    {
        for (int i = 0; i < bitArray.Count; i++)
        {
            if (!bitArray[i])
            {
                return false;
            }
        }

        return true;
    }
#endif

    /// <summary>
    /// Gets a Regex instance for recognizing integer representations of enums.
    /// </summary>
    public static readonly Regex IntegerRegex = CreateIntegerRegex();

    private const string IntegerRegexPattern = @"^\s*(?:\+|\-)?[0-9]+\s*$";

    private const int IntegerRegexTimeoutMs = 200;

#if NET

    [GeneratedRegex(IntegerRegexPattern, RegexOptions.None, matchTimeoutMilliseconds: IntegerRegexTimeoutMs)]
    private static partial Regex CreateIntegerRegex();

#else
    private static Regex CreateIntegerRegex() => new(IntegerRegexPattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(IntegerRegexTimeoutMs));
#endif
}