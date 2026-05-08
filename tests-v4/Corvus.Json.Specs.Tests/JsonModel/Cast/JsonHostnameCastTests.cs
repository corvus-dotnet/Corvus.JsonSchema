// <copyright file="JsonHostnameCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonHostnameCast.
/// </summary>
[TestClass]
public class JsonHostnameCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_hostname()
    {
        var sut = JsonHostname.ParseValue("\"www.example.com\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"www.example.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_hostname()
    {
        var sut = JsonHostname.Parse("\"www.example.com\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"www.example.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_a_hostname()
    {
        var sut = JsonHostname.ParseValue("\"www.example.com\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"www.example.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_a_hostname()
    {
        var sut = JsonHostname.Parse("\"www.example.com\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"www.example.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_a_hostname()
    {
        JsonString sut = JsonString.ParseValue("\"www.example.com\"".AsSpan());
        var result = (JsonHostname)sut;
        Assert.AreEqual(JsonHostname.ParseValue("\"www.example.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_a_hostname()
    {
        var sut = JsonHostname.ParseValue("\"www.example.com\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("www.example.com", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_a_hostname()
    {
        var sut = JsonHostname.Parse("\"www.example.com\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("www.example.com", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_a_hostname()
    {
        string sut = "www.example.com";
        var result = (JsonHostname)sut;
        Assert.AreEqual(JsonHostname.ParseValue("\"www.example.com\"".AsSpan()), result);
    }
}