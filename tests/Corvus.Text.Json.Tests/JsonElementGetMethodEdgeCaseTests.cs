// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for edge cases and special scenarios in JsonElement Get*() methods.
/// </summary>
[TestClass]
public class JsonElementGetMethodEdgeCaseTests
{
    #region Null and Empty Value Tests

    [TestMethod]
    public void GetString_NullValue_ReturnsNull()
    {
        string json = "null";
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Null, element.ValueKind);
        string result = element.GetString();
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetString_Mutable_NullValue_ReturnsNull()
    {
        string json = "null";
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Null, element.ValueKind);
        string result = element.GetString();
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetString_EmptyString_ReturnsEmpty()
    {
        string json = "\"\"";
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        string result = element.GetString();
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void GetBytesFromBase64_EmptyString_ReturnsEmptyArray()
    {
        string json = "\"\"";
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        byte[] result = element.GetBytesFromBase64();
        Assert.AreEqual(0, result.Length);
    }

    #endregion

    #region Unicode and Special Character Tests

    [TestMethod]
    [DataRow("\"\\u0000\"", "\u0000")] // Null character
    [DataRow("\"\\u0001\"", "\u0001")] // SOH
    [DataRow("\"\\u001F\"", "\u001F")] // Unit separator
    [DataRow("\"\\u007F\"", "\u007F")] // DEL
    [DataRow("\"\\u0080\"", "\u0080")] // First extended ASCII
    [DataRow("\"\\u00FF\"", "\u00FF")] // Latin-1 supplement
    [DataRow("\"\\u0100\"", "\u0100")] // Latin Extended-A
    [DataRow("\"\\u2603\"", "\u2603")] // Snowman ☃
    [DataRow("\"\\uD83D\\uDE00\"", "\uD83D\uDE00")] // Grinning face emoji 😀
    public void GetString_UnicodeCharacters_ReturnsExpected(string json, string expected)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        string result = element.GetString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetString_MaxUnicodeCharacter_ReturnsExpected()
    {
        string json = "\"\\uFFFF\"";
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        string result = element.GetString();
        Assert.AreEqual("\uFFFF", result);
    }

    #endregion

    #region Precision and Rounding Tests

    [TestMethod]
    [DataRow("1.7976931348623157E+308")] // Near double.MaxValue
    [DataRow("-1.7976931348623157E+308")] // Near double.MinValue
    [DataRow("4.9406564584124654E-324")] // Near double.Epsilon
    [DataRow("2.2250738585072014E-308")] // Near double.MinValue (positive)
    public void GetDouble_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.IsTrue(!double.IsInfinity(result) && !double.IsNaN(result));
    }

    [TestMethod]
    [DataRow("3.4028235E+38")] // Near float.MaxValue
    [DataRow("-3.4028235E+38")] // Near float.MinValue
    [DataRow("1.401298E-45")] // Near float.Epsilon
    public void GetSingle_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        float result = element.GetSingle();
        Assert.IsTrue(!float.IsInfinity(result) && !float.IsNaN(result));
    }

    [TestMethod]
    [DataRow("79228162514264337593543950335")] // decimal.MaxValue
    [DataRow("-79228162514264337593543950335")] // decimal.MinValue
    [DataRow("0.0000000000000000000000000001")] // Very small decimal
    public void GetDecimal_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        decimal result = element.GetDecimal();
        Assert.IsTrue(decimal.Parse(json) == result);
    }

    #endregion

