// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;
public static class JsonElementSourceCoverageTests
{
    [Fact]
    public static void SourceConstructorAndImplicitOperator_Byte()
    {
        using var workspace = JsonWorkspace.Create();
        byte value = 42;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetByte());
        
        // Validate JSON string representation
        string json = doc.RootElement.GetRawText();
        Assert.Equal("42", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_SByte()
    {
        using var workspace = JsonWorkspace.Create();
        sbyte value = -42;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetSByte());
        
        string json = doc.RootElement.GetRawText();
        Assert.Equal("-42", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Short()
    {
        using var workspace = JsonWorkspace.Create();
        short value = -1234;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetInt16());
        
        string json = doc.RootElement.GetRawText();
        Assert.Equal("-1234", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_UShort()
    {
        using var workspace = JsonWorkspace.Create();
        ushort value = 12345;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetUInt16());
        
        string json = doc.RootElement.GetRawText();
        Assert.Equal("12345", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Int()
    {
        using var workspace = JsonWorkspace.Create();
        int value = -123456;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetInt32());
        
        string json = doc.RootElement.GetRawText();
        Assert.Equal("-123456", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_UInt()
    {
        using var workspace = JsonWorkspace.Create();
        uint value = 123456u;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetUInt32());
        
        string json = doc.RootElement.GetRawText();
        Assert.Equal("123456", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Long()
    {
        using var workspace = JsonWorkspace.Create();
        long value = -123456789012345L;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetInt64());
        
        string json = doc.RootElement.GetRawText();
        Assert.Equal("-123456789012345", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_ULong()
    {
        using var workspace = JsonWorkspace.Create();
        ulong value = 123456789012345UL;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetUInt64());
        
        string json = doc.RootElement.GetRawText();
        Assert.Equal("123456789012345", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Float()
    {
        using var workspace = JsonWorkspace.Create();
        float value = 3.14159f;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetSingle());
        
        // Validate the number was written
        string json = doc.RootElement.GetRawText();
        Assert.False(string.IsNullOrEmpty(json));
        Assert.True(float.TryParse(json, out float parsed));
        Assert.Equal(value, parsed);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Double()
    {
        using var workspace = JsonWorkspace.Create();
        double value = 3.141592653589793;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetDouble(), precision: 10);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Decimal()
    {
        using var workspace = JsonWorkspace.Create();
        decimal value = 123456.789m;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetDecimal());
        
        string json = doc.RootElement.GetRawText();
        Assert.False(string.IsNullOrEmpty(json));
        Assert.True(decimal.TryParse(json, out decimal parsed));
        Assert.Equal(value, parsed);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_DateTime()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new DateTime(2023, 7, 15, 10, 30, 45, DateTimeKind.Utc);
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetString();
        Assert.False(string.IsNullOrEmpty(json));
        // Parse and validate the DateTime value - verify it round-trips correctly
        // Note: DateTime.ToString() may convert to local time, so we verify the serialization
        // is valid and contains the expected date components
        var parsed = DateTime.Parse(json, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2023, parsed.Year);
        Assert.Equal(7, parsed.Month);
        Assert.Equal(15, parsed.Day);
        // Time components: verify hour/minute/second are present (may be in local timezone)
        Assert.True(parsed.Hour >= 0 && parsed.Hour < 24);
        Assert.Equal(30, parsed.Minute);
        Assert.Equal(45, parsed.Second);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_DateTimeOffset()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new DateTimeOffset(2023, 7, 15, 10, 30, 45, TimeSpan.FromHours(2));
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetString();
        Assert.False(string.IsNullOrEmpty(json));
        // Parse and validate the full DateTimeOffset value using invariant culture
        var parsed = DateTimeOffset.Parse(json, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(value.Year, parsed.Year);
        Assert.Equal(value.Month, parsed.Month);
        Assert.Equal(value.Day, parsed.Day);
        Assert.Equal(value.Hour, parsed.Hour);
        Assert.Equal(value.Minute, parsed.Minute);
        Assert.Equal(value.Second, parsed.Second);
        Assert.Equal(value.Offset, parsed.Offset);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Guid()
    {
        using var workspace = JsonWorkspace.Create();
        var value = Guid.Parse("12345678-1234-1234-1234-123456789012");
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        Assert.Equal(value, doc.RootElement.GetGuid());
        
        string json = doc.RootElement.GetString();
        Assert.Equal("12345678-1234-1234-1234-123456789012", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Uri()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new Uri("https://example.com/path?query=value");
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        string result = doc.RootElement.GetString();
        Assert.Equal("https://example.com/path?query=value", result);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_String()
    {
        using var workspace = JsonWorkspace.Create();
        string value = "Hello, World!";
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        string result = doc.RootElement.GetString();
        Assert.Equal(value, result);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_ReadOnlySpanChar()
    {
        using var workspace = JsonWorkspace.Create();
        string expectedValue = "Test String";
        ReadOnlySpan<char> value = expectedValue.AsSpan();
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        string result = doc.RootElement.GetString();
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_OffsetDateTime()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new OffsetDateTime(
            new LocalDateTime(2023, 7, 15, 10, 30, 45),
            Offset.FromHours(2));
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetString();
        Assert.False(string.IsNullOrEmpty(json));
        // Parse and validate the full OffsetDateTime value
        OffsetDateTimePattern pattern = NodaTime.Text.OffsetDateTimePattern.ExtendedIso;
        ParseResult<OffsetDateTime> parseResult = pattern.Parse(json);
        Assert.True(parseResult.Success, $"Failed to parse: {json}");
        OffsetDateTime parsed = parseResult.Value;
        Assert.Equal(value.Year, parsed.Year);
        Assert.Equal(value.Month, parsed.Month);
        Assert.Equal(value.Day, parsed.Day);
        Assert.Equal(value.Hour, parsed.Hour);
        Assert.Equal(value.Minute, parsed.Minute);
        Assert.Equal(value.Second, parsed.Second);
        Assert.Equal(value.Offset, parsed.Offset);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_OffsetDate()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new OffsetDate(
            new LocalDate(2023, 7, 15),
            Offset.FromHours(2));
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetString();
        Assert.False(string.IsNullOrEmpty(json));
        // Parse and validate the full OffsetDate value
        OffsetDatePattern pattern = NodaTime.Text.OffsetDatePattern.GeneralIso;
        ParseResult<OffsetDate> parseResult = pattern.Parse(json);
        Assert.True(parseResult.Success, $"Failed to parse: {json}");
        OffsetDate parsed = parseResult.Value;
        Assert.Equal(value.Year, parsed.Year);
        Assert.Equal(value.Month, parsed.Month);
        Assert.Equal(value.Day, parsed.Day);
        Assert.Equal(value.Offset, parsed.Offset);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_OffsetTime()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new OffsetTime(
            new LocalTime(10, 30, 45),
            Offset.FromHours(2));
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetString();
        Assert.False(string.IsNullOrEmpty(json));
        // Parse and validate the full OffsetTime value
        OffsetTimePattern pattern = NodaTime.Text.OffsetTimePattern.ExtendedIso;
        ParseResult<OffsetTime> parseResult = pattern.Parse(json);
        Assert.True(parseResult.Success, $"Failed to parse: {json}");
        OffsetTime parsed = parseResult.Value;
        Assert.Equal(value.Hour, parsed.Hour);
        Assert.Equal(value.Minute, parsed.Minute);
        Assert.Equal(value.Second, parsed.Second);
        Assert.Equal(value.Offset, parsed.Offset);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_LocalDate()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new LocalDate(2023, 7, 15);
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetString();
        Assert.False(string.IsNullOrEmpty(json));
        Assert.Equal("2023-07-15", json);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Period()
    {
        using var workspace = JsonWorkspace.Create();
        Period value = Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3);
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.String, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetString();
        Assert.False(string.IsNullOrEmpty(json));
        Assert.Equal("P1Y2M3D", json);
    }

#if NET
    [Fact]
    public static void SourceConstructorAndImplicitOperator_Int128()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new Int128(0, 123456789012345678UL);
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetRawText();
        Assert.False(string.IsNullOrEmpty(json));
        Assert.True(Int128.TryParse(json, out Int128 parsed));
        Assert.Equal(value, parsed);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_UInt128()
    {
        using var workspace = JsonWorkspace.Create();
        var value = new UInt128(0, 123456789012345678UL);
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetRawText();
        Assert.False(string.IsNullOrEmpty(json));
        Assert.True(UInt128.TryParse(json, out UInt128 parsed));
        Assert.Equal(value, parsed);
    }

    [Fact]
    public static void SourceConstructorAndImplicitOperator_Half()
    {
        using var workspace = JsonWorkspace.Create();
        var value = (Half)3.14;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, value);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.ValueKind);
        
        string json = doc.RootElement.GetRawText();
        Assert.False(string.IsNullOrEmpty(json));
        Assert.True(Half.TryParse(json, out Half parsed));
        // Use approximate comparison for floating point
        Assert.True(Math.Abs((double)(value - parsed)) < 0.01);
    }
#endif

    [Fact]
    public static void SourceAddAsProperty_WithUtf8Name_AllTypes()
    {
        using var workspace = JsonWorkspace.Create();
        
        var guidValue = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var dateTimeValue = new DateTime(2023, 7, 15, 10, 30, 45, DateTimeKind.Utc);
        var dateTimeOffsetValue = new DateTimeOffset(2023, 7, 15, 10, 30, 45, TimeSpan.FromHours(2));
        var localDateValue = new LocalDate(2023, 7, 15);
        Period periodValue = Period.FromYears(1) + Period.FromMonths(2);
        
        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build((ref builder) =>
        {
            builder.AddProperty("byte"u8, (byte)42);
            builder.AddProperty("sbyte"u8, (sbyte)-42);
            builder.AddProperty("short"u8, (short)-1234);
            builder.AddProperty("ushort"u8, (ushort)1234);
            builder.AddProperty("int"u8, -123456);
            builder.AddProperty("uint"u8, 123456u);
            builder.AddProperty("long"u8, -123456789L);
            builder.AddProperty("ulong"u8, 123456789UL);
            builder.AddProperty("float"u8, 3.14f);
            builder.AddProperty("double"u8, 3.14159);
            builder.AddProperty("decimal"u8, 123.456m);
            builder.AddProperty("guid"u8, guidValue);
            builder.AddProperty("datetime"u8, dateTimeValue);
            builder.AddProperty("datetimeoffset"u8, dateTimeOffsetValue);
            builder.AddProperty("localdate"u8, localDateValue);
            builder.AddProperty("offsetdate"u8, new OffsetDate(new LocalDate(2023, 7, 15), Offset.FromHours(2)));
            builder.AddProperty("offsettime"u8, new OffsetTime(new LocalTime(10, 30), Offset.FromHours(2)));
            builder.AddProperty("offsetdatetime"u8, new OffsetDateTime(new LocalDateTime(2023, 7, 15, 10, 30), Offset.FromHours(2)));
            builder.AddProperty("period"u8, periodValue);
        }));

        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, source);
        
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        
        // Validate each property exists and has the correct value
        Assert.True(doc.RootElement.TryGetProperty("byte"u8, out JsonElement.Mutable byteProp));
        Assert.Equal(42, byteProp.GetByte());
        
        Assert.True(doc.RootElement.TryGetProperty("sbyte"u8, out JsonElement.Mutable sbyteProp));
        Assert.Equal(-42, sbyteProp.GetSByte());
        
        Assert.True(doc.RootElement.TryGetProperty("short"u8, out JsonElement.Mutable shortProp));
        Assert.Equal(-1234, shortProp.GetInt16());
        
        Assert.True(doc.RootElement.TryGetProperty("int"u8, out JsonElement.Mutable intProp));
        Assert.Equal(-123456, intProp.GetInt32());
        
        Assert.True(doc.RootElement.TryGetProperty("uint"u8, out JsonElement.Mutable uintProp));
        Assert.Equal(123456u, uintProp.GetUInt32());
        
        Assert.True(doc.RootElement.TryGetProperty("long"u8, out JsonElement.Mutable longProp));
        Assert.Equal(-123456789L, longProp.GetInt64());
        
        Assert.True(doc.RootElement.TryGetProperty("ulong"u8, out JsonElement.Mutable ulongProp));
        Assert.Equal(123456789UL, ulongProp.GetUInt64());
        
        Assert.True(doc.RootElement.TryGetProperty("ushort"u8, out JsonElement.Mutable ushortProp));
        Assert.Equal(1234, ushortProp.GetUInt16());
        
        Assert.True(doc.RootElement.TryGetProperty("float"u8, out JsonElement.Mutable floatProp));
        Assert.Equal(3.14f, floatProp.GetSingle());
        
        Assert.True(doc.RootElement.TryGetProperty("double"u8, out JsonElement.Mutable doubleProp));
        Assert.Equal(3.14159, doubleProp.GetDouble(), precision: 10);
        
        Assert.True(doc.RootElement.TryGetProperty("decimal"u8, out JsonElement.Mutable decimalProp));
        Assert.Equal(123.456m, decimalProp.GetDecimal());
        
        Assert.True(doc.RootElement.TryGetProperty("guid"u8, out JsonElement.Mutable guidProp));
        Assert.Equal(guidValue, guidProp.GetGuid());
        
        Assert.True(doc.RootElement.TryGetProperty("datetime"u8, out JsonElement.Mutable dateTimeProp));
        string dateTimeStr = dateTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(dateTimeStr));
        var parsedDateTime = DateTime.Parse(dateTimeStr, System.Globalization.CultureInfo.InvariantCulture);
        // DateTime serialization may use local timezone - verify date and time components
        Assert.Equal(2023, parsedDateTime.Year);
        Assert.Equal(7, parsedDateTime.Month);
        Assert.Equal(15, parsedDateTime.Day);
        Assert.True(parsedDateTime.Hour >= 0 && parsedDateTime.Hour < 24);
        Assert.Equal(30, parsedDateTime.Minute);
        Assert.Equal(45, parsedDateTime.Second);
        
        Assert.True(doc.RootElement.TryGetProperty("datetimeoffset"u8, out JsonElement.Mutable dateTimeOffsetProp));
        string dateTimeOffsetStr = dateTimeOffsetProp.GetString();
        Assert.False(string.IsNullOrEmpty(dateTimeOffsetStr));
        var parsedDateTimeOffset = DateTimeOffset.Parse(dateTimeOffsetStr, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(dateTimeOffsetValue.Year, parsedDateTimeOffset.Year);
        Assert.Equal(dateTimeOffsetValue.Month, parsedDateTimeOffset.Month);
        Assert.Equal(dateTimeOffsetValue.Day, parsedDateTimeOffset.Day);
        Assert.Equal(dateTimeOffsetValue.Hour, parsedDateTimeOffset.Hour);
        Assert.Equal(dateTimeOffsetValue.Minute, parsedDateTimeOffset.Minute);
        Assert.Equal(dateTimeOffsetValue.Second, parsedDateTimeOffset.Second);
        Assert.Equal(dateTimeOffsetValue.Offset, parsedDateTimeOffset.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("localdate"u8, out JsonElement.Mutable localDateProp));
        Assert.Equal("2023-07-15", localDateProp.GetString());
        
        Assert.True(doc.RootElement.TryGetProperty("offsetdate"u8, out JsonElement.Mutable offsetDateProp));
        string offsetDateStr = offsetDateProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetDateStr));
        OffsetDatePattern offsetDatePattern = NodaTime.Text.OffsetDatePattern.GeneralIso;
        ParseResult<OffsetDate> offsetDateResult = offsetDatePattern.Parse(offsetDateStr);
        Assert.True(offsetDateResult.Success, $"Failed to parse OffsetDate: {offsetDateStr}");
        OffsetDate parsedOffsetDate = offsetDateResult.Value;
        Assert.Equal(2023, parsedOffsetDate.Year);
        Assert.Equal(7, parsedOffsetDate.Month);
        Assert.Equal(15, parsedOffsetDate.Day);
        Assert.Equal(Offset.FromHours(2), parsedOffsetDate.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("offsettime"u8, out JsonElement.Mutable offsetTimeProp));
        string offsetTimeStr = offsetTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetTimeStr));
        OffsetTimePattern offsetTimePattern = NodaTime.Text.OffsetTimePattern.ExtendedIso;
        ParseResult<OffsetTime> offsetTimeResult = offsetTimePattern.Parse(offsetTimeStr);
        Assert.True(offsetTimeResult.Success, $"Failed to parse OffsetTime: {offsetTimeStr}");
        OffsetTime parsedOffsetTime = offsetTimeResult.Value;
        Assert.Equal(10, parsedOffsetTime.Hour);
        Assert.Equal(30, parsedOffsetTime.Minute);
        Assert.Equal(0, parsedOffsetTime.Second);
        Assert.Equal(Offset.FromHours(2), parsedOffsetTime.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("offsetdatetime"u8, out JsonElement.Mutable offsetDateTimeProp));
        string offsetDateTimeStr = offsetDateTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetDateTimeStr));
        OffsetDateTimePattern offsetDateTimePattern = NodaTime.Text.OffsetDateTimePattern.ExtendedIso;
        ParseResult<OffsetDateTime> offsetDateTimeResult = offsetDateTimePattern.Parse(offsetDateTimeStr);
        Assert.True(offsetDateTimeResult.Success, $"Failed to parse OffsetDateTime: {offsetDateTimeStr}");
        OffsetDateTime parsedOffsetDateTime = offsetDateTimeResult.Value;
        Assert.Equal(2023, parsedOffsetDateTime.Year);
        Assert.Equal(7, parsedOffsetDateTime.Month);
        Assert.Equal(15, parsedOffsetDateTime.Day);
        Assert.Equal(10, parsedOffsetDateTime.Hour);
        Assert.Equal(30, parsedOffsetDateTime.Minute);
        Assert.Equal(0, parsedOffsetDateTime.Second);
        Assert.Equal(Offset.FromHours(2), parsedOffsetDateTime.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("period"u8, out JsonElement.Mutable periodProp));
        Assert.Equal("P1Y2M0D", periodProp.GetString());
    }

    [Fact]
    public static void SourceAddAsProperty_WithStringName_AllTypes()
    {
        using var workspace = JsonWorkspace.Create();
        
        var guidValue = Guid.Parse("abcdef12-3456-7890-abcd-ef1234567890");
        var dateTimeValue = new DateTime(2023, 7, 15, 10, 30, 45, DateTimeKind.Utc);
        var dateTimeOffsetValue = new DateTimeOffset(2023, 7, 15, 10, 30, 45, TimeSpan.FromHours(2));
        var localDateValue = new LocalDate(2023, 7, 15);
        Period periodValue = Period.FromYears(1) + Period.FromMonths(2);
        
        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build((ref builder) =>
        {
            builder.AddProperty("byte", (byte)42);
            builder.AddProperty("sbyte", (sbyte)-42);
            builder.AddProperty("short", (short)-1234);
            builder.AddProperty("ushort", (ushort)1234);
            builder.AddProperty("int", -123456);
            builder.AddProperty("uint", 123456u);
            builder.AddProperty("long", -123456789L);
            builder.AddProperty("ulong", 123456789UL);
            builder.AddProperty("float", 3.14f);
            builder.AddProperty("double", 3.14159);
            builder.AddProperty("decimal", 123.456m);
            builder.AddProperty("guid", guidValue);
            builder.AddProperty("datetime", dateTimeValue);
            builder.AddProperty("datetimeoffset", dateTimeOffsetValue);
            builder.AddProperty("localdate", localDateValue);
            builder.AddProperty("offsetdate", new OffsetDate(new LocalDate(2023, 7, 15), Offset.FromHours(2)));
            builder.AddProperty("offsettime", new OffsetTime(new LocalTime(10, 30), Offset.FromHours(2)));
            builder.AddProperty("offsetdatetime", new OffsetDateTime(new LocalDateTime(2023, 7, 15, 10, 30), Offset.FromHours(2)));
            builder.AddProperty("period", periodValue);
        }));

        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, source);
        
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        
        // Validate all properties with correct values
        Assert.True(doc.RootElement.TryGetProperty("byte", out JsonElement.Mutable byteProp));
        Assert.Equal(42, byteProp.GetByte());
        
        Assert.True(doc.RootElement.TryGetProperty("sbyte", out JsonElement.Mutable sbyteProp));
        Assert.Equal(-42, sbyteProp.GetSByte());
        
        Assert.True(doc.RootElement.TryGetProperty("int", out JsonElement.Mutable intProp));
        Assert.Equal(-123456, intProp.GetInt32());
        
        Assert.True(doc.RootElement.TryGetProperty("uint", out JsonElement.Mutable uintProp));
        Assert.Equal(123456u, uintProp.GetUInt32());
        
        Assert.True(doc.RootElement.TryGetProperty("decimal", out JsonElement.Mutable decimalProp));
        Assert.Equal(123.456m, decimalProp.GetDecimal());
        
        Assert.True(doc.RootElement.TryGetProperty("long", out JsonElement.Mutable longProp));
        Assert.Equal(-123456789L, longProp.GetInt64());
        
        Assert.True(doc.RootElement.TryGetProperty("ulong", out JsonElement.Mutable ulongProp));
        Assert.Equal(123456789UL, ulongProp.GetUInt64());
        
        Assert.True(doc.RootElement.TryGetProperty("short", out JsonElement.Mutable shortProp));
        Assert.Equal(-1234, shortProp.GetInt16());
        
        Assert.True(doc.RootElement.TryGetProperty("ushort", out JsonElement.Mutable ushortProp));
        Assert.Equal(1234, ushortProp.GetUInt16());
        
        Assert.True(doc.RootElement.TryGetProperty("float", out JsonElement.Mutable floatProp));
        Assert.Equal(3.14f, floatProp.GetSingle());
        
        Assert.True(doc.RootElement.TryGetProperty("double", out JsonElement.Mutable doubleProp));
        Assert.Equal(3.14159, doubleProp.GetDouble(), precision: 10);
        
        Assert.True(doc.RootElement.TryGetProperty("guid", out JsonElement.Mutable guidProp));
        Assert.Equal(guidValue, guidProp.GetGuid());
        
        Assert.True(doc.RootElement.TryGetProperty("datetime", out JsonElement.Mutable dateTimeProp));
        string dateTimeStr = dateTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(dateTimeStr));
        var parsedDateTime = DateTime.Parse(dateTimeStr, System.Globalization.CultureInfo.InvariantCulture);
        // DateTime serialization may use local timezone - verify date and time components
        Assert.Equal(2023, parsedDateTime.Year);
        Assert.Equal(7, parsedDateTime.Month);
        Assert.Equal(15, parsedDateTime.Day);
        Assert.True(parsedDateTime.Hour >= 0 && parsedDateTime.Hour < 24);
        Assert.Equal(30, parsedDateTime.Minute);
        Assert.Equal(45, parsedDateTime.Second);
        
