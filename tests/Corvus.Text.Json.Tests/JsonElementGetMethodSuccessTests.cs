// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for successful value retrieval in JsonElement Get*() methods.
/// </summary>
public class JsonElementGetMethodSuccessTests
{
    #region Numeric Success Tests

    [Theory]
    [MemberData(nameof(GetByteTestData))]
    public void GetByte_ValidValues_ReturnsExpected(byte expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        byte result = element.GetByte();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetByteTestData))]
    public void GetByte_Mutable_ValidValues_ReturnsExpected(byte expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        byte result = element.GetByte();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetSByteTestData))]
    public void GetSByte_ValidValues_ReturnsExpected(sbyte expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        sbyte result = element.GetSByte();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetSByteTestData))]
    public void GetSByte_Mutable_ValidValues_ReturnsExpected(sbyte expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        sbyte result = element.GetSByte();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt16TestData))]
    public void GetInt16_ValidValues_ReturnsExpected(short expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        short result = element.GetInt16();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt16TestData))]
    public void GetInt16_Mutable_ValidValues_ReturnsExpected(short expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        short result = element.GetInt16();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt16TestData))]
    public void GetUInt16_ValidValues_ReturnsExpected(ushort expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        ushort result = element.GetUInt16();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt16TestData))]
    public void GetUInt16_Mutable_ValidValues_ReturnsExpected(ushort expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        ushort result = element.GetUInt16();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt32TestData))]
    public void GetInt32_ValidValues_ReturnsExpected(int expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        int result = element.GetInt32();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt32TestData))]
    public void GetInt32_Mutable_ValidValues_ReturnsExpected(int expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        int result = element.GetInt32();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt32TestData))]
    public void GetUInt32_ValidValues_ReturnsExpected(uint expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        uint result = element.GetUInt32();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt32TestData))]
    public void GetUInt32_Mutable_ValidValues_ReturnsExpected(uint expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        uint result = element.GetUInt32();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt64TestData))]
    public void GetInt64_ValidValues_ReturnsExpected(long expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        long result = element.GetInt64();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt64TestData))]
    public void GetInt64_Mutable_ValidValues_ReturnsExpected(long expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        long result = element.GetInt64();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt64TestData))]
    public void GetUInt64_ValidValues_ReturnsExpected(ulong expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        ulong result = element.GetUInt64();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt64TestData))]
    public void GetUInt64_Mutable_ValidValues_ReturnsExpected(ulong expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        ulong result = element.GetUInt64();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetSingleTestData))]
    public void GetSingle_ValidValues_ReturnsExpected(float expected)
    {
        string json = expected.ToString("R");
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        float result = element.GetSingle();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetSingleTestData))]
    public void GetSingle_Mutable_ValidValues_ReturnsExpected(float expected)
    {
        string json = expected.ToString("R");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        float result = element.GetSingle();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetDoubleTestData))]
    public void GetDouble_ValidValues_ReturnsExpected(double expected)
    {
        string json = expected.ToString("R");
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetDoubleTestData))]
    public void GetDouble_Mutable_ValidValues_ReturnsExpected(double expected)
    {
        string json = expected.ToString("R");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        double result = element.GetDouble();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetDecimalTestData))]
    public void GetDecimal_ValidValues_ReturnsExpected(decimal expected)
    {
        string json = expected.ToString();
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        decimal result = element.GetDecimal();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetDecimalTestData))]
    public void GetDecimal_Mutable_ValidValues_ReturnsExpected(decimal expected)
    {
        string json = expected.ToString();
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        decimal result = element.GetDecimal();
        Assert.Equal(expected, result);
    }

    #endregion

