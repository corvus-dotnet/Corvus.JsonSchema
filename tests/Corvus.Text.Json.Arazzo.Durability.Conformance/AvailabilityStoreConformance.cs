// <copyright file="AvailabilityStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IAvailabilityStore"/> must satisfy (design §7.8): the availability matrix keyed
/// by (baseWorkflowId, versionNumber, environment), idempotent make-available, withdraw, additive many-to-many isolation,
/// and the two keyset list axes (environments a version is available in, ordered by environment; versions available in an
/// environment, ordered by base workflow id then version number). A backend's test project derives a concrete
/// <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>; the in-memory store is the reference
/// implementation and runs the same suite.
/// </summary>
public abstract class AvailabilityStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IAvailabilityStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task A_version_is_made_available_and_round_trips_on_both_axes()
    {
        IAvailabilityStore store = await this.NewStoreAsync();

        (ParsedJsonDocument<AvailabilityEntry> entry, bool created) = await store.MakeAvailableAsync("checkout", 3, "production", "alice", default);
        using (entry)
        {
            created.ShouldBeTrue();
            entry.RootElement.BaseWorkflowIdValue.ShouldBe("checkout");
            entry.RootElement.VersionNumberValue.ShouldBe(3);
            entry.RootElement.EnvironmentValue.ShouldBe("production");
            entry.RootElement.CreatedByValue.ShouldBe("alice");
            entry.RootElement.EtagValue.IsNone.ShouldBeFalse();
        }

        using (ParsedJsonDocument<AvailabilityEntry>? got = await store.GetAsync("checkout", 3, "production", default))
        {
            got.ShouldNotBeNull();
            got!.RootElement.EnvironmentValue.ShouldBe("production");
        }

        using (AvailabilityPage byVersion = await store.ListByVersionAsync("checkout", 3, 1000, default, default))
        {
            byVersion.Entries.Select(e => e.EnvironmentValue).ShouldBe(["production"]);
        }

        using (AvailabilityPage byEnv = await store.ListByEnvironmentAsync("production", 1000, default, default))
        {
            byEnv.Entries.Select(e => (e.BaseWorkflowIdValue, e.VersionNumberValue)).ShouldBe([("checkout", 3)]);
        }

        (await store.GetAsync("checkout", 3, "staging", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Making_available_is_idempotent()
    {
        IAvailabilityStore store = await this.NewStoreAsync();

        WorkflowEtag firstEtag;
        (ParsedJsonDocument<AvailabilityEntry> first, bool createdFirst) = await store.MakeAvailableAsync("checkout", 1, "production", "alice", default);
        using (first)
        {
            createdFirst.ShouldBeTrue();
            firstEtag = first.RootElement.EtagValue;
        }

        (ParsedJsonDocument<AvailabilityEntry> second, bool createdSecond) = await store.MakeAvailableAsync("checkout", 1, "production", "bob", default);
        using (second)
        {
            createdSecond.ShouldBeFalse();                           // already available
            second.RootElement.CreatedByValue.ShouldBe("alice");    // the original entry, unchanged
            second.RootElement.EtagValue.ShouldBe(firstEtag);
        }

        using AvailabilityPage page = await store.ListByVersionAsync("checkout", 1, 1000, default, default);
        page.Entries.Count.ShouldBe(1); // no duplicate
    }

    [TestMethod]
    public async Task Withdrawing_removes_availability()
    {
        IAvailabilityStore store = await this.NewStoreAsync();
        await this.MakeAsync(store, "checkout", 1, "production");

        (await store.WithdrawAsync("checkout", 1, "production", default)).ShouldBeTrue();
        (await store.GetAsync("checkout", 1, "production", default)).ShouldBeNull();
        (await store.WithdrawAsync("checkout", 1, "production", default)).ShouldBeFalse(); // already gone
    }

    [TestMethod]
    public async Task Availability_is_additive_and_isolated()
    {
        IAvailabilityStore store = await this.NewStoreAsync();
        await this.MakeAsync(store, "checkout", 1, "production");
        await this.MakeAsync(store, "checkout", 2, "production"); // a second version live alongside V1
        await this.MakeAsync(store, "checkout", 1, "staging");    // V1 also available elsewhere
        await this.MakeAsync(store, "billing", 1, "production");  // a different workflow

        // Withdrawing V1 from production leaves V2/production and V1/staging untouched (additive, not a supersede).
        (await store.WithdrawAsync("checkout", 1, "production", default)).ShouldBeTrue();
        (await store.GetAsync("checkout", 2, "production", default)).ShouldNotBeNull();
        (await store.GetAsync("checkout", 1, "staging", default)).ShouldNotBeNull();

        using AvailabilityPage prod = await store.ListByEnvironmentAsync("production", 1000, default, default);
        prod.Entries.Select(e => (e.BaseWorkflowIdValue, e.VersionNumberValue)).ShouldBe([("billing", 1), ("checkout", 2)]);
    }

    [TestMethod]
    public async Task List_by_version_is_ordered_by_environment()
    {
        IAvailabilityStore store = await this.NewStoreAsync();
        await this.MakeAsync(store, "checkout", 1, "zeta");
        await this.MakeAsync(store, "checkout", 1, "alpha");
        await this.MakeAsync(store, "checkout", 1, "mid");

        using AvailabilityPage page = await store.ListByVersionAsync("checkout", 1, 1000, default, default);
        page.Entries.Select(e => e.EnvironmentValue).ShouldBe(["alpha", "mid", "zeta"]);
    }

    [TestMethod]
    public async Task List_by_environment_is_ordered_by_workflow_then_numeric_version()
    {
        IAvailabilityStore store = await this.NewStoreAsync();
        await this.MakeAsync(store, "beta", 2, "production");
        await this.MakeAsync(store, "alpha", 1, "production");
        await this.MakeAsync(store, "alpha", 10, "production");
        await this.MakeAsync(store, "alpha", 2, "production");

        // Version order is numeric (2 before 10), not lexicographic.
        using AvailabilityPage page = await store.ListByEnvironmentAsync("production", 1000, default, default);
        page.Entries.Select(e => (e.BaseWorkflowIdValue, e.VersionNumberValue))
            .ShouldBe([("alpha", 1), ("alpha", 2), ("alpha", 10), ("beta", 2)]);
    }

    [TestMethod]
    public async Task List_by_version_keyset_pages_without_gaps_or_duplicates()
    {
        IAvailabilityStore store = await this.NewStoreAsync();
        string[] environments = ["prod", "staging", "qa", "dev", "alpha", "zeta", "mid"];
        foreach (string environment in environments.OrderDescending(StringComparer.Ordinal))
        {
            await this.MakeAsync(store, "checkout", 1, environment);
        }

        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using AvailabilityPage page = await store.ListByVersionAsync("checkout", 1, 3, tokenDoc?.RootElement ?? default, default);
            page.Entries.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (AvailabilityEntry e in page.Entries)
            {
                seen.Add(e.EnvironmentValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        pages.ShouldBe(3); // 7 entries, 3 per page
        seen.ShouldBe(environments.OrderBy(e => e, StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public async Task List_by_environment_keyset_pages_without_gaps_or_duplicates()
    {
        IAvailabilityStore store = await this.NewStoreAsync();
        (string Workflow, int Version)[] pairs =
        [
            ("checkout", 1), ("checkout", 2), ("checkout", 10), ("billing", 1), ("billing", 3), ("audit", 1), ("audit", 2),
        ];
        foreach ((string workflow, int version) in pairs.OrderByDescending(p => p.Version))
        {
            await this.MakeAsync(store, workflow, version, "production");
        }

        var seen = new List<(string, int)>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using AvailabilityPage page = await store.ListByEnvironmentAsync("production", 3, tokenDoc?.RootElement ?? default, default);
            page.Entries.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (AvailabilityEntry e in page.Entries)
            {
                seen.Add((e.BaseWorkflowIdValue, e.VersionNumberValue));
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        pages.ShouldBe(3); // 7 entries, 3 per page
        seen.ShouldBe([("audit", 1), ("audit", 2), ("billing", 1), ("billing", 3), ("checkout", 1), ("checkout", 2), ("checkout", 10)]);
    }

    // Wraps an opaque page token's UTF-8 as the JSON string value a request carries it as (mirroring HTTP).
    private static ParsedJsonDocument<JsonString> AsPageToken(ReadOnlySpan<byte> tokenUtf8)
    {
        byte[] quoted = new byte[tokenUtf8.Length + 2];
        quoted[0] = (byte)'"';
        tokenUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<JsonString>.Parse(quoted);
    }

    // Makes a version available and disposes the returned pooled document.
    private async Task MakeAsync(IAvailabilityStore store, string baseWorkflowId, int versionNumber, string environment)
    {
        (ParsedJsonDocument<AvailabilityEntry> entry, _) = await store.MakeAvailableAsync(baseWorkflowId, versionNumber, environment, "system", default);
        entry.Dispose();
    }

    private async ValueTask<IAvailabilityStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IAvailabilityStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}