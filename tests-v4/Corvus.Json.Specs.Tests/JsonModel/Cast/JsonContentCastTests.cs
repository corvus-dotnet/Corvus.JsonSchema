// <copyright file="JsonContentCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonContentCast.
/// </summary>
public class JsonContentCastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_content_JsonContent()
    {
        var sut = JsonContent.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_content_JsonContentPre201909()
    {
        var sut = JsonContentPre201909.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_content_JsonContent()
    {
        var sut = JsonContent.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_content_JsonContentPre201909()
    {
        var sut = JsonContentPre201909.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_content_JsonContent()
    {
        var sut = JsonContent.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_content_JsonContentPre201909()
    {
        var sut = JsonContentPre201909.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_content_JsonContent()
    {
        var sut = JsonContent.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_content_JsonContentPre201909()
    {
        var sut = JsonContentPre201909.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_content_JsonContent()
    {
        JsonString sut = JsonString.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (JsonContent)sut;
        Assert.Equal(JsonContent.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_content_JsonContentPre201909()
    {
        JsonString sut = JsonString.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (JsonContentPre201909)sut;
        Assert.Equal(JsonContentPre201909.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_an_content_JsonContent()
    {
        var sut = JsonContent.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("{\"foo\": \"bar\"}", result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_an_content_JsonContentPre201909()
    {
        var sut = JsonContentPre201909.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("{\"foo\": \"bar\"}", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_an_content_JsonContent()
    {
        var sut = JsonContent.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("{\"foo\": \"bar\"}", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_an_content_JsonContentPre201909()
    {
        var sut = JsonContentPre201909.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("{\"foo\": \"bar\"}", result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_an_content_JsonContent()
    {
        string sut = "{\\\"foo\\\": \\\"bar\\\"}";
        var result = (JsonContent)sut;
        Assert.Equal(JsonContent.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_an_content_JsonContentPre201909()
    {
        string sut = "{\\\"foo\\\": \\\"bar\\\"}";
        var result = (JsonContentPre201909)sut;
        Assert.Equal(JsonContentPre201909.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan()), result);
    }
}