#if NET
    #region Int128/UInt128/Half Success Tests

    [Theory]
    [MemberData(nameof(GetInt128TestData))]
    public void GetInt128_ValidValues_ReturnsExpected(string json, string expectedStr)
    {
        var expected = Int128.Parse(expectedStr);
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Int128 result = element.GetInt128();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt128TestData))]
    public void GetInt128_Mutable_ValidValues_ReturnsExpected(string json, string expectedStr)
    {
        var expected = Int128.Parse(expectedStr);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Int128 result = element.GetInt128();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt128TestData))]
    public void TryGetInt128_ValidValues_ReturnsTrue(string json, string expectedStr)
    {
        var expected = Int128.Parse(expectedStr);
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.True(element.TryGetInt128(out Int128 result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetInt128TestData))]
    public void TryGetInt128_Mutable_ValidValues_ReturnsTrue(string json, string expectedStr)
    {
        var expected = Int128.Parse(expectedStr);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.True(element.TryGetInt128(out Int128 result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt128TestData))]
    public void GetUInt128_ValidValues_ReturnsExpected(string json, string expectedStr)
    {
        var expected = UInt128.Parse(expectedStr);
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        UInt128 result = element.GetUInt128();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt128TestData))]
    public void GetUInt128_Mutable_ValidValues_ReturnsExpected(string json, string expectedStr)
    {
        var expected = UInt128.Parse(expectedStr);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        UInt128 result = element.GetUInt128();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt128TestData))]
    public void TryGetUInt128_ValidValues_ReturnsTrue(string json, string expectedStr)
    {
        var expected = UInt128.Parse(expectedStr);
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.True(element.TryGetUInt128(out UInt128 result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetUInt128TestData))]
    public void TryGetUInt128_Mutable_ValidValues_ReturnsTrue(string json, string expectedStr)
    {
        var expected = UInt128.Parse(expectedStr);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.True(element.TryGetUInt128(out UInt128 result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetHalfTestData))]
    public void GetHalf_ValidValues_ReturnsExpected(string json, double expectedDouble)
    {
        var expected = (Half)expectedDouble;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Half result = element.GetHalf();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetHalfTestData))]
    public void GetHalf_Mutable_ValidValues_ReturnsExpected(string json, double expectedDouble)
    {
        var expected = (Half)expectedDouble;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Half result = element.GetHalf();
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetHalfTestData))]
    public void TryGetHalf_ValidValues_ReturnsTrue(string json, double expectedDouble)
    {
        var expected = (Half)expectedDouble;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.True(element.TryGetHalf(out Half result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetHalfTestData))]
    public void TryGetHalf_Mutable_ValidValues_ReturnsTrue(string json, double expectedDouble)
    {
        var expected = (Half)expectedDouble;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.True(element.TryGetHalf(out Half result));
        Assert.Equal(expected, result);
    }

    #endregion
