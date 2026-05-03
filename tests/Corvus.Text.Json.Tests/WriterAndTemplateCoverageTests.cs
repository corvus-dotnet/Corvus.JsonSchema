// <copyright file="WriterAndTemplateCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Internal;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for Utf8JsonWriter minimized paths, Utf8UriTemplate Unicode paths,
/// and PeriodBuilder indexer.
/// </summary>
public class WriterAndTemplateCoverageTests
{
    #region Utf8JsonWriter Minimized Paths

    [Fact]
    public void WriteGuidMinimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteStringValue(System.Guid.Parse("12345678-1234-1234-1234-123456789abc"));
        writer.WriteStringValue(System.Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.Equal("""["12345678-1234-1234-1234-123456789abc","aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"]""", result);
    }

    [Fact]
    public void WriteDecimalMinimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteNumberValue(123.456m);
        writer.WriteNumberValue(789.012m);
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.Equal("[123.456,789.012]", result);
    }

    [Fact]
    public void WriteFloatMinimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteNumberValue(1.5f);
        writer.WriteNumberValue(2.5f);
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.Equal("[1.5,2.5]", result);
    }

    [Fact]
    public void WriteUInt64Minimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteNumberValue(18446744073709551615UL);
        writer.WriteNumberValue(42UL);
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.Equal("[18446744073709551615,42]", result);
    }

    #endregion

    #region Utf8UriTemplate Unicode (ParseUcsChar is dead code — lookup table handles all > 0x7F)

    [Theory]
    [InlineData("caf\u00e9", true)]                  // é = U+00E9 (2-byte UTF-8: 0xC3 0xA9)
    [InlineData("\u00A0literal", true)]              // U+00A0 = non-breaking space
    [InlineData("path/\u4e2d\u6587/file", true)]    // Chinese characters
    [InlineData("\uFDF0end", true)]                  // U+FDF0
    public void Validate_UnicodeInLiterals_Valid(string template, bool expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(template);
        bool result = Utf8UriTemplate.Validate(utf8);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Validate_SupplementaryCodePoint_Valid()
    {
        // U+10000 (LINEAR B SYLLABLE B008 A) — 4-byte UTF-8: F0 90 80 80
        byte[] utf8 = [0x70, 0x61, 0x74, 0x68, 0xF0, 0x90, 0x80, 0x80]; // "path" + U+10000
        bool result = Utf8UriTemplate.Validate(utf8);
        Assert.True(result);
    }

    [Fact]
    public void Validate_HighBytesAcceptedByLookupTable()
    {
        // The IsLiteralLookup table accepts all bytes >= 0x80 individually,
        // so even a lone continuation byte is accepted as a valid literal.
        byte[] utf8 = [0x70, 0x61, 0x74, 0x68, 0x80]; // "path" + lone continuation byte
        bool result = Utf8UriTemplate.Validate(utf8);
        Assert.True(result);
    }

    #endregion

    #region PeriodBuilder Indexer

    [Theory]
    [InlineData(PeriodUnits.Years, 5)]
    [InlineData(PeriodUnits.Months, 3)]
    [InlineData(PeriodUnits.Weeks, 2)]
    [InlineData(PeriodUnits.Days, 10)]
    [InlineData(PeriodUnits.Hours, 8)]
    [InlineData(PeriodUnits.Minutes, 30)]
    [InlineData(PeriodUnits.Seconds, 45)]
    [InlineData(PeriodUnits.Milliseconds, 500)]
    [InlineData(PeriodUnits.Ticks, 1000)]
    [InlineData(PeriodUnits.Nanoseconds, 999)]
    public void PeriodBuilder_IndexerGetter_ReturnsCorrectValue(PeriodUnits unit, long value)
    {
        var builder = new PeriodBuilder();
        builder[unit] = value;
        Assert.Equal(value, builder[unit]);
    }

    [Fact]
    public void PeriodBuilder_IndexerGetter_InvalidUnit_Throws()
    {
        var builder = new PeriodBuilder();
        Assert.Throws<System.ArgumentOutOfRangeException>(() => builder[(PeriodUnits)0]);
    }

    #endregion
}
