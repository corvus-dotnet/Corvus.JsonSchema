// <copyright file="JsonUuidCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonUuidCast.
/// </summary>
[TestClass]
public class JsonUuidCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_uuid()
    {
        JsonString sut = JsonString.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan());
        var result = (JsonUuid)sut;
        Assert.AreEqual(JsonUuid.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_Guid_for_json_element_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan());
        var result = (Guid)sut;
        Assert.AreEqual(Guid.Parse("c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"), result);
    }

    [TestMethod]
    public void Cast_to_Guid_for_dotnet_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"").AsDotnetBackedValue();
        var result = (Guid)sut;
        Assert.AreEqual(Guid.Parse("c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"), result);
    }

    [TestMethod]
    public void Cast_from_Guid_for_json_element_backed_value_as_an_uuid()
    {
        Guid sut = Guid.Parse("c3f2a2a3-72c1-4abc-a741-b0e7095f20d1");
        var result = (JsonUuid)sut;
        Assert.AreEqual(JsonUuid.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("c3f2a2a3-72c1-4abc-a741-b0e7095f20d1", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_an_uuid()
    {
        var sut = JsonUuid.Parse("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("c3f2a2a3-72c1-4abc-a741-b0e7095f20d1", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_an_uuid()
    {
        string sut = "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1";
        var result = (JsonUuid)sut;
        Assert.AreEqual(JsonUuid.ParseValue("\"c3f2a2a3-72c1-4abc-a741-b0e7095f20d1\"".AsSpan()), result);
    }
}