// <copyright file="WorkflowExecutorLoaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves the <see cref="WorkflowExecutorLoader"/> verifies, loads, caches, runs, and unloads a real compiled
/// executor produced by <see cref="WorkflowExecutorProvider"/> — and refuses a tampered or mismatched one.
/// </summary>
[TestClass]
public class WorkflowExecutorLoaderTests
{
    private const string WorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt-v1",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string PetstoreOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{petId}": {
              "get": {
                "operationId": "getPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task Loads_verifies_caches_and_runs_a_real_executor()
    {
        const string hash = "hash-abc";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader();

        LoadedWorkflow loaded = loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash);

        loaded.Manifest.WorkflowId.ShouldBe("adopt-v1");
        loaded.Workflow.Descriptor.WorkflowId.ShouldBe("adopt-v1");

        // A second Load returns the cached instance, and TryGet sees it.
        loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash).ShouldBeSameAs(loaded);
        loader.TryGet("adopt", 1, out LoadedWorkflow? got).ShouldBeTrue();
        got.ShouldBeSameAs(loaded);

        // The loaded workflow runs durably through the contract.
        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        var run = new FakeWorkflowRun();

        WorkflowRunResultKind kind = await loaded.Workflow.RunAsync(loaded.Workflow.Descriptor.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null, workspace, inputs.RootElement, run, default);

        kind.ShouldBe(WorkflowRunResultKind.Completed);
        run.Completed.ShouldBeTrue();
        transport.Requests[0].Path.ShouldBe("/pets/42");
    }

