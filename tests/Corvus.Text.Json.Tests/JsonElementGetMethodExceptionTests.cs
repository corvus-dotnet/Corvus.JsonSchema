// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for exception scenarios in JsonElement Get*() methods.
/// </summary>
[TestClass]
public class JsonElementGetMethodExceptionTests
{
    public static IEnumerable<object[]> InvalidValueKindsForBoolean()
    {
        yield return new object[] { "null", JsonValueKind.Null };
        yield return new object[] { "[]", JsonValueKind.Array };
        yield return new object[] { "{}", JsonValueKind.Object };
        yield return new object[] { "42", JsonValueKind.Number };
        yield return new object[] { "\"hello\"", JsonValueKind.String };
    }

    public static IEnumerable<object[]> InvalidValueKindsForString()
    {
        yield return new object[] { "true", JsonValueKind.True };
        yield return new object[] { "false", JsonValueKind.False };
        yield return new object[] { "[]", JsonValueKind.Array };
        yield return new object[] { "{}", JsonValueKind.Object };
        yield return new object[] { "42", JsonValueKind.Number };
    }

    public static IEnumerable<object[]> InvalidValueKindsForNumeric()
    {
        yield return new object[] { "true", JsonValueKind.True };
        yield return new object[] { "false", JsonValueKind.False };
        yield return new object[] { "null", JsonValueKind.Null };
        yield return new object[] { "[]", JsonValueKind.Array };
        yield return new object[] { "{}", JsonValueKind.Object };
        yield return new object[] { "\"hello\"", JsonValueKind.String };
    }

    public static IEnumerable<object[]> InvalidValueKindsForStringParsing()
    {
        yield return new object[] { "true", JsonValueKind.True };
        yield return new object[] { "false", JsonValueKind.False };
        yield return new object[] { "null", JsonValueKind.Null };
        yield return new object[] { "[]", JsonValueKind.Array };
        yield return new object[] { "{}", JsonValueKind.Object };
        yield return new object[] { "42", JsonValueKind.Number };
    }

    #region InvalidOperationException Tests - Boolean

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForBoolean))]
    public void GetBoolean_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetBoolean());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForBoolean))]
    public void GetBoolean_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetBoolean());
    }

    #endregion

    #region InvalidOperationException Tests - String

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForString))]
    public void GetString_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetString());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForString))]
    public void GetString_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetString());
    }

    #endregion

    #region InvalidOperationException Tests - Numeric Types

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt32_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt32());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt32_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt32());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt64_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt64());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetDouble_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetDouble());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetDecimal_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetDecimal());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetSingle_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetSingle());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetByte_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetByte());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetSByte_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetSByte());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt16_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt16());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt16_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetUInt16());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt32_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetUInt32());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt64_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetUInt64());
    }

    #endregion

#if NET
    #region InvalidOperationException Tests - Int128/UInt128/Half

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt128());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt128_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt128());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void TryGetInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.TryGetInt128(out _));
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetUInt128());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt128_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetUInt128());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void TryGetUInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.TryGetUInt128(out _));
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetHalf_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetHalf());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void GetHalf_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetHalf());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForNumeric))]
    public void TryGetHalf_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.TryGetHalf(out _));
    }

    [TestMethod]
    public void GetInt128_Overflow_ThrowsFormatException()
    {
        // Int128.MaxValue + 1
        string json = "170141183460469231731687303715884105728";
        var element = JsonElement.ParseValue(json);

        Assert.ThrowsExactly<FormatException>(() => element.GetInt128());
    }

    [TestMethod]
    public void TryGetInt128_Overflow_ReturnsFalse()
    {
        // Int128.MaxValue + 1
        string json = "170141183460469231731687303715884105728";
        var element = JsonElement.ParseValue(json);

        Assert.IsFalse(element.TryGetInt128(out Int128 value));
        Assert.AreEqual(default, value);
    }

    [TestMethod]
    public void GetInt128_Underflow_ThrowsFormatException()
    {
        // Int128.MinValue - 1
        string json = "-170141183460469231731687303715884105729";
        var element = JsonElement.ParseValue(json);

        Assert.ThrowsExactly<FormatException>(() => element.GetInt128());
    }

    [TestMethod]
    public void TryGetInt128_Underflow_ReturnsFalse()
    {
        // Int128.MinValue - 1
        string json = "-170141183460469231731687303715884105729";
        var element = JsonElement.ParseValue(json);

        Assert.IsFalse(element.TryGetInt128(out Int128 value));
        Assert.AreEqual(default, value);
    }

    [TestMethod]
    public void GetUInt128_Overflow_ThrowsFormatException()
    {
        // UInt128.MaxValue + 1
        string json = "340282366920938463463374607431768211456";
        var element = JsonElement.ParseValue(json);

        Assert.ThrowsExactly<FormatException>(() => element.GetUInt128());
    }

    [TestMethod]
    public void TryGetUInt128_Overflow_ReturnsFalse()
    {
        // UInt128.MaxValue + 1
        string json = "340282366920938463463374607431768211456";
        var element = JsonElement.ParseValue(json);

        Assert.IsFalse(element.TryGetUInt128(out UInt128 value));
        Assert.AreEqual(default, value);
    }

    [TestMethod]
    public void GetUInt128_Negative_ThrowsFormatException()
    {
        string json = "-1";
        var element = JsonElement.ParseValue(json);

        Assert.ThrowsExactly<FormatException>(() => element.GetUInt128());
    }

    [TestMethod]
    public void TryGetUInt128_Negative_ReturnsFalse()
    {
        string json = "-1";
        var element = JsonElement.ParseValue(json);

        Assert.IsFalse(element.TryGetUInt128(out UInt128 value));
        Assert.AreEqual(default, value);
    }

    [TestMethod]
    public void TryGetInt128_FractionalValue_ReturnsFalse()
    {
        string json = "1.5";
        var element = JsonElement.ParseValue(json);

        Assert.IsFalse(element.TryGetInt128(out Int128 value));
        Assert.AreEqual(default, value);
    }

    [TestMethod]
    public void TryGetUInt128_FractionalValue_ReturnsFalse()
    {
        string json = "1.5";
        var element = JsonElement.ParseValue(json);

        Assert.IsFalse(element.TryGetUInt128(out UInt128 value));
        Assert.AreEqual(default, value);
    }

    #endregion
