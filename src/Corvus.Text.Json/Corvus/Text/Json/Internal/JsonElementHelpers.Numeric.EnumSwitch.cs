// <copyright file="JsonElementHelpers.Numeric.EnumSwitch.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for JSON numeric operations.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Attempts to convert a normalized JSON number to a <see cref="long"/> value.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the normalized number without leading zeros.</param>
    /// <param name="fractional">The fractional part of the normalized number without trailing zeros.</param>
    /// <param name="exponent">The exponent of the normalized number.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the <see cref="long"/> value.</param>
    /// <returns><see langword="true"/> if the normalized number represents an integer within the <see cref="long"/> range; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// The normalized form is: <c>Number = sign * (integral + fractional) * 10^exponent</c>,
    /// where <c>integral + fractional</c> is the concatenation of digit sequences forming the significand.
    /// </para>
    /// <para>
    /// A number is an integer if and only if <paramref name="exponent"/> &gt;= 0 (the fractional digits,
    /// if any, are absorbed by the significand concatenation and exponent adjusts the scale).
    /// </para>
    /// </remarks>
    public static bool TryGetNormalizedInt64(
        bool isNegative,
        ReadOnlySpan<byte> integral,
        ReadOnlySpan<byte> fractional,
        int exponent,
        out long value)
    {
        value = 0;

        // A negative exponent means the value has decimal places.
        if (exponent < 0)
        {
            return false;
        }

        // Zero is always representable.
        if (integral.IsEmpty && fractional.IsEmpty)
        {
            return true;
        }

        // The total number of effective digits cannot exceed 19 (max digits in long.MaxValue).
        int totalDigits = integral.Length + fractional.Length + exponent;
        if (totalDigits > 19)
        {
            return false;
        }

        // Parse significand from integral + fractional digit sequences.
        ulong significand = 0;

        for (int i = 0; i < integral.Length; i++)
        {
            significand = (significand * 10) + (ulong)(integral[i] - '0');
        }

        for (int i = 0; i < fractional.Length; i++)
        {
            significand = (significand * 10) + (ulong)(fractional[i] - '0');
        }

        // Apply exponent: multiply by 10^exponent.
        for (int i = 0; i < exponent; i++)
        {
            ulong next = significand * 10;
            if (next / 10 != significand)
            {
                return false;
            }

            significand = next;
        }

        // Convert to signed long, checking for overflow.
        if (isNegative)
        {
            // long.MinValue = -9223372036854775808, whose magnitude is (ulong)long.MaxValue + 1
            if (significand > (ulong)long.MaxValue + 1)
            {
                return false;
            }

            value = unchecked(-(long)significand);
        }
        else
        {
            if (significand > (ulong)long.MaxValue)
            {
                return false;
            }

            value = (long)significand;
        }

        return true;
    }
}