// <copyright file="WorkflowDeployWorkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Aot.Tests;

/// <summary>
/// The deploy worker's state machine (ADR 0059): it claims the oldest queued deployment, loads the version's package,
/// verifies the signed native binary and deploys it via the deploy service, and completes the deployment Deployed with the
/// function URL; a deploy failure, a bad or unverifiable binary, or a vanished version is recorded as a Failed completion
/// (not thrown) so one bad deployment does not stall the worker; and a re-enqueue (a redeploy) mid-deploy supersedes the
/// completion, leaving the fresh Queued deployment for the next claim. A real in-memory deployment store exercises the
/// claim/complete concurrency; a fake catalog feeds the attested package; a fake deployer stands in for the function
/// platform.
/// </summary>
[TestClass]
public sealed class WorkflowDeployWorkerTests
{
    // A real managed assembly (the Aot assembly itself) stands in for the signed executor.dll, so the assembler can read
    // its assembly name from valid PE metadata; the fake builder never compiles it.
    private static readonly byte[] ExecutorAssembly = File.ReadAllBytes(typeof(WorkflowAotBuildService).Assembly.Location);

    private static readonly byte[] Workflow = Encoding.UTF8.GetBytes(
        """{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"flow-v1","steps":[]}]}""");

    private static readonly byte[] Native = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3];

    // The content hash of the test version, which the executor manifest's packageHash equals (the executor is built from
    // that exact content). The native attestation binds to it, which the deploy's verify enforces.
    private static readonly string PackageHash = CatalogPackage.HashCanonical(WorkflowPackage.Pack(Workflow, []));

    [TestMethod]
    public async Task Deploys_the_function_and_records_the_url()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ReadOnlyMemory<byte> package = await AttestedPackage(key);

        var deployments = new InMemoryWorkflowDeploymentStore();
        string id = await Enqueue(deployments, "flow", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);
        var deployer = new FakeDeployer(ServerlessDeployResult.Success("https://fn.example/invoke", "deployed"));
        var worker = new WorkflowDeployWorker(deployments, catalog, new WorkflowDeployService(TrustStore(("release-2026", key)), deployer));

        WorkflowDeployWorkerResult result = await worker.DriveNextAsync("deployer-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Claimed.ShouldBeTrue();
        result.WasSuperseded.ShouldBeFalse();
        result.Outcome.ShouldBe(WorkflowDeploymentStatus.Deployed);
        result.FunctionUrl.ShouldBe("https://fn.example/invoke");
        deployer.DeployCalls.ShouldBe(1);

        // The deployment is Deployed in the store, carrying the invoke URL the run backend routes to.
        using ParsedJsonDocument<WorkflowDeployment>? deployment = await deployments.GetAsync(id, CancellationToken.None);
        deployment!.RootElement.StatusValue.ShouldBe("Deployed");
        deployment.RootElement.FunctionUrlOrNull.ShouldBe("https://fn.example/invoke");
        deployment.RootElement.ClaimedByOrNull.ShouldBe("deployer-1");
        deployment.RootElement.CompletedAtValue.ShouldNotBeNull();

        // The dispatch-ready gate's predicate now admits a run for the target (5a). IsDeployedAsync is a default interface
        // method, so it is called through the interface.
        (await ((IWorkflowDeploymentStore)deployments).IsDeployedAsync("flow", 1, "production", "linux-x64", CancellationToken.None)).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_deploy_failure_marks_the_deployment_failed_with_the_log()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ReadOnlyMemory<byte> package = await AttestedPackage(key);

        var deployments = new InMemoryWorkflowDeploymentStore();
        string id = await Enqueue(deployments, "flow", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);
        var worker = new WorkflowDeployWorker(deployments, catalog, new WorkflowDeployService(TrustStore(("release-2026", key)), new FakeDeployer(ServerlessDeployResult.Failure("throttled"))));

        WorkflowDeployWorkerResult result = await worker.DriveNextAsync("deployer-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(WorkflowDeploymentStatus.Failed);
        result.FailureReason.ShouldNotBeNull().ShouldContain("throttled");

        using ParsedJsonDocument<WorkflowDeployment>? deployment = await deployments.GetAsync(id, CancellationToken.None);
        deployment!.RootElement.StatusValue.ShouldBe("Failed");
        deployment.RootElement.FailureReasonOrNull.ShouldNotBeNull().ShouldContain("throttled");
        (await ((IWorkflowDeploymentStore)deployments).IsDeployedAsync("flow", 1, "production", "linux-x64", CancellationToken.None)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task An_unverifiable_binary_marks_the_deployment_failed_without_deploying()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // A package with no native binary attached: the deploy service's verify throws (WorkflowAotBuildException), which
        // the worker records as a Failed completion rather than deploying an unverified binary.
        byte[] noNative = WorkflowPackage.Pack(Workflow, []);
        var deployments = new InMemoryWorkflowDeploymentStore();
        await Enqueue(deployments, "flow", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(noNative);
        var deployer = new FakeDeployer(ServerlessDeployResult.Success("https://never", "never"));
        var worker = new WorkflowDeployWorker(deployments, catalog, new WorkflowDeployService(TrustStore(("release-2026", key)), deployer));

        WorkflowDeployWorkerResult result = await worker.DriveNextAsync("deployer-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(WorkflowDeploymentStatus.Failed);
        deployer.DeployCalls.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_vanished_version_marks_the_deployment_failed()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deployments = new InMemoryWorkflowDeploymentStore();
        await Enqueue(deployments, "flow", 1, "production", "linux-x64");

        // The version was deleted between enqueue and claim: the catalog has no package.
        var catalog = new FakeCatalog(package: null);
        var deployer = new FakeDeployer(ServerlessDeployResult.Success("https://never", "never"));
        var worker = new WorkflowDeployWorker(deployments, catalog, new WorkflowDeployService(TrustStore(("release-2026", key)), deployer));

        WorkflowDeployWorkerResult result = await worker.DriveNextAsync("deployer-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(WorkflowDeploymentStatus.Failed);
        result.FailureReason.ShouldNotBeNull().ShouldContain("no longer exists");
        deployer.DeployCalls.ShouldBe(0);
    }

    [TestMethod]
    public async Task Returns_idle_when_no_deployment_is_queued()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deployments = new InMemoryWorkflowDeploymentStore();
        var catalog = new FakeCatalog(await AttestedPackage(key));
        var deployer = new FakeDeployer(ServerlessDeployResult.Success("https://x", "ok"));
        var worker = new WorkflowDeployWorker(deployments, catalog, new WorkflowDeployService(TrustStore(("release-2026", key)), deployer));

        WorkflowDeployWorkerResult result = await worker.DriveNextAsync("deployer-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Claimed.ShouldBeFalse();
        result.Outcome.ShouldBeNull();
        catalog.GetPackageCalls.ShouldBe(0);
        deployer.DeployCalls.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_re_enqueue_mid_deploy_supersedes_the_completion()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ReadOnlyMemory<byte> package = await AttestedPackage(key);

        var deployments = new InMemoryWorkflowDeploymentStore();
        string id = await Enqueue(deployments, "flow", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);

        // The deploy re-enqueues the same target (a redeploy) while it runs, resetting the claimed deployment to Queued with
        // a new etag; the worker's Deployed completion then no longer matches and is abandoned.
        var deployer = new FakeDeployer(
            ServerlessDeployResult.Success("https://fn.example/invoke", "ok"),
            beforeReturn: async () => await Enqueue(deployments, "flow", 1, "production", "linux-x64"));
        var worker = new WorkflowDeployWorker(deployments, catalog, new WorkflowDeployService(TrustStore(("release-2026", key)), deployer));

        WorkflowDeployWorkerResult result = await worker.DriveNextAsync("deployer-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Claimed.ShouldBeTrue();
        result.WasSuperseded.ShouldBeTrue();
        result.Outcome.ShouldBeNull();

        // The deployment is back to Queued (the fresh re-enqueue), not Deployed — the next claim redeploys it.
        using ParsedJsonDocument<WorkflowDeployment>? deployment = await deployments.GetAsync(id, CancellationToken.None);
        deployment!.RootElement.StatusValue.ShouldBe("Queued");
    }

    [TestMethod]
    public async Task The_hosted_loop_drains_every_queued_deployment()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ReadOnlyMemory<byte> package = await AttestedPackage(key);

        // Three queued deployments for distinct targets (distinct environments, same linux-x64 binary); the loop should
        // deploy all three in one drain, not one per poll interval.
        var deployments = new InMemoryWorkflowDeploymentStore();
        await Enqueue(deployments, "flow", 1, "production", "linux-x64");
        await Enqueue(deployments, "flow", 1, "staging", "linux-x64");
        await Enqueue(deployments, "flow", 1, "dev", "linux-x64");
        var catalog = new FakeCatalog(package);
        var deployer = new FakeDeployer(ServerlessDeployResult.Success("https://fn.example/invoke", "ok"));
        var worker = new WorkflowDeployWorker(deployments, catalog, new WorkflowDeployService(TrustStore(("release-2026", key)), deployer));
        var options = new WorkflowDeployWorkerOptions { WorkerId = "deployer-1", PollInterval = TimeSpan.FromMilliseconds(20) };
        var service = new WorkflowDeployBackgroundService(worker, options, NullLogger<WorkflowDeployBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForDeployedCountAsync(deployments, 3, TimeSpan.FromSeconds(10));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        using PooledDocumentList<WorkflowDeployment> deployed = await deployments.ListAsync(new WorkflowDeploymentQuery(WorkflowDeploymentStatus.Deployed), CancellationToken.None);
        deployed.Count.ShouldBe(3);
        deployer.DeployCalls.ShouldBe(3);
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deployments = new InMemoryWorkflowDeploymentStore();
        var catalog = new FakeCatalog(null);
        var service = new WorkflowDeployService(TrustStore(("release-2026", key)), new FakeDeployer(ServerlessDeployResult.Success("https://x", "ok")));

        Should.Throw<ArgumentNullException>(() => new WorkflowDeployWorker(null!, catalog, service));
        Should.Throw<ArgumentNullException>(() => new WorkflowDeployWorker(deployments, null!, service));
        Should.Throw<ArgumentNullException>(() => new WorkflowDeployWorker(deployments, catalog, null!));
    }

    // Produces a version package carrying an attested, signed native binary for linux-x64 — what the build worker attaches
    // and the deploy service verifies before deploying.
    private static async Task<ReadOnlyMemory<byte>> AttestedPackage(ECDsa key)
    {
        byte[] signed = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));
        var built = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), new FakeBuilder(AotBuildResult.Success(Native, "ok")), Options());
        WorkflowAotBuildOutcome outcome = await built.BuildAndAttachAsync(signed, "linux-x64", CancellationToken.None);
        return outcome.Package;
    }

    private static async Task<string> Enqueue(InMemoryWorkflowDeploymentStore deployments, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier)
    {
        using ParsedJsonDocument<WorkflowDeployment> draft = WorkflowDeployment.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        using ParsedJsonDocument<WorkflowDeployment> enqueued = await deployments.EnqueueAsync(draft.RootElement, "alice", CancellationToken.None);
        return enqueued.RootElement.IdValue;
    }

    private static async Task WaitForDeployedCountAsync(InMemoryWorkflowDeploymentStore deployments, int expected, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using (PooledDocumentList<WorkflowDeployment> deployed = await deployments.ListAsync(new WorkflowDeploymentQuery(WorkflowDeploymentStatus.Deployed), CancellationToken.None))
            {
                if (deployed.Count >= expected)
                {
                    return;
                }
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Fewer than {expected} deployments reached the Deployed state within {timeout}.");
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

    // A trust store holding only the public half of each signing key (what the runner's verifier holds).
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

    // A catalog stand-in that feeds a version's package (or none) and counts the reads. Only GetPackageAsync is exercised by
    // the deploy worker (it never persists the package back); the rest is not part of the worker's contract.
    private sealed class FakeCatalog(ReadOnlyMemory<byte>? package) : IWorkflowCatalogStore
    {
        public int GetPackageCalls { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
        {
            this.GetPackageCalls++;
            return new ValueTask<ReadOnlyMemory<byte>?>(package);
        }

        public ValueTask<bool> UpdatePackageAsync(string baseWorkflowId, int versionNumber, ReadOnlyMemory<byte> updatedPackage, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    // Stands in for the container compile so AttestedPackage can attach a native binary without a real AOT build.
    private sealed class FakeBuilder(AotBuildResult result) : IWorkflowAotBuilder
    {
        public ValueTask<AotBuildResult> BuildAsync(AssembledHostApp hostApp, CancellationToken cancellationToken)
            => new(result);
    }

    // Stands in for the function platform: returns a canned result, optionally running a callback first (to model a
    // concurrent re-enqueue mid-deploy), and counts its invocations.
    private sealed class FakeDeployer(ServerlessDeployResult result, Func<Task>? beforeReturn = null) : IServerlessDeployer
    {
        public int DeployCalls { get; private set; }

        public async ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken)
        {
            this.DeployCalls++;
            if (beforeReturn is not null)
            {
                await beforeReturn().ConfigureAwait(false);
            }

            return result;
        }
    }
}
