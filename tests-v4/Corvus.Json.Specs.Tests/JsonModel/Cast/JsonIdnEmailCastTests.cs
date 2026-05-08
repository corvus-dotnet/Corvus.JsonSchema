// <copyright file="JsonIdnEmailCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonIdnEmailCast.
/// </summary>
[TestClass]
public class JsonIdnEmailCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_idnEmail()
    {
        var sut = JsonIdnEmail.ParseValue("\"hello@endjin.com\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"hello@endjin.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_idnEmail()
    {
        var sut = JsonIdnEmail.Parse("\"hello@endjin.com\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"hello@endjin.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_idnEmail()
    {
        var sut = JsonIdnEmail.ParseValue("\"hello@endjin.com\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"hello@endjin.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_idnEmail()
    {
        var sut = JsonIdnEmail.Parse("\"hello@endjin.com\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"hello@endjin.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_idnEmail()
    {
        JsonString sut = JsonString.ParseValue("\"hello@endjin.com\"".AsSpan());
        var result = (JsonIdnEmail)sut;
        Assert.AreEqual(JsonIdnEmail.ParseValue("\"hello@endjin.com\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_an_idnEmail()
    {
        var sut = JsonIdnEmail.ParseValue("\"hello@endjin.com\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("hello@endjin.com", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_an_idnEmail()
    {
        var sut = JsonIdnEmail.Parse("\"hello@endjin.com\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("hello@endjin.com", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_an_idnEmail()
    {
        string sut = "hello@endjin.com";
        var result = (JsonIdnEmail)sut;
        Assert.AreEqual(JsonIdnEmail.ParseValue("\"hello@endjin.com\"".AsSpan()), result);
    }
}