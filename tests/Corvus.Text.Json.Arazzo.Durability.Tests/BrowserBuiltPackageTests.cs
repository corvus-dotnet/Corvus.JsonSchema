// <copyright file="BrowserBuiltPackageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Locks cross-language compatibility between the zero-build browser package builder
/// (<c>web/arazzo-control-plane-ui/src/workflow-package.js</c>) and the .NET <see cref="WorkflowPackage"/>
/// reader. The bytes below are the verbatim output of <c>packWorkflowPackage(...)</c> (a store-method ZIP);
/// if the JS builder changes shape, regenerate them and confirm these still pass.
/// </summary>
[TestClass]
public sealed class BrowserBuiltPackageTests
{
    // packWorkflowPackage(workflow "nightly-reconcile", sources: [petstore]) — store-method ZIP, base64.
    private const string BrowserBuiltPackageBase64 =
        "UEsDBBQAAAAAAAAAIQCPkO7abQAAAG0AAAANAAAAbWFuaWZlc3QuanNvbnsiZm9ybWF0VmVyc2lvbiI6MSwid29ya2Zsb3ciOiJ3" +
        "b3JrZmxvdy5qc29uIiwic291cmNlcyI6W3sibmFtZSI6InBldHN0b3JlIiwicGF0aCI6InNvdXJjZXMvcGV0c3RvcmUuanNvbiJ9" +
        "XX1QSwMEFAAAAAAAAAAhADEIYj6vAAAArwAAAA0AAAB3b3JrZmxvdy5qc29ueyJhcmF6em8iOiIxLjEuMCIsImluZm8iOnsidGl0" +
        "bGUiOiJOaWdodGx5IFJlY29uY2lsZSJ9LCJzb3VyY2VEZXNjcmlwdGlvbnMiOlt7Im5hbWUiOiJwZXRzdG9yZSIsInR5cGUiOiJv" +
        "cGVuYXBpIn1dLCJ3b3JrZmxvd3MiOlt7IndvcmtmbG93SWQiOiJuaWdodGx5LXJlY29uY2lsZSIsInN0ZXBzIjpbXX1dfVBLAwQU" +
        "AAAAAAAAACEA/L28fEEAAABBAAAAFQAAAHNvdXJjZXMvcGV0c3RvcmUuanNvbnsib3BlbmFwaSI6IjMuMS4wIiwiaW5mbyI6eyJ0" +
        "aXRsZSI6IlBldHN0b3JlIiwidmVyc2lvbiI6IjEuMC4wIn19UEsBAhQAFAAAAAAAAAAhAI+Q7tptAAAAbQAAAA0AAAAAAAAAAAAA" +
        "AAAAAAAAAG1hbmlmZXN0Lmpzb25QSwECFAAUAAAAAAAAACEAMQhiPq8AAACvAAAADQAAAAAAAAAAAAAAAACYAAAAd29ya2Zsb3cu" +
        "anNvblBLAQIUABQAAAAAAAAAIQD8vbx8QQAAAEEAAAAVAAAAAAAAAAAAAAAAAHIBAABzb3VyY2VzL3BldHN0b3JlLmpzb25QSwUG" +
        "AAAAAAMAAwC5AAAA5gEAAAAA";

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
