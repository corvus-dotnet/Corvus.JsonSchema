// <copyright file="WorkflowPackageExecutorProviderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests baking the compiled executor assembly into the canonical package at projection time via an injected
/// <see cref="IWorkflowExecutorProvider"/> (the catalog core treats the bytes as opaque).
/// </summary>
[TestClass]
public class WorkflowPackageExecutorProviderTests
{
    [TestMethod]
    public void Project_bakes_the_executor_and_passes_the_versioned_id_and_hash()
    {
        byte[] package = WorkflowPackage.Pack(Workflow("flow"), []);
        var provider = new FakeExecutorProvider();

        CatalogPackageProjection projection = CatalogPackage.Project(package, "flow", 2, metadataProvider: null, executorProvider: provider);

        CatalogPackage.GetDocument(projection.CanonicalPackage, WorkflowPackage.ExecutorDocumentName)!.Value.ToArray().ShouldBe(FakeExecutorProvider.AssemblyBytes);
        Encoding.UTF8.GetString(CatalogPackage.GetDocument(projection.CanonicalPackage, WorkflowPackage.ExecutorManifestDocumentName)!.Value.Span).ShouldContain("\"entryType\"");

        // The provider sees the rewritten (versioned) workflow id and the content hash to bind the manifest to.
        provider.SeenWorkflow!.ShouldContain("flow-v2");
        provider.SeenHash.ShouldBe(projection.Hash);

        // Baking the executor does not change the content hash (it covers only the workflow + sources).
        CatalogPackageProjection withoutExecutor = CatalogPackage.Project(package, "flow", 2);
        projection.Hash.ShouldBe(withoutExecutor.Hash);
    }

    [TestMethod]
    public void Project_omits_the_executor_when_no_provider()
    {
        CatalogPackageProjection projection = CatalogPackage.Project(WorkflowPackage.Pack(Workflow("x"), []), "x", 1);
        CatalogPackage.GetDocument(projection.CanonicalPackage, WorkflowPackage.ExecutorDocumentName).ShouldBeNull();
        CatalogPackage.GetDocument(projection.CanonicalPackage, WorkflowPackage.ExecutorManifestDocumentName).ShouldBeNull();
    }

    [TestMethod]
    public void Project_omits_the_executor_when_the_provider_returns_null()
    {
        CatalogPackageProjection projection = CatalogPackage.Project(WorkflowPackage.Pack(Workflow("x"), []), "x", 1, executorProvider: new NullExecutorProvider());
        CatalogPackage.GetDocument(projection.CanonicalPackage, WorkflowPackage.ExecutorDocumentName).ShouldBeNull();
    }

    [TestMethod]
    public async Task Store_threads_the_executor_provider_and_serves_the_assembly_via_GetDocument()
    {
        var provider = new FakeExecutorProvider();
        var store = new InMemoryWorkflowCatalogStore(executorProvider: provider);

        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("flow", WorkflowPackage.Pack(Workflow("flow"), []), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        // The store baked the provider's assembly into the stored package and serves it back by name.
        ReadOnlyMemory<byte>? executor = await store.GetDocumentAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, WorkflowPackage.ExecutorDocumentName, default);
        executor.ShouldNotBeNull();
        executor.Value.ToArray().ShouldBe(FakeExecutorProvider.AssemblyBytes);

        // And it passed the rewritten (versioned) id through to the provider.
        provider.SeenWorkflow!.ShouldContain("flow-v1");
    }

    [TestMethod]
    public async Task Store_omits_the_executor_when_no_provider_is_injected()
    {
        var store = new InMemoryWorkflowCatalogStore();
        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("flow", WorkflowPackage.Pack(Workflow("flow"), []), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        ReadOnlyMemory<byte>? executor = await store.GetDocumentAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, WorkflowPackage.ExecutorDocumentName, default);
        executor.ShouldBeNull();
    }

    private static CatalogMetadata Meta() => new(new CatalogOwner("Team", "team@example.com"), "alice");

    private static byte[] Workflow(string id)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"{{id}}","steps":[]}]}""");

    private sealed class FakeExecutorProvider : IWorkflowExecutorProvider
    {
        public static readonly byte[] AssemblyBytes = [0x4D, 0x5A, 0x00, 0xFF];

        public string? SeenWorkflow { get; private set; }

        public string? SeenHash { get; private set; }

        public WorkflowExecutorArtifact? BuildExecutor(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources, string packageHash)
        {
            this.SeenWorkflow = Encoding.UTF8.GetString(workflowUtf8.Span);
            this.SeenHash = packageHash;
            return new WorkflowExecutorArtifact(AssemblyBytes, Encoding.UTF8.GetBytes($$"""{"formatVersion":1,"entryType":"X","packageHash":"{{packageHash}}"}"""));
        }
    }

    private sealed class NullExecutorProvider : IWorkflowExecutorProvider
    {
        public WorkflowExecutorArtifact? BuildExecutor(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources, string packageHash)
            => null;
    }
}
