// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
using Corvus.Runtime.InteropServices;
using Xunit;

namespace Corvus.Text.Json.Tests;

public static class JsonPropertyDocumentBuilderDynamicTests
{
    [Fact]
    public static void CheckByPassingNullWriter()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            foreach (JsonProperty<JsonElement.Mutable> property in doc.RootElement.EnumerateObject())
            {
                AssertExtensions.Throws<ArgumentNullException>("writer", () => property.WriteTo(null));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void WriteObjectValidations(bool skipValidation)
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
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        property.WriteTo(writer);
                    });
                }
                writer.Flush();
                AssertContents("", buffer);
            }
        }
    }

    [Fact]
    public static void WriteSimpleObject()
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
        Assert.Equal(
            expectedValue,
            Encoding.UTF8.GetString(
                buffer.WrittenSpan
#if NETFRAMEWORK
                    .ToArray()
#endif
                ));
    }

    [Theory]
    [InlineData("conne\\u0063tionId", "connectionId")]
    [InlineData("connectionId", "connectionId")]
    [InlineData("123", "123")]
    [InlineData("My name is \\\"Ahson\\\"", "My name is \"Ahson\"")]
    public static void NameEquals_UseGoodMatches_True(string propertyName, string otherText)
    {
        string jsonString = $"{{ \"{propertyName}\" : \"itsValue\" }}";
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement.Mutable jElement = doc.RootElement;
            JsonProperty<JsonElement.Mutable> property = jElement.EnumerateObject().First();
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.True(property.NameEquals(otherText));
            Assert.True(property.NameEquals(otherText.AsSpan()));
            Assert.True(property.NameEquals(expectedGetBytes));
        }
    }

    [Theory]
    [InlineData("conne\\u0063tionId", "conne\\u0063tionId")]
    [InlineData("connectionId", "connectionId")]
    [InlineData("123", "123")]
    [InlineData("My name is \\\"Ahson\\\"", "My name is \\\"Ahson\\\"")]
    public static void JsonMarshal_GetRawUtf8PropertyName_UseGoodMatches_True(string propertyName, string otherText)
    {
        string jsonString = $"{{ \"{propertyName}\" : \"itsValue\" }}";
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDynamicDocument(workspace))
        {
            JsonElement.Mutable jElement = doc.RootElement;
            JsonProperty<JsonElement.Mutable> property = jElement.EnumerateObject().First();
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.True(JsonMarshal.GetRawUtf8PropertyName(property).SequenceEqual(expectedGetBytes));
        }
    }

    [Fact]
    public static void NameEquals_GivenPropertyAndValue_TrueForPropertyName()
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
            Assert.True(property.NameEquals(text));
            Assert.True(property.NameEquals(text.AsSpan()));
            Assert.True(property.NameEquals(expectedGetBytes));

            text = "itsValue";
            expectedGetBytes = Encoding.UTF8.GetBytes(text);
            Assert.False(property.NameEquals(text));
            Assert.False(property.NameEquals(text.AsSpan()));
            Assert.False(property.NameEquals(expectedGetBytes));
        }
    }
}