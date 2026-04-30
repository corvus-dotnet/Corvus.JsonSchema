// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for exception scenarios in JsonElement Get*() methods.
/// </summary>
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

    [Theory]
    [MemberData(nameof(InvalidValueKindsForBoolean))]
    public void GetBoolean_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetBoolean());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForBoolean))]
    public void GetBoolean_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetBoolean());
    }

    #endregion

    #region InvalidOperationException Tests - String

    [Theory]
    [MemberData(nameof(InvalidValueKindsForString))]
    public void GetString_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetString());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForString))]
    public void GetString_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetString());
    }

    #endregion

    #region InvalidOperationException Tests - Numeric Types

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt32_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt32());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt32_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt32());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt64_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt64());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetDouble_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetDouble());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetDecimal_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetDecimal());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetSingle_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetSingle());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetByte_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetByte());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetSByte_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetSByte());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt16_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt16());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt16_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetUInt16());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt32_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetUInt32());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt64_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetUInt64());
    }

    #endregion

#if NET
    #region InvalidOperationException Tests - Int128/UInt128/Half

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt128());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetInt128_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt128());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void TryGetInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.TryGetInt128(out _));
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetUInt128());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetUInt128_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetUInt128());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void TryGetUInt128_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.TryGetUInt128(out _));
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetHalf_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetHalf());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void GetHalf_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetHalf());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForNumeric))]
    public void TryGetHalf_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.TryGetHalf(out _));
    }

    [Fact]
    public void GetInt128_Overflow_ThrowsFormatException()
    {
        // Int128.MaxValue + 1
        string json = "170141183460469231731687303715884105728";
        var element = JsonElement.ParseValue(json);

        Assert.Throws<FormatException>(() => element.GetInt128());
    }

    [Fact]
    public void TryGetInt128_Overflow_ReturnsFalse()
    {
        // Int128.MaxValue + 1
        string json = "170141183460469231731687303715884105728";
        var element = JsonElement.ParseValue(json);

        Assert.False(element.TryGetInt128(out Int128 value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void GetInt128_Underflow_ThrowsFormatException()
    {
        // Int128.MinValue - 1
        string json = "-170141183460469231731687303715884105729";
        var element = JsonElement.ParseValue(json);

        Assert.Throws<FormatException>(() => element.GetInt128());
    }

    [Fact]
    public void TryGetInt128_Underflow_ReturnsFalse()
    {
        // Int128.MinValue - 1
        string json = "-170141183460469231731687303715884105729";
        var element = JsonElement.ParseValue(json);

        Assert.False(element.TryGetInt128(out Int128 value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void GetUInt128_Overflow_ThrowsFormatException()
    {
        // UInt128.MaxValue + 1
        string json = "340282366920938463463374607431768211456";
        var element = JsonElement.ParseValue(json);

        Assert.Throws<FormatException>(() => element.GetUInt128());
    }

    [Fact]
    public void TryGetUInt128_Overflow_ReturnsFalse()
    {
        // UInt128.MaxValue + 1
        string json = "340282366920938463463374607431768211456";
        var element = JsonElement.ParseValue(json);

        Assert.False(element.TryGetUInt128(out UInt128 value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void GetUInt128_Negative_ThrowsFormatException()
    {
        string json = "-1";
        var element = JsonElement.ParseValue(json);

        Assert.Throws<FormatException>(() => element.GetUInt128());
    }

    [Fact]
    public void TryGetUInt128_Negative_ReturnsFalse()
    {
        string json = "-1";
        var element = JsonElement.ParseValue(json);

        Assert.False(element.TryGetUInt128(out UInt128 value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetInt128_FractionalValue_ReturnsFalse()
    {
        string json = "1.5";
        var element = JsonElement.ParseValue(json);

        Assert.False(element.TryGetInt128(out Int128 value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetUInt128_FractionalValue_ReturnsFalse()
    {
        string json = "1.5";
        var element = JsonElement.ParseValue(json);

        Assert.False(element.TryGetUInt128(out UInt128 value));
        Assert.Equal(default, value);
    }

    #endregion
#endif

    #region InvalidOperationException Tests - String Parsing Types

    [Theory]
    [MemberData(nameof(InvalidValueKindsForStringParsing))]
    public void GetDateTimeOffset_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetDateTimeOffset());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForStringParsing))]
    public void GetDateTimeOffset_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetDateTimeOffset());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForStringParsing))]
    public void GetGuid_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetGuid());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForStringParsing))]
    public void GetGuid_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetGuid());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForStringParsing))]
    public void GetBytesFromBase64_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetBytesFromBase64());
    }

    [Theory]
    [MemberData(nameof(InvalidValueKindsForStringParsing))]
    public void GetBytesFromBase64_Mutable_InvalidValueKind_ThrowsInvalidOperationException(string json, JsonValueKind expectedKind)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetBytesFromBase64());
    }

    #endregion

    #region FormatException Tests

    [Theory]
    [InlineData("\"not-a-number\"")]
    [InlineData("\"abc123\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello world\"")]
    public void GetInt32_InvalidFormat_ThrowsInvalidOperationException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt32());
    }

    [Theory]
    [InlineData("\"not-a-number\"")]
    [InlineData("\"abc123\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello world\"")]
    public void GetInt32_Mutable_InvalidFormat_ThrowsInvalidOperationException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<InvalidOperationException>(() => element.GetInt32());
    }

    [Theory]
    [InlineData("\"not-a-guid\"")]
    [InlineData("\"12345678-1234-1234-1234\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello world\"")]
    [InlineData("\"12345678-1234-1234-1234-12345678901G\"")]
    public void GetGuid_InvalidFormat_ThrowsFormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetGuid());
    }

    [Theory]
    [InlineData("\"not-a-guid\"")]
    [InlineData("\"12345678-1234-1234-1234\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello world\"")]
    [InlineData("\"12345678-1234-1234-1234-12345678901G\"")]
    public void GetGuid_Mutable_InvalidFormat_ThrowsFormatException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetGuid());
    }

    [Theory]
    [InlineData("\"not-a-date\"")]
    [InlineData("\"2024-13-50\"")]
    [InlineData("\"2024-01-01T25:00:00Z\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello world\"")]
    [InlineData("\"2024-02-30T10:00:00Z\"")]
    public void GetDateTimeOffset_InvalidFormat_ThrowsFormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetDateTimeOffset());
    }

    [Theory]
    [InlineData("\"not-a-date\"")]
    [InlineData("\"2024-13-50\"")]
    [InlineData("\"2024-01-01T25:00:00Z\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello world\"")]
    [InlineData("\"2024-02-30T10:00:00Z\"")]
    public void GetDateTimeOffset_Mutable_InvalidFormat_ThrowsFormatException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetDateTimeOffset());
    }

    [Theory]
    [InlineData("\"Invalid Base64!\"")]
    [InlineData("\"SGVsbG8\"")]  // Missing padding
    [InlineData("\"SGVsb-8=\"")]  // Invalid character
    [InlineData("\"Hello@World\"")]
    public void GetBytesFromBase64_InvalidFormat_ThrowsFormatException(string json)
    {
        var element = JsonElement.ParseValue(json);

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetBytesFromBase64());
    }

    [Theory]
    [InlineData("\"Invalid Base64!\"")]
    [InlineData("\"SGVsbG8\"")]  // Missing padding
    [InlineData("\"SGVsb-8=\"")]  // Invalid character
    [InlineData("\"Hello@World\"")]
    public void GetBytesFromBase64_Mutable_InvalidFormat_ThrowsFormatException(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Throws<FormatException>(() => element.GetBytesFromBase64());
    }

    #endregion

    #region ObjectDisposedException Tests

    [Fact]
    public void GetBoolean_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("true"))
        {
            element = doc.RootElement;
        }

        Assert.Throws<ObjectDisposedException>(() => element.GetBoolean());
    }

    [Fact]
    public void GetString_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\""))
        {
            element = doc.RootElement;
        }

        Assert.Throws<ObjectDisposedException>(() => element.GetString());
    }

    [Fact]
    public void GetInt32_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("42"))
        {
            element = doc.RootElement;
        }

        Assert.Throws<ObjectDisposedException>(() => element.GetInt32());
    }

    [Fact]
    public void GetDouble_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("3.14"))
        {
            element = doc.RootElement;
        }

        Assert.Throws<ObjectDisposedException>(() => element.GetDouble());
    }

    [Fact]
    public void GetDateTimeOffset_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"2024-01-01T00:00:00Z\""))
        {
            element = doc.RootElement;
        }

        Assert.Throws<ObjectDisposedException>(() => element.GetDateTimeOffset());
    }

    [Fact]
    public void GetGuid_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"12345678-1234-1234-1234-123456789012\""))
        {
            element = doc.RootElement;
        }

        Assert.Throws<ObjectDisposedException>(() => element.GetGuid());
    }

    [Fact]
    public void GetBytesFromBase64_DisposedDocument_ThrowsObjectDisposedException()
    {
        JsonElement element;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"SGVsbG8=\""))
        {
            element = doc.RootElement;
        }

        Assert.Throws<ObjectDisposedException>(() => element.GetBytesFromBase64());
    }

    #endregion
}
