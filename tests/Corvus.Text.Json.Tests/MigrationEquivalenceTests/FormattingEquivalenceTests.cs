// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using System.Globalization;
using Xunit;

using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V5 generated types support IFormattable, ISpanFormattable, and IUtf8SpanFormattable.
/// V4 types do not implement these interfaces, so these tests demonstrate V5-only capabilities.
/// </summary>
public class FormattingEquivalenceTests
{
    private const string PersonJson = """{"name":"Jo","age":30,"email":"jo@example.com","isActive":true,"dateOfBirth":"1990-01-15"}""";

    [Fact]
    public void V5_IFormattable_DateProperty_ShortDate()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // IFormattable: format "d" produces short date
        string result = v5.DateOfBirth.ToString("d", CultureInfo.InvariantCulture);
        Assert.Equal("01/15/1990", result);
    }

    [Fact]
    public void V5_IFormattable_DateProperty_IsoFormat()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // IFormattable: format "o" produces ISO 8601
        string result = v5.DateOfBirth.ToString("o", null);
        Assert.Equal("1990-01-15", result);
    }

    [Fact]
    public void V5_IFormattable_DateProperty_NullFormat_ReturnsCanonical()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // Null format returns the canonical JSON value
        string result = v5.DateOfBirth.ToString(null, null);
        Assert.Equal("1990-01-15", result);
    }

    [Fact]
    public void V5_IFormattable_NumberProperty_GroupedFormat()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // IFormattable: "N0" produces number with group separators
        string result = v5.Age.ToString("N0", CultureInfo.InvariantCulture);
        Assert.Equal("30", result);
    }

    [Fact]
    public void V5_IFormattable_NumberProperty_LargeValue()
    {
        string json = """{"name":"Jo","age":123456,"email":"jo@example.com"}""";
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(json);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        string result = v5.Age.ToString("N0", CultureInfo.InvariantCulture);
        Assert.Equal("123,456", result);
    }

    [Fact]
    public void V5_IFormattable_StringProperty_NullFormat_ReturnsValue()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // String properties: null format returns the string value
        string result = v5.Name.ToString(null, null);
        Assert.Equal("Jo", result);
    }

#if NET
    [Fact]
    public void V5_ISpanFormattable_NumberProperty()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // ISpanFormattable: TryFormat to Span<char>
        Span<char> buffer = stackalloc char[64];
        bool success = v5.Age.TryFormat(buffer, out int charsWritten, "N0", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal("30", buffer[..charsWritten].ToString());
    }

    [Fact]
    public void V5_ISpanFormattable_DateProperty()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // ISpanFormattable: TryFormat to Span<char> for a date
        Span<char> buffer = stackalloc char[64];
        bool success = v5.DateOfBirth.TryFormat(buffer, out int charsWritten, "d", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal("01/15/1990", buffer[..charsWritten].ToString());
    }

    [Fact]
    public void V5_IUtf8SpanFormattable_NumberProperty()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // IUtf8SpanFormattable: TryFormat to Span<byte> (UTF-8)
        Span<byte> buffer = stackalloc byte[64];
        bool success = v5.Age.TryFormat(buffer, out int bytesWritten, "N0", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal("30"u8, buffer[..bytesWritten]);
    }

    [Fact]
    public void V5_IUtf8SpanFormattable_DateProperty()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // IUtf8SpanFormattable: TryFormat to Span<byte> (UTF-8) for a date
        Span<byte> buffer = stackalloc byte[64];
        bool success = v5.DateOfBirth.TryFormat(buffer, out int bytesWritten, "o", CultureInfo.InvariantCulture);
        Assert.True(success);
        Assert.Equal("1990-01-15"u8, buffer[..bytesWritten]);
    }

    [Fact]
    public void V5_ISpanFormattable_StringProperty()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // ISpanFormattable: TryFormat to Span<char> for a string property
        Span<char> buffer = stackalloc char[64];
        bool success = v5.Name.TryFormat(buffer, out int charsWritten, default, null);
        Assert.True(success);
        Assert.Equal("Jo", buffer[..charsWritten].ToString());
    }

    [Fact]
    public void V5_IUtf8SpanFormattable_StringProperty()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // IUtf8SpanFormattable: TryFormat to Span<byte> for a string property
        Span<byte> buffer = stackalloc byte[64];
        bool success = v5.Name.TryFormat(buffer, out int bytesWritten, default, null);
        Assert.True(success);
        Assert.Equal("Jo"u8, buffer[..bytesWritten]);
    }

    [Fact]
    public void V5_IFormattable_ConsistentWithTryFormat()
    {
        using var parsedV5 = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // IFormattable ToString and ISpanFormattable TryFormat produce the same result
        string fromToString = v5.Age.ToString("N0", CultureInfo.InvariantCulture);

        Span<char> buffer = stackalloc char[64];
        bool success = v5.Age.TryFormat(buffer, out int charsWritten, "N0", CultureInfo.InvariantCulture);
        Assert.True(success);

        Assert.Equal(fromToString, buffer[..charsWritten].ToString());
    }
#endif
}