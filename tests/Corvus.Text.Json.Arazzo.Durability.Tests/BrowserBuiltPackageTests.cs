// <copyright file="BrowserBuiltPackageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Locks cross-language compatibility between the zero-build browser package builder
/// (<c>web/arazzo-control-plane-ui/src/workflow-package.js</c>) and the .NET <see cref="WorkflowPackage"/>
/// reader. The bytes below are the verbatim output of <c>packWorkflowPackage(...)</c> (the length-prefixed
/// container, stored encoding); if the JS builder changes shape, regenerate them and confirm these still pass.
/// </summary>
[TestClass]
public sealed class BrowserBuiltPackageTests
{
    // packWorkflowPackage(workflow "nightly-reconcile", sources: [petstore]) — length-prefixed container, base64.
    private const string BrowserBuiltPackageBase64 =
        "QVdQAQIAAAAVAHNvdXJjZXMvcGV0c3RvcmUuanNvbgBBAAAAeyJvcGVuYXBpIjoiMy4xLjAiLCJpbmZvIjp7InRpdG" +
        "xlIjoiUGV0c3RvcmUiLCJ2ZXJzaW9uIjoiMS4wLjAifX0NAHdvcmtmbG93Lmpzb24ArwAAAHsiYXJhenpvIjoiMS4x" +
        "LjAiLCJpbmZvIjp7InRpdGxlIjoiTmlnaHRseSBSZWNvbmNpbGUifSwic291cmNlRGVzY3JpcHRpb25zIjpbeyJuYW" +
        "1lIjoicGV0c3RvcmUiLCJ0eXBlIjoib3BlbmFwaSJ9XSwid29ya2Zsb3dzIjpbeyJ3b3JrZmxvd0lkIjoibmlnaHRs" +
        "eS1yZWNvbmNpbGUiLCJzdGVwcyI6W119XX0=";

    [TestMethod]
    public void OpenReadsTheBrowserBuiltArchive()
    {
        byte[] package = Convert.FromBase64String(BrowserBuiltPackageBase64);

        WorkflowPackageContents contents = WorkflowPackage.Open(package);

        using var workflow = ParsedJsonDocument<JsonElement>.Parse(contents.Workflow);
        Assert.AreEqual(
            "nightly-reconcile",
            workflow.RootElement.GetProperty("workflows"u8)[0].GetProperty("workflowId"u8).GetString());

        Assert.AreEqual(1, contents.Sources.Count);
        Assert.AreEqual("petstore", contents.Sources[0].Key);
        using var source = ParsedJsonDocument<JsonElement>.Parse(contents.Sources[0].Value);
        Assert.AreEqual("3.1.0", source.RootElement.GetProperty("openapi"u8).GetString());
    }

    [TestMethod]
    public void ProjectAssignsTheVersionAndContentHash()
    {
        byte[] package = Convert.FromBase64String(BrowserBuiltPackageBase64);

        string baseId = CatalogPackage.ReadBaseWorkflowId(package);
        Assert.AreEqual("nightly-reconcile", baseId);
        Assert.IsFalse(CatalogPackage.IsVersioned(baseId), "the submitted id carries no -vN suffix");

        CatalogPackageProjection projection = CatalogPackage.Project(package, baseId, 4);
        Assert.AreEqual("nightly-reconcile-v4", projection.WorkflowId);
        Assert.AreEqual("Nightly Reconcile", projection.Title);
        Assert.AreEqual(1, projection.Sources.Count);
        Assert.AreEqual("petstore", projection.Sources[0].Name);
        Assert.AreEqual(64, projection.Hash.Length, "a SHA-256 hex content hash");
    }
}
