// <copyright file="BigNumber.LocaleFormatting.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Globalization;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tests for BigNumber formatting in different locales with exact output verification.
/// Ensures equivalence with System.Decimal for values within decimal range.
/// </summary>
public class BigNumberLocaleFormattingTests
{
    #region Invariant Culture Tests

    [Fact]
    public void Format_InvariantCulture_FixedPoint_ExactMatch()
    {
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("F2", CultureInfo.InvariantCulture);
        string decimalResult = decimalValue.ToString("F2", CultureInfo.InvariantCulture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123.45");
    }

    [Fact]
    public void Format_InvariantCulture_Number_ExactMatch()
    {
        BigNumber bigValue = new(1234567, -2); // 12345.67
        decimal decimalValue = 12345.67m;

        string bigResult = bigValue.ToString("N2", CultureInfo.InvariantCulture);
        string decimalResult = decimalValue.ToString("N2", CultureInfo.InvariantCulture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("12,345.67");
    }

    [Fact]
    public void Format_InvariantCulture_Currency_ExactMatch()
    {
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("C2", CultureInfo.InvariantCulture);
        string decimalResult = decimalValue.ToString("C2", CultureInfo.InvariantCulture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("¤123.45");
    }

    [Fact]
    public void Format_InvariantCulture_Percent_ExactMatch()
    {
        BigNumber bigValue = new(75, -2); // 0.75 = 75%
        decimal decimalValue = 0.75m;

        string bigResult = bigValue.ToString("P2", CultureInfo.InvariantCulture);
        string decimalResult = decimalValue.ToString("P2", CultureInfo.InvariantCulture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("75.00 %");
    }

    [Fact]
    public void Format_InvariantCulture_Exponential_ExactMatch()
    {
        BigNumber bigValue = new(12345, 0);
        decimal decimalValue = 12345m;

        string bigResult = bigValue.ToString("E2", CultureInfo.InvariantCulture);
        string decimalResult = decimalValue.ToString("E2", CultureInfo.InvariantCulture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("1.23E+004");
    }

    [Fact]
    public void Format_InvariantCulture_NegativeFixedPoint_ExactMatch()
    {
        BigNumber bigValue = new(-12345, -2); // -123.45
        decimal decimalValue = -123.45m;

        string bigResult = bigValue.ToString("F2", CultureInfo.InvariantCulture);
        string decimalResult = decimalValue.ToString("F2", CultureInfo.InvariantCulture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("-123.45");
    }

    #endregion

    #region US English (en-US) Tests

    [Fact]
    public void Format_EnglishUS_FixedPoint_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("F2", culture);
        string decimalResult = decimalValue.ToString("F2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123.45");
    }

    [Fact]
    public void Format_EnglishUS_Number_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        BigNumber bigValue = new(1234567, -2); // 12345.67
        decimal decimalValue = 12345.67m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("12,345.67");
    }

    [Fact]
    public void Format_EnglishUS_Currency_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("C2", culture);
        string decimalResult = decimalValue.ToString("C2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("$123.45");
    }

    [Fact]
    public void Format_EnglishUS_Percent_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        BigNumber bigValue = new(75, -2); // 0.75 = 75%
        decimal decimalValue = 0.75m;

        string bigResult = bigValue.ToString("P2", culture);
        string decimalResult = decimalValue.ToString("P2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("75.00%");
    }

    [Fact]
    public void Format_EnglishUS_NegativeCurrency_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        BigNumber bigValue = new(-12345, -2); // -123.45
        decimal decimalValue = -123.45m;

        string bigResult = bigValue.ToString("C2", culture);
        string decimalResult = decimalValue.ToString("C2", culture);

        bigResult.ShouldBe(decimalResult);
        // Note: en-US format may vary by system locale (pattern 0 or 1)
        // Pattern 0: "($123.45)" Pattern 1: "-$123.45"
    }

    [Fact]
    public void Format_EnglishUS_LargeNumber_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        BigNumber bigValue = new(123456789, -2); // 1234567.89
        decimal decimalValue = 1234567.89m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("1,234,567.89");
    }

    #endregion

    #region French (fr-FR) Tests

    [Fact]
    public void Format_FrenchFrance_FixedPoint_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("F2", culture);
        string decimalResult = decimalValue.ToString("F2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123,45");
    }

    [Fact]
    public void Format_FrenchFrance_Number_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        BigNumber bigValue = new(1234567, -2); // 12345.67
        decimal decimalValue = 12345.67m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        // French uses narrow no-break space (U+202F) for thousands separator
        bigResult.ShouldBe("12\u202F345,67");
    }

