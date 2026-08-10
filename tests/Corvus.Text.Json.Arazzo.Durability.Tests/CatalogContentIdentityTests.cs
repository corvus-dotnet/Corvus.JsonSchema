// <copyright file="CatalogContentIdentityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Canonicalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Pins ADR 0031's identity discipline against H13: the content hash is over the RFC 8785 canonical form, so the
/// STORED and COMPILED bytes must be that same canonical form — hash and compile the same bytes. Without that, two
/// documents sharing a canonical form share one hash and version identity while being different compiler inputs,
/// and a loader trusting the stored hash column runs an executor not derived from the stored documents.
/// </summary>
[TestClass]
public sealed class CatalogContentIdentityTests
{
    // The same logical workflow in two raw framings: key order, whitespace, and a number's textual form differ,
    // the RFC 8785 canonical form does not.
    private const string WorkflowVariantA = """{"arazzo":"1.1.0","info":{"title":"Identity","version":"1.0"},"sourceDescriptions":[{"name":"petstore","url":"./petstore.json","type":"openapi"}],"workflows":[{"workflowId":"identity-flow","steps":[],"x-budget":1.50}]}""";
    private const string WorkflowVariantB = """
        {
          "workflows": [ { "x-budget": 1.5, "steps": [], "workflowId": "identity-flow" } ],
          "sourceDescriptions": [ { "type": "openapi", "url": "./petstore.json", "name": "petstore" } ],
          "info": { "version": "1.0", "title": "Identity" },
          "arazzo": "1.1.0"
        }
        """;

    private const string SourceVariantA = """{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""";
    private const string SourceVariantB = """{ "info": { "version": "1.0.0", "title": "Petstore" }, "openapi": "3.1.0" }""";

    [TestMethod]
    public void Two_raw_variants_sharing_a_canonical_form_project_to_identical_stored_bytes()
    {
        byte[] packageA = Package(WorkflowVariantA, SourceVariantA);
        byte[] packageB = Package(WorkflowVariantB, SourceVariantB);

        CatalogPackageProjection a = CatalogPackage.Project(packageA, "identity-flow", 1);
        CatalogPackageProjection b = CatalogPackage.Project(packageB, "identity-flow", 1);

        // The identity always converged (the hash is canonical)...
        b.Hash.ShouldBe(a.Hash);

        // ...and the stored bytes must converge with it: one identity, one compiler input (H13). Before the fix the
        // raw framing survived into the stored package, so one hash covered two different byte streams.
        ReadOnlyMemory<byte>? workflowA = CatalogPackage.GetDocument(a.CanonicalPackage, CatalogPackage.WorkflowDocumentName);
        ReadOnlyMemory<byte>? workflowB = CatalogPackage.GetDocument(b.CanonicalPackage, CatalogPackage.WorkflowDocumentName);
        workflowA.ShouldNotBeNull();
        workflowB.ShouldNotBeNull();
        workflowA.Value.ToArray().ShouldBe(workflowB.Value.ToArray());

        ReadOnlyMemory<byte>? sourceA = CatalogPackage.GetDocument(a.CanonicalPackage, "petstore");
        ReadOnlyMemory<byte>? sourceB = CatalogPackage.GetDocument(b.CanonicalPackage, "petstore");
        sourceA.ShouldNotBeNull();
        sourceB.ShouldNotBeNull();
        sourceA.Value.ToArray().ShouldBe(sourceB.Value.ToArray());

        // The stored bytes ARE the canonical form: canonicalizing them is the identity function.
        using ParsedJsonDocument<JsonElement> storedWorkflow = ParsedJsonDocument<JsonElement>.Parse(workflowA.Value);
        JsonCanonicalizer.Canonicalize(storedWorkflow.RootElement).ShouldBe(workflowA.Value.ToArray());
    }

    [TestMethod]
    public async Task A_stored_hash_diverging_from_the_content_refuses_the_load()
    {
        // The stored column claims an identity the served documents do not hash to — a tampered or corrupted
        // version. The loader must recompute from the content and refuse, not trust the column (H13's second half;
        // the AOT path already recomputes).
        var artifacts = new StubArtifactSource(WorkflowVariantA, SourceVariantA, contentHash: new string('f', 64));
        var resolver = new LoaderHostedWorkflowResolver(artifacts, new WorkflowExecutorLoader());

        InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(
            async () => await resolver.ResolveAsync(NewRun("identity-flow-v1"), default));
        refusal.Message.ShouldContain("diverges");
    }

    [TestMethod]
    public async Task A_stored_hash_matching_the_content_proceeds_to_the_executor_gate()
    {
        // The positive control: with the column agreeing with the recompute, the flow passes the identity gate and
        // fails only at the next one (this stub serves no executor), proving the recompute reproduces the stored
        // hash from the same documents.
        string genuine = WorkflowPackage.ComputeContentHash(
            Encoding.UTF8.GetBytes(WorkflowVariantA),
            [new KeyValuePair<string, byte[]>("petstore", Encoding.UTF8.GetBytes(SourceVariantA))]);
        var artifacts = new StubArtifactSource(WorkflowVariantA, SourceVariantA, genuine);
        var resolver = new LoaderHostedWorkflowResolver(artifacts, new WorkflowExecutorLoader());

        InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(
            async () => await resolver.ResolveAsync(NewRun("identity-flow-v1"), default));
        refusal.Message.ShouldContain("not runnable");
    }

    private static byte[] Package(string workflow, string source)
        => CatalogPackage.Build(
            Encoding.UTF8.GetBytes(workflow),
            [new KeyValuePair<string, byte[]>("petstore", Encoding.UTF8.GetBytes(source))]);

    private static WorkflowRun NewRun(string workflowId)
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        return WorkflowRun.CreateNew(store, "run-1", workflowId, inputs.RootElement, "development");
    }

    // Serves one version's genuine workflow + source documents under a caller-chosen content-hash column, so the
    // tests can pull the column and the content apart.
    private sealed class StubArtifactSource(string workflow, string source, string contentHash) : IWorkflowArtifactSource
    {
        public ValueTask<string?> GetContentHashAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
            => new((string?)contentHash);

        public ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
            => new(documentName switch
            {
                CatalogPackage.WorkflowDocumentName => (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(workflow),
                "petstore" => Encoding.UTF8.GetBytes(source),
                _ => null,
            });
    }
}
