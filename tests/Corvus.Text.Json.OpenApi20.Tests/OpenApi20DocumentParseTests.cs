// <copyright file="OpenApi20DocumentParseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi20;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi20.Tests;

/// <summary>
/// Tests for parsing OpenAPI 2.0 (Swagger) documents into V5 generated types.
/// </summary>
[TestClass]
public class OpenApi20DocumentParseTests
{
    private static readonly Lazy<byte[]> PetstoreBytes = new(
        () => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-2.0.json")));

    [TestMethod]
    public void ParsePetstoreDocument()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, api.ValueKind);
    }

    [TestMethod]
    public void PetstoreSwaggerVersion()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.AreEqual("2.0", (string)api.Swagger);
    }

    [TestMethod]
    public void PetstoreInfoTitle()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var info = api.InfoValue;
        Assert.AreEqual("Swagger Petstore", (string)info.Title);
    }

    [TestMethod]
    public void PetstoreInfoVersion()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var info = api.InfoValue;
        Assert.AreEqual("1.0.7", (string)info.Version);
    }

    [TestMethod]
    public void PetstoreHost()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.AreEqual("petstore.swagger.io", (string)api.Host);
    }

    [TestMethod]
    public void PetstoreBasePath()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.AreEqual("/v2", (string)api.BasePath);
    }

    [TestMethod]
    public void PetstoreValidatesAgainstTheMetaschema()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.IsTrue(api.EvaluateSchema());
    }

    [TestMethod]
    public void DocumentMissingInfoAndPathsIsInvalid()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse("""{"swagger":"2.0"}"""u8.ToArray());
        OpenApiDocument api = doc.RootElement;

        Assert.IsFalse(api.EvaluateSchema());
    }

    [TestMethod]
    public void DocumentWithWrongSwaggerVersionIsInvalid()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(
            """{"swagger":"3.0","info":{"title":"t","version":"1"},"paths":{}}"""u8.ToArray());
        OpenApiDocument api = doc.RootElement;

        Assert.IsFalse(api.EvaluateSchema());
    }
}