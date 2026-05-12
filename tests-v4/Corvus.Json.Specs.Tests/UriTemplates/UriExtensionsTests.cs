// <copyright file="UriExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.UriTemplates;

[TestClass]
public class UriExtensionsTests
{
    [TestMethod]
    public void ChangeExistingParameterWithinMultiple()
    {
        Uri targetUri = new("http://example/customer?view=false&foo=bar", UriKind.RelativeOrAbsolute);
        ImmutableDictionary<string, JsonAny> parameters = targetUri.GetQueryStringParameters();
        parameters = parameters.SetItem("view", JsonAny.Parse("\"true\""));
        UriTemplate template = targetUri.MakeTemplate(parameters);
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example/customer?view=true&foo=bar",
            "http://example/customer?foo=bar&view=true",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ChangeExistingParameter()
    {
        Uri targetUri = new("http://example/customer?view=false&foo=bar", UriKind.RelativeOrAbsolute);
        UriTemplate template = targetUri.MakeTemplate();
        template = template.SetParameter("view", true);
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example/customer?view=true&foo=bar",
            "http://example/customer?foo=bar&view=true",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ClearExistingParameter()
    {
        Uri targetUri = new("http://example/customer?view=false&foo=bar", UriKind.RelativeOrAbsolute);
        UriTemplate template = targetUri.MakeTemplate();
        template = template.ClearParameter("view");
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example/customer?foo=bar",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void AddMultipleParameters()
    {
        Uri targetUri = new("http://example/customer", UriKind.RelativeOrAbsolute);
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("id", JsonAny.Parse("99"));
        builder.Add("view", JsonAny.Parse("false"));
        UriTemplate template = targetUri.MakeTemplate(builder.ToImmutable());
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example/customer?id=99&view=false",
            "http://example/customer?view=false&id=99",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void AddMultipleParametersAsParams()
    {
        Uri targetUri = new("http://example/customer", UriKind.RelativeOrAbsolute);
        var parameters = new (string, JsonAny)[]
        {
            ("id", JsonAny.Parse("99")),
            ("view", JsonAny.Parse("false")),
        };
        UriTemplate template = targetUri.MakeTemplate(parameters);
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example/customer?id=99&view=false",
            "http://example/customer?view=false&id=99",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void AddParametersToQueryStringWithUriIgnoringPathParameter()
    {
        Uri targetUri = new("http://example/customer/{id}?view=true", UriKind.RelativeOrAbsolute);
        ImmutableDictionary<string, JsonAny> parameters = targetUri.GetQueryStringParameters();
        parameters = parameters.SetItem("context", JsonAny.Parse("\"detail\""));
        UriTemplate template = targetUri.MakeTemplate(parameters);
        template = template.SetParameter("id", 99);
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example/customer/99?view=true&context=detail",
            "http://example/customer/99?context=detail&view=true",
        ];

        CollectionAssert.Contains(expected, resolved);
    }
}