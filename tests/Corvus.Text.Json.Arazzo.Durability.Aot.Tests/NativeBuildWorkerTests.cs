// <copyright file="NativeBuildWorkerTests.cs" company="Endjin Limited">
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
/// The build worker's state machine (ADR 0055): it claims the oldest queued job, builds and signs the version's native
/// binary via the build service, persists the attached binary back into the version, and completes the job Ready; a compile
/// failure, a bad executor input, or a vanished version is recorded as a Failed completion (not thrown) so one bad job does
/// not stall the worker; and a re-enqueue (a rebuild) mid-build supersedes the completion, leaving the fresh Queued job for
/// the next claim. A real in-memory job store exercises the claim/complete concurrency; a fake catalog feeds the signed
/// package and records the persist-back; a fake builder stands in for the container compile.
/// </summary>
[TestClass]
public sealed class NativeBuildWorkerTests
{
    // A real managed assembly (the Aot assembly itself) stands in for the signed executor.dll, so the assembler can read
    // its assembly name from valid PE metadata; the fake builder never compiles it.
    private static readonly byte[] ExecutorAssembly = File.ReadAllBytes(typeof(WorkflowAotBuildService).Assembly.Location);

    private static readonly byte[] Workflow = Encoding.UTF8.GetBytes(
        """{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"flow-v1","steps":[]}]}""");

