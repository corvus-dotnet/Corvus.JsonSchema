// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
using Corvus.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonPropertyTests
{
    [TestMethod]
    public void CheckByPassingNullWriter()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        {
            foreach (JsonProperty<JsonElement> property in doc.RootElement.EnumerateObject())
            {
                AssertEx.ThrowsExactly<ArgumentNullException>("writer", () => property.WriteTo(null));
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteObjectValidations(bool skipValidation)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        {
            JsonElement root = doc.RootElement;
            var options = new JsonWriterOptions
            {
                SkipValidation = skipValidation,
            };
            using var writer = new Utf8JsonWriter(buffer, options);
            if (skipValidation)
            {
                foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                writer.Flush();
                AssertContents("\"First\":1", buffer);
            }
            else
            {
                foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
                {
                    Assert.ThrowsExactly<InvalidOperationException>(() =>
                    {
                        property.WriteTo(writer);
                    });
                }
                writer.Flush();
                AssertContents("", buffer);
            }
        }
    }

    [TestMethod]
    public void WriteSimpleObject()
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1, \"Number\":1e400}"))
        {
            using var writer = new Utf8JsonWriter(buffer);
            writer.WriteStartObject();
            foreach (JsonProperty<JsonElement> prop in doc.RootElement.EnumerateObject())
            {
                prop.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.Flush();

            AssertContents("{\"First\":1,\"Number\":1e400}", buffer);
        }
    }

    private static void AssertContents(string expectedValue, ArrayBufferWriter<byte> buffer)
    {
        Assert.AreEqual(
            expectedValue,
            Encoding.UTF8.GetString(
                buffer.WrittenSpan
#if NETFRAMEWORK
                    .ToArray()
#endif
                ));
    }

    [TestMethod]
    [DataRow("hello")]
    [DataRow("")]
    [DataRow(null)]
    public void NameEquals_InvalidInstance_Throws(string text)
    {
        string ErrorMessage = new InvalidOperationException().Message;
        JsonProperty<JsonElement> prop = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => prop.NameEquals(text), ErrorMessage);
        Assert.ThrowsExactly<InvalidOperationException>(() => prop.NameEquals(text.AsSpan()), ErrorMessage);
        byte[] expectedGetBytes = text == null ? null : Encoding.UTF8.GetBytes(text);
        Assert.ThrowsExactly<InvalidOperationException>(() => prop.NameEquals(expectedGetBytes), ErrorMessage);
    }

    [TestMethod]
    public void JsonMarshal_GetRawUtf8PropertyName_InvalidInstance_Throws()
    {
        string ErrorMessage = new InvalidOperationException().Message;
        JsonProperty<JsonElement> prop = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => JsonMarshal.GetRawUtf8PropertyName(prop), ErrorMessage);
    }

    [TestMethod]
    public void JsonMarshal_IsPropertyNameEscaped_InvalidInstance_Throws()
    {
        string ErrorMessage = new InvalidOperationException().Message;
        JsonProperty<JsonElement> prop = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => JsonMarshal.IsPropertyNameEscaped(prop), ErrorMessage);
    }

    [TestMethod]
    [DataRow("conne\\u0063tionId", "connectionId")]
    [DataRow("connectionId", "connectionId")]
    [DataRow("123", "123")]
    [DataRow("My name is \\\"Ahson\\\"", "My name is \"Ahson\"")]
    public void NameEquals_UseGoodMatches_True(string propertyName, string otherText)
    {
        string jsonString = $"{{ \"{propertyName}\" : \"itsValue\" }}";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            JsonProperty<JsonElement> property = jElement.EnumerateObject().First();
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.IsTrue(property.NameEquals(otherText));
            Assert.IsTrue(property.NameEquals(otherText.AsSpan()));
            Assert.IsTrue(property.NameEquals(expectedGetBytes));
        }
    }

    [TestMethod]
    [DataRow("conne\\u0063tionId", "conne\\u0063tionId")]
    [DataRow("connectionId", "connectionId")]
    [DataRow("123", "123")]
    [DataRow("My name is \\\"Ahson\\\"", "My name is \\\"Ahson\\\"")]
    public void JsonMarshal_GetRawUtf8PropertyName_UseGoodMatches_True(string propertyName, string otherText)
    {
        string jsonString = $"{{ \"{propertyName}\" : \"itsValue\" }}";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            JsonProperty<JsonElement> property = jElement.EnumerateObject().First();
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.IsTrue(JsonMarshal.GetRawUtf8PropertyName(property).SequenceEqual(expectedGetBytes));
        }
    }

    [TestMethod]
    [DataRow("conne\\u0063tionId", true)]
    [DataRow("connectionId", false)]
    [DataRow("My name is \\\"Ahson\\\"", true)]
    public void JsonMarshal_IsPropertyNameEscaped_ReturnsEscapedState(string propertyName, bool expected)
    {
        string jsonString = $"{{ \"{propertyName}\" : \"itsValue\" }}";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            JsonProperty<JsonElement> property = jElement.EnumerateObject().First();
            Assert.AreEqual(expected, JsonMarshal.IsPropertyNameEscaped(property));
        }
    }

    [TestMethod]
    public void NameEquals_GivenPropertyAndValue_TrueForPropertyName()
    {
        string jsonString = $"{{ \"aPropertyName\" : \"itsValue\" }}";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            JsonProperty<JsonElement> property = jElement.EnumerateObject().First();

            string text = "aPropertyName";
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(text);
            Assert.IsTrue(property.NameEquals(text));
            Assert.IsTrue(property.NameEquals(text.AsSpan()));
            Assert.IsTrue(property.NameEquals(expectedGetBytes));

            text = "itsValue";
            expectedGetBytes = Encoding.UTF8.GetBytes(text);
            Assert.IsFalse(property.NameEquals(text));
            Assert.IsFalse(property.NameEquals(text.AsSpan()));
            Assert.IsFalse(property.NameEquals(expectedGetBytes));
        }
    }
}