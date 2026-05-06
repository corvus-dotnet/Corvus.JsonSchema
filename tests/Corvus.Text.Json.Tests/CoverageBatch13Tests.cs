// Copyright (c) Endjin Limited. All rights reserved.

using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 13: JsonElement.TryGetBoolean, GetXxx throw paths (NodaTime + numeric),
/// and Utf8JsonReader.TryGet GetXxx throw paths.
/// </summary>
public static class CoverageBatch13Tests
{
    #region JsonElement.TryGetBoolean success paths (lines 521-525)

    /// <summary>
    /// TryGetBoolean on a true element returns true with value=true.
    /// Target: JsonElement.cs line 521.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetBoolean_True_ReturnsTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true"u8.ToArray());
        bool success = doc.RootElement.TryGetBoolean(out bool value);
        Assert.True(success);
        Assert.True(value);
    }

    /// <summary>
    /// TryGetBoolean on a false element returns true with value=false.
    /// Target: JsonElement.cs line 524.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetBoolean_False_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false"u8.ToArray());
        bool success = doc.RootElement.TryGetBoolean(out bool value);
        Assert.True(success);
        Assert.False(value);
    }

    #endregion

#if NET
    #region JsonElement.GetHalf throw path — DEAD CODE documented
    // Lines 1208-1209: GetHalf() → ThrowFormatException is unreachable because
    // Half.TryParse always succeeds for valid JSON Number tokens (returns Infinity for overflow).
    // The type check in TryGetHalf throws InvalidOperationException for non-Number tokens
    // before TryGetHalf returns false.
    #endregion
#endif

    #region JsonElement.GetBigNumber throw path — DEAD CODE documented
    // Lines 1477-1478: GetBigNumber() → ThrowFormatException is unreachable because
    // BigNumber.TryParse always succeeds for valid JSON Number tokens.
    // The type check throws InvalidOperationException for non-Number tokens first.
    #endregion

    #region JsonElement NodaTime GetXxx success paths (lines 1584, 1635, 1710, 1737, 1788)

    /// <summary>
    /// GetLocalDate on a valid date string succeeds.
    /// Target: JsonElement.cs line 1584 (return value).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void GetLocalDate_OnValidString_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            "2024-03-15"
            """u8.ToArray());
        LocalDate result = doc.RootElement.GetLocalDate();
        Assert.Equal(new LocalDate(2024, 3, 15), result);
    }

    /// <summary>
    /// GetOffsetTime on a valid time string succeeds.
    /// Target: JsonElement.cs line 1635 (return value).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void GetOffsetTime_OnValidString_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            "10:30:00+02:00"
            """u8.ToArray());
        OffsetTime result = doc.RootElement.GetOffsetTime();
        Assert.Equal(new OffsetTime(new LocalTime(10, 30, 0), NodaTime.Offset.FromHours(2)), result);
    }

    /// <summary>
    /// GetOffsetDate on a valid date+offset string succeeds.
    /// Target: JsonElement.cs line 1710 (return value).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void GetOffsetDate_OnValidString_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            "2024-03-15+05:30"
            """u8.ToArray());
        OffsetDate result = doc.RootElement.GetOffsetDate();
        Assert.Equal(new OffsetDate(new LocalDate(2024, 3, 15), NodaTime.Offset.FromHoursAndMinutes(5, 30)), result);
    }

    /// <summary>
    /// GetOffsetDateTime on a valid datetime+offset string succeeds.
    /// Target: JsonElement.cs line 1737 (return value).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void GetOffsetDateTime_OnValidString_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            "2024-03-15T10:30:00+02:00"
            """u8.ToArray());
        OffsetDateTime result = doc.RootElement.GetOffsetDateTime();
        Assert.Equal(
            new OffsetDateTime(new LocalDateTime(2024, 3, 15, 10, 30, 0), NodaTime.Offset.FromHours(2)),
            result);
    }

    /// <summary>
    /// GetPeriod on a valid ISO 8601 duration string succeeds.
    /// Target: JsonElement.cs line 1788 (return value).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void GetPeriod_OnValidString_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            "P1Y2M3D"
            """u8.ToArray());
        Period result = doc.RootElement.GetPeriod();
        Assert.Equal(Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3), result);
    }

    #endregion

    #region Utf8JsonReader.TryGet GetXxx throw paths (lines 124, 233, 257, 305, 330, 355, 381, 405)

    /// <summary>
    /// GetByte on a value exceeding byte range throws FormatException.
    /// Target: Utf8JsonReader.TryGet.cs line 124.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonReader_GetByte_Overflow_ThrowsFormatException()
    {
        byte[] json = "999"u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();
        try
        {
            reader.GetByte();
            Assert.Fail("Expected FormatException");
        }
        catch (FormatException)
        {
            // expected
        }
    }

    /// <summary>
    /// GetDecimal on a non-numeric string throws FormatException.
    /// Target: Utf8JsonReader.TryGet.cs line 233.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonReader_GetDecimal_Invalid_ThrowsFormatException()
    {
        // Use a number that's beyond decimal range
        byte[] json = "1e999"u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();
        try
        {
            reader.GetDecimal();
            Assert.Fail("Expected FormatException");
        }
        catch (FormatException)
        {
            // expected
        }
    }

    /// <summary>
    /// GetDouble FormatException path (line 257) is DEAD CODE.
    /// Double.TryParse always succeeds for valid JSON Number tokens (returns Infinity for overflow).
    /// The InvalidOperationException from the type check handles non-Number tokens.
    /// </summary>

    /// <summary>
    /// GetInt16 on an overflowing value throws FormatException.
    /// Target: Utf8JsonReader.TryGet.cs line 305.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonReader_GetInt16_Overflow_ThrowsFormatException()
    {
        byte[] json = "99999"u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();
        try
        {
            reader.GetInt16();
            Assert.Fail("Expected FormatException");
        }
        catch (FormatException)
        {
            // expected
        }
    }

    /// <summary>
    /// GetInt32 on an overflowing value throws FormatException.
    /// Target: Utf8JsonReader.TryGet.cs line 330.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonReader_GetInt32_Overflow_ThrowsFormatException()
    {
        byte[] json = "9999999999"u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();
        try
        {
            reader.GetInt32();
            Assert.Fail("Expected FormatException");
        }
        catch (FormatException)
        {
            // expected
        }
    }

    /// <summary>
    /// GetInt64 on an overflowing value throws FormatException.
    /// Target: Utf8JsonReader.TryGet.cs line 355.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonReader_GetInt64_Overflow_ThrowsFormatException()
    {
        byte[] json = "99999999999999999999"u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();
        try
        {
            reader.GetInt64();
            Assert.Fail("Expected FormatException");
        }
        catch (FormatException)
        {
            // expected
        }
    }

    /// <summary>
    /// GetSByte on an overflowing value throws FormatException.
    /// Target: Utf8JsonReader.TryGet.cs line 381.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void Utf8JsonReader_GetSByte_Overflow_ThrowsFormatException()
    {
        byte[] json = "999"u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();
        try
        {
            reader.GetSByte();
            Assert.Fail("Expected FormatException");
        }
        catch (FormatException)
        {
            // expected
        }
    }

    /// <summary>
    /// GetSingle FormatException path (line 405) is DEAD CODE.
    /// Single.TryParse always succeeds for valid JSON Number tokens (returns Infinity for overflow).
    /// The InvalidOperationException from the type check handles non-Number tokens.
    /// </summary>

    #endregion
}