#endif

    #region Boolean Success Tests

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void GetBoolean_ValidValues_ReturnsExpected(string json, bool expected)
    {
        var element = JsonElement.ParseValue(json);

        bool result = element.GetBoolean();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void GetBoolean_Mutable_ValidValues_ReturnsExpected(string json, bool expected)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        bool result = element.GetBoolean();
        Assert.Equal(expected, result);
    }

    #endregion

    #region String Success Tests

    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("\"Hello World\"", "Hello World")]
    [InlineData("\"\\u0048\\u0065\\u006C\\u006C\\u006F\"", "Hello")]
    [InlineData("\"Line1\\nLine2\"", "Line1\nLine2")]
    [InlineData("\"Tab\\tSeparated\"", "Tab\tSeparated")]
    [InlineData("\"Quote: \\\"Hello\\\"\"", "Quote: \"Hello\"")]
    [InlineData("\"Backslash: \\\\\"", "Backslash: \\")]
    [InlineData("null", null)]
    public void GetString_ValidValues_ReturnsExpected(string json, string expected)
    {
        var element = JsonElement.ParseValue(json);

        string result = element.GetString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("\"Hello World\"", "Hello World")]
    [InlineData("\"\\u0048\\u0065\\u006C\\u006C\\u006F\"", "Hello")]
    [InlineData("\"Line1\\nLine2\"", "Line1\nLine2")]
    [InlineData("\"Tab\\tSeparated\"", "Tab\tSeparated")]
    [InlineData("\"Quote: \\\"Hello\\\"\"", "Quote: \"Hello\"")]
    [InlineData("\"Backslash: \\\\\"", "Backslash: \\")]
    [InlineData("null", null)]
    public void GetString_Mutable_ValidValues_ReturnsExpected(string json, string expected)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        string result = element.GetString();
        Assert.Equal(expected, result);
    }

    #endregion

    #region DateTime Success Tests

    [Theory]
    [InlineData("\"2024-01-01T00:00:00Z\"")]
    [InlineData("\"2024-12-31T23:59:59.999Z\"")]
    [InlineData("\"2024-06-15T12:30:45.123+05:30\"")]
    [InlineData("\"2024-06-15T12:30:45.123-08:00\"")]
    [InlineData("\"1997-07-16T19:20:30.4555555Z\"")]
    public void GetDateTimeOffset_ValidValues_ReturnsExpected(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        DateTimeOffset result = element.GetDateTimeOffset();

        // Verify it parses successfully
        Assert.True(DateTimeOffset.TryParse(json.Trim('"'), out DateTimeOffset expected));
        // Allow for some precision differences in fractional seconds
        Assert.True(Math.Abs((result - expected).TotalMilliseconds) < 1);
    }

    [Theory]
    [InlineData("\"2024-01-01T00:00:00Z\"")]
    [InlineData("\"2024-12-31T23:59:59.999Z\"")]
    [InlineData("\"2024-06-15T12:30:45.123+05:30\"")]
    [InlineData("\"2024-06-15T12:30:45.123-08:00\"")]
    [InlineData("\"1997-07-16T19:20:30.4555555Z\"")]
    public void GetDateTimeOffset_Mutable_ValidValues_ReturnsExpected(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        DateTimeOffset result = element.GetDateTimeOffset();

        // Verify it parses successfully
        Assert.True(DateTimeOffset.TryParse(json.Trim('"'), out DateTimeOffset expected));
        // Allow for some precision differences in fractional seconds
        Assert.True(Math.Abs((result - expected).TotalMilliseconds) < 1);
    }

    #endregion

    #region Guid Success Tests

    [Theory]
    [InlineData("\"12345678-1234-1234-1234-123456789012\"")]
    [InlineData("\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("\"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF\"")]
    [InlineData("\"550e8400-e29b-41d4-a716-446655440000\"")]
    public void GetGuid_ValidValues_ReturnsExpected(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Guid result = element.GetGuid();

        // Verify it parses successfully
        string guidString = json.Trim('"');
        Assert.True(Guid.TryParse(guidString, out Guid expected));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("\"12345678-1234-1234-1234-123456789012\"")]
    [InlineData("\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("\"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF\"")]
    [InlineData("\"550e8400-e29b-41d4-a716-446655440000\"")]
    public void GetGuid_Mutable_ValidValues_ReturnsExpected(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Guid result = element.GetGuid();

        // Verify it parses successfully
        string guidString = json.Trim('"');
        Assert.True(Guid.TryParse(guidString, out Guid expected));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Base64 Success Tests

    [Theory]
    [InlineData("\"\"", new byte[0])]
    [InlineData("\"SGVsbG8=\"", new byte[] { 72, 101, 108, 108, 111 })] // "Hello"
    [InlineData("\"AQID\"", new byte[] { 1, 2, 3 })]
    [InlineData("\"QWxhZGRpbjpvcGVuIHNlc2FtZQ==\"", new byte[] { 65, 108, 97, 100, 100, 105, 110, 58, 111, 112, 101, 110, 32, 115, 101, 115, 97, 109, 101 })] // "Aladdin:open sesame"
    public void GetBytesFromBase64_ValidValues_ReturnsExpected(string json, byte[] expected)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        byte[] result = element.GetBytesFromBase64();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("\"\"", new byte[0])]
    [InlineData("\"SGVsbG8=\"", new byte[] { 72, 101, 108, 108, 111 })] // "Hello"
    [InlineData("\"AQID\"", new byte[] { 1, 2, 3 })]
    [InlineData("\"QWxhZGRpbjpvcGVuIHNlc2FtZQ==\"", new byte[] { 65, 108, 97, 100, 100, 105, 110, 58, 111, 112, 101, 110, 32, 115, 101, 115, 97, 109, 101 })] // "Aladdin:open sesame"
    public void GetBytesFromBase64_Mutable_ValidValues_ReturnsExpected(string json, byte[] expected)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        byte[] result = element.GetBytesFromBase64();
        Assert.Equal(expected, result);
    }

    #endregion

    #region Test Data

    public static IEnumerable<object[]> GetByteTestData()
    {
        return new object[][]
        {
            new object[] { (byte)0 },
            new object[] { (byte)1 },
            new object[] { (byte)127 },
            new object[] { (byte)128 },
            new object[] { (byte)255 },
            new object[] { (byte)42 },
        };
    }

    public static IEnumerable<object[]> GetSByteTestData()
    {
        return new object[][]
        {
            new object[] { (sbyte)-128 },
            new object[] { (sbyte)-1 },
            new object[] { (sbyte)0 },
            new object[] { (sbyte)1 },
            new object[] { (sbyte)127 },
            new object[] { (sbyte)42 },
            new object[] { (sbyte)-42 },
        };
    }

    public static IEnumerable<object[]> GetInt16TestData()
    {
        return new object[][]
        {
            new object[] { (short)-32768 },
            new object[] { (short)-1000 },
            new object[] { (short)-1 },
            new object[] { (short)0 },
            new object[] { (short)1 },
            new object[] { (short)1000 },
            new object[] { (short)32767 },
            new object[] { (short)12345 },
        };
    }

    public static IEnumerable<object[]> GetUInt16TestData()
    {
        return new object[][]
        {
            new object[] { (ushort)0 },
            new object[] { (ushort)1 },
            new object[] { (ushort)1000 },
            new object[] { (ushort)32767 },
            new object[] { (ushort)32768 },
            new object[] { (ushort)65535 },
            new object[] { (ushort)12345 },
        };
    }

    public static IEnumerable<object[]> GetInt32TestData()
    {
        return new object[][]
        {
            new object[] { -2147483648 },
            new object[] { -1000000 },
            new object[] { -1 },
            new object[] { 0 },
            new object[] { 1 },
            new object[] { 1000000 },
            new object[] { 2147483647 },
            new object[] { 42 },
            new object[] { -42 },
        };
    }

    public static IEnumerable<object[]> GetUInt32TestData()
    {
        return new object[][]
        {
            new object[] { 0u },
            new object[] { 1u },
            new object[] { 1000000u },
            new object[] { 2147483647u },
            new object[] { 2147483648u },
            new object[] { 4294967295u },
            new object[] { 42u },
        };
    }

    public static IEnumerable<object[]> GetInt64TestData()
    {
        return new object[][]
        {
            new object[] { -9223372036854775808L },
            new object[] { -1000000000000L },
            new object[] { -1L },
            new object[] { 0L },
            new object[] { 1L },
            new object[] { 1000000000000L },
            new object[] { 9223372036854775807L },
            new object[] { 42L },
            new object[] { -42L },
        };
    }

    public static IEnumerable<object[]> GetUInt64TestData()
    {
        return new object[][]
        {
            new object[] { 0ul },
            new object[] { 1ul },
            new object[] { 1000000000000ul },
            new object[] { 9223372036854775807ul },
            new object[] { 9223372036854775808ul },
            new object[] { 18446744073709551615ul },
            new object[] { 42ul },
        };
    }

    public static IEnumerable<object[]> GetSingleTestData()
    {
        return new object[][]
        {
            new object[] { 0.0f },
            new object[] { 1.0f },
            new object[] { -1.0f },
            new object[] { 3.14159f },
            new object[] { -2.71828f },
            new object[] { float.MaxValue },
            new object[] { float.MinValue },
            new object[] { float.Epsilon },
            new object[] { 42.5f },
        };
    }

    public static IEnumerable<object[]> GetDoubleTestData()
    {
        return new object[][]
        {
            new object[] { 0.0 },
            new object[] { 1.0 },
            new object[] { -1.0 },
            new object[] { 3.141592653589793 },
            new object[] { -2.718281828459045 },
            new object[] { double.MaxValue },
            new object[] { double.MinValue },
            new object[] { double.Epsilon },
            new object[] { 42.5 },
        };
    }

    public static IEnumerable<object[]> GetDecimalTestData()
    {
        return new object[][]
        {
            new object[] { 0m },
            new object[] { 1m },
            new object[] { -1m },
            new object[] { 3.14159265358979323846m },
            new object[] { -2.71828182845904523536m },
            new object[] { decimal.MaxValue },
            new object[] { decimal.MinValue },
            new object[] { 42.5m },
            new object[] { 0.0000000000000000000000000001m },
        };
    }

