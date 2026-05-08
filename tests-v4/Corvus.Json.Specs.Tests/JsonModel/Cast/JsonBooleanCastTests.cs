// <copyright file="JsonBooleanCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonBooleanCast.
/// </summary>
[TestClass]
public class JsonBooleanCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_boolean()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("true".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_boolean()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("true".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_bool_for_json_element_backed_value_as_a_boolean()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        var result = (bool)sut;
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Cast_to_bool_for_dotnet_backed_value_as_a_boolean()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        var result = (bool)sut;
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Cast_from_bool_for_json_element_backed_value_as_a_boolean()
    {
        bool sut = true;
        var result = (JsonBoolean)sut;
        Assert.AreEqual(JsonBoolean.ParseValue("true".AsSpan()), result);
    }
}