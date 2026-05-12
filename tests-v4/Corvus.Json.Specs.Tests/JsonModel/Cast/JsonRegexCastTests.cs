// <copyright file="JsonRegexCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonRegexCast.
/// </summary>
[TestClass]
public class JsonRegexCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_regex()
    {
        var sut = JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"([abc])+\\\\s+$\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_regex()
    {
        var sut = JsonRegex.Parse("\"([abc])+\\\\s+$\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"([abc])+\\\\s+$\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_a_regex()
    {
        var sut = JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"([abc])+\\\\s+$\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_a_regex()
    {
        var sut = JsonRegex.Parse("\"([abc])+\\\\s+$\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"([abc])+\\\\s+$\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_a_regex()
    {
        JsonString sut = JsonString.ParseValue("\"([abc])+\\\\s+$\"".AsSpan());
        var result = (JsonRegex)sut;
        Assert.AreEqual(JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_Regex_for_json_element_backed_value_as_a_regex()
    {
        var sut = JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan());
        var result = (Regex)sut;
        Assert.AreEqual("([abc])+\\s+$", result.ToString());
    }

    [TestMethod]
    public void Cast_to_Regex_for_dotnet_backed_value_as_a_regex()
    {
        var sut = JsonRegex.Parse("\"([abc])+\\\\s+$\"").AsDotnetBackedValue();
        var result = (Regex)sut;
        Assert.AreEqual("([abc])+\\s+$", result.ToString());
    }

    [TestMethod]
    public void Cast_from_Regex_for_json_element_backed_value_as_a_regex()
    {
        Regex sut = new Regex("([abc])+\\s+$");
        var result = (JsonRegex)sut;
        Assert.AreEqual(JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_a_regex()
    {
        var sut = JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("([abc])+\\s+$", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_a_regex()
    {
        var sut = JsonRegex.Parse("\"([abc])+\\\\s+$\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("([abc])+\\s+$", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_a_regex()
    {
        string sut = "([abc])+\\s+$";
        var result = (JsonRegex)sut;
        Assert.AreEqual(JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan()), result);
    }
}