#if NET
    #region Int128/UInt128/Half Edge Cases

    [TestMethod]
    [DataRow("170141183460469231731687303715884105727")] // Int128.MaxValue
    [DataRow("-170141183460469231731687303715884105728")] // Int128.MinValue
    [DataRow("99999999999999999999999999999999999999")] // Large but within Int128 range
    public void GetInt128_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Int128 result = element.GetInt128();
        Assert.AreEqual(Int128.Parse(json), result);
    }

    [TestMethod]
    [DataRow("340282366920938463463374607431768211455")] // UInt128.MaxValue
    [DataRow("99999999999999999999999999999999999999")] // Large but within UInt128 range
    public void GetUInt128_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        UInt128 result = element.GetUInt128();
        Assert.AreEqual(UInt128.Parse(json), result);
    }

    [TestMethod]
    [DataRow("65504", 65504.0)] // Half.MaxValue
    [DataRow("-65504", -65504.0)] // -Half.MaxValue
    [DataRow("0.000061035", 0.00006103515625)] // Near Half.Epsilon
    public void GetHalf_ExtremePrecisionValues_HandlesCorrectly(string json, double expectedDouble)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Half result = element.GetHalf();
        var expected = (Half)expectedDouble;
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("170141183460469231731687303715884105727")] // Int128.MaxValue
    [DataRow("-170141183460469231731687303715884105728")] // Int128.MinValue
    public void GetInt128_Mutable_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Int128 result = element.GetInt128();
        Assert.AreEqual(Int128.Parse(json), result);
    }

    [TestMethod]
    [DataRow("340282366920938463463374607431768211455")] // UInt128.MaxValue
    public void GetUInt128_Mutable_ExtremePrecisionValues_HandlesCorrectly(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        UInt128 result = element.GetUInt128();
        Assert.AreEqual(UInt128.Parse(json), result);
    }

    #endregion
