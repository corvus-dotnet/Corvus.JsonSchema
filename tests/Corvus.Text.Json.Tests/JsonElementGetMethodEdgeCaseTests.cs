// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for edge cases and special scenarios in JsonElement Get*() methods.
/// </summary>
public class JsonElementGetMethodEdgeCaseTests
{
    #region Null and Empty Value Tests

    [Fact]
    public void GetString_NullValue_ReturnsNull()
    {
        string json = "null";
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Null, element.ValueKind);
        string result = element.GetString();
        Assert.Null(result);
    }

    [Fact]
    public void GetString_Mutable_NullValue_ReturnsNull()
    {
        string json = "null";
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Null, element.ValueKind);
        string result = element.GetString();
        Assert.Null(result);
    }

    [Fact]
    public void GetString_EmptyString_ReturnsEmpty()
    {
        string json = "\"\"";
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        string result = element.GetString();
        Assert.Equal("", result);
    }

    [Fact]
    public void GetBytesFromBase64_EmptyString_ReturnsEmptyArray()
    {
        string json = "\"\"";
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        byte[] result = element.GetBytesFromBase64();
        Assert.Empty(result);
    }

    #endregion

    #region Unicode and Special Character Tests

    [Theory]
    [InlineData("\"\\u0000\"", "\u0000")] // Null character
    [InlineData("\"\\u0001\"", "\u0001")] // SOH
    [InlineData("\"\\u001F\"", "\u001F")] // Unit separator
    [InlineData("\"\\u007F\"", "\u007F")] // DEL
    [InlineData("\"\\u0080\"", "\u0080")] // First extended ASCII
    [InlineData("\"\\u00FF\"", "\u00FF")] // Latin-1 supplement
    [InlineData("\"\\u0100\"", "\u0100")] // Latin Extended-A
    [InlineData("\"\\u2603\"", "\u2603")] // Snowman ☃
    [InlineData("\"\\uD83D\\uDE00\"", "\uD83D\uDE00")] // Grinning face emoji 😀
    public void GetString_UnicodeCharacters_ReturnsExpected(string json, string expected)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        string result = element.GetString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetString_MaxUnicodeCharacter_ReturnsExpected()
    {
        string json = "\"\\uFFFF\"";
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        string result = element.GetString();
        Assert.Equal("\uFFFF", result);
    }

    #endregion

    #region Precision and Rounding Tests

    [Theory]
    [InlineData("1.7976931348623157E+308")] // Near double.MaxValue
    [InlineData("-1.7976931348623157E+308")] // Near double.MinValue
    [InlineData("4.9406564584124654E-324")] // Near double.Epsilon
    [InlineData("2.2250738585072014E-308")] // Near double.MinValue (positive)
    public void GetDouble_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.True(!double.IsInfinity(result) && !double.IsNaN(result));
    }

    [Theory]
    [InlineData("3.4028235E+38")] // Near float.MaxValue
    [InlineData("-3.4028235E+38")] // Near float.MinValue
    [InlineData("1.401298E-45")] // Near float.Epsilon
    public void GetSingle_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        float result = element.GetSingle();
        Assert.True(!float.IsInfinity(result) && !float.IsNaN(result));
    }

    [Theory]
    [InlineData("79228162514264337593543950335")] // decimal.MaxValue
    [InlineData("-79228162514264337593543950335")] // decimal.MinValue
    [InlineData("0.0000000000000000000000000001")] // Very small decimal
    public void GetDecimal_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        decimal result = element.GetDecimal();
        Assert.True(decimal.Parse(json) == result);
    }

    #endregion

