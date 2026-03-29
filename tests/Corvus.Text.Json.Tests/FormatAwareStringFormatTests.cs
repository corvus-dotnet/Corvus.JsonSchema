// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for format-aware ToString() and TryFormat() overloads on generated types
/// with string format constraints (date, date-time, time, uuid). Verifies:
///   - all standard BCL format strings delegate to the typed value;
///   - format+culture are both forwarded correctly;
///   - null/empty format falls through to the canonical raw-JSON string value;
///   - mutable types produce the same output as immutable types.
///
/// JSON values used:
///   date     "2024-03-15"               (Friday)
///   dateTime "2024-03-15T10:20:30Z"
///   time     "10:20:30Z"
///   uuid     "12345678-1234-5678-1234-567812345678"
///   duration "P1Y2M3DT4H5M6S"
/// </summary>
public class FormatAwareStringFormatTests
{
    // Canonical raw-JSON string values (without surrounding quotes)
    private const string CanonicalDate = "2024-03-15";
    private const string CanonicalDateTime = "2024-03-15T10:20:30Z";
    private const string CanonicalTime = "10:20:30Z";
    private const string CanonicalUuid = "12345678-1234-5678-1234-567812345678";
    private const string CanonicalDuration = "P1Y2M3DT4H5M6S";

    // JSON literals (with quotes) used to construct parsed documents
    private const string DateJson = "\"2024-03-15\"";
    private const string DateTimeJson = "\"2024-03-15T10:20:30Z\"";
    private const string TimeJson = "\"10:20:30Z\"";
    private const string UuidJson = "\"12345678-1234-5678-1234-567812345678\"";
    private const string DurationJson = "\"P1Y2M3DT4H5M6S\"";

    // ====================================================================
    // date — DateOnly for all overloads on .NET; NodaTime.LocalDate on net481
    // ====================================================================

#if !NET
    // On net481 ToString still delegates to NodaTime.LocalDate, so NodaTime patterns apply.
    [Theory]
    [InlineData("uuuu-MM-dd",     "2024-03-15")]       // ISO — same delimiter as canonical but via NodaTime
    [InlineData("uuuu'/'MM'/'dd", "2024/03/15")]       // literal slashes
    [InlineData("d MMMM uuuu",    "15 March 2024")]    // day month year
    [InlineData("MMMM d, uuuu",   "March 15, 2024")]   // month day, year
    [InlineData("MMMM uuuu",      "March 2024")]       // month year only
    public void DateEntity_ToString_NodaTimePattern_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Assert.Equal(expected, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#endif

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DateEntity_ToString_NullOrEmptyFormat_ReturnsCanonical(string? format)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Assert.Equal(CanonicalDate, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }

#if NET
    // On .NET, ToString delegates to DateOnly and uses BCL standard format specifiers.
    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("D", "Friday, 15 March 2024")]
    [InlineData("m", "March 15")]
    [InlineData("M", "March 15")]
    [InlineData("o", "2024-03-15")]
    [InlineData("O", "2024-03-15")]
    [InlineData("y", "2024 March")]
    [InlineData("Y", "2024 March")]
    public void DateEntity_ToString_StandardFormats_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Assert.Equal(expected, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("o", "2024-03-15")]
    [InlineData("D", "Friday, 15 March 2024")]
    public void DateEntity_Mutable_ToString_FormatsCorrectly(string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDate.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#else
    [Theory]
    [InlineData("uuuu'/'MM'/'dd", "2024/03/15")]
    [InlineData(null,             "2024-03-15")]
    public void DateEntity_Mutable_ToString_FormatsCorrectly(string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDate.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#endif

#if NET
    // All standard BCL DateOnly format specifiers with InvariantCulture.
    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("D", "Friday, 15 March 2024")]
    [InlineData("m", "March 15")]
    [InlineData("M", "March 15")]
    [InlineData("o", "2024-03-15")]
    [InlineData("O", "2024-03-15")]
    [InlineData("y", "2024 March")]
    [InlineData("Y", "2024 March")]
    public void DateEntity_TryFormatChar_InvariantCulture_StandardFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("D", "Friday, 15 March 2024")]
    [InlineData("o", "2024-03-15")]
    [InlineData("Y", "2024 March")]
    public void DateEntity_TryFormatByte_InvariantCulture_StandardFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Span<byte> dest = stackalloc byte[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    // Verify that culture is correctly forwarded: expected value computed via DateOnly directly.
    [Theory]
    [InlineData("d", "en-US")]
    [InlineData("D", "en-US")]
    [InlineData("y", "en-US")]
    [InlineData("d", "fr-FR")]
    [InlineData("D", "fr-FR")]
    [InlineData("y", "fr-FR")]
    [InlineData("d", "de-DE")]
    [InlineData("D", "de-DE")]
    [InlineData("d", "ja-JP")]
    public void DateEntity_TryFormatChar_CultureDelegation(string format, string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        string expected = new DateOnly(2024, 3, 15).ToString(format, culture);

        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Span<char> dest = stackalloc char[200];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, culture));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("")]
    public void DateEntity_TryFormatChar_EmptyFormat_ReturnsCanonical(string format)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(CanonicalDate, dest[..n].ToString());
    }

    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("o", "2024-03-15")]
    [InlineData("D", "Friday, 15 March 2024")]
    public void DateEntity_Mutable_TryFormatChar_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDate.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[100];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("o", "2024-03-15")]
    public void DateEntity_Mutable_TryFormatByte_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDate.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[100];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }
