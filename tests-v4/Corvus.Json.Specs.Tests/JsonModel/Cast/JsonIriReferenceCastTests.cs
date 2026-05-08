// <copyright file="JsonIriReferenceCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonIriReferenceCast.
/// </summary>
[TestClass]
public class JsonIriReferenceCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_json_element_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonString_for_dotnet_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (JsonString)sut;
        Assert.AreEqual(JsonString.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_JsonString_for_json_element_backed_value_as_an_iri()
    {
        JsonString sut = JsonString.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (JsonIriReference)sut;
        Assert.AreEqual(JsonIriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_Uri_for_json_element_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (Uri)sut;
        Assert.AreEqual(new Uri("http://foo.bar/?baz=qux#quux", UriKind.RelativeOrAbsolute), result);
    }

    [TestMethod]
    public void Cast_to_Uri_for_dotnet_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (Uri)sut;
        Assert.AreEqual(new Uri("http://foo.bar/?baz=qux#quux", UriKind.RelativeOrAbsolute), result);
    }

    [TestMethod]
    public void Cast_from_Uri_for_json_element_backed_value_as_an_iri()
    {
        Uri sut = new Uri("http://foo.bar/?baz=qux#quux", UriKind.RelativeOrAbsolute);
        var result = (JsonIriReference)sut;
        Assert.AreEqual(JsonIriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_string_for_json_element_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var result = (string)sut;
        Assert.AreEqual("http://foo.bar/?baz=qux#quux", result);
    }

    [TestMethod]
    public void Cast_to_string_for_dotnet_backed_value_as_an_iri()
    {
        var sut = JsonIriReference.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var result = (string)sut;
        Assert.AreEqual("http://foo.bar/?baz=qux#quux", result);
    }

    [TestMethod]
    public void Cast_from_string_for_json_element_backed_value_as_an_iri()
    {
        string sut = "http://foo.bar/?baz=qux#quux";
        var result = (JsonIriReference)sut;
        Assert.AreEqual(JsonIriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan()), result);
    }
}