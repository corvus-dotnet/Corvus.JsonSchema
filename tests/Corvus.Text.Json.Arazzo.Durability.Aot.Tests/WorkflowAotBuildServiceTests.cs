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
/// The build service's orchestration and trust chain (ADR 0055): it verifies a version's signed executor, assembles the
/// host-app around the signed IL, native-AOT compiles it with the target's builder (a fake here — the real container
/// build is the integration proof), and attaches the native binary to the package without changing the content hash. It
/// refuses to build an unsigned, tampered, or untrusted executor, and surfaces a compile failure as a non-successful
/// outcome carrying the log.
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

    [TestMethod]
    public async Task Builds_verifies_and_attaches_the_native_binary_leaving_the_hash_unchanged()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var builder = new CapturingAotBuilder(AotBuildResult.Success(Native, "ilc ok"));
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), builder, Options());

        WorkflowAotBuildOutcome outcome = await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None);

        outcome.Succeeded.ShouldBeTrue(outcome.Log);
        outcome.RuntimeIdentifier.ShouldBe("linux-x64");

        // The host-app was assembled around the signed executor for the requested target.
        builder.Received.ShouldNotBeNull();
        builder.Received!.Value.RuntimeIdentifier.ShouldBe("linux-x64");
        builder.Received!.Value.EntryType.ShouldBe("X");

        // The native binary is attached and reads back; the content hash is unchanged (a native binary is metadata).
        WorkflowPackage.TryReadNativeArtifact(outcome.Package, "linux-x64", out ReadOnlyMemory<byte> attached).ShouldBeTrue();
        attached.ToArray().ShouldBe(Native);
        CatalogPackage.HashCanonical(outcome.Package).ShouldBe(CatalogPackage.HashCanonical(package));
    }

    [TestMethod]
    public async Task A_native_AOT_compile_failure_is_a_non_successful_outcome_carrying_the_log()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var builder = new CapturingAotBuilder(AotBuildResult.Failure("error IL3050: reflection"));
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), builder, Options());

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
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(unsigned, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public async Task Refuses_to_build_when_the_assembly_does_not_match_the_signed_manifest()
    {
        // The manifest is signed, but its assemblyDigest names a different assembly than the one packed.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest("sha256:deadbeef"));
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public async Task Refuses_to_build_a_signature_from_an_untrusted_key()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(signingKey, "release-2026", Manifest(Digest(ExecutorAssembly)));

        // The verifier trusts the key id but holds a different key, so the signature does not verify.
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", otherKey)), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(package, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public async Task Refuses_a_package_that_carries_no_executor()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] noExecutor = WorkflowPackage.Pack(Workflow, []);
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Ok(), Options());

        await Should.ThrowAsync<WorkflowAotBuildException>(
            async () => await service.BuildAndAttachAsync(noExecutor, "linux-x64", CancellationToken.None));
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        Should.Throw<ArgumentNullException>(() => new WorkflowAotBuildService(null!, Ok(), Options()));
        Should.Throw<ArgumentNullException>(() => new WorkflowAotBuildService(Verifier(), null!, Options()));
        Should.Throw<ArgumentNullException>(() => new WorkflowAotBuildService(Verifier(), Ok(), null!));
    }

    private static AotHostAppOptions Options()
        => new() { RuntimePackageVersion = "5.0.0", FeedSources = [("local", "/tmp/feed")] };

    private static string Digest(byte[] assembly) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(assembly));

    private static byte[] Manifest(string assemblyDigest)
        => Encoding.UTF8.GetBytes(
            $$"""{"formatVersion":2,"targetFramework":"net10.0","packageHash":"h","assemblyDigest":"{{assemblyDigest}}","entryType":"X","workflowId":"flow-v1","durable":true,"engineVersion":"5.0.0"}""");

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