#endif

    // ====================================================================
    // date-time — DateTimeOffset for all overloads on .NET; NodaTime.OffsetDateTime on net481
    // ====================================================================

#if !NET
    // On net481 ToString still delegates to NodaTime.OffsetDateTime, so NodaTime patterns apply.
    [Theory]
    [InlineData("uuuu'/'MM'/'dd HH:mm:ss", "2024/03/15 10:20:30")]   // date+time with literal slashes
    [InlineData("d MMMM uuuu HH:mm",        "15 March 2024 10:20")]  // long date + short time
    [InlineData("HH:mm:ss",                  "10:20:30")]             // time portion only
    public void DateTimeEntity_ToString_NodaTimePattern_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Assert.Equal(expected, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#endif

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DateTimeEntity_ToString_NullOrEmptyFormat_ReturnsCanonical(string? format)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Assert.Equal(CanonicalDateTime, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }

#if NET
    // On .NET, ToString delegates to DateTimeOffset and uses BCL standard format specifiers.
    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("D", "Friday, 15 March 2024")]
    [InlineData("f", "Friday, 15 March 2024 10:20")]
    [InlineData("F", "Friday, 15 March 2024 10:20:30")]
    [InlineData("g", "03/15/2024 10:20")]
    [InlineData("G", "03/15/2024 10:20:30")]
    [InlineData("m", "March 15")]
    [InlineData("M", "March 15")]
    [InlineData("o", "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("O", "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("r", "Fri, 15 Mar 2024 10:20:30 GMT")]
    [InlineData("R", "Fri, 15 Mar 2024 10:20:30 GMT")]
    [InlineData("s", "2024-03-15T10:20:30")]
    [InlineData("t", "10:20")]
    [InlineData("T", "10:20:30")]
    [InlineData("u", "2024-03-15 10:20:30Z")]
    [InlineData("y", "2024 March")]
    [InlineData("Y", "2024 March")]
    public void DateTimeEntity_ToString_StandardFormats_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Assert.Equal(expected, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("G", "03/15/2024 10:20:30")]
    [InlineData("o", "2024-03-15T10:20:30.0000000+00:00")]
    public void DateTimeEntity_Mutable_ToString_FormatsCorrectly(string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDateTime.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#else
    [Theory]
    [InlineData("uuuu'/'MM'/'dd HH:mm:ss", "2024/03/15 10:20:30")]
    [InlineData(null,                        "2024-03-15T10:20:30Z")]
    public void DateTimeEntity_Mutable_ToString_FormatsCorrectly(string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDateTime.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#endif

#if NET
    // All supported standard BCL DateTimeOffset format specifiers with InvariantCulture.
    [Theory]
    [InlineData("d", "03/15/2024")]
    [InlineData("D", "Friday, 15 March 2024")]
    [InlineData("f", "Friday, 15 March 2024 10:20")]
    [InlineData("F", "Friday, 15 March 2024 10:20:30")]
    [InlineData("g", "03/15/2024 10:20")]
    [InlineData("G", "03/15/2024 10:20:30")]
    [InlineData("m", "March 15")]
    [InlineData("M", "March 15")]
    [InlineData("o", "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("O", "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("r", "Fri, 15 Mar 2024 10:20:30 GMT")]
    [InlineData("R", "Fri, 15 Mar 2024 10:20:30 GMT")]
    [InlineData("s", "2024-03-15T10:20:30")]
    [InlineData("t", "10:20")]
    [InlineData("T", "10:20:30")]
    [InlineData("u", "2024-03-15 10:20:30Z")]
    [InlineData("y", "2024 March")]
    [InlineData("Y", "2024 March")]
    public void DateTimeEntity_TryFormatChar_InvariantCulture_StandardFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Span<char> dest = stackalloc char[200];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, dest[..n].ToString());
    }

    // Culture-invariant formats produce identical output regardless of the supplied culture.
    [Theory]
    [InlineData("o", "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("O", "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("r", "Fri, 15 Mar 2024 10:20:30 GMT")]
    [InlineData("R", "Fri, 15 Mar 2024 10:20:30 GMT")]
    [InlineData("s", "2024-03-15T10:20:30")]
    [InlineData("u", "2024-03-15 10:20:30Z")]
    public void DateTimeEntity_TryFormatChar_CultureInvariantFormats_SameForAllCultures(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Span<char> dest = stackalloc char[200];
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("en-US"), new CultureInfo("fr-FR"), new CultureInfo("de-DE") })
        {
            Assert.True(doc.RootElement.TryFormat(dest, out int n, format, culture));
            Assert.Equal(expected, dest[..n].ToString());
        }
    }

    // Culture-sensitive formats — expected computed at test time via DateTimeOffset directly.
    [Theory]
    [InlineData("d", "en-US")]
    [InlineData("D", "en-US")]
    [InlineData("f", "en-US")]
    [InlineData("d", "fr-FR")]
    [InlineData("D", "fr-FR")]
    [InlineData("f", "fr-FR")]
    [InlineData("d", "de-DE")]
    [InlineData("f", "de-DE")]
    public void DateTimeEntity_TryFormatChar_CultureDelegation(string format, string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        var dto = new DateTimeOffset(2024, 3, 15, 10, 20, 30, TimeSpan.Zero);
        string expected = dto.ToString(format, culture);

        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Span<char> dest = stackalloc char[300];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, culture));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("o",  "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("r",  "Fri, 15 Mar 2024 10:20:30 GMT")]
    [InlineData("s",  "2024-03-15T10:20:30")]
    [InlineData("d",  "03/15/2024")]
    public void DateTimeEntity_TryFormatByte_InvariantCulture_StandardFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Span<byte> dest = stackalloc byte[200];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [Theory]
    [InlineData("")]
    public void DateTimeEntity_TryFormatChar_EmptyFormat_ReturnsCanonical(string format)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(CanonicalDateTime, dest[..n].ToString());
    }

    [Theory]
    [InlineData("o",  "2024-03-15T10:20:30.0000000+00:00")]
    [InlineData("s",  "2024-03-15T10:20:30")]
    [InlineData("d",  "03/15/2024")]
    [InlineData("G",  "03/15/2024 10:20:30")]
    public void DateTimeEntity_Mutable_TryFormatChar_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonDateTime>.Parse(DateTimeJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDateTime.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[200];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, dest[..n].ToString());
    }
