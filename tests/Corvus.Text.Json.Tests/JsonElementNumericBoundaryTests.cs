// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for boundary conditions in JsonElement numeric Get*() methods.
/// </summary>
[TestClass]
public class JsonElementNumericBoundaryTests
{
    #region SByte Boundary Tests

    [TestMethod]
    [DataRow("128")]  // sbyte.MaxValue + 1
    [DataRow("-129")] // sbyte.MinValue - 1
    [DataRow("1000")]
    [DataRow("-1000")]
    public void GetSByte_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetSByte());
    }

    [TestMethod]
    [DataRow("128")]  // sbyte.MaxValue + 1
    [DataRow("-129")] // sbyte.MinValue - 1
    [DataRow("1000")]
    [DataRow("-1000")]
    public void GetSByte_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetSByte());
    }

    [TestMethod]
    [DataRow("127")]  // sbyte.MaxValue
    [DataRow("-128")] // sbyte.MinValue
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("-1")]
    public void GetSByte_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        sbyte result = element.GetSByte();
        Assert.AreEqual(sbyte.Parse(value), result);
    }

    #endregion

    #region Byte Boundary Tests

    [TestMethod]
    [DataRow("256")]  // byte.MaxValue + 1
    [DataRow("-1")]   // byte.MinValue - 1
    [DataRow("1000")]
    [DataRow("-100")]
    public void GetByte_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetByte());
    }

    [TestMethod]
    [DataRow("256")]  // byte.MaxValue + 1
    [DataRow("-1")]   // byte.MinValue - 1
    [DataRow("1000")]
    [DataRow("-100")]
    public void GetByte_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetByte());
    }

    [TestMethod]
    [DataRow("255")] // byte.MaxValue
    [DataRow("0")]   // byte.MinValue
    [DataRow("1")]
    [DataRow("128")]
    public void GetByte_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        byte result = element.GetByte();
        Assert.AreEqual(byte.Parse(value), result);
    }

    #endregion

    #region Int16 Boundary Tests

    [TestMethod]
    [DataRow("32768")]   // short.MaxValue + 1
    [DataRow("-32769")]  // short.MinValue - 1
    [DataRow("100000")]
    [DataRow("-100000")]
    public void GetInt16_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt16());
    }

    [TestMethod]
    [DataRow("32768")]   // short.MaxValue + 1
    [DataRow("-32769")]  // short.MinValue - 1
    [DataRow("100000")]
    [DataRow("-100000")]
    public void GetInt16_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt16());
    }

    [TestMethod]
    [DataRow("32767")]  // short.MaxValue
    [DataRow("-32768")] // short.MinValue
    [DataRow("0")]
    [DataRow("1000")]
    [DataRow("-1000")]
    public void GetInt16_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        short result = element.GetInt16();
        Assert.AreEqual(short.Parse(value), result);
    }

    #endregion

    #region UInt16 Boundary Tests

    [TestMethod]
    [DataRow("65536")] // ushort.MaxValue + 1
    [DataRow("-1")]    // ushort.MinValue - 1
    [DataRow("100000")]
    [DataRow("-100")]
    public void GetUInt16_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt16());
    }

    [TestMethod]
    [DataRow("65536")] // ushort.MaxValue + 1
    [DataRow("-1")]    // ushort.MinValue - 1
    [DataRow("100000")]
    [DataRow("-100")]
    public void GetUInt16_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt16());
    }

    [TestMethod]
    [DataRow("65535")] // ushort.MaxValue
    [DataRow("0")]     // ushort.MinValue
    [DataRow("1")]
    [DataRow("32767")]
    public void GetUInt16_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        ushort result = element.GetUInt16();
        Assert.AreEqual(ushort.Parse(value), result);
    }

    #endregion

    #region Int32 Boundary Tests

    [TestMethod]
    [DataRow("2147483648")]   // int.MaxValue + 1
    [DataRow("-2147483649")]  // int.MinValue - 1
    [DataRow("9999999999")]
    [DataRow("-9999999999")]
    public void GetInt32_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt32());
    }

    [TestMethod]
    [DataRow("2147483648")]   // int.MaxValue + 1
    [DataRow("-2147483649")]  // int.MinValue - 1
    [DataRow("9999999999")]
    [DataRow("-9999999999")]
    public void GetInt32_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt32());
    }

    [TestMethod]
    [DataRow("2147483647")]  // int.MaxValue
    [DataRow("-2147483648")] // int.MinValue
    [DataRow("0")]
    [DataRow("1000000")]
    [DataRow("-1000000")]
    public void GetInt32_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        int result = element.GetInt32();
        Assert.AreEqual(int.Parse(value), result);
    }

    #endregion

    #region UInt32 Boundary Tests

    [TestMethod]
    [DataRow("4294967296")] // uint.MaxValue + 1
    [DataRow("-1")]         // uint.MinValue - 1
    [DataRow("9999999999")]
    [DataRow("-100")]
    public void GetUInt32_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt32());
    }

    [TestMethod]
    [DataRow("4294967296")] // uint.MaxValue + 1
    [DataRow("-1")]         // uint.MinValue - 1
    [DataRow("9999999999")]
    [DataRow("-100")]
    public void GetUInt32_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt32());
    }

    [TestMethod]
    [DataRow("4294967295")] // uint.MaxValue
    [DataRow("0")]          // uint.MinValue
    [DataRow("1")]
    [DataRow("2147483647")]
    public void GetUInt32_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        uint result = element.GetUInt32();
        Assert.AreEqual(uint.Parse(value), result);
    }

    #endregion

    #region Int64 Boundary Tests

    [TestMethod]
    [DataRow("9223372036854775808")]   // long.MaxValue + 1
    [DataRow("-9223372036854775809")]  // long.MinValue - 1
    [DataRow("99999999999999999999")]
    [DataRow("-99999999999999999999")]
    public void GetInt64_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt64());
    }

    [TestMethod]
    [DataRow("9223372036854775808")]   // long.MaxValue + 1
    [DataRow("-9223372036854775809")]  // long.MinValue - 1
    [DataRow("99999999999999999999")]
    [DataRow("-99999999999999999999")]
    public void GetInt64_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt64());
    }

    [TestMethod]
    [DataRow("9223372036854775807")]  // long.MaxValue
    [DataRow("-9223372036854775808")] // long.MinValue
    [DataRow("0")]
    [DataRow("1000000000000")]
    [DataRow("-1000000000000")]
    public void GetInt64_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        long result = element.GetInt64();
        Assert.AreEqual(long.Parse(value), result);
    }

    #endregion

    #region UInt64 Boundary Tests

    [TestMethod]
    [DataRow("18446744073709551616")] // ulong.MaxValue + 1
    [DataRow("-1")]                   // ulong.MinValue - 1
    [DataRow("99999999999999999999")]
    [DataRow("-100")]
    public void GetUInt64_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt64());
    }

    [TestMethod]
    [DataRow("18446744073709551616")] // ulong.MaxValue + 1
    [DataRow("-1")]                   // ulong.MinValue - 1
    [DataRow("99999999999999999999")]
    [DataRow("-100")]
    public void GetUInt64_Mutable_OutOfRange_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt64());
    }

    [TestMethod]
    [DataRow("18446744073709551615")] // ulong.MaxValue
    [DataRow("0")]                    // ulong.MinValue
    [DataRow("1")]
    [DataRow("9223372036854775807")]
    public void GetUInt64_InRange_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        ulong result = element.GetUInt64();
        Assert.AreEqual(ulong.Parse(value), result);
    }

    #endregion

    #region Floating Point Edge Cases

    [TestMethod]
    [DataRow("1.7976931348623157E+309")]   // > double.MaxValue
    [DataRow("-1.7976931348623157E+309")]  // < double.MinValue
    public void GetDouble_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