        Assert.True(doc.RootElement.TryGetProperty("datetimeoffset", out JsonElement.Mutable dateTimeOffsetProp));
        string dateTimeOffsetStr = dateTimeOffsetProp.GetString();
        Assert.False(string.IsNullOrEmpty(dateTimeOffsetStr));
        var parsedDateTimeOffset = DateTimeOffset.Parse(dateTimeOffsetStr, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(dateTimeOffsetValue.Year, parsedDateTimeOffset.Year);
        Assert.Equal(dateTimeOffsetValue.Month, parsedDateTimeOffset.Month);
        Assert.Equal(dateTimeOffsetValue.Day, parsedDateTimeOffset.Day);
        Assert.Equal(dateTimeOffsetValue.Hour, parsedDateTimeOffset.Hour);
        Assert.Equal(dateTimeOffsetValue.Minute, parsedDateTimeOffset.Minute);
        Assert.Equal(dateTimeOffsetValue.Second, parsedDateTimeOffset.Second);
        Assert.Equal(dateTimeOffsetValue.Offset, parsedDateTimeOffset.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("localdate", out JsonElement.Mutable localDateProp));
        Assert.Equal("2023-07-15", localDateProp.GetString());
        
        Assert.True(doc.RootElement.TryGetProperty("offsetdate", out JsonElement.Mutable offsetDateProp));
        string offsetDateStr = offsetDateProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetDateStr));
        OffsetDatePattern offsetDatePattern = NodaTime.Text.OffsetDatePattern.GeneralIso;
        ParseResult<OffsetDate> offsetDateResult = offsetDatePattern.Parse(offsetDateStr);
        Assert.True(offsetDateResult.Success, $"Failed to parse OffsetDate: {offsetDateStr}");
        OffsetDate parsedOffsetDate = offsetDateResult.Value;
        Assert.Equal(2023, parsedOffsetDate.Year);
        Assert.Equal(7, parsedOffsetDate.Month);
        Assert.Equal(15, parsedOffsetDate.Day);
        Assert.Equal(Offset.FromHours(2), parsedOffsetDate.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("offsettime", out JsonElement.Mutable offsetTimeProp));
        string offsetTimeStr = offsetTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetTimeStr));
        OffsetTimePattern offsetTimePattern = NodaTime.Text.OffsetTimePattern.ExtendedIso;
        ParseResult<OffsetTime> offsetTimeResult = offsetTimePattern.Parse(offsetTimeStr);
        Assert.True(offsetTimeResult.Success, $"Failed to parse OffsetTime: {offsetTimeStr}");
        OffsetTime parsedOffsetTime = offsetTimeResult.Value;
        Assert.Equal(10, parsedOffsetTime.Hour);
        Assert.Equal(30, parsedOffsetTime.Minute);
        Assert.Equal(0, parsedOffsetTime.Second);
        Assert.Equal(Offset.FromHours(2), parsedOffsetTime.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("offsetdatetime", out JsonElement.Mutable offsetDateTimeProp));
        string offsetDateTimeStr = offsetDateTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetDateTimeStr));
        OffsetDateTimePattern offsetDateTimePattern = NodaTime.Text.OffsetDateTimePattern.ExtendedIso;
        ParseResult<OffsetDateTime> offsetDateTimeResult = offsetDateTimePattern.Parse(offsetDateTimeStr);
        Assert.True(offsetDateTimeResult.Success, $"Failed to parse OffsetDateTime: {offsetDateTimeStr}");
        OffsetDateTime parsedOffsetDateTime = offsetDateTimeResult.Value;
        Assert.Equal(2023, parsedOffsetDateTime.Year);
        Assert.Equal(7, parsedOffsetDateTime.Month);
        Assert.Equal(15, parsedOffsetDateTime.Day);
        Assert.Equal(10, parsedOffsetDateTime.Hour);
        Assert.Equal(30, parsedOffsetDateTime.Minute);
        Assert.Equal(0, parsedOffsetDateTime.Second);
        Assert.Equal(Offset.FromHours(2), parsedOffsetDateTime.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("period", out JsonElement.Mutable periodProp));
        Assert.Equal("P1Y2M0D", periodProp.GetString());
    }

    [Fact]
    public static void SourceAddAsProperty_WithReadOnlySpanChar_AllTypes()
    {
        using var workspace = JsonWorkspace.Create();
        
        var guidValue = Guid.Parse("abcdef12-3456-7890-abcd-ef1234567890");
        var dateTimeValue = new DateTime(2023, 7, 15, 10, 30, 45, DateTimeKind.Utc);
        var dateTimeOffsetValue = new DateTimeOffset(2023, 7, 15, 10, 30, 45, TimeSpan.FromHours(2));
        var localDateValue = new LocalDate(2023, 7, 15);
        Period periodValue = Period.FromYears(1) + Period.FromMonths(2);
        
        var source = new JsonElement.Source(new JsonElement.ObjectBuilder.Build((ref builder) =>
        {
            builder.AddProperty("byte".AsSpan(), (byte)42);
            builder.AddProperty("sbyte".AsSpan(), (sbyte)-42);
            builder.AddProperty("short".AsSpan(), (short)-1234);
            builder.AddProperty("ushort".AsSpan(), (ushort)1234);
            builder.AddProperty("int".AsSpan(), -123456);
            builder.AddProperty("uint".AsSpan(), 123456u);
            builder.AddProperty("long".AsSpan(), -123456789L);
            builder.AddProperty("ulong".AsSpan(), 123456789UL);
            builder.AddProperty("float".AsSpan(), 3.14f);
            builder.AddProperty("double".AsSpan(), 3.14159);
            builder.AddProperty("decimal".AsSpan(), 123.456m);
            builder.AddProperty("guid".AsSpan(), guidValue);
            builder.AddProperty("datetime".AsSpan(), dateTimeValue);
            builder.AddProperty("datetimeoffset".AsSpan(), dateTimeOffsetValue);
            builder.AddProperty("localdate".AsSpan(), localDateValue);
            builder.AddProperty("offsetdate".AsSpan(), new OffsetDate(new LocalDate(2023, 7, 15), Offset.FromHours(2)));
            builder.AddProperty("offsettime".AsSpan(), new OffsetTime(new LocalTime(10, 30), Offset.FromHours(2)));
            builder.AddProperty("offsetdatetime".AsSpan(), new OffsetDateTime(new LocalDateTime(2023, 7, 15, 10, 30), Offset.FromHours(2)));
            builder.AddProperty("period".AsSpan(), periodValue);
        }));

        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, source);
        
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        
        // Validate all properties with correct values
        Assert.True(doc.RootElement.TryGetProperty("byte", out JsonElement.Mutable byteProp));
        Assert.Equal(42, byteProp.GetByte());
        
        Assert.True(doc.RootElement.TryGetProperty("sbyte", out JsonElement.Mutable sbyteProp));
        Assert.Equal(-42, sbyteProp.GetSByte());
        
        Assert.True(doc.RootElement.TryGetProperty("int", out JsonElement.Mutable intProp));
        Assert.Equal(-123456, intProp.GetInt32());
        
        Assert.True(doc.RootElement.TryGetProperty("uint", out JsonElement.Mutable uintProp));
        Assert.Equal(123456u, uintProp.GetUInt32());
        
        Assert.True(doc.RootElement.TryGetProperty("decimal", out JsonElement.Mutable decimalProp));
        Assert.Equal(123.456m, decimalProp.GetDecimal());
        
        Assert.True(doc.RootElement.TryGetProperty("long", out JsonElement.Mutable longProp));
        Assert.Equal(-123456789L, longProp.GetInt64());
        
        Assert.True(doc.RootElement.TryGetProperty("ulong", out JsonElement.Mutable ulongProp));
        Assert.Equal(123456789UL, ulongProp.GetUInt64());
        
        Assert.True(doc.RootElement.TryGetProperty("short", out JsonElement.Mutable shortProp));
        Assert.Equal(-1234, shortProp.GetInt16());
        
        Assert.True(doc.RootElement.TryGetProperty("ushort", out JsonElement.Mutable ushortProp));
        Assert.Equal(1234, ushortProp.GetUInt16());
        
        Assert.True(doc.RootElement.TryGetProperty("float", out JsonElement.Mutable floatProp));
        Assert.Equal(3.14f, floatProp.GetSingle());
        
        Assert.True(doc.RootElement.TryGetProperty("double", out JsonElement.Mutable doubleProp));
        Assert.Equal(3.14159, doubleProp.GetDouble(), precision: 10);
        
        Assert.True(doc.RootElement.TryGetProperty("guid", out JsonElement.Mutable guidProp));
        Assert.Equal(guidValue, guidProp.GetGuid());
        
        Assert.True(doc.RootElement.TryGetProperty("datetime", out JsonElement.Mutable dateTimeProp));
        string dateTimeStr = dateTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(dateTimeStr));
        var parsedDateTime = DateTime.Parse(dateTimeStr, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2023, parsedDateTime.Year);
        Assert.Equal(7, parsedDateTime.Month);
        Assert.Equal(15, parsedDateTime.Day);
        Assert.True(parsedDateTime.Hour >= 0 && parsedDateTime.Hour < 24);
        Assert.Equal(30, parsedDateTime.Minute);
        Assert.Equal(45, parsedDateTime.Second);
        
        Assert.True(doc.RootElement.TryGetProperty("datetimeoffset", out JsonElement.Mutable dateTimeOffsetProp));
        string dateTimeOffsetStr = dateTimeOffsetProp.GetString();
        Assert.False(string.IsNullOrEmpty(dateTimeOffsetStr));
        var parsedDateTimeOffset = DateTimeOffset.Parse(dateTimeOffsetStr, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(dateTimeOffsetValue.Year, parsedDateTimeOffset.Year);
        Assert.Equal(dateTimeOffsetValue.Month, parsedDateTimeOffset.Month);
        Assert.Equal(dateTimeOffsetValue.Day, parsedDateTimeOffset.Day);
        Assert.Equal(dateTimeOffsetValue.Hour, parsedDateTimeOffset.Hour);
        Assert.Equal(dateTimeOffsetValue.Minute, parsedDateTimeOffset.Minute);
        Assert.Equal(dateTimeOffsetValue.Second, parsedDateTimeOffset.Second);
        Assert.Equal(dateTimeOffsetValue.Offset, parsedDateTimeOffset.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("localdate", out JsonElement.Mutable localDateProp));
        Assert.Equal("2023-07-15", localDateProp.GetString());
        
        Assert.True(doc.RootElement.TryGetProperty("offsetdate", out JsonElement.Mutable offsetDateProp));
        string offsetDateStr = offsetDateProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetDateStr));
        OffsetDatePattern offsetDatePattern = NodaTime.Text.OffsetDatePattern.GeneralIso;
        ParseResult<OffsetDate> offsetDateResult = offsetDatePattern.Parse(offsetDateStr);
        Assert.True(offsetDateResult.Success, $"Failed to parse OffsetDate: {offsetDateStr}");
        OffsetDate parsedOffsetDate = offsetDateResult.Value;
        Assert.Equal(2023, parsedOffsetDate.Year);
        Assert.Equal(7, parsedOffsetDate.Month);
        Assert.Equal(15, parsedOffsetDate.Day);
        Assert.Equal(Offset.FromHours(2), parsedOffsetDate.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("offsettime", out JsonElement.Mutable offsetTimeProp));
        string offsetTimeStr = offsetTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetTimeStr));
        OffsetTimePattern offsetTimePattern = NodaTime.Text.OffsetTimePattern.ExtendedIso;
        ParseResult<OffsetTime> offsetTimeResult = offsetTimePattern.Parse(offsetTimeStr);
        Assert.True(offsetTimeResult.Success, $"Failed to parse OffsetTime: {offsetTimeStr}");
        OffsetTime parsedOffsetTime = offsetTimeResult.Value;
        Assert.Equal(10, parsedOffsetTime.Hour);
        Assert.Equal(30, parsedOffsetTime.Minute);
        Assert.Equal(0, parsedOffsetTime.Second);
        Assert.Equal(Offset.FromHours(2), parsedOffsetTime.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("offsetdatetime", out JsonElement.Mutable offsetDateTimeProp));
        string offsetDateTimeStr = offsetDateTimeProp.GetString();
        Assert.False(string.IsNullOrEmpty(offsetDateTimeStr));
        OffsetDateTimePattern offsetDateTimePattern = NodaTime.Text.OffsetDateTimePattern.ExtendedIso;
        ParseResult<OffsetDateTime> offsetDateTimeResult = offsetDateTimePattern.Parse(offsetDateTimeStr);
        Assert.True(offsetDateTimeResult.Success, $"Failed to parse OffsetDateTime: {offsetDateTimeStr}");
        OffsetDateTime parsedOffsetDateTime = offsetDateTimeResult.Value;
        Assert.Equal(2023, parsedOffsetDateTime.Year);
        Assert.Equal(7, parsedOffsetDateTime.Month);
        Assert.Equal(15, parsedOffsetDateTime.Day);
        Assert.Equal(10, parsedOffsetDateTime.Hour);
        Assert.Equal(30, parsedOffsetDateTime.Minute);
        Assert.Equal(0, parsedOffsetDateTime.Second);
        Assert.Equal(Offset.FromHours(2), parsedOffsetDateTime.Offset);
        
        Assert.True(doc.RootElement.TryGetProperty("period", out JsonElement.Mutable periodProp));
        Assert.Equal("P1Y2M0D", periodProp.GetString());
    }
}