#endif

    // ====================================================================
    // time — TimeOnly for all overloads on .NET; NodaTime.OffsetTime on net481
    // ====================================================================

#if !NET
    // On net481 ToString still delegates to NodaTime.OffsetTime, so NodaTime patterns apply.
    [Theory]
    [InlineData("HH:mm:ss",    "10:20:30")]   // time only
    [InlineData("HH'.'mm'.'ss", "10.20.30")]  // literal dots
    [InlineData("HH:mm",       "10:20")]       // hours and minutes
    [InlineData("HH",          "10")]          // hours only
    public void TimeEntity_ToString_NodaTimePattern_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Assert.Equal(expected, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#endif

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TimeEntity_ToString_NullOrEmptyFormat_ReturnsCanonical(string? format)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Assert.Equal(CanonicalTime, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }

#if NET
    // On .NET, ToString delegates to TimeOnly and uses BCL standard format specifiers.
    [Theory]
    [InlineData("t", "10:20")]
    [InlineData("T", "10:20:30")]
    [InlineData("o", "10:20:30.0000000")]
    [InlineData("O", "10:20:30.0000000")]
    [InlineData("r", "10:20:30")]
    [InlineData("R", "10:20:30")]
    public void TimeEntity_ToString_StandardFormats_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Assert.Equal(expected, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("T", "10:20:30")]
    [InlineData("o", "10:20:30.0000000")]
    public void TimeEntity_Mutable_ToString_FormatsCorrectly(string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonTime.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#else
    [Theory]
    [InlineData("HH'.'mm'.'ss", "10.20.30")]
    [InlineData(null,            "10:20:30Z")]
    public void TimeEntity_Mutable_ToString_FormatsCorrectly(string? format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonTime.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
#endif

#if NET
    // All standard BCL TimeOnly format specifiers with InvariantCulture.
    [Theory]
    [InlineData("t", "10:20")]
    [InlineData("T", "10:20:30")]
    [InlineData("o", "10:20:30.0000000")]
    [InlineData("O", "10:20:30.0000000")]
    [InlineData("r", "10:20:30")]
    [InlineData("R", "10:20:30")]
    public void TimeEntity_TryFormatChar_InvariantCulture_StandardFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, dest[..n].ToString());
    }

    // Culture-invariant TimeOnly formats — same output for any culture.
    [Theory]
    [InlineData("o", "10:20:30.0000000")]
    [InlineData("O", "10:20:30.0000000")]
    [InlineData("r", "10:20:30")]
    [InlineData("R", "10:20:30")]
    public void TimeEntity_TryFormatChar_CultureInvariantFormats_SameForAllCultures(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Span<char> dest = stackalloc char[100];
        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("en-US"), new CultureInfo("fr-FR"), new CultureInfo("de-DE") })
        {
            Assert.True(doc.RootElement.TryFormat(dest, out int n, format, culture));
            Assert.Equal(expected, dest[..n].ToString());
        }
    }

    // Culture-sensitive time formats — expected computed via TimeOnly directly.
    [Theory]
    [InlineData("t", "en-US")]
    [InlineData("T", "en-US")]
    [InlineData("t", "fr-FR")]
    [InlineData("T", "fr-FR")]
    [InlineData("t", "de-DE")]
    [InlineData("T", "de-DE")]
    public void TimeEntity_TryFormatChar_CultureDelegation(string format, string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        string expected = new TimeOnly(10, 20, 30).ToString(format, culture);

        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, culture));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("o", "10:20:30.0000000")]
    [InlineData("r", "10:20:30")]
    [InlineData("T", "10:20:30")]
    public void TimeEntity_TryFormatByte_InvariantCulture_StandardFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Span<byte> dest = stackalloc byte[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [Theory]
    [InlineData("")]
    public void TimeEntity_TryFormatChar_EmptyFormat_ReturnsCanonical(string format)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(CanonicalTime, dest[..n].ToString());
    }

    [Theory]
    [InlineData("T", "10:20:30")]
    [InlineData("o", "10:20:30.0000000")]
    public void TimeEntity_Mutable_TryFormatChar_InvariantCulture(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonTime>.Parse(TimeJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonTime.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[100];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, dest[..n].ToString());
    }