#if NET
        double result = element.GetDouble();
        Assert.IsTrue(double.IsInfinity(result));
#else
        // In .NET Framework, GetDouble() may throw an exception instead of returning Infinity
        Assert.ThrowsExactly<FormatException>(() => element.GetDouble());
#endif
    }

    [TestMethod]
    [DataRow("1.7976931348623157E+309")]   // > double.MaxValue
    [DataRow("-1.7976931348623157E+309")]  // < double.MinValue
    public void GetDouble_Mutable_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);

#if NET
        double result = element.GetDouble();
        Assert.IsTrue(double.IsInfinity(result));
#else
        // In .NET Framework, GetDouble() may throw an exception instead of returning Infinity
        Assert.ThrowsExactly<FormatException>(() => element.GetDouble());
#endif
    }

    [TestMethod]
    [DataRow("3.402823E+39")]   // > float.MaxValue
    [DataRow("-3.402823E+39")]  // < float.MinValue
    public void GetSingle_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
#if NET
        float result = element.GetSingle();
        Assert.IsTrue(float.IsInfinity(result));
#else
        // In .NET Framework, GetSingle() may throw an exception instead of returning Infinity
        Assert.ThrowsExactly<FormatException>(() => element.GetSingle());
#endif
    }

    [TestMethod]
    [DataRow("3.402823E+39")]   // > float.MaxValue
    [DataRow("-3.402823E+39")]  // < float.MinValue
    public void GetSingle_Mutable_OutOfRange_ReturnsInfinity(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
#if NET
        float result = element.GetSingle();
        Assert.IsTrue(float.IsInfinity(result));
#else
        // In .NET Framework, GetSingle() may throw an exception instead of returning Infinity
        Assert.ThrowsExactly<FormatException>(() => element.GetSingle());
#endif
    }

    #endregion

    #region Decimal Precision Tests

    [TestMethod]
    [DataRow("79228162514264337593543950336")]   // > decimal.MaxValue
    [DataRow("-79228162514264337593543950336")]  // < decimal.MinValue
    [DataRow("99999999999999999999999999999999999999")]
    public void GetDecimal_OutOfRange_ThrowsOverflowException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetDecimal()); // Could be FormatException
    }

    [TestMethod]
    [DataRow("79228162514264337593543950336")]   // > decimal.MaxValue
    [DataRow("-79228162514264337593543950336")]  // < decimal.MinValue
    [DataRow("99999999999999999999999999999999999999")]
    public void GetDecimal_Mutable_OutOfRange_ThrowsOverflowException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetDecimal()); // Could be FormatException
    }

    [TestMethod]
    [DataRow("0.0000000000000000000000000001")]  // 28 decimal places
    [DataRow("1.2345678901234567890123456789")]   // > 28 decimal places (may truncate)
    [DataRow("79228162514264337593543950335")]    // decimal.MaxValue
    [DataRow("-79228162514264337593543950335")]   // decimal.MinValue
    public void GetDecimal_PrecisionLimits_Success(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        _ = element.GetDecimal();
        // Just verify it doesn't throw, decimal values are always finite (no NaN or Infinity)
        Assert.IsTrue(true); // Decimal type doesn't have infinity or NaN, so just getting here means success
    }

    #endregion

    #region Fractional Number Tests

    [TestMethod]
    [DataRow("3.14159")]
    [DataRow("-2.71828")]
    [DataRow("0.5")]
    public void GetIntegerTypes_FractionalNumber_ThrowsFormatException(string value)
    {
        string json = value;
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt32());
        Assert.ThrowsExactly<FormatException>(() => element.GetInt64());
        Assert.ThrowsExactly<FormatException>(() => element.GetByte());
        Assert.ThrowsExactly<FormatException>(() => element.GetSByte());
        Assert.ThrowsExactly<FormatException>(() => element.GetInt16());
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt16());
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt32());
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt64());
    }

    [TestMethod]
    [DataRow("3.14159")]
    [DataRow("-2.71828")]
    [DataRow("0.5")]
    public void GetIntegerTypes_Mutable_FractionalNumber_ThrowsFormatException(string value)
    {
        string json = value;
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetInt32());
        Assert.ThrowsExactly<FormatException>(() => element.GetInt64());
        Assert.ThrowsExactly<FormatException>(() => element.GetByte());
        Assert.ThrowsExactly<FormatException>(() => element.GetSByte());
        Assert.ThrowsExactly<FormatException>(() => element.GetInt16());
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt16());
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt32());
        Assert.ThrowsExactly<FormatException>(() => element.GetUInt64());
    }

    #endregion
}
