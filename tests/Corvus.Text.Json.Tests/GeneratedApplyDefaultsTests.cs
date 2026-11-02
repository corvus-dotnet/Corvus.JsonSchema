// <copyright file="GeneratedApplyDefaultsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// The generated <c>ApplyDefaults()</c> writes the schema's defaults into the document
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/953).
/// </summary>
[TestClass]
public class GeneratedApplyDefaultsTests
{
    [TestMethod]
    public void ApplyDefaults_WritesTheDefaultOfEachAbsentProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"x"}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.ApplyDefaults();

        Assert.AreEqual("""{"name":"x","count":0,"status":"active"}""", builder.RootElement.ToString());
    }

    [TestMethod]
    public void ApplyDefaults_KeepsAPropertyThatIsPresent()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"x","status":"paused"}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.ApplyDefaults();

        Assert.AreEqual("""{"name":"x","status":"paused","count":0}""", builder.RootElement.ToString());
    }

    [TestMethod]
    public void ApplyDefaults_AppliesTheDefaultsOfNestedObjects()
    {
        // "settings" is absent and defaults to {}, whose own properties then take their defaults. "limits" is present,
        // so its "max" takes its default. Neither the absent nor the present objects lose what they had.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<ApplyDefaultsModel>.Parse("""{"name":"a","limits":{}}""");
        using JsonDocumentBuilder<ApplyDefaultsModel.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.ApplyDefaults();

        ApplyDefaultsModel result = builder.RootElement.Clone();
        Assert.AreEqual(3, (int)result.Retries);
        Assert.AreEqual(502, (int)result.Settings.Port);
        Assert.AreEqual("localhost", (string)result.Settings.Host);
        Assert.AreEqual(10, (int)result.Limits.Max);
        Assert.IsTrue(result.EvaluateSchema());
    }

    [TestMethod]
    public void ApplyDefaults_LeavesAnAbsentObjectWithNoDefaultAbsent()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<ApplyDefaultsModel>.Parse("""{"name":"a","settings":{"port":1}}""");
        using JsonDocumentBuilder<ApplyDefaultsModel.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.ApplyDefaults();

        Assert.AreEqual("""{"name":"a","settings":{"port":1,"host":"localhost"},"retries":3}""", builder.RootElement.ToString());
    }

    [TestMethod]
    public void ApplyDefaults_IsIdempotent()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var source = ParsedJsonDocument<ApplyDefaultsModel>.Parse("""{"name":"a"}""");
        using JsonDocumentBuilder<ApplyDefaultsModel.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        builder.RootElement.ApplyDefaults();
        string once = builder.RootElement.ToString();
        builder.RootElement.ApplyDefaults();

        Assert.AreEqual(once, builder.RootElement.ToString());
    }
}