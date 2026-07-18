// <copyright file="CatalogPackageSigningTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// End-to-end tests of the executor-package signing path (#879): the control plane signs the compiled executor's
/// manifest at catalog-add time (<see cref="CatalogPackage.ProjectAsync"/> / a store's <c>AddAsync</c>), the detached
/// signature rides the canonical package, and a runner verifies it against a public key held in its own trust store —
/// the private key never leaving the signer.
/// </summary>
[TestClass]
public class CatalogPackageSigningTests
{
    [TestMethod]
    public async Task ProjectAsync_signs_the_manifest_and_the_signature_verifies_against_the_trust_store()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(signingKey, "release-2026");
        var provider = new FakeExecutorProvider();

        CatalogPackageProjection projection = await CatalogPackage.ProjectAsync(
            WorkflowPackage.Pack(Workflow("flow"), []), "flow", 3, executorProvider: provider, signer: signer, cancellationToken: default);

        WorkflowPackageContents contents = WorkflowPackage.Open(projection.CanonicalPackage);
        contents.ExecutorSignature.ShouldNotBeNull("the signed projection carries a detached signature");

        // The runner path: verify the carried signature against the manifest bytes the package holds, using only the
        // trusted public key (the signing vault is never contacted).
        IExecutorPackageVerifier verifier = TrustStore(("release-2026", signingKey));
        ExecutorPackageSignature signature = ExecutorPackageSignature.Parse(contents.ExecutorSignature);
        verifier.Verify(contents.ExecutorManifest, in signature).ShouldBeTrue();