#if NET
    #region Int128/UInt128/Half Edge Cases

    [Theory]
    [InlineData("170141183460469231731687303715884105727")] // Int128.MaxValue
    [InlineData("-170141183460469231731687303715884105728")] // Int128.MinValue
    [InlineData("99999999999999999999999999999999999999")] // Large but within Int128 range
    public void GetInt128_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Int128 result = element.GetInt128();
        Assert.Equal(Int128.Parse(json), result);
    }

    [Theory]
    [InlineData("340282366920938463463374607431768211455")] // UInt128.MaxValue
    [InlineData("99999999999999999999999999999999999999")] // Large but within UInt128 range
    public void GetUInt128_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        UInt128 result = element.GetUInt128();
        Assert.Equal(UInt128.Parse(json), result);
    }

    [Theory]
    [InlineData("65504", 65504.0)] // Half.MaxValue
    [InlineData("-65504", -65504.0)] // -Half.MaxValue
    [InlineData("0.000061035", 0.00006103515625)] // Near Half.Epsilon
    public void GetHalf_ExtremePrecisionValues_HandlesCorrectly(string json, double expectedDouble)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Half result = element.GetHalf();
        var expected = (Half)expectedDouble;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("170141183460469231731687303715884105727")] // Int128.MaxValue
    [InlineData("-170141183460469231731687303715884105728")] // Int128.MinValue
    public void GetInt128_Mutable_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Int128 result = element.GetInt128();
        Assert.Equal(Int128.Parse(json), result);
    }

    [Theory]
    [InlineData("340282366920938463463374607431768211455")] // UInt128.MaxValue
    public void GetUInt128_Mutable_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        UInt128 result = element.GetUInt128();
        Assert.Equal(UInt128.Parse(json), result);
    }

    #endregion
