// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
using Corvus.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonPropertyDocumentBuilderDynamicTests
{
    [TestMethod]
    public void CheckByPassingNullWriter()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            foreach (JsonProperty<JsonElement.Mutable> property in doc.RootElement.EnumerateObject())
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement.Mutable root = doc.RootElement;
            var options = new JsonWriterOptions
            {
                SkipValidation = skipValidation,
            };
            using var writer = new Utf8JsonWriter(buffer, options);
            if (skipValidation)
            {
                foreach (JsonProperty<JsonElement.Mutable> property in root.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                writer.Flush();
                AssertContents("\"First\":1", buffer);
            }
            else
            {
                foreach (JsonProperty<JsonElement.Mutable> property in root.EnumerateObject())
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1, \"Number\":1e400}"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            using var writer = new Utf8JsonWriter(buffer);
            writer.WriteStartObject();
            foreach (JsonProperty<JsonElement.Mutable> prop in doc.RootElement.EnumerateObject())
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
    [DataRow("conne\\u0063tionId", "connectionId")]
    [DataRow("connectionId", "connectionId")]
    [DataRow("123", "123")]
    [DataRow("My name is \\\"Ahson\\\"", "My name is \"Ahson\"")]
    public void NameEquals_UseGoodMatches_True(string propertyName, string otherText)
    {
        string jsonString = $"{{ \"{propertyName}\" : \"itsValue\" }}";
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement.Mutable jElement = doc.RootElement;
            JsonProperty<JsonElement.Mutable> property = jElement.EnumerateObject().First();
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.IsTrue(property.NameEquals(otherText));
            Assert.IsTrue(property.NameEquals(otherText.AsSpan()));
            Assert.IsTrue(property.NameEquals(expectedGetBytes));
        }
    }

    [TestMethod]
    [DataRow("conne\\u0063tionId", "connectionId")]
    [DataRow("connectionId", "connectionId")]
    [DataRow("123", "123")]
    [DataRow("My name is \\\"Ahson\\\"", "My name is \\u0022Ahson\\u0022")]
    public void JsonMarshal_GetRawUtf8PropertyName_UseGoodMatches_True(string propertyName, string otherText)
    {
        string jsonString = $"{{ \"{propertyName}\" : \"itsValue\" }}";
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement.Mutable jElement = doc.RootElement;
            JsonProperty<JsonElement.Mutable> property = jElement.EnumerateObject().First();
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.IsTrue(JsonMarshal.GetRawUtf8PropertyName(property).SequenceEqual(expectedGetBytes));
        }
    }

    [TestMethod]
    public void NameEquals_GivenPropertyAndValue_TrueForPropertyName()
    {
        string jsonString = $"{{ \"aPropertyName\" : \"itsValue\" }}";
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement.Mutable jElement = doc.RootElement;
            JsonProperty<JsonElement.Mutable> property = jElement.EnumerateObject().First();

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