#endif

    // ====================================================================
    // uuid — Guid for all overloads (Guid.ToString works on all frameworks)
    // ====================================================================

    // All five standard Guid format specifiers.
    [Theory]
    [InlineData("D", "12345678-1234-5678-1234-567812345678")]    // hyphenated (default)
    [InlineData("N", "12345678123456781234567812345678")]         // no separators
    [InlineData("B", "{12345678-1234-5678-1234-567812345678}")]  // braces
    [InlineData("P", "(12345678-1234-5678-1234-567812345678)")]  // parens
    [InlineData("X", "{0x12345678,0x1234,0x5678,{0x12,0x34,0x56,0x78,0x12,0x34,0x56,0x78}}")] // hex
    public void UuidEntity_ToString_AllGuidFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        Assert.Equal(expected, doc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UuidEntity_ToString_NullOrEmptyFormat_ReturnsCanonical(string? format)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        Assert.Equal(CanonicalUuid, doc.RootElement.ToString(format, null));
    }

    [Theory]
    [InlineData("D", "12345678-1234-5678-1234-567812345678")]
    [InlineData("N", "12345678123456781234567812345678")]
    [InlineData("B", "{12345678-1234-5678-1234-567812345678}")]
    [InlineData("P", "(12345678-1234-5678-1234-567812345678)")]
    [InlineData("X", "{0x12345678,0x1234,0x5678,{0x12,0x34,0x56,0x78,0x12,0x34,0x56,0x78}}")]
    public void UuidEntity_Mutable_ToString_AllGuidFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUuid.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(expected, mutableDoc.RootElement.ToString(format, null));
    }

