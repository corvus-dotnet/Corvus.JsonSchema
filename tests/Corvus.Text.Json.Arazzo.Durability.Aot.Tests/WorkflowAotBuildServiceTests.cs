// <copyright file="WorkflowAotBuildServiceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Aot.Tests;

/// <summary>
/// The build service's orchestration and the end-to-end trust chain (ADR 0055): it verifies a version's signed executor,
/// assembles the host-app around the signed IL, native-AOT compiles it with the target's builder (a fake here, the real
/// container build is the integration proof), signs the resulting binary, and attaches the binary with its attestation
/// without changing the content hash. It refuses to build an unsigned, tampered, or untrusted executor, surfaces a
/// compile failure as a non-successful outcome, and its deploy-time counterpart verifies the native attestation and
/// catches a swapped or untrusted binary.
/// </summary>
[TestClass]
public sealed class WorkflowAotBuildServiceTests
{
    // A real managed assembly (the Aot assembly itself) stands in for the signed executor.dll, so the assembler can read
    // its assembly name from valid PE metadata; the fake builder never compiles it.
    private static readonly byte[] ExecutorAssembly = File.ReadAllBytes(typeof(WorkflowAotBuildService).Assembly.Location);

    private static readonly byte[] Workflow = Encoding.UTF8.GetBytes(
        """{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"flow-v1","steps":[]}]}""");