#endif

    #region InvalidOperationException Tests - String Parsing Types

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForStringParsing))]
    public void GetDateTimeOffset_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetDateTimeOffset());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForStringParsing))]
    public void GetDateTimeOffset_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetDateTimeOffset());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForStringParsing))]
    public void GetGuid_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetGuid());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForStringParsing))]
    public void GetGuid_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetGuid());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForStringParsing))]
    public void GetBytesFromBase64_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetBytesFromBase64());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidValueKindsForStringParsing))]
    public void GetBytesFromBase64_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(expectedKind, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetBytesFromBase64());
    }

    #endregion

    #region FormatException Tests

    [TestMethod]
    [DataRow("\"not-a-number\"")]
    [DataRow("\"abc123\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello world\"")]
    public void GetInt32_InvalidFormat_ThrowsInvalidOperationException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt32());
    }

    [TestMethod]
    [DataRow("\"not-a-number\"")]
    [DataRow("\"abc123\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello world\"")]
    public void GetInt32_Mutable_InvalidFormat_ThrowsInvalidOperationException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<InvalidOperationException>(() => element.GetInt32());
    }

    [TestMethod]
    [DataRow("\"not-a-guid\"")]
    [DataRow("\"12345678-1234-1234-1234\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello world\"")]
    [DataRow("\"12345678-1234-1234-1234-12345678901G\"")]
    public void GetGuid_InvalidFormat_ThrowsFormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetGuid());
    }

    [TestMethod]
    [DataRow("\"not-a-guid\"")]
    [DataRow("\"12345678-1234-1234-1234\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello world\"")]
    [DataRow("\"12345678-1234-1234-1234-12345678901G\"")]
    public void GetGuid_Mutable_InvalidFormat_ThrowsFormatException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetGuid());
    }

    [TestMethod]
    [DataRow("\"not-a-date\"")]
    [DataRow("\"2024-13-50\"")]
    [DataRow("\"2024-01-01T25:00:00Z\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello world\"")]
    [DataRow("\"2024-02-30T10:00:00Z\"")]
    public void GetDateTimeOffset_InvalidFormat_ThrowsFormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetDateTimeOffset());
    }

    [TestMethod]
    [DataRow("\"not-a-date\"")]
    [DataRow("\"2024-13-50\"")]
    [DataRow("\"2024-01-01T25:00:00Z\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello world\"")]
    [DataRow("\"2024-02-30T10:00:00Z\"")]
    public void GetDateTimeOffset_Mutable_InvalidFormat_ThrowsFormatException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetDateTimeOffset());
    }

    [TestMethod]
    [DataRow("\"Invalid Base64!\"")]
    [DataRow("\"SGVsbG8\"")]  // Missing padding
    [DataRow("\"SGVsb-8=\"")]  // Invalid character
    [DataRow("\"Hello@World\"")]
    public void GetBytesFromBase64_InvalidFormat_ThrowsFormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetBytesFromBase64());
    }

    [TestMethod]
    [DataRow("\"Invalid Base64!\"")]
    [DataRow("\"SGVsbG8\"")]  // Missing padding
    [DataRow("\"SGVsb-8=\"")]  // Invalid character
    [DataRow("\"Hello@World\"")]
    public void GetBytesFromBase64_Mutable_InvalidFormat_ThrowsFormatException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.AreEqual(JsonValueKind.String, element.ValueKind);
        Assert.ThrowsExactly<FormatException>(() => element.GetBytesFromBase64());
    }

    #endregion

    #region ObjectDisposedException Tests

    [TestMethod]
    public void GetBoolean_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("true"))
        {
            element = doc.RootElement;
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetBoolean());
    }

    [TestMethod]
    public void GetString_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\""))
        {
            element = doc.RootElement;
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetString());
    }

    [TestMethod]
    public void GetInt32_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("42"))
        {
            element = doc.RootElement;
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetInt32());
    }

    [TestMethod]
    public void GetDouble_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("3.14"))
        {
            element = doc.RootElement;
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetDouble());
    }

    [TestMethod]
    public void GetDateTimeOffset_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"2024-01-01T00:00:00Z\""))
        {
            element = doc.RootElement;
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetDateTimeOffset());
    }

    [TestMethod]
    public void GetGuid_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"12345678-1234-1234-1234-123456789012\""))
        {
            element = doc.RootElement;
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetGuid());
    }

    [TestMethod]
    public void GetBytesFromBase64_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"SGVsbG8=\""))
        {
            element = doc.RootElement;
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => element.GetBytesFromBase64());
    }

    #endregion
}
