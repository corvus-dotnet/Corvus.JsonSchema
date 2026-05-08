// <copyright file="JsonRelativePointerCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonRelativePointerCast.
/// </summary>
[TestClass]
public class JsonRelativePointerCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.ParseValue("\"/a~1b\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.Parse("\"/a~1b\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.ParseValue("\"/a~1b\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.Parse("\"/a~1b\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_a_pointer()
    {
        JsonString sut = JsonString.ParseValue("\"/a~1b\"".AsSpan());
        var result = (JsonPointer)sut;
        Assert.AreEqual(JsonPointer.ParseValue("\"/a~1b\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.ParseValue("\"/a~1b\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("/a~1b", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_a_pointer()
    {
        var sut = JsonPointer.Parse("\"/a~1b\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("/a~1b", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_a_pointer()
    {
        string sut = "/a~1b";
        var result = (JsonPointer)sut;
        Assert.AreEqual(JsonPointer.ParseValue("\"/a~1b\"".AsSpan()), result);
    }
}