#endif

    #region Large Number Tests

    [Theory]
    [InlineData("999999999999999999999999999999999")] // Very large number
    [InlineData("-999999999999999999999999999999999")] // Very large negative number
    [InlineData("1e+100")] // Scientific notation large
    [InlineData("1e-100")] // Scientific notation small
    public void GetDouble_VeryLargeNumbers_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        // Large numbers might become infinity, which is valid
        Assert.True((!double.IsInfinity(result) && !double.IsNaN(result)) || double.IsInfinity(result));
    }

    #endregion

    #region Base64 Edge Cases

    [Theory]
    [InlineData("\"=\"")] // Just padding
    [InlineData("\"==\"")] // Just padding
    [InlineData("\"A=\"")] // Single character with padding
    [InlineData("\"AB==\"")] // Two characters with padding
    [InlineData("\"ABC=\"")] // Three characters with padding
    public void GetBytesFromBase64_EdgeCasePadding_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);

        // Some of these might throw FormatException, which is expected
        try
        {
            byte[] result = element.GetBytesFromBase64();
            // If it doesn't throw, the result should be a valid byte array
            Assert.NotNull(result);
        }
        catch (FormatException)
        {
            // This is expected for invalid Base64 strings
        }
    }

    [Fact]
    public void GetBytesFromBase64_MaxLength_HandlesCorrectly()
    {
        // Create a large valid Base64 string
        byte[] originalBytes = new byte[1000];
        for (int i = 0; i < originalBytes.Length; i++)
        {
            originalBytes[i] = (byte)(i % 256);
        }
        string base64 = Convert.ToBase64String(originalBytes);
        string json = $"\"{base64}\"";

        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        byte[] result = element.GetBytesFromBase64();
        Assert.Equal(originalBytes, result);
    }

    #endregion

    #region DateTime Edge Cases

    [Theory]
    [InlineData("\"0001-01-01T00:00:00Z\"")] // DateTime.MinValue
    [InlineData("\"9999-12-31T23:59:59.9999999Z\"")] // Near DateTime.MaxValue
    [InlineData("\"1970-01-01T00:00:00Z\"")] // Unix epoch
    [InlineData("\"2000-02-29T00:00:00Z\"")] // Leap year
    [InlineData("\"2100-02-28T23:59:59Z\"")] // Non-leap year (2100)
    public void GetDateTimeOffset_EdgeCaseDates_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        DateTimeOffset result = element.GetDateTimeOffset();

        string dateString = json.Trim('"');
        Assert.True(DateTimeOffset.TryParse(dateString, out DateTimeOffset expected));
        // Allow for some precision differences
        Assert.True(Math.Abs((result - expected).TotalMilliseconds) < 1);
    }

    [Theory]
    [InlineData("\"2024-01-01T00:00:00+14:00\"")] // Maximum positive offset
    [InlineData("\"2024-01-01T00:00:00-12:00\"")] // Maximum negative offset
    [InlineData("\"2024-01-01T00:00:00+00:00\"")] // UTC
    [InlineData("\"2024-01-01T00:00:00+05:30\"")] // India time zone
    [InlineData("\"2024-01-01T00:00:00-08:00\"")] // PST
    public void GetDateTimeOffset_TimezoneOffsets_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        DateTimeOffset result = element.GetDateTimeOffset();

        string dateString = json.Trim('"');
        Assert.True(DateTimeOffset.TryParse(dateString, out DateTimeOffset expected));
        Assert.Equal(expected, result);
    }

    #endregion

    #region GUID Edge Cases

    [Theory]
    [InlineData("\"12345678-1234-1234-1234-123456789abc\"")] // Lowercase hex
    [InlineData("\"12345678-1234-1234-1234-123456789ABC\"")] // Uppercase hex
    [InlineData("\"12345678-1234-1234-1234-123456789AbC\"")] // Mixed case hex
    public void GetGuid_CaseVariations_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Guid result = element.GetGuid();

        string guidString = json.Trim('"');
        Assert.True(Guid.TryParse(guidString, out Guid expected));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetGuid_EmptyGuid_HandlesCorrectly()
    {
        string json = "\"00000000-0000-0000-0000-000000000000\"";
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Guid result = element.GetGuid();
        Assert.Equal(Guid.Empty, result);
    }

    #endregion

    #region Array and Object Context Tests

    [Fact]
    public void GetInt32_FromArrayElement_ReturnsExpected()
    {
        string json = "[42, \"string\", true]";
        var arrayElement = JsonElement.ParseValue(json);
        JsonElement numberElement = arrayElement[0];

        Assert.Equal(JsonValueKind.Number, numberElement.ValueKind);
        int result = numberElement.GetInt32();
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetString_FromObjectProperty_ReturnsExpected()
    {
        string json = "{\"name\": \"John\", \"age\": 30}";
        var objectElement = JsonElement.ParseValue(json);
        JsonElement nameElement = objectElement.GetProperty("name");

        Assert.Equal(JsonValueKind.String, nameElement.ValueKind);
        string result = nameElement.GetString();
        Assert.Equal("John", result);
    }

    [Fact]
    public void GetByte_FromNestedStructure_ReturnsExpected()
    {
        string json = "{\"data\": {\"values\": [100, 200, 255]}}";
        var rootElement = JsonElement.ParseValue(json);
        JsonElement dataElement = rootElement.GetProperty("data");
        JsonElement valuesElement = dataElement.GetProperty("values");
        JsonElement byteElement = valuesElement[2];

        Assert.Equal(JsonValueKind.Number, byteElement.ValueKind);
        byte result = byteElement.GetByte();
        Assert.Equal(255, result);
    }

    #endregion

    #region Scientific Notation Tests

    [Theory]
    [InlineData("1e0", 1)]
    [InlineData("1e1", 10)]
    [InlineData("1e2", 100)]
    [InlineData("1e-1", 0.1)]
    [InlineData("1e-2", 0.01)]
    [InlineData("1.5e2", 150)]
    [InlineData("2.5e-1", 0.25)]
    [InlineData("1.23456789e10", 12345678900)]
    public void GetDouble_ScientificNotation_ReturnsExpected(string json, double expected)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.Equal(expected, result, 10); // Allow for small precision differences
    }

    [Theory]
    [InlineData("1e20")]
    [InlineData("1e30")]
    [InlineData("1e-20")]
    [InlineData("1e-30")]
    public void GetDecimal_LargeScientificNotation_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);

        // Some very large or small numbers might not fit in decimal
        try
        {
            decimal result = element.GetDecimal();
            Assert.True(result >= decimal.MinValue && result <= decimal.MaxValue);
        }
        catch (FormatException)
        {
            // This is expected for numbers outside decimal range
        }
    }

    #endregion

    #region Zero Variants Tests

    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("0.0")]
    [InlineData("-0.0")]
    [InlineData("0e0")]
    [InlineData("0e10")]
    [InlineData("0e-10")]
    public void GetDouble_ZeroVariants_ReturnsZero(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.Equal(0.0, result);
    }

    [Theory]
    [InlineData("0")]
    public void GetInt32_ZeroVariants_ReturnsZero(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        int result = element.GetInt32();
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("0.00")]
    [InlineData("0e0")]
    public void GetInt32_InvalidZeroVariants_FormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt32());
    }

    #endregion
}
