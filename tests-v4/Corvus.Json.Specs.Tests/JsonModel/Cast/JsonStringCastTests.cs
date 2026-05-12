// <copyright file="JsonStringCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonStringCast.
/// </summary>
[TestClass]
public class JsonStringCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_string()
    {
        var sut = JsonString.ParseValue("\"Hello\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"Hello\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_string()
    {
        var sut = JsonString.Parse("\"Hello\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"Hello\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_a_string()
    {
        var sut = JsonString.ParseValue("\"Hello\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("Hello", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_a_string()
    {
        string sut = "Hello";
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"Hello\"".AsSpan()), result);
    }
}