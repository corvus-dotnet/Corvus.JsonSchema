// <copyright file="JsonSerializationWithSerializerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

#if NET8_0_OR_GREATER

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Serialization;

/// <summary>
/// Tests for JSON serialization round-tripping via System.Text.Json serializer
/// with inefficient deserialization support enabled.
/// </summary>
[Collection("Serialization")]
public class JsonSerializationWithSerializerTests
{
    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""", JsonValueKind.Object)]
    [InlineData("""[1,2,"3",4.0]""", JsonValueKind.Array)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("\"Hello world\"", JsonValueKind.String)]
    [InlineData("3.2", JsonValueKind.Number)]
    [InlineData("null", JsonValueKind.Null)]
    public void SerializeJsonElementBackedJsonAnyToString(string jsonValue, JsonValueKind expectedKind)
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonAny.Parse(jsonValue);
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            AssertValueKind(expectedKind, result);
            Assert.Equal(JsonAny.Parse(jsonValue), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""", JsonValueKind.Object)]
    [InlineData("""[1,2,"3",4.0]""", JsonValueKind.Array)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("\"Hello world\"", JsonValueKind.String)]
    [InlineData("3.2", JsonValueKind.Number)]
    [InlineData("null", JsonValueKind.Null)]
    public void SerializeDotnetBackedJsonAnyToString(string jsonValue, JsonValueKind expectedKind)
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonAny.Parse(jsonValue).AsDotnetBackedValue();
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            AssertValueKind(expectedKind, result);
            Assert.Equal(JsonAny.Parse(jsonValue), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""", JsonValueKind.Object)]
    [InlineData("""[1,2,"3",4.0]""", JsonValueKind.Array)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("\"Hello world\"", JsonValueKind.String)]
    [InlineData("3.2", JsonValueKind.Number)]
    [InlineData("null", JsonValueKind.Null)]
    public void WriteJsonElementBackedJsonAnyToString(string jsonValue, JsonValueKind expectedKind)
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonAny.Parse(jsonValue);
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            AssertValueKind(expectedKind, result);
            Assert.Equal(JsonAny.Parse(jsonValue), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""", JsonValueKind.Object)]
    [InlineData("""[1,2,"3",4.0]""", JsonValueKind.Array)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("\"Hello world\"", JsonValueKind.String)]
    [InlineData("3.2", JsonValueKind.Number)]
    [InlineData("null", JsonValueKind.Null)]
    public void WriteDotnetBackedJsonAnyToString(string jsonValue, JsonValueKind expectedKind)
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonAny.Parse(jsonValue).AsDotnetBackedValue();
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            AssertValueKind(expectedKind, result);
            Assert.Equal(JsonAny.Parse(jsonValue), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteJsonElementBackedJsonObjectToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonObject.Parse("""{"foo": 3, "bar": "hello", "baz": null}""");
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.Equal(JsonValueKind.Object, result.ValueKind);
            Assert.Equal(JsonAny.Parse("""{"foo": 3, "bar": "hello", "baz": null}"""), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteDotnetBackedJsonObjectToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonObject.Parse("""{"foo": 3, "bar": "hello", "baz": null}""").AsDotnetBackedValue();
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.Equal(JsonValueKind.Object, result.ValueKind);
            Assert.Equal(JsonAny.Parse("""{"foo": 3, "bar": "hello", "baz": null}"""), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteJsonElementBackedJsonArrayToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonArray.Parse("""[1,2,"3",4.0]""");
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.Equal(JsonValueKind.Array, result.ValueKind);
            Assert.Equal(JsonAny.Parse("""[1,2,"3",4.0]"""), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteDotnetBackedJsonArrayToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonArray.Parse("""[1,2,"3",4.0]""").AsDotnetBackedValue();
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.Equal(JsonValueKind.Array, result.ValueKind);
            Assert.Equal(JsonAny.Parse("""[1,2,"3",4.0]"""), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteJsonElementBackedJsonBooleanToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonBoolean.Parse("true");
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.True(result.ValueKind == JsonValueKind.True || result.ValueKind == JsonValueKind.False);
            Assert.Equal(JsonAny.Parse("true"), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteDotnetBackedJsonBooleanToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.True(result.ValueKind == JsonValueKind.True || result.ValueKind == JsonValueKind.False);
            Assert.Equal(JsonAny.Parse("true"), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteJsonElementBackedJsonStringToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonString.Parse("\"Hello, World\"");
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.Equal(JsonValueKind.String, result.ValueKind);
            Assert.Equal(JsonAny.Parse("\"Hello, World\""), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteDotnetBackedJsonStringToString()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = JsonString.Parse("\"Hello, World\"").AsDotnetBackedValue();
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            Assert.Equal(JsonValueKind.String, result.ValueKind);
            Assert.Equal(JsonAny.Parse("\"Hello, World\""), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    [Fact]
    public void WriteJsonElementBackedJsonBase64ContentToString()
    {
        RoundTripWithSerializer<JsonBase64Content>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonBase64ContentToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonBase64Content>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonBase64StringToString()
    {
        RoundTripWithSerializer<JsonBase64String>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonBase64StringToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonBase64String>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonContentToString()
    {
        RoundTripWithSerializer<JsonContent>("\"{\\\"foo\\\": \\\"bar\\\"}\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonContentToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonContent>("\"{\\\"foo\\\": \\\"bar\\\"}\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDateToString()
    {
        RoundTripWithSerializer<JsonDate>("\"2018-11-13\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonDateToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonDate>("\"2018-11-13\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDateTimeToString()
    {
        RoundTripWithSerializer<JsonDateTime>("\"2018-11-13T20:20:39+00:00\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonDateTimeToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonDateTime>("\"2018-11-13T20:20:39+00:00\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDurationToString()
    {
        RoundTripWithSerializer<JsonDuration>("\"P3Y6M4DT12H30M5S\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonDurationToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonDuration>("\"P3Y6M4DT12H30M5S\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonEmailToString()
    {
        RoundTripWithSerializer<JsonEmail>("\"hello@endjin.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonEmailToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonEmail>("\"hello@endjin.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonHostnameToString()
    {
        RoundTripWithSerializer<JsonHostname>("\"www.example.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonHostnameToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonHostname>("\"www.example.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIdnEmailToString()
    {
        RoundTripWithSerializer<JsonIdnEmail>("\"hello@endjin.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonIdnEmailToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonIdnEmail>("\"hello@endjin.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIdnHostnameToString()
    {
        RoundTripWithSerializer<JsonIdnHostname>("\"www.example.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonIdnHostnameToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonIdnHostname>("\"www.example.com\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIntegerToString()
    {
        RoundTripWithSerializer<JsonInteger>("3", JsonValueKind.Number);
    }

    [Fact]
    public void WriteDotnetBackedJsonIntegerToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonInteger>("3", JsonValueKind.Number);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIpV4ToString()
    {
        RoundTripWithSerializer<JsonIpV4>("\"192.168.0.1\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonIpV4ToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonIpV4>("\"192.168.0.1\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIpV6ToString()
    {
        RoundTripWithSerializer<JsonIpV6>("\"::ffff:192.168.0.1\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonIpV6ToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonIpV6>("\"::ffff:c0a8:0001\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIriToString()
    {
        RoundTripWithSerializer<JsonIri>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonIriToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonIri>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIriReferenceToString()
    {
        RoundTripWithSerializer<JsonIriReference>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonIriReferenceToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonIriReference>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonNumberToString()
    {
        RoundTripWithSerializer<JsonNumber>("3.2", JsonValueKind.Number);
    }

    [Fact]
    public void WriteDotnetBackedJsonNumberToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonNumber>("3.2", JsonValueKind.Number);
    }

    [Fact]
    public void WriteJsonElementBackedJsonPointerToString()
    {
        RoundTripWithSerializer<JsonPointer>("\"0/foo/bar\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonPointerToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonPointer>("\"0/foo/bar\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonRegexToString()
    {
        RoundTripWithSerializer<JsonRegex>("\"([abc])+\\\\s+$\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonRegexToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonRegex>("\"([abc])+\\\\s+$\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonRelativePointerToString()
    {
        RoundTripWithSerializer<JsonRelativePointer>("\"/a~1b\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonRelativePointerToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonRelativePointer>("\"/a~1b\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonTimeToString()
    {
        RoundTripWithSerializer<JsonTime>("\"08:30:06+00:20\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonTimeToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonTime>("\"08:30:06+00:20\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriToString()
    {
        RoundTripWithSerializer<JsonUri>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonUri>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriReferenceToString()
    {
        RoundTripWithSerializer<JsonUriReference>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriReferenceToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonUriReference>("\"http://foo.bar/?baz=qux#quux\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriTemplateToString()
    {
        RoundTripWithSerializer<JsonUriTemplate>("\"http://example.com/dictionary/{term:1}/{term}\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriTemplateToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonUriTemplate>("\"http://example.com/dictionary/{term:1}/{term}\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUuidToString()
    {
        RoundTripWithSerializer<JsonUuid>("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"", JsonValueKind.String);
    }

    [Fact]
    public void WriteDotnetBackedJsonUuidToString()
    {
        RoundTripWithSerializerDotnetBacked<JsonUuid>("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"", JsonValueKind.String);
    }

    private static void RoundTripWithSerializer<T>(string jsonValue, JsonValueKind expectedKind)
        where T : struct, IJsonValue<T>
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = T.Parse(jsonValue);
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            AssertValueKind(expectedKind, result);
            Assert.Equal(JsonAny.Parse(jsonValue), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    private static void RoundTripWithSerializerDotnetBacked<T>(string jsonValue, JsonValueKind expectedKind)
        where T : struct, IJsonValue<T>
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        try
        {
            IJsonValue sut = T.Parse(jsonValue).AsDotnetBackedValue();
            string json = JsonSerializer.Serialize(sut, sut.GetType());
            JsonAny result = JsonSerializer.Deserialize<JsonAny>(json);
            AssertValueKind(expectedKind, result);
            Assert.Equal(JsonAny.Parse(jsonValue), result);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }

    private static void AssertValueKind(JsonValueKind expectedKind, JsonAny result)
    {
        if (expectedKind == JsonValueKind.True)
        {
            Assert.True(result.ValueKind == JsonValueKind.True || result.ValueKind == JsonValueKind.False);
        }
        else
        {
            Assert.Equal(expectedKind, result.ValueKind);
        }
    }
}

#endif