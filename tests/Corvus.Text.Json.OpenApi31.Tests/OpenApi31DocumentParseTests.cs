// <copyright file="OpenApi31DocumentParseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi31;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi31.Tests;

/// <summary>
/// Tests for parsing OpenAPI 3.1 documents into V5 generated types.
/// </summary>
[TestClass]
public class OpenApi31DocumentParseTests
{
    private static readonly Lazy<byte[]> PetstoreBytes = new(
        () => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-3.1.json")));

    [TestMethod]
    public void ParsePetstoreDocument()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, api.ValueKind);
    }

    [TestMethod]
    public void PetstoreOpenapiVersion()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.AreEqual("3.1.0", (string)api.Openapi);
    }

    [TestMethod]
    public void PetstoreInfoTitle()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var info = api.InfoValue;
        Assert.AreEqual("Petstore", (string)info.Title);
    }

    [TestMethod]
    public void PetstoreInfoVersion()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var info = api.InfoValue;
        Assert.AreEqual("1.0.0", (string)info.Version);
    }

    [TestMethod]
    public void PetstoreInfoDescription()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var info = api.InfoValue;
        Assert.AreEqual("A sample Petstore API", (string)info.Description);
    }

    [TestMethod]
    public void PetstoreInfoLicense()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var license = api.InfoValue.LicenseValue;
        Assert.AreEqual("Apache 2.0", (string)license.Name);
    }

    [TestMethod]
    public void PetstoreInfoContact()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var contact = api.InfoValue.ContactValue;
        Assert.AreEqual("API Support", (string)contact.Name);
        Assert.AreEqual("support@example.com", (string)contact.Email);
    }

    [TestMethod]
    public void PetstoreServers()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var servers = api.Servers;
        Assert.AreEqual(1, servers.GetArrayLength());

        var server = servers[0];
        Assert.AreEqual("https://petstore.example.com/v1", (string)server.Url);
        Assert.AreEqual("Production server", (string)server.Description);
    }

    [TestMethod]
    public void PetstorePathsExist()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var paths = api.PathsValue;
        Assert.AreEqual(JsonValueKind.Object, paths.ValueKind);
    }

    [TestMethod]
    public void PetstoreComponentsSchemasExist()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        var components = api.ComponentsValue;
        Assert.AreEqual(JsonValueKind.Object, components.ValueKind);

        var schemas = components.Schemas;
        Assert.AreEqual(JsonValueKind.Object, schemas.ValueKind);
    }

    [TestMethod]
    public void PetstoreValidation()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        Assert.IsTrue(api.EvaluateSchema());
    }

    [TestMethod]
    public void PetstoreRoundTrip()
    {
        using var doc = ParsedJsonDocument<OpenApiDocument>.Parse(PetstoreBytes.Value);
        OpenApiDocument api = doc.RootElement;

        string serialized = api.ToString();

        using var doc2 = ParsedJsonDocument<OpenApiDocument>.Parse(
            System.Text.Encoding.UTF8.GetBytes(serialized));
        OpenApiDocument api2 = doc2.RootElement;

        Assert.AreEqual("3.1.0", (string)api2.Openapi);
        Assert.AreEqual("Petstore", (string)api2.InfoValue.Title);
        Assert.AreEqual("1.0.0", (string)api2.InfoValue.Version);
    }
}
