// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementParseTests
{
    public static IEnumerable<object[]> ElementParseCases
    {
        get
        {
            yield return new object[] { "null", JsonValueKind.Null };
            yield return new object[] { "true", JsonValueKind.True };
            yield return new object[] { "false", JsonValueKind.False };
            yield return new object[] { "\"MyString\"", JsonValueKind.String };
            yield return new object[] { @"""\u0033\u002e\u0031""", JsonValueKind.String }; // "3.12"
            yield return new object[] { "1", JsonValueKind.Number };
            yield return new object[] { "3.125e7", JsonValueKind.Number };
            yield return new object[] { "{}", JsonValueKind.Object };
            yield return new object[] { "[]", JsonValueKind.Array };
        }
    }

    public static IEnumerable<object[]> ElementParseInvalidDataCases
    {
        get
        {
            yield return new object[] { "nul" };
            yield return new object[] { "{]" };
        }
    }

    public static IEnumerable<object[]> ElementParsePartialDataCases
    {
        get
        {
            yield return new object[] { "\"MyString" };
            yield return new object[] { "{" };
            yield return new object[] { "[" };
            yield return new object[] { " \n" };
        }
    }

    [TestMethod]
    public void JsonMarshal_GetRawUtf8Value_DisposedDocument_ThrowsObjectDisposedException()
    {
        var jDoc = ParsedJsonDocument<JsonElement>.Parse("{}");
        JsonElement element = jDoc.RootElement;
        jDoc.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => JsonMarshal.GetRawUtf8Value(element));
    }

    [TestMethod]
    public void JsonMarshal_IsValueEscaped_InvalidInstance_Throws()
    {
        JsonElement element = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => JsonMarshal.IsValueEscaped(element));
    }

    [TestMethod]
    [DataRow("\"plain\"", false)]
    [DataRow("\"na\\u006de\"", true)]
    [DataRow("\"Alice \\\"A\\\"\"", true)]
    [DataRow("42", false)]
    public void JsonMarshal_IsValueEscaped_ReturnsEscapedState(string json, bool expected)
    {
        using var jDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = jDoc.RootElement;

        Assert.AreEqual(expected, JsonMarshal.IsValueEscaped(element));
    }

    [TestMethod]
    public void JsonMarshal_GetRawUtf8Value_NestedValues_ReturnsExpectedValue()
    {
        const string json = """
            {
                "date": "2021-06-01T00:00:00Z",
                "temperatureC": 25,
                "summary": "Hot",

                /* The next property is a JSON object */

                "nested": {
                    /* This is a nested JSON object */

                    "nestedDate": "2021-06-01T00:00:00Z",
                    "nestedTemperatureC": 25,
                    "nestedSummary": "Hot"
                },

                /* The next property is a JSON array */

                "nestedArray": [
                    /* This is a JSON array */
                    {
                        "nestedDate": "2021-06-01T00:00:00Z",
                        "nestedTemperatureC": 25,
                        "nestedSummary": "Hot"
                    },
                ]
            }
            """;

        var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        using var jDoc = ParsedJsonDocument<JsonElement>.Parse(json, options);
        JsonElement element = jDoc.RootElement;

        AssertGetRawValue(json, element);
        AssertGetRawValue("\"2021-06-01T00:00:00Z\"", element.GetProperty("date"));
        AssertGetRawValue("25", element.GetProperty("temperatureC"));
        AssertGetRawValue("\"Hot\"", element.GetProperty("summary"));

        JsonElement nested = element.GetProperty("nested");
        AssertGetRawValue("""
                {
                    /* This is a nested JSON object */

                    "nestedDate": "2021-06-01T00:00:00Z",
                    "nestedTemperatureC": 25,
                    "nestedSummary": "Hot"
                }
            """, nested);

        AssertGetRawValue("\"2021-06-01T00:00:00Z\"", nested.GetProperty("nestedDate"));
        AssertGetRawValue("25", nested.GetProperty("nestedTemperatureC"));
        AssertGetRawValue("\"Hot\"", nested.GetProperty("nestedSummary"));

        JsonElement nestedArray = element.GetProperty("nestedArray");
        AssertGetRawValue("""
                [
                    /* This is a JSON array */
                    {
                        "nestedDate": "2021-06-01T00:00:00Z",
                        "nestedTemperatureC": 25,
                        "nestedSummary": "Hot"
                    },
                ]
            """, nestedArray);

        JsonElement nestedArrayElement = nestedArray[0];
        AssertGetRawValue("""
                    {
                        "nestedDate": "2021-06-01T00:00:00Z",
                        "nestedTemperatureC": 25,
                        "nestedSummary": "Hot"
                    }
            """, nestedArrayElement);

        AssertGetRawValue("\"2021-06-01T00:00:00Z\"", nestedArrayElement.GetProperty("nestedDate"));
        AssertGetRawValue("25", nestedArrayElement.GetProperty("nestedTemperatureC"));
        AssertGetRawValue("\"Hot\"", nestedArrayElement.GetProperty("nestedSummary"));

        static void AssertGetRawValue(string expectedJson, JsonElement element)
        {
            using RawUtf8JsonString rawValue = JsonMarshal.GetRawUtf8Value(element);
            Assert.AreEqual(expectedJson.Trim(), Encoding.UTF8.GetString(rawValue.Memory.ToArray()));
        }
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("\r\n    null ")]
    [DataRow("false")]
    [DataRow("true ")]
    [DataRow("   42.0 ")]
    [DataRow(" \"str\" \r\n")]
    [DataRow(" \"string with escaping: \\u0041\\u0042\\u0043\" \r\n")]
    [DataRow(" [     ]")]
    [DataRow(" [null, true, 42.0, \"str\", [], {}, ]")]
    [DataRow(" {  } ")]
    [DataRow("""

        {
            /* I am a comment */
            "key1" : 1,
            "key2" : null,
            "key3" : true,
        }

        """)]
    public void JsonMarshal_GetRawUtf8Value_RootValue_ReturnsFullValue(string json)
    {
        var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        using var jDoc = ParsedJsonDocument<JsonElement>.Parse(json, options);
        JsonElement element = jDoc.RootElement;

        using RawUtf8JsonString rawValue = JsonMarshal.GetRawUtf8Value(element);
        Assert.AreEqual(json.Trim(), Encoding.UTF8.GetString(rawValue.Memory.ToArray()));
    }

    [TestMethod]
    [DynamicData(nameof(ElementParsePartialDataCases))]
    public void Parse_Invalid(string json)
    {
        Assert.Throws<JsonException>(() => JsonElement.ParseValue(json));
        Assert.Throws<JsonException>(() => JsonElement.ParseValue(json.AsSpan()));
        Assert.Throws<JsonException>(() => JsonElement.ParseValue(Encoding.UTF8.GetBytes(json).AsSpan()));
    }

    [TestMethod]
    public void Parse_NullString_Throws()
    {
        AssertEx.ThrowsExactly<ArgumentNullException>("json", () => JsonElement.ParseValue((string)null));
    }

    [TestMethod]
    public void Parse_RespectsOptions()
    {
        const string Json = """
            {
                /* comment */
                "someProp": "value"
            }
            """;

        Assert.Throws<JsonException>(() => JsonElement.ParseValue(Json));
        Assert.Throws<JsonException>(() => JsonElement.ParseValue(Json.AsSpan()));
        Assert.Throws<JsonException>(() => JsonElement.ParseValue(Encoding.UTF8.GetBytes(Json).AsSpan()));

        JsonDocumentOptions options = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
        };

        Validate(JsonElement.ParseValue(Json, options));
        Validate(JsonElement.ParseValue(Json.AsSpan(), options));
        Validate(JsonElement.ParseValue(Encoding.UTF8.GetBytes(Json).AsSpan(), options));

        static void Validate(JsonElement element)
        {
            Assert.AreEqual(JsonValueKind.Object, element.ValueKind);
            Assert.AreEqual(JsonValueKind.String, element.GetProperty("someProp").ValueKind);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ElementParseCases))]
    public void Parse_Valid(string json, JsonValueKind kind)
    {
        Validate(JsonElement.ParseValue(json));
        Validate(JsonElement.ParseValue(json.AsSpan()));
        Validate(JsonElement.ParseValue(Encoding.UTF8.GetBytes(json).AsSpan()));

        void Validate(JsonElement element)
        {
            Assert.AreEqual(kind, element.ValueKind);
            Assert.IsFalse(((Corvus.Text.Json.Internal.IJsonDocument)element.SniffDocument()).IsDisposable);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ElementParseCases))]
    public void ParseValue(string json, JsonValueKind kind)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        var element = JsonElement.ParseValue(ref reader);
        Assert.AreEqual(kind, element.ValueKind);
        Assert.AreEqual(json.Length, reader.BytesConsumed);
        Assert.IsFalse(((Corvus.Text.Json.Internal.IJsonDocument)element.SniffDocument()).IsDisposable);
    }

    [TestMethod]
    public void ParseValue_AllowMultipleValues_TrailingContent()
    {
        var options = new JsonReaderOptions { AllowMultipleValues = true };
        var reader = new Utf8JsonReader("[null,false,42,{},[1]]             <NotJson/>"u8, options);

        var element = JsonElement.ParseValue(ref reader);
        Assert.AreEqual("[null,false,42,{},[1]]", element.GetRawText());
        Assert.AreEqual(JsonTokenType.EndArray, reader.TokenType);

        JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref reader) => reader.Read());
    }

    [TestMethod]
    public void ParseValue_AllowMultipleValues_TrailingJson()
    {
        var options = new JsonReaderOptions { AllowMultipleValues = true };
        var reader = new Utf8JsonReader("[null,false,42,{},[1]]             [43]"u8, options);

        JsonElement element;
        element = JsonElement.ParseValue(ref reader);
        Assert.AreEqual("[null,false,42,{},[1]]", element.GetRawText());
        Assert.AreEqual(JsonTokenType.EndArray, reader.TokenType);

        Assert.IsTrue(reader.Read());
        element = JsonElement.ParseValue(ref reader);
        Assert.AreEqual("[43]", element.GetRawText());

        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DynamicData(nameof(ElementParseInvalidDataCases))]
    public void ParseValueInvalidDataFail(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        Exception ex;
        try
        {
            JsonElement.ParseValue(ref reader);
            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(0, reader.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(ElementParsePartialDataCases))]
    public void ParseValueOutOfData(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), isFinalBlock: false, new JsonReaderState());

        Exception ex;
        try
        {
            JsonElement.ParseValue(ref reader);
            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(0, reader.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(ElementParsePartialDataCases))]
    public void ParseValuePartialDataFail(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        Exception ex;
        try
        {
            JsonElement.ParseValue(ref reader);
            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(0, reader.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(ElementParseCases))]
    public void TryParseValue(string json, JsonValueKind kind)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        bool success = JsonElement.TryParseValue(ref reader, out JsonElement? element);
        Assert.IsTrue(success);
        Assert.AreEqual(kind, element!.Value.ValueKind);
        Assert.AreEqual(json.Length, reader.BytesConsumed);
        Assert.IsFalse(((Corvus.Text.Json.Internal.IJsonDocument)element!.Value.SniffDocument()).IsDisposable);
    }

    [TestMethod]
    [DynamicData(nameof(ElementParseInvalidDataCases))]
    public void TryParseValueInvalidDataFail(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        Exception ex;
        try
        {
            JsonElement.TryParseValue(ref reader, out JsonElement? element);
            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(0, reader.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(ElementParsePartialDataCases))]
    public void TryParseValueOutOfData(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), isFinalBlock: false, new JsonReaderState());
        Assert.IsFalse(JsonElement.TryParseValue(ref reader, out JsonElement? element));
        Assert.IsNull(element);
        Assert.AreEqual(0, reader.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(ElementParsePartialDataCases))]
    public void TryParseValuePartialDataFail(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));

        Exception ex;
        try
        {
            JsonElement.TryParseValue(ref reader, out JsonElement? element);
            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(0, reader.BytesConsumed);
    }
}