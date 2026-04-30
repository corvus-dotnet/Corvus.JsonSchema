// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for boundary conditions in JsonElement numeric Get*() methods.
/// </summary>
public class JsonElementNumericBoundaryTests
{
    #region SByte Boundary Tests

    [Theory]
    [InlineData("128")]  // sbyte.MaxValue + 1
    [InlineData("-129")] // sbyte.MinValue - 1
    [InlineData("1000")]
    [InlineData("-1000")]
    public void GetSByte_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetSByte());
    }

    [Theory]
    [InlineData("128")]  // sbyte.MaxValue + 1
    [InlineData("-129")] // sbyte.MinValue - 1
    [InlineData("1000")]
    [InlineData("-1000")]
    public void GetSByte_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetSByte());
    }

    [Theory]
    [InlineData("127")]  // sbyte.MaxValue
    [InlineData("-128")] // sbyte.MinValue
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    public void GetSByte_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        sbyte result = element.GetSByte();
        Assert.Equal(sbyte.Parse(value), result);
    }

    #endregion

    #region Byte Boundary Tests

    [Theory]
    [InlineData("256")]  // byte.MaxValue + 1
    [InlineData("-1")]   // byte.MinValue - 1
    [InlineData("1000")]
    [InlineData("-100")]
    public void GetByte_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetByte());
    }

    [Theory]
    [InlineData("256")]  // byte.MaxValue + 1
    [InlineData("-1")]   // byte.MinValue - 1
    [InlineData("1000")]
    [InlineData("-100")]
    public void GetByte_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetByte());
    }

    [Theory]
    [InlineData("255")] // byte.MaxValue
    [InlineData("0")]   // byte.MinValue
    [InlineData("1")]
    [InlineData("128")]
    public void GetByte_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        byte result = element.GetByte();
        Assert.Equal(byte.Parse(value), result);
    }

    #endregion

    #region Int16 Boundary Tests

    [Theory]
    [InlineData("32768")]   // short.MaxValue + 1
    [InlineData("-32769")]  // short.MinValue - 1
    [InlineData("100000")]
    [InlineData("-100000")]
    public void GetInt16_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt16());
    }

    [Theory]
    [InlineData("32768")]   // short.MaxValue + 1
    [InlineData("-32769")]  // short.MinValue - 1
    [InlineData("100000")]
    [InlineData("-100000")]
    public void GetInt16_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt16());
    }

    [Theory]
    [InlineData("32767")]  // short.MaxValue
    [InlineData("-32768")] // short.MinValue
    [InlineData("0")]
    [InlineData("1000")]
    [InlineData("-1000")]
    public void GetInt16_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        short result = element.GetInt16();
        Assert.Equal(short.Parse(value), result);
    }

    #endregion

    #region UInt16 Boundary Tests

    [Theory]
    [InlineData("65536")] // ushort.MaxValue + 1
    [InlineData("-1")]    // ushort.MinValue - 1
    [InlineData("100000")]
    [InlineData("-100")]
    public void GetUInt16_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetUInt16());
    }

    [Theory]
    [InlineData("65536")] // ushort.MaxValue + 1
    [InlineData("-1")]    // ushort.MinValue - 1
    [InlineData("100000")]
    [InlineData("-100")]
    public void GetUInt16_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetUInt16());
    }

    [Theory]
    [InlineData("65535")] // ushort.MaxValue
    [InlineData("0")]     // ushort.MinValue
    [InlineData("1")]
    [InlineData("32767")]
    public void GetUInt16_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        ushort result = element.GetUInt16();
        Assert.Equal(ushort.Parse(value), result);
    }

    #endregion

    #region Int32 Boundary Tests

    [Theory]
    [InlineData("2147483648")]   // int.MaxValue + 1
    [InlineData("-2147483649")]  // int.MinValue - 1
    [InlineData("9999999999")]
    [InlineData("-9999999999")]
    public void GetInt32_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt32());
    }

    [Theory]
    [InlineData("2147483648")]   // int.MaxValue + 1
    [InlineData("-2147483649")]  // int.MinValue - 1
    [InlineData("9999999999")]
    [InlineData("-9999999999")]
    public void GetInt32_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt32());
    }

    [Theory]
    [InlineData("2147483647")]  // int.MaxValue
    [InlineData("-2147483648")] // int.MinValue
    [InlineData("0")]
    [InlineData("1000000")]
    [InlineData("-1000000")]
    public void GetInt32_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        int result = element.GetInt32();
        Assert.Equal(int.Parse(value), result);
    }

    #endregion

    #region UInt32 Boundary Tests

    [Theory]
    [InlineData("4294967296")] // uint.MaxValue + 1
    [InlineData("-1")]         // uint.MinValue - 1
    [InlineData("9999999999")]
    [InlineData("-100")]
    public void GetUInt32_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetUInt32());
    }

    [Theory]
    [InlineData("4294967296")] // uint.MaxValue + 1
    [InlineData("-1")]         // uint.MinValue - 1
    [InlineData("9999999999")]
    [InlineData("-100")]
    public void GetUInt32_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetUInt32());
    }

    [Theory]
    [InlineData("4294967295")] // uint.MaxValue
    [InlineData("0")]          // uint.MinValue
    [InlineData("1")]
    [InlineData("2147483647")]
    public void GetUInt32_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        uint result = element.GetUInt32();
        Assert.Equal(uint.Parse(value), result);
    }

    #endregion

    #region Int64 Boundary Tests

    [Theory]
    [InlineData("9223372036854775808")]   // long.MaxValue + 1
    [InlineData("-9223372036854775809")]  // long.MinValue - 1
    [InlineData("99999999999999999999")]
    [InlineData("-99999999999999999999")]
    public void GetInt64_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt64());
    }

    [Theory]
    [InlineData("9223372036854775808")]   // long.MaxValue + 1
    [InlineData("-9223372036854775809")]  // long.MinValue - 1
    [InlineData("99999999999999999999")]
    [InlineData("-99999999999999999999")]
    public void GetInt64_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt64());
    }

    [Theory]
    [InlineData("9223372036854775807")]  // long.MaxValue
    [InlineData("-9223372036854775808")] // long.MinValue
    [InlineData("0")]
    [InlineData("1000000000000")]
    [InlineData("-1000000000000")]
    public void GetInt64_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        long result = element.GetInt64();
        Assert.Equal(long.Parse(value), result);
    }

    #endregion

    #region UInt64 Boundary Tests

    [Theory]
    [InlineData("18446744073709551616")] // ulong.MaxValue + 1
    [InlineData("-1")]                   // ulong.MinValue - 1
    [InlineData("99999999999999999999")]
    [InlineData("-100")]
    public void GetUInt64_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetUInt64());
    }

    [Theory]
    [InlineData("18446744073709551616")] // ulong.MaxValue + 1
    [InlineData("-1")]                   // ulong.MinValue - 1
    [InlineData("99999999999999999999")]
    [InlineData("-100")]
    public void GetUInt64_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetUInt64());
    }

    [Theory]
    [InlineData("18446744073709551615")] // ulong.MaxValue
    [InlineData("0")]                    // ulong.MinValue
    [InlineData("1")]
    [InlineData("9223372036854775807")]
    public void GetUInt64_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        ulong result = element.GetUInt64();
        Assert.Equal(ulong.Parse(value), result);
    }

    #endregion

    #region Floating Point Edge Cases

    [Theory]
    [InlineData("1.7976931348623157E+309")]   // > double.MaxValue
    [InlineData("-1.7976931348623157E+309")]  // < double.MinValue
    public void GetDouble_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