    [TestMethod]
    public void Rejects_a_package_hash_mismatch()
    {
        WorkflowExecutorArtifact artifact = BuildArtifact("the-real-hash");
        using var loader = new WorkflowExecutorLoader();

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, "a-different-hash"));
        ex.Message.ShouldContain("package hash");
        loader.TryGet("adopt", 1, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Rejects_a_tampered_assembly()
    {
        const string hash = "hash-xyz";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        byte[] tampered = artifact.Assembly.ToArray();
        tampered[^1] ^= 0xFF;

        using var loader = new WorkflowExecutorLoader();

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, tampered, artifact.Manifest, hash));
        ex.Message.ShouldContain("digest");
    }

    [TestMethod]
    public void Rejects_an_unsupported_target_framework()
    {
        const string hash = "hash-tfm";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader(supportedTargetFramework: "net9.0");

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash));
        ex.Message.ShouldContain("net9.0");
    }

    [TestMethod]
    public void Unload_evicts_the_version_and_it_can_be_reloaded()
    {
        const string hash = "hash-unload";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader();

        loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash);
        loader.Unload("adopt", 1).ShouldBeTrue();
        loader.TryGet("adopt", 1, out _).ShouldBeFalse();
        loader.Unload("adopt", 1).ShouldBeFalse();

        // Reloading after unload works.
        LoadedWorkflow reloaded = loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash);
        reloaded.Workflow.Descriptor.WorkflowId.ShouldBe("adopt-v1");
    }

    [TestMethod]
    public async Task Ecdsa_signer_and_trust_store_verifier_round_trip()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(key, "k1");
        byte[] payload = Encoding.UTF8.GetBytes("""{"formatVersion":1}""");

        ExecutorPackageSignature signature = await signer.SignAsync(payload, default);
        signature.Algorithm.ShouldBe(ExecutorSignatureAlgorithms.EcdsaP256Sha256);
        signature.KeyId.ShouldBe("k1");

        // Round-trip through the detached .sig JSON form, then verify with a trust store holding only the public key.
        ExecutorPackageSignature parsed = ExecutorPackageSignature.Parse(signature.ToUtf8());
        IExecutorPackageVerifier verifier = TrustStore(("k1", key));
        verifier.Verify(payload, parsed).ShouldBeTrue();

        // A different payload does not verify against the same signature.
        verifier.Verify(Encoding.UTF8.GetBytes("""{"formatVersion":2}"""), parsed).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Verifies_a_signed_package_against_the_trust_store()
    {
        const string hash = "hash-signed";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);

        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(signingKey, "release-2026");
        byte[] signatureUtf8 = (await signer.SignAsync(artifact.Manifest, default)).ToUtf8();

        // The runner trusts only the public key exported from the signing key — never the private key.
        using var loader = new WorkflowExecutorLoader(verifier: TrustStore(("release-2026", signingKey)));

        LoadedWorkflow loaded = loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash, signatureUtf8);
        loaded.Manifest.WorkflowId.ShouldBe("adopt-v1");
    }

    [TestMethod]
    public void Rejects_an_unsigned_package_when_signing_is_required()
    {
        const string hash = "hash-unsigned";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var loader = new WorkflowExecutorLoader(verifier: TrustStore(("release-2026", signingKey)));

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash));
        ex.Message.ShouldContain("unsigned");
        loader.TryGet("adopt", 1, out _).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Rejects_a_signature_from_an_untrusted_key()
    {
        const string hash = "hash-untrusted";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);

        // A signature made by a key the runner does not trust (the untrusted-runner threat: even a valid signature
        // from the wrong key is rejected).
        using ECDsa rogueKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa trustedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(rogueKey, "rogue");
        byte[] signatureUtf8 = (await signer.SignAsync(artifact.Manifest, default)).ToUtf8();

        using var loader = new WorkflowExecutorLoader(verifier: TrustStore(("release-2026", trustedKey)));

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash, signatureUtf8));
        ex.Message.ShouldContain("did not verify");
    }

    [TestMethod]
    public async Task Rejects_a_tampered_signature()
    {
        const string hash = "hash-tampered-sig";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(signingKey, "release-2026");

        ExecutorPackageSignature signature = await signer.SignAsync(artifact.Manifest, default);
        byte[] tampered = signature.Value.ToArray();
        tampered[^1] ^= 0xFF;
        byte[] signatureUtf8 = new ExecutorPackageSignature(signature.Algorithm, signature.KeyId, tampered).ToUtf8();

        using var loader = new WorkflowExecutorLoader(verifier: TrustStore(("release-2026", signingKey)));

        Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash, signatureUtf8));
    }

    [TestMethod]
    public void The_manifest_declares_the_workflows_sources()
    {
        WorkflowExecutorArtifact artifact = BuildArtifact("hash-sources");

        WorkflowExecutorManifest manifest = WorkflowExecutorManifest.Parse(artifact.Manifest);
        manifest.Sources.ShouldContain(s => s.Name == "petstore" && s.Type == "openapi");
    }

    [TestMethod]
    public void Loads_when_the_manifest_declares_every_source_the_workflow_requires()
    {
        const string hash = "hash-sources-ok";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader();

        // The compiled workflow requires "petstore", and the real manifest declares it — the load-time source check passes.
        LoadedWorkflow loaded = loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash);
        loaded.Workflow.Descriptor.Sources.ShouldContain("petstore");
    }

    [TestMethod]
    public void Rejects_a_manifest_that_underdeclares_the_workflows_sources()
    {
        const string hash = "hash-sources-missing";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader();

        // Rebuild the manifest with an empty sources[] but every other field (assembly digest, package hash, entry type,
        // target framework) intact, so integrity verification passes and only the source-consistency check can fail.
        WorkflowExecutorManifest real = WorkflowExecutorManifest.Parse(artifact.Manifest);
        byte[] underdeclared = Encoding.UTF8.GetBytes(
            $$"""{"assemblyDigest":"{{real.AssemblyDigest}}","durable":{{(real.Durable ? "true" : "false")}},"entryType":"{{real.EntryType}}","formatVersion":{{real.FormatVersion}},"packageHash":"{{real.PackageHash}}","sources":[],"targetFramework":"{{real.TargetFramework}}","workflowId":"{{real.WorkflowId}}"}""");

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, underdeclared, hash));
        ex.Message.ShouldContain("petstore");
    }

    // A trust store holding only the PUBLIC half of each signing key (exported without the private parameters), so the
    // verifier proves out the custody split: a runner verifies with material it could never sign with.
    private static IExecutorPackageVerifier TrustStore(params (string KeyId, ECDsa SigningKey)[] keys)
    {
        var trusted = new Dictionary<string, AsymmetricAlgorithm>(StringComparer.Ordinal);
        foreach ((string keyId, ECDsa signingKey) in keys)
        {
            ECDsa publicOnly = ECDsa.Create();
            publicOnly.ImportParameters(signingKey.ExportParameters(includePrivateParameters: false));
            trusted[keyId] = publicOnly;
        }

        return new TrustStoreExecutorPackageVerifier(trusted);
    }

    private static WorkflowExecutorArtifact BuildArtifact(string packageHash)
    {
        var log = new List<string>();
        var provider = new WorkflowExecutorProvider(durable: true, log.Add);
        var sources = new List<KeyValuePair<string, byte[]>>
        {
            new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi)),
        };

        WorkflowExecutorArtifact? artifact = provider.BuildExecutor(Encoding.UTF8.GetBytes(WorkflowJson), sources, packageHash);
        artifact.ShouldNotBeNull($"build failed. Progress:\n{string.Join("\n", log)}");
        return artifact!.Value;
    }
}
