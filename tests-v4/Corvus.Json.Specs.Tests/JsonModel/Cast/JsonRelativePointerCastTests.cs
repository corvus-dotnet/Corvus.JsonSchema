// <copyright file="JsonRelativePointerCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonRelativePointerCast.
/// </summary>
public class JsonRelativePointerCastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.ParseValue("\"/a~1b\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.Parse("\"/a~1b\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.ParseValue("\"/a~1b\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.Parse("\"/a~1b\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_a_pointer()
    {
        JsonString sut = JsonString.ParseValue("\"/a~1b\"".AsSpan());
        var result = (JsonPointer)sut;
        Assert.Equal(JsonPointer.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.ParseValue("\"/a~1b\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("/a~1b", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.Parse("\"/a~1b\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("/a~1b", result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_a_pointer()
    {
        string sut = "/a~1b";
        var result = (JsonPointer)sut;
        Assert.Equal(JsonPointer.ParseValue("\"/a~1b\"".AsSpan()), result);
    }
}