    [Fact]
    public void Format_FrenchFrance_Currency_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("C2", culture);
        string decimalResult = decimalValue.ToString("C2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123,45 €");
    }

    [Fact]
    public void Format_FrenchFrance_Percent_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        BigNumber bigValue = new(75, -2); // 0.75 = 75%
        decimal decimalValue = 0.75m;

        string bigResult = bigValue.ToString("P2", culture);
        string decimalResult = decimalValue.ToString("P2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("75,00 %");
    }

    [Fact]
    public void Format_FrenchFrance_NegativeNumber_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        BigNumber bigValue = new(-1234567, -2); // -12345.67
        decimal decimalValue = -12345.67m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("-12\u202F345,67");
    }

    #endregion

    #region German (de-DE) Tests

    [Fact]
    public void Format_GermanGermany_FixedPoint_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("F2", culture);
        string decimalResult = decimalValue.ToString("F2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123,45");
    }

    [Fact]
    public void Format_GermanGermany_Number_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        BigNumber bigValue = new(1234567, -2); // 12345.67
        decimal decimalValue = 12345.67m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("12.345,67");
    }

    [Fact]
    public void Format_GermanGermany_Currency_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("C2", culture);
        string decimalResult = decimalValue.ToString("C2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123,45 €");
    }

    [Fact]
    public void Format_GermanGermany_Percent_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        BigNumber bigValue = new(75, -2); // 0.75 = 75%
        decimal decimalValue = 0.75m;

        string bigResult = bigValue.ToString("P2", culture);
        string decimalResult = decimalValue.ToString("P2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("75,00 %");
    }

    #endregion

    #region Japanese (ja-JP) Tests

    [Fact]
    public void Format_JapaneseJapan_FixedPoint_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("ja-JP");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("F2", culture);
        string decimalResult = decimalValue.ToString("F2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123.45");
    }

    [Fact]
    public void Format_JapaneseJapan_Number_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("ja-JP");
        BigNumber bigValue = new(1234567, -2); // 12345.67
        decimal decimalValue = 12345.67m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("12,345.67");
    }

    [Fact]
    public void Format_JapaneseJapan_Currency_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("ja-JP");
        BigNumber bigValue = new(12345, 0); // 12345 (yen doesn't use decimals)
        decimal decimalValue = 12345m;

        string bigResult = bigValue.ToString("C0", culture);
        string decimalResult = decimalValue.ToString("C0", culture);

        // Both BigNumber and decimal should use the same yen symbol from the culture
        bigResult.ShouldBe(decimalResult);
    }

    [Fact]
    public void Format_JapaneseJapan_Percent_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("ja-JP");
        BigNumber bigValue = new(75, -2); // 0.75 = 75%
        decimal decimalValue = 0.75m;

        string bigResult = bigValue.ToString("P2", culture);
        string decimalResult = decimalValue.ToString("P2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("75.00%");
    }

    #endregion

    #region British English (en-GB) Tests

    [Fact]
    public void Format_EnglishGB_FixedPoint_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-GB");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("F2", culture);
        string decimalResult = decimalValue.ToString("F2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123.45");
    }

    [Fact]
    public void Format_EnglishGB_Number_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-GB");
        BigNumber bigValue = new(1234567, -2); // 12345.67
        decimal decimalValue = 12345.67m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("12,345.67");
    }

    [Fact]
    public void Format_EnglishGB_Currency_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-GB");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("C2", culture);
        string decimalResult = decimalValue.ToString("C2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("£123.45");
    }

    [Fact]
    public void Format_EnglishGB_Percent_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("en-GB");
        BigNumber bigValue = new(75, -2); // 0.75 = 75%
        decimal decimalValue = 0.75m;

        string bigResult = bigValue.ToString("P2", culture);
        string decimalResult = decimalValue.ToString("P2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("75.00%");
    }

    #endregion

    #region Spanish (es-ES) Tests

    [Fact]
    public void Format_SpanishSpain_FixedPoint_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("F2", culture);
        string decimalResult = decimalValue.ToString("F2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123,45");
    }

    [Fact]
    public void Format_SpanishSpain_Number_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        BigNumber bigValue = new(1234567, -2); // 12345.67
        decimal decimalValue = 12345.67m;

        string bigResult = bigValue.ToString("N2", culture);
        string decimalResult = decimalValue.ToString("N2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("12.345,67");
    }

    [Fact]
    public void Format_SpanishSpain_Currency_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        string bigResult = bigValue.ToString("C2", culture);
        string decimalResult = decimalValue.ToString("C2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("123,45 €");
    }

    [Fact]
    public void Format_SpanishSpain_Percent_ExactMatch()
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        BigNumber bigValue = new(75, -2); // 0.75 = 75%
        decimal decimalValue = 0.75m;

        string bigResult = bigValue.ToString("P2", culture);
        string decimalResult = decimalValue.ToString("P2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("75,00 %");
    }

    #endregion

    #region Zero Value Tests Across Cultures

    [Fact]
    public void Format_Zero_FixedPoint_AllCulturesExactMatch()
    {
        BigNumber bigValue = BigNumber.Zero;
        decimal decimalValue = 0m;

        CultureInfo[] cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE"),
            CultureInfo.GetCultureInfo("ja-JP"),
        };

        foreach (CultureInfo? culture in cultures)
        {
            string bigResult = bigValue.ToString("F2", culture);
            string decimalResult = decimalValue.ToString("F2", culture);

            bigResult.ShouldBe(decimalResult, $"Failed for culture {culture.Name}");
        }
    }

    [Fact]
    public void Format_Zero_Number_AllCulturesExactMatch()
    {
        BigNumber bigValue = BigNumber.Zero;
        decimal decimalValue = 0m;

        CultureInfo[] cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE"),
            CultureInfo.GetCultureInfo("ja-JP"),
        };

        foreach (CultureInfo? culture in cultures)
        {
            string bigResult = bigValue.ToString("N2", culture);
            string decimalResult = decimalValue.ToString("N2", culture);

            bigResult.ShouldBe(decimalResult, $"Failed for culture {culture.Name}");
        }
    }

    [Fact]
    public void Format_Zero_Currency_AllCulturesExactMatch()
    {
        BigNumber bigValue = BigNumber.Zero;
        decimal decimalValue = 0m;

        CultureInfo[] cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE"),
        };

        foreach (CultureInfo? culture in cultures)
        {
            string bigResult = bigValue.ToString("C2", culture);
            string decimalResult = decimalValue.ToString("C2", culture);

            bigResult.ShouldBe(decimalResult, $"Failed for culture {culture.Name}");
        }
    }

    [Fact]
    public void Format_Zero_Percent_AllCulturesExactMatch()
    {
        BigNumber bigValue = BigNumber.Zero;
        decimal decimalValue = 0m;

        CultureInfo[] cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE"),
            CultureInfo.GetCultureInfo("ja-JP"),
        };

        foreach (CultureInfo? culture in cultures)
        {
            string bigResult = bigValue.ToString("P2", culture);
            string decimalResult = decimalValue.ToString("P2", culture);

            bigResult.ShouldBe(decimalResult, $"Failed for culture {culture.Name}");
        }
    }

    [Fact]
    public void Format_Zero_PercentEnUS_ExactMatch()
    {
        BigNumber bigValue = BigNumber.Zero;
        decimal decimalValue = 0m;
        var culture = CultureInfo.GetCultureInfo("en-US");

        string bigResult = bigValue.ToString("P2", culture);
        string decimalResult = decimalValue.ToString("P2", culture);

        bigResult.ShouldBe(decimalResult);
        bigResult.ShouldBe("0.00%");
    }

    #endregion

    #region Precision Tests Across Cultures

    [Fact]
    public void Format_VariousPrecisions_FixedPoint_ExactMatch()
    {
        BigNumber bigValue = new(123456789, -5); // 1234.56789
        decimal decimalValue = 1234.56789m;
        var culture = CultureInfo.GetCultureInfo("en-US");

        for (int precision = 0; precision <= 5; precision++)
        {
            string format = $"F{precision}";
            string bigResult = bigValue.ToString(format, culture);
            string decimalResult = decimalValue.ToString(format, culture);

            bigResult.ShouldBe(decimalResult, $"Failed for precision {precision}");
        }
    }

    [Fact]
    public void Format_VariousPrecisions_Number_ExactMatch()
    {
        BigNumber bigValue = new(123456789, -5); // 1234.56789
        decimal decimalValue = 1234.56789m;
        var culture = CultureInfo.GetCultureInfo("en-US");

        for (int precision = 0; precision <= 5; precision++)
        {
            string format = $"N{precision}";
            string bigResult = bigValue.ToString(format, culture);
            string decimalResult = decimalValue.ToString(format, culture);

            bigResult.ShouldBe(decimalResult, $"Failed for precision {precision}");
        }
    }

    [Fact]
    public void Format_VariousPrecisions_Currency_ExactMatch()
    {
        BigNumber bigValue = new(123456789, -5); // 1234.56789
        decimal decimalValue = 1234.56789m;
        var culture = CultureInfo.GetCultureInfo("en-US");

        for (int precision = 0; precision <= 5; precision++)
        {
            string format = $"C{precision}";
            string bigResult = bigValue.ToString(format, culture);
            string decimalResult = decimalValue.ToString(format, culture);

            bigResult.ShouldBe(decimalResult, $"Failed for precision {precision}");
        }
    }

    #endregion

    #region Negative Value Tests Across Cultures

    [Fact]
    public void Format_NegativeValue_AllCultures_ExactMatch()
    {
        BigNumber bigValue = new(-12345, -2); // -123.45
        decimal decimalValue = -123.45m;

        CultureInfo[] cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE"),
            CultureInfo.GetCultureInfo("ja-JP"),
        };

        foreach (CultureInfo? culture in cultures)
        {
            string bigResultF = bigValue.ToString("F2", culture);
            string decimalResultF = decimalValue.ToString("F2", culture);
            bigResultF.ShouldBe(decimalResultF, $"F2 failed for culture {culture.Name}");

            string bigResultN = bigValue.ToString("N2", culture);
            string decimalResultN = decimalValue.ToString("N2", culture);
            bigResultN.ShouldBe(decimalResultN, $"N2 failed for culture {culture.Name}");
        }
    }

    #endregion

    #region TryFormat with Locales

    [Fact]
    public void TryFormat_UTF16_MultipleCultures_ExactMatch()
    {
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        CultureInfo[] cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("fr-FR"),
            CultureInfo.GetCultureInfo("de-DE"),
        };

        Span<char> buffer = stackalloc char[128];
        foreach (CultureInfo? culture in cultures)
        {
            bool success = bigValue.TryFormat(buffer, out int charsWritten, "N2", culture);

            success.ShouldBeTrue($"TryFormat failed for culture {culture.Name}");
            string bigResult = buffer.Slice(0, charsWritten).ToString();
            string decimalResult = decimalValue.ToString("N2", culture);

            bigResult.ShouldBe(decimalResult, $"Failed for culture {culture.Name}");
        }
    }

    [Fact]
    public void TryFormat_UTF8_MultipleCultures_ExactMatch()
    {
        BigNumber bigValue = new(12345, -2); // 123.45
        decimal decimalValue = 123.45m;

        CultureInfo[] cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("en-US"),
        };

        Span<byte> buffer = stackalloc byte[128];
        foreach (CultureInfo? culture in cultures)
        {
            bool success = bigValue.TryFormat(buffer, out int bytesWritten, "F2", culture);

            success.ShouldBeTrue($"TryFormat failed for culture {culture.Name}");
            string bigResult = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
            string decimalResult = decimalValue.ToString("F2", culture);

            bigResult.ShouldBe(decimalResult, $"Failed for culture {culture.Name}");
        }
    }

    #endregion

    #region Round-Trip Tests with Locales

    [Fact]
    public void RoundTrip_FormattedWithLocale_ParsesCorrectly()
    {
        BigNumber original = new(123456, -2); // 1234.56
        var culture = CultureInfo.GetCultureInfo("de-DE");

        // Format with German culture (uses comma as decimal separator)
        string formatted = original.ToString("F2", culture);
        formatted.ShouldBe("1234,56");

        // Parse back with same culture
        var parsed = BigNumber.Parse(formatted, NumberStyles.Float | NumberStyles.AllowThousands, culture);

        parsed.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_FormattedWithLocale_French_ParsesCorrectly()
    {
        BigNumber original = new(1234567, -2); // 12345.67
        var culture = CultureInfo.GetCultureInfo("fr-FR");

        // Format with French culture
        string formatted = original.ToString("N2", culture);
        formatted.ShouldBe("12\u202F345,67");

        // Parse back with same culture
        var parsed = BigNumber.Parse(formatted, NumberStyles.Float | NumberStyles.AllowThousands, culture);

        parsed.ShouldBe(original);
    }

    #endregion
}