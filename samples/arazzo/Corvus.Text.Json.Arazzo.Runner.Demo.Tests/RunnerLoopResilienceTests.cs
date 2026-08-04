// <copyright file="RunnerLoopResilienceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client;
using Corvus.Text.Json.OpenApi;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using VaultSharp;

namespace Corvus.Text.Json.Arazzo.Runner.Demo.Tests;

/// <summary>
/// The resilience contract shared by every runner background loop: a transient fault inside one iteration is
/// logged and the next tick retries — it never faults <c>ExecuteTask</c>, because the host's default
/// BackgroundServiceExceptionBehavior is StopHost and a faulted loop terminates the whole runner (the live
/// failure that took down the runner AND the system-runner on one Npgsql read timeout).
/// </summary>
[TestClass]
public sealed class RunnerLoopResilienceTests
{
    [TestMethod]
    public async Task Dispatch_loop_survives_runner_api_faults_and_keeps_polling()
    {
        var transport = new ThrowingApiTransport();
        using var service = new WorkflowDispatchService(
            new ArazzoRunnerClient(transport),
            resumer: null!, // never reached: listing the hosted versions faults first, and the loop must absorb that
            new RunnerOptions("runner-under-test", "development"),
            NullLogger<WorkflowDispatchService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // The poll interval is 2s and the first cycle runs immediately: two observed requests prove the
        // loop survived the first fault and came back for another cycle.
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (transport.Requests < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Task executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsFaulted.ShouldBeFalse($"a transient runner-API fault must not fault (and so stop) the runner host: {executeTask.Exception}");
        transport.Requests.ShouldBeGreaterThanOrEqualTo(2, "the dispatch loop must keep polling through transient runner-API faults");

        await service.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task Vault_refresh_loop_survives_client_faults_and_keeps_ticking()
    {
        var vault = new ThrowingVault();
        using var service = new VaultTokenLifecycleService(vault, NullLogger<VaultTokenLifecycleService>.Instance, refreshInterval: TimeSpan.FromMilliseconds(10));

        await service.StartAsync(CancellationToken.None);

        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (vault.Accesses < 3 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        vault.Accesses.ShouldBeGreaterThanOrEqualTo(3, "the refresh loop must keep ticking through vault faults");
        Task executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsFaulted.ShouldBeFalse("a vault fault must not fault (and so stop) the runner host");

        await service.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task Draft_run_pump_survives_runner_faults_and_keeps_polling()
    {
        // A null runner makes every RunPendingAsync throw — the pump must absorb each fault and keep polling.
        using var service = new DraftRunPumpService(runner: null!, NullLogger<DraftRunPumpService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(700); // several 200ms poll cycles, each faulting

        Task executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsFaulted.ShouldBeFalse("a pump fault must not fault (and so stop) the runner host");

        await service.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// A runner-API transport whose every request fails the way a control plane briefly out of reach does. The runner
    /// now reaches the store only through that API, so this is where a transient infrastructure fault arrives, and the
    /// loop has to absorb it exactly as it absorbed a store read timing out.
    /// </summary>
    private sealed class ThrowingApiTransport : IApiTransport
    {
        private int requests;

        public int Requests => this.requests;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private ValueTask<TResponse> Fail<TResponse>()
            where TResponse : struct, IApiResponse<TResponse>
        {
            Interlocked.Increment(ref this.requests);
            throw new TimeoutException("Timeout during reading attempt");
        }
    }

    private sealed class ThrowingVault : IVaultClient
    {
        private int accesses;

        public int Accesses => this.accesses;

        public VaultSharp.V1.IVaultClientV1 V1
        {
            get
            {
                this.accesses++;
                throw new TimeoutException("Timeout during reading attempt");
            }
        }

        public VaultClientSettings Settings => throw new NotSupportedException();
    }
}
