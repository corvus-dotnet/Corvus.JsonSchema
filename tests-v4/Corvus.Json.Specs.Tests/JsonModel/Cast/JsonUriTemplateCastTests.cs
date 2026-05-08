// <copyright file="JsonUriTemplateCastTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Collections.Immutable;
using System.Net;
using System.Text.RegularExpressions;
using Corvus.Json;
using NodaTime;
using NodaTime.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Cast;

/// <summary>
/// Tests for JsonUriTemplateCast.
/// </summary>
[TestClass]
public class JsonUriTemplateCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_uriTemplate()
    {
        var sut = JsonUriTemplate.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_uriTemplate()
    {
        var sut = JsonUriTemplate.Parse("\"http://example.com/dictionary/{term:1}/{term}\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_uriTemplate()
    {
        var sut = JsonUriTemplate.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_uriTemplate()
    {
        var sut = JsonUriTemplate.Parse("\"http://example.com/dictionary/{term:1}/{term}\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_uriTemplate()
    {
        JsonString sut = JsonString.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan());
        var result = (JsonUriTemplate)sut;
        Assert.AreEqual(JsonUriTemplate.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_an_uriTemplate()
    {
        var sut = JsonUriTemplate.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("http://example.com/dictionary/{term:1}/{term}", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_an_uriTemplate()
    {
        var sut = JsonUriTemplate.Parse("\"http://example.com/dictionary/{term:1}/{term}\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("http://example.com/dictionary/{term:1}/{term}", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_an_uriTemplate()
    {
        string sut = "http://example.com/dictionary/{term:1}/{term}";
        var result = (JsonUriTemplate)sut;
        Assert.AreEqual(JsonUriTemplate.ParseValue("\"http://example.com/dictionary/{term:1}/{term}\"".AsSpan()), result);
    }
}