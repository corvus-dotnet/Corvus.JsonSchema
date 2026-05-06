// <copyright file="JsonBase64StringCastTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Collections.Immutable;
using System.Net;
using System.Text.RegularExpressions;
using Corvus.Json;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Cast;

/// <summary>
/// Tests for JsonBase64StringCast.
/// </summary>
public class JsonBase64StringCastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_base64String_JsonBase64String()
    {
        var sut = JsonBase64String.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_base64String_JsonBase64String()
    {
        var sut = JsonBase64String.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        var sut = JsonBase64StringPre201909.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_base64String_JsonBase64String()
    {
        var sut = JsonBase64String.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_base64String_JsonBase64String()
    {
        var sut = JsonBase64String.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        var sut = JsonBase64StringPre201909.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_base64String_JsonBase64String()
    {
        JsonString sut = JsonString.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (JsonBase64String)sut;
        Assert.Equal(JsonBase64String.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        JsonString sut = JsonString.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (JsonBase64StringPre201909)sut;
        Assert.Equal(JsonBase64StringPre201909.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_an_base64String_JsonBase64String()
    {
        var sut = JsonBase64String.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("eyAiaGVsbG8iOiAid29ybGQiIH0=", result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("eyAiaGVsbG8iOiAid29ybGQiIH0=", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_an_base64String_JsonBase64String()
    {
        var sut = JsonBase64String.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("eyAiaGVsbG8iOiAid29ybGQiIH0=", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        var sut = JsonBase64StringPre201909.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("eyAiaGVsbG8iOiAid29ybGQiIH0=", result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_an_base64String_JsonBase64String()
    {
        string sut = "eyAiaGVsbG8iOiAid29ybGQiIH0=";
        var result = (JsonBase64String)sut;
        Assert.Equal(JsonBase64String.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_an_base64String_JsonBase64StringPre201909()
    {
        string sut = "eyAiaGVsbG8iOiAid29ybGQiIH0=";
        var result = (JsonBase64StringPre201909)sut;
        Assert.Equal(JsonBase64StringPre201909.ParseValue("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"".AsSpan()), result);
    }
}