#if NET
    [Theory]
    [InlineData("D", "12345678-1234-5678-1234-567812345678")]
    [InlineData("N", "12345678123456781234567812345678")]
    [InlineData("B", "{12345678-1234-5678-1234-567812345678}")]
    [InlineData("P", "(12345678-1234-5678-1234-567812345678)")]
    [InlineData("X", "{0x12345678,0x1234,0x5678,{0x12,0x34,0x56,0x78,0x12,0x34,0x56,0x78}}")]
    public void UuidEntity_TryFormatChar_AllGuidFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("D", "12345678-1234-5678-1234-567812345678")]
    [InlineData("N", "12345678123456781234567812345678")]
    [InlineData("B", "{12345678-1234-5678-1234-567812345678}")]
    [InlineData("P", "(12345678-1234-5678-1234-567812345678)")]
    [InlineData("X", "{0x12345678,0x1234,0x5678,{0x12,0x34,0x56,0x78,0x12,0x34,0x56,0x78}}")]
    public void UuidEntity_TryFormatByte_AllGuidFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        Span<byte> dest = stackalloc byte[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [Theory]
    [InlineData("")]
    public void UuidEntity_TryFormatChar_EmptyFormat_ReturnsCanonical(string format)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        Span<char> dest = stackalloc char[100];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(CanonicalUuid, dest[..n].ToString());
    }

    [Theory]
    [InlineData("D", "12345678-1234-5678-1234-567812345678")]
    [InlineData("N", "12345678123456781234567812345678")]
    [InlineData("X", "{0x12345678,0x1234,0x5678,{0x12,0x34,0x56,0x78,0x12,0x34,0x56,0x78}}")]
    public void UuidEntity_Mutable_TryFormatChar_AllGuidFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUuid.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[100];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("D", "12345678-1234-5678-1234-567812345678")]
    [InlineData("N", "12345678123456781234567812345678")]
    public void UuidEntity_Mutable_TryFormatByte_GuidFormats(string format, string expected)
    {
        using var doc = ParsedJsonDocument<JsonUuid>.Parse(UuidJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonUuid.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<byte> dest = stackalloc byte[100];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, format, null));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }
#endif

    // ====================================================================
    // Exotic calendars — Japanese Imperial and Hebrew
    //
    // These tests verify that the format-aware path correctly forwards
    // the IFormatProvider (including its Calendar) to the BCL DateOnly type.
    //
    // Only TryFormat is tested here because the ToString path delegates to
    // NodaTime, which uses its own ISO-based calendar system and is not
    // affected by the culture's Calendar property.
    // ====================================================================