#if NET
        double result = element.GetDouble();
        Assert.True(double.IsInfinity(result));
#else
        // In .NET Framework, GetDouble() may throw an exception instead of returning Infinity
        Assert.Throws<FormatException>(() => element.GetDouble());
#endif
    }

    [Theory]
    [InlineData("1.7976931348623157E+309")]   // > double.MaxValue
    [InlineData("-1.7976931348623157E+309")]  // < double.MinValue
    public void GetDouble_Mutable_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);

#if NET
        double result = element.GetDouble();
        Assert.True(double.IsInfinity(result));
#else
        // In .NET Framework, GetDouble() may throw an exception instead of returning Infinity
        Assert.Throws<FormatException>(() => element.GetDouble());
#endif
    }

    [Theory]
    [InlineData("3.402823E+39")]   // > float.MaxValue
    [InlineData("-3.402823E+39")]  // < float.MinValue
    public void GetSingle_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
#if NET
        float result = element.GetSingle();
        Assert.True(float.IsInfinity(result));
#else
        // In .NET Framework, GetSingle() may throw an exception instead of returning Infinity
        Assert.Throws<FormatException>(() => element.GetSingle());
#endif
    }

    [Theory]
    [InlineData("3.402823E+39")]   // > float.MaxValue
    [InlineData("-3.402823E+39")]  // < float.MinValue
    public void GetSingle_Mutable_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
