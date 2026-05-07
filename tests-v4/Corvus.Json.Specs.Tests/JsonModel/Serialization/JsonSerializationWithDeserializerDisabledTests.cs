// <copyright file="JsonSerializationWithDeserializerDisabledTests.cs" company="Endjin Limited">
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
/// with inefficient deserialization support disabled — expects InvalidOperationException.
/// </summary>
[Collection("Serialization")]
public class JsonSerializationWithDeserializerDisabledTests
{
    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [InlineData("""[1,2,"3",4.0]""")]
    [InlineData("true")]
    [InlineData("\"Hello world\"")]
    [InlineData("3.2")]
    [InlineData("null")]
    public void SerializeJsonElementBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: false);
    }

    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [InlineData("""[1,2,"3",4.0]""")]
    [InlineData("true")]
    [InlineData("\"Hello world\"")]
    [InlineData("3.2")]
    [InlineData("null")]
    public void SerializeDotnetBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: true);
    }

    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [InlineData("""[1,2,"3",4.0]""")]
    [InlineData("true")]
    [InlineData("\"Hello world\"")]
    [InlineData("3.2")]
    [InlineData("null")]
    public void WriteJsonElementBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: false);
    }

    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [InlineData("""[1,2,"3",4.0]""")]
    [InlineData("true")]
    [InlineData("\"Hello world\"")]
    [InlineData("3.2")]
    [InlineData("null")]
    public void WriteDotnetBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonObjectToStringThrows()
    {
        AssertDeserializationThrows<JsonObject>("""{"foo": 3, "bar": "hello", "baz": null}""", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonObjectToStringThrows()
    {
        AssertDeserializationThrows<JsonObject>("""{"foo": 3, "bar": "hello", "baz": null}""", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonArrayToStringThrows()
    {
        AssertDeserializationThrows<JsonArray>("""[1,2,"3",4.0]""", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonArrayToStringThrows()
    {
        AssertDeserializationThrows<JsonArray>("""[1,2,"3",4.0]""", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonBooleanToStringThrows()
    {
        AssertDeserializationThrows<JsonBoolean>("true", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonBooleanToStringThrows()
    {
        AssertDeserializationThrows<JsonBoolean>("true", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonStringToStringThrows()
    {
        AssertDeserializationThrows<JsonString>("\"Hello, World\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonStringToStringThrows()
    {
        AssertDeserializationThrows<JsonString>("\"Hello, World\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonBase64ContentToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64Content>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonBase64ContentToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64Content>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonBase64StringToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64String>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonBase64StringToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64String>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonContentToStringThrows()
    {
        AssertDeserializationThrows<JsonContent>("\"{\\\"foo\\\": \\\"bar\\\"}\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonContentToStringThrows()
    {
        AssertDeserializationThrows<JsonContent>("\"{\\\"foo\\\": \\\"bar\\\"}\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDateToStringThrows()
    {
        AssertDeserializationThrows<JsonDate>("\"2018-11-13\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonDateToStringThrows()
    {
        AssertDeserializationThrows<JsonDate>("\"2018-11-13\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDateTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonDateTime>("\"2018-11-13T20:20:39+00:00\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonDateTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonDateTime>("\"2018-11-13T20:20:39+00:00\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDurationToStringThrows()
    {
        AssertDeserializationThrows<JsonDuration>("\"P3Y6M4DT12H30M5S\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonDurationToStringThrows()
    {
        AssertDeserializationThrows<JsonDuration>("\"P3Y6M4DT12H30M5S\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonEmail>("\"hello@endjin.com\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonEmail>("\"hello@endjin.com\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonHostname>("\"www.example.com\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonHostname>("\"www.example.com\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIdnEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnEmail>("\"hello@endjin.com\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonIdnEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnEmail>("\"hello@endjin.com\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIdnHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnHostname>("\"www.example.com\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonIdnHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnHostname>("\"www.example.com\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIntegerToStringThrows()
    {
        AssertDeserializationThrows<JsonInteger>("3", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonIntegerToStringThrows()
    {
        AssertDeserializationThrows<JsonInteger>("3", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIpV4ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV4>("\"192.168.0.1\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonIpV4ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV4>("\"192.168.0.1\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIpV6ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV6>("\"::ffff:192.168.0.1\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonIpV6ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV6>("\"::ffff:c0a8:0001\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIriToStringThrows()
    {
        AssertDeserializationThrows<JsonIri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonIriToStringThrows()
    {
        AssertDeserializationThrows<JsonIri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonIriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonIriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonIriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonNumberToStringThrows()
    {
        AssertDeserializationThrows<JsonNumber>("3.2", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonNumberToStringThrows()
    {
        AssertDeserializationThrows<JsonNumber>("3.2", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonPointerToStringThrows()
    {
        AssertDeserializationThrows<JsonPointer>("\"0/foo/bar\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonPointerToStringThrows()
    {
        AssertDeserializationThrows<JsonPointer>("\"0/foo/bar\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonRegexToStringThrows()
    {
        AssertDeserializationThrows<JsonRegex>("\"([abc])+\\\\s+$\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonRegexToStringThrows()
    {
        AssertDeserializationThrows<JsonRegex>("\"([abc])+\\\\s+$\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonRelativePointerToStringThrows()
    {
        AssertDeserializationThrows<JsonRelativePointer>("\"/a~1b\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonRelativePointerToStringThrows()
    {
        AssertDeserializationThrows<JsonRelativePointer>("\"/a~1b\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonTime>("\"08:30:06+00:20\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonTime>("\"08:30:06+00:20\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriToStringThrows()
    {
        AssertDeserializationThrows<JsonUri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriToStringThrows()
    {
        AssertDeserializationThrows<JsonUri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonUriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonUriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriTemplateToStringThrows()
    {
        AssertDeserializationThrows<JsonUriTemplate>("\"http://example.com/dictionary/{term:1}/{term}\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriTemplateToStringThrows()
    {
        AssertDeserializationThrows<JsonUriTemplate>("\"http://example.com/dictionary/{term:1}/{term}\"", dotnetBacked: true);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUuidToStringThrows()
    {
        AssertDeserializationThrows<JsonUuid>("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"", dotnetBacked: false);
    }

    [Fact]
    public void WriteDotnetBackedJsonUuidToStringThrows()
    {
        AssertDeserializationThrows<JsonUuid>("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"", dotnetBacked: true);
    }

    private static void AssertDeserializationThrows<T>(string jsonValue, bool dotnetBacked)
        where T : struct, IJsonValue<T>
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = false;
        try
        {
            Exception? caughtException = null;
            try
            {
                IJsonValue sut = dotnetBacked
                    ? T.Parse(jsonValue).AsDotnetBackedValue()
                    : T.Parse(jsonValue);
                string json = JsonSerializer.Serialize(sut);
                JsonSerializer.Deserialize<JsonAny>(json);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            Assert.NotNull(caughtException);
            Assert.IsAssignableFrom<InvalidOperationException>(caughtException);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }
}

#endif