        // The signature is over the exact manifest bytes: flip one byte and it no longer verifies.
        byte[] tampered = contents.ExecutorManifest!.ToArray();
        tampered[^2] ^= 0xFF;
        verifier.Verify(tampered, in signature).ShouldBeFalse();
    }

    [TestMethod]
    public async Task ProjectAsync_without_a_signer_leaves_the_package_unsigned()
    {
        CatalogPackageProjection projection = await CatalogPackage.ProjectAsync(
            WorkflowPackage.Pack(Workflow("flow"), []), "flow", 1, executorProvider: new FakeExecutorProvider(), signer: null, cancellationToken: default);

        WorkflowPackage.Open(projection.CanonicalPackage).ExecutorSignature.ShouldBeNull();

        // With no signer the projection is byte-identical to the synchronous one.
        CatalogPackageProjection sync = CatalogPackage.Project(WorkflowPackage.Pack(Workflow("flow"), []), "flow", 1, executorProvider: new FakeExecutorProvider());
        projection.CanonicalPackage.ToArray().ShouldBe(sync.CanonicalPackage.ToArray());
    }

    [TestMethod]
    public async Task ProjectAsync_produces_no_signature_when_no_executor_is_compiled()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(signingKey, "release-2026");

        // The provider yields no executor, so there is nothing to sign; the package is stored unsigned (and not runnable).
        CatalogPackageProjection projection = await CatalogPackage.ProjectAsync(
            WorkflowPackage.Pack(Workflow("flow"), []), "flow", 1, executorProvider: new NullExecutorProvider(), signer: signer, cancellationToken: default);

        projection.HasExecutor.ShouldBeFalse();
        WorkflowPackage.Open(projection.CanonicalPackage).ExecutorSignature.ShouldBeNull();
    }

    [TestMethod]
    public async Task Store_signs_at_add_and_the_stored_package_verifies_at_load()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new InMemoryWorkflowCatalogStore(executorProvider: new FakeExecutorProvider(), signer: new EcdsaExecutorPackageSigner(signingKey, "release-2026"));

        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("flow", WorkflowPackage.Pack(Workflow("flow"), []), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        // Fetch the stored package exactly as a runner would, and verify the executor's provenance before loading it.
        ReadOnlyMemory<byte>? stored = await store.GetPackageAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, default);
        stored.ShouldNotBeNull();

        WorkflowPackageContents contents = WorkflowPackage.Open(stored.Value);
        contents.ExecutorSignature.ShouldNotBeNull();

        IExecutorPackageVerifier verifier = TrustStore(("release-2026", signingKey));
        ExecutorPackageSignature signature = ExecutorPackageSignature.Parse(contents.ExecutorSignature);
        verifier.Verify(contents.ExecutorManifest, in signature).ShouldBeTrue();
    }

    [TestMethod]
    public async Task The_signature_is_addressable_by_document_name_for_the_runner_load_path()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new InMemoryWorkflowCatalogStore(executorProvider: new FakeExecutorProvider(), signer: new EcdsaExecutorPackageSigner(signingKey, "release-2026"));
        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("flow", WorkflowPackage.Pack(Workflow("flow"), []), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        // HostedWorkflowResumer fetches the detached signature exactly this way, alongside the assembly and manifest.
        ReadOnlyMemory<byte>? sig = await store.GetDocumentAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, WorkflowPackage.ExecutorManifestSignatureDocumentName, default);
        ReadOnlyMemory<byte>? manifest = await store.GetDocumentAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, WorkflowPackage.ExecutorManifestDocumentName, default);
        sig.ShouldNotBeNull();
        manifest.ShouldNotBeNull();

        ExecutorPackageSignature signature = ExecutorPackageSignature.Parse(sig.Value);
        TrustStore(("release-2026", signingKey)).Verify(manifest.Value, in signature).ShouldBeTrue();
    }

    [TestMethod]
    public async Task An_unsigned_version_has_no_signature_document()
    {
        var store = new InMemoryWorkflowCatalogStore(executorProvider: new FakeExecutorProvider());
        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("flow", WorkflowPackage.Pack(Workflow("flow"), []), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        // No signer configured → the runner's fetch returns null, and a loader without a verifier loads it unsigned.
        (await store.GetDocumentAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, WorkflowPackage.ExecutorManifestSignatureDocumentName, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_package_signed_by_an_untrusted_key_does_not_verify()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new InMemoryWorkflowCatalogStore(executorProvider: new FakeExecutorProvider(), signer: new EcdsaExecutorPackageSigner(signingKey, "attacker"));

        using ParsedJsonDocument<CatalogVersion> versionDoc = await store.AddAsync("flow", WorkflowPackage.Pack(Workflow("flow"), []), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;
        WorkflowPackageContents contents = WorkflowPackage.Open((await store.GetPackageAsync(version.Ref.BaseWorkflowId, version.Ref.VersionNumber, default))!.Value);

        // The runner trusts only "release-2026" — a signature stamped with an unknown key id is rejected (fail-closed),
        // as is a signature whose key id it trusts but whose bytes were made by a different key.
        ExecutorPackageSignature signature = ExecutorPackageSignature.Parse(contents.ExecutorSignature!);
        TrustStore(("release-2026", otherKey)).Verify(contents.ExecutorManifest, in signature).ShouldBeFalse();
        TrustStore(("attacker", otherKey)).Verify(contents.ExecutorManifest, in signature).ShouldBeFalse();
    }

    // Builds a runner trust store holding only the PUBLIC half of each signing key (exactly what a runner is configured
    // with) — the private parameters are never exported into it.
    private static TrustStoreExecutorPackageVerifier TrustStore(params (string KeyId, ECDsa SigningKey)[] keys)
    {
        var trusted = new Dictionary<string, AsymmetricAlgorithm>(StringComparer.Ordinal);
        foreach ((string keyId, ECDsa signingKey) in keys)
        {
            var publicKey = ECDsa.Create();
            publicKey.ImportParameters(signingKey.ExportParameters(includePrivateParameters: false));
            trusted[keyId] = publicKey;
        }

        return new TrustStoreExecutorPackageVerifier(trusted);
    }

    private static CatalogMetadata Meta() => new(new CatalogOwner("Team", "team@example.com"), "alice");

    private static byte[] Workflow(string id)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"{{id}}","steps":[]}]}""");

    private sealed class FakeExecutorProvider : IWorkflowExecutorProvider
    {
        public WorkflowExecutorArtifact? BuildExecutor(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources, string packageHash)
            => new(
                new byte[] { 0x4D, 0x5A, 0x00, 0xFF },
                Encoding.UTF8.GetBytes($$"""{"formatVersion":1,"entryType":"X","packageHash":"{{packageHash}}"}"""));
    }

    private sealed class NullExecutorProvider : IWorkflowExecutorProvider
    {
        public WorkflowExecutorArtifact? BuildExecutor(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources, string packageHash)
            => null;
    }
}
