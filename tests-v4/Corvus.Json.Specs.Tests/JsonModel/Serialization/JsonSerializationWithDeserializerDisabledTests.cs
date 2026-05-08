// <copyright file="JsonSerializationWithDeserializerDisabledTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

#if NET8_0_OR_GREATER

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Serialization;

/// <summary>
/// Tests for JSON serialization round-tripping via System.Text.Json serializer
/// with inefficient deserialization support disabled — expects InvalidOperationException.
/// </summary>
[DoNotParallelize]
[TestClass]
public class JsonSerializationWithDeserializerDisabledTests
{
    [TestMethod]
    [DataRow("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [DataRow("""[1,2,"3",4.0]""")]
    [DataRow("true")]
    [DataRow("\"Hello world\"")]
    [DataRow("3.2")]
    [DataRow("null")]
    public void SerializeJsonElementBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: false);
    }

    [TestMethod]
    [DataRow("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [DataRow("""[1,2,"3",4.0]""")]
    [DataRow("true")]
    [DataRow("\"Hello world\"")]
    [DataRow("3.2")]
    [DataRow("null")]
    public void SerializeDotnetBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: true);
    }

    [TestMethod]
    [DataRow("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [DataRow("""[1,2,"3",4.0]""")]
    [DataRow("true")]
    [DataRow("\"Hello world\"")]
    [DataRow("3.2")]
    [DataRow("null")]
    public void WriteJsonElementBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: false);
    }

    [TestMethod]
    [DataRow("""{"foo": 3, "bar": "hello", "baz": null}""")]
    [DataRow("""[1,2,"3",4.0]""")]
    [DataRow("true")]
    [DataRow("\"Hello world\"")]
    [DataRow("3.2")]
    [DataRow("null")]
    public void WriteDotnetBackedJsonAnyToStringThrows(string jsonValue)
    {
        AssertDeserializationThrows<JsonAny>(jsonValue, dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonObjectToStringThrows()
    {
        AssertDeserializationThrows<JsonObject>("""{"foo": 3, "bar": "hello", "baz": null}""", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonObjectToStringThrows()
    {
        AssertDeserializationThrows<JsonObject>("""{"foo": 3, "bar": "hello", "baz": null}""", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonArrayToStringThrows()
    {
        AssertDeserializationThrows<JsonArray>("""[1,2,"3",4.0]""", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonArrayToStringThrows()
    {
        AssertDeserializationThrows<JsonArray>("""[1,2,"3",4.0]""", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonBooleanToStringThrows()
    {
        AssertDeserializationThrows<JsonBoolean>("true", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonBooleanToStringThrows()
    {
        AssertDeserializationThrows<JsonBoolean>("true", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonStringToStringThrows()
    {
        AssertDeserializationThrows<JsonString>("\"Hello, World\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonStringToStringThrows()
    {
        AssertDeserializationThrows<JsonString>("\"Hello, World\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonBase64ContentToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64Content>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonBase64ContentToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64Content>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonBase64StringToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64String>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonBase64StringToStringThrows()
    {
        AssertDeserializationThrows<JsonBase64String>("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonContentToStringThrows()
    {
        AssertDeserializationThrows<JsonContent>("\"{\\\"foo\\\": \\\"bar\\\"}\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonContentToStringThrows()
    {
        AssertDeserializationThrows<JsonContent>("\"{\\\"foo\\\": \\\"bar\\\"}\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonDateToStringThrows()
    {
        AssertDeserializationThrows<JsonDate>("\"2018-11-13\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonDateToStringThrows()
    {
        AssertDeserializationThrows<JsonDate>("\"2018-11-13\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonDateTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonDateTime>("\"2018-11-13T20:20:39+00:00\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonDateTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonDateTime>("\"2018-11-13T20:20:39+00:00\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonDurationToStringThrows()
    {
        AssertDeserializationThrows<JsonDuration>("\"P3Y6M4DT12H30M5S\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonDurationToStringThrows()
    {
        AssertDeserializationThrows<JsonDuration>("\"P3Y6M4DT12H30M5S\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonEmail>("\"hello@endjin.com\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonEmail>("\"hello@endjin.com\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonHostname>("\"www.example.com\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonHostname>("\"www.example.com\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonIdnEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnEmail>("\"hello@endjin.com\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonIdnEmailToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnEmail>("\"hello@endjin.com\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonIdnHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnHostname>("\"www.example.com\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonIdnHostnameToStringThrows()
    {
        AssertDeserializationThrows<JsonIdnHostname>("\"www.example.com\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonIntegerToStringThrows()
    {
        AssertDeserializationThrows<JsonInteger>("3", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonIntegerToStringThrows()
    {
        AssertDeserializationThrows<JsonInteger>("3", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonIpV4ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV4>("\"192.168.0.1\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonIpV4ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV4>("\"192.168.0.1\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonIpV6ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV6>("\"::ffff:192.168.0.1\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonIpV6ToStringThrows()
    {
        AssertDeserializationThrows<JsonIpV6>("\"::ffff:c0a8:0001\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonIriToStringThrows()
    {
        AssertDeserializationThrows<JsonIri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonIriToStringThrows()
    {
        AssertDeserializationThrows<JsonIri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonIriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonIriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonIriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonIriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonNumberToStringThrows()
    {
        AssertDeserializationThrows<JsonNumber>("3.2", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonNumberToStringThrows()
    {
        AssertDeserializationThrows<JsonNumber>("3.2", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonPointerToStringThrows()
    {
        AssertDeserializationThrows<JsonPointer>("\"0/foo/bar\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonPointerToStringThrows()
    {
        AssertDeserializationThrows<JsonPointer>("\"0/foo/bar\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonRegexToStringThrows()
    {
        AssertDeserializationThrows<JsonRegex>("\"([abc])+\\\\s+$\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonRegexToStringThrows()
    {
        AssertDeserializationThrows<JsonRegex>("\"([abc])+\\\\s+$\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonRelativePointerToStringThrows()
    {
        AssertDeserializationThrows<JsonRelativePointer>("\"/a~1b\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonRelativePointerToStringThrows()
    {
        AssertDeserializationThrows<JsonRelativePointer>("\"/a~1b\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonTime>("\"08:30:06+00:20\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonTimeToStringThrows()
    {
        AssertDeserializationThrows<JsonTime>("\"08:30:06+00:20\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonUriToStringThrows()
    {
        AssertDeserializationThrows<JsonUri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonUriToStringThrows()
    {
        AssertDeserializationThrows<JsonUri>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonUriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonUriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonUriReferenceToStringThrows()
    {
        AssertDeserializationThrows<JsonUriReference>("\"http://foo.bar/?baz=qux#quux\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonUriTemplateToStringThrows()
    {
        AssertDeserializationThrows<JsonUriTemplate>("\"http://example.com/dictionary/{term:1}/{term}\"", dotnetBacked: false);
    }

    [TestMethod]
    public void WriteDotnetBackedJsonUriTemplateToStringThrows()
    {
        AssertDeserializationThrows<JsonUriTemplate>("\"http://example.com/dictionary/{term:1}/{term}\"", dotnetBacked: true);
    }

    [TestMethod]
    public void WriteJsonElementBackedJsonUuidToStringThrows()
    {
        AssertDeserializationThrows<JsonUuid>("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"", dotnetBacked: false);
    }

    [TestMethod]
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

            Assert.IsNotNull(caughtException);
            Assert.IsInstanceOfType<InvalidOperationException>(caughtException);
        }
        finally
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
        }
    }
}

#endif