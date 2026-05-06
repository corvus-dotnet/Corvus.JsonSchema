// <copyright file="JsonPointerCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonPointerCast.
/// </summary>
public class JsonPointerCastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_relativePointer()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"0/foo/bar\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_relativePointer()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"0/foo/bar\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_a_relativePointer()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"0/foo/bar\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_a_relativePointer()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"0/foo/bar\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_a_relativePointer()
    {
        JsonString sut = JsonString.ParseValue("\"0/foo/bar\"".AsSpan());
        var result = (JsonRelativePointer)sut;
        Assert.Equal(JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_a_relativePointer()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("0/foo/bar", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_a_relativePointer()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("0/foo/bar", result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_a_relativePointer()
    {
        string sut = "0/foo/bar";
        var result = (JsonRelativePointer)sut;
        Assert.Equal(JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan()), result);
    }
}