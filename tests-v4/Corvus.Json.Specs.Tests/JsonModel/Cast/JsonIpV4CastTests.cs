// <copyright file="JsonIpV4CastTests.cs" company="Endjin Limited">
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
/// Tests for JsonIpV4Cast.
/// </summary>
[TestClass]
public class JsonIpV4CastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"192.168.0.1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.Parse("\"192.168.0.1\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"192.168.0.1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"192.168.0.1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.Parse("\"192.168.0.1\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"192.168.0.1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_ipV4()
    {
        JsonString sut = JsonString.ParseValue("\"192.168.0.1\"".AsSpan());
        var result = (JsonIpV4)sut;
        Assert.AreEqual(JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_IPAddress_for_json_element_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan());
        var result = (IPAddress)sut;
        Assert.AreEqual(IPAddress.Parse("192.168.0.1"), result);
    }

    [TestMethod]
    public void Cast_to_IPAddress_for_dotnet_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.Parse("\"192.168.0.1\"").AsDotnetBackedValue();
        var result = (IPAddress)sut;
        Assert.AreEqual(IPAddress.Parse("192.168.0.1"), result);
    }

    [TestMethod]
    public void Cast_from_IPAddress_for_json_element_backed_value_as_an_ipV4()
    {
        IPAddress sut = IPAddress.Parse("192.168.0.1");
        var result = (JsonIpV4)sut;
        Assert.AreEqual(JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("192.168.0.1", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_an_ipV4()
    {
        var sut = JsonIpV4.Parse("\"192.168.0.1\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("192.168.0.1", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_an_ipV4()
    {
        string sut = "192.168.0.1";
        var result = (JsonIpV4)sut;
        Assert.AreEqual(JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan()), result);
    }
}