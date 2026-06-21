// <copyright file="WorkflowPackageMetadataTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests baking the precomputed schema-metadata document into the canonical package at projection/add time
/// via an injected <see cref="IWorkflowMetadataProvider"/> (the catalog core treats the bytes as opaque).
/// </summary>
[TestClass]
public class WorkflowPackageMetadataTests
{
    [TestMethod]
    public void Project_bakes_schemas_into_the_canonical_package_when_a_provider_is_supplied()
    {
        byte[] package = WorkflowPackage.Pack(Workflow("nightly-reconcile"), []);

        CatalogPackageProjection projection = CatalogPackage.Project(package, "nightly-reconcile", 3, new FakeProvider());

        ReadOnlyMemory<byte>? schemas = CatalogPackage.GetDocument(projection.CanonicalPackage, WorkflowPackage.SchemasDocumentName);
        schemas.ShouldNotBeNull();
        Encoding.UTF8.GetString(schemas.Value.Span).ShouldContain("\"baked\":true");

        // The content hash covers only the workflow + sources, so baking metadata does not change it.
        CatalogPackageProjection withoutMetadata = CatalogPackage.Project(package, "nightly-reconcile", 3);
        projection.Hash.ShouldBe(withoutMetadata.Hash);
    }

    [TestMethod]
    public void Project_omits_schemas_when_no_provider()
    {
        byte[] package = WorkflowPackage.Pack(Workflow("x"), []);
        CatalogPackageProjection projection = CatalogPackage.Project(package, "x", 1);
        CatalogPackage.GetDocument(projection.CanonicalPackage, WorkflowPackage.SchemasDocumentName).ShouldBeNull();
    }

    [TestMethod]
    public async Task Store_bakes_and_serves_schemas_via_GetDocument()
    {
        var store = new InMemoryWorkflowCatalogStore(metadataProvider: new FakeProvider());
        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("flow", WorkflowPackage.Pack(Workflow("flow"), []), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        ReadOnlyMemory<byte>? schemas = await store.GetDocumentAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, WorkflowPackage.SchemasDocumentName, default);
        schemas.ShouldNotBeNull();
        Encoding.UTF8.GetString(schemas.Value.Span).ShouldContain("\"baked\":true");
    }

    private static byte[] Workflow(string id)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"{{id}}","steps":[]}]}""");

    private static CatalogMetadata Meta() => new(new CatalogOwner("Team", "team@example.com"), "alice");

    private sealed class FakeProvider : IWorkflowMetadataProvider
    {
        public ReadOnlyMemory<byte>? BuildSchemas(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
            => Encoding.UTF8.GetBytes("""{"formatVersion":1,"baked":true}""");
    }
}