#if NET
    // --- Hebrew calendar ---
    // 2024-03-15 is in Hebrew year 5784 which IS a leap year (13 months).
    // The date falls in month 7 = Adar II — the intercalated extra month
    // that only exists in 13-month years.
    [Theory]
    [InlineData("d")]   // short date — includes Hebrew month number
    [InlineData("y")]   // year/month — spells out the Hebrew month name
    public void DateEntity_TryFormatChar_HebrewCalendar_AdarIILeapMonth(string format)
    {
        var heIL = new CultureInfo("he-IL");
        heIL.DateTimeFormat.Calendar = new System.Globalization.HebrewCalendar();
        // Expected computed via BCL directly — output is complex Hebrew text
        string expected = new DateOnly(2024, 3, 15).ToString(format, heIL);

        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Span<char> dest = stackalloc char[200];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, heIL));
        Assert.Equal(expected, dest[..n].ToString());
    }

    [Theory]
    [InlineData("d")]
    [InlineData("y")]
    public void DateEntity_TryFormatByte_HebrewCalendar_AdarIILeapMonth(string format)
    {
        var heIL = new CultureInfo("he-IL");
        heIL.DateTimeFormat.Calendar = new System.Globalization.HebrewCalendar();

        // NOTE: DateOnly.ToString() and DateOnly.TryFormat(Span<byte>) can produce subtly different
        // character sequences for Hebrew calendar dates (e.g. apostrophe vs. double-quote separators).
        // We therefore derive the expected value from DateOnly.TryFormat(Span<byte>) directly, which
        // proves that our generated code correctly delegates to the underlying BCL method.
        var refDate = new DateOnly(2024, 3, 15);
        byte[] refDest = new byte[500];
        Assert.True(refDate.TryFormat(refDest.AsSpan(), out int refN, format, heIL));
        string expected = System.Text.Encoding.UTF8.GetString(refDest, 0, refN);

        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        Span<byte> dest = stackalloc byte[200];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, heIL));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(dest[..n]));
    }

    [Fact]
    public void DateEntity_Mutable_TryFormatChar_HebrewCalendar_AdarIILeapMonth()
    {
        var heIL = new CultureInfo("he-IL");
        heIL.DateTimeFormat.Calendar = new System.Globalization.HebrewCalendar();
        string expected = new DateOnly(2024, 3, 15).ToString("d", heIL);

        using var doc = ParsedJsonDocument<JsonDate>.Parse(DateJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDate.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Span<char> dest = stackalloc char[200];
        Assert.True(mutableDoc.RootElement.TryFormat(dest, out int n, "d", heIL));
        Assert.Equal(expected, dest[..n].ToString());
    }

    // --- Japanese Imperial calendar ---
    // Era boundary: 2019-04-30 = last day of Heisei, 2019-05-01 = first day of Reiwa.
    // The "D" format for the first year of an era uses "元年" (gan-nen) rather than
    // "1年" — a historically significant edge case that was a source of bugs in early
    // .NET Core releases.
    [Theory]
    [InlineData("1989-01-07", "D")]  // Shōwa 64 (last day)
    [InlineData("2019-04-30", "D")]  // Heisei 31 (last day)
    [InlineData("2019-05-01", "D")]  // Reiwa 元年 (first day — "元年" special case)
    [InlineData("2019-05-01", "d")]  // Short date in Reiwa era
    [InlineData("2024-03-15", "D")]  // A normal date well within Reiwa
    public void DateEntity_TryFormatChar_JapaneseImperialCalendar_EraBoundary(string isoDate, string format)
    {
        var jaJP = new CultureInfo("ja-JP");
        jaJP.DateTimeFormat.Calendar = new System.Globalization.JapaneseCalendar();
        string expected = DateOnly.ParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                               .ToString(format, jaJP);

        using var doc = ParsedJsonDocument<JsonDate>.Parse($"\"{isoDate}\"");
        Span<char> dest = stackalloc char[200];
        Assert.True(doc.RootElement.TryFormat(dest, out int n, format, jaJP));
        Assert.Equal(expected, dest[..n].ToString());
    }

    // A date before the Meiji era (1868) is outside the range of JapaneseCalendar.
    // BCL's DateOnly.TryFormat throws ArgumentOutOfRangeException in this case.
    // This test documents that our format-aware code propagates that exception
    // rather than silently returning an incorrect result or falling through to canonical.
    [Fact]
    public void DateEntity_TryFormatChar_JapaneseImperialCalendar_PreMeijiDate_ThrowsArgumentOutOfRange()
    {
        var jaJP = new CultureInfo("ja-JP");
        jaJP.DateTimeFormat.Calendar = new System.Globalization.JapaneseCalendar();

        using var doc = ParsedJsonDocument<JsonDate>.Parse("\"1200-06-15\"");
        JsonDate entity = doc.RootElement;

        // Span<char> cannot be captured in a lambda, so we use try/catch directly.
        char[] buffer = new char[200];
        bool threw = false;
        try
        {
            entity.TryFormat(buffer.AsSpan(), out _, "D", jaJP);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }
        Assert.True(threw, "Expected ArgumentOutOfRangeException for a pre-Meiji date with JapaneseCalendar");
    }
#endif

    // ====================================================================
    // duration — no format handler; all overloads always fall through to canonical
    // ====================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("G")]
    [InlineData("c")]
    [InlineData("g")]
    public void DurationEntity_ToString_AnyFormat_AlwaysReturnsCanonical(string? format)
    {
        using var doc = ParsedJsonDocument<JsonDuration>.Parse(DurationJson);
        Assert.Equal(CanonicalDuration, doc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("G")]
    public void DurationEntity_Mutable_ToString_AnyFormat_AlwaysReturnsCanonical(string? format)
    {
        using var doc = ParsedJsonDocument<JsonDuration>.Parse(DurationJson);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonDuration.Mutable> mutableDoc = doc.RootElement.CreateBuilder(workspace);
        Assert.Equal(CanonicalDuration, mutableDoc.RootElement.ToString(format, CultureInfo.InvariantCulture));
    }
}
