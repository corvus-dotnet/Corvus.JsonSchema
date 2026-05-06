// <copyright file="JsonIpV6CastTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Collections.Immutable;
using System.Net;
using System.Text.RegularExpressions;
using Corvus.Json;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Cast;

/// <summary>
/// Tests for JsonIpV6Cast.
/// </summary>
public class JsonIpV6CastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan());
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.Equal(JsonString.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_ipV6()
    {
        JsonString sut = JsonString.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan());
        var result = (JsonIpV6)sut;
        Assert.Equal(JsonIpV6.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_IPAddress_for_json_element_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan());
        var result = (IPAddress)sut;
        Assert.Equal(IPAddress.Parse("0:0:0:0:0:ffff:c0a8:0001"), result);
    }

    [Fact]
    public void Cast_to_IPAddress_for_dotnet_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"").AsDotnetBackedValue();
        var result = (IPAddress)sut;
        Assert.Equal(IPAddress.Parse("0:0:0:0:0:ffff:c0a8:0001"), result);
    }

    [Fact]
    public void Cast_from_IPAddress_for_json_element_backed_value_as_an_ipV6()
    {
        IPAddress sut = IPAddress.Parse("::ffff:192.168.0.1");
        var result = (JsonIpV6)sut;
        Assert.Equal(JsonIpV6.ParseValue("\"::ffff:192.168.0.1\"".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_string_for_json_element_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan());
        var result = (string)sut;
        Assert.Equal("0:0:0:0:0:ffff:c0a8:0001", result);
    }

    [Fact]
    public void Cast_to_string_for_dotnet_backed_value_as_an_ipV6()
    {
        var sut = JsonIpV6.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.Equal("0:0:0:0:0:ffff:c0a8:0001", result);
    }

    [Fact]
    public void Cast_from_string_for_json_element_backed_value_as_an_ipV6()
    {
        string sut = "0:0:0:0:0:ffff:c0a8:0001";
        var result = (JsonIpV6)sut;
        Assert.Equal(JsonIpV6.ParseValue("\"0:0:0:0:0:ffff:c0a8:0001\"".AsSpan()), result);
    }
}