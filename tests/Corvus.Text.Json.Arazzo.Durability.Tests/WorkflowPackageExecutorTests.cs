// <copyright file="WorkflowPackageExecutorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests that <see cref="WorkflowPackage"/> can carry the compiled executor assembly + its manifest as
/// optional binary entries (round-trip, absence, binary fidelity, content-hash invariance, document routing).
/// </summary>
[TestClass]
public class WorkflowPackageExecutorTests
{
    // Arbitrary "assembly" bytes including 0x00 and 0xFF to prove binary fidelity (the DLL is not JSON).
    private static readonly byte[] ExecutorBytes = [0x4D, 0x5A, 0x00, 0x01, 0xFF, 0xFE, 0x10, 0x00, 0x7F];
    private static readonly byte[] ManifestBytes = Encoding.UTF8.GetBytes("""{"formatVersion":1,"entryType":"X","assemblyDigest":"abc"}""");

    [TestMethod]
    public void Pack_round_trips_the_executor_and_manifest()
    {
        byte[] package = WorkflowPackage.Pack(Workflow("flow"), [], schemas: default, executor: ExecutorBytes, executorManifest: ManifestBytes);

        WorkflowPackageContents contents = WorkflowPackage.Open(package);
        contents.Executor.ShouldBe(ExecutorBytes);
        contents.ExecutorManifest.ShouldBe(ManifestBytes);
        contents.GetDocument(WorkflowPackage.ExecutorDocumentName).ShouldBe(ExecutorBytes);
        contents.GetDocument(WorkflowPackage.ExecutorManifestDocumentName).ShouldBe(ManifestBytes);
    }

    [TestMethod]
    public void Open_returns_null_executor_when_absent()
    {
        WorkflowPackageContents contents = WorkflowPackage.Open(WorkflowPackage.Pack(Workflow("flow"), []));
        contents.Executor.ShouldBeNull();
        contents.ExecutorManifest.ShouldBeNull();
        contents.GetDocument(WorkflowPackage.ExecutorDocumentName).ShouldBeNull();
        contents.GetDocument(WorkflowPackage.ExecutorManifestDocumentName).ShouldBeNull();
    }

    [TestMethod]
    public void Executor_can_be_packed_without_a_manifest_and_vice_versa()
    {
        WorkflowPackageContents onlyAssembly = WorkflowPackage.Open(WorkflowPackage.Pack(Workflow("flow"), [], executor: ExecutorBytes));
        onlyAssembly.Executor.ShouldBe(ExecutorBytes);
        onlyAssembly.ExecutorManifest.ShouldBeNull();

        WorkflowPackageContents onlyManifest = WorkflowPackage.Open(WorkflowPackage.Pack(Workflow("flow"), [], executorManifest: ManifestBytes));
        onlyManifest.Executor.ShouldBeNull();
        onlyManifest.ExecutorManifest.ShouldBe(ManifestBytes);
    }

    [TestMethod]
    public void Carrying_an_executor_does_not_change_the_logical_content_hash()
    {
        byte[] sourceA = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"a","version":"1"}}""");
        var sources = new[] { new KeyValuePair<string, byte[]>("a", sourceA) };

        string withExecutor = WorkflowPackage.ComputeContentHash(Workflow("flow"), sources);

        // Re-hash from what Open() yields for a package that carries the executor — the logical content is intact.
        WorkflowPackageContents contents = WorkflowPackage.Open(
            WorkflowPackage.Pack(Workflow("flow"), sources, executor: ExecutorBytes, executorManifest: ManifestBytes));
        string reHashed = WorkflowPackage.ComputeContentHash(contents.Workflow, contents.Sources);

        reHashed.ShouldBe(withExecutor);
    }

    [TestMethod]
    public void GetDocument_routes_workflow_sources_and_unknown()
    {
        byte[] sourceA = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"a","version":"1"}}""");
        WorkflowPackageContents contents = WorkflowPackage.Open(
            WorkflowPackage.Pack(Workflow("flow"), [new KeyValuePair<string, byte[]>("a", sourceA)], executor: ExecutorBytes));

        contents.GetDocument(WorkflowPackage.WorkflowDocumentName).ShouldBe(contents.Workflow);
        contents.GetDocument("a").ShouldBe(sourceA);
        contents.GetDocument("does-not-exist").ShouldBeNull();
    }

    private static byte[] Workflow(string id)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"{{id}}","steps":[]}]}""");
}
