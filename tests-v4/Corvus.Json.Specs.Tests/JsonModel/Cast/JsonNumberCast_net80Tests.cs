// <copyright file="JsonNumberCast_net80Tests.cs" company="Endjin Limited">
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
/// Tests for JsonNumberCast-net80.
/// </summary>
[TestClass]
public class JsonNumberCast_net80Tests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("1.2".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(1.2d, (double)result.AsNumber, 3);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        long sut = 12L;
        var result = (JsonHalf)sut;
        Assert.AreEqual(JsonHalf.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(1.2d, (double)result, 3);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(1.2d, (double)result, 3);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        double sut = 1.2;
        var result = (JsonHalf)sut;
        Assert.AreEqual(JsonHalf.ParseValue("1.2".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_decimal_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (decimal)sut;
        Assert.AreEqual(1.2d, (double)result, 3);
    }

    [TestMethod]
    public void Cast_to_decimal_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (decimal)sut;
        Assert.AreEqual(1.2d, (double)result, 3);
    }

    [TestMethod]
    public void Cast_from_decimal_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        decimal sut = 1.2m;
        var result = (JsonHalf)sut;
        Assert.AreEqual(JsonHalf.ParseValue("1.2".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        int sut = 12;
        var result = (JsonHalf)sut;
        Assert.AreEqual(JsonHalf.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(1.2f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(1.2d, (double)result, 3);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        float sut = 1.2f;
        var result = (JsonHalf)sut;
        Assert.AreEqual(JsonHalf.ParseValue("1.2".AsSpan()), result);
    }
}