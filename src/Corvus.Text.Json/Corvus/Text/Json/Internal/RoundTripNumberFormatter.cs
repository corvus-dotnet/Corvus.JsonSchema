// <copyright file="RoundTripNumberFormatter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
#if !NET
using System.Globalization;
#endif

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Formats a <see cref="double"/> or <see cref="float"/> as UTF-8 JSON text that reads back as the same number.
/// </summary>
/// <remarks>
/// <para>
/// On .NET, <see cref="Utf8Formatter"/> writes the shortest such text. On .NET Framework it writes 15 significant
/// digits for a double and 7 for a float, which can lose the number (90.57000000000001 was written as 90.57, and
/// <see cref="double.MaxValue"/> as a value that overflows when it is read back). There, the shortest text that
/// parses back is found by trying the general format with more digits in turn. Every decimal of at most 15
/// significant digits (6 for a float) survives a round trip through a normal double (float), so for a normal number
/// the search starts there; a subnormal number has fewer significant digits, so for one it starts at 1.
/// </para>
/// </remarks>
internal static class RoundTripNumberFormatter
{
    /// <summary>
    /// Formats a double.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the value was written.</returns>
    public static bool TryFormat(double value, Span<byte> destination, out int bytesWritten)
    {
#if NET
        return Utf8Formatter.TryFormat(value, destination, out bytesWritten);
#else
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return Utf8Formatter.TryFormat(value, destination, out bytesWritten);
        }

        string text = null!;
        for (int precision = Math.Abs(value) < 2.2250738585072014E-308 ? 1 : 15; precision <= 17; precision++)
        {
            text = value.ToString(GeneralFormat(precision), CultureInfo.InvariantCulture);
            if (precision == 17 ||
                (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed == value))
            {
                break;
            }
        }

        return TryCopyAscii(text, destination, out bytesWritten);
#endif
    }

    /// <summary>
    /// Formats a float.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the value was written.</returns>
    public static bool TryFormat(float value, Span<byte> destination, out int bytesWritten)
    {
#if NET
        return Utf8Formatter.TryFormat(value, destination, out bytesWritten);
#else
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return Utf8Formatter.TryFormat(value, destination, out bytesWritten);
        }

        string text = null!;
        for (int precision = Math.Abs(value) < 1.17549435E-38f ? 1 : 6; precision <= 9; precision++)
        {
            text = value.ToString(GeneralFormat(precision), CultureInfo.InvariantCulture);
            if (precision == 9 ||
                (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && parsed == value))
            {
                break;
            }
        }

        return TryCopyAscii(text, destination, out bytesWritten);
#endif
    }

#if !NET
    private static string GeneralFormat(int precision) => precision switch
    {
        1 => "G1",
        2 => "G2",
        3 => "G3",
        4 => "G4",
        5 => "G5",
        6 => "G6",
        7 => "G7",
        8 => "G8",
        9 => "G9",
        10 => "G10",
        11 => "G11",
        12 => "G12",
        13 => "G13",
        14 => "G14",
        15 => "G15",
        16 => "G16",
        _ => "G17",
    };

    private static bool TryCopyAscii(string text, Span<byte> destination, out int bytesWritten)
    {
        // The general format of a finite number is ASCII: digits, sign, '.', 'E'.
        if (text.Length > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            destination[i] = (byte)text[i];
        }

        bytesWritten = text.Length;
        return true;
    }
#endif
}