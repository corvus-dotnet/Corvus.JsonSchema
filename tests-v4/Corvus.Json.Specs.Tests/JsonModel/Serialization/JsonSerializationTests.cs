// <copyright file="JsonSerializationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Buffers;
using System.Text.Json;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Serialization;

/// <summary>
/// Tests for JSON serialization round-tripping.
/// </summary>
public class JsonSerializationTests
{
    [Fact]
    public void SerializeObjectWithUndefinedPropertyValue()
    {
        // The original feature sets properties with string value "<undefined>" to JsonString.Undefined
        // which causes them to be omitted during serialization.
        JsonAny parsed = JsonAny.Parse("""{ "foo": 3, "bar": "<undefined>" }""").AsDotnetBackedValue();
        JsonObject interim = parsed.AsObject;
        foreach (JsonObjectProperty property in interim.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String &&
                property.Value.Equals("\u003cundefined\u003e"))
            {
                interim = interim.SetProperty(property.Name, JsonString.Undefined);
            }
        }

        JsonAny sut = interim;
        JsonAny result = JsonAny.Parse(sut.Serialize());
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(JsonAny.Parse("""{"foo":3}"""), result);
    }

    [Theory]
    [InlineData("""{"foo": 3, "bar": "hello", "baz": null}""", JsonValueKind.Object)]
    [InlineData("""[1,2,"3",4.0]""", JsonValueKind.Array)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("\"Hello world\"", JsonValueKind.String)]
    [InlineData("3.2", JsonValueKind.Number)]
    [InlineData("null", JsonValueKind.Null)]
    public void SerializeJsonElementBackedJsonAnyToString(string jsonValue, JsonValueKind expectedKind)
    {
        JsonAny sut = JsonAny.Parse(jsonValue);
        JsonAny result = JsonAny.Parse(sut.Serialize());
        AssertValueKind(expectedKind, result);
        Assert.Equal(JsonAny.Parse(jsonValue), result);
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
        JsonAny sut = JsonAny.Parse(jsonValue).AsDotnetBackedValue();
        JsonAny result = JsonAny.Parse(sut.Serialize());
        AssertValueKind(expectedKind, result);
        Assert.Equal(JsonAny.Parse(jsonValue), result);
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
        JsonAny sut = JsonAny.Parse(jsonValue);
        JsonAny result = RoundTripViaWriter(sut);
        AssertValueKind(expectedKind, result);
        Assert.Equal(JsonAny.Parse(jsonValue), result);
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
        JsonAny sut = JsonAny.Parse(jsonValue).AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        AssertValueKind(expectedKind, result);
        Assert.Equal(JsonAny.Parse(jsonValue), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonObjectToString()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": 3, "bar": "hello", "baz": null}""");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(JsonAny.Parse("""{"foo": 3, "bar": "hello", "baz": null}"""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonObjectToString()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": 3, "bar": "hello", "baz": null}""").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(JsonAny.Parse("""{"foo": 3, "bar": "hello", "baz": null}"""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonObjectToStringWithPrettyFormatting()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": 3, "bar": "hello", "baz": null}""");
        string result = sut.AsAny.Serialize(new JsonSerializerOptions { WriteIndented = true });
        string expected = "{\r\n  \"foo\": 3,\r\n  \"bar\": \"hello\",\r\n  \"baz\": null\r\n}".Replace("\\r\\n", Environment.NewLine);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WriteDotnetBackedJsonObjectToStringWithPrettyFormatting()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": 3, "bar": "hello", "baz": null}""").AsDotnetBackedValue();
        string result = sut.AsAny.Serialize(new JsonSerializerOptions { WriteIndented = true });
        string expected = "{\r\n  \"foo\": 3,\r\n  \"bar\": \"hello\",\r\n  \"baz\": null\r\n}".Replace("\\r\\n", Environment.NewLine);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonArrayToString()
    {
        JsonArray sut = JsonArray.Parse("""[1,2,"3",4.0]""");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(JsonAny.Parse("""[1,2,"3",4.0]"""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonArrayToString()
    {
        JsonArray sut = JsonArray.Parse("""[1,2,"3",4.0]""").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(JsonAny.Parse("""[1,2,"3",4.0]"""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonBooleanToString()
    {
        JsonBoolean sut = JsonBoolean.Parse("true");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.True(result.ValueKind == JsonValueKind.True || result.ValueKind == JsonValueKind.False);
        Assert.Equal(JsonAny.Parse("true"), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonBooleanToString()
    {
        JsonBoolean sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.True(result.ValueKind == JsonValueKind.True || result.ValueKind == JsonValueKind.False);
        Assert.Equal(JsonAny.Parse("true"), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonStringToString()
    {
        JsonString sut = JsonString.Parse("\"Hello, World\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"Hello, World\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonStringToString()
    {
        JsonString sut = JsonString.Parse("\"Hello, World\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"Hello, World\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonBase64ContentToString()
    {
        JsonBase64Content sut = JsonBase64Content.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonBase64ContentToString()
    {
        JsonBase64Content sut = JsonBase64Content.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonBase64StringToString()
    {
        JsonBase64String sut = JsonBase64String.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonBase64StringToString()
    {
        JsonBase64String sut = JsonBase64String.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonContentToString()
    {
        JsonContent sut = JsonContent.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonContentToString()
    {
        JsonContent sut = JsonContent.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDateToString()
    {
        JsonDate sut = JsonDate.Parse("\"2018-11-13\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"2018-11-13\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonDateToString()
    {
        JsonDate sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"2018-11-13\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDateTimeToString()
    {
        JsonDateTime sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonDateTimeToString()
    {
        JsonDateTime sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonDurationToString()
    {
        JsonDuration sut = JsonDuration.Parse("\"P3Y6M4DT12H30M5S\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonDurationToString()
    {
        JsonDuration sut = JsonDuration.Parse("\"P3Y6M4DT12H30M5S\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonEmailToString()
    {
        JsonEmail sut = JsonEmail.Parse("\"hello@endjin.com\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"hello@endjin.com\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonEmailToString()
    {
        JsonEmail sut = JsonEmail.Parse("\"hello@endjin.com\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"hello@endjin.com\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonHostnameToString()
    {
        JsonHostname sut = JsonHostname.Parse("\"www.example.com\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"www.example.com\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonHostnameToString()
    {
        JsonHostname sut = JsonHostname.Parse("\"www.example.com\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"www.example.com\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIdnEmailToString()
    {
        JsonIdnEmail sut = JsonIdnEmail.Parse("\"hello@endjin.com\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"hello@endjin.com\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonIdnEmailToString()
    {
        JsonIdnEmail sut = JsonIdnEmail.Parse("\"hello@endjin.com\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"hello@endjin.com\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIdnHostnameToString()
    {
        JsonIdnHostname sut = JsonIdnHostname.Parse("\"www.example.com\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"www.example.com\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonIdnHostnameToString()
    {
        JsonIdnHostname sut = JsonIdnHostname.Parse("\"www.example.com\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"www.example.com\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIntegerToString()
    {
        JsonInteger sut = JsonInteger.Parse("3");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(JsonAny.Parse("3"), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonIntegerToString()
    {
        JsonInteger sut = JsonInteger.Parse("3").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(JsonAny.Parse("3"), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIpV4ToString()
    {
        JsonIpV4 sut = JsonIpV4.Parse("\"192.168.0.1\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"192.168.0.1\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonIpV4ToString()
    {
        JsonIpV4 sut = JsonIpV4.Parse("\"192.168.0.1\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"192.168.0.1\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIpV6ToString()
    {
        JsonIpV6 sut = JsonIpV6.Parse("\"::ffff:192.168.0.1\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"::ffff:192.168.0.1\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonIpV6ToString()
    {
        JsonIpV6 sut = JsonIpV6.Parse("\"::ffff:c0a8:0001\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"::ffff:c0a8:0001\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIriToString()
    {
        JsonIri sut = JsonIri.Parse("\"http://foo.bar/?baz=qux#quux\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonIriToString()
    {
        JsonIri sut = JsonIri.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonIriReferenceToString()
    {
        JsonIriReference sut = JsonIriReference.Parse("\"http://foo.bar/?baz=qux#quux\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonIriReferenceToString()
    {
        JsonIriReference sut = JsonIriReference.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonNumberToString()
    {
        JsonNumber sut = JsonNumber.Parse("3.2");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(JsonAny.Parse("3.2"), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonNumberToString()
    {
        JsonNumber sut = JsonNumber.Parse("3.2").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(JsonAny.Parse("3.2"), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonPointerToString()
    {
        JsonPointer sut = JsonPointer.Parse("\"0/foo/bar\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"0/foo/bar\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonPointerToString()
    {
        JsonPointer sut = JsonPointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"0/foo/bar\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonRegexToString()
    {
        JsonRegex sut = JsonRegex.Parse("\"([abc])+\\\\s+$\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"([abc])+\\\\s+$\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonRegexToString()
    {
        JsonRegex sut = JsonRegex.Parse("\"([abc])+\\\\s+$\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"([abc])+\\\\s+$\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonRelativePointerToString()
    {
        JsonRelativePointer sut = JsonRelativePointer.Parse("\"/a~1b\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"/a~1b\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonRelativePointerToString()
    {
        JsonRelativePointer sut = JsonRelativePointer.Parse("\"/a~1b\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"/a~1b\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonTimeToString()
    {
        JsonTime sut = JsonTime.Parse("\"08:30:06+00:20\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"08:30:06+00:20\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonTimeToString()
    {
        JsonTime sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"08:30:06+00:20\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriToString()
    {
        JsonUri sut = JsonUri.Parse("\"http://foo.bar/?baz=qux#quux\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriToString()
    {
        JsonUri sut = JsonUri.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUriReferenceToString()
    {
        JsonUriReference sut = JsonUriReference.Parse("\"http://foo.bar/?baz=qux#quux\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriReferenceToString()
    {
        JsonUriReference sut = JsonUriReference.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""), result);
    }

    [Fact]
    public void WriteJsonElementBackedJsonUuidToString()
    {
        JsonUuid sut = JsonUuid.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"");
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\""), result);
    }

    [Fact]
    public void WriteDotnetBackedJsonUuidToString()
    {
        JsonUuid sut = JsonUuid.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"").AsDotnetBackedValue();
        JsonAny result = RoundTripViaWriter(sut);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal(JsonAny.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\""), result);
    }

    private static JsonAny RoundTripViaWriter<T>(T value)
        where T : struct, IJsonValue<T>
    {
        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        ((IJsonValue)value).WriteTo(writer);
        writer.Flush();
        return JsonAny.ParseValue(abw.WrittenSpan);
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