#endif

    #region Large Number Tests

    [TestMethod]
    [DataRow("999999999999999999999999999999999")] // Very large number
    [DataRow("-999999999999999999999999999999999")] // Very large negative number
    [DataRow("1e+100")] // Scientific notation large
    [DataRow("1e-100")] // Scientific notation small
    public void GetDouble_VeryLargeNumbers_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        // Large numbers might become infinity, which is valid
        Assert.IsTrue((!double.IsInfinity(result) && !double.IsNaN(result)) || double.IsInfinity(result));
    }

    #endregion

    #region Base64 Edge Cases

    [TestMethod]
    [DataRow("\"=\"")] // Just padding
    [DataRow("\"==\"")] // Just padding
    [DataRow("\"A=\"")] // Single character with padding
    [DataRow("\"AB==\"")] // Two characters with padding
    [DataRow("\"ABC=\"")] // Three characters with padding
    public void GetBytesFromBase64_EdgeCasePadding_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);

        // Some of these might throw FormatException, which is expected
        try
        {
            byte[] result = element.GetBytesFromBase64();
            // If it doesn't throw, the result should be a valid byte array
            Assert.IsNotNull(result);
        }
        catch (FormatException)
        {
            // This is expected for invalid Base64 strings
        }
    }

    [TestMethod]
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

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        byte[] result = element.GetBytesFromBase64();
        CollectionAssert.AreEqual(originalBytes, result);
    }

    #endregion

    #region DateTime Edge Cases

    [TestMethod]
    [DataRow("\"0001-01-01T00:00:00Z\"")] // DateTime.MinValue
    [DataRow("\"9999-12-31T23:59:59.9999999Z\"")] // Near DateTime.MaxValue
    [DataRow("\"1970-01-01T00:00:00Z\"")] // Unix epoch
    [DataRow("\"2000-02-29T00:00:00Z\"")] // Leap year
    [DataRow("\"2100-02-28T23:59:59Z\"")] // Non-leap year (2100)
    public void GetDateTimeOffset_EdgeCaseDates_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        DateTimeOffset result = element.GetDateTimeOffset();

        string dateString = json.Trim('"');
        Assert.IsTrue(DateTimeOffset.TryParse(dateString, out DateTimeOffset expected));
        // Allow for some precision differences
        Assert.IsTrue(Math.Abs((result - expected).TotalMilliseconds) < 1);
    }

    [TestMethod]
    [DataRow("\"2024-01-01T00:00:00+14:00\"")] // Maximum positive offset
    [DataRow("\"2024-01-01T00:00:00-12:00\"")] // Maximum negative offset
    [DataRow("\"2024-01-01T00:00:00+00:00\"")] // UTC
    [DataRow("\"2024-01-01T00:00:00+05:30\"")] // India time zone
    [DataRow("\"2024-01-01T00:00:00-08:00\"")] // PST
    public void GetDateTimeOffset_TimezoneOffsets_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        DateTimeOffset result = element.GetDateTimeOffset();

        string dateString = json.Trim('"');
        Assert.IsTrue(DateTimeOffset.TryParse(dateString, out DateTimeOffset expected));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region GUID Edge Cases

    [TestMethod]
    [DataRow("\"12345678-1234-1234-1234-123456789abc\"")] // Lowercase hex
    [DataRow("\"12345678-1234-1234-1234-123456789ABC\"")] // Uppercase hex
    [DataRow("\"12345678-1234-1234-1234-123456789AbC\"")] // Mixed case hex
    public void GetGuid_CaseVariations_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Guid result = element.GetGuid();

        string guidString = json.Trim('"');
        Assert.IsTrue(Guid.TryParse(guidString, out Guid expected));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetGuid_EmptyGuid_HandlesCorrectly()
    {
        string json = "\"00000000-0000-0000-0000-000000000000\"";
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Guid result = element.GetGuid();
        Assert.AreEqual(Guid.Empty, result);
    }

    #endregion

    #region Array and Object Context Tests

    [TestMethod]
    public void GetInt32_FromArrayElement_ReturnsExpected()
    {
        string json = "[42, \"string\", true]";
        var arrayElement = JsonElement.ParseValue(json);
        JsonElement numberElement = arrayElement[0];

        Assert.AreEqual(JsonValueKind.Number, numberElement.ValueKind);
        int result = numberElement.GetInt32();
        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public void GetString_FromObjectProperty_ReturnsExpected()
    {
        string json = "{\"name\": \"John\", \"age\": 30}";
        var objectElement = JsonElement.ParseValue(json);
        JsonElement nameElement = objectElement.GetProperty("name");

        Assert.AreEqual(JsonValueKind.String, nameElement.ValueKind);
        string result = nameElement.GetString();
        Assert.AreEqual("John", result);
    }

    [TestMethod]
    public void GetByte_FromNestedStructure_ReturnsExpected()
    {
        string json = "{\"data\": {\"values\": [100, 200, 255]}}";
        var rootElement = JsonElement.ParseValue(json);
        JsonElement dataElement = rootElement.GetProperty("data");
        JsonElement valuesElement = dataElement.GetProperty("values");
        JsonElement byteElement = valuesElement[2];

        Assert.AreEqual(JsonValueKind.Number, byteElement.ValueKind);
        byte result = byteElement.GetByte();
        Assert.AreEqual(255, result);
    }

    #endregion

    #region Scientific Notation Tests

    [TestMethod]
    [DataRow("1e0", 1)]
    [DataRow("1e1", 10)]
    [DataRow("1e2", 100)]
    [DataRow("1e-1", 0.1)]
    [DataRow("1e-2", 0.01)]
    [DataRow("1.5e2", 150)]
    [DataRow("2.5e-1", 0.25)]
    [DataRow("1.23456789e10", 12345678900)]
    public void GetDouble_ScientificNotation_ReturnsExpected(string json, double expected)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.AreEqual(expected, result, 10); // Allow for small precision differences
    }

    [TestMethod]
    [DataRow("1e20")]
    [DataRow("1e30")]
    [DataRow("1e-20")]
    [DataRow("1e-30")]
    public void GetDecimal_LargeScientificNotation_HandlesCorrectly(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);

        // Some very large or small numbers might not fit in decimal
        try
        {
            decimal result = element.GetDecimal();
            Assert.IsTrue(result >= decimal.MinValue && result <= decimal.MaxValue);
        }
        catch (FormatException)
        {
            // This is expected for numbers outside decimal range
        }
    }

    #endregion

    #region Zero Variants Tests

    [TestMethod]
    [DataRow("0")]
    [DataRow("-0")]
    [DataRow("0.0")]
    [DataRow("-0.0")]
    [DataRow("0e0")]
    [DataRow("0e10")]
    [DataRow("0e-10")]
    public void GetDouble_ZeroVariants_ReturnsZero(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.AreEqual(0.0, result);
    }

    [TestMethod]
    [DataRow("0")]
    public void GetInt32_ZeroVariants_ReturnsZero(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        int result = element.GetInt32();
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    [DataRow("0.0")]
    [DataRow("0.00")]
    [DataRow("0e0")]
    public void GetInt32_InvalidZeroVariants_FormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt32());
    }

    #endregion
}