    private static readonly byte[] Native = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3];

    // The content hash of the test version, which a real executor manifest's packageHash equals (the executor is built
    // from that exact content). Binding the attestation to it is what the deploy check enforces.
    private static readonly string PackageHash = CatalogPackage.HashCanonical(WorkflowPackage.Pack(Workflow, []));

    [TestMethod]
    public async Task Builds_verifies_signs_and_attaches_leaving_the_hash_unchanged()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var builder = new CapturingAotBuilder(AotBuildResult.Success(Native, "ilc ok"));
        IExecutorPackageVerifier verifier = TrustStore(("release-2026", key));
        var service = new WorkflowAotBuildService(verifier, Signer(key), builder, Options());

        WorkflowAotBuildOutcome outcome = await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None);

        outcome.Succeeded.ShouldBeTrue(outcome.Log);
        outcome.RuntimeIdentifier.ShouldBe("linux-x64");

        // The host-app was assembled around the signed executor for the requested target.
        builder.Received.ShouldNotBeNull();
        builder.Received!.Value.RuntimeIdentifier.ShouldBe("linux-x64");
        builder.Received!.Value.EntryType.ShouldBe("X");

        // The native binary reads back, its attestation verifies against the trust store, and the content hash is unchanged.
        WorkflowPackage.TryReadNativeArtifact(outcome.Package, "linux-x64", out ReadOnlyMemory<byte> attached).ShouldBeTrue();
        attached.ToArray().ShouldBe(Native);

        NativeArtifactAttestation attestation = WorkflowAotBuildService.VerifyNativeArtifact(outcome.Package, "linux-x64", verifier);
        attestation.RuntimeIdentifier.ShouldBe("linux-x64");
        attestation.NativeDigest.ShouldBe(NativeArtifactAttestation.ComputeDigest(Native));
        attestation.PackageHash.ShouldBe(CatalogPackage.HashCanonical(package));
        attestation.EngineVersion.ShouldBe("5.0.0");

        CatalogPackage.HashCanonical(outcome.Package).ShouldBe(CatalogPackage.HashCanonical(package));
    }

    [TestMethod]
    public async Task A_native_AOT_compile_failure_is_a_non_successful_outcome_carrying_the_log()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var builder = new CapturingAotBuilder(AotBuildResult.Failure("error IL3050: reflection"));
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options());

        WorkflowAotBuildOutcome outcome = await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None);

        outcome.Succeeded.ShouldBeFalse();
        outcome.Log.ShouldContain("IL3050");
        outcome.Package.IsEmpty.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Refuses_to_build_an_unsigned_executor()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] unsigned = WorkflowPackage.Pack(
            Workflow, [], executor: ExecutorAssembly, executorManifest: Manifest(Digest(ExecutorAssembly)));
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(unsigned, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public async Task Refuses_to_build_when_the_assembly_does_not_match_the_signed_manifest()
    {
        // The manifest is signed, but its assemblyDigest names a different assembly than the one packed.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest("sha256:deadbeef"));
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public async Task Refuses_to_build_a_signature_from_an_untrusted_key()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(signingKey, "release-2026", Manifest(Digest(ExecutorAssembly)));

        // The verifier trusts the key id but holds a different key, so the executor signature does not verify.
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", otherKey)), Signer(signingKey), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public async Task Refuses_a_package_that_carries_no_executor()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] noExecutor = WorkflowPackage.Pack(Workflow, []);
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(noExecutor, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public async Task Deploy_verification_catches_a_swapped_native_binary()
    {
        // Sign an attestation over one binary, but attach a different binary alongside it (the swap an attacker would make
        // in storage or transit). Deploy verification recomputes the digest and rejects the mismatch.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        byte[] swapped = [0x7F, (byte)'E', (byte)'L', (byte)'F', 9, 9, 9];

        var attestation = new NativeArtifactAttestation(
            NativeArtifactAttestation.CurrentFormatVersion, CatalogPackage.HashCanonical(package), "linux-x64", "5.0.0", NativeArtifactAttestation.ComputeDigest(Native));
        byte[] manifest = attestation.ToUtf8();
        ExecutorPackageSignature signature = await Signer(key).SignAsync(manifest, CancellationToken.None);
        byte[] tampered = WorkflowPackage.AttachSignedNativeArtifact(package, "linux-x64", swapped, manifest, signature.ToUtf8());

        Should.Throw<WorkflowAotBuildException>(
            () => WorkflowAotBuildService.VerifyNativeArtifact(tampered, "linux-x64", TrustStore(("release-2026", key))));
    }

    [TestMethod]
    public async Task Deploy_verification_rejects_an_attestation_for_a_different_version()
    {
        // A validly-signed native from one version cannot be replayed for another: the attestation's package hash must
        // match the version it rides.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var attestation = new NativeArtifactAttestation(
            NativeArtifactAttestation.CurrentFormatVersion, "a-different-version-hash", "linux-x64", "5.0.0", NativeArtifactAttestation.ComputeDigest(Native));
        byte[] manifest = attestation.ToUtf8();
        ExecutorPackageSignature signature = await Signer(key).SignAsync(manifest, CancellationToken.None);
        byte[] replayed = WorkflowPackage.AttachSignedNativeArtifact(package, "linux-x64", Native, manifest, signature.ToUtf8());

        Should.Throw<WorkflowAotBuildException>(
            () => WorkflowAotBuildService.VerifyNativeArtifact(replayed, "linux-x64", TrustStore(("release-2026", key))));
    }

    [TestMethod]
    public async Task Deploy_verification_rejects_an_untrusted_native_signature_and_an_unsigned_binary()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), new CapturingAotBuilder(AotBuildResult.Success(Native, "ok")), Options());
        WorkflowAotBuildOutcome outcome = await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None);

        // A trust store that does not hold the signing key rejects the native attestation.
        Should.Throw<WorkflowAotBuildException>(
            () => WorkflowAotBuildService.VerifyNativeArtifact(outcome.Package, "linux-x64", TrustStore(("release-2026", otherKey))));

        // A binary attached without an attestation is unsigned; a deploy refuses it.
        byte[] unsignedNative = WorkflowPackage.AttachNativeArtifact(package, "linux-x64", Native);
        Should.Throw<WorkflowAotBuildException>(
            () => WorkflowAotBuildService.VerifyNativeArtifact(unsignedNative, "linux-x64", TrustStore(("release-2026", key))));
    }

    [TestMethod]
    public void Attestation_round_trips_through_utf8()
    {
        var attestation = new NativeArtifactAttestation(1, "packagehash", "linux-x64", "5.0.0", "sha256:abcd");
        NativeArtifactAttestation.Parse(attestation.ToUtf8()).ShouldBe(attestation);
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        Should.Throw<ArgumentNullException>(() => new WorkflowAotBuildService(null!, NullSigner(), Ok(), Options()));
        Should.Throw<ArgumentNullException>(() => new WorkflowAotBuildService(Verifier(), null!, Ok(), Options()));
        Should.Throw<ArgumentNullException>(() => new WorkflowAotBuildService(Verifier(), NullSigner(), null!, Options()));
        Should.Throw<ArgumentNullException>(() => new WorkflowAotBuildService(Verifier(), NullSigner(), Ok(), null!));
    }

    [TestMethod]
    public async Task Deploy_verifies_the_binary_then_hands_it_to_the_deployer()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var built = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), new CapturingAotBuilder(AotBuildResult.Success(Native, "ok")), Options());
        WorkflowAotBuildOutcome build = await built.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None);

        var deployer = new CapturingDeployer(ServerlessDeployResult.Success("https://fn.example/invoke", "deployed"));
        var service = new WorkflowDeployService(TrustStore(("release-2026", key)), deployer);

        WorkflowDeployOutcome outcome = await service.DeployAsync(build.Package, "flow", 1, "production", "linux-x64", CancellationToken.None);

        outcome.Succeeded.ShouldBeTrue(outcome.Log);
        outcome.FunctionUrl.ShouldBe("https://fn.example/invoke");

        // The verified native binary was handed to the deployer for the requested target.
        deployer.Received.ShouldNotBeNull();
        deployer.Received!.Value.BaseWorkflowId.ShouldBe("flow");
        deployer.Received!.Value.Environment.ShouldBe("production");
        deployer.Received!.Value.RuntimeIdentifier.ShouldBe("linux-x64");
        deployer.Received!.Value.NativeBinary.ToArray().ShouldBe(Native);
    }

    [TestMethod]
    public async Task Deploy_returns_a_failure_when_the_deployer_fails()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var built = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), new CapturingAotBuilder(AotBuildResult.Success(Native, "ok")), Options());
        WorkflowAotBuildOutcome build = await built.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None);

        var service = new WorkflowDeployService(TrustStore(("release-2026", key)), new CapturingDeployer(ServerlessDeployResult.Failure("throttled")));
        WorkflowDeployOutcome outcome = await service.DeployAsync(build.Package, "flow", 1, "production", "linux-x64", CancellationToken.None);

        outcome.Succeeded.ShouldBeFalse();
        outcome.FunctionUrl.ShouldBeEmpty();
        outcome.Log.ShouldBe("throttled");
    }

    [TestMethod]
    public async Task Deploy_refuses_a_swapped_binary_before_it_reaches_the_deployer()
    {
        // A binary swapped in storage or transit is caught by the runner's verify before it enters the user's account
        // (ADR 0059), so the deployer is never called.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        byte[] swapped = [0x7F, (byte)'E', (byte)'L', (byte)'F', 9, 9, 9];
        var attestation = new NativeArtifactAttestation(
            NativeArtifactAttestation.CurrentFormatVersion, CatalogPackage.HashCanonical(package), "linux-x64", "5.0.0", NativeArtifactAttestation.ComputeDigest(Native));
        byte[] manifest = attestation.ToUtf8();
        ExecutorPackageSignature signature = await Signer(key).SignAsync(manifest, CancellationToken.None);
        byte[] tampered = WorkflowPackage.AttachSignedNativeArtifact(package, "linux-x64", swapped, manifest, signature.ToUtf8());

        var deployer = new CapturingDeployer(ServerlessDeployResult.Success("https://never", "never"));
        var service = new WorkflowDeployService(TrustStore(("release-2026", key)), deployer);

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.DeployAsync(tampered, "flow", 1, "production", "linux-x64", CancellationToken.None));
        deployer.Received.ShouldBeNull();
    }

    // A deployer that records the request it received and returns a configured result.
    private sealed class CapturingDeployer(ServerlessDeployResult result) : IServerlessDeployer
    {
        public ServerlessDeployRequest? Received { get; private set; }

        public ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken)
        {
            this.Received = request;
            return ValueTask.FromResult(result);
        }
    }

    private static AotHostAppOptions Options()
        => new() { RuntimePackageVersion = "5.0.0", FeedSources = [("local", "/tmp/feed")] };

    private static string Digest(byte[] assembly) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(assembly));

    private static byte[] Manifest(string assemblyDigest)
        => Encoding.UTF8.GetBytes(
            $$"""{"formatVersion":2,"targetFramework":"net10.0","packageHash":"{{PackageHash}}","assemblyDigest":"{{assemblyDigest}}","entryType":"X","workflowId":"flow-v1","durable":true,"engineVersion":"5.0.0"}""");

    private static IExecutorPackageSigner Signer(ECDsa key, string keyId = "release-2026") => new EcdsaExecutorPackageSigner(key, keyId);

    private static async Task<byte[]> SignedPackage(ECDsa signingKey, string keyId, byte[] manifest)
    {
        ExecutorPackageSignature signature = await new EcdsaExecutorPackageSigner(signingKey, keyId).SignAsync(manifest, CancellationToken.None);
        return WorkflowPackage.Pack(
            Workflow, [], executor: ExecutorAssembly, executorManifest: manifest, executorSignature: signature.ToUtf8());
    }

    // A trust store holding only the public half of each signing key (what a runner or the control plane's own verifier holds).
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

    private static IExecutorPackageVerifier Verifier()
        => new TrustStoreExecutorPackageVerifier(new Dictionary<string, AsymmetricAlgorithm>(StringComparer.Ordinal));

    // A signer whose key never signs anything the tests check; used only where the constructor's null guards are under test.
    private static IExecutorPackageSigner NullSigner()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new EcdsaExecutorPackageSigner(key, "unused");
    }

    private static IWorkflowAotBuilder Ok() => new CapturingAotBuilder(AotBuildResult.Success(Native, "ok"));

    // Captures the assembled host-app it is asked to build and returns a canned result, so the trust chain and attach run
    // without a real container build.
    private sealed class CapturingAotBuilder(AotBuildResult result) : IWorkflowAotBuilder
    {
        public AssembledHostApp? Received { get; private set; }

        public ValueTask<AotBuildResult> BuildAsync(AssembledHostApp hostApp, CancellationToken cancellationToken)
        {
            this.Received = hostApp;
            return new ValueTask<AotBuildResult>(result);
        }
    }
}