    private static readonly byte[] Native = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3];

    // The content hash of the test version, which the executor manifest's packageHash equals (the executor is built from
    // that exact content). Binding the native attestation to it is what the deploy check enforces.
    private static readonly string PackageHash = CatalogPackage.HashCanonical(WorkflowPackage.Pack(Workflow, []));

    [TestMethod]
    public async Task Builds_signs_persists_and_marks_the_job_ready()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        TrustStoreExecutorPackageVerifier verifier = TrustStore(("release-2026", key));
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        string id = await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);
        var builder = new FakeBuilder(AotBuildResult.Success(Native, "ilc ok"));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(verifier, Signer(key), builder, Options()));

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Claimed.ShouldBeTrue();
        result.WasSuperseded.ShouldBeFalse();
        result.Outcome.ShouldBe(NativeBuildJobStatus.Ready);
        result.FailureReason.ShouldBeNull();

        // The job is Ready in the store.
        using ParsedJsonDocument<NativeBuildJob>? job = await jobs.GetAsync(id, CancellationToken.None);
        job!.RootElement.StatusValue.ShouldBe("Ready");
        job.RootElement.ClaimedByOrNull.ShouldBe("worker-1");
        job.RootElement.CompletedAtValue.ShouldNotBeNull();

        // The worker persisted the version's package back once, and the persisted package carries the signed native binary
        // that a deploy verifies against the trust store (the worker built from the signed IL and signed the output).
        catalog.UpdateCalls.ShouldBe(1);
        catalog.Persisted.ShouldNotBeNull();
        WorkflowPackage.TryReadNativeArtifact(catalog.Persisted!.Value, "linux-x64", out ReadOnlyMemory<byte> attached).ShouldBeTrue();
        attached.ToArray().ShouldBe(Native);
        NativeArtifactAttestation attestation = WorkflowAotBuildService.VerifyNativeArtifact(catalog.Persisted.Value, "linux-x64", verifier);
        attestation.NativeDigest.ShouldBe(NativeArtifactAttestation.ComputeDigest(Native));
    }

    [TestMethod]
    public async Task A_ready_build_enqueues_a_deployment_for_the_target()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);
        var builder = new FakeBuilder(AotBuildResult.Success(Native, "ilc ok"));
        var deployments = new InMemoryWorkflowDeploymentStore();
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()), deployments);

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(NativeBuildJobStatus.Ready);

        // Deploy-on-build (ADR 0059): a Queued deployment now exists for the same target tuple, awaiting the runner's deploy
        // worker. The query is scoped to (Queued, checkout, 1, production, linux-x64), so a count of one proves it is that
        // exact target's deployment.
        using PooledDocumentList<WorkflowDeployment> pending = await deployments.ListAsync(
            new WorkflowDeploymentQuery(WorkflowDeploymentStatus.Queued, "checkout", 1, "production", "linux-x64"), CancellationToken.None);
        pending.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_failed_build_enqueues_no_deployment()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);
        var builder = new FakeBuilder(AotBuildResult.Failure("error IL3050: reflection is not AOT-safe"));
        var deployments = new InMemoryWorkflowDeploymentStore();
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()), deployments);

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(NativeBuildJobStatus.Failed);

        // A build that never reached Ready deploys nothing: no deployment (in any state) exists for the target.
        using PooledDocumentList<WorkflowDeployment> any = await deployments.ListAsync(
            new WorkflowDeploymentQuery(null, "checkout", 1, "production", "linux-x64"), CancellationToken.None);
        any.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_superseded_build_enqueues_no_deployment()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);

        // The build re-enqueues the same target mid-build (a rebuild), so this cycle's completion is abandoned as superseded;
        // a superseded cycle must not enqueue a deployment (the fresh rebuild will, when it reaches Ready).
        var builder = new FakeBuilder(
            AotBuildResult.Success(Native, "ok"),
            beforeReturn: async () => await Enqueue(jobs, "checkout", 1, "production", "linux-x64"));
        var deployments = new InMemoryWorkflowDeploymentStore();
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()), deployments);

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.WasSuperseded.ShouldBeTrue();

        using PooledDocumentList<WorkflowDeployment> any = await deployments.ListAsync(
            new WorkflowDeploymentQuery(null, "checkout", 1, "production", "linux-x64"), CancellationToken.None);
        any.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task Returns_idle_when_no_job_is_queued()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jobs = new InMemoryNativeBuildJobStore();
        var catalog = new FakeCatalog(await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly))));
        var builder = new FakeBuilder(AotBuildResult.Success(Native, "ok"));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Claimed.ShouldBeFalse();
        result.Outcome.ShouldBeNull();
        catalog.GetPackageCalls.ShouldBe(0);
        builder.BuildCalls.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_native_AOT_compile_failure_marks_the_job_failed_with_the_log()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        string id = await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);
        var builder = new FakeBuilder(AotBuildResult.Failure("error IL3050: reflection is not AOT-safe"));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(NativeBuildJobStatus.Failed);
        result.FailureReason.ShouldNotBeNull().ShouldContain("IL3050");

        using ParsedJsonDocument<NativeBuildJob>? job = await jobs.GetAsync(id, CancellationToken.None);
        job!.RootElement.StatusValue.ShouldBe("Failed");
        job.RootElement.FailureReasonOrNull.ShouldNotBeNull().ShouldContain("IL3050");

        // A failed build is not persisted back into the version.
        catalog.UpdateCalls.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_bad_executor_input_marks_the_job_failed()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // A package whose executor is unsigned: the build service refuses it (WorkflowAotBuildException), which the worker
        // records as a Failed completion rather than letting it escape and orphan the job in Building.
        byte[] unsigned = WorkflowPackage.Pack(
            Workflow, [], executor: ExecutorAssembly, executorManifest: Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        string id = await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(unsigned);
        var builder = new FakeBuilder(AotBuildResult.Success(Native, "ok"));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(NativeBuildJobStatus.Failed);
        result.FailureReason.ShouldNotBeNull().ShouldContain("unsigned");

        using ParsedJsonDocument<NativeBuildJob>? job = await jobs.GetAsync(id, CancellationToken.None);
        job!.RootElement.StatusValue.ShouldBe("Failed");
        builder.BuildCalls.ShouldBe(0);
        catalog.UpdateCalls.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_vanished_version_marks_the_job_failed()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jobs = new InMemoryNativeBuildJobStore();
        string id = await Enqueue(jobs, "checkout", 1, "production", "linux-x64");

        // The version was deleted between enqueue and claim: the catalog has no package.
        var catalog = new FakeCatalog(package: null);
        var builder = new FakeBuilder(AotBuildResult.Success(Native, "ok"));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Outcome.ShouldBe(NativeBuildJobStatus.Failed);
        result.FailureReason.ShouldNotBeNull().ShouldContain("no longer exists");

        using ParsedJsonDocument<NativeBuildJob>? job = await jobs.GetAsync(id, CancellationToken.None);
        job!.RootElement.StatusValue.ShouldBe("Failed");
        builder.BuildCalls.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_re_enqueue_mid_build_supersedes_the_completion()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        string id = await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);

        // The build re-enqueues the same target (a rebuild) while it runs, resetting the claimed job to Queued with a new
        // etag; the worker's Ready completion then no longer matches and is abandoned.
        var builder = new FakeBuilder(
            AotBuildResult.Success(Native, "ok"),
            beforeReturn: async () => await Enqueue(jobs, "checkout", 1, "production", "linux-x64"));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), CancellationToken.None);

        result.Claimed.ShouldBeTrue();
        result.WasSuperseded.ShouldBeTrue();
        result.Outcome.ShouldBeNull();

        // The job is back to Queued (the fresh re-enqueue), not Ready — the next claim rebuilds it.
        using ParsedJsonDocument<NativeBuildJob>? job = await jobs.GetAsync(id, CancellationToken.None);
        job!.RootElement.StatusValue.ShouldBe("Queued");
    }

    [TestMethod]
    public async Task A_long_build_renews_its_lease_and_completes_with_the_renewed_etag()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var jobs = new InMemoryNativeBuildJobStore();
        string id = await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        var catalog = new FakeCatalog(package);

        // The build blocks until the lease heartbeat has renewed at least once (the job's etag advances past the claim
        // etag). A renewal bumps the etag, so completing with the stale claim etag would conflict — the worker must thread
        // the renewed etag forward. A Ready outcome (not Superseded) is the proof it did.
        var builder = new FakeBuilder(
            AotBuildResult.Success(Native, "ok"),
            beforeReturn: () => WaitForEtagToAdvanceAsync(jobs, id, TimeSpan.FromSeconds(10)));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));

        // A short renewal interval so a renewal fires well within the blocked build; the lease TTL stays generous.
        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(10), CancellationToken.None);

        result.WasSuperseded.ShouldBeFalse();
        result.Outcome.ShouldBe(NativeBuildJobStatus.Ready);

        using ParsedJsonDocument<NativeBuildJob>? job = await jobs.GetAsync(id, CancellationToken.None);
        job!.RootElement.StatusValue.ShouldBe("Ready");
    }

    [TestMethod]
    public async Task A_lease_reclaimed_mid_build_is_abandoned_as_superseded()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        var inner = new InMemoryNativeBuildJobStore();
        string id = await Enqueue(inner, "checkout", 1, "production", "linux-x64");
        var renewalAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The store fails every lease renewal with a conflict, modelling another worker reclaiming the (expired) lease. The
        // build blocks until the heartbeat has attempted its renewal, so the worker observes the lost lease and abandons its
        // completion as superseded rather than completing a build another worker now owns.
        var jobs = new ReclaimOnRenewStore(inner, renewalAttempted);
        var catalog = new FakeCatalog(package);
        var builder = new FakeBuilder(AotBuildResult.Success(Native, "ok"), beforeReturn: () => renewalAttempted.Task);
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));

        NativeBuildWorkerResult result = await worker.DriveNextAsync("worker-1", TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(10), CancellationToken.None);

        result.Claimed.ShouldBeTrue();
        result.WasSuperseded.ShouldBeTrue();
        result.Outcome.ShouldBeNull();

        // The superseded worker never completed the job, so it is left Building for the reclaiming worker.
        using ParsedJsonDocument<NativeBuildJob>? job = await inner.GetAsync(id, CancellationToken.None);
        job!.RootElement.StatusValue.ShouldBe("Building");
    }

    [TestMethod]
    public async Task The_hosted_loop_drains_every_queued_job()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] package = await SignedPackage(key, "release-2026", Manifest(Digest(ExecutorAssembly)));

        // Three queued jobs for distinct targets; the loop should build all three in one drain, not one per poll interval.
        var jobs = new InMemoryNativeBuildJobStore();
        await Enqueue(jobs, "checkout", 1, "production", "linux-x64");
        await Enqueue(jobs, "checkout", 1, "production", "linux-arm64");
        await Enqueue(jobs, "checkout", 1, "production", "win-x64");
        var catalog = new FakeCatalog(package);
        var builder = new FakeBuilder(AotBuildResult.Success(Native, "ok"));
        var worker = new NativeBuildWorker(jobs, catalog, new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), builder, Options()));
        var options = new NativeBuildWorkerOptions { WorkerId = "worker-1", PollInterval = TimeSpan.FromMilliseconds(20) };
        var service = new NativeBuildBackgroundService(worker, options, NullLogger<NativeBuildBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForReadyCountAsync(jobs, 3, TimeSpan.FromSeconds(10));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        using PooledDocumentList<NativeBuildJob> ready = await jobs.ListAsync(new NativeBuildJobQuery(NativeBuildJobStatus.Ready), CancellationToken.None);
        ready.Count.ShouldBe(3);
        builder.BuildCalls.ShouldBe(3);
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jobs = new InMemoryNativeBuildJobStore();
        var catalog = new FakeCatalog(null);
        var service = new WorkflowAotBuildService(TrustStore(("release-2026", key)), Signer(key), new FakeBuilder(AotBuildResult.Success(Native, "ok")), Options());

        Should.Throw<ArgumentNullException>(() => new NativeBuildWorker(null!, catalog, service));
        Should.Throw<ArgumentNullException>(() => new NativeBuildWorker(jobs, null!, service));
        Should.Throw<ArgumentNullException>(() => new NativeBuildWorker(jobs, catalog, null!));
    }

    private static async Task<string> Enqueue(InMemoryNativeBuildJobStore jobs, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier)
    {
        using ParsedJsonDocument<NativeBuildJob> draft = NativeBuildJob.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        using ParsedJsonDocument<NativeBuildJob> enqueued = await jobs.EnqueueAsync(draft.RootElement, "alice", CancellationToken.None);
        return enqueued.RootElement.IdValue;
    }

    private static async Task WaitForReadyCountAsync(InMemoryNativeBuildJobStore jobs, int expected, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using (PooledDocumentList<NativeBuildJob> ready = await jobs.ListAsync(new NativeBuildJobQuery(NativeBuildJobStatus.Ready), CancellationToken.None))
            {
                if (ready.Count >= expected)
                {
                    return;
                }
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Fewer than {expected} jobs reached the Ready state within {timeout}.");
    }

    // Waits until the job's etag advances past whatever it is on entry — the observable effect of a lease renewal (each
    // renewal bumps the etag). Polls rather than sleeping a fixed duration so it is robust to timer jitter.
    private static async Task WaitForEtagToAdvanceAsync(InMemoryNativeBuildJobStore jobs, string id, TimeSpan timeout)
    {
        string baseline;
        using (ParsedJsonDocument<NativeBuildJob>? doc = await jobs.GetAsync(id, CancellationToken.None))
        {
            baseline = doc!.RootElement.EtagValue.Value!;
        }

        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using (ParsedJsonDocument<NativeBuildJob>? doc = await jobs.GetAsync(id, CancellationToken.None))
            {
                if (doc!.RootElement.EtagValue.Value != baseline)
                {
                    return;
                }
            }

            await Task.Delay(5);
        }

        throw new TimeoutException($"The lease was not renewed (the etag did not advance) within {timeout}.");
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

    // A trust store holding only the public half of each signing key (what the control plane's own verifier holds).
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

    // A catalog stand-in that feeds a version's package (or none) and records the metadata-only persist-back the worker
    // makes. Only GetPackageAsync and UpdatePackageAsync are exercised; the rest is not part of the worker's contract.
    private sealed class FakeCatalog(ReadOnlyMemory<byte>? package) : IWorkflowCatalogStore
    {
        public int GetPackageCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public ReadOnlyMemory<byte>? Persisted { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
        {
            this.GetPackageCalls++;
            return new ValueTask<ReadOnlyMemory<byte>?>(package);
        }

        public ValueTask<bool> UpdatePackageAsync(string baseWorkflowId, int versionNumber, ReadOnlyMemory<byte> updatedPackage, CancellationToken cancellationToken)
        {
            this.UpdateCalls++;
            this.Persisted = updatedPackage;
            return new ValueTask<bool>(package is not null);
        }

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

    // Stands in for the container compile: returns a canned result, optionally running a callback first (to model a
    // concurrent re-enqueue mid-build), and counts its invocations.
    private sealed class FakeBuilder(AotBuildResult result, Func<Task>? beforeReturn = null) : IWorkflowAotBuilder
    {
        public int BuildCalls { get; private set; }

        public async ValueTask<AotBuildResult> BuildAsync(AssembledHostApp hostApp, CancellationToken cancellationToken)
        {
            this.BuildCalls++;
            if (beforeReturn is not null)
            {
                await beforeReturn().ConfigureAwait(false);
            }

            return result;
        }
    }

    // Wraps a real store but fails every lease renewal with a conflict, modelling another worker having reclaimed the
    // (expired) lease. Signals when the first renewal is attempted so a test can hold a build in flight until the worker has
    // observed the lost lease. Every other operation delegates to the inner store.
    private sealed class ReclaimOnRenewStore(INativeBuildJobStore inner, TaskCompletionSource renewalAttempted) : INativeBuildJobStore
    {
        public ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
            => inner.EnqueueAsync(draft, actor, cancellationToken);

        public ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
            => inner.GetAsync(id, cancellationToken);

        public ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
            => inner.ClaimNextQueuedAsync(claimedBy, leaseTtl, cancellationToken);

        public ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
            => inner.CompleteAsync(id, completion, expectedEtag, cancellationToken);

        public ValueTask<ParsedJsonDocument<NativeBuildJob>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
        {
            renewalAttempted.TrySetResult();
            throw new NativeBuildJobConflictException(id, expectedEtag); // the lease was reclaimed
        }

        public ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
            => inner.ListAsync(query, cancellationToken);
    }
}