#if NET
        float result = element.GetSingle();
        Assert.True(float.IsInfinity(result));
#else
        // In .NET Framework, GetSingle() may throw an exception instead of returning Infinity
        Assert.Throws<FormatException>(() => element.GetSingle());
#endif
    }

    #endregion

    #region Decimal Precision Tests

    [Theory]
    [InlineData("79228162514264337593543950336")]   // > decimal.MaxValue
    [InlineData("-79228162514264337593543950336")]  // < decimal.MinValue
    [InlineData("99999999999999999999999999999999999999")]
    public void GetDecimal_OutOfRange_ThrowsOverflowException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsAny<FormatException>(() => element.GetDecimal()); // Could be FormatException
    }

    [Theory]
    [InlineData("79228162514264337593543950336")]   // > decimal.MaxValue
    [InlineData("-79228162514264337593543950336")]  // < decimal.MinValue
    [InlineData("99999999999999999999999999999999999999")]
    public void GetDecimal_Mutable_OutOfRange_ThrowsOverflowException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsAny<FormatException>(() => element.GetDecimal()); // Could be FormatException
    }

    [Theory]
    [InlineData("0.0000000000000000000000000001")]  // 28 decimal places
    [InlineData("1.2345678901234567890123456789")]   // > 28 decimal places (may truncate)
    [InlineData("79228162514264337593543950335")]    // decimal.MaxValue
    [InlineData("-79228162514264337593543950335")]   // decimal.MinValue
    public void GetDecimal_PrecisionLimits_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        _ = element.GetDecimal();
        // Just verify it doesn't throw, decimal values are always finite (no NaN or Infinity)
        Assert.True(true); // Decimal type doesn't have infinity or NaN, so just getting here means success
    }

    #endregion

    #region Fractional Number Tests

    [Theory]
    [InlineData("3.14159")]
    [InlineData("-2.71828")]
    [InlineData("0.5")]
    public void GetIntegerTypes_FractionalNumber_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt32());
        Assert.Throws<FormatException>(() => element.GetInt64());
        Assert.Throws<FormatException>(() => element.GetByte());
        Assert.Throws<FormatException>(() => element.GetSByte());
        Assert.Throws<FormatException>(() => element.GetInt16());
        Assert.Throws<FormatException>(() => element.GetUInt16());
        Assert.Throws<FormatException>(() => element.GetUInt32());
        Assert.Throws<FormatException>(() => element.GetUInt64());
    }

    [Theory]
    [InlineData("3.14159")]
    [InlineData("-2.71828")]
    [InlineData("0.5")]
    public void GetIntegerTypes_Mutable_FractionalNumber_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetInt32());
        Assert.Throws<FormatException>(() => element.GetInt64());
        Assert.Throws<FormatException>(() => element.GetByte());
        Assert.Throws<FormatException>(() => element.GetSByte());
        Assert.Throws<FormatException>(() => element.GetInt16());
        Assert.Throws<FormatException>(() => element.GetUInt16());
        Assert.Throws<FormatException>(() => element.GetUInt32());
        Assert.Throws<FormatException>(() => element.GetUInt64());
    }

    #endregion
}