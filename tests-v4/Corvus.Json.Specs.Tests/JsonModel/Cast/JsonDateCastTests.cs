// <copyright file="JsonDateCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonDateCast.
/// </summary>
[TestClass]
public class JsonDateCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_date()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"2018-11-13\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_date()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"2018-11-13\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_date()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"2018-11-13\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_date()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"2018-11-13\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_date()
    {
        JsonString sut = JsonString.ParseValue("\"2018-11-13\"".AsSpan());
        var result = (JsonDate)sut;
        Assert.AreEqual(JsonDate.ParseValue("\"2018-11-13\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_LocalDate_for_json_element_backed_value_as_an_date()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        var result = (LocalDate)sut;
        Assert.AreEqual(LocalDatePattern.Iso.Parse("2018-11-13").Value, result);
    }

    [TestMethod]
    public void Cast_to_LocalDate_for_json_element_backed_value_as_an_date()
    {
        LocalDate sut = LocalDatePattern.Iso.Parse("2018-11-13").Value;
        var result = (JsonDate)sut;
        Assert.AreEqual(JsonDate.ParseValue("\"2018-11-13\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_an_date()
    {
        var sut = JsonDate.ParseValue("\"2018-11-13\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("2018-11-13", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_an_date()
    {
        var sut = JsonDate.Parse("\"2018-11-13\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("2018-11-13", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_an_date()
    {
        string sut = "2018-11-13";
        var result = (JsonDate)sut;
        Assert.AreEqual(JsonDate.ParseValue("\"2018-11-13\"".AsSpan()), result);
    }
}