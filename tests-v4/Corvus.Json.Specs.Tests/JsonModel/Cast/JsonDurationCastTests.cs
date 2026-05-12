// <copyright file="JsonDurationCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonDurationCast.
/// </summary>
[TestClass]
public class JsonDurationCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_duration()
    {
        var sut = JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_duration()
    {
        var sut = JsonDuration.Parse("\"P3Y6M4DT12H30M5S\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_a_duration()
    {
        var sut = JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_a_duration()
    {
        var sut = JsonDuration.Parse("\"P3Y6M4DT12H30M5S\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_a_duration()
    {
        JsonString sut = JsonString.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan());
        var result = (JsonDuration)sut;
        Assert.AreEqual(JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_Period_for_json_element_backed_value_as_a_duration()
    {
        var sut = JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan());
        var result = (NodaTime.Period)sut;
        Assert.AreEqual(PeriodPattern.NormalizingIso.Parse("P3Y6M4DT12H30M5S").Value, (NodaTime.Period)result);
    }

    [TestMethod]
    public void Cast_to_Period_for_json_element_backed_value_as_a_duration()
    {
        Period sut = PeriodPattern.NormalizingIso.Parse("P3Y6M4DT12H30M5S").Value;
        var result = (JsonDuration)sut;
        Assert.AreEqual(JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_Corvus_Period_for_json_element_backed_value_as_a_duration()
    {
        var sut = JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan());
        Corvus.Json.Period result = (Corvus.Json.Period)sut;
        Assert.AreEqual(Corvus.Json.Period.Parse("P3Y6M4DT12H30M5S".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_Corvus_Period_for_json_element_backed_value_as_a_duration()
    {
        Corvus.Json.Period sut = Corvus.Json.Period.Parse("P3Y6M4DT12H30M5S".AsSpan());
        var result = (JsonDuration)sut;
        Assert.AreEqual(JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_a_duration()
    {
        var sut = JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("P3Y6M4DT12H30M5S", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_a_duration()
    {
        var sut = JsonDuration.Parse("\"P3Y6M4DT12H30M5S\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("P3Y6M4DT12H30M5S", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_a_duration()
    {
        string sut = "P3Y6M4DT12H30M5S";
        var result = (JsonDuration)sut;
        Assert.AreEqual(JsonDuration.ParseValue("\"P3Y6M4DT12H30M5S\"".AsSpan()), result);
    }
}