#if NET
    public static IEnumerable<object[]> GetInt128TestData()
    {
        return new object[][]
        {
            new object[] { "0", "0" },
            new object[] { "1", "1" },
            new object[] { "-1", "-1" },
            new object[] { "42", "42" },
            new object[] { "-42", "-42" },
            new object[] { "1000000000000", "1000000000000" },
            new object[] { "-1000000000000", "-1000000000000" },
            new object[] { "9223372036854775807", "9223372036854775807" }, // long.MaxValue
            new object[] { "-9223372036854775808", "-9223372036854775808" }, // long.MinValue
            new object[] { "9223372036854775808", "9223372036854775808" }, // long.MaxValue + 1
            new object[] { "170141183460469231731687303715884105727", "170141183460469231731687303715884105727" }, // Int128.MaxValue
            new object[] { "-170141183460469231731687303715884105728", "-170141183460469231731687303715884105728" }, // Int128.MinValue
        };
    }

    public static IEnumerable<object[]> GetUInt128TestData()
    {
        return new object[][]
        {
            new object[] { "0", "0" },
            new object[] { "1", "1" },
            new object[] { "42", "42" },
            new object[] { "1000000000000", "1000000000000" },
            new object[] { "18446744073709551615", "18446744073709551615" }, // ulong.MaxValue
            new object[] { "18446744073709551616", "18446744073709551616" }, // ulong.MaxValue + 1
            new object[] { "340282366920938463463374607431768211455", "340282366920938463463374607431768211455" }, // UInt128.MaxValue
        };
    }

    public static IEnumerable<object[]> GetHalfTestData()
    {
        return new object[][]
        {
            new object[] { "0", 0.0 },
            new object[] { "1", 1.0 },
            new object[] { "-1", -1.0 },
            new object[] { "0.5", 0.5 },
            new object[] { "-0.5", -0.5 },
            new object[] { "3.14", 3.140625 }, // Half rounds to 3.140625
            new object[] { "42", 42.0 },
        };
    }
#endif

    #endregion
}
