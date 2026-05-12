// <copyright file="JsonDateTimeCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonDateTimeCast.
/// </summary>
[TestClass]
public class JsonDateTimeCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_dateTime()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_dateTime()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_dateTime()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_dateTime()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_dateTime()
    {
        JsonString sut = JsonString.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        var result = (JsonDateTime)sut;
        Assert.AreEqual(JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_OffsetDateTime_for_json_element_backed_value_as_an_dateTime()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        var result = (OffsetDateTime)sut;
        Assert.AreEqual(OffsetDateTimePattern.ExtendedIso.Parse("2018-11-13T20:20:39+00:00").Value, result);
    }

    [TestMethod]
    public void Cast_to_OffsetDateTime_for_json_element_backed_value_as_an_dateTime()
    {
        OffsetDateTime sut = OffsetDateTimePattern.ExtendedIso.Parse("2018-11-13T20:20:39+00:00").Value;
        var result = (JsonDateTime)sut;
        Assert.AreEqual(JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_an_dateTime()
    {
        var sut = JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("2018-11-13T20:20:39+00:00", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_an_dateTime()
    {
        var sut = JsonDateTime.Parse("\"2018-11-13T20:20:39+00:00\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("2018-11-13T20:20:39+00:00", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_an_dateTime()
    {
        string sut = "2018-11-13T20:20:39+00:00";
        var result = (JsonDateTime)sut;
        Assert.AreEqual(JsonDateTime.ParseValue("\"2018-11-13T20:20:39+00:00\"".AsSpan()), result);
    }
}