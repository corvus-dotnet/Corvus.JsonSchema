// <copyright file="DraftRunTraceStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IDraftRunTraceStore"/> must satisfy, regardless of backend: a §18 debug
/// run's assembled metadata trace round-trips verbatim keyed by run id, a put overwrites the prior trace, a delete
/// removes it (and reports whether it existed), and a missing trace reads as absent. A backend's test project
/// derives a concrete <see cref="TestClassAttribute"/> from this and implements <see cref="CreateStoreAsync"/>; the
/// in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class DraftRunTraceStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IDraftRunTraceStore> CreateStoreAsync();

    /// <summary>Disposes any stores created during the test.</summary>
    /// <returns>A task that completes when cleanup is done.</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (IAsyncDisposable disposable in this.disposables)
        {
            await disposable.DisposeAsync();
        }

        this.disposables.Clear();
    }

    [TestMethod]
    public async Task Put_then_Get_round_trips_the_trace_bytes()
    {
        IDraftRunTraceStore store = await this.NewStoreAsync();
        byte[] trace = Trace("paused");
        await store.PutAsync("run-1", trace, default);

        ReadOnlyMemory<byte>? loaded = await store.GetAsync("run-1", default);
        loaded.ShouldNotBeNull();
        loaded.Value.ToArray().ShouldBe(trace);
    }

    [TestMethod]
    public async Task Get_of_unknown_run_returns_null()
    {
        IDraftRunTraceStore store = await this.NewStoreAsync();
        (await store.GetAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Put_with_same_id_overwrites_the_trace()
    {
        IDraftRunTraceStore store = await this.NewStoreAsync();
        await store.PutAsync("run-1", Trace("paused"), default);
        await store.PutAsync("run-1", Trace("completed"), default);

        ReadOnlyMemory<byte>? loaded = await store.GetAsync("run-1", default);
        loaded.ShouldNotBeNull();
        loaded.Value.ToArray().ShouldBe(Trace("completed"));
    }

    [TestMethod]
    public async Task Traces_are_kept_per_run()
    {
        IDraftRunTraceStore store = await this.NewStoreAsync();
        await store.PutAsync("run-1", Trace("paused"), default);
        await store.PutAsync("run-2", Trace("completed"), default);

        (await store.GetAsync("run-1", default))!.Value.ToArray().ShouldBe(Trace("paused"));
        (await store.GetAsync("run-2", default))!.Value.ToArray().ShouldBe(Trace("completed"));
    }

    [TestMethod]
    public async Task Delete_removes_the_trace_and_reports_whether_it_existed()
    {
        IDraftRunTraceStore store = await this.NewStoreAsync();
        await store.PutAsync("run-1", Trace("paused"), default);

        (await store.DeleteAsync("run-1", default)).ShouldBeTrue();
        (await store.GetAsync("run-1", default)).ShouldBeNull();
        (await store.DeleteAsync("run-1", default)).ShouldBeFalse();
        (await store.DeleteAsync("never-existed", default)).ShouldBeFalse();
    }

    // A representative SimulationTrace-shaped metadata trace (the store treats it as an opaque UTF-8 blob, but
    // conformance uses the honest shape the runner assembles: an outcome + one completed step + one metadata-only request).
    private static byte[] Trace(string outcome)
        => Encoding.UTF8.GetBytes(
            $$"""{"outcome":"{{outcome}}","steps":[{"stepId":"place-order","status":"completed","attempt":0,"requests":[{"method":"post","path":"/orders","status":201}]}],"stepsExecuted":1}""");

    private async ValueTask<IDraftRunTraceStore> NewStoreAsync()
    {
        IDraftRunTraceStore store = await this.CreateStoreAsync();
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}