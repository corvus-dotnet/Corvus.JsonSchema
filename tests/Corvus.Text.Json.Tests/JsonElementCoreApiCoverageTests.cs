// <copyright file="JsonElementCoreApiCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Corvus.Text.Json;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for uncovered error paths in <see cref="JsonElement"/> core API:
/// GetXxx() methods throwing FormatException when the element is the correct JSON type
/// but the value cannot be parsed, GetHashCode on a default (uninitialized) element,
/// and NodaTime TryGetXxx success/failure paths.
/// </summary>
/// <remarks>
/// <para>
/// <c>GetDouble()</c> and <c>GetSingle()</c> FormatException paths are effectively
/// dead code: <c>Utf8Parser.TryParse</c> succeeds for any valid JSON number (returning
/// Infinity for overflow). Similarly, <c>GetHalf()</c> uses <c>Half.TryParse</c> which
/// also handles overflow. These are not tested because no valid JSON input can trigger them.
/// </para>
/// <para>
/// <c>GetBigNumber()</c> FormatException requires a number exceeding 10,000 characters
/// (<c>BigNumber.MaxInputLength</c>). While technically reachable, the cost of creating
/// such a JSON document makes it impractical for a unit test.
/// </para>
/// </remarks>
public class JsonElementCoreApiCoverageTests
{
    #region GetHashCode on default element

    /// <summary>
    /// A default (uninitialized) JsonElement should return 0 from GetHashCode.
    /// </summary>
    [Fact]
    public void GetHashCode_DefaultElement_ReturnsZero()
    {
        JsonElement element = default;
        Assert.Equal(0, element.GetHashCode());
    }

    #endregion

    #region GetBigInteger throws FormatException on fractional number

    /// <summary>
    /// GetBigInteger throws FormatException when the JSON number has a fractional part.
    /// BigInteger.TryParse (default NumberStyles.Integer) rejects decimal points.
    /// </summary>
    [Fact]
    public void GetBigInteger_OnFractionalNumber_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("1.5");
        Assert.Throws<FormatException>(() => doc.RootElement.GetBigInteger());
    }

    /// <summary>
    /// GetBigInteger throws FormatException when the JSON number uses scientific notation.
    /// BigInteger.TryParse (default NumberStyles.Integer) rejects exponent notation.
    /// </summary>
    [Fact]
    public void GetBigInteger_OnScientificNotation_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("1e2");
        Assert.Throws<FormatException>(() => doc.RootElement.GetBigInteger());
    }

    #endregion

    #region NodaTime Get methods throw FormatException on invalid string content

    /// <summary>
    /// GetLocalDate throws FormatException when the JSON string is not a valid date.
    /// </summary>
    [Fact]
    public void GetLocalDate_OnInvalidDateString_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.Throws<FormatException>(() => doc.RootElement.GetLocalDate());
    }

    /// <summary>
    /// GetOffsetTime throws FormatException when the JSON string is not a valid time.
    /// </summary>
    [Fact]
    public void GetOffsetTime_OnInvalidTimeString_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.Throws<FormatException>(() => doc.RootElement.GetOffsetTime());
    }

    /// <summary>
    /// GetOffsetDate throws FormatException when the JSON string is not a valid date.
    /// </summary>
    [Fact]
    public void GetOffsetDate_OnInvalidDateString_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"not-a-date\"");
        Assert.Throws<FormatException>(() => doc.RootElement.GetOffsetDate());
    }

    /// <summary>
    /// GetPeriod throws FormatException when the JSON string is not a valid period.
    /// </summary>
    [Fact]
    public void GetPeriod_OnInvalidPeriodString_ThrowsFormatException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.Throws<FormatException>(() => doc.RootElement.GetPeriod());
    }

    #endregion

    #region Successful NodaTime conversions (TryGet paths)

    /// <summary>
    /// TryGetLocalDate succeeds for a valid ISO date string.
    /// </summary>
    [Fact]
    public void TryGetLocalDate_ValidIsoDate_Succeeds()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"2023-01-15\"");
        Assert.True(doc.RootElement.TryGetLocalDate(out LocalDate value));
        Assert.Equal(new LocalDate(2023, 1, 15), value);
    }

    /// <summary>
    /// TryGetOffsetTime succeeds for a valid ISO offset time string.
    /// </summary>
    [Fact]
    public void TryGetOffsetTime_ValidIsoTime_Succeeds()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"10:30:00+02:00\"");
        Assert.True(doc.RootElement.TryGetOffsetTime(out OffsetTime value));
        Assert.Equal(new LocalTime(10, 30, 0), value.TimeOfDay);
        Assert.Equal(Offset.FromHours(2), value.Offset);
    }

    /// <summary>
    /// TryGetOffsetDate succeeds for a valid ISO offset date string.
    /// </summary>
    [Fact]
    public void TryGetOffsetDate_ValidIsoDate_Succeeds()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"2023-06-15+01:00\"");
        Assert.True(doc.RootElement.TryGetOffsetDate(out OffsetDate value));
        Assert.Equal(new LocalDate(2023, 6, 15), value.Date);
        Assert.Equal(Offset.FromHours(1), value.Offset);
    }

    /// <summary>
    /// TryGetPeriod succeeds for a valid ISO period string.
    /// </summary>
    [Fact]
    public void TryGetPeriod_ValidIsoPeriod_Succeeds()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2M3D\"");
        Assert.True(doc.RootElement.TryGetPeriod(out Period value));
        Assert.Equal(1, value.Years);
        Assert.Equal(2, value.Months);
        Assert.Equal(3, value.Days);
    }

    /// <summary>
    /// TryGetLocalDate returns false for an invalid date string.
    /// </summary>
    [Fact]
    public void TryGetLocalDate_InvalidDateString_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.False(doc.RootElement.TryGetLocalDate(out _));
    }

    /// <summary>
    /// TryGetOffsetTime returns false for an invalid time string.
    /// </summary>
    [Fact]
    public void TryGetOffsetTime_InvalidString_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"not-a-time\"");
        Assert.False(doc.RootElement.TryGetOffsetTime(out _));
    }

    /// <summary>
    /// TryGetPeriod returns false for an invalid string.
    /// </summary>
    [Fact]
    public void TryGetPeriod_InvalidString_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.False(doc.RootElement.TryGetPeriod(out _));
    }

    #endregion
}
