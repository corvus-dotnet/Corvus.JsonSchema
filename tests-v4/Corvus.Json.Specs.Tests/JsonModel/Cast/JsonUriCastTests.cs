// <copyright file="JsonUriCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonUriCast.
/// </summary>
public class JsonUriCastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_uri()
    {
        var sut = JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_uri()
    {
        var sut = JsonUri.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_uri()
    {
        var sut = JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_uri()
    {
        var sut = JsonUri.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_uri()
    {
        JsonString sut = JsonString.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (JsonUri)sut;
        Assert.Equal(JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_Uri_for_json_element_backed_value_as_an_uri()
    {
        var sut = JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (Uri)sut;
        Assert.Equal(new Uri("http://foo.bar/?baz=qux#quux", UriKind.RelativeOrAbsolute), result);
    }

    [Fact]
    public void Cast_to_Uri_for_dotnet_backed_value_as_an_uri()
    {
        var sut = JsonUri.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (Uri)sut;
        Assert.Equal(new Uri("http://foo.bar/?baz=qux#quux", UriKind.RelativeOrAbsolute), result);
    }

    [Fact]
    public void Cast_from_Uri_for_json_element_backed_value_as_an_uri()
    {
        Uri sut = new Uri("http://foo.bar/?baz=qux#quux", UriKind.RelativeOrAbsolute);
        var result = (JsonUri)sut;
        Assert.Equal(JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_an_uri()
    {
        var sut = JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("http://foo.bar/?baz=qux#quux", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_an_uri()
    {
        var sut = JsonUri.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("http://foo.bar/?baz=qux#quux", result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_an_uri()
    {
        string sut = "http://foo.bar/?baz=qux#quux";
        var result = (JsonUri)sut;
        Assert.Equal(JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }
}