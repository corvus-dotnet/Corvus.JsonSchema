// <copyright file="JsonTimeCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonTimeCast.
/// </summary>
[TestClass]
public class JsonTimeCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_time()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"08:30:06+00:20\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_time()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"08:30:06+00:20\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_a_time()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"08:30:06+00:20\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_a_time()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"08:30:06+00:20\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_a_time()
    {
        JsonString sut = JsonString.ParseValue("\"08:30:06+00:20\"".AsSpan());
        var result = (JsonTime)sut;
        Assert.AreEqual(JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_OffsetTime_for_json_element_backed_value_as_a_time()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        var result = (OffsetTime)sut;
        Assert.AreEqual(OffsetTimePattern.ExtendedIso.Parse("08:30:06+00:20").Value, result);
    }

    [TestMethod]
    public void Cast_to_OffsetTime_for_json_element_backed_value_as_a_time()
    {
        OffsetTime sut = OffsetTimePattern.ExtendedIso.Parse("08:30:06+00:20").Value;
        var result = (JsonTime)sut;
        Assert.AreEqual(JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_a_time()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("08:30:06+00:20", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_a_time()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("08:30:06+00:20", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_a_time()
    {
        string sut = "08:30:06+00:20";
        var result = (JsonTime)sut;
        Assert.AreEqual(JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan()), result);
    }
}